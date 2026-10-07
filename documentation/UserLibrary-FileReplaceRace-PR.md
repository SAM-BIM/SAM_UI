# User library file-replace race - PR record

Branch `fix/user-construction-file-replace-race`, base `sow/2026-Q4` @ `a74938ff`. Not merged. `PROJECT_PROGRESS.md` is not
changed on this branch (closeout after merge, per `AGENTS.md`).

## Current status

Code, tests and evidence complete; PR open for owner review. See "Next step".

## Problem

Intermittent failure of
`UserConstructionCandidateTests.Saved_constructions_follow_the_existing_target_window_made_for_notes_and_the_row_limit`:
`Constructions.json could not be written: Unable to remove the file to be replaced.` Seen 1/15 and 3/15 in isolation on
2026-10-07 and occasionally in full-suite runs; independent of PR5.

## Root cause (evidence)

1. **No in-process reader holds the file.** `ConstructionAlternatives` reads the library synchronously during `Refresh`
   (through `UserLibraryFile.Read`, which opens with `FileShare.ReadWrite | FileShare.Delete` and disposes the stream),
   unsubscribes from `Changed` in `Dispose`, and its background tasks only evaluate U-values (the fake evaluator never
   touches the file). During the 35-save loop no `Setup` exists. Hypothesis "internal/background reader" is not supported.
2. **Stream lifetime / sharing in SAM_UI is correct.** The temporary file is written by `File.WriteAllText` (closed), the
   lock file is a separate path, and readers allow delete sharing. Hypothesis "wrong SAM stream lifetime" is not supported.
3. **Reproduced with no SAM code and no reader at all.** A standalone replica of `UserLibraryFile.Write` (temp file +
   `File.Replace(tmp, path, path.bak, true)`, 40 saves per fresh `%TEMP%` folder, 4000 swaps per run):
   - with a `.bak`: **28/4000** refused, all `IOException 0x80070497` (ERROR_UNABLE_TO_REMOVE_REPLACED);
   - without a backup: 1/4000;
   - with a bounded retry: **0/4000**; 29 refusals recovered, at most 3 attempts and 53 ms.
4. **What Windows refuses (probe).** `File.Replace` fails with `0x80070497` whenever **any** handle is open on the existing
   `.bak` - even one sharing read, write and delete - and with `0x80070020` (sharing violation) when the target is open
   without delete sharing; a delete-sharing handle on the target is fine.
5. **Who holds it.** Each save rotates the previous library file to `.bak`; that file was closed milliseconds earlier.
   Defender real-time protection (`MsMpEng`) and Endpoint DLP (`SenseDlpProcessor`, `MpDlpService`) are active on this
   machine and open freshly written files. Hypothesis "Defender/indexing" is supported (security software was not changed
   to prove it further; the probe shows any such handle is sufficient).

**Verdict: product defect, environment-triggered.** A real user on a scanned machine who saves, renames or removes
constructions or glazing systems in quick succession can hit the same error (the file is then left unchanged and a retry
by hand works). Not a test-only problem.

## Fix

`WPF/SAM.Analytical.UI.WPF/Classes/UserLibrary/UserLibraryFile.cs` (`Write`), the engine shared by "My constructions",
the glazing library and the removed-archive companion:

