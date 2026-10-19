# Licensed Tas / OpenStudio end-to-end validation (Q4, 2026-10-19)

Status: **validation complete; documentation-only draft PR. No product, test, physics, tolerance or baseline change.**
Base: `sow/2026-Q4` (SAM_UI `4ee45fef`). Record branch: `validation/licensed-tas-openstudio-e2e-q4`.
Context: SAM_Validation#15, SAM_UI#237 and SAM_UI#238 are merged; the owner now has an active Tas licence. This record
is the licensed end-to-end acceptance and the first diagnosis of the Tas/OpenStudio numerical differences.
Machine-local run root: `C:\TasOut\licensed-e2e-20261019` (must be copied/provisioned elsewhere). Selected portable
evidence: `documentation/evidence/licensed-e2e-20261019/`.

## Result

| Test | Tas | OpenStudio | Comparison | Result |
|---|---|---|---|---|
| Small reference model, embedded weather (box-aperture; same inputs as historical `box-both`) | Licensed, exit 0, 21 s, Success | E+ 25.1.0 / SDK 3.10.0, exit 0, 6 s | Compared; 6/0/10/2, 4 contributing + 6 informational; Coverage Warn; Provenance + Reconciliation Pass | **PASS** (workflow); numbers differ, explained below |
| Small reference model, explicit source EPW (Budapest TMYx) | Licensed, exit 0, 18 s | exit 0, 6 s | Compared; 6/0/10/2 | **PASS** (workflow) |
| Large building (HungaryHouse, 14 spaces / 84 panels / 20 apertures, embedded design days, explicit EPW) | Licensed, exit 0, 38 s, TSD written (crash **not reproduced**) | exit 0, 37 s | Compared; 48/3/41, 56 N/A, 14/14 spaces matched | **PASS** (workflow); numbers differ, first-level diagnosis only |

All three ran from the **real SAM Analytical UI** (Simulate > Run Simulation > Both), driven by UI Automation, from a
clean isolated build of Q4 (see Environment). No installed SAM release contains Run Simulation; the installed-UI path was
therefore not available (see Environment). `comparison.csv`, `comparison-summary.json` and `comparison.md` of every run
are byte-identical to an independent `benchmark-compare.exe` run on the same B1a documents.

## Environment

| Item | Value |
|---|---|
| SAM_UI / SAM_Validation / SAM / SAM_Tas / SAM_OpenStudio / SAM_Systems | `4ee45fef` / `68eded0` / `618db819` / `a34a298d` / `d4216f3` / `f0838ca` (all `sow/2026-Q4`, clean, equal to SAM-BIM remote heads) |
| Tas | EDSL Tas for Engineers 9.5.7 (TBD/TSD/TPD/TAS3D 9.5.7.0); Sentinel licence services running; licensed runs succeeded |
| OpenStudio / EnergyPlus | OpenStudio 3.10.0+86d7e215a1, EnergyPlus 25.1.0-1c11a3d85f |
| Producers | `SAM_Tas\build\benchmark-tas.exe` (sha256 `07ab3e45…`), `benchmark-openstudio.exe` (`64405919…`), `benchmark-compare.exe` (`5bf2854a…`); built 2026-10-17/18 from the Q4 heads above (no product change since) |
| UI under test | `SAM_UI-wt-e2e-validation` (detached `4ee45fef`), `SAM Analytical.csproj` Release built into `C:\TasOut\licensed-e2e-20261019\app` with `APPDATA`/`USERPROFILE` redirected so the post-build `xcopy` could not touch `%APPDATA%\SAM`; 0 errors |
| Environment variables (per process) | `SAM_COMMIT=618db819…`, `SAM_BENCHMARK_TAS`, `SAM_BENCHMARK_OPENSTUDIO`, OpenStudio 3.10 `bin` first on `PATH` |

