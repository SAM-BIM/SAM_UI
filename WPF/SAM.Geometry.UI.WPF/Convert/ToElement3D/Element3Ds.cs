// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using HelixToolkit.Wpf.SharpDX;
using SAM.Core.UI;
using SAM.Geometry.Object;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Numerics;

namespace SAM.Geometry.UI.WPF
{
    public static partial class Convert
    {
        /// <summary>
        /// SharpDX counterpart of ToMedia3D(GeometryObjectModel) (issue #18 Phase B). Each top
        /// level ISAMGeometryObject (space, panel, ...) becomes one GroupModel3D carrying the
        /// object as its attached IJSAMObject - same tagging as the Media3D scene, so the guid
        /// index and (later) selection resolve objects the same way. Inside the group, all
        /// primitives are merged per material by SharpDXSceneBuilder (the #16 consolidation):
        /// a handful of GPU-resident models per object instead of a Visual3D per face/segment.
        /// </summary>
        public static List<Element3D> ToElement3Ds(this GeometryObjectModel geometryObjectModel)
        {
            if (geometryObjectModel == null)
            {
                return null;
            }

            List<Element3D> result = new List<Element3D>();

            // Reset the per-build diagnostic counters so the lines below reflect only this build. The
            // whole-build cost is timed one level up as ViewportControl.ToElement3D; this splits it into
            // triangulation (Mesh3DCache, #33), the SharpDX append + Build() phases, and the per-object
            // UpdateOctree slice - to find what dominates the ~5-7 s that remains once triangulation is
            // cached (issue #33 follow-up). All timing is opt-in (no overhead when the log is disabled).
            bool logEnabled = PerformanceLog.Enabled;
            if (logEnabled)
            {
                ResetMesh3DCacheStats();
                appendMilliseconds = 0;
                buildMilliseconds = 0;
            }

            List<ISAMGeometryObject> sAMGeometryObjects = geometryObjectModel.GetSAMGeometryObjects<ISAMGeometryObject>();
            if (sAMGeometryObjects != null)
            {
                foreach (ISAMGeometryObject sAMGeometryObject in sAMGeometryObjects)
                {
                    if (sAMGeometryObject == null)
                    {
                        continue;
                    }

                    List<Element3D> element3Ds = ToElement3Ds(sAMGeometryObject);
                    if (element3Ds == null || element3Ds.Count == 0)
                    {
                        continue;
                    }

                    GroupModel3D groupModel3D = new GroupModel3D();
                    foreach (Element3D element3D in element3Ds)
                    {
                        groupModel3D.Children.Add(element3D);
                    }

                    Core.UI.WPF.Modify.SetIJSAMObject(groupModel3D, sAMGeometryObject);
                    result.Add(groupModel3D);
                }
            }

            if (logEnabled)
            {
                string detail = string.Format("[{0} objects]", result.Count);
                LogMesh3DCacheStats(detail);
                // Append includes triangulation (reported separately), the per-Face3D mesh/edge build and
                // the per-bucket copy; Build is the GPU geometry + material construction (octree is now
                // deferred off this path by SharpDXViewportControl.Load).
                PerformanceLog.Write("ViewportControl.ToElement3D.Append", detail, appendMilliseconds);
                PerformanceLog.Write("ViewportControl.ToElement3D.Build", detail, buildMilliseconds);
            }

            return result;
        }

        /// <summary>
        /// Batched SharpDX scene build (issue #16 mesh-batching, behind SAM_UI_VIEWPORT_BATCH; see
        /// documentation/3d-viewport-mesh-batching-plan.md). Appends the primitives of EVERY object into a
        /// SINGLE SharpDXSceneBuilder, so the whole model emits only a few merged models per material
        /// instead of one GroupModel3D per object (~33k on the 10k-space model). This collapses the
        /// per-model GPU attach cost (ViewportControl.ToElement3D.Attach ~3.5 s), which scales with model
        /// count - the only proven lever (batch-attach of per-object models gave nothing, PR #36).
        ///
        /// INCREMENT 1 (render-only): the merged models carry NO per-object IJSAMObject tag, so picking,
        /// hover and selection are inert on this path - they are re-implemented on the batched
        /// representation (triangle-range -> guid map + overlay selection) in later increments. Use only
        /// to measure the attach/regen win for now.
        /// </summary>
        public static List<Element3D> ToBatchedElement3Ds(this GeometryObjectModel geometryObjectModel)
        {
            BatchedScene batchedScene = geometryObjectModel.ToBatchedScene();
            return batchedScene == null ? null : batchedScene.Element3Ds;
        }

