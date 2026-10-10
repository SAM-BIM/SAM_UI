<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR9: Apply best design

Branch `feature/optimisation-apply-best-design` → base `sow/2026-Q4` (cut from `828ed76a`, the PR8 closeout). Record date:
2026-10-10. This is the PR9 record of record.

Plan of record: `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR9 row, D2). It completes the planned journey:
Energy Simulation → Simulate > Optimisation ("Tas model") → Run → **Apply best design** → Energy Simulation.

**Two repositories, merge order:**

1. SAM-BIM/SAM_Tas#93, same branch name (`SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR9.md`): the Tas file
   writers, their protection, the run's file hashes, the 24 hours of a setpoint profile. **Merge first.**
2. SAM-BIM/SAM_UI#220 (this PR): the window, the plan shown before anything is written, the SAM model change, Energy Simulation
   offered. CI builds it against the SAM_Tas branch of the same name while that branch exists, and against
   `sow/2026-Q4` once it is deleted (see Next step).

SAM and SAM_Systems are unchanged. PR7b-2, the coordinated Tas units correction and SAM_Deploy PR10 are not part of PR9.

## Current status

SAM-BIM/SAM_Tas#93 and SAM-BIM/SAM_UI#220 open, **not merged**. An architecture review on 2026-10-10
(`PR9_ARCHITECTURE_ADVICE_2026-10-10.md`, a local file in the SAM-BIM workspace, not in Git) raised three source
findings. All three were **reproduced with failing tests, then fixed** on both branches. Validation was then rerun:
unit and full suites, mutations, and licensed acceptance (see "Safety review" below). A follow-up review
(`PR9_FOLLOWUP_REVIEW_2026-10-10.md`, local) found that finding 1's fix still let an **atomic save** by another program be
lost; that was reproduced through the SAM_Tas applier and fixed in SAM_Tas (row 1b below; this PR's code is unchanged by
it). Every earlier head, CI-green or not (SAM_Tas `dd222a47`/`aad85e39`/`194a1545`, SAM_UI `9b82509d`/`2b776e22`/`60828d31`),
is superseded and is **not** evidence for the final pair. **Blockers for merge:** green CI on the final heads, and
**owner review** of decisions 1–15 below and of the Sizing finding.

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
   folder**), a ⚠ line saying Undo restores the model only, not the Tas files, and **Run Energy Simulation…**, which closes the window and opens the ordinary Energy Simulation dialog for
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
hour by hour ("the model has changed since its last Energy Simulation"), no aperture construction with that TBD pane name,
or one whose pane, frame or window placement is not the TBD's (decision 15), or a chosen system whose pane materials the
model cannot take as evaluated (decision 14). The Tas files must still have the hashes of the files the run evaluated,
checked again under a hold just before they are replaced (decision 13). Values, option numbers and references are
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
   automatically; after success the staged files have become the project files (an empty `staging` folder may stay
   if Tas still holds it), and after a failure the attempt is kept.
7. **A changed source** is any change to any top-level Tas file after the run (SHA-256): refused, nothing repaired.
8. **Items already at the best value write nothing**; when nothing would change, Apply is not offered as runnable.
9. **Saving**: the model is changed as one Undo step and not saved (the Glazing window's convention); the plan and the
   outcome say so.
10. **After Apply** the run is no longer offered and the window reads the model again before another run.
11. **Simulation limit / nullspace** best points are applied (the result rules count them as successful), with a note.
12. **Glazing read-back within one rounding step per quantity** (g, U, light each 0.001, named separately in SAM_Tas): the
    pool's values come from SAM_Tas' TCD glazing readers, rounded to `Core.Tolerance.MacroDistance`; the TBD read-back is
    not rounded. Equal values are not the same glazing, so the pane is also read back layer by layer (13–15).
13. **(Safety review, finding 1 and follow-up) Final check under a hold; capturing OS replacement; conflicts reported, never
    lost; owned-only rollback; no automatic recovery.** The project files to replace are held (delete sharing only) and
    every Tas file is compared with the run's again: a change made meanwhile refuses everything and is kept. The hold
    keeps out programs that *open* the file and refuses a file open elsewhere, but **not an atomic save** (a file renamed
    over the path). So each file is replaced with `File.Replace` capturing whatever it moves away, which must be the run's
    original: if it is another program's save, that save is put back, Apply stops and reports the **conflict** (never
    success), and nothing is lost. A rollback restores only files still holding Apply's written copy, never the backup
    over a later save. Not one transaction: if the process or computer stops part-way, the note
    (`REPLACING-PROJECT-FILES.txt`), the backup and the displaced files are for recovery by hand; nothing is recovered
    automatically. **Undo restores the open model only, never the Tas files**: after an Undo, run Energy Simulation (or
    copy the originals back from the backup) before treating the Tas files or their results as current; the plan and the
    outcome say so.
14. **(Safety review, finding 2) A glazing system's material must be the model's material of that name.** The TBD is
    written with the system's own materials; the model keeps its own material of a name it already has. So: a pane
    material the system's source does not define, or a model material of the same name that is a different thermal
    input to Tas (any property SAM_Tas writes, compared as the TBD's floats), **refuses the item before anything is
    written** (plan ✕, and again in the model change); an equal one is used; one the model lacks is added. Nothing is
    renamed or overwritten — the engineer renames one of them and runs the optimisation again.
15. **(Safety review, finding 3) A glazing construction is the TBD's only if its content and placement are.** The name
    finds the aperture constructions; each one's **pane layers** must be the TBD construction's, its **frame** one of the
    TBD's frames for those windows (both as SAM_Tas writes them, material properties included), and its windows must be
    in the **same zones, as many in each**, as the TBD's pane surfaces. Otherwise the plan refuses the whole application
    ("… the model has changed since its last Energy Simulation"). This compares only what Apply changes and keeps, not
    the whole model; folder and name equality alone are not claimed as provenance.

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
- Safety review: `Query/TasModelSamTarget.cs` (`TasGlazingMaterialProblem`; the glazing target compares pane, frame and
  placement with the TBD), `Modify/ApplyTasModelDesign.cs` (the material check again before the model changes),
  `Classes/TasOptimisation/TasModelApplyPlan.cs` (a refusal headline that does not over-claim; the Undo note);
  tests: 9 new cases in `TasModelApplyTests.cs`; `Helpers/TasModelApplyFixtures.cs` (the windows' walls bound the
  studios; the inventory carries the TBD's pane, frame and zone surfaces; a variant pool system).
- This record.

## Safety review (2026-10-10): findings, reproduction, fixes

Source review: `PR9_ARCHITECTURE_ADVICE_2026-10-10.md` (SAM-BIM workspace, local). Each finding was first reproduced by a
new test failing on the reviewed heads (SAM_Tas `aad85e39`, SAM_UI `2b776e22`), then fixed.

| # | Finding | Reproduced (failing on the reviewed heads) | Fix |
|---|---|---|---|
| 1 | Source changed during staging could be overwritten: hashes were checked only at entry | SAM_Tas: Tas saves the project TBD (or simulates its TSD) while the staging copy is written/read back → Apply **succeeded and overwrote the other program's TBD** with the default `File.Copy`; with a stand-in replacer the post-copy check then "restored" the backup over that save. Another program holding the TBD open and a writer between the check and the replacement were not refused. (4 tests) | Hold + final check under the hold, `File.Replace`, recovery note, restore only files no longer original (decision 13). SAM_Tas `TasModelDesignApplier`. |
| 1b | (Follow-up review, `PR9_FOLLOWUP_REVIEW_2026-10-10.md`) The hold shares delete, so another program can **atomically save** over the live path; the held handle still reads the original, the check passes, and Apply overwrites the save | SAM_Tas on `194a1545`, through the applier's `FileReplacer` injection with real NTFS `File.Replace`: an atomic save just before the replacement → **Apply succeeded and the save was lost**; an atomic save on an already-replaced TBD before a failing TPD replacement → **the rollback copied the pre-run backup over it** (2 tests) | Capturing replacement (the displaced file must be the run's original; otherwise the save is put back and the conflict reported), rollback only of files still holding Apply's copy, half-done replacement undone (decision 13). SAM_Tas `TasModelDesignApplier` only; 5 new tests. |
| 2 | Same-named but different glazing material: the TBD got the system's material, the model kept its own | SAM_UI: a pool system whose `Clear6` has conductivity 0.5 against the model's 1 → **the plan offered Apply**; so did a system whose source does not define its `LowE6` (2 tests; the equal-materials case passed before and after) | `Query.TasGlazingMaterialProblem` in the plan and again in the model change (decision 14); SAM_Tas reads the written pane back layer by layer. |
| 3 | Stale glazing matched by name and usage only | SAM_UI: the model's `GLZ` with another `Clear6` conductivity, another outer pane, another frame thickness, a fifth window, or a window moved to the other studio (same total) → **the plan offered Apply** in every case; an inventory without the detail was not refused (6 tests) | SAM_Tas inventory reads each glazing's pane layers, paired frames and zone surfaces; SAM_UI compares them (decision 15). |

The TBD-side layer values in the SAM_Tas tests are the ones the licensed reader read from the S2 Energy Simulation TBD of
`SAM_zoningAM.sam` (pane: two 6 mm panes and 12 mm argon; frame from the gbXML import), and they equal the model's materials
as SAM_Tas describes them — checked before the comparison was trusted.

## Validation

### Follow-up validation (final code: atomic-save fix, 2026-10-10)

- Release `MSBuild SAM_Tas.sln -restore` and `MSBuild SAM_UI.sln -restore -m:1`: **0 errors**, no warning in a changed
  file; the app build carries the new GenOpt (`573D2416…`). Logs `C:\TasOut\pr9\fix\build-*-race.log` (local).
- `SAM.Analytical.Tas.GenOpt.Tests` **335/335** (330 + 5 replacement-protocol tests). `SAM.Analytical.UI.WPF.Tests`
  **2793/2793**. In the first full run one test unrelated to PR9 failed once:
  `PartOWorkflowSimplificationTests.The_progress_window_keeps_its_content_after_standing_aside_for_a_dialog`. It
  passed 3/3 alone and in a full rerun, with no SAM_UI code changed since `60828d31`. It is recorded as intermittent.
- **Mutations 6/6 caught** (R1–R6, SAM_Tas record; `C:\TasOut\pr9\fix\mutate-race.ps1`, `mutations-race.log`).
- **Licensed rerun on this build** (`C:\TasOut\pr9\fix\race`, local): S1 (TBD), S2 (TBD glazing, the new pane/frame/
  placement checks passed on the genuine model) and D1 (TPD) all PASS. Best points are identical to before (S1
  25.984925003778077 bit-exact in SAM, Energy Simulation 2 within 9.1e-13 kWh; S2 overheating 19 = 19; D1
  −4.799000050374341 bit-exact). No displaced file or note was left after success; no Tas process was left; the
  registry was restored. **Real-application smoke** PASS (exit 0, model not saved, registry unchanged).
- The conflict outcomes are proven with real NTFS replacements in the SAM_Tas tests. A save by Tas itself during Apply
  was not provoked licensed.

### Safety review validation (code of `194a1545`/`60828d31`; replacement protocol superseded above)

- Release `MSBuild SAM_Tas.sln -restore` and `MSBuild SAM_UI.sln -restore -m:1`: **0 errors**; no warning in a changed
  file. Logs: `C:\TasOut\pr9\fix\build-*.log` (local).
- `SAM.Analytical.Tas.GenOpt.Tests`: **330/330** (323 + 7: four for finding 1, the read-back of the pane's layers, two for
  the layer description and comparison). `SAM.Analytical.UI.WPF.Tests`: **2793/2793** (2784 + 9: two for finding 2 plus the
  equal-materials case, six for finding 3; the Undo wording asserted in two existing tests).
- **Mutations** (`C:\TasOut\pr9\fix\mutate-fix.ps1`, `mutations-fix.log`; each alone, rebuilt, the apply suites run, the file
  restored byte for byte; afterwards the unmutated build passed 31/31 and 25/25): **14/14 caught**.

  | Mutation | Failed |
  |---|---|
  | T8 no final source check under the hold | 2 |
  | T9 the hold lets other programs write | 2 |
  | T10 copy-over replacement (the old default) | 5 |
  | T11 read-back ignores the pane layers | 1 |
  | T12 a transparent property left out of SAM's description | 3 |
  | T13 two NaNs differ | 2 |
  | T14 layer thickness not compared | 1 |
  | U9 the pane is not compared with the TBD | 3 |
  | U10 the frame is not compared with the TBD | 1 |
  | U11 the windows compared by total, not by zone | 3 |
  | U12 the plan does not check the system's materials | 3 |
  | U13 the model change does not check the system's materials | 3 in the scripted run; **2 (the expected two) when rerun alone** — the third failure did not reproduce and was not identified |
  | U14 a material the source does not define accepted | 2 |
  | U15 an inventory without the glazing detail accepted | 2 |

- **Licensed acceptance rerun** on the final build (this workstation, Tas licensed; the same harness, inputs and cases as
  below; evidence `C:\TasOut\pr9\fix`, local), because the inventory reader, the glazing checks and the file replacement
  all changed:

  | Case | Plan | Apply (read back) | After |
  |---|---|---|---|
  | S1 (24-hour cooling setpoint, 11 simulations) | offered | TBD Studio 1_0's 12 hours = (float) 25.984924, setback kept; SAM copy profile **25.984925003778077 bit-exact**; backup = original | Energy Simulation 2: 1344.908198852539 kWh vs the best simulation's 1344.90819885254 (−9.1e-13) |
  | S2 (glazing from the open model, 2 options) | **offered: the genuine model passed the new pane, gbXML-frame and per-zone checks** (20 windows) | TBD both elements on `Windows: SIM_EXT_GLZ b9d885 -pane`, g 0.40016 U 1.24339 light 0.80356, **pane layer by layer the system's**; SAM 20 apertures on `SIM_EXT_GLZ 2`; backup = original | overheating 19 h = the best simulation's 19 h |
  | D1 (TPD controller, 11 simulations) | offered (Tas files only) | TPD **-4.799000050374341 bit-exact** via `File.Replace`; T3D/TBD/TSD unchanged | — |

  Best points and values are identical to the first acceptance. The Tas processes listed at the end of each case had exited
  by the next check; none was left. `HKCU\Software\EDSL\TasManager` `Path` was changed by Energy Simulation, as before,
  and restored (`reg-before.reg` = `reg-restored.reg`).
- **Real application** rerun (`C:\TasOut\pr9\fix\app-smoke.ps1`, `app-smoke.log`; `SAM Analytical.exe` of this build,
  GenOpt `B443829C`): Optimisation on the open model's folder, Run → "Optimum found after 11 simulations", best
  27.96482500881551; the plan, with the Undo note; Apply → TBD changed, backup written, outcome with the ⚠ Undo line;
  **Run Energy Simulation…** opened the dialog (cancelled); app exit 0, model not saved, registry unchanged, no Tas process
  left.
- Observed, not changed (it predates this review): after a licensed Apply the **empty** `staging` folder can stay in the
  work folder. Tas still holds it when Apply deletes it, and that error is ignored. The staged files themselves are gone:
  the replacement consumes them.
- Not exercised licensed: the refusals (findings 2–3 negative cases are unit-tested only), internal glazing between two
  spaces, the direct (non-gbXML) export route, an interrupted replacement.

### Before the safety review: build and tests (no licence; superseded by the section above)

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

### Before the safety review: licensed acceptance (this workstation, Tas installed and licensed, 2026-10-10)

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
- Replacing the Tas files is per file, not one transaction; an interrupted replacement or a conflict with another
  program's save is resolved by hand with the note, the backup and the displaced files (decision 13). Undo restores the
  model only.
- The hold does not exclude atomic saves; they are caught after the fact by the displaced-file check (a reported
  conflict, never a loss). A save to a file after Apply replaced it, while Apply goes on, is the other program's.
  `File.Replace` is not verified on network shares (refused → nothing changed). The conflict paths are proven with real
  NTFS replacements in tests, not by provoking Tas itself mid-Apply.
- One intermittent, unrelated UI test (Part O progress window) failed once in a full run this session.
- The glazing placement check counts one TBD pane surface per window and adjacent zone. It matched the licensed S2
  model (external windows). Internal glazing between two spaces and the direct (non-gbXML) route have **not** been run
  licensed. If Tas splits or merges window surfaces there, the plan would refuse a genuine model, not accept a wrong one.
- A material conflict (decision 14) is refused, not resolved: the engineer renames one of the two materials.
- A COM call that hangs cannot be interrupted; Apply waits (one document at a time, its own STA thread).
- The heating design-day condition follows a heating setpoint at the next Energy Simulation (sizing changes), unlike the
  evaluation, which left it. Annual results are unaffected.
- The Optimisation window's glazing pool keeps the model as it was when the window opened.
- The applied TBD/TPD are not simulated by Apply: the TSD (and a TPD's results) stay those of the previous design until
  the next simulation (stated).
- A multi-plant-room TPD and a heating setpoint on a SAM model with heating on are not exercised licensed (no such model
  here); both are unit-covered.
- Unchanged from PR8: names, not identities, bind a definition (R3); the malformed `SIM_EXT_GLZ` Guid in SAM's library.

## Outstanding owner decisions

1. Approve or redirect decisions 1–15 above. New with the safety review (the follow-up reviewer's recommendations in
   brackets — recommendations, not approvals):
   - 13: is a reported conflict plus manual recovery (note, backup, displaced files) enough for V1, or is automatic
     recovery wanted before release? [manual acceptable for V1; never overwrite later legitimate changes — implemented]
   - 14: refuse a same-named different material (current), or import it under a unique name and rewrite only the new
     pane's references? [refuse for V1; unique-name import later]
   - 15: is the narrow check enough (pane, frame, placement; refuse on any doubt)? [keep; complete direct-route and
     internal-glazing licensed coverage before claiming them]
2. The Sizing-TBD finding: option (a), (b) or (c), and its place in the order of PR7b-2, the Tas units correction and PR10.

## Next step

1. Confirm PR CI (`build`, `spdx`) green on the **final** heads of both PRs; earlier green runs do not count.
2. After the owner approves: merge **SAM_Tas#93 first** (merge commit, `--match-head-commit <final head>`) and confirm
   the `sow/2026-Q4` build of SAM_Tas. Then **re-run SAM_UI#220's CI against the merged SAM_Tas**: the UI workflow
   clones a dependency by the PR's head-branch name first and `sow/2026-Q4` next, so delete the SAM_Tas feature branch
   after its merge (owner's call) before re-running; merge SAM_UI#220 on that green run (`--match-head-commit`). A UI run
   made against the feature branch does not validate the final pair. Then the `PROJECT_PROGRESS.md` closeouts as direct
   docs-only `[skip ci]` commits on both `sow/2026-Q4` branches, with the merge SHAs (never on the feature branches).
3. Then, in the owner's order: the Sizing-TBD fix (the reviewer recommends an unambiguous primary project with sizing
   derivatives excluded consistently from inventory and run snapshot — never "first/latest TBD", never deleting
   sizing files — and acceptance on a genuine default Energy Simulation folder with Sizing on, through read, test, run
   and apply), PR7b-2 (licensed matrix: heating, multi-plant-room where fixtures exist), the Tas units correction, PR10
   (SAM_Deploy shipping).

## Hand-over (another session or computer)

- Branch `feature/optimisation-apply-best-design` in SAM_Tas and SAM_UI, both pushed, cut from `a4cd6d32` / `828ed76a`
  (the current `sow/2026-Q4` tips; no rebase needed as of 2026-10-10). SAM and SAM_Systems unchanged (`288c9f57`,
  `5404926`).
- Rebuild before testing: SAM_Tas `MSBuild SAM_Tas.sln -restore -p:Configuration=Release`, then
  `dotnet test SAM_Tas/SAM.Analytical.Tas.GenOpt.Tests -c Release` (335) and
  `dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c Release` in SAM_UI (2793). The GenOpt tests reference the built
  `SAM_Tas\build\*.dll`, not the project: rebuild GenOpt with MSBuild after any change to it before `dotnet test`.
- Licensed evidence is local to the authoring workstation (`C:\TasOut\pr9`: the first acceptance; `C:\TasOut\pr9\fix`:
  the safety-review rerun, mutations, build/test logs; `C:\TasOut\pr9\fix\race`: the follow-up rerun); it is not needed to merge. The review itself is
  `PR9_ARCHITECTURE_ADVICE_2026-10-10.md` and `PR9_FOLLOWUP_REVIEW_2026-10-10.md` in the SAM-BIM workspace folder (not in Git; copy them with the workspace files). To repeat it: set `SAM_APPLY_ACCEPTANCE`
  (output folder; optional `SAM_APPLY_ACCEPTANCE_SAM`, `_DEMO`, `_CASES`) and run the `TasModelApplyAcceptanceHarness`
  test with Tas licensed. Energy Simulation in the harness rewrites `HKCU\Software\EDSL\TasManager` `Path`; export it
  before and re-import it after.
- `gh pr create`/`gh pr view` can fail on these forks (SAML on the parent organisation): use
  `gh api repos/SAM-BIM/<repo>/pulls/<n>` (`--input <json>` for create/patch) instead.
