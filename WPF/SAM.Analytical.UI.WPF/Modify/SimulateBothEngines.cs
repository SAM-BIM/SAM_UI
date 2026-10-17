// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Windows.WPF;
using SAM.Weather;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        private const string SimulateBothEnginesTitle = "Run Simulation - Tas + OpenStudio";

        /// <summary>
        /// Run Simulation &gt; Both (SAM_UI#231): locates the two B1a producers, asks for the output folder and the weather, then runs
        /// <see cref="RunEngineComparison(AnalyticalModel, string, string?, string, string, string?, IProgress{string}?, CancellationToken)"/>
        /// on a worker thread, with a progress window whose Cancel kills the running producer's process tree.
        /// <para>
        /// The engines run one after the other on one frozen snapshot and one weather file; the open model is never changed. Null when
        /// the user cancelled a dialog before the run started. The caller shows the returned run (<see cref="EngineComparisonWindow.ShowRun"/>).
        /// </para>
        /// </summary>
        public static async Task<EngineComparisonRun?> SimulateBothEnginesAsync(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Window? owner = null)
        {
            //The getter returns a deep clone, so later edits of the open model cannot reach this one.
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return null;
            }

            string? tasProducerPath = LocateBenchmarkProducer(ComparisonEngine.Tas, owner);
            if (tasProducerPath == null)
            {
                return null;
            }

            string? openStudioProducerPath = LocateBenchmarkProducer(ComparisonEngine.OpenStudio, owner);
            if (openStudioProducerPath == null)
            {
                return null;
            }

            string? path = uIAnalyticalModel!.Path;
            string directory;
            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new()
            {
                Description = "Select the folder for the Tas + OpenStudio run (a new run folder is created in it)",
                UseDescriptionForTitle = true,
                SelectedPath = (string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path)) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            })
            {
                if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    return null;
                }

                directory = folderBrowserDialog.SelectedPath;
            }

            //One weather file for both engines: the model's WeatherData exported once, or an EPW chosen here.
            string? epwPath = null;
            bool embeddedWeather = analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out WeatherData? weatherData) && weatherData != null;
            if (embeddedWeather)
            {
                MessageBoxResult messageBoxResult = ShowBothEnginesMessage(owner, "Use the model's weather data for both engines?\n\nYes: the model's weather data (exported once to EPW).\nNo: choose an EPW file.", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (messageBoxResult == MessageBoxResult.Cancel)
                {
                    return null;
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

                bool? dialogResult = owner == null ? openFileDialog.ShowDialog() : openFileDialog.ShowDialog(owner);
                if (dialogResult != true || string.IsNullOrWhiteSpace(openFileDialog.FileName))
                {
                    return null;
                }

                epwPath = openFileDialog.FileName;
            }

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            ProgressWindowHost progressWindowHost = new ProgressWindowHost(SimulateBothEnginesTitle, 6, true, "Tas runs first, then OpenStudio/EnergyPlus. Cancel stops the running engine (its process tree is killed). The open model is not changed.");
            try
            {
                progressWindowHost.CancelRequested += (s, e) => cancellationTokenSource.Cancel();

                EngineComparisonProgressReport progress = new EngineComparisonProgressReport(progressWindowHost);
                CancellationToken cancellationToken = cancellationTokenSource.Token;

                return await Task.Run(() => analyticalModel.RunEngineComparison(directory, epwPath, tasProducerPath, openStudioProducerPath, path, progress, cancellationToken));
            }
            finally
            {
                progressWindowHost.Dispose();
            }
        }

        /// <summary>The producer to run, asking the user to locate it (once; remembered) when it is not found. Null when the user declined.</summary>
        public static string? LocateBenchmarkProducer(ComparisonEngine comparisonEngine, System.Windows.Window? owner = null)
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

            string text = string.Format("{0} was not found (environment variable {1}, the application folder, or a location chosen before).\n\n{2}\n\nLocate it now?", fileName, Query.BenchmarkProducerVariable(comparisonEngine), hint);
            if (ShowBothEnginesMessage(owner, text, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return null;
            }

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Locate " + fileName,
                Filter = fileName + "|" + fileName + "|Executables (*.exe)|*.exe",
            };

            bool? dialogResult = owner == null ? openFileDialog.ShowDialog() : openFileDialog.ShowDialog(owner);
            if (dialogResult != true)
            {
                return null;
            }

            SaveBenchmarkProducerPath(comparisonEngine, openFileDialog.FileName);
            return openFileDialog.FileName;
        }

        private static MessageBoxResult ShowBothEnginesMessage(System.Windows.Window? owner, string text, MessageBoxButton messageBoxButton, MessageBoxImage messageBoxImage)
        {
            return owner == null
                ? MessageBox.Show(text, SimulateBothEnginesTitle, messageBoxButton, messageBoxImage)
                : MessageBox.Show(owner, text, SimulateBothEnginesTitle, messageBoxButton, messageBoxImage);
        }

        /// <summary>Step lines into the progress window; it marshals onto its own thread, so any thread may report.</summary>
        private sealed class EngineComparisonProgressReport : IProgress<string>
        {
            private readonly ProgressWindowHost progressWindowHost;

            public EngineComparisonProgressReport(ProgressWindowHost progressWindowHost)
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
