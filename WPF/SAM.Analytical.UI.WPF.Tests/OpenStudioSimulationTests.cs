// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Simulate &gt; OpenStudio Simulation (SAM_UI#229). Everything except the last test runs without OpenStudio: the snapshot/copy
    /// guarantee, the run folder and file names, the result model holding only this run's results, the manifest, and a failed run
    /// leaving the open model as it was. The last test is the real run, and returns early on a machine without openstudio.exe.
    /// </summary>
    public class OpenStudioSimulationTests : IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly string directory;

        public OpenStudioSimulationTests(ITestOutputHelper output)
        {
            this.output = output;
            directory = Path.Combine(Path.GetTempPath(), "SAM-UI-229-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static AnalyticalModel Model(bool withTasResult = false)
        {
            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), "Test model");
            Assert.True(analyticalModel.AddSpace(new Space("Room 1", new Geometry.Spatial.Point3D(1, 1, 1))));

            if (withTasResult)
            {
                AnalyticalModelSimulationResult tasResult = new AnalyticalModelSimulationResult(analyticalModel.Name, AnalyticalModelSimulationResult.TasSource, analyticalModel.Guid.ToString("N"));
                tasResult.SetValue(AnalyticalModelSimulationResultParameter.ConsumptionHeating, 1234.0);
                Assert.True(analyticalModel.AddResult(tasResult));
            }

            return analyticalModel;
        }

        [Fact]
        public void Freeze_WritesTheSnapshot_AndReturnsAnIndependentCopyOfIt()
        {
            AnalyticalModel analyticalModel = Model(withTasResult: true);
            string json_Before = Core.Convert.ToString(analyticalModel);
            string path = Path.Combine(directory, OpenStudioSimulationRun.SnapshotFileName);

            AnalyticalModel frozen = analyticalModel.FreezeAnalyticalModel(path);

            Assert.NotNull(frozen);
            Assert.True(File.Exists(path));
            Assert.NotSame(analyticalModel, frozen);
            Assert.Equal(analyticalModel.Guid, frozen.Guid);
            Assert.Equal(json_Before, Core.Convert.ToString(frozen));

            //Changing the copy leaves the original alone.
            frozen.Remove(frozen.GetResults<Core.Result>());
            Assert.Empty(frozen.GetResults<Core.Result>());
            Assert.Equal(json_Before, Core.Convert.ToString(analyticalModel));
        }

        [Fact]
        public void AddOpenStudioResults_RemovesEveryEarlierResult_SoTheResultModelHoldsOnlyThisRunsResults()
        {
            AnalyticalModel frozen = Model(withTasResult: true);
            Assert.Single(frozen.GetResults<Core.Result>());

            //No SQL: SAM_OpenStudio's AddResults attaches nothing, and no result set means no model-level result. What is left is
            //the removal, which must hold whatever the run produced.
            AnalyticalModel result = frozen.AddOpenStudioResults(Path.Combine(directory, "missing.sql"), null, out int removedCount, out int addedCount, out List<string> _);

            Assert.Equal(1, removedCount);
            Assert.Equal(0, addedCount);
            Assert.Empty(result.GetResults<Core.Result>());
            Assert.Empty(result.GetResults<Core.Result>(AnalyticalModelSimulationResult.TasSource));
        }

        [Fact]
        public void RunDirectory_IsUnderTheChosenFolder_NamedForTheModel_AndUniquePerRun()
        {
            DateTime startedUtc = new DateTime(2026, 10, 17, 9, 5, 7, DateTimeKind.Utc);

            string first = Query.OpenStudioRunDirectory(directory, "Block A: Level 1", startedUtc, "0123abcd");
            string second = Query.OpenStudioRunDirectory(directory, "Block A: Level 1", startedUtc, "89abcdef");

            Assert.Equal(directory, Path.GetDirectoryName(first));
            Assert.Equal("Block_A__Level_1_OpenStudio_20261017-090507_0123abcd", Path.GetFileName(first));
            Assert.NotEqual(first, second);
            Assert.Equal("Model_OpenStudio_20261017-090507_0123abcd", Path.GetFileName(Query.OpenStudioRunDirectory(directory, null, startedUtc, "0123abcd")));
            Assert.Equal("Block_A__Level_1_OpenStudio.json", Query.OpenStudioResultModelFileName("Block A: Level 1"));
            Assert.Equal(64, Path.GetFileNameWithoutExtension(Query.OpenStudioResultModelFileName(new string('x', 200))).Length - "_OpenStudio".Length);
        }

        [Fact]
        public void Sha256_IsTheB1aNotationOfTheFileBytes()
        {
            string path = Path.Combine(directory, "abc.txt");
            File.WriteAllText(path, "abc");

            Assert.Equal("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Query.Sha256(path));
            Assert.Null(Query.Sha256(Path.Combine(directory, "none.txt")));
        }

        [Fact]
        public void LoadFailure_IsRecognised_ThroughWrappers_AndOtherFailuresAreNot()
        {
            Assert.True(Query.IsOpenStudioLoadFailure(new DllNotFoundException("openstudio_csharp")));
            Assert.True(Query.IsOpenStudioLoadFailure(new TypeInitializationException("OpenStudio.OpenStudioUtilitiesCorePINVOKE", new DllNotFoundException("openstudiolib"))));
            Assert.True(Query.IsOpenStudioLoadFailure(new FileNotFoundException("missing", "SAM.Analytical.OpenStudio, Version=1.0.0.0")));
            Assert.False(Query.IsOpenStudioLoadFailure(new FileNotFoundException("missing", @"C:\weather\city.epw")));
            Assert.False(Query.IsOpenStudioLoadFailure(new InvalidOperationException("model")));
            Assert.False(Query.IsOpenStudioLoadFailure(null));

            Assert.Contains("openstudiolib.dll", Query.OpenStudioLoadFailure(new DllNotFoundException("openstudiolib")));
        }

        [Fact]
        public void Manifest_WritesPathsInsideTheRunFolderRelatively_AndKeepsOthersAbsolute()
        {
            string runDirectory = Path.Combine(directory, "run1");
            string cliPath = Path.Combine(directory, "OpenStudio", "bin", "openstudio.exe");
            OpenStudioSimulationRun run = new OpenStudioSimulationRun()
            {
                Id = "0123abcd",
                Status = OpenStudioSimulationStatus.Succeeded,
                StartedUtc = new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc),
                FinishedUtc = new DateTime(2026, 10, 17, 9, 1, 30, DateTimeKind.Utc),
                Directory = runDirectory,
                SnapshotPath = Path.Combine(runDirectory, OpenStudioSimulationRun.SnapshotFileName),
                SnapshotSha256 = "sha256:00",
                SqlPath = Path.Combine(runDirectory, "run", "eplusout.sql"),
                CliPath = cliPath,
                ResultCount = 7,
            };

            JsonObject jsonObject = JsonNode.Parse(run.ToJson()).AsObject();

            Assert.Equal(OpenStudioSimulationRun.Schema, (string)jsonObject["schema"]);
            Assert.Equal("Succeeded", (string)jsonObject["status"]);
            Assert.Equal(90.0, (double)jsonObject["durationSeconds"]);
            Assert.Equal("snapshot.json", (string)jsonObject["snapshot"]["path"]);
            Assert.Equal("run/eplusout.sql", (string)jsonObject["outputs"]["sql"]);
            Assert.Equal(cliPath, (string)jsonObject["engine"]["cliPath"]);
            Assert.Equal("Native-OpenStudio", (string)jsonObject["engine"]["route"]);
            Assert.Equal(7, (int)jsonObject["resultCount"]);
        }

        [Fact]
        public void Manifest_WithDiagnosticsAndAnError_IsWritten()
        {
            //A real run nearly always has diagnostics; the manifest must still serialise (it once threw on the first diagnostic).
            OpenStudioSimulationRun run = new OpenStudioSimulationRun() { Id = "0123abcd", Directory = directory, Exception = new InvalidOperationException("boom") };
            run.Diagnostics.Add("Warning SAM-OS-RUN-001: a warning");
            run.Diagnostics.Add("Error SAM-OS-RUN-001: an error");
            run.ResultDiagnostics.Add("No simulation result found for space 'Cell 1'");

            JsonObject jsonObject = JsonNode.Parse(run.ToJson()).AsObject();

            Assert.Equal(2, jsonObject["diagnostics"].AsArray().Count);
            Assert.Equal("Error SAM-OS-RUN-001: an error", (string)jsonObject["diagnostics"][1]);
            Assert.Single(jsonObject["resultDiagnostics"].AsArray());
            Assert.Equal("InvalidOperationException: boom", (string)jsonObject["error"]);
        }

        [Fact]
        public void Run_WithoutAModelOrAFolder_FailsWithoutThrowing()
        {
            OpenStudioSimulationRun run = Modify.RunOpenStudioSimulation(null, directory);

            Assert.Equal(OpenStudioSimulationStatus.Failed, run.Status);
            Assert.Null(run.Directory);
            Assert.NotEmpty(run.Diagnostics);
        }

        /// <summary>
        /// A model with no weather and no EPW: SAM_OpenStudio blocks the run (on a machine with the OpenStudio stack) or the stack
        /// cannot load (on one without). Either way the run fails, records why, keeps its snapshot and manifest - and the open model is
        /// exactly as it was.
        /// </summary>
        [Fact]
        public void FailedRun_LeavesTheModelUnchanged_AndRecordsItself()
        {
            AnalyticalModel analyticalModel = Model(withTasResult: true);
            string json_Before = Core.Convert.ToString(analyticalModel);

            OpenStudioSimulationRun run = analyticalModel.RunOpenStudioSimulation(directory, null, @"C:\models\source.json");

            output.WriteLine(Query.OpenStudioSimulationSummary(run));

            Assert.Equal(OpenStudioSimulationStatus.Failed, run.Status);
            Assert.Null(run.ResultModelPath);
            Assert.True(run.Exception != null || run.Diagnostics.Any(x => x.StartsWith("Error", StringComparison.Ordinal)));
            Assert.Equal(json_Before, Core.Convert.ToString(analyticalModel));

            Assert.True(File.Exists(run.SnapshotPath));
            Assert.StartsWith("sha256:", run.SnapshotSha256);
            Assert.True(File.Exists(run.ManifestPath));
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(run.ManifestPath)).AsObject();
            Assert.Equal("Failed", (string)manifest["status"]);
            Assert.Equal(@"C:\models\source.json", (string)manifest["sourcePath"]);
            Assert.Contains("failed", Query.OpenStudioSimulationSummary(run));
        }

        /// <summary>
        /// The real run (gate G1's automated part): SAM_OpenStudio's two-cell fixture with its embedded weather, through openstudio.exe.
        /// Returns early - and says so - where OpenStudio is not installed or the SAM_OpenStudio checkout is not beside SAM_UI;
        /// SAM_OPENSTUDIO_UI_TEST_MODEL / SAM_OPENSTUDIO_UI_TEST_EPW override the model and add an explicit EPW.
        /// </summary>
        [Fact]
        [Trait("Category", "Simulation")]
        public void RealRun_SimulatesAFrozenCopy_AndSavesAResultModelWithOnlyOpenStudioResults()
        {
            string cliPath = Core.OpenStudio.Query.OpenStudioCliPath(null);
            string modelPath = Environment.GetEnvironmentVariable("SAM_OPENSTUDIO_UI_TEST_MODEL") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\SAM_OpenStudio\tests\resources\models\rawModel-WindowsWeatherDataDDYAdiabaticAirPanel.sam"));
            string epwPath = Environment.GetEnvironmentVariable("SAM_OPENSTUDIO_UI_TEST_EPW");
            if (cliPath == null || !File.Exists(modelPath))
            {
                output.WriteLine(string.Format("NOT RUN: openstudio.exe {0}; model {1} {2}", cliPath ?? "not found", modelPath, File.Exists(modelPath) ? "found" : "not found"));
                return;
            }

            AnalyticalModel analyticalModel = Core.Convert.ToSAM<AnalyticalModel>(modelPath).First();
            string json_Before = Core.Convert.ToString(analyticalModel);

            OpenStudioSimulationRun run = analyticalModel.RunOpenStudioSimulation(directory, epwPath, modelPath);

            output.WriteLine(Query.OpenStudioSimulationSummary(run, int.MaxValue));
            output.WriteLine(run.ToJson());

            Assert.Equal(OpenStudioSimulationStatus.Succeeded, run.Status);
            Assert.Equal(0, run.FatalCount);
            Assert.True(File.Exists(run.SqlPath));
            Assert.True(File.Exists(run.WeatherPath));
            Assert.Equal(json_Before, Core.Convert.ToString(analyticalModel));

            AnalyticalModel result = Core.Convert.ToSAM<AnalyticalModel>(run.ResultModelPath).First();
            List<Core.Result> results = result.GetResults<Core.Result>();
            Assert.NotEmpty(results);
            Assert.All(results, x => Assert.Equal(AnalyticalModelSimulationResult.OpenStudioSource, x.Source));
            Assert.Single(result.GetAnalyticalModelSimulationResults());
        }
    }
}
