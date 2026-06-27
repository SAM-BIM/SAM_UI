// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SAM.Geometry.Mollier;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms UIMollierProcessControl_Limited (process colour + start/process/end labels).</summary>
    public partial class UIMollierProcessControl_Limited : UserControl
    {
        private UIMollierProcess uIMollierProcess;
        private SystemColor processColor = SystemColor.Empty;

        public UIMollierProcessControl_Limited()
        {
            InitializeComponent();
        }

        public UIMollierProcessControl_Limited(UIMollierProcess uIMollierProcess)
        {
            InitializeComponent();
            if (uIMollierProcess == null)
            {
                return;
            }
            setUIMollierProcess(uIMollierProcess);
        }

        public UIMollierProcess UIMollierProcess
        {
            get
            {
                if (uIMollierProcess == null || uIMollierProcess.UIMollierAppearance == null)
                {
                    return null;
                }

                uIMollierProcess.UIMollierAppearance.Color = processColor;
                uIMollierProcess.UIMollierPointAppearance_Start.Label = StartLabel_Value.Text;
                (uIMollierProcess.UIMollierAppearance as UIMollierAppearance).Label = ProcessLabel_Value.Text;
                uIMollierProcess.UIMollierPointAppearance_End.Label = EndLabel_Value.Text;

                return uIMollierProcess;
            }
            set
            {
                setUIMollierProcess(value);
            }
        }

        private void setUIMollierProcess(UIMollierProcess uIMollierProcess)
        {
            if (uIMollierProcess == null)
            {
                return;
            }

            this.uIMollierProcess = uIMollierProcess;

            if (uIMollierProcess.UIMollierAppearance != null)
            {
                processColor = uIMollierProcess.UIMollierAppearance.Color;
                ApplySwatch(ProcessColor_button, processColor);
                StartLabel_Value.Text = uIMollierProcess.UIMollierPointAppearance_Start.Label;
                ProcessLabel_Value.Text = (uIMollierProcess.UIMollierAppearance as UIMollierAppearance).Label;
                EndLabel_Value.Text = uIMollierProcess.UIMollierPointAppearance_End.Label;
            }
        }

        private void ProcessColor_button_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                if (colorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                processColor = colorDialog.Color;
                ApplySwatch(ProcessColor_button, processColor);
            }
        }

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            processColor = SystemColor.Empty;
            ApplySwatch(ProcessColor_button, processColor);
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
