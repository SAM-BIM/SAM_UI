// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms PropertyGrid used by the SAM_Windows object editors
    /// (MaterialControl, PanelForm, ApertureForm, SpaceControl, ...). Presents a SAM object's
    /// <see cref="CustomParameters"/> in a category-grouped DataGrid with type-aware value
    /// editing. Edits mutate the supplied CustomParameter instances in place, so the same
    /// <see cref="CustomParameters"/> can be read back and applied via Modify.SetValues.
    /// </summary>
    public partial class ParametersControl : UserControl
    {
        private CustomParameters customParameters;
        private bool readOnly;

        public ParametersControl()
        {
            InitializeComponent();
        }

        public CustomParameters CustomParameters
        {
            get
            {
                return customParameters;
            }

            set
            {
                customParameters = value;
                Load();
            }
        }

        /// <summary>When true every value cell is read-only (mirrors the WinForms control's Enabled=false).</summary>
        public bool ReadOnly
        {
            get
            {
                return readOnly;
            }

            set
            {
                readOnly = value;
                Load();
            }
        }

        private void Load()
        {
            DataGrid_Main.ItemsSource = null;

            if (customParameters == null)
            {
                return;
            }

            List<Row> rows = customParameters.Cast<CustomParameter>().Where(x => x != null).Select(x => new Row(x, readOnly)).ToList();

            ListCollectionView listCollectionView = new ListCollectionView(rows);
            listCollectionView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Row.Category)));

            DataGrid_Main.ItemsSource = listCollectionView;
        }

        private sealed class Row : INotifyPropertyChanged
        {
            private readonly CustomParameter customParameter;

            public Row(CustomParameter customParameter, bool readOnly)
            {
                this.customParameter = customParameter;
                EffectiveReadOnly = readOnly || customParameter.IsReadOnly;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public string Name => customParameter.Name;

            public string Description => customParameter.Description;

            public string Category => string.IsNullOrWhiteSpace(customParameter.Category) ? "General" : customParameter.Category;

            public bool EffectiveReadOnly { get; }

            public string ValueText
            {
                get
                {
                    return customParameter.Value?.ToString();
                }

                set
                {
                    customParameter.SetValue(Convert(value));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueText)));
                }
            }

            private object Convert(string text)
            {
                Type type = customParameter.ParameterTypes?.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(text))
                {
                    // Empty -> null so Modify.SetValue removes the parameter (matches the
                    // WinForms behaviour where a blank double became NaN -> null -> RemoveValue).
                    return type == typeof(string) ? string.Empty : null;
                }

                if (type == typeof(double))
                {
                    return Core.Query.TryConvert(text, out double @double) ? @double : (object)null;
                }

                if (type == typeof(int))
                {
                    return int.TryParse(text, out int @int) ? @int : (object)null;
                }

                if (type == typeof(bool))
                {
                    return bool.TryParse(text, out bool @bool) ? @bool : (object)null;
                }

                return text;
            }
        }
    }
}
