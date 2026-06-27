// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.AddressAndLocationForm.
    /// Edits an <see cref="Address"/> and a <see cref="Location"/> via simple text fields
    /// and a country drop-down. Mirrors the original public API (Address, Location).
    /// </summary>
    public partial class AddressAndLocationWindow : Window
    {
        public AddressAndLocationWindow()
        {
            InitializeComponent();

            LoadCountries();
        }

        public AddressAndLocationWindow(Address address, Location location)
        {
            InitializeComponent();

            LoadCountries();

            Address = address;
            Location = location;
        }

        public Address Address
        {
            get
            {
                return new Address(TextBox_Street.Text, TextBox_City.Text, TextBox_PostalCode.Text, GetCountryCode());
            }

            set
            {
                TextBox_Street.Text = null;
                TextBox_City.Text = null;
                TextBox_PostalCode.Text = null;
                ComboBox_Country.Text = string.Empty;

                if (value != null)
                {
                    TextBox_Street.Text = value.Street;
                    TextBox_City.Text = value.City;
                    TextBox_PostalCode.Text = value.PostalCode;
                    ComboBox_Country.Text = value.CountryCode == CountryCode.Undefined ? string.Empty : Core.Query.Description(value.CountryCode);
                }
            }
        }

        public Location Location
        {
            get
            {
                Core.Query.TryConvert(TextBox_Longitude.Text, out double longitude);
                Core.Query.TryConvert(TextBox_Latitude.Text, out double latitude);
                Core.Query.TryConvert(TextBox_Elevation.Text, out double elevation);

                return new Location(null, longitude, latitude, elevation);
            }

            set
            {
                TextBox_Longitude.Text = null;
                TextBox_Latitude.Text = null;
                TextBox_Elevation.Text = null;

                if (value != null)
                {
                    TextBox_Longitude.Text = value.Longitude.ToString();
                    TextBox_Latitude.Text = value.Latitude.ToString();
                    TextBox_Elevation.Text = value.Elevation.ToString();
                }
            }
        }

        private CountryCode GetCountryCode()
        {
            if (string.IsNullOrWhiteSpace(ComboBox_Country.Text))
            {
                return CountryCode.Undefined;
            }

            return Core.Query.Enum<CountryCode>(ComboBox_Country.Text);
        }

        private void LoadCountries()
        {
            ComboBox_Country.Items.Clear();

            ComboBox_Country.Items.Add(string.Empty);
            foreach (CountryCode countryCode in Enum.GetValues(typeof(CountryCode)))
            {
                if (countryCode == CountryCode.Undefined)
                {
                    continue;
                }

                ComboBox_Country.Items.Add(Core.Query.Description(countryCode));
            }
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
