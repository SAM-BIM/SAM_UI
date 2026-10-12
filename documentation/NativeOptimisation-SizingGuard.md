<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation: why zone setpoints and glazing are not offered on frozen Sizing capacities (PR7b-2 D1, SAM_UI part)

Branch `feature/optimisation-sizing-guard` → base `sow/2026-Q4` (cut from `18d59010`). Record date: 2026-10-12. Owner
decision: D1 option (a) for V1. **Record of record:** SAM-BIM/SAM_Tas#97,
`SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_SIZING_GUARD.md` (the rule, its measurements, the four SAM_Tas
layers, the licensed checks). **Depends on SAM_Tas#97** (`TasModelSizingRefusal`, `TasZoneSizing`): merge that first, rerun
this PR's CI against the merged SAM_Tas Q4, then merge this one.

## Current status

Implemented and tested; open for owner review; not merged.

## What the window does

- **"Zone setpoints and glazing" check line** (⚠, does not block): on a model whose TBD zones are not all sized in every
  simulation, SAM_Tas' reason, verbatim — capacities fixed by Energy Simulation's Sizing (the zones named), why the result
  would not hold, that plant controllers can still be optimised, and the Sizing-off route with its limitation. The "Can
  change" list then holds only the plant controllers (SAM_Tas' catalogue).
- **"Setup" ✕ with the reason**: a definition that has a zone setpoint or glazing on such a model (loaded, pasted from the
  AI assistant, or kept from a model read before) is blocked with "“<variable>” changes the TBD. <reason>", not the
  generic "not in this model" (OPT609).
- **AI assistant's text**: SAM_Tas puts the reason in the catalogue's source, printed as `AVAILABLE (…)`.
- **Apply plan**: a best design with a zone setpoint or glazing change from a run whose model turns out not sized in every
  simulation (fixed, or not read) is refused with the reason and "Nothing will be written." before anything is offered;
  SAM_Tas refuses it again when writing. A controller-only best design is offered as before.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/Query/TasOptimisationChecks.cs` — the check line (`SizingTitle`, `TasModelSizingRefusal(session)`)
  and the Setup refusal.
- `WPF/SAM.Analytical.UI.WPF/Classes/TasOptimisation/TasModelApplyPlan.cs` — the plan refusal.
- Tests: `TasModelSizingGuardTests.cs` (new, 7); fixtures with zone sizing (`TasModelJourneyFixtures.Demo` as the Demo,
  `DemoWithFixedCapacities`; `TasModelApplyFixtures.Inventory` sized every simulation).

## Validation

- Release `SAM_UI.sln` build, 0 errors, against SAM_Tas#97's build (GenOpt `E4A6DFC7`, test bin verified).
- `SAM.Analytical.UI.WPF.Tests` **2802/2802** (2795 + 7).
- Mutation (`C:\TasOut\pr7b-2\d1-guard\mutate-ui.ps1`): no warning line → 1 fails; no Setup refusal → 1 fails; no plan
  refusal → 2 fail; source restored, tests pass.
- Licensed: the window's text is SAM_Tas' `TasModelSizingRefusal` of the read inventory; the real read and every refusal
  were checked on Tas in SAM_Tas#97 (ModelA Sizing on/off, Systems Demo).

## V1 limitations

As in SAM_Tas#97: no zone setpoint or glazing optimisation on folders from Energy Simulation with Sizing on (heated
models); Sizing off is not equivalent (plant sized without a design-day limit); fixed capacities set on purpose are refused.

## Next step

Owner review; merge SAM_Tas#97 first, rerun this CI against it, then merge (merge commit, `--match-head-commit`) with owner
approval; then the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
