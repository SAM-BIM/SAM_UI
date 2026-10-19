// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Benchmark;
using SAM.Core;
using SAM.Geometry.UI;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// S04 (finding F7 of SAM_UI#239): the frozen simulation snapshot - the file both Run Simulation &gt; Both producers hash and
    /// OpenStudio Simulation converts - leaves out the one UI-only parameter SAM_UI keeps on a model, "UI Geometry Settings" (its
    /// views, regenerated with new GUIDs every time a model is opened), so the same engineering model gets the same snapshot and the
    /// same canonical model hash in every session, while every engineering edit still changes it.
    /// <para>
    /// The canonical hash is computed here exactly as the producers compute it (load the snapshot into an AnalyticalModel, then
    /// SAM_Validation's canonical JSON v1 of its JSON); the models are saved, reopened through <see cref="UIAnalyticalModel"/> as the
    /// window opens them, and frozen by the real <see cref="Modify.FreezeAnalyticalModel"/>.
    /// </para>
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class CanonicalSnapshotTests : IDisposable
    {
        private readonly string directory;

        public CanonicalSnapshotTests()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM-UI-S04-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>A. The same saved model, opened twice as the window opens it: the UI views differ, the snapshots and hashes do not.</summary>
        [WpfFact]
        public void SameSavedModel_OpenedTwice_GivesTheSameSnapshotAndCanonicalHash()
        {
            string path = Save(Model(), "model.json");

            AnalyticalModel opened_1 = Open(path);
            AnalyticalModel opened_2 = Open(path);

            //The F7 precondition: opening regenerated the UI view state, with different GUIDs each time.
            Assert.NotEqual(ViewGuids(opened_1), ViewGuids(opened_2));

            string snapshot_1 = Freeze(opened_1, "1");
            string snapshot_2 = Freeze(opened_2, "2");

            Assert.Equal(Query.Sha256(snapshot_1), Query.Sha256(snapshot_2));
            Assert.Equal(ProducerCanonicalHash(snapshot_1), ProducerCanonicalHash(snapshot_2));
        }

        /// <summary>B. The same model with different UI-only view settings (new GUIDs, another view, a hidden object): same hash.</summary>
        [WpfFact]
        public void DifferentUiViewState_GivesTheSameCanonicalHash()
        {
            AnalyticalModel analyticalModel_1 = Model();
            AnalyticalModel analyticalModel_2 = new AnalyticalModel(analyticalModel_1);
            analyticalModel_1.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View"));
            analyticalModel_2.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View", "Plan"));

            Assert.NotEqual(Core.Convert.ToString(analyticalModel_1), Core.Convert.ToString(analyticalModel_2));
            Assert.Equal(ProducerCanonicalHash(Freeze(analyticalModel_1, "1")), ProducerCanonicalHash(Freeze(analyticalModel_2, "2")));
        }

        /// <summary>C. Every kind of engineering edit still changes the snapshot and the canonical hash.</summary>
        [WpfTheory]
        [InlineData("construction")]
        [InlineData("space")]
        [InlineData("weather")]
        [InlineData("modelParameter")]
        [InlineData("otherUiAssemblyParameterName")]
        public void AnEngineeringEdit_ChangesTheCanonicalHash(string edit)
        {
            AnalyticalModel analyticalModel = Model();
            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View"));
            string hash_Before = ProducerCanonicalHash(Freeze(analyticalModel, "before"));

            AnalyticalModel analyticalModel_Edited = Edit(analyticalModel, edit);
            analyticalModel_Edited.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View"));

            Assert.NotEqual(hash_Before, ProducerCanonicalHash(Freeze(analyticalModel_Edited, "after")));
        }

        /// <summary>
        /// The snapshot is the source model less exactly one parameter: nothing else is dropped, reordered or regenerated (the omission
        /// direction of the mutation check).
        /// </summary>
        [WpfFact]
        public void Snapshot_IsTheModelLessOnlyTheUiViewState()
        {
            AnalyticalModel analyticalModel = Model();
            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View"));

            JsonObject expected = analyticalModel.ToJsonObject();
            JsonObject uiParameterSet = expected["ParameterSets"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string)x["Name"]! == "SAM.Analytical.UI");
            JsonArray parameters = uiParameterSet["Parameters"]!.AsArray();
            parameters.Remove(parameters.Single(x => (string)x!["Name"]! == "UI Geometry Settings"));

            AnalyticalModel snapshot = Core.Convert.ToSAM<AnalyticalModel>(Freeze(analyticalModel, "snapshot")).Single();

            Assert.Equal(new AnalyticalModel(expected).ToJsonObject().ToJsonString(), snapshot.ToJsonObject().ToJsonString());
            Assert.False(snapshot.HasValue(AnalyticalModelParameter.UIGeometrySettings));
            Assert.True(snapshot.HasValue(Analytical.AnalyticalModelParameter.WeatherData));
        }

        /// <summary>G. Freezing never changes the model given - not its view state, not anything else.</summary>
        [WpfFact]
        public void Freezing_DoesNotChangeTheSourceModel()
        {
            AnalyticalModel analyticalModel = Model();
            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View"));
            string json_Before = Core.Convert.ToString(analyticalModel);

            Freeze(analyticalModel, "1");

            Assert.Equal(json_Before, Core.Convert.ToString(analyticalModel));
            Assert.True(analyticalModel.HasValue(AnalyticalModelParameter.UIGeometrySettings));
        }

        /// <summary>
        /// D, E. Two Both runs of the same model opened in two sessions feed their producers byte-identical snapshots: the snapshot hash
        /// in each manifest is its file's, and both producers of one run read that one file.
        /// </summary>
        [WpfFact]
        public void TwoBothRuns_OfTheSameModelOpenedTwice_FeedIdenticalSnapshots()
        {
            string path = Save(Model(), "model.json");
            string epw = Path.Combine(directory, "test.epw");
            File.WriteAllText(epw, "LOCATION,Test,,,TMY,000000,0,0,0,0\n");

            List<EngineComparisonRun> runs = new List<EngineComparisonRun>();
            foreach (string session in new[] { "session-1", "session-2" })
            {
                List<string> models = new List<string>();
                EngineComparisonRun run = Modify.RunEngineComparison(Open(path), Path.Combine(directory, session), epw, Producer("benchmark-tas.exe"), Producer("benchmark-openstudio.exe"), path, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
                {
                    models.Add(producerRun.Arguments[producerRun.Arguments.IndexOf("--model") + 1]);
                    return 5;
                });

                Assert.Equal(new[] { run.SnapshotPath, run.SnapshotPath }, models);
                Assert.Equal(Query.Sha256(run.SnapshotPath), run.SnapshotSha256);
                Assert.Equal(run.SnapshotSha256, (string)JsonNode.Parse(File.ReadAllText(run.ManifestPath!))!["snapshot"]!["sha256"]!);
                runs.Add(run);
            }

            Assert.NotEqual(runs[0].SnapshotPath, runs[1].SnapshotPath);
            Assert.Equal(runs[0].SnapshotSha256, runs[1].SnapshotSha256);
            Assert.Equal(ProducerCanonicalHash(runs[0].SnapshotPath!), ProducerCanonicalHash(runs[1].SnapshotPath!));
        }

        /// <summary>F. The OpenStudio result model, built from the frozen copy, still carries the open model's view state, as before S04.</summary>
        [WpfFact]
        public void OpenStudioResultModel_KeepsTheSourceViewState()
        {
            AnalyticalModel analyticalModel = Model();
            UIGeometrySettings uIGeometrySettings = ViewState("3D View", "Plan");
            analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, uIGeometrySettings);

            AnalyticalModel analyticalModel_Frozen = Core.Convert.ToSAM<AnalyticalModel>(Freeze(analyticalModel, "frozen")).Single();
            Assert.False(analyticalModel_Frozen.HasValue(AnalyticalModelParameter.UIGeometrySettings));

            AnalyticalModel analyticalModel_Result = Modify.WithViewState(analyticalModel_Frozen, analyticalModel);

            Assert.True(analyticalModel_Result.TryGetValue(AnalyticalModelParameter.UIGeometrySettings, out UIGeometrySettings uIGeometrySettings_Result));
            Assert.Equal(uIGeometrySettings.ToJsonObject().ToJsonString(), uIGeometrySettings_Result.ToJsonObject().ToJsonString());
            Assert.NotSame(uIGeometrySettings, uIGeometrySettings_Result);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>As benchmark-tas and benchmark-openstudio compute canonicalModelHash: the snapshot loaded, then canonical JSON v1 of its JSON.</summary>
        private static string ProducerCanonicalHash(string path)
        {
            AnalyticalModel analyticalModel = Core.Convert.ToSAM<AnalyticalModel>(path).First();
            return BenchmarkCanonicalJson.ComputeSha256(analyticalModel.ToJsonObject().ToJsonString());
        }

        private string Freeze(AnalyticalModel analyticalModel, string name)
        {
            string path = Path.Combine(directory, "snapshot-" + name + ".json");
            Assert.NotNull(analyticalModel.FreezeAnalyticalModel(path));
            return path;
        }

        private string Save(AnalyticalModel analyticalModel, string name)
        {
            string path = Path.Combine(directory, name);
            Assert.True(Core.Convert.ToFile(new IJSAMObject[] { analyticalModel }, path));
            return path;
        }

        /// <summary>
        /// Opens a saved model as the window does: <see cref="UIAnalyticalModel.Open"/>, then what <c>AnalyticalWindow.SetDefaultViewSettings</c>
        /// does on <c>Opened</c> - a model with no views gets <c>UI.Query.DefaultViewSettings</c>, whose GUID is new every time (F7).
        /// </summary>
        private static AnalyticalModel Open(string path)
        {
            UIAnalyticalModel uIAnalyticalModel = new UIAnalyticalModel(path);
            Assert.True(uIAnalyticalModel.Open());

            AnalyticalModel analyticalModel = uIAnalyticalModel.JSAMObject;
            if (!analyticalModel.TryGetValue(AnalyticalModelParameter.UIGeometrySettings, out UIGeometrySettings uIGeometrySettings) || uIGeometrySettings == null)
            {
                uIGeometrySettings = new UIGeometrySettings();
            }

            if (uIGeometrySettings.GetViewSettings<IViewSettings>() is not { Count: > 0 })
            {
                ViewSettings viewSettings = UI.Query.DefaultViewSettings(analyticalModel);
                uIGeometrySettings.AddViewSettings(viewSettings);
                uIGeometrySettings.ActiveGuid = viewSettings.Guid;
                analyticalModel.SetValue(AnalyticalModelParameter.UIGeometrySettings, uIGeometrySettings);
            }

            return analyticalModel;
        }

        private string Producer(string fileName)
        {
            string path = Path.Combine(directory, "producers", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        private static string ViewGuids(AnalyticalModel analyticalModel)
        {
            Assert.True(analyticalModel.TryGetValue(AnalyticalModelParameter.UIGeometrySettings, out UIGeometrySettings uIGeometrySettings));
            return uIGeometrySettings.ToJsonObject().ToJsonString();
        }

        /// <summary>A realistic multizone model: the Part O fixture's flats (spaces, panels, apertures, constructions, internal conditions), with weather.</summary>
        private static AnalyticalModel Model()
        {
            AnalyticalModel analyticalModel = PartOMixedDesignFixture.Baseline(2);
            analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, new WeatherData("Fixture", "Fixture", 51.5, -0.1, 25));
            return analyticalModel;
        }

        private static AnalyticalModel Edit(AnalyticalModel analyticalModel, string edit)
        {
            AnalyticalModel result = new AnalyticalModel(analyticalModel, true);
            switch (edit)
            {
                case "construction":
                    AdjacencyCluster adjacencyCluster = result.AdjacencyCluster;
                    Construction construction = new Construction(Guid.NewGuid(), "Thicker", new List<ConstructionLayer> { new ConstructionLayer("Plasterboard", 0.2) });
                    Panel panel = adjacencyCluster.GetPanels().First();
                    adjacencyCluster.AddObject(Analytical.Create.Panel(panel, construction));
                    return new AnalyticalModel(result, adjacencyCluster);
                case "space":
                    AdjacencyCluster adjacencyCluster_Space = result.AdjacencyCluster;
                    Space space = adjacencyCluster_Space.GetSpaces().First();
                    Space space_Edited = new Space(space);
                    space_Edited.SetValue(SpaceParameter.Volume, 123.0);
                    adjacencyCluster_Space.AddObject(space_Edited);
                    return new AnalyticalModel(result, adjacencyCluster_Space);
                case "weather":
                    result.SetValue(Analytical.AnalyticalModelParameter.WeatherData, new WeatherData("Fixture", "Fixture", 52.5, -0.1, 25));
                    return result;
                case "modelParameter":
                    result.SetValue(Analytical.AnalyticalModelParameter.NorthAngle, 0.5);
                    return result;
                case "otherUiAssemblyParameterName":
                    //Anything else stored in the SAM.Analytical.UI set is kept: only the named view state is UI-only.
                    result.SetValue(AnalyticalModelParameter.UIGeometrySettings, ViewState("3D View"));
                    Assert.True(result.GetParameterSet(typeof(AnalyticalModelParameter).Assembly).Add("Another UI parameter", "kept"));
                    return result;
            }

            throw new ArgumentOutOfRangeException(nameof(edit));
        }

        private static UIGeometrySettings ViewState(params string[] names)
        {
            UIGeometrySettings result = new UIGeometrySettings();
            foreach (string name in names)
            {
                result.AddViewSettings(new ThreeDimensionalViewSettings(Guid.NewGuid(), name, null, null, null));
            }

            return result;
        }
    }
}
