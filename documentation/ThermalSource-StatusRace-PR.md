# Thermal source status race - PR record

Branch `fix/thermal-source-status-race`, base `sow/2026-Q4` @ `245045f`. Not merged. `PROJECT_PROGRESS.md` is not
changed on this branch (closeout after merge, per `AGENTS.md`).

## Current status

Code, tests and evidence complete; PR open for owner review. See "Next step".

## Problem

Intermittent failure of `ThermalSourceTests.A_source_with_nothing_to_offer_says_why_and_adds_no_candidates`, seen once on
2026-10-07 in a loaded full-suite run (2616/2618): `Assert.StartsWith() Failure: String: "Reading panes.tcd…" Expected
start: "panes.tcd contains 3 panes"`, right after `Assert.Equal(ThermalSourceState.Ready, entry.State)` passed. The class
passed 10/10 on its own.

## Root cause

`ThermalSourceCatalog.LoadAsync` passes the reader a `Progress<string>` whose callback did
`if (entry.State == Loading) entry.Message = x;`. The outcome was then published in separate steps with no lock:
`SetSource` (raise), `Message = note` (raise), `State = Ready` (raise).

`Progress<T>` posts each report to the `SynchronizationContext` captured when the read started. With no UI context (the
test runner; any caller off the dispatcher), it runs on a pool thread, concurrently with the publication. If the callback
checks `State` between `Message = note` and `State = Ready`, it then writes the progress text over the note. Once
`State` is `Ready`, `StatusText` returns `message`, so the entry shows "Reading panes.tcd…" permanently. This is not a
read that comes too early: the test reads after the outcome, and the value is wrong for good.

**Verdict: product defect.** The comment on the guard ("a late report must not overwrite the outcome") states the
intent, but a check-then-write without a lock cannot keep it across threads. In the panel today, reads are started from
the UI thread, so the dispatcher serialises the report and the outcome. The user-visible glitch is therefore latent
there, and real for any off-dispatcher caller. The non-atomic publication also raised notifications showing a half-set
outcome (pool present, state still `Loading`).

## Fix

`WPF/SAM.Analytical.UI.WPF/Classes/Thermal/ThermalSourceCatalog.cs`, `ThermalSourceEntry` only:

- a private `sync` lock guards `state`, `message`, `source` and the counts;
- each step of a read is one method that sets everything under the lock and raises once afterwards (never inside the
  lock): `Begin()` (Loading, "Loading…"), `Report(text)` (checks `Loading` **and** writes under the same lock, otherwise
  drops the report), `Fail(reason)`, `Complete(source)` (counts taken outside the lock, then pool, counts, note and
  `Ready` set together);
- getters (`State`, `Message`, `Source`, counts, `HasContent`, `StatusText`) read under the lock, so `StatusText` is a
  consistent snapshot;
- the catalog calls `Begin`/`Report`/`Fail`/`Complete`; behaviour and texts are unchanged. Removed: the `internal`
  setters of `State`/`Message` and `internal SetSource` (no other users). The public surface is unchanged.

The existing test is unchanged (no sleep, no weaker assertion): after the fix, a report either lands before the outcome
(and is replaced by it) or after (and is dropped), so it is deterministic.

## Tests

`WPF/SAM.Analytical.UI.WPF.Tests/ThermalSourceTests.cs`, new
`A_progress_report_that_arrives_while_or_after_the_outcome_is_published_never_replaces_it` (deterministic, no timing):
a queueing `SynchronizationContext` holds every post (progress reports and the read's continuations) and the test
delivers them at each notification the entry raises. The reader is held on a gate. The test asserts that:

- while reading, the progress text shows;
- no notification shows a half-published outcome (pool present while `Loading`);
- from the first `Ready` notification on, the line is the note;
- a report delivered after the outcome is dropped.

**Pre-fix proof:** with the base `ThermalSourceCatalog.cs` and the new test, the class gave 21/22. The new test failed
with the observed sequence `(Loading, no pool, "Reading panes.tcd…")`, `(Loading, pool, "Reading panes.tcd…")`,
`(Loading, pool, note)`, `(Ready, pool, note)`. The 3rd notification is exactly the window in which the flaky report
overwrote the note. With the fix: 22/22.

Not separately pinned by a test: the lock shared by `Report`'s check and write. Hitting it needs a real thread interleaving
inside the callback; it is covered by the stress below and by the reasoning above.

## Validation

All runs used Release builds from VS 18 `MSBuild`, with `APPDATA`/`USERPROFILE` redirected to a scratch profile. That
profile was seeded with copies of `%APPDATA%\SAM\resources` and `Documents\SAM\resources`. `NUGET_PACKAGES` pointed at
the real cache. The real `%APPDATA%\SAM` was not touched.

- `MSBuild SAM_UI.sln -restore -p:Configuration=Release`: exit 0, 0 errors (only existing warnings).
- Stress, sequential: `dotnet test --no-build --filter FullyQualifiedName~ThermalSourceTests`, one fresh process per run:
  **40/40 passed** (22/22 each).
- Stress under load: 8 rounds of 4 such processes in parallel: **32/32 passed**.
- Full suite `dotnet test SAM_UI.sln -c Release --no-build`: **2637/2637 passed** (6 min).
- `git diff --check`: clean.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/Classes/Thermal/ThermalSourceCatalog.cs`
- `WPF/SAM.Analytical.UI.WPF.Tests/ThermalSourceTests.cs`
- `documentation/ThermalSource-StatusRace-PR.md` (this record)

## Risks / unresolved

- `PropertyChanged` is still raised on the thread that publishes (as before); WPF bindings marshal it. Nothing is raised
  inside the lock, so a handler that reads the entry cannot deadlock.
- The lock costs nothing that can be measured (a handful of uncontended locks per read and per binding read).

## Next step

Owner review of the PR; after merge, the post-merge `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` with the merge SHA.
