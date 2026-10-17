# Unified Run Simulation (Tas / OpenStudio / Both) and Tas vs OpenStudio comparison (SAM_UI#231)

Status: implemented on `claude/tas-openstudio-comparison`; draft PR, owner manual test and review required. Not merged.
Issue: SAM-BIM/SAM_UI#231 (gate G2; parent #227, tracker SAM-BIM/SAM#206).
Base: `sow/2026-Q4` at `ebe5e09b30b95e43e5d047b3aef2d8c5f01110e9`.

**Stacked on draft PR #237** (SAM_UI#229, `claude/openstudio-mcp-server-1jmjlo` at `95135ce`). This PR's own changes are the commits after `95135ce`. Merge #237 first; this diff then shrinks to them.

**Depends on SAM-BIM/SAM_Validation#14** (SAM#211, the comparator library), which uses the same branch name. SAM_UI CI selects that branch by name until it is merged.

Tested content SHA: `035224c` (local; comparison workflow `3db8d7b` + unified Run Simulation). SAM_Validation tested at `70f1411`. This record is the only change after it.

## What it does
### Simulate > Run Simulation (the unified command; open model required)
A dialog offers three choices. It only chooses; each choice runs an existing workflow.

| Choice | What runs | Results and diagnostics |
|---|---|---|
| **Tas only** | The existing Energy Simulation path (`Simulate(partORun)`), unchanged: the same code as the Energy Simulation button | As before, written back into the open model |
| **OpenStudio/EnergyPlus only** | #229's OpenStudio Simulation (`SimulateOpenStudioAsync`) on a frozen copy | #229's summary and run folder (`openstudio-run.json`, `<model>_OpenStudio.json`) |
| **Both** | The comparison run below, then the comparison window opens with it | Compared, Partially completed, Failed or Cancelled (see below) |

