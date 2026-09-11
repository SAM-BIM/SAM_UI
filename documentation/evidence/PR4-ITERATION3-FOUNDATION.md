# PR4 — Approved Document O Iteration 3 (A/B) foundation

SAM-BIM/SAM#111 PR4. Orchestration and presentation only, in `SAM_UI`. SAM, SAM_Systems and SAM_Tas
carry **no** production change and were verified clean throughout.

| repository | SHA | changed |
| --- | --- | --- |
| SAM | `413215cca722a70b660c4ef367f6faab1d6d9357` | no |
| SAM_Systems | `89cf139966f4fe426459851d09f052834551792f` | no |
| SAM_Tas | `1d62f380f0d3f0692b9cf0cc8ecf8b37f93fe375` (includes #52, the yearly-profile alignment fix) | no |
| SAM_UI | branched from `af7535db7746825e4132369b557939831b27ef5c` | yes |

SAM_Tas was **built from source** at `1d62f380` before SAM_UI, so no stale `HintPath` DLL could
reintroduce the one-hour profile shift.

---

## 1. What PR4 is, and the line it does not cross

SAM_UI sequences four existing authorities, checks identity between them, computes descriptive
statistics, and presents and persists the evidence. It holds **no** engineering authority of its own:

| owns | authority |
| --- | --- |
| SAM | the analytical design, the Part F sizing, `PreparePartOIteration`, **TM59** |
| SAM_Systems | the mechanical-ventilation materialisation (PR1) |
| SAM_Tas | the no-IZAM thermal source, the TPD conversion, the Systems simulation (PR2), the `IResultantTemperatureProvider` (PR3) |
| SAM_UI | sequencing, scope, identity checks, descriptive statistics, presentation, persistence |

No TM59 criterion, threshold, exceedance rule, count, status or combined verdict exists in SAM_UI. No
heat recovery, fan heat, equipment selection, capacity substitution or manufacturer behaviour is
configured. Part F, the existing Prepare Part O, the ordinary Energy Simulation, the existing TM59
calculations, Iteration 2B and `PartORun.Restore` are untouched.

`DesignAirFlow` is the only airflow authority that reaches TAS, and the frozen invariant

```text
PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow
```

is enforced at reconciliation by cross-checking every bound room's supply and extract duty against the
**prepared design's own terminals**. A Part F requirement and a selected unit's rated capacity
substituted for the design airflow are both pinned as negative tests.

---

## 2. The ordered stage ledger

Fourteen stages, in the order they are attempted:

```text
Input  ReferenceA  ReferenceATM59  SystemScope  Materialisation  ThermalSource
SystemsConversion  SystemsSimulation  ZoneTemperature  ResultantTemperature
CandidateBTM59  Reconciliation  Comparison  Persistence
```

The ledger — not the caller — enforces the pipeline rule. `PartOIteration3Ledger.Refuse` fixes the
refused stage and **rejects any later completion outright**, so every stage after a refusal stays
`NOT RUN` whatever the caller does. `PartOIteration3Result` then drops the comparison unless the ledger
is complete, and the result window collapses the comparison summary, the filters and the grid on a
refusal. A refused pairing presents **no Candidate B number anywhere**.

Refusal reasons from SAM_Systems, SAM_Tas and TM59 are carried **verbatim**.

---

## 3. SAM #114 — the production answer, and the gate PR4 adds

### By identity, in the caller

`PartORun` now captures the identities of `PartOIterationPreparation.VentilationSystems` at the moment
the run adopts the preparation — the only moment the answer is known. They are exposed read-only and
cleared on reset, invalidate and restore. No rule recovers the answer afterwards: type does not (a
legacy MV system is mechanical too), terminals do not (a competing design carries them), and the
display name never does.

`Query.PartOIteration3SystemScope` keeps exactly those systems, removes an authored system carrying no
effective mechanical duty from a **copy** of the cluster with an evidence note, and refuses a system
that does carry one. The design itself is never rewritten; no natural ventilation, uncontrolled
ventilation, opening or infiltration is removed from anything that is simulated.

### The whole-thermal-domain comparability gate

Candidate B's no-IZAM source removes mechanical ventilation **model-wide**. So the gate is both halves:

1. **`SystemScope` refuses** an unrelated system that carries an effective design terminal on **any**
   thermally participating room — not only inside the dwelling scope. A room outside the assessed
   dwellings is not harmless context: its ventilation exists in Reference A, is gone in Candidate B, and
   it is thermally coupled to the assessed rooms.
2. **`Reconciliation` refuses** a retained system's design duty that Candidate B's explicit route did
   not reinstate.

Together they prove every effective mechanical ventilation the sweep removed is explicitly recreated.
PR1 is **not** broadened to materialise unrelated systems: the pairing fails closed instead.

### On the canonical fixture

The three Part O MVHR systems are retained. `NV`, `UV` and `MV` are excluded, each with the recorded
reason that it *carries no design ventilation terminal* — so none of them states mechanical duty
Candidate B has to recreate, and each remains on the design and in the thermal model where its authored
behaviour is simulated exactly as Reference A simulates it. That is the canonical expected #114
behaviour, measured rather than argued.

---

## 4. Candidate B is the same thermal case as Reference A

`Modify.RunPartOSimulation` gains **one** optional seam — a `PartOWorkflowRunner`. Null (the default,
and every existing caller) executes `Modify.RunWorkflow` with exactly the arguments it always had. Only
the workflow's own last step is substitutable; the deep working copy, the material repair, the
construction layers, the gbXML, the solar calculation, the design days, the surface output spec and the
day range are the single shared pipeline.

Candidate B therefore starts from `run.AnalyticalModel_Prepared` and a **complete copy** of Reference
A's `PartOSimulationContext`, changing only the project name — pinned by a reflection test so a property
added later cannot quietly fail to be copied. It is run with `partORun: null`, so it creates no false
Reference-A provenance and no Part O completion state.

Eligibility: an in-session, assessable, completed **Iteration 1a** full-year run with captured prepared
system identities. A restored run may review a completed record and may not start a new Candidate B —
the same rule Iteration 2B follows, for the same reason. Iteration 1b and Iteration 2 are out of scope.

---

## 5. Licensed acceptance

Canonical fixture `SAM_zoningAM-CIBSEfutureZ1.sam`, SHA-256
`A7E09A25AE29C7DBB4C690D747A96FCD9F110CA27FB4DC2ABE816755368D7E4B` — **verified**. Weather
`Leeds_TRY` from `CIBSE Weather 2021.twd`, CIBSE 2021 case, days 1–365. Licensed TAS present.

Driven through the real production calls in the order SAM_UI drives them — `PartFCalculator.Calculate("Flats")`,
`Query.PartFDwellingZones`, `Modify.PreparePartOIteration(BasePassive, …)`, `PartORun.Prepare`,
`Modify.RunPartOSimulation`, `PartORun.Complete`, `Modify.RunPartOIteration3`,
`Modify.ReviewPartOIteration3`.

### 5.1 The pairing

Reference A: 1.0 min. Iteration 3: 2.2 min. **All fourteen stages COMPLETED.**

| gate | required | measured |
| --- | --- | --- |
| physical MVHR air systems | 3 | **3** |
| served rooms | 8 | **8** |
| `Corridor_1` bound | no | **not bound** (8 of 9 spaces) |
| directed legs | 14 | **14** (3 supply, 6 extract, 5 transfer) |
| maximum airflow delta | 0 l/s | **0** — every bound duty equals the prepared design's own terminals |
| ZoneTemperature | 8 × 8760 finite | **8 × 8760 finite**, hours 0..8759 |
| ResultantTemperature | 8 × 8760 finite | **8 × 8760 finite**, hours 0..8759 |
| provider vs Result TSD | identical | **identical over 70 080 values** |
| TM59 path | the same existing one for A and B | **the same**, `PartOTM59Assessment.Assess` |
| IZAM sweep | removed | **removed**; mechanical ventilation gain **neutralised** |
| transfer topology | the design's own | **5 legs**, matching the authored air movements |

TAS answered `"Done"` for each of the three air systems. Each fan derives its duty from the attached
zones, adds no heat (`HeatGainFactor 0`) and runs continuously at factor 1.0 on the yearly schedule
**`Part O continuous operation`**, operable in 8760 of 8760 hours.

### 5.2 A/B verdicts and diagnostics

**TM59: Reference A PASS, Candidate B PASS. 0 of 8 criterion outcomes differ.**

```text
pooled   8 rooms, 70 080 values: bias +0.373 K, RMSE 0.834 K, max |B-A| 2.909 K in 'Ensuite_8' at hour 5491
Flat 1   2 rooms, 17 520 values: bias +0.390 K, RMSE 0.749 K, max 2.454 K in 'Bathroom_2' at hour 1843
Flat 2   3 rooms, 26 280 values: bias +0.455 K, RMSE 0.882 K, max 2.633 K in 'Ensuite_5'  at hour 4819
Flat 3   3 rooms, 26 280 values: bias +0.279 K, RMSE 0.837 K, max 2.909 K in 'Ensuite_8'  at hour 5491
```

Per room, and every criterion both sides: `PR4-comparison.tsv`.

No parity threshold is stated anywhere. SAM #111 is explicit that bit-identical temperatures are not
required merely because the design airflows are identical; these are descriptive diagnostics and an
engineer decides what they mean.

### 5.3 #114 whole-domain comparability on the canonical fixture

Retained: the three Part O MVHR systems, by identity. Excluded, each with its recorded reason:

```text
NV (d50c5c6a-9466-48d4-85b2-abd783154afb)  no design ventilation terminal
UV (7ad6dfcf-2eab-4953-b476-c7886860a1f8)  no design ventilation terminal
MV (22e38d0c-6322-4064-99be-c2622c15394f)  no design ventilation terminal
```

None of the three states mechanical duty on any thermally participating room, so none is unreinstated
mechanical ventilation and the gate passes. `Corridor_1` — the one space outside every dwelling — is
served by `UV`, which carries no design terminal, so the corridor's authored behaviour is identical in
both cases.

### 5.4 The planned refusal

The bridge TBD was held open by another process. Result:

```text
Resultant temperature  REFUSED  Candidate B's resultant temperature was not produced.
    The no-IZAM TBD could not be copied to '…\Flat-It3B-Bridge.tbd': the process cannot access the
    file … because it is being used by another process.
Candidate B TM59       NOT RUN
Reconciliation         NOT RUN
Comparison             NOT RUN
Persistence            NOT RUN
```

- The exact failing stage is `Refused`; every later stage stays `NOT RUN`.
- **No Candidate B numerical result is presented** — `Comparison` is null and the record carries no binding.
- The refused record's artifacts are **only** what this attempt wrote: `Flat-It3B.tbd (updated)`,
  `Flat-It3B.tsd (updated)`, `Flat-It3B.tpd (updated)`. The 16 MB bridge TSD and the TM59 report left by
  the **previous successful** attempt are on disk, unchanged, and are correctly **not** claimed.
- The previous Candidate B `.sam` was **deleted at attempt start**, so no stale reopenable model survived.

Removing the lock and rerunning produced a complete pairing again, with statistics **bit-identical** to
the first run and a **byte-identical** comparison TSV.

### 5.5 Reopen

`Modify.ReviewPartOIteration3` over the record the run had just written:

- **TAS processes before / after the review: 1 / 1.** No `TBD.exe`, `TSD.exe`, `TAS3D.exe` or `TPD.exe`
  was started.
- **Every TAS artifact's length and write time unchanged.** No TAS file was regenerated or touched.
- The record validated: schema, `Path_TSD_ReferenceA`, Reference A's design-state and scenario
  fingerprints, and **all eight** Candidate B file fingerprints by length and write time.

Then the bridge TSD was touched, and the review refused **by name**:

```text
The Iteration 3 Candidate B bridge TSD at '…\Flat-It3B-Bridge.tsd' has been rewritten since the run
produced it, so it is no longer the file this comparison was built from.
```

with no comparison produced. That gate passes.

### 5.6 One gate could not be demonstrated — and it is not PR4's

**Reopening a persisted `.sam` in a fresh process cannot be demonstrated on this fixture, because a
pre-existing defect refuses it before any Iteration 3 code runs.**

`SAM.Analytical.SimulationResultProvenance.Fingerprint(AnalyticalModel)` is **not stable across a
`.sam` save and reload** for a model the TAS workflow returned:

```text
Flat.sam   recorded  model fingerprint  ef530acf1a722455
           reopened  model fingerprint  79c46a0a05ac16b1
PartORun.Restore = False
  "The model has changed since the simulation results at '…\Flat.tsd' were produced from it …"
```

Isolated three ways:

1. The **raw fixture** round-trips stably (`61a33821ae200c09` → `61a33821ae200c09`), so it is not the
   `.sam` writer in general. It is what the TAS workflow leaves on the returned model.
2. It reproduces with **no Iteration 3 anywhere near it** — a plain Iteration 1a
   Prepare → Simulate → persist → reopen.
3. **It reproduces on the untouched baseline build at `af7535db`**, which contains no Iteration 3 code
   at all. Same fixture, same route, same refusal.

So it blocks the pre-existing "reopen a saved Part O run and Review Results" feature on this fixture,
independently of this PR. PR4 changes nothing on that path: `PartORun.Restore`,
`Modify.PersistPartORunModel` and `SimulationResultProvenance` are untouched, and the captured system
identities are never persisted.

The same defect surfaces once more on PR4's own side: `Query.PartOIteration3ReviewRefusals` validates
Candidate B's persisted model against the bridge TSD through the same `SimulationResultProvenance`
authority, so a review that reloads that model refuses too — **correctly, by name, and fail-closed**,
producing no Candidate B numbers. The whole review path is otherwise covered COM-free by eight tests,
including one that hands the review a pipeline whose four TAS-running members throw and asserts it never
reaches one.

**Recommended follow-up, outside PR4 and in the SAM repository:** make the design-state fingerprint
stable for a model carrying TAS result series, or exclude those series from it. This blocks reopen for
both the existing Part O review and Iteration 3, and both are fixed by the same change.

---

## 6. Persistence, lineage and stale-result protection

One JSON pairing record beside Reference A's results — `<run>-Iteration3.json` — plus Candidate B's own
`.sam` provenanced to the **bridge** TSD through the existing `SimulationResultProvenance`.

The record carries the input and scenario fingerprints, Reference A's provenance, Candidate B's source /
TPD / bridge paths with length and write time, the prepared system identities, the scoped-out systems
with their reasons, the PR1 bindings (room → SystemSpace → AirSystem → native zone, with both design
duties), the leg counts by type, the provider method, both TM59 report paths and verdicts, the stage
ledger, the refusal reasons and the provider-versus-TSD identity result.

**Deterministic paths are stale-prone by design**, so ownership is proved rather than assumed.
`PartOIteration3Artifacts` fingerprints every fixed Candidate B path **before a byte is written**; a
file is claimed as this attempt's only where it exists now and either did not exist at the snapshot or
differs from it. A file that is unchanged since the snapshot **refuses** — it is an earlier attempt's.
An unsnapshotted path refuses too: fail closed. The previous Candidate B `.sam` and the previous record
are additionally **deleted** at attempt start, because those two are what a later session would act on.
PR3's and the no-IZAM source's own stale-output deletion is untouched and still runs.

---

## 7. Tests

`SAM.Analytical.UI.WPF.Tests`: **949 passed, 0 failed** — 810 baseline, unchanged, plus 139 new.

| file | covers |
| --- | --- |
| `PartOIteration3LedgerTests` | ordered ledger, refusal propagation, later stages never run, verbatim reasons, comparison dropped on a refused ledger |
| `PartOIteration3SystemScopeTests` | SAM #114 canonical scope, competing in-scope mechanical design, **out-of-scope thermally participating unreinstated duty**, source-cluster immutability including NV/UV, identity over display name |
| `PartOIteration3RunTests` | every pipeline refusal boundary and "the later delegate is never called", the complete pairing, the same-case copy, scoped capture, current-RunId artifact ownership, the record written on a refusal |
| `PartOIteration3ReconciliationTests` | Part F requirement ≠ design airflow, selected capacity ≠ design airflow, missing/extra bindings, criterion mismatch, transfer mismatch, no-IZAM sweep evidence, duplicate room names proving guid joins |
| `PartOIteration3ComparisonTests` | hand-calculated bias / RMSE / max / argmax, pooled ≠ mean-of-rooms, first-hour tie-break, short and non-finite series, statuses carried verbatim including a room exactly on its limit |
| `PartOIteration3RecordTests` | JSON string round-trip of every field, binding, file record and ledger row |
| `PartOIteration3ArtifactTests` | stale / missing / touched files, created vs updated, unsnapshotted path |
| `PartOIteration3CaptureTests` | ResultantTemperature capture keyed through `SimulationSpaceMap`, scoped, non-number hours read as NaN |
| `PartOIteration3StateTests` | prepared-system identity capture / reset / invalidate / restore, simulation-context complete-copy reflection test, exact 8760 × 1.0 schedule and its exact name, deterministic paths |
| `PartOIteration3EligibilityTests` | in-session, full-year, Iteration 1a only, captured systems, restored may review not run, record schema |
| `PartOIteration3ReviewTests` | reopen rebuilds the comparison, **never reaches a TAS-running member**, deterministic rebuild, touched / missing file refused by name, changed design state refused, refused record shows its ledger only |
| `PartOIteration3PresentationTests` | a refusal shows no Candidate B number anywhere, artifacts of this attempt only, the three filters, deterministic Copy All |
| `PartOIteration3ScalingTests` | the comparison join is **exactly one lookup per room per side** at 10 / 1 000 / 5 000 rooms; scope, room index and row build hold linear allocation across four doublings to 5 000 dwellings |

Scaling is proved by a counting dictionary (structural, machine-independent) and by allocation ratios
across doublings — this repository's established evidence — not by a wall-clock threshold.

---

## 8. Programme state

```text
PR1 MERGED   PR2 MERGED   PR3 MERGED   #52 MERGED
PR4 this branch — licensed acceptance passed except the pre-existing reopen defect in section 5.6
```

SAM #111 stays **open** for PR5 manufacturer-aware behaviour. SAM #114's production resolution is
implemented and demonstrated on the canonical fixture; it should be recorded and closed only after this
PR is reviewed and merged.
