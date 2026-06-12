# Floor plan / large model performance — issue drafts

Status: **ideas / inspiration, to be verified** (prototype on a `sow/*` branch before committing to an approach).
Each section below is a ready-to-paste GitHub issue (logged as #11–#16). All code references were verified against **both `master` and `sow/2026-Q2`** (e23112f, June 2026); line numbers below are for `sow/2026-Q2`, the current working branch. Of the cited files, only `UIJSAMObject.cs` and `AnalyticalWindow.xaml.cs` differ between the two branches, and only in unrelated ways (WinForms→WPF dialog migration, Mollier port) — every finding holds on both.

Reported pain points these address:
1. With large models, zooming the floor plan is difficult and spaces disappear.
2. Modifying space data (e.g. InternalCondition) recalculates **all** views and takes a long time.

Architecture context (verified): the floor plan is **not** a 2D canvas — it is the Helix Toolkit
`HelixViewport3D` switched to an orthographic top-down camera
(`ViewportControl.UpdateMode`, `WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs`).
Every view tab holds a `ViewportControl` whose scene is produced by
`AnalyticalModel.ToSAM_GeometryObjectModel(viewSettings)`
(`SAM_UI/SAM.Analytical.UI/Convert/ToSAM/GeometryObjectModel.cs`) and converted to a WPF
`ModelVisual3D` tree by `Convert.ToMedia3D`
(`WPF/SAM.Geometry.UI.WPF/Convert/ToMedia3D/ModelVisual3D.cs`).

---

## Issue 1 — Perf: attribute-only edits (e.g. InternalCondition) trigger full view regeneration; introduce scoped/granular invalidation

**Likely the single highest-impact change for the reported workflow.**

### What the code does today
- `Modify.AssignSpaceInternalCondition(UIAnalyticalModel, Space, InternalCondition)`
  (`WPF/SAM.Analytical.UI.WPF/Modify/AssignSpaceInternalCondition.cs:29`) assigns via the
  `uIAnalyticalModel.JSAMObject` **setter**, which always raises a `FullModification`
  (`SAM_UI/SAM.Core.UI/Classes/UIJSAMObject.cs:62-78`). A `FullModification` forces
  `updateGeometry = true` for **every** open view tab in `AnalyticalWindow.UpdateTabItem`
  (`WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml.cs:3125`).
- The multi-space overload (`AssignSpaceInternalCondition.cs:99`) already passes a GUID-scoped
  `AnalyticalModelModification(sAMObjects)` — but `UpdateTabItem` (lines 3145–3168) still
  regenerates the **entire** `GeometryObjectModel` for any view that merely *contains* one of
  those GUIDs: full `ToSAM_GeometryObjectModel()` (re-sectioning, label solving, legend rebuild)
  plus a full WPF visual-tree rebuild via the `ViewportControl.UIGeometryObjectModel` setter →
  `Load()` → `Convert.ToMedia3D(entire model)`.
- An InternalCondition change only affects the **color, legend entry and label text** of the
  affected spaces — shells, sections and panel curves are unchanged.
- The model/views tree (`AnalyticalModelControl.LoadAnalyticalModel`) also rebuilds **all**
  `TreeViewItem`s from scratch on every `Modified` event, regardless of modification scope.

### Idea
Extend the existing `IModification` infrastructure (already in place and partially used):
1. Add an attribute-level modification type (e.g. `AttributeModification : AnalyticalModelModification`)
   raised by `AssignSpaceInternalCondition`, `EditInternalConditions`, `MapInternalConditions`, etc.
2. Quick win: make the single-space `AssignSpaceInternalCondition` overload use
   `SetJSAMObject(..., new AnalyticalModelModification(...))` instead of the `JSAMObject` setter —
   today a one-space edit invalidates everything.
3. In `UpdateTabItem`, when only attribute modifications are present: update **in place** the
   material/brush of the affected spaces' existing `Visual3D`s (addressable by GUID via
   `ViewportControl.GetVisual3D<T>(guid)`), update label text and refresh the legend —
   no re-sectioning, no scene rebuild.
4. Same scoping for the tree views: update affected `TreeViewItem` headers instead of full rebuild.

### Acceptance criteria
- Assigning/editing an InternalCondition on N spaces in a model with 1000+ spaces and several
  open views updates colors/labels/legend in well under a second, without the
  "View Regeneration" progress window.
- Geometry edits still trigger full (or GUID-scoped) regeneration as today.

---

## Issue 2 — Perf: `UIJSAMObject<T>.JSAMObject` getter deep-clones the whole model on every access

