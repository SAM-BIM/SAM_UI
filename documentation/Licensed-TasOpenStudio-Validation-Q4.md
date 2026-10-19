# Licensed Tas / OpenStudio end-to-end validation (Q4, 2026-10-19)

Status: **validation complete; documentation-only PR, open for owner review. No product, test, physics, tolerance or
baseline change.** Reconciled to the simulation roadmap on 2026-10-19 (see "Status reconciliation" below).
Base: `sow/2026-Q4` (SAM_UI `4ee45fef`). Record branch: `validation/licensed-tas-openstudio-e2e-q4`.
Context: SAM_Validation#15, SAM_UI#237 and SAM_UI#238 are merged; the owner now has an active Tas licence. This record
is the licensed end-to-end acceptance and the first diagnosis of the Tas/OpenStudio numerical differences.
Machine-local run root: `C:\TasOut\licensed-e2e-20261019` (must be copied/provisioned elsewhere). Selected portable
evidence: `documentation/evidence/licensed-e2e-20261019/`.

## Status reconciliation (roadmap task S00, 2026-10-19)

The simulation work is now ordered by the canonical roadmap `SAM/documentation/SIMULATION_ROADMAP_Q4_2026.md`
(SAM-BIM/SAM#213; on `sow/2026-Q4` after it merges). Phase A is reliable Tas / OpenStudio / Both workflows and failure
handling, Phase B is independent verification of native-model conversion and imported results, and Phase C (engine
comparison science) is frozen. **Numerical Tas/EnergyPlus agreement is not an acceptance requirement.** Everything
below is kept unchanged as evidence; this section only states how it is now classified.

| Part of this record | Classification |
|---|---|
| Workflow results (Result table, Phase B, HungaryHouse C1-C5, crash not reproduced) | **Kept** as licensed-run evidence of the merged G1/G2 workflows on Q4 `4ee45fef`. Input to roadmap S05, which repeats the runs on S01-S04 content. |
| Embedded-weather runs and numbers (box embedded row, Phase D "embedded weather" rows, e0/e1) | **Superseded** by SAM-BIM/SAM#212 (F1 fixed, merged `5ac57033`). They were made with the pre-F1 EPW writer and are not comparable with later runs; kept only as the evidence that located F1. Explicit-EPW runs are unaffected. |
| F1 | **Resolved** by SAM-BIM/SAM#212. |
| F4, F5, F6 (Tas result extraction) | Roadmap S10 (Tas results round-trip). |
| F3 (total vs sensible cooling) | Roadmap S11 (OpenStudio results round-trip). |
| F7 (canonical hash not stable) | Roadmap S04. |
| F2, F8, F9 (ventilation, window opening, zone volume) | Owner decisions recorded in roadmap S07; no parity converter. |
| C6, C7, C8, the remaining box-cooling and HungaryHouse heating residuals, Phase D engine-physics attributions | **Frozen** (Phase C). Evidence kept; no further experiments, ASHRAE 140 or engine-physics work without a new owner decision. |
| "Recommended next step" (below) | Replaced by the roadmap; the original text is in commit `d342c7e2`/`1b7972dd`. |

Relevant Phase A acceptance (roadmap S05): every Tas only / OpenStudio only / Both case reports PASS, FAIL, NOT RUN or
BLOCKED, and each reported status, output file and failure message matches what the engines did. HungaryHouse is
machine-local; its provenance and redistribution permission are not established, so it is not committed as a fixture.

## Result

| Test | Tas | OpenStudio | Comparison | Result |
|---|---|---|---|---|
| Small reference model, embedded weather (box-aperture; same inputs as historical `box-both`) | Licensed, exit 0, 21 s, Success | E+ 25.1.0 / SDK 3.10.0, exit 0, 6 s | Compared; 6/0/10/2, 4 contributing + 6 informational; Coverage Warn; Provenance + Reconciliation Pass | **PASS** (workflow); numbers differ, explained below; numbers superseded by SAM#212 (pre-F1 EPW writer) |
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
both engines received. The earlier `Air`-panel suspicion is **refuted for the bathrooms** (C6 below): neither bathroom
touches an `Air` panel, and Tas has no IZAM and no inter-zone air movement. The dominant cause of GF_08 is Tas window
opening (C6, confirmed experimentally).

### C6 — Window-opening experiment (frozen, Phase C; Tas only, 19 Oct, `experiments\hungary-window`, evidence `window-opening/`)

Inputs: the run's `…\tas\000000_SAM_AnalyticalModel.tbd` (sha256 `EE2E4A0B…1545`, untouched; copies simulated with
`tbdDocument.simulate(1, 365, 0, 1, 0, 0, …)`, i.e. aperture output already on, 4–5 s each; no other licensed Tas activity).

