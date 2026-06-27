// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SAM.Geometry.Mollier;
using SAM.Core.Mollier.UI.Controls;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms UIMollierProcessControl (start/process/end point rows + process colour).</summary>
    public partial class UIMollierProcessControl : UserControl
    {
        private UIMollierProcess uIMollierProcess;
        private SystemColor processColor = SystemColor.Empty;

        public event MollierPointSelectingEventHandler MollierPointSelecting;
        public event MollierPointSelectedEventHandler MollierPointSelected;

        public UIMollierProcessControl()
        {
            InitializeComponent();
            WirePointControls();
        }

        public UIMollierProcessControl(UIMollierProcess uIMollierProcess)
        {
            InitializeComponent();
            WirePointControls();
        }

        private void WirePointControls()
        {
            UIMollierProcessPointControl_Start.MollierPointSelected += UIMollierProcessPointControl_MollierPointSelected;
            UIMollierProcessPointControl_Process.MollierPointSelected += UIMollierProcessPointControl_MollierPointSelected;
            UIMollierProcessPointControl_End.MollierPointSelected += UIMollierProcessPointControl_MollierPointSelected;

            UIMollierProcessPointControl_Start.MollierPointSelecting += UIMollierProcessPointControl_MollierPointSelecting;
            UIMollierProcessPointControl_Process.MollierPointSelecting += UIMollierProcessPointControl_MollierPointSelecting;
            UIMollierProcessPointControl_End.MollierPointSelecting += UIMollierProcessPointControl_MollierPointSelecting;
        }

        private void UIMollierProcessPointControl_MollierPointSelecting(object sender, EventArgs e)
        {
            MollierPointSelecting?.Invoke(this, e);
        }

        private void UIMollierProcessPointControl_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            MollierPointSelected?.Invoke(this, e);
        }

        private void Button_ProcessColor_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                if (colorDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                processColor = colorDialog.Color;
                ApplySwatch(Button_ProcessColor, processColor);
            }
        }

        public UIMollierProcess UIMollierProcess
        {
            get { return GetUIMollierProcess(); }
            set { SetUIMollierProcess(value); }
        }

        private UIMollierProcess GetUIMollierProcess()
        {
            UIMollierProcess result = uIMollierProcess == null ? null : new UIMollierProcess(uIMollierProcess);
            if (result != null)
            {
                result.UIMollierPointAppearance_Start = UIMollierProcessPointControl_Start?.UIMollierProcessPoint?.UIMollierAppearance as UIMollierPointAppearance;
                result.UIMollierAppearance = UIMollierProcessPointControl_Process?.UIMollierProcessPoint?.UIMollierAppearance;
                result.UIMollierPointAppearance_End = UIMollierProcessPointControl_End?.UIMollierProcessPoint?.UIMollierAppearance as UIMollierPointAppearance;

                result.UIMollierAppearance.Color = processColor;
            }

            return result;
        }

        private void SetUIMollierProcess(UIMollierProcess uIMollierProcess)
        {
            this.uIMollierProcess = uIMollierProcess;

            UIMollierProcessPointControl_Start.UIMollierProcessPoint = uIMollierProcess == null ? null : new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.Start);
            UIMollierProcessPointControl_Process.UIMollierProcessPoint = uIMollierProcess == null ? null : new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.Process);
            UIMollierProcessPointControl_End.UIMollierProcessPoint = uIMollierProcess == null ? null : new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.End);

            processColor = SystemColor.Empty;

            UIMollierAppearance uIMollierAppearance = uIMollierProcess?.UIMollierAppearance as UIMollierAppearance;
            if (uIMollierAppearance != null)
            {
                if (uIMollierAppearance.Color != SystemColor.Empty)
                {
                    processColor = uIMollierAppearance.Color;
                }
            }

            ApplySwatch(Button_ProcessColor, processColor);
        }

        private void Button_ProcessColor_Clear_Click(object sender, RoutedEventArgs e)
        {
            processColor = SystemColor.Empty;
            ApplySwatch(Button_ProcessColor, processColor);
        }

        public MollierControl MollierControl
        {
            get { return UIMollierProcessPointControl_Start.MollierControl; }
            set
            {
                UIMollierProcessPointControl_Start.MollierControl = value;
                UIMollierProcessPointControl_Process.MollierControl = value;
                UIMollierProcessPointControl_End.MollierControl = value;
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
