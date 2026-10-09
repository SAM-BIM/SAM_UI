// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using File = System.IO.File;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The "tas-model" journey of the Design Optimisation window below the window (PR8): the engines it offers, the
    /// methods it offers for the variables chosen, picking catalogue items into a bound definition, SAM's bound fixtures
    /// through the form, the AI exchange, the option names in the results, the duration estimate, and the readiness
    /// checks against the model's catalogue and SAM_Tas' runner. No Tas: the model is a Demo-shaped inventory.
    /// </summary>
    public class TasModelJourneyTests
    {
        private static OptimisationDefinition Definition(TasOptimisationInput input)
        {
            Assert.True(input.TryGetDefinition(out OptimisationDefinition definition, out List<string> problems), string.Join("; ", problems));
            return Assert.IsType<OptimisationDefinition>(definition);
        }

        private static List<OptimisationDiagnostic> Errors(OptimisationDefinition definition, OptimisationCatalogue? catalogue = null)
        {
            IOptimisationCapabilities capabilities = Analytical.Tas.GenOpt.Query.TasModelCapabilities();
            return (catalogue == null ? definition.Diagnostics(capabilities) : definition.Diagnostics(capabilities, catalogue)).FindAll(x => x.Severity == DiagnosticSeverity.Error);
        }

        // ---- engines and methods -------------------------------------------------------------------------

        [Fact]
        public void The_window_offers_the_model_engine_first_and_keeps_the_script_engine()
        {
            Assert.Equal(["tas-model", "tas-script"], Query.TasOptimisationEngines);
            Assert.Equal("Tas model (no script)", Query.TasOptimisationEngineName("tas-model"));
            Assert.Equal("Tas script", Query.TasOptimisationEngineName("tas-script"));
            Assert.Same(Analytical.Tas.GenOpt.Query.TasModelCapabilities(), Query.TasOptimisationCapabilities("tas-model"));
            Assert.Same(Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities(), Query.TasOptimisationCapabilities("tas-script"));

            //A new "tas-model" form: an empty definition on that engine, nothing else.
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            Assert.True(input.IsTasModel);
            Assert.Empty(input.Parameters);
            Assert.Empty(input.Objectives);
            Assert.Equal("tas-model", Definition(input).Model.Engine);
            Assert.Equal(TasOptimisationInput.NewDefinitionName, Definition(input).Name);

            //The examples are still "tas-script".
            Assert.False(TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection).IsTasModel);
        }

        [Fact]
        public void The_form_offers_only_methods_the_engine_runs_for_the_variables_chosen()
        {
            TasOptimisationParameterRow value() => new TasOptimisationParameterRow("x", "1", "0", "2", "1");
            TasOptimisationParameterRow choice() => new TasOptimisationParameterRow("g", string.Empty, "1", "3", string.Empty) { Type = DesignVariableType.Discrete };

            //"tas-script" is unchanged: both methods, whatever the variables.
            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], Query.TasOptimisationAlgorithms("tas-script", [value(), value()]));
            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], Query.TasOptimisationAlgorithms("tas-script", [choice()]));

            //"tas-model": a choice runs only by trying every option; golden section takes one value.
            Assert.Equal([OptimisationAlgorithm.TryEveryOption], Query.TasOptimisationAlgorithms("tas-model", [choice()]));
            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], Query.TasOptimisationAlgorithms("tas-model", []));
            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], Query.TasOptimisationAlgorithms("tas-model", [value()]));
            Assert.Equal([OptimisationAlgorithm.HookeJeeves], Query.TasOptimisationAlgorithms("tas-model", [value(), value()]));

            Assert.Equal("Try every option", OptimisationAlgorithm.TryEveryOption.TasOptimisationAlgorithmName());
            Assert.Equal("Try every option (one choice)", OptimisationAlgorithm.TryEveryOption.TasOptimisationAlgorithmName(true));
        }

        [Fact]
        public void A_method_the_variables_cannot_run_is_switched_to_one_they_can()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();

            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Office Weekday heating setpoint"));
            Assert.Equal(OptimisationAlgorithm.GoldenSection, input.OptimisationAlgorithm);

            //A second value: golden section takes one, so Hooke–Jeeves, which runs at once: each picked value has a start and a step.
            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Office Weekday cooling setpoint"));
            Assert.Equal(OptimisationAlgorithm.HookeJeeves, input.OptimisationAlgorithm);
            Assert.True(input.StartAndStepApplicable);
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            Assert.Empty(Errors(Definition(input), catalogue));
            Assert.Equal([1.0, 1.0], Definition(input).Variables.Select(x => x.Step!.Value));

            //Back to one value: Hooke–Jeeves is still offered and kept.
            input.Parameters.RemoveAt(1);
            Assert.False(input.EnsureOfferedAlgorithm());
            Assert.Equal(OptimisationAlgorithm.HookeJeeves, input.OptimisationAlgorithm);
        }

        // ---- the catalogue into the form -----------------------------------------------------------------

        [Fact]
        public void Picking_a_setpoint_adds_a_bound_variable_at_the_current_value_within_the_suggested_range()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();

            TasOptimisationParameterRow row = input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Office Weekday heating setpoint"));

            Assert.Equal(("Office Weekday heating setpoint", "°C", "20", "16", "24", "1"), (row.Name, row.Unit, row.Start, row.Minimum, row.Maximum, row.Step));
            Assert.Equal("Changes: Zone heating setpoint of internal condition “Office Weekday”", row.BindingText);
            Assert.False(row.IsChoice);

            OptimisationDefinition definition = Definition(input);
            DesignVariable variable = Assert.Single(definition.Variables);
            Assert.Equal(TasModelKind.HeatingSetpoint, variable.Target.Kind);
            Assert.Equal("Office Weekday", variable.Target.Reference[TasModelKind.InternalConditionKey]);
            Assert.Equal((DesignVariableType.Continuous, OptimisationQuantity.Temperature, "°C", 16.0, 24.0), (variable.Type, variable.Quantity, variable.Unit, variable.Minimum, variable.Maximum));

            //Without an output it is not runnable yet; with one it is, on the model's catalogue.
            Assert.NotEmpty(Errors(definition, catalogue));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual heating demand"));
            Assert.Empty(Errors(Definition(input), catalogue));
        }

        [Theory]
        [InlineData(16, 24, 1)]
        [InlineData(21, 28, 1)]
        [InlineData(0, 2, 0.2)]
        [InlineData(-5, 35, 5)]
        [InlineData(0, 100, 10)]
        [InlineData(0.2, 0.6, 0.05)]
        [InlineData(3, 3, 1)]
        public void A_picked_value_steps_by_about_an_eighth_of_its_range_on_a_1_2_5_scale(double minimum, double maximum, double step)
        {
            Assert.Equal(step, TasOptimisationInput.DefaultStep(minimum, maximum));
        }

        [Fact]
        public void A_controller_has_no_suggested_range_so_the_form_asks_for_one()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();

            TasOptimisationParameterRow row = input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "HeatPumpController setpoint (Plant Room)"));

            Assert.Equal(("3", string.Empty, string.Empty, "°C"), (row.Start, row.Minimum, row.Maximum, row.Unit));
            Assert.Equal("Changes: Plant controller setpoint of plant room “Plant Room”, controller “HeatPumpController”", row.BindingText);
            Assert.False(input.TryGetDefinition(out _, out List<string> problems));
            Assert.Contains(problems, x => x.Contains("minimum: '' is not a number", StringComparison.Ordinal));

            row.Minimum = "-5";
            row.Maximum = "35";
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual plant cost"));
            Assert.Empty(Errors(Definition(input), catalogue));
        }

        [Fact]
        public void Picking_the_glazing_choice_adds_a_choice_numbered_from_1_with_every_option_and_tries_every_option()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();
            OptimisationCatalogueEntry entry = TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)");

            //The model's glazing first, then the pool systems that pass the default filter, in g order.
            Assert.Equal([TasModelJourneyFixtures.Glazing, "Double B", "Triple low-e"], entry.Options);

            TasOptimisationParameterRow row = input.AddTarget(entry);
            Assert.True(row.IsChoice);
            Assert.Equal((string.Empty, "1", "3", string.Empty, string.Empty), (row.Start, row.Minimum, row.Maximum, row.Step, row.Unit));
            Assert.Equal(OptimisationAlgorithm.TryEveryOption, input.OptimisationAlgorithm);
            Assert.Equal([OptimisationAlgorithm.TryEveryOption], input.OfferedAlgorithms);
            Assert.False(input.StartAndStepApplicable);
            Assert.EndsWith("Options: 1 Suncool Example; 2 Double B; 3 Triple low-e", row.BindingText);

            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            OptimisationDefinition definition = Definition(input);
            DesignVariable variable = Assert.Single(definition.Variables);
            Assert.Equal((DesignVariableType.Discrete, 1.0, 3.0, (double?)null, (double?)null), (variable.Type, variable.Minimum, variable.Maximum, variable.Start, variable.Step));
            Assert.Equal(entry.Options, variable.Target.Options);
            Assert.IsType<TryEveryOptionMethod>(definition.Method);
            Assert.Empty(Errors(definition, catalogue));
        }

        [Fact]
        public void Picking_a_measure_binds_the_output_with_its_parameter_and_the_first_output_is_the_objective()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();

            TasOptimisationObjectiveRow cooling = input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            TasOptimisationObjectiveRow overheating = input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Overheating hours"), new Dictionary<string, double> { { "threshold", 25 } });
            TasOptimisationObjectiveRow overheating28 = input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Overheating hours"));

            Assert.True(cooling.Primary);
            Assert.False(overheating.Primary);
            Assert.Equal(("Annual cooling demand", "kWh"), (cooling.Name, cooling.Unit));
            Assert.Equal("Overheating hours (25)", overheating.Name);
            Assert.Equal("Overheating hours", overheating28.Name);
            Assert.Equal("Measures: Overheating hours (resultant temperature threshold 25 °C)", overheating.BindingText);

            OptimisationDefinition definition = Definition(input);
            Assert.Equal("Annual cooling demand", definition.Objective.Output);
            Assert.Equal(25, definition.Output("Overheating hours (25)").Measure.Parameters["threshold"]);
            Assert.Equal(28, definition.Output("Overheating hours").Measure.Parameters["threshold"]);
            Assert.Equal(OptimisationQuantity.Energy, definition.Output("Annual cooling demand").Quantity);
        }

        [Fact]
        public void A_bound_row_keeps_its_binding_when_renamed_and_a_picked_item_gets_a_unique_name()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();

            TasOptimisationParameterRow first = input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Office Weekday cooling setpoint"));
            TasOptimisationParameterRow second = input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Office Weekday cooling setpoint"));
            Assert.Equal("Office Weekday cooling setpoint 2", second.Name);
            Assert.Equal("Item 2", TasOptimisationInput.UniqueName("  ", ["item"]));

            first.Name = "Cooling";
            OptimisationDefinition definition = Definition(input);
            Assert.Equal(TasModelKind.CoolingSetpoint, definition.Variable("Cooling").Target.Kind);
            Assert.Equal(OptimisationQuantity.Temperature, definition.Variable("Cooling").Quantity);

            //The same item twice is the definition's own OPT607.
            Assert.Contains(Errors(definition), x => x.Code == "OPT607");
        }

        [Theory]
        [InlineData("glazing-choice.json")]
        [InlineData("systems-demo-bound-golden-section.json")]
        [InlineData("zone-setpoints-glazing-hooke-jeeves.json")]
        public void SAMs_bound_definitions_read_back_unchanged_through_the_form(string fileName)
        {
            string text = File.ReadAllText(Path.Combine(TasModelJourneyFixtures.FixtureDirectory(), fileName));
            OptimisationDefinition fixture = Assert.IsType<OptimisationDefinition>(global::SAM.Core.Optimisation.Create.OptimisationDefinition(text, out _));

            TasOptimisationInput input = new TasOptimisationInput();
            input.Load(fixture);

            Assert.True(input.IsTasModel);
            Assert.Equal(text, Definition(input).ToJson());

            //And through the session copy.
            Assert.Equal(text, Definition(new TasOptimisationInput(input)).ToJson());
        }

        // ---- AI exchange ---------------------------------------------------------------------------------

        [Fact]
        public void The_AI_text_offers_the_models_items_the_choice_and_try_every_option()
        {
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();

            string text = global::SAM.Core.Optimisation.Query.AIExchangeText(Definition(input), Query.TasOptimisationCapabilities("tas-model"), catalogue, "lowest cooling demand");

            Assert.Contains("\"engine\": \"tas-model\"", text);
            Assert.Contains("{ \"kind\": \"tbd.internal-condition.heating-setpoint\", \"reference\": { \"internalCondition\": \"Office Weekday\" } }", text);
            Assert.Contains("try-every-option", text);
            Assert.Contains("Double B", text);
            Assert.Contains("lowest cooling demand", text);
            Assert.DoesNotContain(TasOptimisationWorkspace.StubExecutable, text);
        }

        [Fact]
        public void A_pasted_reply_is_read_strictly_and_its_findings_are_shown_before_it_is_used()
        {
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)"));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            string json = Definition(input).ToJson();

            //An assistant that adds a fence and a sentence anyway: read, with the extraction warning, and usable.
            TasOptimisationAIReply fenced = new TasOptimisationAIReply("Here it is:\n```json\n" + json + "\n```", "tas-model", catalogue);
            Assert.True(fenced.CanUse);
            Assert.Equal(0, fenced.Errors);
            Assert.Contains(fenced.Checks, x => x.Status == TasOptimisationCheckStatus.Warning);
            Assert.Equal(json, fenced.Definition!.ToJson());
            Assert.StartsWith("The reply holds a definition with 1 design variable(s) and 1 output(s), with warnings", fenced.Summary);

            //An invented model item: readable (it can still be corrected in the form), and the reason is shown.
            TasOptimisationAIReply invented = new TasOptimisationAIReply(json.Replace("\"Triple low-e\"", "\"Triple low-e X\""), "tas-model", catalogue);
            Assert.True(invented.CanUse);
            Assert.Contains(invented.Diagnostics, x => x.Code == "OPT614");
            Assert.Contains(invented.Checks, x => x.Status == TasOptimisationCheckStatus.Blocked && x.Detail.Contains("Triple low-e X", StringComparison.Ordinal));

            //Not a definition: nothing to use.
            TasOptimisationAIReply prose = new TasOptimisationAIReply("I cannot do that.", "tas-model", catalogue);
            Assert.False(prose.CanUse);
            Assert.StartsWith("The reply could not be read", prose.Summary);
            Assert.Contains(prose.Checks, x => x.Status == TasOptimisationCheckStatus.Blocked);
        }

        // ---- results and estimate ------------------------------------------------------------------------

        [Fact]
        public void A_choice_is_shown_by_its_option_number_and_name()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();
            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)"));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));

            TasOptimisationFormatter formatter = new TasOptimisationFormatter(Definition(input), CultureInfo.InvariantCulture);
            TasOptimisationColumn column = formatter.Variables[0];

            Assert.Equal("1: Suncool Example", formatter.Text(column, 1));
            Assert.Equal("3: Triple low-e", formatter.Text(column, 3));
            Assert.Equal("4", formatter.Text(column, 4));
            Assert.Equal("—", formatter.Text(column, double.NaN));
            Assert.Equal("Glazing system (Suncool Example) = 2: Double B", formatter.Pairs(formatter.Variables, [2.0]));
            Assert.Equal("Glazing system (Suncool Example)", formatter.Header()[4]);
        }

        [Fact]
        public void The_estimate_is_exact_for_a_choice_and_a_range_for_a_search()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();
            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)"));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));

            Assert.Equal("Try every option runs one simulation per option: 3 simulations, about 21.6 s.", TasOptimisationTestReport.EstimateText(Definition(input), TimeSpan.FromSeconds(7.2), 2000));

            TasOptimisationInput search = TasOptimisationInput.CreateTasModel();
            search.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Office Weekday heating setpoint"));
            search.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual heating demand"));
            Assert.Equal(
                "About 21.0 s per simulation. Golden section stops when it has converged, so the number of simulations is not known in advance: 10 simulations take about 3 min 30 s, 50 about 17 min 30 s; the limit of 2000 simulations would take about 11 h 40 min.",
                TasOptimisationTestReport.EstimateText(Definition(search), TimeSpan.FromSeconds(21), 2000));

            Assert.Equal("0.0 s", TasOptimisationTestReport.DurationText(TimeSpan.FromSeconds(-1)));
            Assert.Equal("59.9 s", TasOptimisationTestReport.DurationText(TimeSpan.FromSeconds(59.94)));
            Assert.Equal("1 min 0 s", TasOptimisationTestReport.DurationText(TimeSpan.FromSeconds(60)));
            Assert.Equal("1 h 0 min", TasOptimisationTestReport.DurationText(TimeSpan.FromMinutes(60)));
        }

        [Fact]
        public void A_test_report_shows_the_point_the_outputs_with_units_and_full_precision_in_the_tooltip()
        {
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            OptimisationCatalogue catalogue = TasModelJourneyFixtures.Catalogue();
            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)"));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            OptimisationDefinition definition = Definition(input);

            TasOptimisationTestReport report = new TasOptimisationTestReport([1.0], true, [2978.5325259896517], null, TimeSpan.FromSeconds(7), @"C:\runs\0001", definition, new TasOptimisationFormatter(definition, CultureInfo.InvariantCulture), 2000);

            Assert.Equal("One Tas simulation took 7.0 s", report.Headline);
            Assert.Equal(["Design tested", "Outputs", "Run estimate", "Simulation folder"], report.Lines.Select(x => x.Title));
            Assert.Equal("Glazing system (Suncool Example) = 1: Suncool Example", report.Lines[0].Detail);
            Assert.Equal("Annual cooling demand = 2,978.5 kWh", report.Lines[1].Detail);
            Assert.Equal("Annual cooling demand = 2978.5325259896517", report.Lines[1].ToolTipText);
            Assert.Contains("Annual cooling demand = 2978.5325259896517", report.ToText());

            TasOptimisationTestReport failed = new TasOptimisationTestReport([1.0], false, null, "Tas could not open the TBD.", TimeSpan.FromSeconds(3), null, definition, new TasOptimisationFormatter(definition), 2000);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, failed.Status);
            Assert.Null(failed.Estimate);
            Assert.Contains(failed.Lines, x => x.Detail == "Tas could not open the TBD.");
        }

        // ---- readiness -----------------------------------------------------------------------------------

        [Fact]
        public async Task The_model_engine_needs_the_model_read_and_has_no_script_line()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            input.Directory = workspace.Directory;

            List<TasOptimisationCheck> notRead = input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, new TasModelSession());
            Assert.Equal(["Tas project", "Tas model", "Tas optimisation engine", "Setup"], notRead.Select(x => x.Title));
            Assert.Equal(TasOptimisationCheckStatus.Blocked, notRead[1].Status);
            Assert.StartsWith("Not read yet.", notRead[1].Detail);
            Assert.Equal("Add what may change (Can change) and what to measure (Can measure) from the model's lists, or paste an AI assistant's reply.", notRead[3].Detail);

            TasModelSession failing = TasModelJourneyFixtures.FailingSession();
            await failing.ReadAsync(workspace.Directory);
            TasOptimisationCheck model = input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, failing)[1];
            Assert.Equal(TasOptimisationCheckStatus.Blocked, model.Status);
            Assert.StartsWith("Tas could not open the model: Class not registered", model.Detail);
            Assert.EndsWith("Tas (with a licence) must be installed on this computer to read the model and to run the optimisation.", model.Detail);

            //A session of another folder is not this model.
            TasModelSession other = await TasModelJourneyFixtures.ReadSession(Path.GetTempPath());
            Assert.StartsWith("Not read yet.", input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, other)[1].Detail);
        }

        [Fact]
        public async Task A_runnable_choice_is_ready_with_its_summary_and_a_name_not_in_the_model_blocks()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasModelSession session = await TasModelJourneyFixtures.ReadSession(workspace.Directory);
            OptimisationCatalogue catalogue = Assert.IsType<OptimisationCatalogue>(session.Catalogue);

            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            input.Directory = workspace.Directory;
            input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "Glazing system (Suncool Example)"));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual cooling demand"));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Overheating hours"));

            List<TasOptimisationCheck> checks = input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, session);
            Assert.True(checks.CanRun(), string.Join("\n", checks));
            Assert.Equal("Model.tbd, Model.tsd, Model.tpd: 2 internal conditions, 1 glazing construction, 1 plant room with 1 controller. Glazing pool: 4 systems.", checks[1].Detail);
            Assert.Equal("Try every option on Glazing system (Suncool Example) (3 options), minimising Annual cooling demand; recording Overheating hours; at most 2000 simulations.", checks.Single(x => x.Title == "Setup").Detail);

            //A tighter glazing filter: the option the form lists is no longer offered (OPT614), so the run is blocked.
            session.GlazingFilter = new TasGlazingFilter() { MaximumG = 0.3 };
            List<TasOptimisationCheck> filtered = input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, session);
            Assert.False(filtered.CanRun());
            Assert.Contains("Triple low-e", filtered.Single(x => x.Title == "Setup").Detail);
        }

        [Fact]
        public async Task The_session_builds_the_catalogue_from_the_model_the_pool_and_the_filter()
        {
            TasModelSession session = TasModelJourneyFixtures.Session();
            List<string> states = new List<string>();
            session.Changed += (s, e) => states.Add(session.State.ToString());

            Assert.Null(session.Catalogue);
            await session.ReadAsync(Path.GetTempPath());
            Assert.Equal(TasModelReadState.Ready, session.State);
            Assert.Contains("Reading", states);

            //No pool yet: no glazing choice.
            Assert.DoesNotContain(session.Catalogue!.Variables, x => x.Target.Kind == TasModelKind.GlazingChoice);

            //The pool, from two sources: a Guid counts once.
            await session.AddGlazingSourcesAsync([TasModelJourneyFixtures.Source(), TasModelJourneyFixtures.Source("Default library")]);
            Assert.Equal(4, session.Pool.Count);
            Assert.Equal(2, session.Sources.Count);
            Assert.False(session.PoolBusy);
            Assert.Equal(3, session.Catalogue!.Variables.Single(x => x.Target.Kind == TasModelKind.GlazingChoice).Options.Count);

            //The filter rebuilds the options; the runner gets the same pool and filter.
            session.GlazingFilter = new TasGlazingFilter() { MaximumOptions = 2 };
            Assert.Equal(2, session.Catalogue!.Variables.Single(x => x.Target.Kind == TasModelKind.GlazingChoice).Options.Count);
            TasModelRunSettings settings = session.RunSettings(Path.GetTempPath(), null, null);
            Assert.Same(session.Inventory, settings.Inventory);
            Assert.Same(session.GlazingFilter, settings.GlazingFilter);
            Assert.Equal(4, settings.GlazingPool.Count());

            session.Reset();
            Assert.Equal((TasModelReadState.NotRead, (OptimisationCatalogue?)null), (session.State, session.Catalogue));
            Assert.Equal(4, session.Pool.Count);
        }
    }
}
