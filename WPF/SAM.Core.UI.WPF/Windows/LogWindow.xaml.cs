// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.LogForm: a read-only grid of a
    /// <see cref="Log"/>'s records (type + message). Mirrors the original public surface (the (Log)
    /// constructor). NOTE: the legacy per-row type icon is shown as the type name instead of a bitmap.
    /// </summary>
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        public LogWindow(Log log)
            : this()
        {
            LoadLog(log);
        }

        private void LoadLog(Log log)
        {
            List<Row> rows = new List<Row>();

            if (log != null)
            {
                foreach (LogRecord logRecord in log)
                {
                    if (logRecord == null)
                    {
                        continue;
                    }

                    rows.Add(new Row { Type = logRecord.LogRecordType.ToString(), Message = logRecord.Text });
                }
            }

            DataGrid_Main.ItemsSource = rows;
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private sealed class Row
        {
            public string Type { get; set; }

            public string Message { get; set; }
        }
    }
}
