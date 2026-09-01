// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Gives a space the TAS zone identity (<c>SAM.Analytical.Tas.SpaceParameter.ZoneGuid</c>) of the
        /// simulated space it is named after - <b>but only where it does not already have one</b>.
        /// <para>
        /// <b>An existing stamp is authoritative and is never touched.</b>
        /// <c>WorkflowCalculator.Calculate</c> has already stamped every space through
        /// <c>SAM.Analytical.Tas.Modify.UpdateIds</c>, which resolves a space to its zone by the guid it
        /// captured before clearing - preferring that over the name, and refusing rather than guessing where
        /// neither identity matches (<c>Query.ResolvedZone</c>). Overwriting that from here replaced a strong
        /// identity with a weak one, and did so silently.
        /// </para>
        /// <para>
        /// <b>Why the name match survives at all - and it is not a refusal.</b> A model can reach
        /// <see cref="Simulate(UIAnalyticalModel)"/>'s TBD block without having gone through the workflow:
        /// tick "Domestic Overheating" with <b>Simulate</b> unticked and the .tbd is written by
        /// <c>Tas.Convert.ToTBD</c>. That call is made with <c>updateGuids: true</c>, but
        /// <c>AnalyticalModel.AdjacencyCluster</c> hands out a <i>copy</i>, so the zone guids
        /// <c>Tas.Modify.Update</c> stamps go onto a throwaway cluster and never reach the model held here.
        /// The model therefore arrives unstamped.
        /// </para>
        /// <para>
        /// <c>Tas.TM59.Convert.ToXml</c> does <b>not</b> refuse such a space.
        /// <c>Tas.TM59.Convert.ToTM59(Space, TM59Manager, SystemType)</c> falls back to <c>space.Guid</c>
        /// when <c>ZoneGuid</c> is absent or empty
        /// (<c>SAM.Analytical.Tas.TM59/Convert/ToTM59/Zone.cs</c>), and a SAM space guid is not a TAS zone
        /// guid - a TBD zone's guid is minted by <c>building.AddZone()</c> and has no relationship to the
        /// space it was written from. So without this fill the DomOv XML exports successfully and silently
        /// names zones the external TAS TM59 tool cannot find, which is worse than a refusal, not better.
        /// <c>SimulationZoneIdentityTests</c> pins both halves: the fallback that happens without the fill,
        /// and the TAS identity that appears with it.
        /// </para>
        /// <para>
        /// <b>Only the DomOv export depends on this.</b> "Create SAP" is handed
        /// <c>analyticalModel_TBD</c> - the model read back out of the .tbd, whose spaces
        /// <c>Tas.Convert.ToSAM</c> stamps itself - and "Create Part L"
        /// (<c>Tas.Create.TBD_ByPartL</c> -&gt; <c>UpdateInternalConditionByPartL</c> /
        /// <c>UpdateZoneGroupsByPartL</c> / <c>UpdateZoneGroups</c>) reads no <c>ZoneGuid</c> at all. Neither
        /// is a reason to keep this seam.
        /// </para>
        /// <para>
        /// <b>Ambiguity refuses; it does not take the first hit.</b> Every flat in a block has a "Bedroom 2",
        /// and <c>Find(x =&gt; x.Name == space.Name)</c> answered all three design spaces with one flat's
        /// simulated space - collapsing three identities into one and attributing two dwellings' results to a
        /// third. Two simulated spaces stating the <i>same</i> guid are not a conflict: the same answer twice
        /// is still one answer, the rule <c>VentilationStrategyMap</c> already applies to a repeated claim.
        /// </para>
        /// <para>
        /// <b>The value is copied as a string, verbatim.</b> The previous code read
        /// <c>TryGetValue(ZoneGuid, out Guid)</c> from a parameter declared <c>ParameterType.String</c>, so the
        /// raw TAS value was parsed to a <see cref="Guid"/> and converted back on the way in - which
        /// re-spells it (braces and case are lost to <c>Guid.ToString()</c>). <c>Query.SimulationSpaceKey</c>
        /// compares the stored strings ordinally, so a re-spelt stamp stops matching the TSD side even when
        /// the space it came from was the right one.
        /// </para>
        /// <para>
        /// Pure and static so it can be exercised without an installed TAS - the .tbd read that produces
        /// <paramref name="spaces_Simulation"/> is the only part of the block that needs one.
        /// </para>
        /// </summary>
        /// <param name="spaces_Design">
        /// The model's spaces. Mutated in place where a stamp is written; the caller is responsible for
        /// putting the returned spaces back into the cluster.
        /// </param>
        /// <param name="spaces_Simulation">The spaces read back from the .tbd.</param>
        /// <param name="notes">
        /// One sentence per space that was left unstamped, and why. Never null. A space that already carried a
        /// stamp produces no note - that is the normal case, not an event.
        /// </param>
        /// <returns>The spaces that were written, in the order given. Empty where nothing needed a stamp.</returns>
        internal static List<Space> RestampSimulationZoneIdentity(IEnumerable<Space> spaces_Design, IEnumerable<Space> spaces_Simulation, out List<string> notes)
        {
            notes = [];

            List<Space> result = [];

            if (spaces_Design is null)
            {
                return result;
            }

            //Ordinal, and the exact name: the same comparison Modify.UpdateIds indexes its zones by, so this
            //fallback cannot resolve a pairing that one would have rejected.
            Dictionary<string, List<Space>> dictionary_Simulation = new(StringComparer.Ordinal);

            foreach (Space space_Simulation in spaces_Simulation ?? [])
            {
                if (space_Simulation?.Name is not string name_Simulation || string.IsNullOrWhiteSpace(name_Simulation))
                {
                    continue;
                }

                if (!dictionary_Simulation.TryGetValue(name_Simulation, out List<Space> spaces_Named))
                {
                    spaces_Named = [];
                    dictionary_Simulation[name_Simulation] = spaces_Named;
                }

                spaces_Named.Add(space_Simulation);
            }

            foreach (Space space in spaces_Design)
            {
                if (space is null)
                {
                    continue;
                }

                //The whole point. A stamped space is already identified, by a stronger identity than a name.
                if (space.TryGetValue(Analytical.Tas.SpaceParameter.ZoneGuid, out string zoneGuid_Existing) && !string.IsNullOrWhiteSpace(zoneGuid_Existing))
                {
                    continue;
                }

                string name = space.Name;

                if (string.IsNullOrWhiteSpace(name) || !dictionary_Simulation.TryGetValue(name, out List<Space> spaces_Named) || spaces_Named.Count == 0)
                {
                    notes.Add(string.Format("Space '{0}' carries no TAS zone identity and no simulated space is named after it, so none could be established for it.", name ?? "?"));

                    continue;
                }

                //Collected rather than counted: two simulated spaces naming the same zone are one answer, and
                //only genuinely different answers are ambiguous.
                HashSet<string> zoneGuids = new(StringComparer.Ordinal);
                foreach (Space space_Named in spaces_Named)
                {
                    if (space_Named.TryGetValue(Analytical.Tas.SpaceParameter.ZoneGuid, out string zoneGuid_Named) && !string.IsNullOrWhiteSpace(zoneGuid_Named))
                    {
                        zoneGuids.Add(zoneGuid_Named);
                    }
                }

                if (zoneGuids.Count == 0)
                {
                    notes.Add(string.Format("Space '{0}' carries no TAS zone identity, and the {1} simulated space(s) named after it carry none either.", name, spaces_Named.Count));

                    continue;
                }

                if (zoneGuids.Count != 1)
                {
                    notes.Add(string.Format("Space '{0}' carries no TAS zone identity and its name is shared by {1} simulated spaces stating {2} different zones, so which one it is has not been established. It is left unstamped rather than given one of them - naming the rooms of each dwelling distinctly, or simulating the model so the workflow stamps it, settles this.", name, spaces_Named.Count, zoneGuids.Count));

                    continue;
                }

                foreach (string zoneGuid in zoneGuids)
                {
                    //Written as the string it was read as. See the class note on re-spelling.
                    space.SetValue(Analytical.Tas.SpaceParameter.ZoneGuid, zoneGuid);
                }

                result.Add(space);
            }

            return result;
        }
    }
}
