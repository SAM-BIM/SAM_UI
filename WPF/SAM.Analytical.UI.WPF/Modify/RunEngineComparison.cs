// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.OpenStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Run Simulation &gt; Both (SAM_UI#231): runs both engines, one after the other, on ONE frozen snapshot of
        /// <paramref name="analyticalModel"/> and ONE weather file, then compares their B1a documents with SAM_Validation when both completed.
        /// <para>
        /// The engines run through their validated B1a producers, each in its own process: benchmark-tas (SAM_Tas, the native Tas route
        /// with that producer's fixed workflow settings - not the Energy Simulation dialog's) and benchmark-openstudio (SAM_OpenStudio, the
        /// same ToOpenStudio pipeline as Simulate &gt; OpenStudio Simulation). SAM_UI neither maps results to B1a nor compares anything
        /// itself: the documents are the producers', and the comparison and reports are SAM_Validation's (Query.Compare, WriteReports).
        /// </para>
        /// <para>
        /// The snapshot and weather file are made read-only once written, and their SHA-256 is checked again before each producer and
        /// before comparing: a changed input stops the run rather than compare results made from different inputs.
        /// </para>
        /// <para>
        /// A producer that fails does not stop the other one. Both completed: <see cref="EngineComparisonRunStatus.Compared"/>. One completed:
        /// <see cref="EngineComparisonRunStatus.PartiallyCompleted"/> - its document is kept and nothing is compared or substituted for the
        /// other engine. None: <see cref="EngineComparisonRunStatus.Failed"/>.
        /// </para>
        /// <para>
        /// The model given is never changed. Never throws for a failed run: the outcome, each producer's exit code and log, and any error
        /// are on the returned record, which is also written to the run folder (<see cref="EngineComparisonRun.ManifestFileName"/>).
        /// </para>
        /// </summary>
        /// <param name="analyticalModel">The model to simulate; read only.</param>
        /// <param name="directory">The folder the run folder is created in.</param>
        /// <param name="epwPath">An EPW chosen by the user; null to export the model's embedded WeatherData to EPW once, for both engines.</param>
        /// <param name="tasProducerPath">benchmark-tas.exe.</param>
        /// <param name="openStudioProducerPath">benchmark-openstudio.exe.</param>
        /// <param name="sourcePath">The open model's file, recorded for provenance only.</param>
        /// <param name="progress">One line per step.</param>
        /// <param name="cancellationToken">Cancels the run; the running producer's process tree is killed.</param>
        public static EngineComparisonRun RunEngineComparison(this AnalyticalModel analyticalModel, string directory, string? epwPath, string tasProducerPath, string openStudioProducerPath, string? sourcePath = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            return RunEngineComparison(analyticalModel, directory, epwPath, tasProducerPath, openStudioProducerPath, sourcePath, progress, cancellationToken, RunBenchmarkProducer);
        }

        /// <summary>The same run with the producer runner given, so the orchestration can be tested without either engine.</summary>
        internal static EngineComparisonRun RunEngineComparison(AnalyticalModel analyticalModel, string directory, string? epwPath, string tasProducerPath, string openStudioProducerPath, string? sourcePath, IProgress<string>? progress, CancellationToken cancellationToken, Func<EngineComparisonProducerRun, string, CancellationToken, int> runProducer)
        {
            EngineComparisonRun result = new EngineComparisonRun()
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                StartedUtc = DateTime.UtcNow,
                SourcePath = sourcePath,
                ModelName = analyticalModel?.Name,
            };

            if (analyticalModel == null || string.IsNullOrWhiteSpace(directory))
            {
                result.Diagnostics.Add("Error: no model or no output folder was given; nothing was run");
                result.FinishedUtc = DateTime.UtcNow;
                return result;
            }

            result.Directory = Query.EngineComparisonRunDirectory(directory, analyticalModel.Name, result.StartedUtc, result.Id!);

            try
            {
                System.IO.Directory.CreateDirectory(result.Directory);

                progress?.Report("Freezing the model");
                result.SnapshotPath = Path.Combine(result.Directory, OpenStudioSimulationRun.SnapshotFileName);
                AnalyticalModel? analyticalModel_Frozen = analyticalModel.FreezeAnalyticalModel(result.SnapshotPath);
                if (analyticalModel_Frozen == null)
                {
                    result.Diagnostics.Add("Error: the model could not be written to and read back from the snapshot file; nothing was run");
                    return result;
                }

                result.SnapshotSha256 = Query.Sha256(result.SnapshotPath);
                ProtectInput(result.SnapshotPath);

                //One weather file for both engines, so both documents carry the same weather hash.
                progress?.Report("Preparing the weather file");
                string? epwPath_User = !string.IsNullOrWhiteSpace(epwPath) && File.Exists(epwPath) ? epwPath : null;
                string? epwPath_Source = epwPath_User ?? EmbeddedWeatherPath(analyticalModel_Frozen);
                result.WeatherSource = epwPath_User != null ? "File" : epwPath_Source != null ? "Embedded" : "None";
                if (epwPath_Source == null)
                {
                    result.Diagnostics.Add("Error: no weather - choose an EPW file, or give the model annual WeatherData; nothing was run");
                    return result;
                }

                result.WeatherPath = Path.Combine(result.Directory, OpenStudioSimulationRun.WeatherFileName);
                File.Copy(epwPath_Source, result.WeatherPath, true);
                result.WeatherSha256 = Query.Sha256(result.WeatherPath);
                ProtectInput(result.WeatherPath);

                foreach ((ComparisonEngine engine, string producerPath) in new[] { (ComparisonEngine.Tas, tasProducerPath), (ComparisonEngine.OpenStudio, openStudioProducerPath) })
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(engine == ComparisonEngine.Tas ? "Running Tas (benchmark-tas)" : "Running OpenStudio/EnergyPlus (benchmark-openstudio)");

                    string workDirectory = Path.Combine(result.Directory, engine == ComparisonEngine.Tas ? "tas" : "openstudio");
                    System.IO.Directory.CreateDirectory(workDirectory);

                    string benchmarkPath = Path.Combine(result.Directory, engine == ComparisonEngine.Tas ? Query.TasBenchmarkFileName : Query.OpenStudioBenchmarkFileName);
                    EngineComparisonProducerRun producerRun = new EngineComparisonProducerRun()
                    {
                        Engine = engine,
                        ExecutablePath = producerPath,
                        LogPath = Path.Combine(result.Directory, engine == ComparisonEngine.Tas ? "benchmark-tas.log" : "benchmark-openstudio.log"),
                    };
                    producerRun.Arguments.AddRange(Query.BenchmarkProducerArguments(engine, result.SnapshotPath, result.WeatherPath, benchmarkPath, workDirectory, analyticalModel_Frozen.Name));
                    result.ProducerRuns.Add(producerRun);

                    if (string.IsNullOrWhiteSpace(producerPath) || !File.Exists(producerPath))
                    {
                        producerRun.Error = "The producer executable was not found: " + (producerPath ?? "(none)");
                        continue;
                    }

                    string? inputChange = InputChange(result);
                    if (inputChange != null)
                    {
                        producerRun.Error = "Not run: " + inputChange;
                        continue;
                    }

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        producerRun.ExitCode = runProducer(producerRun, workDirectory, cancellationToken);
                    }
                    catch (Exception exception) when (!(exception is OperationCanceledException))
                    {
                        producerRun.Error = exception.GetType().Name + ": " + exception.Message;
                    }
                    finally
                    {
                        producerRun.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
                    }

                    producerRun.BenchmarkPath = File.Exists(benchmarkPath) ? benchmarkPath : null;
                    if (producerRun.ExitCode != 0 && producerRun.Error == null)
                    {
                        producerRun.Error = string.Format("The producer exited with code {0} (2 usage, 3 input/IO, 4 validation, 5 producer failure); see {1}", producerRun.ExitCode, Path.GetFileName(producerRun.LogPath));
                    }

                    //S01b: why it failed, as the failed producer's own document says (for a refused Tas run, the TAS-SIM-001 warnings with the
                    //TAS return code and error log). Its results are still never used.
                    if (!producerRun.Succeeded && producerRun.BenchmarkPath != null)
                    {
                        result.Diagnostics.AddRange(Query.BenchmarkFailureLines(engine, producerRun.BenchmarkPath));
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                EngineComparisonProducerRun tas = result.ProducerRuns[0];
                EngineComparisonProducerRun openStudio = result.ProducerRuns[1];

                string? inputChange_Final = InputChange(result);
                if (inputChange_Final != null)
                {
                    //Which producer saw which input can no longer be told: keep the documents for diagnosis, show and compare none.
                    result.Diagnostics.Add("Error: no comparison - " + inputChange_Final + "; the documents are kept in the run folder but not used");
                    return result;
                }

                if (!tas.Succeeded || !openStudio.Succeeded)
                {
                    EngineComparisonProducerRun? completed = tas.Succeeded ? tas : openStudio.Succeeded ? openStudio : null;
                    if (completed == null)
                    {
                        result.Diagnostics.Add("Error: no comparison - neither Tas nor OpenStudio completed");
                        return result;
                    }

                    EngineComparisonProducerRun notCompleted = completed == tas ? openStudio : tas;
                    result.Status = EngineComparisonRunStatus.PartiallyCompleted;
                    result.Diagnostics.Add(string.Format("Partially completed: not compared - {0} did not complete. The {1} results ({2}) are kept and shown on their own; no {0} result is substituted.",
                        Query.ComparisonEngineText(notCompleted.Engine), Query.ComparisonEngineText(completed.Engine), Path.GetFileName(completed.BenchmarkPath)));
                    return result;
                }

                progress?.Report("Comparing (SAM_Validation)");
                EngineComparison engineComparison = Query.LoadEngineComparison(tas.BenchmarkPath!, openStudio.BenchmarkPath!);
                result.ReportPaths.AddRange(Benchmark.Compare.Modify.WriteReports(engineComparison.ComparisonResult, result.Directory));
                result.Gate = Benchmark.Compare.Format.Gate(engineComparison.ComparisonResult.Gate);

                //Compared is about the workflow, not the engines' agreement: that is SAM_Validation's gate.
                result.Status = EngineComparisonRunStatus.Compared;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.Status = EngineComparisonRunStatus.Cancelled;
            }
            catch (Exception exception)
            {
                result.Status = EngineComparisonRunStatus.Failed;
                result.Exception = exception;
            }
            finally
            {
                result.FinishedUtc = DateTime.UtcNow;
                WriteEngineComparisonManifest(result);
            }

            return result;
        }

        /// <summary>Remembers the producer the user located, for <see cref="Query.BenchmarkProducerPath"/>.</summary>
        public static bool SaveBenchmarkProducerPath(ComparisonEngine comparisonEngine, string path, string? settingsPath = null)
        {
            settingsPath ??= Query.BenchmarkProducerSettingsPath();
            try
            {
                JsonObject jsonObject = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? new JsonObject() : new JsonObject();
                jsonObject[comparisonEngine.ToString()] = path;

                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
                File.WriteAllText(settingsPath, jsonObject.ToJsonString());
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is System.Text.Json.JsonException)
            {
                return false;
            }
        }

        /// <summary>Makes a run input read-only, so neither engine nor the user changes it unnoticed; its hash is checked as well.</summary>
        private static void ProtectInput(string path)
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }

        /// <summary>Null when the snapshot and weather file still have the SHA-256 they were frozen with; else what changed.</summary>
        private static string? InputChange(EngineComparisonRun engineComparisonRun)
        {
            List<string> changes = new List<string>();
            foreach ((string name, string? path, string? sha256) in new[] { ("snapshot", engineComparisonRun.SnapshotPath, engineComparisonRun.SnapshotSha256), ("weather file", engineComparisonRun.WeatherPath, engineComparisonRun.WeatherSha256) })
            {
                string? sha256_Now = path != null && File.Exists(path) ? Query.Sha256(path) : null;
                if (sha256_Now == null || !string.Equals(sha256_Now, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add(string.Format("the {0} changed after it was frozen (SHA-256 {1} -> {2})", name, sha256 ?? "-", sha256_Now ?? "missing"));
                }
            }

            return changes.Count == 0 ? null : string.Join("; ", changes);
        }

        // Its own method, never inlined: SAM_OpenStudio's embedded-weather export is reached here, so a missing SAM_OpenStudio assembly
        // fails inside the caller's try and is recorded on the run.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string? EmbeddedWeatherPath(AnalyticalModel analyticalModel)
        {
            return analyticalModel.EmbeddedAnnualWeatherPath();
        }

        /// <summary>Runs a producer as a child process, its output to its log; cancellation kills the whole process tree.</summary>
        private static int RunBenchmarkProducer(EngineComparisonProducerRun producerRun, string workDirectory, CancellationToken cancellationToken)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(producerRun.ExecutablePath!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workDirectory,
            };
            producerRun.Arguments.ForEach(x => processStartInfo.ArgumentList.Add(x));

            using StreamWriter streamWriter = new StreamWriter(producerRun.LogPath!);
            object @lock = new object();

            using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("The producer process did not start");
            process.OutputDataReceived += (s, e) => { if (e.Data != null) { lock (@lock) { streamWriter.WriteLine(e.Data); } } };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) { lock (@lock) { streamWriter.WriteLine("[stderr] " + e.Data); } } };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.WaitForExit(250))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    process.Kill(true);
                    process.WaitForExit();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            //Drains the asynchronous output before the log is closed.
            process.WaitForExit();
            return process.ExitCode;
        }

        private static void WriteEngineComparisonManifest(EngineComparisonRun engineComparisonRun)
        {
            if (string.IsNullOrWhiteSpace(engineComparisonRun.Directory) || !System.IO.Directory.Exists(engineComparisonRun.Directory))
            {
                return;
            }

            string path = Path.Combine(engineComparisonRun.Directory!, EngineComparisonRun.ManifestFileName);
            try
            {
                File.WriteAllText(path, engineComparisonRun.ToJson());
                engineComparisonRun.ManifestPath = path;
            }
            catch (Exception exception)
            {
                //Written from a finally on a worker thread: whatever goes wrong here must not take the run, or the application, down.
                engineComparisonRun.Diagnostics.Add("Warning: the run manifest could not be written: " + exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