- **Mechanism (confirmed).** Every window pane carries a Tas aperture type (35° opening, Cd 0.43–0.54) with the Part O
  function `zdwno,0,22.00,24.00,99.00` (EDSL TBD "Aperture Functions": zone control with an added overnight rule — if the
  controlling zone is above 23 °C at 11 pm the aperture stays fully open until 8 am). In the TSD: GF_08 opening ramps
  linearly with zone temperature (mean 0.05 @22 °C, 0.48 @23, 0.99 @24, 1.00 above); 49 of 49 nights with GF_08 > 23 °C at
  hour index 22 are fully open 00–07 (1 false positive among 315 other nights).
- **GF_08 airflow (baseline, measured).** Window open > 0 in 5988 h, fully open 1472 h; mean airflow 0.032 m³/s (11 ACH),
  max 0.249 m³/s (86 ACH). ρ·cp·Q·ΔT from the TSD aperture airflow = 1812 kWh vs the TSD infiltration/ventilation loss
  1583 kWh. Heating hits the 230 W capacity in 4915 h; zone dry-bulb is below 23.9 °C in 4836 h (mean 23.4 °C).
- **Baseline re-run reproduces the original TSD bit-for-bit** (max difference 0 over every zone summary column; building
  heating 4507.443 kWh).
- **Closed GF_08 window.** A new building element with the same construction replaced only GF_08's pane (shared aperture
  type, also used by LivingRoom, EntranceHall and Closet — corrected in C7 after a zone-index fix in the inventory tool —
  left untouched); no glazing, geometry or construction removed.

| Quantity | Baseline | GF_08 window closed | Change |
|---|---|---|---|
| GF_08 heating | 1425.6 kWh | 387.6 kWh | −1037.9 (−73%) |
| GF_08 infiltration/ventilation loss | 1583.4 kWh | 71.3 kWh | −1512.1 |
| GF_08 mean temperature | 23.38 °C | 25.66 °C | +2.28 |
| GF_08 hours < 23.9 °C | 4836 | 8 | |
| GF_08 hours at heating cap (230 W) | 4915 | 30 | |
| GF_08 heat to neighbours (bldg transfer) | −160 kWh | −587 kWh | |
| 01_16 bathroom heating | 660.7 kWh | 642.4 kWh | −18.3 |
| Whole-building heating | 4507.4 kWh | 3388.9 kWh | −1118.5 |

Contribution to the 2756.3 kWh model difference (Tas 4507.4 − E+ 1751.2): closing only GF_08's window removes **1118.5 kWh
(40.6%)**, of which GF_08 itself is 1037.9 kWh (37.7%) and 80.6 kWh is warmer-neighbour knock-on in other zones. GF_08's own
gap to E+ (1322.9 kWh) is 78% explained; the residual (387.6 vs 102.7 kWh) coincides with a −587 kWh heat flow to Tas
neighbours at 21–22 °C, whereas E+ neighbours float at 26–48 °C. 01_16 (no window, no Air panel) is unchanged (−18 kWh).
The 230 W cap was not the cause: it is a symptom (cap hours fall 4915 → 30) and E+'s autosized peak (142 W) is lower anyway.

Open (superseded by C7): other windowed zones were untested here; the 01_16 gap (460 kWh) and GF_08 residual are
consistent with the whole-house temperature/internal-gain difference (E+ unconditioned mean 26–32 °C, plantroom 2480 vs
1752 kWh) but not isolated by experiment.

### C7 — All-window sensitivity and Part O control audit (frozen, Phase C; Tas only, 19 Oct; evidence `window-opening/`)

