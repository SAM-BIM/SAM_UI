// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Core.Optimisation;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using File = System.IO.File;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// PR5b: the results view's engineering formatting (<see cref="TasOptimisationFormatter"/>) through SAM's
    /// QuantityFormatter, the full-precision tooltip and copy, and the CSV trace. Display follows the culture; raw text,
    /// copy and CSV are invariant and round-trip. The kernel runs here with delegate evaluators (no process).
    /// </summary>
    public class TasOptimisationFormattingTests
    {
        private static readonly CultureInfo english = CultureInfo.GetCultureInfo("en-GB");
        private static readonly CultureInfo german = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>A definition with one variable and outputs in the given units (the first output is the objective).</summary>
        private static OptimisationDefinition Definition(string? variableUnit, params (string Name, string? Unit)[] outputs)
        {
            OptimisationDefinition result = new OptimisationDefinition() { Name = "Test" };
            result.Variables.Add(new DesignVariable("Setpoint", -5, 35, 3, 1) { Unit = variableUnit });
            foreach ((string name, string? unit) in outputs)
            {
                result.Outputs.Add(new OptimisationOutput(name) { Unit = unit });
            }

            result.Objective = new OptimisationObjective(outputs[0].Name, ObjectiveSense.Minimise);
            return result;
        }

        private static string Text(string? unit, double value, CultureInfo? cultureInfo = null)
        {
            TasOptimisationFormatter formatter = new TasOptimisationFormatter(Definition(null, ("Y", unit)), cultureInfo ?? english);
            return formatter.Text(formatter.Outputs[0], value);
        }

        [Theory]
        [InlineData("kWh", 2978.53252598965, "2,978.5 kWh")]
        [InlineData("°C", 4.968943799848584, "5.0 °C")]
        [InlineData("degC", 21.25, "21.3 °C")]
        [InlineData("K", 1.04, "1.0 K")]
        [InlineData("h", 274, "274 h")]
        [InlineData("h", 22.4, "22 h")]
        [InlineData("%", 37.5, "38 %")]
        [InlineData("kgCO2e", 3932.8885345459, "3,932.9 kgCO2e")]
        [InlineData("kg", 0.04, "0.0 kg")]
        [InlineData("W", 1234.5, "1,235 W")]
        [InlineData("m²", 12.345, "12.3 m²")]
        public void A_unit_SAM_displays_gets_its_display_decimals(string unit, double value, string expected)
        {
            Assert.Equal(expected, Text(unit, value));
        }

        [Theory]
        [InlineData("GBP", 7360.04370117188, "7,360 GBP")]
        [InlineData("£", 7360.04370117188, "7,360 GBP")]
        [InlineData("MWh", 27.3877943115234, "27.39 MWh")]
        [InlineData("kW", 12.3456, "12.35 kW")]
        [InlineData("tCO2e", 3.9328885345459, "3.933 tCO2e")]
        [InlineData("-", 0.336683690547943, "0.3367 -")]
        [InlineData("widgets", 12.3456, "12.35 widgets")]
        [InlineData(null, 4.968943799848584, "4.969")]
        [InlineData(null, 7360.04370117188, "7,360")]
        [InlineData(null, 0.30000000000000004, "0.3000")]
        [InlineData(null, 0.000123, "1.230E-4")]
        [InlineData(null, -0.00001, "-1.000E-5")]
        [InlineData(null, 0, "0")]
        public void Any_other_value_gets_four_significant_figures_and_is_never_converted(string? unit, double value, string expected)
        {
            Assert.Equal(expected, Text(unit, value));
        }

        [Fact]
        public void NaN_and_infinity_show_a_dash_without_a_unit()
        {
            Assert.Equal("—", Text("kWh", double.NaN));
            Assert.Equal("—", Text("GBP", double.PositiveInfinity));
            Assert.Equal("—", Text(null, double.NegativeInfinity));
        }

        [Fact]
        public void Display_follows_the_culture()
        {
            Assert.Equal("2.978,5 kWh", Text("kWh", 2978.53252598965, german));
            Assert.Equal("4,969", Text(null, 4.968943799848584, german));
            Assert.Equal("7.360 GBP", Text("GBP", 7360.04370117188, german));
        }

        [Fact]
        public void Columns_carry_the_canonical_unit_and_the_objective_comes_first()
        {
            OptimisationDefinition definition = Definition("degC", ("Cost", "£"), ("CO2", "kgCO2"), ("Plain", null));
            definition.Objective = new OptimisationObjective("CO2", ObjectiveSense.Minimise);
            TasOptimisationFormatter formatter = new TasOptimisationFormatter(definition, english);

            Assert.Equal("Setpoint [°C]", formatter.Variables[0].Header);
            Assert.Equal(["CO2", "Cost", "Plain"], formatter.Outputs.Select(x => x.Name));
            Assert.Equal(["kgCO2e", "GBP", null], formatter.Outputs.Select(x => x.Unit));
            Assert.Equal([1, null, null], formatter.Outputs.Select(x => x.Decimals));
            Assert.Equal(["Simulation", "Main iteration", "Sub-iteration", "Event", "Setpoint [°C]", "CO2 [kgCO2e] (objective)", "Cost [GBP]", "Plain"], formatter.Header());
        }

        [Fact]
        public void A_choice_and_counts_are_whole_numbers()
        {
            OptimisationDefinition definition = Definition(null, ("Y", "h"));
            definition.Variables[0] = new DesignVariable("Glazing", 1, 3) { Type = DesignVariableType.Discrete };
            TasOptimisationFormatter formatter = new TasOptimisationFormatter(definition, english);

            Assert.True(formatter.Variables[0].Integer);
            Assert.Equal("2", formatter.Text(formatter.Variables[0], 2));
            Assert.Equal("1,234", formatter.Number(formatter.Variables[0], 1234));

            //Simulation counts stay invariant integers (never QuantityFormatter's Count, which means persons).
            Assert.Equal("1234", TasOptimisationReport.Count(1234));
        }

        [Theory]
        [InlineData(4.968943799848584, "4.968943799848584")]
        [InlineData(0.30000000000000004, "0.30000000000000004")]
        [InlineData(7360.04370117188, "7360.04370117188")]
        [InlineData(1e-300, "1E-300")]
        [InlineData(double.NaN, "NaN")]
        [InlineData(double.PositiveInfinity, "Infinity")]
        public void The_raw_text_is_invariant_and_round_trips(double value, string expected)
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = german;
                string raw = TasOptimisationFormatter.Raw(value);
                Assert.Equal(expected, raw);
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture)));
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        private sealed class SynchronousProgress : IProgress<OptimisationProgress>
        {
            public List<OptimisationProgress> Reports { get; } = new List<OptimisationProgress>();

            public void Report(OptimisationProgress value)
            {
                Reports.Add(value);
            }
        }

        /// <summary>A real try-every-option run (SAM#190) whose outputs are awkward doubles.</summary>
        private static OptimisationResult TryEveryOptionResult(IProgress<OptimisationProgress>? progress = null)
        {
            double[][] outputs =
            [
                [4.968943799848584, 0.30000000000000004],
                [double.NaN, 1e-7],
                [7360.04370117188, -0.0],
            ];

            TryEveryOption tryEveryOption = new TryEveryOption();
            return tryEveryOption.Run(new OptimisationProblem([new OptimisationParameter("V1", 1, 1, 3, 1)], 2), new DelegateObjectiveEvaluator((request, token) => ObjectiveEvaluation.Success(outputs[(int)request.Coordinates[0] - 1])), progress);
        }

        private static OptimisationDefinition ChoiceDefinition()
        {
            OptimisationDefinition definition = Definition(null, ("Overheating, worst \"room\"", "h"), ("Cooling", "kWh"));
            definition.Name = "Glazing: choice / test";
            definition.Variables[0] = new DesignVariable("Glazing", 1, 3) { Type = DesignVariableType.Discrete, Unit = "°C" };
            return definition;
        }

        [Fact]
        public void Csv_has_names_and_units_and_every_value_at_full_precision_in_any_culture()
        {
            OptimisationResult result = TryEveryOptionResult();
            CultureInfo before = CultureInfo.CurrentCulture;
            string csv;
            try
            {
                CultureInfo.CurrentCulture = german;
                csv = new TasOptimisationFormatter(ChoiceDefinition(), german).CsvText(result.Entries);
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }

            string[] lines = csv.Split("\r\n");
            Assert.Equal("Simulation,Main iteration,Sub-iteration,Event,Glazing [°C],\"Overheating, worst \"\"room\"\" [h] (objective)\",Cooling [kWh]", lines[0]);
            Assert.Equal(result.Entries.Count + 2, lines.Length);
            Assert.Equal(string.Empty, lines[^1]);
            Assert.StartsWith("1,", lines[1]);
            Assert.Contains(",OptionEvaluated,1,4.968943799848584,0.30000000000000004", lines[1]);
            Assert.EndsWith(",2,NaN,1E-07", lines[2]);
            Assert.EndsWith(",3,7360.04370117188,-0", lines[3]);

            //Every number reads back bit for bit.
            for (int i = 0; i < result.Entries.Count; i++)
            {
                string[] fields = lines[i + 1].Split(',');
                List<double> values = result.Entries[i].Coordinates.Concat(result.Entries[i].Outputs).ToList();
                Assert.Equal(values.Select(BitConverter.DoubleToInt64Bits), fields.Skip(4).Select(x => BitConverter.DoubleToInt64Bits(double.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture))));
            }
        }

        [Fact]
        public void The_copied_trace_is_tab_separated_full_precision_with_a_header()
        {
            OptimisationResult result = TryEveryOptionResult();
            string text = new TasOptimisationFormatter(ChoiceDefinition(), german).TabText(result.Entries);
            string[] lines = text.Split("\r\n");

            Assert.Equal("Simulation\tMain iteration\tSub-iteration\tEvent\tGlazing [°C]\tOverheating, worst \"room\" [h] (objective)\tCooling [kWh]", lines[0]);
            Assert.Equal(["1", "4.968943799848584", "0.30000000000000004"], lines[1].Split('\t').Where((x, i) => i == 0 || i >= 5));
            Assert.DoesNotContain(",", lines[1].Split('\t')[5]);
        }

        [Fact]
        public void Try_every_option_results_format_without_throwing()
        {
            OptimisationResult result = TryEveryOptionResult();
            Assert.Contains(result.Entries, x => x.Event == OptimisationEvent.OptionEvaluated);
            TasOptimisationFormatter formatter = new TasOptimisationFormatter(ChoiceDefinition(), english);

            TasOptimisationProgressState state = new TasOptimisationProgressState(["Glazing"], ["Overheating", "Cooling"], formatter);
            TasOptimisationReport report = new TasOptimisationReport(result, ["Glazing"], ["Overheating", "Cooling"], null, false, formatter);

            List<TasOptimisationTraceRow> rows = result.Entries.Select(x => new TasOptimisationTraceRow(x, formatter)).ToList();
            Assert.Equal(["1 °C", "2 °C", "3 °C"], rows.Where(x => x.Entry.Event == OptimisationEvent.OptionEvaluated).Select(x => x.CoordinateTexts[0]));
            Assert.Equal("—", rows[1].OutputTexts[0]);
            Assert.Equal("NaN", rows[1].OutputRaw[0]);
            Assert.Equal("OptionEvaluated", rows[0].Event);

            Assert.True(report.Successful);
            Assert.Equal("Glazing = 1 °C (simulation 1)", report.Lines.Single(x => x.Title == "Best design").Detail);
            Assert.Equal("Overheating, worst \"room\" = 5 h", report.Lines.Single(x => x.Title == "Objective").Detail);
            Assert.Equal("Overheating = 4.968943799848584", report.Lines.Single(x => x.Title == "Objective").Raw);
        }

        [Fact]
        public void Report_lines_show_engineering_text_and_keep_full_precision_in_the_tooltip_and_the_copied_summary()
        {
            Optimiser goldenSection = new GoldenSection() { StoppingCriterion = GoldenSectionStoppingCriterion.AbsoluteDifference, AbsoluteDifference = 0.1 };
            OptimisationResult result = goldenSection.Run(new OptimisationProblem([new OptimisationParameter("Setpoint", 3, -5, 35, 1)], 2), new DelegateObjectiveEvaluator((request, token) => ObjectiveEvaluation.Success(7000 + System.Math.Pow(request.Coordinates[0] - 4.968943799848584, 2), 2 * request.Coordinates[0])));
            TasOptimisationFormatter formatter = new TasOptimisationFormatter(Definition("°C", ("Cost", "GBP"), ("Energy", "kWh")), english);

            TasOptimisationReport report = new TasOptimisationReport(result, ["Setpoint"], ["Cost", "Energy"], null, false, formatter);
            TasOptimisationCheck best = report.Lines.Single(x => x.Title == "Best design");
            Assert.Matches(@"^Setpoint = \d+\.\d °C \(simulation \d+\)$", best.Detail);
            Assert.Matches(@"^Setpoint = -?\d+\.\d{6,} \(simulation \d+\)$", best.Raw);
            Assert.Equal(best.Raw, best.ToolTipText);
            Assert.Matches(@"^Cost = 7,000 GBP$", report.Lines.Single(x => x.Title == "Objective").Detail);
            Assert.Matches(@"^Energy = \d+\.\d kWh$", report.Lines.Single(x => x.Title == "Recorded outputs").Detail);
            Assert.Matches(@"^\[\d+\.\d, \d+\.\d °C\]$", report.Lines.Single(x => x.Title == "Final interval").Detail);

            //Copy summary: every value at full precision, never the rounded text.
            string text = report.ToText();
            Assert.Contains("Best design: " + best.Raw, text);
            Assert.DoesNotContain("°C", text);
            Assert.DoesNotContain("7,000", text);

            //Without a formatter (as before PR5b) the detail is the full-precision text and there is no separate raw text.
            TasOptimisationReport plain = new TasOptimisationReport(result, ["Setpoint"], ["Cost", "Energy"], null, false);
            Assert.Equal(best.Raw, plain.Lines.Single(x => x.Title == "Best design").Detail);
            Assert.Null(plain.Lines.Single(x => x.Title == "Best design").Raw);
            Assert.Equal(text, plain.ToText());
        }

        [Fact]
        public void Progress_lines_use_the_formatter()
        {
            SynchronousProgress progress = new SynchronousProgress();
            TryEveryOptionResult(progress);
            TasOptimisationProgressState state = new TasOptimisationProgressState(["Glazing"], ["Overheating", "Cooling"], new TasOptimisationFormatter(ChoiceDefinition(), english));
            foreach (OptimisationProgress optimisationProgress in progress.Reports)
            {
                state.Add(optimisationProgress);
            }

            Assert.Equal("Simulation 1: Glazing = 1 °C -> Overheating, worst \"room\" = 5 h", state.LowestText());
        }

        [Theory]
        [InlineData("Office – setpoints", "Office – setpoints - trace.csv")]
        [InlineData("a/b:c*?", "a_b_c__ - trace.csv")]
        [InlineData(null, "Optimisation - trace.csv")]
        [InlineData("  ", "Optimisation - trace.csv")]
        public void The_exported_file_is_named_after_the_definition(string? name, string expected)
        {
            Assert.Equal(expected, TasOptimisationFormatter.CsvFileName(name));
        }
    }

    /// <summary>PR5b in the window: the trace after a real stub run, its tooltips, Copy trace and Export trace.</summary>
    [Collection(WpfCollection.Name)]
    public class TasOptimisationFormattingWindowTests : IDisposable
    {
        public TasOptimisationFormattingWindowTests()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
        }

        private static T Control<T>(FrameworkElement frameworkElement, string name) where T : class
        {
            return Assert.IsAssignableFrom<T>(frameworkElement.FindName(name));
        }

        [WpfFact]
        public async Task The_trace_shows_engineering_values_with_full_precision_tooltips_copy_and_csv()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace.Input(TasOptimisationExample.SystemsDemoGoldenSection));
            Assert.False(Control<Button>(window, "button_CopyTrace").IsEnabled);
            Assert.False(Control<Button>(window, "button_ExportTrace").IsEnabled);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.True(report.Successful);
            Assert.True(Control<Button>(window, "button_CopyTrace").IsEnabled);
            Assert.True(Control<Button>(window, "button_ExportTrace").IsEnabled);

            //The columns keep their headers; each value column shows the engineering text, its tooltip and its copied
            //content are the full-precision value.
            DataGrid dataGrid = Control<DataGrid>(window, "dataGrid_Trace");
            DataGridTextColumn cost = Assert.IsType<DataGridTextColumn>(Assert.Single(dataGrid.Columns, x => (string)x.Header == "Cost"));
            Assert.Equal("OutputTexts[1]", ((Binding)cost.Binding).Path.Path);
            Assert.Equal("OutputRaw[1]", ((Binding)cost.ClipboardContentBinding).Path.Path);
            Assert.Equal("OutputRaw[1]", ((Binding)cost.ElementStyle.Setters.OfType<Setter>().Single(x => x.Property == FrameworkElement.ToolTipProperty).Value).Path.Path);
            Assert.Equal(DataGridClipboardCopyMode.IncludeHeader, dataGrid.ClipboardCopyMode);
            Assert.Equal(DataGridSelectionMode.Extended, dataGrid.SelectionMode);

            TasOptimisationTraceRow row = window.TraceRows[0];
            TasOptimisationFormatter formatter = new TasOptimisationFormatter(Definition(workspace));
            Assert.Equal(row.Outputs[1].ToString("R", CultureInfo.InvariantCulture), row.OutputRaw[1]);
            Assert.Equal(formatter.Text(formatter.Outputs[1], row.Outputs[1]), row.OutputTexts[1]);
            Assert.EndsWith(" GBP", row.OutputTexts[1]);

            //Copy trace: every row, tab-separated, full precision, with a header with units.
            string[] lines = window.TraceText().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Simulation\tMain iteration\tSub-iteration\tEvent\tSetpoint\tResult [GBP] (objective)\tCost [GBP]\tCO2", lines[0]);
            Assert.Equal(window.TraceRows.Count + 1, lines.Length);
            Assert.Equal(row.CoordinateRaw[0], lines[1].Split('\t')[4]);

            //Export: the same rows as CSV, UTF-8 with a byte order mark.
            string path = Path.Combine(workspace.Directory, "trace.csv");
            window.ExportTrace(path);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            string csv = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            Assert.Equal(new TasOptimisationFormatter(Definition(workspace)).CsvText(window.TraceRows.Select(x => x.Entry)), csv);
            Assert.Equal(window.TraceRows.Count + 2, csv.Split("\r\n").Length);

            //The result card: engineering text with the full precision as the tooltip; Copy summary stays full precision.
            TasOptimisationCheck best = report.Lines.Single(x => x.Title == "Best design");
            Assert.NotNull(best.Raw);
            Assert.Contains(report.BestPoint[0].ToString("R", CultureInfo.InvariantCulture), best.Raw);
            Assert.Contains("Best design: " + best.Raw, report.ToText());
        }

        private static OptimisationDefinition Definition(TasOptimisationWorkspace workspace)
        {
            Assert.True(workspace.Input().TryGetDefinition(out OptimisationDefinition definition, out _));
            return definition;
        }
    }
}
