// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas.GenOpt;
using SAM.Analytical.UI.WPF.Tests.Helpers;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// PR7b-2 D1 (owner decision option (a)) in the Design Optimisation window, without Tas: on a model whose TBD zone
    /// capacities are fixed by Energy Simulation's Sizing, the window says why zone setpoints and glazing are not offered
    /// (SAM_Tas' words), blocks a definition that has one with that reason, keeps plant controllers, and the Apply plan
    /// refuses such a change whatever run produced it. In the WPF collection: the stub's spec is process-wide.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasModelSizingGuardTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "SAMSizingUI", Guid.NewGuid().ToString("N").Substring(0, 10));

        public void Dispose()
        {
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

        /// <summary>A controller has no suggested range (it depends on the controller): the user types one.</summary>
        private static void Range(TasOptimisationParameterRow? row)
        {
            Assert.NotNull(row);
            row!.Minimum = "-5";
            row.Maximum = "35";
        }

        [Fact]
        public async Task A_model_with_fixed_capacities_says_why_and_still_runs_a_controller()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasModelInventory inventory = TasModelJourneyFixtures.DemoWithFixedCapacities();
            TasModelSession session = await TasModelJourneyFixtures.ReadSession(workspace.Directory, inventory);
            OptimisationCatalogue catalogue = Assert.IsType<OptimisationCatalogue>(session.Catalogue);
            Assert.Equal(["HeatPumpController setpoint (Plant Room)"], catalogue.Variables.Select(x => x.Name));

            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            input.Directory = workspace.Directory;
            Range(input.AddTarget(TasModelJourneyFixtures.Variable(catalogue, "HeatPumpController setpoint (Plant Room)")));
            input.AddMeasure(TasModelJourneyFixtures.Output(catalogue, "Annual plant energy"));

            List<TasOptimisationCheck> checks = input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, session);
            Assert.Equal(["Tas project", "Tas model", Query.SizingTitle, "Tas optimisation engine", "Setup"], checks.Select(x => x.Title));
            TasOptimisationCheck sizing = checks[2];
            Assert.Equal(TasOptimisationCheckStatus.Warning, sizing.Status);
            Assert.Equal(Analytical.Tas.GenOpt.Query.TasModelSizingRefusal(inventory), sizing.Detail);
            Assert.StartsWith("Zone setpoints and glazing cannot be optimised on this model: its TBD holds zone heating/cooling capacities fixed by Energy Simulation's Sizing (2 zones: “B1_Office 1”, “B1_Office 2”).", sizing.Detail);
            Assert.Contains("Plant controllers can still be optimised.", sizing.Detail);
            Assert.True(checks.CanRun(), string.Join("\n", checks));
        }

        [Fact]
        public async Task A_model_sized_in_every_simulation_has_no_such_line()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasModelSession session = await TasModelJourneyFixtures.ReadSession(workspace.Directory);
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            input.Directory = workspace.Directory;

            Assert.DoesNotContain(Query.SizingTitle, input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, session).Select(x => x.Title));
            Assert.Null(Query.TasModelSizingRefusal(session));
            Assert.Null(Query.TasModelSizingRefusal(new TasModelSession()));
        }

        [Fact]
        public async Task A_definition_with_a_zone_setpoint_is_blocked_with_the_reason_not_as_a_name_not_in_the_model()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasModelInventory inventory = TasModelJourneyFixtures.DemoWithFixedCapacities();
            TasModelSession session = await TasModelJourneyFixtures.ReadSession(workspace.Directory, inventory);

            //A definition made on the model as it was before (or pasted, or loaded): its names come from the Demo's catalogue.
            OptimisationCatalogue before = TasModelJourneyFixtures.Catalogue();
            TasOptimisationInput input = TasOptimisationInput.CreateTasModel();
            input.Directory = workspace.Directory;
            input.AddTarget(TasModelJourneyFixtures.Variable(before, "Office Weekday heating setpoint"));
            Range(input.AddTarget(TasModelJourneyFixtures.Variable(before, "HeatPumpController setpoint (Plant Room)")));
            input.AddMeasure(TasModelJourneyFixtures.Output(before, "Annual plant energy"));

            List<TasOptimisationCheck> checks = input.TasOptimisationChecks(TasOptimisationWorkspace.StubExecutable, session);
            TasOptimisationCheck setup = checks.Single(x => x.Title == "Setup");
            Assert.Equal(TasOptimisationCheckStatus.Blocked, setup.Status);
            Assert.Equal("“Office Weekday heating setpoint” changes the TBD. " + Analytical.Tas.GenOpt.Query.TasModelSizingRefusal(inventory), setup.Detail);
            Assert.DoesNotContain("OPT609", setup.Detail);
            Assert.False(checks.CanRun());
        }

        [Fact]
        public void The_AI_assistants_text_carries_the_reason()
        {
            OptimisationCatalogue catalogue = Analytical.Tas.GenOpt.Query.TasModelCatalogue(TasModelJourneyFixtures.DemoWithFixedCapacities());
            string text = global::SAM.Core.Optimisation.Query.AIExchangeText(null, Analytical.Tas.GenOpt.Query.TasModelCapabilities(), catalogue, null);
            string flat = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " "); // the text is wrapped
            int available = text.IndexOf("\nAVAILABLE", StringComparison.Ordinal);
            Assert.True(flat.Contains("AVAILABLE (read from the Tas model. Zone setpoints and glazing cannot be optimised on this model: its TBD holds zone heating/cooling capacities fixed by Energy Simulation's Sizing", StringComparison.Ordinal), available < 0 ? "no AVAILABLE line" : text.Substring(available, Math.Min(600, text.Length - available)));
            Assert.Contains("Plant controllers can still be optimised.", flat);
        }

        /// <summary>
        /// A result the guard did not see (a run on a model read before, or a definition run on an inventory without its
        /// zones' sizing): the plan says why and offers nothing, before SAM_Tas' applier would refuse it again.
        /// </summary>
        [Theory]
        [InlineData("fixed")]
        [InlineData("unknown")]
        public void The_Apply_plan_refuses_a_zone_setpoint_from_a_run_whose_model_is_not_sized_in_every_simulation(string state)
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasModelInventory inventory = TasModelApplyFixtures.Inventory();
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, inventory, 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            Assert.True(report.Successful, report.ToText());

            inventory.ZoneSizing = state == "fixed" ? new[] { new TasZoneSizing(TasModelApplyFixtures.Studio1, TasZoneSizing.NoSizing, 1500, TasZoneSizing.NoSizing, 2500) } : null;
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, TasModelApplyFixtures.Model(), Path.Combine(project, "model.sam"), null);

            Assert.False(plan.CanApply);
            Assert.Equal("The best design cannot be applied.", plan.Headline);
            string note = Assert.Single(plan.Notes);
            Assert.StartsWith("Zone setpoints and glazing cannot be optimised on this model: ", note);
            Assert.EndsWith(" Nothing will be written.", note);
            Assert.Contains(state == "fixed" ? "fixed by Energy Simulation's Sizing (zone “Studio 1_0”)" : "is not known (not read)", note);
        }

        [Fact]
        public void The_Apply_plan_offers_a_controller_result_on_a_model_with_fixed_capacities()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasModelInventory inventory = TasModelApplyFixtures.Inventory();
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.ControllerDefinition(), project, inventory, -1, out TasModelRunner runner, out OptimisationDefinition definition);
            Assert.True(report.Successful, report.ToText());

            inventory.ZoneSizing = new[] { new TasZoneSizing(TasModelApplyFixtures.Studio1, TasZoneSizing.NoSizing, 1500, TasZoneSizing.NoSizing, 2500) };
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, TasModelApplyFixtures.Model(), Path.Combine(project, "model.sam"), null);

            Assert.True(plan.CanApply, string.Join("\n", plan.Lines.Concat(plan.Notes.Select(x => new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "note", x)))));
            Assert.True(Assert.Single(plan.Items).Change.IsController);
        }
    }
}
