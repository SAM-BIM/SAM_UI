// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// An AI assistant's reply pasted into the Design Optimisation window ("Paste reply"), read before it may replace the
    /// form: the strict SAM.Core.Optimisation reader with <c>extract</c> set (a code fence or text around the JSON is
    /// dropped with a warning), checked against the engine's capabilities and the model's catalogue. Its findings are
    /// shown on the reply; the form changes only when the user then uses it (<see cref="CanUse"/>).
    /// </summary>
    public sealed class TasOptimisationAIReply
    {
        /// <param name="text">The pasted reply.</param>
        /// <param name="engine">The engine the window runs ("tas-model" or "tas-script").</param>
        /// <param name="optimisationCatalogue">The model's catalogue; null when the model has not been read.</param>
        public TasOptimisationAIReply(string? text, string? engine, OptimisationCatalogue? optimisationCatalogue)
        {
            Text = text ?? string.Empty;
            IOptimisationCapabilities capabilities = Query.TasOptimisationCapabilities(engine);
            Definition = global::SAM.Core.Optimisation.Create.OptimisationDefinition(Text, out List<OptimisationDiagnostic> diagnostics, capabilities, optimisationCatalogue, true);
            Diagnostics = diagnostics.AsReadOnly();

            Checks = Diagnostics
                .Where(x => x != null && x.Severity != DiagnosticSeverity.Info)
                .Select(x => new TasOptimisationCheck(x.Severity == DiagnosticSeverity.Error ? TasOptimisationCheckStatus.Blocked : TasOptimisationCheckStatus.Warning, Location(x), string.IsNullOrWhiteSpace(x.Hint) ? x.Message : x.Message + " " + x.Hint))
                .ToList()
                .AsReadOnly();

            Errors = Diagnostics.Count(x => x?.Severity == DiagnosticSeverity.Error);
            Warnings = Diagnostics.Count(x => x?.Severity == DiagnosticSeverity.Warning);
        }

        public string Text { get; }

        /// <summary>The definition read from the reply; null when it could not be read (an OPT1xx error).</summary>
        public OptimisationDefinition? Definition { get; }

        /// <summary>Every finding, errors first.</summary>
        public IReadOnlyList<OptimisationDiagnostic> Diagnostics { get; }

        /// <summary>The findings as the window lists them (errors ✕, warnings ⚠; information is not listed).</summary>
        public IReadOnlyList<TasOptimisationCheck> Checks { get; }

        public int Errors { get; }

        public int Warnings { get; }

        /// <summary>True when the reply holds a definition the form can take: one that could be read, and for this engine.</summary>
        public bool CanUse => Definition != null;

        /// <summary>The line above the findings: what the reply holds and whether it can run.</summary>
        public string Summary
        {
            get
            {
                if (Definition == null)
                {
                    return "The reply could not be read as an optimisation definition. Nothing was changed; ask the assistant again or correct the reply.";
                }

                string text = string.Format(CultureInfo.InvariantCulture, "The reply holds a definition with {0} design variable(s) and {1} output(s)", Definition.Variables.Count, Definition.Outputs.Count);
                if (Errors == 0)
                {
                    return text + (Warnings == 0 ? ", with no problem. Use it to replace the form." : ", with warnings (below). Use it to replace the form.");
                }

                return text + string.Format(CultureInfo.InvariantCulture, ", and {0} problem(s) that stop it running (below). You can still use it and correct the form, or ask the assistant again.", Errors);
            }
        }

        /// <summary>"Line 12, column 7" when the reader located the finding, otherwise its path.</summary>
        private static string Location(OptimisationDiagnostic optimisationDiagnostic)
        {
            if (optimisationDiagnostic.Line != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "Line {0}", optimisationDiagnostic.Line.Value);
            }

            return string.IsNullOrWhiteSpace(optimisationDiagnostic.Path) || optimisationDiagnostic.Path == "$" ? "Reply" : optimisationDiagnostic.Path;
        }
    }
}
