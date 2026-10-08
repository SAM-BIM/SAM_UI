// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Simulate &gt; Optimisation's form (<see cref="TasOptimisationInput"/>) and what it builds
    /// (<see cref="TasOptimisationDefinition"/>): the SAM_Tas GenOpt objects, nothing else. Defaults come from those
    /// objects, the examples reproduce the proven Systems Demo inputs exactly as the SAM_Tas Grasshopper component builds
    /// them, and every rule beyond "is this a number" is SAM_Tas'.
    /// </summary>
    public class TasOptimisationInputTests
    {
        // ---- defaults and examples ------------------------------------------------------------------------

        [Fact]
        public void The_algorithm_and_stopping_defaults_are_read_from_the_SAM_Tas_objects()
        {
            TasOptimisationInput input = new TasOptimisationInput();

            GoldenSectionAlgorithm goldenSectionAlgorithm = new GoldenSectionAlgorithm();
            GPSHookeJeevesAlgorithm gPSHookeJeevesAlgorithm = new GPSHookeJeevesAlgorithm();
            OptimizationSettings optimizationSettings = new OptimizationSettings();

            Assert.Equal(TasOptimisationInput.Text(goldenSectionAlgorithm.AbsDiffFunction), input.AbsDiffFunction);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.MeshSizeDivider), input.MeshSizeDivider);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.InitialMeshSizeExponent), input.InitialMeshSizeExponent);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.MeshSizeExponentIncrement), input.MeshSizeExponentIncrement);
            Assert.Equal(TasOptimisationInput.Text(gPSHookeJeevesAlgorithm.NumberOfStepReduction), input.NumberOfStepReduction);
            Assert.Equal(optimizationSettings.MaxIterations.ToString(CultureInfo.InvariantCulture), input.MaxIterations);
            Assert.Equal(optimizationSettings.MaxEqualResults.ToString(CultureInfo.InvariantCulture), input.MaxEqualResults);

            //The values as they read today, so a change in SAM_Tas is noticed here.
            Assert.Equal(["0.1", "2", "0", "1", "4", "2000", "100"], new[] { input.AbsDiffFunction, input.MeshSizeDivider, input.InitialMeshSizeExponent, input.MeshSizeExponentIncrement, input.NumberOfStepReduction, input.MaxIterations, input.MaxEqualResults });
        }

        [Fact]
        public void Only_the_algorithms_the_native_optimiser_runs_are_offered()
        {
            Assert.Equal([AlgorithmType.GoldenSection, AlgorithmType.GPSHookeJeeves], TasOptimisationInput.AlgorithmTypes);
            Assert.All(TasOptimisationInput.AlgorithmTypes, x => Assert.Contains(x, Analytical.Tas.GenOpt.Convert.NativeAlgorithmTypes));
        }

        [Fact]
        public void The_window_opens_on_the_Systems_Demo_golden_section_example_with_no_folder_or_script()
        {
            Assert.Equal(TasOptimisationExample.SystemsDemoGoldenSection, TasOptimisationInput.DefaultExample);

            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationInput.DefaultExample);

            Assert.Equal(AlgorithmType.GoldenSection, input.AlgorithmType);
            Assert.Equal(string.Empty, input.Directory);
            Assert.Equal(string.Empty, input.ScriptPath);
            Assert.Equal(string.Empty, input.RunsDirectory);

            TasOptimisationParameterRow row = Assert.Single(input.Parameters);
            Assert.Equal(("Setpoint", "3", "-5", "35", "1"), (row.Name, row.Start, row.Minimum, row.Maximum, row.Step));
            Assert.False(row.StartAndStepApplicable);

            Assert.Equal(["Result", "Cost", "CO2"], input.Objectives.Select(x => x.Name));
            Assert.Equal("Result", input.PrimaryObjective?.Name);
            Assert.Single(input.Objectives, x => x.Primary);
        }

        [Fact]
        public void The_Hooke_Jeeves_example_has_its_proven_start_and_step_and_applies_them()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);

            Assert.Equal(AlgorithmType.GPSHookeJeeves, input.AlgorithmType);
            TasOptimisationParameterRow row = Assert.Single(input.Parameters);
            Assert.Equal(("Setpoint", "10", "-5", "35", "2"), (row.Name, row.Start, row.Minimum, row.Maximum, row.Step));
            Assert.True(row.StartAndStepApplicable);
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
            Assert.Equal(AlgorithmType.GPSHookeJeeves, input.AlgorithmType);
        }

        [Fact]
        public void A_copy_is_independent_of_the_original()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            TasOptimisationInput copy = new TasOptimisationInput(input);

            copy.Parameters[0].Minimum = "0";
            copy.Objectives[1].Primary = true;
            copy.Directory = "x";

            Assert.Equal("-5", input.Parameters[0].Minimum);
            Assert.False(input.Objectives[1].Primary);
            Assert.Equal(string.Empty, input.Directory);
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

            Assert.True(TasOptimisationInput.Create(tasOptimisationExample).TryGetDefinition(out TasOptimisationDefinition definition, out List<string> problems), string.Join("; ", problems));
            GenOptDocument document = definition.ToGenOptDocument(@"C:\ws", script);

            Assert.Equal(reference.CommandFile.Text, document.CommandFile.Text);
            Assert.Equal(reference.ParameterFile.Text, document.ParameterFile.Text);
            Assert.Equal(reference.TemplateFile.Text, document.TemplateFile.Text);
            Assert.Equal(reference.OutputFile.Text, document.OutputFile.Text);
            Assert.Equal(reference.ConfigFile.Text, document.ConfigFile.Text);
            Assert.Equal(reference.ScriptFile.Text, document.ScriptFile.Text);

            //And it is what SAM_Tas will run: valid, and mapped to the same kernel problem.
            definition.Validate();
            Assert.Equal(["Result", "Cost", "CO2"], definition.ObjectiveNames);
            Assert.Equal(["Setpoint"], definition.ParameterNames);
        }

        // ---- golden section: Start and Step do not apply -------------------------------------------------

        [Fact]
        public void Golden_section_excludes_Start_and_Step_from_validation_and_passes_valid_values_through()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            NumberParameter numberParameter = Assert.Single(definition.NumberParameters);
            Assert.Equal((3.0, -5.0, 35.0, 1.0), (numberParameter.Initial, numberParameter.Min, numberParameter.Max, numberParameter.Step));

            input.Parameters[0].Start = "not a number";
            input.Parameters[0].Step = string.Empty;

            Assert.True(input.TryGetDefinition(out definition, out List<string> problems), string.Join("; ", problems));
            numberParameter = Assert.Single(definition.NumberParameters);

            //Finite, as SAM_Tas requires, and inert: the lower bound and 0.
            Assert.Equal((-5.0, 0.0), (numberParameter.Initial, numberParameter.Step));
            definition.Validate();
        }

        [Fact]
        public void Pattern_search_validates_Start_and_Step()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters[0].Start = "ten";

            Assert.False(input.TryGetDefinition(out TasOptimisationDefinition definition, out List<string> problems));
            Assert.Null(definition);
            Assert.Contains(problems, x => x.Contains("Design variable 1 (Setpoint) start") && x.Contains("'ten' is not a number"));
        }

        [Fact]
        public void Switching_the_algorithm_updates_whether_Start_and_Step_apply()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            Assert.False(input.Parameters[0].StartAndStepApplicable);

            input.AlgorithmType = AlgorithmType.GPSHookeJeeves;
            input.UpdateApplicability();
            Assert.True(input.Parameters[0].StartAndStepApplicable);

            input.AlgorithmType = AlgorithmType.GoldenSection;
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

        // ---- the primary objective -----------------------------------------------------------------------

        [Fact]
        public void The_primary_objective_is_passed_first_whatever_its_name_and_position()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Objectives = new List<TasOptimisationObjectiveRow>
            {
                new TasOptimisationObjectiveRow("Energy", false),
                new TasOptimisationObjectiveRow("Carbon", false),
                new TasOptimisationObjectiveRow("Peak", true),
            };

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(["Peak", "Energy", "Carbon"], definition.ObjectiveNames);

            //And the document the optimiser receives has it first: SAM_Tas minimises the first objective.
            GenOptDocument document = definition.ToGenOptDocument(@"C:\ws", "s");
            Assert.Contains("Name1 = Peak;", document.ConfigFile.Simulation.ObjectiveFunctionLocation.Text);
        }

        [Fact]
        public void Choosing_another_primary_output_reorders_only_that_one()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Objectives[0].Primary = false;
            input.Objectives[2].Primary = true;

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Equal(["CO2", "Result", "Cost"], definition.ObjectiveNames);
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

        // ---- every other rule is SAM_Tas' ----------------------------------------------------------------

        [Fact]
        public void Golden_section_with_two_parameters_is_refused_with_SAM_Tas_message()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoGoldenSection);
            input.Parameters.Add(new TasOptimisationParameterRow("Other", "0", "0", "1", "1"));

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            GenOptCompatibilityException exception = Assert.Throws<GenOptCompatibilityException>(definition.Validate);

            GenOptCompatibilityException expected = Assert.Throws<GenOptCompatibilityException>(() => Analytical.Tas.GenOpt.Convert.ToSAM_Optimiser(new GoldenSectionAlgorithm(), new OptimizationSettings(), 2));
            Assert.Equal(expected.Message, exception.Message);
        }

        [Theory]
        [InlineData("MeshSizeDivider", "2.5")]
        [InlineData("MeshSizeDivider", "1")]
        [InlineData("NumberOfStepReduction", "0")]
        [InlineData("MaxEqualResults", "1")]
        [InlineData("MaxIterations", "0")]
        public void Settings_SAM_Tas_refuses_are_refused_here_by_SAM_Tas(string setting, string value)
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            typeof(TasOptimisationInput).GetProperty(setting).SetValue(input, value);

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out List<string> problems), string.Join("; ", problems));
            Assert.Throws<GenOptCompatibilityException>(definition.Validate);
        }

        [Fact]
        public void A_parameter_name_SAM_Tas_cannot_use_is_refused_by_SAM_Tas()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters[0].Name = "Set,point";

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            GenOptCompatibilityException exception = Assert.Throws<GenOptCompatibilityException>(definition.Validate);
            Assert.Contains("Set,point", exception.Message);
        }

        [Fact]
        public void No_parameter_is_refused_by_SAM_Tas()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Parameters.Clear();

            Assert.True(input.TryGetDefinition(out TasOptimisationDefinition definition, out _));
            Assert.Throws<GenOptCompatibilityException>(definition.Validate);
        }
    }
}
