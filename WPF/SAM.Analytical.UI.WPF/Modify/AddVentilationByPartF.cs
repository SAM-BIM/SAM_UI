// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        private const string PartFReportTitle = "Part F Ventilation";

        public static void AddVentilationByPartF(this UIAnalyticalModel? uIAnalyticalModel, IWin32Window? owner = null)
        {
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            PartFCalculator partFCalculator = Analytical.Query.DefaultPartFCalculator();
            if (partFCalculator is null)
            {
                return;
            }

            AdjacencyCluster? adjacencyCluster = analyticalModel.AdjacencyCluster;
            if(adjacencyCluster is null)
            {
                return;
            }

            PartFVectilationWindow partFVectilationWindow = new()
            {
                ZoneCategories = [.. adjacencyCluster.GetZoneCategories() ?? []],
                SetbackFlowRateFactor = Analytical.Query.DefaultPartFData()?.SetbackFlowRateFactor ?? PartFData.DefaultSetbackFlowRateFactor,
            };

            bool? showdialog = partFVectilationWindow.ShowDialog();
            if (showdialog == null || !showdialog.HasValue || !showdialog.Value)
            {
                return;
            }

            string? zoneCategoryName = partFVectilationWindow.SelectedZoneCategory;

            partFCalculator.AdjacencyCluster = adjacencyCluster;

            //Set on the calculator, not on the shared default rule set: PartFData is held by
            //ActiveSetting for the whole session, so writing the factor there would silently change every
            //later calculation. The property validates the factor and substitutes the documented default
            //for a negative value, a value above 1, NaN or infinity.
            partFCalculator.SetbackFlowRateFactor = partFVectilationWindow.SetbackFlowRateFactor;

            //One call, whether or not a zone category was chosen. PartFCalculator.Calculate(string)
            //handles both single house mode (empty category) and zoned mode, and only it applies the
            //explicit dwelling filter, the duplicate-zone check and the unzoned-space report. The
            //previous per-zone loop here bypassed all of that, so a shared corridor sitting in the flats
            //category was silently sized as a dwelling and nothing was reported to the user.
            partFCalculator.Calculate(zoneCategoryName);

            analyticalModel = new AnalyticalModel(analyticalModel, partFCalculator.AdjacencyCluster);

            uIAnalyticalModel?.SetJSAMObject(analyticalModel, new FullModification());

            Report(partFCalculator, owner);
        }

        /// <summary>
        /// Builds the calculation's report text: dwelling summaries, excluded zones, unclassified and
        /// unzoned spaces, warnings, notes and the local kitchen extract limitation. Pure text
        /// generation with no UI dependency, so it can be unit tested directly - a model with
        /// thousands of spaces can produce thousands of report lines, and this must not be the layer
        /// that struggles with that.
        /// </summary>
        public static string BuildReportText(PartFCalculator partFCalculator)
        {
            StringBuilder stringBuilder = new();

            if (partFCalculator.DwellingResults is not null && partFCalculator.DwellingResults.Count != 0)
            {
                stringBuilder.AppendLine(string.Format("{0} dwelling(s) sized.", partFCalculator.DwellingResults.Count));

                foreach (PartFDwellingResult dwellingResult in partFCalculator.DwellingResults)
                {
                    int spaceCount = dwellingResult.SpaceNames?.Count ?? 0;

                    stringBuilder.AppendLine(string.Format(
                        "{0}{1} space(s), {2:0.##} m2, {3} habitable room(s), {4} bedroom(s), continuous design {5:0.##} l/s, setback {6:0.##} l/s ({7:0.##}% of continuous design).{8}",
                        string.IsNullOrWhiteSpace(dwellingResult.Name) ? string.Empty : dwellingResult.Name + ": ",
                        spaceCount,
                        dwellingResult.InternalFloorArea_M2,
                        dwellingResult.HabitableRoomCount,
                        dwellingResult.BedroomCount,
                        dwellingResult.ContinuousDesignSystemRate_Lps,
                        dwellingResult.SetbackSystemRate_Lps,
                        dwellingResult.SetbackFlowRateFactor * 100,
                        dwellingResult.OneHabitableRoomRuleApplied ? " Table 1.3 note 1 (one habitable room) applied." : string.Empty));
                }
            }
            else
            {
                stringBuilder.AppendLine("No dwelling was sized.");
            }

            if (partFCalculator.ExcludedZoneNames is not null && partFCalculator.ExcludedZoneNames.Count != 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("Zones not sized as dwellings: " + string.Join(", ", partFCalculator.ExcludedZoneNames));
            }

            if (partFCalculator.UnclassifiedSpaceNames is not null && partFCalculator.UnclassifiedSpaceNames.Count != 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("Unclassified space(s): " + string.Join(", ", partFCalculator.UnclassifiedSpaceNames));
            }

            if (partFCalculator.Warnings is not null && partFCalculator.Warnings.Count != 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("WARNINGS");
                foreach (string warning in partFCalculator.Warnings)
                {
                    stringBuilder.AppendLine(warning);
                }
            }

            if (partFCalculator.Remarks is not null && partFCalculator.Remarks.Count != 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("NOTES");
                foreach (string remark in partFCalculator.Remarks)
                {
                    stringBuilder.AppendLine(remark);
                }
            }

            //Called out separately from the warning list so the local kitchen extract limitation is not
            //lost among the other messages: it is a standing limitation of the model, not a modelling slip.
            if (partFCalculator.Warnings is not null && partFCalculator.Warnings.Any(x => x.Contains("ENGINEERING CHECK REQUIRED")))
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("LOCAL KITCHEN EXTRACT: one or more dwellings contain a cooking space with no explicit local kitchen or cooker extract represented. Wet-room extract may balance the dwelling airflow but does not demonstrate compliance with the local kitchen-extract requirement. Model and verify it separately.");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Using this tool does not by itself demonstrate compliance with Building Regulations Part F. Results must be checked by a suitably qualified engineer against the full Approved Document.");

            return Wrap(stringBuilder.ToString(), reportLineLength);
        }

        /// <summary>
        /// Column at which report lines are wrapped. The Approved Document warnings are full paragraphs
        /// and the unzoned-space list can name thousands of spaces, so without this a line runs several
        /// screens wide and the reader has to scroll horizontally to read one sentence.
        /// </summary>
        private const int reportLineLength = 100;

        /// <summary>
        /// Hard-wraps each line at <paramref name="maxLineLength"/> on whitespace, so the report reads
        /// as a paragraph rather than one very long line. Existing line breaks and blank lines are kept,
        /// so the section structure survives.
        /// <para>
        /// A single word longer than the limit is emitted whole on its own line rather than split: the
        /// long tokens here are space and zone names, and breaking one in half would make the report
        /// name a space that does not exist.
        /// </para>
        /// </summary>
        private static string Wrap(string text, int maxLineLength)
        {
            if (string.IsNullOrEmpty(text) || maxLineLength <= 0)
            {
                return text;
            }

            StringBuilder result = new();

            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                //The text ends with a line break, so the final split segment is empty. Appending it
                //would add a blank line the report did not have.
                if (i == lines.Length - 1 && line.Length == 0)
                {
                    break;
                }

                string line_Trimmed = line.TrimEnd('\r');

                if (line_Trimmed.Length <= maxLineLength)
                {
                    result.AppendLine(line_Trimmed);
                    continue;
                }

                StringBuilder line_Current = new();

                foreach (string word in line_Trimmed.Split(' '))
                {
                    //+1 for the space that would be needed before this word.
                    if (line_Current.Length != 0 && line_Current.Length + 1 + word.Length > maxLineLength)
                    {
                        result.AppendLine(line_Current.ToString());
                        line_Current.Clear();
                    }

                    if (line_Current.Length != 0)
                    {
                        line_Current.Append(' ');
                    }

                    line_Current.Append(word);
                }

                if (line_Current.Length != 0)
                {
                    result.AppendLine(line_Current.ToString());
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Surfaces the calculation's warnings and remarks. Without this the dwelling filter, the
        /// unclassified spaces and the local kitchen extract limitation were all computed and then discarded,
        /// leaving the user with rates and no indication that anything needed checking.
        /// </summary>
        private static void Report(PartFCalculator partFCalculator, IWin32Window? owner)
        {
            string text = BuildReportText(partFCalculator);

            SAM.Core.UI.WPF.ReportWindow reportWindow = new(PartFReportTitle, text);

            //Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(reportWindow).Owner = owner.Handle;
            }

            reportWindow.ShowDialog();
        }
    }
}
