// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SAM.Core.UI;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Media3D = System.Windows.Media.Media3D;

namespace SAM.Geometry.UI.WPF
{
    /// <summary>
    /// Phase A spike of the SharpDX 3D viewport port (issue #18 gate 3, see
    /// documentation/3d-viewport-sharpdx-port-plan.md). Hosts a HelixToolkit.Wpf.SharpDX
    /// Viewport3DX with a trivial lit scene to prove, on the real target machines, that:
    /// - the DirectX 11 device initializes (real GPU or WARP - integrated GPUs, RDP, VMs),
    /// - the D3D surface composes with the surrounding WPF content (no airspace issues),
    /// - the device survives tab switching and repeated tab open/close, and shuts down cleanly.
    ///
    /// Off by default - enable with the SAM_UI_VIEWPORT_SHARPDX environment variable (any value
    /// other than empty/"0"). With the flag off nothing is created and behavior is identical to
    /// before. The trivial scene is replaced by the real ToElement3D conversion in Phase B.
    /// </summary>
    internal static class SharpDXViewportSpike
    {
        public static readonly bool Enabled = ResolveEnabled();

        // The EffectsManager owns the DX11 device and is IDisposable. One instance per process,
        // shared by every ViewportControl (one per tab) - WPF unloads tab content on every tab
        // switch, so the device must not be tied to a single control's lifetime. It is disposed
        // when the UI dispatcher shuts down; individual Viewport3DX instances attach/detach their
        // render hosts on Loaded/Unloaded without touching the device.
        private static IEffectsManager effectsManager;

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
        /// Creates the spike viewport: a Viewport3DX on the shared device rendering a unit box with
        /// ambient + directional light, coordinate system and frame-rate overlay (the overlay also
        /// exercises the Direct2D text path). Mouse orbit/pan/zoom use the Viewport3DX defaults.
        /// </summary>
        public static FrameworkElement CreateViewport()
        {
            Viewport3DX viewport3DX;
            using (PerformanceLog.Measure("ViewportControl.SharpDX.CreateViewport"))
            {
                viewport3DX = new Viewport3DX
                {
                    EffectsManager = GetEffectsManager(),
                    Camera = new HelixToolkit.Wpf.SharpDX.PerspectiveCamera
                    {
                        Position = new Media3D.Point3D(2.5, 2.5, 2.5),
                        LookDirection = new Media3D.Vector3D(-2.5, -2.5, -2.5),
                        UpDirection = new Media3D.Vector3D(0, 0, 1),
                        NearPlaneDistance = 0.1,
                        FarPlaneDistance = 1000
                    },
                    BackgroundColor = Colors.WhiteSmoke,
                    ShowCoordinateSystem = true,
                    ShowViewCube = false,
                    ShowFrameRate = true,
                    Title = "SharpDX spike",
                    ZoomExtentsWhenLoaded = true
                };

                viewport3DX.Items.Add(new AmbientLight3D { Color = Color.FromRgb(64, 64, 64) });
                viewport3DX.Items.Add(new DirectionalLight3D { Color = Colors.White, Direction = new Media3D.Vector3D(-1, -1, -1) });
                viewport3DX.Items.Add(new MeshGeometryModel3D { Geometry = BoxMesh(), Material = PhongMaterials.Blue });
            }

            return viewport3DX;
        }

        // Unit box centered at the origin, 4 vertices per face so each face gets a flat normal.
        // Built by hand rather than via MeshBuilder to keep the spike's API surface minimal.
        private static HelixToolkit.SharpDX.MeshGeometry3D BoxMesh()
        {
            Vector3Collection positions = new Vector3Collection();
            Vector3Collection normals = new Vector3Collection();
            IntCollection indices = new IntCollection();

            void AddQuad(Vector3 point_1, Vector3 point_2, Vector3 point_3, Vector3 point_4, Vector3 normal)
            {
                int index = positions.Count;

                positions.Add(point_1);
                positions.Add(point_2);
                positions.Add(point_3);
                positions.Add(point_4);

                for (int i = 0; i < 4; i++)
                {
                    normals.Add(normal);
                }

                indices.Add(index);
                indices.Add(index + 1);
                indices.Add(index + 2);
                indices.Add(index);
                indices.Add(index + 2);
                indices.Add(index + 3);
            }

            const float s = 0.5f;

            AddQuad(new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(s, -s, s), new Vector3(1, 0, 0));
            AddQuad(new Vector3(-s, s, -s), new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-1, 0, 0));
            AddQuad(new Vector3(s, s, -s), new Vector3(-s, s, -s), new Vector3(-s, s, s), new Vector3(s, s, s), new Vector3(0, 1, 0));
            AddQuad(new Vector3(-s, -s, -s), new Vector3(s, -s, -s), new Vector3(s, -s, s), new Vector3(-s, -s, s), new Vector3(0, -1, 0));
            AddQuad(new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s), new Vector3(0, 0, 1));
            AddQuad(new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, -s, -s), new Vector3(-s, -s, -s), new Vector3(0, 0, -1));

            return new HelixToolkit.SharpDX.MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                Indices = indices
            };
        }
    }
}
