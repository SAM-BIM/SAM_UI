// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Collections.Generic;
using System.IO;
using File = System.IO.File;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The Optimisation window's readiness list (<see cref="Query.TasOptimisationChecks"/>): file checks, SAM_Tas' own
    /// verdict on the settings word for word, and the non-blocking name warning. Nothing is created or started.
    /// </summary>
    public class TasOptimisationChecksTests
    {
        private static TasOptimisationCheck Check(List<TasOptimisationCheck> checks, string title) => Assert.Single(checks, x => x.Title == title);

        [Fact]
        public void A_ready_form_has_no_blocking_line_and_names_what_will_run()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(script: "Variables[\"Setpoint\"]; ScriptOutput.SetValue(\"Result\", 1); \"Cost\" \"CO2\"");

            List<TasOptimisationCheck> checks = workspace.Input().TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable);

            Assert.True(checks.CanRun());
            Assert.All(checks, x => Assert.Equal(TasOptimisationCheckStatus.Ready, x.Status));
            Assert.Contains("Model.tbd", Check(checks, "Tas project folder").Detail);
            Assert.Contains(workspace.RunsDirectory, Check(checks, "Tas project folder").Detail);
            Assert.Equal("Script.txt", Check(checks, "Script").Detail);
            Assert.Equal(TasOptimisationWorkspace.StubExecutable, Check(checks, "TasGenExecute").Detail);
            Assert.Equal("GoldenSection · 1 parameter(s): Setpoint · minimises Result (also records Cost, CO2) · up to 2000 simulations", Check(checks, "Optimisation settings").Detail);
        }

        [Fact]
        public void The_example_alone_is_blocked_on_folder_and_script()
        {
            List<TasOptimisationCheck> checks = TasOptimisationInput.Create(TasOptimisationInput.DefaultExample).TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable);

            Assert.False(checks.CanRun());
            Assert.Equal(TasOptimisationCheckStatus.Blocked, Check(checks, "Tas project folder").Status);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, Check(checks, "Script").Status);
            Assert.Equal(TasOptimisationCheckStatus.Ready, Check(checks, "Optimisation settings").Status);
        }

        [Fact]
        public void A_folder_without_Tas_files_is_blocked()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(tasFile: false);
            File.WriteAllText(Path.Combine(workspace.Directory, "notes.txt"), "x");

            TasOptimisationCheck check = Check(workspace.Input().TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable), "Tas project folder");

            Assert.Equal(TasOptimisationCheckStatus.Blocked, check.Status);
            Assert.Contains(".tbd", check.Detail);
        }

        [Fact]
        public void The_Tas_files_are_SAM_Tas_snapshot_types_at_the_top_level_only()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(tasFile: false);
            foreach (string name in new[] { "a.T3D", "b.tpd", "c.tsd", "d.twd", "e.xml", "Script.txt" })
            {
                File.WriteAllText(Path.Combine(workspace.Directory, name), "x");
            }

            Directory.CreateDirectory(Path.Combine(workspace.Directory, "sub"));
            File.WriteAllText(Path.Combine(workspace.Directory, "sub", "f.tbd"), "x");

            Assert.Equal(["a.T3D", "b.tpd", "c.tsd", "d.twd"], Query.TasOptimisationTasFiles(workspace.Directory));
        }

        [Fact]
        public void A_folder_that_exists_but_cannot_be_listed_is_blocked_rather_than_thrown()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            using (workspace.DenyListing())
            {
                //The situation: the folder is there, but listing it throws.
                Assert.True(Directory.Exists(workspace.Directory));
                Assert.Throws<UnauthorizedAccessException>(() => Directory.GetFiles(workspace.Directory));

                Assert.Empty(Query.TasOptimisationTasFiles(workspace.Directory, out string error));
                Assert.False(string.IsNullOrWhiteSpace(error));
                Assert.Empty(Query.TasOptimisationTasFiles(workspace.Directory));

                List<TasOptimisationCheck> checks = workspace.Input().TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable);
                TasOptimisationCheck check = Check(checks, "Tas project folder");

                Assert.Equal(TasOptimisationCheckStatus.Blocked, check.Status);
                Assert.Equal("The folder cannot be read: " + error, check.Detail);
                Assert.False(checks.CanRun());
            }
        }

        [Fact]
        public void A_missing_or_empty_script_is_blocked()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();

            TasOptimisationInput input = workspace.Input();
            input.ScriptPath = Path.Combine(workspace.Directory, "missing.txt");
            Assert.Equal(TasOptimisationCheckStatus.Blocked, Check(input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable), "Script").Status);

            File.WriteAllText(workspace.ScriptPath, "   ");
            Assert.Contains("empty", Check(workspace.Input().TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable), "Script").Detail);
        }

        [Fact]
        public void A_missing_TasGenExecute_is_blocked_and_named()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            string path = Path.Combine(workspace.Directory, "TasGenExecute.exe");

            TasOptimisationCheck check = Check(workspace.Input().TasOptimisationChecks(path), "TasGenExecute");

            Assert.Equal(TasOptimisationCheckStatus.Blocked, check.Status);
            Assert.Contains(path, check.Detail);
        }

        [Fact]
        public void The_installed_TasGenExecute_is_checked_when_no_path_is_given()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();

            Assert.Contains(Analytical.Tas.GenOpt.Query.TasGenOptExecutePath(), Check(workspace.Input().TasOptimisationChecks(), "TasGenExecute").Detail);
        }

        [Fact]
        public void A_setting_SAM_Tas_refuses_blocks_with_its_message_word_for_word_and_creates_nothing()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationInput input = workspace.Input();
            input.Parameters.Add(new TasOptimisationParameterRow("Other", "0", "0", "1", "1"));

            TasOptimisationCheck check = Check(input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable), "Optimisation settings");

            GenOptCompatibilityException expected = Assert.Throws<GenOptCompatibilityException>(() => Analytical.Tas.GenOpt.Convert.ToSAM_Optimiser(new GoldenSectionAlgorithm(), new OptimizationSettings(), 2));
            Assert.Equal(TasOptimisationCheckStatus.Blocked, check.Status);
            Assert.Equal("Invalid GenOpt settings for the native route: " + expected.Message, check.Detail);
            Assert.False(Directory.Exists(workspace.RunsDirectory));
        }

        [Fact]
        public void Unreadable_numbers_are_listed_one_per_line()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationInput input = workspace.Input();
            input.Parameters[0].Minimum = "a";
            input.MaxIterations = "many";

            TasOptimisationCheck check = Check(input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable), "Optimisation settings");

            Assert.Equal(TasOptimisationCheckStatus.Blocked, check.Status);
            Assert.Equal(2, check.Detail.Split(Environment.NewLine).Length);
        }

        [Fact]
        public void Names_the_script_never_mentions_are_warnings_that_do_not_block()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(script: "Variables[\"SetPoint\"]; ScriptOutput.SetValue(\"Result\", 1);");

            List<TasOptimisationCheck> checks = workspace.Input().TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable);

            Assert.True(checks.CanRun());
            Assert.Equal(["Parameter 'Setpoint'", "Output 'Cost'", "Output 'CO2'"], checks.Where(x => x.Status == TasOptimisationCheckStatus.Warning).Select(x => x.Title));
            Assert.DoesNotContain(checks, x => x.Title == "Output 'Result'");
        }

        [Fact]
        public void The_runs_folder_is_SAM_Tas_default_unless_one_is_given()
        {
            TasOptimisationInput input = TasOptimisationInput.Create(TasOptimisationInput.DefaultExample);
            Assert.Null(input.TasOptimisationRunsDirectory());

            input.Directory = @"C:\P";
            Assert.Equal(@"C:\P\SAM_NativeGenOpt", input.TasOptimisationRunsDirectory());

            input.RunsDirectory = @" C:\R ";
            Assert.Equal(@"C:\R", input.TasOptimisationRunsDirectory());
        }

        [Fact]
        public void The_optimisation_assemblies_load_and_their_locations_are_reported()
        {
            Assert.Null(Query.TasOptimisationAssemblyFailure());

            string text = Query.TasOptimisationAssemblyLocations();
            Assert.Contains("SAM.Math: ", text);
            Assert.Contains("SAM.Math.dll", text);
            Assert.Contains("SAM.Analytical.Tas.GenOpt.dll", text);
        }

        [Fact]
        public void A_stale_assembly_is_recognised_and_explained()
        {
            Exception stale = new TypeLoadException("Could not load type 'SAM.Math.GoldenSection' from assembly 'SAM.Math, Version=1.0.0.0'.");
            Exception missing = new FileNotFoundException("Could not load file or assembly.", "SAM.Analytical.Tas.GenOpt, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            Assert.True(Query.IsTasOptimisationLoadFailure(stale));
            Assert.True(Query.IsTasOptimisationLoadFailure(new TargetInvocationWrapper(stale)));
            Assert.True(Query.IsTasOptimisationLoadFailure(missing));
            Assert.True(Query.IsTasOptimisationLoadFailure(new MissingMethodException("x")));

            //A missing TasGenExecute.exe is a configuration problem, not a load failure.
            Assert.False(Query.IsTasOptimisationLoadFailure(new FileNotFoundException("TasGenExecute was not found.", @"C:\x\TasGenExecute.exe")));
            Assert.False(Query.IsTasOptimisationLoadFailure(new GenOptCompatibilityException("x")));

            string text = Query.TasOptimisationLoadFailure(stale);
            Assert.Contains("stale SAM.Math.dll", text);
            Assert.Contains("TypeLoadException: Could not load type 'SAM.Math.GoldenSection'", text);
            Assert.Contains("Loaded: SAM.Math: ", text);
            Assert.Contains("Application folder: " + AppContext.BaseDirectory, text);

            //The report shows the same explanation.
            Assert.Equal(text, TasOptimisationReport.Message(stale));
        }

        private sealed class TargetInvocationWrapper : Exception
        {
            public TargetInvocationWrapper(Exception inner)
                : base("wrapper", inner)
            {
            }
        }
    }
}
