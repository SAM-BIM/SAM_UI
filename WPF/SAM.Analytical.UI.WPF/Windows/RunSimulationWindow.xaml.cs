// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    /// <summary>The engine choice of Run Simulation (SAM_UI#231).</summary>
    public enum SimulationEngineChoice
    {
        /// <summary>The existing Energy Simulation (Tas), unchanged.</summary>
        Tas,

        /// <summary>The OpenStudio/EnergyPlus simulation of a frozen copy (SAM_UI#229).</summary>
        OpenStudio,

        /// <summary>Both engines on one frozen snapshot, one after the other, then SAM_Validation's comparison.</summary>
        Both,
    }

    /// <summary>
    /// Run Simulation: asks which engine to run. It runs nothing itself - the caller starts the chosen workflow - so the existing Tas
    /// Energy Simulation stays exactly as it was. A choice whose run is already in progress is shown but disabled.
    /// </summary>
    public partial class RunSimulationWindow : System.Windows.Window
    {
        public RunSimulationWindow(SimulationEngineChoice simulationEngineChoice = SimulationEngineChoice.Tas, bool openStudioAvailable = true, bool bothAvailable = true)
        {
            InitializeComponent();

            RadioButton_OpenStudio.IsEnabled = openStudioAvailable;
            RadioButton_Both.IsEnabled = bothAvailable;
            if (!openStudioAvailable)
            {
                RadioButton_OpenStudio.ToolTip = "An OpenStudio run is in progress.";
            }

            if (!bothAvailable)
            {
                RadioButton_Both.ToolTip = "A Tas + OpenStudio run is in progress.";
            }

            switch (simulationEngineChoice)
            {
                case SimulationEngineChoice.OpenStudio when openStudioAvailable:
                    RadioButton_OpenStudio.IsChecked = true;
                    break;
                case SimulationEngineChoice.Both when bothAvailable:
                    RadioButton_Both.IsChecked = true;
                    break;
                default:
                    RadioButton_Tas.IsChecked = true;
                    break;
            }

            Button_Run.Click += (s, e) => DialogResult = true;
        }

        /// <summary>The engine chosen; read it after ShowDialog returned true.</summary>
        public SimulationEngineChoice EngineChoice
        {
            get
            {
                if (RadioButton_Both.IsChecked == true)
                {
                    return SimulationEngineChoice.Both;
                }

                return RadioButton_OpenStudio.IsChecked == true ? SimulationEngineChoice.OpenStudio : SimulationEngineChoice.Tas;
            }
        }
    }
}
