// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Binds each resolved analytical AHU evidence row to the single AirSystem lineage row produced
        /// by SAM_Systems. Empty means the mapping is complete; any returned reason makes the caller
        /// refuse the materialisation stage.
        /// </summary>
        internal static List<string> PartOIteration3EquipmentBindings(
            IEnumerable<PartOIteration3EquipmentEvidence> equipment,
            IEnumerable<MechanicalVentilationBinding> bindings)
        {
            Dictionary<Guid, Guid> airSystemByAirHandlingUnit = [];
            List<string> result = [];

            foreach (MechanicalVentilationBinding mechanicalVentilationBinding in bindings ?? [])
            {
                if (mechanicalVentilationBinding is null
                    || mechanicalVentilationBinding.BindingType != MechanicalVentilationBindingType.AirSystem)
                {
                    continue;
                }

                if (airSystemByAirHandlingUnit.TryGetValue(mechanicalVentilationBinding.Guid_Analytical, out Guid guid_AirSystem)
                    && guid_AirSystem != mechanicalVentilationBinding.Guid_Systems)
                {
                    result.Add(string.Format(
                        "Air handling unit {0} was bound to more than one materialised air system, so its equipment evidence has no single target.",
                        mechanicalVentilationBinding.Guid_Analytical));

                    continue;
                }

                airSystemByAirHandlingUnit[mechanicalVentilationBinding.Guid_Analytical] = mechanicalVentilationBinding.Guid_Systems;
            }

            foreach (PartOIteration3EquipmentEvidence partOIteration3EquipmentEvidence in equipment ?? [])
            {
                if (partOIteration3EquipmentEvidence is null)
                {
                    result.Add("A resolved equipment row is missing, so it cannot be bound to a materialised air system.");
                    continue;
                }

                if (!airSystemByAirHandlingUnit.TryGetValue(partOIteration3EquipmentEvidence.Guid_AirHandlingUnit, out Guid guid_AirSystem)
                    || !partOIteration3EquipmentEvidence.BindAirSystem(guid_AirSystem))
                {
                    result.Add(string.Format(
                        "Air handling unit '{0}' ({1}) resolved equipment behaviour but has no single materialised air-system binding.",
                        partOIteration3EquipmentEvidence.Name_AirHandlingUnit,
                        partOIteration3EquipmentEvidence.Guid_AirHandlingUnit));
                }
            }

            return result;
        }
    }
}
