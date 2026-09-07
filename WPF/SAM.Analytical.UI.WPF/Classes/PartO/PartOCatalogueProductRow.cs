// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One manufacturer ventilation unit product, as a row in the catalogue grid: what it is, what it can
    /// move, and whether the engineer permits it to be chosen.
    ///
    /// <para><b>This is the fix for the confirmed gap</b></para>
    /// <para>
    /// Before this row existed the engineer could see the product each dwelling had been given but not the
    /// catalogue it came from - not what products exist, not their capacities, not which were eligible, and
    /// not what else could be assigned. Understanding the available products meant opening the catalogue
    /// JSON. This row is what puts them on screen, inside the same dialog that configures how they are
    /// chosen, rather than in a separate diagnostic viewer.
    /// </para>
    ///
    /// <para><b>A capability, never a duty</b></para>
    /// <para>
    /// <see cref="MaximumSupply_Lps"/> is what this box can move at most. It is not any dwelling's design
    /// airflow and it is not an Approved Document F requirement, and the column it appears under says
    /// "Maximum" for that reason.
    /// </para>
    ///
    /// <para><b>Ticking a box permits a product; it does not assign one</b></para>
    /// <para>
    /// <see cref="IsUsed"/> contributes to the project's permitted pool -
    /// <c>PartOEquipmentSelection.AllowedVentilationUnitReferences</c> - which bounds what an automatic
    /// selection may offer and what a manual picker normally lists. No dwelling is fitted with anything by
    /// ticking it.
    /// </para>
    /// <para>
    /// One lightweight row per product in a virtualised <c>DataGrid</c>, so a catalogue of hundreds costs a
    /// handful of realised rows rather than hundreds of controls.
    /// </para>
    /// </summary>
    public class PartOCatalogueProductRow : INotifyPropertyChanged
    {
        private bool isUsed;

        public PartOCatalogueProductRow(VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor, bool isUsed)
        {
            Descriptor = ventilationUnitCapacityDescriptor;

            this.isUsed = isUsed;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>What this product is and what it can move. The catalogue's fact, held rather than copied.</summary>
        public VentilationUnitCapacityDescriptor Descriptor { get; }

        /// <summary>This product's identity - what a pool stores and what an assignment writes.</summary>
        public VentilationUnitReference? VentilationUnitReference => Descriptor?.VentilationUnitReference;

        public string? Manufacturer => VentilationUnitReference?.Manufacturer;

        public string? Model => VentilationUnitReference?.Model;

        /// <summary>
        /// The variant reference where the catalogue states one - the cooling module that makes the hybrid
        /// MRXBOX a different product from the bare unit. Part of the identity, so it is shown.
        /// </summary>
        public string? Reference => VentilationUnitReference?.Reference;

        /// <summary>The most this product can supply [l/s]. A ceiling, never a design airflow.</summary>
        public double MaximumSupply_Lps => Descriptor is null ? double.NaN : Descriptor.MaximumSupplyFlowRate_Lps;

        /// <summary>The most this product can extract [l/s]. A ceiling, never a design airflow.</summary>
        public double MaximumExtract_Lps => Descriptor is null ? double.NaN : Descriptor.MaximumExtractFlowRate_Lps;

        /// <summary>
        /// Whether the engineer permits this product to be chosen. Two-way bound to the grid's tick.
        /// </summary>
        public bool IsUsed
        {
            get
            {
                return isUsed;
            }
            set
            {
                if (isUsed == value)
                {
                    return;
                }

                isUsed = value;

                Raise(nameof(IsUsed));
            }
        }

        public override string ToString()
        {
            return string.Format("{0} [{1:0.#} / {2:0.#} l/s maximum]", VentilationUnitReference, MaximumSupply_Lps, MaximumExtract_Lps);
        }

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
