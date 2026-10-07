<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Simulate > Optimisation: the native SAM optimiser in SAM_UI (PR5, 7 Oct 2026)

**Status:** code, automated tests and a visual check are complete. Licensed real-system acceptance (A–E below) is
**pending**. Not merged; the owner must approve the implementation and the acceptance first. Branch
`feature/native-optimisation-ui` from `sow/2026-Q4` `3fa41ba4` (after SAM_UI#209). `PROJECT_PROGRESS.md` is not touched on this branch
(post-merge closeout).

This is the fifth PR of the Java-free GenOpt replacement. The first four are complete and frozen:
- PR1: SAM#182 (oracle) and SAM_Tas#85 (Gate T).
- PR2: SAM#183, the SAM.Math kernel.
- PR3: SAM_Tas#86, `GenOptDocument.RunNative`.
- PR4: SAM_Tas_Grasshopper#11, the Grasshopper component.

This PR builds against SAM `sow/2026-Q4` @ `f391e022` (SAM.Math unchanged since `79c101c2`) and SAM_Tas
`sow/2026-Q4` @ `da891dd2`.

## Why

SAM_UI becomes the human-facing place for repeated real-model optimisation testing, which is easier than Grasshopper.
The runtime is the proven one:

SAM_UI → SAM_Tas GenOpt objects → `GenOptDocument.RunNative` → SAM.Math kernel → `TasGenExecuteObjectiveEvaluator`
→ TasGenExecute.exe → Tas.

SAM_UI implements none of these:
- optimisation;
- `Variables.txt`;
- launching TasGenExecute;
- retries;
- output parsing;
- compatibility logic.

## What changed

- **Simulate > Simulate group: a new Optimisation button, immediately after Energy Simulation.** Energy Simulation is
  the single run; Optimisation is the iterative one.
  - It has its own icon in the SAM line style: axes, a convex curve and its minimum.
  - It is always enabled, because it works on a Tas project folder, not on the open model.
  - Its tooltip distinguishes it from Part O **Optimise (2B)**.
  - There is **no Native/Legacy selector**: SAM_UI exposes the native route only, and Java GenOpt is unreachable from
    SAM_UI.
- **The Optimisation window** (`TasOptimisationWindow`) uses the Prepare & Run style:
  - it merges `Themes/PartOStyles.xaml` and uses Roboto Light and a 72 px label column;
  - the content scrolls, with a fixed action bar underneath;
  - less-used settings sit in an "Advanced / Details" expander.

  It has these sections:
  - **Example**, with **Load**, for the two Systems Demo examples.
  - **Tas project**: the folder and the script.
  - **Algorithm**: GoldenSection or GPSHookeJeeves, each with its own settings, plus Max simulations (MaxIte).
  - **Parameters**: Name, Start, Min, Max and Step. With golden section, Start and Step show **"not used"** and are
    excluded from validation.
  - **Outputs**: one radio button marks the **primary objective** that is minimised.
  - **Readiness**: a ✓/⚠/✕ list.
  - **Run**: status, an indeterminate bar, "Simulation n (limit m) · elapsed", the last reported point, the lowest point
    so far, and a live trace grid with one column per parameter and output.
  - **Result**: the outcome, best point, best objectives, golden-section interval, retries, failure and run folder,
    with **Open run folder** and **Copy summary**.

  The expander holds the runs-folder override, MaxEqualResults, reference text and the loaded assembly locations.
- **Run**:
  - `RunNative` runs on a background thread (`Task.Run`), and progress comes back through `Progress<T>`. The modal
    window stays responsive.
  - The document is built from the window exactly as the SAM_Tas Grasshopper component builds it.
- **Cancellation (frozen semantics)**:
  - **Cancel run** cancels the token.
  - The running TasGenExecute finishes and no further evaluation starts. No process is killed.
  - Closing the window during a run asks first. On yes, the run stops after the current evaluation and the window
    closes afterwards. A second close does not ask again.
- **Stale-assembly guard.** A missing or stale assembly raises a TypeLoad, MissingMember or FileLoad exception, or a
  FileNotFound naming an assembly. Instead of a crash, the user gets a ✕ readiness line (or a message box from the
  ribbon) that names:
  - the error;
  - where SAM.Math and SAM.Analytical.Tas.GenOpt were loaded from;
  - the application folder.

  The kernel is probed when the window opens.
- **Form memory:** the current application session only (a static copy). No persistent settings, and no save or load
  of configuration files.

### Defaults and examples

| Value | Source |
|---|---|
| GoldenSection AbsDiffFunction `0.1`; GPSHookeJeeves mesh `2 / 0 / 1 / 4`; MaxIte `2000`; MaxEqualResults `100` | **SAM_Tas domain objects.** Read from `new GoldenSectionAlgorithm()`, `new GPSHookeJeevesAlgorithm()` and `new OptimizationSettings()`, never typed as literals. |
| Objective delimiter `name::` | SAM_Tas `Objective(name)` |
| Runs folder `<Tas folder>\SAM_NativeGenOpt` | SAM_Tas `RunNative` default |
| Example "Systems Demo — GoldenSection": `Setpoint`, Start 3, Min −5, Max 35, Step 1; outputs `Result` (primary), `Cost`, `CO2` | **Proven Systems Demo** (PR3 / PR4 acceptance inputs) |
| Example "Systems Demo — GPSHookeJeeves": `Setpoint`, Start 10, Min −5, Max 35, Step 2; same outputs | **Proven Systems Demo** |
| The window opens on the GoldenSection example, with the Tas folder and script **empty** | **New UI default** (owner decision 5) |
| Golden section with Start/Step blank or not a number: the lower bound and 0 are passed | **New UI default.** Golden section ignores them, and SAM_Tas only requires finite values. Numbers typed there are passed through, so the example reproduces the proven inputs exactly. |
| New parameter row: empty | **New UI default.** `NumberParameter`'s own 0 / 0–360 / 12 is an orientation example, so it is not offered. |

No Tas model, script or EDSL content is bundled. Only form values are loaded.

### Primary objective

The native optimiser minimises the first objective it is given (`ToSAM_OptimisationProblem`: "the first is
minimised"). The user marks one output as primary; SAM_UI passes it first and the others in their listed order. The
UI says this in a caption. Nothing in the generic behaviour names `Result`; only the examples set it as primary. The
optimiser only minimises: there is no Maximise, weighting or constraint (deferred to the AI-objective phase).

## Decisions and assumptions

These owner decisions were approved for this PR:
1. **Result summary.** The small presentation class (`TasOptimisationReport`) lives in SAM_UI. It repeats PR4's
   outcome rules and best-point rule, and tests pin them. Moving the rule into SAM_Tas is deferred to PR6.
2. **Memory.** The form is remembered for the session only.
3. **Icon.** A new icon, distinct from Energy Simulation's.
4. **Name check.** The non-blocking script-name warning is kept: a parameter or output never written as a quoted
   literal in the script raises ⚠.
5. **Opening state.** The window opens on the GoldenSection example with no folder or script.

Implementation decisions:
- **The rules live in SAM_Tas.** Readiness calls SAM_Tas' own `Convert.NumberParameters`, `Convert.Objectives`,
  `ToSAM_OptimisationProblem` and `ToSAM_Optimiser`, and shows their messages word for word. SAM_UI adds only:
  - "is this a number" (invariant, `.` decimal);
  - an unnamed or repeated output (`ObjectiveFunctionLocation` would otherwise drop or merge it silently);
  - exactly one primary output.
- **SAM.Math is referenced under an extern alias (`SAMMath`).** As a global namespace, it captured every unqualified
  `Math.Min/Abs/Round` in this assembly's `SAM.*` namespaces (CS0234 errors throughout existing Glazing, UValue,
  Thermal and Part O code). With the alias, only the optimisation files import it, and no existing line changed.
- **Live trace.** The kernel reports a main-iteration entry twice, once to OutputListingAll and once to
  OutputListingMain. The trace keeps the OutputListingAll reports, so its rows are exactly `OptimisationResult.Entries`
  (tested).
- **No "evaluation started" hook.** The kernel reports a point only after its evaluation, so the UI shows the last
  reported point and "Tas evaluation running", never a point being evaluated.
- **Late cancel withholds the result** (PR4 rule). A cancel requested before `RunNative` returned withholds even a
  Success.
- **Height.** The window starts no taller than the work area, which is 688 DIP for 1080p at 150% scaling. The
  content scrolls.

## Files changed

- `WPF/SAM.Analytical.UI.WPF/`:
  - `Windows/AnalyticalWindow.xaml(.cs)`: button, icon, tooltip, click handler with the load guard, always enabled.
  - `Windows/TasOptimisationWindow.xaml(.cs)` (new).
  - `Classes/TasOptimisation/{TasOptimisationInput, TasOptimisationDefinition, TasOptimisationParameterRow, TasOptimisationObjectiveRow, TasOptimisationCheck, TasOptimisationReport, TasOptimisationProgressState, TasOptimisationTraceRow}.cs` (new).
  - `Enums/TasOptimisationExample.cs` (new).
  - `Query/{TasOptimisationChecks, TasOptimisationAssemblies}.cs` (new).
  - `Resources/SAM_Optimisation.png` (new) and `Properties/Resources.resx` / `Resources.Designer.cs`.
  - `SAM.Analytical.UI.WPF.csproj`: HintPaths `SAM_Tas\build\SAM.Analytical.Tas.GenOpt.dll` and
    `SAM\build\SAM.Math.dll` (alias `SAMMath`).
- `WPF/SAM.Analytical.UI.WPF.Tests/`:
  - `TasOptimisation{Input, Checks, Report, Window}Tests.cs` and `Helpers/TasOptimisationWorkspace.cs` (new).
  - csproj: the two HintPaths, plus a ProjectReference to SAM_Tas'
    `SAM.Analytical.Tas.GenOpt.Tests.StubTasGenExecute`, the PR4 pattern.
- `documentation/NativeOptimisation-PR5.md` (this record).
- **Not changed:** SAM, SAM_Tas, SAM_Tas_Grasshopper, SAM_Deploy, Energy Simulation, Part O Optimise (2B), the legacy
  Java `Run()`.

## Validation

### Automated (this machine)

- **Build.** Release `msbuild SAM_UI.sln` (VS 18), clean Rebuild: **0 errors**, and no warnings in the new files.
  - APPDATA and USERPROFILE were redirected to scratch, so the post-build xcopy never touched the installed
    `%APPDATA%\SAM`, and `NUGET_PACKAGES` pointed at the real cache.
  - SAM was rebuilt from `f391e022` (SAM#184) first.
- **Full `SAM.Analytical.UI.WPF.Tests`: 2603/2603 pass** (2531 existing + 72 new). `git diff --check` is clean.
- **New tests (72).** They need no Tas, licence or COM:
  - **Input:**
    - defaults are read from the SAM_Tas objects;
    - each example builds byte-identical Command, Parameter, Template, Output, Config and Script GenOpt texts to
      PR4's acceptance inputs;
    - golden section excludes Start/Step and passes valid values through;
    - pattern search validates Start/Step;
    - the primary objective goes first whatever its name and position;
    - there must be exactly one primary;
    - unnamed or repeated outputs are problems;
    - SAM_Tas refusals arrive word for word: golden section with 2 parameters, fractional or too-small mesh,
      MaxEqualResults 1, MaxIte 0, a `,` in a name, no parameter.
  - **Checks:**
    - folder, Tas files (SAM_Tas' allow-list, top level only), script, TasGenExecute (stub and installed path), the
      settings message word for word;
    - nothing is created;
    - name warnings do not block;
    - runs-folder resolution;
    - assembly locations;
    - load-failure recognition and wording (a missing TasGenExecute.exe is not treated as a load failure).
  - **Report**, over **real SAM.Math kernel results** from a delegate evaluator: Success for golden section and for
    Hooke-Jeeves, the simulation limit, a nullspace stop, Cancelled, a late cancel withheld, EvaluationFailed and
    InitialPointInfeasible; best-point ties and NaN; messages; summary text; the live trace equals `Entries` for both
    algorithms.
  - **Window** (`[WpfFact]`), with **real `RunNative` runs through PR3's StubTasGenExecute child process**:
    - **Ribbon:** the button sits right after Energy Simulation, is enabled with no model, and has its own icon.
    - **Opening state:** the GoldenSection example, Start/Step "not used", and blocked on folder and script.
    - **Hooke-Jeeves** switches Start/Step back on.
    - **Load example** keeps the folder and script.
    - **Model-folder pre-fill** happens only when that folder holds Tas files.
    - **Session memory:** the form is remembered for the session only.
    - **A missing TasGenExecute** blocks the run and nothing starts.
    - **Golden section run:**
      - the trace equals `Entries`;
      - the evaluation folders equal the entries;
      - the best point is the lowest entry;
      - it is **bit-identical to a direct `RunNative`** of the same document.
    - **Hooke-Jeeves run:** the best point is the kernel's minimum.
    - **Primary objective:** the one chosen in the window is the one minimised (Cost drives Setpoint to −5).
    - **Cancel during evaluation 2:**
      - evaluation 2 finishes and writes `Output.txt`;
      - only `0001` and `0002` exist;
      - the outcome is Cancelled, with no best point;
      - the form is re-enabled.
    - **Close during a run:** it asks once, the window stays open while TasGenExecute runs, then closes; only `0001`
      exists.
    - **Declining to stop** keeps the run.
    - **Evaluation failure:** the evaluator's message is shown.
    - **Layout:** the actions stay inside the client area at minimum size, and the height is no more than the work
      area.
    - **No Java route:** the built assembly references `GenOptDocument::RunNative`, and no member of the Java route
      (`Run`, `ExecutableFile`, `Command`, `TasGenOptJavaPath`, `Create.Command`, `SetProjectDirectory`).
- **Mutation check.** Each of five deliberate defects was caught, then reverted:

  | Mutation | Tests that failed |
  |---|---|
  | M1: golden-section best = last entry | 4 |
  | M2: primary objective not moved first | 3 |
  | M3: late cancel not withheld | 1 |
  | M4: golden section validates Start/Step | 1 |
  | M5: Cancel does not cancel the token | 2 |

- **Visual check.** The window was rendered (RenderTargetBitmap) in three states: opening, Hooke-Jeeves, and after a
  stub run. A stub with its optimum at the proven Setpoint gives the Systems Demo's golden-section sequence: 11
  simulations, best 4.968943799848584, interval [4.767943850222925, 5.093168600454254].
- **CI.** SAM_UI CI builds the solution (including the tests and the SAM_Tas stub project) and runs no tests (the
  repository convention). See the PR checks.

### Licensed real-system acceptance: PENDING

Planned in the real SAM_UI app, run from a recorded build, with a fresh copy of the PR3/PR4 Systems Demo workspace for
each run (the `Systems Training` T3D/TBD/TPD/TSD and the Demo script; nothing licensed is committed):

| Case | Expected (frozen PR3/PR4 reference) |
|---|---|
| A. GoldenSection example | 11 simulations; best Setpoint `4.968943799848584`; Result `7360.04370117188`; interval [4.767943850222925, 5.093168600454254]; evaluations identical to PR3/PR4 |
| B. GPSHookeJeeves example | 16 simulations; best Setpoint `5`; Result `7360.04370117188` |
| C. Cancel during evaluation 2 | evaluation 2 finishes; no evaluation 3; no process killed |
| D. Refusal (golden section with 2 parameters) | SAM_Tas message; nothing runs; no run folder |
| E. Environment | no java or cmd child of SAM_UI; `HKCU\Software\EDSL\TasManager` unchanged; SHA-256 of `SAM.Math.dll`, `SAM.Analytical.Tas.GenOpt.dll` and `SAM.Analytical.UI.WPF.dll` recorded |

## Unresolved issues, risks

- **Deployment (carried from PR4; a SAM_Deploy release task, not fixed here).** SAM_Deploy must ship one current
  `SAM.Math.dll` to every SAM/Rhino package and dependency folder, and must ship `SAM.Analytical.Tas.GenOpt.dll`
  beside the SAM_UI application. That second requirement has not been verified in SAM_Deploy.
  - On this machine, `%APPDATA%\SAM\SAM.Math.dll` and the Rhino package copy are current (57,344 B, `D91C78B1…`,
    after the manual PR4 fix).
  - `%APPDATA%\SAM\SAMdependencies` and `%APPDATA%\SAM\Revit 2025/2026/2027` still hold the pre-PR2 copy (31,232 B,
    `59146D17…`).
  - SAM_UI's `AnalyticalWindow` is hosted only by the standalone `SAM Analytical.exe`, which loads from its own
    folder, so those stale copies do not affect PR5. The new load guard would name them if they were ever loaded.
  - The deployment issue must still be resolved before broad installer acceptance.
- **Rebuilt hashes differ between builds.** `SAM\build\SAM.Math.dll` rebuilt here from unchanged source hashes
  `46AA4623…`, against `D91C78B1…` for the installed copy. Acceptance records the hashes it actually ran.
- **Runtime and test coverage.**
  - Runs take minutes: about 3.5 min for golden section and 5.5 min for Hooke-Jeeves on the Systems Demo. The window
    is modal for that time.
  - Licensed acceptance covers one parameter, as in PR3/PR4. Multi-parameter runs are covered by the stub and kernel
    tests only.
- **Frozen items, untouched:** the legacy Java `Run()` launch defect (PR3 finding 1) and the GPSCoordinateSearch
  refusal (D3). PR4's report rule is duplicated, to be consolidated in PR6.

## Next step

Run licensed acceptance A–E and record it here. Then come the owner's review of the implementation and the
acceptance, PR CI on the reviewed head, and a merge (merge commit, `--match-head-commit`) only after the owner's
explicit approval. After that: post-merge CI, the `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4` (`[skip ci]`), and
deleting the branch.
