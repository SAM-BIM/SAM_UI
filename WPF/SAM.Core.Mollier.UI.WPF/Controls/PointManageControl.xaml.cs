// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Mollier;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms PointManageControl (one managed-point row in the objects manager).</summary>
    public partial class PointManageControl : UserControl
    {
        private UIMollierPoint uIMollierPoint;
        private MollierControlSettings mollierControlSettings;

        public PointManageControl()
        {
            InitializeComponent();
        }

        public PointManageControl(UIMollierPoint uIMollierPoint, MollierControlSettings mollierControlSettings)
        {
            InitializeComponent();
            this.uIMollierPoint = uIMollierPoint;
            this.mollierControlSettings = mollierControlSettings;
        }

        private void PointManageControl_Load(object sender, RoutedEventArgs e)
        {
            if (uIMollierPoint == null || mollierControlSettings == null)
            {
                return;
            }

            string pointLabel = (uIMollierPoint.UIMollierAppearance as UIMollierAppearance).Label;
            int maxNameLength = 5;

            label_Name.Text = pointLabel == string.Empty || pointLabel == null ? "-" : pointLabel.Substring(0, System.Math.Min(pointLabel.Length, maxNameLength));

            if (mollierControlSettings.ChartType == ChartType.Mollier)
            {
                label_X.Text = String.Format("{0:0.00}", System.Math.Round(uIMollierPoint.HumidityRatio * 1000, 2));
                label_Y.Text = String.Format("{0:0.00}", System.Math.Round(uIMollierPoint.DryBulbTemperature, 2));
            }
            else
            {
                label_X.Text = String.Format("{0:0.00}", System.Math.Round(uIMollierPoint.DryBulbTemperature, 2));
                label_Y.Text = String.Format("{0:0.00}", System.Math.Round(uIMollierPoint.HumidityRatio * 1000, 2));
            }
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void removeButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmResult = MessageBox.Show("Are you sure to delete this item ?", "Delete Confirmation",
                                                 MessageBoxButton.YesNo);
            if (confirmResult == MessageBoxResult.No)
            {
                return;
            }
            else
            {
                // Original WinForms walked Parent.Parent.Parent.Parent to the owning UIMollierObjectsForm;
                // the removal body was already commented out there, so nothing acts here either.
            }
        }
    }
}
