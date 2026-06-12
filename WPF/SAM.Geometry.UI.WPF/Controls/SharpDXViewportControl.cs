// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SAM.Core;
using SAM.Core.UI;
using SAM.Core.UI.WPF;
using SAM.Geometry.Object;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Media3D = System.Windows.Media.Media3D;

namespace SAM.Geometry.UI.WPF
{
    /// <summary>
    /// DirectX 11 renderer for the 3D (ThreeDimensionalViewSettings) view - the SharpDX port of
    /// issue #18 gate 3 (documentation/3d-viewport-sharpdx-port-plan.md, Phase B). Renders the
    /// same GeometryObjectModel as the Helix viewport, but through HelixToolkit.Wpf.SharpDX:
    /// the scene is a few merged GPU-resident models per object (Convert.ToElement3Ds) instead
    /// of a Visual3D per face/segment.
    ///
    /// Hosted by ViewportControl only when the SAM_UI_VIEWPORT_SHARPDX environment variable is
    /// set (off by default; any value other than empty/"0" enables it). Phase B covered scene
    /// build, camera and zoom; Phase C (issue #32) adds hover and selection: octree-backed
    /// FindHits picking on mouse move, single/Ctrl/double click, Escape-to-clear and rectangle
    /// selection (SelectByScreenRect, driven by the ViewportControl RectangularSelector overlay).
    /// Event payloads are detached stub ModelVisual3Ds carrying the same attached IJSAMObject as
    /// the Helix visuals - the FloorPlan2DControl interop pattern - so existing consumers
    /// (AnalyticalWindow) keep working unchanged. Context-menu plumbing (Phase C item 4) and the
    /// orthographic-3D camera and view chrome (Phase D) still run only on the Helix path.
    ///
    /// The DX11 device lives in a single process-wide EffectsManager shared by every instance
    /// (one per tab), created lazily and disposed on dispatcher shutdown - WPF unloads tab
    /// content on every tab switch, so the device must not be tied to a control's lifetime.
    /// </summary>
    public class SharpDXViewportControl : System.Windows.Controls.Grid
    {
        public static readonly bool Enabled = ResolveEnabled();

        private static IEffectsManager effectsManager;

        private readonly Viewport3DX viewport3DX;

        // Scene elements added by Load (everything except the light), and the Guid -> Element3D
        // index mirroring ViewportControl.BuildVisual3DIndex (issue #16). UI-thread only.
        private readonly List<Element3D> sceneElement3Ds = new List<Element3D>();
        private readonly Dictionary<Guid, Element3D> dictionary_Element3D = new Dictionary<Guid, Element3D>();

        // Hover/selection state (issue #32 Phase C). dictionary_Stub: guid -> detached stub
        // ModelVisual3D carrying the object's attached IJSAMObject (event payload interop, see the
        // class note). dictionary_Guid: every scene Element3D (group and its merged child models)
        // -> owning object guid, for resolving FindHits results. The dictionary_Base* maps hold
        // the model-defined appearance of each child so the hover/selection overrides of
        // ApplyAppearance always restore cleanly; entries follow the children (RegisterChildren /
        // UnregisterChildren) through Load and RefreshAppearance rebuilds.
        private readonly Dictionary<Guid, Media3D.ModelVisual3D> dictionary_Stub = new Dictionary<Guid, Media3D.ModelVisual3D>();
        private readonly Dictionary<Element3D, Guid> dictionary_Guid = new Dictionary<Element3D, Guid>();
        private readonly Dictionary<MeshGeometryModel3D, Material> dictionary_BaseMaterial = new Dictionary<MeshGeometryModel3D, Material>();
        private readonly Dictionary<MeshGeometryModel3D, bool> dictionary_BaseIsTransparent = new Dictionary<MeshGeometryModel3D, bool>();
        private readonly Dictionary<LineGeometryModel3D, Color> dictionary_BaseLineColor = new Dictionary<LineGeometryModel3D, Color>();
        private readonly Dictionary<LineGeometryModel3D, double> dictionary_BaseLineThickness = new Dictionary<LineGeometryModel3D, double>();
        private readonly HashSet<Guid> selectedGuids = new HashSet<Guid>();
        private Guid? hooveredGuid;

