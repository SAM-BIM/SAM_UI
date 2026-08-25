// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// Which Part F annotation on an object a manual position belongs to, so one object can carry more
    /// than one movable label without their positions colliding.
    /// </summary>
    public enum PartFAnnotationType
    {
        [Description("Undefined")] Undefined,

        /// <summary>The rate label beside a terminal marker, e.g. "SUP 63.0 l/s".</summary>
        [Description("Terminal")] Terminal,

        /// <summary>The transfer label beside a route, e.g. "TRA 8.0 l/s".</summary>
        [Description("Transfer")] Transfer,

        /// <summary>A space's net airflow label.</summary>
        [Description("Space Net Airflow")] SpaceNetAirflow,

        /// <summary>A door's free-area or undercut requirement label.</summary>
        [Description("Door Requirement")] DoorRequirement,
    }
}
