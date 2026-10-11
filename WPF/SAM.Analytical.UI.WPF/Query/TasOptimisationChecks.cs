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
        /// <param name="tasModelSession">What the window has read of the Tas model ("tas-model" engine only); null when nothing.</param>
        public static List<TasOptimisationCheck> TasOptimisationChecks(this TasOptimisationInput tasOptimisationInput, string? tasGenExecutePath = null, TasModelSession? tasModelSession = null)
        {
            List<TasOptimisationCheck> result = new List<TasOptimisationCheck>();
            if (tasOptimisationInput == null)
            {
                return result;
            }

            if (tasOptimisationInput.IsTasModel)
            {
                return TasModelChecks(tasOptimisationInput, tasGenExecutePath, tasModelSession);
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

        /// <summary>
        /// The readiness list of the "tas-model" engine: the Tas project, the Tas model (read in the background; Tas and its
        /// licence are needed), the Tas optimisation engine, then the setup: the form read as a definition, judged by its
        /// own diagnostics against the "tas-model" capabilities and the model's catalogue (a name not in the model is
        /// OPT609), then by SAM_Tas' runner, which resolves the glazing options and generates the script without starting
        /// anything. Warnings never block. There is no script line: SAM_Tas writes the script.
        /// </summary>
        private static List<TasOptimisationCheck> TasModelChecks(TasOptimisationInput tasOptimisationInput, string? tasGenExecutePath, TasModelSession? tasModelSession)
        {
            List<TasOptimisationCheck> result = new List<TasOptimisationCheck>();

            TasOptimisationCheck tasOptimisationCheck_Directory = TasOptimisationCheck_Directory(tasOptimisationInput);
            result.Add(tasOptimisationCheck_Directory);

            string directory = tasOptimisationInput.Directory?.Trim() ?? string.Empty;
            TasModelSession? session = tasModelSession != null && tasModelSession.Folder != null && string.Equals(SafeFullPath(tasModelSession.Folder), SafeFullPath(directory), StringComparison.OrdinalIgnoreCase) ? tasModelSession : null;
            result.Add(TasOptimisationCheck_Model(tasOptimisationCheck_Directory.Status == TasOptimisationCheckStatus.Blocked, session));

            string path_TasGenExecute = string.IsNullOrWhiteSpace(tasGenExecutePath) ? Analytical.Tas.GenOpt.Query.TasGenOptExecutePath() : tasGenExecutePath!;
            result.Add(System.IO.File.Exists(path_TasGenExecute)
                ? new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Tas optimisation engine", "Installed.")
                : new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Tas optimisation engine", "TasGenExecute.exe was not found at '" + path_TasGenExecute + "'. It is installed with Tas (TasGenOpt)."));

            if (!tasOptimisationInput.TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out List<string> problems) || optimisationDefinition == null)
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", string.Join(Environment.NewLine, problems)));
                return result;
            }

            IOptimisationCapabilities capabilities = Analytical.Tas.GenOpt.Query.TasModelCapabilities();
            OptimisationCatalogue? catalogue = session?.State == TasModelReadState.Ready ? session.Catalogue : null;
            List<OptimisationDiagnostic> diagnostics = catalogue == null ? optimisationDefinition.Diagnostics(capabilities) : optimisationDefinition.Diagnostics(capabilities, catalogue);
            List<OptimisationDiagnostic> errors = diagnostics.FindAll(x => x.Severity == DiagnosticSeverity.Error);
            if (optimisationDefinition.Variables.Count == 0 && optimisationDefinition.Outputs.Count == 0 && errors.Count != 0)
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", "Add what may change (Can change) and what to measure (Can measure) from the model's lists, or paste an AI assistant's reply."));
            }
            else if (errors.Count != 0)
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", string.Join(Environment.NewLine, errors.ConvertAll(Text))));
            }
            else if (session == null || catalogue == null)
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", "The definition is checked against the model once the model has been read."));
            }
            else
            {
                try
                {
                    TasModelRunner tasModelRunner = session.CreateRunner(optimisationDefinition, directory, tasOptimisationInput.RunsDirectory, tasGenExecutePath);
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Setup", Summary(optimisationDefinition, tasModelRunner)));
                }
                catch (TasOptimisationDefinitionException tasOptimisationDefinitionException)
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", tasOptimisationDefinitionException.Message));
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException || exception is System.IO.IOException)
                {
                    result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Setup", exception.Message));
                }
            }

            foreach (OptimisationDiagnostic optimisationDiagnostic in diagnostics.FindAll(x => x.Severity == DiagnosticSeverity.Warning))
            {
                result.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, Title(optimisationDefinition, optimisationDiagnostic), Text(optimisationDiagnostic)));
            }

            return result;
        }

        private static TasOptimisationCheck TasOptimisationCheck_Model(bool blocked_Directory, TasModelSession? tasModelSession)
        {
            const string title = "Tas model";

            if (blocked_Directory)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Read once the Tas project folder is chosen.");
            }

            if (tasModelSession == null || tasModelSession.State == TasModelReadState.NotRead)
            {
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Not read yet. Press Read model to list what can change and what can be measured.");
            }

            switch (tasModelSession.State)
            {
                case TasModelReadState.Reading:
                    return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, "Reading the Tas model (Tas opens the files read-only)…");

                case TasModelReadState.Failed:
                    return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, tasModelSession.Error ?? "The Tas model could not be read.");
            }

            return new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, title, TasModelText(tasModelSession));
        }

        /// <summary>"Model.tbd, Model.tsd, Model.tpd: 7 internal conditions, 2 glazing constructions, 1 plant room with 6 controllers. Glazing pool: 25 systems."</summary>
        public static string TasModelText(TasModelSession tasModelSession)
        {
            TasModelInventory? inventory = tasModelSession?.Inventory;
            if (inventory == null)
            {
                return string.Empty;
            }

            List<string> files = new List<string> { inventory.TbdFileName, inventory.TsdFileName, inventory.TpdFileName }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList()!;
            List<string> parts = new List<string>
            {
                Count(inventory.InternalConditions.Count, "internal condition"),
                Count(inventory.GlazingConstructions.Count, "glazing construction"),
            };

            if (inventory.TpdFileName != null)
            {
                parts.Add(Count(inventory.PlantRooms.Count, "plant room") + " with " + Count(inventory.PlantRooms.Sum(x => x.Controllers.Count), "controller"));
            }

            string text = string.Join(", ", files) + ": " + string.Join(", ", parts) + ".";
            text += tasModelSession!.PoolBusy
                ? " Glazing pool: calculating…"
                : " Glazing pool: " + Count(tasModelSession.Pool.Count, "system") + ".";
            return text;
        }

        private static string Count(int count, string noun)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? string.Empty : "s");
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path) ? string.Empty : System.IO.Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is System.IO.PathTooLongException || exception is System.Security.SecurityException)
            {
                return path;
            }
        }

        /// <summary>
        /// "Try every option on Glazing (3 options), minimising Annual cooling demand; recording …; at most 2000
        /// simulations." For values: "Golden section on Setpoint (−5 to 35), …". The limit is the one the kernel runs.
        /// </summary>
        private static string Summary(OptimisationDefinition optimisationDefinition, TasModelRunner tasModelRunner)
        {
            List<string> names_Objective = tasModelRunner.Kernel.OutputNames.ToList();

            string text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} on {1}, minimising {2}",
                optimisationDefinition.Method.Algorithm.TasOptimisationAlgorithmName(),
                string.Join(", ", optimisationDefinition.Variables.Select(x => x.Target?.Options != null && x.Target.Options.Count != 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} ({1} options)", x.Name, x.Target.Options.Count)
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1} to {2})", x.Name, Number(x.Minimum), Number(x.Maximum)))),
                names_Objective.FirstOrDefault());

            if (names_Objective.Count > 1)
            {
                text += "; recording " + string.Join(", ", names_Objective.Skip(1));
            }

            return text + string.Format(CultureInfo.InvariantCulture, "; at most {0} simulations.", tasModelRunner.Kernel.Optimiser.MaximumSimulations);
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
                return new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, title, tasOptimisationInput.IsTasModel
                    ? "Choose the folder that holds the Tas model (its TBD, and the TSD and TPD when there are results and plant)."
                    : "Choose the folder that holds the Tas files the script works on.");
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
