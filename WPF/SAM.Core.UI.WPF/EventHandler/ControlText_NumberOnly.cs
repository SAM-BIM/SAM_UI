// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Text.RegularExpressions;
using System.Windows.Input;

namespace SAM.Core.UI.WPF
{
    // NOTE: lives on Query (not a class named "EventHandler") - an "EventHandler" type in this
    // namespace would shadow System.EventHandler for the many unqualified delegate fields here.
    public static partial class Query
    {
        /// <summary>
        /// WPF replacement for the SAM.Core.Windows number-only input filter. Wire to a TextBox's
        /// PreviewTextInput to reject anything that is not a digit, decimal separator or sign.
        /// </summary>
        public static void ControlText_NumberOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9.-]+").IsMatch(e.Text);
        }
    }
}
