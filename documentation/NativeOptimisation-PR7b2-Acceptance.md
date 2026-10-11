<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7b-2: licensed acceptance (SAM_UI part)

Branch `feature/optimisation-pr7b2-acceptance` → base `sow/2026-Q4` (cut from `18d59010`, the Sizing closeout). Record
date: 2026-10-11. This is SAM-BIM/SAM_UI#223. **Record of record:** SAM-BIM/SAM_Tas#95, `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B2.md` (the
coverage matrix, the evidence, defect D1 and its options, the missing inputs). Independent of the SAM_Tas PR (no API
change in either); either can merge first.

## Current status

Test-only change, validated; SAM-BIM/SAM_UI#223 open, not merged. **No product code changed.** Owner decision pending on D1 (SAM_Tas record).

## What changed

`WPF/SAM.Analytical.UI.WPF.Tests/TasModelApplyAcceptanceHarness.cs` (opt-in; does nothing unless `SAM_APPLY_ACCEPTANCE`
is set):

- **H1 / C1**: a space's existing heating (H1) or cooling (C1) setpoint on the model as its author set it — the harness
  changes nothing in the model and refuses to run if that setpoint is off. Golden section within
  `SAM_APPLY_ACCEPTANCE_HEATING` / `_COOLING` = "space,minimum,maximum" (defaults "Bathroom_2,14,18", "Cell 1,22,28"),
  minimising that annual demand; Apply checked in the TBD (value or 24-hour profile) and the SAM profile (bit-exact);
  Energy Simulation 2's demand compared with the best simulation, with the relative difference.
- **`SAM_APPLY_ACCEPTANCE_DIRECT=on`**: Energy Simulation on the direct T3D route (the dialog's DirectT3D option) for every
  SAM case; the route is logged.
- The Apply read-back and the comparison after Energy Simulation 2 now handle heating and value profiles; the inventory
  log adds heating demand and every heated internal condition. S1, S2 and D1 behave as before.

## Licensed results (this workstation, 2026-10-11; evidence local, `C:\TasOut\pr7b-2\ui`)

| Run | Model, route, Sizing | Result |
|---|---|---|
| H1 | `SAM_zoningAM-test.sam`, gbXML, on | journey PASS (TBD 24 h = (float)15.527864, SAM bit-exact, sizing copies untouched, backup = original); heating demand ≈ 0, a weak heating fixture |
| H2 | `ModelA.sam`, gbXML, on | Apply PASS; **Energy Simulation 2 11 393.8 kWh vs best 6 263.4 kWh — defect D1** |
| H2 | same, Sizing **off** | PASS: Energy Simulation 2 = best within 1.9e-15 |
| C1 | `ModelA.sam`, gbXML, on | Apply PASS; **ES 2 521.8 vs best 365.3 kWh — D1 for cooling** |
| DR | `SAM_zoningAM.sam`, **direct**, on | S1 PASS (ES 2 = best within 6.8e-16); S2 glazing PASS on the direct-route TBD (placement checks accepted, 19 h = 19 h) |

Harness runs passed as tests (they assert nothing; the logs are the evidence). After each run the wrapper found no Tas
process left; once, the harness's own last check saw a TSD read closing (gone within a second). `HKCU\Software\EDSL`
restored after each run.

## Validation

- Release `MSBuild SAM_UI.sln -restore -m:1`: 0 errors, no warning in the changed file.
- `SAM.Analytical.UI.WPF.Tests` **2795/2795** (the harness passes doing nothing without its variable).

## Next step

Owner review; merge (merge commit, `--match-head-commit`) after the D1 decision or independently of it (test-only), then
the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
