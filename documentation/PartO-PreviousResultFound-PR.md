<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Part O: "Previous result found" (7 Oct 2026)

**Status:** code, automated tests and a licensed real-app acceptance on Direct T3D are complete; awaiting PR CI and review.
Branch `feature/part-o-previous-result-found` from `sow/2026-Q4` `a437975a`. Builds against SAM `sow/2026-Q4` `f391e022` (`PartODesignKey`, SAM#184).

SAM_UI only. `PartODesignKey`, Direct T3D, TAS, TM59, the #207 replacement semantics and Iteration 3 are not touched.

## Why

SAM#184 records a stable semantic `DesignKey` in a Part O result's baseline reference. The guide still said "Automatic 'previous run found'
from the design model is not available yet": a person with a finished run had to know to open `<case>\tas\<name>.sam` by hand, or press
Prepare & Run and be asked only whether to replace it.

## What changed

Pressing **Prepare & Run** on the design model with an output folder whose case already holds a saved result of **this same design** now shows
**Previous *case* result found** (`PartOPreviousResultWindow`) before anything else:

- **Open previous result** (default) opens `<case>\tas\<name>.sam` through the existing File > Open path (`open` delegate; the same one Remove Results
  uses), so the existing `PartORun.Restore` reconnects the run for review. TAS is not run. The workflow then ends (the open model was replaced).
- **Run again** continues unchanged into the #207 **Replace existing results** question; choosing it removes nothing.
- **Cancel** (or closing the window) does nothing.

Compatibility is decided by SAM, not by the UI (`Modify.FindPreviousPartOResult`): the saved result's recorded
`PartOBaselineReference.Design.DesignKey` must equal `Query.PartODesignKey` of the open design, and the result must still validate for review
(`SimulationResultProvenance.TryResolvePath_TSD`), so an offer is never one the reopen would refuse. Not offered: a result with no recorded key (saved
before SAM#184 - still openable by hand), another design, a result derived from another result (2B / Iteration 3), an optimisation round (`-Opt*`), or
a result whose results file has changed. No second fingerprint exists in SAM_UI. The (costly) design key is computed only when a candidate `.sam`
beside a `.tsd` exists; at most four are opened.

Where the host cannot open a model (`open` null) the step is skipped and the run goes on as before.

## Decisions and assumptions

- Only **Prepare & Run** (Iterations 1a, 1b, 2). Iteration 3 keeps its own **Open result** / replace confirmation; Mixed Design is unchanged.
- The offer is by key only (no guid test): the key includes the cluster identities, so a different project cannot share it.
- Opening replaces the design in the window with the result, as File > Open does; File > Open does not ask about unsaved changes, and neither does this.
- Open failure (`open` returns false) is a Hub line, not a dialog; the run is not started.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/Modify/OfferPreviousPartOResult.cs` (new) - finder, decision, `HandlePreviousPartOResult`.
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOPreviousResultWindow.xaml(.cs)` (new) - the three-button question.
- `WPF/SAM.Analytical.UI.WPF/Modify/RunPartOWorkflow.cs` - `PrepareAndRun` asks first; `OutputPathsOf` extracted; the workflow returns when the result was opened.
- `WPF/SAM.Analytical.UI.WPF.Tests/PartOPreviousResultFoundTests.cs` (new) - 11 tests on real `.sam` result models.
- `documentation/user-guides/Part-O-Prepare-and-Run.md` - **Previous result found** replaces "not available yet"; troubleshooting row. The Wiki page must be re-published after merge.

## Validation

- `MSBuild` Release of the test project and of `SAM Analytical.csproj`: no errors, no warnings from the new files.
- New tests: 11/11 pass. They cover matching key offered; different key, legacy no-key result, result-of-result, `-Opt01`/`-OptMax` and rewritten results
  file not offered; Open opens the saved `.sam` and leaves every file untouched; Open that fails says so; Run again continues into
  `ResolveExistingPartOResults` (which still asks, answer Replace) with nothing changed by choosing it; Cancel changes nothing; no opener skips the step.
- Full `SAM.Analytical.UI.WPF.Tests`: 2542 passed, 1 failed (`ThermalSourceTests.A_source_with_nothing_to_offer_says_why_and_adds_no_candidates`,
  "Reading panes.tcd..." status race under full-suite load; passes 21/21 when the class runs alone; not touched by this change).
- **Real-app acceptance** (build of this branch, `Direct-partO-dwellings.json` copy, Direct T3D on, Iteration 1a, output root beside the model):
  1. First run (no result in the folder): no prompt; Review > TAS > TM59 as before; result `.sam`/`.tsd` written.
  2. App closed, reopened on the plain design: Prepare & Run showed **Previous Iteration 1a result found** (path, saved time, three buttons).
  3. **Open previous result**: Hub then read "Saved Iteration 1a results reopened - ready to review", Review Results enabled, Prepare & Run disabled;
     a 250 ms poller saw no TBD/TAS3D/TPD/TWD/TCD/T3D process; the design `.json` and the `.tsd` were byte-identical afterwards.
  4. App reopened on the design again: prompt again; **Run again** showed the #207 **Replace existing results** window ("It holds 8 generated files");
     **Replace** removed and rewrote the case (new `.tsd`/`.sam` timestamps) and the run reached the TM59 window.
  Not walked in the real app: Cancel and a legacy no-key result (both covered by tests).

## Risks

- Offering reads the candidate result `.sam` and hashes the open design on each Prepare & Run press where a result exists (seconds on a large model).
- A result saved before SAM#184 is never offered; the guide says to open it by hand.

## Next step

CI and review; after merge, add the closeout to `PROJECT_PROGRESS.md` on `sow/2026-Q4` and re-publish the Wiki with
`documentation/publish-part-o-wiki.ps1`.
