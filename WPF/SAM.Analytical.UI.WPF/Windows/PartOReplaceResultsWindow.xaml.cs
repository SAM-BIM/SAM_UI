// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Globalization;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The one question asked where a run is about to write into a Part O case folder that holds another run's results:
    /// replace them, or cancel. It names the case and the folder, what is in it and what a replacement does and does not
    /// touch, and it deletes nothing - the caller does, after this returns true.
    /// <para>
    /// Prepare &amp; Run and Iteration 3 share it, so the same situation reads and answers the same way. They differ only
    /// in what replacing means: Prepare &amp; Run removes the case's generated files; Iteration 3 keeps the case folder,
    /// which holds every method's record, and overwrites only the files the new run writes.
    /// </para>
    /// </summary>
    public partial class PartOReplaceResultsWindow : System.Windows.Window
    {
        public PartOReplaceResultsWindow(string caseName, PartOOutputOccupancy partOOutputOccupancy, bool removesGeneratedFiles)
        {
            InitializeComponent();

            textBlock_Heading.Text = string.Format(CultureInfo.CurrentCulture, "{0} already has results in this folder", caseName);
            textBox_Folder.Text = partOOutputOccupancy.Directory_Case;

            textBlock_Contents.Text = Contents(partOOutputOccupancy);
            textBlock_Explanation.Text = Explanation(removesGeneratedFiles, partOOutputOccupancy);

            button_Replace.Click += (s, e) => DialogResult = true;
        }

        /// <summary>What the folder holds, in one sentence. Exposed for tests.</summary>
        internal static string Contents(PartOOutputOccupancy partOOutputOccupancy)
        {
            string when = partOOutputOccupancy.LastWriteUtc.HasValue
                ? string.Format(CultureInfo.CurrentCulture, ", last written {0:d MMM yyyy HH:mm}", partOOutputOccupancy.LastWriteUtc.Value.ToLocalTime())
                : string.Empty;

            return string.Format(
                CultureInfo.CurrentCulture,
                "It holds {0} generated {1} from an earlier run{2}.",
                partOOutputOccupancy.Count_GeneratedFiles,
                partOOutputOccupancy.Count_GeneratedFiles == 1 ? "file" : "files",
                when);
        }

        /// <summary>What replacing does, and what it leaves alone. Exposed for tests.</summary>
        internal static string Explanation(bool removesGeneratedFiles, PartOOutputOccupancy partOOutputOccupancy)
        {
            string other = partOOutputOccupancy.Count_OtherFiles != 0
                ? string.Format(CultureInfo.CurrentCulture, " {0} other {1} in the folder will be left as {2}.", partOOutputOccupancy.Count_OtherFiles, partOOutputOccupancy.Count_OtherFiles == 1 ? "file" : "files", partOOutputOccupancy.Count_OtherFiles == 1 ? "it is" : "they are")
                : string.Empty;

            return removesGeneratedFiles
                ? "Replace existing results removes this case's generated run files - its tas, reports and diagnostics folders - and runs again. Your design model is not changed, and the other iterations' folders are not touched." + other + " Nothing is removed until you press Replace."
                : "Replace existing results runs this method again over the files already there: files the new run writes replace the old ones, and every other file in the folder - including other methods' records - is kept. Your design model is not changed. Nothing is replaced until you press Replace." + other;
        }

        /// <summary>The heading as shown. Exposed for tests.</summary>
        internal string Heading => textBlock_Heading.Text;

        /// <summary>The folder as shown. Exposed for tests.</summary>
        internal string Folder => textBox_Folder.Text;
    }
}
