# S01b - TAS failure reporting in SAM_UI - PR record

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S01 (Phase A). S01b is the SAM_UI half; S01a
(SAM-BIM/SAM_Tas#103, merged `e483877c`, closeout `e84aeff6`) is the SAM_Tas half.

Base: `sow/2026-Q4` `68705375`. Branch: `feature/s01b-tas-failure-reporting`. One PR, draft, not merged.
**Validated content SHA: `f1157173`** (code and tests; the review correction below on top of `1002c5f1`, which the
licensed UI runs used). Record commits follow; the final PR head and hosted checks are in the PR description. `PROJECT_PROGRESS.md` is not touched (post-merge closeout only).

## Status
Implementation, unit tests and licensed UI checks complete on 2026-10-19 (this laptop, TAS licensed). Awaiting owner
review and final-head hosted CI. Not merged.

## Dependency check (before starting)
SAM_Tas#103 is `MERGED` into `sow/2026-Q4` (`e483877c`, 2026-10-19T17:55:57Z). The local `SAM_Tas\build`
(`SAM_Tas` checkout at `e84aeff6`, clean) holds `SAM.Analytical.Tas.dll` `84fb3e61…` and `SAM.Core.Tas.dll` `2a0aab0f…`, equal to
the S01a record's validated `a3be6931` candidate, and contains `NativeReturnCode`, `ZoneResultsRefusal`,
`AddSimulationRefusals` and the benchmark `AddWorkflowFailure`. SAM `62b54156`, SAM_OpenStudio `d4216f3`,
SAM_Validation `68eded0` (all Q4, clean).

## Problem (measured, baseline `68705375`)
Same injected native refusal (see "Refusal injection"), real UI:
- **Energy Simulation / Run Simulation > Tas:** `RunWorkflow` dropped the `WorkflowCalculator`, so its `TAS-SIM-001`
  diagnostics and `SimulationEvidence` never reached the UI. The closing box said only "Model could not be converted."
  (untitled; case E1) and the renamed copy of the open model was adopted as if the run had finished. A `TPD` request
  would have been built from whatever `<name>.tsd` was at the path (the refusal's stub or an earlier run's).
- **Run Simulation > Both:** the status was already right (`PartiallyCompleted`, Tas exit 5, OpenStudio kept; case B0),
  but the refusal detail written by benchmark-tas into its failed B1a document (`TAS-SIM-001` warnings with return code
  and error log) was never shown: only "exited with code 5 … see benchmark-tas.log".

## Failure propagation path (after)
1. **Energy Simulation** (`RibbonButton_EnergySimulation_Click`) and **Run Simulation > Tas**
   (`RibbonButton_RunSimulation_Click` → `SimulationEngineChoice.Tas`) both call `AnalyticalWindow.SimulateTas()` →
   `Modify.Simulate(UIAnalyticalModel, PartORun)` → private core `Modify.Simulate(…, quiet: false)` →
   `RunPartOSimulation(…, out refusal, out failure, …)` → `RunWorkflow(…, out cancelled, out failure, owned: true)` →
   `WorkflowCalculator.Calculate` returns null (S01a) → `Query.TasWorkflowFailure(workflowCalculator.GetDiagnostics(),
   workflowCalculator.SimulationEvidence)` → back in the core `simulationFailed = !cancelled && model == null` →
   `Query.TasSimulationFailedMessage` → Windows Forms `MessageBox` titled **"Simulation failed"** (Error icon):
   "Simulation failed: the TAS simulation did not complete, so this run produced no results.", `TAS return code: 0`,
   `TAS error log: <path>` (only when the file exists), one `Error: <step>: <message> (TAS-SIM-001)` per Error diagnostic
   (capped at 10, remainder counted), "The open model was not changed.", elapsed time. The model is **not adopted**;
   TPD creation is skipped and said so; SAP/TM59/Part L exports, if asked for, are reported as made from inputs only.
   `PartOSimulationOutcome.Failure` carries the same detail (Part O Hub shares the core; its `Message` now opens with
   "Simulation failed" too).
2. **Run Simulation > Both** (`RunBothEnginesAsync` → `SimulateBothEnginesAsync` → `RunEngineComparison`): after a
   producer that did not succeed, `Query.BenchmarkFailureLines(engine, benchmarkPath)` reads `provenance.warnings` and
   `provenance.notes` of its B1a document as plain JSON (no validating reader, so an invalid failure document still says
   why; unreadable → one line saying so) into `EngineComparisonRun.Diagnostics` as `Tas warning: TAS-SIM-001: …` /
   `Tas note: …`. These reach the comparison window's Diagnostics (`EngineComparisonRunLines`, also under the partial
   result's `EngineResultDiagnostics`) and the run manifest's existing `diagnostics` array (no schema change). Status
   logic unchanged: one engine completed → `PartiallyCompleted`; none → `Failed`; never `Compared`. The failed document
   is still never used as a result.

## Decisions and assumptions
- Current-run failure is decided only from the workflow's return value and this run's `WorkflowCalculator`
  (diagnostics + `SimulationEvidence`), never from `<tbd>.json`/`.tsd` in the folder (S01a risk note). Both already
  uses a fresh run folder per run and requires exit 0 + document.
- Any non-cancelled null workflow model is reported as a failure (not only `TAS-SIM-001`): the same condition the UI
  previously called "could not be converted". Without diagnostics the detail is
  `Query.TasSimulationFailedNoDetail`.
- A failed simulation no longer replaces the open model (it added no results; adopting it would present the results the
  model already held as this run's). Export-only runs (Simulate unticked) are unchanged.
- Minimal API change: new overloads `RunWorkflow(…, out bool cancelled, out string? failure, bool owned)` and
  `RunPartOSimulation(…, out string refusal, out string? failure, …)`; the old signatures delegate. The public
  `PartOWorkflowRunner` delegate is unchanged, so a custom runner (Part O Iteration 3) yields no detail.
- Text format follows the Diagnostics Standard line `Severity: Subject: Message` (`Core.Query.DiagnosticMessage`) with
  the code appended in parentheses; severity is the producer's (only Errors are listed).
- No change to SAM_Tas, SAM_OpenStudio, SAM_Validation, producers, B1a schema, exit codes or status enum.

## Files changed
`WPF/SAM.Analytical.UI.WPF/Query/TasWorkflowFailure.cs` (new: `TasWorkflowFailure`, `TasSimulationFailedMessage`,
`TasSimulationFailedNoDetail`), `Modify/RunWorkflow.cs`, `Modify/RunPartOSimulation.cs`, `Modify/Simulate.cs`,
`Classes/PartO/PartOSimulationCase.cs` (`PartOSimulationOutcome.Failure`), `Modify/RunEngineComparison.cs`,
`Query/EngineComparison.cs` (`BenchmarkFailureLines`); tests `WPF/SAM.Analytical.UI.WPF.Tests/TasFailureReportingTests.cs`
(new, 9) and one test in `PartODesignModelProtectionTests.cs`; this record and `documentation/evidence/s01b-20261019/`.

## Tests (local, Windows, content `1002c5f1`)
Build: `dotnet build WPF/SAM.Analytical.UI.WPF.Tests/SAM.Analytical.UI.WPF.Tests.csproj -c Release` (APPDATA/USERPROFILE
redirected, `NUGET_PACKAGES` real) - succeeded, no new warnings. App: `MSBuild "Application\SAM Analytical\SAM
Analytical.csproj" /t:Restore;Rebuild /p:Configuration=Release /p:OutDir=<dir>\` (same redirection) - 0 errors, base and
candidate.

| # | Case | Test | Result |
|---|---|---|---|
| 1-2,5 | Refused evidence → return code, error log, every TAS-SIM-001 | `Failure_OfARefusedRun_NamesTheReturnCode_TheErrorLog_AndEveryTasSim001` (real `SimulationEvidence`: return 0, 22-byte stub, error log) | PASS |
| 1-2 | Only Errors listed; null when nothing reported; cap | `Failure_ListsErrorsOnly_…`, `Failure_CountsTheErrorsItDoesNotList` | PASS |
| 4 | Error log named only if present; folder files ignored | `Failure_NamesTheErrorLogOnlyWhenItIsThere_AndReadsNothingElseFromTheFolder` | PASS |
| 1-2 | Message always "Simulation failed", never "successfuly converted" (all export/TPD combinations) | `FailedMessage_AlwaysOpensWithSimulationFailed_AndNeverClaimsSuccess` | PASS |
| 1,4 | Shared core: null workflow with a 1 MB TSD and a JSON in the folder → Failure, not Completed, design untouched | `PartODesignModelProtectionTests.A_workflow_that_returns_no_model_is_a_failed_simulation_whatever_files_are_in_the_folder` | PASS |
| 3,5 | Both: Tas refused + OpenStudio OK → PartiallyCompleted, OpenStudio document unchanged, TAS-SIM-001 in diagnostics, window text, manifest | `Both_WithTasRefused_IsNotCompared_KeepsOpenStudio_AndShowsTheTasSim001Detail` | PASS |
| 3 | Both failed → Failed, Tas detail still shown | `Both_WithBothFailed_IsFailed_AndStillSaysWhyTasFailed` | PASS |
| 8 | Both success adds no lines | `Both_Succeeded_AddsNoProducerLines` | PASS |
| - | Unreadable / empty failure document | `FailureLines_OfAnUnreadableDocument_…` | PASS |
| 9 | Existing Both orchestration (status, exit codes, failed-document never used) | `EngineComparisonTests` (20, unchanged) | PASS |
| all | Full suite | `SAM.Analytical.UI.WPF.Tests` **2845/2845** (2835 before + 10 new), real `APPDATA` | PASS |

Mutations (each reverted, then rebuilt and the focused 30 re-passed): failure flag never set → Part O core test fails;
Warnings/Info listed as errors → `Failure_ListsErrorsOnly` fails; failed document not read → both Both refusal tests fail.

Note: a first full run with `APPDATA` redirected to an empty folder for the **test run** gave 34 failures, all Part O /
Direct T3D tests needing installed resources (`'MV' topology template could not be resolved`, gbXML defaults). They
pass with the real `APPDATA` (the test run writes nothing there); environmental, not this change
(`evidence/…/unit-tests-full.txt`).

## Licensed UI checks (TAS on this laptop, 2026-10-19)
Real `SAM Analytical.exe` driven by UI Automation (`tools/drive-tas.ps1`, new; `tools/drive-both.ps1`, #239's driver plus
Diagnostics capture and a clean close). Apps built isolated: `app-base` from Q4 product content (worktree
`SAM_UI-wt-e2e-validation` at `d57dab70`, whose `WPF`/`SAM_UI`/`Application`/`Grasshopper` tree equals `68705375`),
`app-ab` = `app-base` with only the candidate `SAM.Analytical.UI.WPF.dll` (both version 1.0.0.0). Every dependency is
byte-identical between them (`hashes.txt`). Model `box-aperture.json` `99ba2ab9…`, EPW `Budapest.epw` `472ffb4e…` (same
inputs as #239/S01a). Dialog defaults recorded in each `driver.log`; Full Year Simulation ticked (Sizing on by default).

**Refusal injection:** a directory at `<output>\<project>.tsd`. TAS cannot write the TSD and refuses natively:
`simulate` returns `0` and writes `<project>_error_log.txt` = "Error accessing the TSD file for writing" (probe with the
S01a harness on the merged build first). For Both, `tools/wrapper-Program.cs.txt` stands in for benchmark-tas: it creates
that directory from `--tbd`, then runs the real `benchmark-tas.exe` with the same arguments. **TAS also opens its error
log in Notepad on such a refusal** (observed for every refusal case; not opened by SAM code).

| ID | Case | Result | Evidence (`documentation/evidence/s01b-20261019/cases/`) |
|---|---|---|---|
| E1 | Baseline, Energy Simulation, refusal | Before: untitled "Model could not be converted." | `E1-base-es-refusal` |
| E2 | Candidate, Energy Simulation, refusal | **PASS**: "Simulation failed" box, `TAS return code: 0`, error log path, 3× `TAS-SIM-001`, "open model was not changed"; no `box.json`; window title unchanged; app closed with no save prompt | `E2-cand-es-refusal` |
| R1 | Candidate, Run Simulation > Tas, refusal | **PASS**: identical report | `R1-cand-runsim-tas-refusal` |
| E3 | Candidate, refusal in a reused folder holding S2's `box.json`/TBDs | **PASS**: reported failed; stale `box.json` untouched (hash equal to S2's) | `E3-cand-es-refusal-reused-folder` |
| S1/S2/S3 | Energy Simulation success: base / candidate / base (control) | **PASS**: unchanged "Model successfuly converted."; persisted results exact on **602/602** numbers (2 space + 28 surface results) S1↔S2 and S1↔S3 | `S*`, `cmp-S1-vs-S2.json`, `cmp-S1-vs-S3.json` |
| R2 | Candidate, Run Simulation > Tas success | **PASS**: unchanged message, 602/602 exact vs S1 | `R2-…`, `cmp-S1-vs-R2.json` |
| B0 | Baseline, Both, Tas refused | Before: PartiallyCompleted, no TAS detail (0 `Tas warning` lines) | `B0-base-both-tas-refused` |
| B1 | Candidate, Both, Tas refused, OpenStudio OK | **PASS**: PartiallyCompleted banner, OpenStudio results shown, 3× `Tas warning: TAS-SIM-001` with return code 0 and error log, manifest status `PartiallyCompleted` with the lines; Tas B1a `state: Failure`, exit 5 (B1a failure behaviour unchanged) | `B1-cand-both-tas-refused` |
| B2/B3 | Both success: candidate / baseline | **PASS**: both `Compared`, same gate (6/0/10/2); `comparison.csv`/`.md` byte-identical; B1a and summary identical except run timestamp/duration and `sourceFileHash`/`canonicalModelHash` (F7/S04: regenerated per load) | `both-success-comparison.txt` |
| E0 | Candidate, Full Year Simulation off (first attempt) | Not a refusal case: sizing only, TAS simulate never called, success correct | `E0-…` |
| - | Comparator sensitivity | one changed digit → 1 difference | `cmp-tamper.json` |
| - | OpenStudio-only workflow (regression 7) | **NOT RUN** licensed; code path untouched by this PR (`SimulateOpenStudio.cs` unchanged), OpenStudio leg of B1/B2 ran unchanged | - |

Reproduce: build both apps as above; `tools/run-tas-case.ps1 -Name <n> -App base|ab -Entry EnergySimulation|RunSimulationTas
[-BlockTsd] [-ReuseFrom <case>]`; `tools/run-both-case.ps1 -Name <n> -App base|ab [-RefuseTas]` (sets `SAM_BENCHMARK_*`,
`SAM_COMMIT`, OpenStudio 3.10 `bin` on `PATH`); `tools/compare-persisted.ps1` (S01a's comparator). Machine-local:
`C:\TasOut\s01b` (apps, runs, `box.json`s), TAS licence, OpenStudio 3.10 runtime under `.icon-migration\runtime`.

## Review correction (2026-10-19, `f1157173`)
- **Finding (confirmed):** `Query.BenchmarkFailureLines` caught parse errors only. For valid JSON whose `provenance` is not
  an object (`{"provenance":"unexpected"}`, `[1,2]`, `42`), `provenance?["warnings"]` threw `InvalidOperationException`
  ("The node must be of type 'JsonObject'") outside the catch. `RunEngineComparison` then ended `Failed` without running
  OpenStudio. Reproduced by the new tests before the fix (3 theory cases threw; the Both test saw calls `[Tas]` only).
- **Fix:** `provenance` is taken `as JsonObject`; any other shape gives no lines. One line of logic in
  `Query/EngineComparison.cs`; no other change.
- **Tests:** `FailureLines_OfValidJsonWithAnUnexpectedProvenanceShape_SayNothing_AndDoNotThrow` (6 shapes, including a
  non-array `warnings` and missing/null `provenance`) and `Both_WithTasFailedOnAMalformedDocument_StillRunsOpenStudio_AndIsPartiallyCompleted`
  (OpenStudio runs, status `PartiallyCompleted`, no exception) - PASS. Normal failed-document reporting, success and
  cancellation unchanged (`Both_WithTasRefused_…`, `EngineComparisonTests` incl. `Run_Cancelled_StopsAndSaysSo`): focused
  37/37 PASS. Full `SAM.Analytical.UI.WPF.Tests` **2852/2852** PASS (real `APPDATA`). Licensed UI runs not repeated:
  the change only affects a malformed failure document, which the real producer does not write.

## Unresolved issues and risks
- TAS opens the error log in Notepad on a refusal; SAM now also shows its own box. Not changed (TAS behaviour).
- A custom `PartOWorkflowRunner` (Part O Iteration 3 no-IZAM source) has no way to return detail; such failures show the
  no-detail text.
- `SAM_UI\build` was rebuilt by the local test builds (library only, as any test build does); `%APPDATA%\SAM` untouched.
- Hosted CI does not run licensed checks; the licensed table is local evidence on this machine.
- E3 needed a second OK click from the driver (Notepad took the foreground); one message box, driver timing only.

## Recommended next step
Owner reviews this draft PR and its final-head CI; on approval, merge into `sow/2026-Q4`, then the `PROJECT_PROGRESS.md`
closeout with the merge SHA. Then S02 (shared simulation concurrency guard) as a separate owner-started task: new session,
Claude Opus 5.5, effort High, Build mode (separate task, SAM_UI only).
