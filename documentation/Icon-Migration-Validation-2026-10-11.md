# Icon migration validation — 2026-10-11

Base: `sow/2026-Q4`. Scope: SAM-BIM only. Reference: [SAM#179](https://github.com/SAM-BIM/SAM/pull/179); [SAM#166](https://github.com/SAM-BIM/SAM/pull/166) is the closed historical design source.

## Changes and decisions

- Grasshopper manifest coverage: 32 concrete objects. GUIDs, APIs, namespaces, component metadata and behavior are preserved.
- Reuse the frozen SAM glyph/palette/badge grammar and frozen SAM-BIM extension v1. Preserve plugin identity, external integration logos, severity symbols and historical attribution.
- Source comparison against current base: 117 icon resource token swaps; 0 other production C# changes, apart from standard SPDX headers and additive resource properties.
- Validated 214 manifest resource references; 0 errors.
- Adds WPF command coverage (85 assignments) and repaints 97 compatibility resources; existing 32 Grasshopper objects remain covered. Distinct operations now have distinct approved badges. See `design/ui-icons/manifest.json` and native-size review sheet.

## Fresh validation

- VS MSBuild Restore,Build, Release: **PASS**. Scratch APPDATA/USERPROFILE; current sibling SAM-BIM dependencies, OCCT native build skipped.
- Regression suites: SAM_UI-0.trx: Failed; passed 2766, failed 2, total 2768
- Re-parsed compiled component declarations: 32/32 manifest rows; 0 coverage/canonical 24x24 RGBA PNG errors.
- Built assembly resource presence and exact embedded PNG bytes checked.

## Follow-up validation

Initial full suite: 2766 passed, 2 failed (WPF packaging/progress-window concurrency and 5000-space stopwatch timing). Reran only the failed-case families in isolation, without changing code or tests: 4/4 passed in four seconds (failed-cases-rerun.trx). Both original failed cases passed; the remaining 2766 had passed the full run. The initial failures remain recorded rather than claiming the first run passed. SAM_Windows packaging code and performance algorithms are unchanged; production changes are limited to icons. No reproducible icon blocker remains. Hosted Rhino fixture stalled during scratch-profile startup and was stopped without a result; no fresh hosted smoke pass is claimed. Vendored hashes, exact manifest-listed UI token guard, GUID guard, resource wiring and embedded PNG byte checks pass.

Next step: verify final-head GitHub checks and merge only if green and safe under the owner authorization. No force-push or branch deletion. Source and embedded-resource checks are the completed icon verification.
