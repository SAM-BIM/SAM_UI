// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The manifest resource name of an example's definition text: a byte-for-byte copy of the SAM fixture
        /// <c>SAM.Tests/Golden/Optimisation/systems-demo-*.json</c>, embedded from <c>Resources/Optimisation</c>.
        /// </summary>
        public static string TasOptimisationExampleResourceName(this TasOptimisationExample tasOptimisationExample)
        {
            return "SAM.Analytical.UI.WPF.Optimisation." + TasOptimisationExampleFileName(tasOptimisationExample);
        }

        /// <summary>The fixture's file name, for example "systems-demo-golden-section.json".</summary>
        public static string TasOptimisationExampleFileName(this TasOptimisationExample tasOptimisationExample)
        {
            switch (tasOptimisationExample)
            {
                case TasOptimisationExample.SystemsDemoHookeJeeves:
                    return "systems-demo-hooke-jeeves.json";

                default:
                    return "systems-demo-golden-section.json";
            }
        }

        /// <summary>An example's definition text (schema sam.optimisation/1), as embedded.</summary>
        public static string TasOptimisationExampleText(this TasOptimisationExample tasOptimisationExample)
        {
            string name = TasOptimisationExampleResourceName(tasOptimisationExample);
            using (Stream? stream = typeof(Query).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("The Design Optimisation example '" + name + "' is not embedded in " + typeof(Query).Assembly.GetName().Name + ".");
                }

                using (StreamReader streamReader = new StreamReader(stream, new UTF8Encoding(false), true))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// An example's definition, read by SAM.Core.Optimisation's strict reader and checked against the Tas engine's
        /// capabilities. A new object each time. An example that does not read as a runnable definition is a build defect,
        /// so it throws rather than offering a broken form.
        /// </summary>
        public static OptimisationDefinition TasOptimisationExampleDefinition(this TasOptimisationExample tasOptimisationExample)
        {
            OptimisationDefinition? result = global::SAM.Core.Optimisation.Create.OptimisationDefinition(TasOptimisationExampleText(tasOptimisationExample), out List<OptimisationDiagnostic> diagnostics, Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities());
            if (result == null || !diagnostics.IsRunnable())
            {
                throw new InvalidOperationException("The Design Optimisation example " + TasOptimisationExampleFileName(tasOptimisationExample) + " is not a runnable definition: " + string.Join(" ", diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error)));
            }

            return result;
        }

        /// <summary>The design variable names in the order SAM_Tas gives them to the optimiser (definition order).</summary>
        public static List<string> TasOptimisationVariableNames(this OptimisationDefinition optimisationDefinition)
        {
            return (optimisationDefinition?.Variables ?? new List<DesignVariable>()).Where(x => x != null).Select(x => x.Name).ToList();
        }

        /// <summary>
        /// The output names in the order SAM_Tas gives them to the optimiser (<c>ToGenOptDocument</c>): the objective's
        /// output first, then the recorded outputs in definition order.
        /// </summary>
        public static List<string> TasOptimisationObjectiveNames(this OptimisationDefinition optimisationDefinition)
        {
            List<string> result = new List<string>();
            if (optimisationDefinition == null)
            {
                return result;
            }

            if (optimisationDefinition.Objective?.Output != null)
            {
                result.Add(optimisationDefinition.Objective.Output);
            }

            result.AddRange(optimisationDefinition.RecordedOutputs().Select(x => x.Name));
            return result;
        }
    }
}
