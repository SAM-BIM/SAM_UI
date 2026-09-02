// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The history of an automatic Iteration 2B optimisation - not a progress dialog after the fact, but the
    /// engineering record of what was tried and what it produced.
    /// <para>
    /// <b>Presentation only.</b> Every number shown is one the optimisation recorded: the design airflows
    /// from the round, the Approved Document F requirements from the adjustments that carry them, the
    /// duties and ratings from the unit states, and the TM59 verdicts from the production assessment.
    /// Nothing is recomputed here, and in particular no pass or fail is re-derived from an Actual and a
    /// Limit.
    /// </para>
    /// <para>
    /// <b>The stop reason is shown first and in the run's own words.</b> An optimisation that ended at the
    /// selected unit's capacity with rooms still failing is a real, useful answer; one that ended because a
    /// simulation would not run is not. A reader must not have to work out which they are looking at.
    /// </para>
    /// <para>
    /// <b>The diagnostic capacity envelope is shown apart from the run's answer, always.</b> It says what
    /// the already-selected unit could deliver if taken to its own ceiling - which is a different statement
    /// from what the optimisation accepted, and would be actively misleading read as the run's best result.
    /// So it has its own line, its own Stage value in both grids, and its own <c>MAX</c> run label; and
    /// where none was calculated, the line says why rather than going blank.
    /// </para>
    /// </summary>
    public partial class PartOOptimisationResultWindow : System.Windows.Window
    {
        public PartOOptimisationResultWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The run to show. Setting it fills both histories and the notes.
        /// </summary>
        public PartOOptimisationRun OptimisationRun
        {
            set
            {
                textBlock_StopReason.Text = value is null
                    ? "No optimisation was run."
                    : string.Format("Stopped: {0}", Core.Query.Description(value.StopReason));

                textBlock_Summary.Text = value?.Description ?? string.Empty;

                //Stated whatever happened - including "there was nothing to diagnose". An optional
                //diagnostic that leaves the line blank leaves a reader unable to tell it was considered.
                textBlock_CapacityEnvelope.Text = value is null || string.IsNullOrWhiteSpace(value.CapacityEnvelopeDescription)
                    ? string.Empty
                    : string.Format("Capacity envelope: {0}", value.CapacityEnvelopeDescription);

                dataGrid_AirFlow.ItemsSource = PartOOptimisationAirFlowRow.Rows(value);
                dataGrid_Unit.ItemsSource = PartOOptimisationUnitRow.Rows(value);

                SetDiagnostics(value);
            }
        }

        /// <summary>
        /// Every iteration's notes, warnings and refusals, in order and labelled by iteration - including
        /// each iteration's unique TSD, which is what makes the run auditable afterwards.
        /// </summary>
        private void SetDiagnostics(PartOOptimisationRun partOOptimisationRun)
        {
            StringBuilder stringBuilder = new();

            int count = 0;

            foreach (PartOOptimisationStep partOOptimisationStep in partOOptimisationRun?.Steps ?? [])
            {
                stringBuilder.AppendLine(string.Format(
                    "RUN {0} ({1}) - weather '{2}', results '{3}'",
                    partOOptimisationStep.IsCapacityEnvelope ? "MAX" : partOOptimisationStep.Iteration.ToString(),
                    Core.Query.Description(partOOptimisationStep.Kind),
                    partOOptimisationStep.WeatherData ?? "-",
                    partOOptimisationStep.Path_TSD ?? "-"));

                foreach (DesignAirFlowTargetRefusal designAirFlowTargetRefusal in partOOptimisationStep.TargetRefusals)
                {
                    stringBuilder.AppendLine(string.Format("  NOT OPTIMISABLE: {0}", designAirFlowTargetRefusal));

                    count++;
                }

                foreach (string note in Distinct(partOOptimisationStep.Notes))
                {
                    stringBuilder.AppendLine(string.Format("  {0}", note));

                    count++;
                }

                foreach (string warning in Distinct(partOOptimisationStep.Warnings))
                {
                    stringBuilder.AppendLine(string.Format("  WARNING: {0}", warning));

                    count++;
                }

                foreach (string refusal in Distinct(partOOptimisationStep.Refusals))
                {
                    stringBuilder.AppendLine(string.Format("  REFUSED: {0}", refusal));

                    count++;
                }

                stringBuilder.AppendLine();
            }

            textBox_Diagnostics.Text = stringBuilder.Length == 0 ? "Nothing was recorded." : stringBuilder.ToString();

            //The warm-start count is a WORKFLOW fact, so it belongs beside the notes rather than anywhere
            //near the engineering summary at the top. Reported whenever any iteration warm started, because
            //an engineer auditing a run's duration needs to know which iterations reused the conversion -
            //and each of those iterations' own notes says so too.
            int warmStarted = partOOptimisationRun?.WarmStarted ?? 0;

            label_Diagnostics.Content = warmStarted == 0
                ? string.Format("Notes, warnings and refusals - {0}", count)
                : string.Format("Notes, warnings and refusals - {0} ({1} iteration(s) reused the baseline conversion; each still ran its own full-year simulation)", count, warmStarted);
        }

        /// <summary>
        /// One line per distinct message. A round reports the same allocation note through both the round
        /// and the dwelling, and repeating it would bury the lines that only appear once.
        /// </summary>
        private static List<string> Distinct(IEnumerable<string> descriptions)
        {
            List<string> result = [];

            HashSet<string> seen = [];

            foreach (string description in descriptions ?? [])
            {
                if (!string.IsNullOrWhiteSpace(description) && seen.Add(description))
                {
                    result.Add(description);
                }
            }

            return result;
        }

        private void button_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine(textBlock_StopReason.Text);
            stringBuilder.AppendLine(textBlock_Summary.Text);

            if (!string.IsNullOrWhiteSpace(textBlock_CapacityEnvelope.Text))
            {
                stringBuilder.AppendLine(textBlock_CapacityEnvelope.Text);
            }

            stringBuilder.AppendLine();

            stringBuilder.AppendLine("Run\tStage\tSpace\tType\tDirection\tDesign before\tRequested\tAchieved\tPart F requires\tTM59");

            foreach (PartOOptimisationAirFlowRow partOOptimisationAirFlowRow in dataGrid_AirFlow.ItemsSource as List<PartOOptimisationAirFlowRow> ?? [])
            {
                stringBuilder.AppendLine(string.Format(
                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5:0.###}\t{6}\t{7:0.###}\t{8:0.###}\t{9}",
                    partOOptimisationAirFlowRow.Run,
                    partOOptimisationAirFlowRow.Stage,
                    partOOptimisationAirFlowRow.Space,
                    partOOptimisationAirFlowRow.Type,
                    partOOptimisationAirFlowRow.Direction,
                    partOOptimisationAirFlowRow.DesignBefore_Lps,
                    partOOptimisationAirFlowRow.Requested_Lps.HasValue ? partOOptimisationAirFlowRow.Requested_Lps.Value.ToString("0.###") : "-",
                    partOOptimisationAirFlowRow.Achieved_Lps,
                    partOOptimisationAirFlowRow.Requirement_Lps,
                    partOOptimisationAirFlowRow.TM59Status));
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Run\tStage\tAHU\tSystem\tDuty\tMaximum\tHeadroom\tProduct\tEquipment");

            foreach (PartOOptimisationUnitRow partOOptimisationUnitRow in dataGrid_Unit.ItemsSource as List<PartOOptimisationUnitRow> ?? [])
            {
                stringBuilder.AppendLine(string.Format(
                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}",
                    partOOptimisationUnitRow.Run,
                    partOOptimisationUnitRow.Stage,
                    partOOptimisationUnitRow.AHU,
                    partOOptimisationUnitRow.System,
                    partOOptimisationUnitRow.Duty,
                    partOOptimisationUnitRow.Maximum,
                    partOOptimisationUnitRow.Headroom,
                    partOOptimisationUnitRow.Product,
                    partOOptimisationUnitRow.Equipment));
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(textBox_Diagnostics.Text);

            try
            {
                Clipboard.SetText(stringBuilder.ToString());
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                //Another process can hold the clipboard open; the text is still on screen.
            }
        }

        private void button_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