**Inventory (confirmed from TBD, TSD and the SAM model).** 9 operable panes in 7 zones, all with the same Part O function
`zdwno,0,22.00,24.00,99.00`, 35° opening, no schedule, `sheltered = 0`; the SAM model carries
`PartOOpeningProperties` (Width × Height = the full pane area, OpeningAngle 35, Factor 1; the loaded snapshot also
shows `OpeningRestriction = Unrestricted`, night 08–23, which are **not** in the input `HungaryHouse.json`, i.e. defaults).
Doors, frames, the entrance door and all other zones have no aperture type. No cooling is modelled (upper limit 150 °C).
The three aperture types are attached to building elements shared across zones (so one element cannot be edited per room):

| Zone | Floor | Heating setpoint | Pane area (m²) | Cd | Shared building element used by |
|---|---|---|---|---|---|
| GF_07 LivingRoom | ground | 21 °C | 6.67 / 8.28 / 2.07 (3 panes) | 0.426 / 0.478 / 0.539 | all three elements |
| GF_12 EntranceHall | ground | 18 °C | 1.96 | 0.539 | LivingRoom, EntranceHall, GF_08, Closet |
| GF_08 Bathroom | ground | **24 °C** | 1.15 | 0.539 | same |
| GF_10 Closet | ground | 21 °C | 2.07 | 0.539 | same |
| GF_11 Bedroom | ground | 21 °C | 7.50 | 0.426 | LivingRoom, GF_11, 01_13, 01_14 |
| 01_13 Bedroom | upper | 21 °C | 2.94 | 0.426 | same |
| 01_14 Bedroom | upper | 21 °C | 2.94 | 0.426 | same |

(`aperture-inventory.csv`; WC, Stairs, Gallery, Voids, Plantroom and 01_16 Bathroom have no operable window.)

**Behaviour in the baseline TSD (measured).** Part O is implemented as documented: in every zone, all nights with the zone
above 23 °C at hour index 22 are fully open 00–07 (LivingRoom 13/13, EntranceHall 17/17, GF_08 49/49, Closet 15/15,
GF_11 17/17, 01_13 49/49, 01_14 52/52). Daytime opening ramps linearly with zone temperature from 22 °C (closed) to 24 °C
(fully open). Mean airflow / ACH by zone: LivingRoom 0.269 m³/s (8.0), EntranceHall 0.019 (3.4), **GF_08 0.032 (11.1)**,
Closet 0.016 (6.2), GF_11 0.067 (4.7), 01_13 0.035 (2.2), 01_14 0.039 (2.4) (`aperture-behaviour-baseline.csv`).

**Three-scenario heating (Tas, full year; `three-scenario-comparison.csv`).** A original; B only GF_08's window closed (C6);
C all aperture types detached from the three building elements (glazing, geometry, constructions, infiltration, internal
conditions, plant and weather unchanged; one run, 0.9 s). E+ = unchanged OpenStudio run, 1751.18 kWh.

| | A original | B GF_08 closed | C all closed | E+ |
|---|---|---|---|---|
| Whole-building heating | 4507.4 | 3388.9 | **3190.5** | 1751.2 |
| Change vs A | — | −1118.5 | **−1317.0** | |
| Change vs B | | — | −198.5 | |
| GF_08 Bathroom | 1425.6 | 387.6 | 348.5 | 100.9 |
| 01_16 Bathroom | 660.7 | 642.4 | 596.5 | 197.8 |
| LivingRoom | 799.5 | 776.3 | 719.5 | 444.3 |
| GF_11 / 01_13 / 01_14 Bedroom | 397.3 / 486.5 / 334.0 | 391.1 / 483.3 / 331.6 | 381.1 / 459.3 / 321.5 | 208.9 / 324.1 / 166.0 |
| Infiltration/ventilation loss, all zones | 9520 | — | 3275 | |

- A → C: −1317.0 kWh, **47.8% of the 2756.3 kWh gap**. B → C adds only −198.5 kWh (7.2% of the gap): GF_08 is the dominant
  single window; the other six windowed zones add about 18% of the B effect. GF_08 itself is 1037.9 of the 1317.0 kWh (79%)
  of what all windows do on heating. Effects are not additive: GF_08's own change is −1037.9 (B) vs −1077.0 (C) and the
  other zones' changes depend on the temperatures set by the open windows.
