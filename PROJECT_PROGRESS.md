# Project Progress

## Branch
`feature/partf-terminal-transfer-compliance` (PR #75, head 2a0d480)

## Last updated
2026-08-25

## Current status
Three OPEN Codex review comments on PR #75 addressed and covered by tests.
All 187 tests in `WPF/SAM.Analytical.UI.WPF.Tests` pass (182 baseline + 5 new).
Committed and pushed to `origin/feature/partf-terminal-transfer-compliance`; final
Codex review and CI pass requested.

## Completed
- OPEN COMMENT 1 (P1): `Recorded` in `Modify/AddVentilationByPartF.cs` now treats
  `Notes`, `ConfirmedBy` and `ConfirmationDate` as recorded evidence, so a withdrawn
  (or notes-only) confirmation always reaches `PersistConfirmations` and is stored as
  `NotAssessed` with its supporting fields retained. Existing NotAssessed behaviour
  untouched.
- OPEN COMMENT 2 (P2): `PartFAirflowRenderer.Load` no longer clears
  `textObstacle2Ds` on a non-geometry (camera-only / attribute-only) update. New
  internal static seam `ResolveTextObstacles(geometryObjectModel, plane, previous)`
  keeps the previous obstacles when no replacement geometry was supplied and
  re-measures them from the supplied geometry when one was. The early-return branch
  (null cluster/plane) still clears them. The alternative call-site fix (passing the
  viewport's geometry model at AnalyticalWindow.xaml.cs) was deliberately NOT made.
- OPEN COMMENT 3 (P2): `Button_Report_Click` and `Button_CopyAll_Click` in
  `PartFAssessmentWindow.xaml.cs` now call `ApplyRows()` before building the report,
  so pending grid edits are committed (and statuses re-resolved) first.
- Tests added (5):
  - `PartFConfirmationsTests.NotesOnlyCheck_IsPersistedAsNotAssessed`
  - `PartFConfirmationsTests.PersonAndDateOnlyCheck_IsPersistedAsNotAssessed`
  - `PartFAirflowObstacleTests.NullGeometry_KeepsThePreviousLoadsObstacles` (new file)
  - `PartFAirflowObstacleTests.ValidGeometry_ReplacesThePreviousLoadsObstacles` (new file)
  - `PartFReportRowEditTests.Report_ShowsTheValueAppliedFromARowEdit` (new file)
- Verified the three earlier findings fixed in fcf0ec8 remain fixed (dwelling-switch
  ApplyRows, NotAssessed persistence for withdrawals, scope-filtered dwelling selector).

## Decisions / assumptions
- For OPEN COMMENT 2 the renderer-side preserve-obstacles approach was chosen
  (review comment's second option) because it is unit-testable in the headless test
  project; the window-level call-site alternative has no test seam.
- Task 2 read-only review findings (report only, no changes):
  - PartFAssessmentCache invariant holds: keyed on model instance; Reload always
    reads a fresh clone from UIAnalyticalModel (asserted by tests). Cache retains the
    last model instance strongly (bounded, not a leak).
  - Doc mismatch in `PartFAnnotationOverride`: it claims overrides key on "the
    annotated object's own guid... NOT the space guid and a role" and that two
    terminals of one role may share a space; the code actually derives keys via
    PartFAnnotationKey (space+role) and the calculator builds at most one terminal
    per role per space. Behaviour is consistent; only the doc is stale.
  - FloorPlan2DControl overlay hit-test (skip overlay subtree) is correct; overlays
    never swallow selection clicks.
  - Observation: UpdatePartFAirflow -> renderer.Load re-sections spaces on every
    UpdateTabItem, including camera-only/attribute-only updates, contrary to Load's
    documented contract. Deterministic, so no visual change, just wasted work.
  - Minor: TextObstacle2Ds measures text with a 96-dpi System.Drawing.Font while
    FloorPlan2DControl draws labels with FormattedText at the control's DPI; boxes
    can differ slightly from drawn labels on scaled displays.

## Files changed
- WPF/SAM.Analytical.UI.WPF/Modify/AddVentilationByPartF.cs
- WPF/SAM.Analytical.UI.WPF/Controls/PartFAirflowRenderer.cs
- WPF/SAM.Analytical.UI.WPF/Windows/PartFAssessmentWindow.xaml.cs
- WPF/SAM.Analytical.UI.WPF.Tests/PartFConfirmationsTests.cs
- WPF/SAM.Analytical.UI.WPF.Tests/PartFAirflowObstacleTests.cs (new)
- WPF/SAM.Analytical.UI.WPF.Tests/PartFReportRowEditTests.cs (new)

## Validation
- Built dependency chain on this machine first: SAM.Core.Excel (VS MSBuild, Release),
  SAM.Core.Mollier / SAM.Geometry.Mollier / SAM.Analytical.Mollier,
  SAM.Core.Windows, SAM.Geometry.Solver / SAM.Analytical.Solver,
  SAM.Analytical.GEM, SAM.Analytical.LadybugTools (all dotnet build, Debug).
  These are build outputs only; no source changes in those repos.
- `dotnet build WPF/SAM.Analytical.UI.WPF.Tests/SAM.Analytical.UI.WPF.Tests.csproj -c Debug` — succeeded.
- `dotnet test ... -c Debug --no-build` — Passed: 187, Failed: 0, Skipped: 0.
- `git diff` inspected: only the four intended source files + two new test files.

## Issues / blockers
- None. The two Task 2 documentation findings (PartFAnnotationOverride doc mismatch,
  DPI text measurement) are noted for a future cleanup, not required for this PR.

## Next step
- Await the Codex review and CI results on the pushed head. Address any new
  findings, then merge PR #75.
