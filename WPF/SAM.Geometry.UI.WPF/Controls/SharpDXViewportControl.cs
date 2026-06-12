// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SAM.Core;
using SAM.Core.UI;
using SAM.Geometry.Object;
using System;
using System.Collections.Generic;
using System.Windows;
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
    /// set (off by default; any value other than empty/"0" enables it). Phase B covers scene
    /// build, camera and zoom; hover/selection/context menu (Phase C) and the orthographic-3D
    /// camera and view chrome (Phase D) still run only on the Helix path.
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

        private bool zoomExtentsPending;

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