        /// <summary>
        /// The batched scene plus the side data needed to keep object identity without one scene model per
        /// object (issue #16 increment 2): a per-merged-mesh vertex-index -> guid pick map, and a guid ->
        /// object map for event payloads / selection queries.
        /// </summary>
        internal sealed class BatchedScene
        {
            public List<Element3D> Element3Ds;
            public Dictionary<MeshGeometryModel3D, PickBucket> PickMap;
            public Dictionary<System.Guid, Core.IJSAMObject> Objects;
        }

        // See ToBatchedElement3Ds: same batched build, but with pick-range capture and the guid/object side
        // maps populated so picking, selection and event payloads work on the batched representation.
        internal static BatchedScene ToBatchedScene(this GeometryObjectModel geometryObjectModel)
        {
            if (geometryObjectModel == null)
            {
                return null;
            }

            bool logEnabled = PerformanceLog.Enabled;
            if (logEnabled)
            {
                ResetMesh3DCacheStats();
                appendMilliseconds = 0;
                buildMilliseconds = 0;
            }

            SharpDXSceneBuilder sharpDXSceneBuilder = new SharpDXSceneBuilder(true);
            Dictionary<System.Guid, Core.IJSAMObject> objects = new Dictionary<System.Guid, Core.IJSAMObject>();

            int objectCount = 0;
            List<ISAMGeometryObject> sAMGeometryObjects = geometryObjectModel.GetSAMGeometryObjects<ISAMGeometryObject>();
            if (sAMGeometryObjects != null)
            {
                foreach (ISAMGeometryObject sAMGeometryObject in sAMGeometryObjects)
                {
                    if (sAMGeometryObject == null)
                    {
                        continue;
                    }

                    objectCount++;

                    // A pickable object needs a guid; untagged objects still render (Guid.Empty range -> not
                    // pickable), mirroring the per-object path where only tagged objects resolve to a hit.
                    Core.SAMObject sAMObject = ResolveSAMObject(sAMGeometryObject);
                    System.Guid guid = sAMObject == null ? System.Guid.Empty : sAMObject.Guid;
                    sharpDXSceneBuilder.SetCurrentObject(guid);

                    if (sAMObject != null && !objects.ContainsKey(guid))
                    {
                        objects[guid] = sAMGeometryObject as Core.IJSAMObject ?? sAMObject;
                    }

                    if (!logEnabled)
                    {
                        Append(sharpDXSceneBuilder, sAMGeometryObject);
                        continue;
                    }

                    System.Diagnostics.Stopwatch appendStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    Append(sharpDXSceneBuilder, sAMGeometryObject);
                    appendStopwatch.Stop();
                    appendMilliseconds += appendStopwatch.Elapsed.TotalMilliseconds;
                }
            }

            List<Element3D> result;
            if (!logEnabled)
            {
                result = sharpDXSceneBuilder.Build();
            }
            else
            {
                System.Diagnostics.Stopwatch buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
                result = sharpDXSceneBuilder.Build();
                buildStopwatch.Stop();
                buildMilliseconds += buildStopwatch.Elapsed.TotalMilliseconds;

                string detail = string.Format("[{0} objects -> {1} batched models]", objectCount, result == null ? 0 : result.Count);
                LogMesh3DCacheStats(detail);
                PerformanceLog.Write("ViewportControl.ToElement3D.Append", detail, appendMilliseconds);
                PerformanceLog.Write("ViewportControl.ToElement3D.Build", detail, buildMilliseconds);
            }

            return new BatchedScene
            {
                Element3Ds = result,
                PickMap = sharpDXSceneBuilder.PickMap,
                Objects = objects
            };
        }

