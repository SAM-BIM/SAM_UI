# Keyboard shortcuts & mouse gestures

Living reference for every key binding and viewport gesture in SAM_UI, with a
comparison to Revit and Rhino conventions. The stated design intent is to follow
**Revit** (Hide / Isolate / Unhide etc.).

> Surfaces referenced below:
> - **Helix 3D** — the legacy `HelixToolkit.Wpf` 3D viewport (`ViewportControl` → `helixViewport3D`).
> - **SharpDX 3D** — the `HelixToolkit.Wpf.SharpDX` 3D viewport behind `SAM_UI_VIEWPORT_SHARPDX` (`SharpDXViewportControl`).
> - **FloorPlan 2D** — the 2D canvas behind `SAM_UI_FLOORPLAN_2D` (`FloorPlan2DControl`), on by default.

---

## 1. Application keyboard shortcuts

Handled globally in `AnalyticalWindow.Window_KeyDown`
([AnalyticalWindow.xaml.cs:3888](../WPF/SAM.Analytical.UI.WPF/Windows/AnalyticalWindow.xaml.cs#L3888)).
They apply to the active window regardless of which viewport surface is showing,
and act on the current selection.

| Key | Action | Method |
| --- | --- | --- |
| `I` | Isolate selected | `Isolate()` |
| `H` | Hide selected | `Hide()` |
| `U` | Unhide All (reveal hidden) | `RevealHidden()` |
| `Delete` | Delete selected | `Delete()` |
| `R` | Reverse geometry direction | `Reverse()` |
| `P` | Show properties of selected | `ShowProperties()` |
| `V` | Edit view settings | `EditViewSettings()` |
| `G` | Select by GUID | `SelectByGuid()` |
| `F` | Select by filter | `SelectByFilter()` |
| `F12` | Show JSON of selection | — |
| `Ctrl+S` | Save As | `SaveAs()` |
| `Esc` | Clear selection | per-viewport handler |

> ⚠️ These are **single-key, global** handlers fired from `Window_KeyDown`. They
> are not gated on input focus, so they can fire while a text field has focus.
> See [§4](#4-observations) for the Revit two-letter alternative.

---

## 2. Viewport mouse gestures

Gestures differ per surface. The table is the current (as-shipped) mapping.

| Action | Helix 3D | SharpDX 3D | FloorPlan 2D |
| --- | --- | --- | --- |
| **Rotate / orbit** | Shift + Right-drag | Right-drag *(stock default)* | n/a (2D) |
| **Pan** | Shift + Left-drag | Middle-drag, Shift + Left-drag | Middle-drag, Shift + Left-drag |
| **Zoom** | Wheel | Wheel (around cursor) | Wheel (around cursor) |
| **Select** | Left-click, Ctrl+click toggles | Left-click, Ctrl+click toggles | Left-click, Ctrl+click toggles |
| **Window select** | L→R = inside, R→L = crossing | same | same |
| **Context menu** | Right-click | Right-click | Right-click |
| **Double-click** | open / `ObjectDoubleClicked` | open / `ObjectDoubleClicked` | open / `ObjectDoubleClicked` |

References:
- Helix pan / rotate: [ViewportControl.xaml.cs:90-91](../WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs#L90)
- SharpDX pan (middle + Shift+Left): [SharpDXViewportControl.cs](../WPF/SAM.Geometry.UI.WPF/Controls/SharpDXViewportControl.cs) constructor `InputBindings`
- FloorPlan 2D pan: [FloorPlan2DControl.cs:503](../WPF/SAM.Geometry.UI.WPF/Controls/FloorPlan2DControl.cs#L503)
- Rectangle selector (5 px threshold, inside vs crossing): [RectangularSelector.cs](../WPF/SAM.Geometry.UI.WPF/Classes/RectangularSelector.cs)

### Zoom Extents / Zoom Selected
Currently available via the **right-click context menu** only (no key binding):
- *Zoom Extents* — [ViewportControl.xaml.cs:210](../WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs#L210)
- *Zoom Selected* — shown only when something is selected, [ViewportControl.xaml.cs:1201](../WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs#L1201)

On the SharpDX path both are framed by `FrameCamera` (fits the bounding sphere,
keeps world Z up) rather than the built-in `Viewport3DX.ZoomExtents`.

> ⚠️ **Known inconsistencies between surfaces:**
> 1. Helix orbits with **Shift+Right**, but SharpDX orbits with plain **Right** — they do not match each other.
> 2. Pan is **Middle** on SharpDX / 2D but **Shift+Left** on Helix.
> 3. `U` is removed from the SharpDX `Viewport3DX` default key bindings so the app's Unhide-All keeps the key (see `RemoveConflictingKeyBindings`).

---

## 3. Comparison to Revit and Rhino

### Visibility (the alignment target)

| Action | **SAM** | **Revit** | **Rhino** |
| --- | --- | --- | --- |
| Hide element | `H` | `HH` (or `EH`) | `Hide` command |
| Isolate element | `I` | `HI` | `Isolate` / `IsolateSel` |
| Unhide / reset | `U` | `HR` (Reset Temporary Hide/Isolate) | `Show` (show all) |
| Hide category | — | `HC` | — |
| Isolate category | — | `IC` | — |

### Common editing / view

| Action | **SAM** | **Revit** | **Rhino** |
| --- | --- | --- | --- |
| Delete | `Delete` | `Delete` / `DE` | `Delete` |
| Properties | `P` | `PP` (toggle palette) | `Properties` / `F3` |
| Zoom Extents | context menu | `ZE` / `ZF` (fit) | `ZE` / middle double-click |
| Zoom Selected | context menu | (zoom to selection) | `ZS` (Zoom Selected) |
| Move / Rotate | toolbar | `MV` / `RO` | `M` / `RO` |
| Visibility / view settings | `V` | `VG` / `VV` | — |
| Clear selection | `Esc` | `Esc` | `Esc` |

### Navigation conventions (largest divergence)

| Action | **SAM 3D** | **Revit 3D** | **Rhino (perspective)** |
| --- | --- | --- | --- |
| Orbit / rotate | Right (SharpDX) / Shift+Right (Helix) | **Shift + Middle** | **Right-drag** |
| Pan | Middle / Shift+Left | **Middle** | **Shift + Right** |
| Zoom | Wheel | Wheel | Wheel / Ctrl + Right |

*Revit and Rhino default values above are the common out-of-the-box conventions;
both apps let users remap. They are listed as targets for comparison, not as
exact authoritative tables.*

---

## 4. Observations

The intent to follow Revit is sound; the current gaps are:

1. **Single-key vs two-key.** SAM uses `H` / `I` / `U`; Revit uses `HH` / `HI` /
   `HR`. Single keys are faster but (a) don't match Revit muscle memory and
   (b) can fire while typing because `Window_KeyDown` is global and not gated on
   focus. Revit's two-letter scheme avoids both.
2. **Unhide mapping.** SAM `U` ↔ Revit `HR`. Keep `U` or add `HR` for parity.
3. **Orbit gesture.** Pan now matches Revit (Middle). The remaining mismatch is
   orbit: Revit uses **Shift+Middle**, SAM uses Right / Shift+Right. Aligning
   the SharpDX orbit to **Shift+Middle** would both match Revit and unify the
   Helix vs SharpDX behaviour.
4. **No key binding for Zoom Extents / Zoom Selected** — Revit's `ZE` / `ZF`
   equivalents would be a natural addition.

### A possible Revit-aligned scheme (proposal, not yet implemented)

| Action | Proposed | Notes |
| --- | --- | --- |
| Hide | `HH` | keep `H` as an alias during transition |
| Isolate | `HI` | keep `I` as an alias |
| Unhide All | `HR` | keep `U` as an alias |
| Zoom Extents | `ZE` / `ZF` | currently context-menu only |
| Zoom Selected | `ZS` | currently context-menu only |
| Orbit (3D) | Shift + Middle-drag | unify Helix + SharpDX, match Revit |

Discuss and agree the target scheme before changing bindings — see the related
tracking issue.
