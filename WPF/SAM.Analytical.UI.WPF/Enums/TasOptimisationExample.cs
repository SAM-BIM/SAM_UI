// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The example form values Simulate &gt; Optimisation can load. They are the inputs of the proven Systems Demo
    /// acceptance (SAM_Tas#86 / SAM_Tas_Grasshopper#11): one parameter <c>Setpoint</c> and the outputs
    /// <c>Result</c> (minimised), <c>Cost</c> and <c>CO2</c>. Only form values are loaded: the Tas project folder and
    /// the script are always the user's own, and no Tas model or script is shipped.
    /// </summary>
    public enum TasOptimisationExample
    {
        /// <summary>Golden section: Setpoint -5 to 35, the GoldenSectionAlgorithm and OptimizationSettings defaults.</summary>
        SystemsDemoGoldenSection,

        /// <summary>GPS Hooke-Jeeves: Setpoint start 10, -5 to 35, step 2, the GPSHookeJeevesAlgorithm and OptimizationSettings defaults.</summary>
        SystemsDemoHookeJeeves,
    }
}