        // Selection appearance - parity with the Helix SelectAction (Query.SelectionSurfaceAppearance:
        // RGB(125,125,255) fill, blue edges). Ambient = Diffuse / no specular matches the flat
        // ambient-only look of SharpDXSceneBuilder materials. One shared instance is fine: materials
        // are shared model objects and all controls live on the UI thread.
        private static readonly PhongMaterial material_Selection = new PhongMaterial
        {
            DiffuseColor = new HelixToolkit.Maths.Color4(125f / 255f, 125f / 255f, 1f, 1f),
            AmbientColor = new HelixToolkit.Maths.Color4(125f / 255f, 125f / 255f, 1f, 1f),
            SpecularColor = new HelixToolkit.Maths.Color4(0, 0, 0, 0)
        };

        private static readonly Color color_SelectionLine = Color.FromRgb(0, 0, 255);

        private bool zoomExtentsPending;

        public event ObjectHooveredEventHandler ObjectHoovered;
        public event ObjectDoubleClickedEventHandler ObjectDoubleClicked;
        public event ObjectSelectionChangedEventHandler ObjectSelectionChanged;

        public SharpDXViewportControl()
        {
            viewport3DX = new Viewport3DX
            {
                EffectsManager = GetEffectsManager(),
                Camera = new HelixToolkit.Wpf.SharpDX.PerspectiveCamera
                {
                    Position = new Media3D.Point3D(10, 10, 10),
                    LookDirection = new Media3D.Vector3D(-10, -10, -10),
                    UpDirection = new Media3D.Vector3D(0, 0, 1),
                    NearPlaneDistance = 0.1,
                    FarPlaneDistance = 10000
                },
                BackgroundColor = Colors.White,
                ShowCoordinateSystem = true,
                ShowViewCube = true,
                ModelUpDirection = new Media3D.Vector3D(0, 0, 1)
            };

            // Single white ambient light - parity with the Helix 3D path (Load adds AmbientLight
            // only), which renders flat unshaded colors.
            viewport3DX.Items.Add(new AmbientLight3D { Color = Colors.White });

            viewport3DX.Loaded += Viewport3DX_Loaded;

            // Phase C interaction (issue #32): hover picking on mouse move, single/double click
            // selection, Escape-to-clear. The left button is free for selection - the Viewport3DX
            // default camera gestures sit on the right button (rotate; Ctrl/Shift variants) - so
            // plain left drags also reach the ViewportControl rectangle-selector overlay. View-cube
            // clicks never get here: the viewport marks them handled in its preview hit-test.
            viewport3DX.PreviewMouseMove += Viewport3DX_PreviewMouseMove;
            viewport3DX.MouseLeftButtonDown += Viewport3DX_MouseLeftButtonDown;
            viewport3DX.MouseLeave += Viewport3DX_MouseLeave;
            viewport3DX.KeyDown += Viewport3DX_KeyDown;

            Children.Add(viewport3DX);
        }

        private static bool ResolveEnabled()
        {
            string value = Environment.GetEnvironmentVariable("SAM_UI_VIEWPORT_SHARPDX");
            return !string.IsNullOrWhiteSpace(value) && value.Trim() != "0";
        }

        private static IEffectsManager GetEffectsManager()
        {
            if (effectsManager == null)
            {
                using (PerformanceLog.Measure("ViewportControl.SharpDX.CreateDevice"))
                {
                    effectsManager = new DefaultEffectsManager();
                }

                Dispatcher.CurrentDispatcher.ShutdownStarted += (sender, e) =>
                {
                    effectsManager?.Dispose();
                    effectsManager = null;
                };
            }

            return effectsManager;
        }

