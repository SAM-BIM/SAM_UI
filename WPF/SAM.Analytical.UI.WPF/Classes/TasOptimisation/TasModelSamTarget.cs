// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Where one "Apply best design" change lands in the open SAM model (PR9), found by <c>Query.TasModelSamTarget</c>:
    /// for a zone setpoint, the space whose name is the TBD internal condition's (SAM writes one TBD internal condition
    /// per space, named after it) and its heating or cooling profile; for a glazing choice, the aperture constructions
    /// whose TBD pane construction is the one the choice replaces.
    /// </summary>
    public sealed class TasModelSamTarget
    {
        internal TasModelSamTarget(Space space, ProfileType profileType, Profile profile, bool sharedProfile)
        {
            Space = space;
            ProfileType = profileType;
            Profile = profile;
            SharedProfile = sharedProfile;
            ApertureConstructions = new List<ApertureConstruction>();
        }

        internal TasModelSamTarget(IEnumerable<ApertureConstruction> apertureConstructions, int apertureCount)
        {
            ApertureConstructions = new List<ApertureConstruction>(apertureConstructions);
            ApertureCount = apertureCount;
        }

        /// <summary>The space of a setpoint; null for a glazing choice.</summary>
        public Space? Space { get; }

        /// <summary>Heating or Cooling for a setpoint.</summary>
        public ProfileType ProfileType { get; }

        /// <summary>The space's heating or cooling profile, as the model holds it.</summary>
        public Profile? Profile { get; }

        /// <summary>
        /// True when another space (or another profile type) uses the same profile: the space then gets its own copy, so
        /// only it changes, as only its TBD internal condition changed in the optimisation.
        /// </summary>
        public bool SharedProfile { get; }

        /// <summary>The aperture constructions of a glazing choice (each keeps its own frame).</summary>
        public IReadOnlyList<ApertureConstruction> ApertureConstructions { get; }

        /// <summary>How many apertures use them.</summary>
        public int ApertureCount { get; }
    }
}
