// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Core.UI
{
    public class Modification :IModification
    {
        // Undoable by default; transient modifications (e.g. camera-only view updates) override this.
        public virtual bool Undoable => true;
    }
}