### What the code does today
- `UIJSAMObject<T>.JSAMObject` **getter** returns `Core.Query.Clone(jSAMObject)` — a deep clone
  (serialize/deserialize round trip) of the entire `AnalyticalModel`
  (`SAM_UI/SAM.Core.UI/Classes/UIJSAMObject.cs:62-72`).
- `AnalyticalWindow.xaml.cs` alone reads `uIAnalyticalModel?.JSAMObject` ~48 times; many are
  casual reads inside event handlers, context-menu handlers and `Reload`
  (`AnalyticalWindow.xaml.cs:1693`). For a large model each such read costs a full-model clone
  (CPU + GC pressure), invisibly multiplying every interaction's latency.

### Idea (to verify)
- Provide a non-cloning read path (e.g. `PeekJSAMObject` / `GetJSAMObject(bool clone)`) and use it
  everywhere the object is only read (rendering, tree building, queries). Keep the cloning getter
  for callers that mutate.
- Alternatively: cache the clone and invalidate the cache in `SetJSAMObject`/`OnModified`.
- Measure first: time `Core.Query.Clone` for a representative large model to size the win.

### Acceptance criteria
- Opening context menus, switching tabs and reloading no longer clone the model per access;
  profiling shows clone count per user action drops to 0–1.

---

## Issue 3 — Perf: 2D floor plan generation — cache space shells/sections, move generation off the UI thread, skip label solver when geometry unchanged

### What the code does today
`ToSAM_GeometryObjectModel(AnalyticalModel, TwoDimensionalViewSettings)`
(`SAM_UI/SAM.Analytical.UI/Convert/ToSAM/GeometryObjectModel.cs:482-794`), synchronously on the
UI thread, for every regeneration of every floor plan tab:
- deep-copies the model again (`new AnalyticalModel(analyticalModel)`, line 491);
- sections **every panel** against the plane (`Analytical.Query.SectionDictionary`, line 507);
- for **every space**: builds the `Shell` (`adjacencyCluster.Shell(space)`, line 552 — an
  expensive topological operation), sections it with the plane, converts to `Face2D`, offsets
  edges (`Face2D.Offset`, line 610);
- runs the label-placement solver with `IterationCount = 100` **per space label**
  (`Solver2D`, lines 640–722);
- creates a `Text3DObject` per space (meshed 3D text, expensive to tessellate in Helix).

None of this is cached: an attribute-only change recomputes everything identically.

### Ideas (to verify)
1. **Cache** per (space GUID, plane): shell, section `Face2D`s and offset results; per
   (panel GUID, plane): section segments. Invalidate per-GUID on geometry modifications only.
   This also helps Issue 1's in-place updates.
2. **Background generation**: compute `GeometryObjectModel` in a `Task` with cancellation
   (rapid successive edits cancel stale work), dispatch only the `ToMedia3D`/scene swap to the
   UI thread. The viewport keeps showing the previous frame instead of freezing.
3. **Label solver**: only re-run `Solver2D` when section geometry or label texts changed; cache
   placed label positions per view. Consider capping iterations by available time or solving
   only labels whose space changed.

### Acceptance criteria
- Regenerating a floor plan after an attribute edit does no shell/section computation
  (cache hits) and the UI thread never blocks for more than ~100 ms.

---

## Issue 4 — Perf: viewport scene — incremental updates instead of full rebuild, mesh batching, cheaper hover hit-testing

### What the code does today
- `ViewportControl.UIGeometryObjectModel_Modified` → `Load()` clears and rebuilds the **entire**
  scene (`WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs:707-710, 496-541`);
  `Convert.ToMedia3D` creates one `ModelVisual3D` per geometry object/collection
  (`WPF/SAM.Geometry.UI.WPF/Convert/ToMedia3D/ModelVisual3D.cs`) — thousands of `Visual3D`
  instances for a large model, which WPF 3D handles poorly (per-visual overhead; Helix guidance
  is to consolidate into few `GeometryModel3D`/`Model3DGroup`s and `Freeze()` them).
- `helixViewport3D_PreviewMouseMove` performs a full ray hit-test against the whole scene on
  **every mouse move** (`ViewportControl.xaml.cs:481-494`), plus cancel/apply of a
  `HighlightAction` — on large models this alone makes pan/zoom feel sluggish.
- `ViewportControl.ContainsAny` (used by `UpdateTabItem` to decide regeneration) walks the whole
  visual tree per modification check (`ViewportControl.xaml.cs:126-134`).

