// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.Mollier.UI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // MollierForm is now a WPF Window (Stage 2 WinForms->WPF migration), so the standalone
            // Mollier app hosts it via a WPF Application instead of WinForms Application.Run(Form).
            System.Windows.Application application = new System.Windows.Application();
            application.Run(new Core.Mollier.UI.MollierForm());
        }
    }
}
