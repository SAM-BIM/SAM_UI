// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The producer executable of an engine: benchmark-tas (SAM_Tas\build) or benchmark-openstudio (SAM_OpenStudio's benchmark
        /// project). These are the validated B1a producers; SAM_UI runs them rather than mapping results to B1a a second way.
        /// </summary>
        public static string BenchmarkProducerFileName(ComparisonEngine comparisonEngine)
        {
            return comparisonEngine == ComparisonEngine.Tas ? "benchmark-tas.exe" : "benchmark-openstudio.exe";
        }

        /// <summary>The environment variable that may name an engine's producer (the executable, or the folder holding it).</summary>
        public static string BenchmarkProducerVariable(ComparisonEngine comparisonEngine)
        {
            return comparisonEngine == ComparisonEngine.Tas ? "SAM_BENCHMARK_TAS" : "SAM_BENCHMARK_OPENSTUDIO";
        }

        /// <summary>Where the producer locations chosen by the user are remembered.</summary>
        public static string BenchmarkProducerSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SAM", "engine-comparison-producers.json");
        }

        /// <summary>
        /// The producer executable to run for <paramref name="comparisonEngine"/>, or null when none is found: the environment variable
        /// first, then the application folder, then the location the user chose before (<see cref="BenchmarkProducerSettingsPath"/>).
        /// </summary>
        public static string? BenchmarkProducerPath(ComparisonEngine comparisonEngine, string? settingsPath = null)
        {
            string fileName = BenchmarkProducerFileName(comparisonEngine);

            List<string?> candidates = new List<string?>
            {
                Environment.GetEnvironmentVariable(BenchmarkProducerVariable(comparisonEngine)),
                Path.Combine(AppContext.BaseDirectory, fileName),
                SavedBenchmarkProducerPath(comparisonEngine, settingsPath ?? BenchmarkProducerSettingsPath()),
            };

            foreach (string? candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string path = Directory.Exists(candidate) ? Path.Combine(candidate!, fileName) : candidate!;
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// The command line of a producer run: the frozen snapshot and one weather file for both engines, so both B1a documents carry the
        /// same model and weather provenance. Tas writes its TBD (and gbXML) under <paramref name="workDirectory"/>; OpenStudio runs there.
        /// </summary>
        public static List<string> BenchmarkProducerArguments(ComparisonEngine comparisonEngine, string snapshotPath, string weatherPath, string benchmarkPath, string workDirectory, string? modelName)
        {
            List<string> result = new List<string> { "--model", snapshotPath, "--weather", weatherPath };

            if (comparisonEngine == ComparisonEngine.Tas)
            {
                result.Add("--tbd");
                result.Add(Path.Combine(workDirectory, FileNameSafe(modelName) + ".tbd"));
            }

            result.Add("--out");
            result.Add(benchmarkPath);

            if (comparisonEngine == ComparisonEngine.OpenStudio)
            {
                result.Add("--work");
                result.Add(workDirectory);
            }

            return result;
        }

        private static string? SavedBenchmarkProducerPath(ComparisonEngine comparisonEngine, string settingsPath)
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return null;
                }

                return JsonNode.Parse(File.ReadAllText(settingsPath))?[comparisonEngine.ToString()]?.GetValue<string>();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is System.Text.Json.JsonException || exception is InvalidOperationException || exception is FormatException)
            {
                return null;
            }
        }
    }
}
