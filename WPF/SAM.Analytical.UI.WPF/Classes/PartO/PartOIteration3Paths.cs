// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Where every file of one Approved Document O Iteration 3 pairing lives - <b>the one naming
    /// authority</b>, derived from Reference A's own output directory and project name and from nothing
    /// else.
    ///
    /// <para><b>Deterministic, so the pairing is reopenable</b></para>
    /// <para>
    /// A review in a later session has to find Candidate B's artifacts and prove they are the ones the run
    /// produced. That is only possible if the run and the review derive the same paths from the same two
    /// facts - so both ask this, exactly as every writer and reader of the per-run model already asks
    /// <see cref="Query.Path_PartORunModel(string)"/> and every TM59 report asks
    /// <see cref="Query.Path_TM59Report(string)"/>.
    /// </para>
    ///
    /// <para><b>And therefore stale-prone, which is handled rather than avoided</b></para>
    /// <para>
    /// Deterministic paths mean a failed attempt leaves its files exactly where the next attempt will
    /// look. That is not solved by randomising the names - a random name is not reopenable - but by
    /// proving ownership: <see cref="PartOIteration3Artifacts"/> fingerprints every path here before the
    /// attempt starts, and a file that has not changed since is never reported as this attempt's. The
    /// previous Candidate B model is additionally <b>deleted</b> at attempt start, because a reopenable
    /// <c>.sam</c> left behind by a failed attempt is the one artifact a later session would act on.
    /// </para>
    ///
    /// <para><b>The suffix is fixed, and is not an optimisation round</b></para>
    /// <para>
    /// Candidate B is <c>&lt;project&gt;-It3B</c> and the bridge is <c>&lt;project&gt;-It3B-Bridge</c>.
    /// Deliberately not a <c>-Opt</c><i>nn</i> name: those belong to Iteration 2B's rounds, sort among
    /// them, and are parsed back by <c>PartOSimulationContext.Iteration_ProjectName</c>. An Iteration 3
    /// candidate is not a round of anything and must not be read as the latest and best of a sequence.
    /// </para>
    /// </summary>
    public class PartOIteration3Paths
    {
        /// <summary>What Candidate B's project name adds to Reference A's.</summary>
        public const string Suffix_CandidateB = "-It3B";

        /// <summary>What the thermostat bridge's copy adds to Candidate B's.</summary>
        public const string Suffix_Bridge = "-Bridge";

        /// <summary>What the pairing record adds to Reference A's results file name.</summary>
        public const string Suffix_Record = "-Iteration3";

        /// <summary>What the persisted A/B review report adds to Reference A's results file name.</summary>
        public const string Suffix_Report = "-Iteration3-Review";

        private PartOIteration3Paths(string outputDirectory, string projectName_ReferenceA, string path_TSD_ReferenceA)
        {
            OutputDirectory = outputDirectory;
            ProjectName_ReferenceA = projectName_ReferenceA;
            Path_TSD_ReferenceA = path_TSD_ReferenceA;

            ProjectName_CandidateB = string.Concat(projectName_ReferenceA, Suffix_CandidateB);
            ProjectName_Bridge = string.Concat(ProjectName_CandidateB, Suffix_Bridge);

            Path_TBD_ThermalSource = Path.Combine(outputDirectory, ProjectName_CandidateB + ".tbd");
            Path_TSD_ThermalSource = Path.ChangeExtension(Path_TBD_ThermalSource, "tsd");
            Path_TPD = Path.Combine(outputDirectory, ProjectName_CandidateB + ".tpd");
            Path_TBD_Bridge = Path.Combine(outputDirectory, ProjectName_Bridge + ".tbd");
            Path_TSD_Bridge = Path.ChangeExtension(Path_TBD_Bridge, "tsd");

            Path_Model_CandidateB = Query.Path_PartORunModel(Path_TSD_Bridge);
            Path_TM59Report_CandidateB = Query.Path_TM59Report(Path_TSD_Bridge);
            Path_TM59Report_ReferenceA = Query.Path_TM59Report(path_TSD_ReferenceA);

            Path_Record = Path.Combine(
                Path.GetDirectoryName(path_TSD_ReferenceA) ?? outputDirectory,
                Path.GetFileNameWithoutExtension(path_TSD_ReferenceA) + Suffix_Record + ".json");
        }

        /// <summary>Where both cases write. Candidate B never writes anywhere Reference A did not.</summary>
        public string OutputDirectory { get; }

        public string ProjectName_ReferenceA { get; }

        public string ProjectName_CandidateB { get; }

        public string ProjectName_Bridge { get; }

        /// <summary>Reference A's results - the pairing is named from these.</summary>
        public string Path_TSD_ReferenceA { get; }

        public string Path_TBD_ThermalSource { get; }

        public string Path_TSD_ThermalSource { get; }

        public string Path_TPD { get; }

        public string Path_TBD_Bridge { get; }

        public string Path_TSD_Bridge { get; }

        /// <summary>Candidate B's reopenable model, beside its own results and named from them.</summary>
        public string Path_Model_CandidateB { get; }

        public string Path_TM59Report_ReferenceA { get; }

        public string Path_TM59Report_CandidateB { get; }

        /// <summary>The pairing record, beside Reference A's results.</summary>
        public string Path_Record { get; }

        /// <summary>
        /// Every fixed path this pairing may write, for the attempt-start snapshot. Reference A's own TSD
        /// and its TM59 report are deliberately <b>not</b> here: A is an input, this run does not write it,
        /// and a path in the snapshot is a path something is expected to have produced.
        /// </summary>
        public string[] Paths_CandidateB =>
        [
            Path_TBD_ThermalSource,
            Path_TSD_ThermalSource,
            Path_TPD,
            Path_TBD_Bridge,
            Path_TSD_Bridge,
            Path_Model_CandidateB,
            Path_TM59Report_CandidateB,
            Path_Record,
        ];

        /// <summary>
        /// The pairing's paths for one completed Part O run, or null where the run states no output
        /// directory, no project name or no results file to derive them from.
        /// </summary>
        public static PartOIteration3Paths Create(PartOSimulationContext partOSimulationContext, string path_TSD_ReferenceA)
        {
            if (partOSimulationContext is null
                || string.IsNullOrWhiteSpace(partOSimulationContext.OutputDirectory)
                || string.IsNullOrWhiteSpace(partOSimulationContext.ProjectName)
                || string.IsNullOrWhiteSpace(path_TSD_ReferenceA))
            {
                return null;
            }

            return new PartOIteration3Paths(partOSimulationContext.OutputDirectory, partOSimulationContext.ProjectName, path_TSD_ReferenceA);
        }

        /// <summary>
        /// The record's path for a results file alone - what a <b>review</b> uses, which has only the
        /// reopened run's TSD and no simulation context at all.
        /// </summary>
        public static string Path_Record_ForResults(string path_TSD)
        {
            if (string.IsNullOrWhiteSpace(path_TSD))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(path_TSD);
            string fileName = Path.GetFileNameWithoutExtension(path_TSD);

            return string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)
                ? null
                : Path.Combine(directory, fileName + Suffix_Record + ".json");
        }

        /// <summary>
        /// Where this pairing's persisted A/B review report lives, derived from the pairing record's own
        /// path - so a run, a review, and a later session looking for the last successful report all
        /// arrive at the same file without holding anything but the record's name.
        /// </summary>
        /// <param name="path_Record">The pairing record's path.</param>
        /// <param name="extension">
        /// <c>"txt"</c> for the report an engineer reads, <c>"json"</c> for its structured sibling.
        /// </param>
        public static string Path_Report_ForRecord(string path_Record, string extension = "txt")
        {
            if (string.IsNullOrWhiteSpace(path_Record) || string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(path_Record);
            string fileName = Path.GetFileNameWithoutExtension(path_Record);

            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            //The record is <run>-Iteration3.json, so the report is <run>-Iteration3-Review.txt. Derived
            //from the record rather than re-derived from the TSD: one of them moving must move both.
            if (fileName.EndsWith(Suffix_Record, StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - Suffix_Record.Length);
            }

            return Path.Combine(directory, fileName + Suffix_Report + "." + extension.TrimStart('.'));
        }
    }
}
