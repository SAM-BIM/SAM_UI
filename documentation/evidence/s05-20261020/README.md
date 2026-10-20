# S05 licensed acceptance evidence (2026-10-20)

Record: `documentation/S05-LicensedAcceptance.md`. Real SAM Analytical ribbon runs on a licensed laptop, on a fresh build of
`sow/2026-Q4` `ad1f2486` and the dependency tips in `build-revisions.txt`.

- `build-revisions.txt`, `build-hashes.txt`, `producers.txt`: what was built and run (`producers.txt` holds the `SAM_COMMIT`
  value used for the Both routes).
- `both-summary.txt`: every Both run's status, snapshot, weather, producer outcomes, reported provenance and canonical hashes.
- `cases/<case>/`: `driver.log`, the message-box / summary / diagnostics text, the run manifests (`box.tas-run.json`,
  `openstudio-run.json`, `engine-comparison-run.json`), B1a documents and `comparison.csv` for Both, `processes.txt`
  (cases 8-9), `concurrency.txt` and `refusals.txt` (case 9), `run-folder-*.txt` and `case-folder.txt` (listings), and a few
  screenshots. `c2-attempt*` and `c9-concurrent-start` are harness attempts kept as evidence (see the record).
- `tools/`: the S05 harness. The S01b drivers it calls are unchanged in `../s01b-20261019/tools/`.

Machine-local, not committed: `C:\TasOut\s05` (app, sandbox profile, full run folders with TBD/TSD/OSM/SQL/EPW, logs).
