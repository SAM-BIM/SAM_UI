// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>Opt-in.</b> Renders the Design Optimisation window to PNG (RenderTargetBitmap) in the states a reviewer looks at: the opening
    /// state, Hooke-Jeeves selected, after a run through SAM_Tas' stub TasGenExecute, and with Diagnostics open. Nothing is
    /// run that needs Tas or a licence, and nothing is written beside the screenshots.
    /// <para>
    /// <c>SAM_OPT_SCREENSHOTS</c> is the folder the images go to. Without it this passes having done nothing.
    /// </para>
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasOptimisationScreenshotHarness : IDisposable
    {
        public TasOptimisationScreenshotHarness()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
        }

        private static void Render(TasOptimisationWindow window, string directory, string name, double height)
        {
            PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory, name), 860, height);
        }

        [WpfFact]
        public async Task Render_the_window_in_its_reviewed_states()
        {
            string directory = Environment.GetEnvironmentVariable("SAM_OPT_SCREENSHOTS");
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);

            //The stub's script (a JSON spec) mentions no design-variable or output name, so a run through it carries the name warnings.
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.968943799848584 }));

            //This script mentions every name, so the form reads as ready (it is never run).
            using TasOptimisationWorkspace workspace_Ready = new TasOptimisationWorkspace("Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", 1); \"Cost\" \"CO2\"");

            //1. Opening: the example, with no Tas project or script chosen yet.
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            Render(window, directory, "1-opening.png", 1100);
            window.Close();

            //1b. A ready form (golden section): the footer says what will run.
            window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace_Ready.Input(TasOptimisationExample.SystemsDemoGoldenSection));
            Render(window, directory, "1b-ready.png", 1100);
            window.Close();

            //2. Hooke-Jeeves selected, Advanced open.
            window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace_Ready.Input(TasOptimisationExample.SystemsDemoHookeJeeves));
            ((Expander)window.FindName("expander_Advanced")).IsExpanded = true;
            Render(window, directory, "2-hooke-jeeves.png", 1250);
            window.Close();

            //3. After a stub run (golden section, the Systems Demo values), and 4. with Diagnostics open.
            window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace.Input(TasOptimisationExample.SystemsDemoGoldenSection));
            await window.RunAsync();
            Render(window, directory, "3-after-run.png", 900);

            ((Expander)window.FindName("expander_Diagnostics")).IsExpanded = true;
            Render(window, directory, "4-diagnostics.png", 1100);

            ((CheckBox)window.FindName("checkBox_SearchDetails")).IsChecked = true;
            Render(window, directory, "5-search-details.png", 1100);
            window.Close();
        }
    }
}
