# Part O journey review (observe only, 25 Sep 2026)

This review walked the four Part O user cases in the real `SAM_UI\build\SAM Analytical.exe` (WPF DLL 15:57, the merged
SAM-BIM/SAM_UI#111 code at `90b42e0`). It used the existing UI Automation + PrintWindow approach. The purpose is to decide
whether the whole journey can be simpler, as input to the next presentation passes. Iteration 2B is treated as a follow-on
of Iteration 2.

**No code was changed and no TAS simulation was started.**
- The driver was `C:\TasOut\parto-journey-review-2026-09-25\scripts\journey.ps1`. It is outside git and reuses the 25 Sep
  helpers.
- Prepare & Run was taken up to "Review iteration" and declined there (Cancel), which happens before any TAS work.
- On the reopened run, Iteration 3 "Run again" was answered No. "Run system case" is never pressed by the driver.
- Result: 38 live captures, 30 with a UIA text dump beside them. There were 0 unexpected message boxes, and no TAS
  process started.

The full report (journey maps, window inventory, ranked findings, proposed journey) is a private artifact:
https://claude.ai/artifact/CV9V9fjuMwjuRZaij5yUd1. The substance is repeated below so that it survives without the link.

## What was observed

**Live, on this build:**
- The Hub on fresh 1a, 1b and 2, with the 2B expander and Advanced.
- "Review iteration" for 1a, 1b and 2, each declined.
- The Hub after the decline.
- The legacy Edit › Prepare iteration picker.
- The reopened 1a smoke run (`C:\TasOut\parto-workflow-smoke-2026-09-25\run`):
  - the Hub;
  - Review Results → Overheating (TM59);
  - Iteration 3 Open result → progress → comparison, with its Technical details;
  - the Run again confirmation (answered No);
  - the pre-flight for all four methods.
- Ribbon enabled states and tooltips.

**From earlier evidence or code, because these states exist only after TAS:**
- The TAS progress during Prepare & Run and during Iteration 3 (25 Sep).
- The TM59 window for 1b and 2. It is the same window; the 1b report text is from 16 Sep.
- The 2B result window (15 Sep). Its code has not changed since 8 Sep.
- The 2B per-round progress and the Hub after 2B (code).
- The post-run "attention" box and the SAM Check LogWindow (code).

**Side effect.** Reviewing a copied run rewrites its TM59 and Iteration 3 review reports at the run's recorded absolute
paths, which is the original smoke folder. The bytes were identical; the original file times were restored.

## Journeys today

- **1a:**
  1. Ribbon, then the Hub. Press Prepare & Run.
  2. Progress (prepare). It hides for the review.
  3. **Review iteration.** OK starts TAS.
  4. Progress (TAS → TM59).
  5. An attention box, only if there are notes.
  6. **Overheating (TM59)**, then Close.
  7. The Hub, with its outcome line.

  That is 3 decisions. The reopened route is the Hub, then Review Results, then about 10 s with **no progress window**,
  then TM59.
- **1b:** the same as 1a. Review iteration shows an empty equipment table with 4 disabled controls, and the warning
  and the note are the same paragraph.
- **2:**
  1. The Hub, with the equipment section open by default and a three-sentence route line.
  2. The 2B choice, which must be ticked **before** preparing, inside a collapsed expander.
  3. Review iteration, with products and headroom. Its summary heads with "Iteration 1a — …".
  4. TAS, then TM59, then the Hub.
  5. Optimise (2B). Each round opens its own generic progress window (not `PartOProgressHost`).
  6. The 2B result window.
  7. The Hub, with **no outcome line**.
- **3:**
  1. A completed mechanical reference case is needed.
  2. The Hub's Iteration 3 panel: method, status and pre-flight.
  3. A Yes/No replace box, only if a result exists.
  4. Progress: 6 named stages, about 6.7 min. This is the reference pattern.
  5. Iteration 3 comparison, then the Hub line.

## Findings

**High:**
- **H1.** Review iteration's unlabelled **OK starts the full-year TAS run**, and Cancel leaves no Hub line.
- **H2.** **Scenario identity changes between screens.** The Iteration 2 review summary begins "Iteration 1a — MVHR
  design duty (no manufacturer unit)", which is the engine iteration. The 1a review mentions catalogue products.
- **H3.** **The TM59 verdict is buried.** The window opens on paths; "FAIL" is line 7 of the monospace report; the
  counts are only in prose.
- **H4.** **2B is outside the Part O language:**
  - a hidden pre-prepare prerequisite;
  - no Part O progress (`OptimisePartOTM59.cs:304` → `RunPartOSimulation` without the host);
  - a repeated paragraph and 863 note lines open in the result window;
  - no Hub outcome (`RunPartOWorkflow.cs:188`);
  - the reopened-run refusal only in a tooltip.

**Medium:**
- **M1.** Review diagnostics are open and dominate the window. There are 8 near-identical warnings.
- **M2.** Equipment controls are drawn on 1a and 1b, where they cannot apply.
- **M3.** The same dwelling is "MVHR 1" in the 1a review and "Flat 1" elsewhere.
- **M4.** On a reopened run, the row reads "Model check ○ Waiting" while the strip reads "Check & simulate ✓ Done".
  "Simulation ✓ Ready" uses the word for a met prerequisite for a completed stage.
- **M5.** Review Results has no progress window.
- **M6.** Only the Hub uses `PartOStyles.xaml`. The consequential button is never primary elsewhere, and the button
  rows differ.
- **M7.** The Hub is 922–949 px tall and scrolls on Iteration 2 and on reopened runs with Iteration 3.
- **M8.** The attention message box is the last modal on the normal route.
- **M9.** The Iteration 3 panel shows its explanation and a long red refusal paragraph before either is asked for.

**Minor:**
- **m1.** "(s)" plurals remain in the Review, TM59, Iteration 3 and 2B windows.
- **m2.** The Iteration 3 method radio buttons have an empty UIA Name.
- **m3.** Duplicate text in 1b and 2B.
- **m4.** Paths are shown first in the TM59 window.
- **m5.** A large, nearly empty "Spaces not assessed" box.
- **m6.** "WHOLE BUILDING" in capitals as the first line of the review.
- **m7.** The A/B wording in the Iteration 3 Technical details. This is verbatim, and acceptable per the owner's
  decision.
- **m8.** Review stays as Next after a review. This is correct under the no-UI-state rule.
- **m9.** The legacy picker's wording.
- **m10.** The Iteration 3 confirmation is a Win32 box. It is fine.

## Proposal (not implemented)

**Journey.** The Hub stays the anchor, and every action returns to it with one outcome line. Long work always uses
`PartOProgressHost`. Every result window opens with a summary, then the engineering tables, then the full report and
diagnostics. The normal route is three clicks: Prepare & Run → Accept and run TAS → Close.

**Detail layers.**
- Level 0: state and next action.
- Level 1: Why? / Show details.
- Level 2: Advanced / Technical details, verbatim.

**State language.** Always a glyph with a word:
- ✓ Ready/Done/Pass
- ● Running/Next
- ○ Not yet
- ! Needs attention
- ✕ Blocked/Fail
- – N/A/Not assessed

"Ready" is never used for something finished.

**Shared pieces:**
- Extend `PartOStyles.xaml` with SecondaryButton, WindowHeader, SummaryTile, VerdictBadge and a common button row.
- Use the read-only `PartOWorkflowStepStripControl` in the Review and TM59 windows.
- Use `PartOProgressHost` everywhere.
- A new `PartOResultSummary`, extracted from the Iteration 3 tiles, for TM59, 2B and Iteration 3.
- A new `PartODiagnosticsPanel`: collapsed and grouped, with every line and Copy all.
- A `PartOWorkflowOutcome` line for every action.

**Guardrails.**
- Summaries bind to the same `TM59AssessmentResult` / `TM59ComplianceStatus` / not-assessed objects the reports are
  written from, and nothing is re-derived.
- No UI-only state, and colour is never used alone.
- Engineering, validation, provenance, scenarios and simulation are unchanged. Dense tables stay dense.

**Order:** small presentation-only PRs, each with a live acceptance pass.
1. Review iteration: H1, H2, M1–M3.
2. TM59 summary and Review progress: H3, M5, m4, m5.
3. 2B in the Part O language: H4. This one needs one short TAS acceptance run: Iteration 2, about 1 min, plus 2B,
   about 10–12 min.
4. Hub tidy and vocabulary: M4, M7, M9, m2.
5. Attention box to inline: M8.
6. Consistency sweep: M6, m1, m3, m6, m7, m9.

## Screenshots in this folder

| File | What it shows |
|---|---|
| `hub-1a-fresh.png` | Hub, fresh Iteration 1a |
| `hub-2-fresh.png` | Hub, Iteration 2, equipment section open by default |
| `hub-2-2b-open.png` | Hub, 2B expander |
| `review-1a.png` | Review iteration, 1a |
| `review-1b.png` | Review iteration, 1b |
| `review-2.png` | Review iteration, Iteration 2 |
| `hub-after-decline.png` | Hub after Cancel on the review: no outcome line |
| `hub-reopened.png` | Hub, reopened 1a run with Iteration 3 |
| `tm59.png` | Overheating (TM59) |
| `hub-after-review.png` | Hub with its outcome line |
| `progress-it3-open.png` | Progress while opening a saved Iteration 3 result |
| `it3-comparison.png` | Iteration 3 comparison |
| `it3-confirm.png` | Iteration 3 replace confirmation |
| `it3-refusal.png` | Iteration 3 pre-flight refusal |
| `legacy-picker.png` | Edit › Prepare iteration |
