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

## Validation, risks, next step

In the PR description (final numbers). No licensed acceptance rerun: presentation output and native execution are
unchanged; PR5 acceptance stands. Deployment: SAM_UI now needs a PR6 `SAM.Analytical.Tas.GenOpt.dll` beside the app
(already a SAM_Deploy follow-up). Next: owner review, merge after SAM_Tas PR6, then the post-merge closeout.
