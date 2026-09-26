// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Create
    {
        /// <summary>
        /// The "Space Assumptions PDF" context-menu item for a Space selection, shared by the 3D/2D view and the
        /// model tree. Phase 1 reports one Space: with several selected the item is shown disabled, with a tooltip
        /// saying why, rather than silently reporting the first.
        /// </summary>
        public static MenuItem MenuItem_SpaceAssumptionsPdf(IEnumerable<Space>? spaces, RoutedEventHandler? click)
        {
            List<Space> spaces_Selected = spaces?.Where(x => x != null).GroupBy(x => x.Guid).Select(x => x.First()).ToList() ?? new List<Space>();

            MenuItem menuItem = new MenuItem()
            {
                Name = "MenuItem_SpaceAssumptionsPdf",
                Header = Query.SpaceAssumptionsPdfTitle,
                Tag = spaces_Selected,
                IsEnabled = spaces_Selected.Count == 1,
                ToolTip = spaces_Selected.Count == 1
                    ? "Create the Space Assumptions PDF for this Space"
                    : "The Space Assumptions PDF is created for one Space at a time: select a single Space.",
            };

            ToolTipService.SetShowOnDisabled(menuItem, true);

            if (click != null)
            {
                menuItem.Click += click;
            }

            return menuItem;
        }
    }
}
