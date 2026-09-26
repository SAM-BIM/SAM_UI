# Part O UX pass 5 - the Iteration 2B journey (26 Sep 2026)

Branch `feature/parto-2b-journey-2026-09-26`, from `sow/2026-Q3` `3b29e41` (Pass 4 / #117 merged). SAM_UI only.

## Step 1 - inventory of the production route, before this pass

| # | Stage | Where | What happened |
|---|---|---|---|
| 1 | Eligibility | `Modify.CanOptimise` (OptimisePartOTM59.cs) | `IsAssessable`; not restored; preparation context present; MVHR route; catalogue offered; full-year TAS case. **Plus** a UI gate: `Capabilities` / ribbon also required `PreparationContext.OptimisationSettings != null`. |
| 2 | Exposure | Hub `PartOWorkflowWindow` | `Optimise (2B)` button in the actions row (secondary). Settings lived in a **collapsed** expander, "Follow-on optimisation (Iteration 2B)", with a "Allow automatic TM59 optimisation after this run" tick. Ribbon: Results - Optimise (2B). Prepare Iteration window: "Automatically optimise TM59 failures" tick. |
| 3 | Run state needed | `PartORun` | `WorkflowCompleted`, live (not restored), with both contexts. The settings had to be recorded at preparation (`AdoptOptimisationSettings` accepts only a `Prepared` run), so **a run prepared without the tick needed a new full-year TAS run to become eligible**. |
| 4 | Preparation | none before 2B | `RunPartOOptimisationResult` read the recorded settings and started at once. |
| 5 | Review/accept | none | No confirmation; no statement of starting run, scope or what changes. |
| 6 | Baseline | `Optimise`, run 0 | Existing results re-assessed (no re-simulation). Pass -> `Passed`; no verdict -> `AssessmentFailed`. Stage 1 "Assess the Iteration 2 results (TM59)". |
| 7 | Rounds | `Optimise` loop | Targets (production TM59 FAIL, mechanical, in scope, +step) -> one design airflow round (retried without at-capacity dwellings) -> re-prepare with NULL catalogue -> full-year TAS under `-OptNN` -> TM59. Stage 2 "Optimisation rounds", activity "round n". Cancel offered only inside `AllowCancel` scopes (Pass 4). |
| 8 | Capacity envelope | `CapacityEnvelope` | Only if asked for and the stop was `CapacityReached` / `IterationLimitReached` with failing targets; one more full-year run under `-OptMax`; never adopted. Stage 3 "Capacity envelope (diagnostic)" only if asked for. |
| 9 | Stop | `PartOOptimisationStopReason` | 9 terminal values (below). |
| 10 | Result | `PartOOptimisationResultWindow` | "Stopped: <enum description>", the run's long `Description` paragraph, the envelope line, two always-open grids and an always-open notes box - no verdict, no next step. |
| 11 | Hub afterwards | `Modify.OptimisationOutcome` | Outcome line from the stop reason (Pass 3). The model is replaced with the last valid design; the run holds that round, so Review Results reads it and 2B can run again (numbering continues). |
| 12 | Reopen | `PartORunResume` sidecar per round's TSD | See Step 10 below. |

Windows/controls: Hub (`button_Optimise`, `textBlock_OptimiseCaption`, the removed `expander_Optimise`), `PartOIterationWindow` (2B tick), `PartOProgressWindow` via `PartOProgressHost`, `PartOOptimisationResultWindow`, ribbon `RibbonButton_OptimisePartOTM59`.

## Step 2 - UX problems found (verified in code)

1. **Pre-run prerequisite** in a collapsed expander; forgetting it cost a full-year TAS re-run. (Main problem.)
2. No start confirmation: nothing said what 2B starts from, may change or will not change.
3. Result screen: no TM59 verdict; stop reason only as an enum word; large grids and notes always open; no next step.
4. Progress (Pass 4) was already correct - one window, real stages, round without total, truthful Cancel. Kept unchanged.
5. Hub outcome (Pass 3) already worded from the stop reason. Kept unchanged.

## What this pass changed

- **Entry** (owner decision, 26 Sep): eligibility is `CanOptimise(run, null)` alone. The Hub expander is removed; Prepare & Run records no 2B settings. `Optimise (2B)…` opens **Start Iteration 2B** (`PartOOptimisationStartWindow`, `PartOOptimisationStart`): purpose, starting run (scenario, results file, dwellings, weather, or "design kept by an earlier 2B run (round n)"), May change / Never changes, settings pre-filled (recorded -> last confirmed this session -> defaults, and says which), validated by `PartOOptimisationSettings.IsValid`. Confirmed settings go straight to `OptimisePartOTM59` and are recorded on `PartOOptimisationRun.Settings`. Cancel = nothing runs, Hub line unchanged.
- The Prepare Iteration window keeps its tick, reworded "Pre-set Iteration 2B settings for this run (optional — confirmed when 2B starts)"; it only pre-fills.
- **Result** (`PartOOptimisationSummary`): verdict band (PASS only on `Passed`; FAIL where the kept design failed; NOT ASSESSED otherwise; UNAVAILABLE with no kept design - the same rule as the Hub), stop headline + meaning, summary facts, "Next:" line; then **Engineering detail** collapsed (tabs: design airflow changes, unit duty by round, notes). Nothing removed: the run's full `Description` heads the notes; Copy All has everything.
- Engine, stop rules, rounds, envelope, cancellation points, TAS behaviour: **unchanged**.

## Step 6 - stop reason presentation

| StopReason | Headline | Next |
|---|---|---|
| Passed (0 rounds) | No optimisation needed — the starting design already passes TM59 | Review TM59 |
| Passed | Stopped: TM59 target reached (first passing design at the step, not a minimum) | Review, save |
| CapacityReached | Stopped: selected ventilation unit capacity reached | Decide: larger unit is deliberate, never automatic |
| IterationLimitReached | Stopped: round limit reached (N rounds) | Run 2B again (continues), allow more rounds |
| NoEligibleTargets | Stopped: no failing space that Iteration 2B can change | Design change outside 2B |
| RebalanceRefused | Stopped: a design airflow round was refused | Open Engineering detail › Notes, resolve, rerun |
| PreparationFailed | Stopped: the optimised design could not be prepared for simulation | same |
| SimulationFailed | Stopped: a round's TAS simulation did not complete | same |
| AssessmentFailed | Stopped: TM59 could not be assessed | same |
| Cancelled | Cancelled | Run again when ready (continues from kept design) |

## Step 8 - comparison (what is and is not shown)

Shown, from the run's record only: baseline vs kept design production TM59 status; "spaces with a failing TM59 check" (distinct design spaces among SAM's Fail rows - a tally, worded as checks, not a space verdict); spaces whose design airflow the **completed** rounds changed (targeted vs balancing); rounds run/completed, limit, step; envelope outcome.
Not shown (limitation, no new calculation): total design airflow before/after, per-unit headroom summary, and SAM's per-space overall status per round - a round records `PartOTM59SpaceResult` check rows, not the `TM59AssessmentReport`, so a SAM per-space verdict per round would need the report kept on the step (a SAM_UI core record change, deferred).

