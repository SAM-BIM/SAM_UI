# Project Instructions

This repository may be worked on by different AI agents,
models, sessions, accounts, and computers.

Git and committed repository files are the authoritative project state.
Do not rely on previous conversation memory.

## Branch context (2026-Q4)

- Active development branch: `sow/2026-Q4` (created 2026-10-06 from `master` `ab080134`).
- `sow/2026-Q3` is frozen. It is the preserved record of the final Q3 release (SAM_Deploy `v20261006.1`,
  deploy SHA `1bee804f`). Do not commit to, rebase, rename, archive or delete it.
- `master` is the product line. It does not carry `AGENTS.md` or `PROJECT_PROGRESS.md`; these internal files live on
  `sow/*` branches only. Never merge or restore them onto `master`.

## Before starting work

Before making significant changes:

1. Read this `AGENTS.md`.
2. Inspect the headings in `PROJECT_PROGRESS.md`, then read the latest
   dated Q4 closeout and the sections relevant to this task. Read the
   record document and live description of any PR you are continuing.
3. Understand the relevant existing code, architecture, tests, and conventions.
4. Review the current Git branch and working tree; preserve existing local changes.
5. Continue from the documented current state.

Prefer small, safe, targeted changes consistent with existing conventions.
Avoid unrelated refactoring unless it is required for the task.

## Repository operations and context

- Validate fetch and push URLs before network operations; target only
  `github.com/SAM-BIM/<repository>`. Use explicit SAM-BIM repository/API
  targets; avoid automatic fork-parent discovery. Do not contact HoareLea
  repositories or request their access/SAML authorization.
- Preserve historical attribution, legacy records and external integration
  references; changes to integrations require a separate migration decision.
- Treat dated bootstrap/Q2/Q3 entries as historical evidence. Check Git and
  live PR metadata before acting on old status/next-step text. Read earlier
  evidence when the task depends on it; do not load whole histories by default.
- Section position is not a period marker: active-quarter closeouts may appear
  beneath a historical heading. Scan all dated headings and classify each entry
  by its date, PR and base branch, not by the heading it sits under.
- These instructions and `CLAUDE.md` belong on `sow/*` and their feature branches;
  never promote internal agent files to `master`.

## Quarter transition (owner-approved release work)

Release, product promotion, quarter freeze and quarter bootstrap each need
explicit owner approval; none is implied by merging an ordinary PR.

- Promote approved Q4 product changes through a reviewed release PR. A normal
  merge includes internal files: prepare and verify the promotion tree excludes
  `AGENTS.md`, `PROJECT_PROGRESS.md`, `CLAUDE.md` and the internal continuity
  records (`documentation/Agent-Continuity-*-Q4.md`).
  Classify other documentation separately; retain product documentation
  selected for release.
- Complete known-SHA Q4 closeouts before freezing `sow/2026-Q4`. The release-promotion
  PR's closeout belongs on the outgoing `sow/2026-Q4`, never on `master`, although
  `master` is that PR's base. Preserve the frozen branch as the quarter's record:
  no commits, rebase, rename, archive or deletion.
- Create `sow/2027-Q1` from the owner-approved promoted product baseline. Carry
  forward useful shared instructions and the Claude import onto Q1 only; update
  the active/frozen quarter references and carry unresolved work with evidence.
- Initialize a concise Q1 progress record only as an explicitly authorized
  quarter-bootstrap operation, referencing the frozen Q4 commit for history.
  This does not widen the ordinary post-merge-only progress-closeout exception.
  These steps describe future release work; they are not authorized by this file.

## Project continuity

`PROJECT_PROGRESS.md` is updated only after a PR has merged.
During implementation, keep status/evidence on the PR branch in the
PR record document and PR description. After merge, fetch the base
branch and add the closeout entry, including the merge SHA, as a
direct docs-only commit on the base branch (for a release-promotion
PR into `master`, on the outgoing `sow` branch instead). Do not modify
unrelated stream entries.

The order of work is:

code + tests + evidence → final PR CI → merge →
`PROJECT_PROGRESS.md` closeout commit on the base branch.

Never change `PROJECT_PROGRESS.md` on a PR branch. The sole direct-Q4
exception is its post-merge docs-only closeout with a known merge SHA
and `[skip ci]`; other changes require a feature branch and owner-reviewed PR.

Do not record trivial actions such as opening files,
searching the repository, or reading documentation.

Keep the PR record document and `PROJECT_PROGRESS.md` concise and
sufficient for another AI agent on another computer to continue
without access to the current conversation.

While a PR is open, its record document must contain, and after merge
the `PROJECT_PROGRESS.md` closeout entry must contain:

- current status;
- work completed;
- important decisions and assumptions;
- files changed;
- tests, builds, checks, or validation performed and results;
- unresolved issues, risks, or blockers;
- exact recommended next step.

