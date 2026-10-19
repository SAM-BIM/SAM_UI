// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.OpenStudio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Runs an annual OpenStudio/EnergyPlus simulation of <paramref name="analyticalModel"/> through SAM_OpenStudio's own pipeline
        /// (<c>ToOpenStudio</c>: conversion, OSM/OSW, the openstudio.exe CLI, SQL results), into a new run folder under
        /// <paramref name="directory"/> (SAM_UI#229).
        /// <para>
        /// <b>The model given is never changed.</b> It is frozen first to <see cref="OpenStudioSimulationRun.SnapshotFileName"/>, and
        /// everything after works on the copy read back from that file - so the input of the run is exactly the snapshot's bytes, and
        /// edits made to the open model while the run is going cannot reach it.
        /// </para>
        /// <para>
        /// On success the copy, with every result it already carried removed (Tas results, say) and this run's OpenStudio results
        /// attached, is saved as the result model (<see cref="Query.OpenStudioResultModelFileName(string)"/>). The results are attached by
        /// SAM_OpenStudio's <c>AddResults</c> - the route its AddResultsBySQL component uses - plus the model-level
        /// <c>AnalyticalModelSimulationResult</c>; nothing is extracted or mapped here.
        /// </para>
        /// <para>
        /// Never throws for a failed run: the outcome, the diagnostics and any exception are on the returned record, which is also
        /// written to the run folder as <see cref="OpenStudioSimulationRun.ManifestFileName"/>. Safe to call off the UI thread.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel">The model to simulate; read only.</param>
        /// <param name="directory">The folder the run folder is created in.</param>
        /// <param name="epwPath">An EPW chosen by the user; null to use the model's embedded WeatherData (SAM_OpenStudio's own precedence).</param>
        /// <param name="sourcePath">The open model's file, recorded for provenance only.</param>
        /// <param name="progress">SAM_OpenStudio's stage progress (saving, CLI, reading results).</param>
        /// <param name="cancellationToken">Cancels the run; the CLI/EnergyPlus process tree is killed.</param>
        public static OpenStudioSimulationRun RunOpenStudioSimulation(this AnalyticalModel analyticalModel, string directory, string? epwPath = null, string? sourcePath = null, IProgress<Core.OpenStudio.OpenStudioSimulationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            OpenStudioSimulationRun result = new OpenStudioSimulationRun()
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                StartedUtc = DateTime.UtcNow,
                SourcePath = sourcePath,
                ModelName = analyticalModel?.Name,
                ModelGuid = analyticalModel?.Guid ?? Guid.Empty,
            };

            if (analyticalModel == null || string.IsNullOrWhiteSpace(directory))
            {
                result.Diagnostics.Add("Error: no model or no output folder was given; nothing was run");
                result.FinishedUtc = DateTime.UtcNow;
                return result;
            }

            result.Directory = Query.OpenStudioRunDirectory(directory, analyticalModel.Name, result.StartedUtc, result.Id!);

            try
            {
                System.IO.Directory.CreateDirectory(result.Directory);

                result.SnapshotPath = Path.Combine(result.Directory, OpenStudioSimulationRun.SnapshotFileName);
                AnalyticalModel? analyticalModel_Frozen = FreezeAnalyticalModel(analyticalModel, result.SnapshotPath);
                if (analyticalModel_Frozen == null)
                {
                    result.Diagnostics.Add("Error: the model could not be written to and read back from the snapshot file; nothing was run");
                    return result;
                }

                result.SnapshotSha256 = Query.Sha256(result.SnapshotPath);

                cancellationToken.ThrowIfCancellationRequested();

                RunOpenStudioSimulation_Run(result, analyticalModel_Frozen, analyticalModel, epwPath, progress, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.Status = OpenStudioSimulationStatus.Cancelled;
            }
            catch (Exception exception)
            {
                result.Status = OpenStudioSimulationStatus.Failed;
                result.Exception = exception;
            }
            finally
            {
                result.FinishedUtc = DateTime.UtcNow;
                WriteManifest(result);
            }

            return result;
        }

        /// <summary>
        /// Writes <paramref name="analyticalModel"/> to <paramref name="path"/> and returns the model read back from that file: an
        /// independent copy whose content is exactly the file's. Null when either step fails.
        /// <para>
        /// The file is <see cref="Query.SimulationSnapshot"/> of the model (S04): everything except SAM_UI's view state, which is
        /// regenerated on every open and would otherwise give the same model a different snapshot and canonical hash in each session.
        /// </para>
        /// </summary>
        public static AnalyticalModel? FreezeAnalyticalModel(this AnalyticalModel analyticalModel, string path)
        {
            if (analyticalModel == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (!Core.Convert.ToFile(Query.SimulationSnapshot(analyticalModel), path))
            {
                return null;
            }

            return Core.Convert.ToSAM<AnalyticalModel>(path)?.FirstOrDefault();
        }

        /// <summary>
        /// The result model of a successful run: <paramref name="analyticalModel_Frozen"/> with every result it carried removed, then this
        /// run's results attached from <paramref name="sqlPath"/> (SAM_OpenStudio <c>AddResults</c>) and the model-level result from
        /// <paramref name="openStudioSimulationResultSet"/>. Removing first keeps the file unambiguous: every result in it is OpenStudio's,
        /// from this run.
        /// </summary>
        public static AnalyticalModel AddOpenStudioResults(this AnalyticalModel analyticalModel_Frozen, string sqlPath, OpenStudioSimulationResultSet? openStudioSimulationResultSet, out int removedCount, out int addedCount, out List<string> diagnostics)
        {
            removedCount = 0;
            addedCount = 0;

            List<Core.Result>? results_Existing = analyticalModel_Frozen.GetResults<Core.Result>();
            if (results_Existing != null && results_Existing.Count != 0)
            {
                removedCount = analyticalModel_Frozen.Remove(results_Existing)?.Count ?? 0;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel_Frozen.AdjacencyCluster ?? new AdjacencyCluster();

            List<Core.Result>? results_Added = adjacencyCluster.AddResults(sqlPath, out diagnostics);
            addedCount = results_Added?.Count ?? 0;

            AnalyticalModel result = new AnalyticalModel(analyticalModel_Frozen, adjacencyCluster);

            AnalyticalModelSimulationResult? analyticalModelSimulationResult = openStudioSimulationResultSet?.ToSAM(result);
            if (analyticalModelSimulationResult != null && result.AddResult(analyticalModelSimulationResult))
            {
                addedCount++;
            }

            return result;
        }

        // Its own method, never inlined: every OpenStudio type is reached here, so a missing SAM_OpenStudio or OpenStudio assembly fails
        // this method's compilation inside the caller's try, and is recorded on the run like any other failure.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunOpenStudioSimulation_Run(OpenStudioSimulationRun openStudioSimulationRun, AnalyticalModel analyticalModel_Frozen, AnalyticalModel analyticalModel_Source, string? epwPath, IProgress<Core.OpenStudio.OpenStudioSimulationProgress>? progress, CancellationToken cancellationToken)
        {
            openStudioSimulationRun.SamOpenStudioVersion = typeof(OpenStudioConversionResult).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? typeof(OpenStudioConversionResult).Assembly.GetName().Version?.ToString();

            //The weather the converter will use, by its own precedence: the user's EPW, else the model's embedded WeatherData. The user's
            //path is passed on as given (null for embedded), so the conversion is exactly SAM_OpenStudio's; the resolved file is only
            //copied into the run folder, so the run keeps its weather when the original or the temporary export is gone.
            string? epwPath_User = !string.IsNullOrWhiteSpace(epwPath) && File.Exists(epwPath) ? epwPath : null;
            string? epwPath_Effective = epwPath_User ?? analyticalModel_Frozen.EmbeddedAnnualWeatherPath();
            openStudioSimulationRun.WeatherSource = epwPath_User != null ? "File" : epwPath_Effective != null ? "Embedded" : "None";
            if (epwPath_Effective != null)
            {
                openStudioSimulationRun.WeatherPath = Path.Combine(openStudioSimulationRun.Directory!, OpenStudioSimulationRun.WeatherFileName);
                File.Copy(epwPath_Effective, openStudioSimulationRun.WeatherPath, true);
                openStudioSimulationRun.WeatherSha256 = Query.Sha256(openStudioSimulationRun.WeatherPath);
            }

            Core.OpenStudio.OpenStudioRunOptions openStudioRunOptions = new Core.OpenStudio.OpenStudioRunOptions()
            {
                //The run folder is already this run's own, so the OSM, OSW and run\ go straight into it.
                UseUniqueRunDirectory = false,
            };

            OpenStudioProgress progress_Run = new OpenStudioProgress(openStudioSimulationRun, progress);

            using OpenStudioConversionResult openStudioConversionResult = analyticalModel_Frozen.ToOpenStudio(epwPath_User, openStudioSimulationRun.Directory, null, openStudioRunOptions, true, progress_Run, cancellationToken);

            if (openStudioConversionResult == null)
            {
                openStudioSimulationRun.Diagnostics.Add("Error: SAM_OpenStudio returned no conversion result");
                return;
            }

            foreach (Core.OpenStudio.OpenStudioDiagnostic openStudioDiagnostic in openStudioConversionResult.Diagnostics)
            {
                if (openStudioDiagnostic != null)
                {
                    openStudioSimulationRun.Diagnostics.Add(string.Format("{0} {1}: {2}", openStudioDiagnostic.Severity, openStudioDiagnostic.Code, openStudioDiagnostic.Message));
                }
            }

            openStudioSimulationRun.OsmPath = openStudioConversionResult.OsmPath;
            openStudioSimulationRun.OswPath = openStudioConversionResult.OswPath;

            Core.OpenStudio.OpenStudioRunResult? openStudioRunResult = openStudioConversionResult.RunResult;
            if (openStudioRunResult != null)
            {
                openStudioSimulationRun.ExitCode = openStudioRunResult.ExitCode;
                openStudioSimulationRun.SqlPath = File.Exists(openStudioRunResult.SqlPath) ? openStudioRunResult.SqlPath : null;
                openStudioSimulationRun.ErrorFilePath = File.Exists(openStudioRunResult.ErrorFilePath) ? openStudioRunResult.ErrorFilePath : null;
                openStudioSimulationRun.SevereCount = openStudioRunResult.SevereErrors?.Count ?? 0;
                openStudioSimulationRun.FatalCount = openStudioRunResult.FatalErrors?.Count ?? 0;
            }

            OpenStudioSimulationResultSet? openStudioSimulationResultSet = openStudioConversionResult.Results;
            if (openStudioSimulationResultSet != null)
            {
                openStudioSimulationRun.WarningCount = openStudioSimulationResultSet.WarningCount;
                openStudioSimulationRun.SevereCount = openStudioSimulationResultSet.SevereCount;
                openStudioSimulationRun.FatalCount = openStudioSimulationResultSet.FatalCount;
            }

            //SAM_OpenStudio reports a cancel as a warning, not as an exception; the token is the authority.
            if (cancellationToken.IsCancellationRequested)
            {
                openStudioSimulationRun.Status = OpenStudioSimulationStatus.Cancelled;
                return;
            }

            if (openStudioRunResult == null || !openStudioRunResult.Success || openStudioSimulationRun.SqlPath == null)
            {
                openStudioSimulationRun.Status = OpenStudioSimulationStatus.Failed;
                return;
            }

            progress?.Report(new Core.OpenStudio.OpenStudioSimulationProgress(Core.OpenStudio.OpenStudioSimulationStage.ReadingResults, "Attaching the results to the result model"));

            AnalyticalModel analyticalModel_Result = analyticalModel_Frozen.AddOpenStudioResults(openStudioSimulationRun.SqlPath, openStudioSimulationResultSet, out int removedCount, out int addedCount, out List<string> diagnostics);
            openStudioSimulationRun.RemovedResultCount = removedCount;
            openStudioSimulationRun.ResultCount = addedCount;
            if (diagnostics != null)
            {
                openStudioSimulationRun.ResultDiagnostics.AddRange(diagnostics);
            }

            //S04: the snapshot left the open model's view state out; the result model, which a person opens, gets it back.
            WithViewState(analyticalModel_Result, analyticalModel_Source);

            string resultModelPath = Path.Combine(openStudioSimulationRun.Directory!, Query.OpenStudioResultModelFileName(analyticalModel_Frozen.Name));
            if (!Core.Convert.ToFile(analyticalModel_Result, resultModelPath))
            {
                openStudioSimulationRun.Diagnostics.Add("Error: the result model could not be saved to " + resultModelPath);
                openStudioSimulationRun.Status = OpenStudioSimulationStatus.Failed;
                return;
            }

            openStudioSimulationRun.ResultModelPath = resultModelPath;
            openStudioSimulationRun.Status = OpenStudioSimulationStatus.Succeeded;
        }

        private static void WriteManifest(OpenStudioSimulationRun openStudioSimulationRun)
        {
            if (string.IsNullOrWhiteSpace(openStudioSimulationRun?.Directory) || !System.IO.Directory.Exists(openStudioSimulationRun!.Directory))
            {
                return;
            }

            string path = Path.Combine(openStudioSimulationRun.Directory!, OpenStudioSimulationRun.ManifestFileName);
            try
            {
                File.WriteAllText(path, openStudioSimulationRun.ToJson());
                openStudioSimulationRun.ManifestPath = path;
            }
            catch (Exception exception)
            {
                //Written from a finally on a worker thread: whatever goes wrong here must not take the run, or the application, down.
                openStudioSimulationRun.Diagnostics.Add("Warning: the run manifest could not be written: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>Passes SAM_OpenStudio's stage reports on, noting which openstudio.exe ran (the CLI stage's message is its path).</summary>
        private sealed class OpenStudioProgress : IProgress<Core.OpenStudio.OpenStudioSimulationProgress>
        {
            private readonly OpenStudioSimulationRun openStudioSimulationRun;
            private readonly IProgress<Core.OpenStudio.OpenStudioSimulationProgress>? progress;

            public OpenStudioProgress(OpenStudioSimulationRun openStudioSimulationRun, IProgress<Core.OpenStudio.OpenStudioSimulationProgress>? progress)
            {
                this.openStudioSimulationRun = openStudioSimulationRun;
                this.progress = progress;
            }

            public void Report(Core.OpenStudio.OpenStudioSimulationProgress value)
            {
                if (value == null)
                {
                    return;
                }

                if (value.Stage == Core.OpenStudio.OpenStudioSimulationStage.RunningCli)
                {
                    openStudioSimulationRun.CliPath = value.Message;
                }

                progress?.Report(value);
            }
        }
    }
}
