<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Design Optimisation without a hand-written script: model bindings (plan, 8 Oct 2026)

**Status:** proposed plan for owner approval. Merging this docs-only PR approves it. No code is written for it yet.

This plan replaces the remaining steps of the native Optimisation programme after PR5a (SAM_UI#215, merged
`84df6141`). It supersedes the earlier PR6 (Definition tab) and PR7 (AI exchange), which assumed the user brings a
Tas script. PR5b (result formatting) is unchanged and independent.

## The problem

Today a run needs a TasGenExecute C# script that changes the Tas model, runs the simulation and works out the outputs.
Only the EDSL Systems Demo script exists. An engineer with a working SAM model cannot be expected to write C# against
the Tas COM API. The planned AI exchange did not help: it edits only the definition and is told not to write code.

## The decision (owner, 8 Oct 2026)

- **The AI produces a recipe; SAM_Tas writes the script.** A design variable says *what in the model it changes* (a
  target) and an output says *what it measures* (a measure). SAM_Tas generates the script from tested building blocks.
  The AI only ever returns definition JSON, which the strict reader and the diagnostics check. Nothing the AI writes
  is compiled or run.
- **The AI is reached by copy and paste** (Copy prompt / Paste reply), not by SAM calling an AI service.
- **"Apply best design" is part of V1.**
- **First targets and measures** are listed below.