- Remaining Tas − E+ difference with all windows closed: **1439.3 kWh** (3190.5 vs 1751.2). Per zone (C − E+, run-period E+ values as corrected in C8): 01_16
  +398.7, LivingRoom +275.2, GF_08 +247.6, GF_11 +172.2, 01_14 +155.5, 01_13 +135.2, EntranceHall +81.3, Closet +20.0,
  Gallery −59.2. (The first C7 draft used E+ per-zone sums that included the 24-hour heating design day: 44.6 kWh, see C8.)
- Temperatures: closing windows lifts mean zone temperature from 21–22 °C to 25–27 °C in occupied rooms (GF_08 23.4 → 28.9,
  01_16 25.6 → 28.7, plantroom 31.5 → 35.1; E+ 26–32 °C, plantroom 48 °C). Tas has no cooling, so the closed-window case
  overheats, as E+ does. GF_08 hours at the 230 W cap 4915 → 28; hours below 23.9 °C 4836 → 6. Other zones' cap hours
  change little (LivingRoom 54 → 48). SAM's unmet-hours step (uncapped re-run) was not repeated; cap hours are the
  proxy used here.
- Infiltration/ventilation loss: GF_08 1583 → 87 kWh; LivingRoom 2562 → 854; GF_11 1333 → 378; 01_13 1264 → 410;
  01_14 1458 → 431; EntranceHall 387 → 135; Closet 210 → 62. Zones without windows gain a little (Stairs 142 → 191,
  Gallery 299 → 386, 01_16 184 → 222) because they are warmer.

**Part O control audit (GF_08).** Findings, classified:
1. *Confirmed:* GF_08 (setpoint 24 °C) has a ramp starting 2 K **below** its heating setpoint, so the window is partly open
   whenever the heater is trying to hold 24 °C (opening 0.05 @22, 0.48 @23, 0.99 @24 °C): heating and natural ventilation
   run against each other for 4915 h at the cap. Other zones (setpoints 18–21 °C) have the ramp above their setpoint, so
   it only relieves overheating. The same string was applied to every window regardless of setpoint.
2. *Confirmed:* the string has four numeric fields after `zdwno` (`0, 22, 24, 99`) while the EDSL page lists five inputs
   (zone, lower, upper, cut-off, upper wind speed) and does not document a string format or the meaning of zone index 0.
   Whether `99` is the cut-off or the wind-speed limit (the other defaulting) is **unresolved**; in either case one
   protection is effectively disabled. Needs an EDSL/Tas confirmation of the string format.
3. *Supported:* EDSL's Zone Control example is 22/24/25 °C with a 5 m/s wind limit; the task brief cites 22–26 °C daytime
   control and accessibility/security conditions for overnight opening (not found on the fetched EDSL page, unverified). The model's `Unrestricted` night
   setting is a loader default (absent from the input file), so overnight opening on every window, including ground
   floor and the bathroom, was probably not an authored decision. Not changed here; flagged as an assumption to confirm.
4. *Confirmed:* the controls are Part O overheating-assessment controls. Using them in a **heating** energy comparison
   makes Tas windows ventilate during heating hours; E+ has none, so this is a model-definition difference, not an engine
   defect.

**Recommendation for window-control implementation (design assessment only; nothing implemented).**
Separate two goals: *reproducing the Tas model* (carry `zdwno`, 22–24 °C ramp, overnight latch, Cd, 35°, as authored) and
*correcting control assumptions* (setpoint-aware ramp, security restrictions, wind/cut-off) — the latter is an owner/design
decision and must be an explicit option, never a silent default.
- **Simplified `ZoneVentilation:DesignFlowRate`:** cheap and robust, but invents a flow instead of using area, Cd, wind and
  stack, so it cannot honour opening angle/Cd, and a schedule cannot hold the overnight latch. Fit only for a rough parity check.
- **`ZoneVentilation:WindandStackOpenArea`:** closest physically to Tas apertures (opening area × Cd, wind + stack, indoor/outdoor
  temperature limits), no AirflowNetwork needed, compatible with Ideal Loads and with `Construction:AirBoundary` as written
  today. Its native temperature limits are step-like, so the 22–24 °C ramp and the 23 °C/23:00 latch need a controlled opening
  fraction (EMS or schedule actuator) per zone. **Recommended next milestone:** this object plus a small EMS control that
  reproduces `zdwno` exactly, off by default, with a Tas-parity test on GF_08 and the all-closed baseline (3190.5 kWh) as
  the control. Verify in E+ that it sees the 35° opening and Cd the same way Tas does before trusting parity.
