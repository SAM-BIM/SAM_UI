# Part O 2B - per-round `.sam` growth (26 Sep 2026)

Investigation of the model growth seen in the Pass 5 live acceptance run, its root cause, and the fix.
Before: `C:\TasOut\parto-2b-journey-2026-09-26\run` (Pass 5 live run, SAM `3ec76eca`, SAM_Tas `39828c6`).
After: `C:\TasOut\parto-2b-sam-growth-2026-09-26\run` (same fresh model, same driver, same settings - 5 l/s, 10
rounds, envelope on - with the fixed SAM + SAM_Tas). Both folders are on the development machine only.

## What grows

Per-type count / compact JSON bytes in the saved round model (`scripts/cluster_counts.py`). Everything else in the
cluster, the libraries and the model parameters is flat.

| Object | base | Opt07 | Opt08 | Opt09 | Opt10 | OptMax | Pattern |
|---|--:|--:|--:|--:|--:|--:|---|
| `DesignDay` | 26 / 41 KB | 3,582 / 5.6 MB | 7,166 / 11.2 MB | 14,334 / 22.4 MB | 28,670 / 44.9 MB | 57,342 / 89.8 MB | **2n + 2 per run** |
| `ZoneSimulationResult` | 24 | 52 | 56 | 60 | 64 | 68 | +4 per run (one per zone) |
| Space / Zone / Panel / InternalCondition | 9 / 4 / 50 / 12 | same | same | same | same | same | stable, names unchanged |
| Space / Surface results | 18 / 440 | same | same | same | same | same | replaced each run |

- `OptNN.prepared.sam` = `Opt(N-1).sam` structurally (same counts; bytes differ by a few Guids/timestamps):
  preparation adds nothing. The whole `2n + 2` happens inside ONE TAS run, including the plain Iteration 2 run
  (prepared 12 -> simulated 26).
- The 12 design days the fresh model already carried were 8 **London** + 4 **CIBSE Z1**, left by earlier runs
  of that model on London weather, and duplicated ever since. The model's `Weather Data` parameter was Z1 only.
- Space names, internal-condition names and zone names do not grow or change across rounds.
- Growth is material from about round 6 (`.sam` +50%); at round 10 the uncompressed model is 25x the baseline.

## Root cause

1. **`2n` - SAM.Core deep clone.** `SAMObjectRelationCluster(cluster, deepClone: true)` shallow-copied the
   dictionaries and then re-added each clone through `AddObject`. A `DesignDay` is a `WeatherDay`, not a `SAMObject`:
   it has no Guid, so the cluster keys it by a generated Guid and can find it again only by `Equals`, which
   `WeatherDay` does not override. The clone therefore got a NEW key and sat BESIDE the original, which was also still
   shared with the source model. `RunPartOSimulation` takes exactly one deep copy per TAS run (its ownership copy,
   SAM `bdcfe5df`, 5 Sep 2026), hence one doubling per run.
2. **`+2` - SAM_Tas `WorkflowCalculator`** appended the run's cooling + heating design day to the cluster and never
   removed the previous ones, although `Modify.AddDesignDays` CLEARS the TBD's design days before writing the run's.
   This is also why the London records survived.
3. **`+4` - SAM_Tas `Modify.AddResults`** replaced space and surface results (same source and load type) but added
   a new `ZoneSimulationResult` per zone per run beside the old ones.

The growth is in SAM / SAM_Tas copy and record-keeping, not in SAM_UI's round orchestration: the 2B loop only
carries forward the model the previous run returned, which is correct.

## Engineering impact: none (classification 3 - performance/storage defect)

- Nothing sizes or simulates from the cluster's `DesignDay` records. `Query.DesignDays_Authoritative` reads the
  run's settings / weather / the model's `CoolingDesignDays`/`HeatingDesignDays` PARAMETERS (absent here), and SAM_UI
  passes the design days derived from the run's weather; `AddDesignDays` clears the TBD and writes only those. No
  Part O / TM59 / optimiser code reads `DesignDay` objects or `ZoneSimulationResult` (checked across SAM, SAM_Tas,
  SAM_UI, SAM_Windows; `Query.DesignHeatingLoad` and zone reports take a zone result but are not on the Part O path).
- Measured: all 12 TM59 reports (baseline, Opt01-Opt10, OptMax) are byte-identical before/after apart from the
  `Source:` path line; per-space airflow parameters are identical every round; same stop (round limit, 10 rounds),
  same kept design (run 10), same 8 spaces changed (6 targeted, 2 balancing), same Hub outcome.
