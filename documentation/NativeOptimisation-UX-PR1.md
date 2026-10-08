<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Design Optimisation: language and information architecture (UX PR1, 8 Oct 2026)

**Status:** code, tests, mutation re-check and screenshot evidence complete; PR open for owner review. **Not merged.**
No licensed Tas run was needed (no execution change); the stub run is bit-identical to a direct `RunNative`.

Branch `feature/optimisation-ux-language` from `sow/2026-Q4` @ `e7a3584d` (after SAM_UI#213). `PROJECT_PROGRESS.md` is
not touched on this branch (post-merge closeout). The approved architecture plan is
`C:\Users\Virtual Machine\.claude\plans\opus-5-5-robust-blum.md` (sections 2, 3 and "Execution plan: PR1"); this is its
PR1 only. SAM, SAM_Tas and SAM_Tas_Grasshopper are not touched.

## Why

The native Optimisation window (PR5/PR6) is correct and accepted, but it reads like a test harness: GenOpt field names
as labels, implementation history in the intro, the TasGenExecute path as a readiness line, kernel bookkeeping in the
cancel text, and one long scroll that needed `BringIntoView` hacks to show the run. This PR changes **presentation and
layout only**. Nothing about the run changes.

## Scope: unchanged by construction

These are untouched in behaviour and are covered by the unchanged tests listed under Validation:

- the `TasOptimisationInput` model and its `TryGetDefinition` → `TasOptimisationDefinition` →
  `GenOptDocument.RunNative` path (only the *wording* of the problem labels in `TryGetDefinition` changed);
- the SAM_Tas validation calls (`TasOptimisationDefinition.Validate`);
- the `NativeGenOptOutcome` rules (success, withholding, best point, interval);
- cancellation, and session memory;
- `MaxEqualResults` is still passed: the form keeps SAM_Tas' default (100), so SAM_Tas validates the same value.

## What changed

### Window (`Windows/TasOptimisationWindow.xaml(.cs)`)

- **Title** "Design Optimisation". **Intro:** "Finds the design-variable values that give the lowest objective result.
  Each simulation runs your Tas script on a copy of the Tas project; your files are not changed."
- **Two tabs, Setup | Run & Results.** The footer (readiness sentence, first problem, Run / Cancel / Close) sits outside
  the tabs. Pressing Run switches to Run & Results (also when a run ends before it starts), which replaces both
  `BringIntoView` calls.
- **Setup** sections:
  - **Model:** Tas project, Tas script, and the line "Simulation engine: Tas (script)". The
    `Variables["name"].VariableValue` / `ScriptOutput.SetValue` syntax is now the Script field's tooltip only.
  - **Design variables** (was "Parameters"): Name, Start, Minimum, Maximum, Step.
  - **Objective:** "Minimise [output ▾]".
  - **Recorded outputs:** the other outputs, "Recorded for every simulation, not optimised."
  - **Method:** "Golden section (one design variable)" / "Hooke–Jeeves pattern search" (the `AlgorithmType` values are
    unchanged; a converter shows the names), one plain description line each. Labels: Objective tolerance, Step
    reduction factor, Step reductions.
  - **Stopping:** Maximum simulations.
  - **Advanced** (collapsed): Runs folder, and for Hooke–Jeeves Initial step exponent and Step exponent increment.
  - **Checks:** the existing readiness list, now with engineering titles (see below).
- **Run & Results:** a "no run yet" line, then progress, the trace, the result card, and a **Diagnostics** expander
  holding the Tas optimisation engine path, the loaded assembly locations (`Query.TasOptimisationAssemblyLocations`),
  how simulations run (one at a time, fresh folder, retry once, `name::value` output) and, after a run, the kernel's
  own notes. A **Show search details** toggle reveals the trace Iteration and Event columns; they are hidden by
  default, and the choice survives the next run.
- **Removed from the normal UI:** "Java GenOpt is not used", the MaxEqualResults control and its compatibility text,
  and every GenOpt field name as a label. GenOpt names appear only in tooltips, as "GenOpt: MeshSizeDivider" etc.
- **Footer sentence:** "✓ Ready: Golden section on Setpoint (−5 to 35), minimising Result; recording Cost, CO2; at
  most 2000 simulations." With name warnings: "⚠ Ready, with warnings: … See Checks on the Setup tab." Blocked: "✕ Before
  running - Tas project: Choose the folder …".
- Ribbon tooltip (`AnalyticalWindow.xaml.cs`): "Find the design-variable values that minimise an objective by running a
  Tas model repeatedly. Works on a Tas project folder, not on the open model. Not the Part O Optimise (2B) command."
  The label stays "Optimisation".

### Readiness (`Query/TasOptimisationChecks.cs`)

- Titles: "Tas project", "Tas script", "Tas optimisation engine", "Setup" (and "Design variable 'x'" / "Output 'x'" for
  the name warnings, whose text no longer repeats the script syntax).
- The engine line says "Installed." when it is there; its path is in Diagnostics. When it is **missing**, the path is
  kept in the message (a person needs it to fix that).
- A SAM_Tas refusal is shown as SAM_Tas words it, **without** the "Invalid GenOpt settings for the native route"
  prefix that `TasOptimisationReport.Message` adds for the Grasshopper wording.

### Report (`Classes/TasOptimisation/TasOptimisationReport.cs`)

- Headlines: "Optimum found after N simulations"; "Stopped at the simulation limit (N simulations) – best design so
  far"; "Stopped: golden section found equal objective values (N simulations)"; "Cancelled"; "A Tas simulation failed
  (simulation N)"; "Start values are outside the design-variable ranges".
- Lines: Best design / Objective / Recorded outputs / Final interval / Retries / Run folder.
- The kernel's outcome code and its simulation-count note ("includes the number it had assigned when the cancel was
  observed") moved to a new `DiagnosticsText`, which the window shows under Diagnostics and `ToText()` carries as a
  final "Diagnostics:" line.
- **Numbers stay full precision** (`"R"`) here; `ToText()` (Copy summary) too. Engineering display formatting is PR5 of
  the plan, on SAM PR2.
- **Counts** (simulations, retries) go through `TasOptimisationReport.Count`, an explicit invariant integer format.
  `QuantityFormatter` Count is never used: it means "persons".

### Other

- `TasOptimisationProgressState`: "Lowest so far" → "Best so far" (wording only).
- `TasOptimisationInput`: only the wording of problem labels (Objective tolerance, Step reduction factor, Initial step
  exponent, Step exponent increment, Step reductions, Maximum simulations, "Design variable 1 (name) minimum", "Choose
  the output to minimise (the objective).").
- New: `Query/TasOptimisationAlgorithmName.cs`, `Classes/TasOptimisation/TasOptimisationAlgorithmNameConverter.cs`.

## Decisions and assumptions

1. **The Objective box is editable.** The plan calls for a ComboBox of the output names bound to `Primary`. A
   read-only box would have no place to *name* the objective, and a person who brings their own script has to rename
   `Result` first. So the box lists the named outputs and also accepts a typed name: a name that is one of the
   outputs selects it; any other name renames the current objective (or creates it when there is none). The previous
   objective stays as a recorded output. Text search is off, so typing never selects by prefix. Enter and leaving the
   box commit; Enter is handled so it cannot trigger the default Run button.
2. **Add output** adds a *recorded* output; the objective is only ever chosen in the Objective box.
3. **Removing the objective output** (reachable through `RemoveOutput`, and through any state loaded from an example
   or session) makes the first remaining output the objective, as before. The objective itself has no ✕ in the UI: to
   drop it, choose another objective and remove the old one from Recorded outputs.
4. **No minimise/maximise selector** (V1 shows only what can execute).
5. **Checks stay on the Setup tab.** The plan's "Show all checks" belongs to a later PR; until then the list sits at the
   bottom of Setup so name warnings stay visible.
6. **GenOpt text inside SAM_Tas messages is not rewritten.** A SAM_Tas refusal such as "MaxIte must be at least 1" is
   still shown as SAM_Tas words it ("structured L5 messages arrive in PR4/PR5" of the plan).
7. Example names are now "Systems Demo – golden section" / "Systems Demo – Hooke–Jeeves".

## Files changed

`WPF/SAM.Analytical.UI.WPF/`: `Windows/TasOptimisationWindow.xaml`, `Windows/TasOptimisationWindow.xaml.cs`,
`Windows/AnalyticalWindow.xaml.cs` (one tooltip string), `Query/TasOptimisationChecks.cs`,
`Query/TasOptimisationAlgorithmName.cs` (new), `Classes/TasOptimisation/TasOptimisationReport.cs`,
`TasOptimisationProgressState.cs`, `TasOptimisationInput.cs` (wording), `TasOptimisationAlgorithmNameConverter.cs` (new).
`WPF/SAM.Analytical.UI.WPF.Tests/`: `TasOptimisationWindowTests.cs`, `TasOptimisationChecksTests.cs`,
`TasOptimisationReportTests.cs`, `TasOptimisationInputTests.cs`, `TasOptimisationScreenshotHarness.cs` (new, opt-in).
`documentation/NativeOptimisation-UX-PR1.md` (this record).

## Validation (2026-10-08)

Build environment: VS MSBuild, `SAM_UI.sln /restore /p:Configuration=Release`, APPDATA/LOCALAPPDATA/USERPROFILE
redirected to a seeded scratch profile (`C:\TasOut\ux-pr1\profile`), real `NUGET_PACKAGES`. SAM_UI built against the
sibling `build\` folders as they stood: SAM_Tas `sow/2026-Q4` @ `7f2043ac`, SAM_Systems @ `5404926`, SAM `build\`
(SAM `sow/2026-Q4` @ `b081065e` plus the other session's additive PR2/PR3 work, which SAM_UI does not reference). SAM
was not rebuilt here.

| Check | Result |
|---|---|
| Release build, `SAM_UI.sln` (Rebuild) | **0 errors** |
| `TasOptimisation*` tests | **92/92** |
| Full `SAM.Analytical.UI.WPF.Tests` | **2654/2654** (the stated baseline is 2636; the current `sow/2026-Q4` has 2637 because SAM_UI#213 added one test; this PR adds **16** window tests and 1 opt-in screenshot harness that passes doing nothing without `SAM_OPT_SCREENSHOTS`, so 2637 + 17 = 2654). The ThermalSource flake did not recur. |
| `git diff --check` | clean |

**Unchanged tests, passing as before** (these prove the no-behaviour-change scope):
- the stub `RunNative` bit-identity test, `A_golden_section_run_shows_the_kernels_trace_and_result_and_matches_a_direct_RunNative`
  (same outcome, simulation count, and every entry's simulation, coordinate, three outputs and event, bit for bit
  against a direct `RunNative` of the same document);
- `Cancel_lets_the_running_evaluation_finish_and_starts_no_other`,
  `Closing_during_a_run_asks_then_stops_after_the_current_evaluation_and_closes`,
  `Declining_to_stop_keeps_the_run_and_the_window`, `An_evaluation_failure_is_reported_with_the_evaluators_message`;
- `The_primary_objective_chosen_in_the_window_is_the_one_minimised` (objective moved first);
- `SAM_UI_calls_RunNative_and_no_member_of_the_legacy_Java_route`, and the layout tests;
- the `TasOptimisationInputTests` that exercise `TryGetDefinition` and SAM_Tas validation (only three wording
  assertions changed: the problem labels).

**Updated for wording:** the ribbon tooltip, the opening state, the readiness titles and summary, the report headlines
and lines, "Best so far", and the problem labels of `TasOptimisationInput`.

**New tests** (16, all `[WpfFact]`):
- **Objective box ⇄ `Primary` flag:**
  - choosing Cost puts Cost first in the definition and makes the old objective a recorded output;
  - a picked entry of the opened drop-down does the same;
  - a typed new name renames the objective, and an existing name selects it;
  - an empty box changes nothing;
  - removing the objective output re-selects one;
  - Add output is recorded only;
  - the output minimised by a stub run is the one chosen in the box (Cost drives Setpoint to −5).
- **MaxEqualResults:** the definition still carries SAM_Tas' default (through edits, an example load and a run), and the
  control no longer exists.
- **Tabs:** Run switches to the Run & Results tab (already while it runs); the footer is outside the tabs.
- **Diagnostics:**
  - the engine path is there, and in no readiness line or footer;
  - the assemblies, the one-at-a-time / fresh folder / retry-once / `name::value` text and the kernel notes of the last
    run are there.
- **Trace:** Iteration and Event are hidden by default, shown by "Show search details", and the choice survives the
  next run.
- **Language:**
  - no GenOpt field name, "Java" or MaxEqualResults text in any visible label of either method;
  - the GenOpt names are in the tooltips ("GenOpt: MeshSizeDivider" etc.);
  - the script syntax is the Script tooltip only.
- **Window and report:** the method list shows plain names over the unchanged `AlgorithmType` values; Advanced is
  collapsed and holds the runs folder and the two step exponents; the footer sentence; the exact summary sentence; the
  refusal without the internal prefix; report headlines, lines and Diagnostics for each outcome (the cancel and
  late-cancel bookkeeping is out of the lines).

**Mutation re-check** (the PR5 mutations, each applied, built, run against `TasOptimisation*` and reverted):

| Mutation | Result |
|---|---|
| M2: primary objective not moved first (`if (index > 0)` made unreachable) | **caught, 5 tests failed**: `TasOptimisationInputTests.The_primary_objective_is_passed_first_whatever_its_name_and_position`, `…Choosing_another_primary_output_reorders_only_that_one`, `TasOptimisationWindowTests.The_primary_objective_chosen_in_the_window_is_the_one_minimised`, and the two new Objective-box tests (`Choosing_an_output_in_the_Objective_box…`, `The_objective_chosen_in_the_Objective_box_is_the_one_the_run_minimises`) |
| M5: Cancel does not cancel the token (`cancellationTokenSource.Cancel()` commented out) | **caught, 3 tests failed**: `Cancel_lets_the_running_evaluation_finish_and_starts_no_other`, `Closing_during_a_run_asks_then_stops_after_the_current_evaluation_and_closes`, and the new `The_result_card_uses_the_engineering_headline…` |

Both files were restored from copies and the suite re-run green (2654/2654 above).

**Screenshots** (RenderTargetBitmap of the real window through the opt-in `TasOptimisationScreenshotHarness`, stored
outside git in `C:\TasOut\ux-pr1\screenshots`; regenerate with `SAM_OPT_SCREENSHOTS=<folder>`):

| File | State |
|---|---|
| `1-opening.png` | Opening state: Setup tab, golden-section example, no project or script yet (✕ footer) |
| `1b-ready.png` | A ready form: "✓ Ready: Golden section on Setpoint (−5 to 35), minimising Result; recording Cost, CO2; at most 2000 simulations." |
| `2-hooke-jeeves.png` | Hooke–Jeeves selected, Advanced open (runs folder, step exponents) |
| `3-after-run.png` | After a stub run: Run & Results tab, "Optimum found after 11 simulations", best design Setpoint = 4.968943799848584 (simulation 9), interval [4.767943850222925, 5.093168600454254] (the proven Systems Demo sequence) |
| `4-diagnostics.png` | Diagnostics open: engine path, loaded assemblies, how simulations run, kernel notes |
| `5-search-details.png` | "Show search details" on: Iteration and Event columns |

## Unresolved issues, risks

- **Not exercised by a person in the installed application.** The PR changes layout only and no licensed run was
  needed, but the Objective box's drop-down and typing were verified through WPF tests (container selection,
  `CommitObjective`), not at the real window. The optional UI Automation open-and-read check was not run.
- **GenOpt names inside SAM_Tas messages** (for example "MaxIte must be at least 1; got 0.") are SAM_Tas' own text and
  still reach the Setup line unchanged; structured, engineering-worded messages are the plan's PR4/PR5.
- **Values stay at full precision** (7360.04370117188, 4.968943799848584) by design; engineering formatting is the
  plan's PR5, on SAM PR2. For the same reason there is **no unit anywhere yet** (plan U1).
- **Setup is long.** The Checks list sits at the bottom of Setup because "Show all checks" is a later PR.
- **The Objective box is editable** (decision 1): a deliberate small extension of "a ComboBox of the output names". The
  owner may prefer a read-only box plus a separate name field.
- Deployment items carried from PR5 (SAM_Deploy shipping current `SAM.Math.dll` and `SAM.Analytical.Tas.GenOpt.dll`)
  are unchanged and not part of this PR.

## Next step

Owner review of the wording, the layout and the screenshots (and a manual look at the Objective box). Then PR CI
(`build`, `spdx`) on the reviewed head and a merge (merge commit, `--match-head-commit`) only after the owner's explicit
approval. After that: the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` (`[skip ci]`) with the merge SHA, and deleting
the branch. Then the plan's next stage (PR4, the SAM_Tas adapter) once PR1–PR3 are accepted.
