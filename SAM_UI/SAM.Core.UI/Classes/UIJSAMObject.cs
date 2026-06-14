// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace SAM.Core.UI
{
    public class UIJSAMObject<T> where T: IJSAMObject
    {
        private string path;

        protected T jSAMObject;

        // Optional cached deep clone for the JSAMObject getter. The getter clones the whole object on
        // every read (dozens of times per view reload). Subclasses whose reads are strictly read-only
        // can opt in via CacheJSAMObjectClone to collapse that to one clone per modification.
        // It is OFF by default: the default getter keeps its defensive-copy contract (a fresh, isolated
        // clone per read), which callers that hand sub-objects to modal editors and cancel rely on.
        // Cache is invalidated (InvalidateClone) whenever jSAMObject is replaced.
        private T cachedClone;
        private bool cachedCloneValid;

        // Opt in (override => true) only when every consumer treats the returned object as read-only.
        protected virtual bool CacheJSAMObjectClone => false;

        protected bool modified;

        // Undo/redo history (issue: undo). Every state-changing SetJSAMObject pushes the *previous*
        // state as a compressed snapshot onto the undo stack and clears the redo stack; Undo/Redo
        // restore them and raise a FullModification so the views reload. Snapshots are compressed
        // (not live clones), so memory stays bounded on large models and one model snapshot captures
        // geometry and view settings together. Capture is skipped for transient modifications
        // (IModification.Undoable == false, e.g. a camera-only view update) and while a restore is in
        // progress.
        //
        // Snapshots are serialized OFF the UI thread. On a large (10k-space / ~33k-object) model a single
        // snapshot is ~26 MB and ~10-20 s of pure serialization (ToJsonObject -> JSON -> gzip), and it ran
        // synchronously inside SetJSAMObject *before* the view reload - so every edit blocked the UI for
        // (snapshot + reload) ~= 35 s, ~half of it just serialization. We instead store a Task<byte[]>:
        // capture returns immediately and the serialization runs in the background. Undo/Redo block on the
        // task result only when actually invoked (rare). The snapshotted reference is the *previous*
        // jSAMObject, which SetJSAMObject replaces on the next line; because every edit operates on a deep
        // Core.Query.Clone (see the JSAMObject getter), that replaced reference is an orphan and safe to
        // read off-thread. See documentation/floor-plan-large-model-performance-issues.md.
        private readonly LinkedList<Task<byte[]>> undoSnapshots = new LinkedList<Task<byte[]>>();
        private readonly LinkedList<Task<byte[]>> redoSnapshots = new LinkedList<Task<byte[]>>();
        private bool restoring;

        // Tail of the snapshot serialization chain. Snapshots are chained (not fired with independent
        // Task.Run calls) so only ONE multi-second / tens-of-MB serialization runs at a time - rapid edits
        // would otherwise saturate the thread pool and hold many large models + their JSON intermediates
        // alive at once. Access is confined to the UI thread (SetJSAMObject / Undo / Redo), so the field
        // needs no locking.
        private Task snapshotChain = Task.CompletedTask;

        // Cap the depth so history memory stays bounded on large (10k-space) models; the oldest is dropped.
        private const int maxHistoryDepth = 20;

        // Two snapshot codecs are kept side by side so they can be A/B'd (size + capture/restore time
        // are logged per snapshot via PerformanceLog):
        //  - GZip: raw gzip(UTF8(JSON)) bytes - the most compact in-memory form (no Base64).
        //  - Sam:  the SAM-native Query.Compress (gzip(JSON) + Base64), the same compression behind
        //          .sam files, stored as the UTF8 bytes of that Base64 string (~33% larger).
        // Select with the SAM_UI_UNDO_SNAPSHOT environment variable ("sam" => Sam; default => GZip).
        private enum SnapshotCodec { GZip, Sam }

        private static readonly SnapshotCodec snapshotCodec = ResolveSnapshotCodec();

        private static SnapshotCodec ResolveSnapshotCodec()
        {
            string value = Environment.GetEnvironmentVariable("SAM_UI_UNDO_SNAPSHOT");
            return !string.IsNullOrWhiteSpace(value) && value.Trim().Equals("sam", StringComparison.OrdinalIgnoreCase) ? SnapshotCodec.Sam : SnapshotCodec.GZip;
        }

        public event EventHandler HistoryChanged;

        public event EventHandler Opening;
        public event OpenedEventHandler Opened;

        public event EventHandler Saving;
        public event EventHandler Saved;

        public event EventHandler Closing;
        public event ClosedEventHandler Closed;

        public event ModifiedEventHandler Modified;

        public UIJSAMObject(string path)
        {
            this.path = path;
            modified = false;
        }

        public UIJSAMObject(T jSAMObject)
        {
            this.jSAMObject = jSAMObject;
            modified = false;
        }

        public UIJSAMObject()
        {
            
        }


        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
                modified = true;
            }
        }

        public T JSAMObject
        {
            get
            {
                if(jSAMObject == null)
                {
                    return default;
                }

                if (CacheJSAMObjectClone && cachedCloneValid)
                {
                    return cachedClone;
                }

                T clone;
                using (PerformanceLog.Measure("UIJSAMObject.Clone", typeof(T).Name))
                {
                    clone = Core.Query.Clone(jSAMObject);
                }

                if (CacheJSAMObjectClone)
                {
                    cachedClone = clone;
                    cachedCloneValid = true;
                }

                return clone;
            }

            set
            {
                SetJSAMObject(value, new FullModification());
            }
        }

        public void SetJSAMObject(T jSAMObject, IModification modification)
        {
            SetJSAMObject(jSAMObject, modification, true);
        }

        /// <summary>
        /// Sets the object and raises Modified. <paramref name="captureHistory"/> = false skips the
        /// undo snapshot - used for transient writebacks that re-persist UI state rather than a user
        /// edit (e.g. active-tab bookkeeping, save-time view-settings sync), so they must not add a
        /// history entry.
        /// </summary>
        public void SetJSAMObject(T jSAMObject, IModification modification, bool captureHistory)
        {
            if(modification == null)
            {
                modification = new FullModification();
            }

            SetJSAMObject(jSAMObject, new List<IModification>() { modification }, captureHistory);
        }

        public void SetJSAMObject(T jSAMObject, IEnumerable<IModification> modifications)
        {
            SetJSAMObject(jSAMObject, modifications, true);
        }

        /// <summary>
        /// Sets the object and raises Modified. <paramref name="captureHistory"/> = false skips the
        /// undo snapshot - used for the reload re-commit (AnalyticalWindow.Reload), which re-stores the
        /// already-current state after the edit was captured, so it must not add a second history entry
        /// (otherwise undo needs two clicks).
        /// </summary>
        public void SetJSAMObject(T jSAMObject, IEnumerable<IModification> modifications, bool captureHistory)
        {
            // Snapshot the state we are about to replace, unless this is a restore, a transient
            // (non-undoable) change, or an explicit no-capture re-commit. Captured (reference grabbed)
            // before the field is overwritten so the snapshot is the pre-edit state; the serialization
            // itself runs in the background (see EnqueueSnapshot).
            if (captureHistory && !restoring && this.jSAMObject != null && IsUndoable(modifications))
            {
                EnqueueSnapshot(this.jSAMObject, undoSnapshots);
                redoSnapshots.Clear();
                OnHistoryChanged();
            }

            this.jSAMObject = jSAMObject;
            InvalidateClone();
            modified = true;
            OnModified(modifications);
        }

        public bool CanUndo => undoSnapshots.Count > 0;

        public bool CanRedo => redoSnapshots.Count > 0;

        /// <summary>
        /// Restores the previous model state from the undo history (no-op when empty). The current
        /// state is pushed onto the redo stack first. Raises Modified (FullModification) so consumers
        /// reload. Returns whether anything was undone.
        /// </summary>
        public bool Undo()
        {
            if (undoSnapshots.Count == 0)
            {
                return false;
            }

            // Capture the current state onto the redo chain before we overwrite it.
            EnqueueSnapshot(jSAMObject, redoSnapshots);

            Task<byte[]> snapshotTask = undoSnapshots.Last.Value;
            undoSnapshots.RemoveLast();

            byte[] snapshot = ResolveSnapshot(snapshotTask);
            if (snapshot == null || snapshot.Length == 0)
            {
                // Serialization failed; drop the broken entry and report nothing was undone.
                OnHistoryChanged();
                return false;
            }

            RestoreFromSnapshot(snapshot);
            return true;
        }

        /// <summary>
        /// Re-applies a state previously undone (no-op when empty). The current state is pushed onto the
        /// undo stack first. Raises Modified (FullModification). Returns whether anything was redone.
        /// </summary>
        public bool Redo()
        {
            if (redoSnapshots.Count == 0)
            {
                return false;
            }

            // Capture the current state onto the undo chain before we overwrite it.
            EnqueueSnapshot(jSAMObject, undoSnapshots);

            Task<byte[]> snapshotTask = redoSnapshots.Last.Value;
            redoSnapshots.RemoveLast();

            byte[] snapshot = ResolveSnapshot(snapshotTask);
            if (snapshot == null || snapshot.Length == 0)
            {
                OnHistoryChanged();
                return false;
            }

            RestoreFromSnapshot(snapshot);
            return true;
        }

        /// <summary>Clears the undo/redo history (e.g. on open/close - history does not span documents).</summary>
        public void ClearHistory()
        {
            bool changed = undoSnapshots.Count > 0 || redoSnapshots.Count > 0;
            undoSnapshots.Clear();
            redoSnapshots.Clear();

            if (changed)
            {
                OnHistoryChanged();
            }
        }

        protected void OnHistoryChanged()
        {
            EventHandler eventHandler = HistoryChanged;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        // Capture only if at least one modification is undoable (a batch with a real edit + a transient
        // change still counts). A null/empty list is treated as a full modification (undoable).
        private static bool IsUndoable(IEnumerable<IModification> modifications)
        {
            if (modifications == null)
            {
                return true;
            }

            bool any = false;
            foreach (IModification modification in modifications)
            {
                any = true;
                if (modification == null || modification.Undoable)
                {
                    return true;
                }
            }

            return !any;
        }

        // Serialize the (now-immutable) previous state on the background snapshot chain and store the
        // pending Task immediately, so capture order is preserved and the UI thread never blocks on the
        // multi-second serialization. The snapshotted object must not be mutated after this call - the edit
        // pipeline always works on a deep Core.Query.Clone (see the JSAMObject getter), so the reference
        // handed in here is an orphan once SetJSAMObject replaces the field, hence safe to read off-thread.
        // Chaining off snapshotChain (rather than an independent Task.Run) keeps serializations one-at-a-time.
        // The serialization runs at BelowNormal thread priority: it overlaps the view reload (on the UI
        // thread) for the same edit, and at equal priority it steals enough CPU to roughly double the render
        // steps. Demoting it lets the render win the CPU - the snapshot just finishes a little later, which is
        // fine because nothing waits on it except a (rare) Undo/Redo of that very edit.
        private void EnqueueSnapshot(T previous, LinkedList<Task<byte[]>> snapshots)
        {
            Task<byte[]> snapshotTask = snapshotChain.ContinueWith(
                _ => CreateSnapshotAtLowPriority(previous),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            snapshotChain = snapshotTask;

            snapshots.AddLast(snapshotTask);
            while (snapshots.Count > maxHistoryDepth)
            {
                snapshots.RemoveFirst();
            }
        }

        // Run CreateSnapshot on the current (thread-pool) thread at BelowNormal priority, restoring the
        // previous priority afterwards so the pooled thread is handed back unchanged.
        private static byte[] CreateSnapshotAtLowPriority(T previous)
        {
            Thread thread = Thread.CurrentThread;
            ThreadPriority previousPriority = thread.Priority;
            try
            {
                thread.Priority = ThreadPriority.BelowNormal;
                return CreateSnapshot(previous);
            }
            finally
            {
                thread.Priority = previousPriority;
            }
        }

        // Block for the background serialization result (only ever called from Undo/Redo, which are rare and
        // user-initiated). Safe from the UI thread: the work runs on TaskScheduler.Default (thread pool) with
        // no captured SynchronizationContext, so there is no .Result deadlock. Returns null on failure.
        private static byte[] ResolveSnapshot(Task<byte[]> snapshotTask)
        {
            try
            {
                return snapshotTask.GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private void RestoreFromSnapshot(byte[] snapshot)
        {
            T state = RestoreSnapshot(snapshot);

            restoring = true;
            try
            {
                jSAMObject = state;
                InvalidateClone();
                modified = true;
                OnModified(new List<IModification>() { new FullModification() });
            }
            finally
            {
                restoring = false;
            }

            OnHistoryChanged();
        }

        // Compressed snapshot of the object, per the selected codec (see snapshotCodec). Size and time
        // are logged so the two codecs can be compared. Returns null if it cannot serialize.
        private static byte[] CreateSnapshot(T jSAMObject)
        {
            byte[] snapshot;
            using (PerformanceLog.Measure("UIJSAMObject.Snapshot.Create", snapshotCodec.ToString()))
            {
                if (snapshotCodec == SnapshotCodec.Sam)
                {
                    // SAM-native gzip(JSON)+Base64; store the Base64 string as UTF8 bytes.
                    string compressed = Core.Query.Compress(new IJSAMObject[] { jSAMObject });
                    snapshot = string.IsNullOrEmpty(compressed) ? null : System.Text.Encoding.UTF8.GetBytes(compressed);
                }
                else
                {
                    // Raw gzip(UTF8(JSON)) bytes - the most compact in-memory form.
                    System.Text.Json.Nodes.JsonObject jObject = jSAMObject?.ToJsonObject();
                    if (jObject == null)
                    {
                        snapshot = null;
                    }
                    else
                    {
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(jObject.ToJsonString());
                        using (System.IO.MemoryStream memoryStream = new System.IO.MemoryStream())
                        {
                            using (System.IO.Compression.GZipStream gZipStream = new System.IO.Compression.GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Fastest, true))
                            {
                                gZipStream.Write(bytes, 0, bytes.Length);
                            }

                            snapshot = memoryStream.ToArray();
                        }
                    }
                }
            }

            if (PerformanceLog.Enabled && snapshot != null)
            {
                PerformanceLog.Write("UIJSAMObject.Snapshot.Size", string.Format("{0} [{1} bytes]", snapshotCodec, snapshot.Length), snapshot.Length);
            }

            return snapshot;
        }

        private static T RestoreSnapshot(byte[] snapshot)
        {
            if (snapshot == null || snapshot.Length == 0)
            {
                return default;
            }

            using (PerformanceLog.Measure("UIJSAMObject.Snapshot.Restore", snapshotCodec.ToString()))
            {
                if (snapshotCodec == SnapshotCodec.Sam)
                {
                    List<T> jSAMObjects = Core.Query.Decompress<T>(System.Text.Encoding.UTF8.GetString(snapshot));
                    return jSAMObjects == null ? default : jSAMObjects.FirstOrDefault();
                }

                byte[] bytes;
                using (System.IO.MemoryStream input = new System.IO.MemoryStream(snapshot))
                using (System.IO.Compression.GZipStream gZipStream = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
                using (System.IO.MemoryStream output = new System.IO.MemoryStream())
                {
                    gZipStream.CopyTo(output);
                    bytes = output.ToArray();
                }

                System.Text.Json.Nodes.JsonObject jObject = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes)) as System.Text.Json.Nodes.JsonObject;
                return jObject == null ? default : (T)Core.Query.IJSAMObject(jObject);
            }
        }

        // Subclasses that assign the jSAMObject field directly (e.g. via Load) must call this so the
        // cached clone returned by the JSAMObject getter does not go stale.
        protected void InvalidateClone()
        {
            cachedClone = default;
            cachedCloneValid = false;
        }


        public virtual bool Open()
        {
            OnOpening();

            bool result = false;
            if(!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                List<T> jSAMObjects = null;
                try
                {
                    jSAMObjects = Core.Convert.ToSAM<T>(path);
                }
                catch(Exception exception)
                {
                    return false;
                }

                if(jSAMObjects != null && jSAMObjects.Count != 0)
                {
                    jSAMObject = jSAMObjects.FirstOrDefault();
                    InvalidateClone();
                    result = jSAMObject != null;
                }
            }

            if(result)
            {
                ClearHistory();
                OnOpened();
                modified = false;
            }

            return result;
        }

        public void OnOpening()
        {
            EventHandler eventHandler = Opening;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnOpened()
        {
            OpenedEventHandler eventHandler;

            eventHandler = Opened;
            if (eventHandler != null)
            {
                eventHandler(this, new OpenedEventArgs());
            }
        }


        public bool Close()
        {
            OnClosing();

            if (modified && jSAMObject != null)
            {
                MessageBoxResult dialogResult = MessageBox.Show("Do you want to save before closing?", "Save", MessageBoxButton.YesNoCancel);
                if(dialogResult == MessageBoxResult.Cancel)
                {
                    return false;
                }

                if(dialogResult == MessageBoxResult.Yes)
                {
                    bool result = Save();
                    if(!result)
                    {
                        return false;
                    }
                }
            }

            jSAMObject = default;
            InvalidateClone();
            ClearHistory();

            modified = false;
            OnClosed();

            return true;
        }

        public void OnClosing()
        {
            EventHandler eventHandler = Closing;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnClosed()
        {
            ClosedEventHandler eventHandler;

            eventHandler = Closed;
            if (eventHandler != null)
            {
                eventHandler(this, new ClosedEventArgs());
            }
        }


        public bool Save()
        {
            OnSaving();

            if(jSAMObject == null)
            {
                return false;
            }

            if(string.IsNullOrWhiteSpace(path))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    Filter = "SAM files (*.sam)|*.sam|json files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 1,
                    DefaultExt = "sam"
                };

                if(saveFileDialog.ShowDialog() != true)
                {
                    return false;
                }

                path = saveFileDialog.FileName;
            }

            bool result = Core.Convert.ToFile(new IJSAMObject[] { jSAMObject }, path);
            if(!result)
            {
                return result;
            }

            modified = false;

            OnSaved();

            return result;
        }

        public void OnSaving()
        {
            EventHandler eventHandler = Saving;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnSaved()
        {
            EventHandler eventHandler = Saved;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        public void OnModified(IEnumerable<IModification> modifications = null)
        {
            IEnumerable<IModification> modifications_Temp = modifications;
            if(modifications_Temp == null || modifications_Temp.Count() == 0)
            {
                modifications_Temp = new List<IModification>() { new FullModification() };
            }
            
            ModifiedEventHandler modifiedEventHandler = Modified;
            if (modifiedEventHandler != null)
            {
                modifiedEventHandler(this, new ModifiedEventArgs(modifications_Temp));
            }
        }

    }
}
