# Simulate > OpenStudio Simulation - minimal OpenStudio/EnergyPlus run from SAM_UI (SAM_UI#229)

Status: implemented on `claude/openstudio-mcp-server-1jmjlo`; owner review and gate G1 manual validation required. Not merged.
Issue: SAM-BIM/SAM_UI#229 (parent SAM-BIM/SAM_UI#227, tracker SAM-BIM/SAM#206).
Base: `sow/2026-Q4` at `ebe5e09b30b95e43e5d047b3aef2d8c5f01110e9`.
PR: the live PR for this branch in `SAM-BIM/SAM_UI` (its number is not committed here).
Tested content SHA: `b7e34cd0dd8de31e1e63eeb88e9340d18fc2038d` for the code and tests. Then `8c1c2a7130b435c6fb8bf47a074c91d09177c1b7` fixed only a `build.yml` comment line that the first push had split: pwsh rejected the script before any build ran. This record is the only change after that.
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

## Next step

1. Check the hosted CI build on the PR.
2. On the owner's Windows machine with OpenStudio 3.10 and a Tas licence, run the G1 manual steps from the PR description.
3. Owner review.
4. Merge, then the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
