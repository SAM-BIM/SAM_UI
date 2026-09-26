# Part O UX pass 6 - final consistency and end-to-end acceptance (26 Sep 2026)

Branch `feature/parto-pass6-acceptance-2026-09-26` from `sow/2026-Q3` `7e7de03` (#121 merged; Part O baseline
`5a0b9bf6` + #120/#121). SAM `sow/2026-Q3` `22f9c743` (rebuilt here; needed by #121's PDF report, not by Part O).
Real app: `SAM_UI\build\SAM Analytical.exe` (Release), real TAS, UI Automation drivers (real window messages).

Not a redesign. Engineering logic, `CanOptimise`, stop rules, cancellation points, provenance, saved-run format and
TM59 rules are unchanged.

## Routes reviewed

| Route | How | Result |
|---|---|---|
| Fresh Hub (Iteration 2) | live, 2B driver | Optimise (2B)… disabled "After an Iteration 2 run"; no 2B expander; Run is the one next action |
| Prepare & Run → Review → TAS → **Cancel** | live, before and after the fix | see *Cancellation* |
| Iteration 2 Prepare & Run → Review → TAS (48 s) → TM59 → Hub | live | "✕ Iteration 2 completed — TM59 FAIL" / "TAS simulation 48s · 8 spaces assessed · 2 pass · 6 fail · 1 not assessed" (04) |
| Start Iteration 2B: pre-fill, invalid step, Cancel | live | defaults, then "Pre-filled from the settings last used in this session"; Cancel ran nothing and left the Hub line (05) |
| Iteration 2B (round limit set to 3, not 10) → result → Hub | live | one Part O window, "Optimisation rounds · round 2", no total, no % (06); stop IterationLimitReached; result and Hub agree (07, 08) |
| Second 2B from the kept design | live | ran to its round limit (files continue `-Opt04..06`); the driver's 15 s cancel delay was longer than a round (~14 s after SAM#142), so 2B Cancel was **not** re-exercised live - Pass 5's live run and `PartOProgressConsistencyTests` cover it |
| Save → restart → reopen (v2 sidecar) | live | "○ Saved Iteration 2 results reopened — ready to review"; no rounds, stop reason or envelope shown; Optimise (2B)… "Needs a live run" on Hub and ribbon (09) |
| Reopened no-sidecar 1a run → Review Results → TM59 → Hub | live | progress "MVHR iteration (1a or 2, not recorded by this saved run)" - not guessed; TM59 FAIL, counts identical in window and Hub (03) |
| Iteration 3 Open result → comparison → Hub | live | 4 stages, "It cannot be cancelled", no % |
| Iteration 1b, Iteration 3 run | code review + existing tests | no live TAS: already proven in earlier passes, no code on those paths changed |

## Inconsistencies found and fixed (presentation only)

1. **Progress window reappeared after "Simulation cancelled"** (known since Pass 4). `PrepareAndRun` re-showed the
   host after the attention box unconditionally, then disposed it. Now it is shown again only where the TM59 stage
   still follows (`partORun.CanAssess`). Proved in place on the base build first:
   - before (`live/journey-cancel-before.txt`): `answer OK` → `PROGRESS 'Part O' #1` (window back) → Hub;
   - after (`live/journey-cancel-after.txt`): `answer OK` → Hub, no progress window.
2. **Iteration 2B result window had no height ceiling** - opening Engineering detail (fixed 380 px tabs) could push
   Copy All / Close below the taskbar. Now the Start window's pattern: `MaxHeight = WorkArea × 0.92`, content in a
   ScrollViewer, buttons outside it (07, live UIA shows the `ScrollViewer` pane).
3. **Iteration 3 said "Pass" / "Fail" / "Undefined"** (tiles, Hub Iteration 3 row, Hub outcome line) where every
   other surface says PASS / FAIL / NOT ASSESSED. One helper, `Query.PartOVerdictText`. A saved Iteration 3 record
   with no status still reads "—" (unknown, not "not assessed"). Nothing re-decided: the status is SAM's.
4. **Iteration 3 "TM59 report" opened the TM59 window under an empty verdict band and empty facts box.** The Iteration
   3 assessment carries report text only, and no verdict may be parsed from text, so the band and facts box are now
   drawn only once a summary is given. Its summary line also lost "space(s)" and the sentence-case verdict.
5. **Twin-surface wording.** Prepare Iteration's 2B pre-set said "Maximum iterations" / "between iterations"; its twin
   (Start Iteration 2B) says "Round limit" / "between rounds" - aligned. Its dwelling count said "dwelling(s)"; the
   Hub's twin uses the counted noun - aligned. Step strip "Optimise 2B" → "Optimise (2B)" (the button's name).
