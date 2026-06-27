// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void EditProfile(this UIAnalyticalModel uIAnalyticalModel, Profile profile, System.Windows.Forms.IWin32Window owner = null)
        {
            if (uIAnalyticalModel?.JSAMObject == null)
            {
                return;
            }

            ProfileLibrary profileLibrary = uIAnalyticalModel.JSAMObject.ProfileLibrary;

            ProfileWindow profileWindow = new ProfileWindow(profile) { ProfileLibrary = profileLibrary };

            // Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(profileWindow).Owner = owner.Handle;
            }

            if (profileWindow.ShowDialog() != true)
            {
                return;
            }

            profileLibrary = profileWindow.ProfileLibrary;
            profile = profileWindow.Profile;
            profileLibrary?.Add(profile);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(uIAnalyticalModel.JSAMObject, uIAnalyticalModel.JSAMObject.AdjacencyCluster, uIAnalyticalModel.JSAMObject.MaterialLibrary, profileLibrary);
        }
    }
}
