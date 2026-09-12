// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System.IO;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Whether the Approved Document O Iteration 3 A/B action can run a Candidate B for this Part O
        /// run, reopen a pairing it already produced, or neither.
        ///
        /// <para><b>What a new Candidate B requires, and why each</b></para>
        /// <list type="bullet">
        /// <item><b>A completed run in THIS session.</b> A restored run carries no preparation and no
        /// simulation context by construction - a file records what was run, not how this session
        /// prepared it - so there is nothing to derive Candidate B's identical thermal case from, and no
        /// record of which authored systems the iteration built. It may review; it may not run. Same rule
        /// as Iteration 2B, for the same reason.</item>
        /// <item><b>Results that are still this run's.</b> Asked of <c>PartORun.IsAssessable</c> by the
        /// caller and passed in, so the filesystem is touched once per showing of the dialog rather than
        /// once per keystroke.</item>
        /// <item><b>The full annual case.</b> Approved Document O's criteria are defined over days 1 to
        /// 365 and Candidate B has to be the same case; a partial year cannot be compared with one.</item>
        /// <item><b>Iteration 1a.</b> The frozen architecture pairs Candidate B against the Iteration 1a
        /// reference and against nothing else. Iteration 1b has no mechanical system to materialise at
        /// all, and Iteration 2 introduces manufacturer behaviour that PR5 owns; extending this
        /// foundation to either of them is a separate decision, not a side effect of this button.</item>
        /// <item><b>Captured system identities.</b> See <c>PartORun.Guids_VentilationSystem_Prepared</c>
        /// and SAM #114 - without them there is no identity-based answer to which authored system is the
        /// design under assessment, and every other answer is a guess.</item>
        /// </list>
        ///
        /// <para><b>What a review requires</b></para>
        /// <para>
        /// A record file at the deterministic path beside this run's results, parseable, of the current
        /// schema, complete, and naming <b>these</b> results. A refused record is still openable - its
        /// ledger is the diagnosis - but the review that reads it presents no Candidate B numbers; see
        /// <c>Modify.ReviewPartOIteration3</c>, which is the authority on that and validates the files
        /// again itself. This only decides whether the action is offered.
        /// </para>
        /// </summary>
        /// <param name="partORun">The session's Part O run.</param>
        /// <param name="resultsAvailable"><c>PartORun.IsAssessable</c>, already asked by the caller.</param>
        /// <param name="resultsRefusal">That authority's own words, where it said no.</param>
        public static PartOIteration3Eligibility PartOIteration3Eligibility(PartORun partORun, bool resultsAvailable, string resultsRefusal)
        {
            if (partORun is null)
            {
                return new PartOIteration3Eligibility(false, "There is no Part O run, so there is nothing to compare.", false, "There is no Part O run, so there is no Iteration 3 pairing to reopen.", null);
            }

            string path_TSD = partORun.Path_TSD;

            string path_Record = PartOIteration3Paths.Path_Record_ForResults(path_TSD);

            //---------------------------------------------------------------------------------------------
            //Review
            //---------------------------------------------------------------------------------------------
            bool canReview = false;
            string refusal_Review;

            if (!resultsAvailable)
            {
                refusal_Review = resultsRefusal ?? "This Part O run has no results, so there is no Iteration 3 pairing to reopen.";
            }
            else if (string.IsNullOrWhiteSpace(path_Record) || !File.Exists(path_Record))
            {
                refusal_Review = string.Format("No Approved Document O Iteration 3 pairing has been recorded for these results{0}.", string.IsNullOrWhiteSpace(path_Record) ? string.Empty : string.Format(" at '{0}'", path_Record));
            }
            else
            {
                PartOIteration3Record partOIteration3Record = Read(path_Record);

                if (partOIteration3Record is null)
                {
                    refusal_Review = string.Format("The Iteration 3 pairing record at '{0}' could not be read.", path_Record);
                }
                else if (!UI.PartOIteration3Record.IsReadableSchema(partOIteration3Record.Schema))
                {
                    refusal_Review = string.Format(
                        "The Iteration 3 pairing record at '{0}' states schema '{1}' and this build reads only '{2}' or '{3}', so it cannot be read as one.",
                        path_Record,
                        partOIteration3Record.Schema ?? "<none>",
                        UI.PartOIteration3Record.CurrentSchema,
                        UI.PartOIteration3Record.LegacySchema_V1);
                }
                else
                {
                    //A refused record is offered too: its ledger is the whole point of having kept it, and
                    //the review presents no Candidate B numbers for one.
                    canReview = true;
                    refusal_Review = null;
                }
            }

            //---------------------------------------------------------------------------------------------
            //Run
            //---------------------------------------------------------------------------------------------
            string refusal_Run = null;

            if (partORun.State != PartORunState.WorkflowCompleted)
            {
                refusal_Run = partORun.State == PartORunState.Prepared
                    ? "The Part O iteration is prepared but has not been simulated, so there is no Reference A to compare against. Run the full-year simulation first."
                    : "No completed Part O run is available. " + (partORun.InvalidationReason ?? "Prepare an Iteration 1a run and simulate the full year.");
            }
            else if (!resultsAvailable)
            {
                refusal_Run = resultsRefusal ?? "This Part O run's results are no longer available, so there is no Reference A to compare against.";
            }
            else if (partORun.IsRestored)
            {
                refusal_Run = "This Part O run was reopened from a saved model, so it records what was run but not how this session prepared it - there is no thermal case to reproduce and no record of which ventilation systems the iteration built. A reopened run can review an existing Iteration 3 pairing but cannot start a new one. Prepare and run Iteration 1a in this session to produce one.";
            }
            else if (partORun.SimulationContext is null)
            {
                refusal_Run = "This Part O run does not record the TAS case it was simulated as, so Candidate B cannot be run as the same case. Prepare and run the iteration again.";
            }
            else if (!partORun.SimulationContext.IsFullYear)
            {
                refusal_Run = "This Part O run was not the full annual case, so it is not an Approved Document O reference and nothing can be compared against it. Prepare and run the full-year simulation.";
            }
            else if (partORun.PreparationContext is null)
            {
                refusal_Run = "This Part O run does not record how it was prepared, so the Iteration 3 pairing cannot state which iteration it is comparing. Prepare and run the iteration again.";
            }
            else if (partORun.PreparationContext.PartOIteration != PartOIteration.BasePassive)
            {
                refusal_Run = string.Format(
                    "Iteration 3 compares the Iteration 1a base MVHR reference against the explicit TAS Systems route, and this run was prepared as '{0}'. Prepare and run Iteration 1a to produce a reference for it.",
                    Core.Query.Description(partORun.PreparationContext.PartOIteration));
            }
            else if (partORun.Guids_VentilationSystem_Prepared.Count == 0)
            {
                refusal_Run = "This Part O run's preparation built no ventilation system, so there is no mechanical ventilation design for the explicit TAS Systems route to materialise.";
            }
            else if (PartOIteration3Paths.Create(partORun.SimulationContext, path_TSD) is null)
            {
                refusal_Run = "This Part O run does not state an output directory, a project name and a results file together, so Candidate B has nowhere deterministic to be written.";
            }

            return new PartOIteration3Eligibility(refusal_Run is null, refusal_Run, canReview, refusal_Review, path_Record);
        }

        /// <summary>
        /// One pairing record off disk, or null where the file is missing, unreadable or not a record.
        /// <para>
        /// Total on purpose: a record is read while reopening a model, and an exception escaping from
        /// here would take the reopen with it. Every failure is a null the caller refuses by name.
        /// </para>
        /// </summary>
        internal static PartOIteration3Record PartOIteration3PairingRecord(string path_Record)
        {
            return Read(path_Record);
        }

        private static PartOIteration3Record Read(string path_Record)
        {
            if (string.IsNullOrWhiteSpace(path_Record) || !File.Exists(path_Record))
            {
                return null;
            }

            try
            {
                return UI.PartOIteration3Record.Parse(File.ReadAllText(path_Record));
            }
            catch (IOException)
            {
                return null;
            }
            catch (System.UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
