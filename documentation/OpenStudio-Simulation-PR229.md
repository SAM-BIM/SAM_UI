# Simulate > OpenStudio Simulation - minimal OpenStudio/EnergyPlus run from SAM_UI (SAM_UI#229)

Status: implemented on `claude/openstudio-mcp-server-1jmjlo`; HOLD on strict explicit-EPW equality (2026-10-18 evidence below). Not merged.
Issue: SAM-BIM/SAM_UI#229 (parent SAM-BIM/SAM_UI#227, tracker SAM-BIM/SAM#206).
Base: `sow/2026-Q4` at `ebe5e09b30b95e43e5d047b3aef2d8c5f01110e9`.
PR: the live PR for this branch in `SAM-BIM/SAM_UI` (its number is not committed here).
Tested content SHA: `3393331` (local Linux harness; hosted CI in the PR description). Earlier heads: `2b140f3` failed CI (a `build.yml` comment line split, pwsh parse error); `f8f10d6` failed CI (CS0117, SAM_UI's own `AnalyticalModelParameter` shadows SAM's); `722f40b` was green on hosted CI but carried the manifest bug fixed in `3393331` (see Validation). This record is the only change after the tested SHA.
Final PR head and hosted check results: recorded in the live PR description.

## What it does

A new ribbon button, **Simulate > OpenStudio Simulation**, sits beside Energy Simulation (Tas). The existing Tas commands are not changed.

1. The user picks an output folder. If the model has embedded weather, the user chooses between that weather and an EPW file; otherwise they must choose an EPW.
2. `UIAnalyticalModel.JSAMObject` (a deep clone) is frozen to `snapshot.json`. Every later step works on the copy read back from that file.
3. SAM_OpenStudio's own `AnalyticalModel.ToOpenStudio(epw, dir, null, runOptions, run: true, progress, token)` converts the model, writes OSM/OSW, runs `openstudio.exe` and reads the SQL. The weather precedence is SAM_OpenStudio's: the user's EPW, otherwise the embedded WeatherData.
4. On success, the copy has every result it already carried removed (Tas results, for example). This run's results are then attached with SAM_OpenStudio's `AdjacencyCluster.AddResults(sql)`, plus the model-level `AnalyticalModelSimulationResult` (`resultSet.ToSAM`). The result is saved as `<model>_OpenStudio.json`.
5. `openstudio-run.json` is written in every case, including failure and cancel.

Run folder: `<chosen folder>\<model>_OpenStudio_<yyyyMMdd-HHmmss>_<id8>\`. It contains:
- `snapshot.json`;
- `weather.epw` (a copy of the EPW used);
- `<model>.osm` and `.osw`;
- `run\eplusout.sql` and `.err`;
- `<model>_OpenStudio.json`;
- `openstudio-run.json`, with schema `sam-ui.openstudio-run/1`, status, SHA-256 of the snapshot and weather (B1a `sourceFileHash` notation), the CLI path, exit code, warning/severe/fatal counts, the SAM.Analytical.OpenStudio InformationalVersion, and the conversion and result diagnostics.

The run executes on a worker thread (`Task.Run`). Progress and Cancel use the existing `ProgressWindowHost`. Cancel triggers the token, and SAM_OpenStudio's job object kills the CLI/EnergyPlus process tree. The window stays usable during the run, and the button is disabled until the run ends.

## Decisions

- **Runs inside SAM_UI's process; no separate runner program.**
  - The OpenStudio bindings (SWIG) are used in process only for conversion, saving and reading results. EnergyPlus already runs out of process under a job object.
  - The same library already runs in process in Rhino (SAM_OpenStudio's Grasshopper components) and in the benchmark CLI.
  - The model is frozen to disk before the native code is reached. A native crash would therefore lose no model state beyond what the snapshot already holds; unsaved edits made *after* the snapshot would still be lost with the process.
  - A separate runner remains an option for Phase 2 (#234) if in-process native failures are seen in practice.
- **No platform change in SAM_UI.**
  - The OpenStudio NuGet targets refuse AnyCPU builds, and SAM_UI is AnyCPU throughout.
  - `OpenStudio.dll` is IL-only (CLR flags `0x9`, identical bytes in the package's x86 and x64 folders). So `SAM.Analytical.UI.WPF` references it, and the three `SAM.*.OpenStudio` assemblies, from `..\..\..\SAM_OpenStudio\build\` by HintPath, like every other SAM dependency.
  - The x64 native files are copied as `None` items, so they also reach the applications and the tests: `openstudiolib.dll`, `openstudio_csharp.dll`, `openstudio_model_csharp.dll`, `openstudio_translators_csharp.dll` and `runtimes\win-x64\native\SQLite.Interop.dll`.
  - If any of them is missing, the build fails early with a clear message.
  - At run time an AnyCPU process on x64 Windows is 64-bit, so the x64 natives load.
- **CI**: `build.yml` clones and builds `SAM_SQLite` and `SAM_OpenStudio` after `SAM_Validation`, which SAM_OpenStudio's benchmark needs. SAM_OpenStudio is built with `/p:Platform=x64`, as in its own CI and in SAM_Deploy's BuildAll, which already builds SAM_SQLite and SAM_OpenStudio (x64) before SAM_UI. Every other solution keeps the default platform.
- **Reuse, no duplication.** Conversion, the run, SQL extraction, result mapping and attachment are all SAM_OpenStudio's. No comparison code is added; comparison belongs to SAM_Validation (#231). The manifest's file hashes are provenance only, not `canonicalModelHash`.
- **Result model holds only this run's results**, so a later B1a producer or comparison cannot pick up stale Tas results from it.
- **Namespace traps** caught by the local compile check:
  - inside `SAM.Analytical.*`, a bare `Window` is `SAM.Analytical.Window` (the aperture type), so the WPF window is written `System.Windows.Window`;
  - `Math` may resolve to `SAM.Math`, so the code uses `System.Math`.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/Classes/OpenStudio/OpenStudioSimulationRun.cs` (new): the run record and manifest. It holds no OpenStudio type.
