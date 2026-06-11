// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows.Controls;
using SAM.Geometry.Mollier;
using SAM.Core.Mollier.UI.Forms;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms MixingProcessControl (two airflow points).</summary>
    public partial class MixingProcessControl : UserControl, IMollierProcessControl
    {
        private MollierForm mollierForm;

        public event SelectMollierPointEventHandler SelectFirstMollierPoint;
        public event SelectMollierPointEventHandler SelectSecondMollierPoint;

        public MixingProcessControl()
        {
            InitializeComponent();

            FirstAirflowControl.ProcessParameterType = ProcessParameterType.Airflow;
            SecondAirflowControl.ProcessParameterType = ProcessParameterType.Airflow;
            MollierPointControl_SecondPoint.PressureVisible = false;

            MollierPointControl_SecondPoint.SelectMollierPoint += MollierPointControl_SecondPoint_SelectMollierPoint;
            MollierPointControl_FirstPoint.SelectMollierPoint += MollierPointControl_FirstPoint_SelectMollierPoint;
        }

        private void MollierPointControl_FirstPoint_SelectMollierPoint(object sender, SelectMollierPointEventArgs e)
        {
            SelectFirstMollierPoint?.Invoke(this, e);

            if (MollierForm != null)
            {
                MollierForm.MollierPointSelected += MollierForm_FirstMollierPointSelected;
            }
        }

        private void MollierForm_FirstMollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            if (MollierForm != null)
            {
                MollierForm.MollierPointSelected -= MollierForm_FirstMollierPointSelected;
            }

            FirstPoint = e.MollierPoint;
        }

        private void MollierPointControl_SecondPoint_SelectMollierPoint(object sender, SelectMollierPointEventArgs e)
        {
            SelectSecondMollierPoint?.Invoke(this, e);

            if (MollierForm != null)
            {
                MollierForm.MollierPointSelected += MollierForm_SecondMollierPointSelected;
            }
        }

        private void MollierForm_SecondMollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            if (MollierForm != null)
            {
                MollierForm.MollierPointSelected -= MollierForm_SecondMollierPointSelected;
            }

            SecondPoint = e.MollierPoint;
        }

        public UIMollierProcess GetUIMollierProcess()
        {
            MollierPointControl_SecondPoint.Pressure = MollierPointControl_FirstPoint.Pressure;
            MollierPoint firstPoint = FirstPoint;
            MollierPoint secondPoint = SecondPoint;
            double airflow_1 = FirstAirflowControl.Value;
            double airflow_2 = SecondAirflowControl.Value;
            MollierProcess mollierProcess = Mollier.Create.MixingProcess(firstPoint, secondPoint, airflow_1, airflow_2);
            return new UIMollierProcess(mollierProcess, SystemColor.Empty);
        }

        public MollierForm MollierForm
        {
            get { return mollierForm; }
            set
            {
                mollierForm = value;
                MollierPointControl_FirstPoint.SelectMollierPointVisible = value != null;
                MollierPointControl_SecondPoint.SelectMollierPointVisible = value != null;
                MollierPointControl_FirstPoint.PressureEnabled = value == null;
                MollierPointControl_SecondPoint.PressureEnabled = value == null;
            }
        }

        public MollierPoint FirstPoint
        {
            get { return MollierPointControl_FirstPoint.MollierPoint; }
            set { MollierPointControl_FirstPoint.MollierPoint = value; }
        }

        public MollierPoint SecondPoint
        {
            get { return MollierPointControl_SecondPoint.MollierPoint; }
            set { MollierPointControl_SecondPoint.MollierPoint = value; }
        }
    }
}
