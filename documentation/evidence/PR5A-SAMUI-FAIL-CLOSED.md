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
