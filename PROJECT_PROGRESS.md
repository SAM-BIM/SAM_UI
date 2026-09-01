# Project Progress

## Branch
`feature/parto-iteration2-ui-orchestration`, branched from `sow/2026-Q3` at **`43564e6`**
(the merge of PR #75). Pushed; **PR #76** open against `sow/2026-Q3`, not merged. First push `1437d88`
passed both checks (build 8m13s, spdx 9s); two P1 review findings have since been fixed on the same PR -
see "Review findings addressed" below.

## Last updated
2026-09-01

## Current status
Approved Document O Iteration 1a / 1b / 2 orchestration implemented in SAM_UI, plus the fix to the
pre-existing zone-identity corruption in `Modify.Simulate`. Builds clean; all **227** tests in
`WPF/SAM.Analytical.UI.WPF.Tests` pass (187 baseline + 40 new). Final review done, and the
zone-identity justification corrected (see BLOCKER 1) - the exporter does not refuse an unstamped
space, it silently exports the wrong identity, and the two new tests pin that. Both P1 review findings
on PR #76 are fixed (see "Review findings addressed"). Awaiting review; not merged.

## Integration state
All sibling repos are on `sow/2026-Q3` at the heads this work was written against, and **none of them
was changed**:

- `SAM` @ `46e450f7` - Iterations 1a/1b/2 licensed acceptance (PR #85).
- `SAM_Systems` @ `fea5055` - Nuaire `MRXBOXAB-ECO5-AECV + MR-ECO-COOL-V`, 150/150 l/s maximum (PR #18).
- `SAM_Tas_Grasshopper` @ `01508b8`.
- `SAM_Tas` @ `78f7afb`.

## Completed

### 1. BLOCKER 1 - the `Modify.Simulate` zone-identity corruption (fixed)

The post-workflow TBD block re-read the `.tbd` and copied `SpaceParameter.ZoneGuid` back onto the model
by matching space **name**. Two independent defects, both silent:

1. **Duplicate room names collapsed identities.** `spaces_TBD.Find(x => x?.Name == space?.Name)` returns
   the *first* match, and the write was unconditional, so three flats each containing "Bedroom 2" all
   received one flat's zone guid - overwriting the strong identity `WorkflowCalculator.Calculate` had
   just written via `SAM.Analytical.Tas.Modify.UpdateIds`.
2. **The value was re-spelled even when the match was right.** The read was
   `TryGetValue(ZoneGuid, out Guid)` against a parameter declared `ParameterType.String`
   (`SAM.Analytical.Tas.SpaceParameter`), so the raw TAS string was parsed to a `Guid` and converted back
   by `ParameterValue.TryConvert` on the way in - losing braces and case.
   `Tas.Query.SimulationSpaceKey` compares the stored strings ordinally, so a re-spelt stamp stops
   matching the TSD side.

**Concrete dependency found - removal was NOT safe. Correction 2026-09-01: the earlier justification for
this was wrong on the mechanism, and only the DomOv export is a dependent.**

The claim recorded here previously was that `Tas.TM59.Convert.ToXml` *refuses* a space whose `ZoneGuid` is
absent or empty. **It does not.**
`Tas.TM59.Convert.ToTM59(Space, TM59Manager, SystemType)`
(`SAM.Analytical.Tas.TM59/Convert/ToTM59/Zone.cs:39-43`) reads the stamp and, when it is absent or empty,
**falls back to `space.Guid`** and exports the zone anyway. No refusal, no note, no failed export.

What makes the seam necessary is that the fallback value is the wrong identity:

- With **Simulate unticked** and "Domestic Overheating" ticked, the `.tbd` is written by
  `Tas.Convert.ToTBD(analyticalModel, path_TBD, null, null, null, true)`. That last argument *is*
  `updateGuids: true`, but `AnalyticalModel.AdjacencyCluster`
  (`SAM/SAM.Analytical/Classes/AnalyticalModel.cs:179-186`) returns `new AdjacencyCluster(...)` - a copy -
  so the zone guids `Tas.Modify.Update` stamps land on a throwaway cluster and never reach the model
  `Modify.Simulate` holds. The model reaches the export **unstamped**.
- A SAM `space.Guid` is not a TAS zone guid. A TBD zone's guid is minted by `building.AddZone()`
  (`SAM.Analytical.Tas/Modify/Update.cs:577`) and bears no relation to the space it was written from, so
  the two values differ by construction.
- Therefore, without the fill, the DomOv XML is written **successfully** with `DomOverheatZoneItem/GUID`
  set to the SAM space guid - naming zones the external TAS TM59 tool cannot find, in a document that
  reports success. That is worse than a refusal, not better.

Pinned by `SimulationZoneIdentityTests.AnUnstampedSpace_ExportsTheSAMSpaceGuid_UntilTheFillGivesItTheTasIdentity`
and `...TheDomOvXmlNamesTheTasZone_OnlyAfterTheFill`, which assert the exported identity on both sides of
the fill rather than the refusal that does not happen.

**SAP and Part L do not depend on this seam** (previously claimed; not supported by their call paths):

- `createSAP` calls `Tas.SAP.Convert.ToFile(analyticalModel_TBD, ...)` - the model read back **out of the
  `.tbd`**, whose spaces `Tas.Convert.ToSAM` stamps itself (`Convert/ToSAM/Space.cs:98`). It never sees the
  restamped design model. (`ToSAP` has the same `space.Guid` fallback, but is never reached with an
  unstamped space on this path.)
- `createPartL` calls `Tas.Create.TBD_ByPartL(analyticalModel, ...)`, whose three writers
  (`UpdateInternalConditionByPartL`, `UpdateZoneGroupsByPartL`, `UpdateZoneGroups`) read no `ZoneGuid` at
  all.

Per the brief, the *removal* was stopped and the (now correctly stated) dependency recorded here.

**What was done instead** - new internal seam `Modify.RestampSimulationZoneIdentity`, and no new matching
algorithm:

- a space that already carries a `ZoneGuid` is **left untouched** (the workflow's stamp is authoritative);
- the name fallback applies only to **unstamped** spaces, i.e. exactly the non-workflow path that depends
  on it;
- an ambiguous name **refuses with a reason** instead of taking the first hit. Two simulated spaces
  stating the *same* guid are one answer, not a conflict (the rule `VentilationStrategyMap` already
  applies to a repeated claim);
- the value is copied **as a string, verbatim** - no `Guid` round trip;
- spaces left unstamped are reported in the completion dialog (capped at 5 with the remainder counted).

The precedence rule mirrors `SAM.Analytical.Tas.Query.ResolvedZone` ("guid first, exact name only as the
compatibility fallback, no match is a refusal, never a guess") rather than inventing one.

### 2. BLOCKER 2 - stateful, stale-safe Part O run context

`PartORun` (in `SAM.Analytical.UI`) with an explicit lifecycle `None -> Prepared -> WorkflowCompleted`:

- **Prepared** owns the prepared model and its `OverheatingScenario` set.
- **WorkflowCompleted** additionally owns the model the TAS workflow *returned* and the corresponding
  `Path_TSD`.
- `AnalyticalModel_Assessment` is non-null **only** in `WorkflowCompleted`. There is no code path on
  which it can be the preparation output, the loaded model, or a later run's model.
- **`WorkflowCompleted` means "this prepared run produced the full-year results being assessed"** - not
  "a TSD exists". `Complete` is legal **only from `Prepared`** and requires, in addition to a non-null
  workflow model and a TSD that exists:
  - **the simulation to have been the full annual run** (days 1-365), which
    `Query.IsPartOFullYearSimulation` decides from the `WorkflowSettings` actually handed to
    `WorkflowCalculator` - not from the "Full Year Simulation" tick box, whose day range still comes from
    the two text boxes beside it and which `shadingUpdated` can turn into a one-day run;
  - **the results file to have been created or rewritten by this workflow**, measured against a
    fingerprint (`exists` + length + write time) captured by `PartORun.ExpectResults` **before** the
    workflow ran.

  Both are enforced through the same arming: `Modify.Simulate` calls `ExpectResults` only where the
  settings describe a full-year run, and `Complete` refuses anything unarmed or announced for a different
  path. So a partial, one-day or sizing-only workflow cannot complete a run even if `Complete` is reached,
  and an earlier session's `<project>.tsd` left in the output directory cannot be adopted as this run's.
- `IsAssessable` re-checks the file's existence and write time **after** completion, so results rewritten
  by another session later are refused. That is a different problem from the one above and both checks are
  kept.
- **Staleness is rejected, not detected.** `ExpectModification()` / `NotifyModified()`: Part O commands
  arm one expectation immediately before their own `SetJSAMObject`; every other model replacement (edit,
  import, undo, redo, a second or unrelated simulation) arrives unarmed and drops the run with a reason.
  Wired in `AnalyticalWindow.UIAnalyticalModel_Modified`; `_Closed`/`_Opened` call `Reset()`.
- Session state only. Nothing is written into the model - `OverheatingScenario` has no persistence seam
  in `SAM.Analytical`, and inventing one in the UI would move engineering state into the UI's ownership.

### 3. BLOCKER 3 - SAM_Systems dependency placed in the WPF layer

`SAM.Analytical.Systems` is referenced **only** from `WPF/SAM.Analytical.UI.WPF.csproj`;
`SAM.Analytical.UI` stays free of it. `VentilationUnitCatalogue` (in `SAM.Analytical.UI.WPF`) calls
`SAM.Analytical.Systems.Query.VentilationUnitTemplates` and then `SAM.Analytical.Query.CapacityDescriptors`
/ `UnselectableVentilationUnitTemplates`. No schema parsing and no selection rule in SAM_UI. Three
distinct states: `Unavailable`, `NoneSelectable`, `Selectable`. The `Unavailable` description explicitly
says it is *not* a statement that no product could serve the dwellings.

The catalogue resolves at runtime from the installed resources
(`%APPDATA%\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON`), which the
SAM_Deploy installer already places; no deployment change was needed.

### 4. Part O preparation UI

- `PartOVentilationStrategyOption` - the picker. Offers only the two base provisions, each carrying the
  canonical word (`NV`, `MVHR`) and the iteration SAM says that route is defined over
  (`Query.PartOIterationVentilationMode`). **No free text anywhere reaches the API**, so the
  `"NaturalVentilation"` synonym - which prepares successfully then refuses every space at assessment - is
  unreachable from the UI. No analytical vocabulary was changed.
- `PartOIterationWindow` - base provision, dwelling scope, catalogue toggle. Scope comes from
  `Query.PartFDwellingZones` (the single source of that policy, including the legacy all-unmarked case);
  out-of-scope zones are derived **by difference from what the policy returned** and reported as
  "Outside current Part O dwelling preparation scope". No `UV` is assigned; no common-space criterion.
- `Modify.PreparePartOIteration` (UI command) - builds the strategy dictionary, reads the catalogue, makes
  **one** call to `SAM.Analytical.Modify.PreparePartOIteration`, and adopts the returned prepared model.
  Passes `null` (not an empty list) when no selection is wanted, so the preparation reads it as
  "no catalogue offered" = Iteration 1a.
- `PartOPreparationWindow` - two grids. Equipment (per AHU): design supply/extract duty, selected product,
  maximum supply/extract, supply/extract headroom, selection outcome - eight separate columns. Spaces:
  Part F required vs design supply/extract. Values read from `Query.AirHandlingUnitDesignDuty`,
  `Query.SelectedVentilationUnitCapacityDescriptor`, `Query.CalculatedSupplyAirFlow` and
  `PartFSpaceData.ContinuousDesignFlowRate_Lps`. The whole-run duty total is labelled as a total across
  dwellings, not as any one dwelling's duty.

### 5. TAS -> TM59 lineage

`Modify.AssessPartOTM59(PartORun)` runs the same sequence as the accepted `Tas.TSDQueryTM59Results`
component - `Convert.ToSAM(TSD)`, `Create.TM59AssessmentCalculator`, `OverheatingScenarioMap`,
`RestoreDesignInternalConditions`, `Spaces(null, null)`, `Calculate`, `TM59AssessmentReport` - and reads
its model from `PartORun.AnalyticalModel_Assessment` only. `PartOTM59ResultWindow` shows the production
report text verbatim plus the spaces that produced no result. No TM59 criterion, limit or verdict is
computed or reformatted in WPF.

`Modify.Simulate` gained a `PartORun` overload; it sets `workflowCompleted` only where
`WorkflowCalculator` actually returned a model, and completes the run with **that** model and
`Path.ChangeExtension(path_TBD, "tsd")`.

### 6. Ribbon

- Edit tab, new `Part O` group: **Prepare Iteration**.
- Results tab, new `Part O` group: **Overheating (TM59)** - enabled only for `CanAssess`, with
  `ToolTipService.ShowOnDisabled` so the reason is visible while disabled.
- The simulation window's "Domestic Overheating" tick (TAS DomOv XML) is untouched and kept conceptually
  separate.

## Review findings addressed (PR #76, 2026-09-01)

Two P1 findings on the first push (`1437d88`), both accepted and both about the same invariant: what
`PartORunState.WorkflowCompleted` is allowed to mean. Restated at the top of `PartORun`'s own
documentation - **"this prepared run produced the full-year results being assessed"**, never "a TSD
exists". No architecture change, no Iteration 2B.

### P1-A - a Part O run may only be completed by a full-year simulation

`completePartORun` required only that `WorkflowCalculator` returned a model. It returns one for a sizing
run too, and `Modify.Simulate` can produce a *fresh one-day* TSD when `shadingUpdated` forces a
simulation over an unticked Full Year box. Any of those promoted the run, and the TM59 command then
assessed criteria and verdicts over an incomplete hourly series.

New pure query `Query.IsPartOFullYearSimulation(WorkflowSettings)` - `Simulate && SimulateFrom == 1 &&
SimulateTo == 365`, read off the settings **actually handed to `WorkflowCalculator`** rather than off the
tick box, because the day range still comes from the two text boxes beside it. `Modify.Simulate` now
carries `workflowSimulatedFullYear` beside `workflowCompleted` and ANDs it into the predicate. Nothing
else reads it, so a normal non-Part-O simulation is unchanged.

A prepared run this simulation cannot complete is now dropped **with the reason it was actually refused
for**, before the model replacement that would otherwise report it as an outside edit, and that sentence
is appended to the completion dialog.

### P1-B - the TSD must be the one this workflow wrote

`Complete` accepted `<project>.tsd` because it existed and then recorded its *already old* write time as
this run's, so `IsAssessable` afterwards approved an earlier session's results against the newly prepared
model. `Modify.Simulate` deletes only the TBD, so a non-simulating workflow leaves the old TSD in place.

New `PartORun.ExpectResults(path_TSD)`, called **before** the workflow, fingerprints whatever is at that
path (`exists` + length + write time). `Complete` now refuses unless a fingerprint was armed for exactly
that path and the file has since been created or changed. Length as well as write time, so a rewrite
inside the filesystem's timestamp granularity is still seen; where both match, the file is treated as
untouched - refusing a genuine rerun is the safe way to be wrong.

Deleting the prior TSD was considered and rejected: it would destroy a user's previous results whenever a
Part O run failed, and it changes the existing workflow contract. The fingerprint changes nothing outside
the Part O promotion.

**Arming is where both fixes meet.** `ExpectResults` is armed only where the settings describe a
full-year run, so a partial, one-day or sizing-only workflow leaves the run unarmed and `Complete`
refuses it *even if reached* - the invariant is held by the state machine, not by the caller remembering
to check. `IsAssessable`'s post-completion stale-file check is kept unchanged: it solves the different
problem of a rewrite after a legitimate completion.

Ten regressions added to `PartORunLineageTests` (11 -> 21), and every successful completion in that file
now goes through a `CompleteThroughAFullYearWorkflow` helper that performs the production
arm -> write -> complete sequence, so a test cannot pass by calling `Complete` the way no caller does.

## Decisions / assumptions
- `PartORun` holds model + scenarios directly rather than a `PartOIterationPreparation`, because that
  type's setters are `internal` to `SAM.Analytical` and so could not be constructed by a test. The
  `Prepare(PartOIterationPreparation)` overload remains as the production entry point.
- Rows are keyed on the `AirHandlingUnit`, not paired positionally with
  `PartOIterationPreparation.VentilationUnitSelections`, which is documented as **not** item-for-item with
  `AirHandlingUnits`. Headroom is stated exactly as `VentilationUnitSelection.SupplyHeadroom_Lps` defines
  it, over the two values already on the row.
- `RibbonButton_CreateTBD_Click` (line ~2076) still calls the parameterless `Simulate()`. Its ribbon
  button is commented out in XAML, so it is unreachable; if re-enabled, its model replacement would
  correctly drop a pending run. Left alone deliberately.
- A note is emitted for a workflow-path space the workflow itself could not resolve. Harmless (the name
  fallback cannot invent a stamp there, because TBD space names come from zone names), and informative.

## Files changed
Modified:
- `WPF/SAM.Analytical.UI.WPF/Modify/Simulate.cs`
- `WPF/SAM.Analytical.UI.WPF/SAM.Analytical.UI.WPF.csproj` (+`SAM.Analytical.Systems`)
- `WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml`
- `WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml.cs`
- `WPF/SAM.Analytical.UI.WPF.Tests/SAM.Analytical.UI.WPF.Tests.csproj` (+`SAM.Analytical.Tas`)

Added:
- `SAM_UI/SAM.Analytical.UI/Enums/PartORunState.cs`
- `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartORun.cs`
- `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOVentilationStrategyOption.cs`
- `WPF/SAM.Analytical.UI.WPF/Enums/VentilationUnitCatalogueState.cs`
- `WPF/SAM.Analytical.UI.WPF/Classes/PartO/VentilationUnitCatalogue.cs`
- `WPF/SAM.Analytical.UI.WPF/Classes/PartO/PartOEquipmentRow.cs`
- `WPF/SAM.Analytical.UI.WPF/Classes/PartO/PartOSpaceRow.cs`
- `WPF/SAM.Analytical.UI.WPF/Modify/RestampSimulationZoneIdentity.cs`
- `WPF/SAM.Analytical.UI.WPF/Query/IsPartOFullYearSimulation.cs`
- `WPF/SAM.Analytical.UI.WPF/Modify/PreparePartOIteration.cs`
- `WPF/SAM.Analytical.UI.WPF/Modify/AssessPartOTM59.cs`
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOIterationWindow.xaml{,.cs}`
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOPreparationWindow.xaml{,.cs}`
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOTM59ResultWindow.xaml{,.cs}`
- `WPF/SAM.Analytical.UI.WPF.Tests/SimulationZoneIdentityTests.cs` (7)
- `WPF/SAM.Analytical.UI.WPF.Tests/PartORunLineageTests.cs` (21)
- `WPF/SAM.Analytical.UI.WPF.Tests/PartOPresentationTests.cs` (12)

## Validation
- `dotnet build SAM_UI.sln -c Debug` - succeeded, 0 errors (the pre-existing MSB3245/MSB3270 warnings for
  `System.Data.DataSetExtensions`, `Microsoft.CSharp`, `PresentationFramework.Aero2` and the Interop
  architecture mismatch are unchanged).
- `dotnet test WPF/SAM.Analytical.UI.WPF.Tests` - **Passed 227, Failed 0, Skipped 0** (187 baseline + 40).
- BLOCKER 1's regression was written first and confirmed red (5 x CS0117, missing seam) before the fix.
- Two genuine defects in this work were caught by its own tests and fixed:
  1. `PartOIterationWindow` reported unmarked zones as out of scope even where `PartFDwellingZones` had
     put them **in** scope (the legacy all-unmarked model). Out-of-scope is now derived by difference from
     the policy's own return value instead of by a second reading of `IsDwelling`.
  2. The `NoneSelectable` test fixture was itself invalid (`VentilationUnitTemplate.IsValid` requires a
     `Source`); the reader had correctly returned `Unavailable`. Fixture fixed, production code was right.
- Authority-leakage sweep over the new files: no call to `SelectSmallestCapableVentilationUnit`,
  `CapableVentilationUnits` or `Modify.SelectVentilationUnit`; no TM59 criterion, limit or
  `MaxExceedableHours` arithmetic. Re-run after the 2026-09-01 correction - still clean; the only hit is
  a doc comment in `VentilationUnitCatalogue` saying why the selection rule is *not* called here.

### Licensed UI acceptance (2026-09-01) - PASSED

Run on the acceptance fixture
`OneDrive - Tetra Tech, Inc/Documents/SAM_daily/2026-07-15 PartO/SAM_zoningAM.sam` (9 spaces, 4 zones,
Flat 1/2/3 `IsDwelling = true`, `Corridor` false), weather `CIBSE Weather 2021.twd`. Three full-year
licensed runs of the same chain, all three PASS and agreeing on every assessed number:

| Run | Output | Sizing | "Domestic Overheating" |
|---|---|---|---|
| A | `C:\TasOut\pui` | on | off |
| B | `C:\TasOut\pui2` | on | on |
| **C (headline)** | `C:\TasOut\pui3` | **off** | on |

Run C is the one to quote: the standing instruction for Part O licensed runs is `Sizing = false`, since the
assessment reads free-running hourly temperatures. On this free-running fixture it made no difference - all
three runs report the same eight exceedance counts (13 / 4 / 3 / 4 / 0 / 2 / 4 / 0 hours against limits
262 / 262 / 262 / 142 / 262 / 262 / 142 / 262) and the same
`TM59 OCCUPIED-SPACE ASSESSMENT: PASS` - which is itself worth recording, since these are internal
conditions with no cooling setpoint.

**How it was driven.** A console harness (`C:\TasOut\poui`, not part of the repository) hosts a WPF
`Application`, calls the production commands - `Modify.AddVentilationByPartF`,
`Modify.PreparePartOIteration`, `Modify.Simulate(uIAnalyticalModel, partORun)`, `Modify.AssessPartOTM59`
- and completes each real dialog from a `Window.Loaded` class handler instead of by mouse, reading the
values straight off the windows' own controls. The one piece of `AnalyticalWindow` wiring it reproduces
is that window's single-line handler `partORun.NotifyModified()` on `UIAnalyticalModel.Modified`. **Not**
covered by this run: the ribbon controls themselves (their enabled state and tooltip are the expression
`RefreshPartOButtons` evaluates over `PartORun.CanAssess` / `State` / `InvalidationReason`, which the
harness logs, and `PartORunLineageTests` covers).

| Check | Result |
|---|---|
| Dwelling scope | `3 dwelling zone(s) in scope`; `'Corridor' (marked not a dwelling)` named as outside it |
| Catalogue | `1 selectable ventilation unit product(s) available`, offered and ticked |
| Flat 1 design duty (`MVHR-01`/`MVHR 1`) | **30.0 / 30.0 l/s** |
| Flat 2 design duty (`MVHR-02`/`MVHR 2`) | **63.0 / 63.0 l/s** |
| Flat 3 design duty (`MVHR-03`/`MVHR 3`) | **63.0 / 63.0 l/s** |
| Selected product maximum | **150.0 / 150.0 l/s**, in its own `Maximum supply`/`Maximum extract` columns, headroom 120/120 and 87/87 |
| Equipment capacity overwriting design airflow | None. Design columns are 30/63/63 with the same 150/150 product on all three |
| TAS workflow | Licensed full year, days 1-365, `Model successfuly converted`, 1min18sec |
| Model assessed | `partORun.AnalyticalModel_Assessment`, the workflow output - the result window states it, and `PartORun` has no other source for it |
| Zone identity on the assessed model | 9 of 9 spaces stamped, 0 unstamped |
| TM59 assessment | `Assessed 9 space(s)`, 8 mechanical results, **PASS** |

**The eight mechanical results are correct for the UI's dwelling scope, and are not the nine the earlier
Grasshopper/harness acceptance reported.** `Corridor_1` is in the `Corridor` zone, which
`Query.PartFDwellingZones` puts outside the Part O dwelling scope, so no `OverheatingScenario` covers it
and the report says so by name under `SPACES NOT ASSESSED`. The SAM-repo acceptance drove
`PreparePartOIteration` over a scope that included it. Eight assessed dwelling spaces + one reported
common space = the nine the model has.

**Simulation-space mapping: no identity-lineage refusal.** Every one of the nine design spaces resolved.
The only `SimulationSpaceMap` refusals are the three MVHR **plant** zones (`MVHR-01/02/03`), which TAS
carries as zones and which have no design space at all - "does not resolve to exactly one design space",
correctly left out rather than name-matched. `Corridor_1` mapped and was refused for the separate reason
that nothing states its ventilation strategy.

Cross-check against the SAM-repo licensed acceptance of the same fixture: annual occupied hours
8,760 for the residential conditions and 4,745 for the kitchens, limits 262 and 142. Identical.

**The restamp seam, measured on the licensed runs.** Runs B and C had "Domestic Overheating" ticked, so
both went through `Modify.RestampSimulationZoneIdentity` on the workflow output. Both produced **no**
zone-identity note in the completion dialog: the seam is a complete no-op on the workflow path, because
`Modify.UpdateIds` had already stamped all nine spaces. And the DomOv XML each wrote carries nine
`DomOverheatZoneItem/GUID` values that are **byte-for-byte the nine TAS `ZoneGuid` stamps** on the
workflow model - checked pairwise, 9/9 match in run C
(`C:\TasOut\pui3\Report XMLs\PartOUI3DomOv.xml`). That is the identity the external TAS tool needs, and
the thing the fill exists to preserve on the Simulate-unticked path.

Those runs also settle the "differ by construction" point empirically: the same fixture, exported three
times, produced three entirely different sets of TAS zone guids (`Studio 1_0` was `{5F14C5BC-...}`,
`{B65C5A8D-...}`, `{1722CE03-...}`) while the SAM `space.Guid` values are stored in the `.sam` and did not
change. A SAM space guid therefore cannot be the TAS zone identity, and the exporter's silent fallback to
it cannot be harmless. The same observation is a second proof that the assessed model is the workflow
output: the stamps on it are freshly minted per run, which neither the loaded model nor the preparation
output could carry.

**Stale state - PASSED.** Prepare (`State=Prepared`, assessment disabled with "A Part O iteration is
prepared but not simulated"), then an unrelated space rename adopted through `SetJSAMObject` with no
`ExpectModification`: the run drops to `State=None` with

> The model changed after the Part O iteration was prepared, so the preparation and its overheating
> scenarios no longer describe it. Prepare the iteration again before simulating.

which is what the ribbon tooltip shows, and the production `Modify.AssessPartOTM59` then refuses with the
same sentence in its own message box. The stale run is not silently used. The reason is readable while the
button is unavailable because `RibbonButton_AssessPartOTM59` declares
`ToolTipService.ShowOnDisabled="True"`.

## Issues / blockers
- **Not empirically reproduced:** the duplicate-name collapse (defect 1) is confirmed by inspection
  (`List.Find` first-match + unconditional write); the old code was replaced rather than characterised in
  a test, and deliberately not re-created in one. The re-spelling half (defect 2) *is* demonstrated - a
  braced upper-case guid now survives verbatim, which the `out Guid` path could not have produced - and
  the *consequence* of having no stamp at all is now demonstrated end to end through the exporter
  (`AnUnstampedSpace_ExportsTheSAMSpaceGuid_UntilTheFillGivesItTheTasIdentity`).
- **The ribbon controls themselves are not exercised by the licensed run.** `AnalyticalWindow` was not
  instantiated; the enabled state and tooltip are the expression `RefreshPartOButtons` evaluates, which
  the harness logs from the same three `PartORun` reads, and `PartORunLineageTests` covers the run states
  behind it. Clicking the two ribbon buttons by hand once is still worth doing before release.
- Out of scope by instruction and not started: Seam 2 targeted design-airflow editing, Iteration 3,
  generic `AirHandlingUnitTemplate`, common-space `UV`, production ventilation-vocabulary normalisation.
- Still open elsewhere (not this repo): `Tas.TSDQueryTM59Results`'s `_analyticalModel` input is documented
  only as "SAM Analytical Model" - the workflow-model requirement is not stated at the seam. A one-line
  description fix in `SAM_Tas_Grasshopper`, deliberately not bundled here.

## Next step
1. Review PR #76 - the GitHub diff, both P1 resolutions and CI - then merge. Nothing is pending in this
   working tree.
2. Before release, click the two new ribbon buttons by hand once in the real application - the licensed
   runs drove the commands and their windows, not the ribbon.
