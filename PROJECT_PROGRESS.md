# Project Progress

## Branch
`feature/parto-iteration2b-tas-warm-start`, branched from
`feature/parto-iteration2b-capacity-envelope` at **`38c16c3`**.

**Depends on, and merges after:**

1. `SAM-BIM/SAM#90` - the capacity-envelope authority.
2. `SAM-BIM/SAM_UI#79` - the capacity-envelope orchestration, which this branch is stacked on.
3. `SAM-BIM/SAM_Tas` `feature/parto-iteration2b-tas-warm-start` - `WorkflowSettings.Path_TBD_Canonical`,
   without which this does not compile.

## Last updated
2026-09-02 - Iteration 2B rounds start from the run's own canonical TBD instead of converting the same
geometry again.

## Latest (2026-09-02): warm starting each round from the canonical TBD

**Status: implemented, tested, and proven equivalent to the full path on the licensed model.**

### Why - measured, not assumed

Between 2B rounds only the ventilation state changes. The licensed acceptance run's own timing CSV shows
what re-converting the rest costs: of a **64.2 s** round on `SAM_zoningAM-CIBSEfutureZ1.sam`, the
gbXML/T3D/shading conversion is **41.6 s** and the full-year TAS simulation is **3.6 s**. Ten more rounds
of conversion is most of the wall clock of a 2B run, and none of it is physics.

### Measured result

| | full conversion | warm start |
| --- | --- | --- |
| run 0 (the conversion itself) | 71.1 s | 71.1 s - unchanged, it *is* the baseline |
| each later round | ~64 s | **21.0 / 23.2 / 21.9 s** |
| of which full-year simulation | 3.6 s | **4.0 s - still a real one** |
| copy of the canonical TBD | - | 4.1 ms |

A warm round's step list is the full one **minus exactly nine conversion steps** - `Opening TBD file`,
`Updating Weather Data`, `Updating HDD and CDD Day Types`, `Opening T3D file`, `Importing gbXML`,
`Updating T3D file`, `T3D to TBD -> Shading`, `Reusing Aperture Definitions`, `Updating Aperture Types` -
and nothing else. `Updating Ids`, `Updating Zones`, `Add IZAMs`, `Simulating Model` and `Adding Results` all
still run.

### Architecture

```
run 0     full production conversion -> canonical TBD + its own TSD   (unchanged)
round N   canonical TBD -> copy to <project>-OptNN.tbd
          -> the current round's complete ventilation state re-applied
          -> full-year TAS -> <project>-OptNN.tsd
          -> workflow-returned AnalyticalModel -> production TM59
```

- **Always cloned from run 0, never from the previous round.** `Run0 -> Opt01`, `Run0 -> Opt02`. Chaining
  would accumulate stale state, which is the whole failure mode being avoided.
- **The clone is `SAM_Tas`'s job, not the UI's.** `WorkflowSettings.Path_TBD_Canonical` makes
  `WorkflowCalculator` copy and skip the conversion block; SAM_UI decides *whether* to, and never touches a
  TBD itself. No low-level TBD mutation was added to the UI.
- **No gbXML is written on the warm path.** The export exists to be imported and converted, and a canonical
  TBD is the product of having done that.
- **`UpdateZones` is forced on** for a warm-started run whatever the solar method: the zones carry the
  internal conditions, and re-deriving them from the current model is half of what makes the round the
  current design rather than the baseline's.

### `PartOCanonicalTBD` - the invalidation authority

**Not a cache.** It is created by run 0 of one optimisation, used only by that optimisation's rounds, and
never persisted or found again. The classic failure of a reused conversion is a stale one surviving a model
edit; a baseline that cannot outlive the run that made it cannot go stale that way at all.

A **fingerprint** over what the conversion reads - space and zone identities *and names* (TAS matches a
zone to a space by name), zone topology, panel and aperture identities, types, constructions and a
millimetre-rounded geometry digest, plus the solar method, weather, day range, sizing, unmet hours,
aperture widths and construction-layer update. **Design airflow is deliberately absent**: it is the thing
that changes every round and the thing the warm-started run re-applies, so including it would turn the warm
start off entirely.

Re-checked **every round**, plus the file's length and write time, so a baseline replaced underneath a
running optimisation is caught. Any mismatch **falls back to the full conversion** and names the category
that changed. A fallback is a note, never a refusal - the full path is always available and always
authoritative.

The digest is FNV-1a rather than `string.GetHashCode`, which is randomized per process and would have made
the comparison work in one place and not another.

### Files

| File | Change |
| --- | --- |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOCanonicalTBD.cs` | New - the baseline and its fingerprint. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOptimisationSettings.cs` | `WarmStart`. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOptimisationStep.cs` | `WarmStarted`, per iteration. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOptimisationRun.cs` | `CanonicalTBD`, its refusal, and the derived `WarmStarted` count. |
| `WPF/SAM.Analytical.UI.WPF/Modify/RunPartOSimulation.cs` | The optional canonical; no gbXML, no SAM-side TBD build, `UpdateZones` on. |
| `WPF/SAM.Analytical.UI.WPF/Modify/OptimisePartOTM59.cs` | Adopt once, check every round, and the same for the envelope. |
| `WPF/SAM.Analytical.UI.WPF/Windows/PartOIterationWindow.xaml(.cs)` | The tick, so the full path stays available as the reference. |
| `WPF/SAM.Analytical.UI.WPF/Windows/PartOOptimisationResultWindow.xaml.cs` | The count, beside the notes. |
| `WPF/SAM.Analytical.UI.WPF.Tests/PartOWarmStartTests.cs` | New - 26 tests. |

### Validation

- `SAM_UI.sln` builds clean.
- `WPF/SAM.Analytical.UI.WPF.Tests`: **325 passed, 0 failed** (299 + 26 new).
- `SAM_Tas`: **655 passed, 0 failed** (649 + 6 new).
- SAM: **1731 passed, 0 failed**, unchanged by this branch.

### Licensed A/B equivalence - ACCEPTED

`SAM_zoningAM-CIBSEfutureZ1.sam`, weather `Z1_DSY1_2050s_HIGH90_CIBSE_v1.1`, 3 rounds plus the capacity
envelope, run twice through the production commands - once with the tick off (the full-conversion
**reference**) and once with it on (the warm-start **candidate**). Identical model, identical design
airflows, identical weather, identical workflow settings; only the output directory differed.

**108 comparable lines, every one identical** - 40 production TM59 rows (Actual, Limit, Margin, PASS/FAIL,
mechanical, per space per step), 24 targeted and 8 derived design airflows, 20 ventilation-unit
duty/maximum/headroom/outcome rows, 3 capacity-envelope groups (scale, movement, duties, binding side), and
every run-level verdict including `STOP_REASON`, `ROUNDS`, `ENVELOPE_OUTCOME` and `COMMAND_WOULD_ARM`.
Paths, project names and the `warm=` flag were excluded from the comparison; nothing else was.