- **`AirflowNetwork` (multizone):** the right tool for buoyancy-driven inter-zone flow through stairs, voids and open
  doors and for real pressure effects, but it replaces infiltration with crack/opening components, adds a pressure model and
  run time, and its interaction with Ideal Loads and AirBoundary mixing must be verified. Defer; consider only after the
  open-door and Air-panel representations are reviewed.
- **Custom logic:** limited to the overnight latch and ramp (EMS); anything else (security restrictions, wind cut-off)
  belongs in the data model first.
- Also decide before implementing: heating comparisons should use equal window assumptions in both engines. Until then
  the fair like-for-like Tas run is scenario C (heating 3190.5 kWh vs 1751.2 kWh).

Unresolved after C7: 1439.3 kWh (52.2% of the original gap). Attributed by data, not isolated: 01_16 (399), LivingRoom
(275), GF_08 residual (248) and bedrooms (463) — E+ zones float at 26–32 °C with plantroom 48 °C and 42% more plantroom
gain, Tas neighbours are cooler, and E+ internal gains differ (equipment, people latent). Next experiment: align internal
gains/plantroom in a Tas-only copy (or E+ copy) and rerun.

### C8 — EnergyPlus window-ventilation proof of concept (frozen, Phase C; GF_08 only; E+ 25.2.0; evidence `window-opening/c8-eplus/`)

Isolated copies of the generated `in.idf` and the explicit `Budapest.epw` (runs in `experiments\hungary-eplus-vent`). Added
for GF_08 only: `ZoneVentilation:WindandStackOpenArea` + a `Schedule:Constant` opening-area fraction driven by EMS
(`BeginTimestepBeforePredictor`; ramp 22→24 °C; optional Part O latch: Tz > 23 °C at 23:00 → fully open until 08:00).
Infiltration (0.15 ACH), setpoints, weather and internal gains unchanged. Not a product implementation.

**Reconciliation of the 44.6 kWh (resolved, no defect).** The unchanged baseline reproduces **1751.176 kWh** (`Zone Ideal Loads
Supply Air Total Heating Energy`, run period only = the producer metric; sensible = total; outdoor-air heating 0). The
24-hour *heating design day* contributes 44.599 kWh: my per-zone figures came from an E+ ESO summary that included both
sizing-day environments (n = 8808 = 8760 + 48). Per-zone E+ values in C7 are corrected above (e.g. GF_08 100.9, not 102.8).
The E+ used here is 25.2.0 (IDF version 25.1 → warning); its baseline equals the 25.1.0 OpenStudio run to 12 digits.

**Native-output verification** (details in `ems-and-reconciliation-checks.txt`). The actuator and the ventilation object
exist in `.edd`/`.eio`. The EMS sensor returns the *previous* timestep's zone temperature exactly (one 10-minute lag;
`Hour` 0–23, `Minute` 10–60). Fraction = ramp(sensed Tz) to 2e-15; the latch is evaluated on the first step of hour 23
(116 of 365 nights triggered), holds fraction 1.0 from 23:00 to 08:00 and never leaks into hours 8–22 (0 violations);
flow is zero whenever the fraction is zero. Caveats: 6 warmup *loads*-convergence Severe messages (other zones) in the
`*_cal` capped runs; Tas evaluates hourly values while E+ uses instants (Tas opening vs ramp(hourly Tz): RMS 0.17).

**Two findings from running it, before any comparison.**
1. *Unlimited Ideal Loads break the control.* The ramp reaches full opening at 24 °C = GF_08's setpoint; an unlimited heater
   holds exactly 24.0 °C, so the window is fully open for 8760 h and heating explodes (**124,044 kWh** with default
   wind/stack inputs, 46,969 kWh calibrated). Tas avoided this because its heater is capped at the 230.3 W design load. A
   like-for-like E+ run therefore needs `Heating Limit = LimitCapacity, 230.301 W` on GF_08 (the `cap` variants below). The
   capacity assumption is part of the comparison, not a detail.
