// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The method as the Design Optimisation window names it. The <see cref="AlgorithmType"/> value itself, which
        /// SAM_Tas and the kernel know, is unchanged.
        /// </summary>
        /// <param name="algorithmType">The SAM_Tas algorithm.</param>
        /// <param name="scope">True to add what the method can handle ("one design variable"), as the method list does.</param>
        public static string TasOptimisationAlgorithmName(this AlgorithmType algorithmType, bool scope = false)
        {
            switch (algorithmType)
            {
                case AlgorithmType.GoldenSection:
                    return scope ? "Golden section (one design variable)" : "Golden section";

                case AlgorithmType.GPSHookeJeeves:
                    return "Hooke–Jeeves pattern search";

                default:
                    return algorithmType.ToString();
            }
        }
    }
}
