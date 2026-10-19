# S05 - Licensed Phase A GUI acceptance - PREFLIGHT (not acceptance)

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S05.

**This is a preflight and rehearsal, not the S05 acceptance record.** Formal S05 acceptance needs S01-S04 merged with green
final-head CI, then every case run from the real UI on that merged content. When this was written, S03 (SAM_UI#242) and
S04 (SAM_UI#243) were open and unmerged. **No case below is an S05 PASS.**

Status values: **PASS**, **FAIL**, **NOT RUN**, **BLOCKED**. Rehearsal results are labelled "rehearsal" and never count as
acceptance results.

## Rehearsal build (2026-10-19)
- **Tree:** local integration merge `8aafcb06` (not pushed): `sow/2026-Q4` `4d7f5bd8` + S04 head `b19b80ab` + S03 head
  `2724f0c5`. The one textual conflict, both PRs appending to `OpenStudioSimulationTests.RealRun_…`, was resolved by keeping
  both blocks.
- **Full `SAM.Analytical.UI.WPF.Tests` on that tree:** **2926/2926 PASS** (2860 + 52 S03 + 14 S04).
- **App:** `Application\SAM Analytical\SAM Analytical.csproj` built with MSBuild (VS 18) `-restore -t:Build -p:Configuration=Release`
  into `C:\TasOut\s05pre\app-int\`. `APPDATA`/`USERPROFILE` were redirected to `C:\TasOut\s05pre\sandbox`, so the installed
  SAM and `%APPDATA%\SAM` were not touched. Exit 0, 495 files. Machine-local.
- **TAS:** 9.5.7 (licensed). Its `TBD.Document` COM server is `C:\Program Files\Environmental Design Solutions Ltd\Tas\TBD.exe`.
- **Producers:** developer builds `SAM_Tas\build\benchmark-tas.exe` and `SAM_OpenStudio\benchmark\...\benchmark-openstudio.exe`.
  They ran with `SAM_COMMIT` set by hand (S03 finding: without it they refuse) and with OpenStudio 3.10.0 `bin` first on
  `PATH`.
- **Fixtures** (machine-local, `C:\TasOut\s01b\inputs`): `box-aperture.json` (sha256 `99ba2ab9…`) and `Budapest.epw` (`472ffb4e…`).
- **Drivers:** the S01b UI Automation drivers in `documentation/evidence/s01b-20261019/tools/` were used unchanged.
  `drive-tas.ps1` covers Energy Simulation and Run Simulation > Tas, with `-BlockTsd` refusal injection. `drive-both.ps1`
  covers Run Simulation > Both.
- **Results unchanged by S03/S04:** case 3's `comparison.csv` has the same content as the committed pre-S03/S04 S01b run B2
  (`documentation/evidence/s01b-20261019/cases/B2-cand-both-success/`, same model and EPW). Only line endings differ, from
  the Git checkout. Both engines' B1a model metrics are bit-identical.
- **Evidence:** `documentation/evidence/s05-preflight-20261019/`, which holds each case's driver log, message-box text or
  diagnostics, and manifest, plus three screenshots.

## Acceptance matrix

Every case runs `SAM Analytical.exe` through the ribbon, on the licensed laptop, built isolated from the merged content.
Engine numbers are recorded, not judged: numerical agreement is not a criterion.

| # | Case | Steps | Observable status (must match what the engines did) | Expected artifacts | Failure message | Proof | Rehearsal result (integration build, real ribbon) |
|---|---|---|---|---|---|---|---|
| 1 | Tas only | Run Simulation > Tas on the box; dialog defaults (model weather), Full Year ticked | success box; model replaced by the TAS result | `box.tbd/.tsd/.json`; **`box.tas-run.json`** `status: Succeeded`, TAS `9.5.7.0` + TBD.exe hash, weather source, the `WorkflowSettings` that ran | none | driver log, message box, `tas-run.json`; for acceptance also persisted values vs baseline (`compare-persisted.ps1`) | **Rehearsal PASS.** Message "Model successfuly converted"; `status: Succeeded`; weather `Embedded`; `T3DRoute: GbXML`, `SimulateTo: 365` (`c1-tas-run-simulation/`) |
| 2 | OpenStudio only | Run Simulation > OpenStudio on the box, explicit EPW | Succeeded summary; open model unchanged | run folder: `openstudio-run.json` `Succeeded`, OSM/OSW/SQL, result model with view state; CLI hash, E+ version from `eplusout.err` | none | manifest, result model, screenshot | **NOT RUN** from the ribbon: no driver. Code path ran with real OpenStudio (S03 evidence `openstudio-*`; S04 `RealRun_…`). |
| 3 | Both | Run Simulation > Both on the box, explicit EPW, **in two separate app sessions** | `Compared`; comparison window opens | `engine-comparison-run.json`, two B1a, comparison csv/md/summary; S03 provenance per producer; S04 identical snapshot and canonical hash across sessions | none | manifests, B1a, comparison window | **Rehearsal PASS** with manual `SAM_COMMIT`. Both sessions `Compared`, snapshot `0c30db14…`, both producers `canonicalModelHash` `05c1e334…` (`c3a-…`, `c3b-…`). Gate "Fail" is engine agreement, not a criterion. **Without `SAM_COMMIT`: FAIL** (developer producers refuse, S03 evidence) - owner decision / S06 |
| 4 | Box, embedded post-F1 weather | as 3 with no EPW chosen | as 3; weather `Embedded` | as 3; `provenance.weather.source: Embedded`, exported EPW hash | none | as 3 | **Rehearsal PASS** with manual `SAM_COMMIT`: `Compared`, weather `Embedded`, same snapshot and canonical hash as 3 (`c4-both-embedded/`). Tas-only embedded is case 1. |
| 5 | Box, explicit EPW | as 3 with `Budapest.epw` | as 3 | as 3; `weather.source: File`, EPW hash `472ffb4e…` | none | as 3 | **Rehearsal PASS** (same runs as 3). |
| 6 | One multizone model | as 3 on a multizone fixture | as 3 | as 3 | none | as 3 | **BLOCKED**: no committed multizone fixture with cleared provenance. HungaryHouse is machine-local and unconfirmed (owner decision 3). |
| 7 | Injected Tas refusal | Energy Simulation with `-BlockTsd` (a directory at `box.tsd`) | "Simulation failed" box: TAS return code 0, error log, TAS-SIM-001; model not replaced | `box.tas-run.json` `status: Failed` with the same failure text | "Simulation failed: the TAS simulation did not complete ..." | driver log, message box, `tas-run.json` | **Rehearsal PASS.** Box title "Simulation failed", return code 0, TAS-SIM-001 lines; `tas-run.json` `Failed` with the same text (`c7-…`). The driver's close step timed out with the app open (as in S01b case E3); the app was closed by hand. TAS opened its error log in Notepad (TAS behaviour). |
| 8 | Cancellation | Start Both / OpenStudio, press Cancel while a producer runs | `Cancelled`; producer tree killed; no comparison | manifest `status: Cancelled` with provenance | none | manifest, process list | **NOT RUN**: no driver. Unit level: `SimulationRunProvenanceTests`, `TasRunManifestTests`. |
| 9 | Concurrent-start refusal | While Both runs, try every other entry point | each refused: "... is already running for this model, so nothing was started." | first run's artifacts only | the S02 text | screenshots, folder listing | **NOT RUN**: no driver. Unit level: S02 `SimulationConcurrencyGuardTests`. |

## Remaining blockers before formal S05
1. S03 (#242) and S04 (#243) merged with green final-head CI. Whichever merges second needs the RealRun conflict resolved.
2. Both prerequisite: an owner decision on `SAM_COMMIT` for developer-built producers, or wait for S06's stamped producers.
3. A multizone fixture with cleared provenance (owner decision 3).
4. New UI Automation drivers:
   - Run Simulation > OpenStudio;
   - Cancel during Both and during OpenStudio;
   - a concurrent start, which needs a long first run (for example the S01b blocking wrapper).
5. The OpenStudio CLI/SDK mismatch on `PATH` (S03 finding) should be settled, ideally in S06, before acceptance evidence is
   archived.

## Formal acceptance procedure (after merge)
1. Fetch `sow/2026-Q4` with S01-S04 merged.
2. Build the app isolated, as above.
3. Run cases 1-9 with the drivers.
4. Collect manifests, driver logs and screenshots in `documentation/evidence/s05-<date>/`.
5. Record PASS/FAIL/NOT RUN/BLOCKED per case in the S05 record, and report GitHub CI separately.

Recommended for the S05 session: new session, Claude Opus 5.5, effort High, Build mode. It is a separate, licensed,
GUI-driven task on merged content, and it needs the three new drivers.
