// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// S03: every Tas, OpenStudio and Both run manifest carries a provenance block - the build, the engine and producer binaries, the
    /// weather, the design days and the settings actually given - filled from what ran, never from a manual SAM_COMMIT or a Git HEAD, and
    /// with every unknown item named with its reason. No engine runs here: producers are faked as in <see cref="EngineComparisonTests"/>,
    /// the Tas route's record is driven through its own writer, and a real OpenStudio run is the existing RealRun test.
    /// </summary>
    [Collection(ProcessEnvironmentCollection.Name)]
    public class SimulationRunProvenanceTests : IDisposable
    {
        private readonly string directory;

        public SimulationRunProvenanceTests()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM-UI-S03-tests", Guid.NewGuid().ToString("N"));
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

        // ---------------------------------------------------------------- the build

        [Theory]
        [InlineData("2026.4.12.0+1bee804f", "1bee804f")]
        [InlineData("2026.4.12.0+1BEE804F", "1bee804f")]
        [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456789abcdef0123456789abcdef01234567")]
        [InlineData("1.0.0", null)]
        [InlineData("1.0.0+unknown", null)]
        [InlineData("1.0.0+abc", null)]
        [InlineData(null, null)]
        public void SourceRevision_IsOnlyTheBuildStamp(string? informationalVersion, string? expected)
        {
            Assert.Equal(expected, SimulationRunProvenance.SourceRevision(informationalVersion));
        }

        /// <summary>
        /// The build is identified by its own stamp and its binaries' hashes. A manual SAM_COMMIT is not read, and an unstamped (developer)
        /// build says "unknown" with its reason - even though it is built from a Git checkout whose HEAD could have been read.
        /// </summary>
        [Fact]
        public void Application_IsIdentifiedByItsStampAndHashes_NeverBySamCommitOrGit()
        {
            string? samCommit = Environment.GetEnvironmentVariable("SAM_COMMIT");
            Environment.SetEnvironmentVariable("SAM_COMMIT", "deadbeefdeadbeef");
            JsonObject provenance;
            try
            {
                provenance = new SimulationRunProvenance("Tas").ToJsonObject();
            }
            finally
            {
                Environment.SetEnvironmentVariable("SAM_COMMIT", samCommit);
            }

            JsonObject application = provenance["application"]!.AsObject();
            string? stamp = SimulationRunProvenance.SourceRevision(SimulationRunProvenance.InformationalVersion(typeof(SimulationRunProvenance).Assembly));
            if (stamp == null)
            {
                Assert.Equal(SimulationRunProvenance.Unknown, (string)application["sourceRevision"]!);
                Assert.Contains("local developer build", (string)provenance["unknown"]!["application.sourceRevision"]!);
            }
            else
            {
                Assert.Equal(stamp, (string)application["sourceRevision"]!);
            }

            Assert.DoesNotContain("deadbeef", provenance.ToJsonString());

            Dictionary<string, JsonObject> components = application["components"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => (string)x["name"]!);
            foreach (Type type in new[] { typeof(Core.SAMObject), typeof(AnalyticalModel), typeof(SimulationRunProvenance) })
            {
                JsonObject component = components[type.Assembly.GetName().Name!];
                Assert.Equal(type.Assembly.Location, (string)component["path"]!);
                Assert.Equal(Query.Sha256(type.Assembly.Location), (string)component["sha256"]!);
                Assert.Equal(type.Assembly.GetName().Version!.ToString(), (string)component["version"]!);
            }

            AssertEveryUnknownHasAReason(provenance);
        }

        // ---------------------------------------------------------------- executables and engines

        [Fact]
        public void Executable_RecordsPathHashAndVersions_AndTheApphostAssembly()
        {
            string path = Process.GetCurrentProcess().MainModule!.FileName!;
            SimulationRunProvenance provenance = new SimulationRunProvenance("Both");
            SimulationRunProvenance.SetExecutable(provenance.Root.Section("executable"), path);

            JsonObject executable = provenance.ToJsonObject()["executable"]!.AsObject();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
            Assert.Equal(path, (string)executable["path"]!);
            Assert.Equal(Query.Sha256(path), (string)executable["sha256"]!);
            Assert.Equal(fileVersionInfo.FileVersion, (string)executable["fileVersion"]!);
            Assert.Equal(fileVersionInfo.ProductVersion, (string)executable["productVersion"]!);

            string path_Assembly = Path.ChangeExtension(path, ".dll");
            if (File.Exists(path_Assembly))
            {
                Assert.Equal(Query.Sha256(path_Assembly), (string)executable["assembly"]!["sha256"]!);
            }
        }

        [Fact]
        public void Executable_MissingOrVersionless_IsUnknownWithItsReason()
        {
            string path_Missing = Path.Combine(directory, "missing.exe");
            string path_Versionless = Path.Combine(directory, "versionless.exe");
            File.WriteAllText(path_Versionless, "not a PE file");

            SimulationRunProvenance provenance = new SimulationRunProvenance("Both");
            SimulationRunProvenance.SetExecutable(provenance.Root.Section("none"), null);
            SimulationRunProvenance.SetExecutable(provenance.Root.Section("missing"), path_Missing);
            SimulationRunProvenance.SetExecutable(provenance.Root.Section("versionless"), path_Versionless);
            JsonObject json = provenance.ToJsonObject();

            Assert.Equal("unknown", (string)json["none"]!["path"]!);
            Assert.Contains("no executable", (string)json["unknown"]!["none.path"]!);
            Assert.Equal("unknown", (string)json["missing"]!["sha256"]!);
            Assert.Contains("does not exist", (string)json["unknown"]!["missing.sha256"]!);
            Assert.Equal(Query.Sha256(path_Versionless), (string)json["versionless"]!["sha256"]!);
            Assert.Equal("unknown", (string)json["versionless"]!["fileVersion"]!);
            Assert.Contains("no version resource", (string)json["unknown"]!["versionless.fileVersion"]!);
            AssertEveryUnknownHasAReason(json);
        }

        [Theory]
        [InlineData("\"C:\\Program Files\\Tas\\TBD.exe\" /Automation", "C:\\Program Files\\Tas\\TBD.exe")]
        [InlineData("C:\\NOTHERE~1\\NOTHERE~2\\Tas\\TBD.exe /Automation", "C:\\NOTHERE~1\\NOTHERE~2\\Tas\\TBD.exe")]
        [InlineData("C:\\Tas\\TBD.exe", "C:\\Tas\\TBD.exe")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void CommandPath_IsTheRegisteredExecutable(string? command, string? expected)
        {
            Assert.Equal(expected, SimulationRunProvenance.CommandPath(command));
        }

        [Fact]
        public void TasEngine_IsTheRegisteredComServer_WithItsVersionAndHash()
        {
            string path = Process.GetCurrentProcess().MainModule!.FileName!;
            SimulationRunProvenance provenance = new SimulationRunProvenance("Tas");
            SimulationRunProvenance.SetTasEngine(provenance.Root.Section("engine"), "\"" + path + "\" -Embedding");
            JsonObject engine = provenance.ToJsonObject()["engine"]!.AsObject();

            Assert.Equal("TBD.Document", (string)engine["comServer"]!);
            Assert.Equal(FileVersionInfo.GetVersionInfo(path).ProductVersion, (string)engine["version"]!);
            Assert.Equal(Query.Sha256(path), (string)engine["executable"]!["sha256"]!);
        }

        [Fact]
        public void TasEngine_NotRegisteredOrVersionless_IsUnknownWithItsReason()
        {
            string path_Versionless = Path.Combine(directory, "TBD.exe");
            File.WriteAllText(path_Versionless, "not a PE file");

            SimulationRunProvenance provenance = new SimulationRunProvenance("Tas");
            SimulationRunProvenance.SetTasEngine(provenance.Root.Section("none"), null);
            SimulationRunProvenance.SetTasEngine(provenance.Root.Section("versionless"), path_Versionless + " /Automation");
            JsonObject json = provenance.ToJsonObject();

            Assert.Equal("unknown", (string)json["none"]!["version"]!);
            Assert.Contains("no TBD.Document COM server is registered", (string)json["unknown"]!["none.version"]!);
            Assert.Equal("unknown", (string)json["versionless"]!["version"]!);
            Assert.Contains("no product version", (string)json["unknown"]!["versionless.version"]!);
            Assert.Equal(Query.Sha256(path_Versionless), (string)json["versionless"]!["executable"]!["sha256"]!);
            AssertEveryUnknownHasAReason(json);
        }

        [Fact]
        public void EnergyPlusVersion_IsTheOneInEplusoutErr()
        {
            string path = Path.Combine(directory, "eplusout.err");
            File.WriteAllText(path, "Program Version,EnergyPlus, Version 25.2.0-cf7368216c, YMD=2026.10.19 10:57,\n   ** Warning ** something\n");
            Assert.Equal("25.2.0-cf7368216c", SimulationRunProvenance.EnergyPlusVersionFromErr(path));

            File.WriteAllText(path, "   ** Severe ** no version line\n");
            Assert.Null(SimulationRunProvenance.EnergyPlusVersionFromErr(path));
            Assert.Null(SimulationRunProvenance.EnergyPlusVersionFromErr(Path.Combine(directory, "none.err")));
            Assert.Null(SimulationRunProvenance.EnergyPlusVersionFromErr(null));
        }

        /// <summary>A collector that fails part-way leaves its whole item unknown, with no reasons left for sub-items it had filed.</summary>
        [Fact]
        public void AFailedCollector_LeavesItsItemUnknown_WithNoStaleReasons()
        {
            SimulationRunProvenance provenance = new SimulationRunProvenance("Tas");
            ProvenanceSection weather = provenance.Root.Section("weather");
            SimulationRunProvenance.Guard(provenance.Root, "weather", () =>
            {
                weather.SetUnknown("file", "not read from a file");
                throw new InvalidOperationException("half way");
            });

            JsonObject json = provenance.ToJsonObject();
            Assert.Equal("unknown", (string)json["weather"]!);
            Assert.Contains("half way", (string)json["unknown"]!["weather"]!);
            Assert.Null(json["unknown"]!["weather.file"]);
            AssertEveryUnknownHasAReason(json);
        }

        /// <summary>The weather's content hash ignores the object's own Guid (new on every read) and nothing else.</summary>
        [Fact]
        public void WeatherContentHash_IgnoresOnlyTheWeathersOwnGuid()
        {
            WeatherData weatherData = Weather("Read", 0.0);
            JsonObject jsonObject = weatherData.ToJsonObject();
            jsonObject["Guid"] = Guid.NewGuid().ToString();
            WeatherData weatherData_ReadAgain = new WeatherData(jsonObject);

            Assert.NotEqual(weatherData.Guid, weatherData_ReadAgain.Guid);
            Assert.Equal(SimulationRunProvenance.WeatherContentSha256(weatherData), SimulationRunProvenance.WeatherContentSha256(weatherData_ReadAgain));
            Assert.NotEqual(SimulationRunProvenance.WeatherContentSha256(weatherData), SimulationRunProvenance.WeatherContentSha256(Weather("Read", 0.5)));
        }

        // ---------------------------------------------------------------- Tas only

        [Fact]
        public void Tas_EmbeddedWeather_DerivedDesignDays_AndTheWorkflowSettingsThatRan()
        {
            WeatherData weatherData = Weather("Embedded", 0.0);
            AnalyticalModel analyticalModel = Model(weatherData);
            WorkflowSettings workflowSettings = WorkflowSettingsAsRunPartOSimulationBuildsThem(new WeatherData(weatherData), 1, 365);

            JsonObject provenance = Query.TasRunProvenance(analyticalModel, new WeatherData(weatherData), null, workflowSettings, Inputs(), null).ToJsonObject();

            Assert.Equal("Tas", (string)provenance["route"]!);
            JsonObject weather = provenance["weather"]!.AsObject();
            string contentSha256 = SimulationRunProvenance.WeatherContentSha256(weatherData);
            Assert.Equal("Embedded", (string)weather["source"]!);
            Assert.Equal("Embedded", (string)weather["name"]!);
            Assert.Equal(contentSha256, (string)weather["contentSha256"]!);
            Assert.Equal("unknown", (string)weather["file"]!);
            Assert.Contains("embedded WeatherData", (string)provenance["unknown"]!["weather.file"]!);

            JsonObject designDays = provenance["designDays"]!.AsObject();
            Assert.Equal("DerivedFromWeather", (string)designDays["source"]!);
            Assert.Single(designDays["heating"]!.AsArray());
            Assert.Single(designDays["cooling"]!.AsArray());
            Assert.True((bool)designDays["usedForSizing"]!);

            //The settings that ran, with the weather identified rather than repeated.
            JsonObject workflow = provenance["settings"]!["workflow"]!.AsObject();
            Assert.Equal(365, (int)workflow["SimulateTo"]!);
            Assert.Equal(1, (int)workflow["SimulateFrom"]!);
            Assert.True((bool)workflow["Sizing"]!);
            Assert.Equal(contentSha256, (string)workflow["WeatherData"]!["contentSha256"]!);
            Assert.Null(workflow["WeatherData"]!["WeatherYears"]);
            Assert.Equal("TAS", (string)provenance["settings"]!["inputs"]!["solarCalculationMethod"]!);

            //No TAS here: the engine is unknown, with the reason.
            Assert.Equal("unknown", (string)provenance["engine"]!["version"]!);
            Assert.Contains("TBD.Document", (string)provenance["unknown"]!["engine.version"]!);
            Assert.Contains(provenance["application"]!["components"]!.AsArray(), x => (string)x!["name"]! == typeof(WorkflowSettings).Assembly.GetName().Name);
            AssertEveryUnknownHasAReason(provenance);
        }

        [Fact]
        public void Tas_ExplicitWeatherFromAFile_IsExplicit_WithTheFilesHash()
        {
            AnalyticalModel analyticalModel = Model(Weather("Embedded", 0.0));
            WeatherData weatherData = Weather("Chosen", 5.0);
            string path_Weather = Path.Combine(directory, "chosen.epw");
            File.WriteAllText(path_Weather, "LOCATION,Chosen,,,TMY,000000,0,0,0,0\n");

            JsonObject provenance = Query.TasRunProvenance(analyticalModel, weatherData, path_Weather, WorkflowSettingsAsRunPartOSimulationBuildsThem(weatherData, 1, 365), Inputs(), null).ToJsonObject();

            JsonObject weather = provenance["weather"]!.AsObject();
            Assert.Equal("Explicit", (string)weather["source"]!);
            Assert.Equal(path_Weather, (string)weather["file"]!["path"]!);
            Assert.Equal(Query.Sha256(path_Weather), (string)weather["file"]!["sha256"]!);
            Assert.Equal(SimulationRunProvenance.WeatherContentSha256(weatherData), (string)weather["contentSha256"]!);
            AssertEveryUnknownHasAReason(provenance);
        }

        /// <summary>A weather labelled as the model's but not its content (a remembered one) is not embedded.</summary>
        [Fact]
        public void Tas_RememberedWeatherNotFromAFile_IsExplicit_AndItsFileUnknown()
        {
            AnalyticalModel analyticalModel = Model(Weather("Embedded", 0.0));
            WeatherData weatherData = Weather("Embedded", 3.0);

            JsonObject provenance = Query.TasRunProvenance(analyticalModel, weatherData, null, null, Inputs(), null).ToJsonObject();

            Assert.Equal("Explicit", (string)provenance["weather"]!["source"]!);
            Assert.Contains("remembered", (string)provenance["unknown"]!["weather.file"]!);
            AssertEveryUnknownHasAReason(provenance);
        }

        [Fact]
        public void Tas_StoppedBeforeTheWorkflow_SaysSoForDesignDaysAndSettings_AndNoWeatherIsNone()
        {
            JsonObject provenance = Query.TasRunProvenance(Model(null), null, null, null, Inputs(), null).ToJsonObject();

            Assert.Equal("None", (string)provenance["weather"]!["source"]!);
            Assert.Equal("unknown", (string)provenance["designDays"]!["source"]!);
            Assert.Contains("before the TAS workflow settings were built", (string)provenance["unknown"]!["designDays.source"]!);
            Assert.Equal("unknown", (string)provenance["settings"]!["workflow"]!);
            Assert.Contains("before the TAS workflow settings were built", (string)provenance["unknown"]!["settings.workflow"]!);
            AssertEveryUnknownHasAReason(provenance);
        }

        [Fact]
        public void Tas_DesignDaysNotDerivedFromTheWeather_AreNotClaimedAsDerived()
        {
            WeatherData weatherData = Weather("Embedded", 0.0);
            WorkflowSettings workflowSettings = WorkflowSettingsAsRunPartOSimulationBuildsThem(Weather("Other", 9.0), 1, 365);

            JsonObject provenance = Query.TasRunProvenance(Model(weatherData), weatherData, null, workflowSettings, Inputs(), null).ToJsonObject();

            Assert.Equal("unknown", (string)provenance["designDays"]!["source"]!);
            Assert.Contains("not the ones derived", (string)provenance["unknown"]!["designDays.source"]!);
        }

        [Theory]
        [InlineData(true, false, null, false, TasSimulationStatus.Succeeded)]
        [InlineData(false, false, null, false, TasSimulationStatus.Failed)]
        [InlineData(false, true, null, false, TasSimulationStatus.Cancelled)]
        [InlineData(false, false, "no gbXML", false, TasSimulationStatus.Refused)]
        [InlineData(true, false, null, true, TasSimulationStatus.Failed)]
        [InlineData(false, true, null, true, TasSimulationStatus.Failed)]
        public void TasStatus_IsTheWorkflowsOwnOutcome(bool returnedModel, bool cancelled, string? refusal, bool threw, TasSimulationStatus expected)
        {
            Assert.Equal(expected, TasSimulationRun.StatusOf(returnedModel, cancelled, refusal, threw ? new InvalidOperationException("COM") : null));
        }

        /// <summary>The Tas record of a failed run (S01b's failure text) is written beside the TBD with its provenance.</summary>
        [Fact]
        public void TasManifest_OfAFailedRun_IsWrittenBesideTheTbd_WithFailureAndProvenance()
        {
            WeatherData weatherData = Weather("Embedded", 0.0);
            TasSimulationRun tasSimulationRun = TasRun(TasSimulationStatus.Failed);
            tasSimulationRun.Failure = "TAS refused to simulate (return code 0); see the error log";

            Modify.WriteTasSimulationManifest(tasSimulationRun, () => Query.TasRunProvenance(Model(weatherData), weatherData, null, WorkflowSettingsAsRunPartOSimulationBuildsThem(weatherData, 1, 365), Inputs(), null));

            Assert.Equal(Path.Combine(directory, "Box" + TasSimulationRun.ManifestFileSuffix), tasSimulationRun.ManifestPath);
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(tasSimulationRun.ManifestPath!))!.AsObject();
            Assert.Equal(TasSimulationRun.Schema, (string)manifest["schema"]!);
            Assert.Equal("Failed", (string)manifest["status"]!);
            Assert.Equal(tasSimulationRun.Failure, (string)manifest["failure"]!);
            Assert.Equal(SimulationRunProvenance.Schema, (string)manifest["provenance"]!["schema"]!);
            Assert.Equal("Embedded", (string)manifest["provenance"]!["weather"]!["source"]!);
            AssertEveryUnknownHasAReason(manifest["provenance"]!.AsObject());
        }

        /// <summary>Collecting provenance that throws neither changes the run's status nor stops the record being written.</summary>
        [Fact]
        public void TasManifest_WhenProvenanceThrows_KeepsTheStatus_AndIsStillWritten()
        {
            TasSimulationRun tasSimulationRun = TasRun(TasSimulationStatus.Succeeded);

            Modify.WriteTasSimulationManifest(tasSimulationRun, () => throw new InvalidOperationException("registry unavailable"));

            Assert.Equal(TasSimulationStatus.Succeeded, tasSimulationRun.Status);
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(tasSimulationRun.ManifestPath!))!.AsObject();
            Assert.Equal("Succeeded", (string)manifest["status"]!);
            Assert.Contains(manifest["notes"]!.AsArray(), x => ((string)x!).Contains("registry unavailable"));
        }

        [Fact]
        public void TasManifest_OfACancelledOrThrowingRun_RecordsIt_AndAMissingFolderWritesNothing()
        {
            TasSimulationRun cancelled = TasRun(TasSimulationStatus.Cancelled);
            Modify.WriteTasSimulationManifest(cancelled, () => new SimulationRunProvenance("Tas"));
            Assert.Equal("Cancelled", (string)JsonNode.Parse(File.ReadAllText(cancelled.ManifestPath!))!["status"]!);

            TasSimulationRun threw = TasRun(TasSimulationStatus.Failed);
            threw.ProjectName = "Threw";
            threw.Exception = new System.Runtime.InteropServices.COMException("TBD server died");
            Modify.WriteTasSimulationManifest(threw, () => new SimulationRunProvenance("Tas"));
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(threw.ManifestPath!))!.AsObject();
            Assert.Equal("Failed", (string)manifest["status"]!);
            Assert.Contains("TBD server died", (string)manifest["error"]!);

            TasSimulationRun missing = TasRun(TasSimulationStatus.Refused);
            missing.Directory = Path.Combine(directory, "never-created");
            Modify.WriteTasSimulationManifest(missing, () => new SimulationRunProvenance("Tas"));
            Assert.Null(missing.ManifestPath);
            Assert.False(Directory.Exists(missing.Directory));
        }

        // ---------------------------------------------------------------- Both

        [Fact]
        public void Both_RecordsEachProducersBinary_AndWhatEachReported_WithoutSamCommit()
        {
            string? samCommit = Environment.GetEnvironmentVariable("SAM_COMMIT");
            Environment.SetEnvironmentVariable("SAM_COMMIT", null);
            EngineComparisonRun run;
            string tasExe = ProducerPath("benchmark-tas.exe", "tas binary");
            string openStudioExe = ProducerPath("benchmark-openstudio.exe", "openstudio binary");
            try
            {
                run = Modify.RunEngineComparison(EngineModel(), directory, Epw(), tasExe, openStudioExe, null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
                {
                    File.Copy(producerRun.Engine == ComparisonEngine.Tas ? TasDocument : OpenStudioDocument, Argument(producerRun, "--out"));
                    return 0;
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable("SAM_COMMIT", samCommit);
            }

            Assert.Equal(EngineComparisonRunStatus.Compared, run.Status);
            JsonObject provenance = Manifest(run.ManifestPath)["provenance"]!.AsObject();
            Assert.Equal("Both", (string)provenance["route"]!);

            JsonObject tas = provenance["producers"]!["Tas"]!.AsObject();
            Assert.Equal(Query.Sha256(tasExe), (string)tas["executable"]!["sha256"]!);
            Assert.Equal("--model", (string)tas["arguments"]![0]!);
            Assert.True((bool)tas["succeeded"]!);
            JsonObject document = JsonNode.Parse(File.ReadAllText(TasDocument))!["provenance"]!.AsObject();
            Assert.Equal((string)document["samCommit"]!, (string)tas["reported"]!["samCommit"]!);
            Assert.Equal((string)document["runnerCommit"]!, (string)tas["reported"]!["runnerCommit"]!);
            Assert.Equal((string)document["designDaySource"]!, (string)tas["reported"]!["designDaySource"]!);
            Assert.Equal(document["engine"]!["kind"]!.ToJsonString(), tas["reported"]!["engine"]!["kind"]!.ToJsonString());

            Assert.Equal(Query.Sha256(openStudioExe), (string)provenance["producers"]!["OpenStudio"]!["executable"]!["sha256"]!);
            Assert.Equal("not set", (string)provenance["environment"]!["SAM_COMMIT"]!);
            Assert.Equal("File", (string)provenance["weather"]!["source"]!);
            Assert.Equal(run.WeatherSha256, (string)provenance["weather"]!["sha256"]!);
            Assert.Contains(provenance["application"]!["components"]!.AsArray(), x => (string)x!["name"]! == "SAM.Analytical.Benchmark.Compare");
            AssertEveryUnknownHasAReason(provenance);
        }

        /// <summary>A manual commit override the producers inherit is recorded as such: the reported commits came from it.</summary>
        [Fact]
        public void Both_RecordsAManualSamCommitOverride()
        {
            string? samCommit = Environment.GetEnvironmentVariable("SAM_COMMIT");
            Environment.SetEnvironmentVariable("SAM_COMMIT", "0123456789abcdef");
            try
            {
                EngineComparisonRun run = Modify.RunEngineComparison(EngineModel(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) => 5);
                Assert.Equal("0123456789abcdef", (string)Manifest(run.ManifestPath)["provenance"]!["environment"]!["SAM_COMMIT"]!);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SAM_COMMIT", samCommit);
            }
        }

        /// <summary>S01b's regression shapes: a failed Tas document whose provenance is not an object, and the run still completes OpenStudio.</summary>
        [Theory]
        [InlineData("{\"provenance\":[]}")]
        [InlineData("{\"provenance\":\"text\"}")]
        [InlineData("{\"provenance\":42}")]
        [InlineData("[]")]
        [InlineData("{not json")]
        public void Both_WithAMalformedFailedTasDocument_RecordsItsProvenanceAsUnknown_AndKeepsOpenStudio(string json)
        {
            EngineComparisonRun run = Modify.RunEngineComparison(EngineModel(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                if (producerRun.Engine == ComparisonEngine.Tas)
                {
                    File.WriteAllText(Argument(producerRun, "--out"), json);
                    return 5;
                }

                File.Copy(OpenStudioDocument, Argument(producerRun, "--out"));
                return 0;
            });

            Assert.Equal(EngineComparisonRunStatus.PartiallyCompleted, run.Status);
            JsonObject provenance = Manifest(run.ManifestPath)["provenance"]!.AsObject();
            Assert.Equal("unknown", (string)provenance["producers"]!["Tas"]!["reported"]!);
            Assert.False((bool)provenance["producers"]!["Tas"]!["succeeded"]!);
            Assert.NotNull(provenance["unknown"]!["producers.Tas.reported"]);
            Assert.True(provenance["producers"]!["OpenStudio"]!["reported"] is JsonObject);
            AssertEveryUnknownHasAReason(provenance);
        }

        [Fact]
        public void Both_WithAProducerNotFound_SaysItDidNotRun()
        {
            EngineComparisonRun run = Modify.RunEngineComparison(EngineModel(), directory, Epw(), ProducerPath("benchmark-tas.exe"), Path.Combine(directory, "missing", "benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                File.Copy(TasDocument, Argument(producerRun, "--out"));
                return 0;
            });

            JsonObject provenance = Manifest(run.ManifestPath)["provenance"]!.AsObject();
            Assert.Equal("unknown", (string)provenance["producers"]!["OpenStudio"]!["executable"]!);
            Assert.Contains("did not run", (string)provenance["unknown"]!["producers.OpenStudio.executable"]!);
            Assert.Contains("wrote no B1a document", (string)provenance["unknown"]!["producers.OpenStudio.reported"]!);
            AssertEveryUnknownHasAReason(provenance);
        }

        [Fact]
        public void Both_CancelledOrThrowing_StillWritesItsProvenance()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            EngineComparisonRun cancelled = Modify.RunEngineComparison(EngineModel(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, cancellationTokenSource.Token, (producerRun, workDirectory, cancellationToken) =>
            {
                cancellationTokenSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return 0;
            });

            Assert.Equal(EngineComparisonRunStatus.Cancelled, cancelled.Status);
            JsonObject provenance = Manifest(cancelled.ManifestPath)["provenance"]!.AsObject();
            Assert.True(provenance["producers"]!["Tas"]!["executable"] is JsonObject);
            AssertEveryUnknownHasAReason(provenance);

            EngineComparisonRun threw = Modify.RunEngineComparison(EngineModel(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) => throw new IOException("disk full"));

            Assert.Equal(EngineComparisonRunStatus.Failed, threw.Status);
            JsonObject provenance_Threw = Manifest(threw.ManifestPath)["provenance"]!.AsObject();
            Assert.False((bool)provenance_Threw["producers"]!["Tas"]!["succeeded"]!);
            AssertEveryUnknownHasAReason(provenance_Threw);
        }

        // ---------------------------------------------------------------- OpenStudio only

        /// <summary>
        /// A model with no weather and no EPW: the run fails (blocked by SAM_OpenStudio, or the stack cannot load). Its provenance says what
        /// did not happen, with reasons, and the outcome is exactly what it was before S03.
        /// </summary>
        [Fact]
        public void OpenStudio_FailedRun_RecordsWhatDidNotHappen_WithReasons()
        {
            OpenStudioSimulationRun run = Model(null).RunOpenStudioSimulation(directory, null, @"C:\models\source.json");

            Assert.Equal(OpenStudioSimulationStatus.Failed, run.Status);
            JsonObject provenance = Manifest(run.ManifestPath)["provenance"]!.AsObject();
            Assert.Equal("OpenStudio", (string)provenance["route"]!);

            //"None" where SAM_OpenStudio loaded and found no weather; unknown (with its reason) where the stack could not load at all.
            Assert.Contains((string)provenance["weather"]!["source"]!, new[] { "None", SimulationRunProvenance.Unknown });
            Assert.Equal("unknown", (string)provenance["weather"]!["sha256"]!);
            Assert.Equal("unknown", (string)provenance["engine"]!["cli"]!);
            Assert.Contains("not started", (string)provenance["unknown"]!["engine.cli"]!);
            Assert.Equal("unknown", (string)provenance["engine"]!["energyPlusVersion"]!);
            AssertEveryUnknownHasAReason(provenance);
        }

        // ---------------------------------------------------------------- historical manifests

        /// <summary>
        /// A manifest written before S03 (committed S01b evidence) has every key, with the same JSON kind, that a manifest written now has
        /// outside the new provenance block: the change is additive, and a reader of the old shape reads the new one.
        /// </summary>
        [Fact]
        public void Both_NewManifest_KeepsEveryHistoricalKey_AndOnlyAddsProvenance()
        {
            JsonObject historical = Manifest(Evidence("s01b-20261019", "cases", "B2-cand-both-success", "engine-comparison-run.json"));
            Assert.Null(historical["provenance"]);

            EngineComparisonRun run = Modify.RunEngineComparison(EngineModel(), directory, Epw(), ProducerPath("benchmark-tas.exe"), ProducerPath("benchmark-openstudio.exe"), @"C:\models\source.json", null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
            {
                File.Copy(producerRun.Engine == ComparisonEngine.Tas ? TasDocument : OpenStudioDocument, Argument(producerRun, "--out"));
                return 0;
            });
            JsonObject current = Manifest(run.ManifestPath);

            Assert.Equal((string)historical["schema"]!, (string)current["schema"]!);
            Assert.Equal(historical.Select(x => x.Key).Append("provenance").OrderBy(x => x), current.Select(x => x.Key).OrderBy(x => x));
            foreach (KeyValuePair<string, JsonNode?> keyValuePair in historical)
            {
                Assert.Equal(keyValuePair.Value?.GetValueKind(), current[keyValuePair.Key]?.GetValueKind());
            }

            JsonObject producer_Historical = historical["producers"]![0]!.AsObject();
            JsonObject producer_Current = current["producers"]![0]!.AsObject();
            Assert.Equal(producer_Historical.Select(x => x.Key).OrderBy(x => x), producer_Current.Select(x => x.Key).OrderBy(x => x));
        }

        [Fact]
        public void OpenStudio_NewManifest_KeepsEveryPreS03Key()
        {
            OpenStudioSimulationRun run = Model(null).RunOpenStudioSimulation(directory, null, null);
            JsonObject manifest = Manifest(run.ManifestPath);

            string[] preS03 = { "schema", "id", "status", "startedUtc", "finishedUtc", "durationSeconds", "sourcePath", "model", "snapshot", "weather", "engine", "outputs", "resultCount", "removedResultCount", "diagnostics", "resultDiagnostics", "error" };
            Assert.Equal(preS03.Append("provenance").OrderBy(x => x), manifest.Select(x => x.Key).OrderBy(x => x));
            Assert.Equal(OpenStudioSimulationRun.Schema, (string)manifest["schema"]!);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Every "unknown" in the provenance has a non-empty reason, and no item outside the copied settings is null or blank.</summary>
        private static void AssertEveryUnknownHasAReason(JsonObject provenance)
        {
            JsonObject unknown = provenance["unknown"]!.AsObject();
            Assert.All(unknown, x => Assert.False(string.IsNullOrWhiteSpace((string?)x.Value)));

            int count = 0;
            Walk(provenance, string.Empty);
            Assert.True(unknown.Count >= count, string.Format("{0} unknown items but {1} reasons", count, unknown.Count));

            //...and the other way round: every reason names an item that is "unknown" (none left behind by a section that became unknown).
            foreach (string itemPath in unknown.Select(x => x.Key).Where(x => !x.Contains('[')))
            {
                JsonNode? jsonNode = provenance;
                foreach (string name in itemPath.Split('.'))
                {
                    jsonNode = jsonNode is JsonObject jsonObject ? jsonObject[name] : null;
                }

                Assert.True(jsonNode is JsonValue jsonValue && jsonValue.TryGetValue(out string? text) && text == SimulationRunProvenance.Unknown, "the reason for '" + itemPath + "' names no unknown item");
            }

            void Walk(JsonNode? jsonNode, string path)
            {
                switch (jsonNode)
                {
                    case null:
                        Assert.Fail("a null item at '" + path + "'");
                        break;
                    case JsonObject jsonObject:
                        foreach (KeyValuePair<string, JsonNode?> keyValuePair in jsonObject)
                        {
                            //The copied engine settings keep their own nulls ("not set"); the reasons map is checked above.
                            if ((path.Length == 0 && (keyValuePair.Key == "settings" || keyValuePair.Key == "unknown")))
                            {
                                continue;
                            }

                            Walk(keyValuePair.Value, path.Length == 0 ? keyValuePair.Key : path + "." + keyValuePair.Key);
                        }

                        break;
                    case JsonArray jsonArray:
                        foreach (JsonNode? item in jsonArray)
                        {
                            Walk(item, path + "[]");
                        }

                        break;
                    case JsonValue jsonValue when jsonValue.TryGetValue(out string? text):
                        Assert.False(string.IsNullOrWhiteSpace(text), "a blank item at '" + path + "'");
                        if (text == SimulationRunProvenance.Unknown)
                        {
                            count++;
                            if (!path.Contains("[]"))
                            {
                                Assert.True(unknown.ContainsKey(path), "no reason for the unknown item '" + path + "'");
                            }
                        }

                        break;
                }
            }
        }

        private static JsonObject Manifest(string? path)
        {
            Assert.True(path != null && File.Exists(path), "no manifest at " + path);
            return JsonNode.Parse(File.ReadAllText(path!))!.AsObject();
        }

        private TasSimulationRun TasRun(TasSimulationStatus status)
        {
            return new TasSimulationRun
            {
                Id = "0123abcd",
                Status = status,
                StartedUtc = DateTime.UtcNow,
                Directory = directory,
                ProjectName = "Box",
                ModelName = "Box",
                ModelGuid = Guid.NewGuid(),
                TbdPath = Path.Combine(directory, "Box.tbd"),
            };
        }

        /// <summary>As <c>Modify.RunPartOSimulation</c> builds them: design days derived from the run's weather.</summary>
        private static WorkflowSettings WorkflowSettingsAsRunPartOSimulationBuildsThem(WeatherData weatherData, int from, int to)
        {
            return new WorkflowSettings
            {
                Path_TBD = @"C:\runs\Box.tbd",
                WeatherData = weatherData,
                DesignDays_Heating = new List<DesignDay> { Analytical.Query.HeatingDesignDay(weatherData) },
                DesignDays_Cooling = new List<DesignDay> { Analytical.Query.CoolingDesignDay(weatherData) },
                Sizing = true,
                UnmetHours = true,
                Simulate = true,
                SimulateFrom = from,
                SimulateTo = to,
            };
        }

        private static JsonObject Inputs()
        {
            return Query.TasRunInputs(true, true, 1, 365, true, true, false, SolarCalculationMethod.TAS, false, false, false, false, false, false, false, null);
        }

        private static WeatherData Weather(string name, double offset)
        {
            WeatherYear weatherYear = new WeatherYear(2018);
            for (int day = 0; day < 365; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    double temperature = offset + 10.0 * Math.Sin(2 * Math.PI * day / 365.0) + 3.0 * Math.Sin(2 * Math.PI * hour / 24.0);
                    weatherYear.Add(day, hour, new Dictionary<string, double>
                    {
                        { WeatherDataType.DryBulbTemperature.ToString(), temperature },
                        { WeatherDataType.RelativeHumidity.ToString(), 60.0 },
                        { WeatherDataType.GlobalSolarRadiation.ToString(), hour > 6 && hour < 18 ? 300.0 : 0.0 },
                    });
                }
            }

            return new WeatherData(name, name, 51.5, -0.1, 0, weatherYear);
        }

        private static AnalyticalModel Model(WeatherData? weatherData)
        {
            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), "Box");
            Assert.True(analyticalModel.AddSpace(new Space("Room 1", new Geometry.Spatial.Point3D(1, 1, 1))));
            if (weatherData != null)
            {
                analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);
            }

            return analyticalModel;
        }

        private static AnalyticalModel EngineModel()
        {
            return Model(null);
        }

        private string Epw()
        {
            string path = Path.Combine(directory, "test.epw");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "LOCATION,Test,,,TMY,000000,0,0,0,0\n");
            }

            return path;
        }

        private string ProducerPath(string fileName, string content = "")
        {
            string path = Path.Combine(directory, "producers", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        private static string Argument(EngineComparisonProducerRun producerRun, string name)
        {
            int index = producerRun.Arguments.IndexOf(name);
            return index < 0 ? null! : producerRun.Arguments[index + 1];
        }

        private static string Resource(string name, [CallerFilePath] string? sourceFilePath = null)
        {
            return Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "Resources", "EngineComparison", name);
        }

        private static string Evidence(string a, string b, string c, string d, [CallerFilePath] string? sourceFilePath = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "documentation", "evidence", a, b, c, d));
        }

        private static string TasDocument => Resource("scenario-benchmark-TAS.json");

        private static string OpenStudioDocument => Resource("scenario-benchmark-OpenStudio.json");
    }
}