**Canonical immutability, from the production code itself.** `AB.tbd` was written at 16:37:26 and never
again, while the four rounds wrote their own TBDs at 16:37:55, 16:38:19, 16:38:45 and 16:39:09 - and
`PartOCanonicalTBD` re-verified its length and write time before every one of them, all four of which warm
started. The baseline step correctly did **not** warm start: it is the conversion the others start from.

### Issues / blockers

- **One bounded residual risk, stated rather than hidden.** `SAM_Tas`'s `Modify.UpdateIZAMs` removes
  internal conditions and IZAMs by the names the *current* cluster's air movements produce, and
  `UpdateZones` removes internal conditions by the current space names. Both then re-add. So a canonical
  entry could survive only if its name is one the current round does not produce. Between 2B rounds the
  movement names derive from space and unit names (invariant) and the movement *set* from the transfer-air
  topology, which is rebuilt from the same Part F requirements (invariant) - and the A/B equivalence above
  confirms it empirically on the licensed model. Cloning always from run 0 rather than chaining bounds this
  to run 0's own entries instead of letting anything accumulate, which is why that rule is not negotiable.
  A model whose round-to-round movement *names* could differ would need `UpdateIZAMs` to remove everything
  it owns before writing; that is not this change.

## Superseded (2026-09-02): the capacity envelope

**Status: implemented and tested; PR open against `sow/2026-Q3`.**

### Why it was needed

The ordinary Iteration 2B loop stops on `CapacityReached` or on its iteration guard with eligible rooms
still failing TM59, hands back the last valid design, and says why. What it cannot say is how close the
ventilation unit **already bought** can get - and that is the thing an engineer needs in order to decide
between changing the fabric, changing the equipment, and accepting the result.

### What was added

One optional final **diagnostic** stage, after the ordinary optimisation has reached its terminal
condition:

```
last ACCEPTED ordinary design
  -> the same deliberate target vector the +5 policy would next have asked for
  -> Modify.EvaluateDesignAirFlowCapacityEnvelope    one coherent scale per equipment group
  -> re-prepare Part O (NO catalogue)                transfer air, network and duties rebuilt
  -> full-year TAS, its own -OptMax TBD/TSD          the SAME weather case
  -> production TM59, on the model the workflow RETURNED
```

Every guarantee the rounds keep is kept here: the preparation is offered no catalogue so the Iteration 2
product survives; the TAS case is the baseline's, verbatim; the assessment reads the model the workflow
returned and never the preparation output; and the results file is the envelope's own.

### The separation, which is the whole design

An envelope is prepared, simulated over the full year and assessed **exactly** as a round is, and it
completes. Nothing about its lifecycle distinguishes it from a round - so everything that could read it as
one was changed to ask what kind of step it is:

- `PartOOptimisationStepKind` - `Baseline` / `OptimisationRound` / `CapacityEnvelope`, stated rather than
  inferred from an iteration number.
- `PartOOptimisationRun.Step_LastValid` excludes the envelope. **This is the most important line in the
  change**: without it the run would hand back, and the command would adopt, a partial step the
  optimiser's own all-or-nothing policy refuses.
- `PartOOptimisationRun.Rounds` excludes it, so it is never reported as another successful +5 step.
- Its model, TSD and scenarios live in their own properties -
  `AnalyticalModel_CapacityEnvelope`, `Path_TSD_CapacityEnvelope`, `OverheatingScenarios_CapacityEnvelope`
  - and `CapacityEnvelope` carries SAM's own per-equipment scales and reasons.
- It runs on its **own private `PartORun`**, so the session's run keeps holding the last accepted ordinary
  design and its results. Driving it through the session's run would have left the user assessing the
  diagnostic's results against the accepted design's model.
- `-OptMax`, named rather than numbered: `-Opt`*nn* would put it in the rounds' sequence where the last one
  is the answer. `Iteration_ProjectName` reads it back as no iteration at all.
- Presentation: a `Stage` column in both histories (BASELINE / OPTIMISATION / CAPACITY ENVELOPE), a `MAX`
  run label, its own line above the grids, and its own labelled clause in `Description`. A room whose first
  movement is in the envelope contributes **no** synthesised baseline row, because the envelope's "before"
  is the last accepted design's airflow and not the baseline's.

### Every "no" is recorded

Not asked for; the run passed; a stop reason an envelope does not answer; no failing verdict; nothing
eligible left to target; no useful headroom; an unresolvable capacity; a vector that cannot be formed. Each
is written to `CapacityEnvelopeDescription` in its own words, and for a non-scaled envelope each equipment
group's own reason is carried up onto that line - "this unit is already at its rating" and "its capacity is
not in the catalogue offered" are different findings and neither may be buried. **No TAS run is spent on
any of them**: every no-run decision is settled before the simulation.

### Optional, and on by default

A new `PartOOptimisationSettings.CapacityEnvelope`, exposed as its own tick under the 2B controls rather
than as another number beside the step and the limit - it is a diagnostic, not a further optimisation
parameter. On by default, because the case it answers is exactly the case in which the run on its own does
not tell an engineer what to do next; it costs one more full-year simulation, and nothing at all on a run
that passes.

### Files

