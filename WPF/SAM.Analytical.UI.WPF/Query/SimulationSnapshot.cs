// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The model as a frozen simulation snapshot holds it (roadmap S04, finding F7 of SAM_UI#239): <paramref name="analyticalModel"/>
        /// without <c>UI Geometry Settings</c>, the view state SAM_UI keeps on a model (its views, their visibility, cameras and legends).
        /// <para>
        /// That parameter never reaches a simulation, and SAM_UI regenerates it - with new GUIDs - every time a model is opened, so a
        /// snapshot that carried it gave the same engineering model a different snapshot hash and canonical model hash in every session.
        /// It is the only parameter left out: everything else on the model, including anything else in the SAM.Analytical.UI parameter
        /// set, stays in the snapshot. The same parameter is already left out of <c>SimulationResultProvenance.Fingerprint</c> in SAM.
        /// </para>
        /// <para>
        /// <b>The model given is never changed.</b> When it carries the view state, the result is a copy (whose parameter sets are its
        /// own clones) with the parameter removed; otherwise it is the model itself.
        /// </para>
        /// </summary>
        public static AnalyticalModel SimulationSnapshot(AnalyticalModel analyticalModel)
        {
            if (analyticalModel == null || !analyticalModel.HasValue(UI.AnalyticalModelParameter.UIGeometrySettings))
            {
                return analyticalModel!;
            }

            AnalyticalModel result = new AnalyticalModel(analyticalModel);
            result.RemoveValue(UI.AnalyticalModelParameter.UIGeometrySettings);
            return result;
        }
    }

    public static partial class Modify
    {
        /// <summary>
        /// Gives <paramref name="analyticalModel"/> - a model derived from a snapshot, such as the OpenStudio result model - a copy of the view
        /// state of <paramref name="analyticalModel_Source"/>, which <see cref="Query.SimulationSnapshot"/> left out of the snapshot. So a
        /// result model opens with the views it opened with before S04. Returns <paramref name="analyticalModel"/>.
        /// </summary>
        public static AnalyticalModel WithViewState(AnalyticalModel analyticalModel, AnalyticalModel? analyticalModel_Source)
        {
            if (analyticalModel != null
                && analyticalModel_Source != null
                && analyticalModel_Source.TryGetValue(UI.AnalyticalModelParameter.UIGeometrySettings, out Geometry.UI.UIGeometrySettings uIGeometrySettings)
                && uIGeometrySettings != null)
            {
                analyticalModel.SetValue(UI.AnalyticalModelParameter.UIGeometrySettings, new Geometry.UI.UIGeometrySettings(uIGeometrySettings));
            }

            return analyticalModel!;
        }
    }
}
