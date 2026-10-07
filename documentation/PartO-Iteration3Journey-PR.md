<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Part O Hub: the Iteration 3 journey (7 Oct 2026)

**Status:** code, automated tests and a licensed real-app walk of the whole journey are complete; awaiting PR CI and
review. Branch `feature/part-o-iteration3-journey` from `sow/2026-Q4` `45ab491c`.

Presentation and routing only. Direct T3D, the simulation engine, TAS, TM59, the Iteration 3 pipeline and the
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
(fixture builder made `internal`).

## Validation

- Release build 0 errors. Full `SAM.Analytical.UI.WPF.Tests`: 2513/2513 pass (2501 before; +12). `git diff --check` clean.
  The first run of the suite failed 3 room-binding tests, which is what led to limiting the automatic choice to
  missing steps: an invalid saved room must keep Run off for the method that is wrong.
- Licensed real-app walk (UI Automation, Direct T3D on, `C:\TasOut\direct-parto\it3\p3`): cleaned copy of the model ->
  Iteration 1a -> Hub auto-selects Route check with the notice and steps -> **Iteration 3 v** scrolls the panel into
  view -> the Mixed Design button opens Mixed Design -> MVHR + cooling + a room per dwelling, Save -> Hub reopens with
  Direct T3D kept -> Iteration 2 -> Iteration 3 opens on the default method with no notice or refusal, runs to
  completion; `.t3d` in Iteration1a/2/3, no `.xml`. The collapsed header showed "Direct T3D" after a UIA toggle (it
  did not before).
- The new outcome line (`MixedDesignSavedOutcome`) was added after that walk and is covered by a unit test only.

## Unresolved issues, risks

- The Direct-vs-gbXML numerical comparison of Iteration 3 (RMS about 3.4 K, max about 16 K on this model) is
  deliberately out of scope; investigate separately only if those values are judged suspicious.
- Seen in the walk, not changed: the modal "Model successfuly converted" box (`Modify/Simulate.cs`) with NV
  pre-simulation warnings on MVHR runs; Mixed Design's multi-selection collapsing to one dwelling after the first
  bulk action.

## Next step

Review and merge; then the `PROJECT_PROGRESS.md` closeout commit on `sow/2026-Q4` with the merge SHA.
