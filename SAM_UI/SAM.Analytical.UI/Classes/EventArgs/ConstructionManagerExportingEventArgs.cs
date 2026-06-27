// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF-side equivalent of SAM.Analytical.Windows.ConstructionManagerExportingEventArgs:
    /// lets a host of ConstructionLibraryWindow override export with its own logic.
    /// </summary>
    public class ConstructionManagerExportingEventArgs
    {
        public ConstructionManager ConstructionManager { get; set; } = null;
        public bool Handled { get; set; } = false;

        public ConstructionManagerExportingEventArgs()
        {
        }
    }
}
