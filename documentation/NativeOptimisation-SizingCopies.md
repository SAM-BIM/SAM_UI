<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation: Energy Simulation's sizing copies (SAM_UI part)

Branch `feature/optimisation-sizing-derivatives` → base `sow/2026-Q4` (cut from `6f85f916`, the PR9 closeout). Record
date: 2026-10-11. **Record of record:** SAM_Tas `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_SIZING_COPIES.md`
(the finding, owner decision (a), the shared split `Query.TasProjectFiles`, decisions, risks).

**Merge order:** the SAM_Tas PR (same branch name) first; then rerun this PR's CI against the merged `sow/2026-Q4` and
merge it.

## Current status

Implemented and validated locally, including the licensed journey with Sizing on and a real-application smoke (below).
Full `SAM.Analytical.UI.WPF.Tests` **2795/2795** (2793 + 2). Open, **not merged**; PR CI and owner review next.

## What the user sees

Simulate > Optimisation's **Tas project** check, on a default Energy Simulation folder (Sizing on):

> 000000_SAM_AnalyticalModel.t3d, 000000_SAM_AnalyticalModel.tbd, 000000_SAM_AnalyticalModel.tsd. Each simulation runs
> on a copy under C:\TasOut\sizing\acc\S1-r; the originals are not changed. Energy Simulation's sizing copies
> (000000_SAM_AnalyticalModel_HDDCDD.tbd, 000000_SAM_AnalyticalModel_Uncapped.tbd) are not used and are left as they are.

(the licensed S1 run below, verbatim).

The "Tas model" engine then reads, runs and applies on the project's TBD; before, the model read failed with "The Tas
project folder holds 3 .tbd files". With the "Tas model" engine, a sizing-named TBD whose model TBD is missing blocks the
check ("… the model to optimise cannot be identified. Run Energy Simulation again, or choose the folder that holds the
model's TBD."); the "Tas script" engine lists it as an ordinary file, as before.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/Query/TasOptimisationChecks.cs`: `TasOptimisationTasFiles` lists SAM_Tas' project files;
  new `TasOptimisationProjectFiles`; the Tas project check names the sizing copies and blocks the unmatched case
  ("Tas model" only).
- Tests: `TasOptimisationChecksTests.cs` (2 new); `TasModelApplyAcceptanceHarness.cs` (opt-in, licensed): Energy
  Simulation uses the dialog's Sizing (on; `SAM_APPLY_ACCEPTANCE_SIZING=off` for PR9's runs), logs the sizing copies
  after each step, checks the run snapshot holds none and Apply leaves them byte-identical.
- This record.

## Validation

See "Validation" in the SAM_Tas record for the library tests. This PR: Release `MSBuild SAM_UI.sln -restore -m:1` 0
errors; `TasOptimisationChecksTests` 20/20 (2 new).

**Licensed journey with Sizing on** (2026-10-11, this workstation, Tas licensed; `TasModelApplyAcceptanceHarness`, the
real window with the licensed readers and writers and the installed TasGenExecute; every input a copy of
`SAM_zoningAM.sam`, sha256 `9280d04d…`, PR9's model; evidence local in `C:\TasOut\sizing\acc`):

| Step | S1 (Studio 1_0 cooling setpoint 23–26 °C, golden section, min annual cooling demand) | S2 (glazing choice, 2 options, min overheating hours) |
|---|---|---|
| Energy Simulation 1 (dialog defaults: **Sizing on**) | 40 s; folder: `.t3d`, `.tbd`, `.tsd` + `_HDDCDD.tbd`, `_Uncapped.tbd` | 37 s; the same five files |
| Read and checks | model read 2.1 s, all checks ✓, the line above | the same |
| Run | 7 simulations, best **25.8966744387541**, 417.020191932678 kWh (ES 1: 741.71) | 2 simulations, best option 2 `SIM_EXT_GLZ b9d885` (Model), 19 h |
| Run snapshot | `.t3d`, `.tbd`, `.tsd`, `Script.txt`: **no sizing copy** | the same |
| Apply | SUCCEEDED; TBD 12 hours = (float) 25.896675, setback 150 kept; SAM copy profile **bit-exact** in 12 hours; backup = original | SUCCEEDED; TBD both elements on `Windows: SIM_EXT_GLZ b9d885 -pane`; SAM 20 apertures on `SIM_EXT_GLZ 2` (system's pane, model's frame) |
| Sizing copies after Apply | **byte-identical** (2 unchanged) | **byte-identical** |
| Energy Simulation 2 (Sizing on) | sizing copies regenerated; cooling **417.02019193267824** vs best 417.020191932678 (2.3e-13) ; TBD hours as applied | overheating **19 h** = the best simulation's 19 h (Test one simulation on ES 2's model) |

No Tas process was left after the run; `HKCU\Software\EDSL\TasManager` `Path`, which Energy Simulation sets, was restored
(`reg-before.reg` = `reg-restored.reg`). With Sizing on, Energy Simulation 1's cooling demand differs from PR9's
Sizing-off runs (741.7 vs 3003.4 kWh), so the best points differ from PR9's; each run is compared with its own best
simulation.

**Real application** (`SAM Analytical.exe` of this build, GenOpt `E9AC3268…`, UI Automation; PR9's smoke script adapted,
`C:\TasOut\sizing\app-smoke.ps1`, `app-smoke.log`): a copy of S1's simulated model **with its sizing copies** opened
with `/Path`; Simulate > Optimisation proposed the model's folder on "Tas model (no script)" and read it (2.4 s; the
sizing-copies line shown); Run → "Optimum found after 6 simulations", best 27.60990336999411; the plan; Apply → TBD
changed, backup of the TBD only, **sizing copies byte-identical**; **Run Energy Simulation…** closed the window and opened
the Energy Simulation dialog (cancelled); app exit 0, model not saved, registry unchanged, no Tas process left.

## Unresolved issues, risks

See the SAM_Tas record.

## Next step

Owner review with the SAM_Tas PR; merge order above; then the `PROJECT_PROGRESS.md` closeouts.
