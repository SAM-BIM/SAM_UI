// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What one run of <c>Modify.PreparePartOIteration</c> produced, laid out so the layers cannot be read
    /// as one another.
    /// <para>
    /// <b>Reporting only.</b> Every number on this window was read off the preparation or through a
    /// <c>SAM.Analytical</c> query; nothing is calculated here and nothing is decided here. OK adopts the
    /// prepared model, Cancel leaves the loaded model untouched.
    /// </para>
    /// <para>
    /// <b>The two grids exist to keep four quantities apart.</b> The spaces grid separates what Approved
    /// Document F requires of a room from what the prepared model will put through it; the equipment grid
    /// separates what a dwelling is designed to move from what the selected product can move at most, with
    /// the unspent difference named as headroom and the selection outcome in a column of its own. There is no
    /// cell on this window in which a product's 150 l/s maximum could be mistaken for a dwelling's design
    /// airflow.
    /// </para>
    /// </summary>
    public partial class PartOPreparationWindow : System.Windows.Window
    {
        private List<PartOEquipmentRow> equipmentRows = [];

        private List<PartOSpaceRow> spaceRows = [];

        public PartOPreparationWindow()
        {
            InitializeComponent();
        }

        /// <summary>The equipment rows, one per air handling unit the preparation built.</summary>
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
            }
        }

        /// <summary>The space rows, one per space of the prepared model.</summary>
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

        /// <summary>The headline: iteration, route, and what the preparation settled.</summary>
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

        /// <summary>
        /// The preparation's own notes, warnings and refusals, in that order and each labelled. Shown rather
        /// than summarised: a refusal names one item that produced no result, and collapsing them would hide
        /// which.
        /// </summary>
        public void SetDiagnostics(IEnumerable<string> notes, IEnumerable<string> warnings, IEnumerable<string> refusals)
        {
            StringBuilder stringBuilder = new();

            Append(stringBuilder, "REFUSAL", refusals);
            Append(stringBuilder, "WARNING", warnings);
            Append(stringBuilder, "NOTE", notes);

            textBox_Notes.Text = stringBuilder.ToString();
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

        private void button_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine(textBlock_Summary.Text);
            stringBuilder.AppendLine();

            stringBuilder.AppendLine("Unit\tSystem\tDesign supply l/s\tDesign extract l/s\tSelected product\tMaximum supply l/s\tMaximum extract l/s\tSupply headroom l/s\tExtract headroom l/s\tSelection");
            foreach (PartOEquipmentRow row in equipmentRows)
            {
                stringBuilder.AppendLine(string.Format("{0}\t{1}\t{2:N1}\t{3:N1}\t{4}\t{5:N1}\t{6:N1}\t{7:N1}\t{8:N1}\t{9}", row.UnitName, row.SystemName, row.DesignSupplyDuty_Lps, row.DesignExtractDuty_Lps, row.SelectedProduct, row.MaximumSupply_Lps, row.MaximumExtract_Lps, row.SupplyHeadroom_Lps, row.ExtractHeadroom_Lps, row.SelectionOutcome));
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
                //Another process can hold the clipboard open. Losing a copy is not worth an unhandled
                //exception over, and the window's own text is still on screen.
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