- **Migration:** Energy Simulation, OpenStudio Simulation and Compare Tas / OpenStudio stay on the ribbon, unchanged. Their tooltips name the matching Run Simulation choice. No command was removed or changed in behaviour. The last choice is offered again; a choice whose run is in progress is disabled.
- **Sequential.** Both runs Tas then OpenStudio, in this milestone. Parallel execution is Phase 2 (SAM_UI#234–#236), not part of this PR.

### The Both run (also *Run Tas + OpenStudio…* in the comparison window)
  1. Freezes the model once to `snapshot.json`, and prepares one `weather.epw`: the user's EPW, or the model's WeatherData exported once. Both files are made **read-only**, and their SHA-256 is checked again before each producer and before comparing.
  2. Runs the **validated B1a producers** one after the other, each in its own process, on that snapshot and EPW:
     - `benchmark-tas --model --weather --tbd --out` (SAM_Tas);
     - `benchmark-openstudio --model --weather --out --work` (SAM_OpenStudio).
  3. Writes stdout/stderr to `benchmark-*.log`. Cancel kills the running producer's process tree.
  4. A producer **completed** when it exited 0 and wrote its B1a document. A failed (or not found) producer does not stop the other one.
  5. Outcome (status in `engine-comparison-run.json`, with `succeeded` per producer):
     - **Compared:** both completed. SAM_Validation compares them and writes `comparison.md/.csv/comparison-summary.json`.
     - **Partially completed:** one completed. Nothing is compared; the completed engine's document is kept and shown on its own. No result is substituted for the other engine.
     - **Failed:** neither completed, the inputs could not be prepared, or the snapshot/weather changed during the run. In the last case the documents are kept for diagnosis but neither shown nor compared.
     - **Cancelled.**

  The run folder is `<folder>\<model>_TasOpenStudio_<yyyyMMdd-HHmmss>_<id>\`, with `tas\` and `openstudio\` working folders. A document left by a failed producer stays in the folder but is never treated as a result.

### Comparison window
Opened by Run Simulation > Both when the run finishes, whatever its status, and by **Simulate > Compare Tas / OpenStudio** (always available, because comparing existing runs needs no open model).
- **Partially completed:** an amber banner, *PARTIALLY COMPLETED - not compared: <engine> did not complete (<error>)*. The Model/Spaces tabs show only the completed engine's column under SAM_Validation's metric keys (`Query.ModelMetricValues`/`SpaceMetricValues`, SAM_Validation#14). The status is *Not compared*, with no difference or band; an unreported metric is blank with *Not reported by <engine>*. Provenance says the gate is not evaluated. Diagnostics give both producers' exit codes, errors and logs.
- **Failed / Cancelled:** a red banner and the run's diagnostics; nothing else.
- **Load run folder…** / **Browse** + **Compare** load existing `benchmark-TAS.json` / `benchmark-OpenStudio.json`. A #229 OpenStudio run folder has no B1a document, and the window says so.
- **Compared banner:** the gate and counts. **NOT THE SAME INPUTS** appears when SAM_Validation's provenance gate finds mismatches.
- **Model / Spaces tabs:** unit, Tas, OpenStudio, absolute difference, difference %, band, status and note.
  - Status is one of *Compared*, *Not equivalent* (SAM_Validation's informational metrics, or a unit mismatch), *Missing on Tas/OpenStudio*, *Not reported by either engine*, or *Not compared* (partial run).
  - Missing values are blank, never 0. Band colours apply to *Compared* rows only, so a non-equivalent or one-sided metric is never shown as passing.
- **Provenance tab:** the provenance gate and mismatches; per engine: route, engine, version, model, canonical hash, source hash, weather, design days and run information. The per-space input manifests are stated as **not available** (SAM_OpenStudio#30, SAM_UI#230).
- **Diagnostics tab:** gate statuses and counts; unmatched, duplicate and ambiguous spaces; model-total reconciliation; B1a warnings and notes; producer exit codes and logs.
- **Open report / Export reports… / Open run folder.** Reports are written by SAM_Validation's `WriteReports`.

## Decisions
- **Run Simulation chooses; it does not re-implement.** Tas only calls the same private `SimulateTas()` as the Energy Simulation button, so the Tas workflow cannot drift between the two entry points.
- **Both uses the producers, so its Tas run is not the Energy Simulation dialog's** (see below). The dialog text says so. Tas only is the dialog-driven run.
- **Immutable inputs:** a read-only attribute plus a SHA-256 re-check, not a copy per engine. Both engines read the same bytes, and a change is detected rather than silently compared.
- **Partial results use SAM_Validation's metric list, not a second one.** SAM_Validation#14 added `ModelMetricValues`/`SpaceMetricValues`, and its comparator now builds its rows from them (goldens unchanged).
- **No comparator in SAM_UI.** All values, differences, bands, notes, gates, alignment and reports come from `SAM.Analytical.Benchmark.Compare` (SAM#211). `EngineComparisonRow` renders them with SAM_Validation's own `Format`; the only addition is the status label, taken from SAM_Validation's availability, unit and informational flags.
- **B1a from the producers, not from SAM_UI.** The validated producers write the B1a documents, and SAM_UI runs them as processes. This avoids a second results-to-B1a mapping, and Tas COM stays out of the UI process.
  - Consequence: Tas runs with the `benchmark-tas` producer's fixed workflow settings (sizing, unmet hours, full year), **not** the Energy Simulation dialog's. The Energy Simulation command itself is unchanged.
  - OpenStudio runs the same `ToOpenStudio` pipeline as #229's Simulate > OpenStudio Simulation, through `benchmark-openstudio`.
- **Producer location:** environment variable `SAM_BENCHMARK_TAS` / `SAM_BENCHMARK_OPENSTUDIO` (the exe or its folder), then the application folder, then a location the user picked once (saved in `%APPDATA%\SAM\engine-comparison-producers.json`).
- **Found while testing:** PR #237's `openstudio-run.json` threw on any diagnostic. It is fixed on #237 in `3393331`, and the same pattern is avoided here.

## Not implemented / blockers (stated, not substituted)
- **Per-space input manifests** (what each engine received) do not exist yet: SAM_OpenStudio#30, SAM_UI#230. Input equivalence is shown only as B1a provenance (same canonical model, weather hash and design-day source).
- **B1a from a #229 OpenStudio Simulation run folder:** not produced. The OpenStudio B1a builder lives inside the `benchmark-openstudio` executable; a library for it would be a SAM_OpenStudio change, like SAM#208 for Tas.
- **Deployment:** SAM_Deploy's BuildAll (master) does not build SAM_Validation, and the producer executables are not deployed. A source build is needed for testing; a SAM_Deploy change is needed before release.
- **Engineering acceptance:** bands are tolerance bands, not verdicts. Benchmark cases and the classification of differences are #232/#233.

## Files changed (this PR's commits)
- `WPF/SAM.Analytical.UI.WPF/Classes/Comparison/EngineComparison.cs` (row, status, loaded comparison, single-engine `EngineResult`) and `EngineComparisonRun.cs` (run record, `PartiallyCompleted`, `Succeeded`, manifest).
- `WPF/SAM.Analytical.UI.WPF/Query/EngineComparison.cs` (load, rows, single-engine rows, provenance, diagnostics, run lines, run folder) and `Query/BenchmarkProducer.cs` (producer location and arguments).
- `WPF/SAM.Analytical.UI.WPF/Modify/RunEngineComparison.cs` (orchestration, read-only inputs, hash checks, partial completion, process runner, saved producer path) and `Modify/SimulateBothEngines.cs` (the Both UI flow: producers, folder, weather, progress).
- `WPF/SAM.Analytical.UI.WPF/Windows/RunSimulationWindow.xaml(.cs)` (the choice dialog), `Windows/EngineComparisonWindow.xaml(.cs)` (`ShowRun`, partial and failed views), `Windows/AnalyticalWindow.xaml(.cs)` (Run Simulation and Compare buttons; `SimulateTas`, `RunOpenStudioSimulationAsync` and `RunBothEnginesAsync` shared by the buttons).
- `WPF/SAM.Analytical.UI.WPF/SAM.Analytical.UI.WPF.csproj` and the tests csproj: references to `SAM_Validation\build\SAM.Analytical.Benchmark(.Compare).dll`.
- `WPF/SAM.Analytical.UI.WPF.Tests/EngineComparisonTests.cs` and `Resources/EngineComparison/*` (fabricated SAM_Validation documents, README).

## Validation
Local runs were in a Linux container (no Windows, Tas or OpenStudio), compiling against SAM `618db819`, SAM_SQLite `ec2eb86d`, SAM_OpenStudio `d4216f35` and SAM_Validation `70f1411`, using machine-local harness projects (not committed). Only `UIAnalyticalModel`, `ProgressWindowHost` and two private `Query` helpers were stubbed.

| Check | Where | Result |
|---|---|---|
| New files plus #237's, including the comparison and Run Simulation XAML windows, compiled as net8.0-windows (nullable) | Local | PASS, 0 errors / 0 warnings |
| `AnalyticalWindow.xaml(.cs)` (ribbon wiring) | Local | **NOT RUN** (not in the harness: too many dependencies). Compiled only by hosted CI |
| `EngineComparisonTests` (16) and `OpenStudioSimulationTests` (10), fabricated documents and fake producers | Local | PASS 26/26 |
| Every view cell equals SAM_Validation's `comparison.csv` (scenario and weather-mismatch) | Local | PASS |
| Orchestration: one snapshot and one EPW to both producers, compare, reports, manifest, model unchanged; cancel and no-weather paths | Local (fake producers) | PASS |
| Partial completion: Tas fails, or a producer is not found → *PartiallyCompleted*, the other engine kept, nothing compared; a failed producer's document never used; both fail → *Failed* | Local (fake producers) | PASS |
| Immutability: snapshot and weather read-only for the producers; a changed snapshot stops the next producer and the comparison | Local (fake producers) | PASS |
| Single-engine rows: SAM_Validation's keys and order, *Not compared*, no difference or band, unreported values blank | Local | PASS |
| Hosted Windows build (SAM_UI.sln, SAM_Validation from the same-named branch) | GitHub CI | See the PR description |
| **Real Tas + EnergyPlus comparison** | Owner's Windows laptop | **NOT RUN** (procedure below) |

No result in this record comes from a real engine.

## Windows laptop test procedure (owner)
**Prerequisites:**
- A licensed Tas with its COM servers registered.
- OpenStudio **3.10.0**: `openstudio --version` prints 3.10.0.
- Visual Studio 2022 (with .NET Framework MSBuild, for the Tas COM references) and the .NET 8 SDK.

1. **Check out, side by side in one folder:**
   - `SAM_Validation` @ `claude/tas-openstudio-comparison`;
   - `SAM_UI` @ `claude/tas-openstudio-comparison`;
   - every other dependency @ `sow/2026-Q4`: SAM, SAM_Mollier, SAM_Systems, SAM_Windows, SAM_gbXML, SAM_SolarCalculator, SAM_Tas, SAM_SQLite, SAM_OpenStudio, SAM_Excel, SAM_GEM, SAM_LadybugTools, SAM_Solver.
2. **Build in CI order** with `msbuild <sln> /t:Restore;Rebuild /p:Configuration=Release`. SAM_OpenStudio additionally gets `/p:Platform=x64`. Order: SAM, SAM_Mollier, SAM_Systems, SAM_Windows, SAM_gbXML, SAM_SolarCalculator, **SAM_Validation**, **SAM_SQLite**, **SAM_OpenStudio (x64)**, SAM_Tas, SAM_Excel, SAM_GEM, SAM_LadybugTools, SAM_Solver, SAM_UI.
3. **Producers.** Confirm that `SAM_Tas\build\benchmark-tas.exe` and `SAM_OpenStudio\benchmark\SAM.Analytical.OpenStudio.Benchmark\bin\x64\Release\net8.0\benchmark-openstudio.exe` exist. Then either set `SAM_BENCHMARK_TAS` and `SAM_BENCHMARK_OPENSTUDIO` to them, or locate them when SAM_UI asks (asked once, then remembered).
4. **Model.** Start SAM Analytical and open a Tas-legal model, for example HungaryHouse (13 spaces). Note: SAM_OpenStudio's two-cell fixture is OpenStudio-oriented and SingleBox is illegal in Tas. **Save** it and note its SHA-256: `certutil -hashfile <file> SHA256`.
5. **Tas unchanged.**
   - Run Simulate > **Energy Simulation** as usual. The dialog and results should be as on `sow/2026-Q4`.
   - Then run Simulate > **Run Simulation** > **Tas only**: the same dialog and the same results.
6. **OpenStudio alone (#229).** Run Simulate > **Run Simulation** > **OpenStudio/EnergyPlus only**, and once via the **OpenStudio Simulation** button. Expect: finished; `openstudio-run.json` with `status` Succeeded, `fatalCount` 0, `diagnostics` filled; and `<model>_OpenStudio.json` holding only `SAM.Analytical.OpenStudio` results.
7. **Both.** Simulate > **Run Simulation** > **Both**: pick a folder, then the model's weather (Yes) or an EPW.
   - The progress window steps: Freezing, Weather, Running Tas, Running OpenStudio/EnergyPlus, Comparing. Cancel must work at any step.
   - When it finishes, the comparison window opens by itself with the run.
   - In the run folder, `snapshot.json` and `weather.epw` are read-only (file Properties).
8. **Run folder.** Expect:
   - `snapshot.json`, `weather.epw`;
   - `benchmark-TAS.json`, `benchmark-OpenStudio.json`;
   - `benchmark-tas.log`, `benchmark-openstudio.log`;
   - `comparison.md/.csv/comparison-summary.json`;
   - `engine-comparison-run.json`, with both producers at exit code 0 and `status` Compared;
   - `tas\<model>.tbd`, and `openstudio\` holding the EnergyPlus run.
9. **View.**
   - The banner has no "NOT THE SAME INPUTS".
   - Provenance shows the same canonical model hash and weather hash for both engines.
   - The Model tab shows heating/cooling energy (kWh) and peaks (kW) with differences and bands. Peak hours and whole-model peaks show as *Not equivalent*.
   - The Spaces tab lists every space by GUID and name. Unmatched spaces are on the Diagnostics tab.
   - Open report and Export work.
10. **Same as SAM_Validation.** `SAM_Validation\build\benchmark-compare.exe --tas …\benchmark-TAS.json --openstudio …\benchmark-OpenStudio.json --out x` gives a `comparison.csv` identical to the one in the run folder.
11. **Original model preserved.** No new undo step or modification; Save and re-hash gives the same hash as step 4.
12. **Existing runs.** Close the window, reopen it, use **Load run folder…** on the step-7 folder: same content. On the step-6 folder, the window explains that there is no B1a document.
12a. **Partial completion.** Make Tas fail without touching the build: close SAM, set `SAM_BENCHMARK_TAS=C:\Windows\System32\where.exe` (an existing executable that rejects the producer arguments and writes no B1a document), restart SAM, and run **Both** again. Expect:
   - OpenStudio still runs;
   - the run's `status` is PartiallyCompleted, Tas has `succeeded: false` with its error, and there is no `comparison.*`;
   - the window shows the amber *PARTIALLY COMPLETED - not compared: Tas did not complete* banner, an empty Tas column, *Not compared* rows with no band, and the OpenStudio values.

   Clear the variable and restart SAM afterwards.
13. **Mismatch.** Compare the step-7 `benchmark-TAS.json` with a `benchmark-OpenStudio.json` made with a different EPW. The banner shows NOT THE SAME INPUTS, and Provenance lists `weatherHash`.
14. **Tests.** `dotnet test WPF\SAM.Analytical.UI.WPF.Tests -c Release --filter "FullyQualifiedName~EngineComparisonTests|FullyQualifiedName~OpenStudioSimulationTests"`, then the full suite.

Report: PASS/FAIL per step (including 12a), `engine-comparison-run.json`, `comparison-summary.json`, the two producer logs if anything fails, and any native-DLL, OpenStudio CLI or Tas COM error.

## Presentation polish and review follow-up - 2026-10-17

Status: implemented on this PR; presentation, labelling and layout only. Not merged.
Tested content SHA: `647637f3039c2fc001aa820913142232cf5fa8d4` (local). The companion SAM_Validation presentation is on `claude/tas-openstudio-comparison` at `4a7da6b` (code `a39e4a9`; CSV/JSON unchanged).

### Presentation

- Model/Spaces grids show engineering quantities, absolute differences and normalized percentages to two decimals; hour-of-year values and hour differences stay integers. Numeric cells are right-aligned. Full precision remains available through cell tooltips and in the exported reports.
- Informational rows keep their band but use neutral styling and the label `Informational — excluded from numerical status`. Unit mismatch has its own `Unit mismatch` label and note. The banner shows the gate's total Fail count with the contributing/informational split (saved example: 10 Fail, 4 contributing, 6 informational) and a separate unit-mismatch count.
- Notes wrap and expose the full text through a tooltip. The Spaces GUID is compact in the grid; the grid context menu copies the complete GUID. Markdown shows the space name beside its GUID.
- The footer defines the unsigned normalized difference, notes that it is not percentage change against TAS, and states that peak hours use circular hours with no percentage. Markdown now carries the same definition and the 5.00%/15.00% thresholds.
- Provenance/Diagnostics text wraps; action buttons wrap below the explanatory text.
- Markdown now includes engine warnings/notes, the `designDaySource=None` explanation and the input-manifest limitation. Reconciliation values use two decimals and include a normalized difference column.
- CSV and summary JSON keep their existing columns, full precision and byte-identical content. No tolerance, gate, engine, conversion or comparison arithmetic changed.

### Review findings fixed

1. **Banner counts informational rows separately from incompatible rows.** `EngineComparison.ContributingFailCount`/`InformationalFailCount` now take SAM_Validation's authoritative split (`ComparisonResult`/`Query.IsInformationalForNumericalGate`) instead of re-deriving it from displayed row strings, and `UnitMismatchCount` is a separate count. The saved `box-both` banner reads: *Gate Fail: 6 match, 0 warn, 10 fail (4 contributing to numerical status, 6 informational and excluded); 2 missing on one engine, 0 unit mismatch, 0 not reported.* The 10 Fail bands are retained as 4 contributing + 6 informational; the six are labelled *Informational*, never as incompatible/not-equivalent.
2. **Model/Spaces sizing at 1180 and 820 px.** The grids no longer sit inside an outer `ScrollViewer` with a star-width Note column: at 820 px WPF compressed every column instead of scrolling. Each DataGrid now owns horizontal scrolling and every column has a fixed, readable width, including `Unit` (wide enough for `hourOfYear`) and `Note` (275 px Model, 160 px Spaces). Headers wrap; notes wrap with full-text tooltips; Metric/Status truncate with their full value on a tooltip; the footer action buttons remain visible. Regression `EngineComparisonWindowTests.Grids_KeepReadableColumns_AndScrollHorizontallyWhenNarrow` asserts every column keeps its width and that each grid scrolls horizontally at 820 px and fits at 1180 px.
3. **Fractional peak-hour thresholds in Markdown.** SAM_Validation `WriteMarkdown` cast `HourWarnAbsolute`/`HourFailAbsolute` to `int`, so a profile with 1.5 h / 24.5 h was reported as 1 h / 24 h. It now uses `Format.Number`, the same full-precision value as the CSV; integer formatting remains for hour-of-year indices and hour differences. The SAM_Validation regression asserts the Markdown value equals the raw profile and `toleranceProfileDetail` in the summary JSON, and the CSV per-row profile columns, for a 1.5/24.5 profile. Default-profile goldens are unchanged.

### Validation

- Focused `EngineComparisonTests` + `OpenStudioSimulationTests` + `EngineComparisonWindowTests` 32/32 PASS.
- Full `SAM.Analytical.UI.WPF.Tests` 2835/2835 PASS.
- `SAM_UI.sln` Release build 0 errors (463 solution warnings; no warning from the changed comparison files) with `SAM_COMMIT` set.
- `SAM.Analytical.Benchmark.Compare.Tests` 100/100 PASS and `SAM.Analytical.Benchmark.Compare.ApiTests` 2/2 PASS in SAM_Validation at `4a7da6b`; `SAM_Validation.sln` Release build 0 errors.
- Saved `box-both` run: still 6 Match / 0 Warn / 10 Fail / 2 N/A, Overall/Numerical Fail, Coverage Warn, Provenance/Reconciliation Pass, 4 contributing + 6 informational. Regenerated `comparison.csv` SHA-256 `8092B384DD9D9BA9289E1C10B77DB7C93BCD95E911B4A0F32CEE93373C0ECD3D` and `comparison-summary.json` SHA-256 `36B9D353B35CDFEB1FBAC70B78317BF36C4F393159F114FCF39716D825FF4A98` are byte-identical to the saved run.
- Reproduce:
  `dotnet test WPF\SAM.Analytical.UI.WPF.Tests -c Release --filter "FullyQualifiedName~EngineComparisonTests|FullyQualifiedName~OpenStudioSimulationTests|FullyQualifiedName~EngineComparisonWindowTests"`
  `dotnet test WPF\SAM.Analytical.UI.WPF.Tests -c Release`
  `dotnet build SAM_UI.sln -c Release`
  `dotnet test SAM.Analytical.Benchmark.Compare.Tests -c Release` (in SAM_Validation)
- Screenshots from the final build are committed under `documentation/evidence/pr231-presentation/`: `compare-model-1180.png`, `compare-model-820.png`, `compare-model-820-scrolled.png`, `compare-spaces-1180.png`, `compare-spaces-820.png`, `compare-spaces-820-scrolled.png`. They were rendered by the opt-in `EngineComparisonScreenshotHarness` (`SAM_ENGINE_COMPARISON_RUN` = the saved run folder, `SAM_ENGINE_COMPARISON_SCREENSHOTS` = an output folder) from `C:\TasOut\pr238-ui\box-both\000000_SAM_AnalyticalModel_TasOpenStudio_20261017-191647_1eb45853` (machine-local prerequisite, not committed).
- A stale ribbon-order expectation from #238's new commands was updated in `TasOptimisationWindowTests` (test-only; it expects Run Simulation, Solar Simulation, Energy Simulation, OpenStudio Simulation, Compare Tas / OpenStudio and Optimisation).

### Remaining gaps

The numerical TAS/OpenStudio discrepancy (heating, cooling, peaks, 871/872 hour, cooling peak) remains a separate engineering investigation, not resolved here. Real Tas/OpenStudio acceptance remains the owner's Windows procedure above. Per-engine input manifests and producer deployment remain as stated earlier.

## Next step
1. Hosted CI green at the final heads of this PR and SAM_Validation#15.
2. Owner review; the presentation findings above are ready for independent verification.
3. Merge order: SAM_Validation#15, #237, then this PR. Do not merge automatically.
4. `PROJECT_PROGRESS.md` closeouts after each merge.