        /// <summary>
        /// Rebuilds the scene from the model (null clears it). The camera is only touched when
        /// the scene was empty before this load - the given camera (view settings) is applied,
        /// or the view zooms to the new extents. Reloads of a populated scene (modifications,
        /// appearance refreshes) keep the user's camera, mirroring the Helix path.
        /// </summary>
        public void Load(GeometryObjectModel geometryObjectModel, Camera camera = null)
        {
            bool wasEmpty = sceneElement3Ds.Count == 0;

            foreach (Element3D element3D in sceneElement3Ds)
            {
                viewport3DX.Items.Remove(element3D);
            }

            sceneElement3Ds.Clear();
            dictionary_Element3D.Clear();
            dictionary_Stub.Clear();
            dictionary_Guid.Clear();
            dictionary_BaseMaterial.Clear();
            dictionary_BaseIsTransparent.Clear();
            dictionary_BaseLineColor.Clear();
            dictionary_BaseLineThickness.Clear();
            selectedGuids.Clear();
            hooveredGuid = null;

            if (geometryObjectModel == null)
            {
                return;
            }

            List<Element3D> element3Ds = Convert.ToElement3Ds(geometryObjectModel);
            if (element3Ds != null)
            {
                foreach (Element3D element3D in element3Ds)
                {
                    viewport3DX.Items.Add(element3D);
                    sceneElement3Ds.Add(element3D);

                    SAMObject sAMObject = Core.UI.WPF.Query.JSAMObject<SAMObject>(element3D);
                    if (sAMObject != null && !dictionary_Element3D.ContainsKey(sAMObject.Guid))
                    {
                        dictionary_Element3D[sAMObject.Guid] = element3D;
                        dictionary_Guid[element3D] = sAMObject.Guid;
                        RegisterChildren(sAMObject.Guid, element3D);

                        // Detached stub carrying the same attached object as the group - the
                        // ObjectHoovered/ObjectDoubleClicked payload (see the class note)
                        Media3D.ModelVisual3D stub = new Media3D.ModelVisual3D();
                        IJSAMObject jSAMObject = Core.UI.WPF.Query.JSAMObject<IJSAMObject>(element3D);
                        if (jSAMObject != null)
                        {
                            Core.UI.WPF.Modify.SetIJSAMObject(stub, jSAMObject);
                        }

                        dictionary_Stub[sAMObject.Guid] = stub;
                    }
                }
            }

            if (wasEmpty)
            {
                if (camera != null)
                {
                    SetCamera(camera);
                }
                else
                {
                    ZoomExtents();
                }
            }
        }

        public Element3D GetElement3D(Guid guid)
        {
            return dictionary_Element3D.TryGetValue(guid, out Element3D element3D) ? element3D : null;
        }

        /// <summary>
        /// The detached stub ModelVisual3D carrying the object's attached IJSAMObject - the payload
        /// of ObjectHoovered/ObjectDoubleClicked and ViewportControl.GetVisual3D on the SharpDX path
        /// (the FloorPlan2DControl interop pattern).
        /// </summary>
        public Media3D.ModelVisual3D GetStubVisual3D(Guid guid)
        {
            return dictionary_Stub.TryGetValue(guid, out Media3D.ModelVisual3D stub) ? stub : null;
        }

        /// <summary>
        /// Replaces the selection with the given objects (unknown guids are ignored). Returns whether
        /// the selection changed; raises no event - callers (ViewportControl) raise
        /// ObjectSelectionChanged from the result, mirroring FloorPlan2DControl.Select.
        /// </summary>
        public bool Select(IEnumerable<Guid> guids)
        {
            HashSet<Guid> selectedGuids_New = new HashSet<Guid>();
            if (guids != null)
            {
                foreach (Guid guid in guids)
                {
                    if (dictionary_Element3D.ContainsKey(guid))
                    {
                        selectedGuids_New.Add(guid);
                    }
                }
            }

            if (selectedGuids.SetEquals(selectedGuids_New))
            {
                return false;
            }

            HashSet<Guid> guids_Apply = new HashSet<Guid>(selectedGuids);
            guids_Apply.UnionWith(selectedGuids_New);

            selectedGuids.Clear();
            selectedGuids.UnionWith(selectedGuids_New);

            foreach (Guid guid in guids_Apply)
            {
                ApplyAppearance(guid);
            }

            return true;
        }

