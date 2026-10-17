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
    }

    /// <summary>
    /// One metric of a Tas/OpenStudio comparison, ready to display. Every value is SAM_Validation's: the numbers, differences, band and
    /// note are its <c>MetricComparison</c>, rendered with its own <c>Format</c>, so the view shows exactly what comparison.csv says. The
    /// only thing decided here is <see cref="Status"/>, a label over SAM_Validation's availability, unit and informational flags.
    /// </summary>
    public sealed class EngineComparisonRow
    {
        public EngineComparisonRow(MetricComparison metricComparison, string? spaceName)
        {
            MetricComparison = metricComparison;
            SpaceName = spaceName;

            Status = Query.ComparisonRowStatus(metricComparison);
        }

        public MetricComparison MetricComparison { get; }

        /// <summary>"model" for a model metric, else the space's SAM GUID (or its name when it has none).</summary>
        public string Scope => MetricComparison.Scope;

        /// <summary>The space's name as the engines report it ("Tas name / OpenStudio name" when they differ); null for a model metric.</summary>
        public string? SpaceName { get; }

        public string Metric => MetricComparison.Key;

        public string Unit => Format.Unit(MetricComparison.Unit);

        public string Tas => Format.Number(MetricComparison.TasValue);

        public string OpenStudio => Format.Number(MetricComparison.OpenStudioValue);

        public string AbsoluteDifference => Format.Number(MetricComparison.AbsoluteDifference);

        /// <summary>The relative difference in percent, as comparison.csv's relativePercent column.</summary>
        public string PercentDifference => Format.Percent(MetricComparison.RelativeDifference);

        /// <summary>SAM_Validation's band: Match, Warn, Fail or N/A. A tolerance band, not an engineering verdict.</summary>
        public string Band => Format.Band(MetricComparison.Band);

        public EngineComparisonRowStatus Status { get; }

        public string StatusText => Query.ComparisonRowStatusText(Status);

        /// <summary>SAM_Validation's note for the metric (why it is N/A or informational), as in comparison.csv.</summary>
        public string Note => Format.MetricNote(MetricComparison);
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
}
