<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Design Optimisation: the form edits an Optimisation Definition (native Optimisation PR5a, 8 Oct 2026)

**Status:** code, tests, mutations, licensed acceptance (A 11/11, B 16/16 bit-identical in the real app) and
screenshots complete; PR open for owner review. **Not merged.** Stop for owner review before PR5b.

Branch `feature/optimisation-definition-form` from `sow/2026-Q4` @ `492cf0b9` (after the SAM_UI#214 closeout).
`PROJECT_PROGRESS.md` is not touched on this branch (post-merge closeout). SAM_UI only: SAM, SAM_Tas, SAM.Math and
SAM_Tas_Grasshopper are unchanged. PR5b (results formatting, CSV, full-precision copy) is not started.

## Context

Programme "SAM Native Optimisation: declarative definition and engineering UX" (schema `sam.optimisation/1`):

| PR | What | State |
|---|---|---|
| PR1 | SAM_UI#214, wording and layout | merged `1d60db39` |
| PR2 | SAM#186, Energy/Mass units, `QuantityFormatter` | merged `b52beaf3` |
| PR3 | SAM#187, `SAM.Core.Optimisation` (model, strict reader, canonical writer, L1–L5 diagnostics) | merged `4eb0c49b` |
| PR4 | SAM_Tas#88, the Tas adapter (`ToGenOptDocument`, capabilities, script diagnostics) | merged `a3837acb` |
| **PR5a** | **this PR: the SAM_UI form ⇄ `OptimisationDefinition`, examples as JSON, run through `ToGenOptDocument`** | open |
| PR5b | engineering result formatting, raw-value tooltip, CSV trace, full-precision copy | not started |

Built against SAM `sow/2026-Q4` `721b5fae` and SAM_Tas `sow/2026-Q4` `861d3e7` (`SAM.Analytical.Tas.GenOpt` rebuilt in
Release on this machine before the SAM_UI build).

## What changed

### The model (`Classes/TasOptimisation/TasOptimisationInput.cs`)

The window's form is still string-backed (a typed non-number stays an inline form problem), but its model is now a
SAM.Core.Optimisation `OptimisationDefinition`:

- **`Base`**: the definition the form was last loaded from (an example), kept as a copy. Everything the form does not
  show comes from it unchanged: name, description, notes, model (engine + description), each variable's type and
  quantity, each output's quantity and aggregation, the objective's sense, the constraints.
- **`Load(OptimisationDefinition)`** fills the form and sets `Base`; `Load(TasOptimisationExample)` loads an example.
  A method setting the definition leaves to the engine shows the SAM_Tas default.
- **`TryGetDefinition(out OptimisationDefinition, out problems)`** overlays the form onto a copy of `Base` (or a new
  `tas-script` definition), matching variables and outputs **by name** to keep their non-form fields. Its problems are
  only form-level reads: not a number / not a whole number, an unnamed or repeated output, no (or two) objective.
- **Property names are definition terms:** `OptimisationAlgorithm` (was `AlgorithmType`), `Tolerance`
  (`AbsDiffFunction`), `StepReductionFactor` (`MeshSizeDivider`), `InitialStepExponent` (`InitialMeshSizeExponent`),
  `StepExponentIncrement` (`MeshSizeExponentIncrement`), `StepReductions` (`NumberOfStepReduction`),
  `MaximumSimulations` (`MaxIterations`). `MaxEqualResults` is removed (not part of the definition; SAM_Tas keeps its
  default 100, as before).
- **`TasOptimisationDefinition` (the GenOpt wrapper) is retired.** The run is
  `definition.ToGenOptDocument(directory, scriptText).RunNative(runsDirectory, tasGenExecutePath, progress, ct)`.
- **Rows:** `TasOptimisationParameterRow` and `TasOptimisationObjectiveRow` gain `Description` and `Unit`.

### Examples (`Resources/Optimisation/*.json`, `Query/TasOptimisationExamples.cs`)

The two examples are embedded byte-for-byte copies of SAM's `SAM.Tests/Golden/Optimisation/systems-demo-{golden-section,
hooke-jeeves}.json` (an `.gitattributes` there keeps them LF on every checkout), read with
`SAM.Core.Optimisation.Create.OptimisationDefinition` against the Tas capabilities. `Query.TasOptimisationObjectiveNames`
gives the output names in adapter order (objective first, then the recorded outputs); `TasOptimisationVariableNames` the
variables. Progress and trace use these names.

### Checks (`Query/TasOptimisationChecks.cs`)

- **Setup** = the form read as a definition, then `definition.Diagnostics(TasOptimisationCapabilities())`: each error
  is one Blocked line, "message hint" (PR3 wording). With no error, the final SAM_Tas gate
  (`Query.TasOptimisationGate`): `ToGenOptDocument`, then exactly what `RunNative` applies before it creates anything
  (`NumberParameters`, `Objectives`, `ToSAM_OptimisationProblem`, `ToSAM_Optimiser`); a refusal is shown word for word
  without the "native route" prefix, as before. The summary sentence is unchanged in wording; its simulation limit is
  the one SAM_Tas runs.
- **Warnings** (never block): the definition's own (for example OPT300, an unrecognised unit) and SAM_Tas'
  `TasScriptDiagnostics` (OPT501 variable never read, OPT502 output never written) instead of the local quoted-literal
  heuristic. Titles stay "Design variable 'X'" / "Output 'X'".
- File, script and TasGenExecute checks are unchanged.

### Window (`Windows/TasOptimisationWindow.xaml(.cs)`)

- **Setup:** Design variables gain **Description** and **Unit** columns (Name, Description, Unit, Start, Minimum,
  Maximum, Step); Recorded outputs gain Description and Unit; the objective's own description and unit sit under the
  Objective box (`grid_Objective`, bound to the objective's row). Units are labelled as declared and never converted.
- Method settings controls are named in definition terms (`textBox_Tolerance`, `textBox_StepReductionFactor`,
  `textBox_InitialStepExponent`, `textBox_StepExponentIncrement`, `textBox_StepReductions`,
  `textBox_MaximumSimulations`); GenOpt names stay in tooltips only, as PR1 left them.
- The Method list is `OptimisationAlgorithm` (GoldenSection, HookeJeeves); `Query.TasOptimisationAlgorithmName` and its
  converter take `OptimisationAlgorithm`.
- Run builds the document through SAM_Tas' adapter; a refusal there (not reachable after the checks) is reported, not
  thrown.
- Unchanged: outcome and report rules (`NativeGenOptOutcome`), cancellation, session memory (now with `Base`),
  Diagnostics, PR1 wording, full-precision values.

### Deployment (`Query/TasOptimisationAssemblies.cs`)

`SAM.Core.Optimisation` is one of the optimisation assemblies: its location is listed in Diagnostics, and the probe
reaches `OptimisationDefinition`, SAM_Tas' `TasOptimisationCapabilities()` and `TasOptimisationDefinitionException`, so
a pre-PR4 `SAM.Analytical.Tas.GenOpt.dll` is reported when the window opens. No static field of a SAM.Core.Optimisation
type is added to a class the existing paths touch (`Query`); `OptimisationAlgorithms` is a property that returns a new
list. A missing `SAM.Core.Optimisation.dll` stops the window opening; the existing ribbon guard reports it with the
load-failure text (which now names the DLL).

## Decisions and assumptions

1. **The form stays string-backed; the definition is its model** (as designed in the PR4 session). `Base` is a copy,
   so editing the form never changes the loaded definition; the session memory copies it too.
2. **Matching by name** keeps a variable's or output's non-form fields (type, quantity, aggregation). A renamed row
   keeps its description and unit (form columns) but starts with no declared quantity.
3. **A declared quantity follows its unit:** when the unit in the form differs from the base's, the quantity becomes
   unspecified. Otherwise a stale quantity (for example currency after changing GBP to kWh) would be an OPT302 error
   that the form cannot show or fix. The unit alone is still checked (OPT300 warning when unknown).
4. **Golden section's start and step** are passed through when they are numbers the definition accepts (so the
   examples reproduce the proven inputs, 3/1); otherwise they are left out, which SAM_Tas runs as the lower bound and 0.
   Beyond the prompt's "not a number → null", a number the definition would refuse (start outside the range, OPT205;
   step ≤ 0, OPT206) is also left out: the fields are shown as "not used" for golden section and must never block a run.
   Before this PR such a value was passed through and ignored by the kernel. Effect on the files (proven by a stub-run
   test): Variables.txt is `name,value,minimum,maximum,step,type`, so a left-out start changes no byte, and a
   left-out step writes 0 in the step field (as the pre-PR5a form already did for an empty or non-numeric step). The
   points evaluated are the same. The examples (3/1) are passed through unchanged.
5. **Empty start or step** is "not given" (null), so Hooke–Jeeves without a start is the definition's OPT406/OPT407
   with its hint, rather than "'' is not a number". A typed non-number is still a form problem.
6. **Hooke–Jeeves settings and Maximum simulations are whole numbers** (the definition's `int?`); "2.0" is accepted,
   "2.5" is a form problem ("is not a whole number"). Before, "2.5" reached SAM_Tas and was refused there; the run is
   blocked either way.
7. **Engine defaults stay left to the engine:** a setting the base definition omits, still showing the SAM_Tas default,
   is written as omitted, so a loaded definition reads back unchanged (canonical `ToJson` equal). The run is the same
   either way (the adapter maps omitted to the same default).
8. **Name warnings now come from SAM_Tas** (`TasScriptDiagnostics`, message + hint). The scan is SAM_Tas' literal one:
   only `Variables["…"]` reads and `ScriptOutput.SetValue("…", …)` writes count, so a script that only mentions a name
   in a string now gets a warning (the old heuristic accepted any quoted literal). The hint names the case-differing
   match or the names the script uses, and it contains the call syntax (`ScriptOutput.SetValue("…", value)`); PR1's
   "no syntax in the warning" assertion was dropped for that reason. **Owner review point.**
9. **PR3 warnings are shown as Warning checks** with the same titles; PR3 information (for example OPT408, golden
   section keeps start/step) is not shown.
10. **The SAM_Tas gate is reproduced from the document** (`CommandFile.Parameters`, `ConfigFile.Simulation
    .ObjectiveFunctionLocation`) so the conversions are exactly the ones `RunNative` applies; no mapping is repeated in
    SAM_UI.
11. **Trace and summary formatting are unchanged** (full precision, invariant): engineering formatting is PR5b.
12. **Control names follow the definition terms** (`textBox_Tolerance` …); GenOpt names are tooltips only.

## Files changed (22, SAM_UI only)

- `WPF/SAM.Analytical.UI.WPF/`: `SAM.Analytical.UI.WPF.csproj` (SAM.Core.Optimisation HintPath, two embedded
  resources); `Classes/TasOptimisation/{TasOptimisationInput, TasOptimisationParameterRow, TasOptimisationObjectiveRow,
  TasOptimisationAlgorithmNameConverter}.cs`; `Classes/TasOptimisation/TasOptimisationDefinition.cs` **deleted**;
  `Enums/TasOptimisationExample.cs` (comments); `Query/{TasOptimisationChecks, TasOptimisationAlgorithmName,
  TasOptimisationAssemblies}.cs`; new `Query/TasOptimisationExamples.cs`; new `Resources/Optimisation/{.gitattributes,
  systems-demo-golden-section.json, systems-demo-hooke-jeeves.json}`; `Windows/TasOptimisationWindow.xaml(.cs)`.
- `WPF/SAM.Analytical.UI.WPF.Tests/`: `SAM.Analytical.UI.WPF.Tests.csproj` (SAM.Core.Optimisation HintPath);
  `TasOptimisation{Input,Checks,Window}Tests.cs`; `TasOptimisationScreenshotHarness.cs` (state 6).
- `documentation/NativeOptimisation-PR5a-DefinitionForm.md` (this record).
- Not changed: SAM, SAM_Tas, SAM.Math, SAM_Tas_Grasshopper, SAM_Deploy; `TasOptimisationReport`,
  `TasOptimisationProgressState`, `TasOptimisationCheck`, `TasOptimisationTraceRow`; the ribbon command.

## Validation (2026-10-08, this machine)

Environment: SAM `721b5fae` and SAM_Tas `861d3e7` (both `sow/2026-Q4`, up to date with origin);
`SAM.Core.Optimisation.dll` built from SAM `466f43e4` (its last code commit); `SAM.Analytical.Tas.GenOpt.dll` rebuilt in
Release before the SAM_UI build. All builds and tests ran with `APPDATA`/`LOCALAPPDATA`/`USERPROFILE` redirected to a
seeded scratch profile (`C:\TasOut\pr5a-def\profile`: copies of `%APPDATA%\SAM\resources` and
`Documents\SAM\resources`) and the real `NUGET_PACKAGES`.

- **Build:** VS 18 MSBuild, `SAM_UI.sln -restore -t:Rebuild -p:Configuration=Release`: 0 errors, no new warning in the
  changed files.
- **`TasOptimisation*`: 121/121** (96 after PR1). **Full `SAM.Analytical.UI.WPF.Tests`: 2683/2683** (2658 after PR1,
  plus 25 net new cases).
- **Ported:** every existing `TasOptimisation*` test, to the definition terms. Unchanged in intent and passing:
  - the stub window run is **bit-identical to a direct `RunNative`** of the document SAM_Tas builds from the same
    definition: trace, objective bits, and now every evaluation's Variables.txt bytes;
  - cancel, close, decline and failure; the objective moved first;
  - no Java route: the metadata test now also requires `ToGenOptDocument`, `TasOptimisationCapabilities` and
    `TasScriptDiagnostics`.
- **New tests:**
  - form ⇄ definition round trip for both examples: `Load` then `TryGetDefinition` gives a canonical `ToJson` equal to
    the example's and to the fixture text, also through the session copy;
  - `Base` fields survive edits (name, description, notes, model, type, quantity, aggregation, sense, constraints; a
    constraint shows OPT414); a quantity follows its unit; omitted engine settings stay omitted;
  - the embedded examples are **byte-identical to the SAM fixtures** (read from the sibling SAM checkout), and they
    build exactly the proven GenOpt files;
  - golden section never blocks on a hidden start or step, and a stub run proves what leaving them out does to
    Variables.txt (decision 4); Hooke–Jeeves settings are whole numbers;
  - Setup shows PR3 errors (OPT412 blocked with message and hint; one line per error), OPT501/OPT502 as non-blocking
    warnings in SAM_Tas' wording, OPT300 for an unknown unit, and a SAM_Tas gate refusal word for word;
  - the window: the Setup columns and the objective's description and unit bound to the definition, a definition error
    blocks Run, the trace labels the objective first, and the probe reports a pre-PR4 GenOpt or a missing
    SAM.Core.Optimisation.
- **Mutations:** each was applied alone, the tests project rebuilt, `TasOptimisation*` run, then reverted. All caught:

  | Mutation | Failing tests |
  |---|---|
  | M2a: output names in definition order, not objective first (`TasOptimisationObjectiveNames`) | 8 |
  | M2b: the objective is the first output row, not the chosen one (`TryGetDefinition`) | 11 |
  | M5: Cancel does not cancel the token | 3 |
  | M6: golden-section start/step pass-through dropped | 4 |
  | M7: `Base` not used (a new definition each time) | 5 |
  | M8: quantity kept when the unit changes | 1 |

- **Licensed acceptance in the real `SAM Analytical.exe`** (Release build of this branch). Driver
  `C:\TasOut\pr5a-acc\drive.ps1`: a UIA copy of the PR5 driver with the PR1 title and example names and the new
  columns. Each case gets a fresh copy of the Systems Demo workspace (Script.txt SHA-256 `d68a7e2e…`). Each case is
  compared with the recorded licensed PR5 run in `C:\TasOut\pr5-acc\{A,B}-ws\SAM_NativeGenOpt\*`: Variables.txt bytes,
  and the Result/Cost/CO2 doubles bit for bit.
  - **A, golden section:** "Optimum found after 11 simulations", Setpoint 4.968943799848584 (simulation 9), Result
    7360.04370117188, final interval [4.767943850222925, 5.093168600454254]. **11/11 evaluations bit-identical**;
    snapshot Script.txt identical. 256 s.
  - **B, Hooke–Jeeves:** "Optimum found after 16 simulations", Setpoint 5 (simulation 9), Result 7360.04370117188.
    **16/16 bit-identical**; 41 trace rows, as in PR5. 357 s.
  - **D, golden section with a second variable:** Setup blocked with the definition's OPT412 wording ("Golden section
    optimises exactly one design variable; 2 are defined. Remove a variable, or choose Hooke–Jeeves."), plus an OPT501
    warning for the new name that names "Setpoint". Run disabled; no runs folder created.
  - In A, B and D:
    - the form showed the example's descriptions and units (Result and Cost in GBP);
    - no Setup warning for the real script (SAM_Tas' scan finds every name);
    - no java, javaw or cmd process; `HKCU\Software\EDSL\TasManager` unchanged; the application exited with 0;
    - TasGenExecute launches seen by the 250 ms monitor: 11 in A, 15 of 16 in B. The B count is identical to the PR5
      record: the sampling misses one short-lived launch, and all 16 evaluation folders have their output.
  - **Not rerun:** C (cancel during evaluation 2). Cancellation is unchanged code and is covered by M5's three tests.
  - **Evidence** (not committed; EDSL material): `C:\TasOut\pr5a-acc\{A,B,D}-drive.log`, `-procs.log`,
    `-compare.log` and screenshots.
- **RenderTargetBitmap screenshots** (`SAM_OPT_SCREENSHOTS`, harness `TasOptimisationScreenshotHarness`; not committed
  because they show local temp paths): `C:\TasOut\pr5a-def\screenshots\{1-opening, 1b-ready, 2-hooke-jeeves,
  3-after-run, 4-diagnostics, 5-search-details, 6-definition-error}.png`.
  - 1b shows the Setup tab with the Description and Unit columns for design variables and recorded outputs, and the
    objective's description and unit row.
  - 6 shows the OPT412 Setup line, the OPT501 warning and the blocked footer.
- `git diff --check` clean; every new `.cs` file has the SPDX header.
- PR CI (`build`, `spdx`): pending.

## Unresolved issues, risks

1. **Owner review point:** the script-name warnings now carry SAM_Tas' hint, which includes the call syntax (decision
   8), and a name mentioned only as a bare string no longer counts.
2. **Deployment (PR8):** SAM_UI now needs `SAM.Core.Optimisation.dll` (and `SAM.Units.dll`) beside the app, as well
   as a PR4 `SAM.Analytical.Tas.GenOpt.dll`.
   - The DLL is copied to `SAM_UI\build`; shipping it through SAM_Deploy is PR8.
   - The existing ribbon guard reports a missing DLL. SAM_UI has no isolated-load-context test of that path (SAM_Tas'
     PR4 deployment tests cover its own types).
3. The Setup tab is longer (the objective's description/unit row, wider rows). Not yet used by a person.
4. Values are still full precision, and units are labels only; formatting is PR5b.
5. Unchanged from earlier records: the SAM_Deploy shipping task (SAM.Math, GenOpt).

## Next step

Owner review of this PR. After approval and green CI: merge with `--match-head-commit`, then write the
`PROJECT_PROGRESS.md` closeout on SAM_UI `sow/2026-Q4` with the merge SHA (`[skip ci]`). Start PR5b (result formatting
with the PR2 `QuantityFormatter`, raw-value tooltip, CSV trace export, full-precision copy) only when the owner
authorises it.
