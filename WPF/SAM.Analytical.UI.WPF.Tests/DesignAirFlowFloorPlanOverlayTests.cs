// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.UI.WPF.Tests.Helpers;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <see cref="DesignAirFlowFloorPlanOverlay"/> reads a completely different authority from
    /// <see cref="PartFFloorPlanOverlay"/> - <c>VentilationTerminal.DesignFlowRate_Lps</c> through
    /// <c>Query.VentilationTerminalDesignDuty_Lps</c>, not a <c>PartFComplianceResult</c> - so these tests
    /// build their own fixtures with real ventilation terminals rather than a Part F assessment.
    /// <para>
    /// Reuses <see cref="PartFPlanModel"/> for the geometry (real rooms on a real plane, which is what the
    /// overlay sections to find an anchor) while adding terminals directly, since a design duty is
    /// deliberately unrelated to any Part F requirement.
    /// </para>
    /// </summary>
    public class DesignAirFlowFloorPlanOverlayTests
    {
        private static readonly Plane plane = Geometry.Spatial.Create.Plane(1.2);

        private static VentilationTerminal AddTerminal(PartFPlanModel model, string spaceName, FlowClassification flowClassification, double? designFlowRate_Lps, string name = null)
        {
            Space space = model.Space(spaceName);

            VentilationTerminal ventilationTerminal = new(name ?? string.Concat(spaceName, " ", flowClassification), flowClassification, designFlowRate_Lps);

            model.AdjacencyCluster.AddObject(ventilationTerminal);
            model.AdjacencyCluster.AddRelation(ventilationTerminal, space);

            return ventilationTerminal;
        }

        /// <summary>A studio with one supply terminal shows a single supply mark, inside the room.</summary>
        [Fact]
        public void OneSpace_OneSupplyTerminal_ShowsSupplyMark()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            AddTerminal(model, "Studio", FlowClassification.Supply, 45.0);

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            DesignAirFlowOverlayMark mark = Assert.Single(overlay.Marks);

            Assert.Equal(DesignAirFlowMarkType.Supply, mark.MarkType);
            Assert.Equal(45.0, mark.FlowRate_Lps);
            Assert.Equal(model.Space("Studio").Guid, mark.SpaceGuid);
        }

        /// <summary>A bathroom with one extract terminal shows a single extract mark, and no supply mark.</summary>
        [Fact]
        public void OneSpace_OneExtractTerminal_ShowsExtractMark()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Bathroom", 3).Close();

            AddTerminal(model, "Bathroom", FlowClassification.Extract, 8.0);

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            DesignAirFlowOverlayMark mark = Assert.Single(overlay.Marks);

            Assert.Equal(DesignAirFlowMarkType.Extract, mark.MarkType);
            Assert.Equal(8.0, mark.FlowRate_Lps);
        }

        /// <summary>
        /// A studio with both a supply and an extract terminal shows both marks, fanned to distinct
        /// positions so neither sits on top of the other - matching <see cref="PartFFloorPlanOverlay"/>'s
        /// own convention for two marks in one space.
        /// </summary>
        [Fact]
        public void OneSpace_SupplyAndExtract_ShowsBothMarksAtDistinctPositions()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            AddTerminal(model, "Studio", FlowClassification.Supply, 30.0);
            AddTerminal(model, "Studio", FlowClassification.Extract, 22.0);

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            Assert.Equal(2, overlay.Marks.Count);

            DesignAirFlowOverlayMark mark_Supply = Assert.Single(overlay.Marks.Where(x => x.MarkType == DesignAirFlowMarkType.Supply));
            DesignAirFlowOverlayMark mark_Extract = Assert.Single(overlay.Marks.Where(x => x.MarkType == DesignAirFlowMarkType.Extract));

            Assert.Equal(30.0, mark_Supply.FlowRate_Lps);
            Assert.Equal(22.0, mark_Extract.FlowRate_Lps);

            Assert.NotEqual(mark_Supply.Position.Y, mark_Extract.Position.Y, 6);
            Assert.Equal(mark_Supply.Position.X, mark_Extract.Position.X, 6);
        }

        /// <summary>
        /// Two supply terminals in one space aggregate to their sum - the space's design duty is the sum
        /// of its terminals, never the count of them, matching <c>VentilationTerminal</c>'s own doc comment.
        /// </summary>
        [Fact]
        public void OneSpace_TwoSupplyTerminals_AggregateToTheirSum()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            AddTerminal(model, "Studio", FlowClassification.Supply, 20.0, "Supply A");
            AddTerminal(model, "Studio", FlowClassification.Supply, 25.0, "Supply B");

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            DesignAirFlowOverlayMark mark = Assert.Single(overlay.Marks);

            Assert.Equal(DesignAirFlowMarkType.Supply, mark.MarkType);
            Assert.Equal(45.0, mark.FlowRate_Lps);
        }

        /// <summary>
        /// A space with a supply terminal and no extract terminal gets NO extract mark at all - never a
        /// mark reading "0 l/s", which would claim the design has established an extract duty of zero.
        /// </summary>
        [Fact]
        public void NoTerminalInADirection_ProducesNoMark_NotAnInventedZero()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            AddTerminal(model, "Studio", FlowClassification.Supply, 30.0);

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            Assert.DoesNotContain(overlay.Marks, x => x.MarkType == DesignAirFlowMarkType.Extract);
            Assert.DoesNotContain(overlay.Marks, x => x.FlowRate_Lps == 0);
        }

        /// <summary>
        /// Net is built only where BOTH directions are established - a space with only an extract terminal
        /// gets no net mark, because the missing supply side is not a known zero.
        /// </summary>
        [Fact]
        public void Net_NotBuilt_WhenOnlyOneDirectionIsEstablished()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Bathroom", 3).Close();

            AddTerminal(model, "Bathroom", FlowClassification.Extract, 8.0);

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane, showNet: true);

            Assert.DoesNotContain(overlay.Marks, x => x.MarkType == DesignAirFlowMarkType.Net);
        }

        /// <summary>Net is supply minus extract, and positive means net supplied.</summary>
        [Fact]
        public void Net_IsSupplyMinusExtract()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            AddTerminal(model, "Studio", FlowClassification.Supply, 45.0);
            AddTerminal(model, "Studio", FlowClassification.Extract, 22.0);

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane, showNet: true);

            DesignAirFlowOverlayMark mark_Net = Assert.Single(overlay.Marks.Where(x => x.MarkType == DesignAirFlowMarkType.Net));

            Assert.Equal(23.0, mark_Net.FlowRate_Lps);
        }

        /// <summary>
        /// Building the Ventilation Design overlay must not read or write a single Part F value - the two
        /// overlays are independent authorities and are meant to be able to disagree.
        /// </summary>
        [Fact]
        public void Build_DoesNotTouchPartFSpaceData()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            Space space = model.Space("Studio");

            //A Part F record, set independently of anything the design airflow overlay touches.
            space.SetValue(SpaceParameter.PartFLocalExtractMethod, "unchanged-part-f-marker");
            model.AdjacencyCluster.AddObject(space);

            AddTerminal(model, "Studio", FlowClassification.Supply, 150.0);

            DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            Assert.Equal("unchanged-part-f-marker", model.Space("Studio").GetValue<string>(SpaceParameter.PartFLocalExtractMethod));
        }

        /// <summary>
        /// Reopening a different model produces marks from THAT model only - nothing from the previous
        /// model's spaces or terminals survives, because <see cref="DesignAirFlowFloorPlanOverlay.Build"/>
        /// is a fresh, stateless factory call every time.
        /// </summary>
        [Fact]
        public void DifferentModel_ProducesIndependentMarks_NoStaleValues()
        {
            PartFPlanModel model_1 = new PartFPlanModel().Room("Studio", 8).Close();
            AddTerminal(model_1, "Studio", FlowClassification.Supply, 30.0);

            PartFPlanModel model_2 = new PartFPlanModel().Room("Bedroom", 4).Close();
            AddTerminal(model_2, "Bedroom", FlowClassification.Supply, 99.0);

            DesignAirFlowFloorPlanOverlay overlay_1 = DesignAirFlowFloorPlanOverlay.Build(model_1.AdjacencyCluster, plane);
            DesignAirFlowFloorPlanOverlay overlay_2 = DesignAirFlowFloorPlanOverlay.Build(model_2.AdjacencyCluster, plane);

            DesignAirFlowOverlayMark mark_1 = Assert.Single(overlay_1.Marks);
            DesignAirFlowOverlayMark mark_2 = Assert.Single(overlay_2.Marks);

            Assert.Equal(30.0, mark_1.FlowRate_Lps);
            Assert.Equal(99.0, mark_2.FlowRate_Lps);
            Assert.NotEqual(mark_1.SpaceGuid, mark_2.SpaceGuid);
        }

        /// <summary>
        /// Building the overlay for a large model stays practical: it sections each space once, not
        /// repeatedly, so a five-figure space count still builds in a few seconds rather than blowing up
        /// quadratically. A loose bound - this is a smoke test against an accidental O(n^2), not a
        /// performance benchmark.
        /// </summary>
        [Fact]
        public void LargeModel_BuildCompletesWithoutQuadraticBlowup()
        {
            const int roomCount = 600;

            PartFPlanModel model = new();

            string name_Previous = null;

            for (int i = 0; i < roomCount; i++)
            {
                string name = string.Format("Room {0}", i);

                model.Room(name, 3);

                //Closes the shared wall with the previous room, so every room's own outline is a closed
                //loop on the plan - Room() alone leaves the far side of each interior room open, and an
                //open outline sections to nothing.
                if (name_Previous is not null)
                {
                    model.Partition(name_Previous, name);
                }

                AddTerminal(model, name, FlowClassification.Supply, 10 + i);

                name_Previous = name;
            }

            model.Close();

            Stopwatch stopwatch = Stopwatch.StartNew();

            DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(model.AdjacencyCluster, plane);

            stopwatch.Stop();

            Assert.Equal(roomCount, overlay.Marks.Count);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), string.Format("Build of {0} spaces took {1}, which suggests a non-linear regression.", roomCount, stopwatch.Elapsed));
        }
    }
}
