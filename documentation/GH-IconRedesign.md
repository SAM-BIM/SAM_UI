# SAM Grasshopper icon redesign — SAM_UI PR record

Branch `feature/sam-gh-icon-redesign`, based on `sow/2026-Q3` @ `05800789`. PR: (to be opened).
Propagates the SAM icon design system from SAM-BIM/SAM#166 (head `cf4d924a`, open, not merged) to this repository.

## Current status
All **32** Grasshopper objects in this repo (31 components + 1 params) use redesigned icons: **32 / 32**.
Built and validated; ready for review. **Not merged.**

## Work completed
- `design/grasshopper-icons/`: the shared SAM-BIM icon kit. `icons.py`, `render.py`, `sam_classify.py` and `ICON_DESIGN_SYSTEM.md` are vendored **verbatim** from SAM#166 (hash-checked). `icons_ext.py` and `ICON_DESIGN_SYSTEM_EXT.md` are the frozen SAM-BIM extension v1 (identical in every SAM-BIM repo). `tools/repo_rules.py` holds this repo's explicit decisions.
- **Inventory**: `tools/inventory.py` parses C# source (every non-abstract class declaring `ComponentGuid`).
- **Manifest** (source of truth): `manifest.json` / `manifest.csv` — per object: GUID, class, source, project, object glyph, operation, modifiers, icon id, resource, glyph/badge origin.
- **Generation**: 19 canonical SVGs → 24×24 PNGs; review sheet `review/contact_sheet.png` (native 24 px on GH normal / orange-warning / dark bodies + 3×) and `review/REVIEW.md`.
- **Integration**: each project's existing mechanism; only the icon token inside each `Icon` getter changes.

| Project | Objects | Icon resources | Mechanism |
|---|---|---|---|
| `SAM.Analytical.UI.Grasshopper` | 5 | 5 | resx / Bitmap |
| `SAM.Analytical.UI.WPF.Grasshopper` | 1 | 1 | resx / Bitmap |
| `SAM.Core.Mollier.UI.Grasshopper` | 26 | 13 | resx / Bitmap |

## Design reuse
- **Reused SAM object families (3)**: `ahu`, `geometry`, `space`
- **New SAM-BIM ext v1 families used (12)**: `mollierChart`, `mollierPoint`, `mollierProcess`, `processAdiabatic`, `processCooling`, `processFan`, `processHeatRecovery`, `processHeating`, `processHumidification`, `processMixing`, `processRoom`, `tasks`
- **Verbs**: `convert`, `create`, `display`, `export`, `run`; new ext verb: `run`
- Distinct icons: **19** (5 on SAM glyphs, 14 on ext glyphs). Icon ids shared with SAM render pixel-identically to SAM's.

## Decisions and assumptions
- Grammar, palette, badge families and construction rules are unchanged (SAM#166). No text, no new colours.
- Qualifier variants (`…By<X>`) share an icon intentionally (see `review/REVIEW.md`).
- Interop direction: external → SAM = import ↓, SAM → external = export ↑.
- Mollier process-creation components use the ext process-direction glyphs (heating →, cooling ←, humidification ↑, adiabatic ↖, room ↗, mixing, heat recovery, fan); the `…By<X>` variants of one process type share its icon.
- `ShowDiagramAHU` / `ShowDiagramSpace` draw what they display the diagram *of* (SAM `ahu` / `space` + display); `ShowDiagram` draws the chart.
- `PrintAHU` / `PrintRDS` are exports (AHU schedule / room data sheets); `MultitaskerWorkflow` = ext `tasks` + ext verb `run`.
- `SAM.Analytical.UI.WPF.Grasshopper` uses a public-generated `Resources.Designer.cs`; the generated properties match that visibility.
- Legacy icon resources are kept (still referenced by context menus / AssemblyInfo); no GUID, name, nickname, category, subcategory, parameter or behaviour change.

## Files changed
- New: `design/grasshopper-icons/**`, `<project>/Resources/Icons/SAM_GH_*.png`, `documentation/GH-IconRedesign.md`.
- Modified: 32 component/param `.cs` files (one icon token each), 3× `Resources.resx`, 3× `Resources.Designer.cs`. No csproj change.

## Validation
| Check | Result |
|---|---|
| `tools/classify.py` | 32 classified, 0 unclassified |
| `tools/build.py` identical-pixel collision check | 0 groups (19 distinct icons; 6 intentionally shared icon(s) for qualifier variants, listed in `review/REVIEW.md`) |
| Icon ids shared with SAM#166 vs SAM's `png/24` | 2 shared, 2 byte-identical |
| `tools/integrate.py` re-parse | 32/32 objects reference their `SAM_GH_*` resource; every PNG exists |
| `tools/check_source.py` vs `origin/sow/2026-Q3` | vendored files OK; icon-token swaps: 32, non-icon changes: 0; base 33, now 33 -> UNCHANGED |
| `dotnet build SAM_UI.sln -c Debug` | Build succeeded, 0 errors |
| `tools/check_assemblies.py` | every assembly embeds every required 24×24 icon → OK |
| `tests/GhIconTest` (real Rhino 8 / Grasshopper, Rhino.Testing) | 32/32 objects load by GUID, name/category match, icon = manifest PNG (max diff 1 level, premultiplied-alpha rounding) |
| `SAM.Analytical.UI.WPF.Tests` | 1397/1397 passed |
| Visual review (`review/contact_sheet.png`, 24 px on normal / warning / dark bodies) | all icons legible; no collisions |

## Unresolved issues / risks
- Built against sibling repos as checked out locally (SAM on `feature/sam-gh-icon-redesign` = SAM#166); icon changes are API-neutral.

## Recommended next step
Review this PR (compare `review/contact_sheet.png`), then merge by the maintainer. After merge, add the `PROJECT_PROGRESS.md` closeout entry on `sow/2026-Q3` with the merge SHA. SAM#166 (the reference design system) remains open.
