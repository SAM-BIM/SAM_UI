# PR5A SAM_UI — generic orchestration and evidence checkpoint

Date: 2026-09-12

Tracker: [SAM #111](https://github.com/SAM-BIM/SAM/issues/111)

Branch: `codex/parto-pr5a-ui`

## Outcome

SAM_UI now exposes Parity (the unchanged B0 default) and Selected product as separate Iteration 3
behaviour modes. Selected product reads Iteration 2's existing selection identity, resolves every scoped
AHU through the merged generic SAM API, passes per-AHU settings to the merged SAM_Systems materialiser,
and selects the merged SAM_Tas `FromSystemsGraph` fan-heat route. Any unresolved unit refuses the whole
attempt before materialisation; nothing selects or substitutes a product in SAM_UI.

The pairing record/report schema persists the mode, exact catalogue provenance, selected identity,
capacity, distinct design/Part F/operating bases, resolved HR/SFP, generic fan mapping, declared
assumptions and AHU-to-AirSystem lineage. Review stays simulation-free and refuses inconsistent or
incomplete selected-product evidence.

## Verification

- Release WPF test-project build: 0 errors (existing repository warnings remain).
- Focused equipment resolver: 15/15.
- Focused record persistence: 8/8.
- Focused review: 24/24.
- Focused presentation: 8/8.
- Full `SAM.Analytical.UI.WPF.Tests`: 1002/1002 passed.
- Full `SAM_UI.sln` Release build with Visual Studio MSBuild: 0 errors.

The tests specifically cover B0 default routing, selected-mode all-or-nothing refusal, exact catalogue
file/schema/hash, missing selection/data, unknown product, unbalanced duty, insufficient capacity without
reselection, deterministic per-AHU resolution, resolved generic values/assumptions, JSON lineage,
unsupported behaviour refusal, missing provenance refusal, and result/report presentation.

## Evidence boundary

No certified E1/E2 curves for the canonical Nuaire product were found in the repositories or the
traceable sources recorded by the tracker. No catalogue values were invented. Therefore no real-product
B1/B2 annual TAS run was attempted or claimed.

The merged lower layers retain their licensed evidence: B0 `ClearToZero`, selected-product
`FromSystemsGraph`, correct native `ExchCalcType`, no duplicate `ExchLatType`, and measured fan heat-gain
policy behaviour. The complete licensed operating-point A-D matrix and annual selected-product exit gate
remain blocked on E1/E2; B3 supply-limit semantics were not interpreted. Issues #113 and #115 remain
independent and were not changed by this slice.

Exact next step: obtain traceable certified E1/E2 performance data for the canonical product, review and
transcribe it into catalogue v2 with provenance, then run the frozen licensed operating-point and annual
B0/B1/B2 acceptance sequence. Do not begin PR5B.

## Review amendment — pre-PR5A pairings (added 2026-09-12)

Before merge, the collision/gap audit found that the v2 schema bump made every existing
`PartOIteration3Record:v1` pairing unreviewable, including the PR4 and PR5A acceptance pairings in
`C:\TasOut\pr4h` and `C:\TasOut\pr5a`. Two things caused this: eligibility and review required the
exact current schema, and review's ledger replay dropped every stage after the `EquipmentResolution`
stage that v1 never had.

- Writers still write v2 only. Review and eligibility read exactly v1 and v2; any other schema still refuses.
- A v1 record with no behaviour mode is the historical Parity / Candidate B0 route. A v1 record carrying
  selected-product behaviour, equipment or catalogue evidence is refused as contradictory.
- A v2 record without a valid explicit mode still refuses.
- For v1 only, the review replays the missing `EquipmentResolution` stage as the Parity no-op, so the
  recorded ledger is shown whole. Review stays simulation-free.

The automated review of the amended head raised three findings in this slice's own code, all fixed:

- **Scope (P1).** Resolution enumerated every `AirHandlingUnit` in the scoped working copy. SAM #114
  scope removes a scoped-out ventilation system but leaves its unit behind, so Selected product would
  refuse, or SAM_Systems would reject settings for a unit it does not materialise. Now only the units a
  retained ventilation system names are resolved; a scoped-out unit is named in the notes.
- **Lineage (P1).** Equipment binding checked uniqueness per unit only. One materialised air system bound by
  two units now refuses at materialisation, before any simulation.
- **Completeness (P2).** A persisted row is complete only when it states its lookup airflow and both bases
  (not missing, not `Undefined`).

Amendment verification (against the merged SAM `b4a1283f`, SAM_Systems `5213ba9c` and SAM_Tas `0f7f59e0`
builds):

- Full `SAM.Analytical.UI.WPF.Tests`: 1012/1012 passed (1002 before, plus 10 new: readable schemas, v1 read
  as Parity, v1 reviewable, v1 reopened whole with no TAS call, v1 with selected-product or catalogue
  evidence refused, v2 without a mode refused, scoped-out unit neither required nor configured, shared
  air-system lineage refused, incomplete lookup coordinate/bases not complete).
- `SAM_UI.sln` Release build with Visual Studio MSBuild: 0 errors.
- `git diff --check`: clean.

## Phase 0 supersession — current state (added 2026-09-12)

The sections above were written before the Phase 0 conclusions were applied to this slice, and are kept
as written. Current state:

- `Selected product` is B2-style behaviour (fan layer + heat-recovery layer). B1 is not yet separately
  producible, so no `B2 − B1` attribution is claimed.
- B3 is the exchanger supply-air `Setpoint`, not `BypassFactor`, which is measured as having no effect. B3
  is not implemented.
- The displacement-ventilation normalisation (`true`, B0's value) is implemented in SAM_Systems #23. The
  licensed re-check of MVRE at ε 0 equal to B0 on the merged heads is still due.
- Plant-room / duct detailed read-back is still an evidence gate before any exchanger-outcome claim. This
  slice claims none.
