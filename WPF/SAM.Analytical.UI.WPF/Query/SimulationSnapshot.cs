// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Linq;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The JSON of <paramref name="analyticalModel"/> as a frozen simulation snapshot holds it (roadmap S04, finding F7 of SAM_UI#239):
        /// the model's own JSON without <c>UI Geometry Settings</c>, the view state SAM_UI keeps on a model (its views, their visibility,
        /// cameras and legends).
        /// <para>
        /// That parameter never reaches a simulation, and SAM_UI regenerates it - with new GUIDs - every time a model is opened, so a
        /// snapshot that carried it gave the same engineering model a different snapshot hash and canonical model hash in every session.
        /// It is the only parameter left out; the same parameter is already left out of <c>SimulationResultProvenance.Fingerprint</c> in
        /// SAM. Its parameter set (SAM.Analytical.UI) goes too, but only when that leaves it empty: an empty set carries nothing but its
        /// GUID, which is the assembly's module version id and so changes with every build of SAM_UI. Anything else stored in that set
        /// stays, as does every other parameter set.
        /// </para>
        /// <para>
        /// <b>The model given is never changed</b>: this edits the freshly built JSON tree, not the model. Null for no model.
        /// </para>
        /// </summary>
        public static JsonObject? SimulationSnapshotJsonObject(AnalyticalModel analyticalModel)
        {
            JsonObject? result = analyticalModel?.ToJsonObject();
            if (result?["ParameterSets"] is not JsonArray parameterSets)
            {
                return result;
            }

            string name_ParameterSet = Core.Query.Name(typeof(UI.AnalyticalModelParameter).Assembly);
            string? name_Parameter = Core.Attributes.ParameterProperties.Get(UI.AnalyticalModelParameter.UIGeometrySettings)?.Name;

            foreach (JsonObject parameterSet in parameterSets.OfType<JsonObject>().Where(x => Text(x["Name"]) == name_ParameterSet).ToList())
            {
                //An empty set is written without "Parameters" at all: it is dropped like an emptied one.
                if (parameterSet["Parameters"] is not JsonArray parameters)
                {
                    if (parameterSet["Parameters"] == null)
                    {
                        parameterSets.Remove(parameterSet);
                    }

                    continue;
                }

                foreach (JsonNode? parameter in parameters.Where(x => x is JsonObject jsonObject && Text(jsonObject["Name"]) == name_Parameter).ToList())
                {
                    parameters.Remove(parameter);
                }

                if (parameters.Count == 0)
                {
                    parameterSets.Remove(parameterSet);
                }
            }

            return result;
        }

        private static string? Text(JsonNode? jsonNode)
        {
            return jsonNode is JsonValue jsonValue && jsonValue.TryGetValue(out string? value) ? value : null;
        }
    }

    public static partial class Query
    {
        /// <summary>
        /// A copy of <paramref name="analyticalModel"/>'s view state (<c>UI Geometry Settings</c>), taken now - at freeze time, so that later
        /// edits to the open model cannot reach it - for <see cref="Modify.WithViewState"/>; null when it has none.
        /// </summary>
        public static Geometry.UI.UIGeometrySettings? ViewState(AnalyticalModel analyticalModel)
        {
            return analyticalModel != null
                && analyticalModel.TryGetValue(UI.AnalyticalModelParameter.UIGeometrySettings, out Geometry.UI.UIGeometrySettings uIGeometrySettings)
                && uIGeometrySettings != null
                ? new Geometry.UI.UIGeometrySettings(uIGeometrySettings)
                : null;
        }
    }

    public static partial class Modify
    {
        /// <summary>
        /// Gives <paramref name="analyticalModel"/> - a model derived from a snapshot, such as the OpenStudio result model - a copy of
        /// <paramref name="viewState"/> (<see cref="Query.ViewState"/> of the open model when it was frozen), which
        /// <see cref="Query.SimulationSnapshotJsonObject"/> left out of the snapshot. So a result model opens with the views it opened with
        /// before S04. Returns <paramref name="analyticalModel"/>.
        /// </summary>
        public static AnalyticalModel WithViewState(AnalyticalModel analyticalModel, Geometry.UI.UIGeometrySettings? viewState)
        {
            if (analyticalModel != null && viewState != null)
            {
                analyticalModel.SetValue(UI.AnalyticalModelParameter.UIGeometrySettings, new Geometry.UI.UIGeometrySettings(viewState));
            }

            return analyticalModel!;
        }
    }
}
