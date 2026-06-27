// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Controls;
using SAM.Geometry.Mollier;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>WPF port of the WinForms PointListOptionForm (labelled-point picker dialog).</summary>
    public partial class PointListOptionForm : Window
    {
        private MollierControl mollierControl;
        private MollierPoint resultPoint;

        /// <summary>True when a point was chosen. Replaces WinForms DialogResult.</summary>
        public bool DialogOk { get; private set; }

        public PointListOptionForm()
        {
            InitializeComponent();
        }

        public MollierPoint MollierPoint
        {
            get
            {
                return resultPoint;
            }
        }

        public PointListOptionForm(MollierControl mollierControl)
        {
            InitializeComponent();
            this.mollierControl = mollierControl;
            List<UIMollierProcess> mollierProcesses = mollierControl.UIMollierObjects<UIMollierProcess>();
            if (mollierProcesses == null)
            {
                return;
            }

            foreach (UIMollierProcess UI_MollierProcess in mollierProcesses)
            {
                MollierPoint start = UI_MollierProcess.Start;
                string start_Label = UI_MollierProcess.UIMollierPointAppearance_Start.Label;
                if (start_Label != null && start_Label != "")
                {
                    flowLayoutPanel1.Children.Add(new ListPointsOptionControl(start, start_Label, this));
                }
                MollierPoint end = UI_MollierProcess.End;
                string end_Label = UI_MollierProcess.UIMollierPointAppearance_End.Label;
                if (end_Label != null && end_Label != "")
                {
                    flowLayoutPanel1.Children.Add(new ListPointsOptionControl(end, end_Label, this));
                }
            }
        }

        public void ChosenPoint(MollierPoint mollierPoint)
        {
            resultPoint = mollierPoint;
            if (resultPoint != null)
            {
                DialogOk = true;
            }
            Close();
        }
    }
}
