# Part O — Prepare & Run Hub: presentation pass (25 Sep 2026)

These are before/after renders of `PartOWorkflowWindow` at 800 px width and full content height. No TAS was run.

They are produced by the opt-in test `PartOWorkflowHubScreenshotHarness`, which reads these environment variables:

- `SAM_PARTO_HUB_FRESH` — the unprepared source model:
  `C:\TasOut\parto-workflow-smoke-2026-09-25\run\SAM_zoningAM-CIBSEfutureZ1.sam`
  (the Nuaire acceptance model, sha `a7e09a25`)
- `SAM_PARTO_HUB_RUN` — the saved Iteration 1a smoke run in the same folder, reopened through `PartORun.Restore`
- `SAM_PARTO_SCREENSHOTS` — the output folder for the images

| State | Before | After |
|---|---|---|
| Fresh Iteration 1a (no run) | `before-1a-fresh.png` | `after-1a-fresh.png`, `after-1a-fresh-details.png` (Show details on) |
| Iteration 1a after its run (reopened; one Iteration 3 guidance record) | `before-1a-after-run.png` | `after-1a-after-run.png` |

The "after run" outcome line is supplied by the harness: it matches the line the live smoke test saw.
