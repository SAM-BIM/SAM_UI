// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.UI;
using SAM.Geometry.UI.WPF;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF.Windows
{
    /// <summary>
    /// Ventilation Design airflow annotation on the NORMAL saved 2D views.
    /// <para>
    /// The design counterpart to <see cref="AnalyticalWindow.PartF"/>: a saved Section or Floor Plan view
    /// carries a <see cref="DesignAirFlowViewSettings"/> alongside its (independent) Part F settings, so
    /// both overlays can be shown together, separately, or not at all. Nothing here reads or writes
    /// <c>PartFComplianceResult</c>, a <c>PartFSpaceData</c> parameter, or any other Part F authority - the
    /// values drawn come only from <c>VentilationTerminal.DesignFlowRate_Lps</c> through
    /// <see cref="DesignAirFlowFloorPlanOverlay"/>.
    /// </para>
    /// </summary>
    public partial class AnalyticalWindow
    {
        /// <summary>
        /// One renderer per view, keyed on the view's guid, matching <c>dictionary_PartFAirflowRenderer</c>.
        /// </summary>
        private readonly Dictionary<Guid, DesignAirFlowRenderer> dictionary_DesignAirFlowRenderer = [];

        /// <summary>
        /// Draws, refreshes or removes the Ventilation Design overlay on one view, from the view's own
        /// settings. Called after the view's geometry has been loaded, alongside <see cref="UpdatePartFAirflow"/>.
        /// </summary>
        private void UpdateVentilationDesignAirflow(ViewportControl viewportControl, AnalyticalModel analyticalModel, IViewSettings viewSettings)
        {
            if (viewportControl is null || viewSettings is null)
            {
                return;
            }

            DesignAirFlowViewSettings designAirFlowViewSettings = DesignAirFlowViewSettings(viewSettings);

            FloorPlan2DControl floorPlan2DControl = viewportControl.FloorPlan2D;

            //Nothing to draw, or nowhere to draw it: a 3D view, or a view never told about the overlay. Any
            //renderer this view had is torn down rather than left holding a plan that is no longer shown.
            if (designAirFlowViewSettings is null || !designAirFlowViewSettings.Enabled || floorPlan2DControl is null || analyticalModel?.AdjacencyCluster is null)
            {
                RemoveVentilationDesignAirflow(viewportControl.Guid);
                return;
            }

            if (!dictionary_DesignAirFlowRenderer.TryGetValue(viewportControl.Guid, out DesignAirFlowRenderer designAirFlowRenderer) || designAirFlowRenderer is null)
            {
                designAirFlowRenderer = new DesignAirFlowRenderer(floorPlan2DControl);

                dictionary_DesignAirFlowRenderer[viewportControl.Guid] = designAirFlowRenderer;
            }

            designAirFlowRenderer.ViewSettings = designAirFlowViewSettings;

            designAirFlowRenderer.Load(analyticalModel.AdjacencyCluster);
        }

        /// <summary>Takes the Ventilation Design overlay off a view and stops the renderer listening to it.</summary>
        private void RemoveVentilationDesignAirflow(Guid guid)
        {
            if (!dictionary_DesignAirFlowRenderer.TryGetValue(guid, out DesignAirFlowRenderer designAirFlowRenderer))
            {
                return;
            }

            designAirFlowRenderer?.Clear();
            designAirFlowRenderer?.Detach();

            dictionary_DesignAirFlowRenderer.Remove(guid);
        }

        /// <summary>
        /// The view's Ventilation Design presentation settings, or null where it has none. Absence means
        /// OFF, matching <see cref="PartFAirflowViewSettings"/>'s own rule.
        /// </summary>
        private static DesignAirFlowViewSettings DesignAirFlowViewSettings(IViewSettings viewSettings)
        {
            return viewSettings is ViewSettings viewSettings_Temp
                && viewSettings_Temp.TryGetValue(AnalyticalViewSettingsParameter.VentilationDesignAirflow, out DesignAirFlowViewSettings result)
                ? result
                : null;
        }
    }
}
