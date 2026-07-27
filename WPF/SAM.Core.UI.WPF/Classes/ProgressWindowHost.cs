// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// Hosts a <see cref="ProgressWindow"/> on a dedicated UI thread running its own dispatcher loop, so the
    /// dialog keeps painting and its Cancel button keeps accepting clicks while the CALLING thread is blocked
    /// inside a long, uninterruptible call — a TAS COM simulate, say.
    /// <para>
    /// This exists because a Cancel button on the blocked thread's own window cannot be relied on. Windows
    /// ghosts a top-level window whose thread has not pumped for a few seconds (the "Not Responding" overlay)
    /// and silently discards clicks on the ghost, so the click is never queued and the later dispatcher pump
    /// has nothing to deliver. The user clicks Cancel, sees nothing happen, and the run continues. Moving only
    /// the dialog to its own thread fixes that without moving any COM work.
    /// </para>
    /// <para>
    /// The job itself stays on the caller's thread and is NOT interrupted — cancellation is still cooperative
    /// and observed between steps. What changes is that the request is always recorded the instant it is made.
    /// </para>
    /// <para>
    /// The WPF counterpart of <c>SAM.Core.Windows.Forms.ProgressFormHost</c>, deliberately a separate class
    /// rather than a reuse of it: this assembly holds no reference to SAM.Core.Windows, and the two differ
    /// where the frameworks do — a dispatcher loop has to be shut down explicitly, where
    /// <c>Application.Run</c> ends on its own when the form closes.
    /// </para>
    /// <para>
    /// Only the members on this class may be touched from the calling thread; they marshal onto the dialog's
    /// thread. <see cref="Dispose"/> closes the window and joins the thread.
    /// </para>
    /// </summary>
    public sealed class ProgressWindowHost : IDisposable
    {
        private readonly Thread thread;
        private volatile ProgressWindow progressWindow;

        /// <summary>
        /// The dialog thread's dispatcher, published as early as that thread can publish it — before the
        /// window is built — so <see cref="Dispose"/> can always shut the loop down, including when
        /// construction of the window itself failed and there is no window to close.
        /// </summary>
        private volatile Dispatcher dispatcher;

        /// <summary>
        /// Volatile because the dialog thread reads it while starting up. The constructor's wait is bounded,
        /// so a caller can be handed the host — and finish the job and dispose it — before this thread has
        /// shown a window. Without this the thread would go on to open a topmost dialog that nothing is left
        /// to close, leaving it stranded over the host application for the rest of the session.
        /// </summary>
        private volatile bool disposed;

        /// <summary>
        /// Whatever killed the dialog thread, if anything. Rethrown by the constructor when it happened during
        /// startup; if it happened later, inside the dispatcher loop, the constructor has already returned and
        /// it is surfaced through <see cref="Exception"/> instead. Either way it is never allowed to escape
        /// the thread itself.
        /// </summary>
        private volatile Exception exception_Startup;

        /// <summary>
        /// Signalled when the dialog is up, and again when its thread exits. A field rather than a local
        /// disposed by the constructor: the dialog thread still signals it after the dispatcher loop returns,
        /// and setting a disposed <see cref="ManualResetEventSlim"/> throws on a background thread with no
        /// catch above it, which takes the whole host process down. Disposed in <see cref="Dispose"/>, after
        /// the thread has been joined.
        /// </summary>
        private readonly ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);

        /// <summary>
        /// Guards <see cref="cancelRequested"/> and <see cref="cancelRequested_Latched"/> together, so a
        /// subscription and a cancel arriving at the same moment cannot interleave into "latched, but the
        /// handler that was being added never heard about it".
        /// </summary>
        private readonly object cancelRequested_Lock = new object();

        /// <summary>
        /// True once the user has asked to cancel, whether or not anyone was listening at the time.
        /// </summary>
        private bool cancelRequested_Latched;

        private System.EventHandler cancelRequested;

        /// <summary>
        /// Raised on the dialog's thread when the user clicks Cancel, so a handler must be safe to call from a
        /// thread other than the one running the job. Cancelling a <c>CancellationTokenSource</c> is.
        /// <para>
        /// Latching rather than a plain field-like event, because the window is clickable before the caller
        /// can subscribe: the constructor returns as soon as the dialog is up, and only then does the caller
        /// get to attach its handler. A click landing in that gap would invoke a null handler list and be
        /// thrown away — the dialog would record the cancellation and nothing would act on it. Subscribing
        /// after the fact therefore fires immediately if a cancel has already been recorded.
        /// </para>
        /// <para>
        /// Handlers must be safe to run on the subscribing thread as well as the dialog's, since that
        /// catch-up call happens inline on whichever thread subscribes.
        /// </para>
        /// </summary>
        public event System.EventHandler CancelRequested
        {
            add
            {
                bool raiseNow;

                lock (cancelRequested_Lock)
                {
                    cancelRequested += value;
                    raiseNow = cancelRequested_Latched;
                }

                // Outside the lock: a handler cancelling a CancellationTokenSource runs its callbacks inline,
                // and those are caller code that must never run under a lock of ours.
                if (raiseNow)
                {
                    value?.Invoke(this, EventArgs.Empty);
                }
            }

            remove
            {
                lock (cancelRequested_Lock)
                {
                    cancelRequested -= value;
                }
            }
        }

        /// <summary>
        /// Records the cancellation and notifies whoever is listening, at most once. Idempotent so the
        /// safety-net call in <see cref="Dispose"/> cannot double-raise a cancel the dialog already forwarded.
        /// </summary>
        private void RaiseCancelRequested()
        {
            System.EventHandler cancelRequested_Temp;

            lock (cancelRequested_Lock)
            {
                if (cancelRequested_Latched)
                {
                    return;
                }

                cancelRequested_Latched = true;
                cancelRequested_Temp = cancelRequested;
            }

            cancelRequested_Temp?.Invoke(this, EventArgs.Empty);
        }

        /// <param name="name">Window title.</param>
        /// <param name="max">Number of steps the progress bar counts to.</param>
        /// <param name="cancellable">Shows the Cancel button and the note line.</param>
        /// <param name="note">Initial note text; see <see cref="ProgressWindow.Note"/>.</param>
        public ProgressWindowHost(string name, int max, bool cancellable, string note)
        {
            thread = new Thread(() =>
            {
                ProgressWindow progressWindow_Temp = null;

                // Everything on this thread is inside the try, construction included: an exception escaping a
                // raw thread terminates the process under the default .NET policy, and a progress dialog must
                // never be able to take the host application down. Constructing the window can throw on its
                // own (a negative max, for one), which happens before any of the code below runs.
                try
                {
                    // First, so Dispose has something to shut down no matter how far the rest gets.
                    dispatcher = Dispatcher.CurrentDispatcher;

                    progressWindow_Temp = new ProgressWindow(name, max, false)
                    {
                        // Not owned by the host application's main window: an owner must live on the same
                        // thread as the owned window, and this one deliberately does not. Topmost keeps it in
                        // front of the frozen host instead.
                        Topmost = true,
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                        OwnsMessageLoop = true,
                        Cancellable = cancellable,
                    };

                    progressWindow_Temp.Note = note;
                    progressWindow_Temp.CancelRequested += (s, e) => RaiseCancelRequested();

                    progressWindow_Temp.Loaded += (s, e) =>
                    {
                        Release();

                        // Disposed between the check below and the window actually coming up: close now that
                        // there is a loop to close. Together with that check this leaves no window in which
                        // the dialog can open and stay open.
                        if (disposed)
                        {
                            progressWindow_Temp.Close();
                        }
                    };

                    // Unlike Application.Run, a bare dispatcher loop does not end when the last window closes
                    // - it has to be told. Without this the thread would sit in Dispatcher.Run forever and
                    // every Dispose would pay the full join timeout.
                    progressWindow_Temp.Closed += (s, e) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);

                    // Set before the loop starts so the caller never sees a null window once it is released.
                    progressWindow = progressWindow_Temp;

                    // Already disposed while this thread was starting up: never open the window at all.
                    if (!disposed)
                    {
                        progressWindow_Temp.Show();

                        Dispatcher.Run();
                    }
                }
                catch (Exception exception)
                {
                    exception_Startup = exception;
                }
                finally
                {
                    // Release the caller even if the window failed before Loaded, rather than making it wait
                    // out the timeout below.
                    Release();

                    try
                    {
                        progressWindow_Temp?.Close();
                    }
                    catch (Exception)
                    {
                        // already closing, or the dispatcher is gone; nothing useful left to do here and
                        // throwing would take the host process down
                    }
                }
            })
            {
                IsBackground = true,
                Name = "sam-progress-ui-wpf",
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Bounded: a dialog that will not come up must never hold up the job it is reporting on.
            manualResetEventSlim.Wait(5000);

            if (exception_Startup != null)
            {
                // The dialog never came up, so hand the caller a failure rather than a live-looking host that
                // silently reports nothing. Construction failed, so nothing will call Dispose - clean up here.
                disposed = true;
                Shutdown();
                thread.Join(5000);
                manualResetEventSlim.Dispose();

                // Rethrow preserving the original stack, so the real cause is not replaced by this line.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception_Startup).Throw();
            }
        }

        /// <summary>
        /// Signals the readiness event, tolerating a Dispose that has already run — the thread signals once
        /// more on its way out, and if a join timed out that can land after disposal. An unhandled exception
        /// on this thread would terminate the host process, so it is swallowed deliberately.
        /// </summary>
        private void Release()
        {
            try
            {
                manualResetEventSlim.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Closes the window and ends the dispatcher loop. Both are posted, and the shutdown is queued behind
        /// the close, so a window that is up gets closed properly first. Shutting the dispatcher down directly
        /// — rather than relying only on the Closed handler — is what covers the case where the loop was never
        /// entered, or the window never built: a dispatcher told to shut down before <c>Dispatcher.Run</c> is
        /// reached makes that call return instead of blocking for the join timeout.
        /// <para>
        /// Both are posted BELOW <see cref="DispatcherPriority.Input"/>, and a synchronising call at Input
        /// priority runs first. This is the whole point rather than a detail: a Cancel click the user has
        /// already made can still be sitting unprocessed when the job finishes, and the dispatcher runs its
        /// queue strictly by priority. Closing at Send (10) or shutting down at Normal (9) — both above Input
        /// (5) — tears the loop down over the top of that click and throws it away, so the run reports success
        /// after the user asked it to stop. That is precisely the "the click never happened" failure this
        /// class exists to prevent, reintroduced one layer down.
        /// </para>
        /// </summary>
        private void Shutdown()
        {
            Dispatcher dispatcher_Temp = dispatcher;
            if (dispatcher_Temp == null)
            {
                return;
            }

            try
            {
                ProgressWindow progressWindow_Temp = progressWindow;

                if (progressWindow_Temp != null)
                {
                    // Let everything at Input priority and above finish before anything below it is queued.
                    // Bounded, because a dialog thread that has wedged must not take the host down with it -
                    // and skipped entirely when no window was ever shown, so a failed construction does not
                    // pay this wait.
                    try
                    {
                        dispatcher_Temp.Invoke(() => { }, DispatcherPriority.Input, CancellationToken.None, TimeSpan.FromSeconds(1));
                    }
                    catch (TimeoutException)
                    {
                        // the dialog thread is not draining its queue; fall through and tear it down anyway
                    }

                    dispatcher_Temp.BeginInvoke(DispatcherPriority.Background, new Action(progressWindow_Temp.Close));
                }

                dispatcher_Temp.BeginInvokeShutdown(DispatcherPriority.Background);
            }
            catch (System.ComponentModel.InvalidAsynchronousStateException)
            {
                // the thread is already gone
            }
            catch (ObjectDisposedException)
            {
                // same
            }
            catch (TaskCanceledException)
            {
                // the dispatcher shut down between the null check and the post
            }
            catch (InvalidOperationException)
            {
                // shutdown had already started; nothing left to ask of this dispatcher
            }
        }

        /// <summary>
        /// Non-null when the dialog thread died of an exception raised inside its dispatcher loop, after the
        /// constructor had already returned. The dialog is gone; the job it was reporting on is unaffected and
        /// keeps running, which is why this is reported rather than thrown.
        /// </summary>
        public Exception Exception
        {
            get
            {
                return exception_Startup;
            }
        }

        /// <summary>True once the user has clicked Cancel. Safe to read from the job's thread.</summary>
        public bool CancellationRequested
        {
            get
            {
                ProgressWindow progressWindow_Temp = progressWindow;

                return progressWindow_Temp != null && progressWindow_Temp.CancellationRequested;
            }
        }

        /// <summary>Number of steps the progress bar counts to; set it once the count is known.</summary>
        public int Max
        {
            set
            {
                ProgressWindow progressWindow_Temp = progressWindow;
                if (progressWindow_Temp != null)
                {
                    progressWindow_Temp.Max = value;
                }
            }
        }

        /// <summary>Note under the main line — say what Cancel can and cannot interrupt at this stage.</summary>
        public string Note
        {
            set
            {
                ProgressWindow progressWindow_Temp = progressWindow;
                if (progressWindow_Temp != null)
                {
                    progressWindow_Temp.Note = value;
                }
            }
        }

        /// <summary>Advances the bar and shows <paramref name="description"/> as the current step.</summary>
        public void Update(string description, bool increment = true)
        {
            ProgressWindow progressWindow_Temp = progressWindow;
            if (progressWindow_Temp != null)
            {
                progressWindow_Temp.Update(description, increment);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            Shutdown();

            // Bounded for the same reason as the startup wait: a stuck dialog thread must not hang the host.
            thread?.Join(5000);

            // Last line of defence, and the reason the caller's "dispose, then observe the token" ordering is
            // actually airtight rather than just narrower. The thread is joined, so the dialog is finished
            // with: whatever the user did has either been forwarded already or is recorded in the window's
            // own volatile flag, which outlives it. Raising from here converts that flag into the cancel the
            // caller is about to look for. RaiseCancelRequested is idempotent, so a click that was forwarded
            // normally does not fire twice.
            ProgressWindow progressWindow_Temp = progressWindow;
            if (progressWindow_Temp != null && progressWindow_Temp.CancellationRequested)
            {
                RaiseCancelRequested();
            }

            progressWindow = null;

            // After the join, so the dialog thread cannot still be signalling it. Release() covers the case
            // where that join timed out and the thread is somehow still alive.
            manualResetEventSlim.Dispose();
        }
    }
}
