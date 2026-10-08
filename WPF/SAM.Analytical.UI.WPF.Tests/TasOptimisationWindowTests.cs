// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.IO;
using File = System.IO.File;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Simulate &gt; Optimisation (Design Optimisation) end to end: the ribbon command, the window's opening state, and real runs of
    /// GenOptDocument.RunNative through SAM_Tas' StubTasGenExecute - a child process that follows the TasGenExecute
    /// protocol - so the SAM.Math kernel, the SAM_Tas evaluator and the window's background run, progress and
    /// cancellation are all the production code. No Tas, licence or COM is needed.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasOptimisationWindowTests : IDisposable
    {
        public TasOptimisationWindowTests()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
        }

        private static T Control<T>(FrameworkElement frameworkElement, string name) where T : class
        {
            return Assert.IsAssignableFrom<T>(frameworkElement.FindName(name));
        }

        private static TasOptimisationWindow Window(TasOptimisationWorkspace workspace, TasOptimisationExample tasOptimisationExample = TasOptimisationExample.SystemsDemoGoldenSection)
        {
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace.Input(tasOptimisationExample));
            return window;
        }

        private static async Task Until(Func<bool> condition, string message, int seconds = 60)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, message);
                await Task.Delay(20);
            }
        }

        // ---- the ribbon command --------------------------------------------------------------------------

        [WpfFact]
        public void The_ribbon_button_sits_right_after_Energy_Simulation_and_needs_no_model()
        {
            Windows.AnalyticalWindow analyticalWindow = new Windows.AnalyticalWindow();
            try
            {
                RibbonGroup ribbonGroup = Assert.IsType<RibbonGroup>(analyticalWindow.FindName("RibbonGroup_Simulate_Simulate"));
                List<RibbonButton> ribbonButtons = ribbonGroup.Items.OfType<RibbonButton>().ToList();

                Assert.Equal(["RibbonButton_SolarSimulation", "RibbonButton_EnergySimulation", "RibbonButton_Optimisation"], ribbonButtons.Select(x => x.Name));

                RibbonButton ribbonButton = ribbonButtons[2];
                Assert.Equal("Optimisation", ribbonButton.Label);
                Assert.NotNull(ribbonButton.LargeImageSource);
                Assert.Equal("Find the design-variable values that minimise an objective by running a Tas model repeatedly. Works on a Tas project folder, not on the open model. Not the Part O Optimise (2B) command.", ribbonButton.ToolTipDescription);

                //Energy Simulation needs an open model; Optimisation works on a Tas project folder.
                Assert.False(ribbonButtons[1].IsEnabled);
                Assert.True(ribbonButton.IsEnabled);
            }
            finally
            {
                analyticalWindow.Close();
            }
        }

        [Fact]
        public void The_icon_is_its_own_not_Energy_Simulations()
        {
            using System.Drawing.Bitmap optimisation = Properties.Resources.SAM_Optimisation;
            using System.Drawing.Bitmap energySimulation = Properties.Resources.SAM_EnergySimulation;

            Assert.Equal((32, 32), (optimisation.Width, optimisation.Height));

            bool different = false;
            for (int x = 0; x < 32 && !different; x++)
            {
                for (int y = 0; y < 32 && !different; y++)
                {
                    different = optimisation.GetPixel(x, y) != energySimulation.GetPixel(x, y);
                }
            }

            Assert.True(different);
        }

        // ---- opening state -------------------------------------------------------------------------------

        [WpfFact]
        public void The_window_opens_on_the_golden_section_example_with_Start_and_Step_not_applicable()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            TasOptimisationInput input = window.Input;
            Assert.Equal(AlgorithmType.GoldenSection, input.AlgorithmType);
            Assert.Equal((string.Empty, string.Empty), (input.Directory, input.ScriptPath));
            Assert.Equal("Setpoint", Assert.Single(input.Parameters).Name);
            Assert.False(input.Parameters[0].StartAndStepApplicable);
            Assert.Equal("Result", input.PrimaryObjective?.Name);

            Assert.Equal(Visibility.Visible, Control<Grid>(window, "grid_GoldenSection").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_HookeJeeves").Visibility);
            Assert.Equal("0.1", Control<TextBox>(window, "textBox_AbsDiffFunction").Text);
            Assert.Equal("2000", Control<TextBox>(window, "textBox_MaxIterations").Text);
            Assert.Equal("Result", Control<ComboBox>(window, "comboBox_Objective").Text);
            Assert.Equal(["Result", "Cost", "CO2"], Control<ComboBox>(window, "comboBox_Objective").Items.Cast<string>());
            Assert.Equal(["Cost", "CO2"], Control<ItemsControl>(window, "itemsControl_Objectives").Items.Cast<TasOptimisationObjectiveRow>().Select(x => x.Name));
            Assert.Contains("lowest Result", Control<TextBlock>(window, "textBlock_Objectives").Text);

            //Nothing to run yet: the folder and script are the user's.
            Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            Assert.Contains(window.Checks, x => x.Title == "Tas project" && x.Status == TasOptimisationCheckStatus.Blocked);
            Assert.StartsWith("✕ Before running - Tas project: ", Control<TextBlock>(window, "textBlock_NextStep").Text);
        }

        [WpfFact]
        public void Choosing_Hooke_Jeeves_makes_Start_and_Step_applicable_and_shows_its_settings()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            Control<ComboBox>(window, "comboBox_Algorithm").SelectedItem = AlgorithmType.GPSHookeJeeves;

            Assert.Equal(AlgorithmType.GPSHookeJeeves, window.Input.AlgorithmType);
            Assert.True(window.Input.Parameters[0].StartAndStepApplicable);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_GoldenSection").Visibility);
            Assert.Equal(Visibility.Visible, Control<Grid>(window, "grid_HookeJeeves").Visibility);
            Assert.Equal("4", Control<TextBox>(window, "textBox_NumberOfStepReduction").Text);

            //Its two exponent settings are Advanced, and only for it.
            Assert.Equal(Visibility.Visible, Control<Grid>(window, "grid_HookeJeevesAdvanced").Visibility);
            Control<ComboBox>(window, "comboBox_Algorithm").SelectedItem = AlgorithmType.GoldenSection;
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_HookeJeevesAdvanced").Visibility);
        }

        [WpfFact]
        public void Loading_the_Hooke_Jeeves_example_keeps_the_folder_and_script()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);

            ComboBox comboBox = Control<ComboBox>(window, "comboBox_Example");
            comboBox.SelectedIndex = 1;
            Control<Button>(window, "button_LoadExample").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            TasOptimisationInput input = window.Input;
            Assert.Equal(AlgorithmType.GPSHookeJeeves, input.AlgorithmType);
            Assert.Equal(("10", "2"), (input.Parameters[0].Start, input.Parameters[0].Step));
            Assert.Equal((workspace.Directory, workspace.ScriptPath), (input.Directory, input.ScriptPath));
        }

        [WpfFact]
        public void The_open_models_folder_is_proposed_only_when_it_holds_Tas_files()
        {
            using TasOptimisationWorkspace withTas = new TasOptimisationWorkspace();
            using TasOptimisationWorkspace withoutTas = new TasOptimisationWorkspace(tasFile: false);

            Assert.Equal(withTas.Directory, new TasOptimisationWindow(withTas.Directory).Input.Directory);
            Assert.Equal(string.Empty, new TasOptimisationWindow(withoutTas.Directory).Input.Directory);
        }

        [WpfFact]
        public void A_Tas_folder_that_cannot_be_listed_blocks_the_run_instead_of_failing_the_window()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            using (workspace.DenyListing())
            {
                //Proposed as the open model's folder (the constructor), then chosen in the form (Refresh).
                TasOptimisationWindow window = new TasOptimisationWindow(workspace.Directory) { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
                Assert.Equal(string.Empty, window.Input.Directory);

                window.SetInput(workspace.Input());

                Assert.Contains(window.Checks, x => x.Title == "Tas project" && x.Status == TasOptimisationCheckStatus.Blocked && x.Detail.StartsWith("The folder cannot be read: "));
                Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            }
        }

        [WpfFact]
        public void The_form_is_remembered_for_the_session_only()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();

            TasOptimisationWindow window = Window(workspace, TasOptimisationExample.SystemsDemoHookeJeeves);
            window.Input.Parameters[0].Maximum = "30";
            window.Show();
            window.Close();

            TasOptimisationInput remembered = new TasOptimisationWindow().Input;
            Assert.Equal((workspace.Directory, AlgorithmType.GPSHookeJeeves, "30"), (remembered.Directory, remembered.AlgorithmType, remembered.Parameters[0].Maximum));

            TasOptimisationWindow.ResetSession();
            Assert.Equal(AlgorithmType.GoldenSection, new TasOptimisationWindow().Input.AlgorithmType);
        }

        [WpfFact]
        public void A_missing_TasGenExecute_blocks_the_run_and_nothing_starts()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = Path.Combine(workspace.Directory, "missing.exe") };
            window.SetInput(workspace.Input());

            Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            Assert.Contains(window.Checks, x => x.Title == "Tas optimisation engine" && x.Status == TasOptimisationCheckStatus.Blocked);

            window.RunAsync().GetAwaiter().GetResult();

            Assert.Null(window.Report);
            Assert.False(Directory.Exists(workspace.RunsDirectory));
        }

        // ---- runs through the stub -----------------------------------------------------------------------

        [WpfFact]
        public async Task A_golden_section_run_shows_the_kernels_trace_and_result_and_matches_a_direct_RunNative()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.Success, report.Outcome);
            Assert.True(report.Successful);
            Assert.False(window.Running);
            Assert.NotNull(report.Interval);

            //The live trace is the kernel's OutputListingAll, row for row.
            Assert.Equal(report.Result.Entries, window.TraceRows.Select(x => x.Entry));
            Assert.Equal(report.Result.Entries.Count, workspace.EvaluationFolders().Count);

            //Golden section: the lowest entry, first on a tie.
            OptimisationTraceEntry lowest = report.Result.Entries.Where(x => !double.IsNaN(x.Objective)).OrderBy(x => x.Objective).First();
            Assert.Equal(lowest.Coordinates, report.BestPoint);
            Assert.Equal(["Result", "Cost", "CO2"], report.ObjectiveNames);
            Assert.StartsWith(workspace.RunsDirectory, report.RunDirectory);
            Assert.True(Directory.Exists(report.RunDirectory));

            //The window adds nothing: the same document run directly gives the same trace, bit for bit.
            Assert.True(workspace.Input().TryGetDefinition(out TasOptimisationDefinition definition, out _));
            NativeGenOptRun direct = definition.ToGenOptDocument(workspace.Directory, File.ReadAllText(workspace.ScriptPath)).RunNative(Path.Combine(workspace.Directory, "direct"), TasOptimisationWorkspace.StubExecutable);
            Assert.Equal(direct.Result.Outcome, report.Outcome);
            Assert.Equal(direct.Result.Simulations, report.Simulations);
            Assert.Equal(direct.Result.Entries.Select(x => (x.Simulation, x.Coordinates[0], x.Outputs[0], x.Outputs[1], x.Outputs[2], x.Event)), report.Result.Entries.Select(x => (x.Simulation, x.Coordinates[0], x.Outputs[0], x.Outputs[1], x.Outputs[2], x.Event)));

            //Shown: the result panel with the outcome and the best point, and Run available again.
            Assert.Equal(Visibility.Visible, Control<Border>(window, "border_Result").Visibility);
            Assert.Equal(report.Headline, Control<TextBlock>(window, "textBlock_ResultHeadline").Text);
            Assert.Equal("Optimum found after " + report.Simulations + " simulations", report.Headline);
            Assert.True(Control<Button>(window, "button_OpenRunFolder").IsEnabled);
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);
            Assert.StartsWith("Best so far: Simulation ", Control<TextBlock>(window, "textBlock_RunLowest").Text);
        }

        [WpfFact]
        public async Task A_Hooke_Jeeves_run_reports_the_kernels_minimum()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 5.0 }));
            TasOptimisationWindow window = Window(workspace, TasOptimisationExample.SystemsDemoHookeJeeves);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.Success, report.Outcome);
            Assert.NotNull(report.Result.Minimum);
            Assert.Equal(report.Result.Minimum.Coordinates, report.BestPoint);
            Assert.Equal(5.0, report.BestPoint[0]);
            Assert.Equal(report.Result.Entries, window.TraceRows.Select(x => x.Entry));
        }

        [WpfFact]
        public async Task The_primary_objective_chosen_in_the_window_is_the_one_minimised()
        {
            //The stub writes Result = (x - 4)^2 and Cost = 1 * x: minimising Cost drives Setpoint to its lower bound.
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.0 }, ["Result", "Cost"]));
            TasOptimisationInput input = workspace.Input(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Objectives = [new TasOptimisationObjectiveRow("Result", false), new TasOptimisationObjectiveRow("Cost", true)];

            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(input);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(["Cost", "Result"], report.ObjectiveNames);
            Assert.Equal(-5.0, report.BestPoint[0]);
        }

        [WpfFact]
        public async Task Cancel_lets_the_running_evaluation_finish_and_starts_no_other()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.968943799848584 }, sleepMs: new Dictionary<string, int> { ["2"] = 2500 }));
            TasOptimisationWindow window = Window(workspace);

            Task task = window.RunAsync();
            Assert.True(window.Running);
            Assert.Equal(Visibility.Visible, Control<Button>(window, "button_Cancel").Visibility);
            Assert.False(Control<TextBox>(window, "textBox_Directory").IsEnabled);

            //Cancel only once evaluation 2's process is running (it records its invocation, then sleeps).
            await Until(() => workspace.EvaluationFolder("0002") is string folder && File.Exists(Path.Combine(folder, "stub-invocation.txt")), "evaluation 2 never started");
            string evaluation2 = workspace.EvaluationFolder("0002");
            Assert.False(File.Exists(Path.Combine(evaluation2, "stub-finished.txt")), "cancel must happen while the process runs");

            window.RequestCancel();
            Assert.False(Control<Button>(window, "button_Cancel").IsEnabled);

            await task;

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.Cancelled, report.Outcome);
            Assert.False(report.Successful);
            Assert.Empty(report.BestPoint);
            Assert.True(File.Exists(Path.Combine(evaluation2, "stub-finished.txt")), "the running evaluation was not allowed to finish");
            Assert.Contains("Result::", File.ReadAllText(Path.Combine(evaluation2, "Output.txt")));
            Assert.Equal(["0001", "0002"], workspace.EvaluationFolders());
            Assert.False(window.Running);
            Assert.True(Control<TextBox>(window, "textBox_Directory").IsEnabled);
        }

        [WpfFact]
        public async Task Closing_during_a_run_asks_then_stops_after_the_current_evaluation_and_closes()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.968943799848584 }, sleepMs: new Dictionary<string, int> { ["1"] = 1500 }));
            TasOptimisationWindow window = Window(workspace);
            int asked = 0;
            window.ConfirmStop = () =>
            {
                asked++;
                return true;
            };

            bool closed = false;
            window.Closed += (s, e) => closed = true;
            window.Show();

            Task task = window.RunAsync();
            await Until(() => workspace.EvaluationFolder("0001") is string folder && File.Exists(Path.Combine(folder, "stub-invocation.txt")), "evaluation 1 never started");

            window.Close();
            Assert.Equal(1, asked);
            Assert.False(closed, "the window must stay open while TasGenExecute runs");
            Assert.True(window.Running);

            //A second close while stopping does not ask again.
            window.Close();
            Assert.Equal(1, asked);

            await task;

            Assert.True(closed);
            Assert.Equal(OptimisationOutcome.Cancelled, window.Report?.Outcome);
            Assert.True(File.Exists(Path.Combine(workspace.EvaluationFolder("0001"), "stub-finished.txt")));
            Assert.Equal(["0001"], workspace.EvaluationFolders());
        }

        [WpfFact]
        public async Task Declining_to_stop_keeps_the_run_and_the_window()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            window.ConfirmStop = () => false;
            window.Show();
            try
            {
                Task task = window.RunAsync();
                window.Close();

                Assert.True(window.IsVisible);
                await task;
                Assert.Equal(OptimisationOutcome.Success, window.Report?.Outcome);
                Assert.True(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public async Task An_evaluation_failure_is_reported_with_the_evaluators_message()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.0 }, modes: new Dictionary<string, string> { ["1"] = "errorFile" }));
            TasOptimisationWindow window = Window(workspace);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.EvaluationFailed, report.Outcome);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, report.Status);
            Assert.Contains(report.Lines, x => x.Detail.Contains("injected script exception"));
            Assert.Equal("✕", Control<TextBlock>(window, "textBlock_ResultGlyph").Text);
        }

        // ---- Design Optimisation: language and information architecture ----------------------------------

        [WpfFact]
        public void The_window_is_Design_Optimisation_with_a_Setup_tab_and_a_Run_and_Results_tab()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            Assert.Equal("Design Optimisation", window.Title);
            Assert.Equal("Finds the design-variable values that give the lowest objective result. Each simulation runs your Tas script on a copy of the Tas project; your files are not changed.", Control<TextBlock>(window, "textBlock_Intro").Text);

            TabControl tabControl = Control<TabControl>(window, "tabControl_Main");
            Assert.Equal(["Setup", "Run & Results"], tabControl.Items.Cast<TabItem>().Select(x => (string)x.Header));
            Assert.Same(tabControl.Items[0], tabControl.SelectedItem);
            Assert.False(window.ShowingRunAndResults);

            //The footer is outside the tabs, so it is the same on both.
            Assert.False(Within(tabControl, Control<Button>(window, "button_Run")));
            Assert.False(Within(tabControl, Control<Button>(window, "button_Close")));
            Assert.False(Within(tabControl, Control<Button>(window, "button_Cancel")));
            Assert.False(Within(tabControl, Control<TextBlock>(window, "textBlock_NextStep")));
            Assert.True(Within(tabControl, Control<Button>(window, "button_Directory")));
            Assert.True(Within(Control<TabItem>(window, "tabItem_Run"), Control<Border>(window, "border_Result")));
        }

        [WpfFact]
        public void The_Model_section_names_the_engine_and_the_script_syntax_is_a_tooltip()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            Assert.Contains(Texts(window), x => x == "Simulation engine: Tas (script)");
            Assert.Contains(Texts(window), x => x == "Design variables");
            Assert.Contains(Texts(window), x => x == "Recorded outputs");
            Assert.Contains(Texts(window), x => x == "Recorded for every simulation, not optimised.");

            //The syntax is on the Script field's tooltip only.
            string tooltip = Assert.IsType<string>(Control<TextBox>(window, "textBox_ScriptPath").ToolTip);
            Assert.Contains("Variables[\"name\"].VariableValue", tooltip);
            Assert.Contains("ScriptOutput.SetValue(\"name\", value)", tooltip);
            Assert.DoesNotContain(Texts(window), x => x.Contains("VariableValue") || x.Contains("ScriptOutput.SetValue"));
        }

        [WpfFact]
        public void No_GenOpt_field_name_or_Java_remark_is_a_label_but_the_GenOpt_names_are_in_tooltips()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            string[] forbidden = ["AbsDiffFunction", "MeshSizeDivider", "NumberOfStepReduction", "InitialMeshSizeExponent", "MeshSizeExponentIncrement", "MaxIte", "MaxEqualResults", "Java"];

            //Every state: golden section, then Hooke-Jeeves (the labels of both are in the tree whichever is shown).
            foreach (AlgorithmType algorithmType in TasOptimisationInput.AlgorithmTypes)
            {
                Control<ComboBox>(window, "comboBox_Algorithm").SelectedItem = algorithmType;
                foreach (string text in Texts(window))
                {
                    Assert.DoesNotContain(forbidden, x => text.Contains(x));
                }
            }

            Assert.Equal("GenOpt: MeshSizeDivider", Control<TextBox>(window, "textBox_MeshSizeDivider").ToolTip);
            Assert.Equal("GenOpt: AbsDiffFunction", Control<TextBox>(window, "textBox_AbsDiffFunction").ToolTip);
            Assert.Equal("GenOpt: MaxIte", Control<TextBox>(window, "textBox_MaxIterations").ToolTip);
            Assert.Equal("GenOpt: NumberOfStepReduction", Control<TextBox>(window, "textBox_NumberOfStepReduction").ToolTip);
            Assert.Equal("GenOpt: InitialMeshSizeExponent", Control<TextBox>(window, "textBox_InitialMeshSizeExponent").ToolTip);
            Assert.Equal("GenOpt: MeshSizeExponentIncrement", Control<TextBox>(window, "textBox_MeshSizeExponentIncrement").ToolTip);

            Assert.Contains("Objective tolerance", Texts(window));
            Assert.Contains("Step reduction factor", Texts(window));
            Assert.Contains("Step reductions", Texts(window));
            Assert.Contains("Maximum simulations", Texts(window));
            Assert.Contains("Initial step exponent", Texts(window));
            Assert.Contains("Step exponent increment", Texts(window));
        }

        [WpfFact]
        public void The_method_list_shows_plain_names_over_the_unchanged_algorithm_values()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();
            ComboBox comboBox = Control<ComboBox>(window, "comboBox_Algorithm");

            Assert.Equal([AlgorithmType.GoldenSection, AlgorithmType.GPSHookeJeeves], comboBox.Items.Cast<AlgorithmType>());

            TasOptimisationAlgorithmNameConverter converter = new TasOptimisationAlgorithmNameConverter();
            Assert.Equal(["Golden section (one design variable)", "Hooke\u2013Jeeves pattern search"], comboBox.Items.Cast<AlgorithmType>().Select(x => converter.Convert(x, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)));

            Assert.Contains("Narrows the range of one design variable", Control<TextBlock>(window, "textBlock_Algorithm").Text);
            comboBox.SelectedItem = AlgorithmType.GPSHookeJeeves;
            Assert.Contains("reducing the step as it converges", Control<TextBlock>(window, "textBlock_Algorithm").Text);
        }

        [WpfFact]
        public void Advanced_is_collapsed_and_holds_the_runs_folder_and_the_step_exponents()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();
            Expander expander = Control<Expander>(window, "expander_Advanced");

            Assert.False(expander.IsExpanded);
            Assert.Equal("Advanced", expander.Header);
            foreach (string name in new[] { "textBox_RunsDirectory", "textBox_InitialMeshSizeExponent", "textBox_MeshSizeExponentIncrement" })
            {
                Assert.True(Within(expander, Control<TextBox>(window, name)), name);
            }

            Assert.False(Within(expander, Control<TextBox>(window, "textBox_MaxIterations")));
        }

        [WpfFact]
        public void The_footer_says_what_will_run_when_the_form_is_ready()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(script: "Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", 1); \"Cost\" \"CO2\"");
            TasOptimisationWindow window = Window(workspace);

            Assert.Equal("✓ Ready: Golden section on Setpoint (−5 to 35), minimising Result; recording Cost, CO2; at most 2000 simulations.", Control<TextBlock>(window, "textBlock_NextStep").Text);
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);

            //Without the engine's path: that is Diagnostics.
            Assert.DoesNotContain(TasOptimisationWorkspace.StubExecutable, Control<TextBlock>(window, "textBlock_NextStep").Text);
            Assert.All(window.Checks, x => Assert.DoesNotContain("TasGenExecute", x.Title + x.Detail));
        }

        [WpfFact]
        public void Choosing_an_output_in_the_Objective_box_makes_it_the_first_output_of_the_definition()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(script: "Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", 1); \"Cost\" \"CO2\"");
            TasOptimisationWindow window = Window(workspace);
            ComboBox comboBox = Control<ComboBox>(window, "comboBox_Objective");

            Assert.Equal("Result", window.Input.PrimaryObjective?.Name);

            comboBox.SelectedItem = "Cost";

            //Bound to the Primary flag: exactly one output is flagged, and it is the one chosen.
            Assert.Equal("Cost", window.Input.PrimaryObjective?.Name);
            Assert.Equal(["Cost"], window.Input.Objectives.Where(x => x.Primary).Select(x => x.Name));
            Assert.True(window.Input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(["Cost", "Result", "CO2"], definition.ObjectiveNames);

            //The old objective is now a recorded output.
            Assert.Equal(["Result", "CO2"], Control<ItemsControl>(window, "itemsControl_Objectives").Items.Cast<TasOptimisationObjectiveRow>().Select(x => x.Name));
            Assert.Equal("Cost", comboBox.Text);
            Assert.Contains("lowest Cost", Control<TextBlock>(window, "textBlock_Objectives").Text);
            Assert.StartsWith("✓ Ready: Golden section on Setpoint (−5 to 35), minimising Cost; recording Result, CO2;", Control<TextBlock>(window, "textBlock_NextStep").Text, StringComparison.Ordinal);
        }

        [WpfFact]
        public void Picking_an_item_in_the_opened_Objective_list_selects_it_as_the_objective()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            window.Show();
            try
            {
                ComboBox comboBox = Control<ComboBox>(window, "comboBox_Objective");
                comboBox.IsDropDownOpen = true;
                window.UpdateLayout();

                //What a click on the second entry does: the container is selected.
                ComboBoxItem comboBoxItem = Assert.IsType<ComboBoxItem>(comboBox.ItemContainerGenerator.ContainerFromIndex(1));
                Assert.Equal("Cost", comboBoxItem.Content);
                comboBoxItem.IsSelected = true;
                comboBox.IsDropDownOpen = false;

                Assert.Equal("Cost", window.Input.PrimaryObjective?.Name);
                Assert.Equal("Cost", comboBox.Text);
                Assert.Equal(["Result", "Cost", "CO2"], comboBox.Items.Cast<string>());
                Assert.Equal(["Result", "CO2"], Control<ItemsControl>(window, "itemsControl_Objectives").Items.Cast<TasOptimisationObjectiveRow>().Select(x => x.Name));
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public async Task The_objective_chosen_in_the_Objective_box_is_the_one_the_run_minimises()
        {
            //The stub writes Result = (x - 4)^2 and Cost = 1 * x: minimising Cost drives Setpoint to its lower bound.
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.0 }, ["Result", "Cost"]));
            TasOptimisationInput input = workspace.Input(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Objectives = [new TasOptimisationObjectiveRow("Result", true), new TasOptimisationObjectiveRow("Cost", false)];
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(input);

            Control<ComboBox>(window, "comboBox_Objective").SelectedItem = "Cost";

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(["Cost", "Result"], report.ObjectiveNames);
            Assert.Equal(-5.0, report.BestPoint[0]);
        }

        [WpfFact]
        public void Typing_a_genuinely_new_name_in_the_Objective_box_makes_it_the_objective_and_keeps_the_old_one_as_recorded()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            ComboBox comboBox = Control<ComboBox>(window, "comboBox_Objective");
            ItemsControl recorded = Control<ItemsControl>(window, "itemsControl_Objectives");

            window.CommitObjective("Energy");

            //Nothing is renamed or lost: Result is still an output, now recorded; Energy is new and is the objective.
            Assert.Equal(["Result", "Cost", "CO2", "Energy"], window.Input.Objectives.Select(x => x.Name));
            Assert.Equal(["Energy"], window.Input.Objectives.Where(x => x.Primary).Select(x => x.Name));
            Assert.Equal(["Result", "Cost", "CO2"], recorded.Items.Cast<TasOptimisationObjectiveRow>().Select(x => x.Name));
            Assert.Equal(["Result", "Cost", "CO2", "Energy"], comboBox.Items.Cast<string>());
            Assert.Equal("Energy", comboBox.Text);

            //The definition minimises Energy and still records the previous objective.
            Assert.True(window.Input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(["Energy", "Result", "Cost", "CO2"], definition.ObjectiveNames);

            //An existing name selects that output.
            window.CommitObjective("CO2");
            Assert.Equal("CO2", window.Input.PrimaryObjective?.Name);
            Assert.Equal(["Result", "Cost", "CO2", "Energy"], window.Input.Objectives.Select(x => x.Name));

            //An empty box changes nothing and shows the objective again.
            window.CommitObjective("  ");
            Assert.Equal("CO2", comboBox.Text);
            Assert.Equal("CO2", window.Input.PrimaryObjective?.Name);
        }

        [WpfTheory]
        [InlineData("cost", "Cost")]
        [InlineData("COST", "Cost")]
        [InlineData("  co2 ", "CO2")]
        [InlineData("result", "Result")]
        public void Typing_the_name_of_an_existing_output_in_another_case_selects_that_output_and_loses_nothing(string typed, string expected)
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            ComboBox comboBox = Control<ComboBox>(window, "comboBox_Objective");

            window.CommitObjective(typed);

            //The same three outputs, in the same order and spelling; only the objective moved.
            Assert.Equal(["Result", "Cost", "CO2"], window.Input.Objectives.Select(x => x.Name));
            Assert.Equal([expected], window.Input.Objectives.Where(x => x.Primary).Select(x => x.Name));
            Assert.Equal(expected, comboBox.Text);
            Assert.Equal(["Result", "Cost", "CO2"], comboBox.Items.Cast<string>());
            Assert.Equal(new[] { "Result", "Cost", "CO2" }.Where(x => x != expected), Control<ItemsControl>(window, "itemsControl_Objectives").Items.Cast<TasOptimisationObjectiveRow>().Select(x => x.Name));
            Assert.True(window.Input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(expected, definition.ObjectiveNames[0]);
            Assert.Equal(3, definition.ObjectiveNames.Count);
        }

        [WpfFact]
        public void Removing_the_objective_output_selects_another_one_and_a_new_output_is_recorded_only()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);

            window.RemoveOutput(window.Input.PrimaryObjective);

            Assert.Equal(["Cost", "CO2"], window.Input.Objectives.Select(x => x.Name));
            Assert.Equal("Cost", window.Input.PrimaryObjective?.Name);
            Assert.Single(window.Input.Objectives, x => x.Primary);
            Assert.Equal("Cost", Control<ComboBox>(window, "comboBox_Objective").Text);
            Assert.True(window.Input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(["Cost", "CO2"], definition.ObjectiveNames);

            //Add output: a recorded output, never the objective.
            Control<Button>(window, "button_AddObjective").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(3, window.Input.Objectives.Count);
            Assert.Single(window.Input.Objectives, x => x.Primary);
            Assert.Equal(["CO2", string.Empty], Control<ItemsControl>(window, "itemsControl_Objectives").Items.Cast<TasOptimisationObjectiveRow>().Select(x => x.Name));

            //Removing the last one leaves a form with no objective, which says so.
            TasOptimisationWindow empty = new TasOptimisationWindow();
            foreach (TasOptimisationObjectiveRow row in empty.Input.Objectives.ToList())
            {
                empty.RemoveOutput(row);
            }

            Assert.Empty(empty.Input.Objectives);
            Assert.Equal(string.Empty, Control<ComboBox>(empty, "comboBox_Objective").Text);
            Assert.Contains("Pick one of the outputs", Control<TextBlock>(empty, "textBlock_Objectives").Text);
        }

        [WpfFact]
        public async Task The_definition_keeps_the_SAM_Tas_default_for_MaxEqualResults_which_has_no_control()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);

            Assert.Null(window.FindName("textBox_MaxEqualResults"));
            Assert.DoesNotContain(Texts(window), x => x.Contains("MaxEqualResults"));

            int expected = new OptimizationSettings().MaxEqualResults;
            Assert.True(window.Input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(expected, definition.OptimizationSettings.MaxEqualResults);
            Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), window.Input.MaxEqualResults);

            //Through other edits, an example load, and a run.
            window.Input.Parameters[0].Maximum = "30";
            Control<ComboBox>(window, "comboBox_Algorithm").SelectedItem = AlgorithmType.GPSHookeJeeves;
            Assert.Equal(expected, window.Input.TryGetDefinition(out definition, out _) ? definition.OptimizationSettings.MaxEqualResults : -1);

            Control<ComboBox>(window, "comboBox_Example").SelectedIndex = 1;
            Control<Button>(window, "button_LoadExample").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(expected, window.Input.TryGetDefinition(out definition, out _) ? definition.OptimizationSettings.MaxEqualResults : -1);

            await window.RunAsync();
            Assert.NotNull(window.Report);
            Assert.Equal(expected, window.Input.TryGetDefinition(out definition, out _) ? definition.OptimizationSettings.MaxEqualResults : -1);
        }

        [WpfFact]
        public async Task Run_switches_to_the_Run_and_Results_tab_and_Setup_stays_one_click_away()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            TabControl tabControl = Control<TabControl>(window, "tabControl_Main");
            Assert.False(window.ShowingRunAndResults);
            Assert.Equal(Visibility.Visible, Control<TextBlock>(window, "textBlock_RunEmpty").Visibility);

            Task task = window.RunAsync();

            Assert.True(window.Running);
            Assert.True(window.ShowingRunAndResults, "pressing Run must show the progress");
            Assert.Equal(Visibility.Collapsed, Control<TextBlock>(window, "textBlock_RunEmpty").Visibility);
            Assert.Equal(Visibility.Visible, Control<StackPanel>(window, "stackPanel_Run").Visibility);

            await task;

            Assert.True(window.ShowingRunAndResults);
            Assert.Equal(Visibility.Visible, Control<Border>(window, "border_Result").Visibility);

            //The footer is the same on both tabs.
            tabControl.SelectedIndex = 0;
            Assert.False(window.ShowingRunAndResults);
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);
        }

        [WpfFact]
        public async Task Diagnostics_holds_the_engine_path_the_loaded_assemblies_and_how_simulations_run()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            Expander expander = Control<Expander>(window, "expander_Diagnostics");
            TextBox textBox = Control<TextBox>(window, "textBox_Diagnostics");

            Assert.Equal("Diagnostics", expander.Header);
            Assert.False(expander.IsExpanded);
            Assert.True(Within(Control<TabItem>(window, "tabItem_Run"), expander));

            //The path is here and in no readiness line.
            Assert.Contains(TasOptimisationWorkspace.StubExecutable, textBox.Text);
            Assert.DoesNotContain(window.Checks, x => x.Title.Contains(TasOptimisationWorkspace.StubExecutable) || x.Detail.Contains(TasOptimisationWorkspace.StubExecutable));
            Assert.DoesNotContain(TasOptimisationWorkspace.StubExecutable, Control<TextBlock>(window, "textBlock_NextStep").Text);

            //The loaded assemblies, and how evaluations run.
            Assert.Contains("SAM.Math.dll", textBox.Text);
            Assert.Contains("SAM.Analytical.Tas.GenOpt.dll", textBox.Text);
            Assert.Contains("one Tas simulation at a time", textBox.Text);
            Assert.Contains("fresh folder", textBox.Text);
            Assert.Contains("retries a failed simulation once", textBox.Text);
            Assert.Contains("'name::value'", textBox.Text);
            Assert.DoesNotContain("Last run:", textBox.Text);

            await window.RunAsync();

            //After a run, the kernel's own notes join it.
            Assert.Contains("Last run: Kernel outcome: Success", textBox.Text);
            Assert.Contains(TasOptimisationWorkspace.StubExecutable, textBox.Text);
        }

        [WpfFact]
        public async Task The_trace_hides_the_search_details_until_asked()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace, TasOptimisationExample.SystemsDemoHookeJeeves);
            CheckBox checkBox = Control<CheckBox>(window, "checkBox_SearchDetails");
            DataGrid dataGrid = Control<DataGrid>(window, "dataGrid_Trace");

            Assert.Equal("Show search details", checkBox.Content);
            Assert.NotEqual(true, checkBox.IsChecked);

            await window.RunAsync();

            DataGridColumn Column(string header) => Assert.Single(dataGrid.Columns, x => (string)x.Header == header);
            Assert.NotEmpty(window.TraceRows);

            //By default: the simulation, the design variables, the objective and the recorded outputs.
            Assert.Equal(Visibility.Collapsed, Column("Iteration").Visibility);
            Assert.Equal(Visibility.Collapsed, Column("Event").Visibility);
            Assert.Equal(Visibility.Visible, Column("Simulation").Visibility);
            Assert.Equal(Visibility.Visible, Column("Setpoint").Visibility);
            Assert.Equal(Visibility.Visible, Column("Result (objective)").Visibility);
            Assert.Equal(Visibility.Visible, Column("Cost").Visibility);

            checkBox.IsChecked = true;
            Assert.Equal(Visibility.Visible, Column("Iteration").Visibility);
            Assert.Equal(Visibility.Visible, Column("Event").Visibility);

            checkBox.IsChecked = false;
            Assert.Equal(Visibility.Collapsed, Column("Iteration").Visibility);
            Assert.Equal(Visibility.Collapsed, Column("Event").Visibility);

            //The choice survives the next run's new columns.
            checkBox.IsChecked = true;
            await window.RunAsync();
            Assert.Equal(Visibility.Visible, Column("Iteration").Visibility);
        }

        [WpfFact]
        public async Task The_result_card_uses_the_engineering_headline_and_lines_and_none_of_the_kernels_bookkeeping()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.968943799848584 }, sleepMs: new Dictionary<string, int> { ["2"] = 1500 }));
            TasOptimisationWindow window = Window(workspace);

            Task task = window.RunAsync();
            await Until(() => workspace.EvaluationFolder("0002") is string folder && File.Exists(Path.Combine(folder, "stub-invocation.txt")), "evaluation 2 never started");
            window.RequestCancel();
            await task;

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal("Cancelled", Control<TextBlock>(window, "textBlock_ResultHeadline").Text);
            Assert.DoesNotContain(report.Lines, x => x.Detail.Contains("kernel", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("simulation count includes the number it had assigned", Control<TextBox>(window, "textBox_Diagnostics").Text);
        }

        /// <summary>True when <paramref name="child"/> is in <paramref name="parent"/>'s logical tree (the visual tree of a collapsed or unshown part does not exist yet).</summary>
        private static bool Within(DependencyObject parent, DependencyObject child)
        {
            for (DependencyObject current = child; current != null; current = LogicalTreeHelper.GetParent(current))
            {
                if (ReferenceEquals(current, parent))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The text a person can read in the window: text blocks, buttons, headers and check box labels, in every tab and every state. Tooltips are not included.</summary>
        private static List<string> Texts(TasOptimisationWindow window)
        {
            List<string> result = new List<string>();
            Collect(window, result);
            return result;
        }

        private static void Collect(DependencyObject dependencyObject, List<string> texts)
        {
            switch (dependencyObject)
            {
                case TextBlock textBlock:
                    texts.Add(textBlock.Text);
                    break;

                case HeaderedContentControl headeredContentControl when headeredContentControl.Header is string header:
                    texts.Add(header);
                    break;

                case ContentControl contentControl when contentControl.Content is string content:
                    texts.Add(content);
                    break;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(dependencyObject))
            {
                if (child is DependencyObject childObject)
                {
                    Collect(childObject, texts);
                }
            }
        }

        // ---- layout --------------------------------------------------------------------------------------

        [WpfFact]
        public void At_its_minimum_size_the_window_keeps_its_actions_inside_the_client_area()
        {
            TasOptimisationWindow window = new TasOptimisationWindow()
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0,
                Top = 0,
            };

            window.Width = window.MinWidth;
            window.Height = window.MinHeight;

            try
            {
                window.Show();
                window.UpdateLayout();

                Size client = Client(window);
                Button button_Close = Control<Button>(window, "button_Close");
                Point point = button_Close.TranslatePoint(new Point(button_Close.ActualWidth, button_Close.ActualHeight), (FrameworkElement)window.Content);

                Assert.True(point.Y > 0, "the window was not laid out, so this assertion would pass without testing anything");
                Assert.True(point.Y <= client.Height + 0.5, string.Format("the Close button's bottom edge is at {0}, below the client area of {1}", point.Y, client.Height));
                Assert.True(point.X <= client.Width + 0.5, string.Format("the Close button's right edge is at {0}, outside the client area of {1}", point.X, client.Width));

                Button button_Run = Control<Button>(window, "button_Run");
                Point point_Run = button_Run.TranslatePoint(new Point(button_Run.ActualWidth, button_Run.ActualHeight), (FrameworkElement)window.Content);
                Assert.True(point_Run.Y <= client.Height + 0.5);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void The_window_is_never_taller_than_the_work_area()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            Assert.True(window.Height <= SystemParameters.WorkArea.Height + 0.5);
            Assert.True(window.MinHeight <= 688, "a 1080p display at 150% scaling gives 688 DIP of work area");
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private static Size Client(System.Windows.Window window)
        {
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || !GetClientRect(handle, out RECT rect))
            {
                return new Size(double.NaN, double.NaN);
            }

            System.Windows.Media.Matrix matrix = PresentationSource.FromVisual(window).CompositionTarget.TransformFromDevice;
            return new Size((rect.Right - rect.Left) * matrix.M11, (rect.Bottom - rect.Top) * matrix.M22);
        }

        // ---- no Java route -------------------------------------------------------------------------------

        /// <summary>
        /// SAM_UI reaches GenOpt only through RunNative: the built assembly references no member of the legacy Java route
        /// (Run, the GenOpt.bat/Java executable file, the simulation command, the Java path, the Tas Manager registry).
        /// </summary>
        [Fact]
        public void SAM_UI_calls_RunNative_and_no_member_of_the_legacy_Java_route()
        {
            string path = typeof(TasOptimisationWindow).Assembly.Location;

            using FileStream fileStream = File.OpenRead(path);
            using PEReader pEReader = new PEReader(fileStream);
            MetadataReader metadataReader = pEReader.GetMetadataReader();

            List<string> members = new List<string>();
            foreach (MemberReferenceHandle memberReferenceHandle in metadataReader.MemberReferences)
            {
                MemberReference memberReference = metadataReader.GetMemberReference(memberReferenceHandle);
                if (memberReference.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                TypeReference typeReference = metadataReader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                members.Add(metadataReader.GetString(typeReference.Namespace) + "." + metadataReader.GetString(typeReference.Name) + "::" + metadataReader.GetString(memberReference.Name));
            }

            Assert.Contains("SAM.Analytical.Tas.GenOpt.GenOptDocument::RunNative", members);

            string[] forbidden =
            [
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::Run",
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::get_ExecutableFile",
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::get_Command",
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::set_Command",
                "SAM.Analytical.Tas.GenOpt.Query::TasGenOptJavaPath",
                "SAM.Analytical.Tas.GenOpt.Create::Command",
                "SAM.Core.Tas.Modify::SetProjectDirectory",
            ];

            Assert.Empty(members.Intersect(forbidden));

            // PR6: success, withholding, the best point, the running lowest and the refusal wording are SAM_Tas' rules.
            Assert.Contains("SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome::.ctor", members);
            Assert.Contains("SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome::IsLower", members);
            Assert.Contains("SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome::RefusalMessage", members);
        }

        [Fact]
        public void A_SAM_Analytical_Tas_GenOpt_without_the_shared_result_rules_is_reported_as_a_load_failure()
        {
            // The probe reaches NativeGenOptOutcome, so a pre-PR6 assembly fails it like a stale SAM.Math does.
            Assert.Null(Query.TasOptimisationAssemblyFailure());
            TypeLoadException stale = new TypeLoadException("Could not load type 'SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome'.");
            Assert.True(Query.IsTasOptimisationLoadFailure(stale));
            Assert.StartsWith("Simulate > Optimisation could not load its assemblies.", TasOptimisationReport.Message(stale));
        }
    }
}
