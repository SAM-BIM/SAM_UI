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
| `U` (or `UU`) | Unhide All (reveal hidden) | `RevealHidden()` |
| `Delete` | Delete selected | `Delete()` |
| `R` | Reverse geometry direction | `Reverse()` |
| `P` | Show properties of selected | `ShowProperties()` |
| `V` | Edit view settings | `EditViewSettings()` |
| `G` | Select by GUID | `SelectByGuid()` |
| `F` | Select by filter | `SelectByFilter()` |
| `F12` | Show JSON of selection | — |
| `Z` then `E` | Zoom extents | `ZoomExtents()` |
| `Z` then `S` | Zoom selected | `ZoomSelected()` |
| `Ctrl+S` | Save As | `SaveAs()` |
| `Esc` | Clear selection | per-viewport handler |

> `Z E` / `Z S` are two-letter chords (Rhino-style): press `Z`, then the second
> key within ~1 s. `Z` on its own does nothing. The single-letter actions
> (`H`/`I`/`U`/`R`…) still fire immediately; `U` and `UU` both Unhide All.
>
> This list is also available in the app via **Help → Shortcuts**
> (`KeyboardShortcutsWindow`). Keep the two in sync when bindings change.
>
> ⚠️ The single-key handlers fired from `Window_KeyDown` are **global** - not
> gated on input focus, so they can fire while a text field has focus.

---

## 2. Viewport mouse gestures

Gestures differ per surface. The table is the current (as-shipped) mapping.

| Action | Helix 3D | SharpDX 3D | FloorPlan 2D |
| --- | --- | --- | --- |
| **Rotate / orbit** | Shift + Right-drag | Right-drag (a plain right-*click* opens the menu) | n/a (2D) |
| **Pan** | Shift + Left-drag | Middle-drag, Shift + Left-drag | Middle-drag, Shift + Left-drag |
| **Zoom** | Wheel | Wheel (around cursor) | Wheel (around cursor) |
| **Select** | Left-click, Ctrl+click toggles | Left-click, Ctrl+click toggles | Left-click, Ctrl+click toggles |
| **Window select** | L→R = inside, R→L = crossing | same | same |
| **Context menu** | Right-click | Right-click | Right-click |
| **Double-click** | open / `ObjectDoubleClicked` | open / `ObjectDoubleClicked` | open / `ObjectDoubleClicked` |
| **Toggle projection** (perspective ⇄ orthographic) | n/a | `Ctrl+Shift+O`, or right-click → *Orthographic* | n/a (always orthographic) |

References:
- Helix pan / rotate: [ViewportControl.xaml.cs:90-91](../WPF/SAM.Geometry.UI.WPF/Controls/ViewportControl.xaml.cs#L90)
- SharpDX pan (middle + Shift+Left): [SharpDXViewportControl.cs](../WPF/SAM.Geometry.UI.WPF/Controls/SharpDXViewportControl.cs) constructor `InputBindings`
- FloorPlan 2D pan: [FloorPlan2DControl.cs:503](../WPF/SAM.Geometry.UI.WPF/Controls/FloorPlan2DControl.cs#L503)
- Rectangle selector (5 px threshold, inside vs crossing): [RectangularSelector.cs](../WPF/SAM.Geometry.UI.WPF/Classes/RectangularSelector.cs)

### Zoom Extents / Zoom Selected
Available via the `Z E` / `Z S` chords (above) and the **right-click context menu**:
- *Zoom Extents* — `ViewportControl.ZoomExtents()`, also the context-menu item
- *Zoom Selected* — `ZoomSelected()` (window) / context-menu item shown only when something is selected

On the SharpDX path both are framed by `FrameCamera` (fits the bounding sphere,
keeps world Z up) rather than the built-in `Viewport3DX.ZoomExtents`.

### Perspective ⇄ orthographic (SharpDX 3D)
`Ctrl+Shift+O` toggles the 3D camera between perspective and orthographic
projection (parity with the Helix `OrthographicToggleGesture` default). The same
toggle is on the right-click menu as a checkable *Orthographic* item, so it is
both discoverable and shows the current projection. The toggle preserves the view
framing; on very large models the projection switch re-renders the scene, so it is
not instant. The view always opens in perspective (projection is not persisted -
parity with the Helix path). See `SharpDXViewportControl.ToggleProjection`.

> On the SharpDX path, a right-*drag* orbits and a right-*click* (no drag) opens
> the context menu, so the menu no longer pops open at the end of every orbit
> (`SharpDXViewportControl_ContextMenuOpening` cancels the menu after a drag).
>
> ⚠️ **Remaining inconsistency between surfaces:**
> - Helix orbits with **Shift+Right**, SharpDX orbits with plain **Right-drag** — they still differ.
> - Pan is **Middle** on SharpDX / 2D but **Shift+Left** on Helix.
>
> All no-modifier `Viewport3DX` key bindings (the cube's F/B/L/R/U/D face-view keys)
> are stripped on the SharpDX path so the app's single-letter shortcuts always win
> (see `RemoveConflictingKeyBindings`); the clickable view cube still snaps to faces.

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

**Implemented (this round):**
- `Z E` Zoom Extents and `Z S` Zoom Selected chords (Rhino-style), `UU` accepted
  as an alias of `U` for Unhide All.
- SharpDX view-cube face-view key bindings stripped so the app's single letters
  always win; the clickable cube still snaps.
- SharpDX right-drag orbits, right-click opens the menu (menu no longer pops
  after an orbit).
- View cube reverted to the library default (Front/Back/Left/Right/Top/Bottom).

**Still open (discuss before changing):**
- **Single-key vs two-key.** SAM uses `H` / `I`; Revit uses `HH` / `HI`. Single
  keys are faster but can fire while typing because `Window_KeyDown` is global and
  not gated on focus. A full Revit two-letter scheme would avoid both.
- **Orbit gesture.** Revit orbits with **Shift+Middle**; SAM SharpDX uses
  **Right-drag** and Helix uses **Shift+Right**. Aligning SharpDX to Shift+Middle
  would match Revit and unify the two viewports.
