// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What one Approved Document O Iteration 3 pairing produced, or refused to produce.
    ///
    /// <para><b>A refused pairing shows no Candidate B number, anywhere</b></para>
    /// <para>
    /// On a refusal the comparison summary, the filters and the whole grid are <b>collapsed</b>, and what
    /// is shown instead is the refused stage, its reasons verbatim, the artifacts that attempt genuinely
    /// produced, and the stages that never ran. This is not a styling choice: a grid of temperatures
    /// beside a refusal is the most convincing wrong answer this window could give, because every number
    /// in it would be real - just not from the run being reported.
    /// </para>
    /// <para>
    /// It is enforced upstream as well as here - <c>PartOIteration3Result</c> holds no comparison for a
    /// refused ledger - so there is nothing to render even if this code forgot.
    /// </para>
    ///
    /// <para><b>Built for five thousand dwellings</b></para>
    /// <para>
    /// Both grids virtualise and recycle, the rows are flat values computed once, the search term is
    /// lower-cased once per keystroke rather than once per row, and the dwelling grouping is a
    /// <see cref="CollectionView"/> group rather than a control per dwelling. Filtering rebuilds one
    /// list and reassigns one <c>ItemsSource</c>; nothing walks the model, and no row is realised until
    /// it scrolls into view.
    /// </para>
    /// </summary>
    public partial class PartOIteration3ResultWindow : System.Windows.Window
    {
        private PartOIteration3Result partOIteration3Result;

        private List<PartOIteration3Row> rows = [];

        public PartOIteration3ResultWindow()
        {
            InitializeComponent();
        }

        /// <summary>The pairing to present. Setting it rebuilds the whole window.</summary>
        public PartOIteration3Result Result
        {
            get
            {
                return partOIteration3Result;
            }

            set
            {
                partOIteration3Result = value;

                Refresh();
            }
        }

        private void Refresh()
        {
            if (partOIteration3Result is null)
            {
                return;
            }

            bool complete = partOIteration3Result.IsComplete;

            textBlock_Outcome.Text = complete
                ? string.Format(
                    "Iteration 3 {0} COMPLETE. Reference A {1}; Candidate B {2}.",
                    partOIteration3Result.IsRestored ? "review" : "run",
                    Verdict(partOIteration3Result.Assessment_ReferenceA),
                    Verdict(partOIteration3Result.Assessment_CandidateB))
                : string.Format(
                    "Iteration 3 {0} REFUSED at {1}. No Candidate B result is presented.",
                    partOIteration3Result.IsRestored ? "review" : "run",
                    partOIteration3Result.Ledger.Stage_Refused.HasValue ? Core.Query.Description(partOIteration3Result.Ledger.Stage_Refused.Value) : "an unrecorded stage");

            textBlock_Summary.Text = Summary();

            dataGrid_Stage.ItemsSource = partOIteration3Result.Ledger.Stages;

            textBlock_Refusal.Text = complete ? string.Empty : Refusal();
            textBlock_Refusal.Visibility = complete ? Visibility.Collapsed : Visibility.Visible;

            Visibility visibility = complete ? Visibility.Visible : Visibility.Collapsed;

            stackPanel_Comparison.Visibility = visibility;
            stackPanel_Filters.Visibility = visibility;
            dataGrid_Comparison.Visibility = visibility;

            if (complete)
            {
                textBlock_Comparison.Text = Comparison();
                textBlock_Dwellings.Text = Dwellings();

                rows = PartOIteration3Row.Rows(partOIteration3Result.Comparison);
            }
            else
            {
                textBlock_Comparison.Text = string.Empty;
                textBlock_Dwellings.Text = string.Empty;

                rows = [];
            }

            ApplyFilter();

            textBox_Diagnostics.Text = Diagnostics();

            button_ReportA.IsEnabled = partOIteration3Result.Assessment_ReferenceA?.ReportText is not null;
            button_ReportA.ToolTip = button_ReportA.IsEnabled
                ? "Show the production CIBSE TM59 report for Reference A - the existing TBD/IZAM route."
                : "Reference A produced no TM59 report on this attempt.";

            button_ReportB.IsEnabled = complete && partOIteration3Result.Assessment_CandidateB?.ReportText is not null;
            button_ReportB.ToolTip = button_ReportB.IsEnabled
                ? "Show the production CIBSE TM59 report for Candidate B - the explicit TAS Systems route."
                : "Candidate B produced no TM59 report on this attempt.";

            button_OpenFolder.IsEnabled = !string.IsNullOrWhiteSpace(Folder());
            button_OpenFolder.ToolTip = button_OpenFolder.IsEnabled ? "Open the folder holding this pairing's files." : "This pairing recorded no folder.";
        }

        private static string Verdict(PartOIteration3Assessment partOIteration3Assessment)
        {
            return partOIteration3Assessment is null || !partOIteration3Assessment.IsAssessed
                ? "was not assessed"
                : Core.Query.Description(partOIteration3Assessment.OccupiedSpaceComplianceStatus);
        }

        private string Summary()
        {
            PartOIteration3Record partOIteration3Record = partOIteration3Result.Record;

            StringBuilder stringBuilder = new();

            stringBuilder.Append(partOIteration3Result.IsRestored
                ? "Reopened from the persisted pairing record. No TAS simulation was run and no TAS file was written."
                : "Produced in this session.");

            if (partOIteration3Record is not null)
            {
                stringBuilder.Append(string.Format(
                    " Reference A '{0}' against Candidate B '{1}'. Case: {2}.",
                    partOIteration3Record.ProjectName_ReferenceA ?? "?",
                    partOIteration3Record.ProjectName_CandidateB ?? "?",
                    partOIteration3Record.Fingerprint_Scenario ?? "not recorded"));

                if (partOIteration3Record.Count_AirSystem != 0)
                {
                    stringBuilder.Append(string.Format(
                        " {0} physical air system(s), {1} bound room(s), {2} directed leg(s) ({3} supply, {4} extract, {5} transfer).",
                        partOIteration3Record.Count_AirSystem,
                        partOIteration3Record.Bindings.Count,
                        partOIteration3Record.Count_Connection_Supply + partOIteration3Record.Count_Connection_Extract + partOIteration3Record.Count_Connection_Transfer,
                        partOIteration3Record.Count_Connection_Supply,
                        partOIteration3Record.Count_Connection_Extract,
                        partOIteration3Record.Count_Connection_Transfer));
                }

                if (!string.IsNullOrWhiteSpace(partOIteration3Record.Method_ResultantTemperature))
                {
                    stringBuilder.Append(string.Format(" Resultant temperature obtained by: {0}.", partOIteration3Record.Method_ResultantTemperature));
                }
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// The refusal, in the order a person needs it: what refused, why in the authority's own words,
        /// what this attempt genuinely produced, and what never ran.
        /// </summary>
        private string Refusal()
        {
            StringBuilder stringBuilder = new();

            PartOIteration3Ledger partOIteration3Ledger = partOIteration3Result.Ledger;

            if (partOIteration3Ledger.Stage_Refused.HasValue)
            {
                PartOIteration3StageState partOIteration3StageState = partOIteration3Ledger.State(partOIteration3Ledger.Stage_Refused.Value);

                stringBuilder.AppendLine(string.Format("REFUSED at {0}: {1}", partOIteration3StageState.Name, partOIteration3StageState.Detail));

                foreach (string reason in partOIteration3StageState.Reasons)
                {
                    stringBuilder.AppendLine(string.Format("  - {0}", reason));
                }
            }

            List<string> artifacts = partOIteration3Ledger.Artifacts;

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(artifacts.Count == 0
                ? "This attempt produced no file. Any Candidate B file in the output folder was left by an earlier attempt and is not evidence of this one."
                : "Files this attempt created or updated:");

            foreach (string artifact in artifacts)
            {
                stringBuilder.AppendLine(string.Format("  - {0}", artifact));
            }

            List<string> notRun = [];
            foreach (PartOIteration3StageState partOIteration3StageState in partOIteration3Ledger.Stages)
            {
                if (partOIteration3StageState.Status == PartOIteration3StageStatus.NotRun)
                {
                    notRun.Add(partOIteration3StageState.Name);
                }
            }

            if (notRun.Count != 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(string.Format("Not run: {0}.", string.Join(", ", notRun)));
            }

            return stringBuilder.ToString().TrimEnd();
        }

        private string Comparison()
        {
            PartOIteration3Statistics partOIteration3Statistics = partOIteration3Result.Comparison.Statistics;

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} room(s), {1} hourly value(s) each side. Mean A {2:0.###} °C, mean B {3:0.###} °C, mean bias B−A {4:0.###} K, RMSE {5:0.###} K, maximum |B−A| {6:0.###} K in '{7}' at hour {8}. {9} TM59 criterion outcome(s) differ.",
                partOIteration3Statistics.Count_Rooms,
                partOIteration3Statistics.Count_Values,
                partOIteration3Statistics.Mean_A,
                partOIteration3Statistics.Mean_B,
                partOIteration3Statistics.MeanBias,
                partOIteration3Statistics.RootMeanSquareError,
                partOIteration3Statistics.MaximumAbsoluteDifference,
                partOIteration3Statistics.Name_Space_MaximumAbsoluteDifference ?? "-",
                partOIteration3Statistics.Hour_MaximumAbsoluteDifference,
                partOIteration3Result.Comparison.Count_Changed);
        }

        private string Dwellings()
        {
            StringBuilder stringBuilder = new();

            foreach (PartOIteration3DwellingStatistics partOIteration3DwellingStatistics in partOIteration3Result.Comparison.Dwellings)
            {
                if (stringBuilder.Length != 0)
                {
                    stringBuilder.Append("   |   ");
                }

                stringBuilder.Append(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}: bias {1:0.###} K, RMSE {2:0.###} K, max {3:0.###} K",
                    partOIteration3DwellingStatistics.Name_Dwelling ?? "-",
                    partOIteration3DwellingStatistics.Statistics.MeanBias,
                    partOIteration3DwellingStatistics.Statistics.RootMeanSquareError,
                    partOIteration3DwellingStatistics.Statistics.MaximumAbsoluteDifference));
            }

            return stringBuilder.ToString();
        }

        private string Diagnostics()
        {
            StringBuilder stringBuilder = new();

            foreach (string note in partOIteration3Result.Notes)
            {
                stringBuilder.AppendLine(note);
            }

            foreach (string note in partOIteration3Result.Record?.Notes_Scope ?? [])
            {
                stringBuilder.AppendLine(note);
            }

            if (!string.IsNullOrWhiteSpace(partOIteration3Result.Path_Record))
            {
                stringBuilder.AppendLine(string.Format("Pairing record: {0}", partOIteration3Result.Path_Record));
            }

            return stringBuilder.Length == 0 ? "Nothing was recorded." : stringBuilder.ToString();
        }

        /// <summary>
        /// Rebuilds the visible rows from the three filters, then regroups. One pass over the rows, one
        /// assignment; the grid virtualises whatever comes out.
        /// </summary>
        private void ApplyFilter()
        {
            if (dataGrid_Comparison is null)
            {
                return;
            }

            string text = textBox_Search?.Text;
            string text_Lower = string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLowerInvariant();

            bool changedOnly = checkBox_Changed?.IsChecked ?? false;
            bool failuresOnly = checkBox_Failures?.IsChecked ?? false;

            List<PartOIteration3Row> result = [];

            foreach (PartOIteration3Row partOIteration3Row in rows)
            {
                if (changedOnly && !partOIteration3Row.Changed)
                {
                    continue;
                }

                if (failuresOnly && !partOIteration3Row.IsFailure)
                {
                    continue;
                }

                if (!partOIteration3Row.Matches(text_Lower))
                {
                    continue;
                }

                result.Add(partOIteration3Row);
            }

            ListCollectionView listCollectionView = new(result);
            listCollectionView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PartOIteration3Row.Dwelling)));

            dataGrid_Comparison.ItemsSource = listCollectionView;

            textBlock_RowCount.Text = result.Count == rows.Count
                ? string.Format("{0} row(s)", rows.Count)
                : string.Format("{0} of {1} row(s)", result.Count, rows.Count);
        }

        /// <summary>The visible rows, in their current order - what Copy All writes. Exposed for tests.</summary>
        internal List<PartOIteration3Row> Rows_Visible
        {
            get
            {
                List<PartOIteration3Row> result = [];

                foreach (object item in dataGrid_Comparison.ItemsSource as ListCollectionView ?? (System.Collections.IEnumerable)new List<PartOIteration3Row>())
                {
                    if (item is PartOIteration3Row partOIteration3Row)
                    {
                        result.Add(partOIteration3Row);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// The whole window as tab-separated text.
        /// <para>
        /// <b>Deterministic.</b> Invariant culture for every number, the rows in their built order, and
        /// nothing read off a rendered control - so re-exporting an unchanged completed pairing, in this
        /// session or after reopening it, produces the same bytes.
        /// </para>
        /// </summary>
        internal string CopyAllText()
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine(textBlock_Outcome.Text);
            stringBuilder.AppendLine(textBlock_Summary.Text);

            if (textBlock_Refusal.Visibility == Visibility.Visible)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(textBlock_Refusal.Text);
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Stage\tStatus\tDetail");

            foreach (PartOIteration3StageState partOIteration3StageState in partOIteration3Result?.Ledger.Stages ?? [])
            {
                stringBuilder.AppendLine(string.Format("{0}\t{1}\t{2}", partOIteration3StageState.Name, partOIteration3StageState.StatusText, partOIteration3StageState.Detail));

                foreach (string reason in partOIteration3StageState.Reasons)
                {
                    stringBuilder.AppendLine(string.Format("\tREFUSED\t{0}", reason));
                }

                foreach (string artifact in partOIteration3StageState.Artifacts)
                {
                    stringBuilder.AppendLine(string.Format("\tARTIFACT\t{0}", artifact));
                }
            }

            if (partOIteration3Result?.IsComplete ?? false)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(textBlock_Comparison.Text);
                stringBuilder.AppendLine(textBlock_Dwellings.Text);

                stringBuilder.AppendLine();
                stringBuilder.AppendLine("Dwelling\tSpace\tTM59 criterion\tMechanical\tA actual\tA limit\tA status\tB actual\tB limit\tB status\tDelta actual\tHours\tMean A\tMean B\tBias B-A\tRMSE\tMax |B-A|\tat hour");

                foreach (PartOIteration3Row partOIteration3Row in Rows_Visible)
                {
                    stringBuilder.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12:0.###}\t{13:0.###}\t{14:0.###}\t{15:0.###}\t{16:0.###}\t{17}",
                        partOIteration3Row.Dwelling,
                        partOIteration3Row.Space,
                        partOIteration3Row.Criterion,
                        partOIteration3Row.Mechanical,
                        Text(partOIteration3Row.Actual_A),
                        Text(partOIteration3Row.Limit_A),
                        partOIteration3Row.Status_A,
                        Text(partOIteration3Row.Actual_B),
                        Text(partOIteration3Row.Limit_B),
                        partOIteration3Row.Status_B,
                        Text(partOIteration3Row.Delta_Actual),
                        partOIteration3Row.Count,
                        partOIteration3Row.Mean_A,
                        partOIteration3Row.Mean_B,
                        partOIteration3Row.MeanBias,
                        partOIteration3Row.RootMeanSquareError,
                        partOIteration3Row.MaximumAbsoluteDifference,
                        partOIteration3Row.Hour_MaximumAbsoluteDifference));
                }
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(textBox_Diagnostics.Text);

            return stringBuilder.ToString();
        }

        /// <summary>A missing count is an em dash, exactly as the grid renders it - so the two agree.</summary>
        private static string Text(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "—";
        }

        private string Folder()
        {
            string path = partOIteration3Result?.Path_Record;

            if (string.IsNullOrWhiteSpace(path))
            {
                path = partOIteration3Result?.Record?.File(PartOIteration3Roles.Bridge_TSD)?.Path;
            }

            return string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetDirectoryName(path);
        }

        private void textBox_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void button_ReportA_Click(object sender, RoutedEventArgs e)
        {
            ShowReport(partOIteration3Result?.Assessment_ReferenceA, partOIteration3Result?.Path_TM59Report_ReferenceA, "Reference A");
        }

        private void button_ReportB_Click(object sender, RoutedEventArgs e)
        {
            ShowReport(partOIteration3Result?.Assessment_CandidateB, partOIteration3Result?.Path_TM59Report_CandidateB, "Candidate B");
        }

        /// <summary>
        /// The existing TM59 result window, over the production report text this assessment produced -
        /// the same window the ordinary Review Results command shows, so there is one place a TM59 report
        /// is read.
        /// <para>
        /// The report file named is the result's, not the assessment's: the result names only a report
        /// this run or this review demonstrably wrote.
        /// </para>
        /// </summary>
        private void ShowReport(PartOIteration3Assessment partOIteration3Assessment, string path_Report, string description)
        {
            if (partOIteration3Assessment?.ReportText is null)
            {
                return;
            }

            PartOTM59ResultWindow partOTM59ResultWindow = new()
            {
                Report = partOIteration3Assessment.ReportText,
                Title = string.Format("Part O — Iteration 3 — {0} — CIBSE TM59", description),
            };

            partOTM59ResultWindow.SetDiagnostics(partOIteration3Assessment.AssociationRefusals, partOIteration3Assessment.VentilationStrategyRefusals);

            partOTM59ResultWindow.Summary = string.Format(
                "{0}: {1} over {2} processed space(s). {3}",
                description,
                Core.Query.Description(partOIteration3Assessment.OccupiedSpaceComplianceStatus),
                partOIteration3Assessment.Count_Processed,
                string.IsNullOrWhiteSpace(path_Report) ? "No report file was written." : string.Format("Report: {0}", path_Report));

            new System.Windows.Interop.WindowInteropHelper(partOTM59ResultWindow).Owner = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            partOTM59ResultWindow.ShowDialog();
        }

        private void button_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = Folder();

            if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
            {
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                //No shell association, or the shell refused. The path is on screen in the notes either way.
            }
        }

        private void button_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(CopyAllText());
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                //Another process can hold the clipboard open; the text is still on screen.
            }
        }

        private void button_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
