// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.MechanicalSystemForm
    /// (+ MechanicalSystemControl): edits a single MechanicalSystem. The legacy control's riser/space
    /// tree actions were NotImplemented, so the only functional edit is the system Id (full name and
    /// type are shown for reference). Mirrors the original surface (the (MechanicalSystem,
    /// AdjacencyCluster) constructor and the MechanicalSystem / AdjacencyCluster getters).
    ///
    /// NOTE: fixes a latent bug in the original GetMechanicalSystem where the copy-source cast used
    /// the MechanicalSystem property (self-recursive); this reads the backing field instead.
    /// </summary>
    public partial class MechanicalSystemWindow : System.Windows.Window
    {
        private AdjacencyCluster adjacencyCluster;
        private MechanicalSystem mechanicalSystem;

        public MechanicalSystemWindow()
        {
            InitializeComponent();
        }

        public MechanicalSystemWindow(MechanicalSystem mechanicalSystem, AdjacencyCluster adjacencyCluster)
            : this()
        {
            this.adjacencyCluster = adjacencyCluster;
            this.mechanicalSystem = mechanicalSystem;

            LoadMechanicalSystem(mechanicalSystem);
        }

        public MechanicalSystem MechanicalSystem
        {
            get
            {
                return GetMechanicalSystem();
            }

            set
            {
                mechanicalSystem = value;
                LoadMechanicalSystem(mechanicalSystem);
            }
        }

        public AdjacencyCluster AdjacencyCluster
        {
            get
            {
                return adjacencyCluster;
            }

            set
            {
                adjacencyCluster = value;
            }
        }

        private List<MechanicalSystemType> MechanicalSystemTypes
        {
            get
            {
                MechanicalSystemType mechanicalSystemType = mechanicalSystem?.Type;

                List<MechanicalSystemType> result = adjacencyCluster?.GetMechanicalSystemTypes<MechanicalSystemType>();
                if (result == null)
                {
                    result = new List<MechanicalSystemType>();
                }

                if (mechanicalSystemType != null && result.Find(x => x.Name == mechanicalSystemType.Name) == null)
                {
                    result.Add(mechanicalSystemType);
                }

                return result;
            }
        }

        private void LoadMechanicalSystem(MechanicalSystem mechanicalSystem)
        {
            List<MechanicalSystemType> mechanicalSystemTypes = MechanicalSystemTypes;
            mechanicalSystemTypes?.Sort((x, y) => x.Name.CompareTo(y.Name));

            ComboBox_MechanicalSystemType.Items.Clear();
            mechanicalSystemTypes?.ForEach(x => ComboBox_MechanicalSystemType.Items.Add(x.Name));

            ComboBox_MechanicalSystemType.Text = mechanicalSystem?.Type?.Name;

            TextBox_FullName.Text = mechanicalSystem?.FullName;
            TextBox_Id.Text = mechanicalSystem?.Id;
        }

        private MechanicalSystem GetMechanicalSystem()
        {
            MechanicalSystem result = null;

            if (mechanicalSystem is VentilationSystem ventilationSystem)
            {
                result = new VentilationSystem(mechanicalSystem.Guid, TextBox_Id.Text, ventilationSystem);
            }
            else if (mechanicalSystem is CoolingSystem coolingSystem)
            {
                result = new CoolingSystem(mechanicalSystem.Guid, TextBox_Id.Text, coolingSystem);
            }
            else if (mechanicalSystem is HeatingSystem heatingSystem)
            {
                result = new HeatingSystem(mechanicalSystem.Guid, TextBox_Id.Text, heatingSystem);
            }

            return result;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
