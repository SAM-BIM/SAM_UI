// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Windows.WPF;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Simulate &gt; Compare Tas/OpenStudio (SAM_UI#231, Phase 1): choose a Tas and an OpenStudio B1a document - or a comparison run
    /// folder - or run both engines on a frozen copy of the open model, and see SAM_Validation's comparison of the two: model and
    /// per-space metrics with units, differences and bands, which metrics are missing, not equivalent or not reported, the provenance,
    /// the diagnostics and the unmatched spaces; open or export the Markdown/CSV/JSON reports.
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
                : "Freeze the open model, run benchmark-tas and benchmark-openstudio on the snapshot one after the other with one weather file, and compare. Needs a licensed Tas and OpenStudio 3.10. The open model is not changed.";

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

            engineComparison = engineComparison_Loaded;
            this.engineComparisonRun = engineComparisonRun;

            TextBox_Tas.Text = tasPath;
            TextBox_OpenStudio.Text = openStudioPath;

            DataGrid_Model.ItemsSource = engineComparison.ModelRows;

            List<string> spaces = new List<string> { AllSpaces };
            spaces.AddRange(engineComparison.SpaceRows.Select(x => x.SpaceName ?? x.Scope).Distinct());
            ComboBox_Space.ItemsSource = spaces;
            ComboBox_Space.SelectedIndex = 0;
            RefreshSpaces();

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

            int notEquivalent = engineComparison.ModelRows.Concat(engineComparison.SpaceRows).Count(x => x.Status == EngineComparisonRowStatus.NotEquivalent);
            int missing = engineComparison.ModelRows.Concat(engineComparison.SpaceRows).Count(x => x.Status == EngineComparisonRowStatus.MissingOnTas || x.Status == EngineComparisonRowStatus.MissingOnOpenStudio);
            int unavailable = engineComparison.ModelRows.Concat(engineComparison.SpaceRows).Count(x => x.Status == EngineComparisonRowStatus.Unavailable);

            string text = string.Format(
                "Gate {0}: {1} match, {2} warn, {3} fail; {4} missing on one engine, {5} not equivalent, {6} not reported. Spaces: {7} matched, {8} only in Tas, {9} only in OpenStudio.",
                Benchmark.Compare.Format.Gate(comparisonResult.Gate), comparisonResult.MatchCount, comparisonResult.WarnCount, comparisonResult.FailCount, missing, notEquivalent, unavailable,
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

        private void RefreshSpaces()
        {
            if (engineComparison == null)
            {
                DataGrid_Spaces.ItemsSource = null;
                return;
            }

            string? space = ComboBox_Space.SelectedItem as string;
            DataGrid_Spaces.ItemsSource = space == null || space == AllSpaces
                ? engineComparison.SpaceRows
                : engineComparison.SpaceRows.Where(x => (x.SpaceName ?? x.Scope) == space).ToList();
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
            if (running)
            {
                return;
            }

            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                MessageBox.Show(this, "Open a model first: Run Tas + OpenStudio simulates the open model.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? tasProducerPath = LocateProducer(ComparisonEngine.Tas);
            if (tasProducerPath == null)
            {
                return;
            }

            string? openStudioProducerPath = LocateProducer(ComparisonEngine.OpenStudio);
            if (openStudioProducerPath == null)
            {
                return;
            }

            string? path = uIAnalyticalModel!.Path;
            string directory;
            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new()
            {
                Description = "Select the folder for the Tas/OpenStudio comparison run (a new run folder is created in it)",
                UseDescriptionForTitle = true,
                SelectedPath = (string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path)) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            })
            {
                if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    return;
                }

                directory = folderBrowserDialog.SelectedPath;
            }

            //One weather file for both engines: the model's WeatherData exported once, or an EPW chosen here.
            string? epwPath = null;
            bool embeddedWeather = analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out WeatherData? weatherData) && weatherData != null;
            if (embeddedWeather)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(this, "Use the model's weather data for both engines?\n\nYes: the model's weather data (exported once to EPW).\nNo: choose an EPW file.", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (messageBoxResult == MessageBoxResult.Cancel)
                {
                    return;
                }

                embeddedWeather = messageBoxResult == MessageBoxResult.Yes;
            }

            if (!embeddedWeather)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
                {
                    Title = "Select the EPW weather file for both engines",
                    Filter = "epw files (*.epw)|*.epw|All files (*.*)|*.*",
                };

                if (openFileDialog.ShowDialog(this) != true)
                {
                    return;
                }

                epwPath = openFileDialog.FileName;
            }

            running = true;
            Button_Run.IsEnabled = false;

            EngineComparisonRun run;
            try
            {
                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                ProgressWindowHost progressWindowHost = new ProgressWindowHost("Compare Tas / OpenStudio", 6, true, "Cancel stops the running engine (its process tree is killed). The open model is not changed.");
                try
                {
                    progressWindowHost.CancelRequested += (s, e) => cancellationTokenSource.Cancel();

                    Progress progress = new Progress(progressWindowHost);
                    CancellationToken cancellationToken = cancellationTokenSource.Token;

                    run = await Task.Run(() => analyticalModel.RunEngineComparison(directory, epwPath, tasProducerPath, openStudioProducerPath, path, progress, cancellationToken));
                }
                finally
                {
                    progressWindowHost.Dispose();
                }
            }
            finally
            {
                running = false;
                Button_Run.IsEnabled = uIAnalyticalModel != null;
            }

            List<string> lines = new List<string> { "Comparison run: " + run.Status, "Run folder: " + (run.Directory ?? "-") };
            if (run.Gate != null)
            {
                lines.Add("SAM_Validation gate: " + run.Gate);
            }

            lines.AddRange(run.ProducerRuns.Where(x => x.Error != null).Select(x => x.Engine + ": " + x.Error));
            lines.AddRange(run.Diagnostics);
            if (run.Exception != null)
            {
                lines.Add("Error: " + run.Exception.GetType().Name + ": " + run.Exception.Message);
            }

            MessageBox.Show(this, string.Join(Environment.NewLine, lines), Title, MessageBoxButton.OK, run.Status == EngineComparisonRunStatus.Compared ? MessageBoxImage.Information : MessageBoxImage.Warning);

            EngineComparisonProducerRun? tas = run.ProducerRuns.FirstOrDefault(x => x.Engine == ComparisonEngine.Tas);
            EngineComparisonProducerRun? openStudio = run.ProducerRuns.FirstOrDefault(x => x.Engine == ComparisonEngine.OpenStudio);
            if (tas?.BenchmarkPath != null && openStudio?.BenchmarkPath != null)
            {
                Compare(tas.BenchmarkPath, openStudio.BenchmarkPath, run);
            }
            else if (run.Directory != null)
            {
                TextBox_Diagnostics.Text = string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine + "Run record: " + Path.Combine(run.Directory, EngineComparisonRun.ManifestFileName);
            }
        }

        /// <summary>The producer to run, asking the user to locate it (once; remembered) when it is not found.</summary>
        private string? LocateProducer(ComparisonEngine comparisonEngine)
        {
            string? path = Query.BenchmarkProducerPath(comparisonEngine);
            if (path != null)
            {
                return path;
            }

            string fileName = Query.BenchmarkProducerFileName(comparisonEngine);
            string hint = comparisonEngine == ComparisonEngine.Tas
                ? "It is built by SAM_Tas into SAM_Tas\\build (needs Visual Studio / .NET Framework MSBuild for the Tas COM references)."
                : "It is built by SAM_OpenStudio (benchmark\\SAM.Analytical.OpenStudio.Benchmark\\bin\\x64\\Release\\...).";

            if (MessageBox.Show(this, string.Format("{0} was not found (environment variable {1}, the application folder, or a location chosen before).\n\n{2}\n\nLocate it now?", fileName, Query.BenchmarkProducerVariable(comparisonEngine), hint), Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return null;
            }

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Locate " + fileName,
                Filter = fileName + "|" + fileName + "|Executables (*.exe)|*.exe",
            };

            if (openFileDialog.ShowDialog(this) != true)
            {
                return null;
            }

            Modify.SaveBenchmarkProducerPath(comparisonEngine, openFileDialog.FileName);
            return openFileDialog.FileName;
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
            if (engineComparison != null)
            {
                ShellOpen(Path.GetDirectoryName(engineComparison.TasPath)!);
            }
        }

        private static void ShellOpen(string path)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }

        /// <summary>Step lines into the progress window; it marshals onto its own thread, so any thread may report.</summary>
        private sealed class Progress : IProgress<string>
        {
            private readonly ProgressWindowHost progressWindowHost;

            public Progress(ProgressWindowHost progressWindowHost)
            {
                this.progressWindowHost = progressWindowHost;
            }

            public void Report(string value)
            {
                progressWindowHost.Update(value);
            }
        }
    }
}
