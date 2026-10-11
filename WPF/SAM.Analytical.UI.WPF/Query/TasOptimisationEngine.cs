// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The engines the Design Optimisation window offers, in the order it lists them: "tas-model" (SAM_Tas changes and
        /// reads the model itself through a generated script, PR7b) and "tas-script" (the user's own TasGenExecute script).
        /// Text, not a SAM.Core.Optimisation type, so <see cref="Query"/> loads without that assembly.
        /// </summary>
        public static IReadOnlyList<string> TasOptimisationEngines => Array.AsReadOnly(new[] { Analytical.Tas.GenOpt.Query.TasModelEngine, Analytical.Tas.GenOpt.Query.TasOptimisationEngine });

        /// <summary>The engine as the window names it: "Tas model (no script)" or "Tas script".</summary>
        public static string TasOptimisationEngineName(string? engine)
        {
            if (engine == Analytical.Tas.GenOpt.Query.TasModelEngine)
            {
                return "Tas model (no script)";
            }

            if (engine == Analytical.Tas.GenOpt.Query.TasOptimisationEngine)
            {
                return "Tas script";
            }

            return string.IsNullOrWhiteSpace(engine) ? "Tas script" : engine!;
        }

        /// <summary>True for the "tas-model" engine.</summary>
        public static bool IsTasModelEngine(string? engine)
        {
            return engine == Analytical.Tas.GenOpt.Query.TasModelEngine;
        }

        /// <summary>What the engine runs: SAM_Tas' "tas-model" or "tas-script" capabilities.</summary>
        public static IOptimisationCapabilities TasOptimisationCapabilities(string? engine)
        {
            return IsTasModelEngine(engine) ? Analytical.Tas.GenOpt.Query.TasModelCapabilities() : Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities();
        }

        /// <summary>
        /// The methods the form offers for the engine and the design variables chosen, so it never offers a method that
        /// cannot run them (the definition's OPT409/OPT412 would block it):
        /// <list type="bullet">
        /// <item>"tas-script": golden section and Hooke–Jeeves, as before PR8.</item>
        /// <item>"tas-model" with a choice (a <c>"discrete"</c> variable): try every option only.</item>
        /// <item>"tas-model" with values: golden section while there is at most one variable, and Hooke–Jeeves.</item>
        /// </list>
        /// </summary>
        public static List<OptimisationAlgorithm> TasOptimisationAlgorithms(string? engine, IEnumerable<TasOptimisationParameterRow>? parameterRows)
        {
            if (!IsTasModelEngine(engine))
            {
                return new List<OptimisationAlgorithm> { OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves };
            }

            List<TasOptimisationParameterRow> rows = (parameterRows ?? Enumerable.Empty<TasOptimisationParameterRow>()).Where(x => x != null).ToList();
            if (rows.Any(x => x.IsChoice))
            {
                return new List<OptimisationAlgorithm> { OptimisationAlgorithm.TryEveryOption };
            }

            List<OptimisationAlgorithm> result = new List<OptimisationAlgorithm>();
            if (rows.Count <= 1)
            {
                result.Add(OptimisationAlgorithm.GoldenSection);
            }

            result.Add(OptimisationAlgorithm.HookeJeeves);
            return result;
        }

        /// <summary>
        /// A target or measure in the engine's words: its kind's display name, then the model items it refers to and its
        /// parameters, for example "Zone heating setpoint of internal condition “Office”" or "Overheating hours
        /// (resultant temperature threshold 28 °C)". An unknown kind is shown as written.
        /// </summary>
        public static string TasOptimisationBindingText(OptimisationBinding? optimisationBinding)
        {
            if (optimisationBinding == null)
            {
                return string.Empty;
            }

            IOptimisationCapabilities capabilities = Analytical.Tas.GenOpt.Query.TasModelCapabilities();
            IEnumerable<OptimisationBindingCapability> kinds = optimisationBinding is OptimisationTarget ? capabilities.Targets : capabilities.Measures;
            OptimisationBindingCapability? kind = kinds?.FirstOrDefault(x => x != null && x.Kind == optimisationBinding.Kind);

            string text = kind?.DisplayName ?? optimisationBinding.Kind ?? string.Empty;

            List<string> references = new List<string>();
            foreach (KeyValuePair<string, string> keyValuePair in optimisationBinding.Reference ?? new Dictionary<string, string>())
            {
                string key = kind?.ReferenceKeys?.FirstOrDefault(x => x?.Name == keyValuePair.Key)?.DisplayName ?? keyValuePair.Key;
                references.Add(key + " “" + keyValuePair.Value + "”");
            }

            // The kind's own key order (plant room before controller), then any other key.
            if (kind?.ReferenceKeys != null)
            {
                List<string> order = kind.ReferenceKeys.Where(x => x != null).Select(x => x.DisplayName ?? x.Name).ToList();
                references = references.OrderBy(x => { int index = order.FindIndex(y => x.StartsWith(y + " ", StringComparison.Ordinal)); return index < 0 ? int.MaxValue : index; }).ToList();
            }

            if (references.Count != 0)
            {
                text += " of " + string.Join(", ", references);
            }

            List<string> parameters = new List<string>();
            foreach (KeyValuePair<string, double> keyValuePair in optimisationBinding.Parameters ?? new Dictionary<string, double>())
            {
                OptimisationBindingParameter? parameter = kind?.Parameters?.FirstOrDefault(x => x?.Name == keyValuePair.Key);
                string value = keyValuePair.Value.ToString("R", CultureInfo.InvariantCulture);
                parameters.Add((parameter?.DisplayName ?? keyValuePair.Key) + " " + (string.IsNullOrWhiteSpace(parameter?.Unit) ? value : value + " " + parameter!.Unit));
            }

            if (parameters.Count != 0)
            {
                text += " (" + string.Join(", ", parameters) + ")";
            }

            return text;
        }
    }
}
