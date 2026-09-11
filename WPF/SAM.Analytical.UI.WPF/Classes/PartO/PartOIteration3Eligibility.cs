// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What the Iteration 3 A/B action can do with the run in front of it, and why not where it cannot.
    ///
    /// <para><b>Two different questions, answered separately</b></para>
    /// <para>
    /// "Can a Candidate B be produced from this run" and "is there a pairing to reopen" have different
    /// answers, different reasons and different costs - one runs TAS for an hour, the other reads two
    /// existing results files. Collapsing them into one boolean would mean a person who can only review
    /// is told why they cannot run, which is not their situation.
    /// </para>
    ///
    /// <para><b>Review wins where a completed pairing exists</b></para>
    /// <para>
    /// A run that has already produced a complete record offers <see cref="Review"/>, because rerunning
    /// TAS to reproduce a comparison that is already on disk is not what the button should do. A run
    /// whose last attempt <i>refused</i> has no complete record, so it offers Run again - which is
    /// exactly what a person who has just fixed the reason wants.
    /// </para>
    /// </summary>
    public class PartOIteration3Eligibility
    {
        internal PartOIteration3Eligibility(bool canRun, string refusal_Run, bool canReview, string refusal_Review, string path_Record)
        {
            CanRun = canRun;
            Refusal_Run = refusal_Run;
            CanReview = canReview;
            Refusal_Review = refusal_Review;
            Path_Record = path_Record;
        }

        /// <summary>Whether a new Candidate B may be produced from this run.</summary>
        public bool CanRun { get; }

        /// <summary>Why it may not, in one sentence.</summary>
        public string Refusal_Run { get; }

        /// <summary>Whether a persisted pairing may be reopened for this run.</summary>
        public bool CanReview { get; }

        /// <summary>Why it may not, in one sentence.</summary>
        public string Refusal_Review { get; }

        /// <summary>Where the pairing record would be, whether or not one is there.</summary>
        public string Path_Record { get; }

        /// <summary>Whether the action does anything at all.</summary>
        public bool Available => CanRun || CanReview;

        /// <summary>Whether the action reviews rather than runs. Review is preferred where both are possible.</summary>
        public bool Review => CanReview;

        /// <summary>
        /// The one sentence the disabled action shows. The review reason where there is a pairing to
        /// explain, otherwise the run reason - a person looking at a button that does nothing needs the
        /// reason for the thing they were trying to do.
        /// </summary>
        public string Refusal => Available ? null : Refusal_Run ?? Refusal_Review;

        public override string ToString()
        {
            return Available
                ? string.Format("Iteration 3: {0}", Review ? "review" : "run")
                : string.Format("Iteration 3 unavailable: {0}", Refusal);
        }
    }
}
