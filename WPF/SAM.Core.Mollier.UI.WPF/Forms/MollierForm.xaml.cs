// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Mollier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SAM.Core.Mollier.UI
{
    /// <summary>WPF port of the WinForms MollierForm (the host window: menu + toolbar + embedded MollierControl).</summary>
    public partial class MollierForm : Window
    {
        private static string mollierControlSettingsPath = System.IO.Path.Combine(Core.Query.UserSAMTemporaryDirectory(), typeof(MollierControlSettings).Name);

        private Forms.MollierPointForm mollierPointForm = null;
        private Forms.MollierProcessForm mollierProcessForm = null;
        private Forms.UIMollierObjectsForm manageMollierObjectsForm = null;

        private UIMollierPoint previousUIMollierPoint = null;

        public event MollierPointSelectedEventHandler MollierPointSelected;

        private List<int> customColors = new List<int>();

        // Guards the toolbar TextChanged handlers from firing during InitializeComponent. WinForms wired
        // those handlers AFTER setting the designer Text values; WPF fires TextChanged as soon as Text is
        // set in XAML, while sibling controls (and their cross-references) may not exist yet.
        private readonly bool initialized;

        public MollierForm()
        {
            InitializeComponent();
            initialized = true;

            LoadMollierControlSettings();

            ColorPointComboBox.SelectedIndex = 1; // "Enthalpy"
        }

        private void MollierForm_Load(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.MollierPointSelected += MollierControl_Main_MollierPointSelected;

            MollierControl_Main.SizeChanged += MollierControl_Main_SizeChanged;

            ContentRendered -= MollierForm_Shown;
            ContentRendered += MollierForm_Shown;
        }

        private void MollierControl_Main_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MollierControl_Main.Regenerate();
        }

        private void MollierControl_Main_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            if (mollierProcessForm != null)
            {
                mollierProcessForm.Show();
            }

            MollierPointSelected?.Invoke(this, e);
        }

        private void resetChartToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void TextBox_Pressure_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized)
            {
                return;
            }

            if (!Core.Query.TryConvert(TextBox_Pressure.Text, out double pressure))
            {
                return;
            }

            if (Limit.Pressure_Min > pressure || pressure > Limit.Pressure_Max)
            {
                return;
            }
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.Pressure = pressure;
            mollierControlSettings.Elevation = System.Math.Round(Core.Query.Calculate_BinarySearch(x => Mollier.Query.Pressure(x), pressure, -1000, 5000));
            TextBox_Pressure.Text = mollierControlSettings.Pressure.ToString();

            TextBox_Elevation.TextChanged -= TextBox_Elevation_TextChanged;
            TextBox_Elevation.Text = mollierControlSettings.Elevation.ToString();
            TextBox_Elevation.TextChanged += TextBox_Elevation_TextChanged;

            MollierControl_Main.MollierControlSettings = mollierControlSettings;
        }

        private void TextBox_Elevation_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized)
            {
                return;
            }

            if (!Core.Query.TryConvert(TextBox_Elevation.Text, out double elevation))
            {
                return;
            }

            if (elevation < -1000 || elevation > 5000)
            {
                return;
            }

            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.Elevation = elevation;
            mollierControlSettings.Pressure = System.Math.Round(Mollier.Query.Pressure(elevation));
            TextBox_Elevation.Text = mollierControlSettings.Elevation.ToString();

            TextBox_Pressure.TextChanged -= TextBox_Pressure_TextChanged;
            TextBox_Pressure.Text = mollierControlSettings.Pressure.ToString();
            TextBox_Pressure.TextChanged += TextBox_Pressure_TextChanged;

            MollierControl_Main.MollierControlSettings = mollierControlSettings;
        }

        public double Pressure
        {
            get
            {
                return MollierControl_Main.MollierControlSettings.Pressure;
            }
            set
            {
                MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
                mollierControlSettings.Pressure = value;
                TextBox_Pressure.Text = value.ToString();
                MollierControl_Main.MollierControlSettings = mollierControlSettings;
            }
        }

        private void ToolStripMenuItem_OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MollierControlSettingsForm mollierSettingsForm = new MollierControlSettingsForm(MollierControl_Main);
            mollierSettingsForm.ApplyClicked += MollierSettingsForm_ApplyClicked;
            mollierSettingsForm.CustomColors = customColors;

            mollierSettingsForm.ShowDialog();
            if (!mollierSettingsForm.DialogOk)
            {
                return;
            }

            customColors = mollierSettingsForm.CustomColors;

            SaveMollierControlSettings();
        }

        private void MollierSettingsForm_ApplyClicked(object sender, EventArgs e)
        {
            MollierControlSettingsForm mollierControlSettingsForm = sender as MollierControlSettingsForm;
            if (mollierControlSettingsForm == null)
            {
                return;
            }

            LoadMollierControlSettings(MollierControl_Main.MollierControlSettings);
        }

        private void SaveMollierControlSettings()
        {
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (mollierControlSettings != null)
            {
                string directoryPath = System.IO.Path.GetDirectoryName(mollierControlSettingsPath);
                if (!System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                }

                Core.Convert.ToFile(mollierControlSettings, mollierControlSettingsPath);
            }
        }

        private void MollierForm_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveMollierControlSettings();
        }

        public bool Clear()
        {
            bool clear = MollierControl_Main.ClearObjects(false);

            if (manageMollierObjectsForm != null)
            {
                manageMollierObjectsForm.Refresh(MollierControl_Main.MollierModel);
            }

            return clear;
        }

        public void SaveAs(string path)
        {
            MollierControl_Main.Save(ChartExportType.EMF, path: path);
        }

        private MollierPoint GetMollierPoint()
        {
            if (!Core.Query.TryConvert(TextBox_Pressure.Text, out double pressure))
            {
                pressure = 101235;
            }

            double dryBulbTemperature = 35;
            double relativeHumidity = 50;
            double humidityRatio = double.NaN;

            if (previousUIMollierPoint != null && previousUIMollierPoint != null && previousUIMollierPoint.IsValid())
            {
                if (!double.IsNaN(previousUIMollierPoint.DryBulbTemperature))
                {
                    dryBulbTemperature = previousUIMollierPoint.DryBulbTemperature;
                }

                if (!double.IsNaN(previousUIMollierPoint.HumidityRatio))
                {
                    humidityRatio = previousUIMollierPoint.HumidityRatio;
                }
            }

            return double.IsNaN(humidityRatio) ? Mollier.Create.MollierPoint_ByRelativeHumidity(dryBulbTemperature, relativeHumidity, pressure) : new MollierPoint(dryBulbTemperature, humidityRatio, pressure);
        }

        private void ShowMollier()
        {
            if (ChartToolStripMenuItem_Mollier.IsChecked)
            {
                return;
            }
            ChartToolStripMenuItem_Mollier.IsChecked = !ChartToolStripMenuItem_Mollier.IsChecked;
            ChartToolStripMenuItem_Psychrometric.IsChecked = !ChartToolStripMenuItem_Mollier.IsChecked;

            MollierControl_Main.SizeChanged -= MollierControl_Main_SizeChanged;

            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (ChartToolStripMenuItem_Mollier.IsChecked)
            {
                mollierControlSettings.ChartType = ChartType.Mollier;
            }
            else if (ChartToolStripMenuItem_Psychrometric.IsChecked)
            {
                mollierControlSettings.ChartType = ChartType.Psychrometric;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            LoadMollierControlSettings(mollierControlSettings);

            MollierControl_Main.SizeChanged += MollierControl_Main_SizeChanged;

            MollierControl_Main.Regenerate();
        }

        private void ShowPsychrometric()
        {
            if (ChartToolStripMenuItem_Psychrometric.IsChecked)
            {
                return;
            }
            ChartToolStripMenuItem_Psychrometric.IsChecked = !ChartToolStripMenuItem_Psychrometric.IsChecked;
            ChartToolStripMenuItem_Mollier.IsChecked = !ChartToolStripMenuItem_Psychrometric.IsChecked;

            MollierControl_Main.SizeChanged -= MollierControl_Main_SizeChanged;

            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (ChartToolStripMenuItem_Mollier.IsChecked)
            {
                mollierControlSettings.ChartType = ChartType.Mollier;
            }
            else if (ChartToolStripMenuItem_Psychrometric.IsChecked)
            {
                mollierControlSettings.ChartType = ChartType.Psychrometric;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            LoadMollierControlSettings(mollierControlSettings);

            MollierControl_Main.SizeChanged += MollierControl_Main_SizeChanged;

            MollierControl_Main.Regenerate();
        }

        private void Reset()
        {
            MollierControlSettings mollierControlSettings = new MollierControlSettings();
            mollierControlSettings.Pressure = MollierControl_Main.MollierControlSettings.Pressure;
            LoadMollierControlSettings(mollierControlSettings);
            MollierControl_Main.Regenerate();
        }

        private void Save()
        {
            List<IJSAMObject> mollierObjects = new List<IJSAMObject>();

            List<UIMollierProcess> uIMollierProcesses = MollierControl_Main.UIMollierObjects<UIMollierProcess>();
            if (uIMollierProcesses != null)
            {
                mollierObjects.AddRange(uIMollierProcesses.Cast<IMollierObject>());
            }

            List<UIMollierPoint> uIMollierPoints = MollierControl_Main.UIMollierObjects<UIMollierPoint>();
            if (uIMollierPoints != null)
            {
                mollierObjects.AddRange(uIMollierPoints.Cast<IMollierObject>());
            }

            string path = null;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.FileName = MollierControl_Main.MollierControlSettings.ChartType == ChartType.Mollier ? "Mollier.json" : "Psychrometric.json";
            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }
            path = saveFileDialog.FileName;

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            mollierObjects.Add(MollierControlSettings);
            Core.Convert.ToFile(mollierObjects, path);
        }

        private void AddPoint()
        {
            Forms.MollierPointForm mollierPointForm = new Forms.MollierPointForm();
            mollierPointForm.MollierPoint = GetMollierPoint();

            mollierPointForm.ShowDialog();
            if (!mollierPointForm.DialogOk)
            {
                return;
            }

            previousUIMollierPoint = mollierPointForm.UIMollierPoint;
            AddMollierObjects(new UIMollierPoint[] { previousUIMollierPoint });
        }

        private void AddProcess()
        {
            if (mollierProcessForm == null)
            {
                mollierProcessForm = new Forms.MollierProcessForm();
                mollierProcessForm.MollierForm = this;
                mollierProcessForm.Closed += MollierProcessForm_FormClosing;
            }

            mollierProcessForm.PreviousMollierPoint = GetMollierPoint();
            mollierProcessForm.Show();
        }

        private void Edit()
        {
            if (manageMollierObjectsForm == null)
            {
                manageMollierObjectsForm = new Forms.UIMollierObjectsForm(MollierControl_Main.MollierModel, MollierControlSettings);

                manageMollierObjectsForm.Closed += ManageMollierObjectsForm_Closing;
                manageMollierObjectsForm.MollierModelEdited += ManageMollierObjectsForm_MollierModelEdited;
                manageMollierObjectsForm.MollierObjectSelected += ManageMollierObjectsForm_MollierObjectSelected;
            }
            manageMollierObjectsForm?.Show();
        }

        private void Epsilon()
        {
            MollierControl_Main.MollierPointSelected -= MollierControl_Main_MollierPointSelected_Epsilon;
            MollierControl_Main.MollierPointSelected += MollierControl_Main_MollierPointSelected_Epsilon;
        }

        private void SHR()
        {
            MollierControl_Main.MollierPointSelected -= MollierControl_Main_MollierPointSelected_SensibleHeatRatio;
            MollierControl_Main.MollierPointSelected += MollierControl_Main_MollierPointSelected_SensibleHeatRatio;
        }

        private void DivisionArea()
        {
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (ToolStripMenuItem_DivisionArea.IsChecked)
            {
                List<UIMollierPoint> mollierPoints = MollierControl_Main.UIMollierObjects<UIMollierPoint>();
                if (mollierPoints == null || mollierPoints.Count == 0)
                {
                    MessageBox.Show("There are no points");
                    ToolStripMenuItem_DivisionArea.IsChecked = false;
                    return;
                }
                mollierControlSettings.DivisionArea = true;
                DivisionAreaLabels_CheckBox.Visibility = Visibility.Visible;
            }
            else
            {
                mollierControlSettings.DivisionArea = false;
                DivisionAreaLabels_CheckBox.Visibility = Visibility.Collapsed;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void CoolingAuxiliaryProcessesVisibility()
        {
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.DisableCoolingAuxiliaryProcesses = !mollierControlSettings.DisableCoolingAuxiliaryProcesses;

            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void MollierProcessForm_FormClosing(object sender, EventArgs e)
        {
            UIMollierProcess uIMollierProcess = mollierProcessForm?.GetUIMollierProcess();
            if (uIMollierProcess == null)
            {
                mollierProcessForm = null;
                return;
            }

            if (!mollierProcessForm.DialogOk)
            {
                mollierProcessForm = null;
                return;
            }

            mollierProcessForm = null;

            previousUIMollierPoint = uIMollierProcess.GetUIMollierPoint_End();
            List<IMollierProcess> mollierProcesses = new List<IMollierProcess>() { uIMollierProcess };

            AddMollierObjects(mollierProcesses);
        }

        private void MollierPointForm_FormClosing(object sender, EventArgs e)
        {
            if (mollierPointForm == null || !mollierPointForm.DialogOk)
            {
                return;
            }

            MollierPoint mollierPoint = mollierPointForm.MollierPoint;
            if (mollierPoint == null)
            {
                return;
            }

            MollierControl_Main.AddMollierObjects(new MollierPoint[] { mollierPoint });
        }

        //disable some function for data reading only
        public bool ReadOnly
        {
            set
            {
                //TextBox_Pressure.ReadOnly = value;
                //TextBox_Elevation.ReadOnly = value;
                //Button_AddPoint.Visible = !value;
                //Button_AddProcess.Visible = !value;
            }
        }

        public bool AddMollierObjects<T>(IEnumerable<T> mollierObjects, bool checkPressure = true, bool regenerate = true) where T : IMollierObject
        {
            if (mollierObjects == null)
            {
                return false;
            }

            MollierControl_Main.AddMollierObjects(mollierObjects, checkPressure, regenerate);
            if (regenerate && manageMollierObjectsForm != null)
            {
                manageMollierObjectsForm.Refresh(MollierControl_Main.MollierModel);
            }

            return true;
        }

        public void Show(bool regenerate)
        {
            if (regenerate)
            {
                ContentRendered -= MollierForm_Shown_Regenerate;
                ContentRendered += MollierForm_Shown_Regenerate;
            }

            Show();
        }

        private void MollierForm_Shown_Regenerate(object sender, EventArgs e)
        {
            ContentRendered -= MollierForm_Shown_Regenerate;

            MollierControl_Main.Regenerate();

            if (manageMollierObjectsForm != null)
            {
                manageMollierObjectsForm.Refresh(MollierControl_Main.MollierModel);
            }
        }

        //function that sets all values from the control to the Form
        public void LoadMollierControlSettings(MollierControlSettings mollierControlSettings = null)
        {
            if (mollierControlSettings == null)
            {
                mollierControlSettings = System.IO.File.Exists(mollierControlSettingsPath) ? Core.Convert.ToSAM<MollierControlSettings>(mollierControlSettingsPath).FirstOrDefault() : new MollierControlSettings();
            }

            if (mollierControlSettings.VisibilitySettings.GetColor(mollierControlSettings.DefaultTemplateName, ChartParameterType.BoldLine, ChartDataType.DryBulbTemperature) == System.Drawing.Color.Empty)
            {
                MollierControlSettings mollierControlSettings_Default = new MollierControlSettings();

                mollierControlSettings.VisibilitySettings.Add(mollierControlSettings.DefaultTemplateName, mollierControlSettings_Default.VisibilitySettings.GetVisibilitySetting(mollierControlSettings.DefaultTemplateName, ChartParameterType.Line, ChartDataType.DryBulbTemperature));
                mollierControlSettings.VisibilitySettings.Add(mollierControlSettings.DefaultTemplateName, mollierControlSettings_Default.VisibilitySettings.GetVisibilitySetting(mollierControlSettings.DefaultTemplateName, ChartParameterType.BoldLine, ChartDataType.DryBulbTemperature));
            }

            ChartToolStripMenuItem_Mollier.IsChecked = mollierControlSettings.ChartType == ChartType.Mollier;
            ChartToolStripMenuItem_Psychrometric.IsChecked = mollierControlSettings.ChartType == ChartType.Psychrometric;
            ToolStripMenuItem_Density.IsChecked = mollierControlSettings.Density_Line;
            ToolStripMenuItem_Enthalpy.IsChecked = mollierControlSettings.Enthalpy_Line;
            ToolStripMenuItem_SpecificVolume.IsChecked = mollierControlSettings.SpecificVolume_Line;
            ToolStripMenuItem_WetBulbTemperature.IsChecked = mollierControlSettings.WetBulbTemperature_Line;
            ToolStripMenuItem_PartialVapourPressure.IsChecked = mollierControlSettings.PartialVapourPressure_Axis;
            defaultToolStripMenuItem.IsChecked = mollierControlSettings.DefaultTemplateName == "default";
            blueToolStripMenuItem.IsChecked = mollierControlSettings.DefaultTemplateName == "blue";
            grayToolStripMenuItem.IsChecked = mollierControlSettings.DefaultTemplateName == "gray";
            blueBlackToolStripMenuItem.IsChecked = mollierControlSettings.DefaultTemplateName == "blue-black";
            if (MollierControl_Main != null)
            {
                MollierControl_Main.MollierControlSettings = mollierControlSettings;
            }

            MollierControlSettings mollierControlSettings_Temp = MollierControlSettings;
            if (mollierControlSettings_Temp != null)
            {
                TextBox_Pressure.Text = mollierControlSettings_Temp.Pressure.ToString();
            }

            double currentWidth = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 1100 : Width);
            double currentHeight = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 800 : Height);
            double width = currentWidth;
            double height = currentHeight;
            switch (mollierControlSettings.ChartType)
            {
                case ChartType.Psychrometric:
                    width = mollierControlSettings.PsychrometricWindowWidth == -1 ? currentWidth : mollierControlSettings.PsychrometricWindowWidth;
                    height = mollierControlSettings.PsychrometricWindowHeight == -1 ? currentHeight : mollierControlSettings.PsychrometricWindowHeight;
                    WindowState = (mollierControlSettings.PsychrometricWindowWidth == -1 || mollierControlSettings.PsychrometricWindowHeight == -1) ? WindowState.Maximized : WindowState.Normal;
                    break;

                case ChartType.Mollier:
                    width = mollierControlSettings.MollierWindowWidth == -1 ? currentWidth : mollierControlSettings.MollierWindowWidth;
                    height = mollierControlSettings.MollierWindowHeight == -1 ? currentHeight : mollierControlSettings.MollierWindowHeight;
                    WindowState = (mollierControlSettings.MollierWindowWidth == -1 || mollierControlSettings.MollierWindowHeight == -1) ? WindowState.Maximized : WindowState.Normal;
                    break;
            }

            if (width > 0 && height > 0)
            {
                Width = width;
                Height = height;
            }
        }

        //buttons which enable to change color, chart or disable line
        private void blueToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (defaultToolStripMenuItem.IsChecked)
            {
                defaultToolStripMenuItem.IsChecked = false;
            }
            if (grayToolStripMenuItem.IsChecked)
            {
                grayToolStripMenuItem.IsChecked = false;
            }
            if (blueBlackToolStripMenuItem.IsChecked)
            {
                blueBlackToolStripMenuItem.IsChecked = false;
            }
            blueToolStripMenuItem.IsChecked = true;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.DefaultTemplateName = "blue";
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void grayToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (defaultToolStripMenuItem.IsChecked)
            {
                defaultToolStripMenuItem.IsChecked = false;
            }
            if (blueToolStripMenuItem.IsChecked)
            {
                blueToolStripMenuItem.IsChecked = false;
            }
            if (blueBlackToolStripMenuItem.IsChecked)
            {
                blueBlackToolStripMenuItem.IsChecked = false;
            }
            grayToolStripMenuItem.IsChecked = true;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.DefaultTemplateName = "gray";
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void defaultToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (blueToolStripMenuItem.IsChecked)
            {
                blueToolStripMenuItem.IsChecked = false;
            }
            if (grayToolStripMenuItem.IsChecked)
            {
                grayToolStripMenuItem.IsChecked = false;
            }
            if (blueBlackToolStripMenuItem.IsChecked)
            {
                blueBlackToolStripMenuItem.IsChecked = false;
            }
            defaultToolStripMenuItem.IsChecked = true;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.DefaultTemplateName = "default";
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void blueBlackToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (blueToolStripMenuItem.IsChecked)
            {
                blueToolStripMenuItem.IsChecked = false;
            }
            if (grayToolStripMenuItem.IsChecked)
            {
                grayToolStripMenuItem.IsChecked = false;
            }
            if (defaultToolStripMenuItem.IsChecked)
            {
                defaultToolStripMenuItem.IsChecked = false;
            }
            blueBlackToolStripMenuItem.IsChecked = true;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.DefaultTemplateName = "blue-black";
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void ChartToolStripMenuItem_Mollier_Click(object sender, RoutedEventArgs e)
        {
            ShowMollier();
        }

        private void ChartToolStripMenuItem_Psychrometric_Click(object sender, RoutedEventArgs e)
        {
            ShowPsychrometric();
        }

        private void ToolStripMenuItem_Density_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_Density.IsChecked = !ToolStripMenuItem_Density.IsChecked;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.Density_Line = ToolStripMenuItem_Density.IsChecked;
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void ToolStripMenuItem_Enthalpy_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_Enthalpy.IsChecked = !ToolStripMenuItem_Enthalpy.IsChecked;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.Enthalpy_Line = ToolStripMenuItem_Enthalpy.IsChecked;
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void ToolStripMenuItem_SpecificVolume_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_SpecificVolume.IsChecked = !ToolStripMenuItem_SpecificVolume.IsChecked;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.SpecificVolume_Line = ToolStripMenuItem_SpecificVolume.IsChecked;
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void ToolStripMenuItem_WetBulbTemperature_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_WetBulbTemperature.IsChecked = !ToolStripMenuItem_WetBulbTemperature.IsChecked;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.WetBulbTemperature_Line = ToolStripMenuItem_WetBulbTemperature.IsChecked;
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        public MollierControlSettings MollierControlSettings
        {
            get
            {
                return MollierControl_Main?.MollierControlSettings;
            }

            set
            {
                if (MollierControl_Main != null)
                {
                    MollierControl_Main.MollierControlSettings = value;
                }
            }
        }

        private void CheckBox_Hoover_CheckedChanged(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.EnableHoover = CheckBox_Hoover.IsChecked == true;
        }

        private void saveAsJPGToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.Save(ChartExportType.JPG);
        }

        // PDF export pipeline (NetOffice Excel) is deferred in the WPF port; Save(PDF) is currently a no-op.
        private void PdfA3_PortraitToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.Save(ChartExportType.PDF);
        }

        private void PdfA3_LandscapeToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.Save(ChartExportType.PDF);
        }

        private void a4PortraitToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.Save(ChartExportType.PDF);
        }

        private void a4LandscapeToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.Save(ChartExportType.PDF);
        }

        private void PointsCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;

            if (PointsCheckBox.IsChecked == true)
            {
                List<UIMollierPoint> mollierPoints = MollierControl_Main.UIMollierObjects<UIMollierPoint>();
                if (mollierPoints == null || mollierPoints.Count < 4)
                {
                    MessageBox.Show("The minimum number of points on the chart required to run this method is 4.", "Error");
                    PointsCheckBox.IsChecked = false;
                    PercentPointsTextBox.Visibility = Visibility.Collapsed;
                    PointsLabel.Visibility = Visibility.Collapsed;
                    ColorPointComboBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    mollierControlSettings.FindPoint = true;
                    mollierControlSettings.FindPoint_Factor = 0.4;
                    mollierControlSettings.FindPointType = ChartDataType.Enthalpy;
                    PercentPointsTextBox.Visibility = Visibility.Visible;
                    PointsLabel.Visibility = Visibility.Visible;
                    ColorPointComboBox.Visibility = Visibility.Visible;
                }
            }
            else
            {
                mollierControlSettings.FindPoint = false;
                PercentPointsTextBox.Visibility = Visibility.Collapsed;
                PointsLabel.Visibility = Visibility.Collapsed;
                ColorPointComboBox.Visibility = Visibility.Collapsed;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void TextBox_KeyPress(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (e.Text == ".")
            {
                // only allow one decimal point
                e.Handled = textBox != null && textBox.Text.IndexOf('.') > -1;
                return;
            }
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void PercentPointsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized)
            {
                return;
            }

            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (Core.Query.TryConvert(PercentPointsTextBox.Text, out double value))
            {
                mollierControlSettings.FindPoint_Factor = value;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void ColorPointComboBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPointComboBox.Visibility != Visibility.Visible)
            {
                return;
            }

            string colorPointText = (ColorPointComboBox.SelectedItem as ComboBoxItem)?.Content as string;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (colorPointText == "Enthalpy")
            {
                mollierControlSettings.FindPointType = ChartDataType.Enthalpy;
            }
            else if (colorPointText == "Temperature")
            {
                mollierControlSettings.FindPointType = ChartDataType.DryBulbTemperature;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void DivisionAreaLabels_CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            if (DivisionAreaLabels_CheckBox.IsChecked == true)
            {
                mollierControlSettings.DivisionAreaLabels = false;
            }
            else
            {
                mollierControlSettings.DivisionAreaLabels = true;
            }
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void saveAsEMFToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveAs(null);
        }

        private void OpenToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string path = null;

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 2;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }
            path = openFileDialog.FileName;

            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            List<IMollierObject> mollierObjects = Core.Convert.ToSAM<IMollierObject>(path);
            if (mollierObjects == null || mollierObjects.Count == 0)
            {
                return;
            }
            Forms.OpenJSONForm.Action action = Forms.OpenJSONForm.Action.Undefined;

            Forms.OpenJSONForm openJSONForm = new Forms.OpenJSONForm();
            openJSONForm.ShowDialog();
            if (!openJSONForm.DialogOk)
            {
                return;
            }
            action = openJSONForm.GetAction();

            switch (action)
            {
                case Forms.OpenJSONForm.Action.Undefined:
                    return;

                case Forms.OpenJSONForm.Action.Replace:
                    Clear();
                    MollierControlSettings mollierControlSettings = System.IO.File.Exists(path) ? Core.Convert.ToSAM<MollierControlSettings>(path).Find(x => x != null) : null;
                    if (mollierControlSettings != null)
                    {
                        MollierControl_Main.MollierControlSettings = mollierControlSettings;
                        LoadMollierControlSettings(mollierControlSettings);
                    }
                    break;
            }

            LoadMollierObjects(mollierObjects);
        }

        private void SaveToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        public void LoadMollierObjects(IEnumerable<IMollierObject> mollierObjects)
        {
            AddMollierObjects(mollierObjects);
        }

        private void printToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // WinForms Print pipeline is deferred in the WPF port.
            MessageBox.Show("Printing is not yet available in the WPF Mollier chart.", "Print");
        }

        private void newToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MollierControl_Main.ClearObjects();
            PointsCheckBox.IsChecked = false;
        }

        private void MollierControl_Main_MollierPointSelected_Epsilon(object sender, MollierPointSelectedEventArgs e)
        {
            AddProcess_ByEpsilonAndHumidityRatioDifference(e);
        }

        private void MollierControl_Main_MollierPointSelected_SensibleHeatRatio(object sender, MollierPointSelectedEventArgs e)
        {
            AddProcess_BySensibleHeatRatio(e);
        }

        private void AddProcess_BySensibleHeatRatio(MollierPointSelectedEventArgs e)
        {
            MollierControl_Main.MollierPointSelected -= MollierControl_Main_MollierPointSelected_SensibleHeatRatio;

            MollierPoint mollierPoint = e.MollierPoint;
            if (mollierPoint == null)
            {
                return;
            }

            if (double.IsNaN(mollierPoint.RelativeHumidity))
            {
                MessageBox.Show("Select point with relative humidity less than 100%");
                return;
            }

            double sensibleHeatRatio = double.NaN;
            using (Windows.Forms.TextBoxForm<double> textBoxForm = new Windows.Forms.TextBoxForm<double>("Sensible Heat Ratio", "Sensible Heat Ratio (SHR) [0-1]"))
            {
                textBoxForm.Value = 0.85;
                if (textBoxForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                sensibleHeatRatio = textBoxForm.Value;
            }

            if (double.IsNaN(sensibleHeatRatio))
            {
                return;
            }

            MollierSensibleHeatRatioLine mollierSensibleHeatRatioLine = new MollierSensibleHeatRatioLine(mollierPoint, sensibleHeatRatio);

            AddMollierObjects(new IMollierCurve[] { new UIMollierCurve(mollierSensibleHeatRatioLine, System.Drawing.Color.LightGray) }, false);
        }

        private void AddProcess_ByEpsilonAndEnthalpyDifference(MollierPointSelectedEventArgs e)
        {
            MollierControl_Main.MollierPointSelected -= MollierControl_Main_MollierPointSelected_Epsilon;

            MollierPoint mollierPoint = e.MollierPoint;
            if (mollierPoint == null)
            {
                return;
            }

            double epsilon = double.NaN;
            using (Windows.Forms.TextBoxForm<double> textBoxForm = new Windows.Forms.TextBoxForm<double>("Epsilon", "Epsilon [ε=Δh/Δx]"))
            {
                textBoxForm.Value = 2501;
                if (textBoxForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                epsilon = textBoxForm.Value;
            }

            if (double.IsNaN(epsilon))
            {
                return;
            }

            double enthalpyDifference = double.NaN;
            using (Windows.Forms.TextBoxForm<double> textBoxForm = new Windows.Forms.TextBoxForm<double>("Enthalpy Difference", "Enthalpy Difference (kJ/kg)"))
            {
                textBoxForm.Value = 10;
                if (textBoxForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                enthalpyDifference = textBoxForm.Value;
            }

            if (double.IsNaN(enthalpyDifference))
            {
                return;
            }

            RoomProcess roomProcess = Mollier.Create.RoomProcess_ByEpsilonAndEnthalpyDifference(mollierPoint, epsilon, enthalpyDifference * 1000);
            if (roomProcess == null)
            {
                return;
            }

            UIMollierProcess uIMollierProcess = new UIMollierProcess(roomProcess, System.Drawing.Color.LightGray);

            AddMollierObjects(new IMollierProcess[] { uIMollierProcess }, false);
        }

        private void AddProcess_ByEpsilonAndHumidityRatioDifference(MollierPointSelectedEventArgs e)
        {
            MollierControl_Main.MollierPointSelected -= MollierControl_Main_MollierPointSelected_Epsilon;

            MollierPoint mollierPoint = e.MollierPoint;
            if (mollierPoint == null)
            {
                return;
            }

            if (double.IsNaN(mollierPoint.RelativeHumidity))
            {
                MessageBox.Show("Select point with relative humidity less than 100%");
                return;
            }

            double epsilon = double.NaN;
            using (Windows.Forms.TextBoxForm<double> textBoxForm = new Windows.Forms.TextBoxForm<double>("Epsilon", "Epsilon [ε=Δh/Δx]"))
            {
                textBoxForm.Value = 2501;
                if (textBoxForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                epsilon = textBoxForm.Value;
            }

            if (double.IsNaN(epsilon))
            {
                return;
            }

            double humidityRatio = double.NaN;
            using (Windows.Forms.TextBoxForm<double> textBoxForm = new Windows.Forms.TextBoxForm<double>("Humidity Ratio", "Humidity Ratio [g/kg] of the end of the process"))
            {
                textBoxForm.Value = 10;
                textBoxForm.Size = new System.Drawing.Size((int)(textBoxForm.Size.Width * 1.2), textBoxForm.Size.Height);

                if (textBoxForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                humidityRatio = textBoxForm.Value;
            }

            if (double.IsNaN(humidityRatio))
            {
                return;
            }

            RoomProcess roomProcess = Mollier.Create.RoomProcess_ByEpsilonAndHumidityRatioDifference(mollierPoint, epsilon, (humidityRatio / 1000) - mollierPoint.HumidityRatio);
            if (roomProcess == null)
            {
                return;
            }

            UIMollierProcess uIMollierProcess = new UIMollierProcess(roomProcess, System.Drawing.Color.LightGray);

            AddMollierObjects(new IMollierProcess[] { uIMollierProcess }, false);
        }

        private void ManageMollierObjectsForm_MollierObjectSelected(object sender, MollierObjectSelectedArgs e)
        {
            MollierControl_Main.Select(e.MollierObject);
        }

        private void ManageMollierObjectsForm_MollierModelEdited(object sender, MollierModelEditedEventArgs e)
        {
            MollierControl_Main.MollierModel = e.MollierModel;
            MollierControl_Main.Regenerate();
        }

        private void ManageMollierObjectsForm_Closing(object sender, EventArgs e)
        {
            manageMollierObjectsForm = null;
        }

        private void ToolStripMenuItem_PartialVapourPressure_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_PartialVapourPressure.IsChecked = !ToolStripMenuItem_PartialVapourPressure.IsChecked;
            MollierControlSettings mollierControlSettings = MollierControl_Main.MollierControlSettings;
            mollierControlSettings.PartialVapourPressure_Axis = ToolStripMenuItem_PartialVapourPressure.IsChecked;
            MollierControl_Main.MollierControlSettings = mollierControlSettings;
            MollierControl_Main.Regenerate();
        }

        private void ToolStripMenuItem_ComfortZoners_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_ComfortZones.IsChecked = !ToolStripMenuItem_ComfortZones.IsChecked;

            if (ToolStripMenuItem_ComfortZones.IsChecked)
            {
                MollierControl_Main.AddMollierObjects(Query.MollierZones());
            }
            else
            {
                MollierControl_Main.RemoveZones(Query.MollierZones());
            }
        }

        // PreviewKeyDown (tunnelling) so the window intercepts the Ctrl shortcuts BEFORE the focused child
        // (chart / text box) — the WPF equivalent of the WinForms KeyPreview = true. Each handled shortcut
        // is marked e.Handled so the focused control does not also act on the key (e.g. Ctrl+C copy).
        private void MollierForm_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.E:
                    SaveAs(null);
                    break;
                case Key.S:
                    Save();
                    break;
                case Key.M:
                    ShowMollier();
                    break;
                case Key.P:
                    ShowPsychrometric();
                    break;
                case Key.D:
                    Edit();
                    break;
                case Key.R:
                    AddProcess();
                    break;
                case Key.O:
                    AddPoint();
                    break;
                case Key.C:
                    CoolingAuxiliaryProcessesVisibility();
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        private void ToolStripMenuItem_Wiki_Click(object sender, RoutedEventArgs e)
        {
            // .NET (Core) defaults ProcessStartInfo.UseShellExecute to false, so Process.Start(url) throws
            // Win32Exception ("The system cannot find the file specified"). Opening a URL needs the shell.
            const string url = "https://github.com/HoareLea/SAM_Mollier/wiki/HomeUI";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                MessageBox.Show("Could not open the wiki page:" + Environment.NewLine + exception.Message, "Wiki");
            }
        }

        private void ToolStripMenuItem_AddPoint_Click(object sender, RoutedEventArgs e)
        {
            AddPoint();
        }

        private void ToolStripMenuItem_AddProcess_Click(object sender, RoutedEventArgs e)
        {
            AddProcess();
        }

        private void ToolStripMenuItem_Edit_Click(object sender, RoutedEventArgs e)
        {
            Edit();
        }

        private void ToolStripMenuItem_Epsilon_Click(object sender, RoutedEventArgs e)
        {
            Epsilon();
        }

        private void ToolStripMenuItem_SHR_Click(object sender, RoutedEventArgs e)
        {
            SHR();
        }

        private void ToolStripMenuItem_DivisionArea_Click(object sender, RoutedEventArgs e)
        {
            ToolStripMenuItem_DivisionArea.IsChecked = !ToolStripMenuItem_DivisionArea.IsChecked;
            DivisionArea();
        }

        private void MollierForm_Shown(object sender, EventArgs e)
        {
            MollierControl_Main.Regenerate();
        }
    }
}
