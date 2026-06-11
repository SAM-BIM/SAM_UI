// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Forms;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms ListPointsOptionControl (one labelled point row in the point picker).</summary>
    public partial class ListPointsOptionControl : UserControl
    {
        private MollierPoint mollierPoint;
        private PointListOptionForm parent;
        private string label;

        public ListPointsOptionControl()
        {
            InitializeComponent();
        }

        public ListPointsOptionControl(MollierPoint mollierPoint, string label, PointListOptionForm pointListOptionForm)
        {
            InitializeComponent();
            parent = pointListOptionForm;
            this.mollierPoint = mollierPoint;
            this.label = label;
            if (label != null && label != "")
            {
                nameLabel.Text = label;
            }
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            parent.ChosenPoint(mollierPoint);
        }
    }
}
