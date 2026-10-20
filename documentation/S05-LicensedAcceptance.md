# S05 - Licensed Phase A GUI acceptance - acceptance record

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S05 (Phase A). SAM_UI record only: no product code changed.
Preflight (rehearsal, not acceptance): `documentation/S05-LicensedAcceptance-Preflight.md` (SAM_UI#244).

Status values: **PASS**, **FAIL**, **NOT RUN**, **BLOCKED**. Numerical agreement between Tas and EnergyPlus is not a
criterion (roadmap); engine numbers are recorded, not judged.

## Status
**All nine cases PASS** on 2026-10-20, run from the real SAM Analytical ribbon on this licensed laptop, on a fresh build of
the merged S01-S04 content. Branch `docs/s05-acceptance`, base `sow/2026-Q4` `ad1f2486`. Record and evidence only.

## Content under test
- **SAM_UI** `sow/2026-Q4` `ad1f2486`: S01 (SAM_Tas#103 `e483877c`, SAM_UI#240 `5b379191`), S02 (#241 `49eebe13`),
  S03 (#242 `1e460bbe`), S04 (#243 `307cb0e3`) merged, with green post-merge Q4 builds (`1e460bbe`, `307cb0e3`).
- **Dependencies** at their `sow/2026-Q4` tips, clean (`build-revisions.txt`): SAM `62b54156`, SAM_Tas `e84aeff6`,
  SAM_OpenStudio `d4216f3`, SAM_Validation `68eded0`, and the other 22 BuildAll repositories.
- **Why a full rebuild was needed (finding):** before this session the laptop's `SAM\build` dated from 2026-10-17, before
  SAM#212 (F1, EPW writer). Every overnight real-engine run (S03, S04 evidence and the S05 preflight) therefore used a pre-F1
  `SAM.Weather.dll`. This record's runs use a fresh build; see case 4 for the proof that F1 is in effect.

## Build (machine-local, reproducible)
1. Dependency stack: `BuildAlls_v5.bat skip2027 nopause` (Debug, Restore/Clean/Rebuild of 26 repositories, 3 min 35 s, exit 0,
   0 errors), with `APPDATA=C:\TasOut\s05\sandbox\AppData`, `USERPROFILE=C:\TasOut\s05\sandbox\Profile`,
   `NUGET_PACKAGES` the real cache. The sandbox `AppData\SAM` was first seeded with `SAM_OCCT\build` (BuildAll's OCCT runtime
   check otherwise tries a native rebuild, which failed in the sandbox; first attempt logged, nothing changed).
2. App: `MSBuild "Application\SAM Analytical\SAM Analytical.csproj" -restore -t:Rebuild -p:Configuration=Release
   -p:OutDir=C:\TasOut\s05\app\` with the same environment: exit 0, 480 files.
3. Checks (`build-hashes.txt`): `SAM.Weather.dll` (`5e6a1a7d…`, contains the F1 code), `SAM.Analytical.dll` and `SAM.Core.dll`
   are byte-identical in `SAM\build`, `SAM_Tas\build`, the OpenStudio producer folder and the app; the app's
   `SAM.Analytical.Tas.dll` equals `SAM_Tas\build`'s; `SAM.Analytical.Benchmark.dll` is identical in all four places.
4. Side effects removed: untracked `NuGet\` (and SAM_UI `Microsoft\`) folders that MSBuild created in the repository roots under
   the sandboxed profile; every file in them was newer than the build start. No tracked file changed in any repository.
   Installed SAM and the real `%APPDATA%\SAM` were not touched.

5. Unit tests on the same content with the rebuilt (post-F1) dependencies: `dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c
   Release` **2926/2926 PASS** (4 min 19 s; machine-local log `C:\TasOut\s05\logs\fulltest-q4-fresh-deps.txt`).

## Environment
- TAS 9.5.7.0 (licensed; `TBD.exe` sha256 `aa658fcd…`).
- OpenStudio 3.10.0 (`.icon-migration\runtime\OpenStudio-3.10.0+86d7e215a1-Windows\bin` first on `PATH`; CLI sha256
  `46a80a3d…`), EnergyPlus 25.1.0-1c11a3d85f. The CLI on `PATH` is set by the harness, as in S01b and the preflight; without it
  this laptop finds ladybug's OpenStudio 3.11 / E+ 25.2 (S03 finding, for S06).
- Producers (developer builds from the rebuild): `SAM_Tas\build\benchmark-tas.exe` (`07ab3e45…`) and
  `SAM_OpenStudio\benchmark\…\bin\x64\Debug\net8.0\benchmark-openstudio.exe` (`64405919…`).
- **`SAM_COMMIT`** (owner decision 2026-10-20: a temporary requirement for developer-built producers until S06): set only for
  the Both routes (cases 3-6, 8a, 9), to `62b5415697d1a5af143c6b581b7d42fbd6551ca8`. It is truthful: SAM's checkout was clean at
  that commit (the `sow/2026-Q4` tip) when it was built, and the producers' `SAM.Analytical.dll` is byte-identical to that
  build. Every Both manifest records it as a manual override (`environment.SAM_COMMIT`). It identifies a developer build; it
  does not prove packaged-app provenance (S06). Tas-only and OpenStudio-only runs had no `SAM_COMMIT` set.
- Each case: a fresh folder `C:\TasOut\s05\runs\<case>` with a copy of the model; the run folder is created beside it. The
  app ran with the sandboxed `APPDATA`/`USERPROFILE`.

## Fixtures
- Box: `box-aperture.json` sha256 `99ba2ab9…` (the S01b/#239 box; machine-local, `C:\TasOut\s05\inputs`). Unchanged after
  every case.
- Explicit weather: `Budapest.epw` sha256 `472ffb4e…` (machine-local, as S01b).
- **Multizone (case 6):** SAM_OpenStudio's committed fixture
  `tests/resources/models/rawModel-WindowsWeatherDataDDYAdiabaticAirPanel.sam` (sha256 `b2ab6826…`, matching its
  `SHA256SUMS.txt`): 2 spaces, 11 panels, an Air partition, 10 apertures, embedded London weather; provenance documented in
  that folder's README. It was chosen because HungaryHouse's provenance is not cleared (roadmap owner decision 3). It is used
  read-only; nothing new is committed as a fixture. Unchanged after both sessions.

## Drivers
- Unchanged S01b drivers (`documentation/evidence/s01b-20261019/tools/`): `drive-tas.ps1` (cases 1, 7), `drive-both.ps1`
  (cases 3-6), `compare-persisted.ps1` (case 1).
- New for S05 (test harness, `documentation/evidence/s05-20261020/tools/`): `drive-s05.ps1` with `s05-uia.ps1` (cases 2, 8, 9),
  `run-s05-case.ps1` (environment and fresh folder per case), `summarise-both.ps1`.

## Acceptance matrix
| # | Case | Result | Observed (what the UI reported = what the engines did) | Evidence (`documentation/evidence/s05-20261020/cases/`) |
|---|---|---|---|---|
| 1 | Tas only: Run Simulation > Tas, box, embedded weather, Full Year | **PASS** | "Model successfuly converted", 21 s. `box.tas-run.json` `Succeeded`; TAS 9.5.7.0 + `TBD.exe` hash; weather `Embedded` (content hash `7666c3a7…`); design days `DerivedFromWeather`; the workflow settings that ran. Persisted results **602/602 exact** against the S01b baseline S1 (2 space + 28 surface results). Model file unchanged. | `c1-tas-run-simulation/` (`cmp-S1-vs-c1.json`) |
| 2 | OpenStudio only: Run Simulation > OpenStudio, box, explicit EPW | **PASS** | Summary "finished. The open model was not changed", 12 s. `openstudio-run.json` `Succeeded`, E+ exit 0, 10 warnings, 0 severe/fatal, 13 results; CLI 3.10.0 hash, SDK 3.10.0, E+ 25.1.0 from `eplusout.err`; weather `File` `472ffb4e…`. Snapshot `0c30db14…` has no view state; the result model has it (S04). Its four stored model metrics equal case 3's OpenStudio values exactly. | `c2-openstudio-explicit/` |
| 3 | Both, explicit EPW, two app sessions | **PASS** | Both sessions `Compared` (28 s, 29 s). Snapshot `0c30db14…` and both producers' `canonicalModelHash` `05c1e334…` in both sessions (S04). Producers report `samCommit` `62b54156…`, runner commits `e84aeff6` (SAM_Tas) / `d4216f3` (SAM_OpenStudio), TAS 9.5.7.0, E+ 25.1.0. `comparison.csv` identical (LF-normalised) in both sessions, to the preflight and to the S01b run B2: unchanged outputs. | `c3a-…`, `c3b-…`, `both-summary.txt` |
| 4 | Both, box, embedded weather (post-F1) | **PASS** | `Compared`; weather `Embedded`; the exported EPW is `5afbeb32…`, **byte-identical to SAM#212's `embedded-corrected.epw`**. The preflight's case 4 exported `e1353bd2…` = SAM#212's `embedded-before.epw` (pre-F1). Same snapshot and canonical hash as case 3. Engine numbers differ from the pre-F1 run, as F1 intends. | `c4-both-embedded/` |
| 5 | Both, box, explicit EPW | **PASS** | The case 3 runs: weather `File`, `472ffb4e…`, the same hash reported by both producers. | `c3a-…`, `c3b-…` |
| 6 | Both, multizone model, explicit EPW, two sessions | **PASS** | Both sessions `Compared` (33 s, 34 s); 2 spaces matched by GUID; coverage Pass; 28/28 metrics comparable. Snapshot `a3b8cff8…` and canonical hash `356dcbed…` identical across sessions. Tas values identical; OpenStudio values differ between sessions by ~1e-11 relative (EnergyPlus run-to-run variation on this air-boundary model, already recorded for it in `OpenStudio-Simulation-PR229.md`; identical inputs). | `c6a-…`, `c6b-…` |
| 7 | Injected Tas refusal: Energy Simulation with a directory at `box.tsd` | **PASS** | One "Simulation failed" box: TAS return code 0, error log `box_error_log.txt` ("Error accessing the TSD file for writing"), TAS-SIM-001 lines, "The open model was not changed". `box.tas-run.json` `Failed` with the same text. Harness: see below. | `c7-tas-refusal-energy-simulation/` |
| 8 | Cancel: (a) Both while the Tas producer runs; (b) OpenStudio while EnergyPlus runs | **PASS** | (a) Cancel 4 s into `benchmark-tas.exe`: run `Cancelled`, banner "CANCELLED - nothing to show or compare", no comparison, OpenStudio producer never started, no B1a (recorded unknown with its reason); no engine process left. (b) Cancel while `openstudio.exe` and its `energyplus.exe` ran: "The OpenStudio simulation was cancelled. No result model was saved", SAM-OS-RUN-001 "process tree was terminated", `openstudio-run.json` `Cancelled`, no result model; no engine process left. Both manifests carry provenance. | `c8a-cancel-both/`, `c8b-cancel-openstudio/` (`processes.txt`) |
| 9 | Concurrent start | **PASS** | While Both ran (its progress window open, `benchmark-tas.exe` running): Energy Simulation, Run Simulation and OpenStudio Simulation each refused with "A Tas + OpenStudio run is already running for this model, so nothing was started." While an OpenStudio Simulation ran (comparison window open): the comparison window's Run and Energy Simulation each refused with "An OpenStudio simulation is already running…". Both first runs completed (`Compared`, `Succeeded`); the folder holds exactly those two run folders, nothing from a refused start. | `c9-concurrent-start-run2/` (`concurrency.txt`, `refusals.txt`) |

Every case's model file kept its hash. GitHub CI for this PR is reported in the PR description, separately from these local
licensed runs.

## Test-harness defects (classified; product behaviour unaffected)
- **Case 2, attempts 1-2** (kept in `cases/c2-attempt*`): the folder dialog's "Select Folder" mouse click was lost; then the
  EPW file dialog showed "C:\TasOut\s05\sandbox\Profile\Desktop is unavailable", because the sandboxed profile had no
  Desktop folder. Fixed in the harness: BM_CLICK on Win32 buttons, the profile's standard folders created, and the EPW path set
  with WM_SETTEXT instead of SendKeys. No product run had started.
- **Case 7:** the S01b driver's BM_CLICK on "OK" was ignored because TAS had put Notepad (its error log) in the foreground,
  so the box stayed open (as S01b E3 and the preflight). WM_ACTIVATE then BM_CLICK closed it, and no second box appeared. One
  message box; recorded in `driver.log`.
- **Case 8:** the log line "… never appeared; cancelling anyway" is wrong: a CIM result was not counted. The process snapshot
  taken immediately before Cancel (`processes.txt`) shows the engines running. The helper now returns a boolean.
- **Case 9, run 1** (kept in `cases/c9-concurrent-start`): the comparison-window refusal box is titled "Compare Tas /
  OpenStudio" and owned by the comparison window, two levels below the main window; the driver looked for "Simulation already
  running" one level down and logged "NOT REFUSED". Run 2, with the detection fixed, shows the refusal (screenshot).

## Scope notes
- The Optimisation window's "Run Energy Simulation…" follow-up was not driven: it calls the same `SimulateTas` guard path as
  Energy Simulation, which case 9 refused; S02's `SimulationConcurrencyGuardTests` cover it.
- The Part O Hub simulations are not guarded (S02 owner decision outstanding) and are not part of this matrix.
- Not observed in these runs: the `ProgressWindowHost` 5 s shutdown fail-safe that reported one S03 unit test Cancelled under
  full-suite load (recorded in the S04 closeout).

## Unresolved, risks
- Both with developer-built producers still requires `SAM_COMMIT`; S06's stamped producers remove it.
- OpenStudio CLI/SDK selection depends on `PATH` on this laptop (S06).
- The app reports `application.sourceRevision: unknown` (developer build); component hashes identify it. An installed candidate
  is S06's scope.
- Machine-local evidence: `C:\TasOut\s05` (app, sandbox, run folders with TBD/TSD/OSM/SQL); TAS licence; OpenStudio 3.10
  runtime under `.icon-migration\runtime`; the S01b baseline `C:\TasOut\s01b\runs\S1-base-es-success`.

## Reproduce
From `C:\TasOut\s05` after the build above: `tools\run-s05-case.ps1 -Name <case> -Kind
TasRunSimulation|TasEnergySimulationRefusal|OpenStudio|Both|CancelBoth|CancelOpenStudio|Concurrent [-Model <file>]
[-EpwPath <epw>]`; `tools\summarise-both.ps1 -Cases …`; `compare-persisted.ps1 -Baseline … -Candidate … -Out …`.

## Next step
Merge this record (owner authorisation 2026-10-20), close out S05 in `PROJECT_PROGRESS.md`, and update the roadmap status
(S01-S05 Done) through a reviewed SAM PR. S06 (early SAM_Deploy smoke candidate) is the next roadmap task and needs its own
owner start.
