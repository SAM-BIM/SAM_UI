# S03 run provenance - representative manifests (2026-10-19)

Real engines on the licensed development laptop (TAS 9.5.7, OpenStudio SDK 3.10.0), SAM_UI at S03 content `29926a2f`,
dependencies SAM `62b54156`, SAM_Tas `e84aeff6`, SAM_OpenStudio `d4216f3`, SAM_Validation `68eded0`. Model
`box-aperture.json` and `Budapest.epw` from `C:\TasOut\s01b\inputs` (machine-local, as in S01b).

**What ran:** the SAM_UI code paths, driven by `tools/S03EvidenceHarness.cs.txt` (a temporary xUnit harness, not part of the
test project) - not the WPF ribbon. The Tas cases use the ordinary Energy Simulation / Run Simulation > Tas core with the real
TAS workflow (`Modify.Simulate` seam with no workflow override). This is manifest evidence, **not** the S05 licensed
real-UI acceptance. Paths in the files are machine-local.

| Folder | Route | Outcome | What it shows |
|---|---|---|---|
| `tas-explicit/box.tas-run.json` | Tas only, real TAS, explicit EPW | Succeeded | TAS 9.5.7.0 from the registered `TBD.Document` server with its SHA-256; weather `Explicit` with the EPW's SHA-256 (equal to S01b's `472ffb4e…`) and the WeatherData content hash; design days `DerivedFromWeather`; the `WorkflowSettings` that ran |
| `tas-embedded/box.tas-run.json` | Tas only, real TAS, model weather | Succeeded | weather `Embedded` by content hash; file unknown with its reason |
| `openstudio-explicit/openstudio-run.json` | OpenStudio only, explicit EPW | Succeeded | SDK 3.10.0 / EnergyPlus 25.1.0 in-process, but the CLI that ran is ladybug_tools' OpenStudio 3.11.0 and EnergyPlus 25.2.0 (from `eplusout.err`) - see the finding in the PR record |
| `openstudio-embedded/openstudio-run.json` | OpenStudio only, embedded weather | Succeeded | weather `Embedded`; the exported EPW's hash |
| `both-no-samcommit/` | Both, real producers, `SAM_COMMIT` not set | Failed | both developer-built producers refuse their own B1a (`provenance.commit.malformed` at `$.provenance.samCommit`, logs included); the manifest still records each producer's binary, `environment.SAM_COMMIT: "not set"` and `reported: "unknown"` with the reason |
| `both-manual-samcommit/` | Both, real producers, `SAM_COMMIT=62b54156…` | Compared | the producers' reported `samCommit` came from the manual override, which the manifest records under `environment`; its value was **not** verified to be the commit the producers were built from |

For the Both cases the harness put the OpenStudio 3.10.0 `bin` first on the producers' `PATH`, as the S01b runs did.