        // Mirrors Core.UI.WPF.Query.JSAMObject<SAMObject>: the object itself if it is a SAMObject, else the
        // SAMObject behind its ITaggable tag - so a batched object resolves to the same guid the per-object
        // path tags onto its GroupModel3D.
        private static Core.SAMObject ResolveSAMObject(ISAMGeometryObject sAMGeometryObject)
        {
            if (sAMGeometryObject is Core.SAMObject sAMObject)
            {
                return sAMObject;
            }

            if (sAMGeometryObject is Core.ITaggable taggable && taggable.Tag != null && taggable.Tag.Value is Core.SAMObject tagged)
            {
                return tagged;
            }

            return null;
        }

        // Diagnostic accumulators for the per-object Append vs Build split (see ToElement3Ds above).
        // Reset before / read after the model-level build loop; populated only when the log is enabled.
        private static double appendMilliseconds;
        private static double buildMilliseconds;

        /// <summary>
        /// Builds the merged SharpDX models for a single object (the children of its GroupModel3D),
        /// without wrapping them in a group or tagging - used both by ToElement3Ds(GeometryObjectModel)
        /// above and by the in-place appearance re-skin (SharpDXViewportControl.RefreshAppearance,
        /// issue #32), which rebuilds only the edited objects from their updated SurfaceAppearances.
        /// </summary>
        internal static List<Element3D> ToElement3Ds(ISAMGeometryObject sAMGeometryObject)
        {
            if (sAMGeometryObject == null)
            {
                return null;
            }

            SharpDXSceneBuilder sharpDXSceneBuilder = new SharpDXSceneBuilder();

            if (!PerformanceLog.Enabled)
            {
                Append(sharpDXSceneBuilder, sAMGeometryObject);
                return sharpDXSceneBuilder.Build();
            }

            // Diagnostic split (accumulated across the model build, reported by the caller). RefreshAppearance
            // also calls this path; its contribution is harmless because the caller resets before each build.
            System.Diagnostics.Stopwatch appendStopwatch = System.Diagnostics.Stopwatch.StartNew();
            Append(sharpDXSceneBuilder, sAMGeometryObject);
            appendStopwatch.Stop();
            appendMilliseconds += appendStopwatch.Elapsed.TotalMilliseconds;

            System.Diagnostics.Stopwatch buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
            List<Element3D> result = sharpDXSceneBuilder.Build();
            buildStopwatch.Stop();
            buildMilliseconds += buildStopwatch.Elapsed.TotalMilliseconds;

            return result;
        }

