// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Weather;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// How each route fills its <see cref="SimulationRunProvenance"/> (roadmap S03). One mechanism for the three routes; each only adds
    /// what its engine actually received.
    /// </summary>
    public static partial class Query
    {
        /// <summary>A new provenance for <paramref name="route"/>; null if even that fails, so starting a run never depends on it.</summary>
        public static SimulationRunProvenance? NewRunProvenance(string route)
        {
            try
            {
                return new SimulationRunProvenance(route);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The provenance of one Energy Simulation / Run Simulation &gt; Tas workflow.
        /// </summary>
        /// <param name="analyticalModel_Source">The model handed to the run, before any change; its embedded WeatherData tells embedded weather from explicit.</param>
        /// <param name="weatherData">The weather the run gave TAS.</param>
        /// <param name="weatherFilePath">The file that weather was read from in the Simulate dialog, when it was; null for the embedded or a remembered weather.</param>
        /// <param name="workflowSettings">The settings handed to WorkflowCalculator; null when the run stopped before they were built.</param>
        /// <param name="inputs">The Simulate dialog's inputs as handed to the run (exports, solar method, ...).</param>
        /// <param name="tasComServerCommand">The registered TBD.Document server (<see cref="SimulationRunProvenance.TasComServerCommand"/>).</param>
        public static SimulationRunProvenance TasRunProvenance(AnalyticalModel? analyticalModel_Source, WeatherData? weatherData, string? weatherFilePath, WorkflowSettings? workflowSettings, JsonObject? inputs, string? tasComServerCommand)
        {
            SimulationRunProvenance result = new SimulationRunProvenance("Tas");
            ProvenanceSection root = result.Root;

            SimulationRunProvenance.Guard(root, "engine", () =>
            {
                result.AddComponent(typeof(WorkflowSettings).Assembly);
                SimulationRunProvenance.SetTasEngine(root.Section("engine"), tasComServerCommand);
            });

            string? weatherSha256 = null;
            SimulationRunProvenance.Guard(root, "weather", () => weatherSha256 = SetTasWeather(root.Section("weather"), analyticalModel_Source, weatherData, weatherFilePath));
            SimulationRunProvenance.Guard(root, "designDays", () => SetTasDesignDays(root.Section("designDays"), weatherData, workflowSettings));

            SimulationRunProvenance.Guard(root, "settings", () =>
            {
                ProvenanceSection settings = root.Section("settings");
                settings.Set("inputs", inputs?.DeepClone(), "the Simulate inputs were not recorded");

                JsonObject? workflow = workflowSettings?.ToJsonObject();
                if (workflow != null && workflow["WeatherData"] != null)
                {
                    //The weather is identified, not repeated: it is the weather above, all 8760 hours of it.
                    workflow["WeatherData"] = new JsonObject { ["name"] = weatherData?.Name, ["contentSha256"] = weatherSha256 };
                }

                settings.Set("workflow", workflow, "the run stopped before the TAS workflow settings were built (see refusal, failure or error)");
            });

            return result;
        }

        /// <summary>The weather of a Tas run: its source (embedded or explicit), name, content hash and, when read from a file, that file's hash. Returns the content hash.</summary>
        private static string? SetTasWeather(ProvenanceSection section, AnalyticalModel? analyticalModel_Source, WeatherData? weatherData, string? weatherFilePath)
        {
            if (weatherData == null)
            {
                section.Json["source"] = "None";
                section.SetUnknown("contentSha256", "the run was given no weather");
                return null;
            }

            string contentSha256 = SimulationRunProvenance.CanonicalSha256(weatherData);

            //Embedded means the very weather the model carries, by content: a remembered or chosen weather that happens to be labelled
            //as the model's is not.
            string? embeddedSha256 = null;
            if (analyticalModel_Source != null && analyticalModel_Source.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out WeatherData weatherData_Embedded) && weatherData_Embedded != null)
            {
                embeddedSha256 = SimulationRunProvenance.CanonicalSha256(weatherData_Embedded);
            }

            section.Json["source"] = embeddedSha256 == contentSha256 ? "Embedded" : "Explicit";
            section.Set("name", weatherData.Name, "the weather has no name");
            section.Json["contentSha256"] = contentSha256;
            section.Json["contentHashOf"] = "the SAM WeatherData JSON given to TAS, canonical JSON v1 (SAM_Validation)";

            if (!string.IsNullOrWhiteSpace(weatherFilePath) && System.IO.File.Exists(weatherFilePath))
            {
                ProvenanceSection file = section.Section("file");
                file.Json["path"] = weatherFilePath;
                file.Set("sha256", SimulationRunProvenance.Sha256(weatherFilePath!), "the weather file could not be read");
            }
            else
            {
                section.SetUnknown("file", embeddedSha256 == contentSha256
                    ? "the weather is the model's embedded WeatherData, not a file; contentSha256 identifies it"
                    : "the weather was not read from a file in this Simulate dialog (a remembered weather); contentSha256 identifies it");
            }

            return contentSha256;
        }

        /// <summary>The design days a Tas run handed TAS, and whether they are the ones derived from its weather.</summary>
        private static void SetTasDesignDays(ProvenanceSection section, WeatherData? weatherData, WorkflowSettings? workflowSettings)
        {
            if (workflowSettings == null)
            {
                section.SetUnknown("source", "the run stopped before the TAS workflow settings were built");
                return;
            }

            List<DesignDay> heating = workflowSettings.DesignDays_Heating ?? new List<DesignDay>();
            List<DesignDay> cooling = workflowSettings.DesignDays_Cooling ?? new List<DesignDay>();

            bool derived = weatherData != null
                && SameDesignDays(heating, Analytical.Query.HeatingDesignDay(weatherData))
                && SameDesignDays(cooling, Analytical.Query.CoolingDesignDay(weatherData));

            if (derived)
            {
                section.Json["source"] = "DerivedFromWeather";
            }
            else
            {
                section.SetUnknown("source", "the design days handed to TAS are not the ones derived from the run's weather, so where they came from could not be established");
            }

            section.Json["heating"] = Names(heating);
            section.Json["cooling"] = Names(cooling);
            section.Json["usedForSizing"] = workflowSettings.Sizing;
        }

        private static bool SameDesignDays(List<DesignDay> designDays, DesignDay? designDay_Derived)
        {
            return designDay_Derived != null && designDays.Count == 1 && designDays[0] != null
                && designDays[0].ToJsonObject().ToJsonString() == designDay_Derived.ToJsonObject().ToJsonString();
        }

        private static JsonArray Names(IEnumerable<DesignDay> designDays)
        {
            JsonArray result = new JsonArray();
            foreach (DesignDay designDay in designDays.Where(x => x != null))
            {
                result.Add(JsonValue.Create(designDay.Name ?? string.Empty));
            }

            return result;
        }

        /// <summary>
        /// The Simulate dialog's inputs as one object, so the manifest shows what was asked for beside the workflow settings that ran.
        /// </summary>
        public static JsonObject TasRunInputs(bool simulate, bool fullYearSimulation, int fullYearSimulation_From, int fullYearSimulation_To, bool sizing, bool unmetHours, bool useWidths, SolarCalculationMethod solarCalculationMethod, bool directT3D, bool updateConstructionLayersByPanelType, bool createTM59, bool createSAP, bool createTPD, bool createPartL, bool printRoomDataSheets, string? zoneCategory)
        {
            return new JsonObject
            {
                ["simulate"] = simulate,
                ["fullYearSimulation"] = fullYearSimulation,
                ["fullYearSimulationFrom"] = fullYearSimulation_From,
                ["fullYearSimulationTo"] = fullYearSimulation_To,
                ["sizing"] = sizing,
                ["unmetHours"] = unmetHours,
                ["useWidths"] = useWidths,
                ["solarCalculationMethod"] = solarCalculationMethod.ToString(),
                ["directT3D"] = directT3D,
                ["updateConstructionLayersByPanelType"] = updateConstructionLayersByPanelType,
                ["createTM59"] = createTM59,
                ["createSAP"] = createSAP,
                ["createTPD"] = createTPD,
                ["createPartL"] = createPartL,
                ["printRoomDataSheets"] = printRoomDataSheets,
                ["zoneCategory"] = zoneCategory ?? "(none)",
            };
        }

        /// <summary>The producer about to run in a Run Simulation &gt; Both: its executable (path, SHA-256, versions) and the arguments it is given.</summary>
        public static void AddProducerProvenance(SimulationRunProvenance? provenance, EngineComparisonProducerRun producerRun)
        {
            if (provenance == null || producerRun == null)
            {
                return;
            }

            ProvenanceSection producer = provenance.Root.Section("producers").Section(producerRun.Engine.ToString());
            SimulationRunProvenance.Guard(producer, "executable", () => SimulationRunProvenance.SetExecutable(producer.Section("executable"), producerRun.ExecutablePath));

            JsonArray arguments = new JsonArray();
            producerRun.Arguments.ForEach(x => arguments.Add(JsonValue.Create(x)));
            producer.Json["arguments"] = arguments;
        }

        /// <summary>
        /// Completes a Run Simulation &gt; Both provenance once the run has ended: the SAM_Validation build that compared, the commit overrides
        /// the producers inherited, the weather, and what each producer reported in its own B1a document (its SAM and runner commits, engine and
        /// version, weather and design-day source). A producer that never ran is recorded as unknown with the reason.
        /// </summary>
        public static void CompleteEngineComparisonRunProvenance(EngineComparisonRun engineComparisonRun)
        {
            SimulationRunProvenance? provenance = engineComparisonRun?.Provenance;
            if (provenance == null)
            {
                return;
            }

            ProvenanceSection root = provenance.Root;

            SimulationRunProvenance.Guard(root, "application", () =>
            {
                provenance.AddComponent(typeof(Benchmark.BenchmarkCanonicalJson).Assembly);
                provenance.AddComponent(typeof(Benchmark.Compare.ComparisonResult).Assembly);
            });

            //The producers resolve samCommit and runnerCommit from these first, before their own build stamp: a value here is a manual
            //override, and the reported commits below came from it.
            SimulationRunProvenance.Guard(root, "environment", () => SimulationRunProvenance.SetEnvironment(root.Section("environment"), "SAM_COMMIT", "RUNNER_COMMIT"));

            SimulationRunProvenance.Guard(root, "weather", () =>
            {
                ProvenanceSection weather = root.Section("weather");
                weather.Set("source", engineComparisonRun!.WeatherSource, "the run stopped before the weather was resolved");
                weather.Set("sha256", engineComparisonRun.WeatherSha256, engineComparisonRun.WeatherSource == "None" ? "the run had no weather file" : "the run stopped before the weather file was copied");
                weather.Set("originalPath", engineComparisonRun.WeatherOriginalPath, engineComparisonRun.WeatherSource == "Embedded"
                    ? "the weather is the model's embedded WeatherData, exported to EPW once for both producers; sha256 identifies the exported file"
                    : "no weather file was chosen");
            });

            root.Section("designDays").Json["source"] = "chosen by each producer from the snapshot (SAM_UI passes none): see producers.<engine>.reported.designDaySource";
            root.Section("settings").Json["producers"] = "each producer's arguments (producers.<engine>.arguments) and its built-in workflow settings, which its executable SHA-256 pins";

            foreach (EngineComparisonProducerRun producerRun in engineComparisonRun!.ProducerRuns)
            {
                ProvenanceSection producer = root.Section("producers").Section(producerRun.Engine.ToString());
                if (producer.Json["executable"] == null)
                {
                    producer.SetUnknown("executable", "the producer did not run: " + (producerRun.Error ?? "no reason was recorded"));
                }

                producer.Json["succeeded"] = producerRun.Succeeded;

                //A failed producer's document is kept for diagnosis (S01b); its provenance is reported as written, never used as a result.
                string? benchmarkPath = producerRun.BenchmarkPath ?? ExpectedBenchmarkPath(engineComparisonRun, producerRun.Engine);
                SimulationRunProvenance.Guard(producer, "reported", () => SimulationRunProvenance.SetReportedProvenance(producer, "reported", benchmarkPath));
            }
        }

        private static string? ExpectedBenchmarkPath(EngineComparisonRun engineComparisonRun, ComparisonEngine comparisonEngine)
        {
            return string.IsNullOrWhiteSpace(engineComparisonRun.Directory) ? null : System.IO.Path.Combine(engineComparisonRun.Directory!, comparisonEngine == ComparisonEngine.Tas ? TasBenchmarkFileName : OpenStudioBenchmarkFileName);
        }

        /// <summary>
        /// Completes an OpenStudio run's provenance from its record once the run has ended: the CLI that ran, the EnergyPlus version in its
        /// eplusout.err, the weather, the design-day source the converter established and the run options it was given.
        /// </summary>
        public static void CompleteOpenStudioRunProvenance(OpenStudioSimulationRun openStudioSimulationRun)
        {
            SimulationRunProvenance? provenance = openStudioSimulationRun?.Provenance;
            if (provenance == null)
            {
                return;
            }

            ProvenanceSection root = provenance.Root;

            SimulationRunProvenance.Guard(root, "engine", () =>
            {
                ProvenanceSection engine = root.Section("engine");
                string notLoaded = "the OpenStudio SDK was not loaded for this run (see error and diagnostics)";
                engine.Set("openStudioSdkVersion", openStudioSimulationRun!.OpenStudioSdkVersion, notLoaded);
                engine.Set("energyPlusVersionOfSdk", openStudioSimulationRun.EnergyPlusSdkVersion, notLoaded);
                engine.Set("energyPlusVersion", SimulationRunProvenance.EnergyPlusVersionFromErr(openStudioSimulationRun.ErrorFilePath), "EnergyPlus wrote no eplusout.err with its version line (it did not run, or the run stopped before it)");

                if (string.IsNullOrWhiteSpace(openStudioSimulationRun.CliPath))
                {
                    engine.SetUnknown("cli", "openstudio.exe was not started for this run");
                }
                else
                {
                    SimulationRunProvenance.SetExecutable(engine.Section("cli"), openStudioSimulationRun.CliPath);
                }
            });

            SimulationRunProvenance.Guard(root, "weather", () =>
            {
                ProvenanceSection weather = root.Section("weather");
                weather.Set("source", openStudioSimulationRun!.WeatherSource, "the run stopped before the weather was resolved");
                weather.Set("sha256", openStudioSimulationRun.WeatherSha256, openStudioSimulationRun.WeatherSource == "None" ? "the run had no weather file" : "the run stopped before the weather file was copied");
                weather.Set("originalPath", openStudioSimulationRun.WeatherOriginalPath, openStudioSimulationRun.WeatherSource == "Embedded"
                    ? "the weather is the model's embedded WeatherData, exported to EPW by SAM_OpenStudio; sha256 identifies the exported file"
                    : "no weather file was chosen");
            });

            SimulationRunProvenance.Guard(root, "designDays", () => root.Section("designDays").Set("source", openStudioSimulationRun!.DesignDaySource, "SAM_OpenStudio returned no conversion result, so the design-day basis it established is not known"));
            SimulationRunProvenance.Guard(root, "settings", () => root.Section("settings").Set("runOptions", openStudioSimulationRun!.RunOptions?.DeepClone(), "the run stopped before the OpenStudio run options were built"));
        }
    }
}