        public bool ClearSelection()
        {
            if (selectedGuids.Count == 0)
            {
                return false;
            }

            List<Guid> guids = new List<Guid>(selectedGuids);
            selectedGuids.Clear();

            foreach (Guid guid in guids)
            {
                ApplyAppearance(guid);
            }

            return true;
        }

        public List<SAMObject> SelectedSAMObjects()
        {
            List<SAMObject> result = new List<SAMObject>();
            foreach (Guid guid in selectedGuids)
            {
                if (dictionary_Element3D.TryGetValue(guid, out Element3D element3D))
                {
                    SAMObject sAMObject = Core.UI.WPF.Query.JSAMObject<SAMObject>(element3D);
                    if (sAMObject != null)
                    {
                        result.Add(sAMObject);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Rectangle selection (issue #32 Phase C): tests every object's projected geometry against
        /// the screen rectangle. Inside requires the whole projection inside the rectangle;
        /// InsideOrIntersect also accepts any touching triangle/segment - the semantics of the
        /// classic Helix Viewport3DHelper.FindHits(rect, mode) used by the Helix path. Meshes define
        /// the footprint (text billboards are ignored, like Text3DObject on the Helix path);
        /// curve-only objects fall back to their line segments. Raises no event - the caller does.
        /// </summary>
        public void SelectByScreenRect(Rect rect, SelectionType selectionType)
        {
            if (rect == Rect.Empty || selectionType == SelectionType.Undefined || viewport3DX.Camera == null)
            {
                return;
            }

            // Camera plane for the behind-camera guard: projecting a point behind the camera yields
            // a mirrored screen position that must not count as a hit
            Vector3 cameraPosition = viewport3DX.Camera.Position.ToVector3();
            Vector3 cameraLookDirection = Vector3.Normalize(viewport3DX.Camera.LookDirection.ToVector3());

            List<Guid> guids = new List<Guid>();
            foreach (KeyValuePair<Guid, Element3D> keyValuePair in dictionary_Element3D)
            {
                if (HitsScreenRect(keyValuePair.Value, rect, selectionType, cameraPosition, cameraLookDirection))
                {
                    guids.Add(keyValuePair.Key);
                }
            }

            Select(guids);
        }

        /// <summary>
        /// In-place appearance refresh for attribute-only edits (issue #32 / #11): rebuilds only the
        /// given objects' merged models from their attached IJSAMObject - whose SurfaceAppearances were
        /// already updated in place (Analytical.UI.Modify.TryRefreshSpaceAppearances) - and swaps each
        /// GroupModel3D's children. Geometry is unchanged by construction, so this re-triangulates only
        /// the edited objects (cheap), leaving the camera and every other object untouched - no full
        /// ToElement3Ds. Mirrors Modify.RefreshAppearance on the Helix path.
        /// </summary>
        public void RefreshAppearance(IEnumerable<Guid> guids)
        {
            if (guids == null)
            {
                return;
            }

            foreach (Guid guid in guids)
            {
                if (!dictionary_Element3D.TryGetValue(guid, out Element3D element3D))
                {
                    continue;
                }

                GroupModel3D groupModel3D = element3D as GroupModel3D;
                if (groupModel3D == null)
                {
                    continue;
                }

                // The attached object is the same instance the analytical refresh mutated (the scene and
                // the UIGeometryObjectModel share the cached model graph - issue #14).
                ISAMGeometryObject sAMGeometryObject = Core.UI.WPF.Query.JSAMObject<ISAMGeometryObject>(groupModel3D);
                if (sAMGeometryObject == null)
                {
                    continue;
                }

                List<Element3D> element3Ds = Convert.ToElement3Ds(sAMGeometryObject);

                UnregisterChildren(groupModel3D);

                groupModel3D.Children.Clear();
                if (element3Ds != null)
                {
                    foreach (Element3D element3D_Child in element3Ds)
                    {
                        groupModel3D.Children.Add(element3D_Child);
                    }
                }

                RegisterChildren(guid, groupModel3D);

                // Hover/selection are appearance overrides on the children - re-apply them on the
                // rebuilt set so a selected/hovered object stays marked through the re-skin
                ApplyAppearance(guid);
            }
        }

        public bool ContainsAny<T>(IEnumerable<Guid> guids) where T : SAMObject
        {
            if (guids == null)
            {
                return false;
            }

            foreach (Guid guid in guids)
            {
                if (dictionary_Element3D.TryGetValue(guid, out Element3D element3D) && Core.UI.WPF.Query.JSAMObject<T>(element3D) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public Camera GetCamera()
        {
            HelixToolkit.Wpf.SharpDX.Camera camera = viewport3DX.Camera;
            if (camera == null)
            {
                return null;
            }

            return new Camera(camera.Position.ToSAM(), camera.LookDirection.ToSAM(), camera.UpDirection.ToSAM());
        }

        public void SetCamera(Camera camera)
        {
            if (camera == null || viewport3DX.Camera == null)
            {
                return;
            }

            // Same look-direction pole nudge as ViewportControl.SetCamera: a look direction
            // parallel to the up axis breaks the camera controller.
            Spatial.Vector3D lookDirection = camera.LookDirection;
            if (lookDirection.AlmostEqual(-Spatial.Vector3D.WorldZ, Tolerance.MacroDistance))
            {
                lookDirection = new Spatial.Vector3D(0, 0.0001, -0.9999);
            }
            else if (lookDirection.AlmostEqual(Spatial.Vector3D.WorldZ, Tolerance.MacroDistance))
            {
                lookDirection = new Spatial.Vector3D(0, 0.0001, 0.9999);
            }

            viewport3DX.Camera.Position = camera.Location.ToMedia3D();
            viewport3DX.Camera.LookDirection = lookDirection.ToMedia3D();

            if (camera.UpDirection is not null)
            {
                viewport3DX.Camera.UpDirection = camera.UpDirection.ToMedia3D();
            }
        }

        /// <summary>
        /// Zooms to the scene extents; deferred until the viewport is loaded with a non-zero size
        /// (zooming a size-less viewport derives a degenerate camera - Phase A, PR #30).
        /// </summary>
        public void ZoomExtents()
        {
            if (viewport3DX.IsLoaded && viewport3DX.ActualWidth > 0)
            {
                viewport3DX.ZoomExtents();
                return;
            }

            zoomExtentsPending = true;
        }

        // Indexes a group's merged child models (child -> guid for hit resolution) and snapshots
        // their model-defined appearance for ApplyAppearance to restore. Called from Load and after
        // every RefreshAppearance child swap.
        private void RegisterChildren(Guid guid, Element3D element3D)
        {
            GroupModel3D groupModel3D = element3D as GroupModel3D;
            if (groupModel3D == null)
            {
                return;
            }

            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                dictionary_Guid[element3D_Child] = guid;

                if (element3D_Child is MeshGeometryModel3D meshGeometryModel3D)
                {
                    dictionary_BaseMaterial[meshGeometryModel3D] = meshGeometryModel3D.Material;
                    dictionary_BaseIsTransparent[meshGeometryModel3D] = meshGeometryModel3D.IsTransparent;
                }
                else if (element3D_Child is LineGeometryModel3D lineGeometryModel3D)
                {
                    dictionary_BaseLineColor[lineGeometryModel3D] = lineGeometryModel3D.Color;
                    dictionary_BaseLineThickness[lineGeometryModel3D] = lineGeometryModel3D.Thickness;
                }
            }
        }

        private void UnregisterChildren(Element3D element3D)
        {
            GroupModel3D groupModel3D = element3D as GroupModel3D;
            if (groupModel3D == null)
            {
                return;
            }

            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                dictionary_Guid.Remove(element3D_Child);

                if (element3D_Child is MeshGeometryModel3D meshGeometryModel3D)
                {
                    dictionary_BaseMaterial.Remove(meshGeometryModel3D);
                    dictionary_BaseIsTransparent.Remove(meshGeometryModel3D);
                }
                else if (element3D_Child is LineGeometryModel3D lineGeometryModel3D)
                {
                    dictionary_BaseLineColor.Remove(lineGeometryModel3D);
                    dictionary_BaseLineThickness.Remove(lineGeometryModel3D);
                }
            }
        }

        /// <summary>
        /// Re-derives an object's displayed appearance from its current hover/selection state:
        /// selected swaps the fill material and edge color (Helix SelectAction parity), hovered
        /// doubles the edge thickness (Helix HighlightAction parity - objects whose edges have
        /// thickness 0 show no hover mark there either), neither restores the model-defined base.
        /// Idempotent - safe to call on any state transition.
        /// </summary>
        private void ApplyAppearance(Guid guid)
        {
            if (!dictionary_Element3D.TryGetValue(guid, out Element3D element3D) || !(element3D is GroupModel3D groupModel3D))
            {
                return;
            }

            bool selected = selectedGuids.Contains(guid);
            bool hoovered = hooveredGuid == guid;

            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                if (element3D_Child is MeshGeometryModel3D meshGeometryModel3D)
                {
                    if (dictionary_BaseMaterial.TryGetValue(meshGeometryModel3D, out Material material))
                    {
                        meshGeometryModel3D.Material = selected ? material_Selection : material;
                    }

                    if (dictionary_BaseIsTransparent.TryGetValue(meshGeometryModel3D, out bool isTransparent))
                    {
                        // The selection fill is opaque - take the model out of the transparent pass
                        meshGeometryModel3D.IsTransparent = !selected && isTransparent;
                    }
                }
                else if (element3D_Child is LineGeometryModel3D lineGeometryModel3D)
                {
                    if (dictionary_BaseLineColor.TryGetValue(lineGeometryModel3D, out Color color))
                    {
                        lineGeometryModel3D.Color = selected ? color_SelectionLine : color;
                    }

                    if (dictionary_BaseLineThickness.TryGetValue(lineGeometryModel3D, out double thickness))
                    {
                        lineGeometryModel3D.Thickness = hoovered ? thickness * 2 : thickness;
                    }
                }
            }
        }

        // Nearest picked object at the given viewport position: octree-backed FindHits, resolved
        // to the owning object through the child -> guid index. Skips hits on un-indexed elements
        // (objects without a SAMObject tag), mirroring what the Helix consumers ignore anyway.
        private Guid? HitTestGuid(Point point)
        {
            // HitTestResult qualified: System.Windows.Media declares one too
            IList<HelixToolkit.SharpDX.HitTestResult> hitTestResults = viewport3DX.FindHits(point);
            if (hitTestResults != null)
            {
                foreach (HelixToolkit.SharpDX.HitTestResult hitTestResult in hitTestResults)
                {
                    if (hitTestResult == null || !hitTestResult.IsValid)
                    {
                        continue;
                    }

                    if (hitTestResult.ModelHit is Element3D element3D && dictionary_Guid.TryGetValue(element3D, out Guid guid))
                    {
                        return guid;
                    }
                }
            }

            return null;
        }

        private void Viewport3DX_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // No hover while a gesture is in progress (left drag = rectangle selection overlay,
            // right drag = camera rotation, middle = pan)
            if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
            {
                return;
            }

            Guid? guid;

            // Unthrottled, unlike the Helix path (#16): the per-geometry octrees keep the hit-test
            // cheap. Only slow hit-tests (>= 25 ms) are logged - the gate check for issue #32.
            using (PerformanceLog.Measure("ViewportControl.HoverHitTest", "SharpDX", 25))
            {
                guid = HitTestGuid(e.GetPosition(viewport3DX));
            }

            if (guid != hooveredGuid)
            {
                Guid? hooveredGuid_Previous = hooveredGuid;
                hooveredGuid = guid;

                if (hooveredGuid_Previous.HasValue)
                {
                    ApplyAppearance(hooveredGuid_Previous.Value);
                }

                if (guid.HasValue)
                {
                    ApplyAppearance(guid.Value);
                }
            }

            if (guid.HasValue && dictionary_Stub.TryGetValue(guid.Value, out Media3D.ModelVisual3D stub))
            {
                ObjectHoovered?.Invoke(this, new ObjectHooveredEventArgs(e, stub));
            }
        }

        private void Viewport3DX_MouseLeave(object sender, MouseEventArgs e)
        {
            if (hooveredGuid.HasValue)
            {
                Guid guid = hooveredGuid.Value;
                hooveredGuid = null;
                ApplyAppearance(guid);
            }
        }

        private void Viewport3DX_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Shift stays reserved for camera gestures (parity with the Helix path bindings)
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                return;
            }

            Guid? guid = HitTestGuid(e.GetPosition(viewport3DX));

            if (e.ClickCount == 2)
            {
                if (guid.HasValue && dictionary_Stub.TryGetValue(guid.Value, out Media3D.ModelVisual3D stub))
                {
                    ObjectDoubleClicked?.Invoke(this, new ObjectDoubleClickedEventArgs(e, stub));
                    e.Handled = true;
                }

                return;
            }

            bool control = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            bool changed = false;
            if (!guid.HasValue)
            {
                if (!control)
                {
                    changed = ClearSelection();
                }
            }
            else
            {
                // Plain click selects just the clicked object; Ctrl toggles it within the current
                // selection - FloorPlan2DControl semantics, matching the Helix path behavior
                List<Guid> guids_Apply = new List<Guid>();

                if (!control)
                {
                    guids_Apply.AddRange(selectedGuids);
                    selectedGuids.Clear();
                }

                if (!selectedGuids.Add(guid.Value))
                {
                    selectedGuids.Remove(guid.Value);
                }

                if (!guids_Apply.Contains(guid.Value))
                {
                    guids_Apply.Add(guid.Value);
                }

                foreach (Guid guid_Apply in guids_Apply)
                {
                    ApplyAppearance(guid_Apply);
                }

                changed = true;
            }

            if (changed)
            {
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
            }
        }

        private void Viewport3DX_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ClearSelection())
            {
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
            }
        }

