// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.IO;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// The Approved Document O run in progress in this session: what was prepared, what the TAS workflow
    /// returned for it, and whether the two still belong together.
    /// <para>
    /// <b>Not a bag holding both models.</b> The states own different things and the transitions are the only
    /// way between them. <see cref="PartORunState.Prepared"/> owns the preparation and its scenarios;
    /// <see cref="PartORunState.WorkflowCompleted"/> owns, in addition, the model the workflow <i>returned</i>
    /// and the TSD it wrote. <see cref="AnalyticalModel_Assessment"/> - the only model an assessment may use -
    /// exists solely in the completed state, and there is no code path on which it can be the preparation
    /// output.
    /// </para>
    /// <para>
    /// <b>Why the preparation model must never be assessed.</b> A TM59 query resolves a simulated space back
    /// to a design space through <c>SpaceParameter.ZoneGuid</c>, the identity TAS preserves across the round
    /// trip, and only the model <c>WorkflowCalculator.Calculate</c> returns carries the <i>current</i> TAS
    /// zone identities - a preparation output can still hold stale guids from an earlier round trip on the
    /// same source file. Measured both ways on the licensed acceptance run: preparation output gives an
    /// incomplete <c>SimulationSpaceMap</c> and every space refused; workflow output resolves all nine.
    /// </para>
    /// <para>
    /// <b>Staleness is rejected, not detected after the fact.</b> Anything that replaces the loaded model
    /// between preparing and completing - an edit, an import, an undo, a redo, a second unrelated simulation -
    /// arrives here as an unexpected <see cref="NotifyModified"/> and drops the run to
    /// <see cref="PartORunState.None"/> with a reason. The Part O commands announce their own writes with
    /// <see cref="ExpectModification"/> first, so the only way to reach
    /// <see cref="PartORunState.WorkflowCompleted"/> is a workflow over the model that was prepared and not
    /// touched since. That is what makes pairing one preparation's scenarios with another run's results
    /// unreachable rather than merely unlikely.
    /// </para>
    /// <para>
    /// <b><see cref="PartORunState.WorkflowCompleted"/> means "this prepared run produced the full-year
    /// results being assessed"</b> - not "a TSD exists". Three things are required, and the second and third
    /// are what <see cref="ExpectResults"/> exists for: the workflow must have returned a model; the
    /// simulation must have been the full annual run a TM59 assessment reads, which is enforced by arming
    /// only for that case; and the results file must have been created or rewritten by <i>this</i> workflow,
    /// measured against a fingerprint taken before it ran. An earlier session's <c>&lt;project&gt;.tsd</c>
    /// left in the output directory satisfies none of them.
    /// </para>
    /// <para>
    /// <b>Session state, deliberately.</b> Nothing here is written into the model. Scenarios have no
    /// persistence seam in <c>SAM.Analytical</c>, and inventing one in the UI would put engineering state
    /// under the UI's ownership; a run that does not survive closing the model is the honest alternative,
    /// and it matches how the Grasshopper canvas holds the same objects on its wires.
    /// </para>
    /// </summary>
    public class PartORun
    {
        private AnalyticalModel analyticalModel_Prepared;

        private List<OverheatingScenario> overheatingScenarios = [];

        private AnalyticalModel analyticalModel_Workflow;

        private string path_TSD;

        private System.DateTime dateTime_TSD;

        private bool modificationExpected;

        //The pre-run fingerprint of the results file this workflow is expected to write. See ExpectResults.
        private bool resultsExpected;

        private string path_TSD_Expected;

        private bool exists_TSD_Expected;

        private long length_TSD_Expected = -1;

        private System.DateTime dateTime_TSD_Expected;

        /// <summary>How far this run has got.</summary>
        public PartORunState State { get; private set; } = PartORunState.None;

        /// <summary>
        /// Why the run was dropped, or null where it never was. Retained through
        /// <see cref="PartORunState.None"/> so the UI can say why the assessment is unavailable instead of
        /// only that it is.
        /// </summary>
        public string InvalidationReason { get; private set; }

        /// <summary>
        /// The prepared model this run is built on, or null in <see cref="PartORunState.None"/>.
        /// <para>
        /// <b>Never assess this.</b> It is exposed so the run can be inspected and so the distinction between
        /// the two models is observable to a test, not because it is an alternative to
        /// <see cref="AnalyticalModel_Assessment"/>. It is the model that was handed TO the workflow; its zone
        /// identities may predate the round trip.
        /// </para>
        /// </summary>
        public AnalyticalModel AnalyticalModel_Prepared => analyticalModel_Prepared;

        /// <summary>
        /// The scenarios the assessment attributes results to. Empty outside a live run.
        /// <para>
        /// These belong to the preparation this run was built on and to no other. Pairing them with a
        /// different run's results is the thing this class prevents.
        /// </para>
        /// </summary>
        public List<OverheatingScenario> OverheatingScenarios => [.. overheatingScenarios];

        /// <summary>
        /// <b>The model a TM59 assessment must be given</b> - the one the completed TAS workflow returned.
        /// Null in every state but <see cref="PartORunState.WorkflowCompleted"/>, so there is nothing to fall
        /// back to and no way to reach the preparation output through this property.
        /// </summary>
        public AnalyticalModel AnalyticalModel_Assessment => State == PartORunState.WorkflowCompleted ? analyticalModel_Workflow : null;

        /// <summary>The TSD the completed workflow wrote. Null outside <see cref="PartORunState.WorkflowCompleted"/>.</summary>
        public string Path_TSD => State == PartORunState.WorkflowCompleted ? path_TSD : null;

        /// <summary>
        /// Whether this run has results at all - what the ribbon enables on.
        /// <para>
        /// A pure state read, evaluated on every ribbon refresh, so it deliberately does not touch the
        /// filesystem. <see cref="IsAssessable"/> is the real gate and the command reads that one; a completed
        /// run whose results have since gone is dropped there, which turns this false and puts the reason in
        /// <see cref="InvalidationReason"/> for the tooltip.
        /// </para>
        /// </summary>
        public bool CanAssess => State == PartORunState.WorkflowCompleted;

        /// <summary>
        /// Announces that the next model replacement is this run's own, so it is not read as an outside edit.
        /// One shot: it is consumed by the next <see cref="NotifyModified"/> and must be re-armed for the next
        /// write.
        /// <para>
        /// Called immediately before a Part O command's own <c>SetJSAMObject</c>. Anything arriving unarmed is
        /// somebody else's change, which is exactly the signal wanted.
        /// </para>
        /// </summary>
        public void ExpectModification()
        {
            modificationExpected = true;
        }

        /// <summary>
        /// Announces, <b>before the workflow runs</b>, which results file this run expects it to write, and
        /// fingerprints whatever is at that path now.
        /// <para>
        /// <b>Why a pre-run fingerprint and not a post-run timestamp.</b> The results path is derived from the
        /// TBD's, so an earlier session's <c>&lt;project&gt;.tsd</c> can already be sitting in the output
        /// directory. <c>Modify.Simulate</c> deletes only the TBD before running, so a sizing-only or otherwise
        /// non-simulating workflow leaves that old file untouched - and <see cref="Complete"/> reading its write
        /// time <i>after</i> the run would record a stale file as this run's result and then let
        /// <see cref="IsAssessable"/> approve it against the newly prepared model and scenarios. Captured here,
        /// the same file being unchanged afterwards is exactly the signal that this workflow wrote nothing.
        /// </para>
        /// <para>
        /// <b>Arming is also where the full-year requirement is enforced.</b> The caller arms this only for the
        /// simulation a TM59 assessment can actually read - a full annual hourly series - so a partial,
        /// one-day or sizing-only workflow leaves the run unarmed and <see cref="Complete"/> refuses it even if
        /// it is reached. <see cref="PartORunState.WorkflowCompleted"/> therefore means "this prepared run
        /// produced the full-year results being assessed", not "a TSD exists".
        /// </para>
        /// <para>
        /// One shot per run, and only from <see cref="PartORunState.Prepared"/>: re-arming replaces the
        /// fingerprint, and <see cref="Invalidate"/> clears it, so a dropped run cannot be completed by a
        /// workflow that was announced to its predecessor.
        /// </para>
        /// </summary>
        /// <param name="path_TSD">The results file the workflow about to run is expected to write.</param>
        /// <returns>Whether a fingerprint was armed.</returns>
        public bool ExpectResults(string path_TSD)
        {
            resultsExpected = false;
            path_TSD_Expected = null;
            exists_TSD_Expected = false;
            length_TSD_Expected = -1;
            dateTime_TSD_Expected = default;

            if (State != PartORunState.Prepared || string.IsNullOrWhiteSpace(path_TSD))
            {
                return false;
            }

            path_TSD_Expected = path_TSD;
            resultsExpected = true;

            //Length as well as write time: two different observations of the same file, and a rewrite that
            //landed inside the filesystem's timestamp granularity still changes one of them. Where they both
            //match, the file is treated as untouched - refusing a genuine rerun is the safe way to be wrong.
            FileInfo fileInfo = new(path_TSD);
            if (fileInfo.Exists)
            {
                exists_TSD_Expected = true;
                length_TSD_Expected = fileInfo.Length;
                dateTime_TSD_Expected = fileInfo.LastWriteTimeUtc;
            }

            return true;
        }

        /// <summary>
        /// The loaded model was replaced. Consumes an armed expectation, or drops the run.
        /// </summary>
        public void NotifyModified()
        {
            if (modificationExpected)
            {
                modificationExpected = false;

                return;
            }

            if (State == PartORunState.None)
            {
                return;
            }

            //Named per state: a prepared run and a completed one are lost for different reasons and the user
            //has different work to redo.
            Invalidate(State == PartORunState.Prepared
                ? "The model changed after the Part O iteration was prepared, so the preparation and its overheating scenarios no longer describe it. Prepare the iteration again before simulating."
                : "The model changed after the Part O results were imported, so the assessment no longer has a model and results that belong together. Prepare the iteration again and re-run the simulation.");
        }

        /// <summary>
        /// Records a successful preparation. Replaces whatever was pending - a new preparation supersedes an
        /// older run rather than sitting beside it.
        /// </summary>
        /// <param name="partOIterationPreparation">
        /// The preparation. A null one, one that refused, or one carrying no model or no scenario leaves the
        /// run in <see cref="PartORunState.None"/>: there is nothing to simulate and nothing to attribute.
        /// </param>
        /// <returns>Whether the run is now <see cref="PartORunState.Prepared"/>.</returns>
        public bool Prepare(PartOIterationPreparation partOIterationPreparation)
        {
            return Prepare(partOIterationPreparation?.AnalyticalModel, partOIterationPreparation?.OverheatingScenarios, partOIterationPreparation?.Refusal);
        }

        /// <summary>
        /// The same transition from the two things a run actually needs from a preparation - its model and its
        /// scenarios. The overload above is how production reaches it; this one is also what a test can call,
        /// since <c>PartOIterationPreparation</c> is only assembled by <c>SAM.Analytical</c> itself.
        /// </summary>
        /// <param name="analyticalModel_Prepared">The prepared copy to simulate.</param>
        /// <param name="overheatingScenarios">The scenarios stated for it.</param>
        /// <param name="refusal">The preparation's fatal refusal, where it had one.</param>
        public bool Prepare(AnalyticalModel analyticalModel_Prepared, IEnumerable<OverheatingScenario> overheatingScenarios, string refusal = null)
        {
            Reset();

            if (analyticalModel_Prepared is null)
            {
                Invalidate(refusal ?? "Nothing was prepared, so there is no Part O run.");

                return false;
            }

            List<OverheatingScenario> overheatingScenarios_Temp = [];
            foreach (OverheatingScenario overheatingScenario in overheatingScenarios ?? [])
            {
                if (overheatingScenario is not null)
                {
                    overheatingScenarios_Temp.Add(overheatingScenario);
                }
            }

            if (overheatingScenarios_Temp.Count == 0)
            {
                //Without a scenario there is no ventilation strategy for any space, so a TM59 assessment would
                //refuse every one of them. Refusing here says so while the user is still looking at the
                //preparation, rather than after a full simulation.
                Invalidate("The preparation stated no overheating scenario, so no space would have a ventilation strategy to be assessed against. Nothing is pending.");

                return false;
            }

            this.analyticalModel_Prepared = analyticalModel_Prepared;
            this.overheatingScenarios = overheatingScenarios_Temp;

            State = PartORunState.Prepared;

            return true;
        }

        /// <summary>
        /// Pairs the prepared run with the model a completed TAS workflow returned, and the TSD it wrote.
        /// </summary>
        /// <param name="analyticalModel_Workflow">
        /// The model <c>WorkflowCalculator.Calculate</c> returned - not the model that was handed to it, and
        /// not the loaded model read back afterwards.
        /// </param>
        /// <param name="path_TSD">
        /// The simulation results. Required to exist, to be the path <see cref="ExpectResults"/> was armed
        /// with, and to have been <b>created or rewritten since that arming</b> - a derived file name is a
        /// guess, an old file at that name is somebody else's run, and a workflow that wrote nothing did not
        /// complete this one. Its write time is then captured so <see cref="IsAssessable"/> can tell that the
        /// file being assessed is still the one this run wrote.
        /// </param>
        /// <param name="refusal">Why the run was not completed, or null where it was.</param>
        public bool Complete(AnalyticalModel analyticalModel_Workflow, string path_TSD, out string refusal)
        {
            refusal = null;

            //Only from Prepared. Completing from None would pair results with nothing; completing from
            //WorkflowCompleted would re-point a finished run at a second simulation's results while keeping
            //the first one's model - the precise stale pairing this type exists to prevent.
            if (State != PartORunState.Prepared)
            {
                refusal = State == PartORunState.None
                    ? "No Part O iteration is prepared, so a workflow result has nothing to complete. " + (InvalidationReason ?? "Prepare an iteration first.")
                    : "This Part O run already has results. Prepare the iteration again before simulating, so the model and the results being assessed are from the same run.";

                Invalidate(refusal);

                return false;
            }

            if (analyticalModel_Workflow is null)
            {
                refusal = "The TAS workflow returned no analytical model, so there is no model carrying the current TAS zone identities to assess against. The preparation output is not a substitute for it.";

                Invalidate(refusal);

                return false;
            }

            if (string.IsNullOrWhiteSpace(path_TSD) || !File.Exists(path_TSD))
            {
                refusal = string.Format("No simulation results were found at '{0}', so the workflow did not complete a run that can be assessed. A sizing-only run writes no TSD.", path_TSD ?? "?");

                Invalidate(refusal);

                return false;
            }

            //Nothing announced this workflow's results, so nothing establishes that they are this run's. That
            //is the state a partial, one-day or sizing-only simulation leaves the run in, because the caller
            //arms ExpectResults only for the full-year run a TM59 assessment can read.
            if (!resultsExpected || !string.Equals(path_TSD_Expected, path_TSD, System.StringComparison.Ordinal))
            {
                refusal = string.Format("The results at '{0}' were not announced as this Part O run's, so it cannot be established that this workflow produced them. Only a full-year simulation of the prepared model completes a Part O run - prepare the iteration again and simulate with Full Year Simulation ticked.", path_TSD);

                Invalidate(refusal);

                return false;
            }

            //The file that was already there, byte-length and write-time unchanged: this workflow did not write
            //it. Accepting it would pair an earlier session's results with the model just prepared.
            FileInfo fileInfo = new(path_TSD);
            if (exists_TSD_Expected && fileInfo.Length == length_TSD_Expected && fileInfo.LastWriteTimeUtc == dateTime_TSD_Expected)
            {
                refusal = string.Format("The simulation results at '{0}' are unchanged from before this workflow ran, so they are an earlier run's and not this one's. Simulate the prepared model with Full Year Simulation ticked to produce results this Part O run can be assessed against.", path_TSD);

                Invalidate(refusal);

                return false;
            }

            this.analyticalModel_Workflow = analyticalModel_Workflow;
            this.path_TSD = path_TSD;
            dateTime_TSD = File.GetLastWriteTimeUtc(path_TSD);

            State = PartORunState.WorkflowCompleted;
            InvalidationReason = null;

            return true;
        }

        /// <summary>
        /// Whether an assessment may run right now, and why not where it may not.
        /// <para>
        /// Re-checks the TSD rather than trusting <see cref="State"/> alone: the file is on disk, and anything
        /// - another SAM session, a rerun from outside this window - can have replaced it since. A result read
        /// from a file this run did not write would be attributed to this run's scenarios.
        /// </para>
        /// <para>
        /// <b>A completed run that fails that check is dropped here, not merely refused.</b> Otherwise
        /// <see cref="State"/> stays <see cref="PartORunState.WorkflowCompleted"/>, <see cref="CanAssess"/>
        /// stays true, and the ribbon re-enables the command with its success tooltip the moment the refusal
        /// dialog is dismissed - offering a click that is known to fail, over and over. It is also what the
        /// invariant requires: the state means "this run produced the full-year results being assessed", and a
        /// deleted or rewritten file is no longer those results. Same rule as everywhere else in this type -
        /// staleness is rejected, not carried.
        /// </para>
        /// <para>
        /// Deliberately <b>not</b> read by <see cref="CanAssess"/>, which stays a pure state read: a property
        /// the ribbon evaluates on every refresh must not touch the filesystem, and must not drop a run as a
        /// side effect of being looked at.
        /// </para>
        /// </summary>
        public bool IsAssessable(out string refusal)
        {
            refusal = null;

            if (State != PartORunState.WorkflowCompleted)
            {
                //NOT invalidated: Prepared is a live run waiting for its simulation, and None has already been
                //explained. Only the two results checks below drop anything.
                refusal = State == PartORunState.Prepared
                    ? "The Part O iteration is prepared but has not been simulated, so there are no results to assess. Run the TAS simulation first."
                    : "No Part O run is available to assess. " + (InvalidationReason ?? "Prepare an iteration and run the TAS simulation.");

                return false;
            }

            if (!File.Exists(path_TSD))
            {
                refusal = string.Format("The simulation results this run produced are no longer at '{0}'. Prepare the iteration again and re-run the simulation.", path_TSD);

                Invalidate(refusal);

                return false;
            }

            if (File.GetLastWriteTimeUtc(path_TSD) != dateTime_TSD)
            {
                refusal = string.Format("The simulation results at '{0}' have been rewritten since this Part O run produced them, so they are no longer the results this run's overheating scenarios describe. Prepare the iteration again and re-run the simulation.", path_TSD);

                Invalidate(refusal);

                return false;
            }

            return true;
        }

        /// <summary>Drops the run and records why. Idempotent; the first reason is not overwritten by a later one.</summary>
        public void Invalidate(string reason)
        {
            analyticalModel_Prepared = null;
            overheatingScenarios = [];
            analyticalModel_Workflow = null;
            path_TSD = null;
            dateTime_TSD = default;
            modificationExpected = false;

            //Cleared with everything else: a workflow announced to the run that has just been dropped must not
            //be able to complete its successor.
            resultsExpected = false;
            path_TSD_Expected = null;
            exists_TSD_Expected = false;
            length_TSD_Expected = -1;
            dateTime_TSD_Expected = default;

            State = PartORunState.None;

            InvalidationReason ??= reason;
        }

        /// <summary>
        /// Clears the run outright - no pending state and no reason. For closing or opening a model, where
        /// there is nothing to explain: the run did not go stale, it stopped applying.
        /// </summary>
        public void Reset()
        {
            Invalidate(null);

            InvalidationReason = null;
        }
    }
}