2. *Zone volumes differ between the engines.* E+ computes geometric volume (GF_08 13.88 m³, floor 4.63 m²); Tas uses the SAM
   space volume (10.40 m³). Across zones E+/Tas = 0.49 (Stairs) … 3.00 (01_18 Void), 1.34 for GF_08, so E+ infiltration
   (0.15 ACH × volume) is larger there. The benchmark reports identical volumes (the SAM value), so this is invisible in the
   comparison. Not investigated further; flagged.

**Variants** (capacity-limited GF_08; opening area 1.15 m² = Tas pane W×H, Cd 0.539, effective angle 0). "default" =
autocalculated wind effectiveness, height difference 1.1 m (half window height). "cal" = wind effectiveness 0.0096
(Cw·A = 0.011 m², fitted on Tas hourly wind/ΔT/flow, R² 0.43) and height difference 0.19 m (fitted); both are calibrations
against Tas flows, not derived from SAM data. B = ramp only, C = full Part O.

| GF_08 / whole building | Tas A (C6) | E+ B default | E+ C default | E+ B cal | E+ C cal |
|---|---|---|---|---|---|
| Whole-building heating (kWh) | 4507.4 | 3477.0 | 3520.2 | 3355.9 | 3422.6 |
| Change vs E+ baseline 1751.2 | (Tas: +1118.5 vs closed) | +1725.8 | +1769.0 | +1604.7 | +1671.4 |
| GF_08 heating (kWh) | 1425.6 | 1617.6 | 1655.3 | 1530.9 | 1583.1 |
| GF_08 mean zone temperature (°C) | 23.38 | 20.29 | 19.89 | 21.19 | 20.69 |
| Ventilation + infiltration loss (kWh) | 1583.4 | 3395.6 | 3512.7 | 2942.4 | 3116.1 |
| Mean window airflow (m³/s) | 0.0322 | 0.1121 | 0.1302 | 0.0513 | 0.0588 |
| Peak window airflow (m³/s) | 0.249 | 1.847 | 1.847 | 0.142 | 0.191 |
| Mean opening fraction / hours fully open | 0.262 / 1472 | 0.292 / 796 | 0.344 / 1506 | 0.357 / 1140 | 0.420 / 2072 |
| Hours at the 230 W cap / hours < 23.9 °C | 4915 / 4836 | 3395 / 7864 | 4049 / 7871 | 2611 / 7509 | 3467 / 7514 |

(Tas "loss" is the TSD infiltration/ventilation channel; E+ is ventilation + infiltration sensible heat loss.)

**Airflow character** (hours with opening > 0.5, flow per unit opening, hourly). Tas: mean 0.102 m³/s, max 0.249, correlation
with |Tz − Tout| **0.81**, with wind **0.09** — stack-dominated single-sided flow. E+ default: mean 0.314 (3.1×), max 1.847
(7.4×), correlation with wind 0.52, with ΔT −0.21 — wind-dominated: the standard cross-ventilation wind term is far too strong
for one window. E+ cal: mean 0.1185 (1.16×), max 0.191, correlation with ΔT **0.95**, wind 0.04. Mean flow in latched (fully
open) night hours: Tas 0.116 m³/s, E+ cal 0.131 (1.12×), E+ default 0.357 (3.1×).

**Overnight behaviour.** Tas latched 49 nights (441 h); E+ cal latched 116 nights (1044 h) because E+ GF_08 is warmer at 23:00
more often (no cooling, hotter neighbours); the rule itself is reproduced exactly. Hourly opening-fraction correlation Tas vs E+
cal = 0.77. The latch adds +52 kWh (GF_08) and +67 kWh (building) in E+ cal, +38/+43 kWh with default parameters; the matching
Tas effect was not isolated (no Tas run without the latch, none needed).

**Double counting.** None found: the 0.15 ACH `ZoneInfiltration:DesignFlowRate` is untouched (5.8–6.5e-4 m³/s) and window flow
is a separate `ZoneVentilation` object, as in Tas where infiltration and aperture flow are separate channels.

**What the heating numbers do and do not show.** Annual GF_08 heating lands near Tas (1531–1655 vs 1425.6 kWh), but the
mechanisms differ: E+ loses about twice the air heat (2942–3513 vs 1583 kWh) while sitting 2–3.5 K colder, with far more hours
below 23.9 °C. Annual heating agreement is **not** evidence of equivalence. Whole-building heating rises by 1605–1769 kWh in
E+ against 1118.5 kWh for the same window in Tas.

