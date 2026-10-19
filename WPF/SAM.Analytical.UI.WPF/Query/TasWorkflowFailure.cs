// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Tas;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>How many Error lines a failure text lists before it counts the rest.</summary>
        public const int TasWorkflowFailureErrorsShown = 10;

        /// <summary>
        /// What a person needs to read about a TAS workflow that returned no model (S01b): the TAS return code and error log the
        /// simulation left (SAM_Tas <see cref="SimulationEvidence"/>), then every Error diagnostic the workflow reported, each as
        /// <c>Error: Subject: Message (Code)</c> - for a refused simulation the <c>TAS-SIM-001</c> lines, which carry the return code
        /// and the error log path themselves.
        /// <para>
        /// Only what the workflow reported is used: nothing is inferred from files in the output folder, so a TSD or JSON an earlier
        /// run left there cannot change the text. The error log is named only when the evidence names it and the file is there.
        /// </para>
        /// <para>
        /// Null when there is nothing to say (no evidence with a return code, no log and no Error); the caller then reports the
        /// failure without detail.
        /// </para>
        /// </summary>
        /// <param name="diagnostics">The workflow's diagnostics (<c>WorkflowCalculator.GetDiagnostics()</c>); null is none.</param>
        /// <param name="simulationEvidence">The workflow's <c>SimulationEvidence</c>; null where it did not simulate.</param>
        public static string? TasWorkflowFailure(IEnumerable<Diagnostic>? diagnostics, SimulationEvidence? simulationEvidence)
        {
            List<string> lines = new List<string>();

            if (simulationEvidence != null && simulationEvidence.NativeReturnCodeRecorded)
            {
                lines.Add("TAS return code: " + (simulationEvidence.NativeReturnCode?.ToString(CultureInfo.InvariantCulture) ?? "not an integer"));
            }

            string? path_ErrorLog = simulationEvidence?.Path_ErrorLog;
            if (!string.IsNullOrWhiteSpace(path_ErrorLog) && File.Exists(path_ErrorLog))
            {
                lines.Add("TAS error log: " + path_ErrorLog);
            }

            List<Diagnostic> errors = diagnostics?.Where(x => x != null && x.Severity == DiagnosticSeverity.Error).ToList() ?? new List<Diagnostic>();
            foreach (Diagnostic error in errors.Take(TasWorkflowFailureErrorsShown))
            {
                string line = Core.Query.DiagnosticMessage(LogRecordType.Error, error.Subject, error.Message);
                lines.Add(string.IsNullOrWhiteSpace(error.Code) ? line : string.Format("{0} ({1})", line, error.Code));
            }

            if (errors.Count > TasWorkflowFailureErrorsShown)
            {
                lines.Add(string.Format("... and {0} more error(s).", errors.Count - TasWorkflowFailureErrorsShown));
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        /// <summary>The failure detail of a TAS workflow that returned no model and reported nothing.</summary>
        public const string TasSimulationFailedNoDetail = "The TAS workflow returned no model and reported no error.";

        /// <summary>
        /// The Energy Simulation closing message for a simulation that started, was not cancelled and returned no model (S01b), in
        /// place of "Model could not be converted." It always opens with "Simulation failed", so the run cannot read as a success
        /// whatever else was written.
        /// </summary>
        /// <param name="failure">What the workflow reported (<see cref="TasWorkflowFailure"/>); null gives <see cref="TasSimulationFailedNoDetail"/>.</param>
        /// <param name="exportsRequested">SAP, TM59 or Part L export was asked for.</param>
        /// <param name="exportsWritten">Those exports were written (they are made from the model's inputs, not from results).</param>
        /// <param name="tpdRequested">A TPD was asked for; none is made from a failed run.</param>
        /// <param name="openModelKept">The open model is left as it was (the ordinary simulation; a Part O case never replaces it).</param>
        public static string TasSimulationFailedMessage(string? failure, bool exportsRequested, bool exportsWritten, bool tpdRequested, bool openModelKept)
        {
            List<string> parts = new List<string>
            {
                "Simulation failed: the TAS simulation did not complete, so this run produced no results.",
                string.IsNullOrWhiteSpace(failure) ? TasSimulationFailedNoDetail : failure!,
            };

            if (exportsRequested)
            {
                parts.Add(exportsWritten
                    ? "The requested SAP/TM59/Part L exports were written from the model's inputs; they contain no simulation results."
                    : "The requested SAP/TM59/Part L exports could not be written.");
            }

            if (tpdRequested)
            {
                parts.Add("No TPD was created: there are no results from this run to create it from.");
            }

            if (openModelKept)
            {
                parts.Add("The open model was not changed.");
            }

            return string.Join("\n\n", parts);
        }
    }
}
