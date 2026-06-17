// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;
using System.Windows.Threading;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.ProgressForm: a determinate,
    /// step-based progress dialog shown non-modally on the calling (UI) thread and advanced via
    /// <see cref="Update"/>. Mirrors the original public surface (the (name) / (name, max)
    /// constructors, Caption, Max and Update). Implements IDisposable (Dispose closes the window) so
    /// existing <c>using (...)</c> call sites keep working.
    /// </summary>
    public partial class ProgressWindow : System.Windows.Window, IDisposable
    {
        private string caption;
        private const int maxLength = 50;

        public ProgressWindow()
        {
            InitializeComponent();
        }

        public ProgressWindow(string name)
        {
            InitializeComponent();

            Title = name;

            ProgressBar_Main.Minimum = 0;
            ProgressBar_Main.Value = 0;

            Show();

            DoEvents();
        }

        public ProgressWindow(string name, int max)
            : this(name)
        {
            ProgressBar_Main.Maximum = max;
        }

        public string Caption
        {
            get
            {
                return caption;
            }

            set
            {
                caption = value;
            }
        }

        public int Max
        {
            get
            {
                return (int)ProgressBar_Main.Maximum;
            }

            set
            {
                ProgressBar_Main.Maximum = value;
            }
        }

        public void Update(string text, bool increment = true)
        {
            string text_Temp = text ?? string.Empty;

            if (increment)
            {
                ProgressBar_Main.Value = Math.Min(ProgressBar_Main.Value + 1, ProgressBar_Main.Maximum);
                caption = text_Temp;
                text_Temp = string.Empty;
            }

            text_Temp = caption + " [" + (int)ProgressBar_Main.Value + "/" + (int)ProgressBar_Main.Maximum + "] " + text_Temp;

            if (text_Temp.Length > maxLength)
            {
                text_Temp = text_Temp.Substring(0, maxLength) + "...";
            }

            Label_Description.Text = text_Temp;

            Activate();

            DoEvents();
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>WPF equivalent of Application.DoEvents: pump the dispatcher queue down to Background priority so the dialog repaints between steps.</summary>
        private static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(f =>
                {
                    ((DispatcherFrame)f).Continue = false;
                    return null;
                }),
                frame);
            Dispatcher.PushFrame(frame);
        }
    }
}
