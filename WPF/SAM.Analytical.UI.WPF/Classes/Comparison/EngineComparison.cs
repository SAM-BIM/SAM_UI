// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Benchmark;
using SAM.Analytical.Benchmark.Compare;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>What a comparison row is, as the Tas/OpenStudio comparison view shows it (SAM_UI#231).</summary>
    public enum EngineComparisonRowStatus
    {
        /// <summary>Both engines report the metric in the same unit; SAM_Validation banded it Match, Warn or Fail.</summary>
        Compared,

        /// <summary>
        /// Both engines report a value, but the two are not the same quantity: SAM_Validation treats the metric as informational
        /// (a peak hour, or a whole-model peak under different peak definitions), or the units differ. Shown, never banded as a result.
        /// </summary>
        NotEquivalent,

        /// <summary>Only OpenStudio reports the metric.</summary>
        MissingOnTas,

        /// <summary>Only Tas reports the metric.</summary>
        MissingOnOpenStudio,

        /// <summary>Neither engine reports the metric.</summary>
        Unavailable,

        /// <summary>
        /// One engine's own result, shown because the other engine did not complete (Run Simulation &gt; Both, partially completed).
        /// Nothing is compared: there is no difference or band, and the other engine's column stays empty.
        /// </summary>
        NotCompared,
    }

    /// <summary>
    /// One metric of a Tas/OpenStudio comparison, ready to display. Every value is SAM_Validation's: the numbers, differences, band and
    /// note are its <c>MetricComparison</c>, rendered with its own <c>Format</c>, so the view shows exactly what comparison.csv says. The
    /// only thing decided here is <see cref="Status"/>, a label over SAM_Validation's availability, unit and informational flags.
    /// <para>
    /// A row may instead hold ONE engine's value (<see cref="EngineComparisonRowStatus.NotCompared"/>), when the other engine did not
    /// complete: then only that engine's column, the unit and the note are filled, and there is no difference or band.
    /// </para>
    /// </summary>
    public sealed class EngineComparisonRow
    {
        public EngineComparisonRow(MetricComparison metricComparison, string? spaceName)
        {
            MetricComparison = metricComparison;
            SpaceName = spaceName;

            Scope = metricComparison.Scope;
            Metric = metricComparison.Key;
            Unit = Format.Unit(metricComparison.Unit);
            Tas = Format.Number(metricComparison.TasValue);
            OpenStudio = Format.Number(metricComparison.OpenStudioValue);
            AbsoluteDifference = Format.Number(metricComparison.AbsoluteDifference);
            PercentDifference = Format.Percent(metricComparison.RelativeDifference);
            Band = Format.Band(metricComparison.Band);
            Note = Format.MetricNote(metricComparison);

            Status = Query.ComparisonRowStatus(metricComparison);
        }

        /// <summary>One engine's own value of a metric; nothing is compared (see <see cref="EngineComparisonRowStatus.NotCompared"/>).</summary>
        public EngineComparisonRow(ComparisonEngine comparisonEngine, string scope, string key, MetricValue? metricValue, string? spaceName, string note)
        {
            SpaceName = spaceName;

            Scope = scope;
            Metric = key;
            Unit = metricValue == null ? string.Empty : Format.Unit(metricValue.Unit);

            //An unreported metric stays empty: SAM_Validation's own rule (MetricValue.Available), never a zero.
            string value = metricValue != null && metricValue.Available ? Format.Number(metricValue.Value) : string.Empty;
            Tas = comparisonEngine == ComparisonEngine.Tas ? value : string.Empty;
            OpenStudio = comparisonEngine == ComparisonEngine.OpenStudio ? value : string.Empty;
            AbsoluteDifference = string.Empty;
            PercentDifference = string.Empty;
            Band = string.Empty;
            Note = metricValue != null && metricValue.Available ? note : "Not reported by " + Query.ComparisonEngineText(comparisonEngine) + ". " + note;

            Status = EngineComparisonRowStatus.NotCompared;
        }

        /// <summary>SAM_Validation's comparison of the metric; null for a single-engine row.</summary>
        public MetricComparison? MetricComparison { get; }

        /// <summary>"model" for a model metric, else the space's SAM GUID (or its name when it has none).</summary>
        public string Scope { get; }

        /// <summary>The space's name as the engines report it ("Tas name / OpenStudio name" when they differ); null for a model metric.</summary>
        public string? SpaceName { get; }

        public string Metric { get; }

        public string Unit { get; }

        public string Tas { get; }

        public string OpenStudio { get; }

        public string AbsoluteDifference { get; }

        /// <summary>The relative difference in percent, as comparison.csv's relativePercent column.</summary>
        public string PercentDifference { get; }

        /// <summary>SAM_Validation's band: Match, Warn, Fail or N/A. A tolerance band, not an engineering verdict. Empty when not compared.</summary>
        public string Band { get; }

        public EngineComparisonRowStatus Status { get; }

        public string StatusText => Query.ComparisonRowStatusText(Status);

        /// <summary>SAM_Validation's note for the metric (why it is N/A or informational), as in comparison.csv; for a single-engine row, why it is not compared.</summary>
        public string Note { get; }
    }

    /// <summary>
    /// A loaded Tas/OpenStudio comparison: the two B1a documents and SAM_Validation's <see cref="ComparisonResult"/> for them, with the rows
    /// the view shows. Built by <see cref="Query.LoadEngineComparison(string, string)"/>.
    /// </summary>
    public sealed class EngineComparison
    {
        public EngineComparison(string tasPath, BenchmarkDocument tas, string openStudioPath, BenchmarkDocument openStudio, ComparisonResult comparisonResult)
        {
            TasPath = tasPath;
            Tas = tas;
            OpenStudioPath = openStudioPath;
            OpenStudio = openStudio;
            ComparisonResult = comparisonResult;

            ModelRows = Query.ComparisonModelRows(comparisonResult);
            SpaceRows = Query.ComparisonSpaceRows(comparisonResult);
        }

        public string TasPath { get; }

        public BenchmarkDocument Tas { get; }

        public string OpenStudioPath { get; }

        public BenchmarkDocument OpenStudio { get; }

        public ComparisonResult ComparisonResult { get; }

        public IReadOnlyList<EngineComparisonRow> ModelRows { get; }

        public IReadOnlyList<EngineComparisonRow> SpaceRows { get; }
    }

    /// <summary>
    /// One engine's B1a document shown on its own, because the other engine of a Run Simulation &gt; Both run did not complete. Built by
    /// <see cref="Query.LoadEngineResult"/>. Holds no comparison: every row is <see cref="EngineComparisonRowStatus.NotCompared"/>.
    /// </summary>
    public sealed class EngineResult
    {
        public EngineResult(ComparisonEngine comparisonEngine, string path, BenchmarkDocument document, string note)
        {
            Engine = comparisonEngine;
            Path = path;
            Document = document;

            ModelRows = Query.EngineResultModelRows(comparisonEngine, document, note);
            SpaceRows = Query.EngineResultSpaceRows(comparisonEngine, document, note);
        }

        public ComparisonEngine Engine { get; }

        public string Path { get; }

        public BenchmarkDocument Document { get; }

        public IReadOnlyList<EngineComparisonRow> ModelRows { get; }

        public IReadOnlyList<EngineComparisonRow> SpaceRows { get; }
    }
}