## Step 10 - reopening / persistence

- `PartOOptimisationRun` (rounds, stop reason, envelope, history) is **session-only**. Closing the 2B result window loses it; restarting SAM_UI loses it. Nothing reconstructs it.
- Persisted: each round's own TBD/TSD (`-OptNN`, `-OptMax`) and TM59 report, and a `PartORunResume` sidecar beside each round's TSD, recording the preparation as **Iteration 2** (the scenario) - the round is only in the file name.
- Reopening a saved kept design restores a run that can be reviewed ("Saved Iteration 2 results") but **not** continued into 2B (`CanOptimise` IsRestored refusal - settled by design); Prepare & Run again to optimise.
- Not added: persistence of the 2B history, or a Hub "reopen last 2B result" (session-only, would need a retained run; possible follow-up).

## Cancellation edge case (reviewed)

A Cancel click inside a round's `AllowCancel` scope followed by a refusal (e.g. the TBD cannot be overwritten) stops the round as `SimulationFailed`, not `Cancelled` - the optimiser's precedence. That is truthful (the round did fail) but read as confusing: "Cancelling…" then a failure. **Smallest safe correction taken:** the command reads `PartOProgressHost.IsCancellationRequested` and the result window adds "Cancel was requested, but the run stopped for the reason above before it reached a point where it could stop for Cancel." The stop reason and its precedence are unchanged.

