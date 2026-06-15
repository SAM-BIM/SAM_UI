// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using System.Windows.Interop;

namespace SAM.Core.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Shows a WPF <see cref="Window"/> modally, parented to a Win32/WinForms owner.
        /// Bridges the legacy WinForms call sites (which pass an <see cref="IWin32Window"/>
        /// owner and expect a DialogResult) to the WPF dialog model. Returns the nullable
        /// bool from <see cref="Window.ShowDialog"/> (true = OK, false/null = cancelled).
        /// </summary>
        public static bool? ShowDialog(this Window window, System.Windows.Forms.IWin32Window owner)
        {
            if (window == null)
            {
                return null;
            }

            if (owner != null && owner.Handle != System.IntPtr.Zero)
            {
                new WindowInteropHelper(window) { Owner = owner.Handle };
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return window.ShowDialog();
        }
    }
}
