// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>Shows a SAM_Tas <see cref="AlgorithmType"/> under the name the Design Optimisation window uses for the method.</summary>
    public sealed class TasOptimisationAlgorithmNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is AlgorithmType algorithmType ? algorithmType.TasOptimisationAlgorithmName(true) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
