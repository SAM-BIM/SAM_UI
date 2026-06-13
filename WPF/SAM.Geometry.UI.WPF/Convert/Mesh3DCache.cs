// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SAM.Geometry.UI.WPF
{
    public static partial class Convert
    {
        // Cache of the triangulated Mesh3D per Face3D, shared by both 3D render paths
        // (SharpDX ToElement3Ds and Helix ToMedia3D). Spatial.Create.Mesh3D(face3D) triangulation
        // is the dominant cost of a warm 3D regen on a large model (ViewportControl.ToElement3D
        // ~6-8 s on the 10k model, issue #33) and is purely geometric, so it is memoized like the
        // space-shell / section / panel-face caches upstream in SAM.Analytical.UI.
        //
        // There is no object guid at this layer - the consumer is a bare Face3D - so the cache is
        // keyed directly by a face-geometry signature (every edge vertex, rounded to mm). A geometry
        // edit produces a different signature and re-triangulates; an identical face (the common case
        // on a warm regen, where the upstream panel-face cache hands back the same geometry) is a hit.
        // The cached Mesh3D is consumed read-only downstream (point / triangle-index reads in AddMesh
        // and ToMedia3D), so sharing one instance across regens is safe.
        private static readonly Dictionary<string, Mesh3D> mesh3DCache = new Dictionary<string, Mesh3D>();
        private static readonly object mesh3DCacheLock = new object();

        // Static and keyed by per-face signature, so a long-running session that opens many large
        // models (each with fresh geometry) would otherwise grow without bound. Cap the entry count
        // and clear when a new key would exceed it - the cap is well above any single model's face
        // count, so the active model stays fully cached and only cross-model accumulation is bounded.
        // (Mirrors the maxCachedSpaces / maxCachedPanels caps in GeometryObjectModel.)
        private const int maxCachedMeshes = 500000;

        // Round to mm for the cache signature so float noise does not cause spurious misses.
        private static string Mesh3DSig(double value)
        {
            return Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);
        }

        // Signature of a Face3D: every edge (outer + inner) vertex, rounded to mm. O(vertices) - cheap
        // relative to triangulation. Captures the full loop geometry that triangulation depends on.
        private static string Face3DSignature(Face3D face3D)
        {
            if (face3D == null)
            {
                return null;
            }

            List<IClosedPlanar3D> edge3Ds = face3D.GetEdge3Ds();
            if (edge3Ds == null || edge3Ds.Count == 0)
            {
                return null;
            }

            StringBuilder stringBuilder = new StringBuilder();
            foreach (IClosedPlanar3D edge3D in edge3Ds)
            {
                List<Point3D> point3Ds = (edge3D as ISegmentable3D)?.GetPoints();
                if (point3Ds == null)
                {
                    continue;
                }

                foreach (Point3D point3D in point3Ds)
                {
                    stringBuilder.Append(Mesh3DSig(point3D.X)).Append(',').Append(Mesh3DSig(point3D.Y)).Append(',').Append(Mesh3DSig(point3D.Z)).Append(';');
                }

                stringBuilder.Append('#');
            }

            return stringBuilder.ToString();
        }

        // Returns the triangulated Mesh3D for a face, reusing a cached instance when an identically
        // shaped face has already been triangulated. Falls back to a direct triangulation (and does
        // not cache) when the face has no signable geometry.
        internal static Mesh3D CachedMesh3D(Face3D face3D)
        {
            if (face3D == null)
            {
                return null;
            }

            string signature = Face3DSignature(face3D);
            if (signature == null)
            {
                return Spatial.Create.Mesh3D(face3D);
            }

            lock (mesh3DCacheLock)
            {
                if (mesh3DCache.TryGetValue(signature, out Mesh3D cached))
                {
                    return cached;
                }
            }

            Mesh3D mesh3D = Spatial.Create.Mesh3D(face3D);

            lock (mesh3DCacheLock)
            {
                if (mesh3DCache.Count >= maxCachedMeshes && !mesh3DCache.ContainsKey(signature))
                {
                    mesh3DCache.Clear();
                }

                mesh3DCache[signature] = mesh3D;
            }

            return mesh3D;
        }
    }
}
