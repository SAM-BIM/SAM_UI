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
    /// <summary>WPF port of the WinForms UIMollierProcessPointControl (one process-point row).</summary>
    public partial class UIMollierProcessPointControl : UserControl
    {
        private static string locationSetText = "SET";

        private UIMollierProcessPoint uIMollierProcessPoint;
        private SystemColor color = SystemColor.Empty;

        public event MollierPointSelectingEventHandler MollierPointSelecting;
        public event MollierPointSelectedEventHandler MollierPointSelected;

        private MollierControl mollierControl = null;

        public UIMollierProcessPointControl()
        {
            InitializeComponent();
        }

        public UIMollierProcessPointControl(UIMollierProcessPoint uIMollierProcessPoint)
        {
            InitializeComponent();
            SetUIMollierProcessPoint(uIMollierProcessPoint);
        }

        public UIMollierProcessPoint UIMollierProcessPoint
        {
            get { return GetUIMollierProcessPoint(); }
            set { SetUIMollierProcessPoint(value); }
        }

        private UIMollierProcessPoint GetUIMollierProcessPoint()
        {
            UIMollierProcessPoint uIMollierProcessPoint = this.uIMollierProcessPoint == null ? null : new UIMollierProcessPoint(this.uIMollierProcessPoint);
            if (uIMollierProcessPoint == null)
            {
                return null;
            }

            UIMollierAppearance uIMollierAppearance = uIMollierProcessPoint?.UIMollierAppearance as UIMollierAppearance;
            if (uIMollierAppearance == null)
            {
                uIMollierAppearance = new UIMollierAppearance();
            }

            UIMollierLabelAppearance uIMollierLabelAppearance = uIMollierAppearance?.UIMollierLabelAppearance;
            if (uIMollierLabelAppearance == null)
            {
                uIMollierLabelAppearance = new UIMollierLabelAppearance();
            }

            uIMollierLabelAppearance.Text = string.IsNullOrEmpty(TextBox_Label.Text) ? null : TextBox_Label.Text;
            uIMollierLabelAppearance.Color = color;

            uIMollierAppearance.UIMollierLabelAppearance = uIMollierLabelAppearance;

            uIMollierProcessPoint.UIMollierAppearance = uIMollierAppearance;

            return uIMollierProcessPoint;
        }

        public string LabelName
        {
            get { return Label_Name.Text; }
            set { Label_Name.Text = value; }
        }

        private void SetUIMollierProcessPoint(UIMollierProcessPoint uIMollierProcessPoint)
        {
            this.uIMollierProcessPoint = uIMollierProcessPoint;
            if (uIMollierProcessPoint == null)
            {
                return;
            }

            UIMollierAppearance uIMollierAppearance = uIMollierProcessPoint?.UIMollierAppearance as UIMollierAppearance;
            if (uIMollierAppearance == null)
            {
                return;
            }

            UIMollierLabelAppearance uIMollierLabelAppearance = uIMollierAppearance?.UIMollierLabelAppearance;
            if (uIMollierLabelAppearance != null)
            {
                TextBox_Label.Text = uIMollierLabelAppearance.Text;
                Button_Vector2D.Content = uIMollierLabelAppearance.Vector2D == null ? string.Empty : locationSetText;

                color = uIMollierLabelAppearance.Color;
                ApplySwatch(Button_Color, color);
            }
        }

        private void Button_Vector2D_Clear_Click(object sender, RoutedEventArgs e)
        {
            UIMollierAppearance uIMollierAppearance = uIMollierProcessPoint.UIMollierAppearance as UIMollierAppearance;
            if (uIMollierAppearance == null)
            {
                return;
            }

            UIMollierLabelAppearance uIMollierLabelAppearance = uIMollierAppearance.UIMollierLabelAppearance;
            if (uIMollierLabelAppearance == null)
            {
                return;
            }

            uIMollierLabelAppearance.Vector2D = null;

            uIMollierAppearance.UIMollierLabelAppearance = uIMollierLabelAppearance;

            uIMollierProcessPoint.UIMollierAppearance = uIMollierAppearance;
        }

        private void Button_Color_Clear_Click(object sender, RoutedEventArgs e)
        {
            color = SystemColor.Empty;
            ApplySwatch(Button_Color, color);
        }

        private void Button_Vector2D_Click(object sender, RoutedEventArgs e)
        {
            MollierPointSelecting?.Invoke(this, EventArgs.Empty);

            mollierControl.MollierPointSelected += MollierControl_MollierPointSelected;
        }

        private void MollierControl_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            MollierPointSelected?.Invoke(this, e);

            mollierControl.MollierPointSelected -= MollierControl_MollierPointSelected;

            UIMollierAppearance uIMollierAppearance = uIMollierProcessPoint.UIMollierAppearance as UIMollierAppearance;

            UIMollierLabelAppearance uIMollierLabelAppearance = uIMollierAppearance?.UIMollierLabelAppearance;
            if (uIMollierLabelAppearance == null)
            {
                uIMollierLabelAppearance = new UIMollierLabelAppearance();
            }

            Point2D point2D_Selected = Convert.ToSAM(e.MollierPoint, mollierControl.MollierControlSettings.ChartType);
            Point2D point2D = Convert.ToSAM(uIMollierProcessPoint, mollierControl.MollierControlSettings.ChartType);

            uIMollierLabelAppearance.Vector2D = point2D_Selected - point2D;

            uIMollierAppearance.UIMollierLabelAppearance = uIMollierLabelAppearance;

            uIMollierProcessPoint.UIMollierAppearance = uIMollierAppearance;

            Button_Vector2D.Content = locationSetText;
        }

        public MollierControl MollierControl
        {
            get { return mollierControl; }
            set { mollierControl = value; }
        }

        private void Button_Color_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                if (colorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                color = colorDialog.Color;
                ApplySwatch(Button_Color, color);
            }
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
