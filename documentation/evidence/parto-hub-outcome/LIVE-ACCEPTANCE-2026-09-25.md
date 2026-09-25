# Part O UX pass 3 - Hub outcome line: live acceptance (25 Sep 2026)

Real `SAM Analytical.exe` from `SAM_UI.sln` Release on `feature/parto-hub-outcome-2026-09-25` (WPF dll 21:05), driven
by UI Automation with PrintWindow captures. **No TAS simulation was started**: 0 TAS processes in either flow and 0
message boxes. The driver is `C:\TasOut\parto-hub-outcome-2026-09-25\scripts\hub.ps1`, outside git, with logs
`journey-prepare.log` and `journey-reopen.log` beside it.

## Prepare flow (fresh model `SAM_zoningAM-CIBSEfutureZ1.sam`, copied with `cp -p`)

| Step | Hub line (glyph · headline · caption) | Before this pass |
|---|---|---|
| Fresh Hub, nothing done | *(no line)* | *(no line)* |
| Prepare & Run > Review iteration > **Cancel** | ○ Iteration 1a review cancelled — no simulation was run · Cancelled before TAS · the model is unchanged | "○ Iteration 1a — MVHR design duty (no manufacturer unit): review cancelled before TAS · no simulation was run and the model is unchanged" |
| Hub closed and **reopened** | *(no line)*: the cancellation was session-only and did not come back | *(no line)* |
| Legacy Edit > Prepare Iteration > **Accept Preparation**, then the Hub | ○ Iteration 2 prepared — waiting for the full-year TAS run · No simulation has been run for it yet | *(no line)*; only the Simulation row said "Prepared · waiting" |

The legacy route prepares Iteration 2 (catalogue offered), as #113's record found. The line names it from the run's
own `PreparationContext`, the same rule the Hub's reuse check uses. The Readiness rows agree: "Prepared for a
different scenario or scope" (the Hub is set to 1a), and the Simulation row reads "Prepared · waiting".

## Reopen flow (copy of the 25 Sep 1a smoke run; results on disk)

| Step | Hub line | Before this pass |
|---|---|---|
| Hub on the reopened run, nothing done | ○ Saved results reopened — ready to review · Review Results shows the TM59 verdict · no new simulation is needed | *(no line)* |
| … with Show details | + "Results: C:\TasOut\parto-workflow-smoke-2026-09-25\run\…-It1a-futureZ1.tsd" under the line | n/a |
| Review Results > TM59 window (✕ TM59 assessment — FAIL, 8 · 2 · 6 · 1) > Close | ✕ Saved results reviewed — TM59 FAIL · 8 spaces assessed · 2 pass · 6 fail · 1 not assessed · no simulation was run (red-tinted, with the glyph and the word) | "Reviewed the TM59 results · Fail · no simulation was run" (grey, no glyph) |
| Hub closed and **reopened** | ○ Saved results reopened — ready to review … (no verdict is carried into a new command) | *(no line)* |

The Hub's verdict and counts are the TM59 window's own: one `PartOTM59ResultSummary` instance.

**Report bytes.** Reviewing rewrote the smoke run's `…-It1a-futureZ1-TM59.txt` at its recorded absolute path (the
known trap). It had the **same SHA-256** before and after (`9ef0c77e…b7bf5`), and its original time (07:54:16) was
restored from the `cp -p` backup.

## Not exercised live (unit tests only; each needs a TAS run to produce)

- Prepare & Run completed PASS / FAIL, TAS cancelled, and not completed.
- The Iteration 2B stop reasons.
- Results gone after completion (a stale run).

`PartOHubOutcomeTests` covers each of these against the real `PartORun` transitions and `Modify.Capabilities`.

## Screenshots (this folder)

| File | What |
|---|---|
| `before-hub-after-decline.png` / `after-hub-after-decline.png` | Review cancelled: the #113 line, and this pass |
| `after-hub-reopened-after-decline.png` | The Hub reopened after the cancellation: no line |
| `before-hub-prepared.png` / `after-hub-prepared.png` | After legacy Accept Preparation: no line before, "Iteration 2 prepared — waiting…" now |
| `before-hub-reopened.png` / `after-hub-reopened.png` | Reopened completed run: no line before, "Saved results reopened — ready to review" now |
| `after-hub-reopened-details.png` | The same, with Show details: the results path under the line |
| `before-hub-after-review.png` / `after-hub-after-review.png` | After Review Results: the #114 line, and "✕ Saved results reviewed — TM59 FAIL" with counts |
| `after-hub-reopened-again.png` | The Hub reopened after the review: the run's own state again |

The before captures are from the #113 and #114 live passes on the pre-change build (same models, same driver
helpers).

## Follow-up in the same PR: reopened runs named as the run they were

A saved run's resume sidecar did not record whether a catalogue was offered. A resumed context carries no
capacity descriptors, so every reopened MVHR run read as Iteration 1a. A reopened **Iteration 2** run was
therefore called "Iteration 1a" by:
- the TM59 window's Scenario fact;
- Iteration 3's "Reference case".

This was reproduced by a throwaway test on the unfixed code (expected "Iteration 2 …", got "Iteration 1a …").

The sidecar is now `PartORunResume:v2` with `VentilationUnitCatalogueOffered`. v1 files still resume, but a v1 run is
not named.

Live re-run of the reopen flow on the same saved 1a smoke run. Its sidecar is v1, so this is the "cannot say" case:

| | Before | After |
|---|---|---|
| TM59 window facts | Scenario "Iteration 1a — MVHR design duty (no manufacturer unit)" · Route MVHR · … | Scenario and Route omitted; Thermal model, Weather, Results, Report saved, Method unchanged |
| Iteration 3 reference case | "Iteration 1a — baseline" | "MVHR iteration (1a or 2, not recorded by this saved run)" |
| Verdict, counts, Hub lines | FAIL 8/2/6/1 | unchanged |

- No TAS was started and no message boxes appeared.
- The TM59 report kept the same SHA-256, and its time was restored.
- The Iteration 3 files were untouched.

A run completed on this build writes v2 and is named 1a or 2 exactly (`PartORunResumeNamingTests`, end to end
through `PersistPartORunResume` and `Restore`).

Screenshots:
- `before-naming-tm59-v1-sidecar.png` (from the #114 pass);
- `after-naming-tm59-v1-sidecar.png`;
- `after-naming-hub-reopened-v1-sidecar.png` (the Iteration 3 reference line).
