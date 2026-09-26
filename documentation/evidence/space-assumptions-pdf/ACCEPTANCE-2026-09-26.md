# Space Assumptions PDF: acceptance (PR3, 26 Sep 2026)

Base: SAM `sow/2026-Q3` @ `ba343bfb` (SAM#141 merged). SAM_UI branched from `sow/2026-Q3` @ `d7f042f7`.
Siblings were rebuilt at their merged heads: SAM_Systems `2213373`, SAM_Tas `b32c0808`.

## 1. Focused automated tests (`SpaceAssumptionsPdfTests`, 27 cases)
Selection:
- exactly one Space;
- none, or a null selection;
- two Spaces are refused, not reduced to the first;
- a duplicate selection counts as one Space;
- a stale selection resolves to the current Space, and a removed Space is refused;
- no model.

Context-menu item: enabled for one Space; disabled for two, with the tooltip shown on the disabled item.

File name:
- the normal case;
- "1.01 Office" is kept whole (no number parsing);
- forbidden characters, trimming and reserved names;
- the Guid fallback for a null, empty, blank or "???" name;
- length cap.

Generation:
- a real PdfRenderer writes a non-empty `%PDF`: one A4 page (595 × 842 pt), no `.tmp` left;
- the process font resolver is `NotoSansFontResolver.Instance`, and a second render reuses it.

Sparse Space (no internal condition, geometry, flows or loads): succeeds, and its notes are carried.

Units, checked with a capturing renderer:
- SI gives m² and no ft²; IP gives ft² and no m²;
- the default is SI, with l/s and no cfm.

Failures:
- a throwing renderer gives a **Rendering** failure; the exception and message are kept, and an existing file is
  untouched;
- a missing folder gives an **Output** failure;
- a destination locked with `FileShare.None` gives an **Output** failure; the original is untouched and no `.tmp` is
  left;
- with the overwrite confirmed, an existing file is replaced.

Result: **27/27**. The full `SAM.Analytical.UI.WPF.Tests` suite gives **1235/1235**.

## 2. Production seam (headless, installed payload)
A scratch console harness, kept outside git, references nothing it ships (`Private=false`). It resolves every SAM,
PDFsharp, MigraDoc and Microsoft.Extensions assembly **only** from `%APPDATA%\SAM`, the installer payload that the
SAM_UI build xcopies. It calls `Modify.WriteSpaceAssumptionsPdf`.

Real Part O project (9 Spaces):
- Studio 1_0 (SI), Corridor_1 (SI), Bathroom_2 (SI) and Studio 1_0 (IP) were all written, **1 page** each;
- the first call took 486 ms, later calls 16–33 ms.

Typical project (1,608 Spaces): Cell 3 and Cell 9, 1 page each; 205 ms, then 17 ms.

Loaded from the payload:
- PdfSharp, PdfSharp.Shared, PdfSharp.System, MigraDoc.DocumentObjectModel and MigraDoc.Rendering, all 6.2.0.0;
- Microsoft.Extensions.Logging.Abstractions 8.0.0.0;
- SAM.Core.Reporting(.Pdf) and SAM.Analytical.Reporting.

The font resolver was `SAM.Core.Reporting.Pdf.NotoSansFontResolver`.

**Negative control:** the payload was copied without PdfSharp*/MigraDoc*. The same call returned a controlled
**Rendering** failure ("Could not load file or assembly 'PdfSharp, Version=6.2.0.0…'") and wrote no file. So the seam
does catch a missing dependency.

## 3. Native UI acceptance (UI Automation driving the real WPF app)
The real `%APPDATA%\SAM\SAM Analytical.exe` (install-style directory) was launched with `/Path=` on a copy of the real
Part O model. It was driven from Windows PowerShell 5.1 with `System.Windows.Automation` plus real mouse and keyboard
input (`SetCursorPos`, `mouse_event`, `SendKeys`). This is UI Automation of the real GUI, not a headless test.

| # | Step | Observed |
|---|---|---|
| A | Edit › Reports › **Space Assumptions PDF**, nothing selected | Info box "Select one Space, then choose Space Assumptions PDF." No dialog. (02) |
| B | Tree: right-click Studio 1_0 › Space Assumptions PDF › save | Default name `Studio 1_0 - Space Assumptions.pdf`, starting in the model folder, PDF filter (03). Result: "Space Assumptions PDF saved: … Open it now?" > No. File 56,210 bytes. |
| C | Tree: Studio 1_0 + Ctrl-click Kitchen_4, right-click | The item is present but **disabled**; tooltip "…one Space at a time: select a single Space." (04) |
| D | Corridor_1 › command › Save dialog **Esc** | No message, no file. |
| E | Sparse Corridor_1 › save | Saved (56,038 bytes) with no error. The seam run of the same Space reports 2 expected-missing-data diagnostics, printed in the PDF, not raised in the UI. |
| F | Studio 1_0 › save over a file held open with `FileShare.None` › Windows' overwrite confirmation › Yes | Error: "The PDF could not be saved to '…': Access to the path is denied. Choose another folder, or close the file if it is open in a PDF viewer." Original SHA-256 unchanged, no `.tmp`. (05) |
| G | Tree › **Select** Kitchen_4 (view selection) › ribbon command › save | Saved. The ribbon uses the active view's selection. |
| H | 2D plan: right-click Bedroom 2_3 › **Space Assumptions PDF** › save › **Yes** | Saved (56,385 bytes) and opened in the default PDF viewer (07). |

The legacy **Print RDS** button was present and enabled throughout (01). An unwritable folder was not driven through
the GUI: the Windows Save dialog refuses such folders itself. The seam test covers the path.

## 4. PDF inspection
The PDF written by the real app in step H (07) was inspected at 100 % and fit-page:
- A4 portrait, **1 / 1 page**;
- SAM mark and Noto Sans; the header band has the title, subject and identity line (Level, Internal condition);
- sections: geometry, design criteria, internal condition, ventilation, systems, fabric and sizing;
- footer: model, SAM version, generation time and Space Guid; legend "— not available"; "Page 1 / 1";
- SI units, air flow in l/s;
- no clipping, and nothing outside its column.

The values are the model's own. For example, this Part O model's −50 / 150 °C set points and 0 W design loads are
printed as supplied.

The committed PDFs were written through the UI: `Studio 1_0 - Space Assumptions.pdf` (B) and the sparse
`Corridor_1 - Space Assumptions.pdf` (E). The PDFs and the driver log are in `C:\TasOut\reporting-pr3-2026-09-26`,
which is outside git.
