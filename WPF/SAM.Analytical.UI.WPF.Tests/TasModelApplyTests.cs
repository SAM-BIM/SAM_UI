// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas.GenOpt;
using SAM.Analytical.UI.WPF.Tests.Helpers;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// "Apply best design" (PR9) without Tas: the plan (what changes, from what, to what, where; refusals), the open
    /// model's change (setpoint profiles, glazing with the model's frames), and the order and protection of the whole
    /// application, with SAM_Tas' runner run through the stub TasGenExecute and the Tas writers replaced by stand-ins.
    /// In the WPF collection: the stub's spec is a process-wide environment variable, so its users must not run at once.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasModelApplyTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "SAMApplyUI", Guid.NewGuid().ToString("N").Substring(0, 10));

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

        private static string ModelPath(string project) => Path.Combine(project, "model.sam");

        private static string Text(string path) => System.IO.File.ReadAllText(path);

        [Fact]
        public void The_TBD_names_of_an_aperture_construction_are_SAM_Tas_own()
        {
            Assert.Equal(["Windows: GLZ -pane", "GLZ -pane"], Query.TasPaneConstructionNames(GlazingFixture.Current()));
        }

        [Fact]
        public void A_cooling_setpoint_lands_on_the_space_of_that_name_and_a_shared_profile_is_copied_for_it_only()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            Assert.True(report.Successful, report.ToText());
            double best = report.BestPoint[0];
            Assert.NotEqual(23, best);

            AnalyticalModel model = TasModelApplyFixtures.Model();
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, model, ModelPath(project), new TasOptimisationFormatter(definition));

            Assert.Equal(TasModelApplyMode.ModelAndTasFiles, plan.Mode);
            Assert.True(plan.CanApply, string.Join("\n", plan.Lines));
            Assert.True(plan.ChangesModel);
            TasModelApplyItem item = Assert.Single(plan.Items);
            Assert.Equal("Cooling setpoint of “Studio 1_0”", item.Item);
            Assert.Equal(best.ToString("R", CultureInfo.InvariantCulture), item.BestRaw);
            Assert.Equal(TasModelApplyFixtures.Studio1, item.SamTarget?.Space?.Name);
            Assert.True(item.SamTarget?.SharedProfile);
            Assert.Contains("gets its own copy of the cooling profile “Cooling 23”", item.Where);
            Assert.Equal("Apply the best design to the open model and its Tas files:", plan.Headline);

            List<string> written = new List<string>();
            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier => Stand_in(applier, written, TasModelApplyFixtures.Inventory(cooling1: Changed(TasModelApplyFixtures.CoolingHours(), 23, best))));

            Assert.True(outcome.Succeeded, outcome.Headline);
            Assert.Equal(["TBD 1"], written);
            AnalyticalModel changed = Assert.IsType<AnalyticalModel>(outcome.AnalyticalModel);

            // Studio 1_0: its own copy with the best value, exactly, in the hours at the setpoint; the setback kept.
            Profile profile1 = Profile(changed, TasModelApplyFixtures.Studio1, ProfileType.Cooling);
            Assert.Equal("Cooling 23 - Studio 1_0", profile1.Name);
            double[] hours1 = Query.TasHours(profile1);
            Assert.All(Enumerable.Range(8, 12), h => Assert.Equal(BitConverter.DoubleToInt64Bits(best), BitConverter.DoubleToInt64Bits(hours1[h])));
            Assert.All(Enumerable.Range(0, 24).Where(h => h < 8 || h >= 20), h => Assert.Equal(150, hours1[h]));

            // Studio 2_0 and the shared profile: as they were. The model given is not modified.
            Profile profile2 = Profile(changed, TasModelApplyFixtures.Studio2, ProfileType.Cooling);
            Assert.Equal("Cooling 23", profile2.Name);
            Assert.Equal(TasModelApplyFixtures.CoolingHours(), Query.TasHours(profile2));
            Assert.Equal("Cooling 23", Profile(model, TasModelApplyFixtures.Studio1, ProfileType.Cooling).Name);
            Assert.Equal(Studio1Heating(model), Studio1Heating(changed));

            // The Tas files: the TBD replaced from staging, its original kept; the TPD and TSD untouched.
            Assert.Equal("tbd written", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
            Assert.Equal("tbd", Text(Path.Combine(outcome.TasResult!.BackupFolder!, TasModelApplyFixtures.Tbd)));
            Assert.Equal("tpd", Text(Path.Combine(project, TasModelApplyFixtures.Tpd)));
            Assert.Contains(outcome.Lines, x => x.Detail.StartsWith("SAM model: space “Studio 1_0” now uses “Cooling 23 - Studio 1_0”", StringComparison.Ordinal));
        }

        [Fact]
        public void A_profile_only_this_space_uses_changes_in_place()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            double best = report.BestPoint[0];
            AnalyticalModel model = TasModelApplyFixtures.Model(shareCooling: false);

            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, model, ModelPath(project), null);
            Assert.False(plan.Items[0].SamTarget?.SharedProfile);

            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier => Stand_in(applier, null, TasModelApplyFixtures.Inventory(cooling1: Changed(TasModelApplyFixtures.CoolingHours(), 23, best))));
            Assert.True(outcome.Succeeded, outcome.Headline);
            Profile profile = Profile(outcome.AnalyticalModel!, TasModelApplyFixtures.Studio1, ProfileType.Cooling);
            Assert.Equal("Cooling 23", profile.Name);
            Assert.Equal(Changed(TasModelApplyFixtures.CoolingHours(), 23, best), Query.TasHours(profile));
            Assert.Equal(TasModelApplyFixtures.CoolingHours(), Query.TasHours(Profile(outcome.AnalyticalModel!, TasModelApplyFixtures.Studio2, ProfileType.Cooling)));
        }

        [Theory]
        [InlineData("no-space", "The open model has no space named “Studio 1_0”")]
        [InlineData("other-profile", "is not the Tas model's cooling setpoint: the model has changed since its last Energy Simulation")]
        public void A_model_that_is_not_the_Tas_models_blocks_the_whole_application(string mode, string message)
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            AnalyticalModel model = mode == "no-space"
                ? TasModelApplyFixtures.Model(studio1: "Studio 9_9")
                : TasModelApplyFixtures.Model(cooling1: Changed(TasModelApplyFixtures.CoolingHours(), 23, 24));

            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, model, ModelPath(project), null);

            Assert.False(plan.CanApply);
            Assert.Contains(message, plan.Items[0].Problem);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, plan.Lines[0].Status);
            Assert.StartsWith("The best design cannot be applied", plan.Headline);

            bool called = false;
            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier => applier.TbdWriter = (path, list) => { called = true; return new List<TasModelAppliedValue>(); });
            Assert.False(outcome.Succeeded);
            Assert.False(called);
            Assert.Equal("tbd", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
        }

        [Fact]
        public void The_glazing_choice_gives_the_windows_the_chosen_pane_and_keeps_the_models_frame()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.GlazingDefinition(), project, TasModelApplyFixtures.Inventory(), 2, out TasModelRunner runner, out OptimisationDefinition definition);
            Assert.Equal([2.0], report.BestPoint);
            AnalyticalModel model = TasModelApplyFixtures.Model();
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, model, ModelPath(project), new TasOptimisationFormatter(definition));

            TasModelApplyItem item = Assert.Single(plan.Items);
            Assert.True(plan.CanApply, string.Join("\n", plan.Lines));
            Assert.Equal("2: Triple low-e", item.Best);
            Assert.Equal(4, item.SamTarget?.ApertureCount);
            Assert.Equal(["GLZ"], item.SamTarget?.ApertureConstructions.Select(x => x.Name));
            Assert.Contains("4 apertures of “GLZ”; the pane changes, the frame is kept", item.Where);

            TasGlazingOption option = plan.Items[0].Change.GlazingOption!;
            TasModelInventory after = new TasModelInventory(TasModelApplyFixtures.Tbd, TasModelApplyFixtures.Tsd, TasModelApplyFixtures.Tpd, TasModelApplyFixtures.Inventory().InternalConditions, new[]
            {
                new TasGlazingConstructionInfo(option.PaneConstruction, new[] { "W1", "W2" }, option.G, option.U, option.Light),
            }, TasModelApplyFixtures.Inventory().PlantRooms);
            List<ApertureConstruction> calculated = new List<ApertureConstruction>();
            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier => Stand_in(applier, null, after), (apertureConstruction, materialLibrary) =>
            {
                calculated.Add(apertureConstruction);
                Assert.NotNull(materialLibrary.GetMaterial(GlazingFixture.LowE));
                return null;
            });

            Assert.True(outcome.Succeeded, outcome.Headline);
            AnalyticalModel changed = outcome.AnalyticalModel!;
            List<Aperture> apertures = changed.AdjacencyCluster.GetApertures();
            ApertureConstruction triple = Assert.Single(changed.AdjacencyCluster.GetApertureConstructions().Where(x => x.Name == "Triple low-e").GroupBy(x => x.Guid).Select(x => x.First()));
            Assert.Equal(4, apertures.Count(x => x.TypeGuid == triple.Guid));
            Assert.Equal(1, apertures.Count(x => x.TypeGuid == GlazingFixture.PaneOnlyGuid));
            Assert.NotEqual(TasModelApplyFixtures.TripleGuid, triple.Guid);
            Assert.Equal([GlazingFixture.LowE, GlazingFixture.Argon, GlazingFixture.Clear], triple.PaneConstructionLayers.Select(x => x.Name));
            Assert.Equal([GlazingFixture.FrameMaterial], triple.FrameConstructionLayers.Select(x => x.Name));
            Assert.Equal([0.05], triple.FrameConstructionLayers.Select(x => x.Thickness));
            Assert.False(triple.HasValue(ApertureConstructionParameter.DefaultFrameWidth), "the frame width default is the model's (none), not the system's 0.09");
            Assert.NotNull(changed.MaterialLibrary.GetMaterial(GlazingFixture.LowE));
            Assert.Null(changed.MaterialLibrary.GetMaterial(TasModelApplyFixtures.SystemFrame));
            Assert.Equal(triple.Guid, Assert.Single(calculated).Guid);

            // The model given is not modified.
            Assert.Equal(5, model.AdjacencyCluster.GetApertures().Count(x => x.TypeGuid == GlazingFixture.CurrentGuid || x.TypeGuid == GlazingFixture.PaneOnlyGuid));
        }

        [Fact]
        public void A_plant_controller_goes_to_the_TPD_only_and_the_plan_says_so()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.ControllerDefinition(), project, TasModelApplyFixtures.Inventory(), 4.968943799848584, out TasModelRunner runner, out OptimisationDefinition definition);
            double best = report.BestPoint[0];
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, TasModelApplyFixtures.Model(), ModelPath(project), null);

            TasModelApplyItem item = Assert.Single(plan.Items);
            Assert.Equal("TPD only: SAM does not hold plant controllers", item.Where);
            Assert.Null(item.SamTarget);
            Assert.False(plan.ChangesModel);
            Assert.Contains(plan.Notes, x => x.StartsWith("A plant controller setpoint is kept in the TPD only", StringComparison.Ordinal));

            List<double> values = new List<double>();
            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier =>
            {
                Stand_in(applier, null, TasModelApplyFixtures.Inventory(controller: best));
                applier.TpdWriter = (path, list) =>
                {
                    values.AddRange(list.Select(x => x.Value));
                    System.IO.File.AppendAllText(path, " written");
                    return list.Select(x => new TasModelAppliedValue(x, "3", x.Value.ToString("R", CultureInfo.InvariantCulture), true)).ToList();
                };
            });

            Assert.True(outcome.Succeeded, outcome.Headline);
            Assert.Null(outcome.AnalyticalModel);
            Assert.Equal([best], values);
            Assert.Equal("tpd written", Text(Path.Combine(project, TasModelApplyFixtures.Tpd)));
            Assert.Equal("tbd", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
        }

        [Fact]
        public void Without_the_models_folder_only_the_Tas_files_change_and_the_plan_says_why()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            double best = report.BestPoint[0];

            TasModelApplyPlan none = TasModelApplyPlan.Create(report, definition, runner, project, null, null, null);
            Assert.Equal(TasModelApplyMode.TasFilesOnly, none.Mode);
            Assert.Equal("TBD only", none.Items[0].Where);
            Assert.StartsWith("No SAM model is open: the open model is not changed.", none.Notes.Single(x => x.StartsWith("No SAM", StringComparison.Ordinal)));

            AnalyticalModel model = TasModelApplyFixtures.Model();
            TasModelApplyPlan elsewhere = TasModelApplyPlan.Create(report, definition, runner, project, model, Path.Combine(folder, "other", "model.sam"), null);
            Assert.Equal(TasModelApplyMode.TasFilesOnly, elsewhere.Mode);
            Assert.Contains(elsewhere.Notes, x => x.StartsWith("The Tas project folder is not the open model's folder", StringComparison.Ordinal));
            Assert.Contains(TasModelApplyPlan.Create(report, definition, runner, project, model, null, null).Notes, x => x.StartsWith("The open model has not been saved", StringComparison.Ordinal));

            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(elsewhere, applier => Stand_in(applier, null, TasModelApplyFixtures.Inventory(cooling1: Changed(TasModelApplyFixtures.CoolingHours(), 23, best))));
            Assert.True(outcome.Succeeded, outcome.Headline);
            Assert.Null(outcome.AnalyticalModel);
            Assert.Equal("tbd written", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
        }

        [Fact]
        public void A_failure_writing_the_Tas_files_leaves_the_model_unchanged()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, TasModelApplyFixtures.Model(), ModelPath(project), null);

            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier => applier.TbdWriter = (path, list) => throw new InvalidOperationException("Internal condition “Studio 1_0” was found 0 times in the TBD."));

            Assert.False(outcome.Succeeded);
            Assert.Null(outcome.AnalyticalModel);
            Assert.Contains("was found 0 times", outcome.Headline);
            Assert.EndsWith("The open model was not changed.", outcome.Headline);
            Assert.False(outcome.ProjectChanged);
            Assert.Equal("tbd", Text(Path.Combine(project, TasModelApplyFixtures.Tbd)));
        }

        [Fact]
        public void Tas_files_that_changed_after_the_run_are_refused()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);
            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, TasModelApplyFixtures.Model(), ModelPath(project), null);
            System.IO.File.WriteAllText(Path.Combine(project, TasModelApplyFixtures.Tbd), "simulated again");

            TasModelApplyOutcome outcome = Modify.ApplyTasModelBestDesign(plan, applier => Stand_in(applier, null, TasModelApplyFixtures.Inventory()));

            Assert.False(outcome.Succeeded);
            Assert.Null(outcome.AnalyticalModel);
            Assert.Contains("The Tas files changed after the optimisation ran (Model.tbd changed)", outcome.Headline);
        }

        [Fact]
        public void Only_a_run_with_a_best_point_can_be_applied()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.CoolingDefinition(), project, TasModelApplyFixtures.Inventory(), 25.976, out TasModelRunner runner, out OptimisationDefinition definition);

            TasModelApplyPlan failed = TasModelApplyPlan.Create(new TasOptimisationReport(new InvalidOperationException("no")), definition, runner, project, null, null, null);
            Assert.False(failed.CanApply);
            Assert.StartsWith("There is no best design to apply", failed.Refusal);

            TasModelApplyPlan withheld = TasModelApplyPlan.Create(new TasOptimisationReport(report.Result!, report.ParameterNames, report.ObjectiveNames, report.RunDirectory, true), definition, runner, project, null, null, null);
            Assert.False(withheld.CanApply);
            Assert.StartsWith("There is no best design to apply: Cancelled as the run finished", withheld.Refusal);

            TasModelApplyPlan script = TasModelApplyPlan.Create(report, definition, null, project, null, null, null);
            Assert.Equal("Only a run of the “Tas model” engine in this window can be applied.", script.Refusal);
        }

        [Fact]
        public void The_current_glazing_as_the_best_option_writes_nothing()
        {
            string project = TasModelApplyFixtures.Project(folder);
            TasOptimisationReport report = TasModelApplyFixtures.Run(TasModelApplyFixtures.GlazingDefinition(), project, TasModelApplyFixtures.Inventory(), 1, out TasModelRunner runner, out OptimisationDefinition definition);
            Assert.Equal([1.0], report.BestPoint);

            TasModelApplyPlan plan = TasModelApplyPlan.Create(report, definition, runner, project, TasModelApplyFixtures.Model(), ModelPath(project), null);
            Assert.True(plan.Items[0].Unchanged);
            Assert.False(plan.CanApply);
            Assert.Equal("The model already holds the best design: there is nothing to apply.", plan.Headline);
            Assert.Contains("already ", plan.Lines[0].Detail);
        }

        [Fact]
        public void The_setpoint_values_follow_the_TBD_block_hour_by_hour()
        {
            Profile heating = new Profile("Heating", ProfileType.Heating, Enumerable.Range(0, 24).Select(h => h >= 7 && h < 22 ? 21.0 : 16.0));
            Assert.Equal(Enumerable.Range(0, 24).Select(h => h >= 7 && h < 22 ? 19.5 : 16.0), Modify.TasSetpointValues(heating, 19.5, true));

            // Below the setback the hours at the setpoint still change, and only they (as the script's block).
            Assert.Equal(Enumerable.Range(0, 24).Select(h => h >= 7 && h < 22 ? 15.0 : 16.0), Modify.TasSetpointValues(heating, 15, true));

            Profile value = new Profile("Value", ProfileType.Cooling, new double[] { 24 });
            Assert.Equal([25.976], Modify.TasSetpointValues(value, 25.976, false));
            Assert.True(Query.TasSetpointProfileMatches(value, new TasSetpointProfile(TasSetpointProfileType.Value, 1, 24f), false));
            Assert.False(Query.TasSetpointProfileMatches(value, new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, 24f, 24, Enumerable.Repeat(24f, 24)), false));
            Assert.True(Query.TasSetpointProfileMatches(heating, new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, 21f, 15, Enumerable.Range(0, 24).Select(h => h >= 7 && h < 22 ? 21f : 16f)), true));
            Assert.False(Query.TasSetpointProfileMatches(heating, new TasSetpointProfile(TasSetpointProfileType.Hourly, 1, 21f, 15, Enumerable.Range(0, 24).Select(h => h >= 8 && h < 22 ? 21f : 16f)), true));
        }

        /// <summary>Stand-in Tas writers (append " written" to the staged file) and a reader that returns <paramref name="after"/>.</summary>
        internal static void Stand_in(TasModelDesignApplier applier, List<string>? written, TasModelInventory after)
        {
            applier.TbdWriter = (path, list) =>
            {
                written?.Add("TBD " + list.Count);
                System.IO.File.AppendAllText(path, " written");
                return list.Select(x => new TasModelAppliedValue(x, "before", x.Value.ToString("R", CultureInfo.InvariantCulture), true)).ToList();
            };

            applier.TpdWriter = (path, list) => throw new InvalidOperationException("no plant change");
            applier.InventoryReader = staging => after;
        }

        private static double[] Changed(double[] hours, double from, double to)
        {
            return hours.Select(x => x == from ? to : x).ToArray();
        }

        private static Profile Profile(AnalyticalModel model, string space, ProfileType profileType)
        {
            Space found = model.AdjacencyCluster.GetSpaces().Single(x => x.Name == space);
            return found.InternalCondition.GetProfile(profileType, model.ProfileLibrary);
        }

        private static string Studio1Heating(AnalyticalModel model)
        {
            Profile profile = Profile(model, TasModelApplyFixtures.Studio1, ProfileType.Heating);
            return profile.Name + " " + string.Join(",", profile.GetValues());
        }
    }
}