### Ideas (to verify)
1. **Incremental scene updates**: keep a `Dictionary<Guid, ModelVisual3D>`; on GUID-scoped
   modifications add/remove/recolor only affected visuals (pairs with Issue 1).
2. **Batching/freezing**: merge static geometry (panel section lines, space fills sharing a
   material) into few frozen `MeshGeometry3D`s; keep per-object visuals only for selectable
   granularity actually needed, or hit-test against a parallel lightweight index instead.
3. **Hover hit-test throttling + spatial index**: throttle `PreviewMouseMove` hit-tests
   (e.g. 30–60 ms) and/or hit-test in 2D mode against a 2D spatial index (quadtree/STRtree over
   space `Face2D`s — geometry already computed during floor plan generation) instead of WPF 3D
   ray casting.
4. Replace tree-walking `ContainsAny` with the GUID dictionary from (1).

### Acceptance criteria
- Pan/zoom at 60 fps with hover highlighting enabled on a model with 1000+ spaces.
- Selecting/highlighting latency below ~50 ms.

---

## Issue 5 — Bug + UX: spaces disappear when zooming the floor plan; zoom does not center on cursor in 2D mode

### Symptoms
On large models, zooming into a floor plan is difficult and spaces disappear at certain zoom
levels.

### Verified starting points (hypotheses to confirm)
- In 2D mode `UpdateMode` sets `helixViewport3D.Orthographic = true` and fixed clip planes
  `NearPlaneDistance = -1000`, `FarPlaneDistance = 1000`
  (`WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs:734-747`). These are set **once**;
  Helix camera interactions (wheel zoom moves the camera position along the look direction even
  for orthographic cameras) can move geometry outside the fixed near/far range, or a camera
  swap (e.g. toggling `Orthographic`, `SetCamera`, `ZoomExtents`) can reset planes to defaults —
  either would clip ("disappear") the section geometry. Reproduce, then either keep clip planes
  tracking the camera position or zoom by changing `OrthographicCamera.Width` only.
- Floor-plan labels are placed 0.1 above the section plane and space fills on the plane
  (`GeometryObjectModel.cs:757`); verify z-fighting/clipping interaction at high zoom.
- **UX**: 2D mode sets `ZoomAroundMouseDownPoint = false` (`ViewportControl.xaml.cs:741`) while
  3D mode sets it to `true` — so floor plan zoom does not zoom toward the cursor, which is the
  main reason zooming to a specific space feels difficult. Likely a one-line improvement
  (verify why it was disabled for 2D).
- There is no zoom-window / zoom-to-selection on the floor plan other than the context-menu
  "Zoom Extents" (`ViewportControl.xaml.cs:374-380`); consider adding "Zoom Selected"
  (the `Zoom(IEnumerable<SAMObject>)` API already exists, `ViewportControl.xaml.cs:305`).

### Acceptance criteria
- No geometry disappears across the full usable zoom range on a large model.
- Mouse-wheel zoom centers on the cursor in 2D views; "Zoom Selected" available.

---

## Issue 6 — Perf: regenerate only the active view tab; refresh background tabs lazily on activation

### What the code does today
`AnalyticalWindow.Reload` → `UpdateTabItems` iterates **all enabled** view settings and calls
`UpdateTabItem` for each (`WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml.cs:3201-3235`),
so one edit pays the regeneration cost (Issue 3) multiplied by the number of open tabs —
synchronously, behind a modal "Reloading" progress window (`AnalyticalWindow.xaml.cs:1683-1705`).

