// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.UI.WPF.Tests.Helpers;
using SAM.Geometry.Object;
using SAM.Geometry.Spatial;
using SAM.Geometry.UI;
using SAM.Geometry.UI.WPF;
using System;
using System.Windows.Media;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <see cref="PartFAirflowRenderer"/> and <see cref="DesignAirFlowRenderer"/> share one
    /// <see cref="FloorPlan2DControl.Overlay"/> - the control's only drawing hook for annotation. Each
    /// renderer must own a child <c>ContainerVisual</c> of its own and clear/rebuild only that, never the
    /// shared parent, or one renderer's redraw silently erases the other's. Both overlays being on at once
    /// is a normal, intended combination - comparing the Part F requirement against the current design
    /// airflow is one of the main reasons the design overlay exists - so these tests build BOTH renderers
    /// on the SAME control and assert neither can blank the other.
    /// <para>
    /// A real <see cref="FloorPlan2DControl"/> is constructed (an STA WPF requirement, hence
    /// <see cref="WpfCollection"/>), given a real <see cref="Plane"/> through
    /// <see cref="FloorPlan2DControl.Load(GeometryObjectModel)"/> with an empty model - enough for both
    /// renderers to place and draw marks, without needing a rendered visual tree or a shown window.
    /// </para>
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class OverlayOwnershipTests
    {
        private static readonly Plane plane = Geometry.Spatial.Create.Plane(1.2);

        /// <summary>
        /// Builds one space with both a Part F requirement (a supply terminal, 30 l/s continuous design)
        /// and a design ventilation terminal (45 l/s supply) - two different authorities, on the same
        /// space, so both renderers have something real of their own to draw.
        /// </summary>
        private static (FloorPlan2DControl Control, PartFAirflowRenderer PartFRenderer, DesignAirFlowRenderer DesignRenderer, ContainerVisual PartFContainer, ContainerVisual DesignContainer) Build()
        {
            PartFPlanModel model = new PartFPlanModel().Room("Studio", 8).Close();

            Space space = model.Space("Studio");

            PartFComplianceResult complianceResult = new("Flat 1")
            {
                Terminals =
                [
                    new PartFVentilationTerminalRequirement("Studio Supply", space.Guid, PartFTerminalRole.Supply)
                    {
                        SpaceName = space.Name,
                        ContinuousDesignFlowRate_Lps = 30,
                        IsRequired = true,
                    },
                ],
            };

            VentilationTerminal ventilationTerminal = new("Studio Design Supply", FlowClassification.Supply, 45.0);
            model.AdjacencyCluster.AddObject(ventilationTerminal);
            model.AdjacencyCluster.AddRelation(ventilationTerminal, space);

            FloorPlan2DControl control = new();

            //An empty model carrying only the view's plane - enough for FloorPlan2DControl.Load to set a
            //real Plane without needing any rendered geometry of its own.
            GeometryObjectModel geometryObjectModel = new();
            geometryObjectModel.SetValue(GeometryObjectModelParameter.ViewSettings, new TwoDimensionalViewSettings(
                Guid.NewGuid(), "Level 0 [1.2m]", plane, null, [typeof(Space)], Geometry.Object.Query.DefaultTextAppearance(), null));

            control.Load(geometryObjectModel);

            PartFAirflowRenderer partFRenderer = new(control)
            {
                ViewSettings = new PartFAirflowViewSettings { Enabled = true, ShowSupply = true, ShowValues = true },
            };

            //Captured immediately after construction: each renderer adds its own container to Overlay
            //exactly once, and the reference stays valid for the container's whole life even after its
            //own Children are cleared and rebuilt.
            ContainerVisual partFContainer = (ContainerVisual)control.Overlay.Children[0];

            DesignAirFlowRenderer designRenderer = new(control)
            {
                ViewSettings = new DesignAirFlowViewSettings { Enabled = true, ShowSupply = true },
            };

            ContainerVisual designContainer = (ContainerVisual)control.Overlay.Children[1];

            partFRenderer.Load(model.AdjacencyCluster, [complianceResult]);
            designRenderer.Load(model.AdjacencyCluster);

            return (control, partFRenderer, designRenderer, partFContainer, designContainer);
        }

        /// <summary>
        /// The failure this guards against: on the pre-fix renderer, the second Load's Draw called
        /// <c>floorPlan2DControl.Overlay.Children.Clear()</c> - the whole shared surface, not its own part
        /// of it - which wiped the first renderer's marks. Fails on 3aec9fe.
        /// </summary>
        [WpfFact]
        public void BothOverlaysEnabled_DrawSimultaneously_BothRenderersLeaveVisuals()
        {
            (FloorPlan2DControl control, _, _, ContainerVisual partFContainer, ContainerVisual designContainer) = Build();

            Assert.Equal(2, control.Overlay.Children.Count);
            Assert.NotEmpty(partFContainer.Children);
            Assert.NotEmpty(designContainer.Children);
        }

        /// <summary>
        /// A pan/zoom/resize redraws every visible renderer through <c>FloorPlan2DControl.ViewChanged</c>.
        /// Repeating that sequence - in both orders - must never leave one renderer's content erased by
        /// the other's redraw.
        /// </summary>
        [WpfFact]
        public void RepeatedRedraw_NeitherRendererRemovesTheOthersContent()
        {
            (_, PartFAirflowRenderer partFRenderer, DesignAirFlowRenderer designRenderer, ContainerVisual partFContainer, ContainerVisual designContainer) = Build();

            for (int i = 0; i < 3; i++)
            {
                partFRenderer.Draw();
                designRenderer.Draw();

                Assert.NotEmpty(partFContainer.Children);
                Assert.NotEmpty(designContainer.Children);
            }

            //The other order too - whichever renderer redraws last must not be the one left standing.
            designRenderer.Draw();
            partFRenderer.Draw();

            Assert.NotEmpty(partFContainer.Children);
            Assert.NotEmpty(designContainer.Children);
        }

        /// <summary>Disabling one overlay clears only that renderer's own container.</summary>
        [WpfFact]
        public void DisablingOneOverlay_RemovesOnlyItsOwnVisuals()
        {
            (_, PartFAirflowRenderer partFRenderer, DesignAirFlowRenderer designRenderer, ContainerVisual partFContainer, ContainerVisual designContainer) = Build();

            designRenderer.ViewSettings = new DesignAirFlowViewSettings { Enabled = false };

            Assert.Empty(designContainer.Children);
            Assert.NotEmpty(partFContainer.Children);

            //And the other way round: disabling the ONE STILL ENABLED overlay leaves the design overlay's
            //own (already-empty) container exactly as it was - neither renderer reaches into the other's.
            partFRenderer.ViewSettings = new PartFAirflowViewSettings { Enabled = false };

            Assert.Empty(partFContainer.Children);
            Assert.Empty(designContainer.Children);
        }

        /// <summary>Removing (detaching) one renderer's overlay does not touch the other's container.</summary>
        [WpfFact]
        public void ClearingOneRenderer_LeavesTheOtherIntact()
        {
            (_, PartFAirflowRenderer partFRenderer, DesignAirFlowRenderer designRenderer, ContainerVisual partFContainer, ContainerVisual designContainer) = Build();

            designRenderer.Clear();

            Assert.Empty(designContainer.Children);
            Assert.NotEmpty(partFContainer.Children);
        }
    }
}