**Confirmed limitations.**
- The default `WindandStackOpenArea` wind term (Cw autocalculated) overstates single-window flow ≈3× mean, 7× peak.
- Reproducing Tas flow-per-opening needs a wind-suppressed, stack-calibrated set (Cw≈0.01, DH≈0.19 m) with no basis in SAM
  data; the area has no definition for "35° opening" either.
- Ideal Loads need a heating capacity limit on GF_08 or the Part O ramp runs away; 230.3 W is a Tas sizing result.
- Different zone volumes (up to 3×), 10-minute vs hourly control timing and E+ zones floating hotter all change the
  temperatures that drive the ramp, so even a good flow model would not give the Tas heating answer.

**Authored versus default (SAM code and input file).** The `zdwno,0,22.00,24.00,99.00` string is **not** a SAM default: the
Grasshopper components, `Create.AnalyticalModel` and the tests default to `zdwno,0,19.00,21.00,99.00`, and `22.00,24.00`
appears nowhere in SAM source, so it was authored (or imported) in the model. Width/Height, OpeningAngle 35° and Factor 1 are
in the input file. `OpeningRestriction = Unrestricted` and night hours 08–23 are **not** in `HungaryHouse.json`; they come from
the documented legacy deserialisation default (missing key → `Unrestricted`). Overnight and heating-hour opening is therefore
one authored control string plus one legacy default; neither was chosen for heating energy, and Part O is an overheating test.

**Recommendation.**
- A SAM_OpenStudio *converter* PR that reproduces Tas is **not yet justified**: parity needs fitted parameters, a
  capacity-limit decision and volume alignment. The EMS and ventilation mechanics are proven sound (verified above).
- Justified now, as a small opt-in design PR (off by default): an explicit per-model/per-aperture natural-ventilation switch,
  authored opening area/Cd/ramp/latch (not inferred from Tas defaults), `WindandStackOpenArea` with a *stated* wind
  effectiveness, the EMS ramp and latch proven here, and a diagnostic that refuses Ideal Loads without a capacity limit when
  the control reaches the heating setpoint. Never propagate `zdwno` or `Unrestricted` automatically.
- Before any parity claim: an `AirflowNetwork` single-zone PoC (`SimpleOpening`/`DetailedOpening`, same area, Cd and ramp) to
  see whether pressure-driven bidirectional flow reproduces Tas's stack-dominated flow without fitted wind terms; also align
  zone volumes and decide the capacity assumption. Until then compare heating with equal window assumptions (scenario C).

## Findings

