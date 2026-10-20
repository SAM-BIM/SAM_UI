// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>How one Simulate &gt; OpenStudio run ended.</summary>
    public enum OpenStudioSimulationStatus
    {
        /// <summary>EnergyPlus ran, and its results were attached to the result model and saved.</summary>
        Succeeded,

        /// <summary>The conversion, the run or reading the results failed; the diagnostics say where.</summary>
        Failed,

        /// <summary>The user cancelled; the CLI/EnergyPlus process tree was terminated and no result model was saved.</summary>
        Cancelled,
    }

    /// <summary>
    /// The record of one Simulate &gt; OpenStudio run (SAM_UI#229): what was simulated, with which weather, where every output
    /// is, and how it ended. It is written next to the outputs as <see cref="ManifestFileName"/>, so a run folder explains
    /// itself without SAM_UI open.
    /// <para>
    /// It deliberately holds no OpenStudio type, so reading it never loads the OpenStudio bindings. It is a provenance record,
    /// not a comparison: the snapshot and weather hashes are SHA-256 over the exact file bytes (the B1a <c>sourceFileHash</c>
    /// sense); the canonical model hash and every Tas/OpenStudio comparison belong to SAM_Validation (SAM_UI#231).
    /// </para>
    /// </summary>
    public sealed class OpenStudioSimulationRun
    {
        /// <summary>Schema identifier written into the manifest.</summary>
        public const string Schema = "sam-ui.openstudio-run/1";

        /// <summary>The manifest's file name inside the run folder.</summary>
        public const string ManifestFileName = "openstudio-run.json";

        /// <summary>The frozen input model's file name inside the run folder.</summary>
        public const string SnapshotFileName = "snapshot.json";

        /// <summary>The copy of the weather file used by the run, inside the run folder.</summary>
        public const string WeatherFileName = "weather.epw";

        public string? Id { get; set; }

        public OpenStudioSimulationStatus Status { get; set; } = OpenStudioSimulationStatus.Failed;

        public DateTime StartedUtc { get; set; }

        public DateTime FinishedUtc { get; set; }

        /// <summary>The run folder: every file of this run is in it.</summary>
        public string? Directory { get; set; }

        /// <summary>The open model's file, when it had one. Never written to by the run.</summary>
        public string? SourcePath { get; set; }

        public string? ModelName { get; set; }

        public Guid ModelGuid { get; set; }

        public string? SnapshotPath { get; set; }

        public string? SnapshotSha256 { get; set; }

        /// <summary>"File" (chosen by the user), "Embedded" (the model's WeatherData) or "None".</summary>
        public string? WeatherSource { get; set; }

        /// <summary>The copy of the EPW used, in the run folder; null when there was none.</summary>
        public string? WeatherPath { get; set; }

        public string? WeatherSha256 { get; set; }

        /// <summary>The EPW the user chose, as given; null for the embedded weather or none.</summary>
        public string? WeatherOriginalPath { get; set; }

        /// <summary>The loaded OpenStudio SDK's version; null when the SDK was not loaded.</summary>
        public string? OpenStudioSdkVersion { get; set; }

        /// <summary>The EnergyPlus version the loaded SDK targets (the version that ran is read from eplusout.err).</summary>
        public string? EnergyPlusSdkVersion { get; set; }

        /// <summary>The design-day basis SAM_OpenStudio's conversion established ("None", "Ddy", "EmbeddedModel"); null without a conversion result.</summary>
        public string? DesignDaySource { get; set; }

        /// <summary>The OpenStudioRunOptions and conversion arguments the run was given, as values.</summary>
        public JsonObject? RunOptions { get; set; }

        /// <summary>The run's provenance (S03), completed when the run ends.</summary>
        public SimulationRunProvenance? Provenance { get; set; }

        /// <summary>The SAM.Analytical.OpenStudio build that converted the model (its InformationalVersion).</summary>
        public string? SamOpenStudioVersion { get; set; }

        /// <summary>The openstudio.exe that ran the simulation; null when none was started.</summary>
        public string? CliPath { get; set; }

        public string? OsmPath { get; set; }

        public string? OswPath { get; set; }

        public string? SqlPath { get; set; }

        public string? ErrorFilePath { get; set; }

        public int? ExitCode { get; set; }

        public int WarningCount { get; set; }

        public int SevereCount { get; set; }

        public int FatalCount { get; set; }

        /// <summary>The saved result model: the frozen copy carrying this run's OpenStudio results and no other results.</summary>
        public string? ResultModelPath { get; set; }

        /// <summary>Number of results (model, space and surface) attached to the result model.</summary>
        public int ResultCount { get; set; }

        /// <summary>Number of results the frozen copy already carried (e.g. from Tas) and the result model does not.</summary>
        public int RemovedResultCount { get; set; }

        /// <summary>Conversion and run diagnostics from SAM_OpenStudio, one line each ("Severity Code: message").</summary>
        public List<string> Diagnostics { get; } = new List<string>();

        /// <summary>Diagnostics of attaching the SQL results to SAM objects (unmatched zones, spaces without a result).</summary>
        public List<string> ResultDiagnostics { get; } = new List<string>();

        /// <summary>An unexpected failure, kept for the caller; only its text is written to the manifest.</summary>
        public Exception? Exception { get; set; }

        public string? ManifestPath { get; set; }

        public JsonObject ToJsonObject()
        {
            JsonObject result = new JsonObject
            {
                ["schema"] = Schema,
                ["id"] = Id,
                ["status"] = Status.ToString(),
                ["startedUtc"] = StartedUtc.ToString("o"),
                ["finishedUtc"] = FinishedUtc.ToString("o"),
                ["durationSeconds"] = System.Math.Round((FinishedUtc - StartedUtc).TotalSeconds, 3),
                ["sourcePath"] = SourcePath,
                ["model"] = new JsonObject
                {
                    ["name"] = ModelName,
                    ["guid"] = ModelGuid.ToString("N"),
                },
                ["snapshot"] = new JsonObject
                {
                    ["path"] = RelativePath(SnapshotPath),
                    ["sha256"] = SnapshotSha256,
                },
                ["weather"] = new JsonObject
                {
                    ["source"] = WeatherSource,
                    ["path"] = RelativePath(WeatherPath),
                    ["sha256"] = WeatherSha256,
                },
                ["engine"] = new JsonObject
                {
                    ["route"] = "Native-OpenStudio",
                    ["samOpenStudioVersion"] = SamOpenStudioVersion,
                    ["cliPath"] = CliPath,
                    ["exitCode"] = ExitCode,
                    ["warningCount"] = WarningCount,
                    ["severeCount"] = SevereCount,
                    ["fatalCount"] = FatalCount,
                },
                ["outputs"] = new JsonObject
                {
                    ["osm"] = RelativePath(OsmPath),
                    ["osw"] = RelativePath(OswPath),
                    ["sql"] = RelativePath(SqlPath),
                    ["err"] = RelativePath(ErrorFilePath),
                    ["resultModel"] = RelativePath(ResultModelPath),
                },
                ["resultCount"] = ResultCount,
                ["removedResultCount"] = RemovedResultCount,
                ["diagnostics"] = ToJsonArray(Diagnostics),
                ["resultDiagnostics"] = ToJsonArray(ResultDiagnostics),
                ["error"] = Exception == null ? null : Exception.GetType().Name + ": " + Exception.Message,
                ["provenance"] = Provenance?.ToJsonObject(),
            };

            return result;
        }

        public string ToJson()
        {
            return ToJsonObject().ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Paths inside the run folder are written relative to it, so a copied or archived run folder stays self-consistent;
        /// anything outside it (the CLI, say) stays absolute.
        /// </summary>
        private string? RelativePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(Directory))
            {
                return path;
            }

            string relativePath = System.IO.Path.GetRelativePath(Directory!, path!);
            return relativePath.StartsWith("..", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relativePath) ? path : relativePath.Replace('\\', '/');
        }

        private static JsonArray ToJsonArray(IEnumerable<string> values)
        {
            JsonArray result = new JsonArray();
            foreach (string value in values)
            {
                //JsonValue.Create, not Add(value): the generic Add<string> makes a value that ToJson's options (no TypeInfoResolver)
                //cannot write, so any run with a diagnostic threw while writing its manifest.
                result.Add(JsonValue.Create(value));
            }

            return result;
        }
    }
}
