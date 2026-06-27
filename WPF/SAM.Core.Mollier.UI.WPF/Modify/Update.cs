// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using SAM.Core.Mollier.UI.Forms;
using SAM.Geometry.Mollier;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Opens the appropriate edit dialog for a UI Mollier object (OxyPlot/WPF port). WinForms
        /// DialogResult.OK is replaced by the dialog's modeless-safe DialogOk flag.
        /// </summary>
        public static bool Update(this IUIMollierObject mollierObject)
        {
            if (mollierObject is UIMollierPoint)
            {
                UIMollierPoint uIMollierPoint = (UIMollierPoint)mollierObject;
                UIMollierPointForm customizePointForm = new UIMollierPointForm(uIMollierPoint);
                customizePointForm.ShowDialog();
                if (!customizePointForm.DialogOk)
                {
                    return false;
                }
                uIMollierPoint = customizePointForm.UIMollierPoint;
            }
            else if (mollierObject is UIMollierProcess)
            {
                UIMollierProcess uIMollierProcess = (UIMollierProcess)mollierObject;
                UIMollierProcessForm_Limited customizeProcessForm = new UIMollierProcessForm_Limited(uIMollierProcess);
                customizeProcessForm.ShowDialog();
                if (!customizeProcessForm.DialogOk)
                {
                    return false;
                }
                uIMollierProcess = customizeProcessForm.UIMollierProcess;
            }
            else
            {
                throw new NotImplementedException();
            }

            return true;
        }
    }
}
