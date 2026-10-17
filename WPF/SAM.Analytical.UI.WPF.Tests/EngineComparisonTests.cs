// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Benchmark.Compare;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Simulate &gt; Compare Tas / OpenStudio (SAM_UI#231). The documents are SAM_Validation's FABRICATED regression documents
    /// (Resources/EngineComparison/README.md), not engine results. These tests prove the view computes nothing - every cell equals
    /// SAM_Validation's own comparison.csv - and that the orchestration feeds both producers one snapshot and one weather file, then
    /// compares and reports through SAM_Validation. The producers are faked here; the real engines are the manual Windows test.
    /// </summary>
    public class EngineComparisonTests : IDisposable
    {
        private readonly string directory;

        public EngineComparisonTests()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM-UI-231-tests", Guid.NewGuid().ToString("N"));
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

        private static string Resource(string name, [CallerFilePath] string sourceFilePath = null)
        {
            return Path.Combine(Path.GetDirectoryName(sourceFilePath), "Resources", "EngineComparison", name);
        }

        private static string Tas => Resource("scenario-benchmark-TAS.json");

        private static string OpenStudio => Resource("scenario-benchmark-OpenStudio.json");

        private static string OpenStudio_WeatherMismatch => Resource("weather-mismatch-benchmark-OpenStudio.json");

        [Theory]
        [InlineData("scenario-benchmark-OpenStudio.json")]
        [InlineData("weather-mismatch-benchmark-OpenStudio.json")]
        public void EveryCell_EqualsSamValidationsComparisonCsv(string openStudio)
        {
            EngineComparison engineComparison = Query.LoadEngineComparison(Tas, Resource(openStudio));

            List<string[]> csv = Benchmark.Compare.Modify.WriteCsv(engineComparison.ComparisonResult).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(ParseCsvLine).ToList();
            List<EngineComparisonRow> rows = engineComparison.ModelRows.Concat(engineComparison.SpaceRows).ToList();

            Assert.Equal(csv.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                string[] expected = csv[i];
                EngineComparisonRow row = rows[i];
                Assert.Equal(expected[0], row.Scope);
                Assert.Equal(expected[1], row.Metric);
                Assert.Equal(expected[2], row.Unit);
                Assert.Equal(expected[4], row.Tas);
                Assert.Equal(expected[5], row.OpenStudio);
                Assert.Equal(expected[6], row.AbsoluteDifference);
                Assert.Equal(expected[8], row.PercentDifference);
                Assert.Equal(expected[10], row.Band);
                Assert.Equal(expected[11], row.Note);
            }
        }

        [Fact]
        public void Status_FollowsSamValidationsFlags_AndMissingValuesAreNeverZero()
        {
            EngineComparison engineComparison = Query.LoadEngineComparison(Tas, OpenStudio);
            List<EngineComparisonRow> rows = engineComparison.ModelRows.Concat(engineComparison.SpaceRows).ToList();

            foreach (EngineComparisonRow row in rows)
            {
                MetricComparison metric = row.MetricComparison;
                switch (row.Status)
                {
                    case EngineComparisonRowStatus.Compared:
                        Assert.Equal(ComparisonAvailability.Both, metric.Availability);
                        Assert.False(Benchmark.Compare.Query.IsInformationalForNumericalGate(metric));
                        Assert.NotEqual("N/A", row.Band);
                        break;
                    case EngineComparisonRowStatus.NotEquivalent:
                        Assert.Equal(ComparisonAvailability.Both, metric.Availability);
                        Assert.True(Benchmark.Compare.Query.IsInformationalForNumericalGate(metric) || metric.NotApplicableReason == NotApplicableReason.UnitMismatch);
                        break;
                    case EngineComparisonRowStatus.MissingOnTas:
                        Assert.Equal(ComparisonAvailability.OpenStudioOnly, metric.Availability);
                        Assert.Equal(string.Empty, row.Tas);
                        Assert.Equal(string.Empty, row.AbsoluteDifference);
                        break;
                    case EngineComparisonRowStatus.MissingOnOpenStudio:
                        Assert.Equal(ComparisonAvailability.TasOnly, metric.Availability);
                        Assert.Equal(string.Empty, row.OpenStudio);
                        Assert.Equal(string.Empty, row.AbsoluteDifference);
                        break;
                    case EngineComparisonRowStatus.Unavailable:
                        Assert.Equal(ComparisonAvailability.Neither, metric.Availability);
                        Assert.Equal(string.Empty, row.Tas);
                        Assert.Equal(string.Empty, row.OpenStudio);
                        break;
                }
            }

            //The scenario spans the cases the view must tell apart.
            Assert.Contains(rows, x => x.Status == EngineComparisonRowStatus.Compared);
            Assert.Contains(rows, x => x.Status == EngineComparisonRowStatus.NotEquivalent);
            Assert.Contains(rows, x => x.Status == EngineComparisonRowStatus.MissingOnTas || x.Status == EngineComparisonRowStatus.MissingOnOpenStudio);
            Assert.Contains(rows, x => x.Status == EngineComparisonRowStatus.Unavailable);
            Assert.Contains(rows, x => x.Band == "Fail");
        }

        [Fact]
        public void Provenance_NamesTheWeatherMismatch_AndTheMissingInputManifests()
        {
            EngineComparison engineComparison = Query.LoadEngineComparison(Tas, OpenStudio_WeatherMismatch);

            string provenance = Query.EngineComparisonProvenance(engineComparison);

            Assert.False(engineComparison.ComparisonResult.ProvenanceCompatibility.IsCompatible);
            Assert.Contains("MISMATCH weatherHash", provenance);
            Assert.Contains("Input manifests", provenance);
            Assert.Contains("not available", provenance);
        }

        [Fact]
        public void Diagnostics_ListUnmatchedSpaces_GateAndReconciliation()
        {
            EngineComparison engineComparison = Query.LoadEngineComparison(Tas, OpenStudio);
            SpaceMatchDiagnostics spaceMatchDiagnostics = engineComparison.ComparisonResult.SpaceDiagnostics;

            string diagnostics = Query.EngineComparisonDiagnostics(engineComparison);

            Assert.Contains("Overall gate: " + Format.Gate(engineComparison.ComparisonResult.Gate), diagnostics);
            Assert.NotEmpty(spaceMatchDiagnostics.OnlyInTas.Concat(spaceMatchDiagnostics.OnlyInOpenStudio));
            foreach (string space in spaceMatchDiagnostics.OnlyInTas.Concat(spaceMatchDiagnostics.OnlyInOpenStudio))
            {
                Assert.Contains(space, diagnostics);
            }
        }

        [Fact]
        public void Run_FeedsBothProducersOneSnapshotAndOneWeatherFile_ThenComparesAndReports_WithoutChangingTheModel()
        {
            AnalyticalModel analyticalModel = Model();
            string json_Before = Core.Convert.ToString(analyticalModel);
            string epwPath = Epw();

            List<EngineComparisonProducerRun> calls = new List<EngineComparisonProducerRun>();
            EngineComparisonRun run = Modify.RunEngineComparison(analyticalModel, directory, epwPath, ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), @"C:\models\source.json", null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                calls.Add(producerRun);
                File.Copy(producerRun.Engine == ComparisonEngine.Tas ? Tas : OpenStudio, Argument(producerRun, "--out"));
                return 0;
            });

            Assert.Equal(EngineComparisonRunStatus.Compared, run.Status);
            Assert.Equal(new[] { ComparisonEngine.Tas, ComparisonEngine.OpenStudio }, calls.Select(x => x.Engine));

            //One snapshot and one weather file, both in the run folder, for both engines.
            Assert.All(calls, x => Assert.Equal(run.SnapshotPath, Argument(x, "--model")));
            Assert.All(calls, x => Assert.Equal(run.WeatherPath, Argument(x, "--weather")));
            Assert.Equal(Query.Sha256(epwPath), run.WeatherSha256);
            Assert.Equal("File", run.WeatherSource);
            Assert.NotNull(Argument(calls[0], "--tbd"));
            Assert.NotNull(Argument(calls[1], "--work"));

            //The reports are SAM_Validation's, for these documents.
            Assert.Equal(3, run.ReportPaths.Count);
            EngineComparison engineComparison = Query.LoadEngineComparison(Tas, OpenStudio);
            Assert.Equal(Benchmark.Compare.Modify.WriteCsv(engineComparison.ComparisonResult), File.ReadAllText(Path.Combine(run.Directory, Benchmark.Compare.Modify.CsvFileName)));
            Assert.Equal(Format.Gate(engineComparison.ComparisonResult.Gate), run.Gate);

            Assert.True(File.Exists(run.ManifestPath));
            Assert.Equal("Compared", (string)JsonNode.Parse(File.ReadAllText(run.ManifestPath))["status"]);
            Assert.Equal(json_Before, Core.Convert.ToString(analyticalModel));
        }

        [Fact]
        public void Run_WithAFailedProducer_IsFailed_AndComparesNothing()
        {
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                if (producerRun.Engine == ComparisonEngine.OpenStudio)
                {
                    File.Copy(OpenStudio, Argument(producerRun, "--out"));
                    return 0;
                }

                return 5;
            });

            Assert.Equal(EngineComparisonRunStatus.Failed, run.Status);
            Assert.Empty(run.ReportPaths);
            Assert.Null(run.Gate);
            Assert.Contains("exited with code 5", run.ProducerRuns[0].Error);
            Assert.Contains(run.Diagnostics, x => x.Contains("Tas wrote no B1a document"));
        }

        [Fact]
        public void Run_Cancelled_StopsAndSaysSo()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, cancellationTokenSource.Token, (producerRun, workDirectory, cancellationToken) =>
            {
                cancellationTokenSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return 0;
            });

            Assert.Equal(EngineComparisonRunStatus.Cancelled, run.Status);
            Assert.Single(run.ProducerRuns);
            Assert.Empty(run.ReportPaths);
        }

        [Fact]
        public void Run_WithoutWeather_RunsNothing()
        {
            int calls = 0;
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, null, ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) => ++calls);

            //No EPW was given and the model has no WeatherData; on a machine without the OpenStudio stack the embedded-weather export
            //itself fails to load. Either way nothing runs.
            Assert.Equal(EngineComparisonRunStatus.Failed, run.Status);
            Assert.Equal(0, calls);
            Assert.True(run.Exception != null || run.Diagnostics.Any(x => x.Contains("no weather")));
        }

        [Fact]
        public void Producer_IsFoundByVariableOrSavedLocation_AndArgumentsMatchTheProducers()
        {
            string settingsPath = Path.Combine(directory, "producers.json");
            string producer = ProducerPath("benchmark-openstudio.exe");

            string variable = Query.BenchmarkProducerVariable(ComparisonEngine.OpenStudio);
            string previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, null);
                Assert.Null(Query.BenchmarkProducerPath(ComparisonEngine.OpenStudio, settingsPath));

                Assert.True(Modify.SaveBenchmarkProducerPath(ComparisonEngine.OpenStudio, producer, settingsPath));
                Assert.Equal(producer, Query.BenchmarkProducerPath(ComparisonEngine.OpenStudio, settingsPath));

                //The variable may name the folder.
                Environment.SetEnvironmentVariable(variable, Path.GetDirectoryName(producer));
                Assert.Equal(producer, Query.BenchmarkProducerPath(ComparisonEngine.OpenStudio, Path.Combine(directory, "none.json")));
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }

            //benchmark-tas --model --weather --tbd --out; benchmark-openstudio --model --weather --out --work.
            List<string> tas = Query.BenchmarkProducerArguments(ComparisonEngine.Tas, "m.json", "w.epw", "b.json", "work", "Block A");
            Assert.Equal(new[] { "--model", "m.json", "--weather", "w.epw", "--tbd", Path.Combine("work", "Block_A.tbd"), "--out", "b.json" }, tas);
            List<string> openStudio = Query.BenchmarkProducerArguments(ComparisonEngine.OpenStudio, "m.json", "w.epw", "b.json", "work", "Block A");
            Assert.Equal(new[] { "--model", "m.json", "--weather", "w.epw", "--out", "b.json", "--work", "work" }, openStudio);
        }

        [Fact]
        public void Folder_WithoutB1aDocuments_FindsNone()
        {
            (string tas, string openStudio) = Query.EngineComparisonDocuments(directory);
            Assert.Null(tas);
            Assert.Null(openStudio);

            File.Copy(Tas, Path.Combine(directory, Query.TasBenchmarkFileName));
            (tas, openStudio) = Query.EngineComparisonDocuments(directory);
            Assert.NotNull(tas);
            Assert.Null(openStudio);
        }

        private static AnalyticalModel Model()
        {
            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), "Test model");
            Assert.True(analyticalModel.AddSpace(new Space("Room 1", new Geometry.Spatial.Point3D(1, 1, 1))));
            return analyticalModel;
        }

        private string Epw()
        {
            string path = Path.Combine(directory, "test.epw");
            File.WriteAllText(path, "LOCATION,Test,,,TMY,000000,0,0,0,0\n");
            return path;
        }

        private string ProducerPath(string fileName)
        {
            string path = Path.Combine(directory, "producers", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Empty);
            return path;
        }

        private static string Argument(EngineComparisonProducerRun producerRun, string name)
        {
            int index = producerRun.Arguments.IndexOf(name);
            return index < 0 ? null : producerRun.Arguments[index + 1];
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> fields = new List<string>();
            System.Text.StringBuilder field = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }

            fields.Add(field.ToString());
            return fields.ToArray();
        }
    }
}
