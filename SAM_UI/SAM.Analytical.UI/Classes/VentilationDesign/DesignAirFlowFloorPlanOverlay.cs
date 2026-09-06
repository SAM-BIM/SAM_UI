// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// Where each Ventilation Design mark belongs on a floor plan, in that plan's own 2D coordinates.
    /// <para>
    /// This class works out <b>positions</b> and nothing else. Every rate it carries is read from
    /// <c>Query.VentilationTerminals</c> / <c>Query.VentilationTerminalDesignDuty_Lps</c> unchanged - the
    /// same DESIGN airflow authority Iteration 2B writes to, on <c>VentilationTerminal.DesignFlowRate_Lps</c>.
    /// No Part F regulatory value is read or written here, and none of this is written back onto
    /// <c>Space</c> - <see cref="PartFFloorPlanOverlay"/> and its <c>PartFComplianceResult</c> are a
    /// completely separate data path, so both overlays can disagree after an optimisation round and both
    /// be right: one shows what Part F requires, this shows what the model is currently designed to move.
    /// </para>
    /// <para>
    /// It is deliberately free of any user interface dependency: no WPF, no brushes, no screen coordinates.
    /// It answers "where, in the building, does this space's design airflow value go", which is the part
    /// worth testing, and leaves drawing to the renderer.
    /// </para>
    /// <para>
    /// A space with no terminal in a direction gets no mark in that direction - not a mark reading "0 l/s".
    /// Absence of a terminal and an authored zero design duty are different answers, and only the model can
    /// tell them apart; see <c>VentilationTerminal.DesignFlowRate_Lps</c>.
    /// </para>
    /// </summary>
    public class DesignAirFlowFloorPlanOverlay
    {
        /// <summary>Spacing [m] between two marks anchored in the same space, so they do not overlap.</summary>
        public const double MarkSpacing_M = 0.55;

        private DesignAirFlowFloorPlanOverlay()
        {
        }

        /// <summary>Every mark to draw, in a stable order.</summary>
        public List<DesignAirFlowOverlayMark> Marks { get; private set; } = [];

        /// <summary>
        /// What could not be placed on this plan, and why - a space with a design duty but no outline on
        /// this plan. Reported rather than approximated, matching <see cref="PartFFloorPlanOverlay.Unplaced"/>.
        /// </summary>
        public List<string> Unplaced { get; private set; } = [];

        /// <summary>
        /// Builds the overlay for every space in the model that has an established design supply or
        /// extract duty.
        /// </summary>
        /// <param name="adjacencyCluster">The model the plan is drawn from.</param>
        /// <param name="plane">The floor plan's section plane. Marks are in this plane's 2D coordinates.</param>
        /// <param name="showNet">Whether a net mark is built for a space that has both a supply and an extract duty.</param>
        public static DesignAirFlowFloorPlanOverlay Build(AdjacencyCluster adjacencyCluster, Plane plane, bool showNet = false)
        {
            DesignAirFlowFloorPlanOverlay result = new();

            if (adjacencyCluster is null || plane is null)
            {
                return result;
            }

            Dictionary<Guid, Point2D> dictionary_Anchor = [];

            foreach (Space space in adjacencyCluster.GetSpaces() ?? [])
            {
                if (space is null)
                {
                    continue;
                }

                List<VentilationTerminal> ventilationTerminals = adjacencyCluster.VentilationTerminals(space);

                double? supply_Lps = ventilationTerminals?.VentilationTerminalDesignDuty_Lps(FlowClassification.Supply);
                double? extract_Lps = ventilationTerminals?.VentilationTerminalDesignDuty_Lps(FlowClassification.Extract);

                //Nothing serves this space in either direction: no mark, not a mark reading "0 l/s".
                if (supply_Lps is null && extract_Lps is null)
                {
                    continue;
                }

                Point2D point2D_Anchor = Anchor(adjacencyCluster, plane, dictionary_Anchor, space);
                if (point2D_Anchor is null)
                {
                    result.Unplaced.Add(string.Format("'{0}' has no outline on this floor plan, so its design airflow mark(s) were not drawn. Check that the space reaches the plan's cut level.", space.Name));
                    continue;
                }

                List<(DesignAirFlowMarkType MarkType, double? FlowRate_Lps)> entries = [];

                if (supply_Lps is not null)
                {
                    entries.Add((DesignAirFlowMarkType.Supply, supply_Lps));
                }

                if (extract_Lps is not null)
                {
                    entries.Add((DesignAirFlowMarkType.Extract, extract_Lps));
                }

                //Net is only meaningful where BOTH directions are established. A space with an extract duty
                //and no supply terminal at all has no "net" the model has actually said anything about -
                //treating the missing side as zero would be inventing a value the same way a bare "0 l/s"
                //terminal mark would.
                if (showNet && supply_Lps is not null && extract_Lps is not null)
                {
                    entries.Add((DesignAirFlowMarkType.Net, supply_Lps.Value - extract_Lps.Value));
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    (DesignAirFlowMarkType markType, double? flowRate_Lps) = entries[i];

                    //Fanned vertically about the anchor, matching PartFFloorPlanOverlay: deterministic, so
                    //the same model always draws the same plan, and no mark ever sits on top of another.
                    double offset = (i - ((entries.Count - 1) / 2.0)) * MarkSpacing_M;

                    result.Marks.Add(new DesignAirFlowOverlayMark
                    {
                        SpaceGuid = space.Guid,
                        SpaceName = space.Name,
                        MarkType = markType,
                        FlowRate_Lps = flowRate_Lps,
                        Position = new Point2D(point2D_Anchor.X, point2D_Anchor.Y + offset),
                        Label = Label(markType, flowRate_Lps),
                    });
                }
            }

            return result;
        }

        /// <summary>The marks belonging to one space.</summary>
        public List<DesignAirFlowOverlayMark> MarksOf(Guid spaceGuid)
        {
            return [.. Marks.Where(x => x.SpaceGuid == spaceGuid)];
        }

        /// <summary>
        /// The tag text for one mark: the direction's abbreviation and its rate, or an explicit statement
        /// that no duty has been established - never a bare "0 l/s" standing in for "no terminal".
        /// </summary>
        private static string Label(DesignAirFlowMarkType markType, double? flowRate_Lps)
        {
            string abbreviation = markType switch
            {
                DesignAirFlowMarkType.Supply => "SUP",
                DesignAirFlowMarkType.Extract => "EXT",
                DesignAirFlowMarkType.Net => "NET",
                _ => string.Empty,
            };

            if (flowRate_Lps is null)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} not established", abbreviation);
            }

            return markType == DesignAirFlowMarkType.Net
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1}{2:0.0} l/s", abbreviation, flowRate_Lps.Value >= 0 ? "+" : string.Empty, flowRate_Lps.Value)
                : string.Format(CultureInfo.InvariantCulture, "{0} {1:0.0} l/s", abbreviation, flowRate_Lps.Value);
        }

        /// <summary>
        /// A point inside the space's own outline on this plan, or null where the space does not reach it.
        /// Same rule as <see cref="PartFFloorPlanOverlay"/>'s own anchor: the outline's internal point, not
        /// its centroid, so an L-shaped room is anchored inside itself.
        /// </summary>
        private static Point2D Anchor(AdjacencyCluster adjacencyCluster, Plane plane, Dictionary<Guid, Point2D> dictionary_Anchor, Space space)
        {
            if (dictionary_Anchor.TryGetValue(space.Guid, out Point2D result))
            {
                return result;
            }

            List<Face2D> face2Ds = adjacencyCluster.SpaceSectionFace2Ds(space, plane);

            Face2D face2D = face2Ds?.Where(x => x is not null).OrderByDescending(x => x.GetArea()).FirstOrDefault();

            result = face2D?.GetInternalPoint2D();

            dictionary_Anchor[space.Guid] = result;

            return result;
        }
    }
}
