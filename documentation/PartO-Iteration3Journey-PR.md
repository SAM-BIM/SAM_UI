<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Part O Hub: the Iteration 3 journey (7 Oct 2026)

**Status:** code, automated tests and a licensed real-app walk of the whole journey are complete; awaiting PR CI and
review. Branch `feature/part-o-iteration3-journey` from `sow/2026-Q4` `45ab491c`.

Presentation and routing only (plus one defect in this PR's own first version, below). Direct T3D, the simulation engine, TAS, TM59, the Iteration 3 pipeline and the
pre-flight authorities are not touched. No result, key, fingerprint or file name changes.

## Why

A walk of Simulate > Prepare & Run to Iteration 3 on `Direct-partO-dwellings.json` (Direct T3D on) found that the
default Iteration 3 method could not run and the Hub did not say what to do about it:

- The default method (manufacturer operating guidance) needs a cooling control room saved for each dwelling. Only
  Mixed Design saves it. The Hub's refusal ("no valid saved Part O dwelling strategy selections...") never named
  Mixed Design.
- After a 1a run no method with a product could run, and the one that could (Route check) was hidden under
  Advanced.
- On a typical screen the Iteration 3 panel was below the fold, under the readiness list and equipment selection.
- The Direct T3D header text did not follow a change made without a mouse click.

## What changed

1. **Actionable refusal, and a way to do the step.** `Query.PartOIteration3ProductMethodAdvice` turns the
   missing-rooms refusal into the steps that supply them, written from the open design model and the reference run:
   Remove Results first where the model carries results (Mixed Design refuses it); choose rooms in Mixed Design and
   Save selection; then Prepare & Run Iteration 2 (or again), because the prepared design - which Iteration 3 reads -
   carries the rooms only if it was prepared after they were saved. A new button **Choose cooling control rooms (Mixed
   Design)...** closes the Hub with the new `PartOWorkflowAction.MixedDesign`; `RunPartOWorkflow` runs the existing
   `RunPartOMixedDesign` and reopens the Hub. Where that visit saved rooms, the Hub's line says so and states the next
   step (`MixedDesignSavedOutcome`).
2. **A runnable method is selected for the person, but only for a missing step.** Where nobody chose a method and
   the default is refused because a step is missing (no saved rooms, or no unit has a product yet), the first
   method that can run is selected - product methods before validation methods - and an amber notice names both
   methods, says why and gives the steps. A method the person chose, or one carried from an earlier showing, is never
   replaced, and an automatic choice is not carried to the next showing. A fault (an invalid saved room, a product the
   catalogue does not hold) still leaves the default selected and refused, with Run off.
3. **The panel is reachable.** An **Iteration 3 v** button in the actions row (shown wherever the panel is) scrolls the
   panel into view, so its Run and Open result buttons are visible.
4. **The Direct T3D box follows every change** (`Checked`/`Unchecked` instead of `Click`), so the collapsed Simulation
   case header always states the route. The route itself is untouched.

5. **Follow-up after the owner's first run (same day), three changes:**
   - **Defect fixed - Run acted on the wrong method.** `Iteration3Mode` returned the default whenever the Hub had selected
     another method itself, and the command used that value for Run Iteration 3 and Open result. Pressing Run on the
     automatically selected Route check therefore ran the manufacturer-guidance method and refused at Equipment
     resolution ("no valid saved Part O dwelling strategy selections"). `Iteration3Mode` is now the method on screen (what
     actions use); `Iteration3ModeCarried` is the chosen one (what the next showing starts from). Found by the owner running
     Iteration 2 and then Iteration 3 on a model that carries results, where Mixed Design is not offered so the automatic
     choice applies.
   - **No pop-up in the middle of a run.** A completed Prepare & Run no longer stops on a message box ("Model successfuly
     converted", time elapsed, pre-simulation warnings) between TAS and the TM59 result. Its notes are kept on the Hub line
     ("2 notes - see Show details") and in its full text (`CompletedOutcome`, `NotesText`). A run that did NOT complete still
     shows its refusal box, because the run stops there. Review iteration (Accept) and the Iteration 3 replace confirmations
     are decisions and are unchanged.
   - **The steps are a checklist, not red text.** The missing-rooms refusal is no longer shown as a refusal. The panel says what
     the method needs, then lists the steps with where each stands (done / current / later), and the current one has a button:
     **Remove Results...** (new `PartOWorkflowAction.RemoveResults`, the existing command; opening the cleaned copy ends the Hub,
     as it replaces the model) or **Choose cooling control rooms (Mixed Design)...**. The automatic-selection notice points to
     the checklist instead of repeating it.

6. **Acceptance blocker: Prepare & Run refused a folder with an earlier session's results and left only another folder.**
   Every new session is a new run, and the output-folder guard (SAM_UI#194) refuses a case folder owned by another run - and it
   did so after the Review window, with a bare message. Now:
   - **Asked first.** When Prepare & Run is pressed, the case folder is derived from the scenario (`PartOOutputPaths.CaseOfScenario`)
     and read without changing anything (`PartOOutputPaths.Occupancy`). If another run's results are there, a window
     (`PartOReplaceResultsWindow`) names the case and the folder, says how many generated files it holds and when they were last
     written, and offers **Replace existing results** or **Cancel** (Cancel is the default and Escape).
   - **Nothing is deleted until TAS is about to start.** Confirming only carries the decision; the files are removed in
     `Modify.Simulate` at the point where the folder is claimed. Cancelling the window, or cancelling the Review after confirming,
     leaves every file as it was.
   - **What is removed, and what is not.** `PartOOutputPaths.TryReplaceGenerated` removes the case's `tas`, `reports` and
     `diagnostics` folders and its `PartOCase.json` marker - nothing else: any other file in the case folder, every other case's
     folder (e.g. Iteration 3) and everything outside the output folder stay. A file in use refuses with "could not be removed, so
     nothing was run". The design model is never written.
   - **Same folder, no new folder.** The new run claims the same case folder.
   - **Iteration 3 is consistent.** It uses the same window with its own meaning (replacing keeps the folder - it holds every
     method's record - and overwrites only what the new run writes; this behaviour is unchanged). The older "A completed result
     already exists - run it again?" box is not asked a second time after the Replace window was just confirmed.

## Decisions and assumptions

- **Not built: a default cooling control room chosen by the Hub.** The room is an engineering choice that changes
  what Candidate B simulates; defaulting it silently would change results. The Hub points to where it is chosen.
- **Route check stays classified as a validation method under Advanced.** It is surfaced (auto-selected with a
  notice, the Advanced group opens) rather than moved, so the existing split between product and validation methods
  is unchanged.
- **Single-Hub guard not added.** The Hub is modal (`ShowDialog`); the duplicate Hubs seen in the walk came from UI
  Automation invoking the ribbon behind a modal, which a person cannot do.
- **Direct T3D and the output folder are still not remembered across separate openings of the command** (by design
  in SAM_UI#206); within one opening, including a round trip through Mixed Design, they are carried.
- Iteration 3 still reads the dwelling strategies from the PREPARED design, as SAM_UI#193 decided; the advice says
  to prepare again rather than reading the open model at run time.

## Files changed

`SAM_UI/SAM.Analytical.UI/Enums/PartOWorkflowAction.cs` (new value);
`WPF/SAM.Analytical.UI.WPF/Query/PartOIteration3ProductMethodAdvice.cs` (new),
`Query/PartOIteration3GuidanceResolution.cs` (the refusal text is now a shared constant, unchanged),
`Windows/PartOWorkflowWindow.xaml`, `Windows/PartOWorkflowWindow.Iteration3.cs`, `Modify/RunPartOWorkflow.cs`,
`Modify/PartOHubOutcome.cs`; tests `PartOIteration3JourneyTests.cs` (new), `PartOIteration3RoomBindingTests.cs`
(fixture builder made `internal`), `PartOHubOutcomeTests.cs` (the pinned notes wording).
Replace-existing-results blocker: `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOutputOccupancy.cs` (new) and `PartOOutputPaths.cs`
(`Occupancy`, `TryReplaceGenerated`, `CaseOfScenario`; `TryClaimRun` now asks `Occupancy`, same rule); `WPF/.../Windows/PartOReplaceResultsWindow.xaml(.cs)`
(new), `Modify/ConfirmReplacePartOResults.cs` (new), `Modify/RunPartOWorkflow.cs`, `Modify/Simulate.cs`, `Modify/PartOIteration3.cs`;
tests `PartOReplaceExistingResultsTests.cs` (new) and one test in `PartODesignModelProtectionTests.cs`.

## Validation

- Release build 0 errors. Full `SAM.Analytical.UI.WPF.Tests`: **2531/2531** pass (2501 before; +30). New: `PartOReplaceExistingResultsTests`
  (no results / marker only / same run - nothing asked; another run - confirm and cancel, cancel changes nothing, confirming alone
  deletes nothing; what a replacement removes and keeps; the claim afterwards; a locked file; the scenario names its folder; the
  window's words) and an end-to-end test through the production simulation core in a second session
  (`A_new_session_is_refused_the_folder_of_an_earlier_run_unless_the_replacement_was_confirmed`). `git diff --check` clean.
  The first run of the suite failed 3 room-binding tests, which is what led to limiting the automatic choice to
  missing steps: an invalid saved room must keep Run off for the method that is wrong.
- Licensed real-app walk (UI Automation, Direct T3D on, `C:\TasOut\direct-parto\it3\p3`): cleaned copy of the model ->
  Iteration 1a -> Hub auto-selects Route check with the notice and steps -> **Iteration 3 v** scrolls the panel into
  view -> the Mixed Design button opens Mixed Design -> MVHR + cooling + a room per dwelling, Save -> Hub reopens with
  Direct T3D kept -> Iteration 2 -> Iteration 3 opens on the default method with no notice or refusal, runs to
  completion; `.t3d` in Iteration1a/2/3, no `.xml`. The collapsed header showed "Direct T3D" after a UIA toggle (it
  did not before).
- The new outcome line (`MixedDesignSavedOutcome`) was added after that walk and is covered by a unit test only.
- Second real-app run on the owner's own model (`Direct-partO-dwellings1.json`, which carries results; Direct T3D on): Iteration 2
  ran straight through to TM59 with no message box; the Hub showed the checklist with **Remove Results...** on step 1 and the
  Route check selected; **Run Iteration 3** ran the Route check (heading "Route check", completed) instead of refusing.

- Third real-app run (Direct T3D on, one output folder `C:\TasOut\direct-parto\it3\p5`, the owner's model, UI Automation): a fresh folder ran with
  no question; a **new session** into the same folder showed the Replace window **before the Review**; **Cancel** left the folder
  byte-identical; **Replace then Cancel at the Review** also left it identical; **Replace then Accept** completed Iteration 2 in the
  same folder (stale file gone, a non-SAM `my-notes.txt` kept, design file hash unchanged, `.t3d` present, no `.xml`, no numbered
  folder). Iteration 3 (Route check) in another new session showed the same window with its own wording and, after Replace,
  completed with no further message box (the autopilot logs any box). The output folder held only `Iteration2` and `Iteration3`.

## Investigation: reopening the saved result model (no feature built)

**Does opening `<output>\Iteration2\tas\<name>.sam` restore the completed run? Yes**, verified in the real app. The Hub says
"Saved Iteration 2 results reopened - ready to review"; Review Results is enabled and reproduces the TM59 verdict (FAIL, 3 pass / 2 fail /
1 not assessed) with no simulation (only a TSD document server starts, to read the results); Iteration 3 is enabled, names the reference
"Iteration 2 ... reopened from the saved run, so it does not need to be run again" and lists the Route check result saved earlier
("Result available ... reference FAIL / system FAIL"). **Not restored or limited:** Prepare & Run is disabled ("This is a Part O result.
Part O cases run from a design model ... derived from the design model 'model1.json', which has changed since"); Optimise (2B) says "Needs a
live run"; the Scenario box shows Iteration 1a although the reference is Iteration 2; the Hub's Simulation case header shows no Direct T3D
(the case is re-created, not restored) although the restored run was Direct.

**What exists that could match a saved run to its design and case:**
- the output folder's `PartOCase.json` marker: case folder, the run's Guid, and the TAS case key (weather identity | solar method | `T3D=Direct`);
- the `<name>.partorun.json` sidecar (`PartORunResume:v2`): iteration, whether a catalogue was offered, dwelling-zone and ventilation-system Guids, the
  TAS case (solar method, days, `DirectT3D`), the TSD's length and timestamp and the prepared model's fingerprint - a resume is refused if either changed;
- the result `.sam`: `SimulationResultProvenance` (model fingerprint, TSD length and timestamp) and **`PartOBaselineReference`**, which records the
  design the result came from - its model Guid, name, relative path and a fingerprint (FNV-1a over the cluster, material and profile libraries,
  location and the model's parameter sets, less provenance, scenarios and view settings);
- `Analytical.Query.PartOModelResolution` already finds the design by that relative path and judges Resolved / Changed / Unknown by recomputing the fingerprint.

**How SAM can tell the design changed - and why today's check cannot be used as it is.** The mechanism exists (the fingerprint above), but it
reports "changed" for a design that was not touched: for the owner's unmodified file the recorded fingerprint (`675bd9cf...`) and the file's
current one (`15fc1cc5...`) differ. The cause, found with a probe over the saved result and the file: the Hub writes a Part O Equipment Selection onto
the open design with **new Guids every session** (selection `38cb489f...` in the file vs `4d42acc8...` in the result, and every product reference),
and that parameter is part of the fingerprint. So the same design gives a different fingerprint each time it is run, and any "previous run
still current?" test built on it would always answer "stale".

**Proposed smallest UX (not implemented):** when Prepare & Run is pressed and the case folder holds a run of the same case, ask once, before the
Replace window: **"Previous compatible run found (Iteration 2, 7 Oct 16:46) - [Reopen previous run] / [Run again]"**. Reopen restores the
run exactly as File > Open of the saved `.sam` does (nothing is simulated); Run again goes on to the Replace window above. "Compatible" must mean all of:
(1) same case folder and scenario; (2) the marker's TAS case key equals the Hub's current one (weather, solar method, Direct T3D); (3) the sidecar is valid
(TSD length and timestamp unchanged, prepared-model fingerprint unchanged); (4) the design is unchanged by a **stable** design key. Rule 4 needs one
decision before any build: compute the design key without the per-session Guids - exclude `Part O Equipment Selection` (and the other Part O inputs
written by the Hub) from the design fingerprint and compare their meaning (mode, product Model/Reference, project test unit values, dwelling strategies)
separately - and record the new key in `PartOBaselineReference` (a SAM change, so a separate PR; old results without it are simply "not matched").
Until rule 4 is settled the Hub should say nothing rather than offer a match it cannot defend.

## Unresolved issues, risks

- The Direct-vs-gbXML numerical comparison of Iteration 3 (RMS about 3.4 K, max about 16 K on this model) is
  deliberately out of scope; investigate separately only if those values are judged suspicious.
- Seen in the walk, not changed: the modal "Model successfuly converted" box (`Modify/Simulate.cs`) with NV
  pre-simulation warnings on MVHR runs; Mixed Design's multi-selection collapsing to one dwelling after the first
  bulk action.

## Next step

Review and merge; then the `PROJECT_PROGRESS.md` closeout commit on `sow/2026-Q4` with the merge SHA.
