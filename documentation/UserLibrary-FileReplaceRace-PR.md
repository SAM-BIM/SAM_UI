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
- the temporary file is still always removed; the file is unchanged on failure.
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

Mutations: `ReplaceAttempts = 1` (old behaviour) -> 3 tests fail; "retry everything" -> 5 tests fail. Both reverted.

## Validation

- Release `MSBuild SAM_UI.sln /t:Rebuild` (VS 18; `APPDATA`/`USERPROFILE` redirected to scratch, `NUGET_PACKAGES` real
  cache): exit 0, 0 errors.
- Isolated stress, fresh `dotnet test --no-build --filter FullyQualifiedName~UserConstructionCandidateTests` process per run:
  - **before** (base binary): **25/30 passed, 5 failed**, every failure the target test with the exact message;
  - **after**: **50/50 passed**.
- User-library tests (`UserLibraryFileTests|UserConstruction|UserGlazing`): 190/190.
- Full suite `dotnet test SAM_UI.sln -c Release --no-build`: **2631/2631 passed** (2617 before + 14 new), 7 min 53 s. The
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
- `UserGlazingLibrary`'s doc comment cites `File.Replace(string, string, string)`; unchanged (still accurate in substance).

## Next step

Owner review of the PR; after merge, the post-merge `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` with the merge SHA.
