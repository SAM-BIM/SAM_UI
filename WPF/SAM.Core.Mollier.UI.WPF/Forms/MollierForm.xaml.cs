// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>
    /// SKELETON (Stage 2e). The full MollierForm (the ~1,164-LOC host window) is ported in the final
    /// 2e batch. Only the surface referenced by the process controls / process forms lives here for now,
    /// so the dependency cluster compiles bottom-up: the <see cref="MollierPointSelected"/> event.
    /// </summary>
    public partial class MollierForm : Window
    {
        /// <summary>Raised when the user picks a point on the chart (subscribed by process controls).</summary>
        public event MollierPointSelectedEventHandler MollierPointSelected;

        public MollierForm()
        {
            InitializeComponent();
        }

        // Lets the skeleton raise the event without "never used" warnings; the full batch wires this to
        // the embedded MollierControl's MollierPointSelected.
        protected void OnMollierPointSelected(MollierPointSelectedEventArgs e)
        {
            MollierPointSelected?.Invoke(this, e);
        }
    }
}
