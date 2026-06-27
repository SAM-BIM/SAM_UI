// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SAM.Core.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// WPF replacement for SAM.Analytical.Windows.Query.JsonForm: when <paramref name="e"/>
        /// is the trigger key (F12 by default), opens a modal <see cref="JsonWindow"/> showing the
        /// JSON of the given objects. No-op otherwise. Returns the dialog result (null if not shown).
        /// </summary>
        public static bool? JsonForm<T>(this IEnumerable<T> jSAMObjects, Window owner, KeyEventArgs e, Key key = Key.F12) where T : IJSAMObject
        {
            if (e == null || jSAMObjects == null)
            {
                return null;
            }

            if (e.Key != key)
            {
                return null;
            }

            JsonWindow jsonWindow = new JsonWindow(jSAMObjects.Cast<IJSAMObject>());
            if (owner != null)
            {
                jsonWindow.Owner = owner;
            }

            return jsonWindow.ShowDialog();
        }

        public static bool? JsonForm<T>(this T jSAMObject, Window owner, KeyEventArgs e, Key key = Key.F12) where T : IJSAMObject
        {
            if (jSAMObject == null)
            {
                return null;
            }

            return JsonForm(new T[] { jSAMObject }, owner, e, key);
        }
    }
}
