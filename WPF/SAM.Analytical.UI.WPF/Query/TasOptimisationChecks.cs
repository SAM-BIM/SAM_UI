// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The readiness list of Simulate &gt; Optimisation, in the order the questions arise: the Tas project folder,
        /// the script, TasGenExecute, the optimisation settings, then the non-blocking name check. Nothing is created or
        /// started. The settings are judged by SAM_Tas (<see cref="TasOptimisationDefinition.Validate"/>), and its message
        /// is shown as it is.
        /// </summary>
        /// <param name="tasOptimisationInput">The form.</param>
        /// <param name="tasGenExecutePath">TasGenExecute.exe; null for the installed one, which RunNative uses.</param>
        public static List<TasOptimisationCheck> TasOptimisationChecks(this TasOptimisationInput tasOptimisationInput, string? tasGenExecutePath = null)
        {
            List<TasOptimisationCheck> result = new List<TasOptimisationCheck>();
            if (tasOptimisationInput == null)
            {
                return result;
            }

            result.Add(TasOptimisationCheck_Directory(tasOptimisationInput));

            TasOptimisationCheck tasOptimisationCheck_Script = TasOptimisationCheck_Script(tasOptimisationInput.ScriptPath, out string? scriptText);
            result.Add(tasOptimisationCheck_Script);

            string path_TasGenExecute = string.IsNullOrWhiteSpace(tasGenExecutePath) ? Analytical.Tas.GenOpt.Query.TasGenOptExecutePath() : tasGenExecutePath!;
            result.Add(System.IO.File.Exists(path_TasGenExecute)
                ? new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "TasGenExecute", path_TasGenExecute)
                : new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "TasGenExecute", "TasGenExecute.exe was not found at '" + path_TasGenExecute + "'. It is installed with Tas (TasGenOpt)."));

            TasOptimisationDefinition? tasOptimisationDefinition = null;
            if (!tasOptimisationInput.TryGetDefinition(out tasOptimisationDefinition, out List<string> problems))
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Optimisation settings", string.Join(Environment.NewLine, problems)));
                tasOptimisationDefinition = null;
            }
            else
            {
                try
                {
                    tasOptimisationDefinition!.Validate();
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Optimisation settings", Summary(tasOptimisationDefinition)));
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException)
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Optimisation settings", TasOptimisationReport.Message(exception)));
                }
            }

            if (scriptText != null)
            {
                result.AddRange(TasOptimisationChecks_Names(tasOptimisationInput, scriptText));
            }

            return result;
        }

        /// <summary>True when no check blocks the run.</summary>
        public static bool CanRun(this IEnumerable<TasOptimisationCheck> tasOptimisationChecks)
        {
            return tasOptimisationChecks != null && tasOptimisationChecks.All(x => x.Status != TasOptimisationCheckStatus.Blocked);
        }

        /// <summary>The Tas files TasGenExecute would receive from <paramref name="directory"/>: its top-level files of SAM_Tas' Tas types.</summary>
        public static List<string> TasOptimisationTasFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return new List<string>();
            }

            return Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly).Where(NativeGenOptWorkspace.IsTasFile).Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()!;
        }

        /// <summary>Where the run folder will be created: the runs folder if one is given, else SAM_Tas' default.</summary>
        public static string? TasOptimisationRunsDirectory(this TasOptimisationInput tasOptimisationInput)
        {
            if (tasOptimisationInput == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(tasOptimisationInput.RunsDirectory))
            {
                return tasOptimisationInput.RunsDirectory.Trim();
            }

            return string.IsNullOrWhiteSpace(tasOptimisationInput.Directory) ? null : Path.Combine(tasOptimisationInput.Directory.Trim(), "SAM_NativeGenOpt");
        }

        private static TasOptimisationCheck TasOptimisationCheck_Directory(TasOptimisationInput tasOptimisationInput)
        {
            const string title = "Tas project folder";

            string directory = tasOptimisationInput.Directory?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Choose the folder that holds the Tas files the script works on.");
            }

            if (!Directory.Exists(directory))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The folder does not exist: '" + directory + "'.");
            }

            List<string> names = TasOptimisationTasFiles(directory);
            if (names.Count == 0)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The folder holds no Tas file (" + string.Join(", ", NativeGenOptWorkspace.TasFileExtensions) + ") at its top level, so TasGenExecute would receive none.");
            }

            return new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, title, string.Join(", ", names) + ". Each run copies these into its own folder under " + tasOptimisationInput.TasOptimisationRunsDirectory() + "; the originals are not changed.");
        }

        private static TasOptimisationCheck TasOptimisationCheck_Script(string scriptPath, out string? scriptText)
        {
            const string title = "Script";

            scriptText = null;

            string path = scriptPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Choose the TasGenExecute script (C#) to run for each evaluation.");
            }

            if (!System.IO.File.Exists(path))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The script does not exist: '" + path + "'.");
            }

            try
            {
                scriptText = System.IO.File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The script cannot be read: " + exception.Message);
            }

            if (string.IsNullOrWhiteSpace(scriptText))
            {
                scriptText = null;
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The script is empty.");
            }

            return new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, title, Path.GetFileName(path));
        }

        /// <summary>
        /// A name the script never writes as a quoted literal: probably a typing mistake, which would make every evaluation
        /// fail. A warning only, as a script may build a name at run time.
        /// </summary>
        private static List<TasOptimisationCheck> TasOptimisationChecks_Names(TasOptimisationInput tasOptimisationInput, string scriptText)
        {
            List<TasOptimisationCheck> result = new List<TasOptimisationCheck>();

            foreach (string name in tasOptimisationInput.Parameters.Select(x => x?.Name?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!)
            {
                if (!Quoted(scriptText, name))
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Parameter '" + name + "'", string.Format(CultureInfo.InvariantCulture, "The script never mentions \"{0}\". It reads each parameter as Variables[\"{0}\"]; check the name.", name)));
                }
            }

            foreach (string name in tasOptimisationInput.Objectives.Select(x => x?.Name?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!)
            {
                if (!Quoted(scriptText, name))
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Output '" + name + "'", string.Format(CultureInfo.InvariantCulture, "The script never mentions \"{0}\". It writes each output as ScriptOutput.SetValue(\"{0}\", value); check the name.", name)));
                }
            }

            return result;
        }

        private static bool Quoted(string text, string name)
        {
            return text.IndexOf("\"" + name + "\"", StringComparison.Ordinal) >= 0;
        }

        private static string Summary(TasOptimisationDefinition tasOptimisationDefinition)
        {
            IReadOnlyList<string> names_Objective = tasOptimisationDefinition.ObjectiveNames;

            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} · {1} parameter(s): {2} · minimises {3}",
                tasOptimisationDefinition.Algorithm.AlgorithmType,
                tasOptimisationDefinition.NumberParameters.Count,
                string.Join(", ", tasOptimisationDefinition.ParameterNames),
                names_Objective.FirstOrDefault());

            if (names_Objective.Count > 1)
            {
                text += " (also records " + string.Join(", ", names_Objective.Skip(1)) + ")";
            }

            return text + string.Format(CultureInfo.InvariantCulture, " · up to {0} simulations", tasOptimisationDefinition.OptimizationSettings.MaxIterations);
        }
    }
}
