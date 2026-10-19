# S02 - Shared simulation concurrency guard - PR record

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S02 (Phase A). SAM_UI only.

Base: `sow/2026-Q4` `ce23dca1` (S01b closeout; S01a SAM_Tas#103 `e483877c` and S01b SAM_UI#240 `5b379191` merged).
Branch: `feature/s02-simulation-concurrency-guard`. One PR, draft, not merged. **Validated content SHA: `578ede41`** (code and tests). Record commits follow.
The final PR head and hosted checks are in the PR description. `PROJECT_PROGRESS.md` is not touched (post-merge closeout only).

## Status
Implementation and local tests complete on 2026-10-19. Awaiting hosted CI and owner review. Not merged.

## Problem (baseline `ce23dca1`)
`AnalyticalWindow` kept two separate window flags, `openStudioSimulationRunning` and `engineComparisonRunning`.
Energy Simulation / Run Simulation > Tas (`SimulateTas`) had none, and the comparison window's Run (also Both) had its
own `running` flag. So, for the same model, an OpenStudio run did not stop a Both run or a Tas run, and the reverse.

## Change
- **`Classes/SimulationGuard.cs` (new):** one guard per open model. The key is the window's `UIAnalyticalModel`
  instance: `AnalyticalWindow.Open` makes a new one per opened model, so the key is the model the user simulates.
  - `TryAcquire(model, simulation, out running)` returns a lease, or null plus the name of the running simulation.
  - `Running(model)` returns that name, or null when nothing runs.
  - `Dispose` releases once. A late second dispose cannot release a later lease.
  - It uses a lock and a `ConditionalWeakTable`, so there is no leak.
  - It is not a queue, and nothing runs in parallel.
- **`Windows/AnalyticalWindow.xaml.cs`:** the guard is taken before any dialog, worker or producer. It is released in
  `finally` on every path. The guard replaces both flags.
  - **Energy Simulation and Run Simulation > Tas:** `SimulateTas`.
  - **OpenStudio Simulation and Run Simulation > OpenStudio:** `RunOpenStudioSimulationAsync`.
  - **Run Simulation > Both:** `RunBothEnginesAsync` holds one guard for the whole run. Both's internal sequential Tas
    then OpenStudio producers (`Modify.RunEngineComparison`) run inside it and take no guard of their own.
  - **Run Simulation button:** refuses before its dialog opens when a simulation runs. The `RunSimulationWindow` engine
    options are no longer disabled per flag; the file is unchanged.
  - **Optimisation's "Run Energy Simulation…" follow-up:** now goes through `SimulateTas`, so it is guarded too.
  - **OpenStudio button:** stays off while an OpenStudio run holds the guard, as before.
  - A refused start shows "<Simulation> is already running for this model, so nothing was started. …"
    (title "Simulation already running"). It changes nothing else: no producer is started, and no results, diagnostics,
    status, files, `runSimulationChoice` or dialog are touched.
- **`Windows/EngineComparisonWindow.xaml.cs`:** Run takes the same guard as Both. Its `running` flag remains only for
  the window's close prompt.
- **Test seams:** `internal` delegates `TasSimulation`, `OpenStudioSimulation`, `BothEnginesSimulation` and
  `SimulationRefused` on both windows. When null, the real simulation runs and a message box is shown.
- **Unchanged:** simulation physics, model conversion, comparison logic, B1a schemas, status semantics, SAM_Tas,
  SAM_OpenStudio and SAM_Validation.

## Decisions and assumptions
- "Same model" means the open model's `UIAnalyticalModel` instance, not `AnalyticalModel.Guid`. The instance is how the
  window and the comparison window share the model. A Tas run replaces the model object, which would make a key based
  on the model object unstable.
- Consequences of this choice:
  - Opening another model while an OpenStudio or Both run goes in the background gives a new key, so that model can
    be simulated.
  - Re-opening the same file in the same window also gives a new key. Each OpenStudio or Both run writes into its own
    new run folder, so the two runs do not share output files.
- Part O Hub simulations (`SimulatePartO` / Prepare & Run) are not among the four S02 entry points and are not guarded.
  This is a gap left for the owner to decide.
- The guard does not cover other processes or other SAM_UI instances.

## Tests
New `WPF/SAM.Analytical.UI.WPF.Tests/SimulationConcurrencyGuardTests.cs` (8 tests, in `WpfCollection`). All
simulations are fakes, through the seams. No Tas, OpenStudio or licence is needed.
- **Guard:** a second acquire is refused and names what runs. Release happens once, and a late dispose is safe. A
  different open model is not blocked.
- **Async first run:** OpenStudio button, Run Simulation > OpenStudio, Run Simulation > Both and the comparison
  window's Run are each held open. While each runs, every entry point is refused at once and starts nothing:
  - Energy Simulation;
  - Run Simulation > Tas, OpenStudio and Both;
  - the OpenStudio button;
  - the comparison window's Run;
  - the Run Simulation button.

  The first run's call count, guard holder and OpenStudio button state are unchanged. The first run then completes and
  releases the guard.
- **Tas first run** (Energy Simulation and Run Simulation > Tas): the same refusals, made from inside the running Tas
  simulation.
- **Release:** checked for every entry point after success, failure (returns normally), cancellation
  (`OperationCanceledException`) and a thrown exception. The next start is accepted each time.
- **Sequential runs:** all six entry points, one after another. Every one starts and none is refused.
- **Both's internal producers:** run through the real `Modify.RunEngineComparison` with faked producer processes, inside
  Run Simulation > Both's guard. Tas then OpenStudio are both called, with the guard held by Both throughout.

## Validation
Local results are on this laptop. Dependencies are SAM `62b54156`, SAM_Tas `e84aeff6`, SAM_OpenStudio `d4216f3` and
SAM_Validation `68eded0`, all on `sow/2026-Q4` and clean.
- `dotnet build WPF/SAM.Analytical.UI.WPF.Tests -c Release`: succeeded, with no new warnings.
- Focused: `dotnet test … --filter "FullyQualifiedName~SimulationConcurrencyGuardTests|FullyQualifiedName~WpfCollectionTests"`
  gave **9/9 PASS**.
- **Mutation checks** (each reverted):
  - Tas does not take the guard: 2 tests fail.
  - The Tas release is removed: 3 tests fail.
- Full `dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c Release` (real `APPDATA`) on `578ede41`: **2860/2860 PASS** (2852 before plus the 8 new tests).
- Hosted Build and SPDX: in the PR description.

## Unresolved, risks
- No licensed real-UI concurrent-start attempt was run. Roadmap S05 includes one.
- Part O Hub simulations are not guarded (see decisions).

## Next step
Owner review of the draft PR. After merge, add the post-merge `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`. S03 needs a
separate owner start. Recommended: new session, Claude Opus 5.5, effort High, Build mode, because it is a separate task.
