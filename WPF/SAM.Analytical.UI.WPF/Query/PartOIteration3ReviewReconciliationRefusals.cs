// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The reconciliation a <b>review</b> can still make: that the two reassessed results files agree
        /// with the recorded pairing about which rooms were compared and against which TM59 criteria.
        ///
        /// <para><b>Why this is narrower than the run's reconciliation, and why that is correct</b></para>
        /// <para>
        /// The run's reconciliation cross-checks the route's design airflows and transfer topology against
        /// the prepared design itself - and it is the only thing that can, because the prepared design
        /// exists only while the session that prepared it is open. A review has the record, the reopened
        /// Reference A model and two results files. Re-deriving a design cross-check from those would be
        /// asking a different question of different inputs and calling it the same check.
        /// </para>
        /// <para>
        /// So the run's answer is not recomputed: the record carries it, the design-state fingerprint
        /// proves the design has not moved since, and the file fingerprints prove the results have not.
        /// What a review re-establishes is the part that depends on reading the files again - that the
        /// rooms and criteria still come back the same, so the statistics rebuilt from them are the same
        /// statistics.
        /// </para>
        /// <para>
        /// <b>Fail closed, same as the run.</b> Any disagreement refuses the whole review rather than
        /// comparing the rooms that still agree.
        /// </para>
        /// </summary>
        internal static List<string> PartOIteration3ReviewReconciliationRefusals(
            PartOIteration3Record partOIteration3Record,
            PartOIteration3Assessment partOIteration3Assessment_A,
            PartOIteration3Assessment partOIteration3Assessment_B,
            Dictionary<Guid, PartOIteration3Room> dictionary_Room,
            out List<PartOIteration3Room> rooms_Comparable,
            out List<PartOIteration3CriterionComparison> criteria)
        {
            List<string> result = [];

            rooms_Comparable = [];
            criteria = [];

            if (partOIteration3Record is null || partOIteration3Assessment_A is null || partOIteration3Assessment_B is null)
            {
                result.Add("The Iteration 3 review was asked to reconcile something that is not there.");

                return result;
            }

            Dictionary<Guid, Dictionary<string, PartOTM59SpaceResult>> dictionary_A = Criteria(partOIteration3Assessment_A, result, "Reference A");
            Dictionary<Guid, Dictionary<string, PartOTM59SpaceResult>> dictionary_B = Criteria(partOIteration3Assessment_B, result, "Candidate B");

            List<Guid> guids = [.. dictionary_Room.Keys];
            guids.Sort();

            foreach (Guid guid_Space in guids)
            {
                PartOIteration3Room partOIteration3Room = dictionary_Room[guid_Space];

                bool assessed_A = dictionary_A.TryGetValue(guid_Space, out Dictionary<string, PartOTM59SpaceResult> criteria_A);
                bool assessed_B = dictionary_B.TryGetValue(guid_Space, out Dictionary<string, PartOTM59SpaceResult> criteria_B);

                if (!assessed_A || !assessed_B)
                {
                    result.Add(string.Format(
                        "Room '{0}' ({1}) was compared when this pairing was produced and now produces a TM59 result in {2} only, so the existing results no longer describe the pairing.",
                        partOIteration3Room.Name_Space,
                        guid_Space,
                        assessed_A ? "Reference A" : "Candidate B"));

                    continue;
                }

                if (partOIteration3Assessment_A.ResultantTemperature(guid_Space) is null || partOIteration3Assessment_B.ResultantTemperature(guid_Space) is null)
                {
                    result.Add(string.Format(
                        "Room '{0}' ({1}) no longer carries a resultant temperature series on both sides, so its comparison cannot be rebuilt.",
                        partOIteration3Room.Name_Space,
                        guid_Space));

                    continue;
                }

                bool matched = true;

                foreach (KeyValuePair<string, PartOTM59SpaceResult> keyValuePair in criteria_A)
                {
                    if (!criteria_B.ContainsKey(keyValuePair.Key))
                    {
                        result.Add(string.Format("Room '{0}' ({1}) is assessed against TM59 criterion '{2}' in Reference A and not in Candidate B.", partOIteration3Room.Name_Space, guid_Space, keyValuePair.Key));

                        matched = false;
                    }
                }

                foreach (KeyValuePair<string, PartOTM59SpaceResult> keyValuePair in criteria_B)
                {
                    if (!criteria_A.ContainsKey(keyValuePair.Key))
                    {
                        result.Add(string.Format("Room '{0}' ({1}) is assessed against TM59 criterion '{2}' in Candidate B and not in Reference A.", partOIteration3Room.Name_Space, guid_Space, keyValuePair.Key));

                        matched = false;
                    }
                }

                if (!matched)
                {
                    continue;
                }

                rooms_Comparable.Add(partOIteration3Room);

                foreach (KeyValuePair<string, PartOTM59SpaceResult> keyValuePair in criteria_A)
                {
                    PartOTM59SpaceResult partOTM59SpaceResult_A = keyValuePair.Value;
                    PartOTM59SpaceResult partOTM59SpaceResult_B = criteria_B[keyValuePair.Key];

                    criteria.Add(new PartOIteration3CriterionComparison(
                        guid_Space,
                        partOIteration3Room.Name_Space,
                        partOIteration3Room.Guid_Dwelling,
                        partOIteration3Room.Name_Dwelling,
                        keyValuePair.Key,
                        partOTM59SpaceResult_A.Mechanical,
                        partOTM59SpaceResult_A.Actual,
                        partOTM59SpaceResult_A.Limit,
                        partOTM59SpaceResult_A.ComplianceStatus,
                        partOTM59SpaceResult_B.Actual,
                        partOTM59SpaceResult_B.Limit,
                        partOTM59SpaceResult_B.ComplianceStatus));
                }
            }

            if (result.Count != 0)
            {
                rooms_Comparable.Clear();
                criteria.Clear();
            }

            return result;
        }
    }
}
