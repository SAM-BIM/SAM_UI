// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.UI;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void CopyViewSettings(this UIAnalyticalModel uIAnalyticalModel, System.Guid guid)
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

            IViewSettings viewSettings_Destination = uIGeometrySettings.GetViewSettings(guid);
            if (viewSettings_Destination == null)
            {
                return;
            }

            IEnumerable<IViewSettings>? viewSettings_Compatible = null;
            if (viewSettings_Destination is TwoDimensionalViewSettings)
            {
                viewSettings_Compatible = uIGeometrySettings.GetViewSettings<TwoDimensionalViewSettings>().Cast<IViewSettings>();
            }
            else if (viewSettings_Destination is ThreeDimensionalViewSettings)
            {
                viewSettings_Compatible = uIGeometrySettings.GetViewSettings<ThreeDimensionalViewSettings>().Cast<IViewSettings>();
            }

            if(viewSettings_Compatible is null || !viewSettings_Compatible.Any())
            {
                return;
            }

            IViewSettings? viewSettings_Source = null;
            using (Core.Windows.Forms.ComboBoxForm<IViewSettings> textBoxForm = new ("Copy View", viewSettings_Compatible, x => x.Name))
            {
                if (textBoxForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                viewSettings_Source = textBoxForm.SelectedItem;
            }

            if(viewSettings_Source is null)
            {
                return;
            }


            if (viewSettings_Source is TwoDimensionalViewSettings twoDimensionalViewSettings)
            {
                viewSettings_Destination = new TwoDimensionalViewSettings(viewSettings_Destination.Guid, viewSettings_Destination.Name, twoDimensionalViewSettings);
            }
            else if (viewSettings_Source is ThreeDimensionalViewSettings threeDimensionalViewSettings)
            {
                viewSettings_Destination = new ThreeDimensionalViewSettings(viewSettings_Destination.Guid, viewSettings_Destination.Name, threeDimensionalViewSettings);
            }
            else
            {
                return;
            }


            if (!uIGeometrySettings.AddViewSettings(viewSettings_Destination))
            {
                return;
            };

            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, uIGeometrySettings);

            // "Load view" copies the whole source view settings (appearances, types, legend, camera),
            // so the geometry must be regenerated as well as the camera applied - hence updateGeometry:
            // true. ("Load camera" / CopyViewSettingsCamera copies only the camera, updateGeometry: false.)
            uIAnalyticalModel.SetJSAMObject(analyticalModel, new ViewSettingsModification(viewSettings_Destination, true, true));
        }
    }
}