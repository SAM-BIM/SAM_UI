// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One space's row in the Part O preparation window: what Approved Document F requires of it, and what
    /// the prepared model will actually put through it.
    /// <para>
    /// <b>The two are separate columns because they are separate quantities.</b> The Part F figure is the
    /// requirement; the design airflow is what the realized terminal network moves. They coincide on a
    /// dwelling designed to the minimum and diverge the moment anything is designed above it, so a single
    /// column would hide the only thing worth looking at.
    /// </para>
    /// <para>
    /// <b>Read back through the queries the simulation uses</b>, not off the values that were written -
    /// <c>Query.CalculatedSupplyAirFlow</c> and the internal condition's exhaust airflow, converted from
    /// m3/s to l/s for display and otherwise untouched. This is the same pair the accepted Grasshopper
    /// component reports, for the same reason: what is shown here is what the export will see.
    /// </para>
    /// </summary>
    public class PartOSpaceRow
    {
        /// <summary>
        /// Builds the row for one space of the prepared model.
        /// </summary>
        public PartOSpaceRow(Space space)
        {
            Name = space?.Name;

            //Qualified: SAM.Analytical.UI.WPF declares a Query of its own.
            PartFRequired_Lps = space?.GetValue<PartFSpaceData>(SpaceParameter.PartFSpaceData)?.ContinuousDesignFlowRate_Lps ?? double.NaN;

            DesignSupply_Lps = ToLps(Analytical.Query.CalculatedSupplyAirFlow(space));

            DesignExtract_Lps = space?.InternalCondition is not null && space.InternalCondition.TryGetValue(InternalConditionParameter.ExhaustAirFlow, out double exhaustAirFlow)
                ? ToLps(exhaustAirFlow)
                : 0;
        }

        /// <summary>The space.</summary>
        public string Name { get; }

        /// <summary>
        /// What Approved Document F requires of this space [l/s], from its own
        /// <c>PartFSpaceData</c>. <see cref="double.NaN"/> where the space carries none - which is what a
        /// space that was never sized looks like, and is not zero.
        /// </summary>
        public double PartFRequired_Lps { get; }

        /// <summary>The design supply airflow [l/s] the prepared model will simulate.</summary>
        public double DesignSupply_Lps { get; }

        /// <summary>The design extract airflow [l/s] the prepared model will simulate.</summary>
        public double DesignExtract_Lps { get; }

        private static double ToLps(double value_M3s)
        {
            return double.IsNaN(value_M3s) ? double.NaN : value_M3s * 1000;
        }
    }
}
