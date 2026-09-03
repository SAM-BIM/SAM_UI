// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.IO;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Where one run's persisted CIBSE TM59 report lives: beside its own results file, named from it -
        /// <c>&lt;project&gt;-TM59.txt</c> for a baseline, <c>&lt;project&gt;-Opt01-TM59.txt</c> for a round,
        /// <c>&lt;project&gt;-OptMax-TM59.txt</c> for the capacity envelope.
        /// <para>
        /// <b>The one naming authority.</b> Every writer - the interactive assessment command and the
        /// Iteration 2B loop, baseline and envelope included - derives the report path here, from the run's
        /// own TSD and nothing else, so a report always lands beside the results it was assessed from and no
        /// run can overwrite another's: the per-iteration naming
        /// (<see cref="PartOSimulationContext.ProjectName_Iteration(int)"/>) is what keeps the TSDs apart,
        /// and this inherits it.
        /// </para>
        /// </summary>
        /// <param name="path_TSD">The run's own results file.</param>
        /// <returns>The report path, or null where there is no results path to derive one from.</returns>
        internal static string Path_TM59Report(string path_TSD)
        {
            if (string.IsNullOrWhiteSpace(path_TSD))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(path_TSD);
            string fileName = Path.GetFileNameWithoutExtension(path_TSD);

            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            return Path.Combine(directory, fileName + "-TM59.txt");
        }
    }
}
