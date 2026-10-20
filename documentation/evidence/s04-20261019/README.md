# S04 stable canonical snapshot - real-producer evidence (2026-10-19)

Licensed development laptop. SAM_UI S04 content `0594e708`. Producers are developer builds: SAM_Tas `e84aeff6` and
SAM_OpenStudio `d4216f3`, with `SAM_COMMIT` set by hand (see S03, SAM_UI#242). The model is `box-aperture.json` and the
weather `Budapest.epw` (machine-local, `C:\TasOut\s01b\inputs`). The SAM_UI code paths were driven by
`tools/S04EvidenceHarness.cs.txt`, a temporary xUnit harness that is not compiled, and not by the ribbon.

- `real-producers.txt`: the output of the harness.
  - **E1:** two Both runs, each on the model opened afresh as the window opens it, with different view GUIDs. Both runs
    gave snapshot `0c30db14…`, and both real producers reported `canonicalModelHash` `05c1e334…`.
  - **E2:** the four historical S01b snapshots were frozen again under S04. They came from two app builds and had four
    pre-S04 canonical hashes. All four now give the same snapshot and hash as E1.
- `session-1/` and `session-2/`: each run's `engine-comparison-run.json` and both B1a documents.

Paths inside the files are machine-local.
