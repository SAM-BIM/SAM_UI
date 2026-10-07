// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What Simulate &gt; Optimisation runs, as the existing SAM_Tas GenOpt objects: the algorithm, the
    /// OptimizationSettings, the NumberParameters and the Objectives, the primary (minimised) objective first.
    /// Built by <see cref="TasOptimisationInput.TryGetDefinition"/>. Nothing here optimises, validates beyond
    /// SAM_Tas' own rules, or launches anything: <see cref="Validate"/> calls SAM_Tas' conversion, and
    /// <see cref="ToGenOptDocument"/> builds the document whose <c>RunNative</c> does the work.
    /// </summary>
    public sealed class TasOptimisationDefinition
    {
        internal TasOptimisationDefinition(Algorithm algorithm, OptimizationSettings optimizationSettings, IEnumerable<NumberParameter> numberParameters, IEnumerable<Objective> objectives)
        {
            Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            OptimizationSettings = optimizationSettings ?? throw new ArgumentNullException(nameof(optimizationSettings));
            NumberParameters = (numberParameters ?? Enumerable.Empty<NumberParameter>()).ToList().AsReadOnly();
            Objectives = (objectives ?? Enumerable.Empty<Objective>()).ToList().AsReadOnly();
        }

        public Algorithm Algorithm { get; }

        public OptimizationSettings OptimizationSettings { get; }

        /// <summary>The parameters in coordinate order.</summary>
        public IReadOnlyList<NumberParameter> NumberParameters { get; }

        /// <summary>The objectives in output order. The first is the primary one, which the optimiser minimises.</summary>
        public IReadOnlyList<Objective> Objectives { get; }

        public IReadOnlyList<string> ParameterNames => NumberParameters.Select(x => x.Name).ToList();

        public IReadOnlyList<string> ObjectiveNames => Objectives.Select(x => x.Name).ToList();

        /// <summary>
        /// Validates the definition exactly as <see cref="GenOptDocument.RunNative"/> does before it creates a folder or
        /// starts a process, by calling the same SAM_Tas conversions. It throws what they throw:
        /// <see cref="GenOptCompatibilityException"/> for an invalid setting, or one GenOpt itself would not accept, and
        /// <see cref="NotSupportedException"/> for an algorithm the native optimiser does not run. Nothing is created.
        /// </summary>
        public void Validate()
        {
            List<NumberParameter> numberParameters = Analytical.Tas.GenOpt.Convert.NumberParameters(NumberParameters.Cast<IParameter>());
            ObjectiveFunctionLocation objectiveFunctionLocation = ObjectiveFunctionLocation();
            Analytical.Tas.GenOpt.Convert.Objectives(objectiveFunctionLocation);
            Analytical.Tas.GenOpt.Convert.ToSAM_OptimisationProblem(numberParameters.Cast<IParameter>(), objectiveFunctionLocation);
            Analytical.Tas.GenOpt.Convert.ToSAM_Optimiser(Algorithm, OptimizationSettings, numberParameters.Count);
        }

        /// <summary>
        /// The document <c>RunNative</c> runs: the workspace folder, the script text, and this definition's objects, added
        /// in the order the SAM_Tas Grasshopper component adds them.
        /// </summary>
        public GenOptDocument ToGenOptDocument(string directory, string scriptText)
        {
            GenOptDocument result = new GenOptDocument(directory)
            {
                Algorithm = Algorithm,
                OptimizationSettings = OptimizationSettings,
            };

            result.AddScript(scriptText);

            foreach (Objective objective in Objectives)
            {
                result.AddObjective(objective);
            }

            foreach (NumberParameter numberParameter in NumberParameters)
            {
                result.AddParameter(numberParameter);
            }

            return result;
        }

        private ObjectiveFunctionLocation ObjectiveFunctionLocation()
        {
            ObjectiveFunctionLocation result = new ObjectiveFunctionLocation();
            foreach (Objective objective in Objectives)
            {
                result.Add(objective);
            }

            return result;
        }
    }
}
