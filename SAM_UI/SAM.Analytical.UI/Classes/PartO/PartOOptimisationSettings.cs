// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    /// <summary>
    /// What an automatic Approved Document O Iteration 2B optimisation is allowed to do: how big a design
    /// airflow step it takes, and how many rounds it may take before it stops on its own.
    /// <para>
    /// <b>A fixed step, deliberately.</b> Iteration 2B v1 raises each eligible failing room's design airflow
    /// by the same amount every round and reruns the whole thermal case. It does not search for the least
    /// airflow that would pass, and a result reached at a 5 l/s step must therefore never be described as a
    /// minimum - it is the first tested passing design at that step. Anything else would be claiming a
    /// search nobody ran.
    /// </para>
    /// <para>
    /// <b>The iteration limit is a guard, not a target.</b> A full-year TAS run per round is minutes of
    /// work, and a design that responds non-monotonically could otherwise iterate until somebody noticed.
    /// The optimisation is expected to end at
    /// <see cref="PartOOptimisationStopReason.Passed"/> or
    /// <see cref="PartOOptimisationStopReason.CapacityReached"/> long before it.
    /// </para>
    /// </summary>
    public class PartOOptimisationSettings
    {
        /// <summary>The step every Iteration 2B v1 optimisation defaults to [l/s].</summary>
        public const double DefaultAirFlowStep_Lps = 5;

        /// <summary>
        /// The default safety limit on optimisation rounds. Chosen so a dwelling starting at its Approved
        /// Document F minimum can explore a realistic domestic unit's whole capacity in 5 l/s steps and
        /// still stop on the equipment rather than on the guard.
        /// </summary>
        public const int DefaultMaximumIterations = 10;

        /// <summary>
        /// How much each eligible failing room's design airflow is raised per round [l/s]. Must be positive:
        /// a step of zero would rerun the same design forever, and a negative one is not an optimisation.
        /// </summary>
        public double AirFlowStep_Lps { get; set; } = DefaultAirFlowStep_Lps;

        /// <summary>
        /// The most optimisation rounds to run, not counting the baseline. Must be at least one.
        /// </summary>
        public int MaximumIterations { get; set; } = DefaultMaximumIterations;

        /// <summary>Flow rate comparison tolerance [l/s], passed to every design airflow round.</summary>
        public double Tolerance_Lps { get; set; } = 0.001;

        /// <summary>
        /// Whether these settings describe an optimisation that can actually run, and why not where they do
        /// not.
        /// </summary>
        public bool IsValid(out string refusal)
        {
            refusal = null;

            if (double.IsNaN(AirFlowStep_Lps) || double.IsInfinity(AirFlowStep_Lps) || AirFlowStep_Lps <= 0)
            {
                refusal = string.Format("An airflow step of {0} l/s is not an optimisation step - it has to be a finite airflow greater than zero. A step of zero would re-simulate the same design every round.", AirFlowStep_Lps);

                return false;
            }

            if (MaximumIterations < 1)
            {
                refusal = string.Format("A maximum of {0} optimisation iteration(s) leaves nothing to run. Allow at least one round.", MaximumIterations);

                return false;
            }

            return true;
        }
    }
}
