// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Where a "Apply best design" change lands in the open model (PR9), or why it cannot be placed. Names are matched
        /// exactly as SAM_Tas writes them into the TBD, and the model must still say what the Tas model says; nothing is
        /// guessed:
        /// <list type="bullet">
        /// <item>Setpoint: exactly one space named as the TBD internal condition (SAM writes one per space, named after
        /// it), with a heating/cooling profile that SAM_Tas would write as exactly the TBD's profile (value profile: the
        /// value; 24-hour profile: all 24 hours; compared as the floats the TBD stores).</item>
        /// <item>Glazing choice: the aperture constructions with apertures whose TBD pane construction is the choice's
        /// glazing construction, by SAM_Tas' gbXML-route naming (<c>Windows: &lt;name&gt; -pane</c>) or the direct
        /// route's preferred naming (<c>&lt;name&gt; -pane</c>). A collision-qualified TBD name cannot be traced back.</item>
        /// </list>
        /// </summary>
        /// <param name="analyticalModel">The open model.</param>
        /// <param name="tasModelDesignChange">A building change (setpoint or glazing choice).</param>
        /// <param name="tasModelInventory">The Tas model the optimisation ran on.</param>
        /// <param name="problem">Why the change cannot be placed; null when it can.</param>
        public static TasModelSamTarget? TasModelSamTarget(AnalyticalModel analyticalModel, TasModelDesignChange tasModelDesignChange, TasModelInventory tasModelInventory, out string? problem)
        {
            problem = null;
            AdjacencyCluster? adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (analyticalModel == null || adjacencyCluster == null || tasModelDesignChange == null)
            {
                problem = "There is no open model.";
                return null;
            }

            if (tasModelDesignChange.IsSetpoint)
            {
                return SetpointTarget(analyticalModel, adjacencyCluster, tasModelDesignChange, tasModelInventory, out problem);
            }

            if (tasModelDesignChange.IsGlazing)
            {
                return GlazingTarget(adjacencyCluster, tasModelDesignChange, out problem);
            }

            problem = "SAM does not hold this item; it is written to the TPD only.";
            return null;
        }

        /// <summary>
        /// The TBD pane construction names SAM_Tas gives an aperture construction: the gbXML route's
        /// (<c>Modify.UpdateConstructions</c>: <c>Windows: &lt;name&gt; -pane</c>) and the direct route's preferred one
        /// (<c>Query.ConstructionName</c>: <c>&lt;name&gt; -pane</c>).
        /// </summary>
        public static List<string> TasPaneConstructionNames(ApertureConstruction apertureConstruction)
        {
            List<string> result = new List<string>();
            if (apertureConstruction == null)
            {
                return result;
            }

            string name = Analytical.Tas.Query.Name(apertureConstruction.UniqueName(), true, true, false, false);
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(Analytical.Query.PaneApertureConstructionUniqueName(name));
            }

            string @base = Analytical.Tas.Query.ConstructionNameBase(apertureConstruction.Name);
            if (!string.IsNullOrWhiteSpace(@base))
            {
                string direct = @base + " " + Analytical.Tas.Query.Sufix(AperturePart.Pane);
                if (!result.Contains(direct, StringComparer.Ordinal))
                {
                    result.Add(direct);
                }
            }

            return result;
        }

        /// <summary>
        /// The profile SAM_Tas would write into the TBD for <paramref name="profile"/> (<c>Modify.Update(profile, Profile, 1)</c>),
        /// compared with the TBD's: a one-value profile is a value profile, a profile of up to 24 values a 24-hour profile
        /// (hours 0-23 by the profile's own indexer), anything longer a yearly profile (not a setpoint the engine changes).
        /// </summary>
        public static bool TasSetpointProfileMatches(Profile profile, TasSetpointProfile tasSetpointProfile, bool heating)
        {
            if (profile == null || tasSetpointProfile == null || tasSetpointProfile.Setpoint == null || tasSetpointProfile.Factor != 1)
            {
                return false;
            }

            int count = profile.Count;
            if (count == 1)
            {
                double[] values = profile.GetValues();
                return tasSetpointProfile.Type == TasSetpointProfileType.Value && values != null && values.Length > 0 && (float)values[0] == (float)tasSetpointProfile.Setpoint.Value;
            }

            if (count < 1 || count > 24 || tasSetpointProfile.Type != TasSetpointProfileType.Hourly)
            {
                return false;
            }

            float[] hours = TasHours(profile).Select(x => (float)x).ToArray();
            if (tasSetpointProfile.Hours != null)
            {
                return hours.SequenceEqual(tasSetpointProfile.Hours);
            }

            float setpoint = heating ? hours.Max() : hours.Min();
            return setpoint == (float)tasSetpointProfile.Setpoint.Value && hours.Count(x => x == setpoint) == tasSetpointProfile.ChangedHours;
        }

        /// <summary>Hours 0-23 of a profile as SAM_Tas reads them into a 24-hour TBD profile (the profile's indexer).</summary>
        internal static double[] TasHours(Profile profile)
        {
            return Enumerable.Range(0, 24).Select(i => profile[i]).ToArray();
        }

        private static TasModelSamTarget? SetpointTarget(AnalyticalModel analyticalModel, AdjacencyCluster adjacencyCluster, TasModelDesignChange change, TasModelInventory tasModelInventory, out string? problem)
        {
            string name = change.InternalCondition;
            string what = change.IsHeating ? "heating" : "cooling";
            ProfileType profileType = change.IsHeating ? ProfileType.Heating : ProfileType.Cooling;

            List<Space> spaces = (adjacencyCluster.GetSpaces() ?? new List<Space>()).FindAll(x => string.Equals(x?.Name, name, StringComparison.Ordinal));
            if (spaces.Count != 1)
            {
                problem = spaces.Count == 0
                    ? "The open model has no space named “" + name + "”, so it is not the model the Tas files were made from."
                    : string.Format(CultureInfo.InvariantCulture, "The open model has {0} spaces named “{1}”, so it cannot tell which one the Tas internal condition is.", spaces.Count, name);
                return null;
            }

            Space space = spaces[0];
            InternalCondition? internalCondition = space.InternalCondition;
            ProfileLibrary? profileLibrary = analyticalModel.ProfileLibrary;
            Profile? profile = internalCondition?.GetProfile(profileType, profileLibrary);
            if (profile == null)
            {
                problem = "Space “" + name + "” has no " + what + " profile in the open model.";
                return null;
            }

            List<TasInternalConditionInfo> infos = tasModelInventory?.InternalConditions.Where(x => x.Name == name).ToList() ?? new List<TasInternalConditionInfo>();
            TasInternalConditionInfo? info = infos.Count == 1 ? infos[0] : null;
            TasSetpointProfile? tasSetpointProfile = change.IsHeating ? info?.Heating : info?.Cooling;
            if (!TasSetpointProfileMatches(profile, tasSetpointProfile!, change.IsHeating))
            {
                problem = "The " + what + " profile “" + profile.Name + "” of space “" + name + "” is not the Tas model's " + what + " setpoint: the model has changed since its last Energy Simulation. Run Energy Simulation, then the optimisation again.";
                return null;
            }

            // Who else uses this profile: any space, by any profile type that resolves to it.
            string id = ProfileLibrary.UniqueId(profile);
            int users = 0;
            foreach (Space space_Other in adjacencyCluster.GetSpaces() ?? new List<Space>())
            {
                InternalCondition? internalCondition_Other = space_Other?.InternalCondition;
                Dictionary<ProfileType, string>? names = internalCondition_Other?.GetProfileTypeDictionary();
                if (names == null)
                {
                    continue;
                }

                foreach (KeyValuePair<ProfileType, string> pair in names)
                {
                    Profile? profile_Other = profileLibrary?.GetProfile(pair.Value, pair.Key, true);
                    if (profile_Other != null && ProfileLibrary.UniqueId(profile_Other) == id)
                    {
                        users++;
                    }
                }
            }

            problem = null;
            return new TasModelSamTarget(space, profileType, profile, users != 1);
        }

        private static TasModelSamTarget? GlazingTarget(AdjacencyCluster adjacencyCluster, TasModelDesignChange change, out string? problem)
        {
            string glazingConstruction = change.GlazingConstruction;
            List<ApertureConstruction> matches = new List<ApertureConstruction>();
            int apertures = 0;
            HashSet<Guid> guids = new HashSet<Guid>();
            foreach (ApertureConstruction apertureConstruction in adjacencyCluster.GetApertureConstructions() ?? new List<ApertureConstruction>())
            {
                if (apertureConstruction == null || !guids.Add(apertureConstruction.Guid))
                {
                    continue;
                }

                if (!TasPaneConstructionNames(apertureConstruction).Contains(glazingConstruction, StringComparer.Ordinal))
                {
                    continue;
                }

                int count = adjacencyCluster.GetApertures(apertureConstruction)?.Count ?? 0;
                if (count == 0)
                {
                    continue;
                }

                matches.Add(apertureConstruction);
                apertures += count;
            }

            if (matches.Count == 0)
            {
                problem = "No aperture construction of the open model is the Tas glazing “" + glazingConstruction + "”, so the open model is not the one the Tas files were made from (or the TBD names it in a way SAM cannot trace back).";
                return null;
            }

            problem = null;
            return new TasModelSamTarget(matches, apertures);
        }
    }
}
