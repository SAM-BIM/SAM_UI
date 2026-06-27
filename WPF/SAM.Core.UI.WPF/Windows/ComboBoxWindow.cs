// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.ComboBoxForm&lt;T&gt;.
    /// A modal single-selection picker: an optional description above a drop-down,
    /// with OK/Cancel buttons. Implemented in code (no XAML) because WPF does not
    /// support a generic x:Class. Mirrors the original public API
    /// (constructors, SelectedItem, Description, DialogResult via ShowDialog()).
    /// </summary>
    /// <typeparam name="T">Type of the items presented in the drop-down.</typeparam>
    public class ComboBoxWindow<T> : Window
    {
        private sealed class Item
        {
            public T Object { get; set; }
            public string Text { get; set; }
        }

        private readonly TextBlock textBlock_Description;
        private readonly ComboBox comboBox_Main;
        private readonly List<Item> items = new List<Item>();

        public ComboBoxWindow()
        {
            // Build the layout once; populated through the AddRange overloads / constructors.
            Width = 369;
            Height = 124;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Title = "ComboBoxWindow";

            Grid grid = new Grid { Margin = new Thickness(12, 5, 12, 5) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(33) });

            textBlock_Description = new TextBlock
            {
                Text = string.Empty,
                Margin = new Thickness(0, 0, 0, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(textBlock_Description, 0);
            grid.Children.Add(textBlock_Description);

            comboBox_Main = new ComboBox
            {
                DisplayMemberPath = "Text",
                VerticalAlignment = VerticalAlignment.Top,
                Height = 23
            };
            Grid.SetRow(comboBox_Main, 1);
            grid.Children.Add(comboBox_Main);

            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(stackPanel, 2);

            Button button_OK = new Button
            {
                Content = "OK",
                Width = 75,
                Height = 28,
                Margin = new Thickness(5, 0, 5, 0),
                IsDefault = true
            };
            button_OK.Click += Button_OK_Click;
            stackPanel.Children.Add(button_OK);

            Button button_Cancel = new Button
            {
                Content = "Cancel",
                Width = 75,
                Height = 28,
                Margin = new Thickness(5, 0, 0, 0),
                IsCancel = true
            };
            button_Cancel.Click += Button_Cancel_Click;
            stackPanel.Children.Add(button_Cancel);

            grid.Children.Add(stackPanel);

            Content = grid;
        }

        public ComboBoxWindow(string name)
            : this()
        {
            Title = name;

            if (typeof(T).IsEnum)
            {
                List<Enum> @enums = new List<Enum>();
                foreach (Enum @enum in Enum.GetValues(typeof(T)))
                {
                    @enums.Add(@enum);
                }

                AddRange(@enums.Cast<T>(), (T x) => Core.Query.Description((Enum)(object)x));
            }
        }

        public ComboBoxWindow(string name, IEnumerable<T> items)
            : this()
        {
            Title = name;
            AddRange(items);
        }

        public ComboBoxWindow(string name, IEnumerable<T> items, Func<T, string> text)
            : this()
        {
            Title = name;
            AddRange(items, text);
        }

        public ComboBoxWindow(string name, IEnumerable<T> items, Func<T, string> text, T selectedItem)
            : this()
        {
            Title = name;
            AddRange(items, text, selectedItem);
        }

        private void AddRange(IEnumerable<T> items, Func<T, string> text = null, T selectedItem = default)
        {
            if (items == null)
            {
                return;
            }

            foreach (T item in items)
            {
                string value_text = text == null ? item?.ToString() : text.Invoke(item);
                if (value_text == null)
                {
                    continue;
                }

                this.items.Add(new Item { Object = item, Text = value_text });
            }

            comboBox_Main.ItemsSource = null;
            comboBox_Main.ItemsSource = this.items;

            SetSelectedItem(selectedItem);
        }

        private void SetSelectedItem(T item)
        {
            Item selected = items.FirstOrDefault(x => EqualityComparer<T>.Default.Equals(x.Object, item));
            comboBox_Main.SelectedItem = selected;
        }

        public T SelectedItem
        {
            get
            {
                return comboBox_Main.SelectedItem is Item item ? item.Object : default;
            }

            set
            {
                SetSelectedItem(value);
            }
        }

        public string Description
        {
            get
            {
                return textBlock_Description.Text;
            }

            set
            {
                textBlock_Description.Text = value;
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