        // Screen-rectangle test for one object - see SelectByScreenRect for the semantics.
        private bool HitsScreenRect(Element3D element3D, Rect rect, SelectionType selectionType, Vector3 cameraPosition, Vector3 cameraLookDirection)
        {
            GroupModel3D groupModel3D = element3D as GroupModel3D;
            if (groupModel3D == null)
            {
                return false;
            }

            bool inside = selectionType == SelectionType.Inside;

            bool hasMesh = false;
            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                if (element3D_Child is MeshGeometryModel3D)
                {
                    hasMesh = true;
                    break;
                }
            }

            bool hasGeometry = false;
            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                Geometry3D geometry3D = null;
                bool mesh = false;

                if (element3D_Child is MeshGeometryModel3D meshGeometryModel3D)
                {
                    geometry3D = meshGeometryModel3D.Geometry;
                    mesh = true;
                }
                else if (!hasMesh && element3D_Child is LineGeometryModel3D lineGeometryModel3D)
                {
                    geometry3D = lineGeometryModel3D.Geometry;
                }

                if (geometry3D?.Positions == null || geometry3D.Positions.Count == 0 || geometry3D.Indices == null)
                {
                    continue;
                }

                hasGeometry = true;

                int count = geometry3D.Positions.Count;
                Point[] points = new Point[count];
                bool[] valids = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3 position = geometry3D.Positions[i];
                    if (Vector3.Dot(position - cameraPosition, cameraLookDirection) <= 0)
                    {
                        if (inside)
                        {
                            return false;
                        }

                        continue;
                    }

