// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// AnalyticalModelModification for edits that change attributes only and can never change
    /// geometry, view visibility or label text (e.g. assigning an InternalCondition to a space).
    /// Views handle it by refreshing the affected objects' colors and the legend in place
    /// instead of regenerating sections, labels and the scene.
    /// </summary>
    public class AttributeModification : AnalyticalModelModification
    {
        public AttributeModification(IEnumerable<SAMObject> sAMObjects)
            : base(sAMObjects)
        {
        }
    }
}
