// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The TM59 assessment of a completed Part O run.
    /// <para>
    /// <b>Presentation only, and deliberately thin.</b> The body of this window is the text
    /// <c>TM59AssessmentReport</c> produced, shown as it was given. The criteria, their limits, the hours
    /// counted and the pass/fail verdicts are the assessment's - none of them is restated, reformatted into a
    /// grid of this window's own making, or recomputed. The natural-ventilation criteria use their own summer
    /// and night subsets and the mechanical criterion its annual one; a window that laid out "exceedable
    /// hours" itself would be the place those got confused.
    /// </para>
    /// <para>
    /// <b>Spaces that were not assessed are shown, not hidden.</b> An identity that did not resolve, or a
    /// scenario that stated no strategy this assessment has a criterion for, means a space produced no result -
    /// a visible gap. Suppressing those would make a partial assessment look complete.
    /// </para>
    /// </summary>
    public partial class PartOTM59ResultWindow : System.Windows.Window
    {
        public PartOTM59ResultWindow()
        {
            InitializeComponent();
        }

        /// <summary>The production report text.</summary>
        public string Report
        {
            get
            {
                return textBox_Report.Text;
            }
            set
            {
                textBox_Report.Text = value;
            }
        }

        /// <summary>What was assessed, and from which results file.</summary>
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
        /// Why individual spaces produced no result: identities that did not resolve, and scenarios stating a
        /// strategy no criterion is known for.
        /// </summary>
        public void SetDiagnostics(IEnumerable<string> associationRefusals, IEnumerable<string> ventilationStrategyRefusals)
        {
            StringBuilder stringBuilder = new();

            //Distinct: RestoreDesignInternalConditions and Spaces are both asked for AssociationRefusals, and
            //the second call re-reports what the first already said.
            HashSet<string> descriptions = [];

            foreach (string description in associationRefusals ?? [])
            {
                if (!string.IsNullOrWhiteSpace(description) && descriptions.Add(description))
                {
                    stringBuilder.AppendLine(string.Format("NOT ASSESSED: {0}", description));
                }
            }

            foreach (string description in ventilationStrategyRefusals ?? [])
            {
                if (!string.IsNullOrWhiteSpace(description) && descriptions.Add(description))
                {
                    stringBuilder.AppendLine(string.Format("NO CRITERION: {0}", description));
                }
            }

            textBox_Diagnostics.Text = descriptions.Count == 0 ? "Every space resolved and was assessed." : stringBuilder.ToString();

            label_Diagnostics.Content = descriptions.Count == 0 ? "Spaces not assessed - none" : string.Format("Spaces not assessed - {0}", descriptions.Count);
        }

        private void button_CopyAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine(textBlock_Summary.Text);
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(textBox_Report.Text);
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
