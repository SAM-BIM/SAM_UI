<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Native Optimisation PR5b: results formatting, raw tooltip, CSV trace, full-precision copy

Branch `feature/optimisation-results-formatting` → base `sow/2026-Q4` (cut from `96e20c9e`). Record date: 2026-10-09.

Plan of record: `documentation/NativeOptimisation-Plan-ModelBindings.md`. PR5b "can go at any point; it touches only the
results view". Earlier records keep the values at full precision on purpose and leave the formatting to this PR:
PR5a decision 11, UX PR1 "Numbers stay full precision". The formatter is SAM#186's `QuantityFormatter`
(`documentation/Units-EnergyMass-Formatting-PR.md` in SAM). **SAM_UI only.**

## Current status

PR open, **not merged**. Code and tests complete; awaiting PR CI and owner review.

## What changed (the Run & Results tab only)

| Where | Before | Now |
|---|---|---|
| Trace grid cells | full precision (`4.968943799848584`, `7360.04370117188`) | engineering text with the declared unit (`4.969`, `7,360 GBP`), right-aligned. The **tooltip** of each value is its full-precision value. |
| Copy from the grid | single row, displayed text | rows selectable (Extended). **Ctrl+C** copies the selected rows with the header row, tab-separated, **full precision** (each column's clipboard content is the raw value). |
| Copy trace (new button) | — | The whole trace to the clipboard, tab-separated, full precision, header row `Name [unit]`. Excel pastes it into cells. |
| Export trace (CSV)… (new button) | — | Save dialog. Default name `<definition name> - trace.csv`; it opens in the run folder when that exists. Nothing is stored in the definition. |
| Result card (Best design, Objective, Recorded outputs, Final interval) | full precision | Engineering text with units. The line's **tooltip** is the same line at full precision. |
| Progress lines (Last reported, Best so far) | full precision | Engineering text with units. |
| Copy summary | full precision | Unchanged: every value at full precision, never the rounded text. |
| Column headers, Setup tab, readiness list, outcome rules | — | Unchanged. |

### The formatting rule (`TasOptimisationFormatter`)

- **Units are declarations, never converted.** A value is shown in the unit the definition declares (canonical symbol:
  `degC` → `°C`, `£` → `GBP`, `kgCO2` → `kgCO2e`).
- **Declared unit = SAM's display unit for its category** → that display unit's decimals, through
  `QuantityFormatter.FormatNumber`:

  | Unit | Decimals |
  |---|---|
  | kWh, °C, K, kg (so kgCO2e), m², ° | 1 |
  | h, %, W | 0 |
  | m | 2 |

- **Anything else** → 4 significant figures (`QuantityFormatter.FormatSignificant`, SAM#186's engineering default):
  - no unit, currency (GBP, EUR, USD), "-";
  - a unit SAM shows differently (MWh, kW, tCO2e), which would otherwise need a conversion;
  - a unit SAM does not know.
  - Below 1e-3 the value is in scientific notation (`1.230E-4`).
- **Whole numbers:** a choice's option number (`"discrete"`, and the reserved `"integer"`) and every count (simulation
  numbers, retries: `TasOptimisationReport.Count`, unchanged).
- NaN and ±∞ show "—" without a unit.
- **Display culture:** the current UI culture (separators). Raw text, copy and CSV are **invariant** round-trip `"R"`
  text that reads back to the same double: `NaN`, `Infinity`, `-0`, `1E-07` included.

### CSV layout

Illustrative (the Systems Demo example; counters and values abridged):

```
Simulation,Main iteration,Sub-iteration,Event,Setpoint,Result [GBP] (objective),Cost [GBP],CO2
1,0,1,LineSearch,10.278640450004207,7392.29309082031,7392.29309082031,4615.20091247559
```

- RFC 4180: comma-separated, CRLF line ends, a field quoted when it holds a comma, quote or line break (quotes doubled).
- UTF-8 **with a byte order mark**, so Excel reads `°C` correctly.
- One row per kernel trace entry (`OptimisationResult.Entries`, the grid's rows). The variables come in coordinate
  order, then the outputs in the optimiser's order: the objective first, marked `(objective)`.
- The clipboard text (Copy trace) has the same header and fields, tab-separated.

### New values from the kernel (SAM#190)

`OptimisationEvent.OptionEvaluated` rows and `OptimisationAlgorithm.TryEveryOption` results format without throwing:
the event name is shown as it is, and the option number is a whole number. **No choice UI is built (PR8).**

## Owner decisions (please confirm or redirect)

1. **4 significant figures** for values without a SAM display unit (SAM#186's default).
   - Near the optimum, objectives that differ only in the 5th figure look equal (`7,360 GBP`); the tooltip, Copy trace
     and CSV give the full value.
   - Alternative: 6 figures for outputs.
2. **Currency** gets significant figures, not 2 decimals (SAM has no currency display unit).
3. **Units in the cells, not in the headers.** The headers stay the names, so the "Show search details" toggle and the
   existing tests keep working. The CSV and copied headers carry `Name [unit]`.
4. **CSV columns:** Simulation, Main iteration, Sub-iteration, Event, the variables, the outputs with the objective
   first and marked. The search details are always in the file (the grid hides them by default).
5. **Default file name** `<definition name> - trace.csv`; the dialog opens in the run folder.
6. **UTF-8 with a byte order mark** for the CSV (Excel and units).

## Files changed

- New: `WPF/SAM.Analytical.UI.WPF/Classes/TasOptimisation/TasOptimisationFormatter.cs` (`TasOptimisationFormatter`,
  `TasOptimisationColumn`).
- Changed (`WPF/SAM.Analytical.UI.WPF/`):
  - `Classes/TasOptimisation/TasOptimisationTraceRow.cs`: display and raw texts per cell;
  - `TasOptimisationCheck.cs`: optional `Raw`, `ToolTipText`;
  - `TasOptimisationReport.cs`: optional formatter; `ToText` keeps full precision;
  - `TasOptimisationProgressState.cs`: optional formatter;
  - `Windows/TasOptimisationWindow.xaml(.cs)`: value columns, grid copy mode, Copy trace, Export trace (CSV)…, the
    tooltip binding of the check template.
- Tests: new `WPF/SAM.Analytical.UI.WPF.Tests/TasOptimisationFormattingTests.cs`.
- This record.

## Validation

- Step 0 on this workstation: SAM `288c9f57`, SAM_Tas `4abad64b`, SAM_UI `96e20c9e`, SAM_Systems `5404926`, all
  `origin/sow/2026-Q4`, rebuilt in order. Release `SAM_UI.sln` built (scratch profile, real `NUGET_PACKAGES`, `-m:1`).
  The WPF and test projects were rebuilt after the change: 0 errors.
- `TasOptimisation*` tests: **165/165** (121 before, all unchanged and passing, plus 44 new):
  - **formatting:** per unit (11 SAM display units and 13 other cases incl. currency, MWh with no conversion,
    synonyms, unknown units, no unit, scientific notation, zero); NaN/∞; de-DE separators; canonical symbols and
    the objective-first order; whole numbers for a choice; counts stay invariant;
  - **raw text:** `4.968943799848584`, `0.30000000000000004`, `1E-300`, NaN, ∞ round-trip bit for bit under de-DE;
  - **CSV:** header with names, units and quoting (`"Overheating, worst ""room"" [h] (objective)"`); every value reads
    back bit for bit (NaN, `-0`, `1E-07`) with de-DE as the current culture; CRLF; one row per entry;
  - **copy:** tab-separated, full precision, header;
  - **try every option:** `OptionEvaluated` rows, NaN option, report and progress format without throwing;
  - **report:** engineering detail, raw tooltip, `ToText` full precision and equal to the report without a formatter;
  - **progress lines;** file names;
  - **window** (`[WpfFact]`, WPF collection, real stub run):
    - the value columns bind text / raw tooltip / raw clipboard content; copy mode and selection;
    - Copy trace text; the exported file's bytes (BOM, the formatter's CSV);
    - the result card's raw tooltip and the full-precision summary.
- Mutations (each applied, built, run against `TasOptimisation*`, reverted and touched): **8/8 caught**.
  - U1: raw text in the current culture: 5 failed.
  - U2: declared unit ignored: 15.
  - U3: a converted unit's decimals used (MWh as kWh): 4.
  - U4: CSV fields never quoted: 1.
  - U5: a choice shown with decimals: 3.
  - U6: the copied summary uses the rounded text: 2.
  - U7: copied grid cells are the rounded text: 1.
  - U8: export without a byte order mark: 1.
  - After the last revert the unmutated build passed again (165/165).
- `git diff --check` clean; SPDX headers on the new `.cs`.

## Not done

- No screenshots or licensed run: the change is display only, and the stub run covers the window. PR8's licensed
  acceptance will show it on the Systems Demo.

## Unresolved issues and risks

- Formatting depends on declared units. A definition that declares no unit gets significant figures, which is correct
  but less informative. PR7b's tas-model engine declares units for every kind.
- A `"%"` output is shown as a number with `%` and is never multiplied (the declared value is taken as a percentage).

## Next step

1. PR CI (`build`, `spdx`) green on the head.
2. **Owner review** (decisions above). Merge (merge commit, `--match-head-commit`) only after approval, then the
   `PROJECT_PROGRESS.md` closeout on SAM_UI `sow/2026-Q4`.
