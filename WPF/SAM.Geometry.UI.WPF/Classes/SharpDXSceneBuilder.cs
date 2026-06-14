// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SAM.Geometry.UI.WPF
{
    /// <summary>
    /// Accumulates the primitives of one model object and emits the few merged SharpDX scene
    /// elements for it (issue #18 Phase B / the #16 consolidation): one MeshGeometryModel3D per
    /// fill material, one LineGeometryModel3D per curve material and one BillboardTextModel3D per
    /// text material - instead of one Visual3D per face/segment as in the Media3D scene. Deals
    /// only in System.Numerics/System.Drawing primitives; the SAM object traversal lives in
    /// Convert.ToElement3Ds.
    ///
    /// Triangle winding/normals: the scene is lit by a single white AmbientLight3D (parity with
    /// the Helix 3D path, which is ambient-only), so shading ignores normals - they are filled
    /// with a constant. Faces render double-sided via CullMode.None, replacing the doubled
    /// triangles of the Media3D conversion.
    /// </summary>
    /// <summary>
    /// Maps a hit triangle's vertex index back to the guid of the object that owns it, within one merged
    /// (material-batched) mesh (issue #16 mesh-batching increment 2). Each object contributes a contiguous
    /// block of vertices to a bucket - they are appended one object at a time - so the owning object is the
    /// one whose recorded start is the greatest start &lt;= the vertex index (binary search).
    /// </summary>
    internal sealed class PickBucket
    {
        private readonly int[] vertexStarts;
        private readonly Guid[] guids;

        public PickBucket(int[] vertexStarts, Guid[] guids)
        {
            this.vertexStarts = vertexStarts;
            this.guids = guids;
        }

        public Guid Resolve(int vertexIndex)
        {
            if (vertexStarts == null || vertexStarts.Length == 0)
            {
                return Guid.Empty;
            }

            int low = 0;
            int high = vertexStarts.Length - 1;
            int found = -1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (vertexStarts[mid] <= vertexIndex)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return found >= 0 ? guids[found] : Guid.Empty;
        }
    }

    internal class SharpDXSceneBuilder
    {
        private class MeshBucket
        {
            public Vector3Collection Positions = new Vector3Collection();
            public IntCollection Indices = new IntCollection();

            // Pick ranges (batched build only): the starting vertex index and owning guid of each object
            // that contributed to this bucket, in vertex order. Null unless pick capture is on.
            public List<int> RangeVertexStarts;
            public List<Guid> RangeGuids;
            public bool HasRange;
            public Guid LastRangeGuid;
        }

        private class LineBucket
        {
            public Vector3Collection Positions = new Vector3Collection();
            public IntCollection Indices = new IntCollection();
        }

        private class TextBucket
        {
            public List<Tuple<string, Vector3, float>> Texts = new List<Tuple<string, Vector3, float>>();
        }

        // Color key: ARGB fill/stroke color with the appearance opacity already folded into the alpha
        private readonly Dictionary<int, MeshBucket> meshBuckets = new Dictionary<int, MeshBucket>();
        private readonly Dictionary<Tuple<int, double>, LineBucket> lineBuckets = new Dictionary<Tuple<int, double>, LineBucket>();
        private readonly Dictionary<int, TextBucket> textBuckets = new Dictionary<int, TextBucket>();

        // Pick-range capture (batched scene build, issue #16 increment 2). When on, AddMesh records which
        // object's vertices it is appending (SetCurrentObject), so Build can hand back a per-mesh
        // vertex-index -> guid map (PickMap) for resolving a FindHits result to an object without one
        // scene model per object. Off for the per-object path (one model already = one object).
        private readonly bool capturePickRanges;
        private Guid currentObjectGuid;

        // Per-merged-mesh pick map, populated by Build when capturePickRanges is on.
        public Dictionary<MeshGeometryModel3D, PickBucket> PickMap { get; private set; }

        public SharpDXSceneBuilder()
        {
        }

        public SharpDXSceneBuilder(bool capturePickRanges)
        {
            this.capturePickRanges = capturePickRanges;
        }

        // Set before appending each object's primitives (batched build) so the ranges record the right guid.
        public void SetCurrentObject(Guid guid)
        {
            currentObjectGuid = guid;
        }

        private static int ToKey(System.Drawing.Color color, double opacity)
        {
            int alpha = (int)System.Math.Round(color.A * System.Math.Max(0.0, System.Math.Min(1.0, opacity)));
            return (alpha << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        private static Color4 ToColor4(int key)
        {
            return new Color4(((key >> 16) & 0xFF) / 255f, ((key >> 8) & 0xFF) / 255f, (key & 0xFF) / 255f, ((key >> 24) & 0xFF) / 255f);
        }

        private static double ToLineThickness(double thickness)
        {
            if (thickness <= 0)
            {
                return 0;
            }

            // SAM curve thickness is a world-space tube radius in the Media3D path. SharpDX line
            // thickness is screen-space pixels, so keep the common 0.04 panel edge visible as 1 px.
            return System.Math.Max(1, thickness * 25);
        }

        public void AddMesh(System.Drawing.Color color, double opacity, IList<Vector3> positions, IList<int> triangleIndices)
        {
            if (positions == null || positions.Count == 0 || triangleIndices == null || triangleIndices.Count == 0)
            {
                return;
            }

            int key = ToKey(color, opacity);
            if (!meshBuckets.TryGetValue(key, out MeshBucket meshBucket))
            {
                meshBucket = new MeshBucket();
                meshBuckets[key] = meshBucket;
            }

            // Record a pick range when the contributing object changes (its first vertices in this bucket).
            if (capturePickRanges)
            {
                if (meshBucket.RangeVertexStarts == null)
                {
                    meshBucket.RangeVertexStarts = new List<int>();
                    meshBucket.RangeGuids = new List<Guid>();
                }

                if (!meshBucket.HasRange || meshBucket.LastRangeGuid != currentObjectGuid)
                {
                    meshBucket.RangeVertexStarts.Add(meshBucket.Positions.Count);
                    meshBucket.RangeGuids.Add(currentObjectGuid);
                    meshBucket.LastRangeGuid = currentObjectGuid;
                    meshBucket.HasRange = true;
                }
            }

            int offset = meshBucket.Positions.Count;
            foreach (Vector3 position in positions)
            {
                meshBucket.Positions.Add(position);
            }

            foreach (int triangleIndex in triangleIndices)
            {
                meshBucket.Indices.Add(offset + triangleIndex);
            }
        }

        public void AddSegment(System.Drawing.Color color, double opacity, double thickness, Vector3 start, Vector3 end)
        {
            double lineThickness = ToLineThickness(thickness);
            if (lineThickness <= 0)
            {
                return;
            }

            Tuple<int, double> key = Tuple.Create(ToKey(color, opacity), lineThickness);
            if (!lineBuckets.TryGetValue(key, out LineBucket lineBucket))
            {
                lineBucket = new LineBucket();
                lineBuckets[key] = lineBucket;
            }

            int offset = lineBucket.Positions.Count;
            lineBucket.Positions.Add(start);
            lineBucket.Positions.Add(end);
            lineBucket.Indices.Add(offset);
            lineBucket.Indices.Add(offset + 1);
        }

        public void AddText(System.Drawing.Color color, double opacity, string text, Vector3 position, double height)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int key = ToKey(color, opacity);
            if (!textBuckets.TryGetValue(key, out TextBucket textBucket))
            {
                textBucket = new TextBucket();
                textBuckets[key] = textBucket;
            }

            textBucket.Texts.Add(new Tuple<string, Vector3, float>(text, position, (float)height));
        }

        public List<Element3D> Build()
        {
            List<Element3D> result = new List<Element3D>();

            if (capturePickRanges)
            {
                PickMap = new Dictionary<MeshGeometryModel3D, PickBucket>();
            }

            foreach (KeyValuePair<int, MeshBucket> keyValuePair in meshBuckets)
            {
                Color4 color4 = ToColor4(keyValuePair.Key);

                HelixToolkit.SharpDX.MeshGeometry3D meshGeometry3D = new HelixToolkit.SharpDX.MeshGeometry3D
                {
                    Positions = keyValuePair.Value.Positions,
                    Indices = keyValuePair.Value.Indices,
                    Normals = UnitNormals(keyValuePair.Value.Positions.Count)
                };

                // Static per-geometry octree for hover/selection picking (issue #32 Phase C): FindHits
                // walks it instead of every triangle. NOT built here - on a large model UpdateOctree()
                // across ~31k geometries cost ~0.45 s on the regen critical path (issue #33 follow-up),
                // so the caller (SharpDXViewportControl.Load) builds octrees in a deferred Background
                // dispatcher pass after attach. Until then FindHits falls back to a linear triangle test,
                // which is correct and cheap for these small per-object meshes.

                // Ambient = Diffuse reproduces the flat, unshaded look of the Helix 3D path
                // (ambient-only lighting); specular off.
                PhongMaterial phongMaterial = new PhongMaterial
                {
                    DiffuseColor = color4,
                    AmbientColor = color4,
                    SpecularColor = new Color4(0, 0, 0, 0)
                };

                MeshGeometryModel3D meshGeometryModel3D = new MeshGeometryModel3D
                {
                    Geometry = meshGeometry3D,
                    Material = phongMaterial,
                    CullMode = global::SharpDX.Direct3D11.CullMode.None,
                    IsTransparent = color4.Alpha < 1f
                };

                result.Add(meshGeometryModel3D);

                if (capturePickRanges && keyValuePair.Value.RangeVertexStarts != null)
                {
                    PickMap[meshGeometryModel3D] = new PickBucket(keyValuePair.Value.RangeVertexStarts.ToArray(), keyValuePair.Value.RangeGuids.ToArray());
                }
            }

            foreach (KeyValuePair<Tuple<int, double>, LineBucket> keyValuePair in lineBuckets)
            {
                Color4 color4 = ToColor4(keyValuePair.Key.Item1);

                LineGeometry3D lineGeometry3D = new LineGeometry3D
                {
                    Positions = keyValuePair.Value.Positions,
                    Indices = keyValuePair.Value.Indices
                };

                // Lines and billboards are not pickable (IsHitTestVisible = false): the meshes define
                // an object's pickable footprint, so a click near an edge selects the face under it
                // instead of snapping to screen-space line proximity (issue #32 Phase C). Mirrors the
                // Helix path, where hit-testing is ray-mesh only.
                result.Add(new LineGeometryModel3D
                {
                    Geometry = lineGeometry3D,
                    Color = System.Windows.Media.Color.FromArgb((byte)(color4.Alpha * 255), (byte)(color4.Red * 255), (byte)(color4.Green * 255), (byte)(color4.Blue * 255)),
                    Thickness = keyValuePair.Key.Item2,
                    IsHitTestVisible = false
                });
            }

            foreach (KeyValuePair<int, TextBucket> keyValuePair in textBuckets)
            {
                Color4 color4 = ToColor4(keyValuePair.Key);

                BillboardText3D billboardText3D = new BillboardText3D();
                foreach (Tuple<string, Vector3, float> text in keyValuePair.Value.Texts)
                {
                    billboardText3D.TextInfo.Add(new TextInfo(text.Item1, text.Item2)
                    {
                        Foreground = color4,
                        // World-sized text (FixedSize = false below): the default billboard font is
                        // ~1 world unit tall at Scale = 1, so the appearance height maps to Scale.
                        // To be tuned against the Helix meshed text on the first visual comparison.
                        Scale = text.Item3
                    });
                }

                result.Add(new BillboardTextModel3D
                {
                    Geometry = billboardText3D,
                    FixedSize = false,
                    IsHitTestVisible = false
                });
            }

            return result;
        }

        // Constant normals: shading is ambient-only (see class note), so normals only need to exist.
        private static Vector3Collection UnitNormals(int count)
        {
            Vector3Collection result = new Vector3Collection(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(Vector3.UnitZ);
            }

            return result;
        }
    }
}
