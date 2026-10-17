// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The Tas/OpenStudio comparison view (SAM_UI#231, Phase 1), opened by Run Simulation &gt; Both when its run finishes
    /// (<see cref="ShowRun"/>) and by Simulate &gt; Compare Tas/OpenStudio: choose a Tas and an OpenStudio B1a document - or a comparison
    /// run folder - or run both engines on a frozen copy of the open model, and see SAM_Validation's comparison of the two: model and
    /// per-space metrics with units, differences and bands, which metrics are missing, not equivalent or not reported, the provenance,
    /// the diagnostics and the unmatched spaces; open or export the Markdown/CSV/JSON reports. When only one engine of a run completed,
    /// that engine's results are shown on their own, marked partially completed and not compared.
    /// <para>
    /// The window is read-only: it never changes the open model or a run folder (it writes only to a new run folder, an export folder or
    /// a temporary report folder). It computes nothing - see <see cref="EngineComparisonRow"/>.
    /// </para>
    /// </summary>
    public partial class EngineComparisonWindow : System.Windows.Window
    {
        private const string AllSpaces = "(all spaces)";

        private readonly UIAnalyticalModel? uIAnalyticalModel;

        private EngineComparison? engineComparison;

        private EngineComparisonRun? engineComparisonRun;

        private EngineResult? engineResult;

        private IReadOnlyList<EngineComparisonRow>? spaceRows;

        private bool running;

        public EngineComparisonWindow(UIAnalyticalModel? uIAnalyticalModel)
        {
            InitializeComponent();

            this.uIAnalyticalModel = uIAnalyticalModel;

            Button_BrowseTas.Click += (s, e) => Browse(TextBox_Tas, "Select the Tas B1a document (benchmark-TAS.json)");
            Button_BrowseOpenStudio.Click += (s, e) => Browse(TextBox_OpenStudio, "Select the OpenStudio B1a document (benchmark-OpenStudio.json)");
            Button_LoadFolder.Click += (s, e) => LoadFolder();
            Button_Compare.Click += (s, e) => Compare(TextBox_Tas.Text, TextBox_OpenStudio.Text, null);
            Button_Run.Click += async (s, e) => await RunAsync();
            Button_OpenReport.Click += (s, e) => OpenReport();
            Button_Export.Click += (s, e) => Export();
            Button_OpenFolder.Click += (s, e) => OpenFolder();
            Button_Close.Click += (s, e) => Close();
            ComboBox_Space.SelectionChanged += (s, e) => RefreshSpaces();

            Button_Run.IsEnabled = uIAnalyticalModel != null;
            Button_Run.ToolTip = uIAnalyticalModel == null
                ? "Open a model to run both engines on it."
                : "The same as Run Simulation > Both: freeze the open model, run benchmark-tas and benchmark-openstudio on the read-only snapshot one after the other with one weather file, and compare when both complete. Needs a licensed Tas and OpenStudio 3.10. The open model is not changed.";

            Closing += (s, e) =>
            {
                if (running)
                {
                    MessageBox.Show(this, "A comparison run is in progress. Cancel it in its progress window first.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                    e.Cancel = true;
                }
            };
        }

        /// <summary>Loads two B1a documents into the window, as Compare does.</summary>
        public bool Compare(string? tasPath, string? openStudioPath, EngineComparisonRun? engineComparisonRun)
        {
            if (string.IsNullOrWhiteSpace(tasPath) || !File.Exists(tasPath) || string.IsNullOrWhiteSpace(openStudioPath) || !File.Exists(openStudioPath))
            {
                MessageBox.Show(this, "Choose both a Tas and an OpenStudio B1a document first.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            EngineComparison engineComparison_Loaded;
            try
            {
                engineComparison_Loaded = Query.LoadEngineComparison(tasPath!, openStudioPath!);
            }
            catch (Exception exception)
            {
                //SAM_Validation rejects unreadable, invalid or schema-incompatible documents; say so rather than show anything.
                MessageBox.Show(this, "The documents could not be compared:\n\n" + exception.GetType().Name + ": " + exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            ClearResults();
            engineComparison = engineComparison_Loaded;
            this.engineComparisonRun = engineComparisonRun;

            TextBox_Tas.Text = tasPath;
            TextBox_OpenStudio.Text = openStudioPath;

            DataGrid_Model.ItemsSource = engineComparison.ModelRows;
            SetSpaces(engineComparison.SpaceRows);

            TextBox_Provenance.Text = Query.EngineComparisonProvenance(engineComparison);

            string diagnostics = Query.EngineComparisonDiagnostics(engineComparison, engineComparisonRun);
            string? manifestPath = Path.Combine(Path.GetDirectoryName(tasPath!)!, EngineComparisonRun.ManifestFileName);
            if (engineComparisonRun == null && File.Exists(manifestPath))
            {
                diagnostics += Environment.NewLine + Environment.NewLine + "Run record: " + manifestPath + " (producer exit codes and logs; the logs are benchmark-tas.log and benchmark-openstudio.log in the same folder).";
            }

            TextBox_Diagnostics.Text = diagnostics;

            RefreshBanner();

            Button_OpenReport.IsEnabled = true;
            Button_Export.IsEnabled = true;
            Button_OpenFolder.IsEnabled = true;

            return true;
        }

        private void RefreshBanner()
        {
            if (engineComparison == null)
            {
                return;
            }

            Benchmark.Compare.ComparisonResult comparisonResult = engineComparison.ComparisonResult;

            int notEquivalent = engineComparison.AllRows.Count(x => x.Status == EngineComparisonRowStatus.NotEquivalent);
            int missing = engineComparison.AllRows.Count(x => x.Status == EngineComparisonRowStatus.MissingOnTas || x.Status == EngineComparisonRowStatus.MissingOnOpenStudio);
            int unavailable = engineComparison.AllRows.Count(x => x.Status == EngineComparisonRowStatus.Unavailable);

            string text = string.Format(
                "Gate {0}: {1} match, {2} warn, {3} fail ({4} contributing to numerical status, {5} informational); {6} missing on one engine, {7} not equivalent, {8} not reported. Spaces: {9} matched, {10} only in Tas, {11} only in OpenStudio.",
                Benchmark.Compare.Format.Gate(comparisonResult.Gate), comparisonResult.MatchCount, comparisonResult.WarnCount, comparisonResult.FailCount,
                engineComparison.ContributingFailCount, engineComparison.InformationalFailCount, missing, notEquivalent, unavailable,
                comparisonResult.SpaceDiagnostics.MatchedByGuid + comparisonResult.SpaceDiagnostics.MatchedByName, comparisonResult.SpaceDiagnostics.OnlyInTas.Count, comparisonResult.SpaceDiagnostics.OnlyInOpenStudio.Count);

            if (!comparisonResult.ProvenanceCompatibility.IsCompatible)
            {
                //The provenance gate: the two runs were not made from the same model, weather or design days.
                text = "NOT THE SAME INPUTS - " + string.Join("; ", comparisonResult.ProvenanceCompatibility.Mismatches.Select(x => x.Field)) + " differ (see Provenance). " + text;
                Border_Banner.Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xD2, 0xD2));
            }
            else
            {
                Border_Banner.Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));
            }

            TextBlock_Banner.Text = text;
        }

        private void SetSpaces(IReadOnlyList<EngineComparisonRow>? rows)
        {
            spaceRows = rows;

            List<string> spaces = new List<string> { AllSpaces };
            if (rows != null)
            {
                spaces.AddRange(rows.Select(x => x.SpaceName ?? x.Scope).Distinct());
            }

            ComboBox_Space.ItemsSource = spaces;
            ComboBox_Space.SelectedIndex = 0;
            RefreshSpaces();
        }

        private void RefreshSpaces()
        {
            if (spaceRows == null)
            {
                DataGrid_Spaces.ItemsSource = null;
                return;
            }

            string? space = ComboBox_Space.SelectedItem as string;
            DataGrid_Spaces.ItemsSource = space == null || space == AllSpaces
                ? spaceRows
                : spaceRows.Where(x => (x.SpaceName ?? x.Scope) == space).ToList();
        }

        /// <summary>The GUID column shows a compact value; the grid context menu copies the selected row's complete identifier.</summary>
        private void CopyGuid_Click(object sender, RoutedEventArgs e)
        {
            EngineComparisonRow? row = (sender as FrameworkElement)?.DataContext as EngineComparisonRow
                ?? DataGrid_Spaces.SelectedItem as EngineComparisonRow;
            if (row != null && !string.IsNullOrWhiteSpace(row.ScopeFull))
            {
                Clipboard.SetText(row.ScopeFull);
            }
        }

        private void Browse(System.Windows.Controls.TextBox textBox, string title)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = title,
                Filter = "B1a benchmark documents (*.json)|*.json|All files (*.*)|*.*",
            };

            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(textBox.Text);
            }

            if (openFileDialog.ShowDialog(this) == true)
            {
                textBox.Text = openFileDialog.FileName;
            }
        }

        private void LoadFolder()
        {
            using System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new()
            {
                Description = "Select a comparison run folder (holding benchmark-TAS.json and benchmark-OpenStudio.json)",
                UseDescriptionForTitle = true,
            };

            if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            (string? tas, string? openStudio) = Query.EngineComparisonDocuments(folderBrowserDialog.SelectedPath);
            if (tas == null || openStudio == null)
            {
                //A Simulate > OpenStudio Simulation run folder (SAM_UI#229) has a result model but no B1a document; it is not comparable as it is.
                MessageBox.Show(this, string.Format("This folder has no {0}.\n\nA comparison needs both B1a documents. A Simulate > OpenStudio Simulation run folder has none: use Run Tas + OpenStudio, or choose documents written by benchmark-tas and benchmark-openstudio.",
                    tas == null && openStudio == null ? Query.TasBenchmarkFileName + " and " + Query.OpenStudioBenchmarkFileName : tas == null ? Query.TasBenchmarkFileName : Query.OpenStudioBenchmarkFileName), Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Compare(tas, openStudio, null);
        }

        private async Task RunAsync()
        {
            if (running || uIAnalyticalModel == null)
            {
                return;
            }

            running = true;
            Button_Run.IsEnabled = false;

            EngineComparisonRun? run;
            try
            {
                run = await uIAnalyticalModel.SimulateBothEnginesAsync(this);
            }
            finally
            {
                running = false;
                Button_Run.IsEnabled = uIAnalyticalModel != null;
            }

            if (run != null)
            {
                ShowRun(run);
            }
        }

        /// <summary>
        /// Shows a Run Simulation &gt; Both run: SAM_Validation's comparison when both engines completed; the completed engine's own
        /// results, marked not compared, when only one did; else the run's status and diagnostics. Nothing is filled in for an engine that
        /// did not complete.
        /// </summary>
        public void ShowRun(EngineComparisonRun engineComparisonRun)
        {
            if (engineComparisonRun == null)
            {
                return;
            }

            EngineComparisonProducerRun? tas = engineComparisonRun.ProducerRuns.Find(x => x.Engine == ComparisonEngine.Tas);
            EngineComparisonProducerRun? openStudio = engineComparisonRun.ProducerRuns.Find(x => x.Engine == ComparisonEngine.OpenStudio);

            if (engineComparisonRun.Status == EngineComparisonRunStatus.Compared && tas?.BenchmarkPath != null && openStudio?.BenchmarkPath != null)
            {
                Compare(tas.BenchmarkPath, openStudio.BenchmarkPath, engineComparisonRun);
                return;
            }

            if (engineComparisonRun.Status == EngineComparisonRunStatus.PartiallyCompleted)
            {
                EngineComparisonProducerRun? completed = tas != null && tas.Succeeded ? tas : openStudio != null && openStudio.Succeeded ? openStudio : null;
                if (completed != null && ShowEngineResult(completed, engineComparisonRun))
                {
                    return;
                }
            }

            ClearResults();
            this.engineComparisonRun = engineComparisonRun;
            TextBox_Diagnostics.Text = string.Join(Environment.NewLine, Query.EngineComparisonRunLines(engineComparisonRun)).TrimStart();
            TextBlock_Banner.Text = Query.EngineComparisonRunStatusText(engineComparisonRun.Status).ToUpperInvariant() + " - nothing to show or compare. See Diagnostics for each engine's exit code, error and log.";
            Border_Banner.Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xD2, 0xD2));
            Button_OpenFolder.IsEnabled = engineComparisonRun.Directory != null && Directory.Exists(engineComparisonRun.Directory);
        }

        /// <summary>One engine's results on their own, with a partial-completion banner; nothing is compared.</summary>
        private bool ShowEngineResult(EngineComparisonProducerRun completed, EngineComparisonRun engineComparisonRun)
        {
            ComparisonEngine notCompleted = completed.Engine == ComparisonEngine.Tas ? ComparisonEngine.OpenStudio : ComparisonEngine.Tas;

            EngineResult engineResult_Loaded;
            try
            {
                engineResult_Loaded = Query.LoadEngineResult(completed.Engine, completed.BenchmarkPath!, notCompleted);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "The " + Query.ComparisonEngineText(completed.Engine) + " results could not be read:\n\n" + exception.GetType().Name + ": " + exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            ClearResults();
            engineResult = engineResult_Loaded;
            this.engineComparisonRun = engineComparisonRun;

            TextBox_Tas.Text = completed.Engine == ComparisonEngine.Tas ? completed.BenchmarkPath : "(did not complete)";
            TextBox_OpenStudio.Text = completed.Engine == ComparisonEngine.OpenStudio ? completed.BenchmarkPath : "(did not complete)";

            DataGrid_Model.ItemsSource = engineResult.ModelRows;
            SetSpaces(engineResult.SpaceRows);

            TextBox_Provenance.Text = Query.EngineResultProvenance(engineResult);
            TextBox_Diagnostics.Text = Query.EngineResultDiagnostics(engineResult, engineComparisonRun);

            EngineComparisonProducerRun? failed = engineComparisonRun.ProducerRuns.Find(x => x.Engine == notCompleted);
            TextBlock_Banner.Text = string.Format("PARTIALLY COMPLETED - not compared: {0} did not complete{1}. Showing the {2} results only; no {0} result is substituted and nothing is banded. See Diagnostics.",
                Query.ComparisonEngineText(notCompleted), string.IsNullOrWhiteSpace(failed?.Error) ? string.Empty : " (" + failed!.Error + ")", Query.ComparisonEngineText(completed.Engine));
            Border_Banner.Background = new SolidColorBrush(Color.FromRgb(0xFC, 0xEB, 0xC7));

            Button_OpenFolder.IsEnabled = true;
            return true;
        }

        /// <summary>Empties the view before another run or comparison is shown in it.</summary>
        private void ClearResults()
        {
            engineComparison = null;
            engineResult = null;
            engineComparisonRun = null;

            DataGrid_Model.ItemsSource = null;
            SetSpaces(null);
            TextBox_Provenance.Text = string.Empty;
            TextBox_Diagnostics.Text = string.Empty;

            Button_OpenReport.IsEnabled = false;
            Button_Export.IsEnabled = false;
            Button_OpenFolder.IsEnabled = false;
        }

        private void OpenReport()
        {
            if (engineComparison == null)
            {
                return;
            }

            //The run folder's own reports when they are there for these documents; else freshly written ones in a temporary folder.
            string directory = Path.GetDirectoryName(engineComparison.TasPath)!;
            string markdownPath = Path.Combine(directory, Benchmark.Compare.Modify.MarkdownFileName);
            bool sameRun = Path.GetDirectoryName(engineComparison.OpenStudioPath) == directory && File.Exists(markdownPath);
            if (!sameRun)
            {
                directory = Path.Combine(Path.GetTempPath(), "SAM", "engine-comparison", Guid.NewGuid().ToString("N").Substring(0, 8));
                Benchmark.Compare.Modify.WriteReports(engineComparison.ComparisonResult, directory);
                markdownPath = Path.Combine(directory, Benchmark.Compare.Modify.MarkdownFileName);
            }

            ShellOpen(markdownPath);
        }

        private void Export()
        {
            if (engineComparison == null)
            {
                return;
            }

            using System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new()
            {
                Description = "Export comparison.md, comparison.csv and comparison-summary.json to",
                UseDescriptionForTitle = true,
            };

            if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            IReadOnlyList<string> paths = Benchmark.Compare.Modify.WriteReports(engineComparison.ComparisonResult, folderBrowserDialog.SelectedPath);
            MessageBox.Show(this, "Exported:\n" + string.Join(Environment.NewLine, paths), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFolder()
        {
            string? directory = engineComparison != null ? Path.GetDirectoryName(engineComparison.TasPath)
                : engineResult != null ? Path.GetDirectoryName(engineResult.Path)
                : engineComparisonRun?.Directory;

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                ShellOpen(directory!);
            }
        }

        private static void ShellOpen(string path)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
