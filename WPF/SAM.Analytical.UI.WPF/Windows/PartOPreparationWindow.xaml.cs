// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What the Part O preparation produced, for review before it is adopted - and, where the engineer is
    /// the selection authority, where the per-dwelling equipment assignments are made.
    ///
    /// <para><b>Why the manual work is here and not in the previous dialog</b></para>
    /// <para>
    /// A dwelling's design duty, its equipment's capacity and the headroom between them only exist once the
    /// iteration has been prepared. The previous dialog can state which products are permitted; it cannot
    /// show what any of them would mean for a dwelling. So the pool and the mode are chosen there, and the
    /// assignments - including "Convert to Manual" and every per-dwelling override - are made here, against
    /// real numbers, before OK adopts the result.
    /// </para>
    ///
    /// <para><b>Nothing in this window decides anything</b></para>
    /// <para>
    /// Every edit is delegated to <see cref="EquipmentAssignmentSet"/>, which owns validation, suggestions
    /// and the single write path. There is no capacity comparison and no selection rule in this file - a
    /// second implementation of either is how a dialog comes to disagree with the engine it is a view of.
    /// The set writes to a model only when <c>Modify.PreparePartOIteration</c> commits it, after OK.
    /// </para>
    /// </summary>
    public partial class PartOPreparationWindow : System.Windows.Window
    {
        private List<PartOEquipmentRow> equipmentRows = [];

        private List<PartOSpaceRow> spaceRows = [];

        private PartOEquipmentAssignmentSet? partOEquipmentAssignmentSet;

        public PartOPreparationWindow()
        {
            InitializeComponent();

            UpdateEquipmentAvailability();
        }

        /// <summary>The equipment rows, one per dwelling air handling unit.</summary>
        public List<PartOEquipmentRow> EquipmentRows
        {
            get
            {
                return equipmentRows;
            }
            set
            {
                equipmentRows = value ?? [];

                dataGrid_Equipment.ItemsSource = equipmentRows;

                UpdateEquipmentAvailability();
            }
        }

        /// <summary>
        /// The assignment table this window edits, or null where equipment selection is not in play at all -
        /// Iteration 1a, or a catalogue that could not be read.
        /// <para>
        /// Setting it builds the rows from it, so the grid and the set cannot be given different content.
        /// </para>
        /// </summary>
        public PartOEquipmentAssignmentSet? EquipmentAssignmentSet
        {
            get
            {
                return partOEquipmentAssignmentSet;
            }
            set
            {
                partOEquipmentAssignmentSet = value;

                if (value is not null)
                {
                    EquipmentRows = value.Assignments.ConvertAll(x => new PartOEquipmentRow(x, value));
                }
                else
                {
                    UpdateEquipmentAvailability();
                }
            }
        }

        /// <summary>The space rows.</summary>
        public List<PartOSpaceRow> SpaceRows
        {
            get
            {
                return spaceRows;
            }
            set
            {
                spaceRows = value ?? [];

                dataGrid_Spaces.ItemsSource = spaceRows;
            }
        }

        /// <summary>The preparation summary.</summary>
        public string Summary
        {
            get
            {
                return textBlock_Summary.Text;
            }
            set
            {
                textBlock_Summary.Text = value;
            }
        }

        /// <summary>What this window currently says about the selection authority. For a test to read.</summary>
        public string ModeDescription => textBlock_Mode.Text;

        /// <summary>What this window currently says about the selected dwelling. For a test to read.</summary>
        public string AssignmentDescription => textBlock_Assignment.Text;

        /// <summary>Whether "Convert to Manual" is currently offered.</summary>
        internal bool CanConvertToManual => button_ConvertToManual.IsEnabled;

        /// <summary>Whether the assignment grid is currently editable - true only in manual mode.</summary>
        internal bool IsEquipmentEditable => !dataGrid_Equipment.IsReadOnly;

        /// <summary>
        /// <b>Convert to Manual.</b> Hands the selection authority to the engineer and preserves every
        /// dwelling's product exactly, by not touching any of them.
        /// <para>
        /// No selection is rerun, no product is improved upon, no airflow of any kind is recalculated and no
        /// simulation work is triggered. All that changes is who decides next - and, as a consequence, that
        /// the assigned-product cells become editable.
        /// </para>
        /// </summary>
        internal void ConvertToManual()
        {
            if (partOEquipmentAssignmentSet is null || partOEquipmentAssignmentSet.IsManual)
            {
                return;
            }

            partOEquipmentAssignmentSet.ConvertToManual();

            //The identities are untouched; only the derived columns and the editability move.
            foreach (PartOEquipmentRow partOEquipmentRow in equipmentRows)
            {
                partOEquipmentRow.Refresh();
            }

            UpdateEquipmentAvailability();
        }

        /// <summary>Takes the suggested product for the currently selected dwelling. An explicit act.</summary>
        internal bool AssignSuggested()
        {
            if (dataGrid_Equipment.SelectedItem is not PartOEquipmentRow partOEquipmentRow || !partOEquipmentRow.AssignSuggested())
            {
                return false;
            }

            //One dwelling changed, so every row's derived state is re-read - a pool or suggestion is a
            //property of the set, and only the set knows what a change means for the rest.
            foreach (PartOEquipmentRow partOEquipmentRow_Other in equipmentRows)
            {
                partOEquipmentRow_Other.Refresh();
            }

            UpdateEquipmentAvailability();

            return true;
        }

        /// <summary>Notes, warnings and refusals, refusals first.</summary>
        public void SetDiagnostics(IEnumerable<string> notes, IEnumerable<string> warnings, IEnumerable<string> refusals)
        {
            StringBuilder stringBuilder = new();

            Append(stringBuilder, "REFUSAL", refusals);
            Append(stringBuilder, "WARNING", warnings);
            Append(stringBuilder, "NOTE", notes);

            textBox_Notes.Text = stringBuilder.ToString();
        }

        /// <summary>
        /// Keeps the equipment controls consistent with who the authority currently is: the grid is
        /// read-only in the automatic modes, where its rows are results, and editable in manual mode, where
        /// they are choices.
        /// </summary>
        private void UpdateEquipmentAvailability()
        {
            bool manual = partOEquipmentAssignmentSet?.IsManual ?? false;

            dataGrid_Equipment.IsReadOnly = !manual;

            //Offered only where there is an automatic answer to convert. Converting an empty table would
            //change an authority over nothing, and converting a manual one is already done.
            button_ConvertToManual.IsEnabled = partOEquipmentAssignmentSet is not null
                && !partOEquipmentAssignmentSet.IsManual
                && partOEquipmentAssignmentSet.HasAssignments;

            textBlock_Mode.Text = partOEquipmentAssignmentSet is null
                ? string.Empty
                : Core.Query.Description(partOEquipmentAssignmentSet.EquipmentSelection.Mode);

            UpdateAssignmentText();
        }

        /// <summary>The selected dwelling in words, and whether a suggestion can be taken for it.</summary>
        private void UpdateAssignmentText()
        {
            PartOEquipmentRow? partOEquipmentRow = dataGrid_Equipment.SelectedItem as PartOEquipmentRow;

            textBlock_Assignment.Text = partOEquipmentRow?.Description ?? string.Empty;

            //A suggestion can only be TAKEN in manual mode. In an automatic mode the product is the rule's
            //answer, and applying a suggestion over it would be an authored assignment made without the
            //engineer having taken authority.
            button_AssignSuggested.IsEnabled = (partOEquipmentAssignmentSet?.IsManual ?? false) && (partOEquipmentRow?.HasSuggestion ?? false);
        }

        private static void Append(StringBuilder stringBuilder, string label, IEnumerable<string> descriptions)
        {
            foreach (string description in descriptions ?? [])
            {
                if (!string.IsNullOrWhiteSpace(description))
                {
                    stringBuilder.AppendLine(string.Format("{0}: {1}", label, description));
                }
            }
        }

        private void button_ConvertToManual_Click(object sender, RoutedEventArgs e)
        {
            ConvertToManual();
        }

        private void button_AssignSuggested_Click(object sender, RoutedEventArgs e)
        {
            AssignSuggested();
        }

        private void dataGrid_Equipment_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateAssignmentText();
        }

        /// <summary>
        /// After a picker commits, every row's derived state is re-read. The edit itself happened in
        /// <c>PartOEquipmentRow.SelectedCandidate</c>, which delegated it to the assignment set; this only
        /// makes the rest of the table agree with what the set now says.
        /// </summary>
        private void dataGrid_Equipment_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != System.Windows.Controls.DataGridEditAction.Commit)
            {
                return;
            }

            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                foreach (PartOEquipmentRow partOEquipmentRow in equipmentRows)
                {
                    partOEquipmentRow.Refresh();
                }

                UpdateAssignmentText();
            }));
        }

        private void button_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine(textBlock_Summary.Text);
            stringBuilder.AppendLine();

            stringBuilder.AppendLine("Dwelling\tUnit\tDesign supply l/s\tDesign extract l/s\tAssigned product\tMaximum supply l/s\tMaximum extract l/s\tSupply headroom l/s\tExtract headroom l/s\tStatus");
            foreach (PartOEquipmentRow row in equipmentRows)
            {
                stringBuilder.AppendLine(string.Format("{0}\t{1}\t{2:N1}\t{3:N1}\t{4}\t{5:N1}\t{6:N1}\t{7:N1}\t{8:N1}\t{9}", row.Dwelling, row.UnitName, row.DesignSupplyDuty_Lps, row.DesignExtractDuty_Lps, row.SelectedProduct, row.MaximumSupply_Lps, row.MaximumExtract_Lps, row.SupplyHeadroom_Lps, row.ExtractHeadroom_Lps, row.SelectionOutcome));
            }

            stringBuilder.AppendLine();

            stringBuilder.AppendLine("Space\tPart F required l/s\tDesign supply l/s\tDesign extract l/s");
            foreach (PartOSpaceRow row in spaceRows)
            {
                stringBuilder.AppendLine(string.Format("{0}\t{1:N1}\t{2:N1}\t{3:N1}", row.Name, row.PartFRequired_Lps, row.DesignSupply_Lps, row.DesignExtract_Lps));
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(textBox_Notes.Text);

            try
            {
                Clipboard.SetText(stringBuilder.ToString());
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                //The clipboard is held by another process. Nothing about the preparation depends on it.
            }
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