A hand-written script stays possible as "Advanced: own script" (today's `tas-script` engine).

## User journey

Starting point: a SAM model that has been through Energy Simulation once (T3D, TBD, TSD; plus TPD when "Create TPD" was
ticked).

1. **Simulate > Optimisation.** The Tas project is the model's simulation folder (already proposed today).
2. **SAM lists what can change and what can be measured,** read from the Tas files, with the model's own names and
   current values:
   - can change: zone heating and cooling setpoints (per internal condition), glazing g-value (per glazing
     construction), plant controller setpoints (TPD);
   - can measure: annual heating demand, annual cooling demand, overheating hours, and plant energy, cost and CO2
     (only when the TPD exists).
3. **Say what you want.** Either:
   - **with AI:** type the goal ("lowest annual energy, heating setpoint 18 to 23 °C, g-value 0.3 to 0.6"), press
     Copy prompt, paste it into any AI chat, copy the reply and press Paste reply; or
   - **without AI:** pick the same items in the Setup lists.

   Either way the Setup tab shows the result: variables with bounds and units, the objective, the recorded outputs and
   the method. Anything that is not in the model is refused with a message.
4. **Test one simulation.** One Tas simulation at the start values; the window shows its outputs ("Annual heating
   demand 412 MWh; overheating 37 h") so the engineer can compare them with the Energy Simulation before a long run.
   The generated script can be viewed (read-only) under Advanced.
5. **Run.** As today: progress, trace, best design. The window states the likely duration (simulations × the time of
   one simulation, measured by the test).
6. **Apply best design.** Writes the best values into the SAM model (and the Tas files), so a normal Energy Simulation
   and its reports follow.

V1 limits, stated in the window and in the AI prompt: minimise only (maximise = minimise the negative), limits are
recorded not enforced, continuous values only (no "construction A, B or C"), golden section for one variable.

## First targets and measures (V1)

| Kind | Name in the window | Tas file and what the script does | Needs |
|---|---|---|---|
| target | Zone heating setpoint (internal condition) | TBD: set the heating thermostat value of the internal condition | building simulation |
| target | Zone cooling setpoint (internal condition) | TBD: set the cooling thermostat value of the internal condition | building simulation |
| target | Glazing g-value (glazing construction) | TBD: change the glazing layer so the construction's g-value is the value (see risk R1) | building simulation |
| target | Plant controller setpoint (plant room, controller) | TPD: set the controller setpoint (proven by the Systems Demo) | plant simulation |
| measure | Annual heating demand (kWh) | TSD: building heating load summed over the year | building results |
| measure | Annual cooling demand (kWh) | TSD: building cooling load summed over the year | building results |
| measure | Overheating hours (h) | TSD: occupied hours above a threshold (see decision D1) | building results |
| measure | Plant energy, cost, CO2 (kWh, GBP, kgCO2e) | TPD annual result sets (as the Systems Demo script) | plant simulation |

The generator decides the simulation chain from the bindings: any TBD target runs the building simulation (TBD → TSD);
any plant measure, or any TPD target, also runs the plant simulation on that TSD. A TPD-only optimisation reuses the
existing TSD, as the Systems Demo does, and is much faster.

## What is built, in order

Each step is its own PR, owner-reviewed, with the record/closeout rules of `AGENTS.md`.

| PR | Repository | Content |
|---|---|---|
| **PR6** | SAM (SAM.Core.Optimisation) | **Bindings in the schema.** `DesignVariable.Target` and `OptimisationOutput.Measure` (a kind, the model item it refers to, and parameters such as a threshold). Strict reader, canonical writer, diagnostics (unknown kind, item not in the catalogue, unit/quantity of the kind). The catalogue (`OptimisationCatalogue`) gains targets and measures with current values, units and suggested ranges. The AI text offers only catalogue items. Still `sam.optimisation/1` (nothing is released yet). No execution. |
| **PR7a** | SAM_Tas | **Spike, evidence only (licensed).** In TasGenExecute: edit the TBD, run the building simulation, read the TSD and run the TPD in one script; time per evaluation on the Systems Demo; the g-value method (R1); the overheating measure (D1). Result decides the blocks. |
| **PR7b** | SAM_Tas | **Catalogue reader and script generator.** `Query.TasModelCatalogue(projectFolder)` (internal conditions, glazing constructions, TPD controllers; which measures exist). `Create.TasScript(definition)` from tested blocks for the V1 targets and measures. A new engine `tas-model` (bindings, generated script) beside `tas-script` (own script). A single-evaluation call for Test one simulation. Licensed proof per block: each target changes the result in the expected direction, and each measure equals what SAM_Tas' own readers report. |
| **PR8** | SAM_UI | **The journey.** Setup lists "Can change" / "Can measure" with current values; Copy prompt / Paste reply (diagnostics shown on the reply); Open/Save definition (.json); generated script view; Test one simulation; duration estimate. Licensed acceptance on the Systems Demo and on a SAM-generated model. |
| **PR9** | SAM_UI (+ SAM_Tas if needed) | **Apply best design** to the SAM model (internal condition setpoints, glazing) and the Tas files, then offer Energy Simulation. |
| **PR10** | SAM_Deploy | **Shipping:** SAM.Core.Optimisation, SAM.Units, SAM.Math and SAM.Analytical.Tas.GenOpt beside every installed SAM_UI (was PR8). |

PR5b (engineering formatting of results, raw-value tooltip, CSV trace, full-precision copy) can go at any point; it
touches only the results view.

## Risks

- **R1 Glazing g-value.** TBD reports a construction's g-value (`GetGlazingValues`) but has no field to set it: it
  follows from the glazing layers. PR7a must find an exact, repeatable way (for example solving for the layer's solar
  transmittance) or the target becomes "glazing solar transmittance" with the g-value reported.
- **R2 Duration.** Building-level targets run a full-year building simulation per evaluation (20 s on the Systems
  Demo; minutes on a real building). The test simulation measures it, and the window shows the estimate.
- **R3 Names.** Bindings refer to model items by name (internal condition, construction, controller). A renamed item
  breaks the binding; the diagnostics must say which one.
- **R4 Generated code in Tas.** The blocks run through TasGenExecute's own compiler, which changes with the Tas
  version. Each block is proven by a licensed run, and the test simulation catches a failure before a long run.

## Decisions still open

- **D1 Overheating measure.** Proposed default: occupied hours with a resultant temperature above 28 °C, in the worst
  zone, threshold editable. Alternatives: a TM59-style criterion, or the sum over zones.
- **D2 Apply for plant targets.** The SAM model may not hold the TPD controllers. Proposed: apply them to the TPD file
  only, and say so.