- `WPF/SAM.Analytical.UI.WPF/Modify/RunOpenStudioSimulation.cs` (new):
  - `RunOpenStudioSimulation` (no UI, never throws for a failed run);
  - `FreezeAnalyticalModel`;
  - `AddOpenStudioResults`;
  - the OpenStudio-touching code sits in a `NoInlining` method, so a load failure is recorded on the run.
- `WPF/SAM.Analytical.UI.WPF/Modify/SimulateOpenStudio.cs` (new): the dialogs, the progress/cancel host, the worker thread and the summary message.
- `WPF/SAM.Analytical.UI.WPF/Query/OpenStudioSimulation.cs` (new): run folder and result file names, SHA-256, load-failure detection and message, and the summary text.
- `WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml(.cs)`: the button, its tooltip, the click handler (catches load failures and blocks a second run) and the enable logic.
- `WPF/SAM.Analytical.UI.WPF/SAM.Analytical.UI.WPF.csproj`: the SAM_OpenStudio references, the native file items and the missing-file check.
- `WPF/SAM.Analytical.UI.WPF.Tests/SAM.Analytical.UI.WPF.Tests.csproj`: SAM_OpenStudio references for compilation.
- `WPF/SAM.Analytical.UI.WPF.Tests/OpenStudioSimulationTests.cs` (new): 9 tests.
- `.github/workflows/build.yml`: SAM_SQLite and SAM_OpenStudio (x64) added to the clone and build order.

## Validation

Local results are from a Linux cloud container (no Windows, Tas or OpenStudio CLI), using the .NET SDK 10.0.112 compiler; .NET 8.0.131's compiler cannot build SAM `sow/2026-Q4`, which uses a C# 12 collection expression it rejects. Dependencies were built from `sow/2026-Q4`: SAM `618db819`, SAM_SQLite `ec2eb86d`, SAM_OpenStudio `d4216f35` (`-p:Platform=x64 -p:PostBuildEvent=`).

