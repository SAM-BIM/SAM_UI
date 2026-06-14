# 3D viewport mesh-batching — design & incremental plan (SAM-BIM/SAM#16, #32)

Status: **design / in progress** on branch `perf/viewport-mesh-batching`. Behind an env flag
(`SAM_UI_VIEWPORT_BATCH`), default off, until each increment is verified in VS on the 10k-space model.

## Goal

The dominant warm-regen cost is `ViewportControl.ToElement3D.Attach` (~3.5 s on the 33,635-object
model). It scales with the **number of individually-attached scene models**: today
`Convert.ToElement3Ds(GeometryObjectModel)` emits **one `GroupModel3D` per object** (~33k groups, each
with 1–3 `MeshGeometryModel3D` / `LineGeometryModel3D` children), and `SharpDXViewportControl.Load` adds
every one to `viewport3DX.Items`. Batch-attach was already tried and gave nothing (PR #36) — the cost is
inherent per-model GPU attach, so the only lever is **reducing the model count**.

Plan: merge all objects' geometry into a **few** `MeshGeometryModel3D`/`LineGeometryModel3D` grouped by
material (colour+opacity / colour+thickness), and keep object identity for picking/selection via a
**CPU-side index** instead of one scene model per object.

Today's per-object structure also powers picking, hover, selection, bounds and `RefreshAppearance`
(all keyed on one `Element3D` per guid), so those must be re-implemented on the batched representation.

## Architecture (current, verified)

- `Convert.ToElement3Ds(GeometryObjectModel)` → per object: `SharpDXSceneBuilder` already merges that
  object's primitives by material into a `GroupModel3D` tagged with the object via the attached
  `IJSAMObject` property. (`WPF/SAM.Geometry.UI.WPF/Convert/ToElement3D/Element3Ds.cs`,
  `Classes/SharpDXSceneBuilder.cs`.)
- `SharpDXViewportControl` indices (all keyed per object): `dictionary_Element3D` (guid→group),
  `dictionary_Guid` (element→guid, incl. children), `dictionary_Stub` (guid→event-payload stub),
  `dictionary_Base*` (per-child base material/colour/thickness for hover/selection restore).
- Picking: `viewport3DX.FindHits(point)` → hit `Element3D` → `dictionary_Guid` → guid. Each child mesh
  has a deferred per-geometry octree (`UpdateOctree`).
- Hover/selection: `ApplyAppearance(guid)` swaps the **whole child model's** material/colour.
- `IBoundable3D.GetBoundingBox()` is available on all SAM geometry → cheap per-object bounds.

## The three hard parts

1. **Build**: merge across *all* objects into global material buckets → few models. (Straightforward —
   `SharpDXSceneBuilder` already buckets by material per object; lift it to scene scope.)
2. **Pick → guid** without per-object models: when building each merged mesh, record a sorted
   **triangle-range → guid** map (and segment-range → guid for lines). `FindHits` returns the hit
   triangle index on the merged geometry; binary-search the ranges to resolve the guid. (No separate
   spatial index needed — `FindHits` already does ray/triangle work via the merged mesh's octree.)
   Screen-rect select and `TryGetBounds`/`Zoom` use a parallel **guid → bounds + ranges** index.
3. **Recolour one object** (hover/selection) inside a shared mesh — the open decision below.

## Decision needed: per-object recolour mechanism

| Option | How | Pros | Cons / risk |
| --- | --- | --- | --- |
| **A. Per-vertex colour** | Add a `Color4Collection` to the merged `MeshGeometry3D`; on select/hover rewrite the colour entries for that object's vertex range; use a vertex-colour-aware material | GPU-native, no extra draw calls, scales | HelixToolkit support for live vertex-colour patching is **unverified**; needs writable buffers + a material that blends vertex colour; prototype required |
| **B. Overlay mesh** | Keep base batches static; on selection build a small **separate** mesh of just the selected objects' triangles in the selection colour, drawn on top; hover = overlay or edge-thicken | No change to base buffers; clean separation; only touches selected objects | Extra mesh rebuilt on each selection change (cheap — selection is small); slight overdraw; hover highlight needs its own overlay or is dropped |
| **C. Hybrid** | Batch the bulk; keep the *currently selected* objects as their own per-object models (small N) | Reuses today's per-object appearance for the few selected | More moving parts; objects move between batched/unbatched sets on selection |

Recommendation: **B (overlay mesh)** for the first shippable version — it avoids the HelixToolkit
per-vertex unknown, keeps the base batches immutable (so the Attach win is clean), and selection sets
are small so rebuilding an overlay is cheap. Revisit A later if hover needs per-triangle highlighting at
scale.

## Incremental plan (each behind the flag, each VS-tested before the next)

1. **Batched build + attach (render-only), measure the win.** New `SAM_UI_VIEWPORT_BATCH=1` path:
   `Convert.ToElement3Ds` (or a new scene-level builder) merges all objects into global material
   batches; `Load` attaches the few models. Build the triangle-range→guid map and guid→bounds index.
   Picking/hover/selection temporarily disabled in this mode. **Goal: confirm `Attach` drops from
   ~3.5 s to ~0 and regen wall-clock falls.**
2. **Picking.** `HitTestGuid` resolves the merged-mesh hit triangle index → guid via the range map;
   wire hover payload + single/double click selection.
3. **Selection/hover appearance** via overlay mesh (Option B): build/drop the selection overlay on
   selection change; hover via overlay or edge thickening.
4. **Bounds-dependent features** on the guid→bounds index: `Zoom`/Zoom-Selected, `TryGetBounds`,
   `UpdateRotationPivot`, `SelectByScreenRect`.
5. **`RefreshAppearance` (attribute edits)** in batched mode: recolour an object's triangle range in the
   affected batch (or rebuild the affected batch), without a full regen.
6. **Flip default / remove the per-object path** once parity is confirmed (gate sign-off, like the
   SharpDX port phases).

## Risks / open questions

- HelixToolkit `FindHits` returning a usable triangle index on a merged `MeshGeometryModel3D` (needed
  for step 2) — verify early.
- One giant merged octree vs many small ones: build cost and pick latency — measure in step 1/2.
- Transparent batches (spaces) ordering vs opaque — current per-object `IsTransparent` becomes
  per-batch; confirm blending still looks right.
- Line thickness is bucketed in screen pixels already (`SharpDXSceneBuilder`), so line batching is
  uniform — OK.

See [[floor-plan-large-model-performance-issues]] (Issue 4 / "Proposed next" item 1) for context.
