﻿using SAM.Core.Attributes;
using SAM.Geometry.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for PanelAppearanceSettingsControl.xaml
    /// </summary>
    public partial class PanelAppearanceSettingsControl : UserControl
    {
        private AdjacencyCluster adjacencyCluster;

        public event EventHandler ValueChanged;

        public PanelAppearanceSettingsControl()
        {
            InitializeComponent();
        }

        public AdjacencyCluster AdjacencyCluster
        {
            get
            {
                return AdjacencyCluster;
            }

            set
            {
                SetAdjacencyCluster(value);
            }
        }

        private void LoadParameterNames()
        {
            comboBox_ParameterName.Items.Clear();
            comboBox_ParameterName.Items.Add(string.Empty);

            if (adjacencyCluster == null)
            {
                return;
            }

            if (radioButton_Default.IsChecked.HasValue && radioButton_Default.IsChecked.Value)
            {
                comboBox_ParameterName.Visibility = Visibility.Hidden;
                label_ParameterName.Visibility = Visibility.Hidden;
                return;
            }
            else
            {
                comboBox_ParameterName.Visibility = Visibility.Visible;
                label_ParameterName.Visibility = Visibility.Visible;
            }

            Type type = null;

            List<object> @objects = new List<object>();
            if (radioButton_Construction.IsChecked.HasValue && radioButton_Construction.IsChecked.Value)
            {
                IEnumerable<Construction> constructions = adjacencyCluster.GetConstructions();
                if (constructions != null)
                {
                    foreach (Construction construction in constructions)
                    {
                        if (construction == null)
                        {
                            continue;
                        }

                        objects.Add(construction);
                    }
                }
                type = typeof(Zone);
            }
            else if (radioButton_Panel.IsChecked.HasValue && radioButton_Panel.IsChecked.Value)
            {
                IEnumerable<Panel> panels = adjacencyCluster.GetPanels();
                if (panels != null)
                {
                    foreach (Panel panel in panels)
                    {
                        if (panel == null)
                        {
                            continue;
                        }

                        objects.Add(panel);
                    }
                }
                type = typeof(Panel);
            }

            HashSet<string> parameterNames = new HashSet<string>();
            foreach (object @object in objects)
            {
                Core.Query.UserFriendlyNames(@object)?.ForEach(x => parameterNames.Add(x));

            }

            if (type != null)
            {
                foreach (Enum @enum in Core.Query.Enums(type))
                {
                    string name = ParameterProperties.Get(@enum)?.Name;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    parameterNames.Add(name);
                }
            }

            if (parameterNames != null && parameterNames.Count != 0)
            {
                List<string> parameterNames_Sorted = parameterNames.ToList();
                parameterNames_Sorted.Sort();

                foreach (string parameterName in parameterNames_Sorted)
                {
                    comboBox_ParameterName.Items.Add(parameterName);
                }
            }
        }

        private void SetAdjacencyCluster(AdjacencyCluster adjacencyCluster)
        {
            this.adjacencyCluster = adjacencyCluster;
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            LoadParameterNames();
            ValueChanged?.Invoke(this, new EventArgs());
        }

        public PanelAppearanceSettings PanelAppearanceSettings
        {
            get
            {
                return GetPanelAppearanceSettings();
            }

            set
            {
                SetPanelAppearanceSettings(value);
            }
        }

        private PanelAppearanceSettings GetPanelAppearanceSettings()
        {
            if (radioButton_Panel.IsChecked.HasValue && radioButton_Panel.IsChecked.Value)
            {
                return new PanelAppearanceSettings(comboBox_ParameterName?.SelectedItem?.ToString());
            }

            if (radioButton_Construction.IsChecked.HasValue && radioButton_Construction.IsChecked.Value)
            {
                return new PanelAppearanceSettings(new ConstructionAppearanceSettings(comboBox_ParameterName?.SelectedItem?.ToString()));
            }

            if (radioButton_Default.IsChecked.HasValue && radioButton_Default.IsChecked.Value)
            {
                return null;
            }

            return null;
        }

        private void SetPanelAppearanceSettings(PanelAppearanceSettings panelAppearanceSettings)
        {
            Core.UI.IAppearanceSettings appearanceSettings = panelAppearanceSettings?.GetValueAppearanceSettings<ValueAppearanceSettings>();
            if (appearanceSettings == null)
            {
                radioButton_Default.IsChecked = true;
                LoadParameterNames();
                return;
            }

            string parameterName = null;
            if (appearanceSettings is ParameterAppearanceSettings)
            {
                parameterName = ((ParameterAppearanceSettings)appearanceSettings).ParameterName;
            }
            else if (appearanceSettings is ITypeAppearanceSettings)
            {
                parameterName = ((ITypeAppearanceSettings)appearanceSettings).GetValueAppearanceSettings<ParameterAppearanceSettings>()?.ParameterName;
            }

            if (appearanceSettings is ConstructionAppearanceSettings)
            {
                radioButton_Construction.IsChecked = true;
            }
            else
            {
                radioButton_Panel.IsChecked = true;
            }

            LoadParameterNames();
            if(parameterName != null)
            {
                comboBox_ParameterName.SelectedItem = parameterName;
            }
        }

        private void comboBox_ParameterName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValueChanged?.Invoke(this, new EventArgs());
        }
    }
}
