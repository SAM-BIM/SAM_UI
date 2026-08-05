// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void AddVentilationByPartF(this UIAnalyticalModel? uIAnalyticalModel)
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

            Report(partFCalculator);
        }

        /// <summary>
        /// Surfaces the calculation's warnings and remarks. Without this the dwelling filter, the
        /// unclassified spaces and the local kitchen extract limitation were all computed and then discarded,
        /// leaving the user with rates and no indication that anything needed checking.
        /// </summary>
        private static void Report(PartFCalculator partFCalculator)
        {
            List<string> lines = [];

            if (partFCalculator.DwellingResults is not null && partFCalculator.DwellingResults.Count != 0)
            {
                lines.Add(string.Format("{0} dwelling(s) sized.", partFCalculator.DwellingResults.Count));

                foreach (PartFDwellingResult dwellingResult in partFCalculator.DwellingResults)
                {
                    lines.Add(string.Format(
                        "{0}{1} habitable room(s), {2} bedroom(s), {3:0.##} m2, continuous design {4:0.##} l/s, setback {5:0.##} l/s ({6:0.##}% of continuous design).{7}",
                        string.IsNullOrWhiteSpace(dwellingResult.Name) ? string.Empty : dwellingResult.Name + ": ",
                        dwellingResult.HabitableRoomCount,
                        dwellingResult.BedroomCount,
                        dwellingResult.InternalFloorArea_M2,
                        dwellingResult.ContinuousDesignSystemRate_Lps,
                        dwellingResult.SetbackSystemRate_Lps,
                        dwellingResult.SetbackFlowRateFactor * 100,
                        dwellingResult.OneHabitableRoomRuleApplied ? " Table 1.3 note 1 (one habitable room) applied." : string.Empty));
                }
            }
            else
            {
                lines.Add("No dwelling was sized.");
            }

            if (partFCalculator.ExcludedZoneNames is not null && partFCalculator.ExcludedZoneNames.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("Zones not sized as dwellings: " + string.Join(", ", partFCalculator.ExcludedZoneNames));
            }

            if (partFCalculator.UnclassifiedSpaceNames is not null && partFCalculator.UnclassifiedSpaceNames.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("Unclassified space(s): " + string.Join(", ", partFCalculator.UnclassifiedSpaceNames));
            }

            if (partFCalculator.Warnings is not null && partFCalculator.Warnings.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("WARNINGS");
                lines.AddRange(partFCalculator.Warnings);
            }

            if (partFCalculator.Remarks is not null && partFCalculator.Remarks.Count != 0)
            {
                lines.Add(string.Empty);
                lines.Add("NOTES");
                lines.AddRange(partFCalculator.Remarks);
            }

            //Called out separately from the warning list so the local kitchen extract limitation is not
            //lost among the other messages: it is a standing limitation of the model, not a modelling slip.
            if (partFCalculator.Warnings is not null && partFCalculator.Warnings.Any(x => x.Contains("ENGINEERING CHECK REQUIRED")))
            {
                lines.Add(string.Empty);
                lines.Add("LOCAL KITCHEN EXTRACT: one or more dwellings contain a cooking space with no explicit local kitchen or cooker extract represented. Wet-room extract may balance the dwelling airflow but does not demonstrate compliance with the local kitchen-extract requirement. Model and verify it separately.");
            }

            lines.Add(string.Empty);
            lines.Add("Using this tool does not by itself demonstrate compliance with Building Regulations Part F. Results must be checked by a suitably qualified engineer against the full Approved Document.");

            MessageBoxImage messageBoxImage = partFCalculator.Warnings is not null && partFCalculator.Warnings.Count != 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;

            MessageBox.Show(string.Join("\n", lines), "Part F Ventilation", MessageBoxButton.OK, messageBoxImage);
        }
    }
}
