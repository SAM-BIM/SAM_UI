# Part O UX pass 4 — shared progress window: live acceptance (25 Sep 2026)

Real executable (`build\SAM Analytical.exe`, Release), driven through UI Automation. Captures are PrintWindow
(the window's own pixels). Driver, outside git: `C:\TasOut\parto-progress-2026-09-25\scripts\progress.ps1`.
Every route ran on a fresh copy of an existing saved artifact, so no earlier run's files were touched.

## Before (base `sow/2026-Q3` 91aeb43, this branch's changes stashed)

- **Review Results** (`journey-review-before.log`): a generic `Part O TM59` box flashed. It was gone before a
  capture could land (0 px), and UI Automation read no text from it. Its code set "Assessing..." only *after* the
  assessment had finished.
- **Iteration 3 Open result**: already the shared Part O window (`before-iteration3-review.png`):
  "1s elapsed", with no note at all, because the note was shown only on cancellable windows.

## After (this branch)

| Route | How | What was confirmed |
|---|---|---|
| A. Review Results (no TAS) | reopened 1a smoke run | "Checking TM59 results"; one stage "● TM59 assessment" with a running time; detail line; "Elapsed 5s"; indeterminate bar; "No percentage is shown: this step does not report one. It cannot be cancelled."; no Cancel button. TM59 window then FAIL 8/2/6/1, identical to before. `after-review-results.png` |
| B. Prepare & Run, TAS started then cancelled | fresh model, Iteration 1a | ✓ Prepare and review · ● TAS simulation (full year) · ○ TM59 assessment; detail "Opening TBD file"; "Elapsed 20s"; indeterminate bar; TAS note. Cancel at +23 s in the TAS stage: button "Cancelling…" (disabled), note "Cancel requested. It stops at the next safe point between steps; a TAS step already running … finishes first, which can take minutes." Stopped about 11 s later at the next step boundary ("Opening T3D file"), then the existing "Simulation cancelled" box, then the Hub line "○ Iteration 1a TAS run cancelled — no results were produced / The prepared iteration is kept, so it can be run again". TAS3D exited; no TAS process was left. `after-prepare-run-tas.png`, `after-prepare-run-cancelling.png`, `after-hub-after-cancel.png` |
| C. Iteration 3 Open result (no TAS) | reopened run, saved MG result | Same shared window. It finished in about 3 s, too fast to capture reliably on this branch. The Hub line was unchanged: "○ Opened the saved Iteration 3 result · … (3s)" |
| C'. Iteration 3 run, mid-TAS | real window, synthetic state (env-gated test) | 6 stages; ✓/●/○ hierarchy; "Elapsed 9m 14s"; TAS note; Cancel. `after-iteration3-running-rendered.png` |
| D. Iteration 2B round | real window, synthetic state (env-gated test) | "Iteration 2B — TM59 optimisation"; "The limit is 10 rounds; how many run depends on the results."; ✓ Assess the Iteration 2 results · ● Optimisation rounds · round 2 · ○ Capacity envelope (diagnostic); no round total. `after-iteration2B-round-rendered.png` |

UI Automation reads each stage row as one line, with its status in words: "Completed: Prepare and review the
iteration", "Running now: TAS simulation (full year)", "Upcoming: TM59 assessment". The duration is read
separately, once.

## Not run live, and why

- **Iteration 2B.** No saved completed Iteration 2 run exists (every saved sidecar is 1a), and 2B cannot start
  without one. Producing one needs a full-year TAS run, then a round of another, just to take a screenshot, which
  the brief rules out. D uses the production window, stage list and subheading through the test seam instead.
- **A full Iteration 3 run.** That is several full TAS simulations. C' uses the production window and stage
  names.
