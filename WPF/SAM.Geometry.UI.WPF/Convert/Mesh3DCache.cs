// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading;

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

        // Diagnostic counters for a single top-level build (reset by ResetMesh3DCacheStats, read by
        // LogMesh3DCacheStats). Triangulation is the suspected #1 cost of ViewportControl.ToElement3D
        // (#33), but that timer wraps the whole scene build; splitting out cache hits / misses / the
        // remaining (miss-path) triangulation time shows whether the cache is actually hit and how much
        // triangulation cost is left - same diagnostic split as View3D.Panels.FixEdges upstream. Counts
        // are cheap and always kept; the miss-path stopwatch runs only when the performance log is on.
        private static long mesh3DCacheHits;
        private static long mesh3DCacheMisses;
        private static double mesh3DTriangulateMilliseconds;
        private static readonly object mesh3DStatsLock = new object();

        // PROTOTYPE (SAM-BIM/SAM#16, improvement #4) - cache of the per-face edge/segment endpoints feeding
        // the SharpDX curve build, flattened as consecutive start/end Vector3 pairs (outer boundary + holes).
        // PR #36 measured ToElement3D.Append at ~1.2 s on the 10k model and pinned the cost to this per-face
        // edge/segment work (GetEdge3Ds -> GetSegments -> ToVector3), NOT the mesh-array copy in AddMesh
        // (memoizing that gave no measurable gain and was reverted - do not re-attempt). The endpoints are
        // purely geometric and appearance-independent (color / opacity / thickness are applied at use time),
        // so they are memoized by the same Face3D signature and cap as the triangulation cache.
        // Unconfirmed: a 10k re-run must show ToElement3D.Append drop with [N hits] before this is relied on.
        private static readonly Dictionary<string, List<Vector3>> faceEdgeSegmentCache = new Dictionary<string, List<Vector3>>();
        private static readonly object faceEdgeSegmentCacheLock = new object();
        private static long faceEdgeSegmentCacheHits;
        private static long faceEdgeSegmentCacheMisses;

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
                return Triangulate(face3D);
            }

            lock (mesh3DCacheLock)
            {
                if (mesh3DCache.TryGetValue(signature, out Mesh3D cached))
                {
                    Interlocked.Increment(ref mesh3DCacheHits);
                    return cached;
                }
            }

            Mesh3D mesh3D = Triangulate(face3D);

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

        // Cache miss: run the (expensive) triangulation, counting it and - when the performance log is
        // enabled - timing it so the remaining miss-path cost is visible in LogMesh3DCacheStats.
        private static Mesh3D Triangulate(Face3D face3D)
        {
            Interlocked.Increment(ref mesh3DCacheMisses);

            if (!PerformanceLog.Enabled)
            {
                return Spatial.Create.Mesh3D(face3D);
            }

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Mesh3D mesh3D = Spatial.Create.Mesh3D(face3D);
            stopwatch.Stop();

            lock (mesh3DStatsLock)
            {
                mesh3DTriangulateMilliseconds += stopwatch.Elapsed.TotalMilliseconds;
            }

            return mesh3D;
        }

        // PROTOTYPE (#4): returns the face's edge/segment endpoints (flattened start/end pairs), reusing a
        // cached result when an identically shaped face has already been built. Falls back to a direct build
        // (uncached) when the face has no signable geometry. Mirrors CachedMesh3D one level over.
        internal static List<Vector3> CachedFaceEdgeSegments(Face3D face3D)
        {
            if (face3D == null)
            {
                return null;
            }

            string signature = Face3DSignature(face3D);
            if (signature == null)
            {
                return BuildFaceEdgeSegments(face3D);
            }

            lock (faceEdgeSegmentCacheLock)
            {
                if (faceEdgeSegmentCache.TryGetValue(signature, out List<Vector3> cached))
                {
                    Interlocked.Increment(ref faceEdgeSegmentCacheHits);
                    return cached;
                }
            }

            List<Vector3> result = BuildFaceEdgeSegments(face3D);

            lock (faceEdgeSegmentCacheLock)
            {
                if (faceEdgeSegmentCache.Count >= maxCachedMeshes && !faceEdgeSegmentCache.ContainsKey(signature))
                {
                    faceEdgeSegmentCache.Clear();
                }

                faceEdgeSegmentCache[signature] = result;
            }

            return result;
        }

        // Cache miss: derive the edge/segment endpoints (the work PR #36 pinned ToElement3D.Append to).
        private static List<Vector3> BuildFaceEdgeSegments(Face3D face3D)
        {
            Interlocked.Increment(ref faceEdgeSegmentCacheMisses);

            List<Vector3> result = new List<Vector3>();
            if (face3D == null)
            {
                return result;
            }

            List<IClosedPlanar3D> edge3Ds = face3D.GetEdge3Ds();
            if (edge3Ds == null)
            {
                return result;
            }

            foreach (IClosedPlanar3D edge3D in edge3Ds)
            {
                List<Segment3D> segment3Ds = (edge3D as ISegmentable3D)?.GetSegments();
                if (segment3Ds == null)
                {
                    continue;
                }

                foreach (Segment3D segment3D in segment3Ds)
                {
                    if (segment3D == null)
                    {
                        continue;
                    }

                    result.Add(new Vector3((float)segment3D[0].X, (float)segment3D[0].Y, (float)segment3D[0].Z));
                    result.Add(new Vector3((float)segment3D[1].X, (float)segment3D[1].Y, (float)segment3D[1].Z));
                }
            }

            return result;
        }

        // Zero the per-build diagnostic counters. Call once at the start of a top-level scene build.
        internal static void ResetMesh3DCacheStats()
        {
            Interlocked.Exchange(ref mesh3DCacheHits, 0);
            Interlocked.Exchange(ref mesh3DCacheMisses, 0);
            Interlocked.Exchange(ref faceEdgeSegmentCacheHits, 0);
            Interlocked.Exchange(ref faceEdgeSegmentCacheMisses, 0);
            lock (mesh3DStatsLock)
            {
                mesh3DTriangulateMilliseconds = 0;
            }
        }

        // Emit the diagnostic line: miss-path triangulation time + hit / miss counts for this build.
        // On a warm regen of an unchanged model, hits should dominate and the time should be near zero.
        internal static void LogMesh3DCacheStats(string detail)
        {
            long hits = Interlocked.Read(ref mesh3DCacheHits);
            long misses = Interlocked.Read(ref mesh3DCacheMisses);
            double milliseconds;
            lock (mesh3DStatsLock)
            {
                milliseconds = mesh3DTriangulateMilliseconds;
            }

            PerformanceLog.Write("ViewportControl.ToElement3D.Triangulate", string.Format("{0} [{1} hits / {2} misses]", detail ?? string.Empty, hits, misses), milliseconds);

            // PROTOTYPE (#4): edge/segment cache hit-rate for this build. No separate timer - the win shows as
            // a drop in ToElement3D.Append; this line just confirms the cache is actually hit on warm regens.
            long edgeHits = Interlocked.Read(ref faceEdgeSegmentCacheHits);
            long edgeMisses = Interlocked.Read(ref faceEdgeSegmentCacheMisses);
            PerformanceLog.Write("ViewportControl.ToElement3D.EdgeSegments", string.Format("{0} [{1} hits / {2} misses]", detail ?? string.Empty, edgeHits, edgeMisses), 0);
        }
    }
}
