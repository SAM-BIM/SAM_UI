// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The example definitions Simulate &gt; Optimisation can load: SAM.Core.Optimisation's Systems Demo fixtures
    /// (<c>systems-demo-*.json</c>, embedded, <see cref="Query.TasOptimisationExampleDefinition"/>). They are the inputs of
    /// the proven Systems Demo acceptance (SAM_Tas#86 / SAM_Tas_Grasshopper#11): one design variable <c>Setpoint</c> and
    /// the outputs <c>Result</c> (the objective, minimised), <c>Cost</c> and <c>CO2</c>. Only the definition is loaded:
    /// the Tas project folder and the script are always the user's own, and no Tas model or script is shipped.
    /// </summary>
    public enum TasOptimisationExample
    {
        /// <summary>Golden section (systems-demo-golden-section.json): Setpoint -5 to 35 (start 3, step 1, not used), tolerance 0.1, at most 2000 simulations.</summary>
        SystemsDemoGoldenSection,

        /// <summary>Hooke–Jeeves (systems-demo-hooke-jeeves.json): Setpoint start 10, -5 to 35, step 2, the SAM_Tas default settings (2, 0, 1, 4), at most 2000 simulations.</summary>
        SystemsDemoHookeJeeves,
    }
}
