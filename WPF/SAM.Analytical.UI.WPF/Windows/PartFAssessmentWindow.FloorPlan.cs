// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Core.UI.WPF;
using SAM.Geometry.Object;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;
using SAM.Geometry.Spatial;
using SAM.Geometry.UI;
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
    /// The Part F airflow view: the dwelling's REAL floor plan with the airflow drawn over it.
    /// <para>
    /// This replaced an abstract node-and-edge diagram that laid rooms out in three columns of its own
    /// invention. That diagram could show which room passed air to which, but it could not show an
    /// engineer where the air physically goes, and its arrows crossed spaces that do not adjoin. The plan
    /// explains where the air moves; the text schematic on the Schematic tab explains how the calculation
    /// got there. Both are kept, and both read the same numbers.
    /// </para>
    /// <para>
    /// The plan itself is the shared <see cref="FloorPlan2DControl"/>, loaded from the shared
    /// <c>ToSAM_GeometryObjectModel</c> pipeline, so spaces, walls, doors and text are drawn exactly as
    /// they are everywhere else in SAM and no second room-layout algorithm exists. Only the airflow marks
    /// are ours, and they go on the control's overlay layer.
    /// </para>
    /// <para>
    /// Positions come from <see cref="PartFFloorPlanOverlay"/>, which is user-interface-free and unit
    /// tested. Nothing in this file computes a rate, a direction or a status.
    /// </para>
    /// </summary>
    public partial class PartFAssessmentWindow
    {
        /// <summary>Height [m] above a level's floor that the plan is cut at, the usual plan convention.</summary>
        private const double cutHeight_M = 1.2;

        /// <summary>Screen length [px] of an arrowhead, held constant so it stays legible at every zoom.</summary>
        private const double arrowHead_Px = 9;

        /// <summary>
        /// Padding [px] inside a tag, at the annotation scale. Compact: a tag is a label on a drawing, not a
        /// panel, and it has to sit inside a room without covering it.
        /// </summary>
        private const double tagPadding_Px = 3;

        /// <summary>The tag's white background. See <see cref="Plate"/>.</summary>
        private static readonly Brush plateBrush = Plate();

        /// <summary>The tag's border. See <see cref="TagPen"/>.</summary>
        private static readonly Brush tagBorderBrush = TagBorder();

        /// <summary>
        /// Tag text size [px] <b>on the sheet</b> - the size it is measured at, and the size it draws at when
        /// the view is at the annotation scale. See <see cref="AnnotationScale"/>.
        /// </summary>
        private const double labelSize_Px = 11.5;

        /// <summary>Caption text size [px] on the sheet, smaller so a caption reads as a qualifier.</summary>
        private const double captionSize_Px = 9.5;

        private AnalyticalModel analyticalModel;
        private PartFFloorPlanOverlay overlay;
        private List<double> elevations = [];

        /// <summary>
        /// Where every tag goes, from the shared placement engine through <see cref="PartFTagPlacement"/>.
        /// Keyed on the mark so drawing is a lookup rather than a second search.
        /// </summary>
        private Dictionary<PartFOverlayMark, PartFTagPlacementResult> placements = [];

        /// <summary>
        /// Text the plan itself drew that a tag must not cover - room names, door labels - as boxes in the
        /// view plane's own coordinates. Rebuilt with the plan, not with the view: the words do not move when
        /// the camera does. See <see cref="TextObstacle2Ds"/>.
        /// </summary>
        private List<IClosed2D> textObstacle2Ds = [];

        /// <summary>
        /// The drawing scale the annotation is laid out for, as its denominator: 50 means 1:50. Setting it
        /// lays the tags out again; the camera never does.
        /// <para>
        /// This is the distinction between an annotation scale and a viewport. A tag is a fixed size on the
        /// sheet, so something has to say how much building it covers - and if that were the current zoom,
        /// then panning and zooming around a plan would keep rearranging an engineering drawing's labels,
        /// which is an auto-arrange command nobody issued. It is a property of the drawing instead, it is
        /// saved with the view (<see cref="PartFAirflowViewSettings.AnnotationScale"/>), and it is the same
        /// on any machine at any window size.
        /// </para>
        /// </summary>
        public double AnnotationScale
        {
            get
            {
                return annotationScale;
            }

            set
            {
                double annotationScale_New = value > 0 ? value : PartFTagPlacement.DefaultAnnotationScale;

                if (annotationScale_New == annotationScale)
                {
                    return;
                }

                annotationScale = annotationScale_New;

                Place();

                DrawMarks();
            }
        }

        private double annotationScale = PartFTagPlacement.DefaultAnnotationScale;

        /// <summary>
        /// True while the plan is being rebuilt, when the view's own move events must not trigger a
        /// placement: the overlay they would run against is the one about to be replaced.
        /// </summary>
        private bool loading_FloorPlan;

        /// <summary>
        /// The model the plan is drawn from. Without one the window still works - every grid, the report
        /// and the schematic are unaffected - and the airflow tab says so instead of drawing a diagram of
        /// its own devising.
        /// </summary>
        public AnalyticalModel AnalyticalModel
        {
            get
            {
                return analyticalModel;
            }

            set
            {
                analyticalModel = value;
                LoadFloorPlan();
            }
        }

        /// <summary>The level currently shown, or null where the dwelling sits on one level.</summary>
        private double? Elevation
        {
            get
            {
                int index = ComboBox_Level.SelectedIndex;

                return index >= 0 && index < elevations.Count ? elevations[index] : null;
            }
        }

        /// <summary>
        /// Connects the plan. Called once from the constructor.
        /// </summary>
        private void InitialiseFloorPlan()
        {
            //The overlay lives in screen coordinates, so it has to be redrawn whenever the view moves.
            FloorPlan.ViewChanged += FloorPlan_ViewChanged;

            //Selection comes from the plan's own hit testing, on the real spaces and panels, so clicking a
            //room selects that room rather than whatever the overlay happened to draw on top of it.
            FloorPlan.ObjectSelectionChanged += FloorPlan_ObjectSelectionChanged;
            FloorPlan.MouseLeftButtonUp += FloorPlan_MouseLeftButtonUp;
        }

        /// <summary>
        /// Re-reads the values at the current operating condition without touching a single ANCHOR, and
        /// redraws. The topology is not recomputed.
        /// <para>
        /// The tags are placed again, because the text changes: "8 l/s" at the continuous condition can
        /// become "13 l/s" at the high one, and a wider tag needs more room than the old layout left it.
        /// The anchors - what the marks point at - are untouched, which is the expensive part.
        /// </para>
        /// </summary>
        private void Refresh()
        {
            overlay?.Refresh(Selected?.ComplianceResult, OperatingMode);

            Place();

            DrawMarks();
        }

        /// <summary>
        /// The camera moved - a pan, a zoom, a resize. <b>Redraw only.</b>
        /// <para>
        /// Deliberately does not place anything, and must not start to. The tags are laid out for the view's
        /// <see cref="AnnotationScale"/>, in the plane's own coordinates, so moving the camera cannot change
        /// where any of them belongs - it only changes where that is on screen. Solving here would turn
        /// ordinary navigation into an implicit auto-arrange command: look around a plan, and the annotation
        /// of an engineering drawing rearranges itself behind you. It would also crawl, since this fires on
        /// every mouse move of a drag.
        /// </para>
        /// </summary>
        private void FloorPlan_ViewChanged(object sender, EventArgs e)
        {
            if (loading_FloorPlan)
            {
                return;
            }

            DrawMarks();
        }

        // ------------------------------------------------------------------
        // Loading
        // ------------------------------------------------------------------

        /// <summary>
        /// Rebuilds the plan for the selected dwelling and level. Called when either changes - NOT when
        /// the operating mode changes, which only alters the numbers on the marks.
        /// </summary>
        private void LoadFloorPlan()
        {
            //Loading the plan and zooming it to fit both move the view, and this method places the tags at
            //the end anyway. Without this the view's own events would run a placement against the overlay of
            //the dwelling being replaced - wasted work on a large model, and a flicker of the wrong marks.
            loading_FloorPlan = true;

            try
            {
                LoadFloorPlan_Geometry();
            }
            finally
            {
                loading_FloorPlan = false;
            }
        }

        private void LoadFloorPlan_Geometry()
        {
            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;
            PartFComplianceResult complianceResult = Selected?.ComplianceResult;

            if (adjacencyCluster is null || complianceResult is null)
            {
                FloorPlan.Clear();
                overlay = null;
                placements = [];
                textObstacle2Ds = [];

                TextBlock_PlanMessage.Text = adjacencyCluster is null
                    ? "No analytical model was supplied to this window, so the floor plan cannot be drawn. Every schedule, the checks and the report are unaffected, and the Schematic tab shows the airflow as text."
                    : "This dwelling has no assessment to draw.";

                TextBlock_PlanMessage.Visibility = Visibility.Visible;

                DrawMarks();
                return;
            }

            TextBlock_PlanMessage.Visibility = Visibility.Collapsed;

            LoadLevels(adjacencyCluster, complianceResult);

            double elevation = Elevation ?? 0;

            Plane plane = Geometry.Spatial.Create.Plane(elevation + cutHeight_M);

            TwoDimensionalViewSettings twoDimensionalViewSettings = new(
                Guid.NewGuid(),
                string.Format("Part F {0}", Selected?.Name ?? "dwelling"),
                plane,
                null,
                [typeof(Space), typeof(Panel), typeof(Aperture)],
                Geometry.Object.Query.DefaultTextAppearance(),
                null);

            GeometryObjectModel geometryObjectModel = analyticalModel.ToSAM_GeometryObjectModel(twoDimensionalViewSettings);

            FloorPlan.Load(geometryObjectModel);
            FloorPlan.ZoomExtents();

            //Positions are worked out once per plan, not once per repaint: the topology does not change
            //when the operating mode does, and recomputing every section on a mode switch would make a
            //large model crawl for no reason.
            overlay = PartFFloorPlanOverlay.Build(adjacencyCluster, complianceResult, plane, OperatingMode);

            textObstacle2Ds = TextObstacle2Ds(geometryObjectModel, plane);

            Place();

            DrawMarks();
        }

        /// <summary>
        /// The bounds of every piece of text the plan itself has drawn - room names, door labels, anything
        /// else the view puts on the drawing - in the plan's own 2D coordinates.
        /// <para>
        /// <b>Protected geometry.</b> A room name is the primary annotation of a space and a Part F tag is
        /// engineering annotation near it; the tag gives way, never the other way round. So these go to the
        /// placement as obstacles.
        /// </para>
        /// <para>
        /// Measured, not assumed. The positions come from the loaded geometry, because the plan's own labels
        /// were placed by this same shared solver moments earlier and can sit a long way from the space's
        /// anchor - reserving a band around the anchor, as this used to, guards the wrong place. The widths
        /// come from the text and its own <c>TextAppearance</c>, through the same measurement the plan used
        /// when it laid the label out, so the box a tag is kept out of is the box the words actually fill.
        /// </para>
        /// <para>
        /// In the view plane's units and independent of both the camera and the annotation scale, because the
        /// text is sized in the model's own units - so this is computed once with the plan.
        /// </para>
        /// </summary>
        private static List<IClosed2D> TextObstacle2Ds(GeometryObjectModel geometryObjectModel, Plane plane)
        {
            List<IClosed2D> result = [];

            if (geometryObjectModel is null || plane is null)
            {
                return result;
            }

            foreach (Geometry3DObjectCollection geometry3DObjectCollection in geometryObjectModel.GetSAMGeometryObjects<Geometry3DObjectCollection>() ?? [])
            {
                foreach (ISAMGeometry3DObject sAMGeometry3DObject in geometry3DObjectCollection ?? [])
                {
                    //Text the plan blanked is text its own label solve could not place; there is nothing drawn
                    //to keep clear of.
                    if (sAMGeometry3DObject is not Text3DObject text3DObject || string.IsNullOrWhiteSpace(text3DObject.Text))
                    {
                        continue;
                    }

                    Point2D point2D = Geometry.Spatial.Query.Convert(plane, text3DObject.Plane?.Origin);

                    double height = text3DObject.TextAppearance?.Height ?? 0;

                    if (point2D is null || height <= 0)
                    {
                        continue;
                    }

                    //The same call the plan makes to size its own label, so the two agree.
                    double width = SAM.Core.UI.WPF.Query.Width(text3DObject.Text, new System.Drawing.Font(text3DObject.TextAppearance.FontFamilyName, System.Convert.ToSingle(height)), height);
                    if (width <= 0)
                    {
                        continue;
                    }

                    //Centred on the drawn position: the plan's label solver returns its rectangle's centroid,
                    //and that centroid is what became this text's origin.
                    result.Add(new Rectangle2D(new Point2D(point2D.X - (width / 2), point2D.Y - (height / 2)), width, height));
                }
            }

            return result;
        }

        /// <summary>
        /// Places every visible tag, by handing them all to the shared engine through
        /// <see cref="PartFTagPlacement"/>.
        /// <para>
        /// Called on a change of dwelling, level, operating mode, visibility toggle or view scale - never on
        /// a pan and never on a repaint. Nothing here decides where a tag goes; this assembles what the
        /// engine needs and stores what it decided.
        /// </para>
        /// </summary>
        private void Place()
        {
            placements = [];

            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;

            if (overlay is null || adjacencyCluster is null || FloorPlan.Plane is null)
            {
                return;
            }

            //The ANNOTATION scale, never the view transform: see AnnotationScale. Everything below is a
            //function of the model and of this number, so the layout is the same wherever the camera is.
            double scale = PartFTagPlacement.PixelsPerMetre(AnnotationScale);

            Dictionary<Guid, IClosed2D> dictionary_LimitArea = [];

            List<PartFTagPlacementItem> partFTagPlacementItems = [];

            foreach (PartFOverlayMark mark in overlay.Marks)
            {
                if (!Visible(mark))
                {
                    continue;
                }

                Size(mark, out double width_Px, out double height_Px);

                partFTagPlacementItems.Add(new PartFTagPlacementItem()
                {
                    ObjectGuid = mark.AnnotationGuid,
                    AnnotationType = mark.AnnotationType,
                    Priority = PartFTagPlacement.Priority(mark),
                    Anchor2D = mark.End,

                    //Text is measured in pixels and solved in metres, at the annotation scale: a layout held
                    //in pixels would move every time the window was resized, and one held at the viewport's
                    //scale would move every time somebody zoomed. See PartFAnnotationOverride.
                    Width = width_Px / scale,
                    Height = height_Px / scale,

                    //A terminal tag's centre stays in its own room, so it cannot end up reading as the room
                    //next door's. A transfer tag belongs to the opening between two spaces and so to neither
                    //outline, and gets none.
                    LimitArea = mark.IsTransfer ? null : LimitArea(adjacencyCluster, dictionary_LimitArea, mark.SpaceGuid),

                    Tag = mark,
                });
            }

            //The room names and door labels the plan drew are protected: the tags go round them.
            foreach (PartFTagPlacementResult partFTagPlacementResult in PartFTagPlacement.Solve(partFTagPlacementItems, Overrides(), textObstacle2Ds))
            {
                if (partFTagPlacementResult.Tag is PartFOverlayMark mark)
                {
                    placements[mark] = partFTagPlacementResult;
                }
            }
        }

        /// <summary>
        /// The space's own section outline on this plan, cached per space for the length of one placement.
        /// </summary>
        private IClosed2D LimitArea(AdjacencyCluster adjacencyCluster, Dictionary<Guid, IClosed2D> dictionary_LimitArea, Guid guid_Space)
        {
            if (dictionary_LimitArea.TryGetValue(guid_Space, out IClosed2D result))
            {
                return result;
            }

            Space space = adjacencyCluster.GetSpaces()?.Find(x => x is not null && x.Guid == guid_Space);

            //The largest piece, matching the anchor: a room cut into a big part and a sliver is tagged in the
            //big part, so constraining the tag to the sliver would leave it unplaceable.
            result = space is null
                ? null
                : adjacencyCluster.SpaceSectionFace2Ds(space, FloorPlan.Plane)?.Where(x => x is not null).OrderByDescending(x => x.GetArea()).FirstOrDefault();

            dictionary_LimitArea[guid_Space] = result;

            return result;
        }

        /// <summary>
        /// Manual tag positions for this view. None yet - the drag-to-move user interface is still to come -
        /// but the placement takes them as obstacles rather than omissions from the day it does, so an
        /// automatic tag can never be dropped on top of one somebody moved deliberately.
        /// </summary>
        private static List<PartFAnnotationOverride> Overrides()
        {
            return [];
        }

        /// <summary>
        /// Fills the level selector from the dwelling's OWN spaces, so a single-storey flat in a
        /// multi-storey block offers one level rather than the whole building's.
        /// </summary>
        private void LoadLevels(AdjacencyCluster adjacencyCluster, PartFComplianceResult partFComplianceResult)
        {
            HashSet<Guid> guids = [.. (partFComplianceResult.Terminals ?? []).Select(x => x.SpaceGuid)];

            List<double> result = [];

            foreach (Space space in adjacencyCluster.GetSpaces() ?? [])
            {
                if (space is null || !guids.Contains(space.Guid))
                {
                    continue;
                }

                double elevation = space.MinElevation(adjacencyCluster);
                if (double.IsNaN(elevation) || double.IsInfinity(elevation))
                {
                    continue;
                }

                //Rounded to the millimetre before grouping: two spaces on one floor slab can differ by a
                //rounding artefact, and an untidied list would offer the same level twice.
                elevation = System.Math.Round(elevation, 3);

                if (!result.Contains(elevation))
                {
                    result.Add(elevation);
                }
            }

            result.Sort();

            if (result.SequenceEqual(elevations))
            {
                return;
            }

            elevations = result;

            bool loading_Previous = loading;
            loading = true;

            ComboBox_Level.Items.Clear();
            foreach (double elevation in elevations)
            {
                ComboBox_Level.Items.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.###} m", elevation));
            }

            loading = loading_Previous;

            if (ComboBox_Level.Items.Count != 0)
            {
                ComboBox_Level.SelectedIndex = 0;
            }

            //A dwelling on one level has no level to choose, and a selector with one entry is clutter.
            ComboBox_Level.Visibility = elevations.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            TextBlock_Level.Visibility = ComboBox_Level.Visibility;
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        /// <summary>
        /// Redraws the overlay against the current view transform. Cheap and called often - on every pan,
        /// zoom, resize, toggle and operating-mode change.
        /// </summary>
        private void DrawMarks()
        {
            FloorPlan.Overlay.Children.Clear();

            if (overlay is null)
            {
                return;
            }

            System.Windows.Media.Matrix matrix = FloorPlan.WorldToScreen;

            DrawContextVeil();

            foreach (PartFOverlayMark mark in overlay.Marks)
            {
                if (!Visible(mark))
                {
                    continue;
                }

                DrawingVisual drawingVisual = new();

                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    //The layout was solved in the plane, on a change of input; this only transforms it.
                    placements.TryGetValue(mark, out PartFTagPlacementResult partFTagPlacementResult);

                    Draw(drawingContext, mark, matrix, partFTagPlacementResult);
                }

                FloorPlan.Overlay.Children.Add(drawingVisual);
            }
        }

        /// <summary>
        /// Fades the plan outside the selected dwelling, so the flat being assessed reads first and the
        /// building around it stays as context rather than disappearing.
        /// <para>
        /// A translucent veil over the other spaces, drawn on the overlay, rather than a filtered model.
        /// Filtering would mean building a second, reduced <c>AnalyticalModel</c> for every dwelling and
        /// every level, and it would take the neighbouring walls away - which is exactly the context an
        /// engineer needs to recognise where the flat sits.
        /// </para>
        /// </summary>
        private void DrawContextVeil()
        {
            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;
            PartFComplianceResult complianceResult = Selected?.ComplianceResult;

            if (CheckBox_Context.IsChecked != true || adjacencyCluster is null || complianceResult is null || FloorPlan.Plane is null)
            {
                return;
            }

            HashSet<Guid> guids = [.. (complianceResult.Terminals ?? []).Select(x => x.SpaceGuid)];

            SolidColorBrush brush = new(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF));
            brush.Freeze();

            System.Windows.Media.Matrix matrix = FloorPlan.WorldToScreen;

            DrawingVisual drawingVisual = new();

            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                foreach (Space space in adjacencyCluster.GetSpaces() ?? [])
                {
                    if (space is null || guids.Contains(space.Guid))
                    {
                        continue;
                    }

                    foreach (Face2D face2D in adjacencyCluster.SpaceSectionFace2Ds(space, FloorPlan.Plane) ?? [])
                    {
                        List<Point2D> point2Ds = (face2D?.ExternalEdge2D as ISegmentable2D)?.GetPoints();
                        if (point2Ds is null || point2Ds.Count < 3)
                        {
                            continue;
                        }

                        StreamGeometry streamGeometry = new();
                        using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
                        {
                            streamGeometryContext.BeginFigure(matrix.Transform(new System.Windows.Point(point2Ds[0].X, point2Ds[0].Y)), true, true);

                            for (int i = 1; i < point2Ds.Count; i++)
                            {
                                streamGeometryContext.LineTo(matrix.Transform(new System.Windows.Point(point2Ds[i].X, point2Ds[i].Y)), false, false);
                            }
                        }

                        streamGeometry.Freeze();

                        drawingContext.DrawGeometry(brush, null, streamGeometry);
                    }
                }
            }

            FloorPlan.Overlay.Children.Add(drawingVisual);
        }

        private bool Visible(PartFOverlayMark mark)
        {
            if (mark.IsTransfer)
            {
                if (CheckBox_Transfer.IsChecked != true)
                {
                    return false;
                }

                return !mark.IsUnresolved || CheckBox_Unresolved.IsChecked == true;
            }

            return CheckBox_Terminals.IsChecked == true;
        }

        /// <summary>
        /// Draws one mark. What gets drawn depends on whether there is a real coordinate behind it.
        /// <para>
        /// <b>A terminal is a tag and nothing else.</b> It used to carry a short directional stub and an
        /// arrowhead, and both were claims the assessment cannot support: SAM does not know where the grille
        /// is, only that the room requires one, so the mark sat at a synthetic room-level point and the arrow
        /// implied a direction of air movement at a location nobody had established. A compact tag says
        /// exactly what is known - this room requires this much supply or extract - in the way a drawing tag
        /// normally says it.
        /// </para>
        /// <para>
        /// <b>A transfer route keeps its geometry</b>, because it has real geometry to keep: it crosses a
        /// modelled door or a modelled partition, both of which are on the plan. A route the model gives no
        /// opening for gets no span and no arrowhead - just a small dashed cross where the air would have to
        /// cross, and a tag saying so.
        /// </para>
        /// </summary>
        private void Draw(DrawingContext drawingContext, PartFOverlayMark mark, System.Windows.Media.Matrix matrix, PartFTagPlacementResult partFTagPlacementResult)
        {
            PartFAirflowAppearance appearance = PartFAirflowAppearance.Get(mark.AirType);

            if (mark.IsTransfer)
            {
                DrawTransfer(drawingContext, mark, appearance, matrix);
            }

            //Always drawn. The tag IS the terminal graphic now that the arrow is gone, so gating it on the
            //value toggles - as this did when there was an arrow underneath to fall back on - would make a
            //terminal disappear from the plan entirely. "Values" chooses between "SUP 63.0 l/s" and "SUP".
            DrawLabel(drawingContext, mark, appearance, matrix, partFTagPlacementResult);
        }

        /// <summary>
        /// The route geometry of a transfer mark: an arrow across the opening it crosses, or a dashed cross
        /// on the partition where the model establishes no opening at all.
        /// </summary>
        private static void DrawTransfer(DrawingContext drawingContext, PartFOverlayMark mark, PartFAirflowAppearance appearance, System.Windows.Media.Matrix matrix)
        {
            System.Windows.Point point_Start = matrix.Transform(new System.Windows.Point(mark.Start.X, mark.Start.Y));
            System.Windows.Point point_End = matrix.Transform(new System.Windows.Point(mark.End.X, mark.End.Y));

            Pen pen = Pen(appearance, mark.IsUnresolved);

            //No span: the route has no established opening, so there is nothing to draw an arrow along and no
            //direction that could honestly be pointed. A small dashed cross marks where the air would have to
            //cross, and the tag carries the rest. A long room-to-room arrow here would be the visual claim
            //that the air has a way through, which is exactly the claim this route cannot make.
            if (point_Start == point_End)
            {
                DrawWarningMarker(drawingContext, pen, point_End);
                return;
            }

            drawingContext.DrawLine(pen, point_Start, point_End);

            DrawHead(drawingContext, pen, point_Start, point_End);
        }

        /// <summary>A small dashed cross marking a route that has nowhere established to pass through.</summary>
        private static void DrawWarningMarker(DrawingContext drawingContext, Pen pen, System.Windows.Point point)
        {
            const double size = 6;

            drawingContext.DrawLine(pen, new System.Windows.Point(point.X - size, point.Y - size), new System.Windows.Point(point.X + size, point.Y + size));
            drawingContext.DrawLine(pen, new System.Windows.Point(point.X - size, point.Y + size), new System.Windows.Point(point.X + size, point.Y - size));
        }

        private static void DrawHead(DrawingContext drawingContext, Pen pen, System.Windows.Point point_Start, System.Windows.Point point_End)
        {
            Vector vector = point_End - point_Start;
            if (vector.Length <= 0)
            {
                return;
            }

            vector.Normalize();

            Vector vector_Normal = new(-vector.Y, vector.X);

            //Built in SCREEN space so it stays the same size at every zoom. A head scaled with the
            //building is a dot on a site plan and a wedge across a room.
            System.Windows.Point point_1 = point_End - (vector * arrowHead_Px) + (vector_Normal * (arrowHead_Px / 2.5));
            System.Windows.Point point_2 = point_End - (vector * arrowHead_Px) - (vector_Normal * (arrowHead_Px / 2.5));

            StreamGeometry streamGeometry = new();
            using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
            {
                streamGeometryContext.BeginFigure(point_End, true, true);
                streamGeometryContext.LineTo(point_1, true, false);
                streamGeometryContext.LineTo(point_2, true, false);
            }

            streamGeometry.Freeze();

            drawingContext.DrawGeometry(pen.Brush, null, streamGeometry);
        }

        /// <summary>
        /// Draws one tag where the placement engine put it: a white box with a hairline border, the way a
        /// drawing tag reads, with the text in the air type's own colour so supply, extract and local kitchen
        /// extract stay distinguishable and the legend keeps meaning something.
        /// <para>
        /// A leader only where there is a real coordinate to lead back to - see
        /// <see cref="HasPhysicalAnchor"/>.
        /// </para>
        /// </summary>
        private void DrawLabel(DrawingContext drawingContext, PartFOverlayMark mark, PartFAirflowAppearance appearance, System.Windows.Media.Matrix matrix, PartFTagPlacementResult partFTagPlacementResult)
        {
            Brush brush = new SolidColorBrush(Color.FromRgb(appearance.Red, appearance.Green, appearance.Blue));
            brush.Freeze();

            //The tag was laid out for the annotation scale, so it is drawn at the size that scale implies at
            //the current zoom: exactly the measured size when the view is at the annotation scale, and
            //proportionally larger or smaller elsewhere. Fixing the text at its measured pixel size instead
            //would let tags visibly collide at every zoom except one, and the drawing would stop matching the
            //layout that was actually solved - which is the layout the leaders and the plates are drawn from.
            double factor = System.Math.Max(System.Math.Abs(matrix.M11), System.Math.Abs(matrix.M22)) / PartFTagPlacement.PixelsPerMetre(AnnotationScale);

            FormattedText formattedText = Text(Label(mark), brush, labelSize_Px * factor, true);

            FormattedText formattedText_Caption = Caption(mark) is string caption ? Text(caption, brush, captionSize_Px * factor, false) : null;

            double width = System.Math.Max(formattedText.Width, formattedText_Caption?.Width ?? 0);
            double height = formattedText.Height + (formattedText_Caption?.Height ?? 0);

            System.Windows.Point point_Anchor = matrix.Transform(new System.Windows.Point(mark.End.X, mark.End.Y));

            //Nothing was placed for this mark - it became visible between the last placement and this repaint.
            //Drawn beside the anchor rather than dropped: a tag carries a flow rate, and the next placement
            //will tidy it.
            System.Windows.Point point_Text = Screen(partFTagPlacementResult?.Rectangle2D, matrix) is Rect rect
                ? rect.TopLeft
                : new System.Windows.Point(point_Anchor.X + 6, point_Anchor.Y - (height / 2));

            //A leader only where the tag is attached to something the model actually locates. Built from the
            //engineering anchor and the solved rectangle, in the view layer - the shared solver knows nothing
            //about annotation and must not start to.
            if (HasPhysicalAnchor(mark) && partFTagPlacementResult?.Leader2D() is Segment2D segment2D)
            {
                Pen pen_Leader = LeaderPen(brush);

                drawingContext.DrawLine(
                    pen_Leader,
                    matrix.Transform(new System.Windows.Point(segment2D[0].X, segment2D[0].Y)),
                    matrix.Transform(new System.Windows.Point(segment2D[1].X, segment2D[1].Y)));
            }

            //The tag: a white box with a hairline border and compact padding, drawn behind the text. Nearly
            //opaque rather than a translucent wash, so a tag over a hatched wall or a coloured space fill
            //reads as an annotation sitting on the drawing rather than as part of it.
            Rect rect_Tag = new(
                point_Text.X - (tagPadding_Px * factor),
                point_Text.Y - (tagPadding_Px * factor / 2),
                width + (tagPadding_Px * factor * 2),
                height + (tagPadding_Px * factor));

            drawingContext.DrawRectangle(plateBrush, TagPen(factor), rect_Tag);

            drawingContext.DrawText(formattedText, point_Text);

            if (formattedText_Caption is not null)
            {
                drawingContext.DrawText(formattedText_Caption, new System.Windows.Point(point_Text.X, point_Text.Y + formattedText.Height));
            }
        }

        /// <summary>
        /// Whether this mark stands for something the model puts at an actual coordinate, which is what
        /// decides whether a leader from the tag would be informative or a fabrication.
        /// <para>
        /// A transfer route does: it crosses a modelled door, or a modelled partition between two spaces, and
        /// both are drawn on the plan. A terminal does not. Approved Document F requires a room to have
        /// extract or supply; it does not say where in the ceiling the grille goes, and nothing in the
        /// analytical model does either - so the terminal mark sits at a synthetic point inside the room, and
        /// a leader drawn to it would point confidently at a location nobody has established. The tag is
        /// inside the room it concerns, and that is the whole of what is known.
        /// </para>
        /// <para>
        /// The placement layer still computes leaders for every tag, and this is the only thing suppressing
        /// them - so the day a real terminal object with a coordinate exists, this returns true for it and
        /// its leader appears. That is why the capability is kept rather than deleted.
        /// </para>
        /// </summary>
        private static bool HasPhysicalAnchor(PartFOverlayMark mark)
        {
            return mark.IsTransfer;
        }

        /// <summary>
        /// The tag's text: the semantic abbreviation, the rate, and a status symbol where it adds something -
        /// "SUP 63.0 l/s ✓". The abbreviation carries the air type in words, so the tag reads on a black and
        /// white printout and to a colour-blind reader without relying on the colour of the text.
        /// <para>
        /// No directional symbol. The old "▶" said which way air moves through a terminal whose position
        /// SAM does not know, at a point it placed itself; that is a claim about the installation, and a tag
        /// should only carry what has been established.
        /// </para>
        /// </summary>
        private string Label(PartFOverlayMark mark)
        {
            PartFAirflowAppearance appearance = PartFAirflowAppearance.Get(mark.AirType);

            string result = CheckBox_Values.IsChecked == true ? mark.Label : appearance.Abbreviation;

            if (CheckBox_Compliance.IsChecked != true)
            {
                return result;
            }

            string symbol = PartFAirflowAppearance.Status(mark.Status).Symbol;

            //An unresolved transfer route already carries a trailing "?" in its own label, so adding the
            //cannot-be-determined symbol produced "TRA 63.0 l/s ? ?". One question mark is the message.
            return string.IsNullOrWhiteSpace(symbol) || result.TrimEnd().EndsWith(symbol, StringComparison.Ordinal)
                ? result
                : string.Concat(result, " ", symbol);
        }

        /// <summary>The tag's second line, or null where the mark needs no qualifying.</summary>
        private string Caption(PartFOverlayMark mark)
        {
            return CheckBox_DoorData.IsChecked == true && !string.IsNullOrWhiteSpace(mark.Caption) ? mark.Caption : null;
        }

        /// <summary>
        /// A tag's measured size in SCREEN pixels, which is what the placement converts into plane units.
        /// Measured exactly as it is drawn, so the box the engine reserves is the box the text fills.
        /// </summary>
        private void Size(PartFOverlayMark mark, out double width, out double height)
        {
            Brush brush = Brushes.Black;

            FormattedText formattedText = Text(Label(mark), brush, labelSize_Px, true);

            FormattedText formattedText_Caption = Caption(mark) is string caption ? Text(caption, brush, captionSize_Px, false) : null;

            width = System.Math.Max(formattedText.Width, formattedText_Caption?.Width ?? 0);
            height = formattedText.Height + (formattedText_Caption?.Height ?? 0);
        }

        /// <summary>
        /// A plane rectangle as a screen rectangle: the bounding box of its transformed corners, so it is
        /// right whichever way the view flips the axes.
        /// </summary>
        private static Rect? Screen(Rectangle2D rectangle2D, System.Windows.Media.Matrix matrix)
        {
            List<Point2D> point2Ds = rectangle2D?.GetPoints();
            if (point2Ds is null || point2Ds.Count == 0)
            {
                return null;
            }

            double x_Min = double.MaxValue;
            double y_Min = double.MaxValue;
            double x_Max = double.MinValue;
            double y_Max = double.MinValue;

            foreach (Point2D point2D in point2Ds)
            {
                System.Windows.Point point = matrix.Transform(new System.Windows.Point(point2D.X, point2D.Y));

                x_Min = System.Math.Min(x_Min, point.X);
                y_Min = System.Math.Min(y_Min, point.Y);
                x_Max = System.Math.Max(x_Max, point.X);
                y_Max = System.Math.Max(y_Max, point.Y);
            }

            return new Rect(new System.Windows.Point(x_Min, y_Min), new System.Windows.Point(x_Max, y_Max));
        }

        /// <summary>
        /// A leader is a thin hairline in the mark's own colour: it has to connect the tag to the mark
        /// without competing with either.
        /// </summary>
        private static Pen LeaderPen(Brush brush)
        {
            Pen result = new(brush, 0.7);

            result.Freeze();

            return result;
        }

        /// <summary>
        /// The tag's background: white, and nearly opaque. Not a translucent wash - a tag has to read as
        /// annotation laid ON the drawing, and a wash over a hatched wall or a coloured space fill turns the
        /// text into part of the pattern behind it. A little transparency is kept so the geometry underneath
        /// can still be made out.
        /// </summary>
        private static Brush Plate()
        {
            SolidColorBrush result = new(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));

            result.Freeze();

            return result;
        }

        /// <summary>
        /// The tag's border: a neutral grey, deliberately not the air type's colour. The border's job is to
        /// separate the tag from what it sits on; the air type is already said by the abbreviation and by the
        /// colour of the text, and a coloured outline as well would make a small tag shout.
        /// </summary>
        private static Brush TagBorder()
        {
            SolidColorBrush result = new(Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A));

            result.Freeze();

            return result;
        }

        /// <summary>A hairline border, scaled with the annotation so it stays a hairline at every zoom.</summary>
        private static Pen TagPen(double factor)
        {
            Pen result = new(tagBorderBrush, System.Math.Max(0.6, 0.7 * factor));

            result.Freeze();

            return result;
        }

        private static FormattedText Text(string text, Brush brush, double size, bool bold)
        {
            return new FormattedText(
                text ?? string.Empty,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                size,
                brush,
                96);
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        /// <summary>
        /// A space clicked on the plan shows its terminals and its net airflow; where the space carries
        /// more than one terminal, all of them are shown, because a studio's supply and its local kitchen
        /// extract are both the answer to "what does this room do".
        /// </summary>
        private void FloorPlan_ObjectSelectionChanged(object sender, ObjectSelectionChangedEventArgs e)
        {
            PartFComplianceResult complianceResult = Selected?.ComplianceResult;
            if (complianceResult is null)
            {
                return;
            }

            List<Core.SAMObject> sAMObjects = FloorPlan.SelectedSAMObjects();

            //A door first: clicking one is a more specific request than clicking the room it sits in.
            PartFDoorTransferData partFDoorTransferData = PartFSelectionResolver.TransferPath(sAMObjects, complianceResult);
            if (partFDoorTransferData is not null)
            {
                Show(partFDoorTransferData);
                return;
            }

            //Searched by guid across the WHOLE selection, not taken from the front of it. A click can
            //select several objects and they arrive unordered, so picking the first one reported a
            //bedroom as having no terminals while this same view drew its supply arrow.
            Guid guid_Space = PartFSelectionResolver.SpaceGuid(sAMObjects, complianceResult);
            if (guid_Space == Guid.Empty)
            {
                Space space_Unassessed = sAMObjects?.OfType<Space>().FirstOrDefault();
                if (space_Unassessed is not null)
                {
                    TextBlock_Selection.Text = string.Format("{0}{1}{1}No Part F terminal is required in this space.{1}It may belong to another dwelling, to a communal area, or to a category that takes neither supply nor extract.", space_Unassessed.Name, Environment.NewLine);
                }

                return;
            }

            List<PartFVentilationTerminalRequirement> terminals = PartFSelectionResolver.Terminals(guid_Space, complianceResult);

            List<string> lines =
            [
                terminals[0].SpaceName,
                string.Format("Operating mode shown: {0}", Core.Query.Description(OperatingMode)),
                string.Empty,
            ];

            double net = 0;

            foreach (PartFVentilationTerminalRequirement terminal in terminals)
            {
                double? rate = PartFSchematic.Rate(terminal, OperatingMode);

                if (rate is not null && terminal.IsInBalancedFlow)
                {
                    net += terminal.IsExtract ? -rate.Value : rate.Value;
                }

                lines.Add(string.Format("{0}: {1}", Core.Query.Description(terminal.TerminalRole), Rate(rate)));
                lines.Add(string.Format("   continuous {0}, high {1}, setback {2}, measured {3}", Rate(terminal.ContinuousDesignFlowRate_Lps), Rate(terminal.HighFlowRate_Lps), Rate(terminal.SetbackFlowRate_Lps), Rate(terminal.MeasuredContinuousFlowRate_Lps)));
                lines.Add(string.Format("   required high rate {0}, provision {1}", Rate(terminal.RequiredHighFlowRate_Lps), Core.Query.Description(terminal.ProvisionStatus)));
                lines.Add(string.Format("   status {0}", Core.Query.Description(terminal.ComplianceStatus)));
                lines.Add(string.Empty);
            }

            lines.Add(string.Format("Net airflow: {0}", Rate(net)));

            List<PartFDoorTransferData> routes = PartFSelectionResolver.TransferPaths(guid_Space, complianceResult);
            if (routes.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("Connected transfer routes:");
                routes.ForEach(x => lines.Add(string.Format("   {0} to {1}, {2}{3}", x.UpstreamSpaceName, x.DownstreamSpaceName, Rate(PartFSchematic.Rate(x, OperatingMode)), x.IsOpeningUnresolved ? " (opening not established)" : string.Empty)));
            }

            TextBlock_Selection.Text = string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// A click near a transfer arrow selects the route it stands for. Handled here rather than through
        /// the plan's hit testing because a transfer mark belongs to the opening between two spaces, which
        /// is not a thing the plan has a visual for.
        /// </summary>
        private void FloorPlan_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            PartFComplianceResult complianceResult = Selected?.ComplianceResult;
            if (overlay is null || complianceResult is null)
            {
                return;
            }

            System.Windows.Point point = e.GetPosition(FloorPlan);

            System.Windows.Media.Matrix matrix = FloorPlan.WorldToScreen;

            PartFOverlayMark mark_Nearest = null;
            double distance_Nearest = double.MaxValue;

            foreach (PartFOverlayMark mark in overlay.Marks.Where(x => x.IsTransfer && Visible(x)))
            {
                System.Windows.Point point_Mid = matrix.Transform(new System.Windows.Point((mark.Start.X + mark.End.X) / 2, (mark.Start.Y + mark.End.Y) / 2));

                double distance = (point_Mid - point).Length;
                if (distance < distance_Nearest)
                {
                    distance_Nearest = distance;
                    mark_Nearest = mark;
                }
            }

            //A generous but finite radius in SCREEN pixels: close enough to be a deliberate click on the
            //arrow, and never so far that clicking empty floor selects a route across the flat.
            if (mark_Nearest is null || distance_Nearest > 24)
            {
                return;
            }

            Show((complianceResult.TransferPaths ?? []).Find(x => x is not null && string.Equals(x.Name, mark_Nearest.DoorName, StringComparison.Ordinal) && x.UpstreamSpaceGuid == mark_Nearest.SpaceGuid));
        }

        private static Pen Pen(PartFAirflowAppearance partFAirflowAppearance, bool unresolved)
        {
            SolidColorBrush brush = new(Color.FromRgb(partFAirflowAppearance.Red, partFAirflowAppearance.Green, partFAirflowAppearance.Blue));
            brush.Freeze();

            Pen result = new(brush, partFAirflowAppearance.Thickness);

            //An unresolved mark is always dashed, whatever its air type's own pattern says. It is the one
            //case where the appearance is overridden here, and it is overridden in the safe direction:
            //a route that has not been established never looks more certain than one that has.
            if (unresolved || partFAirflowAppearance.LinePattern == PartFAirflowAppearance.Pattern.Dashed)
            {
                result.DashStyle = unresolved ? new DashStyle([2, 2], 0) : new DashStyle([4, 3], 0);
            }

            result.Freeze();

            return result;
        }
    }
}
