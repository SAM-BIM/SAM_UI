// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Globalization;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The one question asked where the case a person is about to run already holds a saved result of the same design:
    /// open it, run again, or cancel. It decides nothing and changes nothing - the caller acts on <see cref="Choice"/>.
    /// Closing it any other way (Escape, the close button) is Cancel.
    /// </summary>
    public partial class PartOPreviousResultWindow : System.Windows.Window
    {
        internal PartOPreviousResultWindow(string caseName, PartOPreviousResult partOPreviousResult)
        {
            InitializeComponent();

            textBlock_Heading.Text = Heading_Text(caseName);
            textBox_Result.Text = partOPreviousResult.Path_Model;
            textBlock_Contents.Text = Contents(partOPreviousResult);
            textBlock_Explanation.Text = Explanation;

            button_Open.Click += (s, e) => Choose(PartOPreviousResultChoice.OpenPrevious);
            button_RunAgain.Click += (s, e) => Choose(PartOPreviousResultChoice.RunAgain);
        }

        /// <summary>What the person chose; <see cref="PartOPreviousResultChoice.Cancel"/> until they choose.</summary>
        internal PartOPreviousResultChoice Choice { get; private set; } = PartOPreviousResultChoice.Cancel;

        private void Choose(PartOPreviousResultChoice partOPreviousResultChoice)
        {
            Choice = partOPreviousResultChoice;
            DialogResult = true;
        }

        /// <summary>The heading. Exposed for tests.</summary>
        internal static string Heading_Text(string caseName)
        {
            return string.Format(CultureInfo.CurrentCulture, "Previous {0} result found", caseName);
        }

        /// <summary>When it was saved, in one sentence. Exposed for tests.</summary>
        internal static string Contents(PartOPreviousResult partOPreviousResult)
        {
            return string.Format(CultureInfo.CurrentCulture, "It was produced from this design and saved {0:d MMM yyyy HH:mm}.", partOPreviousResult.LastWriteUtc.ToLocalTime());
        }

        /// <summary>What each choice does. Exposed for tests.</summary>
        internal const string Explanation = "Open previous result reopens it for review - TM59 without simulating again - and does not run TAS. Run again goes on to simulate the case afresh, and asks before replacing anything. Nothing is changed until you choose.";

        /// <summary>The heading as shown. Exposed for tests.</summary>
        internal string Heading => textBlock_Heading.Text;
    }
}
