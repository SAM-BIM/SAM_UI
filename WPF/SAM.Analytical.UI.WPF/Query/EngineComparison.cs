// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Benchmark;
using SAM.Analytical.Benchmark.Compare;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>The B1a document file names the producers write and this workflow looks for in a run folder.</summary>
        public const string TasBenchmarkFileName = "benchmark-TAS.json";

        public const string OpenStudioBenchmarkFileName = "benchmark-OpenStudio.json";

        /// <summary>
        /// The folder of one Tas/OpenStudio comparison run: &lt;directory&gt;\&lt;model&gt;_TasOpenStudio_&lt;yyyyMMdd-HHmmss&gt;_&lt;id&gt;, so a run
        /// never writes into another run's folder (as <see cref="OpenStudioRunDirectory"/>).
        /// </summary>
        public static string EngineComparisonRunDirectory(string directory, string? modelName, DateTime startedUtc, string id)
        {
            return Path.Combine(directory, string.Format("{0}_TasOpenStudio_{1:yyyyMMdd-HHmmss}_{2}", FileNameSafe(modelName), startedUtc, id));
        }

        /// <summary>
        /// Reads two B1a documents and compares them with SAM_Validation (<c>Query.Compare</c>, default tolerance profile) - the same
        /// comparison <c>benchmark-compare</c> runs. Nothing is computed here. Throws what SAM_Validation throws for an unreadable,
        /// invalid or incompatible document, so the caller can show it.
        /// </summary>
        public static EngineComparison LoadEngineComparison(string tasPath, string openStudioPath)
        {
            BenchmarkDocument tas = BenchmarkSerializer.Read(tasPath);
            BenchmarkDocument openStudio = BenchmarkSerializer.Read(openStudioPath);

            ComparisonResult comparisonResult = Benchmark.Compare.Query.Compare(tas, openStudio, ToleranceProfile.Default);

            return new EngineComparison(tasPath, tas, openStudioPath, openStudio, comparisonResult);
        }

        /// <summary>
        /// The two B1a documents in <paramref name="directory"/> (a Tas/OpenStudio comparison run folder, or any folder holding both
        /// producer outputs); null for a document that is not there.
        /// </summary>
        public static (string? Tas, string? OpenStudio) EngineComparisonDocuments(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return (null, null);
            }

            string tas = Path.Combine(directory!, TasBenchmarkFileName);
            string openStudio = Path.Combine(directory!, OpenStudioBenchmarkFileName);

            return (File.Exists(tas) ? tas : null, File.Exists(openStudio) ? openStudio : null);
        }

        /// <summary>
        /// How a row is shown. A label over SAM_Validation's own flags - availability, unit mismatch and its informational rule - never a
        /// second comparison: no value, difference or band is looked at.
        /// </summary>
        public static EngineComparisonRowStatus ComparisonRowStatus(MetricComparison metricComparison)
        {
            switch (metricComparison.Availability)
            {
                case ComparisonAvailability.Neither:
                    return EngineComparisonRowStatus.Unavailable;
                case ComparisonAvailability.TasOnly:
                    return EngineComparisonRowStatus.MissingOnOpenStudio;
                case ComparisonAvailability.OpenStudioOnly:
                    return EngineComparisonRowStatus.MissingOnTas;
            }

            if (metricComparison.NotApplicableReason == NotApplicableReason.UnitMismatch || Benchmark.Compare.Query.IsInformationalForNumericalGate(metricComparison))
            {
                return EngineComparisonRowStatus.NotEquivalent;
            }

            return EngineComparisonRowStatus.Compared;
        }

        public static string ComparisonRowStatusText(EngineComparisonRowStatus status)
        {
            switch (status)
            {
                case EngineComparisonRowStatus.Compared:
                    return "Compared";
                case EngineComparisonRowStatus.NotEquivalent:
                    return "Not equivalent";
                case EngineComparisonRowStatus.MissingOnTas:
                    return "Missing on Tas";
                case EngineComparisonRowStatus.MissingOnOpenStudio:
                    return "Missing on OpenStudio";
                case EngineComparisonRowStatus.NotCompared:
                    return "Not compared";
                default:
                    return "Not reported by either engine";
            }
        }

        public static string ComparisonEngineText(ComparisonEngine comparisonEngine)
        {
            return comparisonEngine == ComparisonEngine.Tas ? "Tas" : "OpenStudio";
        }

        /// <summary>
        /// Reads one engine's B1a document (SAM_Validation's reader, which validates it) to show it on its own: Run Simulation &gt; Both
        /// when the other engine did not complete. Nothing is compared. Throws what SAM_Validation throws for an unreadable or invalid
        /// document.
        /// </summary>
        public static EngineResult LoadEngineResult(ComparisonEngine comparisonEngine, string path, ComparisonEngine? notCompleted = null)
        {
            BenchmarkDocument benchmarkDocument = BenchmarkSerializer.Read(path);

            string note = notCompleted == null
                ? "Not compared."
                : "Not compared - " + ComparisonEngineText(notCompleted.Value) + " did not complete.";

            return new EngineResult(comparisonEngine, path, benchmarkDocument, note);
        }

        /// <summary>One engine's model metrics, under SAM_Validation's keys and in its order (ModelMetricValues).</summary>
        public static IReadOnlyList<EngineComparisonRow> EngineResultModelRows(ComparisonEngine comparisonEngine, BenchmarkDocument benchmarkDocument, string note)
        {
            return Benchmark.Compare.Query.ModelMetricValues(benchmarkDocument.Model)
                .Select(x => new EngineComparisonRow(comparisonEngine, Benchmark.Compare.Query.ModelScope, x.Key, x.Value, null, note)).ToList();
        }

        /// <summary>One engine's per-space metrics, under SAM_Validation's keys and in its order (SpaceMetricValues), in document order.</summary>
        public static IReadOnlyList<EngineComparisonRow> EngineResultSpaceRows(ComparisonEngine comparisonEngine, BenchmarkDocument benchmarkDocument, string note)
        {
            List<EngineComparisonRow> result = new List<EngineComparisonRow>();
            foreach (BenchmarkSpaceResult benchmarkSpaceResult in benchmarkDocument.Spaces ?? new List<BenchmarkSpaceResult>())
            {
                string scope = benchmarkSpaceResult.Guid ?? benchmarkSpaceResult.Name ?? "(unnamed)";
                result.AddRange(Benchmark.Compare.Query.SpaceMetricValues(benchmarkSpaceResult)
                    .Select(x => new EngineComparisonRow(comparisonEngine, scope, x.Key, x.Value, benchmarkSpaceResult.Name, note)));
            }

            return result;
        }

        /// <summary>The input provenance of one engine's result shown on its own.</summary>
        public static string EngineResultProvenance(EngineResult engineResult)
        {
            List<string> lines = new List<string>
            {
                "Provenance gate: not evaluated - only " + ComparisonEngineText(engineResult.Engine) + " completed, so there is nothing to check it against.",
                string.Empty,
            };

            lines.AddRange(ProvenanceLines(ComparisonEngineText(engineResult.Engine), engineResult.Path, engineResult.Document.Provenance));
            lines.Add(string.Empty);
            lines.Add("Input manifests (per-space inputs each engine received): not available - not implemented yet (SAM_OpenStudio#30, SAM_UI#230).");

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>The diagnostics of one engine's result shown on its own, and the run's producers and diagnostics.</summary>
        public static string EngineResultDiagnostics(EngineResult engineResult, EngineComparisonRun? engineComparisonRun = null)
        {
            List<string> lines = new List<string>
            {
                "Not compared: only " + ComparisonEngineText(engineResult.Engine) + " completed. No gate, difference or band is given, and no result is substituted for the other engine.",
                string.Empty,
            };

            lines.AddRange(RunDiagnosticLines(ComparisonEngineText(engineResult.Engine), engineResult.Document.Provenance));
            lines.AddRange(EngineComparisonRunLines(engineComparisonRun));

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The warnings and notes a FAILED producer wrote into its B1a document (S01b), one line each: <c>Tas warning: ...</c>,
        /// <c>Tas note: ...</c>. For a refused Tas simulation the warnings are SAM_Tas' <c>TAS-SIM-001</c> errors, carrying the TAS
        /// return code and error log. Read as plain JSON (<c>provenance.warnings</c> and <c>provenance.notes</c>) rather than through the
        /// validating reader, so a failure document that does not validate still says why; one that cannot be read gives one line
        /// saying so. Nothing else of the document is used: it is never a result.
        /// </summary>
        public static IEnumerable<string> BenchmarkFailureLines(ComparisonEngine comparisonEngine, string path)
        {
            string engine = ComparisonEngineText(comparisonEngine);

            System.Text.Json.Nodes.JsonNode? provenance;
            try
            {
                provenance = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))?["provenance"];
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is System.Text.Json.JsonException || exception is InvalidOperationException)
            {
                return new[] { string.Format("{0}: the failed producer's document could not be read ({1}: {2})", engine, exception.GetType().Name, exception.Message) };
            }

            List<string> result = new List<string>();
            foreach ((string name, string label) in new[] { ("warnings", "warning"), ("notes", "note") })
            {
                if (provenance?[name] is System.Text.Json.Nodes.JsonArray jsonArray)
                {
                    foreach (System.Text.Json.Nodes.JsonNode? jsonNode in jsonArray)
                    {
                        string? text = jsonNode is System.Text.Json.Nodes.JsonValue jsonValue && jsonValue.TryGetValue(out string? value) ? value : jsonNode?.ToJsonString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            result.Add(string.Format("{0} {1}: {2}", engine, label, text));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// A Run Simulation &gt; Both run's status, producers (exit code, duration, log, error) and diagnostics, as text; empty for no run.
        /// </summary>
        public static IEnumerable<string> EngineComparisonRunLines(EngineComparisonRun? engineComparisonRun)
        {
            if (engineComparisonRun == null)
            {
                yield break;
            }

            yield return string.Empty;
            yield return "Run: " + EngineComparisonRunStatusText(engineComparisonRun.Status) + (engineComparisonRun.Directory == null ? string.Empty : " - " + engineComparisonRun.Directory);
            foreach (EngineComparisonProducerRun producerRun in engineComparisonRun.ProducerRuns)
            {
                yield return string.Format(CultureInfo.InvariantCulture, "{0} producer: {1}, {2}, exit code {3}, {4:0.#} s, log {5}", producerRun.Engine, producerRun.Succeeded ? "completed" : "did not complete", producerRun.ExecutablePath ?? "(not found)", producerRun.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "-", producerRun.DurationSeconds, producerRun.LogPath ?? "-");
                if (!string.IsNullOrWhiteSpace(producerRun.Error))
                {
                    yield return "  " + producerRun.Error;
                }
            }

            foreach (string diagnostic in engineComparisonRun.Diagnostics)
            {
                yield return diagnostic;
            }

            if (engineComparisonRun.Exception != null)
            {
                yield return "Error: " + engineComparisonRun.Exception.GetType().Name + ": " + engineComparisonRun.Exception.Message;
            }

            if (engineComparisonRun.ManifestPath != null)
            {
                yield return "Run record: " + engineComparisonRun.ManifestPath;
            }
        }

        public static string EngineComparisonRunStatusText(EngineComparisonRunStatus engineComparisonRunStatus)
        {
            switch (engineComparisonRunStatus)
            {
                case EngineComparisonRunStatus.Compared:
                    return "Compared";
                case EngineComparisonRunStatus.PartiallyCompleted:
                    return "Partially completed";
                case EngineComparisonRunStatus.Cancelled:
                    return "Cancelled";
                default:
                    return "Failed";
            }
        }

        /// <summary>The model-level rows, in SAM_Validation's order.</summary>
        public static IReadOnlyList<EngineComparisonRow> ComparisonModelRows(ComparisonResult comparisonResult)
        {
            return comparisonResult.ModelMetrics.Select(x => new EngineComparisonRow(x, null)).ToList();
        }

        /// <summary>Every space's rows, in SAM_Validation's order (spaces aligned by GUID, then name).</summary>
        public static IReadOnlyList<EngineComparisonRow> ComparisonSpaceRows(ComparisonResult comparisonResult)
        {
            List<EngineComparisonRow> result = new List<EngineComparisonRow>();
            foreach (SpaceComparison spaceComparison in comparisonResult.Spaces)
            {
                string? name = spaceComparison.TasName ?? spaceComparison.OpenStudioName;
                if (spaceComparison.TasName != null && spaceComparison.OpenStudioName != null && !string.Equals(spaceComparison.TasName, spaceComparison.OpenStudioName, StringComparison.Ordinal))
                {
                    name = spaceComparison.TasName + " / " + spaceComparison.OpenStudioName;
                }

                result.AddRange(spaceComparison.Metrics.Select(x => new EngineComparisonRow(x, name)));
            }

            return result;
        }

        /// <summary>
        /// The input provenance of a comparison: SAM_Validation's provenance gate and mismatches, then what each B1a document says about
        /// its inputs. Per-space input manifests are named as not available: they do not exist yet (SAM_OpenStudio#30, SAM_UI#230).
        /// </summary>
        public static string EngineComparisonProvenance(EngineComparison engineComparison)
        {
            ComparisonResult comparisonResult = engineComparison.ComparisonResult;

            List<string> lines = new List<string>
            {
                "Provenance gate: " + Format.Gate(comparisonResult.ProvenanceStatus) + (comparisonResult.ProvenanceCompatibility.IsCompatible ? " (same model, weather and design days)" : string.Empty),
            };

            foreach (ProvenanceMismatch provenanceMismatch in comparisonResult.ProvenanceCompatibility.Mismatches)
            {
                lines.Add(string.Format("  MISMATCH {0}: Tas = {1}; OpenStudio = {2}", provenanceMismatch.Field, provenanceMismatch.Tas ?? "(none)", provenanceMismatch.OpenStudio ?? "(none)"));
            }

            lines.Add(string.Empty);
            lines.AddRange(ProvenanceLines("Tas", engineComparison.TasPath, engineComparison.Tas.Provenance));
            lines.Add(string.Empty);
            lines.AddRange(ProvenanceLines("OpenStudio", engineComparison.OpenStudioPath, engineComparison.OpenStudio.Provenance));
            lines.Add(string.Empty);
            lines.Add("Input manifests (per-space inputs each engine received): not available - not implemented yet (SAM_OpenStudio#30, SAM_UI#230).");
            lines.Add("Input equivalence is therefore shown only as the provenance above (same canonical model, weather file and design-day source).");

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The diagnostics of a comparison: the gate statuses, each run's state, warnings and notes from its B1a document, space alignment
        /// problems, model-total reconciliation, and - for a run made by this workflow - each producer's exit code and log.
        /// </summary>
        public static string EngineComparisonDiagnostics(EngineComparison engineComparison, EngineComparisonRun? engineComparisonRun = null)
        {
            ComparisonResult comparisonResult = engineComparison.ComparisonResult;

            List<string> lines = new List<string>
            {
                string.Format(CultureInfo.InvariantCulture, "Overall gate: {0} (numerical {1}, coverage {2}, provenance {3}, reconciliation {4})",
                    Format.Gate(comparisonResult.Gate), Format.Gate(comparisonResult.NumericalStatus), Format.Gate(comparisonResult.CoverageStatus), Format.Gate(comparisonResult.ProvenanceStatus), Format.Gate(comparisonResult.ReconciliationStatus)),
                string.Format(CultureInfo.InvariantCulture, "Metrics: {0} match, {1} warn, {2} fail, {3} N/A; {4} of {5} required metrics comparable. Tolerance profile '{6}'. A band is a tolerance band, not an engineering verdict (SAM_UI#232/#233).",
                    comparisonResult.MatchCount, comparisonResult.WarnCount, comparisonResult.FailCount, comparisonResult.NotApplicableCount, comparisonResult.ComparableMetricCount, comparisonResult.RequiredMetricCount, comparisonResult.ToleranceProfileName),
            };

            if (!string.IsNullOrWhiteSpace(comparisonResult.SchemaDriftNote))
            {
                lines.Add("Schema: " + comparisonResult.SchemaDriftNote);
            }

            SpaceMatchDiagnostics spaceMatchDiagnostics = comparisonResult.SpaceDiagnostics;
            lines.Add(string.Empty);
            lines.Add(string.Format(CultureInfo.InvariantCulture, "Spaces: Tas {0}, OpenStudio {1}; matched by GUID {2}, by name {3}.", spaceMatchDiagnostics.TasCount, spaceMatchDiagnostics.OpenStudioCount, spaceMatchDiagnostics.MatchedByGuid, spaceMatchDiagnostics.MatchedByName));
            AddList(lines, "Unmatched - only in Tas", spaceMatchDiagnostics.OnlyInTas);
            AddList(lines, "Unmatched - only in OpenStudio", spaceMatchDiagnostics.OnlyInOpenStudio);
            AddList(lines, "Duplicate GUIDs in Tas", spaceMatchDiagnostics.DuplicateTasGuids);
            AddList(lines, "Duplicate GUIDs in OpenStudio", spaceMatchDiagnostics.DuplicateOpenStudioGuids);
            AddList(lines, "Duplicate names in Tas", spaceMatchDiagnostics.DuplicateTasNames);
            AddList(lines, "Duplicate names in OpenStudio", spaceMatchDiagnostics.DuplicateOpenStudioNames);
            AddList(lines, "Ambiguous name matches", spaceMatchDiagnostics.AmbiguousNameMatches);

            if (comparisonResult.Reconciliations.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("Model total vs sum of spaces:");
                foreach (ReconciliationResult reconciliationResult in comparisonResult.Reconciliations)
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0} {1}: model {2}, sum of {3} spaces {4} {5} -> {6}{7}",
                        reconciliationResult.Engine, reconciliationResult.Key, reconciliationResult.ModelTotalAvailable ? Format.Number(reconciliationResult.ModelTotal) : "(not reported)", reconciliationResult.ContributingSpaces,
                        Format.Number(reconciliationResult.SumOfSpaces), Format.Unit(reconciliationResult.Unit), Format.Band(reconciliationResult.Band), reconciliationResult.Complete ? string.Empty : " (incomplete)"));
                }
            }

            lines.Add(string.Empty);
            lines.AddRange(RunDiagnosticLines("Tas", engineComparison.Tas.Provenance));
            lines.Add(string.Empty);
            lines.AddRange(RunDiagnosticLines("OpenStudio", engineComparison.OpenStudio.Provenance));

            lines.AddRange(EngineComparisonRunLines(engineComparisonRun));

            return string.Join(Environment.NewLine, lines);
        }

        private static IEnumerable<string> ProvenanceLines(string engine, string path, BenchmarkProvenance? provenance)
        {
            yield return engine + ": " + path;
            if (provenance == null)
            {
                yield return "  (the document has no provenance)";
                yield break;
            }

            yield return string.Format(CultureInfo.InvariantCulture, "  Route {0}; engine {1} {2} (SDK {3}); state {4}", provenance.Route, provenance.Engine?.Name ?? "?", provenance.Engine?.Version ?? "?", provenance.Engine?.SdkVersion ?? "-", provenance.State);
            yield return "  Model: " + (provenance.SourceModelName ?? "?") + " (" + (provenance.SourceModelGuid ?? "?") + ")";
            yield return "  Canonical model hash: " + (provenance.CanonicalModelHash ?? "(none)") + " (canonicalization " + (provenance.CanonicalizationVersion ?? "?") + ")";
            yield return "  Source file hash: " + (provenance.SourceFileHash ?? "(none)");
            yield return "  Weather: " + (provenance.Weather?.Identity ?? "(none)") + ", " + (provenance.Weather?.Hash ?? "(no hash)");
            yield return "  Design-day source: " + provenance.DesignDaySource;
            yield return string.Format(CultureInfo.InvariantCulture, "  Run: {0:u}, {1} s; SAM {2}; runner {3}", provenance.RunTimestampUtc, provenance.DurationSeconds?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?", provenance.SamCommit ?? "?", provenance.RunnerCommit ?? "?");
        }

        private static IEnumerable<string> RunDiagnosticLines(string engine, BenchmarkProvenance? provenance)
        {
            yield return engine + " run: " + (provenance == null ? "(no provenance)" : provenance.State.ToString());
            if (provenance?.Warnings != null)
            {
                foreach (string warning in provenance.Warnings)
                {
                    yield return "  Warning: " + warning;
                }
            }

            if (provenance?.Notes != null)
            {
                foreach (string note in provenance.Notes)
                {
                    yield return "  Note: " + note;
                }
            }

            if (provenance?.ResultSources != null && provenance.ResultSources.Count != 0)
            {
                yield return "  Result sources: " + string.Join(", ", provenance.ResultSources);
            }
        }

        private static void AddList(List<string> lines, string title, IReadOnlyList<string> values)
        {
            if (values != null && values.Count != 0)
            {
                lines.Add("  " + title + ": " + string.Join(", ", values));
            }
        }
    }
}
