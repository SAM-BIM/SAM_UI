// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.UI;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void SetActiveGuid(this UIAnalyticalModel uIAnalyticalModel, System.Guid guid)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            if (!analyticalModel.TryGetValue(AnalyticalModelParameter.UIGeometrySettings, out UIGeometrySettings uIGeometrySettings) || uIGeometrySettings == null)
            {
                return;
            }

            if(uIGeometrySettings.ActiveGuid == guid)
            {
                return;
            }

            uIGeometrySettings.ActiveGuid = guid;

            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, uIGeometrySettings);

            // Active-tab bookkeeping: persisting which view is active is transient UI state, not a user
            // edit, so it must not be captured on the undo history (otherwise Ctrl+Z steps through tab
            // changes and consumes the bounded history).
            uIAnalyticalModel.SetJSAMObject(analyticalModel, new ViewSettingsModification(uIGeometrySettings.GetViewSettings(guid)), false);
        }
    }
}