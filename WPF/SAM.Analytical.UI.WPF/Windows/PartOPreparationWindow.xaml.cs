// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Whether bulk assignment is currently offered at all - manual authority only.</summary>
        internal bool IsBulkAssignmentAvailable => grid_Bulk.IsEnabled;

        /// <summary>Whether "Apply to selected" can currently be clicked.</summary>
        internal bool CanApplyToSelected => button_ApplyToSelected.IsEnabled;

        /// <summary>What the window currently says about the selection. For a test to read.</summary>
        internal string BulkSelectionDescription => textBlock_BulkSelection.Text;

        /// <summary>The products bulk assignment currently offers. For a test to read.</summary>
        internal List<VentilationUnitCapacityDescriptor> BulkProducts => [.. comboBox_BulkProduct.ItemsSource?.OfType<VentilationUnitCapacityDescriptor>() ?? []];

        /// <summary>
        /// Selects the rows for the named dwelling units, as clicking and Ctrl+clicking them would. For a
        /// test to drive a bulk assignment without a mouse.
        /// </summary>
        internal void SelectEquipmentRows(IEnumerable<Guid> guids_AirHandlingUnit)
        {
            HashSet<Guid> guids = [.. guids_AirHandlingUnit ?? []];

            dataGrid_Equipment.SelectedItems.Clear();

            foreach (PartOEquipmentRow partOEquipmentRow in equipmentRows)
            {
                if (guids.Contains(partOEquipmentRow.Guid_AirHandlingUnit))
                {
                    dataGrid_Equipment.SelectedItems.Add(partOEquipmentRow);
                }
            }

            UpdateBulkAvailability();
        }

        /// <summary>The product bulk assignment will apply. Settable so a test can choose one.</summary>
        internal VentilationUnitCapacityDescriptor? BulkProduct
        {
            get
            {
                return comboBox_BulkProduct.SelectedItem as VentilationUnitCapacityDescriptor;
            }
            set
            {
                comboBox_BulkProduct.SelectedItem = value;

                UpdateBulkAvailability();
            }
        }

        /// <summary>
        /// <b>Apply to selected.</b> Assigns the chosen product to every selected dwelling and to no other,
        /// as one deliberate act.
        ///
        /// <para><b>Why this exists rather than "editing a cell edits the selection"</b></para>
        /// <para>
        /// Because a hundred or a thousand dwellings cannot be authored one row at a time, and because the
        /// obvious alternative is dangerous: a picker that quietly wrote its value into every highlighted
        /// row would be triggered by an ordinary mis-click and would leave no trace of having done it. So
        /// single-row editing stays single-row, and bulk assignment is a named button next to a count of
        /// what it will touch.
        /// </para>
        ///
        /// <para><b>Each dwelling is judged on its own duty</b></para>
        /// <para>
        /// The set re-evaluates each assigned row independently, so capability, pool membership, headroom,
        /// status and any suggestion are that dwelling's own answer. Nothing here compares a capacity or
        /// reduces a design airflow to fit a product - and an insufficient assignment stays assigned and is
        /// reported, exactly as a single-row one does.
        /// </para>
        ///
        /// <para><b>Only the rows that changed are refreshed</b></para>
        /// <para>
        /// An assignment changes that row's derived state and no other row's, so refreshing the whole table
        /// would be O(D) work per bulk operation for nothing. On a thousand-dwelling project that is the
        /// difference between an instant operation and a visible pause.
        /// </para>
        /// </summary>
        /// <returns>True where every selected dwelling was assigned.</returns>
        internal bool ApplyToSelected()
        {
            if (partOEquipmentAssignmentSet is null || !partOEquipmentAssignmentSet.IsManual)
            {
                return false;
            }

            if (comboBox_BulkProduct.SelectedItem is not VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor)
            {
                return false;
            }

            List<PartOEquipmentRow> partOEquipmentRows = [.. dataGrid_Equipment.SelectedItems.OfType<PartOEquipmentRow>()];

            if (partOEquipmentRows.Count == 0)
            {
                return false;
            }

            bool result = partOEquipmentAssignmentSet.Assign(
                partOEquipmentRows.ConvertAll(x => x.Guid_AirHandlingUnit),
                ventilationUnitCapacityDescriptor.VentilationUnitReference,
                out List<Guid> guids_Assigned,
                out List<string> refusals);

            HashSet<Guid> guids = [.. guids_Assigned];

            foreach (PartOEquipmentRow partOEquipmentRow in partOEquipmentRows)
            {
                if (guids.Contains(partOEquipmentRow.Guid_AirHandlingUnit))
                {
                    partOEquipmentRow.Refresh();
                }
            }

            UpdateAssignmentText();

            UpdateBulkAvailability();

            if (refusals.Count != 0)
            {
                MessageBox.Show(string.Format("Not every selected dwelling was assigned.\n\n{0}", string.Join("\n\n", refusals)));
            }

            return result;
        }

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

            //Offered only under manual authority: in an automatic mode these rows are a rule's results, and
            //a bulk override made without taking authority would be an authored assignment nobody authored.
            grid_Bulk.IsEnabled = manual;

            //Rebuilt from the project's permitted set, which "Convert to Manual" and a pool change both
            //move. The chosen product is preserved by identity where it is still permitted.
            VentilationUnitReference? ventilationUnitReference = (comboBox_BulkProduct.SelectedItem as VentilationUnitCapacityDescriptor)?.VentilationUnitReference;

            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = partOEquipmentAssignmentSet?.AllowedCandidates ?? [];

            comboBox_BulkProduct.ItemsSource = ventilationUnitCapacityDescriptors;
            comboBox_BulkProduct.SelectedItem = ventilationUnitReference is null
                ? null
                : ventilationUnitCapacityDescriptors.Find(x => ventilationUnitReference.Matches(x.VentilationUnitReference));

            UpdateBulkAvailability();

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

        /// <summary>
        /// What a bulk assignment would currently do, and whether it can be done at all: a product has to
        /// be chosen, at least one dwelling selected, and the engineer has to hold the authority.
        /// </summary>
        private void UpdateBulkAvailability()
        {
            bool manual = partOEquipmentAssignmentSet?.IsManual ?? false;

            int selected = dataGrid_Equipment.SelectedItems.OfType<PartOEquipmentRow>().Count();

            textBlock_BulkSelection.Text = manual
                ? string.Format("Selected dwellings: {0}", selected)
                : string.Empty;

            button_ApplyToSelected.IsEnabled = manual && selected != 0 && comboBox_BulkProduct.SelectedItem is VentilationUnitCapacityDescriptor;
        }

        private void dataGrid_Equipment_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateAssignmentText();

            UpdateBulkAvailability();
        }

        private void comboBox_BulkProduct_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateBulkAvailability();
        }

        private void button_ApplyToSelected_Click(object sender, RoutedEventArgs e)
        {
            ApplyToSelected();
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

            //THAT row, and not the table. One dwelling's assignment cannot change another dwelling's
            //capability, pool membership, headroom, status or suggestion - each of those is an answer about
            //that dwelling's own duty against the project's own permitted set. Refreshing all of them was
            //harmless on a demonstration model and is O(D) per keystroke-committed edit on a real one.
            PartOEquipmentRow? partOEquipmentRow_Edited = e.Row?.Item as PartOEquipmentRow;

            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                partOEquipmentRow_Edited?.Refresh();

                UpdateAssignmentText();

                UpdateBulkAvailability();
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

            stringBuilder.AppendLine("Dwelling / Zone\tSpace\tPart F required l/s\tDesign supply l/s\tDesign extract l/s");
            foreach (PartOSpaceRow row in spaceRows)
            {
                stringBuilder.AppendLine(string.Format("{0}\t{1}\t{2:N1}\t{3:N1}\t{4:N1}", row.Dwelling, row.Name, row.PartFRequired_Lps, row.DesignSupply_Lps, row.DesignExtract_Lps));
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
