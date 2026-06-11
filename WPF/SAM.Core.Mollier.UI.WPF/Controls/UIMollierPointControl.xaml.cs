// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using SAM.Core.Mollier.UI.Controls;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of the WinForms UIMollierPointControl: point colour + label + label colour + label-location
    /// editor. The WinForms Button.BackColor / SystemColors.Control sentinel becomes explicit
    /// System.Drawing.Color fields (Color.Empty = none); Button.Text becomes Button.Content.
    /// </summary>
    public partial class UIMollierPointControl : UserControl
    {
        private static string locationSetText = "SET";

        public event MollierPointSelectingEventHandler MollierPointSelecting;
        public event MollierPointSelectedEventHandler MollierPointSelected;

        private MollierControl mollierControl = null;
        private UIMollierPoint uIMollierPoint;

        private SystemColor pointColor = SystemColor.Empty;
        private SystemColor labelColor = SystemColor.Empty;

        public UIMollierPointControl()
        {
            InitializeComponent();
            SetLocationVisibility(false);
        }

        public UIMollierPointControl(UIMollierPoint uIMollierPoint)
        {
            InitializeComponent();

            if (uIMollierPoint != null)
            {
                SetUIMollierPoint(uIMollierPoint);
            }

            SetLocationVisibility(false);
        }

        public UIMollierPoint UIMollierPoint
        {
            get
            {
                if (uIMollierPoint == null || uIMollierPoint.UIMollierAppearance == null)
                {
                    return null;
                }

                UIMollierAppearance uIMollierAppearance = uIMollierPoint.UIMollierAppearance as UIMollierAppearance;

                uIMollierAppearance.Color = pointColor;
                uIMollierAppearance.Label = PointLabel_TextBox.Text;

                UIMollierLabelAppearance uIMollierLabelAppearance = (uIMollierPoint.UIMollierAppearance as UIMollierAppearance).UIMollierLabelAppearance;
                if ((Button_LabelLocation.Content as string) != locationSetText)
                {
                    uIMollierLabelAppearance.Vector2D = null;
                }

                if (labelColor == SystemColor.Empty)
                {
                    if (uIMollierLabelAppearance != null)
                    {
                        uIMollierLabelAppearance.Color = SystemColor.Empty;
                    }
                }
                else
                {
                    if (uIMollierLabelAppearance == null)
                    {
                        uIMollierLabelAppearance = new UIMollierLabelAppearance();
                    }

                    uIMollierLabelAppearance.Color = labelColor;
                }

                uIMollierAppearance.UIMollierLabelAppearance = uIMollierLabelAppearance;

                uIMollierPoint.UIMollierAppearance = uIMollierLabelAppearance;

                return uIMollierPoint;
            }
            set
            {
                SetUIMollierPoint(value);
            }
        }

        public MollierControl MollierControl
        {
            get { return mollierControl; }
            set
            {
                mollierControl = value;
                SetLocationVisibility(mollierControl != null);
            }
        }

        private void SetLocationVisibility(bool visible)
        {
            Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            Label_LabelLocation.Visibility = visibility;
            Button_LabelLocation.Visibility = visibility;
            Button_LabelLocationClear.Visibility = visibility;
        }

        private void PointColor_Button_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                if (colorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                pointColor = colorDialog.Color;
                ApplySwatch(PointColor_Button, pointColor);
            }
        }

        private void SetUIMollierPoint(UIMollierPoint uIMollierPoint)
        {
            if (uIMollierPoint == null)
            {
                return;
            }

            this.uIMollierPoint = uIMollierPoint;

            if (uIMollierPoint.UIMollierAppearance != null)
            {
                PointLabel_TextBox.Text = (uIMollierPoint.UIMollierAppearance as UIMollierAppearance)?.Label;
                pointColor = uIMollierPoint.UIMollierAppearance.Color;
                ApplySwatch(PointColor_Button, pointColor);

                if ((uIMollierPoint.UIMollierAppearance as UIMollierAppearance)?.UIMollierLabelAppearance != null)
                {
                    UIMollierLabelAppearance uIMollierLabelAppearance = (uIMollierPoint.UIMollierAppearance as UIMollierAppearance)?.UIMollierLabelAppearance;

                    labelColor = uIMollierLabelAppearance.Color;
                    ApplySwatch(LabelColor_Button, labelColor);

                    if (uIMollierLabelAppearance.Vector2D != null)
                    {
                        Button_LabelLocation.Content = locationSetText;
                    }
                }
                else
                {
                    labelColor = SystemColor.Empty;
                    ApplySwatch(LabelColor_Button, labelColor);
                }
            }
        }

        private void LabelColor_Button_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                if (colorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                labelColor = colorDialog.Color;
                ApplySwatch(LabelColor_Button, labelColor);
            }
        }

        private void Button_PointClear_Click(object sender, RoutedEventArgs e)
        {
            pointColor = SystemColor.Empty;
            ApplySwatch(PointColor_Button, pointColor);
        }

        private void Button_LabelClear_Click(object sender, RoutedEventArgs e)
        {
            labelColor = SystemColor.Empty;
            ApplySwatch(LabelColor_Button, labelColor);
        }

        private void Button_LabelLocation_Click(object sender, RoutedEventArgs e)
        {
            MollierPointSelecting?.Invoke(this, EventArgs.Empty);

            mollierControl.MollierPointSelected += MollierControl_MollierPointSelected;
        }

        private void MollierControl_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            MollierPointSelected?.Invoke(this, e);

            mollierControl.MollierPointSelected -= MollierControl_MollierPointSelected;

            if (uIMollierPoint.UIMollierAppearance == null)
            {
                uIMollierPoint.UIMollierAppearance = new UIMollierPointAppearance();
            }

            UIMollierAppearance uIMollierAppearance = uIMollierPoint.UIMollierAppearance as UIMollierAppearance;

            UIMollierLabelAppearance uIMollierLabelAppearance = uIMollierAppearance?.UIMollierLabelAppearance;
            if (uIMollierLabelAppearance == null)
            {
                uIMollierLabelAppearance = new UIMollierLabelAppearance();
            }

            Point2D point2D_Selected = Convert.ToSAM(e.MollierPoint, mollierControl.MollierControlSettings.ChartType);
            Point2D point2D = Convert.ToSAM(uIMollierPoint, mollierControl.MollierControlSettings.ChartType);

            uIMollierLabelAppearance.Vector2D = point2D_Selected - point2D;

            uIMollierAppearance.UIMollierLabelAppearance = uIMollierLabelAppearance;

            uIMollierPoint.UIMollierAppearance = uIMollierAppearance;

            Button_LabelLocation.Content = locationSetText;
        }

        private void Button_LabelLocationClear_Click(object sender, RoutedEventArgs e)
        {
            UIMollierLabelAppearance uIMollierLabelAppearance = (uIMollierPoint.UIMollierAppearance as UIMollierAppearance)?.UIMollierLabelAppearance;
            if (uIMollierLabelAppearance == null)
            {
                return;
            }

            uIMollierLabelAppearance.Vector2D = null;

            (uIMollierPoint.UIMollierAppearance as UIMollierAppearance).UIMollierLabelAppearance = uIMollierLabelAppearance;
            Button_LabelLocation.Content = null;
        }

        private static void ApplySwatch(Button button, SystemColor color)
        {
            button.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            if (color != SystemColor.Empty)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            }
        }
    }
}