### Idea (to verify)
- On `Modified`: regenerate only the **active** tab; mark other affected tabs dirty.
- Regenerate a dirty tab when it becomes selected (`TabControl_SelectionChanged` already exists),
  optionally pre-warming in the background (pairs with Issue 3's background generation).

### Acceptance criteria
- Edit latency is independent of the number of open view tabs.
- Switching to a dirty tab regenerates only that tab (with progress indication if needed).

---

## Suggested order of attack

| Order | Issue | Effort | Impact on reported pain |
| --- | --- | --- | --- |
| 1 | Issue 1 (scoped invalidation) | M | Eliminates "edit IC → all views recalc" |
| 2 | Issue 6 (active tab only) | S | Multiplies Issue 1/3 wins by tab count |
| 3 | Issue 5 (zoom bug + cursor zoom) | S–M | Directly fixes zoom/disappearing UX |
| 4 | Issue 2 (clone-on-get) | S–M | Cross-cutting latency reduction |
| 5 | Issue 3 (caching + background gen) | L | Makes unavoidable regenerations fast |
| 6 | Issue 4 (incremental scene/batching) | L | Smooth pan/zoom/hover at scale |

---

## Phase 0 instrumentation: how to capture timings

A lightweight, opt-in timing log (`SAM.Core.UI.PerformanceLog`) instruments the operations above.
**Off by default** — zero behavior change unless the `SAM_UI_PERFORMANCE_LOG` environment variable is set:

```
SAM_UI_PERFORMANCE_LOG=1              -> log to %TEMP%\SAM_UI_Performance.log
SAM_UI_PERFORMANCE_LOG=C:\dir\my.log  -> log to a specific file
```

Lines are tab-separated (`timestamp`, `elapsed ms`, `operation`, `detail`) and also written to the
debugger output window. Measured operations:

| Operation | What it measures | Issue |
| --- | --- | --- |
| `AnalyticalWindow.Reload` | Whole reload on any model modification (detail: modification types) | #11, #12 |
| `AnalyticalWindow.ViewRegeneration.GeometryObjectModel` | `ToSAM_GeometryObjectModel` per view (detail: view name + settings type) | #11, #15 |
| `AnalyticalWindow.ViewRegeneration.Viewport` | Scene swap into `ViewportControl` per view | #16 |
| `ViewportControl.ToMedia3D` | `GeometryObjectModel` → WPF `ModelVisual3D` tree conversion | #16 |
| `ViewportControl.HoverHitTest` | Ray hit-test on mouse move — only logged when ≥ 25 ms | #16 |
| `FloorPlan.SectionPanels` | Sectioning all panels against the floor plan plane | #15 |
| `FloorPlan.SectionSpaces` | Shell build + plane section for all spaces (detail: space count) | #15 |
| `FloorPlan.LabelSolver` | `Solver2D` label placement (detail: label count) | #15 |
| `View3D.SpaceShells` | Shell build/cut loop in 3D views (detail: space count) | #15 |
| `AnalyticalModelControl.LoadAnalyticalModel` | Full model/views tree rebuild | #11 |
| `UIJSAMObject.Clone` | Deep clone in the `JSAMObject` getter — one line **per access** | #14 |

Suggested baseline session on a large model: open model, open 1/3/5 view tabs, assign an
InternalCondition to one space and to 50 spaces, pan/zoom the floor plan for ~30 s — then keep the
log file next to the timings table in this document.

---

## Experimental: 2D floor plan renderer (flag-gated)

Implements the "2D canvas for floor plans" direction from issue #18 (also addresses #13 and the
floor-plan part of #16). **Off by default** — enable with the `SAM_UI_FLOORPLAN_2D` environment
variable (any value other than empty/`0`). With the flag off, behavior is byte-for-byte identical
to before.

- `WPF/SAM.Geometry.UI.WPF/Controls/FloorPlan2DControl.cs` — renders the same
  `GeometryObjectModel` as flat WPF `DrawingVisual`s: spaces as filled `StreamGeometry`
  (even-odd fill handles holes), panels as lines, labels as `FormattedText`. Pan/zoom are a
  matrix transform — cursor-centered wheel zoom, Shift+Left or Middle drag to pan, no clip
  planes (fixes the disappearing-spaces failure mode by construction). Hover/selection redraw
  only the affected visual; selection colors match the Helix path (fill 125,125,255 / edge blue).
- `ViewportControl` hosts it: when the flag is set and the view is `TwoDimensional`, the Helix
  viewport is collapsed and left empty, and all public APIs (`Select`, `Zoom`, `ContainsAny`,
  `SAMObjects`, `SelectedSAMObjects`, rectangle select, Zoom Extents menu) route to the 2D
  control. 3D views are untouched.
- Interop: each top-level geometry collection gets a detached stub `ModelVisual3D` carrying the
  same `IJSAMObject` attached property as Helix visuals, so `ObjectHoovered` /
  `ObjectDoubleClicked` / `ObjectContextMenuOpening` consumers in `AnalyticalWindow` work
  unchanged.
- Instrumented: `FloorPlan2DControl.Load` and `FloorPlan2DControl.HoverHitTest` appear in the
  performance log for direct comparison against `ViewportControl.ToMedia3D` /
  `ViewportControl.HoverHitTest` on the same model.

Known v1 limitations (fine for an experiment behind a flag): camera save/restore between
sessions is not wired for the 2D path (opens at zoom-extents), and `UpdateCamera` view-settings
modifications are a visual no-op in 2D.