- Latent risk removed: a mixed-weather design-day record in a saved model, and stale zone results with no marker of
  which is current, for any future reader.

## Scalability and performance

- Exponential in the number of runs a model has been through, independent of its size: `(n0 + 2) * 2^k`. Every
  Part O TAS run counts (Iteration 1a/1b/2, each 2B round, the envelope), and the records persist in saved models,
  so the growth continued across sessions. 20 runs from the fresh model would be ~15M design days (~24 GB JSON) -
  an out-of-memory failure, on any project. `ZoneSimulationResult` is linear: +zones per run (a 1,000-zone model
  would gain ~1,000 records, ~1 MB, per round).
- Measured on the 8-space model, workflow "Saving Model" step: 0.19 s (Opt01) -> 6.7 s (Opt10) -> 14.0 s (OptMax);
  round workflow total 12.6 s -> 26.5 s. SAM_UI's own deep copy, `.sam` write and preparation scale the same way (not
  timed separately). 2B wall time for the 10-round run: 243 s before (incl. envelope) -> 160 s after.

## Fix and result

- SAM (`fix/deepclone-guidless-objects-2026-09-26`): the deep clone stores each clone in the original's own slot
  (`RelationCluster.ReplaceObjects`), so relations keyed by that Guid resolve to the copy's instance; the
  throw-rather-than-share guard is kept.
- SAM_Tas (`fix/parto-replace-run-records-2026-09-26`): `Modify.ReplaceDesignDays` (the cluster records exactly
  the design days of the TBD it came back from) and `AddResults` replaces a zone's cooling result like the space
  results.
- SAM_UI: no production change; regression `PartORunModelGrowthTests` drives the real `RunPartOSimulation` round
  after round with TAS replaced at `PartOWorkflowRunner`.

Live, after the fix (`scripts/compare_runs.py`):

| Round | `.sam` before | `.sam` after | JSON before | JSON after | DesignDay b/a | ZoneResult b/a | TM59 same | airflows same |
|---|--:|--:|--:|--:|--:|--:|:-:|:-:|
| baseline | 170 KB | 166 KB | 1.89 MB | 1.83 MB | 26 / 2 | 24 / 4 | yes | yes |
| Opt01 | 172 KB | 166 KB | 1.94 MB | 1.83 MB | 54 / 2 | 28 / 4 | yes | yes |
| Opt02 | 176 KB | 166 KB | 2.04 MB | 1.83 MB | 110 / 2 | 32 / 4 | yes | yes |
| Opt03 | 184 KB | 166 KB | 2.22 MB | 1.83 MB | 222 / 2 | 36 / 4 | yes | yes |
| Opt04 | 197 KB | 166 KB | 2.57 MB | 1.83 MB | 446 / 2 | 40 / 4 | yes | yes |
| Opt05 | 225 KB | 166 KB | 3.28 MB | 1.83 MB | 894 / 2 | 44 / 4 | yes | yes |
| Opt06 | 279 KB | 166 KB | 4.69 MB | 1.83 MB | 1,790 / 2 | 48 / 4 | yes | yes |
| Opt07 | 387 KB | 166 KB | 7.51 MB | 1.83 MB | 3,582 / 2 | 52 / 4 | yes | yes |
| Opt08 | 602 KB | 166 KB | 13.14 MB | 1.83 MB | 7,166 / 2 | 56 / 4 | yes | yes |
| Opt09 | 1,032 KB | 166 KB | 24.39 MB | 1.83 MB | 14,334 / 2 | 60 / 4 | yes | yes |
| Opt10 | 1,886 KB | 166 KB | 46.90 MB | 1.83 MB | 28,670 / 2 | 64 / 4 | yes | yes |
| OptMax | 3,599 KB | 166 KB | 91.89 MB | 1.83 MB | 57,342 / 2 | 68 / 4 | yes | yes |

("TM59 same" ignores the report's `Source:` path line, the only difference.) The design days left are the Z1 pair
of the run's own weather; the London records are gone. Workflow "Saving Model" at OptMax: 14.0 s -> 0.13 s; round
workflow total flat at ~12.6 s. The saved kept model: 1.93 MB -> 170 KB.

The second 2B (driver cancels 15 s after Cancel is offered) now stopped in round 2 rather than round 1 - the rounds
are faster, so round 1 completed first. Same "Cancelled" outcome and kept-design rule.

Existing saved models keep their accumulated records until they are next simulated: the first run through the
fixed workflow replaces the design days and zone results (`Replacement_ClearsRecordsLeftByEarlierAccumulation`).
