// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Core.UI
{
    public interface IModification
    {
        // Whether this modification represents a change that should be captured on the undo/redo
        // history (see UIJSAMObject). Real model/view-settings edits are undoable; transient changes
        // such as a camera-only view update are not.
        bool Undoable { get; }
    }
}
