// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Weather;
using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void EditWeatherData(this UIAnalyticalModel uIAnalyticalModel, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            WeatherData weatherData = null;

            analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out weatherData);
            if(weatherData == null)
            {
                ImportWeatherData(uIAnalyticalModel, owner);
            }

            analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out weatherData);
            if (weatherData == null)
            {
                return;
            }

            SAM.Weather.UI.WPF.WeatherDataWindow weatherDataWindow = new SAM.Weather.UI.WPF.WeatherDataWindow(weatherData, Core.Query.Enums(typeof(WeatherData)));

            // Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(weatherDataWindow).Owner = owner.Handle;
            }

            if (weatherDataWindow.ShowDialog() != true)
            {
                return;
            }

            weatherData = weatherDataWindow.WeatherData;
            if(weatherData == null)
            {
                return;
            }

            analyticalModel = new AnalyticalModel(analyticalModel);
            analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }
    }
}
