<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR9: Apply best design

Branch `feature/optimisation-apply-best-design` → base `sow/2026-Q4` (cut from `828ed76a`, the PR8 closeout). Record date:
2026-10-10. This is the PR9 record of record.

Plan of record: `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR9 row, D2). It completes the planned journey:
Energy Simulation → Simulate > Optimisation ("Tas model") → Run → **Apply best design** → Energy Simulation.

**Two repositories, merge order:**

1. SAM_Tas, same branch name (`SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR9.md`): the Tas file writers,
   their protection, the run's file hashes, the 24 hours of a setpoint profile. **Merge first.**
2. SAM_UI (this PR): the window, the plan shown before anything is written, the SAM model change, Energy Simulation
   offered. CI builds it against the SAM_Tas branch of the same name; after (1) merges it builds against `sow/2026-Q4`.

SAM and SAM_Systems are unchanged. PR7b-2, the coordinated Tas units correction and SAM_Deploy PR10 are not part of PR9.

## Current status

Both PRs open, **not merged**. Code, tests, mutations and licensed acceptance (harness and real application) are complete;
awaiting final PR CI and **owner review** of the decisions below.

## What the user sees

After a "Tas model" run whose result rules (SAM_Tas `NativeGenOptOutcome`) give a best point (success, simulation limit
or golden-section nullspace; never a cancelled, withheld or failed run), the result card offers **Apply best design…**.

1. **The plan, before anything is written** (an "Apply best design" panel on Run & Results): one line per design variable,
   with the item, the value the Tas model holds now, the best value (formatted; the tooltip has it at full precision) and
   where it goes, for example:
   - "Cooling setpoint of “Studio 1_0”: 23.0 °C → 26.0 °C. SAM model (space “Studio 1_0” gets its own copy of the cooling
     profile “PR9 Cooling 23 08-20”; the other spaces keep it) and TBD" (tooltip: "… → 25.984925003778077 …");
   - "Glazing of “Windows: SIM_EXT_GLZ_SKY -pane”: option 1 … → 2: SIM_EXT_GLZ b9d885. SAM model (20 apertures of
     “SIM_EXT_GLZ_SKY”; the pane changes, the frame is kept) and TBD";
   - "Setpoint of controller “HeatPumpController” (Plant Room): 3.0 °C → -4.8 °C. **TPD only: SAM does not hold plant
     controllers**".

   Notes say what else to know (TPD only and "Create TPD"; the backup folder; the TSD is stale until the next simulation;
   one Undo step, not saved; a heating setpoint's design-day condition follows at the next Energy Simulation; a run that
   ended at the simulation limit applies its best so far). **Apply** is enabled only when every item can be applied and at
   least one changes something.
2. **Apply** (its own STA thread; Close is held until it ends): the model change is made in memory first, then SAM_Tas
   writes the Tas files (the run's hashes checked, staging copies written and read back, originals backed up, files
   replaced with rollback). If the Tas files fail, the model is not changed. Then the model is put in the window as **one
   Undo step** (`SetJSAMObject`, `FullModification`), as the Glazing window does; it is **not saved**.
3. **What was done**: each value before → after as read back, the SAM model change, the backup folder (**Open backup
   folder**), and **Run Energy Simulation…**, which closes the window and opens the ordinary Energy Simulation dialog for
   the open model (the ribbon's command). After Apply, the run cannot be applied again, and the window reads the model again
   before another run.

## Where each target goes

| Target | SAM model (Tas project = the model's folder) | Tas files |
|---|---|---|
| Zone heating/cooling setpoint | The space named as the TBD internal condition (SAM writes one per space, named after it). Its heating/cooling profile gets the best value **exactly (double)** where SAM_Tas reads the TBD setpoint from: a one-value profile's value; in a 24-hour profile the hours at the setpoint (heating: highest, cooling: lowest, compared as floats), the setback kept. A profile used by anything else is copied for this space ("<profile> - <space>"); one only it uses changes in place. | TBD: the same block as the evaluation (`(float)value`). |
| Glazing choice | Each aperture construction whose TBD pane is the choice's glazing construction (SAM_Tas' names: "Windows: <name> -pane", or the direct route's "<name> -pane") is replaced through the Glazing window's `SetGlazing` by a new construction: **the chosen system's pane layers and the model's own frame layers** and frame parameters, named after the system (unique), its aperture parameters from a Tas calculation of that construction (TCD; the option's g/Ug/light if it fails). Pane materials the model lacks are added. | TBD: the system written under its unique name and assigned to every element of the target; frames kept. |
| Plant controller setpoint | Not held by SAM: nothing. | **TPD only** (double). |

Where the Tas project is **not** the open model's folder (no model, a never-saved model, another folder): **Tas files
only**, and the plan says why and that an Energy Simulation of an open model would not use them; Energy Simulation is not
offered.

**Never guessed:** where the Tas project is the model's folder, every building item must be found in the model as the Tas
model has it, or nothing is applied: no space of that name (or several), a profile that would not write the TBD's profile
hour by hour ("the model has changed since its last Energy Simulation"), no aperture construction with that TBD pane name.
The Tas files must still have the hashes of the files the run evaluated. Values, option numbers and references are
checked against the definition (SAM_Tas `TasModelDesignChanges`).

## Owner decisions (proposed and implemented; please confirm or redirect)

1. **SAM model and Tas files together, or Tas files only.** Both when the Tas project is the open model's folder; any
   building item not found there as the Tas model has it blocks the whole application (Energy Simulation would rebuild
   the TBD from the model). Otherwise Tas files only, stated.
2. **A setpoint changes one space**, as in the evaluation (one TBD internal condition per space): a shared profile is
   copied for that space ("<profile> - <space>", made unique); an unshared one changes in place. The changed profile is
   written as 24 explicit hourly values (or its values with the first replaced, for a one-value profile): values
   identical for SAM_Tas, layout flattened. The space's internal condition gets a new Guid (SAM's `Space.InternalCondition`
   setter does that).
3. **Only the hours at the setpoint change** (heating highest / cooling lowest, as floats), the setback hours kept: the
   evaluation's block. A best value beyond the setback still changes only those hours.
4. **Glazing keeps the model's frame**: a new aperture construction (new Guid, the system's name, unique) with the system's
   pane and the model's frame layers and frame-side parameters (default frame width, frame additional heat transfer,
   default panel type). Several aperture constructions with the same TBD pane name are each replaced, each keeping its
   own frame. A collision-qualified TBD name ("<name>_<hash> -pane") is refused (it cannot be traced back).
5. **Controllers go to the TPD only**, stated in the plan; "Create TPD" may not keep them.
6. **Originals** are backed up under `<project>\SAM_ApplyBestDesign\<time>-<id>\original` and never deleted
   automatically; the staging copies are deleted after success and kept after a failure.
7. **A changed source** is any change to any top-level Tas file after the run (SHA-256): refused, nothing repaired.
8. **Items already at the best value write nothing**; when nothing would change, Apply is not offered as runnable.
9. **Saving**: the model is changed as one Undo step and not saved (the Glazing window's convention); the plan and the
   outcome say so.
10. **After Apply** the run is no longer offered and the window reads the model again before another run.
11. **Simulation limit / nullspace** best points are applied (the result rules count them as successful), with a note.
12. **Glazing read-back within 0.001** of the option's g/U/light (SAM_Tas): the pool may hold a system's values rounded.

## Finding for the owner (not changed in PR9)

**Energy Simulation with Sizing (the dialog's default) leaves `<name>_HDDCDD.tbd` and `<name>_Uncapped.tbd` beside the
TBD**, and SAM_Tas' `Query.TasModelInventory` refuses a folder with more than one TBD ("The Tas project folder holds 3
.tbd files …"). So Simulate > Optimisation cannot read the folder of a default Energy Simulation; the acceptance ran
Energy Simulation with Sizing off. This is PR7b/PR8 behaviour, not PR9's, and it was not in the earlier evidence (PR7a/PR8
copied only the TBD and TSD). Options: (a) the reader takes the TBD named like the TSD/T3D and ignores `_HDDCDD`/
`_Uncapped` (small SAM_Tas change, also the run's snapshot); (b) Energy Simulation writes the sizing copies elsewhere;
(c) document "Sizing off before optimising". Recommended: (a), as its own small PR.

## Files changed

- New (`WPF/SAM.Analytical.UI.WPF/`):
  - `Classes/TasOptimisation/TasModelApplyPlan.cs` (`TasModelApplyPlan`, `TasModelApplyItem`, `TasModelApplyMode`);
  - `Classes/TasOptimisation/TasModelSamTarget.cs`;
  - `Query/TasModelSamTarget.cs` (`TasModelSamTarget`, `TasPaneConstructionNames`, `TasSetpointProfileMatches`, `TasHours`);
  - `Modify/ApplyTasModelDesign.cs` (`ApplyTasModelBestDesign`, `TasModelApplyOutcome`, the SAM model change,
    `TasSetpointValues`, `TasGlazingComposite`).
- Changed: `Windows/TasOptimisationWindow.xaml(.cs)` (Apply best design button and panel, `UIAnalyticalModel`,
  `EnergySimulationRequested`, `PrepareApply`/`ApplyAsync`/`RequestEnergySimulation`; Run and Test held while applying);
  `Windows/AnalyticalWindow.xaml.cs` (passes the open model; opens Energy Simulation when asked).
- Tests (`WPF/SAM.Analytical.UI.WPF.Tests/`): `TasModelApplyTests.cs`, `TasModelApplyWindowTests.cs`,
  `Helpers/TasModelApplyFixtures.cs`, the opt-in `TasModelApplyAcceptanceHarness.cs` (does nothing unless
  `SAM_APPLY_ACCEPTANCE` is set).
- This record.

## Validation

### Build and tests (no licence)

- Step 0: SAM `288c9f57`, SAM_Tas `a4cd6d32`, SAM_UI `828ed76a`, SAM_Systems `5404926`, all at `origin/sow/2026-Q4`, clean.
  SAM and SAM_Systems `build\` were already current (no change since PR8).
- Release `MSBuild SAM_Tas.sln` and `MSBuild SAM_UI.sln -m:1` (scratch profile, real `NUGET_PACKAGES`): **0 errors**; no
  warning in a changed file or line.
- SAM_UI new tests: **15** (+ the opt-in harness, which passes doing nothing without its variable):
  - plan: SAM_Tas' TBD names of an aperture construction; a cooling setpoint placed on the space of that name, a shared
    profile copied for it only (the other space and the original unchanged, the given model unchanged, the best value
    bit-exact in the hours at the setpoint, setback kept); an unshared profile changed in place; a model without the space
    or with another profile blocks everything and writes nothing; the glazing choice (the chosen pane, the model's frame
    and no system frame width, pane materials added, the TCD calculation asked for the new construction, other apertures
    untouched); a controller "TPD only" with its note, the value passed as a double, no model change; no model / another
    folder / an unsaved model → Tas files only with the reason; a Tas failure leaves the model unchanged; changed Tas files
    refused; a failed or withheld run and a "Tas script" run refused; the current glazing as best writes nothing; the
    setpoint values hour by hour (a value below the setback);
  - window (`[WpfFact]`): no Apply before a run; a stub run, the plan shown with nothing written, Apply → the model in the
    window changed as one Undo step (Undo restores it), Run Energy Simulation… sets the request and closes, the run no
    longer offered and the model to be read again; a failed run offers nothing.
- Full `SAM.Analytical.UI.WPF.Tests`: **2784/2784** (PR8's 2768 + 15 + the opt-in harness).
- SAM_Tas `SAM.Analytical.Tas.GenOpt.Tests`: **323/323** (299 + 24; see its record).
- **Mutations** (each applied alone, rebuilt, the apply suites run, the file restored byte for byte; GenOpt rebuilt unmutated
  before the SAM_UI ones; after the last, the unmutated build passed 30/30 and 16/16): **15/15 caught**, plus one equivalent
  mutant. Script and log: `C:\TasOut\pr9\mutate.ps1`, `mutations.log`.

  | Mutation | Failed |
  |---|---|
  | T1 the run's file hashes not checked | 1 |
  | T2 the read-back ignores items outside the design | 1 |
  | T3 no restore after a failed replacement | 1 |
  | T4 glazing read-back tolerance 0.01 | 1 |
  | T5 a value outside its range accepted | 1 |
  | T6 an item already at the best value written anyway | 1 |
  | T7 a 24-hour profile checked by its setpoint only | 1 |
  | U1 a shared profile changed in place | 2 |
  | U2 every hour of a 24-hour profile set | 3 |
  | U3 the system's frame kept instead of the model's | 1 |
  | U4 a profile that does not match the TBD accepted | 1 |
  | U5 `Successful` not checked (BestPoint still checked) | 0 — equivalent: `BestPoint` is empty unless the run is successful |
  | U5b no best-point check at all | 2 |
  | U6 the model returned although the Tas files failed | 2 |
  | U7 the window does not put the model in (no Undo step) | 1 |
  | U8 any folder treated as the model's | 1 |

### Licensed acceptance (this workstation, Tas installed and licensed, 2026-10-10)

Every input is a copy; evidence (logs, renders, definitions, process logs, registry exports) is local in `C:\TasOut\pr9`
(not committed). **PR8's missing evidence is closed here: the source model is a SAM model (`SAM_zoningAM.sam`, the
PR7a model's design family, found on this workstation at `Documents\SAM_daily\2026-07-15 PartO\`, sha256 `9280d04d…`),
and S2 uses the open model as the glazing source.** Nothing was needed from the other laptop.

Harness (`TasModelApplyAcceptanceHarness`, the real window on screen with its licensed readers, the installed
TasGenExecute, the licensed writers): a copy of the model is simulated with the ordinary Energy Simulation core
(`Modify.RunPartOSimulation`, no Part O run, the dialog's options, Full Year 1–365, Sizing off — see the finding), then
optimised, applied, checked, and simulated again.

| Case | Setup | Run | Apply (read back) | Energy Simulation after Apply |
|---|---|---|---|---|
| S1 | SAM model; two rooms (Studio 1_0, Bedroom 2_3) share a 24-hour cooling profile (23 °C 08–20, else 150); Studio 1_0 cooling setpoint 23…26 °C, golden section, min annual cooling demand | 11 simulations, best **25.984925003778077** °C, 1344.90819885254 kWh (from 3003.44) | TBD: Studio 1_0's 12 hours 23 → 25.984924 (float), setback 150 kept; SAM: Studio 1_0 now uses "PR9 Cooling 23 08-20 - Studio 1_0" with **25.984925003778077 bit-exact** in the 12 hours; Bedroom 2_3 keeps the shared profile; backup = original | the regenerated TBD holds the same hours; annual cooling demand **1344.908198852539** kWh vs the best simulation's 1344.90819885254 (TasGenExecute writes 15 significant digits; difference 9.1e-13) |
| S2 | SAM model; its windows given the default library's SIM_EXT_GLZ_SKY (g 0.548) through `SetGlazing`, its SIM_EXT_GLZ kept in the model unassigned; glazing choice with the **default filter**, min overheating hours | 2 options: current SKY 27 h, **SIM_EXT_GLZ b9d885 (source: Model)** 19 h; best option 2 | TBD: both glazing elements on "Windows: SIM_EXT_GLZ b9d885 -pane", g 0.40016 U 1.24339 light 0.80356; SAM: 20 apertures on "SIM_EXT_GLZ 2" (SIM_EXT_GLZ's pane, SKY's frame); backup = original | the regenerated TBD has "Windows: SIM_EXT_GLZ 2 -pane" with the same g/U/light; **overheating 19 h** measured on it (Test one simulation of a fresh window) = the best simulation's 19 h |
| D1 | Systems Demo (no model open: Tas files only); HeatPumpController −5…35, golden section, min plant energy | 11 simulations, best **-4.799000050374341**, 26359.7830810547 kWh | TPD: **-4.799000050374341 bit-exact**; TBD, TSD, T3D unchanged (hashes); backup = original | — (no SAM model; the plan says to simulate in Tas) |

- **Repeats:** S1 three times and D1 twice, on successive builds of this PR in this session: identical best points and
  values each time.
- **First S2 attempt** refused the write (read-back tolerance 1e-4 against the pool's rounded g 0.4 / light 0.804): nothing
  was changed, the model untouched, the attempt kept — the failure path working; the tolerance became 0.001 (decision 12).
- **Real application** (`SAM Analytical.exe` of this build, UI Automation, `C:\TasOut\pr9\app-smoke.ps1`): opened a copy of
  S1's simulated model with `/Path`; Simulate > Optimisation opened on "Tas model (no script)" with the model's folder
  proposed and read; Add "Studio 1_0 cooling setpoint" and "Annual cooling demand"; Run → "Optimum found after 11
  simulations", best 27.96482500881551 (default range 21–28); **Apply best design…** → the plan ("23.0 °C → 28.0 °C …
  gets its own copy …"), TBD unchanged before Apply; Apply → "The best design is in the open model and its Tas files …",
  TBD changed, backup written; **Run Energy Simulation…** closed the Optimisation window and opened the Energy Simulation
  dialog ("Convert to TAS"), cancelled there (the harness runs that simulation); app exit 0; the model file not saved.
  (Screen captures on this machine come out blank; the harness renders `C:\TasOut\pr9\{S1,S2,D1}-{1..4}-*.png` are the
  visual evidence.)
- **One Tas operation at a time**, bounded: a watchdog cancels a run over 30 min; Apply holds Run/Test/Close. A 1 s process
  monitor (`procs-*.log`) saw only the expected TBD/TSD/TPD/TCD/TasGenExecute starts; **no Tas process was left** after any
  case or the app.
- **Registry:** the app smoke left `HKCU\Software\EDSL\TasManager` unchanged. Energy Simulation itself
  (`WorkflowCalculator.SetProjectDirectory`) sets its `Path` to the simulated folder — existing behaviour, not Apply's; it
  was restored to its original value after the harness runs.

## Unresolved issues and risks

- **Default Energy Simulation folders cannot be optimised** (Sizing's extra TBDs): see the finding.
- A COM call that hangs cannot be interrupted; Apply waits (one document at a time, its own STA thread).
- The heating design-day condition follows a heating setpoint at the next Energy Simulation (sizing changes), unlike the
  evaluation, which left it. Annual results are unaffected.
- The Optimisation window's glazing pool keeps the model as it was when the window opened.
- The applied TBD/TPD are not simulated by Apply: the TSD (and a TPD's results) stay those of the previous design until
  the next simulation (stated).
- A multi-plant-room TPD and a heating setpoint on a SAM model with heating on are not exercised licensed (no such model
  here); both are unit-covered.
- Unchanged from PR8: names, not identities, bind a definition (R3); the malformed `SIM_EXT_GLZ` Guid in SAM's library.

## Next step

1. PR CI (`build`, `spdx`) green on both heads.
2. Owner review of both PRs and the decisions above (and the finding). Then merge **SAM_Tas first**, then SAM_UI (merge
   commits, `--match-head-commit`), then the `PROJECT_PROGRESS.md` closeouts on both `sow/2026-Q4` branches (`[skip ci]`).
3. Then, in the owner's order: the Sizing-TBD fix (finding), PR7b-2 (SAM_Tas licensed acceptance matrix), the Tas units
   correction, PR10 (SAM_Deploy shipping).
