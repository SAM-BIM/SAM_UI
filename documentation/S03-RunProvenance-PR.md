# S03 - Run settings and version traceability - PR record

Roadmap: SAM `documentation/SIMULATION_ROADMAP_Q4_2026.md`, task S03 (Phase A). SAM_UI only; no SAM_Validation schema change.

Base: `sow/2026-Q4` `4d7f5bd8` (S02 closeout; S01 SAM_Tas#103 / SAM_UI#240 and S02 SAM_UI#241 merged).
Branch: `feature/s03-simulation-provenance`. One PR, not merged. **Validated content SHA: `87126efc`** (code and tests, after the review fixes; first content `29926a2f`).
Record commits follow it. The final PR head and hosted checks are in the PR description. `PROJECT_PROGRESS.md` is not
touched (post-merge closeout only).

## Status
Implementation and local tests complete on 2026-10-19 (overnight owner-authorised S03+S04 session). Awaiting hosted CI
and owner review. Not merged.

## Problem (baseline `4d7f5bd8`)
- Tas only (Energy Simulation, Run Simulation > Tas) wrote no run record at all.
- `openstudio-run.json` named the CLI path and the SAM.Analytical.OpenStudio version, but not the CLI's identity, the
  OpenStudio/EnergyPlus versions that ran, the design-day source or the run options.
- `engine-comparison-run.json` (Both) named each producer's path, not its binary. The SAM commit came only from the
  producers' B1a documents, which in a developer build require `SAM_COMMIT` to be set by hand.
- No manifest identified the SAM_UI build that ran.

## Change
- **`Classes/Provenance/SimulationRunProvenance.cs` (new):** one provenance mechanism for the three routes, written into
  each manifest as `"provenance"` (`sam-ui.run-provenance/1`, versioned independently of the manifests).
  - **Build:** `application.sourceRevision` is the build's own stamp, the `+` metadata of SAM.Analytical.UI.WPF's
    InformationalVersion, which SAM_Deploy installers set to their SAM_Deploy commit. It is never read from `SAM_COMMIT`
    or a Git HEAD. `application.components` gives name, version, InformationalVersion, path and SHA-256 of every SAM
    assembly the run used (SAM.Core, SAM.Analytical, SAM.Analytical.UI.WPF, plus SAM.Analytical.Tas, SAM.Analytical.OpenStudio /
    SAM.Core.OpenStudio or SAM_Validation's Benchmark assemblies per route). The hashes identify an unstamped developer build.
  - **Unknown items:** written as `"unknown"`, with the reason under `provenance.unknown["<item path>"]`. No item is blank.
  - **Never changes an outcome:** each collector catches its own failure and records it as unknown; the routes complete the
    provenance from their `finally` and a failure there is only a warning in the run's diagnostics/notes.
- **Tas only (`Modify/Simulate.cs`, `Classes/Tas/TasSimulationRun.cs` new):** the ordinary command's core writes
  `<project>.tas-run.json` (`sam-ui.tas-run/1`) beside the TBD on every outcome: Succeeded, Failed, Cancelled, Refused, and
  a thrown exception (recorded through an exception filter, so it still propagates unchanged). The status is the workflow's
  own return (`TasSimulationRun.StatusOf`), never read from files (S01b). Provenance:
  - engine: the registered `TBD.Document` COM server (`LocalServer32`), its product version (the TAS version) and SHA-256;
  - weather: `Embedded` only when the weather given to TAS has the same content hash (SAM WeatherData without its own Guid, canonical JSON v1)
    as the model's embedded weather, else `Explicit`; the file it was read from in the Simulate dialog, with its SHA-256;
  - design days: `DerivedFromWeather` only when the days handed to TAS equal those derived from the run's weather;
  - settings: the `WorkflowSettings` actually handed to `WorkflowCalculator` (new internal overload of
    `RunPartOSimulation` returns them; existing overloads delegate unchanged), with the weather identified rather than
    repeated, plus the Simulate dialog's inputs.
  - Not written for the Part O routes (Hub, guided Part O), whose case folders and records are their own.
- **OpenStudio only (`Modify/RunOpenStudioSimulation.cs`):** engine `openStudioSdkVersion` and `energyPlusVersionOfSdk`
  (read after the conversion, so a missing OpenStudio stack still fails exactly as before), `energyPlusVersion` from the
  first line of the run's `eplusout.err`, and the CLI that ran (path, SHA-256, file/product version); weather source,
  hash and original path; the design-day source the converter established; the `OpenStudioRunOptions` and conversion
  arguments given.
- **Both (`Modify/RunEngineComparison.cs`):** each producer's executable (path, SHA-256, versions; for a .NET apphost also its
  `.dll`) recorded before it runs, with its arguments; after the run, what each producer reported in its own B1a document
  (`samCommit`, `runnerCommit`, engine, route, weather, `designDaySource`, model hashes, state), copied as written. A
  document that is missing, unreadable or whose provenance is not an object (the S01b regression shapes) is recorded as
  unknown with its reason. `environment.SAM_COMMIT` / `RUNNER_COMMIT` record a manual override the producers inherited
  ("not set" otherwise).
- **Unchanged:** run statuses, exceptions, cancellation, output files and folders, simulation settings, conversion,
  comparison, B1a documents and schemas, SAM_Tas, SAM_OpenStudio, SAM_Validation. Every pre-S03 manifest key is kept with
  the same JSON kind; `provenance` is additive and the manifest schema ids are unchanged.

## Decisions and assumptions
- **No Git HEAD, no SAM_COMMIT for SAM_UI's own identity.** A developer build carries no source revision, so it is reported
  unknown with that reason, and its assembly hashes identify it. An installed build is identified by its stamp.
- **Producers' `samCommit` is still the producers' own.** benchmark-tas / benchmark-openstudio resolve it from
  `SAM_COMMIT`, then their own build stamp; their B1a validation rejects a missing one. SAM_UI does not pass a value it
  cannot verify. So a *developer-built* producer still needs `SAM_COMMIT` (now recorded as a manual override in the
  manifest); an installed, stamped producer (roadmap S06) does not. Owner decision listed below.
- **Weather content hash** for Tas: TAS receives a SAM `WeatherData`, not a file; its canonical JSON v1 hash
  (SAM_Validation's helper) identifies it. The file hash is added when it was read from a file in the dialog.
- `<project>.tas-run.json` is overwritten with the TBD/TSD by the next run of the same project into the same folder, as
  those files are.

## Tests
- New `SimulationRunProvenanceTests.cs` (46): build stamp parsing; SAM assemblies identified by path and SHA-256;
  `SAM_COMMIT` set in the environment is not used and an unstamped build is unknown with its reason; executable identity
  (hash, versions, apphost `.dll`), missing and version-less files; the TAS COM server (registered, versioned, version-less,
  not registered) and command-line parsing; EnergyPlus version from `eplusout.err`; Tas embedded weather + derived design days
  + the workflow settings, explicit weather from a file, a remembered weather, a run stopped before the workflow, design days
  not derived; Tas status mapping (incl. exception and cancel); the Tas record for a failed / cancelled / throwing run, a
  provenance collector that throws (status unchanged, record written), a missing folder; Both: every producer's binary and
  reported provenance without `SAM_COMMIT`, a manual `SAM_COMMIT` override, the five S01b malformed-document shapes
  (still PartiallyCompleted, OpenStudio kept), a producer not found, cancel and a throw; OpenStudio failed run; historical
  compatibility against committed pre-S03 S01b evidence (`engine-comparison-run.json`) and the pre-S03 OpenStudio key set.
  Each test checks that every `unknown` has a reason and no item is null or blank.
- New `TasRunManifestTests.cs` (6, WPF collection): the real ordinary Tas core, with the TAS workflow step faked through its
  existing seam: Succeeded (settings recorded equal the settings the workflow received), explicit weather file hash,
  Failed whatever files are in the folder, Cancelled, a thrown exception still propagates and is recorded, Refused (locked
  TBD) with its reason.
- `OpenStudioSimulationTests.RealRun_…` (existing, runs where OpenStudio is installed) now also asserts the provenance of a
  real run: CLI hash, EnergyPlus version from `eplusout.err`, SDK version, design-day source, weather, run options.

## Validation
Local results on this laptop. Dependencies: SAM `62b54156`, SAM_Tas `e84aeff6`, SAM_OpenStudio `d4216f3`,
SAM_Validation `68eded0`, all `sow/2026-Q4`, clean.
- `dotnet build WPF/SAM.Analytical.UI.WPF.Tests -c Release`: 0 errors; no new warnings in the changed files.
- Focused (`SimulationRunProvenanceTests|TasRunManifestTests|TasFailureReportingTests|SimulationConcurrencyGuardTests|EngineComparisonTests|OpenStudioSimulationTests|WpfCollectionTests`): **107/107 PASS** on `87126efc` (105/105 on `29926a2f`), including the real OpenStudio run.
- Mutation checks on `29926a2f` (each reverted): no Tas record → 6 fail; `SAM_COMMIT` used as source revision → 1 fails;
  weather "Embedded" by presence instead of content → 3 fail; status ignores an exception → 2 fail.
- Full `dotnet test WPF/SAM.Analytical.UI.WPF.Tests -c Release` (real `APPDATA`): `29926a2f` **2910/2910 PASS**; after the review fixes `87126efc` **2912/2912 PASS** (2860 + 52 new).
- Real engines (manifest evidence, not the S05 real-UI acceptance): Tas only explicit and embedded, OpenStudio only explicit and embedded, Both with and without `SAM_COMMIT`; see below.

## Independent review (2026-10-19) and fixes in `87126efc`
A separate regression-minded review of the combined S03+S04 diff confirmed no S03 status, exception, cancellation or
output change: the exception filter, definite assignment, overload delegation and Part O gating were checked. Fixes:
- **Weather `contentSha256`** included the WeatherData's own `Guid`, which is new on every read, so it was not a true
  content hash. It now leaves that `Guid` out. The S03 evidence manifests were written on `29926a2f`, before this fix, so
  their `contentSha256` values include the `Guid`. `Embedded` detection was correct either way.
- **`TBD.Document`** is now looked up in both registry views, so a 32-bit-only registration is no longer reported as "none".
- **No stale reasons:** a collector that fails part-way now leaves its item `unknown` with no reasons left over for its
  sub-items. The test helper also checks that every reason names an item that is `unknown`.
- **Tests that set `SAM_COMMIT`** now run in a non-parallel `ProcessEnvironmentCollection`.
- **New tests (2), mutation-checked:** removing the stale-reason cleanup → 1 fails; keeping the `Guid` in the content
  hash → 1 fails.
- **Accepted, not changed:**
  - The weather file is hashed when the run ends, not when the dialog read it.
  - Provenance for the Tas route is collected on the UI thread in the `finally`. The TBD.exe hash is cached per process;
    the added time was not measured.

## Representative manifest evidence
`documentation/evidence/s03-20261019/` (README there): six manifests from real engines on this licensed laptop, driven
through the SAM_UI code paths by a temporary harness (kept as `tools/S03EvidenceHarness.cs.txt`, not compiled), not the
WPF ribbon. Excerpt of `tas-explicit/box.tas-run.json` (real TAS 9.5.7, Budapest EPW):

```json
"status": "Succeeded",
"provenance": {
  "schema": "sam-ui.run-provenance/1", "route": "Tas",
  "application": { "sourceRevision": "unknown", "components": [ { "name": "SAM.Analytical", "sha256": "sha256:…" }, … ] },
  "engine": { "comServer": "TBD.Document", "version": "9.5.7.0",
              "executable": { "path": "C:\\Program Files\\Environmental Design Solutions Ltd\\Tas\\TBD.exe", "sha256": "sha256:aa658fcd…", "productVersion": "9.5.7.0" } },
  "weather": { "source": "Explicit", "contentSha256": "sha256:357bc8af…",
               "file": { "path": "C:\\TasOut\\s01b\\inputs\\Budapest.epw", "sha256": "sha256:472ffb4e…" } },
  "designDays": { "source": "DerivedFromWeather", "usedForSizing": true, … },
  "settings": { "workflow": { "SimulateFrom": 1, "SimulateTo": 365, "Sizing": true, "T3DRoute": "Direct", … }, "inputs": { … } },
  "unknown": { "application.sourceRevision": "this build's InformationalVersion '1.0.0' has no source revision after '+' (a local developer build); …" }
}
```

## Findings surfaced by the new provenance (not fixed here)
- **Both needs `SAM_COMMIT` with developer-built producers (blocker for the roadmap wording, owner decision).** With
  `SAM_COMMIT` unset, both producers refused their own B1a (`provenance.commit.malformed` at `$.provenance.samCommit`,
  exit 4) and the run Failed (`both-no-samcommit/`, logs included). This is the producers' existing behaviour, not an S03
  change; S03 makes it visible (`environment.SAM_COMMIT: "not set"`, `reported: "unknown"` with the reason). SAM_UI cannot
  supply a verified value. It resolves once the producers are stamped builds (roadmap S06), or by a SAM_Tas /
  SAM_OpenStudio / SAM_Validation change that is outside S03's scope.
- **OpenStudio CLI/SDK mismatch on this laptop:** OpenStudio Simulation loads the OpenStudio 3.10.0 SDK (EnergyPlus 25.1.0)
  in-process, but the CLI discovered on `PATH`, which runs the simulation, is ladybug_tools' OpenStudio 3.11.0, and the
  EnergyPlus that ran is 25.2.0-cf7368216c (from `eplusout.err`). Earlier records that say "OpenStudio 3.10 / EnergyPlus
  25.1" for SAM_UI-launched runs may describe the SDK, not the engine that ran. In the Both evidence, with OpenStudio
  3.10.0 first on the producers' `PATH`, benchmark-openstudio reports EnergyPlus 25.1.0. So OpenStudio only and Both can run
  different EnergyPlus builds on one machine, depending on `PATH`. Roadmap S06 (installed candidate with pinned OpenStudio
  dependencies) is where this belongs.

## Unresolved, risks
- **Roadmap acceptance, partly met:** every Tas and OpenStudio manifest has every item without manual environment
  variables, and every Both manifest records every item it can (unknown with a reason otherwise). But a Both run with
  developer-built producers does not *complete* without `SAM_COMMIT` (finding above). Owner decision: accept until S06, or
  a separate producer/SAM_Validation change (for example an explicit unknown `samCommit`).
- The Part O routes (Hub, guided Part O) write no `tas-run.json`.
- **Merge with S04 (SAM_UI#243).** Both PRs append assertions to the end of `OpenStudioSimulationTests.RealRun_…`, so
  whichever PR merges second has a textual conflict there. The resolution is to keep both blocks. It was resolved that
  way in a local integration merge, which passed the full suite (see the PR description).
- No licensed real-UI run of the three routes on this content (roadmap S05). See the evidence README for what was run.
- `sourceRevision` of an installed build is the SAM_Deploy commit (what its installer stamps), not a SAM commit; the record
  says so in `sourceRevisionOf`.

## Next step
Owner review of the PR. After merge, add the post-merge `PROJECT_PROGRESS.md` closeout on `sow/2026-Q4`.
