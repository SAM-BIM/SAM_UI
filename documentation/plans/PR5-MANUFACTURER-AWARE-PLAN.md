# Part O Iteration 3 — PR5 manufacturer-aware plan

Status: frozen implementation companion for [SAM #111](https://github.com/SAM-BIM/SAM/issues/111).
The complete planning record and its 16 resolved questions are the tracker comment headed
`PR5 PLANNING — investigation complete (no code)`. This checked-in companion records the decisions
that govern the code in this repository so another machine or agent does not need conversation history.

## Scope and repository order

PR5A adds generic manufacturer-aware MVHR behaviour. PR5B is separate and must not begin until PR5A's
technical and human evidence gates pass.

The frozen merge order is:

1. `SAM`: optional catalogue performance vocabulary and generic operating-point resolution.
2. `SAM_Systems`: generic per-AHU settings, MVRE materialisation and Phase-0 zone normalisation.
3. `SAM_Tas`: exchanger conversion plus selectable fan-heat grounding policy.
4. `SAM_UI`: mode selection, orchestration, persistence, review and presentation.

## Absolute airflow invariant

```text
PartFRequiredAirFlow
!= DesignAirFlow
!= SelectedEquipmentCapacity
!= OperatingAirFlow
```

Capacity is a feasibility gate and may become an explicitly authorised later ceiling. It never replaces
design airflow. PR5A keeps operating airflow equal to design airflow multiplied by the unchanged yearly
schedule of 1.0. No code silently reselects a product.

## Behaviour modes

The UI wording is:

```text
Ventilation equipment behaviour: Parity (foundation control) | Selected product
```

`Parity` is the default and is frozen B0: MV topology, no heat recovery, fan heat factor zero, no
manufacturer behaviour, unchanged design airflow and the same explicit supply/extract/transfer graph.

`Selected product` reads the authoritative `VentilationUnitReference` already assigned by Iteration 2.
It resolves all scoped AHUs or refuses the whole attempt. It layers generic fan and heat-recovery
behaviour over the foundation and does not introduce a new selection surface or authority.

## Catalogue and generic mapping

Catalogue v1 remains readable and means that no behaviour data is stated. Catalogue v2 can carry:

- sensible heat-recovery performance versus airflow, including its basis and domain policy;
- whole-unit specific fan power versus airflow, including its basis and domain policy;
- source, product identity and supply/extract maximum capacities.

Missing, invalid, ambiguous or out-of-domain data refuses. Manufacturer and model display strings are
never control-flow switches.

The PR5A generic mapping is:

- operating point: balanced design supply/extract duty under the unchanged 1.0 schedule;
- exchanger sensible efficiency: the catalogue resolver's own answer;
- fan power: certified total-both-fans SFP represented by an explicitly declared equal pressure split,
  `SupplyPressure = ExtractPressure = 1000 × SFP / 2`, with overall efficiency 1.0;
- fan heat: selected-product routing preserves the Systems graph's heat gain factors; the current PR5A
  assumption is 1.0 for supply and extract and is labelled as an assumption, not manufacturer data;
- topology: B0 uses MV; selected-product settings use the existing MVRE topology;
- Phase-0 normalisation: MVRE's zone displacement flag is aligned to the frozen B0 condition so changing
  topology does not introduce unrelated physics.

## Fail-closed rules

The selected-product attempt refuses before materialisation when any scoped AHU has:

- no selected product;
- an invalid, missing or ambiguous catalogue identity;
- no valid design duty;
- a selected product that is insufficient for that duty;
- missing or invalid certified E1/E2 data;
- an operating point the catalogue's domain policy refuses.

It also refuses if materialisation cannot bind each resolved AHU to exactly one physical AirSystem.
Partial dictionaries, fallback figures, capacity-as-airflow and product substitution are forbidden.

## Persisted evidence

Every attempt records its behaviour mode. A completed selected-product attempt additionally records:

- catalogue directory, exact file, schema and SHA-256;
- AHU GUID/name and materialised AirSystem GUID;
- selected manufacturer/model/reference and source;
- maximum supply/extract capacity;
- design supply/extract duty and separate Part F duty when recorded;
- operating-airflow basis;
- resolved heat-recovery efficiency and basis;
- resolved SFP and basis;
- mapped fan pressures, efficiency and heat gain factors;
- fan-power split rule and fan-heat assumption;
- resolver clamp/refusal state.

Review is simulation-free. It refuses an unsupported record schema/mode, contradictory parity equipment,
missing selected-product provenance, incomplete rows, or duplicate AHU/AirSystem lineage. The result
window and persisted report use the same text authority. Large comparison rows retain the existing
grouped/virtualised grid; equipment evidence is concise in the result and complete in provenance.

## Evidence gates

The first real product is Nuaire `MRXBOXAB-ECO5-AECV` (`MR-ECO-COOL-V`). Real B1/B2 acceptance requires
traceable certified/manufacturer evidence for:

- E1: sensible heat-recovery efficiency versus airflow, including basis/configuration;
- E2: specific fan power versus airflow, including basis/configuration.

Marketing maxima, retail figures, MVRE defaults, guessed interpolation and another product's figures are
not evidence. If E1/E2 are absent, the generic implementation may merge only as a fail-closed path; no
real selected-product annual result may be claimed.

Licensed TAS component evidence must establish native exchanger and fan semantics before annual work.
Full-year source data with narrow TPD windows is used for component checks; the known problematic one-day
TSD route is not used. Native readback must agree with the input and hand calculation.

## Acceptance

COM-free coverage must demonstrate v1 compatibility, v2 reading, invalid-schema/data refusal, identity
resolution, capacity recheck, generic settings, structural fan roles, exchanger mapping, phase-0 zone
normalisation, deep isolation, deterministic output, B0 equivalence and acceptable 5,000-space scaling.

Once E1/E2 exist, each licensed annual variant must retain 3 systems, 8 rooms, 14 legs, zero design-flow
delta, complete 8 × 8760 zone/resultant-temperature series, product identity, resolved generic values,
native readback, in-session review, close/reopen review, and no TAS simulation during review.

Attribution is reported separately:

```text
B1 - B0 = fan behaviour/heat layer
B2 - B1 = heat-recovery layer
B3 - B2 = bypass layer, only if later licensed evidence retains B3
```

Success is not judged by resemblance to Reference A: the B0 forensic study closed the remaining A/B
residual as an expected legacy-IZAM versus explicit-Systems representation difference.

## Explicit exclusions

PR5A does not add DX trim cooling, increased cooling airflow, room-temperature controllers, cooling
plant, variable operating airflow above design, manufacturer-name branches, or PR5B vocabulary.
