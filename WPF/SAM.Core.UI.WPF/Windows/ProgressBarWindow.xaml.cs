using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// Interaction logic for ProgressBarWindow.xaml
    /// </summary>
    public partial class ProgressBarWindow : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly DispatcherTimer animationTimer;

        private Action action;

        public ProgressBarWindow()
        {
            InitializeComponent();
        }

        public ProgressBarWindow(string title, string text)
        {
            InitializeComponent();
            Title = title;
            label.Content = text;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            // When constructed via the static Show(action) helpers, run the supplied work on a
            // background thread (mirrors the WinForms MarqueeProgressForm BackgroundWorker) and close
            // the (indeterminate) dialog once it completes.
            if (action != null)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        Dispatcher.Invoke(new Action(Close));
                    }
                });
            }
        }

        /// <summary>
        /// WPF replacement for the static SAM.Core.Windows.Forms.MarqueeProgressForm.Show: shows an
        /// indeterminate progress dialog while <paramref name="action"/> runs on a background thread,
        /// then closes.
        /// </summary>
        public static void Show(string name, Action action)
        {
            Show(name, action, null);
        }

        public static void Show(string name, Action action, System.Windows.Forms.IWin32Window owner)
        {
            if (action == null)
            {
                return;
            }

            ProgressBarWindow progressBarWindow = new ProgressBarWindow(name, name) { action = action };
            if (owner == null)
            {
                progressBarWindow.ShowDialog();
            }
            else
            {
                progressBarWindow.ShowDialog(owner);
            }
        }
    }
}