**Found and avoided: the local `SAM_UI\build` (and its `%APPDATA%\SAM` mirror) cannot run Run Simulation > Both or
OpenStudio Simulation.** Its `SAM Analytical.deps.json` (2026-10-18 10:12, sha256 `f3780769…`) is byte-identical to the
G1 *baseline* build of pre-#237 `ebe5e09b`; the later incremental build replaced the DLLs but not the deps file, so the
self-contained host does not load `SAM.Analytical.Benchmark.Compare`/OpenStudio (`FileNotFoundException … Application
folder: …\SAM_UI\build\`). The clean build's deps file lists the 18 Benchmark/OpenStudio entries and works. This is a
machine-local build-state defect, not a product defect: anyone testing locally after switching between pre- and
post-#237 checkouts needs a **Rebuild** (not Build) of `SAM Analytical`. `SAM_UI\build` and `%APPDATA%\SAM` were left
unchanged (owner's environment; not overwritten).

## Phase B — small model, fresh licensed runs

Inputs: `box-aperture.json` sha256 `99ba2ab9…` (identical to the historical input; unchanged after every run);
`Budapest.epw` `472ffb4e…`. Run folders: `box\…_20261019-061322_7228de06`, `box-epw\…_20261019-063735_c6d5d9da`.

- Orchestration, read-only `snapshot.json`/`weather.epw`, SHA re-checks, separate `tas\`/`openstudio\` work folders,
  manifest `status: Compared`, both `succeeded: true`, comparison window opened automatically. Model and Spaces tabs,
  banner and grid values equal the CSV (screenshots).
- **Versus historical `box-both` (2026-10-17):** Tas values and hours are bit-identical. OpenStudio values differ by
  ≤ 5.6e-6 kWh / 1.6e-8 kW (relative ≤ 3.1e-9): the same non-bit-identical kind as the owner-accepted G1 residuals,
  larger in absolute terms than G1's 3.1e-7 kWh; the frozen snapshots also differ (F7). Counts, gates and bands are identical.
  The snapshot and canonical model hashes differ: the only snapshot difference is the UI-added `SAM.Analytical.UI`
  ParameterSet ("UI Geometry Settings") whose GUIDs are regenerated on every load (finding F7).
- Independent checks: Tas B1a model values equal the TSD building heating/cooling profile sums/maxima/0-based indices;
  E+ B1a values equal the ESO run-period Ideal Loads sums/maxima; every compared row's absolute/normalised difference
  recomputed from raw values (140 rows across four runs, 0 mismatches; `arithmetic-check.txt` — its single flagged row is
  a PowerShell `decimal` parse artefact, the JSON and CSV texts are identical).
- **Time conventions:** Tas TSD external temperature and global radiation equal the EPW columns at lag 0 (r = 1, rms 0).
  Hourly gains (lighting, equipment) are identical at lag 0 (rms 0); zone temperature, heating, cooling and solar all peak
  in correlation at lag 0 (`alignment-boxepw*.txt`). There is **no time-index offset between the engines**.

## Phase D — numerical discrepancy diagnosis (box-aperture)

| Metric | Tas | E+ | Diff | Cause | Class |
|---|---|---|---|---|---|
| Heating, embedded weather | 742.8 kWh | 2810.0 kWh | 73.6% | 99% of E+ heating (2769.7 kWh) is Ideal Loads **outdoor-air** load; Tas has no ventilation (F2); plus degraded weather (F1) | Inputs (1) |
| Cooling, embedded weather | 2156.9 kWh | 3832.5 kWh | 43.7% | Tas solar gain = 0 all year because exported GHI = 0 (F1); E+ value is total incl. 399.9 kWh latent (F3) | Weather (3) + extraction semantics (4) |
| Heating, real EPW | 220.6 kWh | 2173.9 kWh | 89.9% | Ventilation (F2): removing only E+ outdoor air gives **195.6 kWh** (-11% vs Tas) with the **same peak hour 775** | Inputs (1) |
| Cooling, real EPW (sensible) | 5540.3 kWh | 4140.8 kWh (total 4556.2) | 17.8% (as reported) | With E+ outdoor air removed E+ is 6629.3 kWh (+20% vs Tas); radiant split (+0.3%) and ground temperature (≈0%) excluded by experiment; remainder unexplained | Engine algorithms (2), suspected |
| Peak heating hour | 871 (embedded) / 775 (EPW) | 919 / 8455 | — | E+ peak is driven by unscheduled outdoor air; without it both engines peak at hour 775 | Inputs (1) |
| Hour 871 vs 872 | model 871 | — | 1 h | Tas space `peakHour` is the raw 1-based Tas index; model hour is 0-based (F4) | Extraction (4), **SAM defect** |
| Space peak loads | 1927.0 W heating / 3415.5 W cooling (EPW) | 3259.2 / 5332.6 W | — | Tas space `peakLoad`/`peakHour` are the *governing* (design-day if larger) peak, not the annual one (annual heating peak 1423.8 W @775); E+ is annual (F5) | Extraction (4), **SAM defect** |
| Gains / schedules | 1606.0 / 5475.0 / 2140.6 kWh (lights/equipment/people) | identical | 0 | — | Equal |
| Infiltration | −1132.9 kWh | −1030.7 kWh | 10% | 0.2 ACH both; temperature-difference dynamics | Engine (2) |
| Solar into zone | 4009.7 kWh | 3623.7 kWh transmitted | 11% | Different quantity definitions (zone solar gain vs transmitted) and window models | Engine (2), expected |

Single-variable E+ experiments (re-run of the producer's `in.idf` with `energyplus.exe`, `C:\TasOut\licensed-e2e-20261019\experiments`):
e0 control reproduces B1a 2810.0183 kWh; e1 no outdoor air (embedded weather) 341.0/5340.8 kWh; e2 control (EPW)
2173.9/4140.8; e3 no outdoor air (EPW) 195.6/6629.3 kWh, peak heating @775; e4 = e3 + Tas radiant fractions 196.1/6648.4;
e5 = e3 + constant 10.32 °C ground 177.9/6633.3 (heating/sensible cooling).

## Phase C — HungaryHouse

`HungaryHouse.json` sha256 `e16ccbd2…` (unchanged). C1 model: 14 spaces, 84 panels (3 `Air`, 7 slab-on-grade, 26
internal walls), 20 apertures, 1 AHU, embedded heating/cooling design days. C2/C3: `benchmark-tas` standalone run
(`hungary\c3-tas-asis`) exit 0, 38 s, `state = Success`, HDD/CDD sizing, simulation, unmet hours and design loads all
completed (`c3-tas-timing.csv`). C4/C5: UI Both run `…_20261019-064324_fe8d0daa`, both exit 0, Compared.

**Crash status: not reproduced.** The 2026-10-17 failure was a burst of `TBD.exe`/`TSD.exe` 0xc0000005 faults
(combase/OLEAUT32, RPC 0x800706BA) across 17–18 Oct that also occurred around successful runs; none occurred today. A
`x86\tas3d.exe` fault (0xc0000005 then 0xc000041d at the same offset `0x0158445b`) occurs on **every** Tas run including
the successful box runs (08:13, 08:37) and the 17 Oct successful `box-both`; it happens in the geometry stage and does not
affect results (`c3-application-events.txt`). Recommend reporting that signature to EDSL; no SAM change indicated.

First-level numbers: heating Tas 4507.4 vs E+ 1751.2 kWh (E+ identical to the 17 Oct OpenStudio-only run, 1751.176);
cooling 0 vs 5.2 kWh. The Tas excess is concentrated in the bathrooms (GF_08: 1425.6 vs 102.7 kWh; 01_16: 660.7 vs
200.8 kWh), whose Tas infiltration/ventilation loss (GF_08 −1583 kWh) is far beyond their 0.15 ACH infiltration, which
both engines received. Suspected cause: the model's `Air` panels are open holes with inter-zone air exchange in Tas, while
SAM_OpenStudio writes `Construction:AirBoundary` with Air Exchange Method `None`. **Not confirmed** (next task).

## Findings

- **F1 (SAM defect, weather, high impact).** `SAM.Weather.Query.DataString` (SAM `SAM/SAM.Weather/Query/DataString.cs`)
  writes EPW extraterrestrial horizontal, extraterrestrial direct-normal, horizontal IR and **global horizontal as 0**,
  writes `DirectSolarRadiation` (here global − diffuse, i.e. direct **horizontal**) into the **direct-normal** column, writes
  total sky cover as a 0–1 fraction (EPW expects tenths) and opaque sky cover 0. Non-solar fields round-trip exactly.
  Every "Embedded weather" run (Run Simulation > Both, OpenStudio Simulation, historical `box-both`) uses
  `EmbeddedAnnualWeatherPath` → `ToEPW`: Tas then simulates with **no solar gain**, E+ with under-stated beam and a
  0 W/m² sky. Not a minimal fix (direct-normal needs a solar-position conversion; changes established inputs): owner
  decision; until fixed, compare with an explicit EPW.
- **F2 (input mapping, route semantics).** SAM_OpenStudio turns `Supply Air Flow Per Person` into Ideal Loads outdoor air
  at design occupancy, 24 h, unscheduled, no heat recovery. SAM_Tas writes ticV only when a Ventilation profile exists
  (`UpdateInternalCondition`), and IZAMs only for AHU/legacy IZAM parameters; the rate otherwise lands in `freshAirRate`,
  which does not ventilate the zone. The box has no Ventilation profile and no AHU, so Tas has no ventilation. Needs an
  owner decision on the single intended semantics, then an aligned change in one route.
- **F3 (metric semantics).** OpenStudio `consumptionCooling`/`peakCoolingLoad` use Ideal Loads *Total* cooling (includes
  latent from the default minimum supply humidity ratio); Tas uses the sensible `coolingProfile` (its latent removal is
  separate, 6.4 kWh). Record as a definition difference; changing a metric needs comparator/producer agreement.
- **F4 (SAM defect, SAM_Tas benchmark mapping).** `ToBenchmark` emits `SpaceSimulationResultParameter.LoadIndex` (raw
  1-based Tas index, `Convert/ToSAM/Results.cs`) as `peakHour` "hour-of-year 0..8759"; model hours are 0-based `IndexOf`.
  Proposed patch: emit the annual peak's 0-based time from `AnnualPeak` (`SpaceLoadPeak`), never `LoadIndex`.
- **F5 (SAM defect, SAM_Tas benchmark mapping).** Space `peakLoad`/`peakHour` come from the governing peak (design day
  unless the annual peak is strictly larger); for a design-day governor the index is not an hour of the year. Proposed
  patch together with F4: map `peakLoad`/`peakHour` from `AnnualPeak`; keep design values in `designLoad`. Changes Tas
  golden values; needs the SAM_Tas benchmark tests updated. **SAM_Tas is read-only while the HVAC conversion is active**,
  so neither is implemented here.
- **F6 (reporting).** A measured-zero Tas peak (HungaryHouse cooling) is emitted with `peakCoolingHour = 0`, which
  the comparator then compares as an hour (786 h). An all-zero profile has no peak hour.
- **F7 (provenance stability).** The canonical model hash includes the UI-only `SAM.Analytical.UI` ParameterSet, whose
  GUIDs change on each load, so the same model gets a different canonical hash in each session. Same-run provenance is
  unaffected.
- **Withdrawn:** E+ peaks are *not* contaminated by design-day hours (the hourly query filters `EnvironmentType = 3`;
  independently, HungaryHouse run-period max 5720.6 W @775 = B1a, design-day max 5684.3 W).

## Unresolved

- Box cooling: ≈20% E+ > Tas after aligning ventilation (radiant split and ground excluded) — engine surface heat
  balance/sky/glazing algorithms suspected, not identified.
- HungaryHouse heating: Tas bathrooms; inter-zone `Air` panel treatment suspected, not confirmed.
- F1–F7 remain open; no product change made.

## Reproduce

1. Build the UI isolated: `MSBuild "Application\SAM Analytical\SAM Analytical.csproj" /t:Restore;Build /p:Configuration=Release /p:OutDir=<root>\app\`
   from a Q4 worktree with `APPDATA`/`USERPROFILE` pointed at a sandbox folder (protects `%APPDATA%\SAM`).
2. Set `SAM_COMMIT`, `SAM_BENCHMARK_TAS`, `SAM_BENCHMARK_OPENSTUDIO`, OpenStudio 3.10 `bin` on `PATH`; run
   `tools/drive-both.ps1 -ModelPath <model.json> -EvidenceDir <dir> [-EpwPath <epw>]` (UI Automation driver).
3. `benchmark-compare.exe --tas … --openstudio … --out x` and compare bytes with the run folder.
4. Native outputs: `tools/TsdDump.cs` / `TbdDump.cs` (compile in Windows PowerShell with `Interop.TSD.dll`/`Interop.TBD.dll`
   preloaded; open **copies** read-only, only while Tas is idle), `tools/EsoSummary.cs`, `tools/Align.cs`.
5. E+ experiments: copy the producer's `run\in.idf`, change one object (byte-preserving, CRLF), run
   `energyplus.exe -w <run>\weather.epw -d . in.idf`.

## Recommended next step

Owner review of F1 and F2, which explain most of the disagreement. Then two bounded tasks: (a) after the HVAC conversion
lands, a SAM_Tas PR for F4+F5(+F6) with updated benchmark goldens; (b) a SAM PR for the EPW writer (F1) with an
EPW round-trip regression test, followed by a re-run of this validation with embedded weather.

## Evidence and files

- Record: this file. Portable evidence (`documentation/evidence/licensed-e2e-20261019/`): per run `comparison.csv`,
  `comparison-summary.json`, `engine-comparison-run.json`, both B1a documents; Tas TSD/TBD and E+ ESO summaries,
  hourly alignment and arithmetic checks; `hashes.csv`; HungaryHouse standalone Tas timing and Windows fault events;
  UI screenshots; the analysis tools and UI driver (`tools/`).
- Machine-local only (`C:\TasOut\licensed-e2e-20261019`, copy to continue elsewhere): full run folders (snapshots, EPWs,
  TBD/TSD, OSM/IDF/SQL/ESO, `comparison.md`, producer logs — `*.log` is git-ignored), hourly CSVs, the E+ experiment
  folders e0–e5, the isolated app build and driver logs/screenshots.
