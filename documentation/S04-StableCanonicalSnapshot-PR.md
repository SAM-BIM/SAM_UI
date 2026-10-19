# S04 - Stable canonical simulation snapshot - PR record

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S04 (Phase A), finding F7 of SAM_UI#239. SAM_UI only;
SAM_Validation's canonical JSON v1 and `canonicalizationVersion` are unchanged.

Base: `sow/2026-Q4` `4d7f5bd8`. **Independent of S03** (SAM_UI#242): neither needs the other. Both PRs add the same
`using System.Text.Json.Nodes;` line to `Modify/RunOpenStudioSimulation.cs` and otherwise touch different hunks; the
combination was merged and tested locally (see the S03/S04 integration note in the PR description).
Branch: `feature/s04-stable-canonical-snapshot`. Draft PR, not merged. **Validated content SHA: `0594e708`** (code and tests).
Record commits follow it. The final PR head and hosted checks are in the PR description. `PROJECT_PROGRESS.md` is not touched.

## Status
Implementation and local tests complete on 2026-10-19 (overnight owner-authorised S03+S04 session). Awaiting hosted CI
and owner review. Not merged.

## Problem (baseline `4d7f5bd8`), established from real evidence
- **Two sessions, one model.** S01b runs B2 and B3 (machine-local in `C:\TasOut\s01b\runs`) each froze the same
  `box-aperture.json`. The two snapshots, 3,442,974 bytes each, differ in exactly two values: the `Guid` and `ActiveGuid` of
  the "3D View" inside `ParameterSets[Name="SAM.Analytical.UI"].Parameters[Name="UI Geometry Settings"]`.
- **Four runs, four hashes.** The committed S01b B1a documents for four real Both runs of that one model carry four
  different `canonicalModelHash` values: `6d455480…`, `fcb0cd9b…`, `a3df983c…` and `5e56751e…`.
- **Cause.** `AnalyticalWindow.SetDefaultViewSettings` runs on `Opened`. A model with no views gets
  `UI.Query.DefaultViewSettings`, whose GUID is `Guid.NewGuid()`.
- **What the parameter is.** SAM_UI's view state: views, visibility, cameras and legends. It never reaches a simulation.
  SAM already excludes it from `SimulationResultProvenance.Fingerprint` for the same reason.
- **Found while fixing it, with the real producers.** Once that parameter is removed, its set is left empty, and the
  empty set still carries a GUID: `Core.Query.Guid(assembly)`, the SAM.Analytical.UI module version id. That id changes with
  every SAM_UI build. The test build had `afeedf2b…` and the S01b app build `29968788…`, so two builds still disagreed.

## Change
- **`Query/SimulationSnapshot.cs` (new):** `Query.SimulationSnapshotJsonObject(model)` takes the model's own freshly built
  JSON tree and changes only that tree, never the model:
  - it removes the `UI Geometry Settings` parameter;
  - it removes the SAM.Analytical.UI parameter set only if that leaves it empty. Anything else in that set stays, as does
    every other parameter set, GUID and piece of metadata.
- **`Modify.FreezeAnalyticalModel`:** writes that JSON for the one snapshot both routes use:
  - Run Simulation > Both: the file both producers hash (`sourceFileHash`, `canonicalModelHash`) and simulate;
  - OpenStudio Simulation: the frozen copy is read back from it and converted.
  
  A `.json` snapshot is written exactly as `Core.Convert.ToFile` writes JSON: a one-element array, with
  `Core.Convert.SerializerOptions(Indented)`. A model with no view state is therefore frozen byte for byte as before. Other
  file types go through `Core.Convert.ToFile`, as before.
- **`Modify.WithViewState`:** the OpenStudio result model is built from the frozen copy. Before it is saved, it gets a copy
  of the open model's view state back, so it opens with the views it had before S04.
- **Unchanged:**
  - geometry, constructions, schedules, weather, design days, simulation options and native-model conversion;
  - the producers, SAM_Validation and its `canonicalizationVersion`;
  - the manifests and their schemas;
  - the Tas-only route, which writes no snapshot.

## Decisions and assumptions
- **Fix the input, not the hash rule.** The canonical hash is computed by the producers (SAM_Tas, SAM_OpenStudio) from the
  snapshot SAM_UI writes, using SAM_Validation's generic canonical JSON v1. Excluding a SAM_UI parameter inside that generic
  rule would be a canonicalization change across three more repositories. Keeping UI-only state out of the engine input is
  the smallest correction, and it also stabilises `sourceFileHash` and the snapshot SHA-256.
- **Deny one named parameter, not an allow list.** This is the same name and the same reasoning as SAM's Fingerprint
  exclusion. A parameter added later that bears on a simulation is kept automatically.
- **The emptied set is dropped, a non-empty one is kept.** An empty set carries only its per-build GUID. A set holding
  anything else is left alone: removing it would be indiscriminate.
- **Historical documents keep their hashes.** Earlier B1a documents and manifests keep their session-dependent hashes. A new
  snapshot of the same model has a canonical hash that differs from every pre-S04 one, just as pre-S04 hashes already
  differed from session to session. `canonicalizationVersion` is unchanged because the algorithm is unchanged; only the
  input lost its UI-only state.

## Tests
New `CanonicalSnapshotTests.cs`: 13 tests in the WPF collection, on the Part O multizone fixture (2 flats plus a corridor,
with spaces, panels, apertures, constructions, internal conditions and weather).
- **Method:** models are saved, then reopened as the window opens them: `UIAnalyticalModel.Open`, then the window's
  default-view step using the production `UI.Query.DefaultViewSettings`. The canonical hash is computed as the producers
  compute it: the snapshot is loaded into an AnalyticalModel, then canonical JSON v1 is taken of its JSON.
- **A:** the same saved model opened twice. The opened models' view GUIDs differ (the F7 precondition). The snapshot
  SHA-256 and the canonical hash are equal.
- **B:** different UI-only view state (another view, new GUIDs) gives the same canonical hash. The view state's set with
  another build's GUID also gives the same snapshot and hash.
- **C (theory):** each of these changes the hash:
  - a construction;
  - a space parameter;
  - the weather;
  - a model parameter;
  - another parameter in the SAM.Analytical.UI set (only the named view state is dropped).
- **Snapshot is the source less exactly the view state:** its JSON equals the source's JSON with only the parameter and its
  emptied set removed. A model with no view state is frozen byte for byte as `Core.Convert.ToFile` freezes it.
- **D/E:** two Run Simulation > Both runs of the model, opened in two sessions, feed their producers byte-identical
  snapshots in different run folders. Each manifest's `snapshot.sha256` is its own file's hash, and both producers of a run
  read that one file.
- **F:** `WithViewState` gives the frozen result model a copy (not the instance) of the source's view state.
  - The existing `OpenStudioSimulationTests.RealRun_…` now gives the model a view state. It checks that the real snapshot has
    none and the real result model has it, compared after the JSON round trip the saved file makes.
  - That test runs only where OpenStudio is installed.
- **G:** freezing leaves the source model byte-identical, and the source keeps its view state.
- Existing snapshot consumers pass unchanged: `EngineComparisonTests`, `OpenStudioSimulationTests`,
  `TasFailureReportingTests`, `SimulationConcurrencyGuardTests`.

## Validation
Local results on this laptop. Dependencies: SAM `62b54156`, SAM_Tas `e84aeff6`, SAM_OpenStudio `d4216f3` and
SAM_Validation `68eded0`, all on `sow/2026-Q4` and clean.
- `dotnet build WPF/SAM.Analytical.UI.WPF.Tests -c Release`: 0 errors.
- Focused (`CanonicalSnapshotTests|OpenStudioSimulationTests|EngineComparisonTests|WpfCollectionTests`):
  **44/44 PASS**, including the real OpenStudio run.
- **Mutation checks on `0594e708`** (each reverted), all caught:

  | Mutation | Tests failed |
  |---|---|
  | Snapshot written as before S04 | 7 |
  | Every SAM.Analytical.UI set dropped (over-removal) | 1 |
  | Emptied set kept | 2 |
  | Result model not given its view state back | 1 (the real OpenStudio run) |

  An earlier variant was also caught: removing the weather too failed 2 tests, on `1f24f8af`.
- Full `SAM.Analytical.UI.WPF.Tests` (real `APPDATA`) on `0594e708`: **2873/2873 PASS** (2860 before plus 13 new).

## Evidence (real producers, licensed laptop)
Evidence is in `documentation/evidence/s04-20261019/`: `real-producers.txt`, plus a manifest and both B1a documents for each
session. The harness is `tools/S04EvidenceHarness.cs.txt`; it is not compiled.

**E1: two sessions, real producers.** The model is opened afresh as the window opens it, and run through real Both,
benchmark-tas and benchmark-openstudio.

| | Session 1 | Session 2 |
|---|---|---|
| View GUID | `cc6f50a9…` | `d72e130c…` |
| Snapshot | `0c30db14…` | `0c30db14…` |
| `sourceFileHash` | `0c30db14…` | `0c30db14…` |
| `canonicalModelHash`, both producers | `05c1e334…` | `05c1e334…` |
| Status | Compared | Compared |

**E2: the four historical S01b snapshots, refrozen under S04.** These were made by two different app builds, and before
S04 they had four canonical hashes (`6d455480…`, `fcb0cd9b…`, `a3df983c…`, `5e56751e…`). All four now give snapshot
`0c30db14…` and canonical hash `05c1e334…`, the same values as E1, which came from the test build.

## Unresolved, risks
- `UIGeometrySettings` does not round-trip its default view's appearance dictionary through JSON, because its keys are
  Guids. This is pre-existing, behaves the same before and after S04, and was found while writing the result-model test.
- No licensed real-UI run has been done; that is roadmap S05.
- The Both runs in the evidence needed `SAM_COMMIT` for the developer-built producers. See S03 (SAM_UI#242).

## Next step
Owner review of the draft PR. After merge, add the post-merge `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
