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
        /// <summary>
        /// Simulate &gt; OpenStudio (SAM_UI#229): asks for the output folder and the weather, then runs
        /// <see cref="RunOpenStudioSimulation(AnalyticalModel, string, string?, string?, IProgress{Core.OpenStudio.OpenStudioSimulationProgress}?, CancellationToken)"/>
        /// on a worker thread, with a progress window whose Cancel kills the OpenStudio/EnergyPlus process tree.
        /// <para>
        /// The UI is not blocked while the run goes, and <b>the open model is never changed</b>: the run works on a frozen copy and
        /// saves its own result model in its own run folder. The Tas workflow is not touched. Null when the user cancelled a dialog
        /// before the run started.
        /// </para>
        /// </summary>
        public static async Task<OpenStudioSimulationRun?> SimulateOpenStudioAsync(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Window? owner = null)
        {
            //The getter returns a deep clone, so later edits of the open model cannot reach this one.
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return null;
            }

            string? path = uIAnalyticalModel!.Path;

            string? directory = null;
            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new()
            {
                Description = "Select the folder for the OpenStudio run (a new run folder is created in it)",
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

            //SAM_OpenStudio's precedence: an EPW chosen here, else the model's embedded WeatherData.
            string? epwPath = null;
            bool embeddedWeather = analyticalModel.TryGetValue(AnalyticalModelParameter.WeatherData, out WeatherData? weatherData) && weatherData != null;
            if (embeddedWeather)
            {
                MessageBoxResult messageBoxResult = ShowOpenStudioMessage(owner, "Use the model's weather data for the OpenStudio run?\n\nYes: the model's weather data.\nNo: choose an EPW file.", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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
                    Title = "Select the EPW weather file for the OpenStudio run",
                    Filter = "epw files (*.epw)|*.epw|All files (*.*)|*.*",
                };

                bool? dialogResult = owner == null ? openFileDialog.ShowDialog() : openFileDialog.ShowDialog(owner);
                if (dialogResult != true || string.IsNullOrWhiteSpace(openFileDialog.FileName))
                {
                    return null;
                }

                epwPath = openFileDialog.FileName;
            }

            string directory_Run = directory;

            OpenStudioSimulationRun openStudioSimulationRun;
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                //The same host the Tas workflow uses: the dialog lives on its own thread, so Cancel always answers.
                ProgressWindowHost progressWindowHost = new ProgressWindowHost("OpenStudio Simulation", 5, true, "Cancel stops the OpenStudio/EnergyPlus run. The open model is not changed.");
                try
                {
                    progressWindowHost.CancelRequested += (s, e) => cancellationTokenSource.Cancel();
                    progressWindowHost.Update("Freezing and converting the model", false);

                    OpenStudioProgressReport progress = new OpenStudioProgressReport(progressWindowHost);
                    CancellationToken cancellationToken = cancellationTokenSource.Token;

                    openStudioSimulationRun = await Task.Run(() => analyticalModel.RunOpenStudioSimulation(directory_Run, epwPath, path, progress, cancellationToken));
                }
                finally
                {
                    progressWindowHost.Dispose();
                }
            }

            ShowOpenStudioSimulationRun(openStudioSimulationRun, owner);

            return openStudioSimulationRun;
        }

        private static void ShowOpenStudioSimulationRun(OpenStudioSimulationRun openStudioSimulationRun, System.Windows.Window? owner)
        {
            string text = Query.OpenStudioSimulationSummary(openStudioSimulationRun);

            if (openStudioSimulationRun.Exception != null && Query.IsOpenStudioLoadFailure(openStudioSimulationRun.Exception))
            {
                text = Query.OpenStudioLoadFailure(openStudioSimulationRun.Exception) + Environment.NewLine + Environment.NewLine + text;
            }

            MessageBoxImage messageBoxImage = openStudioSimulationRun.Status switch
            {
                OpenStudioSimulationStatus.Succeeded => MessageBoxImage.Information,
                OpenStudioSimulationStatus.Cancelled => MessageBoxImage.Warning,
                _ => MessageBoxImage.Error,
            };

            bool directoryExists = !string.IsNullOrWhiteSpace(openStudioSimulationRun.Directory) && Directory.Exists(openStudioSimulationRun.Directory);
            if (!directoryExists)
            {
                ShowOpenStudioMessage(owner, text, MessageBoxButton.OK, messageBoxImage);
                return;
            }

            if (ShowOpenStudioMessage(owner, text + Environment.NewLine + Environment.NewLine + "Open the run folder?", MessageBoxButton.YesNo, messageBoxImage) == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(openStudioSimulationRun.Directory!) { UseShellExecute = true });
            }
        }

        private static MessageBoxResult ShowOpenStudioMessage(System.Windows.Window? owner, string text, MessageBoxButton messageBoxButton, MessageBoxImage messageBoxImage)
        {
            //The window may have been closed while the run went on; a message box cannot be owned by a closed window.
            return owner != null && owner.IsLoaded
                ? MessageBox.Show(owner, text, "OpenStudio Simulation", messageBoxButton, messageBoxImage)
                : MessageBox.Show(text, "OpenStudio Simulation", messageBoxButton, messageBoxImage);
        }

        /// <summary>SAM_OpenStudio's stages as progress-window steps. ProgressWindowHost marshals onto its own thread, so any thread may report.</summary>
        private sealed class OpenStudioProgressReport : IProgress<Core.OpenStudio.OpenStudioSimulationProgress>
        {
            private readonly ProgressWindowHost progressWindowHost;

            public OpenStudioProgressReport(ProgressWindowHost progressWindowHost)
            {
                this.progressWindowHost = progressWindowHost;
            }

            public void Report(Core.OpenStudio.OpenStudioSimulationProgress value)
            {
                if (value == null)
                {
                    return;
                }

                string description = value.Stage switch
                {
                    Core.OpenStudio.OpenStudioSimulationStage.SavingOsm => "Saving the OpenStudio model (OSM)",
                    Core.OpenStudio.OpenStudioSimulationStage.WritingOsw => "Writing the OpenStudio workflow (OSW)",
                    Core.OpenStudio.OpenStudioSimulationStage.RunningCli => "Running OpenStudio/EnergyPlus",
                    Core.OpenStudio.OpenStudioSimulationStage.ReadingResults => "Reading the results",
                    _ => "Finishing",
                };

                progressWindowHost.Update(description);
            }
        }
    }
}
