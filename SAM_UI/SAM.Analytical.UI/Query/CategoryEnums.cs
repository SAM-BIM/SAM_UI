// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Ported from SAM.Analytical.Windows.Query.CategoryEnums: the ProfileType + ProfileGroup
        /// values used to populate the profile-category picker.
        /// </summary>
        public static List<Enum> CategoryEnums(bool includeUndefined = false)
        {
            List<Enum> result = new List<Enum>();

            foreach (ProfileType profileType in Enum.GetValues(typeof(ProfileType)))
            {
                if (!includeUndefined && profileType == ProfileType.Undefined)
                {
                    continue;
                }

                result.Add(profileType);
            }

            foreach (ProfileGroup profileGroup in Enum.GetValues(typeof(ProfileGroup)))
            {
                if (!includeUndefined && profileGroup == ProfileGroup.Undefined)
                {
                    continue;
                }

                result.Add(profileGroup);
            }

            return result;
        }
    }
}
