// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using File = System.IO.File;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Simulate &gt; Optimisation's form (<see cref="TasOptimisationInput"/>) over a SAM.Core.Optimisation definition, and
    /// what SAM_Tas builds from it (<c>ToGenOptDocument</c>). Defaults come from the SAM_Tas objects, the examples are the
    /// SAM fixtures and reproduce the proven Systems Demo inputs exactly as the SAM_Tas Grasshopper component builds them,
    /// the form keeps every non-form field of its base definition, and every rule beyond "is this a number" is
    /// SAM.Core.Optimisation's or SAM_Tas'.
    /// </summary>
    public class TasOptimisationInputTests
    {
        private static OptimisationDefinition Definition(TasOptimisationInput input)
        {
            Assert.True(input.TryGetDefinition(out OptimisationDefinition definition, out List<string> problems), string.Join("; ", problems));
            return Assert.IsType<OptimisationDefinition>(definition);
        }

        private static List<OptimisationDiagnostic> Errors(OptimisationDefinition definition)
        {
            return definition.Diagnostics(Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities()).FindAll(x => x.Severity == DiagnosticSeverity.Error);
        }

        private static NumberParameter Parameter(GenOptDocument document)
        {
            return Assert.IsType<NumberParameter>(Assert.Single(document.CommandFile.Parameters));
        }

        // ---- defaults and examples ------------------------------------------------------------------------

        [Fact]
        public void The_method_and_stopping_defaults_are_read_from_the_SAM_Tas_objects()
        {
            TasOptimisationInput input = new TasOptimisationInput();

            GoldenSectionAlgorithm goldenSectionAlgorithm = new GoldenSectionAlgorithm();
            GPSHookeJeevesAlgorithm gPSHookeJeevesAlgorithm = new GPSHookeJeevesAlgorithm();
            OptimizationSettings optimizationSettings = new OptimizationSettings();

            Assert.Null(input.Base);
            Assert.Equal(TasOptimisationInput.Text(goldenSectionAlgorithm.AbsDiffFunction), input.Tolerance);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.MeshSizeDivider), input.StepReductionFactor);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.InitialMeshSizeExponent), input.InitialStepExponent);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.MeshSizeExponentIncrement), input.StepExponentIncrement);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.NumberOfStepReduction), input.StepReductions);
            Assert.Equal(optimizationSettings.MaxIterations.ToString(CultureInfo.InvariantCulture), input.MaximumSimulations);

            //The values as they read today, so a change in SAM_Tas is noticed here.
            Assert.Equal(["0.1", "2", "0", "1", "4", "2000"], new[] { input.Tolerance, input.StepReductionFactor, input.InitialStepExponent, input.StepExponentIncrement, input.StepReductions, input.MaximumSimulations });
        }

        [Fact]
        public void Only_the_methods_the_Tas_engine_runs_are_offered()
        {
            Assert.Equal([OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves], TasOptimisationInput.OptimisationAlgorithms);
            Assert.Equal(TasOptimisationInput.OptimisationAlgorithms, Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities().Algorithms.Select(x => x.Algorithm));
        }

        [Fact]
        public void The_window_opens_on_the_Systems_Demo_golden_section_example_with_no_folder_or_script()
        {
            Assert.Equal(TasOptimisationExample.SystemsDemoGoldenSection, TasOptimisationInput.DefaultExample);

            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationInput.DefaultExample);

            Assert.Equal(OptimisationAlgorithm.GoldenSection, input.OptimisationAlgorithm);
            Assert.Equal(string.Empty, input.Directory);
            Assert.Equal(string.Empty, input.ScriptPath);
            Assert.Equal(string.Empty, input.RunsDirectory);
            Assert.Equal("0.1", input.Tolerance);
            Assert.Equal("2000", input.MaximumSimulations);

            TasOptimisationParameterRow row = Assert.Single(input.Parameters);
            Assert.Equal(("Setpoint", "3", "-5", "35", "1"), (row.Name, row.Start, row.Minimum, row.Maximum, row.Step));
            Assert.Equal(("Setpoint of the HeatPumpController in plant room 1", string.Empty), (row.Description, row.Unit));
            Assert.False(row.StartAndStepApplicable);

            Assert.Equal(["Result", "Cost", "CO2"], input.Objectives.Select(x => x.Name));
            Assert.Equal(["GBP", "GBP", string.Empty], input.Objectives.Select(x => x.Unit));
            Assert.Equal(["Annual cost; the script's objective value (equal to Cost)", "Annual cost", "Annual CO2"], input.Objectives.Select(x => x.Description));
            Assert.Equal("Result", input.PrimaryObjective?.Name);
            Assert.Single(input.Objectives, x => x.Primary);

            Assert.Equal("Systems Demo – heat pump controller setpoint (golden section)", input.Base?.Name);
        }

        [Fact]
        public void The_Hooke_Jeeves_example_has_its_proven_start_and_step_and_applies_them()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);

            Assert.Equal(OptimisationAlgorithm.HookeJeeves, input.OptimisationAlgorithm);
            TasOptimisationParameterRow row = Assert.Single(input.Parameters);
            Assert.Equal(("Setpoint", "10", "-5", "35", "2"), (row.Name, row.Start, row.Minimum, row.Maximum, row.Step));
            Assert.True(row.StartAndStepApplicable);
            Assert.Equal(["2", "0", "1", "4"], new[] { input.StepReductionFactor, input.InitialStepExponent, input.StepExponentIncrement, input.StepReductions });
            Assert.Equal("Result", input.PrimaryObjective?.Name);
        }

        [Fact]
        public void Loading_an_example_keeps_the_users_folder_script_and_runs_folder()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Directory = @"C:\Projects\A";
            input.ScriptPath = @"C:\Projects\A\Script.txt";
            input.RunsDirectory = @"C:\R";
            input.Parameters[0].Name = "Changed";

            input.Load(TasOptimisationExample.SystemsDemoHookeJeeves);

            Assert.Equal((@"C:\Projects\A", @"C:\Projects\A\Script.txt", @"C:\R"), (input.Directory, input.ScriptPath, input.RunsDirectory));
            Assert.Equal("Setpoint", Assert.Single(input.Parameters).Name);
            Assert.Equal(OptimisationAlgorithm.HookeJeeves, input.OptimisationAlgorithm);
            Assert.Equal("Systems Demo – heat pump controller setpoint (Hooke–Jeeves)", input.Base?.Name);
        }

        [Fact]
        public void A_copy_is_independent_of_the_original()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            TasOptimisationInput copy = new TasOptimisationInput(input);

            copy.Parameters[0].Minimum = "0";
            copy.Parameters[0].Unit = "K";
            copy.Objectives[1].Primary = true;
            copy.Directory = "x";
            copy.Base.Name = "changed";
            copy.Base.Variables[0].Description = "changed";

            Assert.Equal("-5", input.Parameters[0].Minimum);
            Assert.Equal(string.Empty, input.Parameters[0].Unit);
            Assert.False(input.Objectives[1].Primary);
            Assert.Equal(string.Empty, input.Directory);
            Assert.Equal("Systems Demo – heat pump controller setpoint (golden section)", input.Base?.Name);
            Assert.Equal("Setpoint of the HeatPumpController in plant room 1", input.Base?.Variables[0].Description);
        }

        // ---- the examples are the SAM fixtures -----------------------------------------------------------

        /// <summary>The SAM fixture folder in the sibling SAM checkout (SAM/SAM/SAM.Tests/Golden/Optimisation), as SAM_Tas' GenOpt tests read it.</summary>
        private static string FixtureDirectory()
        {
            for (DirectoryInfo directoryInfo = new DirectoryInfo(AppContext.BaseDirectory); directoryInfo != null; directoryInfo = directoryInfo.Parent)
            {
                string path = Path.Combine(directoryInfo.FullName, "SAM", "SAM", "SAM.Tests", "Golden", "Optimisation");
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            throw new DirectoryNotFoundException("The sibling SAM checkout (SAM/SAM/SAM.Tests/Golden/Optimisation) was not found above " + AppContext.BaseDirectory + ".");
        }

        [Theory]
        [InlineData(TasOptimisationExample.SystemsDemoGoldenSection, "systems-demo-golden-section.json")]
        [InlineData(TasOptimisationExample.SystemsDemoHookeJeeves, "systems-demo-hooke-jeeves.json")]
        public void Each_example_is_byte_for_byte_the_SAM_fixture(TasOptimisationExample tasOptimisationExample, string fileName)
        {
            Assert.Equal(fileName, tasOptimisationExample.TasOptimisationExampleFileName());

            using Stream stream = Assert.IsAssignableFrom<Stream>(typeof(TasOptimisationInput).Assembly.GetManifestResourceStream(tasOptimisationExample.TasOptimisationExampleResourceName()));
            using MemoryStream memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            byte[] expected = File.ReadAllBytes(Path.Combine(FixtureDirectory(), fileName));
            Assert.Equal(expected, memoryStream.ToArray());

            //And it is what the form loads: read by the strict reader, runnable on Tas.
            Assert.Equal(File.ReadAllText(Path.Combine(FixtureDirectory(), fileName)), tasOptimisationExample.TasOptimisationExampleText());
            Assert.Empty(Errors(tasOptimisationExample.TasOptimisationExampleDefinition()));
        }

        // ---- form <-> definition -------------------------------------------------------------------------

        [Theory]
        [InlineData(TasOptimisationExample.SystemsDemoGoldenSection)]
        [InlineData(TasOptimisationExample.SystemsDemoHookeJeeves)]
        public void Loading_an_example_and_reading_it_back_gives_the_example_unchanged(TasOptimisationExample tasOptimisationExample)
        {
            OptimisationDefinition example = tasOptimisationExample.TasOptimisationExampleDefinition();

            TasOptimisationInput input = new TasOptimisationInput();
            input.Load(example);
            OptimisationDefinition definition = Definition(input);

            Assert.NotSame(example, definition);
            Assert.Equal(example.ToJson(), definition.ToJson());

            //The canonical text is the fixture's text: nothing is added, dropped or reformatted.
            Assert.Equal(tasOptimisationExample.TasOptimisationExampleText(), definition.ToJson());

            //Through a copy (the session memory) as well.
            Assert.Equal(example.ToJson(), Definition(new TasOptimisationInput(input)).ToJson());
        }

        [Fact]
        public void The_fields_the_form_does_not_show_survive_edits()
        {
            OptimisationDefinition definition_Base = TasOptimisationExample.SystemsDemoHookeJeeves.TasOptimisationExampleDefinition();
            definition_Base.Notes = "Owner notes";
            definition_Base.Model.Description = "A model description";
            definition_Base.Variables[0].Quantity = OptimisationQuantity.Temperature;
            definition_Base.Variables[0].Unit = "°C";
            definition_Base.Outputs[2].Aggregation = "annual-total";
            definition_Base.Constraints.Add(new OptimisationConstraint() { Output = "CO2", AtMost = 1000 });

            TasOptimisationInput input = new TasOptimisationInput();
            input.Load(definition_Base);
            Assert.Equal("°C", input.Parameters[0].Unit);

            //Edits to what the form shows: a bound, a description, the method's setting, the objective.
            input.Parameters[0].Maximum = "30";
            input.Parameters[0].Description = "Heating setpoint";
            input.StepReductions = "5";
            input.Objectives[0].Primary = false;
            input.Objectives[1].Primary = true;

            OptimisationDefinition definition = Definition(input);

            Assert.Equal((definition_Base.Name, definition_Base.Description, "Owner notes"), (definition.Name, definition.Description, definition.Notes));
            Assert.Equal(("tas-script", "A model description"), (definition.Model?.Engine, definition.Model?.Description));
            Assert.Equal((DesignVariableType.Continuous, OptimisationQuantity.Temperature, "°C"), (definition.Variables[0].Type, definition.Variables[0].Quantity, definition.Variables[0].Unit));
            Assert.Equal((30.0, "Heating setpoint"), (definition.Variables[0].Maximum, definition.Variables[0].Description));
            Assert.Equal([OptimisationQuantity.Currency, OptimisationQuantity.Currency, OptimisationQuantity.Unspecified], definition.Outputs.Select(x => x.Quantity));
            Assert.Equal("annual-total", definition.Outputs[2].Aggregation);
            Assert.Equal(("Cost", ObjectiveSense.Minimise), (definition.Objective?.Output, definition.Objective?.Sense));
            Assert.Equal(5, Assert.IsType<HookeJeevesMethod>(definition.Method).StepReductions);

            //Constraints are kept as they are (the Tas engine refuses them: OPT414, which the Setup check shows).
            OptimisationConstraint constraint = Assert.Single(definition.Constraints);
            Assert.Equal(("CO2", 1000.0), (constraint.Output, constraint.AtMost));
            Assert.Contains(Errors(definition), x => x.Code == "OPT414");

            //The base itself is never changed by the form.
            Assert.Equal(35.0, input.Base?.Variables[0].Maximum);
            Assert.Equal("Result", input.Base?.Objective?.Output);
        }

        [Fact]
        public void A_declared_quantity_follows_its_unit_and_an_unmatched_name_starts_with_none()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);

            //Changing Cost's unit drops its declared quantity (currency): the form cannot show or change it, so a stale
            //quantity would make the new unit an error the user cannot fix.
            input.Objectives[1].Unit = "kWh";
            //Renaming an output: its non-form fields belonged to the old name.
            input.Objectives[0].Name = "Total";

            OptimisationDefinition definition = Definition(input);

            Assert.Equal(["Total", "Cost", "CO2"], definition.Outputs.Select(x => x.Name));
            Assert.Equal([OptimisationQuantity.Unspecified, OptimisationQuantity.Unspecified, OptimisationQuantity.Unspecified], definition.Outputs.Select(x => x.Quantity));
            Assert.Equal(["GBP", "kWh", null], definition.Outputs.Select(x => x.Unit));
            Assert.Equal("Total", definition.Objective?.Output);
            Assert.Empty(Errors(definition));
        }

        [Fact]
        public void Settings_a_definition_leaves_to_the_engine_stay_left_to_the_engine()
        {
            OptimisationDefinition definition_Base = TasOptimisationExample.SystemsDemoGoldenSection.TasOptimisationExampleDefinition();
            definition_Base.Method = new GoldenSectionMethod();
            definition_Base.Stopping = null;

            TasOptimisationInput input = new TasOptimisationInput();
            input.Load(definition_Base);

            //The form shows what SAM_Tas will use...
            Assert.Equal(("0.1", "2000"), (input.Tolerance, input.MaximumSimulations));

            //...and reads back the definition as it was.
            OptimisationDefinition definition = Definition(input);
            Assert.Equal(definition_Base.ToJson(), definition.ToJson());
            Assert.Null(Assert.IsType<GoldenSectionMethod>(definition.Method).Tolerance);
            Assert.Null(definition.Stopping);

            //A value the user changes is written.
            input.Tolerance = "0.05";
            input.MaximumSimulations = "50";
            definition = Definition(input);
            Assert.Equal(0.05, Assert.IsType<GoldenSectionMethod>(definition.Method).Tolerance);
            Assert.Equal(50, definition.Stopping?.MaximumSimulations);
        }

        [Fact]
        public void A_form_never_loaded_reads_as_a_tas_script_definition_minimising_its_objective()
        {
            TasOptimisationInput input = new TasOptimisationInput();
            input.Parameters.Add(new TasOptimisationParameterRow("X", string.Empty, "0", "1", string.Empty));
            input.Objectives.Add(new TasOptimisationObjectiveRow("Result", true));

            OptimisationDefinition definition = Definition(input);

            Assert.Equal(Analytical.Tas.GenOpt.Query.TasOptimisationEngine, definition.Model?.Engine);
            Assert.Equal(("Result", ObjectiveSense.Minimise), (definition.Objective?.Output, definition.Objective?.Sense));
            Assert.Empty(Errors(definition));
        }

        // ---- the examples reproduce the proven inputs ----------------------------------------------------

        [Theory]
        [InlineData(TasOptimisationExample.SystemsDemoGoldenSection)]
        [InlineData(TasOptimisationExample.SystemsDemoHookeJeeves)]
        public void Each_example_builds_exactly_the_GenOpt_files_of_the_proven_Systems_Demo_run(TasOptimisationExample tasOptimisationExample)
        {
            const string script = "// script";

            //As SAM_Tas_Grasshopper#11's acceptance built it: the algorithm component with its defaults, the panels
            //"Setpoint,3,-5,35,1" (golden section) / "Setpoint,10,-5,35,2" (Hooke-Jeeves) and "Result / Cost / CO2".
            GenOptDocument reference = new GenOptDocument(@"C:\ws");
            bool goldenSection = tasOptimisationExample == TasOptimisationExample.SystemsDemoGoldenSection;
            reference.Algorithm = goldenSection ? new GoldenSectionAlgorithm() : new GPSHookeJeevesAlgorithm();
            reference.AddScript(script);
            foreach (string name in new[] { "Result", "Cost", "CO2" })
            {
                reference.AddObjective(new Objective(name));
            }

            reference.AddParameter(goldenSection
                ? new NumberParameter() { Name = "Setpoint", Initial = 3, Min = -5, Max = 35, Step = 1 }
                : new NumberParameter() { Name = "Setpoint", Initial = 10, Min = -5, Max = 35, Step = 2 });

            OptimisationDefinition definition = Definition(TasOptimisationInput.Create(tasOptimisationExample));
            GenOptDocument document = definition.ToGenOptDocument(@"C:\ws", script);

            Assert.Equal(reference.CommandFile.Text, document.CommandFile.Text);
            Assert.Equal(reference.ParameterFile.Text, document.ParameterFile.Text);
            Assert.Equal(reference.TemplateFile.Text, document.TemplateFile.Text);
            Assert.Equal(reference.OutputFile.Text, document.OutputFile.Text);
            Assert.Equal(reference.ConfigFile.Text, document.ConfigFile.Text);
            Assert.Equal(reference.ScriptFile.Text, document.ScriptFile.Text);
            Assert.Equal(new OptimizationSettings().MaxEqualResults, document.OptimizationSettings.MaxEqualResults);

            //And it is what SAM_Tas will run: it passes SAM_Tas' own gate, with the objective first.
            Query.TasOptimisationGate(definition, @"C:\ws", script);
            Assert.Equal(["Result", "Cost", "CO2"], definition.TasOptimisationObjectiveNames());
            Assert.Equal(["Setpoint"], definition.TasOptimisationVariableNames());
        }

        // ---- golden section: Start and Step do not apply -------------------------------------------------

        [Fact]
        public void Golden_section_excludes_Start_and_Step_from_validation_and_passes_valid_values_through()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);

            OptimisationDefinition definition = Definition(input);
            Assert.Equal((3.0, 1.0), (definition.Variables[0].Start, definition.Variables[0].Step));
            NumberParameter numberParameter = Parameter(definition.ToGenOptDocument(@"C:\ws", "s"));
            Assert.Equal((3.0, -5.0, 35.0, 1.0), (numberParameter.Initial, numberParameter.Min, numberParameter.Max, numberParameter.Step));

            input.Parameters[0].Start = "not a number";
            input.Parameters[0].Step = string.Empty;

            definition = Definition(input);
            Assert.Equal(((double?)null, (double?)null), (definition.Variables[0].Start, definition.Variables[0].Step));
            Assert.Empty(Errors(definition));

            //Left out, SAM_Tas runs them as the lower bound and 0: finite, as it requires, and inert.
            numberParameter = Parameter(definition.ToGenOptDocument(@"C:\ws", "s"));
            Assert.Equal((-5.0, 0.0), (numberParameter.Initial, numberParameter.Step));
        }

        [Theory]
        [InlineData("50", "1")]
        [InlineData("-6", "1")]
        [InlineData("3", "0")]
        [InlineData("3", "-2")]
        public void Golden_section_never_blocks_on_a_start_or_step_it_does_not_use(string start, string step)
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Parameters[0].Start = start;
            input.Parameters[0].Step = step;

            OptimisationDefinition definition = Definition(input);

            //A value shown as "not used" that the definition would refuse (OPT205 outside the range, OPT206 not above 0) is left out.
            Assert.Empty(Errors(definition));
            Assert.True(definition.Variables[0].Start == null || definition.Variables[0].Start == 3.0);
            Assert.True(definition.Variables[0].Step == null || definition.Variables[0].Step == 1.0);
            Assert.False(definition.Variables[0].Start == 3.0 && definition.Variables[0].Step == 1.0);
        }

        [Fact]
        public void A_golden_section_start_or_step_left_out_runs_the_same_points_and_only_the_step_field_says_so()
        {
            //Variables.txt is "name,value,minimum,maximum,step,type": the start is not written, the step is. Golden section
            //uses only the bounds, so leaving the start out changes no byte, and leaving the step out changes only the step
            //field (0, which the form also wrote before PR5a for an empty or non-numeric step). The examples keep 3/1.
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            string script = File.ReadAllText(workspace.ScriptPath);

            List<string> Variables(OptimisationDefinition definition, string name)
            {
                NativeGenOptRun run = definition.ToGenOptDocument(workspace.Directory, script).RunNative(Path.Combine(workspace.Directory, name), TasOptimisationWorkspace.StubExecutable);
                return Directory.GetDirectories(run.Workspace.EvaluationsDirectory).OrderBy(x => x, StringComparer.Ordinal).Select(x => File.ReadAllText(Path.Combine(x, "Variables.txt"))).ToList();
            }

            OptimisationDefinition example = Definition(workspace.Input());
            OptimisationDefinition noStart = Definition(workspace.Input());
            noStart.Variables[0].Start = null;
            OptimisationDefinition noStep = Definition(workspace.Input());
            noStep.Variables[0].Step = null;

            List<string> variables_Example = Variables(example, "example");
            Assert.True(variables_Example.Count > 2);
            Assert.All(variables_Example, x => Assert.EndsWith(",-5,35,1,System.Double", x.TrimEnd()));

            Assert.Equal(variables_Example, Variables(noStart, "no-start"));
            Assert.Equal(variables_Example.Select(x => x.Replace(",35,1,System.Double", ",35,0,System.Double")), Variables(noStep, "no-step"));
        }

        [Fact]
        public void Pattern_search_validates_Start_and_Step()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters[0].Start = "ten";

            Assert.False(input.TryGetDefinition(out OptimisationDefinition definition, out List<string> problems));
            Assert.Null(definition);
            Assert.Contains(problems, x => x.Contains("Design variable 1 (Setpoint) start") && x.Contains("'ten' is not a number"));
        }

        [Fact]
        public void Pattern_search_without_a_start_is_refused_by_the_definition_with_its_own_message()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters[0].Start = " ";

            OptimisationDefinition definition = Definition(input);

            Assert.Null(definition.Variables[0].Start);
            Assert.Equal("OPT406", Assert.Single(Errors(definition)).Code);
        }

        [Fact]
        public void Switching_the_method_updates_whether_Start_and_Step_apply()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            Assert.False(input.Parameters[0].StartAndStepApplicable);

            input.OptimisationAlgorithm = OptimisationAlgorithm.HookeJeeves;
            input.UpdateApplicability();
            Assert.True(input.Parameters[0].StartAndStepApplicable);

            //The Hooke-Jeeves settings the form shows (SAM_Tas' defaults) are what the definition gets.
            HookeJeevesMethod hookeJeevesMethod = Assert.IsType<HookeJeevesMethod>(Definition(input).Method);
            Assert.Equal(TasOptimisationInput.Text(new GPSHookeJeevesAlgorithm().NumberOfStepReduction), input.StepReductions);
            Assert.True(hookeJeevesMethod.StepReductions == null || hookeJeevesMethod.StepReductions == 4);

            input.OptimisationAlgorithm = OptimisationAlgorithm.GoldenSection;
            input.UpdateApplicability();
            Assert.False(input.Parameters[0].StartAndStepApplicable);
        }

        [Fact]
        public void Bounds_are_validated_for_golden_section()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Parameters[0].Maximum = "3,5";

            Assert.False(input.TryGetDefinition(out _, out List<string> problems));
            Assert.Contains(problems, x => x.Contains("Design variable 1 (Setpoint) maximum") && x.Contains("'.' as the decimal separator"));
        }

        [Theory]
        [InlineData("2.5", false)]
        [InlineData("two", false)]
        [InlineData("2.0", true)]
        [InlineData(" 3 ", true)]
        public void Hooke_Jeeves_settings_are_whole_numbers(string value, bool accepted)
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.StepReductionFactor = value;

            Assert.Equal(accepted, input.TryGetDefinition(out OptimisationDefinition definition, out List<string> problems));
            if (accepted)
            {
                Assert.Equal((int)double.Parse(value, CultureInfo.InvariantCulture), Assert.IsType<HookeJeevesMethod>(definition.Method).StepReductionFactor);
            }
            else
            {
                Assert.Equal("Step reduction factor: '" + value.Trim() + "' is not a whole number.", Assert.Single(problems));
            }
        }

        // ---- the objective -------------------------------------------------------------------------------

        [Fact]
        public void The_primary_output_is_the_objective_and_SAM_Tas_passes_it_first_whatever_its_position()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Objectives = new List<TasOptimisationObjectiveRow>
            {
                new TasOptimisationObjectiveRow("Energy", false),
                new TasOptimisationObjectiveRow("Carbon", false),
                new TasOptimisationObjectiveRow("Peak", true),
            };

            OptimisationDefinition definition = Definition(input);

            //The definition keeps the form's order and names its objective...
            Assert.Equal(["Energy", "Carbon", "Peak"], definition.Outputs.Select(x => x.Name));
            Assert.Equal("Peak", definition.Objective?.Output);

            //...and the optimiser receives it first: SAM_Tas minimises the first objective.
            Assert.Equal(["Peak", "Energy", "Carbon"], definition.TasOptimisationObjectiveNames());
            GenOptDocument document = definition.ToGenOptDocument(@"C:\ws", "s");
            Assert.Contains("Name1 = Peak;", document.ConfigFile.Simulation.ObjectiveFunctionLocation.Text);
        }

        [Fact]
        public void Choosing_another_primary_output_reorders_only_that_one()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Objectives[0].Primary = false;
            input.Objectives[2].Primary = true;

            Assert.Equal(["CO2", "Result", "Cost"], Definition(input).TasOptimisationObjectiveNames());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void There_must_be_exactly_one_primary_output(int primaries)
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            for (int i = 0; i < input.Objectives.Count; i++)
            {
                input.Objectives[i].Primary = i < primaries;
            }

            Assert.False(input.TryGetDefinition(out _, out List<string> problems));
            Assert.Contains(problems, x => x == "Choose the output to minimise (the objective).");
        }

        [Fact]
        public void An_unnamed_or_repeated_output_is_a_problem_rather_than_silently_dropped_or_merged()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Objectives.Add(new TasOptimisationObjectiveRow(" ", false));
            input.Objectives.Add(new TasOptimisationObjectiveRow("Cost", false));

            Assert.False(input.TryGetDefinition(out _, out List<string> problems));
            Assert.Contains(problems, x => x.Contains("Output 4 has no name"));
            Assert.Contains(problems, x => x.Contains("'Cost' is listed twice"));
        }

        // ---- every other rule is the definition's or SAM_Tas' ---------------------------------------------

        [Fact]
        public void Golden_section_with_two_variables_is_refused_by_the_definition()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Parameters.Add(new TasOptimisationParameterRow("Other", "0", "0", "1", "1"));

            OptimisationDefinition definition = Definition(input);

            Assert.Equal("OPT412", Assert.Single(Errors(definition)).Code);
            TasOptimisationDefinitionException exception = Assert.Throws<TasOptimisationDefinitionException>(() => Query.TasOptimisationGate(definition, @"C:\ws", "s"));
            Assert.Contains(exception.Diagnostics, x => x.Code == "OPT412");
        }

        [Theory]
        [InlineData(nameof(TasOptimisationInput.StepReductionFactor), "1", "OPT402")]
        [InlineData(nameof(TasOptimisationInput.StepReductions), "0", "OPT405")]
        [InlineData(nameof(TasOptimisationInput.StepExponentIncrement), "0", "OPT404")]
        [InlineData(nameof(TasOptimisationInput.MaximumSimulations), "0", "OPT213")]
        public void Settings_the_definition_refuses_are_refused_with_its_code(string setting, string value, string code)
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            typeof(TasOptimisationInput).GetProperty(setting).SetValue(input, value);

            Assert.Equal(code, Assert.Single(Errors(Definition(input))).Code);
        }

        [Fact]
        public void A_variable_name_SAM_Tas_cannot_use_is_refused_by_SAM_Tas()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters[0].Name = "Set,point";

            OptimisationDefinition definition = Definition(input);
            Assert.Empty(Errors(definition));

            GenOptCompatibilityException exception = Assert.Throws<GenOptCompatibilityException>(() => Query.TasOptimisationGate(definition, @"C:\ws", "s"));
            Assert.Contains("Set,point", exception.Message);
        }

        [Fact]
        public void No_variable_is_refused_by_the_definition()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters.Clear();

            Assert.Contains(Errors(Definition(input)), x => x.Code == "OPT201");
        }
    }
}
