# Prepare & Run Hub: live acceptance (25 Sep 2026)

The acceptance was run on the real `SAM_UI\build\SAM Analytical.exe` (WPF DLL built 14:33), driven through UI Automation. Window captures use PrintWindow, so they show the window's own pixels.

- The driver is `C:\TasOut\parto-hub-presentation-2026-09-25\scripts\accept.ps1`. It is not in git. It reuses the 25 Sep smoke helpers.
- The raw logs `accept-fresh.log` and `accept-reopen.log` are outside git (`*.log` is ignored), in `C:\TasOut\parto-hub-presentation-2026-09-25\`.
- No TAS run was started, no review was opened, and nothing was written beside either model.

## Fresh Iteration 1a

**Model and startup.** The model was a copy of `SAM_zoningAM-CIBSEfutureZ1.sam` (sha `a7e09a25`). The Hub opened with no message boxes, at 96 DPI.

**Route, scope and strip.**
- The route line reads "MVHR · Design duty only · No manufacturer unit required".
- The scope line reads "All 3 eligible dwellings · simulated inside the whole building".
- The strip reads ✓ Configure (Done) · 2 Prepare model (Next) · 3 Check & simulate · 4 Review.

**Status rows.** The Equipment row is not drawn on 1a.

| Stage | Glyph and label | Short line |
|---|---|---|
| Dwelling scope | ✓ Ready | 3 dwellings · 8 spaces |
| TM59 mapping | ✓ Ready | 8/8 spaces mapped |
| Part F requirements | ✓ Ready | 8/8 spaces defined |
| Ventilation design | ○ Not prepared | Built by Prepare & Run |
| Model check | ○ Waiting | — |
| Simulation | ○ Not run | — |
| Results | ○ Not available | — |

**Show details.**
- Turned on, the window grows from 553 to 707 px, and each row's complete sentence appears under it.
- A sentence that says no more than its short line is not repeated.
- Turned off, the window returns to 553 px.

**Simulation case.**
- It is collapsed on open.
- It expands to 670 px, showing the fields with the output folder, and collapses back.
- The header reads "— Z1_DSY1_2050s_HIGH90_CIBSE_v1.1 · TAS solar".
- The tooltip lists weather, solar calculation, output folder and "Full year".

**Actions.**
- Prepare & Run is blue and SemiBold, and enabled.
- Review is disabled, captioned "No results yet". Its tooltip gives the reason.
- Optimise is disabled, captioned "Iteration 2 only".

**Other scenarios.**
- Iteration 2: the equipment section, the 2B expander and the Optimise 2B strip step all appear. The Optimise caption reads "After an Iteration 2 run".
- Iteration 1b: the route line reads "NV · No mechanical system, unit or terminal". The Part F row reads "– N/A · Continuous mechanical rates are not applied on the natural-ventilation route."
- Iteration 3 panel: absent in 1a, 2 and 1b.

**Resize and monitors.**
- At the 700 px minimum width everything fits.
- At 1200 px wide the layout is clean.
- The window moved to DISPLAY1 (1920×1200) and back cleanly.
- The height follows content (SizeToContent).
- All three monitors are at 96 DPI. The system scale was not changed, so a non-100% scale was not tested.

## Iteration 1a after its run, reopened in a fresh process

**Model.** The model was the saved 25 Sep smoke run `000000_SAM_AnalyticalModel-It1a-futureZ1.sam`.

**Strip.** It reads ✓ Configure · 2 Prepare model · ✓ Check & simulate (Done) · 4 Review (Next).

**Ventilation design row.**
- It reads "○ Not prepared · Rebuilt for the next Prepare & Run · the existing results are reviewable as they are".
- The Prepare model step's tooltip says the loaded model is not prepared for this scenario and scope, so the next run rebuilds the design.

**Other rows.** Simulation and Results are ✓ Ready. The group heading reads "Existing run / results".

**Next step and actions.**
- The next-step line reads "Next: Review the TM59 results. Iteration 3 can then compare them with an explicit ventilation system (below)."
- Review is enabled, with no caption.

**Iteration 3.** The panel is shown, with the reference case named and the guidance result "✓ Result available".

## Fixed during this pass (presentation only)

- **Mixed-state wording.** The design row beside existing results now says the design is rebuilt for the next run. Before, it read "Built by Prepare & Run" next to "Check & simulate: Done", as though a simulation had run without a design.
- **Optimise tooltip on 1a/1b.** It now gives the scenario reason, using the same sentence the 2B section states. Before, it said "no results", which contradicted the "Iteration 2 only" caption.
- **Strip tooltips.** They did not appear, because hit-testing failed between the glyph and the label. A transparent background on each step fixes it.
- **Tooltip wrapping.** Tooltips ran on one line, up to about 1,435 px. They now wrap at 480 px, through an implicit style in `PartOStyles.xaml`.
- **Window off screen.** The Hub could open with its action row below the working area; a reopened run opened at y=208 with a height of 910 on a 1080 px display. It is now moved up to fit when it first renders, once, and never resized.
