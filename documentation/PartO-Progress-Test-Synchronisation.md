<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Part O progress window test synchronisation

Record date: 2026-10-11. Branch `feature/part-o-progress-test-synchronisation`, based on
`sow/2026-Q4` at `e216dae751527089d9bc46be898d5bb75b111de5`. Separate from optimisation PR9
(SAM-BIM/SAM_UI#220); no PR9 branch, production code or `PROJECT_PROGRESS.md` changes.

## Cause and reproduction

`PartOProgressHost.Start` changes shared state; it does not synchronously render the window.
`PartOProgressWindow` renders on its own dispatcher with a 500 ms background timer, replacing
`itemsControl_Stages.ItemsSource` each time. UI Automation traversal and property reads are separate
calls and can overlap that replacement. Sleeping 1500 ms does not make the traversal atomic.
`Hide` and `Show` already marshal synchronously, so the intervening 500 ms sleep provides no guarantee.
The WPF collection serialises its member tests; this failure reproduces with only this test running.

On the unchanged Q4 test, the normal isolated run passed. For stress, temporarily replace the final
`string after = ProgressText();` with a loop of 1000 `ProgressText()` reads, each followed by the
original `Assert.Contains("TAS simulation (full year)", after)`. Keep the original sleeps and host
operations. Run the target test in Release. Two stress runs failed with the reported signature:

```text
Assert.Contains() Failure: Sub-string not found
String:    "Prepare & Run | ✓ | Completed: Prepare an"
Not found: "TAS simulation (full year)"
```

This reproduces partial observation during normal timer refreshes, without another WPF test or an
artificially shortened sleep. The temporary stress edits were reverted.

## Fix

Only `WPF/SAM.Analytical.UI.WPF.Tests/PartOWorkflowSimplificationTests.cs` and this record change.
Replace fixed sleeps with polling every 50 ms for up to 10 seconds. Require every expected string
in one UI Automation snapshot; never concatenate separate snapshots. Retry only the transient
`ElementNotAvailableException` raised when a discovered row has been replaced. Timeout reports
the expected text and last snapshot; permanent missing content still fails.

Keep the real host, desktop visibility assertion, UI Automation observation and Hide/Show cycle.
Strengthen the checks: before hiding, require running preparation, simulation name and elapsed text;
after showing, require completed preparation, running simulation and elapsed text. Pending simulation
text or stale preparation alone cannot satisfy the post-show check. No production render is forced.

## Validation

- Release target test: PASS.
- Same stress after the fix: 1000 calls to the post-show `WaitForProgressText`, PASS (10 s test duration),
  across normal timer refreshes. Temporary stress loop reverted.
- Missing-content mutation: rename the simulation stage in the host to `missing simulation row` while
  retaining the expected simulation text. FAIL after 10 seconds, with the last snapshot showing the
  missing row, preparation and elapsed time. Mutation reverted.
- Complete Release suite: pending at this checkpoint.
- `git diff --check`: PASS. Existing package compatibility and test-analyser warnings remain.

Commands: `dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c Release --no-restore`; for the target add
`--filter FullyQualifiedName~The_progress_window_keeps_its_content_after_standing_aside_for_a_dialog`.
Run with Windows desktop access. Local ignored logs: `progress-baseline-desktop.log`,
`progress-stress-before.log`, `progress-stress-detail.log`, `progress-fixed.log`,
`progress-stress-after.log`, `progress-mutation.log`, `progress-full.log`. TRX evidence is under the
test project's ignored `TestResults/` directory; these files do not travel with Git.

## Status and next step

Implementation complete; finish full-suite validation, push this branch and open a separate PR against
`SAM-BIM/SAM_UI:sow/2026-Q4`. Review final-head CI and obtain owner merge approval. Merge this fix
before PR9, then incorporate the merged Q4 change into PR9 and revalidate its final head. After this
PR merges, add the `PROJECT_PROGRESS.md` closeout directly to Q4 with its known merge SHA and
`[skip ci]`. No merge authorised by this task; no known production risk from the test-only change.
