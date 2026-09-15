# SAM #111 - Part O Iteration 3 PR5B: production acceptance (B4 = B0 + selected-product cooling module)

Date: 2026-09-15 · Tracker: https://github.com/SAM-BIM/SAM/issues/111 · Integration branch: `sow/2026-Q3`

Text evidence only. No binaries, TAS documents or manufacturer tables are checked in. The licensed outputs
live outside every repository, under `C:\TasOut\pr5b-production\` (`gen\`, `final\`, `ord\`, `compare.py`).

## 1. What was accepted

| repo | branch | commit | base (`sow/2026-Q3`) | tests |
| --- | --- | --- | --- | --- |
| SAM_Systems | `feature/parto-pr5b-recirculation-cooling` | `c36ba11` | `5213ba9` | `SAM.Analytical.Systems.Tests` **203/203** (+27) |
| SAM_Tas | `feature/parto-pr5b-recirculation-cooling` | `220ea11f` | `00f51520` | `SAM.Analytical.Tas.TM59.Tests` **922/922** (+16) |
| SAM_UI | `feature/parto-pr5b-recirculation-cooling` | `0d1833ed` | `0daa072d` | `SAM.Analytical.UI.WPF.Tests` **1027/1027** (+15) |
| SAM | - | no change | `b4a1283f` | - |

Test counts were re-run on 2026-09-15 against DLLs no older than every tracked source file. SAM needs no
change: the shipped SAM_Systems catalogue already carries the canonical product's 96-cell
`SupplyAirTemperature` table and its 22 -> 26 C / 0.30 -> 1.00 flow-fraction law.

Merge order: **SAM_Systems -> SAM_Tas -> SAM_UI**. SAM_Tas consumes SAM_Systems' new
`MechanicalVentilationCoolingSettings` / `MechanicalVentilationRecirculationCooling` /
`MechanicalVentilationMaterialisation.RecirculationCoolings`. SAM_UI consumes those types and SAM_Tas'
`RecirculationCoolingResult(s)`.

The frozen PR5B architecture is unchanged:
- same-AirSystem recirculation branch;
- damper-controlled linear law 22 C -> 0.30, 26 C -> 1.0;
- DX `HeatingSetpoint` 22 C with `HeatingDuty` 0 as the cooling gate;
- surrogate recirculation fan, HGF 0;
- 3 x 4 x 8 equality table, `Extrapolate = false`;
- 120 l/s validated ceiling (the table's airflow-axis maximum);
- canonical creation order (SAM_Tas #57) and table round trip (SAM_Tas #58).

## 2. MV vs MVRE: why production B4 is Parity MV plus cooling

**Decision: production B4 = the B0 Parity ventilation (`MV.json`, no unit settings, `ClearToZero`) plus the
cooling module, so B4 - B0 isolates the cooling layer.** This is option A, and it is deliberate. Evidence:

1. **The ventilation layers are evidence-blocked.**
   - #111 (2026-09-14): B1 is BLOCKED and B2 is EVIDENCE BLOCKED. E1/E2 are unsourced, and the PCDB figure is a
     package supply-temperature ratio, not an exchanger-only ε.
   - EDSL is still to clarify displacement plus active-HR behaviour, and quantitative B2 deltas may not be
     accepted as final.
   - The PR5A `SelectedProduct` mode therefore fails closed for the canonical product by design.
   - A cumulative MVRE B4 would either refuse (no certified ε/SFP) or run on MVRE.json's uncertified template ε
     0.7. The PR5A plan forbids the latter ("never MVRE's 0.7").
2. **The cooling module no longer depends on the MVRE path.**
   - The original plan (#111, 2026-09-11 §E) put a DX coil after the exchanger on the supply path, hence
     "PR5B depends on ... the MVRE route".
   - The licensed PR5B work found that path's entering-temperature axis NOT representable.
   - It replaced it with the frozen same-AirSystem recirculation branch, whose coil draws room / mixed-return
     air and is independent of any exchanger.
3. **The contract is layered.**
   - Plan §I names the variant by the layers that are on: `B0 | SelectedProduct{FanHeat, HeatRecovery, Bypass[, Cooling]}`.
   - Plan §I pairs every B-variant with B0.
   - The attribution ladder (§H) is one layer per delta. So `SelectedProduct{Cooling}` against B0 is the
     cooling layer's own attribution.
   - SAM_UI states this in `PartOIteration3BehaviourMode.SelectedProductCooling`. Review refuses a cooling
     record that also carries selected-product fan or HR rows.
4. **The Parity B0 reproduces the frozen foundation; the archived MVRE pair did not.**
   - The production B0 here gives per-room TM59 > 26 C hours of 16 / 12 / 3 / 0 / 4 / 3 / 0 / 4, identical to
     PR4's frozen Candidate B (`PR4-comparison.tsv`).

**Correction to earlier tracker numbers.** The archived harness (`C:\TasOut\pr5b-continuation`, runs
`a-B0c` / `a-B4c-s1`, reported on #111 on 2026-09-15 13:24) generated **both** its "B0" and "B4" on `MVRE.json` with no unit
settings. The harness source passes `Settings(null)`. SAM_Systems applies ε only from unit settings, and
SAM_Tas writes the template's own value. So both sides carried the template's uncertified ε 0.7. Their
TM59 hours (Studio 18 -> 8, Bathroom 2 -> 0) are therefore historical engineering evidence for the
architecture, not a B0 baseline, and they are superseded by §4 below.

A cumulative MVRE-based B4 remains a later variant (B2 + cooling). It becomes possible only once E1/E2 are
sourced and the EDSL question is answered. No production code is changed for it.

## 3. Gate 1 - generation, reconciliation, native order (`gen\`, `ord\`)

- **B4 converts and reconciles.**
  - 3 air systems, 8 rooms, 25 lineage bindings.
  - 14 legs: 3 supply, 6 extract, 5 transfer, "every design flow matched on its native carrier".
  - Room/SystemZone count equals B0: the branch adds no room, no air system and no outdoor air.
- **The branch is grounded in all three systems, with native read-back:**
  - 96-cell cooling setpoint table, extrapolation off;
  - gate 22 C with no modifier; `HeatingDuty` an absolute 0;
  - recirculation fan HGF 0, variable speed, 120 l/s absolute;
  - a normal controller on the coil's single mixed-return inlet duct acting on 6 / 6 / 4 recirculation
    dampers, on every day type.
- **Floor-area shares of the 120 l/s ceiling:**
  - MVHR-03: Kitchen_7 42.857, Bedroom 2_6 60, Ensuite_8 17.143;
  - MVHR-02: Kitchen_4 42.857, Bedroom 2_3 60, Ensuite_5 17.143;
  - MVHR-01: Studio 1_0 90, Bathroom_2 30.
- **Native order.**
  - B4 and B4-id2 (every derived identity re-keyed) have identical logical graph, component order and duct
    order hashes in all 3 systems. That is 24 / 30 / 30 components and 27 / 35 / 35 ducts, against B0's
    13 / 15 / 15 and 13 / 15 / 15.
  - Against the archived proven MVRE dump, the only difference in each system is `Exchanger 1`.

## 4. Paired annual acceptance (`final\`, `final\compare.md`)

Route, for B0, B4 and B4-id2 (all full year, all "PROD COMPLETE: Pass"):
- production `Create.MechanicalVentilation`;
- `Create.SystemVentilationRoute`, production `SimulateSystems`: every air system "Done";
- ZoneTemperature;
- ResultantTemperature thermostat bridge on a copy of the no-IZAM TBD;
- the unchanged SAM_UI TM59 authority.

B4 resolution goes through SAM_UI's production `Query.PartOIteration3CoolingResolution` (`resolve=ui`). B0
and B4 were regenerated together on the same binaries and the same canonical creation order.

Timing:
- B4 generation + simulation + evidence: 189.9 s (B0: 40.0 s);
- bridge: 80.6 s (B4) / 87.2 s (B4-id2).

### TM59 and room temperatures, B0 -> B4

| room | B0 h > 26 C | B4 h > 26 C | TM59 B0 / B4 | mean dRT (K) | RMSE dRT (K) | max abs dRT (K) | mean dZT (K) | max abs dZT (K) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Bathroom_2 | 16 | 0 | Pass / Pass | -0.261 | 1.006 | 3.85 | -0.358 | 5.34 |
| Studio 1_0 | 12 | 7 | Pass / Pass | +0.172 | 0.263 | 1.65 | +0.218 | 2.39 |
| Bedroom 2_3 | 3 | 2 | Pass / Pass | +0.132 | 0.179 | 0.60 | +0.157 | 0.87 |
| Bedroom 2_6 | 3 | 2 | Pass / Pass | +0.148 | 0.182 | 0.55 | +0.171 | 0.77 |
| Kitchen_4 | 4 | 3 | Pass / Pass | +0.062 | 0.152 | 0.93 | +0.061 | 1.37 |
| Kitchen_7 | 4 | 3 | Pass / Pass | +0.072 | 0.150 | 0.76 | +0.073 | 1.02 |
| Ensuite_5 | 0 | 0 | Pass / Pass | +0.015 | 0.367 | 2.72 | +0.003 | 3.92 |
| Ensuite_8 | 0 | 0 | Pass / Pass | +0.039 | 0.318 | 2.85 | +0.041 | 4.04 |

- TM59 outcome changes B0 -> B4: **0 of 8**. Exceedance hours fall in 6 of 8 rooms and are unchanged in the
  two ensuites.
- **Room coupling, reported rather than hidden.**
  - The declared law's floor (0.30 x 120 = 36 l/s) recirculates year-round: 7288-8029 h per unit sit on the
    floor.
  - That mixes each dwelling's rooms. It moves heat between them and adds none.
  - The warmest room (Bathroom_2) cools on the annual mean. The occupied dry rooms warm by +0.06..+0.17 K on
    the annual mean, while their hot-hour exceedance falls.
  - This is the consequence of the published flow law, not a defect.

### Cooling-branch evidence (route's own plant-room pass on a disposable copy)

| check | MVHR-03 | MVHR-02 | MVHR-01 |
| --- | --- | --- | --- |
| mixed return below the 22 C gate | 8105 h | 7927 h | 7096 h |
| DX cooling below the gate | **0 h** | **0 h** | **0 h** |
| heating | 0 h | 0 h | 0 h |
| cooling hours / air-side sensible | 655 h / 397.9 kWh | 833 h / 485.2 kWh | 1664 h / 929.8 kWh |
| OperatingAirFlow min / mean / max (l/s) | 36 / 38.172 / 120.013 | 36 / 38.414 / 120 | 36 / 40.489 / 120 |
| out of law range (tolerance 0.05 l/s) | 0 h | 0 h | 0 h |
| DX outlet vs table held at its edges | 2e-6 K | 2e-6 K | 2e-6 K |
| canonical ventilation max deviation from design | 0.0263 l/s | 0.0258 l/s | 0.0025 l/s |
| off the ideal law by > 0.5 l/s (reported only) | 235 h | 137 h | 743 h |
| hours inside the published table domain | 0 | 0 | 0 |

- The evidence pass reproduces the route's room temperatures to **0 K**.
- OperatingAirFlow stays at or below the 120 l/s validated ceiling (<= 150 l/s capacity) every hour. It is
  persisted separately from every DesignAirFlow.
- Cooling runs March-December in MVHR-01 and April/May-November in the others, peaking in July.
- **Off-law hours** are the native within-hour convergence artefact already measured in the gate weeks: the
  mixed-return sensor reads slightly off the flow-weighted return. They are counted, never refused.
- **Published domain.** The outdoor dry bulb is at or above 29 C in only 3 h a year here. Every other hour the
  coil sits at the table's published edges (`Extrapolate = false`), as frozen.

### Reproducibility (B4 vs B4-id2, every derived identity re-keyed)

- ZoneTemperature bit-identical, 8 x 8760.
- ResultantTemperature bit-identical.
- TM59 identical.
- Cooling series bit-identical (3 x 8760 rows under the 1:1 re-keyed air-system mapping). `compare.py`'s
  guid-ordered check prints False only because re-keyed guids sort differently.

## 5. Limitations (stated, not blockers)

- The cooling module is an **aggregate thermal surrogate**: published supply-air temperature only. There is
  no compressor, refrigerant, COP/EER, electrical, latent or humidity behaviour. kWh are air-side sensible
  only.
- The recirculation fan is a heat-gain-free pressure-flow carrier copied from the unit's supply fan. **It is
  not a manufacturer fan.**
- **Product assignment.** The canonical fixture carries no Iteration 2 selection. The harness stamped the
  smallest capable catalogue unit through `AirHandlingUnitParameter.VentilationUnitReference`; SAM_UI's
  production resolution then read it.
- **Cannot show fail -> pass.** The canonical fixture's B0 already passes TM59. Fail -> pass evidence
  needs the known failing real project, which was not run in this session.
- **Test coverage of the full mode.** The full `Modify.RunPartOIteration3` cooling mode is covered by
  fake-pipeline tests, and licensed through the harness via SAM_UI's production resolution. There was no
  UI-driven licensed run.
- There is one pairing record per Reference A. B4 documents use the `-It3B4` suffix, so B0's documents are
  preserved beside them, but the record is the latest attempt.
- Archived MVRE-based harness numbers are historical architecture evidence only (see §2).
- A cumulative B2 + cooling variant waits on E1/E2 and EDSL. PR5A's exit gates stay open, so #111 stays OPEN.
