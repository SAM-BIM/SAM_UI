// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Core.UI
{
    /// <summary>
    /// Raised by the WPF MaterialLibraryWindow when the user imports a library, letting a host
    /// supply the imported <see cref="MaterialLibrary"/> (set <see cref="Handled"/> to suppress
    /// the default file import). Ported from SAM.Core.Windows.MaterialLibraryImportingEventArgs.
    /// </summary>
    public class MaterialLibraryImportingEventArgs : EventArgs
    {
        public MaterialLibrary MaterialLibrary { get; set; } = null;
        public bool Handled { get; set; } = false;

        public MaterialLibraryImportingEventArgs()
        {
        }
    }
}
