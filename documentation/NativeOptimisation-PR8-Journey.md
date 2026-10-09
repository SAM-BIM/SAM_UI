<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR8: the Design Optimisation journey on the "tas-model" engine

Branch `feature/optimisation-journey` → base `sow/2026-Q4` (cut from `f417b347`, after the SAM_UI#218 closeout). Record
date: 2026-10-09.

Plan of record: `documentation/NativeOptimisation-Plan-ModelBindings.md` (PR8 row and the choice follow-up). The engine it
calls is SAM_Tas PR7b-1 (SAM_Tas#91, merged `0e3a2aa0`; record `SAM_Tas/SAM.Analytical.Tas.GenOpt/NATIVE_OPTIMISATION_PR7B.md`).
The schema rules are SAM#188 (bindings, OPT6xx) and SAM#190 (try every option). The results view is PR5b's (SAM_UI#218,
merged). **SAM_UI only**: SAM and SAM_Tas are unchanged.

## Current status

PR open, **not merged**. Code, tests, mutations and licensed acceptance are complete; the ten owner decisions below were
**approved by the owner on 2026-10-09** (implementation checked against them; no review findings or comments open); awaiting
final PR CI on the head, then merge. Not split: the whole PR8 scope is here (the PR8a/PR8b split was not needed).

## What the window does now

Simulate > Optimisation ("Design Optimisation") offers two engines:

| Engine | What the user gives | Who writes the Tas script |
|---|---|---|
| **Tas model (no script)** (`tas-model`, new) | the Tas project folder, then what may change and what to measure, picked from the model | SAM_Tas, from tested blocks (PR7b) |
| **Tas script** (`tas-script`, unchanged) | the Tas project folder and a TasGenExecute C# script | the user |

The **Tas script** route is exactly as before: the same controls, examples, checks, run path (`ToGenOptDocument` →
`RunNative`) and results. The 165 existing `TasOptimisation*` tests pass unchanged.

### Setup on "Tas model"

1. **Engine** (Model section). The model's section appears; the script field and the examples are hidden. Each engine
   keeps its own form while the window is open (switching back restores it); the folders are shared.
2. **From the model.** The model is read in the background with SAM_Tas' licensed reader (`Query.TasModelInventory`) as
   soon as the window is on screen and the folder holds a TBD or TSD; a busy bar shows it, and **Read model** reads it
   again. Without Tas or its licence the line says so ("Tas could not open the model: … Tas (with a licence) must be
   installed …") and the run is blocked.
   - **Can change**: the catalogue's targets (`Query.TasModelCatalogue`): zone heating/cooling setpoints per internal
     condition with the current value and suggested range, a glazing system choice per glazing construction with each
     option's g, Ug, light and source, and each plant controller (no suggested range: the user enters one).
   - **Can measure**: annual heating/cooling demand (with the current value), overheating hours (with an editable
     threshold, default 28 °C), plant energy, cost (£ only) and CO2.
   - **Add** puts a bound design variable or output into the form (the same rows as before, with a line under each
     saying what it changes or measures). The first output added is the objective.
3. **Glazing options** (expander). The pool is the Glazing window's: the open model's systems, the default library and
   "My glazing systems", calculated once with Tas (TCD, `Query.TasGlazingSystems`), plus **Load more glazing…** (.tcd or
   .json). The default filter (PR7a-2) is editable: g from/to, Ug at most current + 0.3, light at least current − 0.1,
   at most 8 options. Changing it rebuilds the options at once; a value that does not read leaves the filter unchanged
   and says why.
4. **Method.** The list offers only what the engine runs for the variables chosen: a choice → **Try every option**
   (no settings); one value → golden section or Hooke–Jeeves; several values → Hooke–Jeeves. The method follows when a
   variable is added or removed.
5. **Ask an AI assistant** (expander). Type the request, **Copy prompt** (SAM.Core.Optimisation `AIExchangeText` with the
   "tas-model" capabilities and this model's catalogue), paste it into any AI chat, then **Paste reply**. The reply is
   read strictly (`extract = true`) against the capabilities and the catalogue; its findings are listed on the reply,
   and the form changes only on **Use this definition**.
6. **Definition: Open… / Save…** (top of Setup, both engines). Save writes the canonical JSON (UTF-8, no BOM); the Tas
   project folder, runs folder and script path are local settings and are never in the file. Open reads strictly; a
   file that cannot be read, or that names another engine, changes nothing.
7. **Advanced → Show generated script…**: the TasGenExecute script SAM_Tas generates (`TasModelRunner.ScriptText`),
   read-only.
8. **Test one simulation** (footer): one Tas simulation at the start values (option 1, the model as it is, for a
   choice) through `TasModelRunner.Test`, in its own run folder; the Setup tab shows the point, the outputs with units,
   the time, and the **run estimate**.
9. **Checks** ("Tas model"): Tas project, Tas model, Tas optimisation engine, Setup. Setup = the form as a definition,
   then its diagnostics against the "tas-model" capabilities **and the model's catalogue** (OPT609/OPT614 for a name not
   in the model), then SAM_Tas' `TasModelRunner` constructor (it resolves the glazing options and generates the script;
   nothing starts). Summary example: "Try every option on Glazing system (Suncool Example) (3 options), minimising
   Annual cooling demand; recording Overheating hours; at most 2000 simulations."

### Run & Results

**Run** creates SAM_Tas' runner on the model as read (inventory, glazing pool and filter) and runs it on an STA thread
(the glazing systems are written into the run's snapshot TBD through Tas COM). Progress, trace, result card, copy and
CSV are PR5b's. A choice is shown by option number **and name** ("3: Triple low-e") in the trace, progress lines and
result card; the tooltip, Copy trace, Copy summary and CSV keep the bare number at full precision.

## Owner decisions (APPROVED by the owner, 2026-10-09; not to be re-asked)

1. **Opening engine.** A first opening on a model whose folder holds a TBD opens on "Tas model"; otherwise (no model,
   or a remembered session) the window opens as before (the Systems Demo script example).
2. **The model is read automatically** once the window is on screen and the folder holds a TBD/TSD, once per folder; a
   failed read is not retried until **Read model**. (Tas starts its own processes, so nothing is read for a window that
   is not shown.)
3. **Each engine keeps its own form** while the window is open; the folders are shared between them.
4. **Picked values.** A setpoint starts at its current value with the suggested range and a **step of about an eighth
   of the range on a 1-2-5 scale** (16–24 °C → 1), so Hooke–Jeeves runs at once (licensed acceptance found it blocked
   by OPT407 without one; golden section ignores the step); a controller has no range (the user enters it: "no
   suggested range: enter one"); a choice lists **every** option the filter gives (1 to n) and the method becomes try
   every option; an overheating measure picked with another threshold is named "Overheating hours (25)". The lists
   show a setpoint's current value exactly and a stored result as the results are shown (PR5b: "now 15,227.8 kWh").
5. **A binding travels with its row** (renaming keeps the target or measure); the declared quantity stays with the
   name, as PR5a decided.
6. **The glazing filter lives in the window, not in the definition.** A saved choice lists its options by name; on
   another day (another pool or filter) a missing option is the definition's own OPT614.
7. **Estimate.** Try every option: exact (options × one simulation). Golden section and Hooke–Jeeves stop on
   convergence, so the line gives the time of 10 and 50 simulations and of the simulation limit.
8. **AI reply with errors.** A reply that can be read may be used even with errors (they then block in Checks, where
   the form can fix them); a reply that cannot be read cannot be used.
9. **Definition file** UTF-8 without BOM (the canonical writer's text; PR5b's CSV keeps its BOM for Excel).
10. **CSV of a choice** keeps the option number only (PR5b's approved layout); the names are in the window.

## Files changed

- New (`WPF/SAM.Analytical.UI.WPF/`):
  - `Classes/TasOptimisation/TasModelSession.cs`: the model read, the glazing pool and filter, the catalogue, the
    runner (licensed readers replaceable for tests; background work on STA threads);
  - `Classes/TasOptimisation/TasModelCatalogueItem.cs`: a line of Can change / Can measure;
  - `Classes/TasOptimisation/TasOptimisationAIReply.cs`: a pasted reply, read and checked;
  - `Classes/TasOptimisation/TasOptimisationTestReport.cs`: Test one simulation and the estimate;
  - `Query/TasOptimisationEngine.cs`: engines, their names and capabilities, the offered methods, binding wording.
- Changed (`WPF/SAM.Analytical.UI.WPF/`):
  - `Classes/TasOptimisation/TasOptimisationInput.cs`: `Engine`, `CreateTasModel`, `OfferedAlgorithms`,
    `EnsureOfferedAlgorithm`, `AddTarget`, `AddMeasure`, `DefaultStep`, `UniqueName`; try every option in
    `Load`/`TryGetDefinition`; row bindings and types carried through;
  - `TasOptimisationParameterRow.cs` / `TasOptimisationObjectiveRow.cs`: `Target` / `Measure`, type, declared quantity,
    `BindingText`;
  - `TasOptimisationFormatter.cs`: option names for a choice; `Text(value, unit)`;
  - `Query/TasOptimisationChecks.cs`: the "Tas model" readiness list and summary;
  - `Query/TasOptimisationAlgorithmName.cs`: "Try every option"; `Query/TasOptimisationAssemblies.cs`: the probe reaches
    the PR7b engine and the try-every-option kernel;
  - `Windows/TasOptimisationWindow.xaml(.cs)`: engine choice, From the model, Glazing options, Ask an AI assistant,
    Definition Open/Save, generated script, Test one simulation, the tas-model run;
  - `Windows/AnalyticalWindow.xaml.cs`: the open model is passed for the glazing pool.
- Tests (`WPF/SAM.Analytical.UI.WPF.Tests/`): new `TasModelJourneyTests.cs`, `TasModelJourneyWindowTests.cs`,
  `Helpers/TasModelJourneyFixtures.cs`, and the opt-in `TasModelAcceptanceHarness.cs` (does nothing unless
  `SAM_OPT_ACCEPTANCE` is set).
- This record.

## Validation

### Build and tests (no licence)

- Step 0 on this workstation: SAM `288c9f57`, SAM_Tas `a4cd6d32`, SAM_UI `f417b347`, SAM_Systems `5404926`, all at
  `origin/sow/2026-Q4` and clean; `build\` folders rebuilt in order (SAM, SAM.Tests, SAM_Systems, SAM_Tas, SAM_UI;
  scratch profile, real `NUGET_PACKAGES`, `-m:1`). Release `SAM_UI.sln`: 0 errors; no warning in a changed file.
- `TasOptimisation*`: **165/165 unchanged** (the "Tas script" route as before).
- New: **41** (`TasModelJourneyTests` 26 with theory rows, `TasModelJourneyWindowTests` 14, the opt-in harness 1, which
  passes doing nothing without its variable). Window tests are `[WpfFact]` in the WPF collection and await (no
  `.Result`/`.Wait()`). Stub runs go through SAM_Tas' `TasModelRunner` and `StubTasGenExecute` (spec from
  `SAM_TAS_GENOPT_STUB_SPEC`), with a stand-in inventory and glazing writer.
  - catalogue → form: setpoint (value, range, step, unit, quantity, binding), controller (no range → form problem),
    glazing choice (discrete 1..n, every option, try every option), measures with a threshold, the first output as the
    objective, unique names, a renamed row keeps its binding, OPT607 for the same item twice;
  - methods offered per engine and variables; the method follows added/removed variables;
  - SAM's bound fixtures (`glazing-choice.json`, `systems-demo-bound-golden-section.json`,
    `zone-setpoints-glazing-hooke-jeeves.json`) read back byte-identical through the form;
  - AI: the prompt carries the model's items, the request and try-every-option and no machine path; a fenced reply is
    read with its warning and used; an invented option is OPT614 on the reply; prose cannot be used;
  - Open/Save: the saved file equals the canonical writer's text, no BOM, no workspace or temp path; reopening gives the
    same definition and method list; an unreadable file or another engine changes nothing;
  - readiness: not read / failed read (the Tas message) / another folder's session; a runnable choice's summary; a
    tighter glazing filter turns a listed option into OPT614;
  - the window: engine switch keeps each form, opening engine, the model read only for a window on screen and once per
    folder, the lists' text, the filter (applied and refused), the generated script, Test one simulation (option 1, the
    estimate, the systems written to the snapshot only), a glazing choice run (option names in the trace and result
    card, numbers in tooltip/copy), a controller golden-section run, and a definition with a name not in the model
    blocked with nothing started.
- Full `SAM.Analytical.UI.WPF.Tests`: **2768/2768** (2727 before: PR5a's 2683 + PR5b's 44; + 41).
- **Mutations** (each applied alone, both projects rebuilt, `TasOptimisation*` + `TasModel*` run, reverted and touched;
  after the last revert the unmutated build passed 198/198): **12/12 caught**.

  | Mutation | Failed |
  |---|---|
  | J1 a choice is offered golden section and Hooke–Jeeves | 13 |
  | J2 the row's target is dropped from the definition | 18 |
  | J3 a picked choice is not `"discrete"` | 13 |
  | J4 the option name is not shown | 4 |
  | J5 the runner gets an empty glazing pool | 8 |
  | J6 the AI reply is checked without the catalogue | 1 |
  | J7 the definition file is written with a BOM | 1 |
  | J8 opening a definition loses the Tas project folder | 3 |
  | J9 the model is read for a window that is not shown | 1 |
  | J10 a measure's typed threshold is ignored | 1 |
  | J11 the checks ignore the model's catalogue | 7 |
  | J12 try every option is never built | 14 |

- `git diff --check` clean; SPDX headers on every new file.

### Licensed acceptance (this workstation, Tas installed and licensed, 2026-10-09)

Driven through the window itself (`TasModelAcceptanceHarness`): the real Design Optimisation window, the "Tas model"
engine, the model read by the window's licensed readers once on screen, items picked with the same code as the Add
buttons, fields typed into the window's text boxes, then Test one simulation and Run with the installed
TasGenExecute. Inputs are copies: the Systems Demo `C:\TasOut\pr7b\demo` (SHA-256 equal to PR7a's `base-local`: TBD
`bf3c0c7d…`, TSD `40f169af…`, TPD `c7178b65…`) and PR7a's SAM-generated Part O model `C:\TasOut\pr7b\sam`. The glazing pool
is the app's (default library + the user's own "My glazing systems"; see risks for the test host). Raw evidence (logs,
traces, CSV, definitions, generated scripts, renders) is local in `C:\TasOut\pr8\` (not committed).

| Case | Model | Setup | Test one simulation | Run | Compared with SAM_Tas#91's licensed smoke |
|---|---|---|---|---|---|
| A | Demo | HeatPumpController −5…35 (golden section), min plant cost; CO2, energy | 3 °C: cost 7363.16638183594, CO2 3932.8885345459, energy 27387.7943115234; 21.7 s | 11 simulations, best **4.968943799848584**, cost 7360.03466796875; 246 s | **bit-identical**: the test point and all 11 evaluations |
| B | Demo | Office Weekday heating and cooling setpoints (Hooke–Jeeves, limit 40), min cooling demand; heating demand, overheating 25 °C | 20/24 °C: 2978.53252598965 / 15227.8406637096 kWh, 274 h; 9.1 s | 18 simulations: heating → 16, cooling → 28 °C, cooling demand 2978.5 → **799.4 kWh**; 164 s | start point = Demo baseline bit for bit; directions as expected |
| C | Demo | glazing choice, filter widened in the window (Ug +5, light −1, 5 options), min cooling demand | option 1 = baseline | 5 options: 2978.53 (current), SIM_EXT_GLZ (copy) 3219.90, SIM_EXT_GLZ b9d885 3251.20, SIM_EXT_GLZ_SKY 3946.02, SIM_INT_GLZ 3934.92 kWh; best option 1; 46 s | option 1, SIM_EXT_GLZ (copy) (3219.89605521363 / 14728.9187967328 / 355 h) and SIM_INT_GLZ (3934.92031348047 / 21166.1359825536 / 508 h) **bit-identical** |
| F | Demo | glazing choice with the **default filter**, untouched | option 1 = baseline | 3 options (current, SIM_EXT_GLZ (copy), SIM_EXT_GLZ b9d885); best option 1; 28 s | the same values as C |
| D | SAM model | glazing choice (filter widened, 4 options), min cooling demand; overheating | option 1: 2523.75187299657 kWh, 32 h; 7.1 s | best option 2, SIM_EXT_GLZ (copy): **2496.3943804884 kWh**, 31 h; 28 s | options 1 and 2 **bit-identical** |
| E | SAM model | Studio 1_0 cooling setpoint (24-hour profile) 23…26 °C, golden section (tolerance 10), min cooling demand | 23 °C: 2523.75187299657 kWh (the baseline) | 10 simulations, → 25.976 °C, 1128.27 kWh (cooling falls as the setpoint rises); 74 s | direction as PR7b (26 °C: 1123.32 kWh) |

- **Repeats:** A, C (options common to both sessions) and E were run in two sessions (`run1` and the final run):
  bit-identical.
- **Durations** (for the estimate): Demo building simulation 9.1–9.2 s, Demo plant (controller) 21.7–27.2 s, SAM model
  7.0–7.2 s. Model read 0.8–5.9 s; glazing pool (13 systems, TCD) about 1 s more.
- **Processes and registry:** a 1 s process monitor saw only the expected TBD/TSD/TPD/TCD/TasGenExecute starts; no Tas
  process was left after any batch. `HKCU\Software\EDSL\TasManager` unchanged. The only change under `HKCU\Software\EDSL`
  is TPD's own recent-files list (`File1..4`), which every TPD open updates (the read-only inventory read and the
  plant evaluations).
- **Real application** (`SAM Analytical.exe` from this build, driven by UI Automation, `C:\TasOut\pr8\app-smoke.ps1`):
  Simulate > Optimisation; choose "Tas model (no script)" (the script field and examples leave the window, Test one
  simulation appears); type the Demo copy's folder; the window reads the model ("… 7 internal conditions, 2 glazing
  constructions, 1 plant room with 6 controllers. Glazing pool: 13 systems.", the user's "My glazing systems"
  included); Add "Office Weekday cooling setpoint" and "Annual cooling demand" → "✓ Ready: Golden section on Office
  Weekday cooling setpoint (21 to 28), minimising Annual cooling demand; at most 2000 simulations."; Test one
  simulation → "One Tas simulation took 6.8 s", "Annual cooling demand = 2,978.5 kWh" (the baseline); Close. Exit code
  0, TasManager unchanged, no Tas process left. (Screen captures on this VM come out blank; the window renders above are
  the visual evidence.) One earlier attempt timed out because the driver clicked Add while the pool was still being
  calculated and the list was being rebuilt under it; the final driver waits for the pool, as the busy bar tells a
  user.
- **Renders** (RenderTargetBitmap of the window during the harness): `C:\TasOut\pr8\{A..F}-{1-setup,2-test,3-result}.png`.

## Not done, and why

- **Apply best design** (writing the best values into the SAM model, the Tas files and the TPD) is PR9.
- **A plant measure on the SAM-generated model**: it has no TPD, so no plant case there (the Demo covers plant).
- **The open model as a glazing source** was not exercised licensed: the Demo has no SAM model, and the SAM-generated
  model's `.sam` file is not among PR7a's inputs. The source is the Glazing window's `GlazingSource.FromModel`.
- **A multi-plant-room TPD** is unproven (PR7b-1 owner decision 2; no such model here).
- **Shipping** (SAM_Deploy: SAM.Core.Optimisation, SAM.Math, GenOpt and its interop dependencies beside SAM_UI) is PR10.

## Unresolved issues and risks

- **The window starts Tas processes** to read the model (TBD, TSD, TPD) and the glazing pool (TCD) as soon as it is on
  screen with a Tas folder. They are read-only opens and close again; a Tas without a licence gives the stated message.
- **The lists are rebuilt when the pool finishes** (about a second after the model read): an Add click in that moment
  still adds a row, but the row under the pointer may have moved. A busy bar shows the calculation.
- **Names, not identities (R3).** A saved definition refers to model items and glazing options by name; another model,
  pool or filter reports them (OPT609/OPT614) rather than repairing them.
- **Test host:** `SAM.Analytical.UI.WPF.Tests` points `UserGlazingLibrary.Shared` at an empty temporary library
  (`TestIsolation`). The licensed harness therefore gives the window the user's library explicitly; the real
  application reads it itself (shown by the app smoke).
- **Golden section on a monotonic objective** (case E) ends near a bound (25.976 of 26 °C), as golden section does; the
  window reports it as found. Not specific to this PR.
- Unchanged from PR7b-1: `Modify.UpdateConstructions` overwrites by name (the runner writes unique names), the malformed
  `SIM_EXT_GLZ` Guid in SAM's library (its short id is not stable across sessions), TPD shutdown faults ignored after the
  values are read.

## Next step

1. PR CI (`build`, `spdx`) green on the head.
2. Owner review done: decisions approved 2026-10-09, merge authorised. Merge (merge commit, `--match-head-commit`), then the `PROJECT_PROGRESS.md` closeout on SAM_UI `sow/2026-Q4` (`[skip ci]`).
3. Then PR9 (Apply best design) or PR7b-2 (SAM_Tas licensed acceptance matrix), in the owner's order.
