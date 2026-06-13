// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// In-app reference for the keyboard shortcuts and viewport gestures (Help -> Shortcuts). Built in
    /// code rather than XAML so it stays a single self-contained file; the content mirrors
    /// documentation/keyboard-shortcuts.md. Keep the two in sync when bindings change.
    /// </summary>
    public class KeyboardShortcutsWindow : System.Windows.Window
    {
        public KeyboardShortcutsWindow()
        {
            Title = "Keyboard Shortcuts";
            Width = 560;
            Height = 660;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = SystemColors.WindowBrush;

            StackPanel stackPanel = new StackPanel { Margin = new Thickness(16) };

            stackPanel.Children.Add(Section("General", new[,]
            {
                { "Ctrl + Z", "Undo" },
                { "Ctrl + Y  /  Ctrl + Shift + Z", "Redo" },
                { "Ctrl + S", "Save as" },
                { "G", "Select by Guid" },
                { "F", "Select by filter" },
                { "V", "Edit view settings" },
                { "P", "Show properties of the selection" },
                { "I", "Isolate the selection" },
                { "H", "Hide the selection" },
                { "U  (or UU)", "Unhide all" },
                { "R", "Reverse (panels)" },
                { "Delete", "Delete the selection" },
                { "F12", "Show the selection as JSON" },
                { "Z E", "Zoom extents" },
                { "Z S", "Zoom selected" },
                { "Esc", "Clear the selection" },
            }));

            stackPanel.Children.Add(Section("3D view - camera", new[,]
            {
                { "Right drag", "Orbit (around the selection, else the cursor point)" },
                { "Middle drag", "Pan" },
                { "Shift + Left drag", "Pan" },
                { "Mouse wheel", "Zoom (toward the cursor)" },
                { "Ctrl + Shift + O", "Toggle perspective / orthographic" },
                { "View cube", "Click a face/edge/corner to snap; drag to orbit" },
                { "Right click", "Context menu (Zoom Extents/Selected, Orthographic)" },
            }));

            stackPanel.Children.Add(Section("Selection", new[,]
            {
                { "Left click", "Select the object under the cursor" },
                { "Ctrl + Left click", "Toggle the object in/out of the selection" },
                { "Left drag", "Rectangle select (L->R inside, R->L crossing)" },
                { "Double click", "Open / drill into the object" },
            }));

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };
        }

        // One titled block: a bold header above a two-column (gesture | action) grid.
        private static FrameworkElement Section(string title, string[,] rows)
        {
            StackPanel stackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 6)
            });

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int count = rows.GetLength(0);
            for (int i = 0; i < count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                TextBlock textBlock_Key = new TextBlock
                {
                    Text = rows[i, 0],
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    Margin = new Thickness(0, 2, 8, 2)
                };
                Grid.SetRow(textBlock_Key, i);
                Grid.SetColumn(textBlock_Key, 0);
                grid.Children.Add(textBlock_Key);

                TextBlock textBlock_Action = new TextBlock
                {
                    Text = rows[i, 1],
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                Grid.SetRow(textBlock_Action, i);
                Grid.SetColumn(textBlock_Action, 1);
                grid.Children.Add(textBlock_Action);
            }

            stackPanel.Children.Add(grid);
            return stackPanel;
        }
    }
}
