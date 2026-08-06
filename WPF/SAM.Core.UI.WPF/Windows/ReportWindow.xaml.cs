// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// Read-only, scrollable plain-text report viewer, for callers whose report text can be too long
    /// for a MessageBox (which has no scrollbar and is impractical past a few lines) - e.g. a model
    /// with thousands of spaces. Generic and presentation-only: it has no knowledge of what produced
    /// the text, so it carries none of the caller's domain or regulatory logic.
    /// </summary>
    public partial class ReportWindow : Window
    {
        public ReportWindow()
        {
            InitializeComponent();
        }

        public ReportWindow(string title, string text)
            : this()
        {
            if (!string.IsNullOrEmpty(title))
            {
                Title = title;
            }

            Text = text;
        }

        public string Text
        {
            get
            {
                return TextBox_Main.Text;
            }

            set
            {
                TextBox_Main.Text = value;
            }
        }

        private void Button_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            string text = TextBox_Main.Text;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (!TrySetClipboardText(text))
            {
                MessageBox.Show(this, "Could not copy the report to the clipboard. Another application may be holding it open - please try again.", "Copy All", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// The Windows clipboard is briefly locked by other processes (clipboard managers, remote
        /// desktop, antivirus scanners) far more often than most apps expect; Clipboard.SetText then
        /// throws a COMException (CLIPBRD_E_CANT_OPEN). This application has no global
        /// unhandled-exception handler, so letting that escape from a button click crashes the whole
        /// app over a transient, retryable condition. A short retry is the standard mitigation.
        /// </summary>
        private static bool TrySetClipboardText(string text)
        {
            const int maxAttempts = 5;
            const int retryDelayMilliseconds = 100;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    if (attempt == maxAttempts)
                    {
                        return false;
                    }

                    System.Threading.Thread.Sleep(retryDelayMilliseconds);
                }
            }

            return false;
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
