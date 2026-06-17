// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using System.Windows.Input;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.OccupancyForm (+ OccupancyControl):
    /// edits a Space's occupancy via area-per-person and/or a direct occupancy count, showing the
    /// resulting calculated occupancy. Mirrors the original public surface (parameterless ctor + the
    /// Space get/set). On read-back the calculated occupancy is applied via Space.UpdateOccupancy.
    /// </summary>
    public partial class OccupancyWindow : System.Windows.Window
    {
        private Space space;

        public OccupancyWindow()
        {
            InitializeComponent();
        }

        public Space Space
        {
            get
            {
                return GetSpace();
            }

            set
            {
                SetSpace(value);
            }
        }

        private Space GetSpace()
        {
            if (space == null)
            {
                return null;
            }

            Space result = new Space(space);

            if (!Core.Query.TryConvert(TextBox_CalculatedOccupancy.Text, out double occupancy) || double.IsNaN(occupancy))
            {
                occupancy = double.NaN;
            }

            UpdateOccupancy(result, occupancy);
            return result;
        }

        /// <summary>
        /// Ported from the retired SAM.Analytical.Windows.Modify.UpdateOccupancy: updates the Space
        /// occupancy and (when an area is available) the InternalCondition AreaPerPerson.
        /// </summary>
        private static void UpdateOccupancy(Space space, double occupancy)
        {
            if (space == null || occupancy < 0)
            {
                return;
            }

            if (double.IsNaN(occupancy))
            {
                space.RemoveValue(SpaceParameter.Occupancy);
            }
            else
            {
                space.SetValue(SpaceParameter.Occupancy, occupancy);

                if (space.TryGetValue(SpaceParameter.Area, out double area) && !double.IsNaN(area) && area > 0)
                {
                    InternalCondition internalCondition = space.InternalCondition;
                    if (internalCondition != null)
                    {
                        internalCondition.SetValue(InternalConditionParameter.AreaPerPerson, occupancy == 0 ? 0 : area / occupancy);
                        space.InternalCondition = internalCondition;
                    }
                }
            }
        }

        private void SetSpace(Space space)
        {
            this.space = space;

            CheckBox_InternalCondition.IsEnabled = false;
            CheckBox_InternalCondition.IsChecked = false;

            TextBox_AreaPerPerson.IsEnabled = false;
            Label_AreaPerPerson.IsEnabled = false;
            Label_AreaPerPerson_Unit.IsEnabled = false;

            TextBox_Occupancy.IsEnabled = false;
            Label_Occupancy.IsEnabled = false;
            Label_Occupancy_Unit.IsEnabled = false;

            TextBox_AreaPerPerson.Text = string.Empty;
            TextBox_Occupancy.Text = string.Empty;

            if (space == null)
            {
                return;
            }

            InternalCondition internalCondition = space.InternalCondition;
            if (internalCondition != null)
            {
                CheckBox_InternalCondition.IsEnabled = true;
            }

            TextBox_Occupancy.IsEnabled = true;
            Label_Occupancy.IsEnabled = true;
            Label_Occupancy_Unit.IsEnabled = true;

            if (space.TryGetValue(SpaceParameter.Area, out double area) && !double.IsNaN(area) && area > 0)
            {
                TextBox_AreaPerPerson.IsEnabled = true;
                Label_AreaPerPerson.IsEnabled = true;
                Label_AreaPerPerson_Unit.IsEnabled = true;
            }

            if (!space.TryGetValue(SpaceParameter.Occupancy, out double occupancy) || double.IsNaN(occupancy))
            {
                return;
            }

            TextBox_Occupancy.Text = occupancy.ToString();
        }

        private void UpdateCalculatedOccupancy()
        {
            TextBox_CalculatedOccupancy.Text = null;

            if (string.IsNullOrWhiteSpace(TextBox_AreaPerPerson.Text) && string.IsNullOrWhiteSpace(TextBox_Occupancy.Text))
            {
                return;
            }

            double calculatedOccupancy = double.NaN;
            if (Core.Query.TryConvert(TextBox_AreaPerPerson.Text, out double areaPerPerson) && !double.IsNaN(areaPerPerson))
            {
                if (space.TryGetValue(SpaceParameter.Area, out double area) && !double.IsNaN(area) && area > 0)
                {
                    calculatedOccupancy = area / areaPerPerson;
                }
            }

            if (Core.Query.TryConvert(TextBox_Occupancy.Text, out double occupancy) && !double.IsNaN(occupancy))
            {
                if (double.IsNaN(calculatedOccupancy))
                {
                    calculatedOccupancy = 0;
                }

                calculatedOccupancy += occupancy;
            }

            if (double.IsNaN(calculatedOccupancy))
            {
                return;
            }

            TextBox_CalculatedOccupancy.Text = Core.Query.Round(calculatedOccupancy, Core.Tolerance.MacroDistance).ToString();
        }

        private void TextBox_Occupancy_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateCalculatedOccupancy();
        }

        private void TextBox_AreaPerPerson_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateCalculatedOccupancy();
        }

        private void CheckBox_InternalCondition_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (CheckBox_InternalCondition.IsChecked == true)
            {
                TextBox_AreaPerPerson.IsEnabled = false;
                Label_AreaPerPerson.IsEnabled = false;
                Label_AreaPerPerson_Unit.IsEnabled = false;

                InternalCondition internalCondition = space?.InternalCondition;
                if (internalCondition != null)
                {
                    if (internalCondition.TryGetValue(InternalConditionParameter.AreaPerPerson, out double areaPerPerson) && !double.IsNaN(areaPerPerson))
                    {
                        TextBox_AreaPerPerson.Text = Core.Query.Round(areaPerPerson, Core.Tolerance.MacroDistance).ToString();
                    }
                }
            }
            else
            {
                TextBox_AreaPerPerson.IsEnabled = true;
                Label_AreaPerPerson.IsEnabled = true;
                Label_AreaPerPerson_Unit.IsEnabled = true;
            }
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = System.Text.RegularExpressions.Regex.IsMatch(e.Text, "[^0-9.-]+");
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
