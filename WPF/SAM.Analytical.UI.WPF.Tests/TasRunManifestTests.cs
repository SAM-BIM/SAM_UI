// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas;
using SAM.Core;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// S03, Tas only: the ordinary Energy Simulation / Run Simulation &gt; Tas core writes <c>&lt;project&gt;.tas-run.json</c> beside the TBD for
    /// every outcome - success, failure, cancel, a throw and a refusal - with the workflow settings that actually ran. The TAS workflow step
    /// is faked through the core's existing seam, so no TAS is needed; the core's progress window makes these STA tests.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasRunManifestTests : IDisposable
    {
        private readonly string directory;

        public TasRunManifestTests()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM-UI-S03-tas-tests", Guid.NewGuid().ToString("N"));
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

        [WpfFact]
        public void Succeeded_RecordsTheWorkflowSettingsThatRan_AndTheEmbeddedWeather()
        {
            WeatherData weatherData = Weather("Embedded", 0.0);
            UIAnalyticalModel uIAnalyticalModel = Open(weatherData);
            WorkflowSettings? workflowSettings_Seen = null;

            PartOSimulationOutcome partOSimulationOutcome = Modify.Simulate(uIAnalyticalModel, "Box", directory, new WeatherData(weatherData), null, (AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken cancellationToken, out bool cancelled) =>
            {
                cancelled = false;
                workflowSettings_Seen = workflowSettings;
                File.WriteAllText(workflowSettings.Path_TBD, "tbd");
                File.WriteAllText(Path.ChangeExtension(workflowSettings.Path_TBD, "tsd"), "tsd");
                return analyticalModel;
            });

            Assert.True(partOSimulationOutcome.Ran);
            JsonObject manifest = Manifest();
            Assert.Equal(TasSimulationRun.Schema, (string)manifest["schema"]!);
            Assert.Equal("Succeeded", (string)manifest["status"]!);
            Assert.Equal(Path.Combine(directory, "Box.tbd"), (string)manifest["outputs"]!["tbd"]!);
            Assert.Equal(Path.Combine(directory, "Box.tsd"), (string)manifest["outputs"]!["tsd"]!);
            Assert.Equal("Box", (string)manifest["projectName"]!);

            //The settings the workflow was handed, as it was handed them.
            JsonObject provenance = manifest["provenance"]!.AsObject();
            JsonObject workflow = provenance["settings"]!["workflow"]!.AsObject();
            Assert.NotNull(workflowSettings_Seen);
            Assert.Equal(workflowSettings_Seen!.SimulateFrom, (int)workflow["SimulateFrom"]!);
            Assert.Equal(workflowSettings_Seen.SimulateTo, (int)workflow["SimulateTo"]!);
            Assert.Equal(365, (int)workflow["SimulateTo"]!);
            Assert.Equal(workflowSettings_Seen.Sizing, (bool)workflow["Sizing"]!);
            Assert.Equal(workflowSettings_Seen.UnmetHours, (bool)workflow["UnmetHours"]!);
            Assert.Equal("Direct", (string)workflow["T3DRoute"]!);
            Assert.Equal("Embedded", (string)provenance["weather"]!["source"]!);
            Assert.Equal("DerivedFromWeather", (string)provenance["designDays"]!["source"]!);
            Assert.Equal("Tas", (string)provenance["route"]!);
        }

        [WpfFact]
        public void ExplicitWeatherFromAFile_IsRecordedWithTheFilesHash()
        {
            UIAnalyticalModel uIAnalyticalModel = Open(Weather("Embedded", 0.0));
            string path_Weather = Path.Combine(directory, "chosen.epw");
            File.WriteAllText(path_Weather, "LOCATION,Chosen,,,TMY,000000,0,0,0,0\n");

            Modify.Simulate(uIAnalyticalModel, "Box", directory, Weather("Chosen", 4.0), path_Weather, (AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken cancellationToken, out bool cancelled) =>
            {
                cancelled = false;
                return analyticalModel;
            });

            JsonObject weather = Manifest()["provenance"]!["weather"]!.AsObject();
            Assert.Equal("Explicit", (string)weather["source"]!);
            Assert.Equal(Query.Sha256(path_Weather), (string)weather["file"]!["sha256"]!);
        }

        /// <summary>S01b: a workflow that returns no model is failed - decided by the workflow, whatever files are in the folder.</summary>
        [WpfFact]
        public void Failed_IsRecordedAsFailed_WhateverFilesAreInTheFolder()
        {
            UIAnalyticalModel uIAnalyticalModel = Open(Weather("Embedded", 0.0));

            PartOSimulationOutcome partOSimulationOutcome = Modify.Simulate(uIAnalyticalModel, "Box", directory, Weather("Embedded", 0.0), null, (AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken cancellationToken, out bool cancelled) =>
            {
                cancelled = false;
                File.WriteAllBytes(Path.ChangeExtension(workflowSettings.Path_TBD, "tsd"), new byte[1024]);
                return null;
            });

            Assert.NotNull(partOSimulationOutcome.Failure);
            JsonObject manifest = Manifest();
            Assert.Equal("Failed", (string)manifest["status"]!);
            Assert.True(manifest["provenance"]!["settings"]!["workflow"] is JsonObject);
        }

        [WpfFact]
        public void Cancelled_IsRecordedAsCancelled()
        {
            UIAnalyticalModel uIAnalyticalModel = Open(Weather("Embedded", 0.0));

            PartOSimulationOutcome partOSimulationOutcome = Modify.Simulate(uIAnalyticalModel, "Box", directory, Weather("Embedded", 0.0), null, (AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken cancellationToken, out bool cancelled) =>
            {
                cancelled = true;
                return null;
            });

            Assert.True(partOSimulationOutcome.Cancelled);
            Assert.Equal("Cancelled", (string)Manifest()["status"]!);
        }

        /// <summary>A throw from the workflow still propagates exactly as before; the record says Failed and carries the error.</summary>
        [WpfFact]
        public void Exception_StillPropagates_AndIsRecordedAsFailed()
        {
            UIAnalyticalModel uIAnalyticalModel = Open(Weather("Embedded", 0.0));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Modify.Simulate(uIAnalyticalModel, "Box", directory, Weather("Embedded", 0.0), null, (AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken cancellationToken, out bool cancelled) =>
            {
                throw new InvalidOperationException("TBD server died");
            }));

            Assert.Equal("TBD server died", exception.Message);
            JsonObject manifest = Manifest();
            Assert.Equal("Failed", (string)manifest["status"]!);
            Assert.Contains("TBD server died", (string)manifest["error"]!);
        }

        /// <summary>A run that could not start (the old TBD cannot be replaced) is Refused, says why, and has no workflow settings.</summary>
        [WpfFact]
        public void Refused_IsRecordedAsRefused_WithTheReason()
        {
            UIAnalyticalModel uIAnalyticalModel = Open(Weather("Embedded", 0.0));
            string path_TBD = Path.Combine(directory, "Box.tbd");
            File.WriteAllText(path_TBD, "locked");
            bool ran = false;

            using (new FileStream(path_TBD, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Modify.Simulate(uIAnalyticalModel, "Box", directory, Weather("Embedded", 0.0), null, (AnalyticalModel analyticalModel, WorkflowSettings workflowSettings, CancellationToken cancellationToken, out bool cancelled) =>
                {
                    ran = true;
                    cancelled = false;
                    return analyticalModel;
                });
            }

            Assert.False(ran);
            JsonObject manifest = Manifest();
            Assert.Equal("Refused", (string)manifest["status"]!);
            Assert.Contains("could not be overwritten", (string)manifest["refusal"]!);
            Assert.Equal("unknown", (string)manifest["provenance"]!["settings"]!["workflow"]!);
        }

        private JsonObject Manifest()
        {
            string path = TasSimulationRun.ManifestPathOf(directory, "Box");
            Assert.True(File.Exists(path), "no Tas run record at " + path);
            return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        }

        private UIAnalyticalModel Open(WeatherData weatherData)
        {
            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), "Box");
            Assert.True(analyticalModel.AddSpace(new Space("Room 1", new Geometry.Spatial.Point3D(1, 1, 1))));
            analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);

            string path = Path.Combine(directory, "Box.json");
            Assert.True(Core.Convert.ToFile(new IJSAMObject[] { analyticalModel }, path));
            UIAnalyticalModel uIAnalyticalModel = new UIAnalyticalModel(path);
            Assert.True(uIAnalyticalModel.Open());
            return uIAnalyticalModel;
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
                    });
                }
            }

            return new WeatherData(name, name, 51.5, -0.1, 0, weatherYear);
        }
    }
}
