// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// Which ventilation equipment behaviour an Iteration 3 run materialises Candidate B with - PR5A
    /// (SAM#111 plan §J).
    /// <para>
    /// <b><see cref="Parity"/> is the foundation control, and stays the default.</b> It is exactly Candidate
    /// B0: no unit settings are resolved, the topology template is the shipped <c>MV.json</c>, and the
    /// route's fan heat gain policy stays <c>ClearToZero</c>. Every existing caller of
    /// <c>Modify.RunPartOIteration3</c> that does not name a mode gets this, byte-for-byte unchanged from
    /// before PR5A existed.
    /// </para>
    /// <para>
    /// <b><see cref="SelectedProduct"/> resolves every scoped air handling unit's already-selected
    /// product</b> (Iteration 2's own selection authority; nothing here selects or reselects) to its
    /// certified heat-recovery efficiency and specific fan power, and materialises the same design with
    /// those values applied. It is a <i>paired</i> comparison, not a replacement: Reference A is unchanged
    /// and B0 remains separately available. A unit with no selection, an unresolvable reference, or
    /// missing certified data (E1/E2) refuses the whole run rather than silently falling back to B0 for
    /// that one unit.
    /// </para>
    /// </summary>
    public enum PartOIteration3BehaviourMode
    {
        /// <summary>The foundation control - Candidate B0, unchanged. The default.</summary>
        [Description("Parity (foundation control)")] Parity,

        /// <summary>Every scoped air handling unit's selected product, resolved and applied.</summary>
        [Description("Selected product")] SelectedProduct,
    }
}
