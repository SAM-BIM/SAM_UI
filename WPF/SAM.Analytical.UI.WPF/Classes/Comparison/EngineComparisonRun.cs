// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>The simulation engines of the Tas/OpenStudio comparison.</summary>
    public enum ComparisonEngine
    {
        Tas,
        OpenStudio,
    }

    /// <summary>How a Tas/OpenStudio comparison run ended.</summary>
    public enum EngineComparisonRunStatus
    {
        /// <summary>
        /// Both producers completed and SAM_Validation compared their B1a documents; the reports are written. Says nothing about whether
        /// the engines agree: that is SAM_Validation's gate.
        /// </summary>
        Compared,

        /// <summary>
        /// One producer completed and the other did not (failed, not found or not run): the completed engine's B1a document is kept and
        /// shown on its own, and nothing is compared. The missing engine's results are never substituted.
        /// </summary>
        PartiallyCompleted,

        /// <summary>No producer completed, the inputs could not be prepared or changed during the run, or the documents could not be
        /// compared; the diagnostics and logs say which.</summary>
        Failed,

        /// <summary>The user cancelled; the running producer's process tree was killed.</summary>
        Cancelled,
    }

    /// <summary>One producer run (benchmark-tas or benchmark-openstudio) of a comparison.</summary>
    public sealed class EngineComparisonProducerRun
    {
        public ComparisonEngine Engine { get; set; }

        public string? ExecutablePath { get; set; }

        public List<string> Arguments { get; } = new List<string>();

        public int? ExitCode { get; set; }

        public double DurationSeconds { get; set; }

        /// <summary>The producer's standard output and error.</summary>
        public string? LogPath { get; set; }

        /// <summary>The B1a document it wrote; null when it wrote none.</summary>
        public string? BenchmarkPath { get; set; }

        public string? Error { get; set; }

        /// <summary>
        /// True when the producer ran, exited with 0 and wrote its B1a document. Only such a document is shown or compared: a document
        /// left by a failed producer is kept in the run folder for diagnosis but never treated as a result.
        /// </summary>
        public bool Succeeded => ExitCode == 0 && BenchmarkPath != null && Error == null;
    }

    /// <summary>
    /// The record of one Run Simulation &gt; Both run (SAM_UI#231): one frozen, read-only snapshot and weather file, both engines'
    /// producers run on them in turn, and SAM_Validation's comparison of the two B1a documents when both completed. Written to the run folder as
    /// <see cref="ManifestFileName"/>. It holds no result value: the results are the B1a documents and SAM_Validation's reports.
    /// </summary>
    public sealed class EngineComparisonRun
    {
        public const string Schema = "sam-ui.engine-comparison-run/1";

        public const string ManifestFileName = "engine-comparison-run.json";

        public string? Id { get; set; }

        public EngineComparisonRunStatus Status { get; set; } = EngineComparisonRunStatus.Failed;

        public DateTime StartedUtc { get; set; }

        public DateTime FinishedUtc { get; set; }

        public string? Directory { get; set; }

        public string? SourcePath { get; set; }

        public string? ModelName { get; set; }

        public string? SnapshotPath { get; set; }

        public string? SnapshotSha256 { get; set; }

        /// <summary>"File" (chosen by the user), "Embedded" (the model's WeatherData exported to EPW) or "None".</summary>
        public string? WeatherSource { get; set; }

        public string? WeatherPath { get; set; }

        public string? WeatherSha256 { get; set; }

        /// <summary>The EPW the user chose, as given; null for the embedded weather or none.</summary>
        public string? WeatherOriginalPath { get; set; }

        /// <summary>The run's provenance (S03): this build, each producer's identity and what each producer reported.</summary>
        public SimulationRunProvenance? Provenance { get; set; }

        public List<EngineComparisonProducerRun> ProducerRuns { get; } = new List<EngineComparisonProducerRun>();

        /// <summary>SAM_Validation's overall gate ("Pass", "Warn", "Fail") when the documents were compared.</summary>
        public string? Gate { get; set; }

        /// <summary>comparison.md, comparison.csv and comparison-summary.json, as SAM_Validation wrote them.</summary>
        public List<string> ReportPaths { get; } = new List<string>();

        public List<string> Diagnostics { get; } = new List<string>();

        public Exception? Exception { get; set; }

        public string? ManifestPath { get; set; }

        /// <remarks>Strings are added with JsonValue.Create: the generic JsonArray.Add&lt;string&gt; cannot be written with these options.</remarks>
        public string ToJson()
        {
            JsonArray producerRuns = new JsonArray();
            foreach (EngineComparisonProducerRun producerRun in ProducerRuns)
            {
                JsonArray arguments = new JsonArray();
                producerRun.Arguments.ForEach(x => arguments.Add(JsonValue.Create(x)));

                producerRuns.Add(new JsonObject
                {
                    ["engine"] = producerRun.Engine.ToString(),
                    ["executable"] = producerRun.ExecutablePath,
                    ["arguments"] = arguments,
                    ["exitCode"] = producerRun.ExitCode,
                    ["durationSeconds"] = System.Math.Round(producerRun.DurationSeconds, 3),
                    ["log"] = producerRun.LogPath,
                    ["benchmark"] = producerRun.BenchmarkPath,
                    ["error"] = producerRun.Error,
                    ["succeeded"] = producerRun.Succeeded,
                });
            }

            JsonArray reportPaths = new JsonArray();
            ReportPaths.ForEach(x => reportPaths.Add(JsonValue.Create(x)));

            JsonArray diagnostics = new JsonArray();
            Diagnostics.ForEach(x => diagnostics.Add(JsonValue.Create(x)));

            JsonObject jsonObject = new JsonObject
            {
                ["schema"] = Schema,
                ["id"] = Id,
                ["status"] = Status.ToString(),
                ["startedUtc"] = StartedUtc.ToString("o"),
                ["finishedUtc"] = FinishedUtc.ToString("o"),
                ["sourcePath"] = SourcePath,
                ["modelName"] = ModelName,
                ["snapshot"] = new JsonObject { ["path"] = SnapshotPath, ["sha256"] = SnapshotSha256 },
                ["weather"] = new JsonObject { ["source"] = WeatherSource, ["path"] = WeatherPath, ["sha256"] = WeatherSha256 },
                ["producers"] = producerRuns,
                ["gate"] = Gate,
                ["reports"] = reportPaths,
                ["diagnostics"] = diagnostics,
                ["error"] = Exception == null ? null : Exception.GetType().Name + ": " + Exception.Message,
                ["provenance"] = Provenance?.ToJsonObject(),
            };

            return jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