        // Mirrors the type dispatch of Create.Model3D, but accumulates primitives into the
        // per-object builder instead of allocating a Model3D per geometry. Collections are
        // flattened: per-element visuals are not needed once selection works per object.
        private static void Append(SharpDXSceneBuilder sharpDXSceneBuilder, ISAMGeometryObject sAMGeometryObject)
        {
            if (sharpDXSceneBuilder == null || sAMGeometryObject == null)
            {
                return;
            }

            if (sAMGeometryObject is Geometry3DObjectCollection geometry3DObjectCollection)
            {
                foreach (ISAMGeometry3DObject sAMGeometry3DObject in geometry3DObjectCollection)
                {
                    Append(sharpDXSceneBuilder, sAMGeometry3DObject);
                }

                return;
            }

            if (sAMGeometryObject is SAMGeometryObjectCollection sAMGeometryObjectCollection)
            {
                foreach (ISAMGeometryObject sAMGeometryObject_Temp in sAMGeometryObjectCollection)
                {
                    Append(sharpDXSceneBuilder, sAMGeometryObject_Temp);
                }

                return;
            }

            if (sAMGeometryObject is SAMGeometry3DObjectCollection sAMGeometry3DObjectCollection)
            {
                foreach (ISAMGeometry3DObject sAMGeometry3DObject in sAMGeometry3DObjectCollection)
                {
                    Append(sharpDXSceneBuilder, sAMGeometry3DObject);
                }

                return;
            }

            if (sAMGeometryObject is Text3DObject text3DObject)
            {
                TextAppearance textAppearance = text3DObject.TextAppearance;
                Spatial.Plane plane = text3DObject.Plane; // Spatial. qualifier: System.Numerics also has a Plane
                if (textAppearance != null && plane != null)
                {
                    sharpDXSceneBuilder.AddText(textAppearance.Color, textAppearance.Opacity, text3DObject.Text, ToVector3(plane.Origin), textAppearance.Height);
                }

                return;
            }

            if (sAMGeometryObject is IFace3DObject face3DObject_Interface)
            {
                Face3DObject face3DObject = sAMGeometryObject as Face3DObject ?? new Face3DObject(face3DObject_Interface.Face3D, Object.Query.DefaultSurfaceAppearance());

                SurfaceAppearance surfaceAppearance = face3DObject.SurfaceAppearance;
                Face3D face3D = face3DObject.Face3D;
                if (surfaceAppearance == null || face3D == null)
                {
                    return;
                }

                AddMesh(sharpDXSceneBuilder, CachedMesh3D(face3D), surfaceAppearance);

                CurveAppearance curveAppearance = surfaceAppearance.CurveAppearance;
                if (curveAppearance != null && curveAppearance.Thickness != 0)
                {
                    // PROTOTYPE (#4): reuse the cached, appearance-independent edge/segment endpoints instead of
                    // re-deriving GetEdge3Ds -> GetSegments -> ToVector3 every regen (the measured ToElement3D.Append
                    // cost, PR #36). The appearance is applied here at use time. Endpoints are flattened start/end
                    // pairs, so step by two.
                    List<Vector3> edgeSegmentEndpoints = CachedFaceEdgeSegments(face3D);
                    if (edgeSegmentEndpoints != null)
                    {
                        for (int i = 0; i + 1 < edgeSegmentEndpoints.Count; i += 2)
                        {
                            sharpDXSceneBuilder.AddSegment(curveAppearance.Color, curveAppearance.Opacity, curveAppearance.Thickness, edgeSegmentEndpoints[i], edgeSegmentEndpoints[i + 1]);
                        }
                    }
                }

                return;
            }

            if (sAMGeometryObject is IMesh3DObject mesh3DObject_Interface)
            {
                Mesh3DObject mesh3DObject = sAMGeometryObject as Mesh3DObject ?? new Mesh3DObject(mesh3DObject_Interface.Mesh3D, Object.Query.DefaultSurfaceAppearance());

                SurfaceAppearance surfaceAppearance = mesh3DObject.SurfaceAppearance;
                Mesh3D mesh3D = mesh3DObject.Mesh3D;
                if (surfaceAppearance == null || mesh3D == null)
                {
                    return;
                }

                AddMesh(sharpDXSceneBuilder, mesh3D, surfaceAppearance);

                CurveAppearance curveAppearance = surfaceAppearance.CurveAppearance;
                if (curveAppearance != null && curveAppearance.Thickness != 0)
                {
                    List<Segment3D> segment3Ds = mesh3D.GetSegments();
                    if (segment3Ds != null)
                    {
                        foreach (Segment3D segment3D in segment3Ds)
                        {
                            AddSegment(sharpDXSceneBuilder, segment3D, curveAppearance);
                        }
                    }
                }

                return;
            }

            if (sAMGeometryObject is IShellObject shellObject_Interface)
            {
                ShellObject shellObject = sAMGeometryObject as ShellObject ?? new ShellObject(shellObject_Interface.Shell, Object.Query.DefaultSurfaceAppearance());

                SurfaceAppearance surfaceAppearance = shellObject.SurfaceAppearance;
                Shell shell = shellObject.Shell;
                if (surfaceAppearance == null || shell == null)
                {
                    return;
                }

                foreach (Face3D face3D in shell.Face3Ds)
                {
                    Append(sharpDXSceneBuilder, new Face3DObject(face3D, surfaceAppearance));
                }

                return;
            }

            if (sAMGeometryObject is IExtrusionObject extrusionObject_Interface)
            {
                ExtrusionObject extrusionObject = sAMGeometryObject as ExtrusionObject ?? new ExtrusionObject(extrusionObject_Interface.Extrusion, Object.Query.DefaultSurfaceAppearance());

                SurfaceAppearance surfaceAppearance = extrusionObject.SurfaceAppearance;
                Extrusion extrusion = extrusionObject.Extrusion;
                if (surfaceAppearance == null || extrusion == null)
                {
                    return;
                }

                foreach (Face3D face3D in extrusion.Face3Ds())
                {
                    Append(sharpDXSceneBuilder, new Face3DObject(face3D, surfaceAppearance));
                }

                return;
            }

            if (sAMGeometryObject is ISphereObject sphereObject_Interface)
            {
                SphereObject sphereObject = sAMGeometryObject as SphereObject ?? new SphereObject(sphereObject_Interface.Sphere, Object.Query.DefaultSurfaceAppearance());

                SurfaceAppearance surfaceAppearance = sphereObject.SurfaceAppearance;
                Sphere sphere = sphereObject.Sphere;
                if (surfaceAppearance == null || sphere == null)
                {
                    return;
                }

                List<Face3D> face3Ds = sphere.Mesh3D(1)?.GetTriangles()?.ConvertAll(x => new Face3D(x));
                if (face3Ds != null)
                {
                    foreach (Face3D face3D in face3Ds)
                    {
                        Append(sharpDXSceneBuilder, new Face3DObject(face3D, surfaceAppearance));
                    }
                }

                return;
            }

            if (sAMGeometryObject is ISegment3DObject segment3DObject_Interface)
            {
                Segment3DObject segment3DObject = sAMGeometryObject as Segment3DObject ?? new Segment3DObject(segment3DObject_Interface.Segment3D, Object.Query.DefaultCurveAppearance());

                AddSegment(sharpDXSceneBuilder, segment3DObject.Segment3D, segment3DObject.CurveAppearance);
                return;
            }

            if (sAMGeometryObject is IPolygon3DObject polygon3DObject_Interface)
            {
                Polygon3DObject polygon3DObject = sAMGeometryObject as Polygon3DObject ?? new Polygon3DObject(polygon3DObject_Interface.Polygon3D, Object.Query.DefaultCurveAppearance());

                AddSegments(sharpDXSceneBuilder, polygon3DObject.Polygon3D, polygon3DObject.CurveAppearance);
                return;
            }

            if (sAMGeometryObject is IPoint3DObject point3DObject_Interface)
            {
                Point3DObject point3DObject = sAMGeometryObject as Point3DObject ?? new Point3DObject(point3DObject_Interface.Point3D, Object.Query.DefaultPointAppearance());

                PointAppearance pointAppearance = point3DObject.PointAppearance;
                Point3D point3D = point3DObject.Point3D;
                if (pointAppearance == null || point3D == null)
                {
                    return;
                }

                // Same sphere marker as Convert.ToMedia3D(Point3D)
                AddMesh(sharpDXSceneBuilder, Spatial.Create.Mesh3D(new Sphere(point3D, pointAppearance.Thickness), 0.01, 2, 3), pointAppearance.Color, pointAppearance.Opacity);
                return;
            }

            if (sAMGeometryObject is IPolyline3DObject polyline3DObject_Interface)
            {
                Polyline3DObject polyline3DObject = sAMGeometryObject as Polyline3DObject ?? new Polyline3DObject(polyline3DObject_Interface.Polyline3D, Object.Query.DefaultCurveAppearance());

                AddSegments(sharpDXSceneBuilder, polyline3DObject.Polyline3D, polyline3DObject.CurveAppearance);
                return;
            }

            if (sAMGeometryObject is IBoundingBox3DObject boundingBox3DObject_Interface)
            {
                BoundingBox3DObject boundingBox3DObject = sAMGeometryObject as BoundingBox3DObject ?? new BoundingBox3DObject(boundingBox3DObject_Interface.BoundingBox3D, Object.Query.DefaultCurveAppearance());

                AddSegments(sharpDXSceneBuilder, boundingBox3DObject.BoundingBox3D as ISegmentable3D, boundingBox3DObject.CurveAppearance);
                return;
            }

            if (sAMGeometryObject is IRectangle3DObject rectangle3DObject_Interface)
            {
                Rectangle3DObject rectangle3DObject = sAMGeometryObject as Rectangle3DObject ?? new Rectangle3DObject(rectangle3DObject_Interface.Rectangle3D, Object.Query.DefaultCurveAppearance());

                AddSegments(sharpDXSceneBuilder, rectangle3DObject.Rectangle3D, rectangle3DObject.CurveAppearance);
                return;
            }

            if (sAMGeometryObject is ITriangle3DObject triangle3DObject_Interface)
            {
                Triangle3DObject triangle3DObject = sAMGeometryObject as Triangle3DObject ?? new Triangle3DObject(triangle3DObject_Interface.Triangle3D, Object.Query.DefaultCurveAppearance());

                AddSegments(sharpDXSceneBuilder, triangle3DObject.Triangle3D, triangle3DObject.CurveAppearance);
                return;
            }

            if (sAMGeometryObject is ISAMGeometry3DGroupObject sAMGeometry3DGroupObject_Interface)
            {
                SAMGeometry3DGroupObject sAMGeometry3DGroupObject = sAMGeometryObject as SAMGeometry3DGroupObject ?? new SAMGeometry3DGroupObject(sAMGeometry3DGroupObject_Interface.SAMGeometry3DGroup, Object.Query.DefaultPointAppearance(), Object.Query.DefaultCurveAppearance(), Object.Query.DefaultSurfaceAppearance());

                foreach (ISAMGeometry3D sAMGeometry3D in sAMGeometry3DGroupObject)
                {
                    ISAMGeometryObject sAMGeometryObject_Temp = Object.Convert.ToSAM_ISAMGeometryObject(sAMGeometry3D, sAMGeometry3DGroupObject.PointAppearance, sAMGeometry3DGroupObject.CurveAppearance, sAMGeometry3DGroupObject.SurfaceAppearance);
                    Append(sharpDXSceneBuilder, sAMGeometryObject_Temp);
                }

                return;
            }
        }

