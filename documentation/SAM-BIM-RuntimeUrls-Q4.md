# SAM-BIM runtime URLs (Q4) - SAM_UI

Branch `fix/sam-bim-runtime-urls-q4` -> base `sow/2026-Q4`. Record date: 2026-10-06.

## Current status

PR open, **not merged**. Source-only, minimum change: the Mollier form's help link opened the HoareLea wiki and now
opens the SAM-BIM wiki. No `.gitmodules`, gitlink, workflow, `master`, `sow/2026-Q3` or icon-redesign change.

## Work completed

`WPF/SAM.Core.Mollier.UI.WPF/Forms/MollierForm.xaml.cs`: `https://github.com/HoareLea/SAM_Mollier/wiki/HomeUI` -> `https://github.com/SAM-BIM/SAM_Mollier/wiki/HomeUI`.

## Decisions and assumptions

- SAM-BIM is the authoritative ecosystem; HoareLea is no longer the synchronised operational source.
- `SAM-BIM/SAM_Mollier.wiki` holds the same `HomeUI` page. This repository already links to `https://github.com/SAM-BIM/SAM_UI/wiki`
  (`AnalyticalWindow.xaml.cs`), so the new link follows the existing convention.
- Only the destination changed; no label or tooltip names HoareLea.

## Files changed

`MollierForm.xaml.cs` plus this record. One product line changed.

## Validation

- `git diff --check` clean; diff reviewed (1 insertion, 1 deletion in product code).
- `msbuild SAM_UI.sln -p:Configuration=Release` with `APPDATA`/`USERPROFILE` redirected: 0 errors.
- The compiled string in `SAM.Core.Mollier.UI.WPF.dll` was not byte-checked (see the report); the source diff is the evidence.
- Not run locally: Rhino/WPF UI tests. PR CI results are recorded in the PR description.

## Unresolved issues, risks

- None introduced by this change.

## Exact next step

Maintainer review and merge into `sow/2026-Q4` once CI is green (not merged automatically). After merge, the
`PROJECT_PROGRESS.md` closeout is a direct docs commit on `sow/2026-Q4` (never on this branch).