6. **Hub glossary** said Iteration 3 compares against "a completed Iteration 1a run"; the panel and eligibility accept
   1a or 2 - glossary corrected.
7. **Nine Part O message boxes had an empty title bar** - now "Part O — Preparation / Review iteration / Prepare
   Iteration / Iteration 2B / Prepare & Run", like the existing "Part O — Prepare & Run" / "Part O — Iteration 3".
   Ribbon tooltip "Part O - Prepare & Run" → em dash; Iteration 3 "Copy all" → "Copy All".

## Deliberately left unchanged (recorded, not Pass 6)

- **Iteration 3 refusal pane wording** ("REFUSED at …", "Candidate B", "Reference A" in ledger reasons). The text is
  built from the pipeline's ledger and is also persisted in the Iteration 3 report JSON (`Refusal`), so rewording it
  is not presentation-only. Follow-up.
- **2B "Run" vs "round"**: grids and facts count runs (0 = baseline, MAX = envelope), which is not the same as rounds;
  "production TM59 status" and "MAX" in Engineering detail are dense on purpose.
- **l/s vs L/s**: every Part O window says `l/s`; SAM#143 moved the *reporting* symbol to `L/s` (Space Assumptions
  PDF). One symbol needs a deliberate cross-repo decision (SAM's own Part O presentation test pins `l/s`).
- "(s)" plurals left in the Iteration 3 window and equipment control; the Iteration 3 window, Prepare Iteration window
  and progress window do not merge `PartOStyles.xaml` (current tooltips are short); "Technical details" (Iteration 3)
  vs "Engineering detail" (2B) vs Hub "Advanced"; minor IsDefault gaps.
- **2B cancel followed by a refusal** reports the refusal (band says Cancel was requested). Engineering precedence kept.
- Generic `RunPartOSimulation` host-less "Preparing Model" fallback (max 8 / 4 steps): still unreachable from Part O.
- 2B round history is session-only (by design since Pass 5); output-path portability; `tas3d.exe` crashes on exit
  (Windows logs ~100 in 3 days on this machine, results unaffected - TAS, not SAM_UI).

## Large projects

Per-space grids virtualise (Review window, Iteration 3 comparison with grouping, 2B histories, dwelling lists);
diagnostics and Engineering detail start collapsed; the Hub summarises counts; no per-space workflow choice; 2B
history is bounded by the round limit. No change needed.

## Communal corridor

`PartOTM59CorridorReportingTests` (2) pass: classification is SAM's exact InternalCondition
`TM59_Communal Corridor`, never a space name.

## Validation

- `SAM_UI.sln` Release (VS 18 MSBuild): 0 errors.
- `SAM.Analytical.UI.WPF.Tests`: **1243/1243** (1237 + 6 in `PartOFinalConsistencyTests`; one Iteration 3 pin updated
  to PASS/FAIL in `PartOWorkflowSimplificationTests`).
- Coverage still present: Review intent (`PartOReviewIterationTests`), Hub outcomes (`PartOHubOutcomeTests`),
  reopened scenario authority (`PartORunResumeNamingTests`), TM59 summary (`PartOTM59ResultTests`), shared progress and
  truthful Cancel (`PartOProgressConsistencyTests`), 2B entry and stop reasons (`PartOIteration2BJourneyTests`),
  corridor (`PartOTM59CorridorReportingTests`), model growth (`PartORunModelGrowthTests`).
- `git diff --check` clean.

## Files

`01`-`09` PNG captures (PrintWindow); `live/` driver logs (`.txt`) and two UIA dumps. Drivers (this machine only):
`C:\TasOut\parto-pass6-2026-09-26\scripts\progress.ps1` (Pass 4 driver, parts `cancel` / `review`) and
`scripts-2b\journey2b.ps1` (Pass 5 driver + `-Rounds 3`). The 2B run's log stops after Accept (log writes failed
mid-run); its UIA dumps and files show the whole journey, and the key dumps are copied into `live/`.
