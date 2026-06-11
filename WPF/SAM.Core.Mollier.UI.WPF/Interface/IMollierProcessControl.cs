// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Forms;
using SAM.Geometry.Mollier;

namespace SAM.Core.Mollier.UI
{
    public interface IMollierProcessControl
    {
        UIMollierProcess GetUIMollierProcess();

        MollierForm MollierForm { get; set; }
    }
}
