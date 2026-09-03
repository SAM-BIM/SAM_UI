// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Persists one run's TM59 report beside the results it was assessed from - the durable evidence of
        /// THAT run: the baseline's, an optimisation round's, or the capacity envelope's, each at its own
        /// path (<see cref="Query.Path_TM59Report"/>).
        /// <para>
        /// <b>Best effort, and a failure says so.</b> The report is evidence, not part of the run: a
        /// read-only directory or a locked file must not fail an assessment that already succeeded, so the
        /// failure is handed back for the caller to record rather than thrown.
        /// </para>
        /// </summary>
        /// <param name="path_TSD">The run's own results file - what the report is named and sited from.</param>
        /// <param name="tM59AssessmentReport">The assessment's own report, written verbatim.</param>
        /// <param name="path_TM59Report">Where the report was written, or where it would have been.</param>
        /// <param name="refusal">Why no report was written, or null where one was.</param>
        internal static bool SavePartOTM59Report(string path_TSD, TM59AssessmentReport tM59AssessmentReport, out string path_TM59Report, out string refusal)
        {
            path_TM59Report = Query.Path_TM59Report(path_TSD);
            refusal = null;

            if (path_TM59Report is null)
            {
                refusal = "The run's results path is not one a TM59 report path can be derived from, so the report was not saved.";

                return false;
            }

            if (tM59AssessmentReport is null)
            {
                refusal = "There is no TM59 report to save.";

                return false;
            }

            try
            {
                File.WriteAllText(path_TM59Report, tM59AssessmentReport.ToString());

                return true;
            }
            catch (Exception exception)
            {
                refusal = string.Format("The TM59 report could not be written to '{0}': {1}", path_TM59Report, exception.Message);

                return false;
            }
        }
    }
}
