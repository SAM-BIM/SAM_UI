# Project Progress - SAM_UI (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `ab080134`. Frozen Q3 record: `sow/2026-Q3` @ `f6fab16c` (not modified).

## Last updated

2026-10-06 (Q4 icon-redesign migration).

## Current status

Q4 branch cut from `master` `ab080134`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`) and a narrow CI branch-reference update (see Decisions). No product source changed. No Q4 product work has started.

## Q4 priorities

Not yet set by the owner. Record them here at the first Q4 planning pass. Known carry-over work is listed below.

## Known carry-over work

- **SAM Grasshopper icon redesign - PR #138** (`feature/sam-gh-icon-redesign` @ `51de3fc7`, open, base `sow/2026-Q3`, not merged). Analysed 2026-10-06: the branch carries only its own 5 icon-only commits (`1ac7af2`, `da7c037`, `20e800a`, `6d4becb`, `51de3fc`) on top of Q3 commit `05800789`. Those commits are not reachable from `sow/2026-Q4` (Q4 is built on the promoted `master` line), so a plain retarget would list 335 commits. Replaying exactly those commits onto `sow/2026-Q4` @ `638f22e7` is conflict-free (verified commit-by-commit with `git merge-tree`; identical to the net-diff merge). Planned action: rebase-onto Q4 as a new branch + PR, then close this one; owner-approved controlled task, not yet executed. **Update:** migrated; replacement Q4 PR SAM_UI#203 (see the Q4 icon-redesign migration section); this old PR stays open for now.

## Repository-specific next steps

- The Part O programme and its closeout record are in the frozen Q3 history below. Any further Part O work is a new Q4 task.
- Follow the continuity convention in `AGENTS.md` for every PR and closeout.

## Decisions / assumptions

- Q4 base is `master` `ab080134`; the internal files were recovered from `sow/2026-Q3` into this branch only, never onto `master`.
- Q4 history intentionally does not contain the Q3 branch history (the maintained `master` is the promoted Q3 line, which is not a descendant of `sow/2026-Q3`); the frozen `sow/2026-Q3` branch is the permanent record.
- Historical Q2/Q3 content below is kept as evidence; its branch names, SHAs and next steps describe Q3 and are not current instructions.
- CI: the hard fallback list for dependency checkout now tries `sow/2026-Q4` first (then the previous Q3/Q2 entries).

## Validation

- Bootstrap verified 2026-10-06: `sow/2026-Q4` was created at exactly `ab080134` and the push was a normal (non-forced) branch creation.

## Issues / blockers

- None at bootstrap.

## Next step

- Owner to set Q4 priorities; then start the first Q4 task from this branch.

## Late-Q3 local entries (2026-10-06)

Three documentation deliverables were produced on 2026-10-06 after the last committed Q3 record. Their only repository effect was a local continuity note
that was never committed; it is summarised here so it is not lost. All produced artifacts live outside Git (an external documentation folder on the
authoring machine) and must be transferred separately.

- Standalone Part O guide (20-page PDF): clean-model preparation, openings, dwelling zones, TM59 mapping, weather/scope, Iterations 1a/1b/2/2B/3,
  explicit cooling rooms, Mixed Design and reporting. Based on `documentation/user-guides/Part-O-Prepare-and-Run.md` at `f6fab16` and the published wiki.
  Findings recorded for the supplied model: occupied rooms pass; the Iteration 1b corridor shows significant risk (285 hours against 262); the supplied
  Mixed Design sidecar has no FinalRun. No final coordinated pass is claimed; a 2B case marker is not proof of completion.
- Part O training manual revision 2 (20-page PDF), reframed as a general modelling/MEP training document with the original numerical results preserved.
- User Libraries and Builder training manual (22-page PDF), based on `documentation/user-guides/SAM-User-Libraries-and-Builder-User-Guide.md` and the
  published wiki; illustrative values are labelled as assumed data, not certified performance.
- No application code, TAS rerun or physics change was involved in any of the three.

## Q4 operational cleanup (2026-10-06)

- Reviewed every active Q2/Q3 reference in this repository on `sow/2026-Q4` (workflow branch filters, dependency-branch resolution, `.gitmodules`/validation, docs). Historical Q2/Q3 mentions (feature documentation records, the frozen Q3 section below) are intentionally unchanged.
- Changed (`638f22e`): replaced the hard-coded fallback list `['sow/2026-Q4', 'sow/2026-Q3', 'sow/2026-Q2']` in `.github/workflows/build.yml` with a lookup of the newest `sow/YYYY-Qn` branch each dependency has (explicit head/base/canonical-quarter candidates unchanged), so a Q4 build can never fall back to the frozen Q3/Q2 lines and the next quarter needs no edit here. Resolves to `sow/2026-Q4` today.
- Checked, no action: the `github.repository_owner == 'SAM-BIM'` build guard (intentional; its comment names HoareLea only to explain why the guard exists), CODEOWNERS (SAM-BIM owners), and workflow secrets (no HoareLea-named secret). The local `upstream` (HoareLea) remote is preserved.
- Carry-over: **SAM Grasshopper icon redesign - PR #138** (`feature/sam-gh-icon-redesign` @ `51de3fc7`, open, base `sow/2026-Q3`, not merged). Analysed 2026-10-06: the branch carries only its own 5 icon-only commits (`1ac7af2`, `da7c037`, `20e800a`, `6d4becb`, `51de3fc`) on top of Q3 commit `05800789`. Those commits are not reachable from `sow/2026-Q4` (Q4 is built on the promoted `master` line), so a plain retarget would list 335 commits. Replaying exactly those commits onto `sow/2026-Q4` @ `638f22e7` is conflict-free (verified commit-by-commit with `git merge-tree`; identical to the net-diff merge). Planned action: rebase-onto Q4 as a new branch + PR, then close this one; owner-approved controlled task, not yet executed.
- Full cross-repository record, migration table and owner decisions: `SAM_Deploy:sow/2026-Q4` `PROJECT_PROGRESS.md`.

## Q4 icon-redesign migration (2026-10-06)

- Old PR: SAM-BIM/SAM_UI#138 (`feature/sam-gh-icon-redesign` @ `51de3fc7`, base `sow/2026-Q3`) - **preserved, open, untouched**.
- New branch `feature/sam-gh-icon-redesign-q4` cut from `sow/2026-Q4` @ `9bef0158`; new PR **SAM-BIM/SAM_UI#203** (base `sow/2026-Q4`), replay-only tip `017b65d9`, feature head `a114cd54`. **Not merged.** Companion: SAM-BIM/SAM#179 (Q4 migration of SAM#166).
- Replayed (old -> new, `cherry-pick -x`): `1ac7af2`->`93ddfbc`, `da7c037`->`3b2e42c`, `20e800a`->`5ba9eee`, `6d4becb`->`3ec6660`, `51de3fc`->`017b65d`; plus one new docs commit `a114cd5` pointing `documentation/GH-IconRedesign.md` at the new PR. No Q3 history imported.
- High-scrutiny review: `Grasshopper/` is byte-identical at the Q3 base, the Q3 tip and Q4 (`b0f66384`). All Q3/Q4 UI/product work after the PR was cut (231 Q3-line commits; `WPF/*`, `SAM_UI/SAM.Analytical.UI`, `documentation/`) touches no file of this PR, so the replay overwrites no newer product logic.
- Verified at the replay-only tip, before push: tree identical to the net-diff merge of the old feature onto Q4 (`a9a3300b`); same aggregate and per-commit `git patch-id`, file set, numstat and blobs (all PNG/resx/designer) as the old PR; ComponentGuid lines 33 unchanged; no EOL/BOM churn; no workflow/solution/product-project/`.gitmodules`/`AGENTS.md`/`PROJECT_PROGRESS.md` change; `git diff --check` output identical to the old PR.
- Validation: `check_source.py origin/sow/2026-Q4` OK (32 swaps, 18 SPDX headers, 0 non-icon changes, GUIDs unchanged); Release `msbuild SAM_UI.sln` with the CI flags against SAM `feature/sam-gh-icon-redesign-q4` and local sibling build outputs (APPDATA/USERPROFILE redirected) 0 errors; `check_assemblies.py` OK; PR CI `build`, `spdx` green; mergeable. Not run: Rhino-hosted `tests/GhIconTest` (Rhino does not start under the redirected profile; not in CI) and `SAM.Analytical.UI.WPF.Tests` (no WPF/UI file changed).
- Next: owner decides whether/when to close the old PR; merge remains the maintainer's call.

## Q4 runtime-URL cleanup (2026-10-06)

- **Status:** complete. SAM-BIM/SAM_UI#204 merged into `sow/2026-Q4` as merge commit `34519b5a3592e29877f152dfb24277b3635bfe96` (PR head `9fc0ed46313e1d57d13dd179dcf7b6b955246577`, Q4 base `70c86f3`); merge method: merge commit (repository convention). Remote and local `fix/sam-bim-runtime-urls-q4` removed.
- **Work completed:** The Mollier form help link now opens `https://github.com/SAM-BIM/SAM_Mollier/wiki/HomeUI` instead of `https://github.com/HoareLea/SAM_Mollier/wiki/HomeUI` (the identical page exists in the SAM-BIM wiki). SAM-BIM is the authoritative ecosystem; HoareLea is no longer the synchronised operational source. Record: the PR's `SAM-BIM-RuntimeUrls-Q4.md` document.
- **Decisions / owner classifications:** Assembly author/contact strings (`Hoare Lea`, `@hoarelea.com` in `Kernel/AssemblyInfo.cs`) are provenance/metadata, not repository ownership: KEEP unchanged. The `references/gbXMLSerializer.dll` binary and the `build.yml` guard comment still contain the string HoareLea: provenance, KEEP.
- **Files changed:** `WPF/SAM.Core.Mollier.UI.WPF/Forms/MollierForm.xaml.cs`, `documentation/SAM-BIM-RuntimeUrls-Q4.md` (1 product line).
- **Validation:** `msbuild SAM_UI.sln -p:Configuration=Release` (APPDATA/USERPROFILE redirected): 0 errors. PR CI build and spdx green.
- **Unresolved issues, risks:** None introduced.
- **Next step:** Review/merge of the deferred icon PR (SAM_UI#203) is the maintainer's call; untouched.

---

# Historical record - 2026-Q3 (frozen)

Source: last revision of the file on `sow/2026-Q3`, commit `c8a932f` (the file was removed from the Q3 tip by `bd70455`; `sow/2026-Q3` tip is `f6fab16c`). Preserved verbatim except that heading levels are shifted down one. Everything below describes Q3 and is not a current instruction.

## SAM_UI Part O progress

Base: `sow/2026-Q3`. PR2 merged as SAM-BIM/SAM_UI#192 at `a551d321a94bd2d93fc7a1401c8a9d287f07e9b2` on 2026-10-04.

### Completed
Iteration 3 record carries QA weather identity, coordinates and calculated dry-bulb peak from Reference A's provenanced result model. Selected cooling-stat room is recorded with existing guidance settings. Text report presents these facts, the operating-history file, and explicit UNAVAILABLE labels. Existing result/restore and Iteration 3 file validation remain the authority; no physics changed.

### Files changed
PartOIteration3Record, PartOIteration3GuidanceEvidence, PartOIteration3WeatherEvidence, RunPartOIteration3, PartOIteration3GuidanceResolution, PartOIteration3ReportText, PartOIteration3RecordTests; this file.

### Validation
WPF project builds with VS MSBuild. Iteration 3/result reopen suite: 290 passed; final record/report tests: 13 passed. SAM SimulationResultProvenance tests: 23 passed, including changed weather. Existing missing, wrong, stale and moved TSD tests passed in the UI suite. PR Windows build and SPDX passed. Diff review found no physics edits.

### Native acceptance result (2026-10-04): FAIL, stopped at defect
Full evidence and findings: `C:\TasOut\parto-nuaire-acceptance-2026-10-04\ACCEPTANCE_REPORT.md`. The supplied model was an earlier Part O result; SAM_UI correctly refused it as a design baseline. The UI saved a clean copy. The source remained unchanged (SHA-256 `25DD56B6...70E572`). The current app at `sow/2026-Q3` `8d3deab` ran native TAS/COM.

Fetched `sow/2026-Q3` in SAM_UI, SAM, SAM_Systems and SAM_Tas; each local HEAD matched its origin tip and all worktrees were clean before this report update. Dependency tips: SAM `64a735f`, SAM_Systems `5cd9ee1`, SAM_Tas `1ea4b5d`.

Mixed Design UI required an explicit room before Build. With Nuaire units assigned to three flats and Flat 1 cooling stat set to `Studio 1_0`, the native TAS Systems mixed run completed: 3 dwelling PASS; separate significant corridor risk. The 8,760-hour operating CSV shows background 30/30 l/s, first DX at hour 2605 with 80/80 l/s, bypass and recovery, 13 °C cooling supply minimum, and actual 38.1 °C outdoor peak at hour 4935. Room choices are TEST ASSUMPTIONS only.

Two separate Iteration 2 Prepare & Run reference runs completed through native TAS and TM59 PASS. Iteration 3 refused before Candidate B TAS in both attempts. After the first refusal, all three valid cooling rooms (`Studio 1_0`, `Bedroom 2_3`, `Bedroom 2_6`) were confirmed and saved. The second prepared and result models contain all three GUIDs, yet the Iteration 3 record resolves all three `Guid_CoolingStatSpace` fields to empty and materialisation refuses `MVHR-03`. `PartOIteration3GuidanceResolution` reads fresh catalogue settings without the saved dwelling strategy room; the Mixed route has the required transfer. No Candidate B file was produced. Weather identity, coordinates and 38.1 °C peak were recorded from the actual reference run; complete PR2 A/B diagnostics and moved Candidate B provenance could not be verified.

The acceptance note was committed on `sow/2026-Q3` as `42325cc` before the fix branch `codex/part-o-iteration3-room-binding` was created. No physics changed.

### Iteration 3 room-binding fix and native acceptance (2026-10-05)
PR #193 merged into `sow/2026-Q3` at `b2f17d906b1923d08541d0ea0215f576c2ac3e8b`. `PartOIteration3GuidanceResolution` reads the saved PR1 `PartODwellingStrategySet` from the prepared model, maps each unit by served room GUID to one dwelling, validates its selected stat room, and writes one binding to settings and guidance evidence. Run, preflight and result re-read call this resolver with the same prepared selections. The hub disables Run when resolution refuses. Changed `PartOIteration3GuidanceResolution.cs`, `PartOIteration3Preflight.cs`, `RunPartOIteration3.cs`; added `PartOIteration3RoomBindingTests.cs`. No simulation physics changed.

Focused regression: 4 passed. Broader Iteration 3 plus Mixed Design suite: 388 passed, 0 failed. The three-dwelling fixture materialised through the production SAM Systems path; missing, unknown and another dwelling's room refuse preflight and disable Run. PR Windows build and SPDX checks passed; merged app rebuilt locally.

Native acceptance: **PASS WITH ISSUES**. Full record: `C:\TasOut\parto-nuaire-acceptance-2026-10-05-selected\ACCEPTANCE_REPORT.md`. The fully selected `000000_SAM_AnalyticalModel-Cleaned.sam` was copied and opened in SAM_UI. Part O Prepare & Run Iteration 2 completed native TAS and TM59 PASS (5/5). Iteration 3 preflight showed three units with no refusal and enabled Run. Candidate B materialised, completed TAS thermal source, TAS Systems, guidance read-back and resultant temperatures, and reached A/B comparison: Reference A PASS, Candidate B PASS, 0/5 changed; mean system-minus-reference +0.90 K, RMS 1.33 K, largest 3.95 K. The saved record holds all three correct nonempty stat-room GUIDs and 22 C setpoint; weather `Z1_DSY1_2030s_HIGH50_CIBSE_v1.1`, runtime dry-bulb peak 38.1 C at hour 4935. Candidate result provenance locator, byte length and UTC timestamp match its TSD; report states provider matches result file. Hourly read-back has NORMAL/COOLING and BYPASS/RECOVERY for all units, with UNAVAILABLE hours explicitly labelled.

Issues: the first local attempt copied `Workflow.sam`, which lacked the saved strategy set; its correct preflight refusal showed this was the wrong input. The selected model retained its prior output-root setting, so the successful run wrote into the 2026-10-04 `PartO-complete` folder and overwrote some old run artifacts. The completed 27-file output tree was immediately copied to the 2026-10-05-selected evidence folder; the report records this limitation. No further code defect was established. Next: review the acceptance report and A/B outcome; retain the block on manufacturer-physics PR3 until separately authorized.

### Iteration 3 A/B forensic review (2026-10-05)
Read the completed acceptance record and both matching native TSDs read-only, plus Candidate B's 26,280-row operating CSV. No code or simulation physics changed. A is the legacy IZAM route: design flows, no separate exchanger/DX, supply represented by a TAS plant zone. B is the explicit Systems route with background recovery, bypass and controlled DX. The routes are not component-equivalent; A is the Part O/TM59 benchmark, not a Nuaire supply-temperature truth model.

The +0.901 K eight-room annual B−A mean is dominated by cold, normal-flow recovery hours. In roughly 4,700 hours per unit with outdoor <12 C and normal/recovery, A plant-zone air averaged 7.84–8.32 C while B delivered supply averaged 16.02–16.04 C; stat-room resultant B−A averaged +1.60 K (Flat 1), +2.24 K (Flat 2), +2.17 K (Flat 3). These are different representation points, so the 7.7–8.2 K gap is explanatory evidence, not a like-for-like component measurement. During B's classified cooling hours the dwelling room biases average −0.11 to −0.12 K. At peak outdoor hour 4935 (38.1 C), all three B units are 80/80 l/s, recovery fraction 0.8576, DX on and final supply 27.23–27.73 C, exactly 8.245 K below measured exchanger leaving; all three stat-room resultant temperatures are 0.14–0.40 K cooler than A. The maximum absolute A/B gap is B−A = −3.948 K in Studio 1_0 at hour 5743; B is NORMAL/BYPASS, 30/30 l/s, DX off. This is not an annual warming example.

B's 22 C room bindings, balanced 30/30 or 63/63 background to 80/80 cooling flow, bypass/recovery states, 13 C coil floor, and no high-temperature derating are supported by the record/read-back. The report notes 85/166/164 DX hours without a distinguishable stat signal and 88/166/164 operating-state UNAVAILABLE hours; these limit complete control attribution, without proving a defect. Supply below 13 C occurred only when air entered the coil below 13 C; the report finds zero hours cooled below the minimum by the coil. Background HX 0.80, topology and latent/performance curves remain blocked for change. The previous output-root setting overwrote some 2026-10-04 files; the copied 27-file 2026-10-05 tree is internally provenance-consistent, but cannot restore overwritten prior evidence. Recommended next step: one small generic output-root collision safeguard PR for new Part O/Iteration 3 runs, preserving intentional same-run resume/review; no Nuaire physics change or extra annual run for this A/B result.

### Part O output evidence safeguard (2026-10-05): COMPLETE
PR #194 merged to `sow/2026-Q3` at `efe56591fe0095a103ffe37ca0d4f0df8801b672`. The existing `PartOCase.json` marker now records the prepared-run identity and content-based simulation case key. Prepare & Run, Iteration 2B and Iteration 3 refuse an occupied case folder owned by another run before TAS or Iteration 3 cleanup. Iteration 3 offers explicit replacement; same-run/same-case retries remain allowed. The collision refusal does not call the Iteration 3 result writer with the occupied path, so it cannot replace an earlier pairing record. Legacy unowned evidence is protected. Changed `PartOOutputPaths`, `PartORun`, `Simulate`, `OptimisePartOTM59`, `RunPartOIteration3`, `PartOIteration3`, output-folder tests, and Iteration 3 run/review fixture tests; no physics or relative-result-locator code changed.

Validation: affected tests 196 passed; output-folder/resume/window acceptance tests 15 passed; local desktop build and PR Windows build/SPDX passed. Native check used a copy of the selected design with its remembered 2026-10-04 output root. Prepare & Run refused that occupied Iteration 2 folder before TAS; all 27 old files matched pre-run SHA-256 hashes afterward. Setting `C:\TasOut\parto-output-guard-2026-10-05\PartO-fresh` reused the same prepared attempt and completed native Iteration 2 full-year TAS, TM59 PASS (5/5). Iteration 3 completed native A/B (A PASS, B PASS). An intentionally confirmed rerun was cancelled after all TAS stages and before Candidate B TM59; the Hub kept its TAS results. Retrying completed in about 11 seconds with “Reusing the completed TAS results,” no TAS stage rerun, and A/B PASS. The fresh output marker identifies the same run/case for Iteration 2 and 3; its complete pairing record has relative A/result locators. The old 27-file tree remained hash-identical after the whole workflow. Evidence: `C:\TasOut\parto-output-guard-2026-10-05`. No unresolved safeguard defect. Next: no manufacturer-physics PR3 without separate authorization.

### Broader Iteration 3 journey coverage (2026-10-05): COMPLETE
PR #195 merged to `sow/2026-Q3` at `27a378e98d1223dd66f72437ba1969a0b09f87e8`. Added test fixture variation for one or four dwellings, changed valid stat-room selections, and a full-year modified weather case. The tests exercise selected-room guidance/materialisation, Iteration 3 report and weather provenance, relative Reference A locator, and reopen/review of a completed A/B pairing. No production or simulation physics file changed. Focused new tests: 4 passed; affected Iteration 3 review/binding/record/comparison/artifact/presentation suite: 89 passed, 0 failed; `git diff --check` clean. PR Windows build and SPDX checks passed.

Native acceptance PASS for the desktop workflow and persistence. Evidence: `C:\TasOut\parto-alt-weather-journey-2026-10-05\ACCEPTANCE_REPORT.md` and its `PartO/` output and `shots/`. A copy of the selected three-flat design used a genuinely modified full-year weather series (+1 C dry bulb in all 8,760 hours). SAM_UI recognized the new weather, completed native Iteration 2 TAS/COM and TM59 (3/5 PASS, 2/5 FAIL under hotter weather), then completed all Iteration 3 stages and A/B (A FAIL, B FAIL, 0/5 criteria changed; mean B−A +0.737 K, RMS 1.175 K). The record has the 39.1 C actual weather peak, all three persisted stat-room GUIDs, complete result provenance and relative Reference A locators. Closing/reopening in-session and reopening the saved Reference A model in a new SAM_UI process both restored the completed Iteration 3 comparison without TAS. A new run from that result model was correctly disabled. The native dwelling remains the same three-flat layout; one/four-dwelling and alternate stat-room variation are covered by the automated tests. PR #194 output-root collision behavior was not rerun. No known workflow blocker from this acceptance. Next: choose any further coverage as a separate task; retain the block on manufacturer-physics PR3 without separate authorization.

### Final Iteration 3 readiness review (2026-10-05)

Verdict: **READY WITH DOCUMENTED LIMITATIONS** for normal engineering/project use; close this development and acceptance cycle. Reviewed the merged PR #193-#195 checkpoints and the original-weather, output-safeguard/resume, and modified-weather native reports read-only. No code or simulation physics changed and no new tests were run. Workflow completion, A/B comparison, persisted room bindings, runtime weather provenance, saved-result reopen, output protection, and same-attempt resume are accepted. Candidate B's generic supply/control behaviour is supported by the hourly read-back; Reference A is the legacy IZAM approximation and its temperatures need not converge with B. The remaining limits are model assumptions and incomplete diagnostic attribution during UNAVAILABLE / indistinguishable-stat hours. Manufacturer clarification is still required before changing HX 0.80 versus the proposed equation, fan/DX physical order, or latent/performance curves. No blocking defect is established. Reopen development when Nuaire answers those questions or real project use exposes a reproducible defect. Exact next step: use Iteration 3 on a real project with the documented modelling assumptions; keep physics changes separate until manufacturer evidence arrives.

### Part O Prepare & Run end-user guide (2026-10-05)

Added a standalone operating guide at `documentation/user-guides/Part-O-Prepare-and-Run.md` and linked it from `README.md`. Verified current Simulate/Results ribbon labels, Prepare & Run scenario and action labels, Mixed Design cooling-control-room selection and persistence, Iteration 3 method/review labels, result window, output guard and room-binding tests. The guide distinguishes the clean design from saved results, Reference A from Candidate B, explains native TAS, weather, output evidence protection, resume/reopen, diagnostics and documented Nuaire limits. No application code or physics changed. Validation: guide and README local link present; `git diff --check`, SPDX and Windows build passed. The linked SAM Wiki was reviewed; it contains general SAM material rather than this current Part O workflow. Documentation PR #196 merged into `sow/2026-Q3` at `ffd3c3989110587dfb852b491cbd2f9c34d877ed` on 2026-10-05. A fresh interactive UI walkthrough was not run for this documentation pass. Next: use the guide on a real project; retain manufacturer-physics work as a separate future task pending evidence.

### Part O Wiki publication (2026-10-05)

Published `Part-O-Prepare-and-Run.md` to `SAM_UI.wiki` and linked it under Home > User Guides. Wiki commits `d614531` and `f207f3c` add the page, repair Home's four links to unpublished tab pages, and clarify that the repository guide is the source. Existing View Tab, Keyboard Shortcuts and User Libraries pages were not changed. Added `documentation/publish-part-o-wiki.ps1` to copy the authoritative guide byte-for-byte into a Wiki clone, add the Home link idempotently, and validate relative Wiki links; publishing remains a deliberate Wiki commit/push, not an automatic GitHub Action. Validation: source/Wiki SHA-256 matched, script rerun passed, relative links resolved, external User Libraries source resolved, live Wiki page and Home link returned HTTP 200, and Wiki `git diff --check` passed. No application code changed. Next: after any guide edit, rerun the script against a current `SAM_UI.wiki` clone, review its diff, and push the Wiki change.

### User Libraries and Builder guide restructure (2026-10-05)

Documentation only. Restructured `documentation/user-guides/SAM-User-Libraries-and-Builder-User-Guide.md` around the engineer's journey (choose or create, review performance, save for reuse, select, Apply, reuse later). It now leads with a workflow-at-a-glance diagram and a Save-versus-Apply diagram ("Save = keep for reuse. Apply = change this model."), then a "choose what you want to do" table and first-five-minutes steps. Glazing, opaque-construction, Target-U and My library workflows are separate sections, with provenance, material conflicts, validation messages, storage/identity/archive and current limitations moved into Reference. My library vs Analytical Model behaviour is explicit: library operations never change the model; only Apply does (one Undo).

Source verification against `sow/2026-Q3` (relevant source unchanged since `a16ff93f`) corrected or clarified: the opaque Alternatives list appears only after a Target U is typed and lists existing constructions that meet it or are within 10 % above it (max 30, display order differs from source order); the generated variant is the default pending change once its target is reachable and SAM never auto-chooses an existing construction; a Builder opened from the Change… list chooses the newly saved system as the pending change (not so from My library), and a Builder-created system must be saved before it can be applied; My library has no Apply; an unreachable Target U makes Save to My constructions… save the current construction. Added the verified EN ISO 10077-1 reference-window description of the Builder's example Uw.

Validation: independent review (approve with small fixes) and a targeted fix pass; both Mermaid diagrams render-tested with mermaid v11 in a browser; 19 internal anchors resolve; `git diff --check` clean. No application code changed; screenshot placeholders remain (no safe screenshots exist). Beginner follow-up in the same PR: a short key-terms table, how to open Thermal Performance (View ribbon tab > Panels group > Thermal Performance toggle, or 3D right-click Set U-value... / Set glazing...; verified in `AnalyticalWindow.xaml`), and a which-source-to-use table. Next: review and merge the documentation PR; no follow-up code work implied.

### Part O engineer-facing guide rewrite (2026-10-05)

Reworked `documentation/user-guides/Part-O-Prepare-and-Run.md` around preparation, strategy testing, and Mixed Design. Added a top-level workflow diagram, a Mixed Design diagram, preparation instructions for opening properties, dwelling zones/`IsDwelling`, TM59 internal conditions, and weather. Retained evidence safety, result reopening/resume, control-room and Nuaire limitations. Source verification confirmed 1a = MVHR design duty without manufacturer unit; 1b = natural ventilation; 2 = MVHR with manufacturer unit; 3 = explicit system/cooling assessment against completed 1a or 2; Mixed Design selects ventilation and cooling per dwelling rather than an Iteration 3 option. Screening currently offers natural, MVHR baseline, and product MVHR; cooling/optimised screening is unavailable. No application code changed.

Validation: checked current XAML labels, scenario and mixed-design source, opening/zone/TM59 controls, and related tests; `git diff --check` passed. Both Mermaid blocks and headings were accepted by the GitHub GFM render API; no Markdown links were introduced. No live desktop simulation was run for this documentation edit. Next: show the requested review snapshot, then open a PR against sow/2026-Q3 after user review; do not publish Wiki or merge.

### Part O guide editorial review (2026-10-05)

On `codex/part-o-engineer-guide`, named 1a and 1b explicitly as alternative initial strategies in the top diagram, described weather as a full-year simulation, simplified the Mixed Design 1a example, and replaced user-facing "design authority" wording. Only the guide and this checkpoint changed. GitHub GFM accepted both Mermaid blocks; the guide has no Markdown links. `git diff --check` passed, and no application code changed. PR [#198](https://github.com/SAM-BIM/SAM_UI/pull/198) targets `sow/2026-Q3`; it is open and unmerged. Next: review PR #198; do not publish the Wiki before approval and merge.

### PR #198 final review finding (2026-10-05)

At expected head `147c8ec`, build and SPDX passed, base was unchanged and PR was mergeable, but a new review thread identified a genuine error in the four-flat Mixed Design example. Catalogue selection is project-wide: with products offered for cooled/product dwellings, a generic Iteration 1a MVHR selection does not remain generic in the final run. Removed that impossible example from the guide and second diagram, and added the project-wide catalogue caveat. The targeted fix was pushed as `fc22199`; the addressed review thread was resolved. GitHub GFM accepted both Mermaid blocks, the guide has no Markdown links, and `git diff --check` passed. Only the guide and this checkpoint changed; no application code changed. Because the requested merge protection names the prior head exactly, **do not merge or publish the Wiki** under that authorization. Next: wait for checks on the corrected PR head, then obtain a new explicit merge instruction naming that head.

A second review comment identified misleading wording about missing TM59 internal conditions. Source inspection found that a wholly unmapped scope blocks readiness, while a partially unmapped scope can pass that check with unassessed rooms; the exporter also refuses an unmapped room. The guide now tells engineers to resolve every in-scope assignment before running, without claiming a uniform blocker. The wording fix was pushed as `9b9aa2e`, and both addressed review threads are resolved. GitHub GFM still accepted both Mermaid blocks; the guide has no Markdown links, and `git diff --check` passed. No application code changed. Next: await checks and a new merge instruction for the corrected PR head; leave PR #198 open and the Wiki unchanged.

### Part O guide merge and Wiki publication (2026-10-05)

PR #198 merged into `sow/2026-Q3` as `3d7758dd5234b9d84c9747c6d6a971763cc21a4a` with protected head `278791aae6b71076b2a401ce57f426989261f8a1`. Windows build and SPDX passed; both review threads were resolved. Local base fast-forwarded to the merge and matched origin; `codex/part-o-engineer-guide` was deleted locally and remotely. Ran `documentation/publish-part-o-wiki.ps1` from the merged base and committed the byte-identical Wiki page as `87cfd7c2f20785607dc73ae0fc4c2316421c48be`. Source and Wiki SHA-256 match (`9D4BF845E8D9ABD25119AA26658128E6AFAEBE98F6658C13CE97DA45C1DE343A`). The live Wiki shows both Mermaid diagrams rendered, the corrected Mixed Design catalogue guidance, the missing-internal-conditions guidance, and a working Home > User Guides link. No application code changed. Next: no follow-up for this task; make future guide edits in the repository source before republishing the Wiki.

### Floor-plan label solver, clustered anchors (#58), 2026-10-05: COMPLETE

**Merges and branches**
- The root cause was in the shared SAM `Solver2D`. The fix merged as SAM-BIM/SAM#177 at `2eecf11ac6ae1a07384ab84d54ec8d90b2120841`, with the SAM closeout at `689f75d5`. It is exact: no label moves.
- The end-to-end test `WPF/SAM.Analytical.UI.WPF.Tests/FloorPlanClusteredLabelTests.cs` merged as #200 at `6689063bd12275cdc6af9e0f9acb0af88626eea9` (head `58d4fab2`, which merged in the docs-only base `46ded6f7`).
- No SAM_UI source changed. Local `sow/2026-Q3` matches origin, and both feature branches are deleted.

**Evidence**
- End-to-end, 400 clustered 20 m spaces: 143 solved / 257 over budget / 500 034 units / ~5.5 s before, and 400 solved / 0 / 78 887 units / ~0.3 s after.
- Small project model smoke: label texts and positions are identical before and after.

**Validation**
- Focused WPF tests: 74 passed.
- Full WPF suite: 2 445 passed, with one pre-existing failure. `WpfCollectionTests` flags `PartOIteration3RoomBindingTests` for a missing `[Collection(WpfCollection.Name)]`. This is a separate task and was not fixed here.
- #200 CI: build and SPDX green.

**Follow-up**
- The layout-changing fast path for heterogeneous near-coincident piles is tracked in #199. Do not start it unless requested.

### WPF collection convention fix (2026-10-05): COMPLETE

PR #201 merged into `sow/2026-Q3` as `0f7b662220918367729ad1e34079ef566d92d276`. Added `[Collection(WpfCollection.Name)]` to `PartOIteration3RoomBindingTests`, which resolves the `WpfCollectionTests.EveryClassWithStaTests_IsInTheWpfCollection` failure noted in the #58 section above. Test-only; no production or physics change. Validation: VS MSBuild Release build succeeded; filtered `WpfCollectionTests` + `PartOIteration3RoomBindingTests` run 7 passed, 0 failed; PR build and SPDX checks passed. The full WPF suite was not rerun. Next: none for this task.

### Thermal B0 docking spike branch resolved (2026-10-05): COMPLETE

`spike/thermal-b0-docking-2026-10-01` (single commit `330c740a`, never a PR) was superseded and deleted locally and on origin; it was not merged. Its content already existed in `sow/2026-Q3` as the rebased equivalent `2727a2ce` (identical stable patch-id `89c7feeb`; `git cherry` reported it as upstream). Stage B PR #167 (`ea39b810`) replaced the spike implementation with `ThermalPerformanceControl` / `AnalyticalWindow.ThermalPerformance.cs` and fixed the tab-column layout; #168 (`6445e7a9`) supplied the hybrid docked/floating host. Toggle, splitter resize, session width memory and selection-following are all covered there and by `ThermalPerformanceTests` / `ThermalPerformanceHostTests`. Cross-session persistence of host/width was never in the spike and remains deferred (`documentation/Thermal-StageB-Hosts.md`). Added a historical note to `documentation/plans/Thermal-B0-DockingSpike.md`, which is kept as the decision record. Docs-only; no code changed, no build/test run needed. Next: none for this task.
