// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The method as the Design Optimisation window names it. The <see cref="OptimisationAlgorithm"/> value itself,
        /// which the definition holds and SAM_Tas maps to its GenOpt algorithm, is unchanged.
        /// </summary>
        /// <param name="optimisationAlgorithm">The definition's search method.</param>
        /// <param name="scope">True to add what the method can handle ("one design variable"), as the method list does.</param>
        public static string TasOptimisationAlgorithmName(this OptimisationAlgorithm optimisationAlgorithm, bool scope = false)
        {
            switch (optimisationAlgorithm)
            {
                case OptimisationAlgorithm.GoldenSection:
                    return scope ? "Golden section (one design variable)" : "Golden section";

                case OptimisationAlgorithm.HookeJeeves:
                    return "Hooke–Jeeves pattern search";

                default:
                    return optimisationAlgorithm.ToString();
            }
        }
    }
}
