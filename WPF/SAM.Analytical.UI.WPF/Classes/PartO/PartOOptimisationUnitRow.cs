// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One row of the Iteration 2B equipment history: what one air handling unit was carrying at one
    /// iteration, beside what the product selected for it can move.
    /// <para>
    /// <b>Duty, Maximum and Headroom are three different quantities and the row keeps them apart.</b> The
    /// maximum is the selected product's rating - a ceiling the optimisation stops at, never a design
    /// airflow and never a room's target. The headroom is what is left, reported and deliberately not
    /// spent.
    /// </para>
    /// <para>
    /// <b>Equipment says "Selected" or "Kept", never "Reselected".</b> Iteration 2B works within the
    /// product chosen at Iteration 2 and does not change it; a run that needs a bigger unit stops and says
    /// so, leaving that decision to a person.
    /// </para>
    /// </summary>
    public class PartOOptimisationUnitRow
    {
        private PartOOptimisationUnitRow(int iteration, PartOOptimisationUnitState partOOptimisationUnitState, bool baseline)
        {
            Run = iteration;
            AHU = partOOptimisationUnitState.AirHandlingUnitName;
            System = partOOptimisationUnitState.VentilationSystemName;
            Duty = string.Format("{0:0.#}/{1:0.#}", partOOptimisationUnitState.SupplyDuty_Lps, partOOptimisationUnitState.ExtractDuty_Lps);

            VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor = partOOptimisationUnitState.VentilationUnitCapacityDescriptor;

            Maximum = ventilationUnitCapacityDescriptor is null
                ? "-"
                : string.Format("{0:0.#}/{1:0.#}", ventilationUnitCapacityDescriptor.MaximumSupplyFlowRate_Lps, ventilationUnitCapacityDescriptor.MaximumExtractFlowRate_Lps);

            Headroom = ventilationUnitCapacityDescriptor is null
                ? "-"
                : string.Format("{0:0.#}/{1:0.#}", partOOptimisationUnitState.SupplyHeadroom_Lps, partOOptimisationUnitState.ExtractHeadroom_Lps);

            Product = partOOptimisationUnitState.VentilationUnitReference is null ? "-" : partOOptimisationUnitState.VentilationUnitReference.ToString();

            Equipment = partOOptimisationUnitState.VentilationUnitReference is null
                ? "None selected"
                : baseline ? "Selected" : partOOptimisationUnitState.VentilationUnitSelectionOutcome == Enums.VentilationUnitSelectionOutcome.Refused ? "At capacity" : "Kept";
        }

        /// <summary>Which iteration - 0 is the baseline.</summary>
        public int Run { get; }

        /// <summary>The unit instance.</summary>
        public string AHU { get; }

        /// <summary>The dwelling system or systems it supplies.</summary>
        public string System { get; }

        /// <summary>Design supply/extract duty at this iteration [l/s].</summary>
        public string Duty { get; }

        /// <summary>The selected product's rated maximum supply/extract [l/s]. <b>Never a design airflow.</b></summary>
        public string Maximum { get; }

        /// <summary>Maximum less duty [l/s]. Reported, never spent.</summary>
        public string Headroom { get; }

        /// <summary>The selected product.</summary>
        public string Product { get; }

        /// <summary>Selected, Kept, At capacity, or None selected. Never Reselected - 2B does not buy equipment.</summary>
        public string Equipment { get; }

        /// <summary>Every equipment row of a whole optimisation run, iteration by iteration.</summary>
        public static List<PartOOptimisationUnitRow> Rows(PartOOptimisationRun? partOOptimisationRun)
        {
            List<PartOOptimisationUnitRow> result = [];

            foreach (PartOOptimisationStep partOOptimisationStep in partOOptimisationRun?.Steps ?? [])
            {
                foreach (PartOOptimisationUnitState partOOptimisationUnitState in partOOptimisationStep.UnitStates)
                {
                    result.Add(new PartOOptimisationUnitRow(partOOptimisationStep.Iteration, partOOptimisationUnitState, partOOptimisationStep.IsBaseline));
                }
            }

            return result;
        }
    }
}
