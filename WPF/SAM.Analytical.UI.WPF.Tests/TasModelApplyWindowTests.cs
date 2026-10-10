// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// "Apply best design" in the Design Optimisation window (PR9): offered only after a run with a best point, shown
    /// before anything is written, then the open model changed as one Undo step and Energy Simulation offered. The run goes
    /// through SAM_Tas' runner and the stub TasGenExecute; the Tas writers are stand-ins.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasModelApplyWindowTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "SAMApplyWin", Guid.NewGuid().ToString("N").Substring(0, 10));

        public TasModelApplyWindowTests()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static T Control<T>(FrameworkElement frameworkElement, string name) where T : class
        {
            return Assert.IsAssignableFrom<T>(frameworkElement.FindName(name));
        }

        private static string Text(string path) => System.IO.File.ReadAllText(path);

        /// <summary>A window on the "tas-model" engine over the project, the model read through the stand-ins, with the open model.</summary>
        private static async Task<TasOptimisationWindow> Window(string project, UIAnalyticalModel uIAnalyticalModel)
        {
            TasModelSession session = TasModelJourneyFixtures.Session(TasModelApplyFixtures.Inventory());
            session.GlazingWriter = (tbd, options) => { };
            TasOptimisationWindow window = new TasOptimisationWindow(null, uIAnalyticalModel.JSAMObject, session, () => [])
            {
                TasGenExecutePath = TasOptimisationWorkspace.StubExecutable,
                UIAnalyticalModel = uIAnalyticalModel,
            };

            window.SwitchEngine("tas-model");
            Control<TextBox>(window, "textBox_Directory").Text = project;
            await window.ReadModel(project);
            window.SetInput(new TasOptimisationInput(window.Input));
            window.AddTarget(window.CanChangeItems.Single(x => x.Name == "Studio 1_0 cooling setpoint"));
            window.AddMeasure(window.CanMeasureItems.Single(x => x.Name == "Annual cooling demand"));
            window.SetInput(new TasOptimisationInput(window.Input));
            Assert.True(window.Checks.CanRun(), string.Join("\n", window.Checks));
            return window;
        }

        [WpfFact]
        public async Task The_best_design_is_shown_then_applied_to_the_model_as_one_undo_step_and_Energy_Simulation_is_offered()
        {
            string project = TasModelApplyFixtures.Project(folder);
            AnalyticalModel model = TasModelApplyFixtures.Model();
            UIAnalyticalModel ui = new UIAnalyticalModel(model) { Path = Path.Combine(project, "model.sam") };
            TasOptimisationWindow window = await Window(project, ui);
            Button applyBest = Control<Button>(window, "button_ApplyBest");
            Assert.Equal(Visibility.Collapsed, applyBest.Visibility);

            using (new TasModelJourneyFixtures.StubSpec("{\"kind\":\"quadratic\",\"center\":[25.976],\"offset\":10,\"outputs\":[\"Y1\"]}"))
            {
                await window.RunAsync();
            }

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.True(report.Successful, report.ToText());
            double best = report.BestPoint[0];
            Assert.Equal(Visibility.Visible, applyBest.Visibility);

            // Shown first; nothing written.
            TasModelApplyPlan plan = window.PrepareApply();
            Assert.True(plan.CanApply, string.Join("\n", plan.Lines));
            Assert.Equal(Visibility.Visible, Control<Border>(window, "border_Apply").Visibility);
            Assert.Equal(plan.Headline, Control<TextBlock>(window, "textBlock_ApplyHeadline").Text);
            Assert.True(Control<Button>(window, "button_ApplyConfirm").IsEnabled);
            TasOptimisationCheck line = Assert.Single(Assert.IsAssignableFrom<IEnumerable<TasOptimisationCheck>>(Control<ItemsControl>(window, "itemsControl_Apply").ItemsSource));
            Assert.Contains("Cooling setpoint of “Studio 1_0”", line.Detail);
            Assert.Contains(best.ToString("R", CultureInfo.InvariantCulture), line.ToolTipText);
            Assert.Equal("tbd", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
            Assert.False(ui.CanUndo);

            window.ConfigureApplier = applier => TasModelApplyTests.Stand_in(applier, null, TasModelApplyFixtures.Inventory(cooling1: TasModelApplyFixtures.CoolingHours().Select(x => x == 23 ? best : x).ToArray()));
            TasModelApplyOutcome outcome = Assert.IsType<TasModelApplyOutcome>(await window.ApplyAsync());

            Assert.True(outcome.Succeeded, outcome.Headline);
            Assert.Equal("tbd written", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
            AnalyticalModel changed = ui.JSAMObject;
            Assert.NotSame(model, changed);
            Assert.Equal(best, Query.TasHours(Cooling(changed))[8]);
            Assert.True(ui.CanUndo);
            Assert.Equal(outcome.Headline, Control<TextBlock>(window, "textBlock_ApplyHeadline").Text);
            Assert.Equal(Visibility.Visible, Control<Button>(window, "button_EnergySimulation").Visibility);
            Assert.Equal(Visibility.Visible, Control<Button>(window, "button_OpenBackup").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<Button>(window, "button_ApplyConfirm").Visibility);

            // Applied once: the run is no longer offered, and the model is read again before another run.
            Assert.Equal(Visibility.Collapsed, applyBest.Visibility);
            Assert.Equal(TasModelReadState.NotRead, window.ModelSession.State);

            // One Undo step brings the model back (the restore completes asynchronously).
            Assert.True(ui.Undo());
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            while (Cooling(ui.JSAMObject).Name != "Cooling 23")
            {
                Assert.True(DateTime.UtcNow < deadline, "Undo did not restore the model.");
                await Task.Delay(20);
            }

            Assert.False(ui.CanUndo);
            Assert.Equal(23, Query.TasHours(Cooling(ui.JSAMObject))[8]);

            window.RequestEnergySimulation();
            Assert.True(window.EnergySimulationRequested);
        }

        [WpfFact]
        public async Task A_run_that_failed_offers_nothing_to_apply()
        {
            string project = TasModelApplyFixtures.Project(folder);
            UIAnalyticalModel ui = new UIAnalyticalModel(TasModelApplyFixtures.Model()) { Path = Path.Combine(project, "model.sam") };
            TasOptimisationWindow window = await Window(project, ui);

            using (new TasModelJourneyFixtures.StubSpec("{\"kind\":\"quadratic\",\"center\":[25.976],\"outputs\":[\"Y1\"],\"modes\":{\"1\":\"errorFile\",\"2\":\"errorFile\"}}"))
            {
                await window.RunAsync();
            }

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.False(report.Successful);
            Assert.Equal(OptimisationOutcome.EvaluationFailed, report.Outcome);
            Assert.Equal(Visibility.Collapsed, Control<Button>(window, "button_ApplyBest").Visibility);

            TasModelApplyPlan plan = window.PrepareApply();
            Assert.False(plan.CanApply);
            Assert.StartsWith("There is no best design to apply", plan.Refusal);
            Assert.Null(await window.ApplyAsync());
            Assert.Equal("tbd", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
            window.RequestEnergySimulation();
            Assert.False(window.EnergySimulationRequested);
        }

        private static Profile Cooling(AnalyticalModel model)
        {
            Space space = model.AdjacencyCluster.GetSpaces().Single(x => x.Name == TasModelApplyFixtures.Studio1);
            return space.InternalCondition.GetProfile(ProfileType.Cooling, model.ProfileLibrary);
        }
    }
}
