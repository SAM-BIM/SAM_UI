// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Core.UI
{
    /// <summary>
    /// Raised by the WPF MaterialLibraryWindow when the user exports the library, letting a
    /// host supply its own export logic (set <see cref="Handled"/> to suppress the default).
    /// Ported from SAM.Core.Windows.MaterialLibraryExportingEventArgs.
    /// </summary>
    public class MaterialLibraryExportingEventArgs : EventArgs
    {
        public MaterialLibrary MaterialLibrary { get; set; } = null;
        public bool Handled { get; set; } = false;

        public MaterialLibraryExportingEventArgs()
        {
        }
    }
}
