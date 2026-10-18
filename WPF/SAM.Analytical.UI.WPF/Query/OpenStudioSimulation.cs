// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The folder of one Simulate &gt; OpenStudio run: &lt;directory&gt;\&lt;model&gt;_OpenStudio_&lt;yyyyMMdd-HHmmss&gt;_&lt;id&gt;. The
        /// timestamp makes runs sort in order and the id makes two runs started in the same second distinct, so a run never
        /// writes into another run's folder.
        /// </summary>
        public static string OpenStudioRunDirectory(string directory, string? modelName, DateTime startedUtc, string id)
        {
            return Path.Combine(directory, string.Format("{0}_OpenStudio_{1:yyyyMMdd-HHmmss}_{2}", FileNameSafe(modelName), startedUtc, id));
        }

        /// <summary>The result model's file name: the model name with an _OpenStudio suffix, so it is never mistaken for the source model.</summary>
        public static string OpenStudioResultModelFileName(string? modelName)
        {
            return FileNameSafe(modelName) + "_OpenStudio.json";
        }

        /// <summary>"sha256:" and the lowercase hex SHA-256 of the file's bytes (the B1a hash notation); null when there is no file.</summary>
        public static string? Sha256(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using FileStream fileStream = File.OpenRead(path!);
            return "sha256:" + System.Convert.ToHexString(SHA256.HashData(fileStream)).ToLowerInvariant();
        }

        /// <summary>
        /// True when <paramref name="exception"/> (or an inner one) means the OpenStudio stack could not be loaded: a missing or
        /// stale SAM_OpenStudio assembly, OpenStudio.dll, or one of the x64 native libraries (openstudiolib.dll, openstudio_*csharp.dll,
        /// SQLite.Interop.dll) next to the application. That is a deployment problem, not a problem with the model.
        /// </summary>
        public static bool IsOpenStudioLoadFailure(Exception? exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is DllNotFoundException || current is EntryPointNotFoundException || current is BadImageFormatException || current is TypeLoadException || current is MissingMemberException || current is FileLoadException)
                {
                    return true;
                }

                if (current is FileNotFoundException fileNotFoundException && IsAssemblyName(fileNotFoundException.FileName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What to tell the user about an OpenStudio load failure. It references no OpenStudio type, so it works when those cannot be loaded.</summary>
        public static string OpenStudioLoadFailure(Exception? exception)
        {
            List<string> lines = new List<string>
            {
                "Simulate > OpenStudio could not load the OpenStudio components. It needs SAM.Analytical.OpenStudio.dll, SAM.Core.OpenStudio.dll, SAM.Geometry.OpenStudio.dll, OpenStudio.dll and the 64-bit native openstudiolib.dll, openstudio_csharp.dll, openstudio_model_csharp.dll and openstudio_translators_csharp.dll beside the application (from the SAM_OpenStudio x64 build). This is a deployment problem, not a problem with the model; the open model was not changed.",
                string.Empty,
                "Error: " + Innermost(exception),
                "Application folder: " + AppContext.BaseDirectory,
            };

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// The user-facing account of one Simulate &gt; OpenStudio run: how it ended, where its outputs are, the EnergyPlus error counts
        /// and the first diagnostics that matter (errors before warnings). Engine results are reported as they are; nothing here
        /// compares them with Tas.
        /// </summary>
        public static string OpenStudioSimulationSummary(OpenStudioSimulationRun? openStudioSimulationRun, int maxDiagnostics = 8)
        {
            if (openStudioSimulationRun == null)
            {
                return "The OpenStudio simulation did not run.";
            }

            List<string> lines = new List<string>();

            switch (openStudioSimulationRun.Status)
            {
                case OpenStudioSimulationStatus.Succeeded:
                    lines.Add("The OpenStudio/EnergyPlus simulation finished. The open model was not changed.");
                    break;
                case OpenStudioSimulationStatus.Cancelled:
                    lines.Add("The OpenStudio simulation was cancelled. No result model was saved and the open model was not changed.");
                    break;
                default:
                    lines.Add("The OpenStudio simulation failed. No result model was saved and the open model was not changed.");
                    break;
            }

            lines.Add(string.Empty);

            if (!string.IsNullOrWhiteSpace(openStudioSimulationRun.Directory))
            {
                lines.Add("Run folder: " + openStudioSimulationRun.Directory);
            }

            if (!string.IsNullOrWhiteSpace(openStudioSimulationRun.ResultModelPath))
            {
                lines.Add(string.Format("Result model: {0} ({1} results)", Path.GetFileName(openStudioSimulationRun.ResultModelPath), openStudioSimulationRun.ResultCount));
            }

            if (!string.IsNullOrWhiteSpace(openStudioSimulationRun.WeatherSource))
            {
                lines.Add("Weather: " + openStudioSimulationRun.WeatherSource);
            }

            if (openStudioSimulationRun.ExitCode != null)
            {
                lines.Add(string.Format("EnergyPlus: exit code {0}, {1} warnings, {2} severe, {3} fatal", openStudioSimulationRun.ExitCode, openStudioSimulationRun.WarningCount, openStudioSimulationRun.SevereCount, openStudioSimulationRun.FatalCount));
            }

            if (openStudioSimulationRun.Exception != null)
            {
                lines.Add("Error: " + openStudioSimulationRun.Exception.GetType().Name + ": " + openStudioSimulationRun.Exception.Message);
            }

            List<string> diagnostics = openStudioSimulationRun.Diagnostics.Where(x => x.StartsWith("Error", StringComparison.Ordinal))
                .Concat(openStudioSimulationRun.Diagnostics.Where(x => x.StartsWith("Warning", StringComparison.Ordinal)))
                .ToList();

            if (diagnostics.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("Diagnostics:");
                lines.AddRange(diagnostics.Take(maxDiagnostics).Select(x => "- " + x));
                if (diagnostics.Count > maxDiagnostics)
                {
                    lines.Add(string.Format("- ... and {0} more (see {1})", diagnostics.Count - maxDiagnostics, OpenStudioSimulationRun.ManifestFileName));
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FileNameSafe(string? name)
        {
            string result = string.IsNullOrWhiteSpace(name) ? "Model" : name!.Trim();

            //Windows' invalid characters whatever the platform, so a run folder copied to Windows keeps its name.
            HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*"));
            result = new string(result.Select(x => invalidChars.Contains(x) || char.IsWhiteSpace(x) || char.IsControl(x) ? '_' : x).ToArray());

            //Long names would push run outputs (run\eplusout.sql and the EnergyPlus files) towards MAX_PATH.
            return result.Length > 64 ? result.Substring(0, 64) : result;
        }
    }
}
