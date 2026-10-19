// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Runtime.CompilerServices;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One simulation at a time for an open model (S02): Energy Simulation, Run Simulation &gt; Tas, OpenStudio and Both, and the
    /// comparison window's Run all take this guard before they start anything, so a second start for the same model is refused
    /// instead of running alongside the first. Not a queue and not parallel execution.
    /// <para>
    /// The model is the window's <see cref="UIAnalyticalModel"/> instance: opening a model makes a new one, so a model opened while
    /// a run of the previous one goes is not blocked by it. Both's own Tas then OpenStudio producers run inside the one guard taken
    /// for Both. Dispose the lease to release it, in a finally, on every path.
    /// </para>
    /// </summary>
    public sealed class SimulationGuard : IDisposable
    {
        /// <summary>Energy Simulation and Run Simulation &gt; Tas: the one Tas path.</summary>
        public const string Tas = "a Tas simulation";

        /// <summary>OpenStudio Simulation and Run Simulation &gt; OpenStudio.</summary>
        public const string OpenStudio = "an OpenStudio simulation";

        /// <summary>Run Simulation &gt; Both, and the comparison window's Run: Tas then OpenStudio, inside this one guard.</summary>
        public const string Both = "a Tas + OpenStudio run";

        private static readonly object @lock = new();
        private static readonly ConditionalWeakTable<UIAnalyticalModel, SimulationGuard> leases = new();

        private readonly UIAnalyticalModel uIAnalyticalModel;
        private bool released;

        private SimulationGuard(UIAnalyticalModel uIAnalyticalModel, string simulation)
        {
            this.uIAnalyticalModel = uIAnalyticalModel;
            Simulation = simulation;
        }

        /// <summary>What holds the guard, as a person reads it ("a Tas simulation").</summary>
        public string Simulation { get; }

        /// <summary>
        /// The lease for <paramref name="simulation"/>; null, with what holds the guard in <paramref name="running"/>, when a simulation
        /// already runs for this model. A refusal changes nothing.
        /// </summary>
        public static SimulationGuard? TryAcquire(UIAnalyticalModel uIAnalyticalModel, string simulation, out string? running)
        {
            if (uIAnalyticalModel == null)
            {
                throw new ArgumentNullException(nameof(uIAnalyticalModel));
            }

            lock (@lock)
            {
                if (leases.TryGetValue(uIAnalyticalModel, out SimulationGuard? simulationGuard_Running))
                {
                    running = simulationGuard_Running.Simulation;
                    return null;
                }

                running = null;
                SimulationGuard simulationGuard = new SimulationGuard(uIAnalyticalModel, simulation);
                leases.Add(uIAnalyticalModel, simulationGuard);
                return simulationGuard;
            }
        }

        /// <summary>What is running for this model, or null when nothing is.</summary>
        public static string? Running(UIAnalyticalModel? uIAnalyticalModel)
        {
            if (uIAnalyticalModel == null)
            {
                return null;
            }

            lock (@lock)
            {
                return leases.TryGetValue(uIAnalyticalModel, out SimulationGuard? simulationGuard) ? simulationGuard.Simulation : null;
            }
        }

        /// <summary>The message for a refused start.</summary>
        public static string RefusalMessage(string running)
        {
            return string.Format("{0} is already running for this model, so nothing was started.\n\nWait until it finishes, or cancel it, then start the simulation again.", Capitalise(running));
        }

        /// <summary>Releases the guard; a second call does nothing.</summary>
        public void Dispose()
        {
            lock (@lock)
            {
                if (released)
                {
                    return;
                }

                released = true;
                if (leases.TryGetValue(uIAnalyticalModel, out SimulationGuard? simulationGuard) && ReferenceEquals(simulationGuard, this))
                {
                    leases.Remove(uIAnalyticalModel);
                }
            }
        }

        private static string Capitalise(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