                    Vector2 vector2 = viewport3DX.Project(position);
                    points[i] = new Point(vector2.X, vector2.Y);
                    valids[i] = true;

                    if (inside)
                    {
                        if (!rect.Contains(points[i]))
                        {
                            return false;
                        }
                    }
                    else if (rect.Contains(points[i]))
                    {
                        return true;
                    }
                }

                if (inside)
                {
                    continue;
                }

                // InsideOrIntersect with no vertex inside the rectangle: edges crossing it, or the
                // rectangle sitting fully inside a triangle, still count as touching
                if (mesh)
                {
                    for (int i = 0; i + 2 < geometry3D.Indices.Count; i += 3)
                    {
                        int index_1 = geometry3D.Indices[i];
                        int index_2 = geometry3D.Indices[i + 1];
                        int index_3 = geometry3D.Indices[i + 2];
                        if (!valids[index_1] || !valids[index_2] || !valids[index_3])
                        {
                            continue;
                        }

                        if (TriangleHitsRect(points[index_1], points[index_2], points[index_3], rect))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i + 1 < geometry3D.Indices.Count; i += 2)
                    {
                        int index_1 = geometry3D.Indices[i];
                        int index_2 = geometry3D.Indices[i + 1];
                        if (!valids[index_1] || !valids[index_2])
                        {
                            continue;
                        }

                        if (SegmentHitsRect(points[index_1], points[index_2], rect))
                        {
                            return true;
                        }
                    }
                }
            }

            return inside && hasGeometry;
        }

        private static bool TriangleHitsRect(Point point_1, Point point_2, Point point_3, Rect rect)
        {
            // Vertices inside the rectangle were already tested by the caller
            if (SegmentHitsRect(point_1, point_2, rect) || SegmentHitsRect(point_2, point_3, rect) || SegmentHitsRect(point_3, point_1, rect))
            {
                return true;
            }

            // Rectangle fully inside the triangle: one corner inside is enough to decide
            return PointInTriangle(rect.TopLeft, point_1, point_2, point_3);
        }

        private static bool SegmentHitsRect(Point point_1, Point point_2, Rect rect)
        {
            if (rect.Contains(point_1) || rect.Contains(point_2))
            {
                return true;
            }

            Point topLeft = rect.TopLeft;
            Point topRight = rect.TopRight;
            Point bottomLeft = rect.BottomLeft;
            Point bottomRight = rect.BottomRight;

            return SegmentsIntersect(point_1, point_2, topLeft, topRight)
                || SegmentsIntersect(point_1, point_2, topRight, bottomRight)
                || SegmentsIntersect(point_1, point_2, bottomRight, bottomLeft)
                || SegmentsIntersect(point_1, point_2, bottomLeft, topLeft);
        }

        // Proper (non-collinear) segment intersection - collinear touches are below pixel relevance here
        private static bool SegmentsIntersect(Point point_1, Point point_2, Point point_3, Point point_4)
        {
            double d1 = Cross(point_3, point_4, point_1);
            double d2 = Cross(point_3, point_4, point_2);
            double d3 = Cross(point_1, point_2, point_3);
            double d4 = Cross(point_1, point_2, point_4);

            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross(Point point_1, Point point_2, Point point_3)
        {
            return (point_2.X - point_1.X) * (point_3.Y - point_1.Y) - (point_2.Y - point_1.Y) * (point_3.X - point_1.X);
        }

        private static bool PointInTriangle(Point point, Point point_1, Point point_2, Point point_3)
        {
            double d1 = Cross(point_1, point_2, point);
            double d2 = Cross(point_2, point_3, point);
            double d3 = Cross(point_3, point_1, point);

            bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;

            return !(hasNegative && hasPositive);
        }

        private void Viewport3DX_Loaded(object sender, RoutedEventArgs e)
        {
            if (zoomExtentsPending)
            {
                zoomExtentsPending = false;

                // Loaded priority: run after the pending layout pass so ActualWidth is real
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ZoomExtents));
            }
        }
    }
}
