// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using SAM.Geometry.UI.WPF;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Draws the Ventilation Design floor-plan overlay over a 2D floor plan: each space's aggregated
    /// design supply/extract/net airflow, read from <c>VentilationTerminal.DesignFlowRate_Lps</c> through
    /// <see cref="DesignAirFlowFloorPlanOverlay"/>.
    /// <para>
    /// <b>Separate from <see cref="PartFAirflowRenderer"/> on purpose.</b> That renderer draws the Part F
    /// regulatory requirement from <c>PartFComplianceResult</c>; this one draws the model's current DESIGN
    /// airflow. The two read different authorities and can disagree after an optimisation round - that is
    /// the point, not a bug, and is why they stay two renderers rather than one with a mode switch.
    /// </para>
    /// <para>
    /// <b>Load is the expensive call</b> - it sections every space on the plan - so it runs only when the
    /// model, the plan or the view's visibility settings change. <see cref="Draw"/> is cheap and is what
    /// every pan, zoom and resize calls; it never re-reads the model. On a five-thousand-space model this is
    /// the difference between one section pass and one per frame.
    /// </para>
    /// </summary>
    public class DesignAirFlowRenderer
    {
        /// <summary>Tag text size [px], drawn at a fixed screen size so it stays legible at every zoom.</summary>
        private const double labelSize_Px = 11.5;

        /// <summary>Padding [px] inside a tag.</summary>
        private const double tagPadding_Px = 3;

        private static readonly Brush plateBrush = Plate();
        private static readonly Brush tagBorderBrush = TagBorder();

        private static readonly Color supplyColor = Color.FromRgb(0x1B, 0x6E, 0xC2);
        private static readonly Color extractColor = Color.FromRgb(0xC2, 0x5B, 0x1B);
        private static readonly Color netColor = Color.FromRgb(0x4A, 0x4A, 0x4A);

        private readonly FloorPlan2DControl floorPlan2DControl;

        private AdjacencyCluster adjacencyCluster;
        private DesignAirFlowFloorPlanOverlay overlay = DesignAirFlowFloorPlanOverlay.Build(null, null);

        /// <summary>
        /// Attaches to a 2D floor plan. The control's own <c>ViewChanged</c> only ever triggers a redraw -
        /// see <see cref="Draw"/>.
        /// </summary>
        public DesignAirFlowRenderer(FloorPlan2DControl floorPlan2DControl)
        {
            this.floorPlan2DControl = floorPlan2DControl;

            if (this.floorPlan2DControl is not null)
            {
                this.floorPlan2DControl.ViewChanged += FloorPlan2DControl_ViewChanged;
            }
        }

        /// <summary>
        /// How the overlay is presented. Never null; assigning replaces it and redraws - visibility is the
        /// only thing a settings change can affect, since positions and rates come from <see cref="Load"/>.
        /// </summary>
        public DesignAirFlowViewSettings ViewSettings
        {
            get
            {
                return designAirFlowViewSettings;
            }

            set
            {
                designAirFlowViewSettings = value ?? new DesignAirFlowViewSettings();

                Draw();
            }
        }

        private DesignAirFlowViewSettings designAirFlowViewSettings = new();

        /// <summary>Every mark drawn, in a stable order.</summary>
        public List<DesignAirFlowOverlayMark> Marks
        {
            get { return overlay.Marks; }
        }

        /// <summary>What could not be placed on this plan, and why.</summary>
        public List<string> Unplaced
        {
            get { return overlay.Unplaced; }
        }

        /// <summary>
        /// Reads every space's design duty and works out where its marks go. The expensive call: it
        /// sections every space, so it is made when the model or the plan changes - not when the view moves
        /// and not when a visibility checkbox is toggled.
        /// </summary>
        /// <param name="adjacencyCluster">The model the plan is drawn from.</param>
        public void Load(AdjacencyCluster adjacencyCluster)
        {
            this.adjacencyCluster = adjacencyCluster;

            Plane plane = floorPlan2DControl?.Plane;

            overlay = DesignAirFlowFloorPlanOverlay.Build(adjacencyCluster, plane, ViewSettings.ShowNet);

            Draw();
        }

        /// <summary>
        /// Redraws the overlay against the current view transform. Cheap and called often - on every pan,
        /// zoom, resize and visibility toggle. Nothing here re-reads the model or moves an anchor.
        /// </summary>
        public void Draw()
        {
            System.Windows.Media.ContainerVisual containerVisual = floorPlan2DControl?.Overlay;
            if (containerVisual is null)
            {
                return;
            }

            containerVisual.Children.Clear();

            if (!ViewSettings.Enabled || overlay.Marks.Count == 0)
            {
                return;
            }

            System.Windows.Media.Matrix matrix = floorPlan2DControl.WorldToScreen;

            foreach (DesignAirFlowOverlayMark mark in overlay.Marks)
            {
                if (!Visible(mark))
                {
                    continue;
                }

                DrawingVisual drawingVisual = new();

                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    DrawTag(drawingContext, mark, matrix);
                }

                containerVisual.Children.Add(drawingVisual);
            }
        }

        /// <summary>Clears everything drawn and forgets the model.</summary>
        public void Clear()
        {
            adjacencyCluster = null;
            overlay = DesignAirFlowFloorPlanOverlay.Build(null, null);

            Draw();
        }

        /// <summary>Stops listening to the control. Call when the view it draws on goes away.</summary>
        public void Detach()
        {
            if (floorPlan2DControl is not null)
            {
                floorPlan2DControl.ViewChanged -= FloorPlan2DControl_ViewChanged;
            }
        }

        private void FloorPlan2DControl_ViewChanged(object sender, EventArgs e)
        {
            Draw();
        }

        private bool Visible(DesignAirFlowOverlayMark mark)
        {
            return mark.MarkType switch
            {
                DesignAirFlowMarkType.Supply => ViewSettings.ShowSupply,
                DesignAirFlowMarkType.Extract => ViewSettings.ShowExtract,
                DesignAirFlowMarkType.Net => ViewSettings.ShowNet,
                _ => true,
            };
        }

        private static void DrawTag(DrawingContext drawingContext, DesignAirFlowOverlayMark mark, System.Windows.Media.Matrix matrix)
        {
            Color color = mark.MarkType switch
            {
                DesignAirFlowMarkType.Supply => supplyColor,
                DesignAirFlowMarkType.Extract => extractColor,
                _ => netColor,
            };

            Brush brush = new SolidColorBrush(color);
            brush.Freeze();

            FormattedText formattedText = Text(mark.Label, brush);

            System.Windows.Point point_Anchor = matrix.Transform(new System.Windows.Point(mark.Position.X, mark.Position.Y));

            System.Windows.Point point_Text = new(point_Anchor.X + 6, point_Anchor.Y - (formattedText.Height / 2));

            Rect rect_Tag = new(
                point_Text.X - tagPadding_Px,
                point_Text.Y - (tagPadding_Px / 2),
                formattedText.Width + (tagPadding_Px * 2),
                formattedText.Height + tagPadding_Px);

            drawingContext.DrawRectangle(plateBrush, TagPen(), rect_Tag);

            drawingContext.DrawText(formattedText, point_Text);
        }

        private static FormattedText Text(string text, Brush brush)
        {
            return new FormattedText(
                text ?? string.Empty,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                labelSize_Px,
                brush,
                96);
        }

        private static Brush Plate()
        {
            SolidColorBrush result = new(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
            result.Freeze();
            return result;
        }

        private static Brush TagBorder()
        {
            SolidColorBrush result = new(Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A));
            result.Freeze();
            return result;
        }

        private static Pen TagPen()
        {
            Pen result = new(tagBorderBrush, 0.7);
            result.Freeze();
            return result;
        }
    }
}