| Check | Where | Result |
|---|---|---|
| `openstudio-run.json` threw `InvalidOperationException` when a run had any diagnostic: `JsonArray.Add(string)` binds to the generic `Add<T>`, which `ToJson`'s options cannot write. The exception was raised from a `finally` on the worker thread, so a real run with a warning would have failed or crashed SAM_UI. Found by the comparison work (SAM_UI#231). Fixed with `JsonValue.Create`, and the manifest write can no longer throw. Regression test `Manifest_WithDiagnosticsAndAnError_IsWritten`: fails before the fix, passes after (harness 10/10) | Local | FIXED in `3393331` |
| Hosted CI on `f8f10d6` failed: `SimulateOpenStudio.cs(55,89) CS0117 'AnalyticalModelParameter' does not contain a definition for 'WeatherData'`. SAM_UI's own `SAM.Analytical.UI.AnalyticalModelParameter` shadows SAM's enum. Reproduced locally by adding that enum as a stub; fixed by qualifying it as `Analytical.AnalyticalModelParameter`, as `Modify/Simulate.cs` does. The new files were swept for other names shadowed by SAM_UI types: none found | Local | FIXED |
| New production files compiled against the real SAM/SAM_OpenStudio builds (net8.0-windows, nullable enabled, `EnableWindowsTargeting`). Machine-local harness; only `UIAnalyticalModel`, `ProgressWindowHost` and two existing private `Query` helpers stubbed | Local | PASS (0 errors, 0 warnings) |
| `OpenStudioSimulationTests` (9) with the non-WPF production files, net8.0 harness | Local | PASS 9/9. The real-run test returned early (no `openstudio.exe`), so it is **NOT RUN** |
| Failure path for real: no weather, and the OpenStudio natives cannot load on Linux (`TypeInitializationException` from `OpenStudioModelCorePINVOKE`). Run recorded as Failed, model JSON unchanged, snapshot and manifest written | Local | PASS |
| Native-file items copied into a referencing project, including `runtimes/win-x64/native/SQLite.Interop.dll`; missing files give the clear early error | Local, scratch MSBuild projects | PASS |
| Full SAM_UI solution build, including the XAML and `AnalyticalWindow`, and the CI dependency changes | GitHub CI (Windows) | See the PR description |
| Full WPF test suite on Windows (`dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c Release`) | Windows | NOT RUN |
| **G1**: real OpenStudio run from the SAM_UI app | Windows + OpenStudio 3.10 | NOT RUN |
| G1: results identical to `benchmark-openstudio` for the same model and EPW | Windows + OpenStudio 3.10 | NOT RUN |
| G1: Tas not regressed (same model, Energy Simulation before and after) | Licensed Tas | NOT RUN |
| G1: Cancel kills `openstudio.exe`/`energyplus.exe` and saves no result model | Windows, manual | NOT RUN |

Commands, all machine-local; the harness projects are not committed:

```bash
# deps (Linux; SAM_OpenStudio post-build is a Windows copy command)
dotnet build SAM/SAM/SAM.Analytical/SAM.Analytical.csproj -c Release -p:EnableWindowsTargeting=true
dotnet build SAM_SQLite/SAM_SQLite/SAM.Core.SQLite/SAM.Core.SQLite.csproj -c Release
dotnet build SAM_OpenStudio/SAM_OpenStudio/SAM.Analytical.OpenStudio/SAM.Analytical.OpenStudio.csproj -c Release -p:Platform=x64 -p:PostBuildEvent=
# harness: compiles the new files plus OpenStudioSimulationTests.cs against those builds
dotnet test <harness>/Tests.csproj -c Release
```

On Windows, the real run test is `dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c Release --filter "FullyQualifiedName~OpenStudioSimulationTests"`. It needs OpenStudio 3.10 (`openstudio.exe` on PATH or in a standard install) and SAM_OpenStudio checked out beside SAM_UI. `SAM_OPENSTUDIO_UI_TEST_MODEL` and `SAM_OPENSTUDIO_UI_TEST_EPW` override the model and the EPW.

## Not claimed

- No engineering equivalence with Tas.
- No claim that the OpenStudio results are correct beyond SAM_OpenStudio's own validation.
- Comparison is #231; benchmark acceptance is #232/#233.

## Unresolved / risks

- SAM_Deploy installer contents were not checked. The deploy build order already builds SAM_SQLite and SAM_OpenStudio (x64) before SAM_UI, and SAM_OpenStudio's Grasshopper post-build copies its build folder, natives included, to `%APPDATA%\SAM`. Confirm on an installed machine that the button loads.
- OpenStudio CLI (3.10) must be installed by the user. A missing CLI is reported through SAM_OpenStudio's `RunCliFailed` diagnostic.
- The CI build-artifact upload grows by about 150 MB of OpenStudio natives.
- The SWIG bindings run in process. See Decisions.

## Historical gated acceptance reconciliation - 2026-10-17

Status: HOLD for G1 acceptance. This documentation-only correction does not authorize a merge or waive a required check. The owner-authorized merge candidate was `95135ceb0ac6efb877f3c17735659084bdbc2aa3`; any record correction creates a new candidate requiring a new owner-approved pin. Product content remains `3393331`.

