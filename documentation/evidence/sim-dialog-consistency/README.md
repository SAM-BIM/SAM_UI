# SAM_UI#246 visual acceptance evidence (2026-10-20)

Rendered from the isolated worktree build (Release, tested head = the PR's XAML commit) by a temporary,
uncommitted WPF test harness: each real window instance, fabricated SAM_Validation comparison documents,
no simulation. Captures are window content only (no title bar), at 100% (`-100`) and 150% (`-150`) scale.
Roboto Light is not installed on this machine, so all windows (Part O reference included) fall back to the
same system font.

- partO-100: Part O reference (Previous result found)
- run-both-100/150: Run Simulation, Both selected (longest explanation)
- run-disabled-150: Run Simulation with OpenStudio/Both disabled
- compare-model-1180-100: Compare Tas / OpenStudio, Model grid
- compare-spaces-760-150: Compare, Spaces grid at minimum width (horizontal scroll)
