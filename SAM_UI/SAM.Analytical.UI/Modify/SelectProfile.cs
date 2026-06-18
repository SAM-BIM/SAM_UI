// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    public static partial class Modify
    {
        /// <summary>
        /// WPF replacement for SAM.Analytical.Windows.Modify.SelectProfile: opens the profile library
        /// browser locked to the given type, mutates <paramref name="profileLibrary"/> in place with
        /// the edited profiles and returns the selected profile.
        /// </summary>
        public static Profile SelectProfile(this ProfileLibrary profileLibrary, ProfileType profileType, Window owner = null)
        {
            return SelectProfile(profileLibrary, (Enum)profileType, owner);
        }

        public static Profile SelectProfile(this ProfileLibrary profileLibrary, ProfileGroup profileGroup, Window owner = null)
        {
            return SelectProfile(profileLibrary, (Enum)profileGroup, owner);
        }

        private static Profile SelectProfile(this ProfileLibrary profileLibrary, Enum type, Window owner)
        {
            if (profileLibrary == null)
            {
                return null;
            }

            ProfileLibraryWindow profileLibraryWindow = new ProfileLibraryWindow(profileLibrary) { Type = type, TypeEnabled = false };
            if (owner != null)
            {
                profileLibraryWindow.Owner = owner;
            }

            if (profileLibraryWindow.ShowDialog() != true)
            {
                return null;
            }

            profileLibrary.RemoveAll();
            profileLibraryWindow.ProfileLibrary?.GetProfiles()?.ForEach(x => profileLibrary.Add(x));

            return profileLibraryWindow.GetProfiles(true)?.FirstOrDefault();
        }
    }
}
