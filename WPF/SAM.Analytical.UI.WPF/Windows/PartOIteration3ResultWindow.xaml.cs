// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <para><b>The last successful report stays reachable, and stays labelled historical</b></para>
    /// <para>
    /// Every completed pairing writes its whole review to a file beside the pairing record. When the
    /// design later moves and Review correctly refuses the pairing, that file is <b>not</b> overwritten
    /// and <b>not</b> presented as the current answer - the window offers it under its own name, says in
    /// the refusal text what it is and is not, and leaves the refusal as the only statement about the
    /// model in front of the user.
    /// </para>
    ///
    /// <para><b>It renders text, it does not compose it</b></para>
    /// <para>
    /// Every line here comes from <see cref="PartOIteration3ReportText"/>, which is also what the
    /// persisted report is assembled from. A window that composed its own wording would be a second
    /// answer waiting to disagree with the saved one.
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

        /// <summary>The persisted report this window can open, or null where there is none on disk.</summary>
        private string path_Report_Available;

        public PartOIteration3ResultWindow()
        {
            InitializeComponent();

            Fit();
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

        /// <summary>
        /// Keeps the window inside the desktop it opens on.
        /// <para>
        /// The XAML asks for 1400x820, which is what this window wants on a 1920x1080 desktop at 100%
        /// scaling. At 125% that desktop is 825 device-independent pixels tall and at 150% it is 688, so
        /// the requested height alone would put the action bar below the taskbar with no way to reach it:
        /// the window is resizable, but a person cannot drag a title bar up past the top of the screen.
        /// So the request is clamped to the work area and the window is re-centred inside it.
        /// </para>
        /// <para>
        /// <c>SystemParameters.WorkArea</c> is already in device-independent pixels, which is what
        /// <c>Width</c> and <c>Height</c> are measured in, so no DPI arithmetic belongs here.
        /// </para>
        /// </summary>
        private void Fit()
        {
            Rect rect;

            try
            {
                rect = SystemParameters.WorkArea;
            }
            catch (Exception)
            {
                //No desktop to measure - a test host, or a session with no interactive window station.
                //The XAML's own size stands.
                return;
            }

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            //Allowance for the window chrome, which is outside the client size these properties set.
            double width = Math.Min(Width, Math.Max(MinWidth, rect.Width - 20));
            double height = Math.Min(Height, Math.Max(MinHeight, rect.Height - 40));

            bool clamped = width < Width || height < Height;

            Width = width;
            Height = height;

            if (!clamped)
            {
                return;
            }

            //Re-centred rather than left where CenterOwner would put a window that was taller than the
            //screen a moment ago.
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = rect.Left + ((rect.Width - width) / 2);
            Top = rect.Top + ((rect.Height - height) / 2);
        }

        private void Refresh()
        {
            if (partOIteration3Result is null)
            {
                return;
            }

            bool complete = partOIteration3Result.IsComplete;

            textBlock_Outcome.Text = PartOIteration3ReportText.Outcome(partOIteration3Result);
            textBlock_Summary.Text = PartOIteration3ReportText.Summary(partOIteration3Result);

            dataGrid_Stage.ItemsSource = partOIteration3Result.Ledger.Stages;

            //Resolved before the refusal text is composed, because a refusal that has a historical report
            //to point at says so, and one that has not must not.
            path_Report_Available = Path_Report();

            textBlock_Refusal.Text = complete ? string.Empty : Refusal();
            scrollViewer_Refusal.Visibility = complete ? Visibility.Collapsed : Visibility.Visible;

            //Exactly one of the refusal and the comparison grid is ever shown, and the one that is takes
            //the slack. A star row holding a collapsed control is an empty band in the middle of the
            //window, and on a refusal that band is where the evidence should have been.
            rowDefinition_Refusal.Height = complete ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
            rowDefinition_Refusal.MinHeight = complete ? 0 : 80;

            rowDefinition_Comparison.Height = complete ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
            rowDefinition_Comparison.MinHeight = complete ? 96 : 0;

            Visibility visibility = complete ? Visibility.Visible : Visibility.Collapsed;

            stackPanel_Comparison.Visibility = visibility;
            stackPanel_Filters.Visibility = visibility;
            dataGrid_Comparison.Visibility = visibility;

            if (complete)
            {
                textBlock_Comparison.Text = PartOIteration3ReportText.Comparison(partOIteration3Result, CultureInfo.CurrentCulture);
                textBlock_Dwellings.Text = PartOIteration3ReportText.Dwellings(partOIteration3Result, CultureInfo.CurrentCulture);

                rows = PartOIteration3Row.Rows(partOIteration3Result.Comparison);
            }
            else
            {
                textBlock_Comparison.Text = string.Empty;
                textBlock_Dwellings.Text = string.Empty;

                rows = [];
            }

            ApplyFilter();

            textBox_Diagnostics.Text = PartOIteration3ReportText.Diagnostics(partOIteration3Result);

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

            RefreshReportButton(complete);
        }

        /// <summary>
        /// The report button, which says two different things depending on what it would open.
        /// <para>
        /// After a completed review it opens <b>this</b> review's own saved report. After a refusal it
        /// opens the last successful one, under a name that says so - because the refusal means the
        /// pairing no longer describes the model in front of the user, and a button labelled the same way
        /// in both states would be an invitation to read a stale report as a current one.
        /// </para>
        /// </summary>
        private void RefreshReportButton(bool complete)
        {
            bool exists = !string.IsNullOrWhiteSpace(path_Report_Available);

            button_OpenReport.IsEnabled = exists;

            if (complete)
            {
                button_OpenReport.Content = "Open A/B Review Report";

                button_OpenReport.ToolTip = exists
                    ? string.Format("Open the full A/B review report this review saved:{0}{1}", Environment.NewLine, path_Report_Available)
                    : partOIteration3Result?.Refusal_Report ?? "This review saved no A/B report.";

                return;
            }

            button_OpenReport.Content = "Open Last Successful Report";

            button_OpenReport.ToolTip = exists
                ? string.Format(
                    "Open the last A/B review report saved for this pairing:{0}{1}{0}{0}It is HISTORICAL. It describes the pairing as it was when it succeeded, NOT the model in front of you - which is why this review refused.",
                    Environment.NewLine,
                    path_Report_Available)
                : "No successful A/B review report has been saved for this pairing.";
        }

        /// <summary>
        /// The refusal as the report authority states it, plus - only where one is actually on disk - a
        /// statement that a historical report exists, and what it is and is not.
        /// </summary>
        private string Refusal()
        {
            string refusal = PartOIteration3ReportText.Refusal(partOIteration3Result);

            if (string.IsNullOrWhiteSpace(path_Report_Available))
            {
                return refusal;
            }

            return string.Concat(
                refusal,
                Environment.NewLine,
                Environment.NewLine,
                string.Format(
                    "A previously saved A/B review report for this pairing is still on disk at '{0}'. It is HISTORICAL: it describes the pairing as it was when it succeeded, and it does NOT describe the analytical model in front of you. This refusal has not overwritten it, and it is not offered as this attempt's result.",
                    path_Report_Available));
        }

        /// <summary>
        /// The persisted report to offer, or null where there is none.
        /// <para>
        /// A completed review names the report it has just written. A refused one names nothing, so the
        /// path is derived from the pairing record exactly as the writer derives it, and is offered only
        /// where the file is genuinely there.
        /// </para>
        /// </summary>
        private string Path_Report()
        {
            string path = partOIteration3Result?.Path_Report;

            if (string.IsNullOrWhiteSpace(path))
            {
                path = PartOIteration3Paths.Path_Report_ForRecord(partOIteration3Result?.Path_Record);
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return System.IO.File.Exists(path) ? path : null;
            }
            catch (Exception)
            {
                //A path that cannot be stat'ed offers nothing, which is the safe direction: a disabled
                //button is better than one claiming a report exists.
                return null;
            }
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
        /// The whole window as tab-separated text, over the rows currently shown.
        /// <para>
        /// The same composition the persisted report uses, so a copy and the saved file say the same
        /// things in the same order. They differ in exactly two stated ways: a copy follows the filters a
        /// person is holding and is formatted for their culture, while the saved report takes every row,
        /// the invariant culture and the pairing's full provenance.
        /// </para>
        /// </summary>
        internal string CopyAllText()
        {
            return PartOIteration3ReportText.Text(partOIteration3Result, Rows_Visible, CultureInfo.CurrentCulture);
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

            //Owned, so the child cannot be lost behind the main application window.
            new System.Windows.Interop.WindowInteropHelper(partOTM59ResultWindow).Owner = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            partOTM59ResultWindow.ShowDialog();
        }

        private void button_OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Open(Folder());
        }

        private void button_OpenReport_Click(object sender, RoutedEventArgs e)
        {
            //Re-resolved on the click: the file may have been moved or deleted since the window opened,
            //and handing the shell a path that has gone is an error dialog nobody asked for.
            string path = Path_Report();

            if (string.IsNullOrWhiteSpace(path))
            {
                path_Report_Available = null;

                RefreshReportButton(partOIteration3Result?.IsComplete ?? false);

                return;
            }

            Open(path);
        }

        /// <summary>
        /// Hands a path to the shell, or does nothing where there is nothing to hand it.
        /// <para>
        /// Every failure is a refusal to act, never an exception: a missing file or folder, a path with
        /// no shell association, and a shell that declines all leave the window exactly as it was, with
        /// the path still on screen in the notes.
        /// </para>
        /// </summary>
        private static void Open(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
                {
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception)
            {
                //No shell association, a refused launch, or a path the file system would not answer for.
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
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException)
            {
                //Shown with Show() rather than ShowDialog() - a host that embeds this window rather than
                //the production command, which always shows it modally. Closing is the same intent.
                Close();
            }
        }
    }
}
