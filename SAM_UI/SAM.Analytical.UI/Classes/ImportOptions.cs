// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF-side equivalent of SAM.Analytical.Windows.ImportOptions, used by the WPF Query.Import.
    /// </summary>
    public class ImportOptions
    {
        public bool UserSelection { get; set; } = true;
        public bool SuppressMessages { get; set; } = false;

        public ImportOptions()
        {
        }
    }
}
