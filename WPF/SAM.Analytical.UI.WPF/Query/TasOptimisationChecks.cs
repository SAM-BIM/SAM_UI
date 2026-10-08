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
        /// The readiness list of Simulate &gt; Optimisation (Design Optimisation), in the order the questions arise: the
        /// Tas project, the Tas script, the Tas optimisation engine (TasGenExecute), the setup, then the non-blocking name
        /// check. Nothing is created or started. The setup is judged by SAM_Tas
        /// (<see cref="TasOptimisationDefinition.Validate"/>); its message is shown as it is, without the internal prefix
        /// the Grasshopper wording adds. The engine's path is not part of a ready line: the window shows it under Diagnostics.
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
                ? new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Tas optimisation engine", "Installed.")
                : new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Tas optimisation engine", "TasGenExecute.exe was not found at '" + path_TasGenExecute + "'. It is installed with Tas (TasGenOpt)."));

            TasOptimisationDefinition? tasOptimisationDefinition = null;
            if (!tasOptimisationInput.TryGetDefinition(out tasOptimisationDefinition, out List<string> problems))
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", string.Join(Environment.NewLine, problems)));
                tasOptimisationDefinition = null;
            }
            else
            {
                try
                {
                    tasOptimisationDefinition!.Validate();
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Setup", Summary(tasOptimisationDefinition)));
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException)
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", SettingsMessage(exception)));
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
            return TasOptimisationTasFiles(directory, out _);
        }

        /// <summary>
        /// As <see cref="TasOptimisationTasFiles(string)"/>; <paramref name="error"/> says why a folder that exists could
        /// not be listed (no list permission, a disconnected share, an I/O error), in which case the list is empty.
        /// </summary>
        public static List<string> TasOptimisationTasFiles(string directory, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return new List<string>();
            }

            try
            {
                return Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly).Where(NativeGenOptWorkspace.IsTasFile).Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()!;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = exception.Message;
                return new List<string>();
            }
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
            const string title = "Tas project";

            string directory = tasOptimisationInput.Directory?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Choose the folder that holds the Tas files the script works on.");
            }

            if (!Directory.Exists(directory))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The folder does not exist: '" + directory + "'.");
            }

            List<string> names = TasOptimisationTasFiles(directory, out string? error);
            if (error != null)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The folder cannot be read: " + error);
            }

            if (names.Count == 0)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The folder holds no Tas file (" + string.Join(", ", NativeGenOptWorkspace.TasFileExtensions) + ") at its top level, so the simulations would have no model to run.");
            }

            return new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, title, string.Join(", ", names) + ". Each simulation runs on a copy under " + tasOptimisationInput.TasOptimisationRunsDirectory() + "; the originals are not changed.");
        }

        private static TasOptimisationCheck TasOptimisationCheck_Script(string scriptPath, out string? scriptText)
        {
            const string title = "Tas script";

            scriptText = null;

            string path = scriptPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Choose the Tas script (C#) to run for each simulation.");
            }

            if (!System.IO.File.Exists(path))
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The Tas script does not exist: '" + path + "'.");
            }

            try
            {
                scriptText = System.IO.File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The Tas script cannot be read: " + exception.Message);
            }

            if (string.IsNullOrWhiteSpace(scriptText))
            {
                scriptText = null;
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "The Tas script is empty.");
            }

            return new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, title, Path.GetFileName(path));
        }

        /// <summary>
        /// A name the Tas script never mentions as a quoted literal: probably a typing mistake, which would make every
        /// simulation fail. A warning only, as a script may build a name at run time. The script's syntax is not repeated
        /// here: it is in the Script field's tooltip.
        /// </summary>
        private static List<TasOptimisationCheck> TasOptimisationChecks_Names(TasOptimisationInput tasOptimisationInput, string scriptText)
        {
            List<TasOptimisationCheck> result = new List<TasOptimisationCheck>();

            foreach (string name in tasOptimisationInput.Parameters.Select(x => x?.Name?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!)
            {
                if (!Quoted(scriptText, name))
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Design variable '" + name + "'", string.Format(CultureInfo.InvariantCulture, "The Tas script never mentions \"{0}\". Check that the name matches the one the script reads.", name)));
                }
            }

            foreach (string name in tasOptimisationInput.Objectives.Select(x => x?.Name?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()!)
            {
                if (!Quoted(scriptText, name))
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Output '" + name + "'", string.Format(CultureInfo.InvariantCulture, "The Tas script never mentions \"{0}\". Check that the name matches the one the script writes.", name)));
                }
            }

            return result;
        }

        private static bool Quoted(string text, string name)
        {
            return text.IndexOf("\"" + name + "\"", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// A SAM_Tas refusal as the Setup line shows it: the message itself, without the "Invalid GenOpt settings for the
        /// native route" or "Not supported by the native route" prefix that <see cref="TasOptimisationReport.Message"/>
        /// adds for the Grasshopper wording. Other failures keep that wording.
        /// </summary>
        private static string SettingsMessage(Exception exception)
        {
            if (exception is GenOptCompatibilityException || exception is NotSupportedException)
            {
                return exception.Message;
            }

            return TasOptimisationReport.Message(exception);
        }

        /// <summary>
        /// "Golden section on Setpoint (−5 to 35), minimising Result; recording Cost, CO2; at most 2000 simulations."
        /// Numbers are the entered ones at full precision; the simulation limit is an integer.
        /// </summary>
        private static string Summary(TasOptimisationDefinition tasOptimisationDefinition)
        {
            IReadOnlyList<string> names_Objective = tasOptimisationDefinition.ObjectiveNames;

            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} on {1}, minimising {2}",
                tasOptimisationDefinition.Algorithm.AlgorithmType.TasOptimisationAlgorithmName(),
                string.Join(", ", tasOptimisationDefinition.NumberParameters.Select(x => string.Format(CultureInfo.InvariantCulture, "{0} ({1} to {2})", x.Name, Number(x.Min), Number(x.Max)))),
                names_Objective.FirstOrDefault());

            if (names_Objective.Count > 1)
            {
                text += "; recording " + string.Join(", ", names_Objective.Skip(1));
            }

            return text + string.Format(CultureInfo.InvariantCulture, "; at most {0} simulations.", tasOptimisationDefinition.OptimizationSettings.MaxIterations);
        }

        /// <summary>Full precision (round-trip), with a true minus sign.</summary>
        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture).Replace('-', '−');
        }
    }
}
