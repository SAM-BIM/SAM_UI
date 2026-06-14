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
// 'Material' is ambiguous between SAM.Core.Material and HelixToolkit.Wpf.SharpDX.Material once both
// namespaces are imported; the SharpDX scene material (MeshGeometryModel3D.Material) is meant here.
using Material = HelixToolkit.Wpf.SharpDX.Material;

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
    /// (AnalyticalWindow) keep working unchanged. Context-menu plumbing (Phase C item 4) is in;
    /// Phase D adds the orthographic-3D camera (Ctrl+Shift+O toggle, parity with the Helix
    /// OrthographicToggleGesture) on top of the camera framing / view chrome already here.
    ///
    /// The DX11 device lives in a single process-wide EffectsManager shared by every instance
    /// (one per tab), created lazily and disposed on dispatcher shutdown - WPF unloads tab
    /// content on every tab switch, so the device must not be tied to a control's lifetime.
    /// </summary>
    public class SharpDXViewportControl : System.Windows.Controls.Grid
    {
        public static readonly bool Enabled = ResolveEnabled();

        // Mesh-batching (issue #16, behind SAM_UI_VIEWPORT_BATCH; off by default). When set, Load builds the
        // scene as a few merged-by-material models for the whole model (Convert.ToBatchedElement3Ds) instead
        // of one GroupModel3D per object, to collapse the per-model attach cost. INCREMENT 1 is render-only:
        // the batched models carry no per-object tag, so picking/hover/selection are inert in this mode.
        // See documentation/3d-viewport-mesh-batching-plan.md.
        public static readonly bool BatchEnabled = ResolveBatchEnabled();

        private static IEffectsManager effectsManager;

        private readonly Viewport3DX viewport3DX;

        // Scene elements added by Load (everything except the light), and the Guid -> Element3D
        // index mirroring ViewportControl.BuildVisual3DIndex (issue #16). UI-thread only.
        private readonly List<Element3D> sceneElement3Ds = new List<Element3D>();
        private readonly Dictionary<Guid, Element3D> dictionary_Element3D = new Dictionary<Guid, Element3D>();

        // Mesh geometries awaiting a deferred UpdateOctree() pass (issue #33 follow-up): the octree build
        // is moved off the regen critical path to a Background dispatcher tick after attach. Guarded by a
        // generation token so a newer Load cancels a stale pending pass.
        private readonly List<HelixToolkit.SharpDX.MeshGeometry3D> pendingOctreeGeometries = new List<HelixToolkit.SharpDX.MeshGeometry3D>();
        private int sceneGeneration;

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

        // Batched-scene state (issue #16 mesh-batching, when SAM_UI_VIEWPORT_BATCH is on). In this mode there
        // is no per-object Element3D, so object identity comes from these instead of dictionary_Element3D/
        // dictionary_Guid: dictionary_PickBucket resolves a FindHits triangle vertex -> guid per merged mesh,
        // dictionary_BatchedObject maps guid -> object (event payloads + selection queries). Increment 2 wires
        // picking + selection state + events; the selection/hover *visual* (overlay) is increment 3, so
        // ApplyAppearance is a no-op while sceneBatched.
        private bool sceneBatched;
        private readonly Dictionary<MeshGeometryModel3D, PickBucket> dictionary_PickBucket = new Dictionary<MeshGeometryModel3D, PickBucket>();
        private readonly Dictionary<Guid, IJSAMObject> dictionary_BatchedObject = new Dictionary<Guid, IJSAMObject>();

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

        // Field of view of the perspective camera, remembered across an orthographic toggle so the
        // perspective look is restored exactly when switching back (the OrthographicCamera carries a
        // Width instead of a field of view). Defaults to the constructor PerspectiveCamera's 45.
        private double fieldOfView_Perspective = 45.0;

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

                // View-cube interaction: drag the cube to orbit (IsViewCubeMoverEnabled) and click
                // edges/corners to snap to diagonal/isometric views (IsViewCubeEdgeClicksEnabled), not
                // only the flat faces. The default library cube (Front/Back/Left/Right/Top/Bottom) is
                // kept - no custom face texture.
                IsViewCubeMoverEnabled = true,
                IsViewCubeEdgeClicksEnabled = true,
                ModelUpDirection = new Media3D.Vector3D(0, 0, 1),

                // Centre of rotation / zoom follows the cursor (issue #32 item 1, and the earlier
                // "zoom to mouse does not work" report): rotate and zoom around the point under the
                // mouse where the gesture starts. When the user has a selection, UpdateRotationPivot
                // switches the rotation centre to the selected objects' centroid (FixedRotationPoint)
                // - the "or the currently selected element" half of the request.
                RotateAroundMouseDownPoint = true,
                ZoomAroundMouseDownPoint = true
            };

            // Middle-button drag pans (CAD convention / the "middle click does not pan" report), plus
            // Shift+Left to mirror the legacy Helix viewport's pan gesture (ViewportControl sets
            // helixViewport3D.PanGesture to Shift+Left). Viewport3DX configures gestures through
            // InputBindings + ViewportCommands, not gesture properties. Left stays free for selection,
            // right for the context menu, wheel for zoom; the stock right-button rotate is kept.
            viewport3DX.InputBindings.Add(new MouseBinding(ViewportCommands.Pan, new MouseGesture(MouseAction.MiddleClick)));
            viewport3DX.InputBindings.Add(new MouseBinding(ViewportCommands.Pan, new MouseGesture(MouseAction.LeftClick, ModifierKeys.Shift)));

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

            // Right-drag orbits and right-click opens the context menu - the same button. Track whether
            // the right button was dragged (orbit) and, if so, suppress the context menu so it no longer
            // pops open at the end of every orbit (Rhino behaviour: drag = orbit, click = menu). The
            // handler is registered here, before ViewportControl subscribes, so cancelling it (Handled)
            // stops the menu the host would otherwise build.
            viewport3DX.PreviewMouseRightButtonDown += Viewport3DX_PreviewMouseRightButtonDown;
            ContextMenuOpening += SharpDXViewportControl_ContextMenuOpening;

            // The viewport binds single letter keys to camera view commands by default (F/B/L/R/U/D ->
            // front/back/... views). Those swallow the app's global shortcuts (U = Unhide All, R =
            // Reverse, F = Select by Filter, handled in AnalyticalWindow.Window_KeyDown) before they
            // bubble out of the viewport. Drop every no-modifier key binding so the keystrokes reach the
            // window; the clickable view cube still snaps to faces. Re-applied on Loaded in case the
            // defaults are (re)added when the template is applied.
            RemoveConflictingKeyBindings();

            Children.Add(viewport3DX);
        }

        // Right-drag (orbit) vs right-click (context menu) discrimination - see the ctor. Set on
        // right-button-down, flipped once the mouse moves past the drag threshold while the right
        // button is held, read in SharpDXViewportControl_ContextMenuOpening.
        private Point rightButtonDownPoint;
        private bool rightButtonDragged;
        private const double rightDragThreshold = 4;

        private void Viewport3DX_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            rightButtonDownPoint = e.GetPosition(this);
            rightButtonDragged = false;
        }

        private void SharpDXViewportControl_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            // Orbit just happened (right-drag) - don't open the menu the host is about to build.
            if (rightButtonDragged)
            {
                e.Handled = true;
            }
        }

        private void RemoveConflictingKeyBindings()
        {
            for (int i = viewport3DX.InputBindings.Count - 1; i >= 0; i--)
            {
                if (viewport3DX.InputBindings[i] is KeyBinding keyBinding && keyBinding.Modifiers == ModifierKeys.None)
                {
                    viewport3DX.InputBindings.RemoveAt(i);
                }
            }
        }

        private static bool ResolveEnabled()
        {
            string value = Environment.GetEnvironmentVariable("SAM_UI_VIEWPORT_SHARPDX");
            return !string.IsNullOrWhiteSpace(value) && value.Trim() != "0";
        }

        private static bool ResolveBatchEnabled()
        {
            string value = Environment.GetEnvironmentVariable("SAM_UI_VIEWPORT_BATCH");
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

            // Invalidate any pending deferred-octree pass from the previous scene (generation bump -
            // see ScheduleOctreeBuild) before tearing it down.
            sceneGeneration++;
            pendingOctreeGeometries.Clear();

            // Double-buffer the regen: build the new scene BEFORE removing the old one. Convert.ToElement3Ds
            // is the ~4-5 s bulk of the rebuild and only creates new objects - it never touches the live
            // viewport - so the previous scene stays on screen throughout instead of the SharpDX render
            // thread drawing an empty viewport for the whole build (the post-undo / post-edit "white
            // screen"). The old elements are removed just before the new ones attach (below), so GPU memory
            // is not doubled. A null model means no new scene - just tear the old one down.
            // ViewportControl.ToElement3D (one level up) wraps this whole method; the .Generate/.Attach
            // split shows which phase dominates the regen (#33).
            List<Element3D> element3Ds = null;
            Convert.BatchedScene batchedScene = null;
            if (geometryObjectModel != null)
            {
                using (PerformanceLog.Measure("ViewportControl.ToElement3D.Generate"))
                {
                    // Batched path (#16): a few merged-by-material models for the whole model instead of one
                    // GroupModel3D per object. ToBatchedScene also returns the per-mesh vertex -> guid pick map
                    // and the guid -> object map, so picking/selection/events work without a model per object
                    // (increment 2); the selection visual is the overlay path (increment 3).
                    if (BatchEnabled)
                    {
                        batchedScene = geometryObjectModel.ToBatchedScene();
                        element3Ds = batchedScene == null ? null : batchedScene.Element3Ds;
                    }
                    else
                    {
                        element3Ds = Convert.ToElement3Ds(geometryObjectModel);
                    }
                }
            }

            // The new scene is built; now swap. Remove the previous elements and reset the indices.
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
            dictionary_PickBucket.Clear();
            dictionary_BatchedObject.Clear();
            sceneBatched = false;
            selectedGuids.Clear();
            hooveredGuid = null;

            // Selection is gone after a rebuild - rotation falls back to the cursor pivot
            UpdateRotationPivot();

            if (geometryObjectModel == null)
            {
                return;
            }

            sceneBatched = batchedScene != null;

            if (element3Ds != null)
            {
                using (PerformanceLog.Measure("ViewportControl.ToElement3D.Attach", string.Format("[{0} objects]", element3Ds.Count)))
                {
                    if (sceneBatched)
                    {
                        AttachBatched(batchedScene);
                    }
                    else
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
                                CollectOctreeGeometries(element3D);

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
                }
            }

            if (wasEmpty)
            {
                using (PerformanceLog.Measure("ViewportControl.ToElement3D.Camera"))
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

            // Build the picking octrees off the regen critical path (see pendingOctreeGeometries).
            ScheduleOctreeBuild();
        }

        // Batched attach (#16 increment 2): add the few merged models, index the per-mesh pick map and the
        // guid -> object map, build one event-payload stub per object (parity with the per-object path), and
        // queue the merged mesh geometries for the deferred picking octree so FindHits stays cheap.
        private void AttachBatched(Convert.BatchedScene batchedScene)
        {
            if (batchedScene.PickMap != null)
            {
                foreach (KeyValuePair<MeshGeometryModel3D, PickBucket> keyValuePair in batchedScene.PickMap)
                {
                    dictionary_PickBucket[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            foreach (Element3D element3D in batchedScene.Element3Ds)
            {
                viewport3DX.Items.Add(element3D);
                sceneElement3Ds.Add(element3D);

                if (element3D is MeshGeometryModel3D meshGeometryModel3D && meshGeometryModel3D.Geometry is HelixToolkit.SharpDX.MeshGeometry3D meshGeometry3D)
                {
                    pendingOctreeGeometries.Add(meshGeometry3D);
                }
            }

            if (batchedScene.Objects != null)
            {
                foreach (KeyValuePair<Guid, IJSAMObject> keyValuePair in batchedScene.Objects)
                {
                    dictionary_BatchedObject[keyValuePair.Key] = keyValuePair.Value;

                    Media3D.ModelVisual3D stub = new Media3D.ModelVisual3D();
                    Core.UI.WPF.Modify.SetIJSAMObject(stub, keyValuePair.Value);
                    dictionary_Stub[keyValuePair.Key] = stub;
                }
            }
        }

        // Object identity helpers that work in both modes: per-object (dictionary_Element3D) or batched
        // (dictionary_BatchedObject). KnowsGuid gates selection; ResolveSAMObject mirrors
        // Core.UI.WPF.Query.JSAMObject<SAMObject> for a batched payload object.
        private bool KnowsGuid(Guid guid)
        {
            return sceneBatched ? dictionary_BatchedObject.ContainsKey(guid) : dictionary_Element3D.ContainsKey(guid);
        }

        private static SAMObject ResolveSAMObject(IJSAMObject jSAMObject)
        {
            if (jSAMObject is SAMObject sAMObject)
            {
                return sAMObject;
            }

            if (jSAMObject is ITaggable taggable && taggable.Tag != null && taggable.Tag.Value is SAMObject tagged)
            {
                return tagged;
            }

            return null;
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
                    if (KnowsGuid(guid))
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

            UpdateRotationPivot();
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

            UpdateRotationPivot();
            return true;
        }

        public List<SAMObject> SelectedSAMObjects()
        {
            List<SAMObject> result = new List<SAMObject>();
            foreach (Guid guid in selectedGuids)
            {
                SAMObject sAMObject;
                if (sceneBatched)
                {
                    sAMObject = dictionary_BatchedObject.TryGetValue(guid, out IJSAMObject jSAMObject) ? ResolveSAMObject(jSAMObject) : null;
                }
                else
                {
                    sAMObject = dictionary_Element3D.TryGetValue(guid, out Element3D element3D) ? Core.UI.WPF.Query.JSAMObject<SAMObject>(element3D) : null;
                }

                if (sAMObject != null)
                {
                    result.Add(sAMObject);
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

                // Build() no longer builds octrees (deferred on the Load path); for this small per-object
                // rebuild do it inline so the re-skinned object is immediately pickable.
                BuildOctreesNow(groupModel3D);

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
                if (sceneBatched)
                {
                    if (dictionary_BatchedObject.TryGetValue(guid, out IJSAMObject jSAMObject) && ResolveSAMObject(jSAMObject) is T)
                    {
                        return true;
                    }
                }
                else if (dictionary_Element3D.TryGetValue(guid, out Element3D element3D) && Core.UI.WPF.Query.JSAMObject<T>(element3D) != null)
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
        /// True when the 3D camera is orthographic. Setting it switches projection, preserving the
        /// view framing (issue #37 Phase D). Mirrors HelixViewport3D.Orthographic.
        /// </summary>
        public bool Orthographic
        {
            get
            {
                return viewport3DX.Camera is OrthographicCamera;
            }

            set
            {
                SetOrthographic(value);
            }
        }

        /// <summary>
        /// Toggles the 3D camera between perspective and orthographic projection (Ctrl+Shift+O,
        /// parity with the Helix OrthographicToggleGesture / HelixViewport3D.Orthographic).
        /// </summary>
        public void ToggleProjection()
        {
            SetOrthographic(!(viewport3DX.Camera is OrthographicCamera));
        }

        // Swaps the viewport camera to the requested projection, carrying Position/LookDirection/
        // UpDirection and the clip planes across so the view does not jump. Perspective -> orthographic
        // derives the orthographic Width from the field of view and the look-at distance
        // (width = 2 * d * tan(fov/2)), so the on-screen scale is continuous; the reverse restores the
        // remembered field of view. Projection is intentionally not persisted in the camera/view
        // settings - the Helix path doesn't persist it either - so a reloaded view opens in perspective.
        private void SetOrthographic(bool orthographic)
        {
            // The clip planes live on ProjectionCamera (the shared base of both camera types), not the
            // Camera base; carry them across so the switch doesn't reset the depth range.
            HelixToolkit.Wpf.SharpDX.ProjectionCamera projectionCamera = viewport3DX.Camera as HelixToolkit.Wpf.SharpDX.ProjectionCamera;
            if (projectionCamera == null)
            {
                return;
            }

            if (orthographic)
            {
                if (projectionCamera is OrthographicCamera)
                {
                    return;
                }

                if (projectionCamera is HelixToolkit.Wpf.SharpDX.PerspectiveCamera perspectiveCamera)
                {
                    fieldOfView_Perspective = perspectiveCamera.FieldOfView;
                }

                double distance = projectionCamera.LookDirection.Length;
                double width = 2.0 * distance * System.Math.Tan(0.5 * fieldOfView_Perspective * System.Math.PI / 180.0);
                if (width <= 0 || double.IsNaN(width) || double.IsInfinity(width))
                {
                    width = distance > 0 ? distance : 1.0;
                }

                viewport3DX.Camera = new OrthographicCamera
                {
                    Position = projectionCamera.Position,
                    LookDirection = projectionCamera.LookDirection,
                    UpDirection = projectionCamera.UpDirection,
                    NearPlaneDistance = projectionCamera.NearPlaneDistance,
                    FarPlaneDistance = projectionCamera.FarPlaneDistance,
                    Width = width
                };
            }
            else
            {
                if (projectionCamera is HelixToolkit.Wpf.SharpDX.PerspectiveCamera)
                {
                    return;
                }

                viewport3DX.Camera = new HelixToolkit.Wpf.SharpDX.PerspectiveCamera
                {
                    Position = projectionCamera.Position,
                    LookDirection = projectionCamera.LookDirection,
                    UpDirection = projectionCamera.UpDirection,
                    NearPlaneDistance = projectionCamera.NearPlaneDistance,
                    FarPlaneDistance = projectionCamera.FarPlaneDistance,
                    FieldOfView = fieldOfView_Perspective
                };
            }
        }

        /// <summary>
        /// Zooms to the combined extents of the given objects ("Zoom Selected", issue #32 / #13).
        /// Bounds are taken from the merged mesh/line positions (world space - the scene has no
        /// per-object transforms). Returns false when none of the guids are present or have geometry.
        ///
        /// Frames the object by re-aiming the camera at the bounds centre, not via
        /// Viewport3DX.ZoomExtents(Rect3D): that overload only pulls the camera in/out along the
        /// current look direction, so it leaves the camera pointed at the old target (the rotation
        /// point) and the selected object off to the side - the "zooms away from the element" report
        /// (issue #32 item 3). Here the look direction is kept but the camera is moved onto the line
        /// through the bounds centre at a distance that fits the bounding sphere in the field of view.
        /// </summary>
        public bool Zoom(IEnumerable<Guid> guids)
        {
            if (!TryGetBounds(guids, out Vector3 min, out Vector3 max))
            {
                return false;
            }

            Vector3 center = (min + max) * 0.5f;
            float radius = (max - min).Length() * 0.5f;
            FrameCamera(center, radius);
            return true;
        }

        // Combined world-space bounds of the given objects' merged mesh/line geometry. False when
        // none of the guids are present or carry geometry.
        private bool TryGetBounds(IEnumerable<Guid> guids, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);

            if (guids == null)
            {
                return false;
            }

            bool any = false;
            foreach (Guid guid in guids)
            {
                if (!dictionary_Element3D.TryGetValue(guid, out Element3D element3D) || !(element3D is GroupModel3D groupModel3D))
                {
                    continue;
                }

                foreach (Element3D element3D_Child in groupModel3D.Children)
                {
                    Geometry3D geometry3D = (element3D_Child as MeshGeometryModel3D)?.Geometry ?? (element3D_Child as LineGeometryModel3D)?.Geometry;
                    if (geometry3D?.Positions == null)
                    {
                        continue;
                    }

                    foreach (Vector3 position in geometry3D.Positions)
                    {
                        any = true;
                        min = Vector3.Min(min, position);
                        max = Vector3.Max(max, position);
                    }
                }
            }

            return any;
        }

        // Centre of rotation tracks the selection (issue #32 item 1): with objects selected, rotation
        // pivots around their combined centre (FixedRotationPoint); with nothing selected it falls back
        // to RotateAroundMouseDownPoint (the point under the cursor). Called on every selection change.
        private void UpdateRotationPivot()
        {
            if (selectedGuids.Count > 0 && TryGetBounds(selectedGuids, out Vector3 min, out Vector3 max))
            {
                Vector3 center = (min + max) * 0.5f;
                viewport3DX.FixedRotationPoint = new Media3D.Point3D(center.X, center.Y, center.Z);
                viewport3DX.FixedRotationPointEnabled = true;
            }
            else
            {
                viewport3DX.FixedRotationPointEnabled = false;
            }
        }

        // Moves the camera so the given world sphere fills the view, keeping the current look
        // direction and up. Shared by Zoom (above): the distance fits the sphere in the perspective
        // field of view, with a small margin so the object is not flush against the edges.
        private void FrameCamera(Vector3 center, float radius)
        {
            HelixToolkit.Wpf.SharpDX.Camera camera = viewport3DX.Camera;
            if (camera == null)
            {
                return;
            }

            if (radius <= 0)
            {
                // A single degenerate point still deserves a sensible standoff
                radius = 1f;
            }

            Media3D.Vector3D lookDirection = camera.LookDirection;
            if (lookDirection.Length < 1e-6)
            {
                lookDirection = new Media3D.Vector3D(-1, -1, -1);
            }

            lookDirection.Normalize();

            double fieldOfView = (camera as HelixToolkit.Wpf.SharpDX.PerspectiveCamera)?.FieldOfView ?? fieldOfView_Perspective;
            double distance = 1.1 * radius / System.Math.Sin(0.5 * fieldOfView * System.Math.PI / 180.0);

            Media3D.Point3D centerPoint = new Media3D.Point3D(center.X, center.Y, center.Z);
            camera.Position = centerPoint - lookDirection * distance;
            camera.LookDirection = lookDirection * distance;

            // An orthographic camera ignores distance for scale, so fit the sphere by its Width
            // (the perspective branch above already framed it through position/distance).
            if (camera is OrthographicCamera orthographicCamera)
            {
                orthographicCamera.Width = 2.2 * radius;
            }

            // Keep world Z up here too (Zoom Selected), so framing a selection after rotating around
            // an off-axis pivot does not leave the view rolled - same intent as ZoomExtents.
            LevelCameraUp();
        }

        /// <summary>
        /// Zooms to the scene extents; deferred until the viewport is loaded with a non-zero size
        /// (zooming a size-less viewport derives a degenerate camera - Phase A, PR #30).
        /// </summary>
        public void ZoomExtents()
        {
            if (viewport3DX.IsLoaded && viewport3DX.ActualWidth > 0)
            {
                // Frame with our own bounds + FrameCamera (the Zoom Selected path) rather than the
                // built-in Viewport3DX.ZoomExtents. The built-in mis-frames a single small isolated
                // object - the camera ends up too far / off to the side, so the element falls outside
                // the view ("out of visual extent" on an isolated tiny element). FrameCamera fits the
                // bounding sphere in the field of view and keeps world Z up (the "rotates off the Z
                // axis" report). dictionary_Element3D holds only the currently visible objects (hidden
                // ones are dropped from the rebuilt scene on isolate/hide), so its bounds are exactly
                // the visible extents. Fall back to the built-in when there is no geometry to measure.
                if (TryGetBounds(dictionary_Element3D.Keys, out Vector3 min, out Vector3 max))
                {
                    Vector3 center = (min + max) * 0.5f;
                    float radius = (max - min).Length() * 0.5f;
                    FrameCamera(center, radius);
                }
                else
                {
                    LevelCameraUp();
                    viewport3DX.ZoomExtents();
                }

                return;
            }

            zoomExtentsPending = true;
        }

        // Re-levels the camera so world Z stays "up" in the view (the horizon stays level).
        // After rotating around an off-axis pivot the camera's up direction can roll; the built-in
        // Viewport3DX.ZoomExtents preserves that roll, so framing a tiny isolated building leaves the
        // view tilted (the "rotates it in a strange tilted way" report). Re-projects the up direction
        // onto the plane perpendicular to the look direction, aligned with world Z. No-op when looking
        // straight up/down (Z is degenerate there - keep whatever up the camera had).
        private void LevelCameraUp()
        {
            HelixToolkit.Wpf.SharpDX.Camera camera = viewport3DX.Camera;
            if (camera == null)
            {
                return;
            }

            Media3D.Vector3D lookDirection = camera.LookDirection;
            if (lookDirection.Length < 1e-6)
            {
                return;
            }

            lookDirection.Normalize();

            Media3D.Vector3D worldUp = new Media3D.Vector3D(0, 0, 1);
            Media3D.Vector3D up = worldUp - Media3D.Vector3D.DotProduct(worldUp, lookDirection) * lookDirection;
            if (up.Length < 1e-6)
            {
                // Looking straight along Z; nothing to level against - leave the up direction as is.
                return;
            }

            up.Normalize();
            camera.UpDirection = up;
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

        // Queues a group's mesh geometries for the deferred octree pass (the bulk Load path).
        private void CollectOctreeGeometries(Element3D element3D)
        {
            if (!(element3D is GroupModel3D groupModel3D))
            {
                return;
            }

            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                if (element3D_Child is MeshGeometryModel3D meshGeometryModel3D && meshGeometryModel3D.Geometry is HelixToolkit.SharpDX.MeshGeometry3D meshGeometry3D)
                {
                    pendingOctreeGeometries.Add(meshGeometry3D);
                }
            }
        }

        // Build the per-geometry picking octrees after the scene is attached, on a Background dispatcher
        // tick so it stays off the regen critical path (issue #33 follow-up). Until it runs, FindHits
        // falls back to a correct linear triangle test. A newer Load bumps sceneGeneration and cancels a
        // stale pass; the captured list keeps this independent of later pendingOctreeGeometries churn.
        private void ScheduleOctreeBuild()
        {
            if (pendingOctreeGeometries.Count == 0)
            {
                return;
            }

            int generation = sceneGeneration;
            List<HelixToolkit.SharpDX.MeshGeometry3D> geometries = new List<HelixToolkit.SharpDX.MeshGeometry3D>(pendingOctreeGeometries);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (generation != sceneGeneration)
                {
                    return;
                }

                System.Diagnostics.Stopwatch stopwatch = PerformanceLog.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
                foreach (HelixToolkit.SharpDX.MeshGeometry3D meshGeometry3D in geometries)
                {
                    if (generation != sceneGeneration)
                    {
                        return;
                    }

                    meshGeometry3D?.UpdateOctree();
                }

                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    PerformanceLog.Write("ViewportControl.SharpDX.OctreeBuild", string.Format("[{0} geometries, deferred]", geometries.Count), stopwatch.Elapsed.TotalMilliseconds);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // Build a single group's octrees immediately - used by the per-object RefreshAppearance path,
        // where the object count is small and the user may hover the re-skinned object right away.
        private void BuildOctreesNow(Element3D element3D)
        {
            if (!(element3D is GroupModel3D groupModel3D))
            {
                return;
            }

            foreach (Element3D element3D_Child in groupModel3D.Children)
            {
                if (element3D_Child is MeshGeometryModel3D meshGeometryModel3D && meshGeometryModel3D.Geometry is HelixToolkit.SharpDX.MeshGeometry3D meshGeometry3D)
                {
                    meshGeometry3D.UpdateOctree();
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
            // Batched mode has no per-object model to recolour - the selection/hover visual is the overlay
            // mesh path (#16 increment 3). Selection state + events still flow; only the in-view highlight
            // waits. So this is a no-op while batched.
            if (sceneBatched)
            {
                return;
            }

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

                    if (sceneBatched)
                    {
                        // Batched: one merged mesh holds many objects. Resolve the hit triangle's vertex
                        // index to its owning object's guid via that mesh's pick map (#16 increment 2).
                        if (hitTestResult.ModelHit is MeshGeometryModel3D meshGeometryModel3D
                            && dictionary_PickBucket.TryGetValue(meshGeometryModel3D, out PickBucket pickBucket)
                            && hitTestResult.TriangleIndices != null)
                        {
                            Guid guid_Batched = pickBucket.Resolve(hitTestResult.TriangleIndices.Item1);
                            if (guid_Batched != Guid.Empty)
                            {
                                return guid_Batched;
                            }
                        }

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
            // Track a right-button drag (orbit) so the context menu can be suppressed at the end of it
            // (see SharpDXViewportControl_ContextMenuOpening).
            if (e.RightButton == MouseButtonState.Pressed && !rightButtonDragged)
            {
                Point point = e.GetPosition(this);
                if (System.Math.Abs(point.X - rightButtonDownPoint.X) >= rightDragThreshold || System.Math.Abs(point.Y - rightButtonDownPoint.Y) >= rightDragThreshold)
                {
                    rightButtonDragged = true;
                }
            }

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
                UpdateRotationPivot();
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
            }
        }

        private void Viewport3DX_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ClearSelection())
            {
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                return;
            }

            // Ctrl+Shift+O toggles perspective <-> orthographic projection - parity with the Helix
            // path, whose HelixViewport3D.OrthographicToggleGesture defaults to Ctrl+Shift+O.
            if (e.Key == Key.O && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                ToggleProjection();
                e.Handled = true;
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
            RemoveConflictingKeyBindings();

            if (zoomExtentsPending)
            {
                zoomExtentsPending = false;

                // Loaded priority: run after the pending layout pass so ActualWidth is real
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ZoomExtents));
            }
        }
    }
}
