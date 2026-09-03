// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// Everything <c>SAM.Analytical.Modify.PreparePartOIteration</c> was given for a run, kept so the
    /// <b>same</b> preparation can be repeated over a changed design without asking the user again.
    /// <para>
    /// <b>Why an optimisation needs this at all.</b> Iteration 2B changes design airflow and then has to
    /// rebuild the Part O analytical state around it - the transfer air movements, the network, the unit
    /// duties - by re-preparing. Re-preparing with a different route, a different dwelling scope or a
    /// different catalogue would make each iteration a different engineering case, and the TM59 results
    /// across the run would no longer be comparable. This is how "the same case throughout" is enforced
    /// rather than hoped for.
    /// </para>
    /// <para>
    /// <b>Not the design.</b> Nothing here is an airflow, a requirement or a capacity - it is the set of
    /// choices a person made in the preparation dialog. The design the preparation runs over is whatever
    /// model it is handed, which is exactly what an optimisation round changes between iterations.
    /// </para>
    /// <para>
    /// <b>The catalogue is carried, never re-read.</b> An optimisation must be checked against the products
    /// the run actually started with; re-reading the catalogue mid-run could change what the selected unit
    /// is understood to be rated at, halfway through.
    /// </para>
    /// </summary>
    public class PartOPreparationContext
    {
        /// <param name="partOIteration">The base provision the run is defined over.</param>
        /// <param name="zones">The dwelling zones in scope, as <c>Query.PartFDwellingZones</c> returned them.</param>
        /// <param name="dictionary_VentilationStrategy">The canonical ventilation route stated per zone.</param>
        /// <param name="ventilationUnitCapacityDescriptors">
        /// The products offered to selection, or null for a run with no equipment selection - the same
        /// distinction the preparation itself reads, where null means Iteration 1a and an empty list means a
        /// catalogue that offers nothing.
        /// </param>
        public PartOPreparationContext(PartOIteration partOIteration, IEnumerable<Zone> zones, Dictionary<Guid, string> dictionary_VentilationStrategy, IEnumerable<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors)
        {
            PartOIteration = partOIteration;

            foreach (Zone zone in zones ?? [])
            {
                if (zone is not null)
                {
                    Zones.Add(zone);
                }
            }

            foreach (KeyValuePair<Guid, string> keyValuePair in dictionary_VentilationStrategy ?? [])
            {
                VentilationStrategies[keyValuePair.Key] = keyValuePair.Value;
            }

            if (ventilationUnitCapacityDescriptors is not null)
            {
                VentilationUnitCapacityDescriptors = [];

                foreach (VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor in ventilationUnitCapacityDescriptors)
                {
                    if (ventilationUnitCapacityDescriptor is not null)
                    {
                        VentilationUnitCapacityDescriptors.Add(ventilationUnitCapacityDescriptor);
                    }
                }
            }
        }

        /// <summary>The base provision - Iteration 1a or 1b - the run is defined over.</summary>
        public PartOIteration PartOIteration { get; }

        /// <summary>The dwelling zones in scope.</summary>
        public List<Zone> Zones { get; } = [];

        /// <summary>The canonical ventilation route stated for each of them.</summary>
        public Dictionary<Guid, string> VentilationStrategies { get; } = [];

        /// <summary>
        /// The products offered to selection. <b>Null means no equipment selection ran</b> - which is
        /// Iteration 1a, and is not an Iteration 2B starting point.
        /// </summary>
        public List<VentilationUnitCapacityDescriptor> VentilationUnitCapacityDescriptors { get; }

        /// <summary>Whether a product catalogue was offered at all - the Iteration 2 precondition for 2B.</summary>
        public bool HasVentilationUnitCatalogue => VentilationUnitCapacityDescriptors is not null && VentilationUnitCapacityDescriptors.Count != 0;

        /// <summary>
        /// The Iteration 2B optimisation this run was set up to allow afterwards, or null where none was
        /// asked for.
        /// <para>
        /// <b>Not a preparation input.</b> The preparation neither reads it nor is affected by it - Iteration
        /// 2B is an optimisation performed ON an Iteration 2 design, never a base provision, and it is
        /// deliberately not one of the choices in the base-provision list. It rides here because it is a
        /// choice made at the same moment, about the same run, and has to survive until there are results to
        /// optimise from.
        /// </para>
        /// </summary>
        public PartOOptimisationSettings OptimisationSettings { get; set; }

        /// <summary>
        /// Whether this run's thermal model is the <b>isolated</b> derived model of the dwellings in scope
        /// rather than the whole building.
        /// <para>
        /// <b>Recorded, not re-applied.</b> An Iteration 2B round re-prepares the model the previous round
        /// left behind, and that model is already isolated - so a round must NOT isolate again. Doing so
        /// would rebuild the cut and the shading context on a model that already has them, and the geometry
        /// would no longer be bit-for-bit what the canonical TBD was converted from, turning the warm start
        /// off for every round. This says what the run is, so a report can state its scope; the isolated
        /// geometry itself is carried forward by the model.
        /// </para>
        /// <para>
        /// The authority for whether a given MODEL is isolated remains
        /// <c>AnalyticalModelParameter.PartOIsolationContext</c> stamped on the model, which is what
        /// survives into the run's <c>.sam</c> and back out of it in a later session. This is the session's
        /// record of what was asked for.
        /// </para>
        /// </summary>
        public bool Isolated { get; set; }
    }
}
