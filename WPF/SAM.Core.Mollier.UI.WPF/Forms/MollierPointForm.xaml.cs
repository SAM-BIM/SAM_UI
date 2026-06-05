// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;
using SAM.Geometry.Mollier;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>WPF port of the WinForms MollierPointForm (point editor dialog).</summary>
    public partial class MollierPointForm : Window
    {
        public event EventHandler SelectPointClicked;

        /// <summary>True when closed via OK. Replaces WinForms DialogResult (WPF DialogResult throws on modeless windows).</summary>
        public bool DialogOk { get; private set; }

        public MollierPointForm()
        {
            InitializeComponent();

            // Default appearance (WinForms designer seeded: empty colour, blank label, size 1, visible).
            UIMollierAppearanceControl_Main.UIMollierAppearance = new UIMollierAppearance(SystemColor.Empty, "") { Size = 1, Visible = true };

            MollierPointControl_Main.SelectMollierPoint += MollierPointControl_Main_SelectPointClicked;
        }

        private void MollierPointControl_Main_SelectPointClicked(object sender, EventArgs e)
        {
            SelectPointClicked?.Invoke(this, e);
        }

        public UIMollierPoint UIMollierPoint
        {
            get
            {
                UIMollierAppearance uIMollierAppearance = UIMollierAppearance;
                if (uIMollierAppearance == null)
                {
                    return new UIMollierPoint(MollierPoint, null);
                }

                if (uIMollierAppearance is UIMollierPointAppearance)
                {
                    return new UIMollierPoint(MollierPoint, (UIMollierPointAppearance)uIMollierAppearance);
                }

                if (uIMollierAppearance is UIMollierAppearance)
                {
                    return new UIMollierPoint(MollierPoint, new UIMollierPointAppearance(uIMollierAppearance));
                }

                return null;
            }
            set
            {
                MollierPoint = value;
                UIMollierAppearance = value?.UIMollierAppearance as UIMollierAppearance;
            }
        }

        public MollierPoint MollierPoint
        {
            get { return MollierPointControl_Main.MollierPoint; }
            set { MollierPointControl_Main.MollierPoint = value; }
        }

        public UIMollierAppearance UIMollierAppearance
        {
            get { return UIMollierAppearanceControl_Main.UIMollierAppearance; }
            set { UIMollierAppearanceControl_Main.UIMollierAppearance = value; }
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = false;
            Close();
        }
    }
}
