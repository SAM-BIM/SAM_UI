// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
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
using File = System.IO.File;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The Design Optimisation window on the "tas-model" engine (PR8), end to end without Tas: the engine choice, the
    /// model read in the background into "Can change" / "Can measure", picking items, the glazing filter, Open / Save,
    /// the AI exchange, the generated script, Test one simulation and a run - both through SAM_Tas' TasModelRunner and its
    /// StubTasGenExecute (spec from <c>SAM_TAS_GENOPT_STUB_SPEC</c>), with a stand-in inventory and glazing writer.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasModelJourneyWindowTests : IDisposable
    {
        public TasModelJourneyWindowTests()
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

        /// <summary>A window on the "tas-model" engine over <paramref name="workspace"/>, its model read through the stand-ins.</summary>
        private static async Task<TasOptimisationWindow> ModelWindow(TasOptimisationWorkspace workspace, TasModelSession? session = null, List<string>? written = null)
        {
            TasModelSession tasModelSession = session ?? TasModelJourneyFixtures.Session();
            tasModelSession.GlazingWriter = (tbd, options) => written?.AddRange(options.Select(x => Path.GetFileName(tbd) + " <- " + x.Text));

            TasOptimisationWindow window = new TasOptimisationWindow(null, null, tasModelSession, () => [TasModelJourneyFixtures.Source()]) { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SwitchEngine("tas-model");
            Control<TextBox>(window, "textBox_Directory").Text = workspace.Directory;
            await window.ReadModel(workspace.Directory);

            //The checks follow the model at the window's next refresh; refresh now.
            window.SetInput(new TasOptimisationInput(window.Input));
            return window;
        }

        private static async Task Until(Func<bool> condition, string message, int seconds = 30)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, message);
                await Task.Delay(20);
            }
        }

        private static TasModelCatalogueItem Item(IEnumerable<TasModelCatalogueItem> items, string name)
        {
            return items.Single(x => x.Name == name);
        }

        /// <summary>A glazing choice of the Demo's three options minimising cooling demand, with overheating recorded.</summary>
        private static void GlazingChoice(TasOptimisationWindow window)
        {
            window.AddTarget(Item(window.CanChangeItems, "Glazing system (Suncool Example)"));
            window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
            window.AddMeasure(Item(window.CanMeasureItems, "Overheating hours"));
        }

        // ---- engine choice -------------------------------------------------------------------------------

        [WpfFact]
        public void The_engine_choice_shows_the_project_folder_only_for_the_model_and_keeps_each_engines_form()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = new TasOptimisationWindow(null, null, TasModelJourneyFixtures.Session(), () => []) { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace.Input());

            ComboBox engine = Control<ComboBox>(window, "comboBox_Engine");
            Assert.Equal(["Tas model (no script)", "Tas script"], engine.Items.OfType<ComboBoxItem>().Select(x => x.Content));
            Assert.Equal("Tas script", ((ComboBoxItem)engine.SelectedItem).Content);
            Assert.Equal(Visibility.Visible, Control<TextBox>(window, "textBox_ScriptPath").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<StackPanel>(window, "stackPanel_TasModel").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<Button>(window, "button_TestSimulation").Visibility);
            window.Input.Parameters[0].Maximum = "30";

            //Choosing the model: no script field, the model's section, an empty definition on "tas-model"; the folder is kept.
            engine.SelectedItem = engine.Items[0];
            Assert.True(window.Input.IsTasModel);
            Assert.Equal(workspace.Directory, window.Input.Directory);
            Assert.Empty(window.Input.Parameters);
            Assert.Equal(Visibility.Collapsed, Control<TextBox>(window, "textBox_ScriptPath").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_Example").Visibility);
            Assert.Equal(Visibility.Visible, Control<StackPanel>(window, "stackPanel_TasModel").Visibility);
            Assert.Equal(Visibility.Visible, Control<Button>(window, "button_TestSimulation").Visibility);
            Assert.DoesNotContain(window.Checks, x => x.Title == "Tas script");
            Assert.Contains(window.Checks, x => x.Title == "Tas model");

            //Back to the script: the edited example is as it was, and runs as before.
            engine.SelectedItem = engine.Items[1];
            Assert.False(window.Input.IsTasModel);
            Assert.Equal("30", window.Input.Parameters[0].Maximum);
            Assert.Equal(workspace.ScriptPath, window.Input.ScriptPath);
            Assert.True(window.Checks.CanRun(), string.Join("\n", window.Checks));
        }

        [WpfFact]
        public void An_open_model_with_a_TBD_opens_on_the_model_engine_and_without_one_on_the_script_example()
        {
            using TasOptimisationWorkspace withTbd = new TasOptimisationWorkspace();
            using TasOptimisationWorkspace withoutTbd = new TasOptimisationWorkspace(tasFile: false);
            File.WriteAllText(Path.Combine(withoutTbd.Directory, "Model.t3d"), "placeholder");

            TasOptimisationWindow window = new TasOptimisationWindow(withTbd.Directory, null, TasModelJourneyFixtures.Session(), () => []);
            Assert.True(window.Input.IsTasModel);
            Assert.Equal(withTbd.Directory, window.Input.Directory);

            //Not shown, so nothing was read (Tas is started only for a window on screen).
            Assert.Equal(TasModelReadState.NotRead, window.ModelSession.State);

            TasOptimisationWindow script = new TasOptimisationWindow(withoutTbd.Directory, null, TasModelJourneyFixtures.Session(), () => []);
            Assert.False(script.Input.IsTasModel);
            Assert.Equal(withoutTbd.Directory, script.Input.Directory);
        }

        [WpfFact]
        public async Task A_window_on_screen_reads_the_model_once_per_folder_in_the_background()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            List<string> reads = new List<string>();
            TasOptimisationWindow window = new TasOptimisationWindow(workspace.Directory, null, TasModelJourneyFixtures.Session(reads: reads), () => [TasModelJourneyFixtures.Source()]) { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            try
            {
                window.Show();
                await Until(() => window.ModelSession.State == TasModelReadState.Ready && !window.ModelSession.PoolBusy, "the model was not read once the window was shown");
                await window.ModelTask;
                Assert.Equal([workspace.Directory], reads);
                Assert.Equal(TasModelReadState.Ready, window.ModelSession.State);
                Assert.StartsWith("Model.tbd, Model.tsd, Model.tpd: 2 internal conditions", Control<TextBlock>(window, "textBlock_ModelStatus").Text);

                //Refreshing again reads nothing more.
                window.SetInput(new TasOptimisationInput(window.Input));
                await window.ModelTask;
                Assert.Single(reads);
            }
            finally
            {
                window.Close();
            }
        }

        // ---- the model's lists ---------------------------------------------------------------------------

        [WpfFact]
        public async Task The_lists_show_the_models_items_with_values_ranges_units_and_glazing_options()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);

            Assert.Equal(
                ["Office Weekday heating setpoint", "Office Weekday cooling setpoint", "Steady State Heating heating setpoint", "Glazing system (Suncool Example)", "HeatPumpController setpoint (Plant Room)"],
                window.CanChangeItems.Select(x => x.Name));
            Assert.Equal("now 20 °C; suggested 16 to 24 °C", Item(window.CanChangeItems, "Office Weekday heating setpoint").Detail);
            Assert.Equal("now 3 °C; no suggested range: enter one", Item(window.CanChangeItems, "HeatPumpController setpoint (Plant Room)").Detail);

            TasModelCatalogueItem glazing = Item(window.CanChangeItems, "Glazing system (Suncool Example)");
            Assert.Equal("3 options; option 1 is the model's glazing", glazing.Detail);
            Assert.Equal(
                [
                    "1 Suncool Example: g 0.337, Ug 1.05, light 0.642 (Model (current))",
                    "2 Double B: g 0.284, Ug 1.30, light 0.797 (My glazing systems)",
                    "3 Triple low-e: g 0.369, Ug 1.00, light 0.728 (My glazing systems)",
                ],
                glazing.OptionsText.Split('\n'));

            Assert.Equal(
                ["Annual heating demand", "Annual cooling demand", "Overheating hours", "Annual plant energy", "Annual plant cost", "Annual plant CO2"],
                window.CanMeasureItems.Select(x => x.Name));
            Assert.Equal("now " + new TasOptimisationFormatter(new OptimisationDefinition()).Text(15227.840663709641, "kWh"), Item(window.CanMeasureItems, "Annual heating demand").Detail);
            Assert.Equal("now 7,363 GBP", Item(window.CanMeasureItems, "Annual plant cost").Detail.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator, ","));
            Assert.Equal("no stored result yet; in h", Item(window.CanMeasureItems, "Overheating hours").Detail);
            TasModelCatalogueItem overheating = Item(window.CanMeasureItems, "Overheating hours");
            Assert.Equal(("Threshold (°C)", "28"), (overheating.ParameterLabel, overheating.ParameterText));
        }

        [WpfFact]
        public async Task A_model_that_cannot_be_read_says_Tas_is_needed_and_blocks()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace, TasModelJourneyFixtures.FailingSession());

            Assert.Empty(window.CanChangeItems);
            Assert.StartsWith("✕ Tas could not open the model", Control<TextBlock>(window, "textBlock_ModelStatus").Text);
            Assert.Contains(window.Checks, x => x.Title == "Tas model" && x.Status == TasOptimisationCheckStatus.Blocked);
            Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            Assert.False(Control<Button>(window, "button_TestSimulation").IsEnabled);
        }

        [WpfFact]
        public async Task Picking_items_fills_the_form_and_the_Method_list_offers_only_what_can_run_them()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);
            ComboBox method = Control<ComboBox>(window, "comboBox_Algorithm");

            window.AddTarget(Item(window.CanChangeItems, "Office Weekday heating setpoint"));
            window.AddMeasure(Item(window.CanMeasureItems, "Annual heating demand"));
            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], method.ItemsSource.Cast<OptimisationAlgorithm>());
            Assert.Equal(OptimisationAlgorithm.GoldenSection, method.SelectedItem);
            Assert.True(window.Checks.CanRun(), string.Join("\n", window.Checks));
            Assert.Equal("Golden section on Office Weekday heating setpoint (16 to 24), minimising Annual heating demand; at most 2000 simulations.", window.Checks.Single(x => x.Title == "Setup").Detail);

            window.AddTarget(Item(window.CanChangeItems, "Office Weekday cooling setpoint"));
            Assert.Equal([OptimisationAlgorithm.HookeJeeves], method.ItemsSource.Cast<OptimisationAlgorithm>());
            Assert.Equal(OptimisationAlgorithm.HookeJeeves, method.SelectedItem);

            //Removing both values and adding the glazing choice: try every option only.
            foreach (TasOptimisationParameterRow row in window.Input.Parameters.ToList())
            {
                window.RemoveParameter(row);
            }

            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], method.ItemsSource.Cast<OptimisationAlgorithm>());
            window.AddTarget(Item(window.CanChangeItems, "Glazing system (Suncool Example)"));
            Assert.Equal([OptimisationAlgorithm.TryEveryOption], method.ItemsSource.Cast<OptimisationAlgorithm>());
            Assert.Equal(OptimisationAlgorithm.TryEveryOption, method.SelectedItem);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_GoldenSection").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_HookeJeeves").Visibility);
            Assert.True(window.Checks.CanRun(), string.Join("\n", window.Checks));
        }

        [WpfFact]
        public async Task The_glazing_filter_changes_the_options_and_a_bad_value_leaves_it_as_it_was()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);
            Assert.Equal(("", "", "0.3", "0.1", "8"), (Control<TextBox>(window, "textBox_GlazingMinimumG").Text, Control<TextBox>(window, "textBox_GlazingMaximumG").Text, Control<TextBox>(window, "textBox_GlazingUgAllowance").Text, Control<TextBox>(window, "textBox_GlazingLightAllowance").Text, Control<TextBox>(window, "textBox_GlazingMaximumOptions").Text));

            GlazingChoice(window);
            Assert.True(window.Checks.CanRun());

            //Up to g 0.3: Triple low-e (0.369) is no longer an option, so the listed choice is refused (OPT614).
            Control<TextBox>(window, "textBox_GlazingMaximumG").Text = "0.3";
            window.SetInput(new TasOptimisationInput(window.Input));
            Assert.Equal(0.3, window.ModelSession.GlazingFilter.MaximumG);
            Assert.Equal("2 options; option 1 is the model's glazing", Item(window.CanChangeItems, "Glazing system (Suncool Example)").Detail);
            Assert.False(window.Checks.CanRun());
            Assert.Contains("Triple low-e", window.Checks.Single(x => x.Title == "Setup").Detail);

            //Not a number: the filter is unchanged and the reason is shown.
            Control<TextBox>(window, "textBox_GlazingUgAllowance").Text = "abc";
            window.SetInput(new TasOptimisationInput(window.Input));
            Assert.Equal(0.3, window.ModelSession.GlazingFilter.UgAllowance);
            Assert.StartsWith("✕ The Ug allowance 'abc' is not a number.", Control<TextBlock>(window, "textBlock_GlazingFilter").Text);
        }

        // ---- open / save ---------------------------------------------------------------------------------

        [WpfFact]
        public async Task A_saved_definition_has_no_machine_path_and_opens_to_the_same_form()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);
            GlazingChoice(window);
            string expected = Assert.IsType<OptimisationDefinition>(window.Input.TryGetDefinition(out OptimisationDefinition? definition, out _) ? definition : null).ToJson();

            string path = Path.Combine(workspace.Directory, "choice.json");
            Assert.Null(window.SaveDefinition(path));
            string text = File.ReadAllText(path);
            Assert.Equal(expected, text);
            Assert.DoesNotContain(workspace.Directory, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Path.GetTempPath().TrimEnd('\\'), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("StubTasGenExecute", text);
            Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);

            //Opened into a fresh form on the script engine: the engine, variables, outputs and method come back; the folder stays.
            TasOptimisationWindow other = new TasOptimisationWindow(null, null, TasModelJourneyFixtures.Session(), () => []) { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            other.SetInput(workspace.Input());
            Assert.Null(other.OpenDefinition(path));
            Assert.True(other.Input.IsTasModel);
            Assert.Equal(workspace.Directory, other.Input.Directory);
            Assert.Equal(expected, Assert.IsType<OptimisationDefinition>(other.Input.TryGetDefinition(out OptimisationDefinition? reopened, out _) ? reopened : null).ToJson());
            Assert.Equal([OptimisationAlgorithm.TryEveryOption], Control<ComboBox>(other, "comboBox_Algorithm").ItemsSource.Cast<OptimisationAlgorithm>());
        }

        [WpfFact]
        public void A_file_that_is_not_a_definition_or_names_another_engine_changes_nothing()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = new TasOptimisationWindow(null, null, TasModelJourneyFixtures.Session(), () => []);
            window.SetInput(workspace.Input());
            string before = window.Input.Base!.ToJson();

            string garbage = Path.Combine(workspace.Directory, "garbage.json");
            File.WriteAllText(garbage, "{ \"schema\": \"sam.optimisation/1\", \"oops\": 1 }");
            Assert.StartsWith("The file is not an optimisation definition this version reads: ", window.OpenDefinition(garbage));

            string otherEngine = Path.Combine(workspace.Directory, "other.json");
            File.WriteAllText(otherEngine, File.ReadAllText(Path.Combine(TasModelJourneyFixtures.FixtureDirectory(), "glazing-choice.json")).Replace("\"tas-model\"", "\"energyplus\""));
            Assert.Equal("The definition is for the engine 'energyplus', which this window does not run (it runs 'tas-model' and 'tas-script').", window.OpenDefinition(otherEngine));

            Assert.False(window.Input.IsTasModel);
            Assert.Equal(before, window.Input.Base!.ToJson());

            //SAM's bound fixture opens, and the names that are not in this model are the definition's own findings.
            Assert.Null(window.OpenDefinition(Path.Combine(TasModelJourneyFixtures.FixtureDirectory(), "glazing-choice.json")));
            Assert.True(window.Input.IsTasModel);
        }

        // ---- AI exchange ---------------------------------------------------------------------------------

        [WpfFact]
        public async Task The_AI_prompt_carries_the_models_items_and_a_pasted_reply_replaces_the_form_only_when_used()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);
            Control<TextBox>(window, "textBox_AITask").Text = "lowest cooling demand by choosing the glazing";

            string prompt = window.AIPromptText();
            Assert.Contains("\"engine\": \"tas-model\"", prompt);
            Assert.Contains("lowest cooling demand by choosing the glazing", prompt);
            Assert.Contains("\"glazingConstruction\": \"Suncool Example\"", prompt);
            Assert.Contains("Triple low-e", prompt);
            Assert.DoesNotContain(workspace.Directory, prompt, StringComparison.OrdinalIgnoreCase);

            //The reply an assistant would give (built here through a second form), with a fence around it.
            TasOptimisationInput reply = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = window.ModelSession.Catalogue!;
            reply.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)"));
            reply.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            Assert.True(reply.TryGetDefinition(out OptimisationDefinition? definition, out _));
            string json = definition!.ToJson();

            Control<TextBox>(window, "textBox_AIReply").Text = "```json\n" + json + "\n```";
            Assert.NotNull(window.AIReply);
            Assert.True(Control<Button>(window, "button_UseReply").IsEnabled);
            Assert.Contains(window.AIReply!.Checks, x => x.Status == TasOptimisationCheckStatus.Warning);
            Assert.Empty(window.Input.Parameters);

            Assert.True(window.UseReply());
            Assert.Equal(json, Assert.IsType<OptimisationDefinition>(window.Input.TryGetDefinition(out OptimisationDefinition? used, out _) ? used : null).ToJson());
            Assert.Equal(workspace.Directory, window.Input.Directory);
            Assert.True(window.Checks.CanRun(), string.Join("\n", window.Checks));

            //A reply that is not a definition cannot be used.
            Control<TextBox>(window, "textBox_AIReply").Text = "Sorry, I can't.";
            Assert.False(Control<Button>(window, "button_UseReply").IsEnabled);
            Assert.False(window.UseReply());
        }

        // ---- generated script ----------------------------------------------------------------------------

        [WpfFact]
        public async Task The_generated_script_is_SAM_Tas_scripts_for_the_form()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);

            Assert.Null(window.GeneratedScript(out string? empty));
            Assert.NotNull(empty);

            GlazingChoice(window);
            string script = Assert.IsType<string>(window.GeneratedScript(out string? problem));
            Assert.Null(problem);
            Assert.Contains("using System.Linq;", script);
            Assert.Contains("\"Windows: Triple low-e a5191c -pane\"", script);
            Assert.Equal(Visibility.Visible, Control<StackPanel>(window, "stackPanel_GeneratedScript").Visibility);
        }

        // ---- test one simulation and run (stub TasGenExecute) ---------------------------------------------

        [WpfFact]
        public async Task Test_one_simulation_runs_option_1_and_estimates_the_run()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            List<string> written = new List<string>();
            TasOptimisationWindow window = await ModelWindow(workspace, written: written);
            GlazingChoice(window);
            Assert.True(Control<Button>(window, "button_TestSimulation").IsEnabled);

            using (new TasModelJourneyFixtures.StubSpec("{\"kind\":\"quadratic\",\"center\":[3],\"offset\":10,\"outputs\":[\"Y1\",\"Y2\",\"Y3\"]}"))
            {
                await window.TestAsync();
            }

            TasOptimisationTestReport report = Assert.IsType<TasOptimisationTestReport>(window.TestReport);
            Assert.True(report.Succeeded, report.ToText());
            Assert.Equal([1.0], report.Coordinates);
            Assert.Equal(14, report.Outputs[0]);
            Assert.Equal("Glazing system (Suncool Example) = 1: Suncool Example", report.Lines[0].Detail);
            Assert.StartsWith("Try every option runs one simulation per option: 3 simulations, about ", report.Estimate);
            Assert.Equal(Visibility.Visible, Control<Border>(window, "border_Test").Visibility);
            Assert.StartsWith("One Tas simulation took ", Control<TextBlock>(window, "textBlock_TestHeadline").Text);

            //The pool systems are written into the run's snapshot only, under their unique names.
            Assert.Equal(["Model.tbd <- Double B", "Model.tbd <- Triple low-e"], written);
            Assert.Equal(["0001"], workspace.EvaluationFolders());
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);
        }

        [WpfFact]
        public async Task A_glazing_choice_runs_every_option_and_the_result_names_the_options()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);
            GlazingChoice(window);

            using (new TasModelJourneyFixtures.StubSpec("{\"kind\":\"quadratic\",\"center\":[3],\"offset\":10,\"outputs\":[\"Y1\",\"Y2\",\"Y3\"]}"))
            {
                await window.RunAsync();
            }

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.True(report.Successful, report.ToText());
            Assert.Equal(OptimisationOutcome.Success, report.Outcome);
            Assert.Equal("Optimum found after 3 simulations", report.Headline);

            List<TasOptimisationTraceRow> options = window.TraceRows.Where(x => x.Entry.Event == OptimisationEvent.OptionEvaluated).ToList();
            Assert.Equal([1.0, 2.0, 3.0], options.Select(x => x.Coordinates[0]));
            Assert.Equal([14.0, 11.0, 10.0], options.Select(x => x.Outputs[0]));
            Assert.Equal(["1: Suncool Example", "2: Double B", "3: Triple low-e"], options.Select(x => x.CoordinateTexts[0]));
            Assert.Equal(["1", "2", "3"], options.Select(x => x.CoordinateRaw[0]));

            //The best is option 3, by name on the result card; the copied summary keeps the number at full precision.
            TasOptimisationCheck best = report.Lines.Single(x => x.Title == "Best design");
            Assert.Equal("Glazing system (Suncool Example) = 3: Triple low-e (simulation 3)", best.Detail);
            Assert.Equal("Glazing system (Suncool Example) = 3 (simulation 3)", best.Raw);
            Assert.Contains("Annual cooling demand", window.TraceText());
            Assert.Equal(3, workspace.EvaluationFolders().Count);
        }

        [WpfFact]
        public async Task A_controller_golden_section_runs_through_the_runner_and_a_refused_definition_never_starts()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = await ModelWindow(workspace);
            TasOptimisationParameterRow row = Assert.IsType<TasOptimisationParameterRow>(window.AddTarget(Item(window.CanChangeItems, "HeatPumpController setpoint (Plant Room)")));
            row.Minimum = "-5";
            row.Maximum = "35";
            window.AddMeasure(Item(window.CanMeasureItems, "Annual plant cost"));
            window.AddMeasure(Item(window.CanMeasureItems, "Annual plant CO2"));
            window.SetInput(new TasOptimisationInput(window.Input));
            Assert.True(window.Checks.CanRun(), string.Join("\n", window.Checks));

            using (new TasModelJourneyFixtures.StubSpec("{\"kind\":\"quadratic\",\"center\":[4.968943799848584],\"offset\":7000,\"outputs\":[\"Y1\",\"Y2\"]}"))
            {
                await window.RunAsync();
            }

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.True(report.Successful, report.ToText());
            Assert.Equal(["HeatPumpController setpoint (Plant Room)"], report.ParameterNames);
            Assert.Equal(["Annual plant cost", "Annual plant CO2"], report.ObjectiveNames);
            Assert.InRange(report.BestPoint[0], 4.5, 5.5);
            Assert.EndsWith("GBP", report.Lines.Single(x => x.Title == "Objective").Detail);

            //A name the model does not have (an edited definition): blocked, and Run starts nothing.
            int folders = workspace.EvaluationFolders().Count;
            OptimisationDefinition edited = Assert.IsType<OptimisationDefinition>(window.Input.TryGetDefinition(out OptimisationDefinition? definition, out _) ? definition : null);
            edited.Variables[0].Target.Reference[TasModelKind.ControllerKey] = "Missing controller";
            string path = Path.Combine(workspace.Directory, "edited.json");
            File.WriteAllText(path, edited.ToJson());
            Assert.Null(window.OpenDefinition(path));
            Assert.False(window.Checks.CanRun());
            Assert.Contains(window.Checks, x => x.Title == "Setup" && x.Detail.Contains("Missing controller", StringComparison.Ordinal));

            await window.RunAsync();
            Assert.Equal(folders, workspace.EvaluationFolders().Count);
        }
    }
}
