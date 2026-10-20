// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>How one Energy Simulation / Run Simulation &gt; Tas workflow ended.</summary>
    public enum TasSimulationStatus
    {
        /// <summary>The TAS workflow ran and returned a model.</summary>
        Succeeded,

        /// <summary>The workflow started and returned no model (a TAS refusal, TAS-SIM-001, or any other failure), or threw.</summary>
        Failed,

        /// <summary>The user cancelled.</summary>
        Cancelled,

        /// <summary>The run could not start (no gbXML, a TBD that could not be replaced, ...); nothing was simulated.</summary>
        Refused,
    }

    /// <summary>
    /// The record of one TAS workflow started by Energy Simulation or Run Simulation &gt; Tas (roadmap S03): which model, where its files
    /// are, how it ended and its provenance. Written beside the TBD as <c>&lt;project&gt;</c><see cref="ManifestFileSuffix"/>, overwritten with
    /// the TBD and TSD by the next run of the same project into the same folder. It holds no result value.
    /// <para>
    /// The status is the workflow's own outcome (<see cref="StatusOf"/>), never read from files in the folder (S01b).
    /// </para>
    /// </summary>
    public sealed class TasSimulationRun
    {
        public const string Schema = "sam-ui.tas-run/1";

        public const string ManifestFileSuffix = ".tas-run.json";

        public string? Id { get; set; }

        public TasSimulationStatus Status { get; set; } = TasSimulationStatus.Failed;

        public DateTime StartedUtc { get; set; }

        public DateTime FinishedUtc { get; set; }

        /// <summary>The output folder: the TBD, the TSD and this record are in it.</summary>
        public string? Directory { get; set; }

        /// <summary>The open model's file, when it had one. Never written to by the run.</summary>
        public string? SourcePath { get; set; }

        public string? ProjectName { get; set; }

        public string? ModelName { get; set; }

        public Guid ModelGuid { get; set; }

        public string? TbdPath { get; set; }

        public string? TsdPath { get; set; }

        /// <summary>Why the run could not start; null where it started.</summary>
        public string? Refusal { get; set; }

        /// <summary>What the workflow reported about a failure (S01b: TAS return code, error log, TAS-SIM-001).</summary>
        public string? Failure { get; set; }

        public List<string> Notes { get; } = new List<string>();

        /// <summary>An unexpected failure, kept for the caller; only its text is written.</summary>
        public Exception? Exception { get; set; }

        public SimulationRunProvenance? Provenance { get; set; }

        public string? ManifestPath { get; set; }

        /// <summary>
        /// The outcome from what the workflow itself returned: a throw or a model-less return is Failed, a refusal Refused, a cancel Cancelled,
        /// and only a returned model Succeeded.
        /// </summary>
        public static TasSimulationStatus StatusOf(bool returnedModel, bool cancelled, string? refusal, Exception? exception)
        {
            if (exception != null)
            {
                return TasSimulationStatus.Failed;
            }

            if (refusal != null)
            {
                return TasSimulationStatus.Refused;
            }

            if (cancelled)
            {
                return TasSimulationStatus.Cancelled;
            }

            return returnedModel ? TasSimulationStatus.Succeeded : TasSimulationStatus.Failed;
        }

        /// <summary>The manifest's path for <paramref name="projectName"/> in <paramref name="directory"/>.</summary>
        public static string ManifestPathOf(string directory, string projectName)
        {
            return System.IO.Path.Combine(directory, projectName + ManifestFileSuffix);
        }

        public JsonObject ToJsonObject()
        {
            JsonArray notes = new JsonArray();
            Notes.ForEach(x => notes.Add(JsonValue.Create(x)));

            return new JsonObject
            {
                ["schema"] = Schema,
                ["id"] = Id,
                ["status"] = Status.ToString(),
                ["startedUtc"] = StartedUtc.ToString("o"),
                ["finishedUtc"] = FinishedUtc.ToString("o"),
                ["durationSeconds"] = System.Math.Round((FinishedUtc - StartedUtc).TotalSeconds, 3),
                ["sourcePath"] = SourcePath,
                ["projectName"] = ProjectName,
                ["model"] = new JsonObject
                {
                    ["name"] = ModelName,
                    ["guid"] = ModelGuid.ToString("N"),
                },
                ["outputs"] = new JsonObject
                {
                    ["directory"] = Directory,
                    ["tbd"] = TbdPath,
                    ["tsd"] = TsdPath,
                },
                ["refusal"] = Refusal,
                ["failure"] = Failure,
                ["notes"] = notes,
                ["error"] = Exception == null ? null : Exception.GetType().Name + ": " + Exception.Message,
                ["provenance"] = Provenance?.ToJsonObject(),
            };
        }

        public string ToJson()
        {
            return ToJsonObject().ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
