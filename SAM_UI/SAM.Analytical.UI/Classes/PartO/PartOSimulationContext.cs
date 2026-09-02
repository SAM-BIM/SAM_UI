// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Weather;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// The TAS case one Approved Document O run was simulated as - the weather, the solar method, the day
    /// range and the workflow options - kept so the <b>same</b> case can be run again over a changed design.
    /// <para>
    /// <b>This is what makes an optimisation's iterations comparable.</b> Iteration 2B changes design
    /// airflow and reruns; if the second run used different weather, a different day range or a different
    /// solar calculation, the change in TM59 results would no longer be attributable to the airflow. So the
    /// case is captured once, from the simulation the baseline was actually produced by, and reused
    /// verbatim.
    /// </para>
    /// <para>
    /// <b>Everything except the project name.</b> That is the one thing an optimisation iteration must
    /// change, because it is what gives each iteration its own TBD and its own TSD - see
    /// <see cref="ProjectName"/>. Overwriting the previous iteration's results would destroy the evidence
    /// for the round that produced them.
    /// </para>
    /// <para>
    /// <b>Simulation only.</b> The deliverable exports a person might also have ticked - room data sheets,
    /// the SAP or Part L export, the TAS domestic-overheating XML, the TPD - are deliberately not carried.
    /// They are outputs of a run somebody asked for, not part of the thermal case, and producing a dozen
    /// copies of each during an optimisation would be noise.
    /// </para>
    /// </summary>
    public class PartOSimulationContext
    {
        /// <param name="outputDirectory">Where the TBD and TSD are written.</param>
        /// <param name="projectName">The base project name the run used.</param>
        /// <param name="weatherData">The weather the run was simulated against.</param>
        /// <param name="solarCalculationMethod">Which solar calculation produced it.</param>
        /// <param name="simulateFrom">First day of the simulated range.</param>
        /// <param name="simulateTo">Last day of the simulated range.</param>
        public PartOSimulationContext(string outputDirectory, string projectName, WeatherData weatherData, SolarCalculationMethod solarCalculationMethod, int simulateFrom, int simulateTo)
        {
            OutputDirectory = outputDirectory;
            ProjectName = projectName;
            WeatherData = weatherData;
            SolarCalculationMethod = solarCalculationMethod;
            SimulateFrom = simulateFrom;
            SimulateTo = simulateTo;
        }

        /// <summary>The directory the TBD and TSD are written to.</summary>
        public string OutputDirectory { get; }

        /// <summary>
        /// The project name the baseline used. An optimisation iteration derives its own name from this -
        /// <c>&lt;project&gt;-Opt01</c>, <c>-Opt02</c> - so every iteration keeps its own results file and no
        /// round can overwrite the evidence for another.
        /// </summary>
        public string ProjectName { get; }

        /// <summary>The weather. Never changed by an optimisation.</summary>
        public WeatherData WeatherData { get; }

        /// <summary>Which solar calculation the case uses.</summary>
        public SolarCalculationMethod SolarCalculationMethod { get; }

        /// <summary>First day of the simulated range.</summary>
        public int SimulateFrom { get; }

        /// <summary>Last day of the simulated range.</summary>
        public int SimulateTo { get; }

        /// <summary>Whether unmet hours are calculated.</summary>
        public bool UnmetHours { get; set; } = true;

        /// <summary>Whether plant sizing runs.</summary>
        public bool Sizing { get; set; } = true;

        /// <summary>Whether aperture widths are used.</summary>
        public bool UseWidths { get; set; }

        /// <summary>Whether construction layers are updated by panel type before conversion.</summary>
        public bool UpdateConstructionLayersByPanelType { get; set; } = true;

        /// <summary>
        /// Whether this is the full annual hourly series a TM59 assessment can read. Anything less cannot
        /// complete a Part O run, so an optimisation cannot be started from it either.
        /// </summary>
        public bool IsFullYear => SimulateFrom == 1 && SimulateTo == 365;

        /// <summary>
        /// The project name for one optimisation iteration - <c>&lt;project&gt;-Opt00</c> for the baseline,
        /// <c>-Opt01</c> onwards for each round.
        /// <para>
        /// Zero padded to two digits so the files sort in the order they were produced in, which is the
        /// order anybody auditing the run will want to read them in.
        /// </para>
        /// </summary>
        public string ProjectName_Iteration(int iteration)
        {
            return string.Format("{0}-Opt{1:00}", ProjectName, iteration);
        }
    }
}
