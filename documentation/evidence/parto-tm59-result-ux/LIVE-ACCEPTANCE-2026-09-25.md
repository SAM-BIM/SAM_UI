# Part O UX pass 2 - TM59 result window - live acceptance (25 Sep 2026)

Branch `feature/parto-tm59-result-ux-2026-09-25`, built from `sow/2026-Q3` `d123f3d` (PR #113 merged).
The exe is `SAM_UI\build\SAM Analytical.exe`, from the Release build of this branch (WPF dll 2026-09-25 18:03).

## What was run

- **No TAS simulation was started.** The run is a copy of the 25 Sep 1a smoke run, reopened from its saved
  results: `C:\TasOut\parto-journey-review-2026-09-25\reopen`, copied with `cp -p` to
  `C:\TasOut\parto-tm59-result-ux-2026-09-25\reopen`. The copy's TSD is the smoke run's
  (`C:\TasOut\parto-workflow-smoke-2026-09-25\run\...-It1a-futureZ1.tsd`).
- The driver is `C:\TasOut\parto-tm59-result-ux-2026-09-25\scripts\tm59.ps1` (outside git). It is the journey-review
  driver with a TM59 step, using UI Automation and PrintWindow. Log: `journey-tm59.log` beside it.
- It covers two entry points:
  1. Hub > Review Results > TM59 window > Show details > Close > the Hub's outcome line.
  2. Ribbon Results > Part O TM59 > the same window.

## Result

| Check | Observed |
|---|---|
| Verdict | `✕ TM59 assessment — FAIL`: glyph, word and a red rule, so it is not colour alone |
| Counts | `8 spaces assessed · 2 pass · 6 fail · 1 not assessed` (Corridor_1, no overheating scenario) |
| Agreement with the report | The report's own table: 2 PASS rows, 6 FAIL rows, 1 space under SPACES NOT ASSESSED; `TM59 OCCUPIED-SPACE ASSESSMENT: FAIL` |
| Context | Scenario "Iteration 1a — MVHR design duty (no manufacturer unit)" · Route MVHR · Whole building · weather Z1_DSY1_2050s_HIGH90_CIBSE_v1.1 · results `.tsd` (full path on tooltip) · report saved · CIBSE TM59:2017 · TM52 Category II |
| Not assessed | "1 reason", collapsed. Show details opens the Corridor_1 sentence (the UIA toggle works) |
| Detailed report | 5074 characters, identical to the saved `-TM59.txt` apart from a trailing newline |
| Hub line | "Reviewed the TM59 results · Fail · no simulation was run", the same wording as before |
| Message boxes | 0 |
| TAS processes | None. The driver's count of 1 was `taskhostw.exe`, started 09:18, which its `^TAS` regex matched |

**Report bytes.** Reviewing a run rewrites its TM59 report at the recorded absolute path (the journey-review
trap), here the smoke run's `...-It1a-futureZ1-TM59.txt`. The rewritten file had the **same SHA-256**
(`9ef0c77e…b7bf5`) as the backup taken before, so the TM59 output is unchanged by this work. The file's original
time (07:54:16) was then restored from the `cp -p` backup in `...\backup\`.

## Screenshots (this folder)

| File | What |
|---|---|
| `before-tm59.png` | The window before this pass: paths first, FAIL on report line 7 (journey review, 25 Sep) |
| `after-tm59.png` | This pass, from the Hub: the verdict band, counts, context, collapsed reasons, the report below |
| `after-tm59-details.png` | The same, with Show details on |
| `after-tm59-ribbon.png` | The ribbon entry point: the same window |
| `after-hub-after-review.png` | The Hub's outcome line after Close |

## Not covered live, and why

- The **PASS**, **NOT ASSESSED** (a pass over part of the dwelling scope, or no occupied verdict) and **UNAVAILABLE**
  (missing, stale or unreadable results) states. No saved Part O run produces them without a new TAS run. The Hub
  and ribbon keep Review disabled while results are not assessable, so UNAVAILABLE is only reachable through a race.
  Each state is covered by `PartOTM59ResultTests` against real `TM59AssessmentReport` objects and the real window.
- Prepare & Run's closing line ("· TM59 Fail") uses the same word; it was not re-run live because that needs TAS.
- Non-100 % DPI, as in the earlier passes.

## Round 2 (owner correction: the per-space status moves into SAM)

The space counts are now a tally of SAM's `TM59AssessmentReport.OccupiedSpaces[].ComplianceStatus` (SAM-BIM/SAM#137),
and the report formatter prints the same value. The same driver was re-run on the same saved FAIL run, against the
rebuilt SAM and SAM_UI (round 1 captures and log kept as `shots-tm59-round1`, `journey-tm59-round1.log`):
- heading, counts, facts and Hub line are identical to round 1: FAIL, `8 spaces assessed · 2 pass · 6 fail · 1 not assessed`;
- 0 message boxes, no TAS;
- the report it rewrote had the same SHA-256 again (`9ef0c77e…b7bf5`), and its original time was restored.
