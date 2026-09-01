// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One air handling unit's row in the Part O preparation window: what the dwelling is designed to move,
    /// what product was selected for it, and what that product can move at most.
    /// <para>
    /// <b>These are four different quantities and the row keeps them in four different places.</b>
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c>. The
    /// design duty is what the dwelling moves; the maximum is a property of the equipment. On the accepted
    /// fixture three dwellings designed at 30/30, 63/63 and 63/63 l/s all select the same 150/150 l/s
    /// product, and the 150 belongs only in <see cref="MaximumSupply_Lps"/> /
    /// <see cref="MaximumExtract_Lps"/> - never in <see cref="DesignSupplyDuty_Lps"/>. Selecting a larger
    /// unit does not make a dwelling move more air.
    /// </para>
    /// <para>
    /// <b>Every value is read from its own authority; none is computed here.</b> The duties come from
    /// <c>Query.AirHandlingUnitDesignDuty</c>, the product from
    /// <c>Query.SelectedVentilationUnitReference</c>, the capacity from the
    /// <c>VentilationUnitCapacityDescriptor</c> that product resolves to. The one derived pair is headroom,
    /// which is stated here exactly as <c>VentilationUnitSelection.SupplyHeadroom_Lps</c> defines it -
    /// <c>Descriptor.MaximumSupplyFlowRate_Lps - SupplyDuty_Lps</c> - over the two values already on this
    /// row, so what is shown as headroom cannot disagree with the capacity and duty shown beside it.
    /// </para>
    /// <para>
    /// <b>Nothing here selects.</b> Selection happened inside <c>Modify.PreparePartOIteration</c>; this row
    /// reports its outcome.
    /// </para>
    /// </summary>
    public class PartOEquipmentRow
    {
        /// <param name="unitName">The air handling unit's name.</param>
        /// <param name="systemName">The ventilation system it supplies, for identifying the dwelling.</param>
        /// <param name="designSupplyDuty_Lps">The dwelling's design supply duty, from SAM.</param>
        /// <param name="designExtractDuty_Lps">The dwelling's design extract duty, from SAM.</param>
        /// <param name="ventilationUnitCapacityDescriptor">
        /// The selected product's capacity record, or null where no product is selected - which is Iteration
        /// 1a's normal state and not a failure.
        /// </param>
        /// <param name="refusal">Why no product was selected, where that is the reason. Null otherwise.</param>
        public PartOEquipmentRow(string unitName, string systemName, double designSupplyDuty_Lps, double designExtractDuty_Lps, VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor, string refusal = null)
        {
            UnitName = unitName;
            SystemName = systemName;
            DesignSupplyDuty_Lps = designSupplyDuty_Lps;
            DesignExtractDuty_Lps = designExtractDuty_Lps;
            Descriptor = ventilationUnitCapacityDescriptor;
            Refusal = refusal;
        }

        /// <summary>The air handling unit this row reports on.</summary>
        public string UnitName { get; }

        /// <summary>The ventilation system it supplies.</summary>
        public string SystemName { get; }

        /// <summary><b>Design</b> supply duty [l/s] - what the dwelling is designed to move.</summary>
        public double DesignSupplyDuty_Lps { get; }

        /// <summary><b>Design</b> extract duty [l/s] - what the dwelling is designed to move.</summary>
        public double DesignExtractDuty_Lps { get; }

        /// <summary>The selected product's capacity record, or null where none is selected.</summary>
        public VentilationUnitCapacityDescriptor Descriptor { get; }

        /// <summary>Why no product is selected, where a catalogue was offered and refused. Null otherwise.</summary>
        public string Refusal { get; }

        /// <summary>Whether a manufacturer product is selected for this unit.</summary>
        public bool HasSelectedProduct => Descriptor?.VentilationUnitReference is not null;

        /// <summary>
        /// The selected product, or a sentence saying that none is - never blank, so an empty cell cannot be
        /// read as "no equipment needed".
        /// </summary>
        public string SelectedProduct
        {
            get
            {
                if (HasSelectedProduct)
                {
                    return Descriptor.VentilationUnitReference.ToString();
                }

                return Refusal is null ? "No product selected (no catalogue offered)" : "No product selected";
            }
        }

        /// <summary>
        /// The selected product's <b>maximum</b> supply airflow [l/s], or <see cref="double.NaN"/> where no
        /// product is selected. A capability ceiling, not a duty - see the class note.
        /// </summary>
        public double MaximumSupply_Lps => HasSelectedProduct ? Descriptor.MaximumSupplyFlowRate_Lps : double.NaN;

        /// <summary>The selected product's <b>maximum</b> extract airflow [l/s]. See <see cref="MaximumSupply_Lps"/>.</summary>
        public double MaximumExtract_Lps => HasSelectedProduct ? Descriptor.MaximumExtractFlowRate_Lps : double.NaN;

        /// <summary>
        /// The selected product's unused supply capacity [l/s], or <see cref="double.NaN"/> where none is
        /// selected. Headroom deliberately left unspent - it is not part of the design duty and raising the
        /// design to consume it is a separate engineering decision.
        /// </summary>
        public double SupplyHeadroom_Lps => HasSelectedProduct ? Descriptor.MaximumSupplyFlowRate_Lps - DesignSupplyDuty_Lps : double.NaN;

        /// <summary>The selected product's unused extract capacity [l/s]. See <see cref="SupplyHeadroom_Lps"/>.</summary>
        public double ExtractHeadroom_Lps => HasSelectedProduct ? Descriptor.MaximumExtractFlowRate_Lps - DesignExtractDuty_Lps : double.NaN;

        /// <summary>
        /// What the equipment step did for this unit, in one word or phrase. Reported beside the design duty
        /// and never folded into it: whether a product could be found says nothing about whether the design
        /// airflow is right.
        /// </summary>
        public string SelectionOutcome
        {
            get
            {
                if (HasSelectedProduct)
                {
                    return "Selected";
                }

                return Refusal is null ? "Not applicable" : "Refused";
            }
        }
    }
}