The original NOT RUN entries above describe the Linux investigation. Later Windows evidence on #238 (build `0e1980a9d5e302da22fc32db488a5ccefd81d037`, incorporating #237 `95135ce`) supports a real standalone run and Both integration; it does not establish every G1 requirement. #238's live description records owner acceptance for integration, with numerical equivalence left open. No explicit owner deferral of the remaining G1 checks was found in the inspected records, PR comments or current merge instruction.

Machine-local evidence (must be copied/provisioned on another machine):

- Report: `C:\TasOut\pr238-ui\PR238-BOTH-REPORT.md`.
- Standalone: `C:\TasOut\pr238-ui\input-box\box-aperture_OpenStudio_20261017-191359_3beea496`. `openstudio-run.json`: Succeeded, exit 0, 10 warnings, 0 severe, 0 fatal, 13 results, embedded weather, OpenStudio 3.10. The result model, snapshot, EPW, OSM/OSW and SQL are present.
- Successful Both: `C:\TasOut\pr238-ui\box-both\000000_SAM_AnalyticalModel_TasOpenStudio_20261017-191647_1eb45853`. Both producer exits 0; Compared; provenance Pass. Overall/Numerical Fail, Coverage Warn; reports retain 6 Match / 0 Warn / 10 Fail / 2 N/A. This proves orchestration, not engine equivalence.
- Partial: `C:\TasOut\pr238-ui\both-3\000000_SAM_AnalyticalModel_TasOpenStudio_20261017-185627_e65f7d4c`. Tas exit 5, OpenStudio exit 0; PartiallyCompleted, no comparison reports; failed Tas document is not substituted. The report records the UI banner.

Read-only audit during this merge session (no new simulation): compared the standalone `box-aperture_OpenStudio.json` AnalyticalModelSimulationResult parameters with `box-both`'s `benchmark-OpenStudio.json` model fields. All four stored values are exactly equal: heating 2810.0182949034006 kWh; cooling 3832.503031616723 kWh; peak heating 3.343728936987215 kW; peak cooling 5.206164980706354 kW. Model GUID is `1988f7b93ec047d0b87bf9cb345d9eab`; both weather files hash to `E1353BD2600A401F76262D52A8E16176A15DC45A4967E58D9107C374704ABF7C`. Equality is evidenced for this box scenario, not for the prescribed two-cell explicit-EPW G1 procedure. Snapshot file hashes differ; no claim of byte-identical snapshots is made.

| G1 requirement | Current evidence / decision |
|---|---|
| Standalone OpenStudio real run | Evidenced on box, as above; original full fixture/UI step checklist not supplied |
| Standalone vs benchmark equality | Four model metrics exactly equal for the saved box; prescribed explicit-EPW fixture not run/evidenced |
| Cancel and process cleanup | NOT EVIDENCED for a real run; fake-producer tests are not native-process acceptance; no explicit owner deferral |
| Concurrent-edit isolation | NOT EVIDENCED for a real UI run; frozen-copy tests do not demonstrate concurrent UI editing; no explicit owner deferral |
| Tas baseline comparison | NOT EVIDENCED: no same-model before/after Energy Simulation comparison against Q4; successful benchmark-tas is a different workflow; no explicit owner deferral |
| Full WPF suite | Existing #238 record reports 2835/2835; not rerun in this merge session |
| Opt-in RealRun actually executed | NOT established by 2835/2835: `OpenStudioSimulationTests.RealRun_...` returns early with NOT RUN when CLI or model is missing |

Exact next step: owner supplies missing G1 evidence or explicitly accepts each deferral (including whether the box equality substitutes for the prescribed fixture), reviews this correction and approves its new exact head. Refresh final-head CI against merged SAM_Validation Q4, check dependency logs, and recheck all merge gates. #238 remains dependent on #237. Do not merge either UI PR under the original pins after changing its record. Numerical disagreement, HungaryHouse Tas crash, unavailable per-engine input manifests and producer deployment remain separate unresolved follow-ups.

## Windows G1 acceptance execution - 2026-10-18

**Current status: HOLD on strict explicit-EPW equality.** The owner instruction of 2026-10-18 authorizes focused acceptance work and later merge of documentation-only evidence heads once all gates pass; another approval of such pins is not required. It does not waive the failed equality requirement. No product code, engine physics, units, tolerances, comparison arithmetic or deployment scope changed. The earlier dated HOLD and pin-approval wording above is historical and superseded by this section.

