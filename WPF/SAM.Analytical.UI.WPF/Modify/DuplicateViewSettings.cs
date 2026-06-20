// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.UI;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void DuplicateViewSettings(this UIAnalyticalModel uIAnalyticalModel, System.Guid guid)
        {
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            if (!analyticalModel.TryGetValue(AnalyticalModelParameter.UIGeometrySettings, out UIGeometrySettings uIGeometrySettings) || uIGeometrySettings == null)
            {
                return;
            }

            IViewSettings viewSettings = uIGeometrySettings.GetViewSettings(guid);
            if (viewSettings == null)
            {
                return;
            }

            string name = string.IsNullOrWhiteSpace(viewSettings.Name) ? viewSettings.DefaultName() : viewSettings.Name;
            name = string.Format("New {0}", name);

            SAM.Core.UI.WPF.TextBoxWindow textBoxWindow = new SAM.Core.UI.WPF.TextBoxWindow("Duplicate View", "Name") { Value = name };
            if (textBoxWindow.ShowDialog() != true)
            {
                return;
            }

            name = textBoxWindow.Value;

            if (viewSettings is TwoDimensionalViewSettings)
            {
                viewSettings = new TwoDimensionalViewSettings(System.Guid.NewGuid(), name, (TwoDimensionalViewSettings)viewSettings);
            }
            else if (viewSettings is ThreeDimensionalViewSettings)
            {
                viewSettings = new ThreeDimensionalViewSettings(System.Guid.NewGuid(), name, (ThreeDimensionalViewSettings)viewSettings);
            }
            else
            {
                return;
            }


            if (!uIGeometrySettings.AddViewSettings(viewSettings))
            {
                return;
            };

            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, uIGeometrySettings);

            uIAnalyticalModel?.SetJSAMObject(analyticalModel, new ViewSettingsModification(viewSettings, true, true));
        }
    }
}