| File | Change |
| --- | --- |
| `SAM_UI/SAM.Analytical.UI/Enums/PartOOptimisationStepKind.cs` | New - the three kinds of step. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOptimisationStep.cs` | `Kind`, `IsOptimisationRound`, `IsCapacityEnvelope`. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOptimisationRun.cs` | Envelope model/TSD/scenarios/description; `Rounds` and `Step_LastValid` exclude the envelope. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOOptimisationSettings.cs` | `CapacityEnvelope`. |
| `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOSimulationContext.cs` | `ProjectName_CapacityEnvelope()` - `-OptMax`. |
| `WPF/SAM.Analytical.UI.WPF/Modify/OptimisePartOTM59.cs` | The envelope stage; the loop split out unchanged as `Optimise`. |
| `WPF/SAM.Analytical.UI.WPF/Classes/PartO/PartOOptimisationAirFlowRow.cs` | `Stage`; `Run` is a string; envelope excluded from baseline synthesis. |
| `WPF/SAM.Analytical.UI.WPF/Classes/PartO/PartOOptimisationUnitRow.cs` | `Stage`; `Run` is a string. |
| `WPF/SAM.Analytical.UI.WPF/Windows/PartOOptimisationResultWindow.xaml(.cs)` | Envelope line, Stage columns, kind-labelled diagnostics, Copy All. |
| `WPF/SAM.Analytical.UI.WPF/Windows/PartOIterationWindow.xaml(.cs)` | The envelope tick and its explanation. |
| `WPF/SAM.Analytical.UI.WPF.Tests/PartOCapacityEnvelopeTests.cs` | New - 22 tests. |
| `WPF/SAM.Analytical.UI.WPF.Tests/PartOOptimisationTests.cs` | `Run` comparisons follow the string column. |

### Validation

- `SAM_UI.sln` builds clean.
- `WPF/SAM.Analytical.UI.WPF.Tests`: **299 passed, 0 failed** (277 post-#78 baseline + 22 new).
- SAM side: `SAM.Tests` **1727 passed, 0 failed** (1703 + 24 new).
- The 22 new tests cover: a completed envelope is not the last valid design, by object identity; it is not
  counted as a round; the description keeps it apart; its production TM59 is stored separately and can pass
  while the run's answer still fails; `-OptMax` is distinct from every round name and reads back as no
  iteration; the three stages are distinguishable in both grids; a room only the envelope moves gets no
  synthesised baseline row; and each no-run decision - not asked for, passed, every non-answering stop
  reason, no failing verdict, no eligible target, passing rooms only, and no useful headroom - reaches no
  simulation and appends no step.

### Issues / blockers

- None known.

### Next step

- Merge SAM `feature/parto-iteration2b-capacity-envelope` first, then this PR.
- Task 2, the canonical-TBD TAS warm-start, is a separate deliverable and a separate PR.

## Superseded (2026-09-02): pinning the subset-pass guard and the round count - merged as PR #78

**Status: implemented and tested; PR open against `sow/2026-Q3`.**

`81d9117` fixed two findings - a TM59 pass over a subset is not a pass (P1), and an unattempted round is
not a round (P2) - with no regression tests, the only review fixes on either PR shipped that way. This
change pins them.

- `Modify.PartialAssessment` and the `PartOTM59Assessment` constructor are **internal** rather than
  private, so the guard is testable through the existing `InternalsVisibleTo` - the same seam
  `PartODwellingSpaceGuids` was given in `81d9117`. The production route to an assessment remains
  `Assess`, which needs a real TSD.
- New tests in `PartOOptimisationTests`: an in-scope room the assessment never looked at turns the pass
  into a refusal naming that room; unassessed rooms outside the dwelling scope (corridor, AHU simulation
  zone) do not; a fully assessed pass is untouched. Verified the first fails with the guard neutralised.
- The phantom-step fix's recording side sits behind the TAS seam and is not unit-testable here; what is
  pinned is the run-level contract it restored - a baseline-only run reports **0 rounds** and the baseline
  as the last valid design.

**277** tests in `WPF/SAM.Analytical.UI.WPF.Tests` pass (273 + 4 new).

### Next step

- Merge the PR.

## Superseded (2026-09-02): Iteration 2B automatic optimisation - merged as PR #77

## Current status (at the #77 merge)
Iteration 2B is implemented end to end in SAM_UI and accepted on the licensed future-weather fixture.
From a completed Iteration 2 run it raises the design airflow of every eligible failing mechanically
ventilated room by a fixed step, rebalances through the new SAM round, rebuilds the Part O state,
re-simulates the **same** weather case under its own project name, reassesses with production TM59, and
repeats until an explicit stop reason is reached.

Merged as PR #77 (`d056af7`); **273** tests in `WPF/SAM.Analytical.UI.WPF.Tests` passed at merge.

## Integration state
Sibling repos, and what changed:

- `SAM` @ `sow/2026-Q3` `0ae4b929` + **PR #88** - the new `Modify.EvaluateTargetedDesignAirFlows` round.
  **This is a required companion change.** (Merged.)
- `SAM_Systems` @ `fea5055` - **unchanged**. Nuaire `MRXBOXAB-ECO5-AECV + MR-ECO-COOL-V`, 150/150 l/s.
- `SAM_Tas` @ `78f7afb` - **unchanged**.

### What was built

- `PartOOptimisationSettings` - the fixed step (5 l/s default) and the mandatory iteration guard.
- `PartOPreparationContext` / `PartOSimulationContext`, both carried on `PartORun` - how the run was
  prepared and which TAS case produced it, so a round repeats **both** rather than re-asking. Cleared when
  a run is dropped.
- `Modify.RunPartOSimulation` - `Simulate`'s TAS pipeline, extracted so the optimiser repeats the exact
  same case under its own project name (and therefore its own TBD/TSD) without a second copy existing to
  drift. `Simulate` now calls it.
- `PartOTM59Assessment` - the assessment sequence, extracted for the same reason, and resolving every
  result to its **design** space by identity through `SimulationSpaceMap`.
- `Query.PartOOptimisationTargets` - the target policy: production TM59 mechanical failures, inside the
  Part O dwelling scope, supply/extract/both-means-supply, and an explicit *not automatically optimisable*
  record where a room has no design terminal. Scope excludes the corridor and the AHU simulation zones
  **by scope, not by name**.
- `Modify.OptimisePartOTM59` - the loop. Each round is a complete `PartORun` lifecycle, so every PR #76
  guarantee holds rather than being worked around.
- `PartOOptimisationRun` / `PartOOptimisationStep` / `PartOOptimisationStopReason` - the history, with the
  baseline as run 0.
- UI: the 2B tick with step and iteration limit on the preparation window (**not** in the base-provision
  dropdown), an *Optimise (2B)* ribbon button, and a results window with both histories.

### Two settled judgement calls

1. **Per-dwelling capacity.** A dwelling whose selected unit refuses a round leaves the optimisation and
   its last valid design is preserved; independently served dwellings continue. No partial or clamped step
   is ever adopted for the blocked dwelling.
2. **Run 0 keeps the Iteration 2 workflow's own TSD** rather than being re-simulated as `-Opt00.tsd`. It
   retains its original TSD, weather and workflow identity in the history.

### Equipment is fixed for the whole optimisation

The re-preparation is given **no catalogue**. `PreparePartOIteration` with one re-runs smallest-capable
selection against the realized duty, so every round the design grew it would quietly buy the next product
up and capacity would keep moving. With null it reuses the existing system and unit through their design
terminals, so the Iteration 2 selection survives. The catalogue is still used - by the round, to read what
the selected product is rated at.

### Licensed acceptance - ACCEPTED

`SAM_zoningAM-CIBSEfutureZ1.sam`, weather `Z1_DSY1_2050s_HIGH90_CIBSE_v1.1`, 12 full-year TAS runs, 13 min.

- Iteration 2 Run 0 reproduces the Iteration 1a baseline **exactly**; product selection caused **no**
  thermal change (8 results identical, 0 different).
- Round 1 produced the intended targets: Flat 1 Studio 1_0 supply 30->35 and Bathroom_2 extract 8->13 with
  **no** derived change (they balance directly); Flats 2 and 3 two extract targets each deriving
  Bedroom 2_3 / 2_6 supply 63->**73**.
- TM59 improved monotonically every round (Bathroom_2 731->375, Ensuite_8 677->331, Studio 1_0 388->361).
  Passing rooms were never targeted.
- MVHR-02/03 reached 143/143 l/s (7 l/s headroom) by round 8; at round 9 the round would have designed
  153/153 against 150/150 and was **refused**, those flats left the optimisation, and Flat 1 continued
  alone to 80/80. Product `Kept` on every row, never `Reselected`.
- Final stop `IterationLimitReached` at the guard of 10, last valid design = run 10.

### Review findings addressed (Codex, PR #77)

Round 1 (`dd6b24a`):
1. **P1** - a round whose assessment failed left `PartORun` in `WorkflowCompleted` holding that round's
   model and TSD while the previous design was restored. Now invalidated, and the command arms
   `ExpectModification` only where the run holds the very model being adopted.
2. **P2** - 2B was offered on the natural-ventilation route. Now gated on the selected option's
   ventilation mode and refreshed when the base provision changes.
3. **P2** - the airflow history began at run 1. Baseline rows are now synthesised from each room's first
   adjustment plus the baseline step's own production verdict.

Round 2:
4. **P1** - `Simulate` built the simulation context and then completed the run through the **context-less**
   `Complete` overload, so every baseline produced through the window had a null `SimulationContext` and
   was refused by `CanOptimise`. The Optimise button could never have started. Fixed; a test pins the
   invariant.
5. **P1** - `OccupiedSpaceComplianceStatus != Fail` treated `Undefined` / `NotApplicable` as a pass, so an
   assessment that reached no verdict would have been reported as every eligible space passing. Only an
   explicit `Pass` now ends a run as passed; anything else stops as `AssessmentFailed`.

Round 3:
6. **P2** - an optimisation run a second time over the design a previous one left behind restarted its
   round numbering at `-Opt01`, overwriting the first run's TBD and TSD. The numbering now continues from
   the iteration read back off the results file the starting design came from, so `-Opt11` follows
   `-Opt10` and nothing is written twice; the baseline step is also labelled with the run that actually
   produced it rather than the context's base name.

Round 4:
7. **P1** - a TM59 report can return an explicit `Pass` over a SUBSET: `PartOTM59Assessment` excludes a
   space whose simulated counterpart does not resolve and records a warning, and the remaining rooms then
   all pass. The run would have announced that every eligible occupied space passes on the strength of an
   assessment that never looked at one of them. A pass is now believed only where no space inside the Part
   O dwelling scope was excluded; `PartOTM59Assessment.SpaceGuids_Unassessed` exposes that as identities
   rather than prose.
8. **P2** - a step was appended before the zero-target check, so a run that stopped at
   `NoEligibleTargets` reported a round that was never attempted and inflated `Rounds`. The step is now
   recorded only once there is a round to attempt, and the target-selection reasons go on the step that
   produced the design they were derived from.

### Validation

- `SAM_UI.sln` builds clean.
- `WPF/SAM.Analytical.UI.WPF.Tests`: **273 passed, 0 failed** (237 baseline + 36 new).
- `PartORunLineageTests` + `PartOPresentationTests` + `SimulationZoneIdentityTests`: 47/47 - no lineage
  regression.
- SAM side: `SAM.Tests` **1694 passed, 0 failed**.

### Issues / blockers

- `Build (Windows)` on PR #77 fails until SAM #88 merges - see "Branch". Expected, not a defect here.
- One Codex P2 on SAM #88 was **declined with reasons**: validating terminal quantities across every room
  of a touched system, not only the rooms a round writes. `ApplyTargetedDesignAirFlow` validates the same
  narrow set, and widening only the round would make it refuse dwellings a manual edit accepts - the exact
  drift the shared-helper design prevents. Raised as a separate follow-up against both seams.

### Next step

- Merge `SAM-BIM/SAM` #88 first, then this PR.
- The parked manual-seam defect (`ApplyTargetedDesignAirFlow` ventilation-unit reselection leaking onto the
  caller's `AnalyticalModel`) remains untouched and outstanding.

## Completed

### 1. BLOCKER 1 - the `Modify.Simulate` zone-identity corruption (fixed)

The post-workflow TBD block re-read the `.tbd` and copied `SpaceParameter.ZoneGuid` back onto the model
by matching space **name**. Two independent defects, both silent:

1. **Duplicate room names collapsed identities.** `spaces_TBD.Find(x => x?.Name == space?.Name)` returns
   the *first* match, and the write was unconditional, so three flats each containing "Bedroom 2" all
   received one flat's zone guid - overwriting the strong identity `WorkflowCalculator.Calculate` had
   just written via `SAM.Analytical.Tas.Modify.UpdateIds`.
2. **The value was re-spelled even when the match was right.** The read was
   `TryGetValue(ZoneGuid, out Guid)` against a parameter declared `ParameterType.String`
   (`SAM.Analytical.Tas.SpaceParameter`), so the raw TAS string was parsed to a `Guid` and converted back
   by `ParameterValue.TryConvert` on the way in - losing braces and case.
   `Tas.Query.SimulationSpaceKey` compares the stored strings ordinally, so a re-spelt stamp stops
   matching the TSD side.

**Concrete dependency found - removal was NOT safe. Correction 2026-09-01: the earlier justification for
this was wrong on the mechanism, and only the DomOv export is a dependent.**

The claim recorded here previously was that `Tas.TM59.Convert.ToXml` *refuses* a space whose `ZoneGuid` is
absent or empty. **It does not.**
`Tas.TM59.Convert.ToTM59(Space, TM59Manager, SystemType)`
(`SAM.Analytical.Tas.TM59/Convert/ToTM59/Zone.cs:39-43`) reads the stamp and, when it is absent or empty,
**falls back to `space.Guid`** and exports the zone anyway. No refusal, no note, no failed export.

What makes the seam necessary is that the fallback value is the wrong identity:

- With **Simulate unticked** and "Domestic Overheating" ticked, the `.tbd` is written by
  `Tas.Convert.ToTBD(analyticalModel, path_TBD, null, null, null, true)`. That last argument *is*
  `updateGuids: true`, but `AnalyticalModel.AdjacencyCluster`
  (`SAM/SAM.Analytical/Classes/AnalyticalModel.cs:179-186`) returns `new AdjacencyCluster(...)` - a copy -
  so the zone guids `Tas.Modify.Update` stamps land on a throwaway cluster and never reach the model
  `Modify.Simulate` holds. Nothing on that path stamps the model, so it reaches the export either
  unstamped or - if it was saved after an earlier simulation - carrying a *stale* stamp. **Measured on the
  acceptance fixture: `SAM_zoningAM.sam` carries nine saved stamps with zero overlap with the zones of the
  `.tbd` the export writes.** See round 2's P1 under "Review findings addressed".
- A SAM `space.Guid` is not a TAS zone guid. A TBD zone's guid is minted by `building.AddZone()`
  (`SAM.Analytical.Tas/Modify/Update.cs:577`) and bears no relation to the space it was written from, so
  the two values differ by construction.
- Therefore, without the fill, the DomOv XML is written **successfully** with `DomOverheatZoneItem/GUID`
  set to the SAM space guid - naming zones the external TAS TM59 tool cannot find, in a document that
  reports success. That is worse than a refusal, not better.

Pinned by `SimulationZoneIdentityTests.AnUnstampedSpace_ExportsTheSAMSpaceGuid_UntilTheFillGivesItTheTasIdentity`
and `...TheDomOvXmlNamesTheTasZone_OnlyAfterTheFill`, which assert the exported identity on both sides of
the fill rather than the refusal that does not happen.

**SAP and Part L do not depend on this seam** (previously claimed; not supported by their call paths):

- `createSAP` calls `Tas.SAP.Convert.ToFile(analyticalModel_TBD, ...)` - the model read back **out of the
  `.tbd`**, whose spaces `Tas.Convert.ToSAM` stamps itself (`Convert/ToSAM/Space.cs:98`). It never sees the
  restamped design model. (`ToSAP` has the same `space.Guid` fallback, but is never reached with an
  unstamped space on this path.)
- `createPartL` calls `Tas.Create.TBD_ByPartL(analyticalModel, ...)`, whose three writers
  (`UpdateInternalConditionByPartL`, `UpdateZoneGroupsByPartL`, `UpdateZoneGroups`) read no `ZoneGuid` at
  all.

Per the brief, the *removal* was stopped and the (now correctly stated) dependency recorded here.

**What was done instead** - new internal seam `Modify.RestampSimulationZoneIdentity`, and no new matching
algorithm:

- a space carrying a `ZoneGuid` **written for the `.tbd` being exported** is left untouched (the
  workflow's stamp is authoritative *and* current). Whether that is so is the caller's
  `workflowCompleted`, passed in as `stampsWrittenForThisTBD` - see round 2's P1 below, which corrected an
  earlier version of this rule that trusted any stamp;
- on the non-workflow path every space is re-derived from the newly read `.tbd`: an unambiguous name match
  replaces the stamp, anything less discards it with a note;
- an ambiguous name **refuses with a reason** instead of taking the first hit. Two simulated spaces
  stating the *same* guid are one answer, not a conflict (the rule `VentilationStrategyMap` already
  applies to a repeated claim);
- the value is copied **as a string, verbatim** - no `Guid` round trip;
- spaces left unstamped are reported in the completion dialog (capped at 5 with the remainder counted).

The precedence rule mirrors `SAM.Analytical.Tas.Query.ResolvedZone` ("guid first, exact name only as the
compatibility fallback, no match is a refusal, never a guess") rather than inventing one.

### 2. BLOCKER 2 - stateful, stale-safe Part O run context

`PartORun` (in `SAM.Analytical.UI`) with an explicit lifecycle `None -> Prepared -> WorkflowCompleted`:

- **Prepared** owns the prepared model and its `OverheatingScenario` set.
- **WorkflowCompleted** additionally owns the model the TAS workflow *returned* and the corresponding
  `Path_TSD`.
- `AnalyticalModel_Assessment` is non-null **only** in `WorkflowCompleted`. There is no code path on
  which it can be the preparation output, the loaded model, or a later run's model.
- **`WorkflowCompleted` means "this prepared run produced the full-year results being assessed"** - not
  "a TSD exists". `Complete` is legal **only from `Prepared`** and requires, in addition to a non-null
  workflow model and a TSD that exists:
  - **the simulation to have been the full annual run** (days 1-365), which
    `Query.IsPartOFullYearSimulation` decides from the `WorkflowSettings` actually handed to
    `WorkflowCalculator` - not from the "Full Year Simulation" tick box, whose day range still comes from
    the two text boxes beside it and which `shadingUpdated` can turn into a one-day run;
  - **the results file to have been created or rewritten by this workflow**, measured against a
    fingerprint (`exists` + length + write time) captured by `PartORun.ExpectResults` **before** the
    workflow ran.

  Both are enforced through the same arming: `Modify.Simulate` calls `ExpectResults` only where the
  settings describe a full-year run, and `Complete` refuses anything unarmed or announced for a different
  path. So a partial, one-day or sizing-only workflow cannot complete a run even if `Complete` is reached,
  and an earlier session's `<project>.tsd` left in the output directory cannot be adopted as this run's.
- `IsAssessable` re-checks the file's existence and write time **after** completion, so results rewritten
  by another session later are refused. That is a different problem from the one above and both checks are
  kept.
- **Staleness is rejected, not detected.** `ExpectModification()` / `NotifyModified()`: Part O commands
  arm one expectation immediately before their own `SetJSAMObject`; every other model replacement (edit,
  import, undo, redo, a second or unrelated simulation) arrives unarmed and drops the run with a reason.
  Wired in `AnalyticalWindow.UIAnalyticalModel_Modified`; `_Closed`/`_Opened` call `Reset()`.
- Session state only. Nothing is written into the model - `OverheatingScenario` has no persistence seam
  in `SAM.Analytical`, and inventing one in the UI would move engineering state into the UI's ownership.

### 3. BLOCKER 3 - SAM_Systems dependency placed in the WPF layer

`SAM.Analytical.Systems` is referenced **only** from `WPF/SAM.Analytical.UI.WPF.csproj`;
`SAM.Analytical.UI` stays free of it. `VentilationUnitCatalogue` (in `SAM.Analytical.UI.WPF`) calls
`SAM.Analytical.Systems.Query.VentilationUnitTemplates` and then `SAM.Analytical.Query.CapacityDescriptors`
/ `UnselectableVentilationUnitTemplates`. No schema parsing and no selection rule in SAM_UI. Three
distinct states: `Unavailable`, `NoneSelectable`, `Selectable`. The `Unavailable` description explicitly
says it is *not* a statement that no product could serve the dwellings.

The catalogue resolves at runtime from the installed resources
(`%APPDATA%\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON`), which the
SAM_Deploy installer already places; no deployment change was needed.

### 4. Part O preparation UI

- `PartOVentilationStrategyOption` - the picker. Offers only the two base provisions, each carrying the
  canonical word (`NV`, `MVHR`) and the iteration SAM says that route is defined over
  (`Query.PartOIterationVentilationMode`). **No free text anywhere reaches the API**, so the
  `"NaturalVentilation"` synonym - which prepares successfully then refuses every space at assessment - is
  unreachable from the UI. No analytical vocabulary was changed.
- `PartOIterationWindow` - base provision, dwelling scope, catalogue toggle. Scope comes from
  `Query.PartFDwellingZones` (the single source of that policy, including the legacy all-unmarked case);
  out-of-scope zones are derived **by difference from what the policy returned** and reported as
  "Outside current Part O dwelling preparation scope". No `UV` is assigned; no common-space criterion.
- `Modify.PreparePartOIteration` (UI command) - builds the strategy dictionary, reads the catalogue, makes
  **one** call to `SAM.Analytical.Modify.PreparePartOIteration`, and adopts the returned prepared model.
  Passes `null` (not an empty list) when no selection is wanted, so the preparation reads it as
  "no catalogue offered" = Iteration 1a.
- `PartOPreparationWindow` - two grids. Equipment (per AHU): design supply/extract duty, selected product,
  maximum supply/extract, supply/extract headroom, selection outcome - eight separate columns. Spaces:
  Part F required vs design supply/extract. Values read from `Query.AirHandlingUnitDesignDuty`,
  `Query.SelectedVentilationUnitCapacityDescriptor`, `Query.CalculatedSupplyAirFlow` and
  `PartFSpaceData.ContinuousDesignFlowRate_Lps`. The whole-run duty total is labelled as a total across
  dwellings, not as any one dwelling's duty.

### 5. TAS -> TM59 lineage

`Modify.AssessPartOTM59(PartORun)` runs the same sequence as the accepted `Tas.TSDQueryTM59Results`
component - `Convert.ToSAM(TSD)`, `Create.TM59AssessmentCalculator`, `OverheatingScenarioMap`,
`RestoreDesignInternalConditions`, `Spaces(null, null)`, `Calculate`, `TM59AssessmentReport` - and reads
its model from `PartORun.AnalyticalModel_Assessment` only. `PartOTM59ResultWindow` shows the production
report text verbatim plus the spaces that produced no result. No TM59 criterion, limit or verdict is
computed or reformatted in WPF.

`Modify.Simulate` gained a `PartORun` overload; it sets `workflowCompleted` only where
`WorkflowCalculator` actually returned a model, and completes the run with **that** model and
`Path.ChangeExtension(path_TBD, "tsd")`.

### 6. Ribbon

- Edit tab, new `Part O` group: **Prepare Iteration**.
- Results tab, new `Part O` group: **Overheating (TM59)** - enabled only for `CanAssess`, with
  `ToolTipService.ShowOnDisabled` so the reason is visible while disabled.
- The simulation window's "Domestic Overheating" tick (TAS DomOv XML) is untouched and kept conceptually
  separate.

## Review findings addressed (PR #76, 2026-09-01)

Four findings over two review rounds. Round 1: two P1 on the completion predicate and the results file.
Round 2: one P1 on stale stamps and one P2 on the assessment gate.

Two P1 findings on the first push (`1437d88`), both accepted and both about the same invariant: what
`PartORunState.WorkflowCompleted` is allowed to mean. Restated at the top of `PartORun`'s own
documentation - **"this prepared run produced the full-year results being assessed"**, never "a TSD
exists". No architecture change, no Iteration 2B.

### P1-A - a Part O run may only be completed by a full-year simulation

`completePartORun` required only that `WorkflowCalculator` returned a model. It returns one for a sizing
run too, and `Modify.Simulate` can produce a *fresh one-day* TSD when `shadingUpdated` forces a
simulation over an unticked Full Year box. Any of those promoted the run, and the TM59 command then
assessed criteria and verdicts over an incomplete hourly series.

New pure query `Query.IsPartOFullYearSimulation(WorkflowSettings)` - `Simulate && SimulateFrom == 1 &&
SimulateTo == 365`, read off the settings **actually handed to `WorkflowCalculator`** rather than off the
tick box, because the day range still comes from the two text boxes beside it. `Modify.Simulate` now
carries `workflowSimulatedFullYear` beside `workflowCompleted` and ANDs it into the predicate. Nothing
else reads it, so a normal non-Part-O simulation is unchanged.

A prepared run this simulation cannot complete is now dropped **with the reason it was actually refused
for**, before the model replacement that would otherwise report it as an outside edit, and that sentence
is appended to the completion dialog.

### P1-B - the TSD must be the one this workflow wrote

`Complete` accepted `<project>.tsd` because it existed and then recorded its *already old* write time as
this run's, so `IsAssessable` afterwards approved an earlier session's results against the newly prepared
model. `Modify.Simulate` deletes only the TBD, so a non-simulating workflow leaves the old TSD in place.

New `PartORun.ExpectResults(path_TSD)`, called **before** the workflow, fingerprints whatever is at that
path (`exists` + length + write time). `Complete` now refuses unless a fingerprint was armed for exactly
that path and the file has since been created or changed. Length as well as write time, so a rewrite
inside the filesystem's timestamp granularity is still seen; where both match, the file is treated as
untouched - refusing a genuine rerun is the safe way to be wrong.

Deleting the prior TSD was considered and rejected: it would destroy a user's previous results whenever a
Part O run failed, and it changes the existing workflow contract. The fingerprint changes nothing outside
the Part O promotion.

**Arming is where both fixes meet.** `ExpectResults` is armed only where the settings describe a
full-year run, so a partial, one-day or sizing-only workflow leaves the run unarmed and `Complete`
refuses it *even if reached* - the invariant is held by the state machine, not by the caller remembering
to check. `IsAssessable`'s post-completion stale-file check is kept unchanged: it solves the different
problem of a rewrite after a legitimate completion.

Ten regressions added to `PartORunLineageTests` (11 -> 21), and every successful completion in that file
now goes through a `CompleteThroughAFullYearWorkflow` helper that performs the production
arm -> write -> complete sequence, so a test cannot pass by calling `Complete` the way no caller does.

### Round 2, P1 - a stamp is authoritative only if the current run wrote it

`RestampSimulationZoneIdentity` treated **any** present `ZoneGuid` as authoritative. On the workflow path
that is right. On the Simulate-unticked DomOv path it is wrong: `Tas.Convert.ToTBD` deletes any existing
`.tbd` and mints new zone guids, so a stamp the model was already carrying names a zone in an *earlier*
file. The early exit preserved it and `Tas.TM59.Convert.ToXml` then exported GUIDs from the previous TBD,
which the TAS tool cannot associate with the TBD beside them.

**Not hypothetical, and not rare.** The acceptance fixture itself carries nine saved stamps, spelled
unbraced/lower-case (the fingerprint of the old `Guid` round trip), with **zero overlap** with the zones of
the `.tbd` the export writes.

The fix is a mode, because "is this stamp current?" is a fact about the run and not about the stamp:

```csharp
RestampSimulationZoneIdentity(spaces_Design, spaces_Simulation, workflowCompleted, out notes)
```

- `true` - `WorkflowCalculator` wrote `path_TBD` and stamped this model against it. Existing stamps are
  authoritative and current: **untouched**, fill only where absent. Unchanged from before.
- `false` - no workflow ran, so every space is re-derived from the newly read `.tbd`. An unambiguous name
  match **replaces** the stamp; anything less **discards** it with a note.

**The duplicate-name fix is intact.** Ambiguity is refused in both modes - the mode decides whether a
*stale* stamp survives, never whether a name may be guessed at. Three flats each with a "Bedroom 2" are
still refused, and on the non-workflow path their stale stamps are dropped rather than exported.

**Discarding, not keeping, an unreplaceable stale stamp** follows `Tas.Modify.UpdateIds`'s own rule: it
clears every stamp before re-resolving, so a failed resolution leaves the space unstamped. Absent beats
wrong - every consumer already handles and reports absent (`Query.ResolvedZone` falls back to the name,
`SimulationSpaceKey` reads null, the DomOv exporter falls back to `space.Guid` and the note says so),
whereas a stamp naming a zone in a discarded `.tbd` looks exactly like a good one.

### Round 2, P2 - a completed run whose results have gone is dropped, not just refused

`IsAssessable` refused the click but left `State` at `WorkflowCompleted`, so `RefreshPartOButtons`
re-enabled the button with the success tooltip as soon as the dialog closed - offering a click known to
fail, indefinitely.

`IsAssessable`'s two results checks now `Invalidate` the run with their own reason. The state check does
**not**: a `Prepared` run is live and waiting for its simulation, and `None` has already been explained.
So one click gives a dialog, a disabled button, and the exact reason in the tooltip.

`CanAssess` stays a pure state read - a property the ribbon evaluates on every refresh must not touch the
filesystem, and must not drop a run as a side effect of being looked at. The command reads `IsAssessable`,
which is the real gate. The window between the file changing and the next click is unavoidable without
polling, and the command re-checks at click time by design.

## Decisions / assumptions
- `PartORun` holds model + scenarios directly rather than a `PartOIterationPreparation`, because that
  type's setters are `internal` to `SAM.Analytical` and so could not be constructed by a test. The
  `Prepare(PartOIterationPreparation)` overload remains as the production entry point.
- Rows are keyed on the `AirHandlingUnit`, not paired positionally with
  `PartOIterationPreparation.VentilationUnitSelections`, which is documented as **not** item-for-item with
  `AirHandlingUnits`. Headroom is stated exactly as `VentilationUnitSelection.SupplyHeadroom_Lps` defines
  it, over the two values already on the row.
- `RibbonButton_CreateTBD_Click` (line ~2076) still calls the parameterless `Simulate()`. Its ribbon
  button is commented out in XAML, so it is unreachable; if re-enabled, its model replacement would
  correctly drop a pending run. Left alone deliberately.
- A note is emitted for a workflow-path space the workflow itself could not resolve. Harmless (the name
  fallback cannot invent a stamp there, because TBD space names come from zone names), and informative.

## Files changed
Modified:
- `WPF/SAM.Analytical.UI.WPF/Modify/Simulate.cs`
- `WPF/SAM.Analytical.UI.WPF/SAM.Analytical.UI.WPF.csproj` (+`SAM.Analytical.Systems`)
- `WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml`
- `WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml.cs`
- `WPF/SAM.Analytical.UI.WPF.Tests/SAM.Analytical.UI.WPF.Tests.csproj` (+`SAM.Analytical.Tas`)

Added:
- `SAM_UI/SAM.Analytical.UI/Enums/PartORunState.cs`
- `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartORun.cs`
- `SAM_UI/SAM.Analytical.UI/Classes/PartO/PartOVentilationStrategyOption.cs`
- `WPF/SAM.Analytical.UI.WPF/Enums/VentilationUnitCatalogueState.cs`
- `WPF/SAM.Analytical.UI.WPF/Classes/PartO/VentilationUnitCatalogue.cs`
- `WPF/SAM.Analytical.UI.WPF/Classes/PartO/PartOEquipmentRow.cs`
- `WPF/SAM.Analytical.UI.WPF/Classes/PartO/PartOSpaceRow.cs`
- `WPF/SAM.Analytical.UI.WPF/Modify/RestampSimulationZoneIdentity.cs`
- `WPF/SAM.Analytical.UI.WPF/Query/IsPartOFullYearSimulation.cs`
- `WPF/SAM.Analytical.UI.WPF/Modify/PreparePartOIteration.cs`
- `WPF/SAM.Analytical.UI.WPF/Modify/AssessPartOTM59.cs`
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOIterationWindow.xaml{,.cs}`
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOPreparationWindow.xaml{,.cs}`
- `WPF/SAM.Analytical.UI.WPF/Windows/PartOTM59ResultWindow.xaml{,.cs}`
- `WPF/SAM.Analytical.UI.WPF.Tests/SimulationZoneIdentityTests.cs` (11)
- `WPF/SAM.Analytical.UI.WPF.Tests/PartORunLineageTests.cs` (24)
- `WPF/SAM.Analytical.UI.WPF.Tests/PartOPresentationTests.cs` (12)

## Validation
- `dotnet build SAM_UI.sln -c Debug` - succeeded, 0 errors (the pre-existing MSB3245/MSB3270 warnings for
  `System.Data.DataSetExtensions`, `Microsoft.CSharp`, `PresentationFramework.Aero2` and the Interop
  architecture mismatch are unchanged).
- `dotnet test WPF/SAM.Analytical.UI.WPF.Tests` - **Passed 234, Failed 0, Skipped 0** (187 baseline + 47).
- BLOCKER 1's regression was written first and confirmed red (5 x CS0117, missing seam) before the fix.
- Two genuine defects in this work were caught by its own tests and fixed:
  1. `PartOIterationWindow` reported unmarked zones as out of scope even where `PartFDwellingZones` had
     put them **in** scope (the legacy all-unmarked model). Out-of-scope is now derived by difference from
     the policy's own return value instead of by a second reading of `IsDwelling`.
  2. The `NoneSelectable` test fixture was itself invalid (`VentilationUnitTemplate.IsValid` requires a
     `Source`); the reader had correctly returned `Unavailable`. Fixture fixed, production code was right.
- Authority-leakage sweep over the new files: no call to `SelectSmallestCapableVentilationUnit`,
  `CapableVentilationUnits` or `Modify.SelectVentilationUnit`; no TM59 criterion, limit or
  `MaxExceedableHours` arithmetic. Re-run after the 2026-09-01 correction - still clean; the only hit is
  a doc comment in `VentilationUnitCatalogue` saying why the selection rule is *not* called here.

### Licensed UI acceptance (2026-09-01) - PASSED

Run on the acceptance fixture
`OneDrive - Tetra Tech, Inc/Documents/SAM_daily/2026-07-15 PartO/SAM_zoningAM.sam` (9 spaces, 4 zones,
Flat 1/2/3 `IsDwelling = true`, `Corridor` false), weather `CIBSE Weather 2021.twd`. Three full-year
licensed runs of the same chain, all three PASS and agreeing on every assessed number:

| Run | Output | Sizing | "Domestic Overheating" |
|---|---|---|---|
| A | `C:\TasOut\pui` | on | off |
| B | `C:\TasOut\pui2` | on | on |
| **C (headline)** | `C:\TasOut\pui3` | **off** | on |

Run C is the one to quote: the standing instruction for Part O licensed runs is `Sizing = false`, since the
assessment reads free-running hourly temperatures. On this free-running fixture it made no difference - all
three runs report the same eight exceedance counts (13 / 4 / 3 / 4 / 0 / 2 / 4 / 0 hours against limits
262 / 262 / 262 / 142 / 262 / 262 / 142 / 262) and the same
`TM59 OCCUPIED-SPACE ASSESSMENT: PASS` - which is itself worth recording, since these are internal
conditions with no cooling setpoint.

**How it was driven.** A console harness (`C:\TasOut\poui`, not part of the repository) hosts a WPF
`Application`, calls the production commands - `Modify.AddVentilationByPartF`,
`Modify.PreparePartOIteration`, `Modify.Simulate(uIAnalyticalModel, partORun)`, `Modify.AssessPartOTM59`
- and completes each real dialog from a `Window.Loaded` class handler instead of by mouse, reading the
values straight off the windows' own controls. The one piece of `AnalyticalWindow` wiring it reproduces
is that window's single-line handler `partORun.NotifyModified()` on `UIAnalyticalModel.Modified`. **Not**
covered by this run: the ribbon controls themselves (their enabled state and tooltip are the expression
`RefreshPartOButtons` evaluates over `PartORun.CanAssess` / `State` / `InvalidationReason`, which the
harness logs, and `PartORunLineageTests` covers).

| Check | Result |
|---|---|
| Dwelling scope | `3 dwelling zone(s) in scope`; `'Corridor' (marked not a dwelling)` named as outside it |
| Catalogue | `1 selectable ventilation unit product(s) available`, offered and ticked |
| Flat 1 design duty (`MVHR-01`/`MVHR 1`) | **30.0 / 30.0 l/s** |
| Flat 2 design duty (`MVHR-02`/`MVHR 2`) | **63.0 / 63.0 l/s** |
| Flat 3 design duty (`MVHR-03`/`MVHR 3`) | **63.0 / 63.0 l/s** |
| Selected product maximum | **150.0 / 150.0 l/s**, in its own `Maximum supply`/`Maximum extract` columns, headroom 120/120 and 87/87 |
| Equipment capacity overwriting design airflow | None. Design columns are 30/63/63 with the same 150/150 product on all three |
| TAS workflow | Licensed full year, days 1-365, `Model successfuly converted`, 1min18sec |
| Model assessed | `partORun.AnalyticalModel_Assessment`, the workflow output - the result window states it, and `PartORun` has no other source for it |
| Zone identity on the assessed model | 9 of 9 spaces stamped, 0 unstamped |
| TM59 assessment | `Assessed 9 space(s)`, 8 mechanical results, **PASS** |

**The eight mechanical results are correct for the UI's dwelling scope, and are not the nine the earlier
Grasshopper/harness acceptance reported.** `Corridor_1` is in the `Corridor` zone, which
`Query.PartFDwellingZones` puts outside the Part O dwelling scope, so no `OverheatingScenario` covers it
and the report says so by name under `SPACES NOT ASSESSED`. The SAM-repo acceptance drove
`PreparePartOIteration` over a scope that included it. Eight assessed dwelling spaces + one reported
common space = the nine the model has.

**Simulation-space mapping: no identity-lineage refusal.** Every one of the nine design spaces resolved.
The only `SimulationSpaceMap` refusals are the three MVHR **plant** zones (`MVHR-01/02/03`), which TAS
carries as zones and which have no design space at all - "does not resolve to exactly one design space",
correctly left out rather than name-matched. `Corridor_1` mapped and was refused for the separate reason
that nothing states its ventilation strategy.

Cross-check against the SAM-repo licensed acceptance of the same fixture: annual occupied hours
8,760 for the residential conditions and 4,745 for the kitchens, limits 262 and 142. Identical.

**The restamp seam, measured on the licensed runs.** Runs B and C had "Domestic Overheating" ticked, so
both went through `Modify.RestampSimulationZoneIdentity` on the workflow output. Both produced **no**
zone-identity note in the completion dialog: the seam is a complete no-op on the workflow path, because
`Modify.UpdateIds` had already stamped all nine spaces. And the DomOv XML each wrote carries nine
`DomOverheatZoneItem/GUID` values that are **byte-for-byte the nine TAS `ZoneGuid` stamps** on the
workflow model - checked pairwise, 9/9 match in run C
(`C:\TasOut\pui3\Report XMLs\PartOUI3DomOv.xml`). That is the identity the external TAS tool needs, and
the thing the fill exists to preserve on the Simulate-unticked path.

Those runs also settle the "differ by construction" point empirically: the same fixture, exported three
times, produced three entirely different sets of TAS zone guids (`Studio 1_0` was `{5F14C5BC-...}`,
`{B65C5A8D-...}`, `{1722CE03-...}`) while the SAM `space.Guid` values are stored in the `.sam` and did not
change. A SAM space guid therefore cannot be the TAS zone identity, and the exporter's silent fallback to
it cannot be harmless. The same observation is a second proof that the assessed model is the workflow
output: the stamps on it are freshly minted per run, which neither the loaded model nor the preparation
output could carry.

**Stale state - PASSED.** Prepare (`State=Prepared`, assessment disabled with "A Part O iteration is
prepared but not simulated"), then an unrelated space rename adopted through `SetJSAMObject` with no
`ExpectModification`: the run drops to `State=None` with

> The model changed after the Part O iteration was prepared, so the preparation and its overheating
> scenarios no longer describe it. Prepare the iteration again before simulating.

which is what the ribbon tooltip shows, and the production `Modify.AssessPartOTM59` then refuses with the
same sentence in its own message box. The stale run is not silently used. The reason is readable while the
button is unavailable because `RibbonButton_AssessPartOTM59` declares
`ToolTipService.ShowOnDisabled="True"`.

## Issues / blockers
- **Not empirically reproduced:** the duplicate-name collapse (defect 1) is confirmed by inspection
  (`List.Find` first-match + unconditional write); the old code was replaced rather than characterised in
  a test, and deliberately not re-created in one. The re-spelling half (defect 2) *is* demonstrated - a
  braced upper-case guid now survives verbatim, which the `out Guid` path could not have produced - and
  the *consequence* of having no stamp at all is now demonstrated end to end through the exporter
  (`AnUnstampedSpace_ExportsTheSAMSpaceGuid_UntilTheFillGivesItTheTasIdentity`).
- **The ribbon controls themselves are not exercised by the licensed run.** `AnalyticalWindow` was not
  instantiated; the enabled state and tooltip are the expression `RefreshPartOButtons` evaluates, which
  the harness logs from the same three `PartORun` reads, and `PartORunLineageTests` covers the run states
  behind it. Clicking the two ribbon buttons by hand once is still worth doing before release.
- Out of scope by instruction and not started: Seam 2 targeted design-airflow editing, Iteration 3,
  generic `AirHandlingUnitTemplate`, common-space `UV`, production ventilation-vocabulary normalisation.
- Still open elsewhere (not this repo): `Tas.TSDQueryTM59Results`'s `_analyticalModel` input is documented
  only as "SAM Analytical Model" - the workflow-model requirement is not stated at the seam. A one-line
  description fix in `SAM_Tas_Grasshopper`, deliberately not bundled here.

## Next step
1. Review PR #76 - the GitHub diff, both P1 resolutions and CI - then merge. Nothing is pending in this
   working tree.
2. Before release, click the two new ribbon buttons by hand once in the real application - the licensed
   runs drove the commands and their windows, not the ribbon.
