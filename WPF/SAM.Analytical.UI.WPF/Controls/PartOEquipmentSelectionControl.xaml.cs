// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The Approved Document O equipment preselection, as one control: which authority chooses the products,
    /// and - visibly - which products there are to choose from.
    ///
    /// <para><b>Why this is a control and not code in each window</b></para>
    /// <para>
    /// Two workflows configure the same thing. <c>PartOIterationWindow</c> is the single-command route and
    /// <c>PartOWorkflowWindow</c> is Prepare &amp; Run, and both have to offer the same three modes over the
    /// same catalogue and produce the same <see cref="Analytical.PartOEquipmentSelection"/>. Held twice they
    /// would drift, and the first symptom of drift is the one native testing found: Prepare &amp; Run
    /// describing a smallest-capable selection over a project the engineer had put under manual authority.
    /// So the controls, the mode rules and the wording live here once, and each window hosts this.
    /// </para>
    ///
    /// <para><b>It configures; it never selects</b></para>
    /// <para>
    /// Nothing here runs a selection rule, compares a capacity or writes to a model. It produces a
    /// statement of intent - a mode and a permitted pool by identity - which
    /// <c>Modify.PreparePartOIteration</c> turns into a candidate set through
    /// <c>PartOEquipmentSelection.CandidateDescriptors</c>. Ticking a product <b>permits</b> it; it assigns
    /// nothing to anybody.
    /// </para>
    ///
    /// <para><b>The pool is never quietly widened</b></para>
    /// <para>
    /// Under <see cref="PartOEquipmentSelectionMode.AutomaticSelectedPool"/> with nothing ticked, this says
    /// so and <see cref="HasCandidates"/> is false - it does not tick everything to make the run proceed.
    /// A pool that silently became the whole catalogue would produce an answer that looked entirely correct.
    /// </para>
    /// </summary>
    public partial class PartOEquipmentSelectionControl : UserControl
    {
        private VentilationUnitCatalogue? ventilationUnitCatalogue;

        /// <summary>
        /// One row per selectable catalogue product, held so the ticks survive a mode change. The permitted
        /// set is read off these rather than off the grid's own selection, which the grid is free to discard
        /// when it re-virtualises.
        /// </summary>
        private List<PartOCatalogueProductRow> catalogueProductRows = [];

        private bool isSelectionEnabled = true;

        public PartOEquipmentSelectionControl()
        {
            InitializeComponent();

            radioButton_AutomaticAll.Checked += (s, e) => Apply();
            radioButton_AutomaticPool.Checked += (s, e) => Apply();
            radioButton_Manual.Checked += (s, e) => Apply();

            Apply();
        }

        /// <summary>
        /// Raised whenever the stated mode or the permitted pool moves, so a host can restate whatever it
        /// says about equipment elsewhere on its own surface.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// The catalogue this control offers. Setting it rebuilds the rows, all ticked - a catalogue arrives
        /// with nothing narrowed, which is the historic default and what the all-products mode means anyway.
        /// </summary>
        public VentilationUnitCatalogue? VentilationUnitCatalogue
        {
            get
            {
                return ventilationUnitCatalogue;
            }
            set
            {
                ventilationUnitCatalogue = value;

                catalogueProductRows = (value?.CapacityDescriptors ?? []).ConvertAll(x => new PartOCatalogueProductRow(x, true));

                //Subscribed, because the grid's tick writes straight to the row and tells nobody else. Left
                //unsubscribed, the line below the grid would keep reporting the pool the engineer had BEFORE
                //they last ticked something - which is the one place they look to find out whether an empty
                //pool is about to refuse the run.
                foreach (PartOCatalogueProductRow partOCatalogueProductRow in catalogueProductRows)
                {
                    partOCatalogueProductRow.PropertyChanged += (sender, eventArgs) =>
                    {
                        if (eventArgs.PropertyName == nameof(PartOCatalogueProductRow.IsUsed))
                        {
                            Apply();
                        }
                    };
                }

                dataGrid_Catalogue.ItemsSource = catalogueProductRows;

                Apply();
            }
        }

        /// <summary>
        /// Whether equipment selection is in play at all - the Iteration 1a / Iteration 2 difference, which
        /// each host decides its own way (a tick in one, the chosen scenario in the other). False disables
        /// everything here without hiding the catalogue.
        /// </summary>
        public bool IsSelectionEnabled
        {
            get
            {
                return isSelectionEnabled;
            }
            set
            {
                //Guarded, because a host may restate this on every refresh of its own. Apply raises
                //SelectionChanged, which a host may answer with that very refresh; without this the two
                //would bounce off each other for a value that never moved.
                if (isSelectionEnabled == value)
                {
                    return;
                }

                isSelectionEnabled = value;

                Apply();
            }
        }

        /// <summary>
        /// The preselection this control currently states, and the way a project's own configuration is
        /// restored into it.
        /// <para>
        /// Read off the controls rather than stored beside them, so there is no second copy to fall out of
        /// step with what the engineer is looking at. A product in a restored pool that the current
        /// catalogue no longer holds has no row to tick and is dropped, rather than keeping a permission
        /// nothing can act on.
        /// </para>
        /// </summary>
        public PartOEquipmentSelection EquipmentSelection
        {
            get
            {
                return new PartOEquipmentSelection(Mode, AllowedVentilationUnitReferences());
            }
            set
            {
                //Absent reads as the historic default rather than as a refusal - a project that has never
                //stated a preference has none. See PartOEquipmentSelection.
                PartOEquipmentSelectionMode partOEquipmentSelectionMode = value?.Mode ?? PartOEquipmentSelectionMode.AutomaticAllProducts;

                radioButton_AutomaticPool.IsChecked = partOEquipmentSelectionMode == PartOEquipmentSelectionMode.AutomaticSelectedPool;
                radioButton_Manual.IsChecked = partOEquipmentSelectionMode == PartOEquipmentSelectionMode.ManualPerDwelling;
                radioButton_AutomaticAll.IsChecked = partOEquipmentSelectionMode == PartOEquipmentSelectionMode.AutomaticAllProducts;

                bool all = value is null || !value.HasAllowedVentilationUnitReferences;

                foreach (PartOCatalogueProductRow partOCatalogueProductRow in catalogueProductRows)
                {
                    partOCatalogueProductRow.IsUsed = all || value.IsAllowed(partOCatalogueProductRow.VentilationUnitReference);
                }

                Apply();
            }
        }

        /// <summary>The selection authority this control currently states.</summary>
        public PartOEquipmentSelectionMode Mode
        {
            get
            {
                if (radioButton_Manual.IsChecked ?? false)
                {
                    return PartOEquipmentSelectionMode.ManualPerDwelling;
                }

                return (radioButton_AutomaticPool.IsChecked ?? false)
                    ? PartOEquipmentSelectionMode.AutomaticSelectedPool
                    : PartOEquipmentSelectionMode.AutomaticAllProducts;
            }
        }

        /// <summary>
        /// Whether an automatic selection has anything to choose from. False only for the pooled mode with
        /// nothing ticked - the one combination that will not prepare, and which is reported rather than
        /// corrected.
        /// </summary>
        public bool HasCandidates => Mode != PartOEquipmentSelectionMode.AutomaticSelectedPool || AllowedVentilationUnitReferences().Count != 0;

        /// <summary>
        /// The active mode in one sentence, for a host that describes the run elsewhere on its surface. This
        /// is the wording that used to be a fixed "the smallest capable unit is selected per dwelling" -
        /// which was simply untrue of a project under manual authority.
        /// </summary>
        public string ModeDescription
        {
            get
            {
                int used = AllowedVentilationUnitReferences().Count;

                switch (Mode)
                {
                    case PartOEquipmentSelectionMode.AutomaticSelectedPool:
                        return used == 0
                            ? "Equipment selection: Automatic - selected pool, with no product permitted. There is nothing for an automatic selection to choose from and this run will not prepare; no fallback to the full catalogue occurs."
                            : string.Format("Equipment selection: Automatic - selected pool. The smallest capable product is selected per dwelling only from the {0} allowed catalogue product(s). No fallback to the full catalogue occurs.", used);

                    case PartOEquipmentSelectionMode.ManualPerDwelling:
                        return "Equipment selection: Manual per dwelling. Existing authored equipment assignments are preserved. No automatic equipment selection runs.";

                    default:
                        return "Equipment selection: Automatic - all catalogue products. The smallest capable product is selected per dwelling from the full selectable catalogue. Product maximum capacity is a ceiling and never becomes a design airflow.";
                }
            }
        }

        /// <summary>What this control says about the catalogue and the current pool. The visible text.</summary>
        public string CatalogueDescription => textBlock_Catalogue.Text;

        /// <summary>The catalogue rows, so a test can read exactly what the engineer can see.</summary>
        internal List<PartOCatalogueProductRow> CatalogueProductRows => catalogueProductRows;

        /// <summary>Whether the ticks can currently be edited. See <see cref="Apply"/>.</summary>
        internal bool IsPoolEditable => !dataGrid_Catalogue.IsReadOnly;

        /// <summary>
        /// Keeps the controls consistent with what the current choice actually offers, and restates the line
        /// under the grid.
        /// <para>
        /// <b>Under "Automatic - all" the ticks are shown and locked.</b> Every product is eligible by
        /// definition in that mode, so an editable tick would offer a choice the mode does not have - but
        /// hiding the grid would take away the catalogue visibility this control exists to provide. So it is
        /// shown, all ticked, read-only.
        /// </para>
        /// </summary>
        private void Apply()
        {
            stackPanel_Mode.IsEnabled = isSelectionEnabled;
            label_Catalogue.IsEnabled = isSelectionEnabled;
            dataGrid_Catalogue.IsEnabled = isSelectionEnabled;

            bool all = Mode == PartOEquipmentSelectionMode.AutomaticAllProducts;

            dataGrid_Catalogue.IsReadOnly = !isSelectionEnabled || all;

            if (isSelectionEnabled && all)
            {
                foreach (PartOCatalogueProductRow partOCatalogueProductRow in catalogueProductRows)
                {
                    partOCatalogueProductRow.IsUsed = true;
                }
            }

            UpdateCatalogueText();

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// What the catalogue and the current pool amount to, in the words an engineer needs - including the
        /// one combination that will not prepare.
        /// </summary>
        private void UpdateCatalogueText()
        {
            if (!isSelectionEnabled)
            {
                textBlock_Catalogue.Text = ventilationUnitCatalogue is null
                    ? "The ventilation unit catalogue has not been read."
                    : string.Format("No equipment selection runs for this route, so no product is selected. {0}", ventilationUnitCatalogue.Description);

                return;
            }

            int used = AllowedVentilationUnitReferences().Count;
            int total = catalogueProductRows.Count;

            switch (Mode)
            {
                case PartOEquipmentSelectionMode.AutomaticSelectedPool:
                    textBlock_Catalogue.Text = used == 0
                        ? string.Format("No product is ticked, so an automatic selection has nothing to choose from and this will not prepare. Tick the products this project permits - SAM will not fall back to the other {0} in the catalogue.", total)
                        : string.Format("{0} of {1} product(s) permitted. Each dwelling is given the smallest permitted product that can meet its own design duty; the others are never selected.", used, total);
                    break;

                case PartOEquipmentSelectionMode.ManualPerDwelling:
                    textBlock_Catalogue.Text = used == 0 || used == total
                        ? string.Format("No selection rule runs. Every dwelling keeps the product it already has, and all {0} catalogue product(s) are offered when the assignments are stated.", total)
                        : string.Format("No selection rule runs. Every dwelling keeps the product it already has, and the {0} product(s) ticked here are offered when the assignments are stated.", used);
                    break;

                default:
                    textBlock_Catalogue.Text = string.Format("{0} Each dwelling is given the smallest product that can meet its own design duty. A product's Maximum is its capability ceiling and is never a design airflow.", ventilationUnitCatalogue?.Description);
                    break;
            }
        }

        /// <summary>The ticked products' identities - the project's permitted pool.</summary>
        private List<VentilationUnitReference> AllowedVentilationUnitReferences()
        {
            List<VentilationUnitReference> result = [];

            foreach (PartOCatalogueProductRow partOCatalogueProductRow in catalogueProductRows)
            {
                if (partOCatalogueProductRow.IsUsed && partOCatalogueProductRow.VentilationUnitReference is not null)
                {
                    result.Add(partOCatalogueProductRow.VentilationUnitReference);
                }
            }

            return result;
        }
    }
}
