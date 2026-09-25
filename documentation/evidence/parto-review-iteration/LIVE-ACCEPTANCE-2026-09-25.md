# Part O UX pass 1 — Review iteration window: live acceptance (25 Sep 2026)

Branch `feature/parto-review-iteration-2026-09-25`, from `sow/2026-Q3` `a3b38ee`. Real exe
(`SAM_UI\build\SAM Analytical.exe`), fresh copy of `SAM_zoningAM-CIBSEfutureZ1.sam` (3 dwellings, 8 spaces), driven by
UI Automation + PrintWindow. Driver: `C:\TasOut\parto-review-iteration-2026-09-25\scripts\review.ps1` (outside git;
adapted from the journey-review driver). Full log, outside git: `C:\TasOut\parto-review-iteration-2026-09-25\journey-prepare.log`.

**No TAS run was started.** Each route went Hub → Prepare & Run → Review iteration → captured → **Cancel**. The
production path after "Accept & Run TAS" is covered by `PartOReviewIterationTests.Accept_follows_the_production_adoption`
(adoption into the run, run Prepared, model replaced once); no seam exists that stops after acceptance but before the
solver, so it was not pressed live.

## Result

| Route | Heading | Diagnostics header | Lines behind "Show details" | Hub line after Cancel |
|---|---|---|---|---|
| 1a | Iteration 1a — MVHR design duty (no manufacturer unit) | Warnings (8) · Notes (38) | 46 (8 + 38) | ○ … review cancelled before TAS · no simulation was run and the model is unchanged |
| 1b | Iteration 1b — Natural ventilation (no mechanical system) | Warnings (1) · Notes (1) | 2 | same, for 1b |
| 2 | **Iteration 2 — MVHR with manufacturer unit** (was "Iteration 1a — …") | Warnings (8) · Notes (41) | 49 (8 + 41) | same, for 2 |

- 0 message boxes; no TAS process afterwards.
- Diagnostics collapsed by default on all three routes; the box holds every line (counts match the header).
- 1a: dwelling column reads Flat 1–3 (was MVHR 1–3); equipment authority controls not drawn.
- 1b: the empty 10-column table is replaced by one sentence; the space table takes the height.
- 2: Convert to Manual / Assign suggested / bulk row drawn and behave as before; all engineering columns intact.
- Window rects all inside the working area (bottom ≤ 916 of 1032).

## Defect found and fixed during the pass

The first run (`shots-prepare-run1`, not kept in git) showed "Show details" ticked through UI Automation with no box
shown: the switch listened to `Click`, which a UIA Toggle does not raise. It now listens to `Checked`/`Unchecked`;
regression `The_details_switch_answers_a_toggle_that_is_not_a_click`. The second run above is after the fix.

## Screenshots

| Before (journey review, #111 build) | After |
|---|---|
| `before-1a.png` | `after-1a.png`, `after-1a-details.png` |
| `before-1b.png` | `after-1b.png`, `after-1b-details.png` |
| `before-2.png` | `after-2.png`, `after-2-details.png` |
| `before-hub-after-decline.png` | `after-hub-after-decline.png` |

The "before" images are the observe-only journey review's captures of the same model on `90b42e0`; the Review window
and its orchestration were unchanged between that build and `a3b38ee`.

## Not covered live

- Safe positioning was not forced live (the OS cascade placed the window on-screen each time). The arithmetic is the
  Hub's tested `PartOWorkflowWindow.Placement`, now shared through `PartOWindowPlacement`.
- The summary tooltips (design duty, equipment) were read through UIA HelpText, not hovered; wrapping comes from the
  shared `PartOStyles.xaml` ToolTip style the window now merges.
- The isolated-scope detail line and a preparation with refusals (diagnostics open by default) are unit-tested only;
  this model produces neither.
- Non-100% DPI.

## Round 2 — contextual decision wording (Codex P1 on #113)

Codex found that the same window is opened by the legacy **Prepare Iteration** ribbon command, which adopts the model
and stops; it never starts TAS. "Accept & Run TAS" was a false promise there. The window now takes a
`PartOReviewIntent` from its caller. It changes wording only; neither command's behaviour changed.

| Entry point | Intent | Primary action | Caption |
|---|---|---|---|
| Prepare & Run Hub | `PrepareAndRun` | Accept & Run TAS | …starts the full-year TAS simulation, then the TM59 assessment… |
| Edit › Prepare Iteration | `PrepareOnly` (also the default) | Accept Preparation | Accept Preparation adopts this prepared model. No TAS simulation is started. Cancel changes nothing. |

**Live, legacy route** (`scripts\legacy.ps1`, log `journey-legacy.log`; both outside git):
- Ribbon → picker (project defaults: Iteration 2, catalogue) → OK → Review headed "Iteration 2 — MVHR with
  manufacturer unit". It showed **Accept Preparation**, the prepare-only subtitle and caption, and the tooltip
  "…Nothing is simulated."
- **Accepted.** SAM showed its "Reloading" window as the model was replaced; there was no message box.
- **Prepared and adopted.** The Results ribbon then read "A Part O iteration is prepared but not simulated. Run the
  energy simulation first." The Hub's Simulation row read "Prepared · waiting for the full-year TAS run", and its
  Results row read "prepared but has not been simulated".
- The Hub opens on its default scenario, 1a, so its Ventilation design row said "Prepared for a different scenario or
  scope". This is correct, because the legacy run prepared Iteration 2.
- **No TAS.** No TAS process was seen from acceptance to the end of the run. The driver's name filter also matched
  Windows' `taskhostw`, which is not TAS.

**Live, Hub routes re-run** after the change (1a, 1b, 2, each declined): all three still showed **Accept & Run TAS**
and the run caption. Each Cancel produced the Hub line. 0 message boxes.

Screenshots: `legacy-1-picker.png`, `legacy-2-review-accept-preparation.png`, `legacy-3-hub-after-accept.png`.
The Hub's "Accept & Run TAS" was not pressed live: it would start a full-year TAS run. It stays covered by the
production-path tests.
