# Simulate > Compare Tas / OpenStudio - end-to-end comparison workflow (SAM_UI#231)

Status: implemented on `claude/tas-openstudio-comparison`; draft PR, owner manual test and review required. Not merged.
Issue: SAM-BIM/SAM_UI#231 (gate G2; parent #227, tracker SAM-BIM/SAM#206).
Base: `sow/2026-Q4` at `ebe5e09b30b95e43e5d047b3aef2d8c5f01110e9`.

**Stacked on draft PR #237** (SAM_UI#229, `claude/openstudio-mcp-server-1jmjlo` at `95135ce`). This PR's own change is the single commit after `95135ce`. Merge #237 first; this diff then shrinks to that commit.

**Depends on SAM-BIM/SAM_Validation#14** (SAM#211, the comparator library), which uses the same branch name. SAM_UI CI selects that branch by name until it is merged.

Tested content SHA: `3db8d7bfa99b59c2be2e87429e1ed36aba3c22a1` (local). This record is the only change after it.

## What it does
**Simulate > Compare Tas / OpenStudio** opens a read-only window. It is always available, because comparing existing runs needs no open model.

- **Run Tas + OpenStudio…** (open model required):
  1. Freezes the model once to `snapshot.json`, and prepares one `weather.epw`: the user's EPW, or the model's WeatherData exported once.
  2. Runs the **validated B1a producers** one after the other, each in its own process, on that snapshot and EPW:
     - `benchmark-tas --model --weather --tbd --out` (SAM_Tas);
     - `benchmark-openstudio --model --weather --out --work` (SAM_OpenStudio).
  3. Writes stdout/stderr to `benchmark-*.log`. Cancel kills the running producer's process tree.
  4. Compares the two documents with SAM_Validation and writes `comparison.md/.csv/comparison-summary.json`, plus the run record `engine-comparison-run.json`.

  The run folder is `<folder>\<model>_TasOpenStudio_<yyyyMMdd-HHmmss>_<id>\`, with `tas\` and `openstudio\` working folders.
- **Load run folder…** / **Browse** + **Compare** load existing `benchmark-TAS.json` / `benchmark-OpenStudio.json`. A #229 OpenStudio run folder has no B1a document, and the window says so.
- **Banner:** the gate and counts. **NOT THE SAME INPUTS** appears when SAM_Validation's provenance gate finds mismatches.
- **Model / Spaces tabs:** unit, Tas, OpenStudio, absolute difference, difference %, band, status and note.
  - Status is one of *Compared*, *Not equivalent* (SAM_Validation's informational metrics, or a unit mismatch), *Missing on Tas/OpenStudio*, or *Not reported by either engine*.
  - Missing values are blank, never 0. Band colours apply to *Compared* rows only.
- **Provenance tab:** the provenance gate and mismatches; per engine: route, engine, version, model, canonical hash, source hash, weather, design days and run information. The per-space input manifests are stated as **not available** (SAM_OpenStudio#30, SAM_UI#230).
- **Diagnostics tab:** gate statuses and counts; unmatched, duplicate and ambiguous spaces; model-total reconciliation; B1a warnings and notes; producer exit codes and logs.
- **Open report / Export reports… / Open run folder.** Reports are written by SAM_Validation's `WriteReports`.

## Decisions
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

## Files changed (this PR's commit)
- `WPF/SAM.Analytical.UI.WPF/Classes/Comparison/EngineComparison.cs` (row, status, loaded comparison) and `EngineComparisonRun.cs` (run record and manifest).
- `WPF/SAM.Analytical.UI.WPF/Query/EngineComparison.cs` (load, rows, provenance, diagnostics, run folder) and `Query/BenchmarkProducer.cs` (producer location and arguments).
- `WPF/SAM.Analytical.UI.WPF/Modify/RunEngineComparison.cs` (orchestration, process runner, saved producer path).
- `WPF/SAM.Analytical.UI.WPF/Windows/EngineComparisonWindow.xaml(.cs)`; `Windows/AnalyticalWindow.xaml(.cs)` (ribbon button).
- `WPF/SAM.Analytical.UI.WPF/SAM.Analytical.UI.WPF.csproj` and the tests csproj: references to `SAM_Validation\build\SAM.Analytical.Benchmark(.Compare).dll`.
- `WPF/SAM.Analytical.UI.WPF.Tests/EngineComparisonTests.cs` and `Resources/EngineComparison/*` (fabricated SAM_Validation documents, README).

## Validation
Local runs were in a Linux container (no Windows, Tas or OpenStudio), compiling against SAM `618db819`, SAM_SQLite `ec2eb86d`, SAM_OpenStudio `d4216f35` and SAM_Validation `b0f3a16`, using machine-local harness projects (not committed). Only `UIAnalyticalModel`, `ProgressWindowHost` and two private `Query` helpers were stubbed.

| Check | Where | Result |
|---|---|---|
| New files plus #237's, including the XAML window, compiled as net8.0-windows (nullable) | Local | PASS, 0 errors / 0 warnings |
| `EngineComparisonTests` (11) and `OpenStudioSimulationTests` (10), fabricated documents and fake producers | Local | PASS 21/21 |
| Every view cell equals SAM_Validation's `comparison.csv` (scenario and weather-mismatch) | Local | PASS |
| Orchestration: one snapshot and one EPW to both producers, compare, reports, manifest, model unchanged; failed-producer, cancel and no-weather paths | Local (fake producers) | PASS |
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
5. **Tas unchanged.** Run Simulate > **Energy Simulation** as usual. The dialog and results should be as on `sow/2026-Q4`.
6. **OpenStudio alone (#229).** Run Simulate > **OpenStudio Simulation**. Expect: finished; `openstudio-run.json` with `status` Succeeded, `fatalCount` 0, `diagnostics` filled; and `<model>_OpenStudio.json` holding only `SAM.Analytical.OpenStudio` results.
7. **Comparison run.** Simulate > **Compare Tas / OpenStudio** > **Run Tas + OpenStudio…**: pick a folder, then the model's weather (Yes) or an EPW. The progress window steps: Freezing, Weather, Running Tas, Running OpenStudio/EnergyPlus, Comparing. Cancel must work at any step.
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
13. **Mismatch.** Compare the step-7 `benchmark-TAS.json` with a `benchmark-OpenStudio.json` made with a different EPW. The banner shows NOT THE SAME INPUTS, and Provenance lists `weatherHash`.
14. **Tests.** `dotnet test WPF\SAM.Analytical.UI.WPF.Tests -c Release --filter "FullyQualifiedName~EngineComparisonTests|FullyQualifiedName~OpenStudioSimulationTests"`, then the full suite.

Report: PASS/FAIL per step, `engine-comparison-run.json`, `comparison-summary.json`, the two producer logs if anything fails, and any native-DLL, OpenStudio CLI or Tas COM error.

## Next step
1. Hosted CI green on this PR and on SAM_Validation#14.
2. Owner runs the procedure above.
3. Fix what it finds.
4. Owner review. Merge order: SAM_Validation#14, #237, then this PR.
5. `PROJECT_PROGRESS.md` closeouts after each merge.
