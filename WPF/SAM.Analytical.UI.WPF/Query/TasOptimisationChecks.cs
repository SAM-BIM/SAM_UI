// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
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
        /// Tas project, the Tas script, the Tas optimisation engine (TasGenExecute), the setup, then the non-blocking
        /// warnings. Nothing is created or started. The engine's path is not part of a ready line: the window shows it
        /// under Diagnostics.
        /// <para>
        /// The setup is the form read as a SAM.Core.Optimisation definition (<see cref="TasOptimisationInput.TryGetDefinition"/>),
        /// judged first by its own diagnostics against the Tas engine's capabilities (each error blocks, with its message and
        /// hint), then by SAM_Tas exactly as <c>RunNative</c> judges the document before it creates anything
        /// (<see cref="TasOptimisationGate"/>); a SAM_Tas refusal is shown as it is, without the internal prefix the
        /// Grasshopper wording adds. The definition's warnings and SAM_Tas' script-name warnings
        /// (<c>TasScriptDiagnostics</c>: a name the script never reads or writes) never block.
        /// </para>
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

            if (!tasOptimisationInput.TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out List<string> problems) || optimisationDefinition == null)
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", string.Join(Environment.NewLine, problems)));
                return result;
            }

            List<OptimisationDiagnostic> diagnostics = optimisationDefinition.Diagnostics(Analytical.Tas.GenOpt.Query.TasOptimisationCapabilities());
            List<OptimisationDiagnostic> errors = diagnostics.FindAll(x => x.Severity == DiagnosticSeverity.Error);
            if (errors.Count != 0)
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", string.Join(Environment.NewLine, errors.ConvertAll(Text))));
            }
            else
            {
                try
                {
                    GenOptDocument genOptDocument = TasOptimisationGate(optimisationDefinition, tasOptimisationInput.Directory?.Trim(), scriptText);
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Setup", Summary(optimisationDefinition, genOptDocument)));
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException)
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", SettingsMessage(exception)));
                }
            }

            foreach (OptimisationDiagnostic optimisationDiagnostic in diagnostics.FindAll(x => x.Severity == DiagnosticSeverity.Warning))
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, Title(optimisationDefinition, optimisationDiagnostic), Text(optimisationDiagnostic)));
            }

            if (scriptText != null)
            {
                foreach (OptimisationDiagnostic optimisationDiagnostic in optimisationDefinition.TasScriptDiagnostics(scriptText))
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, Title(optimisationDefinition, optimisationDiagnostic), Text(optimisationDiagnostic)));
                }
            }

            return result;
        }

        /// <summary>
        /// The final SAM_Tas gate, with no folder created and no process started: builds the document SAM_Tas will run
        /// (<c>ToGenOptDocument</c>, which refuses a definition with any diagnostic error) and applies the conversions
        /// <see cref="GenOptDocument.RunNative"/> applies to it first (parameters, objectives, the kernel problem and the
        /// optimiser). It throws what they throw: <see cref="GenOptCompatibilityException"/> for a setting SAM_Tas or
        /// GenOpt would not accept, <see cref="NotSupportedException"/> for an algorithm the native optimiser does not run.
        /// </summary>
        /// <param name="optimisationDefinition">The definition.</param>
        /// <param name="directory">The Tas project folder; it is not read.</param>
        /// <param name="scriptText">The script text; null when it cannot be read yet (the script check says why).</param>
        public static GenOptDocument TasOptimisationGate(OptimisationDefinition optimisationDefinition, string? directory, string? scriptText)
        {
            GenOptDocument result = optimisationDefinition.ToGenOptDocument(directory ?? string.Empty, scriptText ?? string.Empty);

            List<NumberParameter> numberParameters = Analytical.Tas.GenOpt.Convert.NumberParameters(result.CommandFile.Parameters);
            ObjectiveFunctionLocation objectiveFunctionLocation = result.ConfigFile.Simulation.ObjectiveFunctionLocation;
            Analytical.Tas.GenOpt.Convert.Objectives(objectiveFunctionLocation);
            Analytical.Tas.GenOpt.Convert.ToSAM_OptimisationProblem(numberParameters.Cast<IParameter>(), objectiveFunctionLocation);
            Analytical.Tas.GenOpt.Convert.ToSAM_Optimiser(result.Algorithm, result.OptimizationSettings, numberParameters.Count);

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

        /// <summary>A diagnostic as a check line shows it: the message, then the hint when there is one.</summary>
        private static string Text(OptimisationDiagnostic optimisationDiagnostic)
        {
            return string.IsNullOrWhiteSpace(optimisationDiagnostic.Hint) ? optimisationDiagnostic.Message : optimisationDiagnostic.Message + " " + optimisationDiagnostic.Hint;
        }

        /// <summary>
        /// The check title of a warning: what it is about, from its path ("Design variable 'Setpoint'", "Output 'Cost'"),
        /// otherwise "Setup".
        /// </summary>
        private static string Title(OptimisationDefinition optimisationDefinition, OptimisationDiagnostic optimisationDiagnostic)
        {
            string path = optimisationDiagnostic.Path ?? string.Empty;
            if (Index(path, "$.variables[") is int index_Variable && index_Variable < optimisationDefinition.Variables.Count)
            {
                return "Design variable '" + optimisationDefinition.Variables[index_Variable]?.Name + "'";
            }

            if (Index(path, "$.outputs[") is int index_Output && index_Output < optimisationDefinition.Outputs.Count)
            {
                return "Output '" + optimisationDefinition.Outputs[index_Output]?.Name + "'";
            }

            return "Setup";
        }

        private static int? Index(string path, string prefix)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            int end = path.IndexOf(']', prefix.Length);
            return end > prefix.Length && int.TryParse(path.Substring(prefix.Length, end - prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int result) ? result : (int?)null;
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
        /// Numbers are the entered ones at full precision; the simulation limit is the one SAM_Tas runs (the engine
        /// default when the definition leaves it out), an integer.
        /// </summary>
        private static string Summary(OptimisationDefinition optimisationDefinition, GenOptDocument genOptDocument)
        {
            List<string> names_Objective = optimisationDefinition.TasOptimisationObjectiveNames();

            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} on {1}, minimising {2}",
                optimisationDefinition.Method.Algorithm.TasOptimisationAlgorithmName(),
                string.Join(", ", optimisationDefinition.Variables.Select(x => string.Format(CultureInfo.InvariantCulture, "{0} ({1} to {2})", x.Name, Number(x.Minimum), Number(x.Maximum)))),
                names_Objective.FirstOrDefault());

            if (names_Objective.Count > 1)
            {
                text += "; recording " + string.Join(", ", names_Objective.Skip(1));
            }

            return text + string.Format(CultureInfo.InvariantCulture, "; at most {0} simulations.", genOptDocument.OptimizationSettings.MaxIterations);
        }

        /// <summary>Full precision (round-trip), with a true minus sign.</summary>
        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture).Replace('-', '−');
        }
    }
}
