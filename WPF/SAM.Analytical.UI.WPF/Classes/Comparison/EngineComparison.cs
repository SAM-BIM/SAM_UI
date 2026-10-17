// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Benchmark;
using SAM.Analytical.Benchmark.Compare;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
            ScopeFull = metricComparison.Scope;
            ScopeDisplay = string.Equals(metricComparison.Scope, Benchmark.Compare.Query.ModelScope, StringComparison.Ordinal)
                ? "Model"
                : Guid.TryParseExact(metricComparison.Scope, "N", out _)
                    ? PresentationFormat.CompactGuid(metricComparison.Scope)
                    : metricComparison.Scope;

            Metric = metricComparison.Key;
            Unit = Format.Unit(metricComparison.Unit);

            //Presentation only: two decimals; the raw values and differences stay on the *Full properties and in the reports.
            Tas = PresentationFormat.Number(metricComparison.TasValue, metricComparison.Unit, CultureInfo.CurrentCulture, string.Empty);
            OpenStudio = PresentationFormat.Number(metricComparison.OpenStudioValue, metricComparison.Unit, CultureInfo.CurrentCulture, string.Empty);
            AbsoluteDifference = PresentationFormat.Number(
                metricComparison.AbsoluteDifference,
                metricComparison.Circular ? MetricUnit.HourOfYear : metricComparison.Unit,
                CultureInfo.CurrentCulture,
                string.Empty);
            PercentDifference = metricComparison.Circular
                ? string.Empty
                : PresentationFormat.Percent(metricComparison.RelativeDifference, CultureInfo.CurrentCulture, string.Empty);

            TasFull = Format.Number(metricComparison.TasValue);
            OpenStudioFull = Format.Number(metricComparison.OpenStudioValue);
            AbsoluteDifferenceFull = Format.Number(metricComparison.AbsoluteDifference);
            PercentDifferenceFull = metricComparison.Circular ? string.Empty : Format.Percent(metricComparison.RelativeDifference);

            Band = Format.Band(metricComparison.Band);
            Note = PresentationFormat.Note(metricComparison);
            IsInformational = metricComparison.Band != ComparisonBand.NotApplicable && Benchmark.Compare.Query.IsInformationalForNumericalGate(metricComparison);
            IsUnitMismatch = metricComparison.NotApplicableReason == NotApplicableReason.UnitMismatch;

            Status = Query.ComparisonRowStatus(metricComparison);
        }

        /// <summary>One engine's own value of a metric; nothing is compared (see <see cref="EngineComparisonRowStatus.NotCompared"/>).</summary>
        public EngineComparisonRow(ComparisonEngine comparisonEngine, string scope, string key, MetricValue? metricValue, string? spaceName, string note)
        {
            SpaceName = spaceName;

            Scope = scope;
            ScopeFull = scope;
            ScopeDisplay = string.Equals(scope, Benchmark.Compare.Query.ModelScope, StringComparison.Ordinal)
                ? "Model"
                : Guid.TryParseExact(scope, "N", out _)
                    ? PresentationFormat.CompactGuid(scope)
                    : scope;

            Metric = key;
            Unit = metricValue == null ? string.Empty : Format.Unit(metricValue.Unit);

            //An unreported metric stays empty: SAM_Validation's own rule (MetricValue.Available), never a zero.
            bool available = metricValue != null && metricValue.Available;
            string value = available ? PresentationFormat.Number(metricValue!.Value, metricValue.Unit, CultureInfo.CurrentCulture, string.Empty) : string.Empty;
            string full = available ? Format.Number(metricValue!.Value) : string.Empty;
            Tas = comparisonEngine == ComparisonEngine.Tas ? value : string.Empty;
            OpenStudio = comparisonEngine == ComparisonEngine.OpenStudio ? value : string.Empty;
            TasFull = comparisonEngine == ComparisonEngine.Tas ? full : string.Empty;
            OpenStudioFull = comparisonEngine == ComparisonEngine.OpenStudio ? full : string.Empty;
            AbsoluteDifference = string.Empty;
            PercentDifference = string.Empty;
            AbsoluteDifferenceFull = string.Empty;
            PercentDifferenceFull = string.Empty;
            Band = string.Empty;
            Note = available ? note : "Not reported by " + Query.ComparisonEngineText(comparisonEngine) + ". " + note;
            IsInformational = false;
            IsUnitMismatch = false;

            Status = EngineComparisonRowStatus.NotCompared;
        }

        /// <summary>SAM_Validation's comparison of the metric; null for a single-engine row.</summary>
        public MetricComparison? MetricComparison { get; }

        /// <summary>"model" for a model metric, else the space's SAM GUID (or its name when it has none).</summary>
        public string Scope { get; }

        /// <summary>A compact grid label for <see cref="Scope"/>: "Model", a short GUID, or the space name.</summary>
        public string ScopeDisplay { get; }

        /// <summary>The complete scope identifier, for inspection and copying.</summary>
        public string ScopeFull { get; }

        /// <summary>The space's name as the engines report it ("Tas name / OpenStudio name" when they differ); null for a model metric.</summary>
        public string? SpaceName { get; }

        public string Metric { get; }

        public string Unit { get; }

        /// <summary>Two-decimal display value; empty when the engine did not report it.</summary>
        public string Tas { get; }

        public string OpenStudio { get; }

        public string AbsoluteDifference { get; }

        /// <summary>The normalized difference in percent, two decimals; empty for a circular hour or a missing value.</summary>
        public string PercentDifference { get; }

        /// <summary>Full-precision Tas value for the cell tooltip; empty when unavailable.</summary>
        public string TasFull { get; }

        /// <summary>Full-precision OpenStudio value for the cell tooltip; empty when unavailable.</summary>
        public string OpenStudioFull { get; }

        /// <summary>Full-precision absolute difference for the cell tooltip; empty when unavailable.</summary>
        public string AbsoluteDifferenceFull { get; }

        /// <summary>Full-precision normalized percentage for the cell tooltip; empty for a circular hour.</summary>
        public string PercentDifferenceFull { get; }

        /// <summary>SAM_Validation's band: Match, Warn, Fail or N/A. A tolerance band, not an engineering verdict. Empty when not compared.</summary>
        public string Band { get; }

        public EngineComparisonRowStatus Status { get; }

        /// <summary>True when the row carries a band that is excluded from the numerical status.</summary>
        public bool IsInformational { get; }

        /// <summary>True when the two engines declared incompatible units (not an informational metric).</summary>
        public bool IsUnitMismatch { get; }

        public string StatusText
        {
            get
            {
                if (Status == EngineComparisonRowStatus.NotEquivalent)
                {
                    if (IsUnitMismatch)
                    {
                        return "Unit mismatch";
                    }

                    return IsInformational ? "Informational" : "Not equivalent";
                }

                return Query.ComparisonRowStatusText(Status);
            }
        }

        /// <summary>SAM_Validation's presentation note for the metric; for a single-engine row, why it is not compared.</summary>
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

        public IReadOnlyList<EngineComparisonRow> AllRows => ModelRows.Concat(SpaceRows).ToList();

        /// <summary>Fail bands that contribute to the numerical status.</summary>
        public int ContributingFailCount => AllRows.Count(x => x.Band == "Fail" && !x.IsInformational);

        /// <summary>Fail bands shown but excluded from the numerical status; they are always included in the gate's fail count.</summary>
        public int InformationalFailCount => AllRows.Count(x => x.Band == "Fail" && x.IsInformational);
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
