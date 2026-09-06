// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Planar;
using System;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// One drawn value on the Ventilation Design floor-plan overlay: a space's aggregated design supply,
    /// design extract, or net, positioned in a floor plan's own 2D coordinates.
    /// <para>
    /// Carries no engineering authority of its own. <see cref="FlowRate_Lps"/> is read unchanged from
    /// <c>Query.VentilationTerminalDesignDuty_Lps</c>; nothing here recomputes, rounds or re-derives it.
    /// </para>
    /// </summary>
    public class DesignAirFlowOverlayMark
    {
        /// <summary>The space this mark belongs to.</summary>
        public Guid SpaceGuid { get; set; }

        /// <summary>The space's name, kept alongside the guid so a mark can be labelled without a model lookup.</summary>
        public string SpaceName { get; set; }

        public DesignAirFlowMarkType MarkType { get; set; }

        /// <summary>
        /// The aggregated design duty [l/s], or null. Null means no terminal in this direction serves the
        /// space - not that the duty is zero. See <c>Query.VentilationTerminalDesignDuty_Lps</c>.
        /// </summary>
        public double? FlowRate_Lps { get; set; }

        /// <summary>Where the mark sits on the floor plan, in the plan's own 2D coordinates.</summary>
        public Point2D Position { get; set; }

        /// <summary>The formatted text a renderer draws - "SUP 45.0 l/s", "EXT not established", etc.</summary>
        public string Label { get; set; }
    }
}
