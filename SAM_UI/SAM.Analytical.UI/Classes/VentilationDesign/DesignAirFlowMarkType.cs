// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    /// <summary>
    /// What one <see cref="DesignAirFlowOverlayMark"/> reports: a space's aggregated design supply, its
    /// aggregated design extract, or the net of the two. Distinct from Approved Document F's own
    /// <c>PartFTerminalRole</c> - this is the DESIGN airflow authority
    /// (<c>VentilationTerminal.DesignFlowRate_Lps</c>), not a regulatory requirement.
    /// </summary>
    public enum DesignAirFlowMarkType
    {
        Supply,
        Extract,

        /// <summary>Supply minus extract. Positive means the space is net supplied; negative, net extracted.</summary>
        Net,
    }
}