        private static void AddMesh(SharpDXSceneBuilder sharpDXSceneBuilder, Mesh3D mesh3D, SurfaceAppearance surfaceAppearance)
        {
            if (surfaceAppearance == null)
            {
                return;
            }

            AddMesh(sharpDXSceneBuilder, mesh3D, surfaceAppearance.Color, surfaceAppearance.Opacity);
        }

        private static void AddMesh(SharpDXSceneBuilder sharpDXSceneBuilder, Mesh3D mesh3D, System.Drawing.Color color, double opacity)
        {
            if (mesh3D == null)
            {
                return;
            }

            List<Point3D> point3Ds = mesh3D.GetPoints();
            if (point3Ds == null || point3Ds.Count == 0)
            {
                return;
            }

            // All points are kept (invalid ones collapse to the origin) so the triangle indexes
            // stay aligned with the positions list.
            List<Vector3> positions = point3Ds.ConvertAll(x => x == null || !x.IsValid() ? Vector3.Zero : ToVector3(x));

            int count = mesh3D.TrianglesCount;
            List<int> triangleIndices = new List<int>(count * 3);
            for (int i = 0; i < count; i++)
            {
                System.Tuple<int, int, int> tuple = mesh3D.GetTriangleIndexes(i);
                if (tuple == null)
                {
                    continue;
                }

                triangleIndices.Add(tuple.Item1);
                triangleIndices.Add(tuple.Item2);
                triangleIndices.Add(tuple.Item3);
            }

            sharpDXSceneBuilder.AddMesh(color, opacity, positions, triangleIndices);
        }

        private static void AddSegments(SharpDXSceneBuilder sharpDXSceneBuilder, ISegmentable3D segmentable3D, CurveAppearance curveAppearance)
        {
            List<Segment3D> segment3Ds = segmentable3D?.GetSegments();
            if (segment3Ds == null)
            {
                return;
            }

            foreach (Segment3D segment3D in segment3Ds)
            {
                AddSegment(sharpDXSceneBuilder, segment3D, curveAppearance);
            }
        }

        private static void AddSegment(SharpDXSceneBuilder sharpDXSceneBuilder, Segment3D segment3D, CurveAppearance curveAppearance)
        {
            if (segment3D == null || curveAppearance == null)
            {
                return;
            }

            sharpDXSceneBuilder.AddSegment(curveAppearance.Color, curveAppearance.Opacity, curveAppearance.Thickness, ToVector3(segment3D[0]), ToVector3(segment3D[1]));
        }

        private static Vector3 ToVector3(Point3D point3D)
        {
            return new Vector3((float)point3D.X, (float)point3D.Y, (float)point3D.Z);
        }
    }
}
