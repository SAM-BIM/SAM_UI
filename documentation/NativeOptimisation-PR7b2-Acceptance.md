<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR7b-2: licensed acceptance (SAM_UI part)

Branch `feature/optimisation-pr7b2-acceptance` → base `sow/2026-Q4` (cut from `18d59010`, the Sizing closeout). Record
date: 2026-10-11. This is SAM-BIM/SAM_UI#223. **Record of record:** SAM-BIM/SAM_Tas#95, `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B2.md` (the
coverage matrix, the evidence, defect D1 and its options, the missing inputs). Independent of the SAM_Tas PR (no API
change in either); either can merge first.

## Current status

Test-only change, validated; SAM-BIM/SAM_UI#223 open, not merged. **No product code changed.** Owner decision pending on D1
(SAM_Tas record); the 2026-10-12 follow-up added case **G1**, which measured that D1 covers glazing too. D2 (SAM_Tas only)
is fixed in SAM-BIM/SAM_Tas#96.

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
- **G1** (2026-10-12): the glazing choice on the model's own windows (nothing swapped in), heating and cooling as the
  author set them, the filter widened in the window (Ug +5, light +1), try every option minimising
  `SAM_APPLY_ACCEPTANCE_GLAZING_OBJECTIVE` (default "Annual heating demand"), recording the other two of heating, cooling,
  overheating. After Apply and Energy Simulation 2: heating and cooling of the best simulation against ES 2's TSD, then
  "Test one simulation" on ES 2's files against the best (the control).

## Licensed results (this workstation, 2026-10-11; evidence local, `C:\TasOut\pr7b-2\ui`)

| Run | Model, route, Sizing | Result |
|---|---|---|
| H1 | `SAM_zoningAM-test.sam`, gbXML, on | journey PASS (TBD 24 h = (float)15.527864, SAM bit-exact, sizing copies untouched, backup = original); heating demand ≈ 0, a weak heating fixture |
| H2 | `ModelA.sam`, gbXML, on | Apply PASS; **Energy Simulation 2 11 393.8 kWh vs best 6 263.4 kWh — defect D1** |
| H2 | same, Sizing **off** | PASS: Energy Simulation 2 = best within 1.9e-15 |
| C1 | `ModelA.sam`, gbXML, on | Apply PASS; **ES 2 521.8 vs best 365.3 kWh — D1 for cooling** |
| DR | `SAM_zoningAM.sam`, **direct**, on | S1 PASS (ES 2 = best within 6.8e-16); S2 glazing PASS on the direct-route TBD (placement checks accepted, 19 h = 19 h) |
| G1 (10-12) | `ModelA.sam`, gbXML, on, min heating | best = current glazing, nothing to apply; ES 2 = best (1.9e-15) — non-binding |
| G1-cooling | same, min cooling | option 2 (Ug 2.24) applied exactly; **ES 2 heating 13 856.7 vs best 12 128.0 kWh (+14.3%), cooling 537.2 vs 540.8 — D1 for glazing**; Test on ES 2's files = ES 2's TSD exactly |
| G1-cooling | same, Sizing **off** | PASS: ES 2 = best within 7.1e-16 (heating) / 1.6e-15 (cooling) |

Harness runs passed as tests (they assert nothing; the logs are the evidence). After each run the wrapper found no Tas
process left; once, the harness's own last check saw a TSD read closing (gone within a second). `HKCU\Software\EDSL`
restored after each run. G1 evidence: `C:\TasOut\pr7b-2\ui\G1`, `G1-cooling`, `G1-cooling-sizing-off` (builds and
expectations in `C:\TasOut\pr7b-2\followup-2026-10-12`).

## Validation

- Release `MSBuild SAM_UI.sln -restore -m:1`: 0 errors, no warning in the changed file.
- `SAM.Analytical.UI.WPF.Tests` **2795/2795** (the harness passes doing nothing without its variable); rerun
  2026-10-12 with G1: **2795/2795** (`C:\TasOut\pr7b-2\followup-2026-10-12\build\ui-full.trx`).

## Next step

Owner review; merge only with owner approval (merge commit, `--match-head-commit`), after the D1 decision or
independently of it (test-only), then the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
