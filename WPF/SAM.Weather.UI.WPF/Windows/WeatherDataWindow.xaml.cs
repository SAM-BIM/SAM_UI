// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using SAM.Core.UI;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Weather.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Weather.Windows.Forms.WeatherDataForm (+ WeatherDataControl).
    /// Shows a WeatherData's identity, a category-grouped custom-parameter grid
    /// (<see cref="SAM.Core.UI.WPF.ParametersControl"/>), a values table and one OxyPlot line chart per
    /// weather data type. Mirrors the legacy public API (the two constructors and the WeatherData
    /// property). The legacy WinForms DataVisualization chart and the WM_SETREDRAW P/Invoke (a
    /// DataGridView redraw-suspend optimisation) are intentionally dropped.
    /// </summary>
    public partial class WeatherDataWindow : Window
    {
        private WeatherData weatherData;
        private HashSet<Enum> enums;

        public WeatherDataWindow()
        {
            InitializeComponent();

            enums = new HashSet<Enum>(Core.Query.Enums(typeof(WeatherData)));

            LoadWeatherData(null);
        }

        public WeatherDataWindow(WeatherData weatherData, IEnumerable<Enum> enums)
        {
            InitializeComponent();

            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            WeatherData = weatherData;
        }

        public WeatherData WeatherData
        {
            get
            {
                if (weatherData == null)
                {
                    return null;
                }

                WeatherData result = new WeatherData(weatherData);

                CustomParameters customParameters = ParametersControl_Main.CustomParameters;

                SAM.Core.UI.Modify.SetValues(result, customParameters);

                return result;
            }

            set
            {
                weatherData = value;
                LoadWeatherData(weatherData);
            }
        }

        private void LoadWeatherData(WeatherData weatherData)
        {
            TextBox_Name.Text = weatherData?.Name;
            TextBox_Guid.Text = weatherData?.Guid.ToString();

            LoadParameters();

            TabControl_Main.Items.Clear();
            DataGrid_Main.ItemsSource = null;

            if (weatherData == null)
            {
                foreach (WeatherDataType weatherDataType in Enum.GetValues(typeof(WeatherDataType)))
                {
                    if (weatherDataType == WeatherDataType.Undefined)
                    {
                        continue;
                    }

                    TabControl_Main.Items.Add(new TabItem { Header = Core.Query.Description(weatherDataType) });
                }

                return;
            }

            Dictionary<DateTime, Dictionary<WeatherDataType, double>> dictionary_Values = new Dictionary<DateTime, Dictionary<WeatherDataType, double>>();
            List<WeatherDataType> weatherDataTypes = new List<WeatherDataType>();

            foreach (WeatherDataType weatherDataType in Enum.GetValues(typeof(WeatherDataType)))
            {
                if (weatherDataType == WeatherDataType.Undefined)
                {
                    continue;
                }

                Dictionary<DateTime, double> dictionary = Query.Values(weatherData, weatherDataType);
                if (dictionary == null || dictionary.Count == 0)
                {
                    continue;
                }

                string name = Core.Query.Description(weatherDataType);

                TabControl_Main.Items.Add(new TabItem { Header = name, Content = CreateChart(name, weatherDataType, dictionary) });

                weatherDataTypes.Add(weatherDataType);
                foreach (KeyValuePair<DateTime, double> keyValuePair in dictionary)
                {
                    if (!dictionary_Values.TryGetValue(keyValuePair.Key, out Dictionary<WeatherDataType, double> values))
                    {
                        values = new Dictionary<WeatherDataType, double>();
                        dictionary_Values[keyValuePair.Key] = values;
                    }

                    values[weatherDataType] = keyValuePair.Value;
                }
            }

            LoadDataGrid(weatherDataTypes, dictionary_Values);
        }

        private static PlotView CreateChart(string name, WeatherDataType weatherDataType, Dictionary<DateTime, double> dictionary)
        {
            PlotModel plotModel = new PlotModel { Title = name };
            plotModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "MMM dd" });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left });

            LineSeries lineSeries = new LineSeries { Title = name };

            System.Drawing.Color color = Query.Color(weatherDataType);
            if (color != System.Drawing.Color.Empty)
            {
                lineSeries.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
            }

            foreach (KeyValuePair<DateTime, double> keyValuePair in dictionary.OrderBy(x => x.Key))
            {
                lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(keyValuePair.Key), keyValuePair.Value));
            }

            plotModel.Series.Add(lineSeries);

            return new PlotView { Model = plotModel };
        }

        private void LoadDataGrid(List<WeatherDataType> weatherDataTypes, Dictionary<DateTime, Dictionary<WeatherDataType, double>> dictionary_Values)
        {
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("Date", typeof(string));

            foreach (WeatherDataType weatherDataType in weatherDataTypes)
            {
                dataTable.Columns.Add(Core.Query.Description(weatherDataType), typeof(double));
            }

            foreach (KeyValuePair<DateTime, Dictionary<WeatherDataType, double>> keyValuePair_DateTime in dictionary_Values.OrderBy(x => x.Key))
            {
                DataRow dataRow = dataTable.NewRow();
                dataRow["Date"] = keyValuePair_DateTime.Key.ToString("yyyy-MM-dd HH:mm");

                foreach (KeyValuePair<WeatherDataType, double> keyValuePair_WeatherDataType in keyValuePair_DateTime.Value)
                {
                    dataRow[Core.Query.Description(keyValuePair_WeatherDataType.Key)] = keyValuePair_WeatherDataType.Value;
                }

                dataTable.Rows.Add(dataRow);
            }

            DataGrid_Main.ItemsSource = dataTable.DefaultView;
        }

        private void LoadParameters()
        {
            ParametersControl_Main.CustomParameters = null;

            if (weatherData == null)
            {
                return;
            }

            ParametersControl_Main.CustomParameters = SAM.Core.UI.Create.CustomParameters(weatherData, enums?.ToArray());
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