Identify the PR, base branch and tested head SHA. Distinguish local results
from GitHub checks, including failures/skips, required dependency order and
remaining gaps. Give reproducible commands and committed evidence paths; label
machine-local evidence and prerequisites that must be copied or provisioned
elsewhere. Record the validated content SHA in the committed PR record. If a
record-only commit follows, keep that tested SHA and its commands in the record,
validate the final docs diff, and identify the final PR head/checks in the PR
description. The post-merge closeout records the immutable merge SHA.

When updating `PROJECT_PROGRESS.md`, preserve still-relevant information
from previous sessions. Remove or replace information only when it is
obsolete, resolved, or superseded.

Before ending meaningful work, verify that the PR record document and
PR description (or, after merge, the `PROJECT_PROGRESS.md` closeout
entry) accurately represent the current repository state.

When appropriate, remind the user to commit and push changes before
switching computers, accounts, sessions, or AI agents.

Never assume conversation history, terminal history, generated files,
or uncommitted local changes will exist on another computer.

## Pull requests and merging

- Implementation, tests, workflows and ordinary documentation changes go
  through focused feature branches and owner-reviewed PRs into `sow/2026-Q4`.
  Continue an existing PR for the task rather than opening a duplicate.
- Merge only after owner/code-owner approval, required checks passing at the
  final PR head, and each review finding resolved or explicitly accepted by
  the owner.
- Permission for the direct post-merge closeout does not authorize source,
  test, workflow or any other file change. A specific owner exception must be
  recorded explicitly and kept narrowly scoped.

## Post-merge branch cleanup

Once the owner authorizes a PR merge, cleaning up that task's fully merged
feature branches is included; no separate cleanup approval is needed.

Before deleting a branch:

- verify the remote head SHA is the expected (approved) head;
- verify it is completely merged into the intended base on the remote;
- confirm no unmerged local or remote work remains on it;
- confirm no open PR uses it as head or base;
- exclude protected, default, quarterly (`sow/*`), shared and specifically
  protected repository branches.

Delete an eligible remote task branch with a lease guard, for example
`git push origin --force-with-lease=refs/heads/<branch>:<expected-sha> :refs/heads/<branch>`,
then confirm the ref is gone. Return the clean checkout to the active base
and delete the local task branch only when it is fully merged and no worktree
uses it. Never delete or archive worktrees automatically. If any condition
fails, preserve the branch and report why. Never force deletion of unmerged work.

Dependency resolution: downstream CI may select a dependency branch by PR
branch name before falling back to `sow/2026-Q4`. Inspect that resolution. When
a merged task branch would shadow Q4, clean it up first, then rerun downstream
CI and verify in the logs that the intended merged dependency was selected.

This covers only the current task's branches. Older unrelated leftovers need
a separate inventory and authorization.

## Recommendations and handoffs

When recommending a task or writing a prompt or handover, state the model,
effort, new or existing session and Plan or Build mode, with a brief reason.
A recommendation does not mean any setting was changed automatically.

## SAM_UI dialog conventions

`CLAUDE.md` imports this file, so these are the only copy; do not restate them elsewhere.
The reference is the Part O windows (`WPF/SAM.Analytical.UI.WPF/Windows/PartO*Window.xaml`);
do not invent a parallel design system and do not restyle Part O when touching other dialogs.

- Merge `Themes/PartOStyles.xaml` and use its resources: `PartO.Brush.*` colours (no literal hex for text,
  borders, surfaces or status), `PartO.SectionHeading`, `PartO.Caption` for secondary text,
  `PartO.PrimaryButton` for the one action a dialog wants, `PartO.StatusGlyph` beside a status label.
- Window: `FontFamily="Roboto Light"`, `WindowStartupLocation="CenterOwner"`, `Owner` set by the caller,
  `ShowInTaskbar="False"` for a modal dialog, `SizeToContent="Height"` with a `Width`/`MinWidth` for forms,
  `ResizeMode="CanResizeWithGrip"` only where content can grow. Content margin `16,14`.
- Buttons: bottom-right, `Height="28"`, `MinWidth` 90 (or `Padding="12,0"`), 8 px apart; one primary
  with `IsDefault`, Cancel/Close with `IsCancel`. A disabled choice keeps a tooltip saying why
  (`ToolTipService.ShowOnDisabled`).
- Status is never colour alone: pair colour with a glyph or words. Warning is amber, failure red, success
  green (`PartO.Brush.Warning/Danger/Success`). Long operations use the shared progress window
  (`documentation/ProgressDialogPattern.md`), not an ad-hoc bar.
- Restyling is XAML-only: keep element names, handlers, cancellation, diagnostics and result logic as they are.

## Validation policy

Size validation to the change; do not run the full 26-repository BuildAll or the licensed simulation suite
for styling changes unless a concrete dependency or regression risk requires it.

- Documentation change: documentation review (links, accuracy, no internal files promoted to `master`).
- UI-only change: build the affected SAM_UI project(s) and run the targeted tests that cover the changed code;
  compare the window against the Part O reference (ownership, resize, DPI scaling, buttons, error and cancel paths).
- Simulation logic change: focused unit tests plus the relevant workflow checks.
- Installer change: packaging and deployment validation.
