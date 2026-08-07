// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Object;
using SAM.Geometry.UI;
using SAM.Geometry.UI.WPF;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI.WPF.Windows
{
    /// <summary>
    /// Part F airflow annotation on the NORMAL saved 2D views.
    /// <para>
    /// This is where Part F becomes a drawing rather than a checking tool. A saved Section or Floor Plan view
    /// carries a <see cref="PartFAirflowViewSettings"/>, so a view called "Level 0 [0m] Part F" can hold its
    /// colour scheme AND its airflow annotation and reopen with both. The assessment window's airflow tab
    /// remains for checking the assessment; both draw through the same
    /// <see cref="PartFAirflowRenderer"/>, so what is checked there is what is issued here.
    /// </para>
    /// <para>
    /// The existing <c>PartF Data</c> colour scheme is untouched and orthogonal: it is a
    /// <c>ValueAppearanceSettings</c> that colours the space fills, and it works with or without the
    /// annotation. Both at once is the intended combination.
    /// </para>
    /// </summary>
    public partial class AnalyticalWindow
    {
        /// <summary>
        /// One renderer per view, keyed on the view's guid. Views come and go with their tabs, and each one
        /// draws on its own plan with its own settings.
        /// </summary>
        private readonly Dictionary<Guid, PartFAirflowRenderer> dictionary_PartFAirflowRenderer = [];

        /// <summary>
        /// The assessment last calculated, and the model it was calculated from.
        /// <para>
        /// Cached against the model INSTANCE, not copied into the view. A view holds no result and no rate -
        /// it holds how to present them - so every regeneration re-reads the assessment; and since
        /// <c>UIAnalyticalModel</c> hands out a new <c>AnalyticalModel</c> on every edit, a stale cache cannot
        /// survive a change to the building. Several Part F views on one model then share one calculation
        /// instead of running it once per tab.
        /// </para>
        /// </summary>
        private AnalyticalModel analyticalModel_PartF;

        private string zoneCategoryName_PartF;

        private List<PartFComplianceResult> partFComplianceResults;

        /// <summary>
        /// Draws, refreshes or removes the Part F annotation on one view, from the view's own settings.
        /// Called after the view's geometry has been loaded, so the plan and its text are there to annotate
        /// and to keep clear of.
        /// </summary>
        private void UpdatePartFAirflow(ViewportControl viewportControl, AnalyticalModel analyticalModel, IViewSettings viewSettings, GeometryObjectModel geometryObjectModel)
        {
            if (viewportControl is null || viewSettings is null)
            {
                return;
            }

            PartFAirflowViewSettings partFAirflowViewSettings = PartFAirflowViewSettings(viewSettings);

            FloorPlan2DControl floorPlan2DControl = viewportControl.FloorPlan2D;

            //Nothing to draw, or nowhere to draw it: a 3D view, or the legacy orthographic 2D path. Any
            //renderer this view had is torn down rather than left holding a plan that is no longer shown.
            if (partFAirflowViewSettings is null || !partFAirflowViewSettings.Enabled || floorPlan2DControl is null || analyticalModel?.AdjacencyCluster is null)
            {
                RemovePartFAirflow(viewportControl.Guid);
                return;
            }

            if (!dictionary_PartFAirflowRenderer.TryGetValue(viewportControl.Guid, out PartFAirflowRenderer partFAirflowRenderer) || partFAirflowRenderer is null)
            {
                partFAirflowRenderer = new PartFAirflowRenderer(floorPlan2DControl);

                dictionary_PartFAirflowRenderer[viewportControl.Guid] = partFAirflowRenderer;
            }

            partFAirflowRenderer.ViewSettings = partFAirflowViewSettings;

            partFAirflowRenderer.Load(analyticalModel.AdjacencyCluster, PartFComplianceResults(analyticalModel, partFAirflowViewSettings), geometryObjectModel);
        }

        /// <summary>Takes the Part F annotation off a view and stops the renderer listening to it.</summary>
        private void RemovePartFAirflow(Guid guid)
        {
            if (!dictionary_PartFAirflowRenderer.TryGetValue(guid, out PartFAirflowRenderer partFAirflowRenderer))
            {
                return;
            }

            partFAirflowRenderer?.Clear();
            partFAirflowRenderer?.Detach();

            dictionary_PartFAirflowRenderer.Remove(guid);
        }

        /// <summary>
        /// The view's Part F presentation settings, or null where it has none.
        /// <para>
        /// Absence means OFF, deliberately. Every view saved before the annotation existed carries no such
        /// parameter, and reading absence as "defaults on" would make every one of them sprout tags the first
        /// time it was reopened.
        /// </para>
        /// </summary>
        private static PartFAirflowViewSettings PartFAirflowViewSettings(IViewSettings viewSettings)
        {
            return viewSettings is ViewSettings viewSettings_Temp
                && viewSettings_Temp.TryGetValue(AnalyticalViewSettingsParameter.PartFAirflow, out PartFAirflowViewSettings result)
                ? result
                : null;
        }

        /// <summary>
        /// The calculated assessment of every dwelling in the scope this view reports on.
        /// <para>
        /// Run through the same <c>PartFCalculator</c> the Part F command uses, so a view and the assessment
        /// window can never disagree about a number, and cached per model and scope so several Part F views
        /// do not each pay for it.
        /// </para>
        /// </summary>
        private List<PartFComplianceResult> PartFComplianceResults(AnalyticalModel analyticalModel, PartFAirflowViewSettings partFAirflowViewSettings)
        {
            string zoneCategoryName = partFAirflowViewSettings.ZoneCategoryName;

            if (ReferenceEquals(analyticalModel, analyticalModel_PartF)
                && string.Equals(zoneCategoryName, zoneCategoryName_PartF, StringComparison.Ordinal)
                && partFComplianceResults is not null)
            {
                return partFComplianceResults;
            }

            PartFCalculator partFCalculator = Analytical.Query.DefaultPartFCalculator();
            if (partFCalculator is null)
            {
                return [];
            }

            partFCalculator.AdjacencyCluster = analyticalModel.AdjacencyCluster;

            using (Core.UI.PerformanceLog.Measure("AnalyticalWindow.PartF.Calculate", zoneCategoryName ?? "(whole model)"))
            {
                partFCalculator.Calculate(zoneCategoryName);
            }

            analyticalModel_PartF = analyticalModel;
            zoneCategoryName_PartF = zoneCategoryName;

            partFComplianceResults = [.. (partFCalculator.DwellingResults ?? [])
                .Where(x => x?.ComplianceResult is not null)
                .Select(x => x.ComplianceResult)];

            return partFComplianceResults;
        }
    }
}