- the swap (`File.Replace`, or `File.Move` when there is no file yet) is retried only for the momentary-hold codes:
  `0x80070020` sharing, `0x80070021` lock, `0x80070497` UNABLE_TO_REMOVE_REPLACED, `0x80070498` UNABLE_TO_MOVE_REPLACEMENT,
  `0x80070499` UNABLE_TO_MOVE_REPLACEMENT_2 (after which the target is missing and the next attempt's `Move` restores it);
- at most `ReplaceAttempts = 5` attempts, waits 25/50/100/200 ms (< 0.4 s in all), under the existing lock;
- anything else (access denied, path not found, disk full, a plain IOException) is reported at once;
- exhausted retries keep the OS message and add the attempt count and the likely cause:
  `... could not be written: Unable to remove the file to be replaced. (still refused after 5 attempts; another program
  may be holding Constructions.json or Constructions.json.bak open)`;
- the temporary file is still always removed; the file is unchanged on failure. That includes a **partial swap**
  (`0x80070499`: Windows has already moved the previous file to `.bak` and the new one is still the temporary file):
  the next attempt moves the new file in; if no attempt does (exhaustion or another failure), the previous file is
  copied back from `.bak` before the error is reported, and if even that fails the error says it is kept as `.bak`
  (Codex review P1 on the first head `4a88290`).
- Test-only seam `BeforeReplace(attempt)` (same pattern as the existing `BeforeWrite`), copied to companions. No public API
  change.

Lifetime/sharing fixes (preferred order 1-3) do not apply: the holder is another process.

## Tests

`WPF/SAM.Analytical.UI.WPF.Tests/UserLibraryFileTests.cs` (+14 cases). The seam opens the real `.bak`/target, so each
refusal is a genuine Windows `0x80070497`/`0x80070020`, deterministic:

- `.bak` held for attempt 1 -> attempts `[1, 2]`, write lands, previous file is the `.bak`, no temp/lock file;
- target held without delete sharing for attempts 1-2 -> `[1, 2, 3]`, write lands;
- `.bak` held throughout -> exactly 5 attempts, < 2 s, message with reason and count, file and `.bak` unchanged, no
  temp/lock; the next save succeeds;
- a non-transient IOException -> 1 attempt, original message;
- transient-code classification (9 codes); companion shares the seam.

Partial swap (seam reproduces what `0x80070499` leaves): next attempt completes the write with the previous file as
`.bak`; never completed -> previous file back, retry message; followed by another failure -> previous file back, that
message; cannot be put back (`.bak` held exclusively) -> "The previous ... could not be put back (...); it is kept as
....bak", the `.bak` intact.

Mutations: `ReplaceAttempts = 1` (old behaviour) -> 3 tests fail; "retry everything" -> 5 tests fail; no restore after a
partial swap -> 3 tests fail. All reverted.

## Validation

- Release `MSBuild SAM_UI.sln /t:Rebuild` (VS 18; `APPDATA`/`USERPROFILE` redirected to scratch, `NUGET_PACKAGES` real
  cache): exit 0, 0 errors.
- Isolated stress, fresh `dotnet test --no-build --filter FullyQualifiedName~UserConstructionCandidateTests` process per run:
  - **before** (base binary): **25/30 passed, 5 failed**, every failure the target test with the exact message;
  - **after** (first head `4a88290`): **50/50 passed**; **after the partial-swap fix**: a second 50-run, **50/50 passed**.
- User-library tests (`UserLibraryFileTests|UserConstruction|UserGlazing`): 190/190 on `4a88290`; `UserLibraryFileTests`
  41/41 after the partial-swap fix.
- Full suite `dotnet test SAM_UI.sln -c Release --no-build`: **2635/2635 passed** on the final code (2617 before + 18
  new; 2631/2631 on `4a88290`). Release Rebuild repeated on the final code: 0 errors. The
  redirected profile was seeded with a read-only copy of `%APPDATA%\SAM\resources` and `Documents\SAM\resources`; a
  first run without them had 23 environment-only failures (default libraries missing: NRE in gbXML export / Part O).
- `git diff --check`: clean.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/Classes/UserLibrary/UserLibraryFile.cs`
- `WPF/SAM.Analytical.UI.WPF.Tests/UserLibraryFileTests.cs`
- `documentation/UserLibrary-FileReplaceRace-PR.md` (this record)

## Risks / unresolved

- A save can take up to ~0.4 s longer when another program keeps the file; beyond that it fails as before, with a clearer
  message.
- A scanner that holds the `.bak` longer than 0.4 s still produces the (now explained) error; the user can save again.
- Codex review: P1 (partial swap could leave no target on exhaustion) fixed as above; thread answered.
- `UserGlazingLibrary`'s doc comment cites `File.Replace(string, string, string)`; unchanged (still accurate in substance).

## Next step

Owner review of the PR; after merge, the post-merge `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` with the merge SHA.
