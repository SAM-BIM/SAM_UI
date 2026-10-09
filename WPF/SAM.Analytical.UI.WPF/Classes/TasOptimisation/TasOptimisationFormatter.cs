// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Core.Optimisation;
using SAM.Core.Reporting;
using SAM.Units;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One design variable or output as the results view shows it: its name, its declared unit and how its values are
    /// rounded for display. The unit is the definition's declaration; a value is never converted.
    /// </summary>
    public sealed class TasOptimisationColumn
    {
        internal TasOptimisationColumn(string name, string? unit, int? decimals, bool integer, IEnumerable<string>? options = null)
        {
            Name = name;
            Unit = unit;
            Decimals = decimals;
            Integer = integer;
            Options = (options ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        }

        /// <summary>A choice's options in order: option k is <c>Options[k - 1]</c>. Empty for a value.</summary>
        public IReadOnlyList<string> Options { get; }

        /// <summary>The option a value names (a whole number from 1 to the number of options); null otherwise.</summary>
        public string? Option(double value)
        {
            if (Options.Count == 0 || double.IsNaN(value) || double.IsInfinity(value) || value != System.Math.Floor(value) || value < 1 || value > Options.Count)
            {
                return null;
            }

            return Options[(int)value - 1];
        }

        public string Name { get; }

        /// <summary>The declared unit (its canonical symbol when SAM knows it, otherwise as written); null when none is declared.</summary>
        public string? Unit { get; }

        /// <summary>
        /// The fixed decimals of SAM's display unit for the declared unit; null when the value is shown to
        /// <see cref="TasOptimisationFormatter.SignificantFigures"/> significant figures instead.
        /// </summary>
        public int? Decimals { get; }

        /// <summary>True for a whole number (a choice's option number).</summary>
        public bool Integer { get; }

        /// <summary>"Name [unit]", or the name alone without a unit (CSV and copied headers).</summary>
        public string Header => Unit == null ? Name : Name + " [" + Unit + "]";
    }

    /// <summary>
    /// Engineering display of an optimisation's numbers (PR5b), through SAM's <see cref="QuantityFormatter"/>:
    /// <list type="bullet">
    /// <item>A value whose declared unit is SAM's display unit for its category (kWh, kg and so kgCO2e, °C, K, h, %, W,
    /// m, m², °) gets that display unit's decimals (kWh 1, °C 1, h 0, …).</item>
    /// <item>Any other value (no unit, currency, a unit SAM does not display such as MWh or tCO2e) gets
    /// <see cref="SignificantFigures"/> significant figures. The unit is the definition's declaration, so nothing is ever
    /// converted: a value declared in MWh stays in MWh.</item>
    /// <item>A choice's option number, and every count, is a whole number.</item>
    /// </list>
    /// The display follows the given culture's separators. The raw value (<see cref="Raw"/>), the copied trace
    /// (<see cref="TabText"/>) and the CSV export (<see cref="CsvText"/>) are full precision and invariant: round-trip
    /// "R" text that reads back to the same double on any machine.
    /// </summary>
    public sealed class TasOptimisationFormatter
    {
        /// <summary>Significant figures for a value without a SAM display unit (SAM#186's engineering default).</summary>
        public const int SignificantFigures = 4;

        private readonly QuantityFormatter quantityFormatter;

        /// <param name="optimisationDefinition">The definition the run executes (its variables, outputs and units).</param>
        /// <param name="cultureInfo">The display culture; null for the current culture.</param>
        public TasOptimisationFormatter(OptimisationDefinition optimisationDefinition, CultureInfo? cultureInfo = null)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            Culture = cultureInfo ?? CultureInfo.CurrentCulture;
            quantityFormatter = new QuantityFormatter(new DocumentOptions() { Culture = Culture, UnitSystem = UnitStyle.SI });

            Variables = optimisationDefinition.Variables.Where(x => x != null).Select(x => Column(x.Name, x.Unit, x.Type == DesignVariableType.Discrete || x.Type == DesignVariableType.Integer, x.Target?.Options)).ToList().AsReadOnly();

            List<OptimisationOutput> outputs = new List<OptimisationOutput>();
            OptimisationOutput? objective = optimisationDefinition.Objective?.Output == null ? null : optimisationDefinition.Output(optimisationDefinition.Objective.Output);
            if (objective != null)
            {
                outputs.Add(objective);
            }

            outputs.AddRange(optimisationDefinition.RecordedOutputs());
            Outputs = outputs.Select(x => Column(x.Name, x.Unit, false)).ToList().AsReadOnly();
        }

        public CultureInfo Culture { get; }

        /// <summary>The design variables in coordinate order.</summary>
        public IReadOnlyList<TasOptimisationColumn> Variables { get; }

        /// <summary>The outputs in the order the optimiser reports them: the objective first, then the recorded outputs.</summary>
        public IReadOnlyList<TasOptimisationColumn> Outputs { get; }

        /// <summary>The value for display, without its unit: "2,978.5", "4.969", "—" for NaN or infinity.</summary>
        public string Number(TasOptimisationColumn column, double value)
        {
            if (column == null)
            {
                return Raw(value);
            }

            if (column.Integer)
            {
                return quantityFormatter.FormatNumber(value, 0);
            }

            if (column.Decimals.HasValue)
            {
                return quantityFormatter.FormatNumber(value, column.Decimals.Value);
            }

            return quantityFormatter.FormatSignificant(value, SignificantFigures);
        }

        /// <summary>The value for display with its unit: "2,978.5 kWh", "7,360 GBP", "4.969" (no unit declared).</summary>
        public string Text(TasOptimisationColumn column, double value)
        {
            string number = Number(column, value);

            //A choice names its option: "2: Triple low-e".
            string? option = column?.Option(value);
            if (option != null)
            {
                return number + ": " + option;
            }

            return column?.Unit == null || double.IsNaN(value) || double.IsInfinity(value) ? number : number + " " + column.Unit;
        }

        /// <summary>"name = value unit" pairs for display, in column order.</summary>
        public string Pairs(IReadOnlyList<TasOptimisationColumn> columns, IReadOnlyList<double> values, int offset = 0)
        {
            return string.Join(", ", values.Select((x, i) => (offset + i < columns.Count ? columns[offset + i].Name : "#" + (offset + i).ToString(CultureInfo.InvariantCulture)) + " = " + (offset + i < columns.Count ? Text(columns[offset + i], x) : Raw(x))));
        }

        /// <summary>The full-precision value: invariant round-trip text ("R"), "NaN", "Infinity" or "-Infinity".</summary>
        public static string Raw(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>"name = raw value" pairs (full precision), in column order.</summary>
        public static string RawPairs(IReadOnlyList<string>? names, IReadOnlyList<double> values, int offset = 0)
        {
            return string.Join(", ", values.Select((x, i) => (names != null && offset + i < names.Count ? names[offset + i] : "#" + (offset + i).ToString(CultureInfo.InvariantCulture)) + " = " + Raw(x)));
        }

        /// <summary>The trace's header row: simulation, iterations, event, the variables, then the outputs (objective marked), with units.</summary>
        public IReadOnlyList<string> Header()
        {
            List<string> result = new List<string> { "Simulation", "Main iteration", "Sub-iteration", "Event" };
            result.AddRange(Variables.Select(x => x.Header));
            result.AddRange(Outputs.Select((x, i) => i == 0 ? x.Header + " (objective)" : x.Header));
            return result;
        }

        /// <summary>One trace entry as full-precision invariant fields, in <see cref="Header"/> order.</summary>
        public IReadOnlyList<string> Fields(OptimisationTraceEntry optimisationTraceEntry)
        {
            List<string> result = new List<string>
            {
                optimisationTraceEntry.Simulation.ToString(CultureInfo.InvariantCulture),
                optimisationTraceEntry.MainIteration.ToString(CultureInfo.InvariantCulture),
                optimisationTraceEntry.SubIteration.ToString(CultureInfo.InvariantCulture),
                optimisationTraceEntry.Event.ToString(),
            };

            result.AddRange(optimisationTraceEntry.Coordinates.Select(Raw));
            result.AddRange(optimisationTraceEntry.Outputs.Select(Raw));
            return result;
        }

        /// <summary>
        /// The trace as CSV (RFC 4180): comma-separated, CRLF line ends, a field quoted when it holds a comma, quote or line
        /// break, one row per entry, every number full precision and invariant. Write it as UTF-8 (units such as °C).
        /// </summary>
        public string CsvText(IEnumerable<OptimisationTraceEntry> optimisationTraceEntries)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(string.Join(",", Header().Select(Csv))).Append("\r\n");
            foreach (OptimisationTraceEntry optimisationTraceEntry in optimisationTraceEntries ?? Enumerable.Empty<OptimisationTraceEntry>())
            {
                stringBuilder.Append(string.Join(",", Fields(optimisationTraceEntry).Select(Csv))).Append("\r\n");
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// The trace as tab-separated text for the clipboard (Excel pastes it into cells): the header row, then one row per
        /// entry, full precision and invariant. A tab or line break inside a name is replaced by a space.
        /// </summary>
        public string TabText(IEnumerable<OptimisationTraceEntry> optimisationTraceEntries)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(string.Join("\t", Header().Select(Tab))).Append("\r\n");
            foreach (OptimisationTraceEntry optimisationTraceEntry in optimisationTraceEntries ?? Enumerable.Empty<OptimisationTraceEntry>())
            {
                stringBuilder.Append(string.Join("\t", Fields(optimisationTraceEntry).Select(Tab))).Append("\r\n");
            }

            return stringBuilder.ToString();
        }

        /// <summary>A file name for the exported trace: the definition's name made safe for a file, then " - trace.csv".</summary>
        public static string CsvFileName(string? definitionName)
        {
            string name = string.IsNullOrWhiteSpace(definitionName) ? "Optimisation" : definitionName!.Trim();
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            name = new string(name.Select(x => invalid.Contains(x) ? '_' : x).ToArray());
            if (name.Length > 80)
            {
                name = name.Substring(0, 80).TrimEnd();
            }

            return name + " - trace.csv";
        }

        private TasOptimisationColumn Column(string name, string? unit, bool integer, IEnumerable<string>? options = null)
        {
            string? symbol = string.IsNullOrWhiteSpace(unit) ? null : unit!.Trim();
            int? decimals = null;
            if (symbol != null)
            {
                OptimisationUnit? optimisationUnit = global::SAM.Core.Optimisation.Query.OptimisationUnit(symbol, out _);
                if (optimisationUnit != null)
                {
                    symbol = optimisationUnit.Symbol;
                    if (optimisationUnit.UnitType != UnitType.Undefined)
                    {
                        // Only when SAM displays the declared unit itself; otherwise its display unit would need a
                        // conversion, which this view never does.
                        DisplayUnit displayUnit = quantityFormatter.DisplayUnit(optimisationUnit.UnitType.UnitCategory());
                        if (displayUnit.UnitType == optimisationUnit.UnitType)
                        {
                            decimals = displayUnit.Decimals;
                        }
                    }
                }
            }

            return new TasOptimisationColumn(name ?? string.Empty, symbol, decimals, integer, options);
        }

        private static string Csv(string field)
        {
            if (field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return field;
            }

            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        private static string Tab(string field)
        {
            return field.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
