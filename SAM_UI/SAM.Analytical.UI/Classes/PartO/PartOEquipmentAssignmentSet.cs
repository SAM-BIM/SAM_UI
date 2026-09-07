// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// The whole dwelling-by-dwelling equipment assignment table, and every operation the equipment
    /// selection workflow performs on it: convert to manual, override one dwelling, change the permitted
    /// pool, and commit.
    ///
    /// <para><b>Pure until Commit</b></para>
    /// <para>
    /// Nothing here touches a model except <see cref="Commit"/>. Converting authority, editing a row,
    /// widening or narrowing the pool and calculating a suggestion are all operations on captured numbers,
    /// so the table can be re-evaluated freely without a model rescan and without a half-applied change
    /// ever reaching an <c>AdjacencyCluster</c>. <see cref="Commit"/> is the one seam, it writes only rows
    /// the engineer actually changed, and it writes them through
    /// <c>Analytical.Modify.AssignVentilationUnit</c> - the same parameter the automatic path writes.
    /// </para>
    ///
    /// <para><b>The selection rule is called, never reimplemented</b></para>
    /// <para>
    /// Suggestions come from <c>Analytical.Query.SelectSmallestCapableVentilationUnit</c>. A second
    /// smallest-capable implementation in the user interface is exactly how a dialog comes to disagree with
    /// the engine it is a view of.
    /// </para>
    ///
    /// <para><b>Cost</b></para>
    /// <para>
    /// The catalogue is indexed by identity once, so resolving what each dwelling's product can move is one
    /// dictionary probe rather than a scan: a full re-evaluation is O(D + P), rising to O(D x P) only where
    /// dwellings are insufficient and each needs a suggestion chosen from the permitted products. Editing
    /// one row re-evaluates that row. No operation re-derives a design duty, and none calls
    /// <c>GetSpaces</c>, <c>GetZones</c> or <c>GetObjects</c> - the rows were built from those once.
    /// </para>
    /// </summary>
    public class PartOEquipmentAssignmentSet
    {
        private readonly List<PartOEquipmentAssignment> assignments = [];

        private readonly List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors = [];

        /// <summary>
        /// Identity -> capability, built once. A key whose value is null is an identity the catalogue gives
        /// two different capacities to: unknown rather than either of them, because returning one would make
        /// a unit's adequacy depend on the order the catalogue was read in.
        /// <c>Analytical.Query.SelectedVentilationUnitCapacityDescriptor</c> refuses the same case the same
        /// way, and this index has to agree with it.
        /// </summary>
        private readonly Dictionary<string, VentilationUnitCapacityDescriptor> dictionary_Descriptor = [];

        private List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors_Allowed = [];

        /// <param name="assignments">One row per dwelling air handling unit, already captured.</param>
        /// <param name="ventilationUnitCapacityDescriptors">
        /// The <b>whole</b> selectable catalogue, never the pool. This is the capability lookup - a manually
        /// assigned product that has since left the pool still has to resolve its own rating, or a
        /// procurement change would silently turn a sound dwelling into "capacity unknown".
        /// </param>
        /// <param name="partOEquipmentSelection">The project's mode and permitted pool. Null reads as the default.</param>
        public PartOEquipmentAssignmentSet(IEnumerable<PartOEquipmentAssignment> assignments, IEnumerable<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors, PartOEquipmentSelection partOEquipmentSelection)
        {
            foreach (PartOEquipmentAssignment partOEquipmentAssignment in assignments ?? [])
            {
                if (partOEquipmentAssignment is not null)
                {
                    this.assignments.Add(partOEquipmentAssignment);
                }
            }

            foreach (VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor in ventilationUnitCapacityDescriptors ?? [])
            {
                if (ventilationUnitCapacityDescriptor is null || !ventilationUnitCapacityDescriptor.IsValid)
                {
                    continue;
                }

                this.ventilationUnitCapacityDescriptors.Add(ventilationUnitCapacityDescriptor);

                Index(ventilationUnitCapacityDescriptor);
            }

            EquipmentSelection = partOEquipmentSelection is null ? new PartOEquipmentSelection() : new PartOEquipmentSelection(partOEquipmentSelection);

            Refresh();
        }

        /// <summary>
        /// Builds the table from a prepared model, doing every model read once and in one place.
        /// <para>
        /// The duties are derived here, per unit, and captured - which is the only time a duty is derived
        /// for this table at all. <paramref name="dictionary_DwellingName"/> is handed in rather than
        /// resolved, because the caller already knows the run's zones and rebuilding that mapping per unit
        /// is precisely the per-dwelling model rescan this design exists to avoid.
        /// </para>
        /// </summary>
        /// <param name="adjacencyCluster">The prepared cluster.</param>
        /// <param name="airHandlingUnits">The run's own dwelling units, already scoped.</param>
        /// <param name="dictionary_VentilationSystemName">Unit guid -> the system it supplies. Display only.</param>
        /// <param name="dictionary_DwellingName">Unit guid -> what to call the dwelling. Display only.</param>
        /// <param name="ventilationUnitCapacityDescriptors">The whole selectable catalogue.</param>
        /// <param name="partOEquipmentSelection">The project's mode and permitted pool.</param>
        public static PartOEquipmentAssignmentSet Create(
            AdjacencyCluster adjacencyCluster,
            IEnumerable<AirHandlingUnit> airHandlingUnits,
            Dictionary<Guid, string> dictionary_VentilationSystemName,
            Dictionary<Guid, string> dictionary_DwellingName,
            IEnumerable<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors,
            PartOEquipmentSelection partOEquipmentSelection)
        {
            List<PartOEquipmentAssignment> assignments = [];

            foreach (AirHandlingUnit airHandlingUnit in airHandlingUnits ?? [])
            {
                if (airHandlingUnit is null)
                {
                    continue;
                }

                if (adjacencyCluster is null || !Analytical.Query.AirHandlingUnitDesignDuty(adjacencyCluster, airHandlingUnit, out double supplyDuty_Lps, out double extractDuty_Lps))
                {
                    //No duty is captured as no duty, not as zero - see PartOEquipmentAssignment.Status.
                    supplyDuty_Lps = double.NaN;
                    extractDuty_Lps = double.NaN;
                }

                string ventilationSystemName = null;
                dictionary_VentilationSystemName?.TryGetValue(airHandlingUnit.Guid, out ventilationSystemName);

                string dwellingName = null;
                dictionary_DwellingName?.TryGetValue(airHandlingUnit.Guid, out dwellingName);

                assignments.Add(new PartOEquipmentAssignment(
                    airHandlingUnit.Guid,
                    airHandlingUnit.Name,
                    ventilationSystemName,
                    dwellingName,
                    supplyDuty_Lps,
                    extractDuty_Lps,
                    airHandlingUnit.SelectedVentilationUnitReference()));
            }

            return new PartOEquipmentAssignmentSet(assignments, ventilationUnitCapacityDescriptors, partOEquipmentSelection);
        }

        /// <summary>The project's selection mode and permitted pool, as this table currently stands.</summary>
        public PartOEquipmentSelection EquipmentSelection { get; private set; }

        /// <summary>Whether the engineer is the authority - and so whether rows are editable.</summary>
        public bool IsManual => EquipmentSelection.Mode == PartOEquipmentSelectionMode.ManualPerDwelling;

        /// <summary>The rows, in the order the preparation produced them.</summary>
        public List<PartOEquipmentAssignment> Assignments => [.. assignments];

        /// <summary>Whether any row's product differs from the one the model already carries.</summary>
        public bool HasChanges => assignments.Find(x => x.HasChanged) is not null;

        /// <summary>Whether any row currently has a product assigned - what makes "Convert to Manual" worth offering.</summary>
        public bool HasAssignments => assignments.Find(x => x.IsAssigned) is not null;

        /// <summary>
        /// <b>Convert to Manual.</b> Changes the selection authority to the engineer and <b>preserves every
        /// dwelling's product exactly</b>, by not touching any of them.
        /// <para>
        /// It reruns no selection, chooses nothing "better", nothing smaller, and recalculates no airflow of
        /// any kind. There is nothing to copy across either: the rows already hold the identities the model
        /// carries, so preserving them is the absence of an action rather than an action. A dwelling with no
        /// valid assignment stays <see cref="PartOEquipmentAssignmentStatus.NotAssigned"/> and is shown as
        /// such rather than being given a plausible product.
        /// </para>
        /// <para>
        /// The permitted pool is preserved too, and becomes the normal candidate list each dwelling's
        /// picker offers - see <see cref="Candidates"/>.
        /// </para>
        /// </summary>
        public void ConvertToManual()
        {
            EquipmentSelection = EquipmentSelection.Manual();

            //Only the derived columns move: a product outside the pool is now worth flagging, and an
            //insufficient one is now worth suggesting against. No identity is read or written here.
            Refresh();
        }

        /// <summary>
        /// Assigns one dwelling's product. Changes that row and no other, and writes nothing until
        /// <see cref="Commit"/>.
        /// <para>
        /// A product that cannot meet the dwelling's duty is accepted and reported as insufficient, not
        /// refused - <c>Analytical.Modify.AssignVentilationUnit</c> says at length why. A deliberately
        /// larger capable product is equally accepted and is never reduced to the smallest capable one.
        /// </para>
        /// </summary>
        /// <param name="guid_AirHandlingUnit">Which dwelling's unit.</param>
        /// <param name="ventilationUnitReference">The product's identity.</param>
        /// <param name="refusal">Why nothing changed, where nothing did.</param>
        public bool Assign(Guid guid_AirHandlingUnit, VentilationUnitReference ventilationUnitReference, out string refusal)
        {
            refusal = null;

            PartOEquipmentAssignment partOEquipmentAssignment = assignments.Find(x => x.Guid_AirHandlingUnit == guid_AirHandlingUnit);

            if (partOEquipmentAssignment is null)
            {
                refusal = "That air handling unit is not one of this iteration's dwelling units, so no product was assigned to it.";

                return false;
            }

            if (ventilationUnitReference is null || !ventilationUnitReference.IsValid)
            {
                refusal = string.Format(
                    "No product was assigned to '{0}': the product offered states neither a manufacturer nor a model and so identifies nothing. Its existing assignment is unchanged.",
                    partOEquipmentAssignment.AirHandlingUnitName);

                return false;
            }

            partOEquipmentAssignment.VentilationUnitReference = new VentilationUnitReference(ventilationUnitReference);

            Refresh(partOEquipmentAssignment);

            return true;
        }

        /// <summary>
        /// Takes the suggestion offered for one dwelling. <b>An explicit act</b> - this exists so that
        /// accepting a suggestion is something the engineer does, and never something that happens because a
        /// suggestion was calculated.
        /// </summary>
        public bool AssignSuggested(Guid guid_AirHandlingUnit, out string refusal)
        {
            refusal = null;

            PartOEquipmentAssignment partOEquipmentAssignment = assignments.Find(x => x.Guid_AirHandlingUnit == guid_AirHandlingUnit);

            if (partOEquipmentAssignment?.Suggestion is null)
            {
                refusal = "No capable product was suggested for that dwelling, so there is nothing to accept.";

                return false;
            }

            return Assign(guid_AirHandlingUnit, partOEquipmentAssignment.Suggestion.VentilationUnitReference, out refusal);
        }

        /// <summary>
        /// The products one dwelling's picker offers: the project's permitted set, <b>plus the product
        /// already assigned to this dwelling where it has since left that set</b>.
        /// <para>
        /// Including the outsider is what stops a pool change from quietly making an authored assignment
        /// unpickable - the engineer can still see it, is told it is outside the pool
        /// (<c>PartOEquipmentAssignment.IsOutsideAllowedPool</c>), and decides whether to change it.
        /// </para>
        /// </summary>
        public List<VentilationUnitCapacityDescriptor> Candidates(Guid guid_AirHandlingUnit)
        {
            List<VentilationUnitCapacityDescriptor> result = [.. ventilationUnitCapacityDescriptors_Allowed];

            PartOEquipmentAssignment partOEquipmentAssignment = assignments.Find(x => x.Guid_AirHandlingUnit == guid_AirHandlingUnit);

            if (partOEquipmentAssignment is not null && partOEquipmentAssignment.IsOutsideAllowedPool && partOEquipmentAssignment.Descriptor is not null)
            {
                result.Add(partOEquipmentAssignment.Descriptor);
            }

            return result;
        }

        /// <summary>
        /// Restates which products the project permits, and re-evaluates the derived columns.
        /// <para>
        /// <b>No dwelling is reassigned.</b> Narrowing the pool cannot delete or replace an authored
        /// assignment; it can only mark one as outside the pool. That is the whole of the contract between a
        /// procurement decision and an authored design.
        /// </para>
        /// </summary>
        public void SetAllowedVentilationUnitReferences(IEnumerable<VentilationUnitReference> ventilationUnitReferences_Allowed)
        {
            EquipmentSelection = new PartOEquipmentSelection(EquipmentSelection.Mode, ventilationUnitReferences_Allowed);

            Refresh();
        }

        /// <summary>Restates the selection mode, and re-evaluates the derived columns. Reassigns nothing.</summary>
        public void SetMode(PartOEquipmentSelectionMode partOEquipmentSelectionMode)
        {
            EquipmentSelection = new PartOEquipmentSelection(partOEquipmentSelectionMode, EquipmentSelection.AllowedVentilationUnitReferences);

            Refresh();
        }

        /// <summary>
        /// Writes the changed rows onto the model - the only thing here that touches one.
        /// <para>
        /// <b>Changed rows only.</b> A converted-to-manual table has changed no identity, so it commits
        /// nothing and cannot perturb a model by being adopted. Each write goes through
        /// <c>Analytical.Modify.AssignVentilationUnit</c>, which resolves the cluster's own unit by guid and
        /// moves no airflow.
        /// </para>
        /// </summary>
        /// <returns>True where every changed row was written, or where there was nothing to write.</returns>
        public bool Commit(AdjacencyCluster adjacencyCluster, out List<string> notes, out List<string> refusals)
        {
            notes = [];
            refusals = [];

            if (adjacencyCluster is null)
            {
                refusals.Add("No model was supplied, so no equipment assignment was written.");

                return false;
            }

            bool result = true;

            foreach (PartOEquipmentAssignment partOEquipmentAssignment in assignments)
            {
                if (!partOEquipmentAssignment.HasChanged)
                {
                    continue;
                }

                //Resolved by guid out of the cluster rather than held from build time: the cluster is what
                //everything downstream reads, and Modify.AssignVentilationUnit refuses a detached unit for
                //exactly this reason.
                AirHandlingUnit airHandlingUnit = adjacencyCluster.GetObject<AirHandlingUnit>(partOEquipmentAssignment.Guid_AirHandlingUnit);

                if (airHandlingUnit is null)
                {
                    refusals.Add(string.Format(
                        "Air handling unit '{0}' is no longer in this model, so the product assigned to it could not be written.",
                        partOEquipmentAssignment.AirHandlingUnitName));

                    result = false;

                    continue;
                }

                if (adjacencyCluster.AssignVentilationUnit(airHandlingUnit, partOEquipmentAssignment.VentilationUnitReference, out List<string> notes_Assign, out List<string> refusals_Assign))
                {
                    notes.AddRange(notes_Assign);
                }
                else
                {
                    refusals.AddRange(refusals_Assign);

                    result = false;
                }
            }

            return result;
        }

        /// <summary>
        /// The table in one line, for the preparation summary: how the products were chosen, how many
        /// dwellings are assigned, and how many have something an engineer needs to look at.
        /// </summary>
        public string Description
        {
            get
            {
                int assigned = 0;
                int insufficient = 0;
                int unknown = 0;
                int unassigned = 0;
                int outsidePool = 0;

                foreach (PartOEquipmentAssignment partOEquipmentAssignment in assignments)
                {
                    switch (partOEquipmentAssignment.Status)
                    {
                        case PartOEquipmentAssignmentStatus.Ok:
                            assigned++;
                            break;

                        case PartOEquipmentAssignmentStatus.Insufficient:
                            assigned++;
                            insufficient++;
                            break;

                        case PartOEquipmentAssignmentStatus.CapacityUnknown:
                            assigned++;
                            unknown++;
                            break;

                        default:
                            unassigned++;
                            break;
                    }

                    if (partOEquipmentAssignment.IsOutsideAllowedPool)
                    {
                        outsidePool++;
                    }
                }

                List<string> descriptions = [];

                if (insufficient != 0)
                {
                    descriptions.Add(string.Format("{0} cannot meet its design duty", insufficient));
                }

                if (unknown != 0)
                {
                    descriptions.Add(string.Format("{0} has a capacity the current catalogue cannot resolve", unknown));
                }

                if (unassigned != 0)
                {
                    descriptions.Add(string.Format("{0} has no product assigned", unassigned));
                }

                if (outsidePool != 0)
                {
                    descriptions.Add(string.Format("{0} is assigned a product outside the current allowed pool", outsidePool));
                }

                return string.Format(
                    "Equipment selection: {0}. {1} of {2} dwelling(s) have a product assigned.{3} A product's maximum is its capability ceiling and is never a design airflow.",
                    Core.Query.Description(EquipmentSelection.Mode),
                    assigned,
                    assignments.Count,
                    descriptions.Count == 0 ? string.Empty : string.Format(" Of those, {0}.", string.Join("; ", descriptions)));
            }
        }

        /// <summary>Re-evaluates every row's derived columns. See the class remarks for the cost.</summary>
        private void Refresh()
        {
            ventilationUnitCapacityDescriptors_Allowed = EquipmentSelection.AllowedDescriptors(ventilationUnitCapacityDescriptors);

            foreach (PartOEquipmentAssignment partOEquipmentAssignment in assignments)
            {
                Refresh(partOEquipmentAssignment);
            }
        }

        /// <summary>Re-evaluates one row's derived columns - capability, pool membership and suggestion.</summary>
        private void Refresh(PartOEquipmentAssignment partOEquipmentAssignment)
        {
            partOEquipmentAssignment.Descriptor = Descriptor(partOEquipmentAssignment.VentilationUnitReference);

            partOEquipmentAssignment.IsOutsideAllowedPool = partOEquipmentAssignment.IsAssigned && !IsAllowed(partOEquipmentAssignment.VentilationUnitReference);

            //A suggestion is offered only where the assignment cannot do the job. Never where it can - a
            //dwelling that works does not need to be told a different box exists, and offering one would
            //read as a correction of a sound decision.
            partOEquipmentAssignment.Suggestion = partOEquipmentAssignment.Status == PartOEquipmentAssignmentStatus.Insufficient
                ? ventilationUnitCapacityDescriptors_Allowed.SelectSmallestCapableVentilationUnit(partOEquipmentAssignment.DesignSupplyDuty_Lps, partOEquipmentAssignment.DesignExtractDuty_Lps).Descriptor
                : null;
        }

        /// <summary>
        /// Whether a product is one the project permits. Asked of the allowed list rather than of
        /// <c>PartOEquipmentSelection.IsAllowed</c>, so that a permitted identity the current catalogue does
        /// not hold is not silently reported as pickable.
        /// </summary>
        private bool IsAllowed(VentilationUnitReference ventilationUnitReference)
        {
            return ventilationUnitCapacityDescriptors_Allowed.Find(x => ventilationUnitReference.Matches(x.VentilationUnitReference)) is not null;
        }

        /// <summary>What a product can move, by one dictionary probe. Null is "unknown", never a pass.</summary>
        private VentilationUnitCapacityDescriptor Descriptor(VentilationUnitReference ventilationUnitReference)
        {
            if (ventilationUnitReference is null || !ventilationUnitReference.IsValid)
            {
                return null;
            }

            return dictionary_Descriptor.TryGetValue(Key(ventilationUnitReference), out VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor)
                ? ventilationUnitCapacityDescriptor
                : null;
        }

        private void Index(VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor)
        {
            string key = Key(ventilationUnitCapacityDescriptor.VentilationUnitReference);

            if (!dictionary_Descriptor.TryGetValue(key, out VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor_Existing))
            {
                dictionary_Descriptor[key] = ventilationUnitCapacityDescriptor;

                return;
            }

            //Already conflicted, or an exact repeat that answers the same question the same way.
            if (ventilationUnitCapacityDescriptor_Existing is null)
            {
                return;
            }

            if (ventilationUnitCapacityDescriptor.MaximumSupplyFlowRate_Lps != ventilationUnitCapacityDescriptor_Existing.MaximumSupplyFlowRate_Lps
                || ventilationUnitCapacityDescriptor.MaximumExtractFlowRate_Lps != ventilationUnitCapacityDescriptor_Existing.MaximumExtractFlowRate_Lps
                || ventilationUnitCapacityDescriptor.Rank != ventilationUnitCapacityDescriptor_Existing.Rank)
            {
                //One identity, two meanings. Unknown, so that a unit's adequacy cannot depend on the order
                //the catalogue was read in.
                dictionary_Descriptor[key] = null;
            }
        }

        /// <summary>
        /// The three identity fields, joined by a separator a product name cannot contain.
        /// <para>
        /// The separator is load-bearing rather than cosmetic. Joined with a space, manufacturer
        /// "Nuaire Group" model "X" and manufacturer "Nuaire" model "Group X" would key the same, and two
        /// genuinely different products would silently share one capacity. So two references share a key
        /// exactly where <c>VentilationUnitReference.Compare</c> calls them equal - which is what makes this
        /// index agree with <c>Matches</c>, and they have to agree or a product could be pickable and
        /// unresolvable at the same time.
        /// </para>
        /// </summary>
        private static string Key(VentilationUnitReference ventilationUnitReference)
        {
            return string.Concat(
                ventilationUnitReference.Manufacturer ?? string.Empty,
                "\u0000",
                ventilationUnitReference.Model ?? string.Empty,
                "\u0000",
                ventilationUnitReference.Reference ?? string.Empty);
        }
    }
}
