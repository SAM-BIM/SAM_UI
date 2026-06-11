// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows.Controls;
using System.Windows.Media;
using SAM.Geometry.Mollier;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of the WinForms UIMollierAppearanceControl: a colour-swatch button + label text box.
    /// The WinForms Button.BackColor sentinel (SystemColors.Control = "no colour") becomes an
    /// explicit System.Drawing.Color field (Color.Empty = none). Colour picking uses the WinForms
    /// ColorDialog (available via the OxyPlot.WindowsForms transitive reference).
    /// </summary>
    public partial class UIMollierAppearanceControl : UserControl
    {
        private SystemColor color = SystemColor.Empty;

        public UIMollierAppearanceControl()
        {
            InitializeComponent();
            ApplySwatch();
        }

        public UIMollierAppearanceControl(UIMollierAppearance uIMollierAppearance)
        {
            InitializeComponent();
            SetUIMollierAppearance(uIMollierAppearance);
        }

        public UIMollierAppearance UIMollierAppearance
        {
            get { return GetUIMollierAppearance(); }
            set { SetUIMollierAppearance(value); }
        }

        private UIMollierAppearance GetUIMollierAppearance()
        {
            return new UIMollierAppearance(color, TextBox_Label.Text);
        }

        private void SetUIMollierAppearance(UIMollierAppearance uIMollierAppearance)
        {
            if (uIMollierAppearance == null)
            {
                color = SystemColor.Empty;
                TextBox_Label.Text = null;
                ApplySwatch();
                return;
            }

            color = uIMollierAppearance.Color;
            TextBox_Label.Text = uIMollierAppearance.Label;
            ApplySwatch();
        }

        private void ApplySwatch()
        {
            // Color.Empty -> default button background (the WinForms SystemColors.Control sentinel).
            Button_Color.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            if (color != SystemColor.Empty)
            {
                Button_Color.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            }
        }

        private void Button_Color_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            using (System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog())
            {
                colorDialog.Color = color == SystemColor.Empty ? SystemColor.Empty : color;
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    color = colorDialog.Color;
                    ApplySwatch();
                }
            }
        }
    }
}
