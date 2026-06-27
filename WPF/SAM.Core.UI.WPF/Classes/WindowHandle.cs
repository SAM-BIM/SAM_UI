// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows.Forms;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF-native replacement for the WinForms SAM.Core.Windows.WindowHandle: wraps a window handle
    /// as an <see cref="IWin32Window"/> so a WPF window can act as the owner for the
    /// <c>ShowDialog(this Window, IWin32Window)</c> bridge.
    /// </summary>
    public class WindowHandle : IWin32Window
    {
        private readonly IntPtr handle;

        public WindowHandle(IntPtr handle)
        {
            this.handle = handle;
        }

        public WindowHandle(System.Windows.Window window)
        {
            if (window != null)
            {
                handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            }
        }

        public IntPtr Handle
        {
            get
            {
                return handle;
            }
        }
    }
}
