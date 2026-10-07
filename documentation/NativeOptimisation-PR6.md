# Native Optimisation in SAM_UI - PR6 record (consumer side)

Branch `feature/native-optimisation-pr6-retire-legacy`, base `sow/2026-Q4` @ `a74938ff`. Part of the Java-free GenOpt
PR6 set; the full record (classified inventory, decisions, deployment follow-up) is SAM_Tas
`SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_GENOPT_PR6.md`. **Depends on the SAM_Tas PR6 PR (merge it first).** CI
clones SAM_Tas at this branch name until then. `PROJECT_PROGRESS.md` is not changed here.

## Status

Code, tests and evidence complete; PR open for owner review. Not merged.

## What changed

- `TasOptimisationReport` reads its rules from SAM_Tas `NativeGenOptOutcome` (success, withholding after a cancel, best
  point, interval, refusal wording); its own `Best` and refusal mapping are removed. Headline, status, lines and their
  order are unchanged; the stale/missing-assembly message still comes first (`Message`), and the shared call sits in a
  non-inlined method so that branch works with a stale `SAM.Analytical.Tas.GenOpt.dll`.
- `TasOptimisationProgressState` keeps "Lowest so far" with `NativeGenOptOutcome.IsLower` (same rule).
- `Query.TasOptimisationAssemblyFailure`'s probe also reaches `NativeGenOptOutcome`, so a pre-PR6
  `SAM.Analytical.Tas.GenOpt.dll` is reported as a load failure when the window opens, not mid-run.
- Comments: "Java-incompatible setting" -> "a setting GenOpt itself would not accept"; GPSCoordinateSearch "refused as
  unsupported". The window still says "Java GenOpt is not used" (true).
- Tests: report tests use `NativeGenOptOutcome.Best` and check the report's best point equals it; the metadata test also
  requires `NativeGenOptOutcome` (`.ctor`, `IsLower`, `RefusalMessage`); a stale-GenOpt load failure is reported as such.

## Files changed

`WPF/SAM.Analytical.UI.WPF/Classes/TasOptimisation/{TasOptimisationReport,TasOptimisationProgressState,
TasOptimisationDefinition,TasOptimisationInput}.cs`, `Query/TasOptimisationAssemblies.cs`; tests
`TasOptimisationReportTests.cs`, `TasOptimisationWindowTests.cs`; this record.

## Validation (code head `3b84213`, against the PR6 SAM_Tas build `31ae7ef`)

- Release `MSBuild SAM_UI.sln /t:Rebuild` (VS 18; `APPDATA`/`USERPROFILE` redirected to a scratch profile seeded with a
  read-only copy of `%APPDATA%\SAM\resources` and `Documents\SAM\resources`; real `NUGET_PACKAGES`): 0 errors.
- `--filter FullyQualifiedName~TasOptimisation`: **75/75** (74 before + 1 new).
- Full suite `dotnet test SAM_UI.sln -c Release --no-build`: **2616/2618**. Both failures are unrelated, pre-existing
  flakes outside the optimisation code:
  - `UserConstructionCandidateTests.Saved_constructions_follow_..._row_limit` - the user-library file-replace race,
    fixed separately in SAM-BIM/SAM_UI#211 (this branch is deliberately not based on it);
  - `ThermalSourceTests.A_source_with_nothing_to_offer_says_why_and_adds_no_candidates` - status text read before it
    updated under full-suite load; the class passed 10/10 when run alone; flagged as a separate task.
- PR CI `spdx` green; `build` see the PR. `git diff --check` clean; SPDX headers present.
- **Licensed acceptance not rerun:** report output and native execution are unchanged; PR5 licensed acceptance
  (GoldenSection 11/11 and GPSHookeJeeves 16/16 bit-identical to PR3, cancel, no Java/cmd/registry) stands.

## Risks

- Needs SAM_Tas PR6 merged first. SAM_UI now needs a PR6 `SAM.Analytical.Tas.GenOpt.dll` beside the app (already the
  SAM_Deploy shipping follow-up); an older one is reported by the readiness probe when the window opens.

## Next step

Owner review; merge after SAM_Tas PR6; then the post-merge `PROJECT_PROGRESS.md` closeout.
