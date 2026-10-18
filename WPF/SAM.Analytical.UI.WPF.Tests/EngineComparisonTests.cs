// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Benchmark;
using SAM.Analytical.Benchmark.Compare;
using System;
using System.Collections.Generic;
using System.Globalization;
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
                //The run's snapshot and weather file are read-only, which Directory.Delete refuses on Windows.
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
                Assert.Equal(expected[10], row.Band);

                //The CSV stays the raw, full-precision contract; the grid is the two-decimal presentation.
                Assert.Equal(expected[4], row.TasFull);
                Assert.Equal(expected[5], row.OpenStudioFull);
                Assert.Equal(expected[6], row.AbsoluteDifferenceFull);
                Assert.Equal(expected[8], row.PercentDifferenceFull);
                Assert.Equal(PresentationFormat.Note(row.MetricComparison), row.Note);

                Assert.Equal(PresentationFormat.Number(row.MetricComparison.TasValue, row.MetricComparison.Unit, System.Globalization.CultureInfo.CurrentCulture, string.Empty), row.Tas);
                Assert.Equal(PresentationFormat.Number(row.MetricComparison.OpenStudioValue, row.MetricComparison.Unit, System.Globalization.CultureInfo.CurrentCulture, string.Empty), row.OpenStudio);
                Assert.Equal(
                    PresentationFormat.Number(row.MetricComparison.AbsoluteDifference, row.MetricComparison.Circular ? MetricUnit.HourOfYear : row.MetricComparison.Unit, System.Globalization.CultureInfo.CurrentCulture, string.Empty),
                    row.AbsoluteDifference);
                Assert.Equal(
                    row.MetricComparison.Circular ? string.Empty : PresentationFormat.Percent(row.MetricComparison.RelativeDifference, System.Globalization.CultureInfo.CurrentCulture, string.Empty),
                    row.PercentDifference);
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
        public void Run_WithTasFailed_IsPartiallyCompleted_KeepsOpenStudio_AndComparesNothing()
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

            Assert.Equal(EngineComparisonRunStatus.PartiallyCompleted, run.Status);
            Assert.Empty(run.ReportPaths);
            Assert.Null(run.Gate);
            Assert.Contains("exited with code 5", run.ProducerRuns[0].Error);
            Assert.False(run.ProducerRuns[0].Succeeded);
            Assert.True(run.ProducerRuns[1].Succeeded);
            Assert.True(File.Exists(run.ProducerRuns[1].BenchmarkPath));
            Assert.Contains(run.Diagnostics, x => x.Contains("not compared - Tas did not complete"));

            JsonNode manifest = JsonNode.Parse(File.ReadAllText(run.ManifestPath));
            Assert.Equal("PartiallyCompleted", (string)manifest["status"]);
            Assert.False((bool)manifest["producers"][0]["succeeded"]);
            Assert.True((bool)manifest["producers"][1]["succeeded"]);
        }

        [Fact]
        public void Run_WithAProducerNotFound_IsPartiallyCompleted_AndTheOtherEngineStillRuns()
        {
            List<ComparisonEngine> calls = new List<ComparisonEngine>();
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, Epw(), ProducerPath("benchmark-tas.exe"), Path.Combine(directory, "missing", "benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                calls.Add(producerRun.Engine);
                File.Copy(Tas, Argument(producerRun, "--out"));
                return 0;
            });

            Assert.Equal(new[] { ComparisonEngine.Tas }, calls);
            Assert.Equal(EngineComparisonRunStatus.PartiallyCompleted, run.Status);
            Assert.Contains("not found", run.ProducerRuns[1].Error);
            Assert.Contains(run.Diagnostics, x => x.Contains("not compared - OpenStudio did not complete"));
            Assert.Empty(run.ReportPaths);
        }

        [Fact]
        public void Run_DocumentOfAFailedProducer_IsKeptButNeverUsed()
        {
            //Both write a document, but Tas exits with 5: its document is not a result, so nothing is compared.
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                File.Copy(producerRun.Engine == ComparisonEngine.Tas ? Tas : OpenStudio, Argument(producerRun, "--out"));
                return producerRun.Engine == ComparisonEngine.Tas ? 5 : 0;
            });

            Assert.Equal(EngineComparisonRunStatus.PartiallyCompleted, run.Status);
            Assert.NotNull(run.ProducerRuns[0].BenchmarkPath);
            Assert.False(run.ProducerRuns[0].Succeeded);
            Assert.Empty(run.ReportPaths);
            Assert.Null(run.Gate);
        }

        [Fact]
        public void Run_WithBothFailed_IsFailed()
        {
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) => 5);

            Assert.Equal(EngineComparisonRunStatus.Failed, run.Status);
            Assert.Equal(2, run.ProducerRuns.Count);
            Assert.Contains(run.Diagnostics, x => x.Contains("neither Tas nor OpenStudio completed"));
            Assert.Empty(run.ReportPaths);
        }

        [Fact]
        public void Run_SnapshotAndWeatherAreReadOnly_AndAChangedSnapshotStopsTheRun()
        {
            List<ComparisonEngine> calls = new List<ComparisonEngine>();
            EngineComparisonRun run = Modify.RunEngineComparison(Model(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                calls.Add(producerRun.Engine);

                string snapshot = Argument(producerRun, "--model");
                Assert.True(File.GetAttributes(snapshot).HasFlag(FileAttributes.ReadOnly));
                Assert.True(File.GetAttributes(Argument(producerRun, "--weather")).HasFlag(FileAttributes.ReadOnly));

                //Something changes the frozen snapshot while Tas runs.
                File.SetAttributes(snapshot, FileAttributes.Normal);
                File.AppendAllText(snapshot, " ");

                File.Copy(Tas, Argument(producerRun, "--out"));
                return 0;
            });

            //OpenStudio is not run on the changed snapshot, and the Tas document is not shown or compared.
            Assert.Equal(new[] { ComparisonEngine.Tas }, calls);
            Assert.Equal(EngineComparisonRunStatus.Failed, run.Status);
            Assert.Contains("snapshot changed after it was frozen", run.ProducerRuns[1].Error);
            Assert.Contains(run.Diagnostics, x => x.Contains("no comparison - the snapshot changed"));
            Assert.Empty(run.ReportPaths);
        }

        [Fact]
        public void EngineResult_ShowsOneEngineOnly_UnderSamValidationsKeys_NotComparedAndNothingFilledIn()
        {
            EngineResult engineResult = Query.LoadEngineResult(ComparisonEngine.OpenStudio, OpenStudio, ComparisonEngine.Tas);
            Benchmark.BenchmarkDocument document = Benchmark.BenchmarkSerializer.Read(OpenStudio);
            EngineComparison engineComparison = Query.LoadEngineComparison(Tas, OpenStudio);

            //The same metric keys, in the same order, as SAM_Validation's comparison rows.
            Assert.Equal(engineComparison.ModelRows.Select(x => x.Metric), engineResult.ModelRows.Select(x => x.Metric));
            Assert.Equal(document.Spaces.Count * Benchmark.Compare.Query.SpaceMetricValues(null).Count, engineResult.SpaceRows.Count);

            IReadOnlyList<KeyValuePair<string, Benchmark.MetricValue>> values = Benchmark.Compare.Query.ModelMetricValues(document.Model);
            for (int i = 0; i < values.Count; i++)
            {
                EngineComparisonRow row = engineResult.ModelRows[i];
                Assert.Equal(EngineComparisonRowStatus.NotCompared, row.Status);
                Assert.Null(row.MetricComparison);
                Assert.Equal(string.Empty, row.Tas);
                Assert.Equal(string.Empty, row.AbsoluteDifference);
                Assert.Equal(string.Empty, row.PercentDifference);
                Assert.Equal(string.Empty, row.Band);
                Assert.Contains("Tas did not complete", row.Note);

                Benchmark.MetricValue value = values[i].Value;
                if (value != null && value.Available)
                {
                    Assert.Equal(PresentationFormat.Number(value.Value, value.Unit, CultureInfo.CurrentCulture, string.Empty), row.OpenStudio);
                }
                else
                {
                    //Never a zero or another engine's value.
                    Assert.Equal(string.Empty, row.OpenStudio);
                    Assert.Contains("Not reported by OpenStudio", row.Note);
                }
            }

            Assert.All(engineResult.SpaceRows, x => Assert.Equal(EngineComparisonRowStatus.NotCompared, x.Status));
            Assert.Contains("not evaluated", Query.EngineResultProvenance(engineResult));
            Assert.Contains("Not compared: only OpenStudio completed", Query.EngineResultDiagnostics(engineResult));
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

        [Fact]
        public void Presentation_TwoDecimals_HoursStayInteger_AndMissingStaysEmpty()
        {
            MetricComparison heating = Metric(
                "model", "consumptionHeating", MetricUnit.KilowattHour,
                742.7750866578222, true, 2810.0182949034006, true,
                circular: false, absoluteDifference: 2067.2432082455784, relativeDifference: 0.7356689499121726,
                band: ComparisonBand.Fail, reason: NotApplicableReason.None);

            EngineComparisonRow heatingRow = new EngineComparisonRow(heating, null);
            Assert.Equal("742.78", heatingRow.Tas);
            Assert.Equal("742.7750866578222", heatingRow.TasFull);
            Assert.Equal("2810.02", heatingRow.OpenStudio);
            Assert.Equal("2067.24", heatingRow.AbsoluteDifference);
            Assert.Equal("73.57", heatingRow.PercentDifference);
            Assert.Equal("Compared", heatingRow.StatusText);
            Assert.False(heatingRow.IsInformational);

            MetricComparison hour = Metric(
                "model", "peakHeatingHour", MetricUnit.HourOfYear,
                871, true, 919, true,
                circular: true, absoluteDifference: 48, relativeDifference: null,
                band: ComparisonBand.Fail, reason: NotApplicableReason.None);

            EngineComparisonRow hourRow = new EngineComparisonRow(hour, null);
            Assert.Equal("871", hourRow.Tas);
            Assert.Equal("919", hourRow.OpenStudio);
            Assert.Equal("48", hourRow.AbsoluteDifference);
            Assert.Equal(string.Empty, hourRow.PercentDifference);
            Assert.Equal(48.ToString(CultureInfo.InvariantCulture), hourRow.AbsoluteDifferenceFull);
            Assert.True(hourRow.IsInformational);
            Assert.Equal("Informational", hourRow.StatusText);
            Assert.Contains(PresentationFormat.InformationalNote, hourRow.Note);

            MetricComparison missing = Metric(
                "model", "heating.designLoad", MetricUnit.Watt,
                1927.0106201171875, true, null, false,
                circular: false, absoluteDifference: null, relativeDifference: null,
                band: ComparisonBand.NotApplicable, reason: NotApplicableReason.Unavailable);

            EngineComparisonRow missingRow = new EngineComparisonRow(missing, "Office");
            Assert.Equal("1927.01", missingRow.Tas);
            Assert.Equal(string.Empty, missingRow.OpenStudio);
            Assert.Equal(string.Empty, missingRow.AbsoluteDifference);
            Assert.Equal(string.Empty, missingRow.PercentDifference);
            Assert.Equal("Missing on OpenStudio", missingRow.StatusText);
            Assert.Equal(string.Empty, missingRow.OpenStudioFull);
        }

        [Fact]
        public void ScopeDisplay_IsCompactWhileFullGuidStaysAvailable()
        {
            string guid = "cb68bed195af496bb255a13ffa5c58e9";
            EngineComparisonRow row = new EngineComparisonRow(
                Metric("model", "area", MetricUnit.SquareMetre, 50, true, 50, true, false, 0, 0, ComparisonBand.Match, NotApplicableReason.None),
                "Office");

            //The comparison scope is the model for this metric; the space GUID is carried by the space row factory.
            Assert.Equal("Model", row.ScopeDisplay);
            Assert.Equal("model", row.ScopeFull);

            MetricComparison spaceMetric = Metric(
                guid, "area", MetricUnit.SquareMetre, 50, true, 50, true,
                circular: false, absoluteDifference: 0, relativeDifference: 0,
                band: ComparisonBand.Match, reason: NotApplicableReason.None);

            EngineComparisonRow spaceRow = new EngineComparisonRow(spaceMetric, "Office");
            Assert.Equal(guid, spaceRow.ScopeFull);
            Assert.Equal(guid.Substring(0, 8) + "…", spaceRow.ScopeDisplay);
        }

        [Fact]
        public void UnitMismatch_HasItsOwnStatusAndNote()
        {
            MetricComparison mismatch = new MetricComparison(
                "model", "consumptionHeating", MetricUnit.Kilowatt,
                1, true, 2, true, false, null, null, null,
                ComparisonBand.NotApplicable, NotApplicableReason.UnitMismatch,
                MetricUnit.Kilowatt, MetricUnit.Watt);

            EngineComparisonRow row = new EngineComparisonRow(mismatch, null);
            Assert.True(row.IsUnitMismatch);
            Assert.False(row.IsInformational);
            Assert.Equal("Unit mismatch", row.StatusText);
            Assert.Contains("Unit mismatch (TAS kW vs OpenStudio W)", row.Note);
            Assert.DoesNotContain(PresentationFormat.InformationalNote, row.Note);
        }

        [Fact]
        public void InformationalFailCountsAreSeparateFromContributingFailCounts()
        {
            EngineComparison comparison = Query.LoadEngineComparison(Tas, OpenStudio);
            Assert.True(comparison.InformationalFailCount > 0);
            Assert.True(comparison.ContributingFailCount > 0);
            Assert.Equal(comparison.ComparisonResult.FailCount, comparison.ContributingFailCount + comparison.InformationalFailCount);
            Assert.All(comparison.AllRows.Where(x => x.IsInformational), x => Assert.Equal("Informational", x.StatusText));
        }

        private static MetricComparison Metric(
            string scope,
            string key,
            MetricUnit unit,
            double? tas,
            bool tasAvailable,
            double? openStudio,
            bool openStudioAvailable,
            bool circular,
            double? absoluteDifference,
            double? relativeDifference,
            ComparisonBand band,
            NotApplicableReason reason)
        {
            return new MetricComparison(
                scope, key, unit,
                tas, tasAvailable,
                openStudio, openStudioAvailable,
                circular, absoluteDifference, relativeDifference, null,
                band, reason,
                unit, unit);
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