Freshly tested UI content: `b7483443f53dd53a4d16f159d6bc327c901a7770` (#237). Pre-PR UI Q4 baseline: `ebe5e09b30b95e43e5d047b3aef2d8c5f01110e9`. Both Release builds passed; candidate and baseline run from isolated copied build folders. All 14 dependency source pins are in `documentation/evidence/g1-20261018/candidate-dependency-pins.csv`; these dependencies were unchanged for both builds. Eight relevant engine/core/interop DLLs are SHA-256 identical across builds (`dependency-binary-hashes.csv`); UI DLL hashes are in `ui-build-hashes.csv`. SAM `618db819300df8b698b3bbe5174a203244134738`, SAM_OpenStudio `d4216f3525c01ca762c1b3e9b6d099733d037f54`, SAM_Tas `a34a298dde2803de514081a0ed72ac3ff790997b`, SAM_Validation Q4 `68eded0060047af714853a02a152fb9c960bfeb1` (product merge `912ae94e`). Runtime: OpenStudio 3.10 / EnergyPlus 25.1, Tas 9.5.7, Windows x64. These local builds use default informational version 1.0.0; `SAM_COMMIT=618db819300df8b698b3bbe5174a203244134738` was set for benchmark provenance.

| Required check | Newly executed evidence and result |
|---|---|
| Real Cancel/process cleanup | **PASS.** Cancel pressed during the observed Running OpenStudio/EnergyPlus stage while run-owned CLI PID 49008 and EnergyPlus child PID 45448 were alive. Both disappeared immediately around the click; manifest `Cancelled`, engine exit -1, resultModel null, no completed result model. No external kill was used. `cancellation-assertions.json` and screenshots. A first attempt clicked after native completion (exit 0) and is retained locally but excluded from this pass. |
| Concurrent-edit isolation | **PASS.** A real HungaryHouse OpenStudio run froze its snapshot; the ordinary Rename Spaces UI then committed `GF_07 LivingRoom` -> `G1_ISOLATION_EDIT` at 08:56:09.092Z, before run completion at 08:56:14.164Z. Snapshot and result retain the old name for space `c7df54b3c0064fc5a7c6e6012dbb9b8e`; the saved disposable open model retains the edit. Snapshot SHA-256 unchanged. `isolation-assertions.json`, screenshot. |
| Tas before/after Q4 baseline | **PASS for the exercised box case.** Existing Energy Simulation button used on the same original box model, internal weather, TAS solar/gbXML route, Simulate + Sizing + Full Year 1-365 + Unmet Hours + Update Construction Layers enabled; other options off. Settings screenshots committed. Both workflows completed, wrote TBD/TSD/result JSON, and adopted results. **608/608 persisted numerical values match exactly**, aligned by result type/name/load type and surface reference; matching 2 space, 28 surface and 2 adjacency-cluster results. Criterion is exact equality, no numerical tolerance, because platform, inputs, settings, dependencies and engine binaries are held fixed. Run-generated GUIDs/timestamps are metadata, excluded from numerical comparison. This does not claim byte-identical TSDs or audit every hourly native record. `tas-comparison.json`. `Modify/Simulate.cs` is identical at the two UI pins. |
| Prescribed two-cell explicit EPW | **Executed; strict equality FAIL/HOLD.** Untouched copy of `rawModel-WindowsWeatherDataDDYAdiabaticAirPanel.sam`, prescribed Boston EPW; UI explicitly chose No to embedded weather and selected the file. Manifest Succeeded, Weather=File, exit 0, 13 warnings, zero severe/fatal, 31 OpenStudio results. Benchmark uses the same source model/EPW and default conversion settings and succeeds. Weather SHA-256 and model GUID match; fixture preserved. Four model metrics differ by tiny nonzero amounts; values below. |
| Opt-in real-run test | **PASS, actually executed (1/1).** Explicit valid CLI/model/EPW supplied; detailed output contains Succeeded, no NOT RUN, TRX has exactly one Passed test. No broad suite rerun. |

Explicit-EPW values, in stored units (standalone / benchmark / difference):

- Heating kWh: `31072.86245449682` / `31072.862454810253` / `-3.134337021037936e-7`.
- Cooling kWh: `5764.530483239447` / `5764.530483144583` / `9.486393537372351e-8`.
- Peak heating kW: `19.82144246485972` / `19.821442464872938` / `-1.3216094885137863e-11`.
- Peak cooling kW: `14.079292685507252` / `14.07929268578085` / `-2.735980331181054e-10`.

Investigation: an independent second **CLI-only** run on the identical source/EPW also varies (heating difference `-6.598711479455233e-7 kWh` versus CLI run 1). All three generated IDFs have the same 163 objects and identical engineering fields after matching regenerated aperture/frame names by their complete physical content and sorting object order: normalized SHA-256 `3b0dbe8f96a18c8b99b311811b2cccdc15c7b34046a549a0981a385cdf0da397`. The audit does not round or omit physical fields. Raw IDs/order and raw IDF hashes differ; expected source weather hash `sha256:eded5c982c1da37e35930622badf7f0e0a0a82b30fd9410723ea3f1c4b52b80b`, source model GUID `b74990da73b240978846cc12560be0aa`, design-day source EmbeddedModel. Evidence supports existing conversion/name-order reproducibility, not a newly established UI mapping defect. It does **not** satisfy the stated bit-exact equality, and no tolerance/acceptance deferral was silently introduced. See `equality-observed.json`, `benchmark-repeat-deltas.json`, `idf-semantic-audit.json`, `provenance-audit.json`.

Reproduce on this machine (PowerShell, quoted paths):

```powershell
$env:PATH='C:\Users\michal\Documents\GitHub\SAM-BIM\.icon-migration\runtime\OpenStudio-3.10.0+86d7e215a1-Windows\bin;'+$env:PATH
$env:SAM_COMMIT='618db819300df8b698b3bbe5174a203244134738'
$env:SAM_OPENSTUDIO_UI_TEST_MODEL='C:\TasOut\pr237-g1-20261018\inputs\two-cell.sam'
$env:SAM_OPENSTUDIO_UI_TEST_EPW='C:\TasOut\pr237-g1-20261018\inputs\Boston.epw'
dotnet test WPF/SAM.Analytical.UI.WPF.Tests/SAM.Analytical.UI.WPF.Tests.csproj -c Release --filter 'FullyQualifiedName~RealRun_SimulatesAFrozenCopy_AndSavesAResultModelWithOnlyOpenStudioResults' --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=real-run.trx' --results-directory C:\TasOut\pr237-g1-20261018\opt-in
& '..\SAM_OpenStudio\benchmark\SAM.Analytical.OpenStudio.Benchmark\bin\x64\Release\net8.0\benchmark-openstudio.exe' --model $env:SAM_OPENSTUDIO_UI_TEST_MODEL --weather $env:SAM_OPENSTUDIO_UI_TEST_EPW --out C:\TasOut\pr237-g1-20261018\explicit\benchmark-openstudio.json --work C:\TasOut\pr237-g1-20261018\explicit\benchmark-work
```

The existing paths above are preserved evidence: use fresh output paths for a repeat. Release builds used VS 2026 MSBuild 18.10.1: `MSBuild.exe SAM_UI.sln /t:Restore;Rebuild /m:1 /nr:false /v:m /p:Configuration=Release /p:UseSharedCompilation=false` (quote `/t:Restore;Rebuild` in PowerShell).

Machine-local evidence root: `C:\TasOut\pr237-g1-20261018` (must be copied/provisioned elsewhere). `cancel/...084042_454bf00b`; `isolation/...085502_021b72af`; `explicit/...084639_45a832b9`; `explicit/benchmark-work`, `explicit/benchmark-repeat-work`; `tas-baseline`, `tas-candidate`; `opt-in/real-run.trx`; logs/builds/process CSV/audit scripts. Portable summaries, verifier scripts and selected screenshots are committed under `documentation/evidence/g1-20261018/`; the verifiers use this evidence root and require the preserved full run artifacts. Originals remain untouched; `inputs/HungaryHouse.json` is the intentionally edited disposable live model. The original `C:\TasOut\pr238-ui\inputs\HungaryHouse.json` still hashes to `E16CCBD2BFE5ABA710D6C2EE88C68019A3265A75C41D50483E7867018E28B2CD`. The two-cell original/copy and EPW hashes are recorded in `input-hashes.csv`. Existing successful Both/partial runs, 2835/2835 suite and presentation evidence were inspected, not rerun.

Exact next step: resolve the strict equality specification with the owner/domain reviewer, or prepare a separately reviewed deterministic conversion fix if bit identity is required. Do not merge #237 or dependent #238 while this required gate fails. Do not change physics, comparison tolerances/arithmetic or deployment to satisfy it. Documentation-only final heads are authorized by the 2026-10-18 instruction once all gates pass; the current hold is **acceptance**, not a request to reapprove documentation pins. Numerical Tas/OpenStudio disagreement, HungaryHouse Tas crash, unavailable input manifests and producer deployment remain separate open follow-ups. No PROJECT_PROGRESS.md change or UI branch cleanup is permitted before merge.