## Live acceptance - real app, real TAS (26 Sep 2026, 08:17-08:29)

Exe `build\SAM Analytical.exe`, WPF dll 08:10 (then 08:27 for the reopen re-check after the fixes below). Model: a fresh copy of the 25 Sep smoke model (`SAM_zoningAM-CIBSEfutureZ1.sam`: 3 dwellings, 8 spaces, weather Z1_DSY1_2050s_HIGH90_CIBSE_v1.1). Driver `C:\TasOut\parto-2b-journey-2026-09-26\scripts\journey2b.ps1` (UIA; captures are PrintWindow). Driver logs (`journey-*.txt`) and the key captures are in `live/`.

1. **No pre-run 2B tick (the regression scenario).** Hub, Iteration 2 selected: no 2B expander anywhere; Optimise (2B)… disabled, "After an Iteration 2 run" (`01`). Prepare & Run → Review iteration → Accept → one Part O window, real full-year TAS, 54 s (`06`) → TM59 window: FAIL, 8 assessed · 2 pass · 6 fail · 1 not assessed (`07`). Hub: "✕ Iteration 2 completed — TM59 FAIL"; **Optimise (2B)… enabled, caption "Optimise ventilation"**, on `CanOptimise` alone; no re-run (`08`).
2. **Start Iteration 2B** (`09`, `10`): source "The completed Iteration 2 run and its TM59 results", `000000_SAM_AnalyticalModel-It1a-futureZ1.tsd` (the model's own project name), 3 dwellings, the weather. May change / Never changes as designed. Pre-filled 5 l/s · 10 rounds · envelope on · warm start on, "Pre-filled with the default settings." Step `0` → Start disabled with `PartOOptimisationSettings.IsValid`'s sentence; `abc` → "'abc' is not an airflow step…"; `5` → enabled. **Cancel** → Hub unchanged, no progress window, no TAS process (`11`).
3. **Optimisation** (`13`, `17`, `23`): Start → ONE "Part O" window. Stages "Assess the Iteration 2 results (TM59)" ✓ 1 s → "Optimisation rounds · round n" (round 1…10, never a total; subheading "The limit is 10 rounds; how many run depends on the results") → "Capacity envelope (diagnostic)". Indeterminate bar, no percentage, the note says why. Cancel disabled with the "offered only while a TAS simulation is being prepared or run" note during assessments; enabled during each round's TAS steps. 10 rounds + envelope in 243 s (warm-started rounds ≈ 14-30 s each). Files `-Opt01`…`-Opt10`, `-OptMax`, each with its own TSD, TM59 report and sidecar.
4. **Result** (`26`, `27`): **✕ TM59 FAIL · "Stopped: round limit reached (10 rounds)"** · "Eligible spaces were still failing after the last allowed round. The last complete design (run 10) is kept." Rounds "10 completed · limit 10 · 5 l/s step"; starting design run 0 Fail · 6 spaces with a failing check; kept design run 10 Fail · 6 spaces; design airflow changed "8 spaces · 6 targeted, 2 balancing"; capacity envelope "Calculated (diagnostic, not adopted) · production TM59 status Fail". Next: "Optimise (2B) again continues from the kept design, and you can allow more rounds…". Engineering detail collapsed; expanded: 108 airflow rows, 36 unit rows, 863 notes - nothing hidden.
5. **Hub after** (`28`): "✕ Iteration 2B optimisation completed — TM59 FAIL · Stopped: iteration limit reached · 10 rounds · last valid design: run 10" (captured before the "round limit" wording change below) - agrees with the result, one line, not a log; Optimise still available (the run holds the kept design).
6. **Second 2B, cancelled** (`29`, `33`, `34`, `35`): Start window pre-filled "from the settings last used in this session" (Codex P2 fix), source "The design kept by an earlier Iteration 2B run (round 10); numbering continues from it" (round written as `-Opt11`, nothing overwritten). Cancel pressed during round 1's TAS workflow → "Cancel requested. It stops at the next safe point…" → stopped 8 s later. Result: "Cancelled — The round in progress was stopped and is not a result. The starting design is kept."; rounds "1 run, 0 completed in full"; Next: "Iteration 2B cannot continue from this stop, because the stop closed the session run: … Prepare & Run …" (Codex P2 fix). Hub: "○ Iteration 2B optimisation cancelled · 1 round · last valid design: run 0"; Optimise disabled with the optimiser's own reason - consistent with the Next step.
7. **Save → restart → reopen** (`40`, `41`): the saved kept design (title `…-Opt10`) reopens as "○ Saved Iteration 2 results reopened — ready to review"; Review Results available; Optimise (2B) disabled (Hub and ribbon). No round history or stop reason is shown or inferred - as designed (see Step 10).

**Issues found live, and fixed (small, presentation only):**
- *Reopen caption/tooltip.* With the scenario box back on its first entry (Iteration 1a), a reopened **Iteration 2** run showed "Iteration 2 only" / "available on Iteration 2 only" (`40`). This behaviour predates Pass 5. Now, where a run with results exists, the tooltip is `CanOptimise`'s refusal ("reopened from a saved model … Run Prepare & Run again …") and the caption is "Needs a live run" (restored) or "Not available" (`41`, re-checked live 08:29).
- *Hub vocabulary.* The Hub said "iteration limit reached" beside the result's "round limit reached". Changed to "round limit reached", the word every 2B window uses.
- (Codex P2s on #118, fixed before this run and proven in 6: next step only offers continuing where the run survives; session pre-fill on every route.)

**Observed, not changed (outside Pass 5):**
- Each round's saved `.sam` roughly doubles (172 KB → 1.9 MB at `-Opt10`, 3.7 MB `-OptMax`) - the workflow model accumulates between rounds. A scalability concern for large projects; flagged separately.
- After a 2B the Hub reads "Ventilation design: Not prepared - rebuilt for the next Prepare & Run": the loaded model is the kept design, not a prepared one. Pre-existing and accurate.
- A cancelled round leaves its partial `-Opt11` TBD/TSD on disk; it is recorded as not a result.
- Stop reasons not reached live - Passed, CapacityReached, NoEligibleTargets, the failure stops, baseline-already-passes - are pinned by `PartOIteration2BJourneyTests` (one headline per reason, next step, verdict rule) and rendered in the synthetic evidence below; no state was manufactured for screenshots.

## Communal-corridor reporting (verified, no defect)

Authority: SAM `TM59AssessmentReport` - a corridor-bucket check lands in `CorridorChecks` only where the space's InternalCondition is exactly `TM59_Communal Corridor (including pipework gains)` (`TM59InternalConditionResolver.CommunalCorridorInternalConditionName`); everything else goes to `SupplementaryChecks`. SAM.Tests prove both directions (`CommunalCorridor_IsPositivelyIdentifiedByInternalCondition_NotBySpaceName`, `ASpaceNamedCorridor_WithoutTheCommunalCorridorInternalCondition_IsNotClassifiedAsOne`). SAM_UI's `PartOTM59ResultSummary` presents exactly that split ("Communal corridor" / "Other >28 °C checks") with no classification of its own. New `PartOTM59CorridorReportingTests` pins it: `Room_99` with the corridor IC → Communal corridor; `Corridor_5` with `TM59_Internal Corridor` → supplementary. The live model has no such IC (its `Corridor_1` has no overheating scenario and is "not assessed"), so this was not checked live.

## Evidence

Live captures and logs: `live/` (numbers above). Synthetic renders:
`2b-start.png`, `2b-start-invalid.png`, `2b-result-capacity.png`, `2b-result-capacity-detail.png`, `2b-result-passed.png`, `2b-result-baseline-passes.png`, `2b-result-simulation-failed-after-cancel.png` - rendered by the env-gated test `Evidence_renders_the_start_confirmation_and_the_result_window` (`SAM_PARTO_2B_EVIDENCE`) from synthetic histories; no TAS.
