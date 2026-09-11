// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Persists one <b>successful</b> Approved Document O Iteration 3 (A/B) review as a report beside
        /// the pairing record it belongs to: the readable <c>&lt;run&gt;-Iteration3-Review.txt</c> and its
        /// structured sibling <c>&lt;run&gt;-Iteration3-Review.json</c>.
        ///
        /// <para><b>Only a completed pairing writes, and that is the whole overwrite rule</b></para>
        /// <para>
        /// A refused review writes nothing at all. That is not tidiness: once Reference A's design state
        /// moves, live Review correctly refuses the pairing - and if a refusal still wrote a report, the
        /// last report that actually described a real A/B comparison would be replaced by one that
        /// describes a refusal. So the previous successful report survives every later refusal, and the
        /// window offers it explicitly as <b>historical</b> rather than as the current answer.
        /// </para>
        ///
        /// <para><b>It persists the review, and produces no engineering of its own</b></para>
        /// <para>
        /// Both files are composed from <see cref="PartOIteration3ReportText"/> and
        /// <see cref="PartOIteration3ReportJson"/> over the result the review already computed. Nothing
        /// here reads a model, a results file or a TAS document, and no statistic is recalculated.
        /// </para>
        ///
        /// <para><b>Deterministic</b></para>
        /// <para>
        /// Invariant culture, every row rather than whatever filter a window happened to hold, and the
        /// comparison's own identity ordering - so re-saving an unchanged completed pairing produces the
        /// same bytes.
        /// </para>
        ///
        /// <para><b>Best effort, and a failure says so</b></para>
        /// <para>
        /// A read-only directory or a locked file must not fail a review that already succeeded, so the
        /// failure is recorded on the result - which puts it in the window's notes - rather than thrown.
        /// </para>
        /// </summary>
        /// <param name="partOIteration3Result">
        /// The review to persist. Its <c>RecordReport</c> is called either way, so the window can say
        /// where the report is or why there is none.
        /// </param>
        /// <returns>Whether a report was written.</returns>
        internal static bool SavePartOIteration3Report(PartOIteration3Result partOIteration3Result)
        {
            if (partOIteration3Result is null)
            {
                return false;
            }

            if (!partOIteration3Result.IsComplete)
            {
                //Deliberately silent. A refusal is not a failed save - there is nothing to save, and the
                //last successful report of this pairing is left exactly where it is.
                partOIteration3Result.RecordReport(null, null, null);

                return false;
            }

            string path_Report = PartOIteration3Paths.Path_Report_ForRecord(partOIteration3Result.Path_Record);
            string path_Report_Json = PartOIteration3Paths.Path_Report_ForRecord(partOIteration3Result.Path_Record, "json");

            if (string.IsNullOrWhiteSpace(path_Report))
            {
                partOIteration3Result.RecordReport(
                    null,
                    null,
                    "This pairing states no record path, so no report path could be derived from it and the A/B review was not saved.");

                return false;
            }

            List<PartOIteration3Row> rows = PartOIteration3Row.Rows(partOIteration3Result.Comparison);

            //Recorded BEFORE the write, so the report names itself: a file found on its own, a year
            //later, states which file it is as well as which pairing it describes. The paths are derived
            //from the record and do not vary between saves, so this costs no determinism.
            partOIteration3Result.RecordReport(path_Report, path_Report_Json, null);

            try
            {
                File.WriteAllText(path_Report, PartOIteration3ReportText.Text(partOIteration3Result, rows, CultureInfo.InvariantCulture, true));
                File.WriteAllText(path_Report_Json, PartOIteration3ReportJson.Text(partOIteration3Result, rows));
            }
            catch (Exception exception)
            {
                partOIteration3Result.RecordReport(
                    null,
                    null,
                    string.Format("The Iteration 3 A/B review report could not be written to '{0}': {1}", path_Report, exception.Message));

                return false;
            }

            return true;
        }
    }
}