- **F1 (SAM defect, weather, high impact; resolved by SAM-BIM/SAM#212, merged `5ac57033`).** `SAM.Weather.Query.DataString` (SAM `SAM/SAM.Weather/Query/DataString.cs`)
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
- HungaryHouse heating: Tas window opening (F8, C6/C7) explains 1317.0 kWh (47.8%) of the 2756.3 kWh gap; the remaining
  1439.3 kWh (01_16, GF_08 residual, living, bedrooms) is attributed by data to whole-house temperature and internal-gain
  differences but not isolated by experiment. The `Air`-panel explanation is refuted for the bathrooms.
- **F8 (input mapping, model fidelity).** SAM_Tas writes `zdwno` aperture functions that Tas executes (35° opening,
  22–24 °C ramp, overnight latch); SAM_OpenStudio converts no opening properties (diagnostic only). The engines therefore
  simulate different buildings. Not a software defect in either engine; the mapping needs an owner decision. C7: the same
  `zdwno,0,22,24,99` string is on all 9 windows, its ramp starts below GF_08's 24 °C setpoint, the string has one field
  fewer than EDSL documents, and `Unrestricted` night opening is a loader default; see C7 for the audit.
- **F9 (model fidelity, found in C8).** E+ computes zone volume from geometry while Tas uses the SAM space volume; ratios E+/Tas range 0.49–3.00 (GF_08 1.34), the benchmark shows the SAM value for both. Infiltration (ACH × volume) and air thermal mass therefore differ; cause not investigated.
- F1 is resolved by SAM-BIM/SAM#212; F2–F9 remain open and are mapped to roadmap tasks in "Status reconciliation". No
  product change was made in this PR.

## Reproduce

1. Build the UI isolated: `MSBuild "Application\SAM Analytical\SAM Analytical.csproj" /t:Restore;Build /p:Configuration=Release /p:OutDir=<root>\app\`
   from a Q4 worktree with `APPDATA`/`USERPROFILE` pointed at a sandbox folder (protects `%APPDATA%\SAM`).
2. Set `SAM_COMMIT`, `SAM_BENCHMARK_TAS`, `SAM_BENCHMARK_OPENSTUDIO`, OpenStudio 3.10 `bin` on `PATH`; run
   `tools/drive-both.ps1 -ModelPath <model.json> -EvidenceDir <dir> [-EpwPath <epw>]` (UI Automation driver).
3. `benchmark-compare.exe --tas … --openstudio … --out x` and compare bytes with the run folder.
4. Native outputs: `tools/TsdDump.cs` / `TbdDump.cs` (compile in Windows PowerShell with `Interop.TSD.dll`/`Interop.TBD.dll`
   preloaded; open **copies** read-only, only while Tas is idle), `tools/EsoSummary.cs`, `tools/Align.cs`.
5a. Window experiment (C6): copy the run's `.tbd` twice; compile `window-opening/tools/*.cs` with `csc /platform:x64` against
   the matching `Interop.TBD.dll`/`Interop.TSD.dll`; `RunClosedWindow <copy>.tbd <copy>.tsd base|close`, then
   `ZoneSummary <tsd> <prefix>`; only while no other Tas process runs.
5. E+ experiments: copy the producer's `run\in.idf`, change one object (byte-preserving, CRLF), run
   `energyplus.exe -w <run>\weather.epw -d . in.idf`.

## Recommended next step

**Superseded (S00, 2026-10-19).** The next step is owner review of this PR and of the roadmap (SAM-BIM/SAM#213); further
work follows the roadmap's task register, one task at a time. The original recommendation is kept below for reference:
(a) maps to S10; in (b) the EPW fix is done (SAM#212) and the embedded-weather re-run moves to S05; (c) and the natural-ventilation milestone are frozen (Phase C).

*Original (pre-S00):* Owner review of F1 and F2, which explain most of the disagreement. Then two bounded tasks: (a) after the HVAC conversion
lands, a SAM_Tas PR for F4+F5(+F6) with updated benchmark goldens; (b) a SAM PR for the EPW writer (F1) with an
EPW round-trip regression test, followed by a re-run of this validation with embedded weather. (c) For HungaryHouse,
window-control mapping (F8) first (all windows 47.8% of the heating gap, GF_08 alone 40.6%). C8 result: the E+ EMS ramp/latch mechanics are verified, but `WindandStackOpenArea` only approximates Tas flow with fitted parameters and an Ideal Loads capacity limit, so the next milestone is an opt-in, off-by-default natural-ventilation design PR (authored area/Cd/ramp/latch, no Tas-default propagation) plus an AirflowNetwork single-zone PoC before any parity claim, with an explicit decision on reproducing vs correcting the Part O settings (C7/C8); then internal-gain alignment (plantroom, bathroom equipment,
people); plant-capacity comparison last (not causal, see C6/C7).
Machine-local C6/C7 runs: `C:\TasOut\licensed-e2e-20261019\experiments\hungary-window\{base,close,closeall}`; C8: `experiments\hungary-eplus-vent{A,B*,C*,Bcap*,Ccap*}`.

## Evidence and files

- Record: this file. Portable evidence (`documentation/evidence/licensed-e2e-20261019/`): per run `comparison.csv`,
  `comparison-summary.json`, `engine-comparison-run.json`, both B1a documents; Tas TSD/TBD and E+ ESO summaries,
  hourly alignment and arithmetic checks; `hashes.csv`; HungaryHouse standalone Tas timing and Windows fault events;
  UI screenshots; the analysis tools and UI driver (`tools/`).
- Machine-local only (`C:\TasOut\licensed-e2e-20261019`, copy to continue elsewhere): full run folders (snapshots, EPWs,
  TBD/TSD, OSM/IDF/SQL/ESO, `comparison.md`, producer logs — `*.log` is git-ignored), hourly CSVs, the E+ experiment
  folders e0–e5, the isolated app build and driver logs/screenshots.
