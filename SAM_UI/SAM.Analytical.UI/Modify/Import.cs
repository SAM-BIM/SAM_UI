// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void Import(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            analyticalModel = Query.Import(analyticalModel, new ImportOptions(), owner);
            if(analyticalModel == null)
            {
                return;
            }

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }

        public static void Import<T>(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Window owner = null) where T : IJSAMObject
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            analyticalModel = Query.Import<T>(analyticalModel, new ImportOptions(), owner);
            if (analyticalModel == null)
            {
                return;
            }

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }
    }
}