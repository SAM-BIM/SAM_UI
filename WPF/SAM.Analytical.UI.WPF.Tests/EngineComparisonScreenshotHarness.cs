// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>Opt-in.</b> Renders the Tas/OpenStudio comparison window at the two reviewed widths (1180 px and 820 px), on both the Model and
    /// the Spaces tab, including a narrow view scrolled to the right. Nothing is run and nothing is written beside the screenshots.
    /// <para>
    /// <c>SAM_ENGINE_COMPARISON_RUN</c> is a folder holding <c>benchmark-TAS.json</c> and <c>benchmark-OpenStudio.json</c> (a saved
    /// comparison run folder works); <c>SAM_ENGINE_COMPARISON_SCREENSHOTS</c> is where the images go. Without them this passes having done
    /// nothing.
    /// </para>
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class EngineComparisonScreenshotHarness
    {
        [WpfFact]
        public void Render_both_grids_at_1180_and_820()
        {
            string directory_Run = Environment.GetEnvironmentVariable("SAM_ENGINE_COMPARISON_RUN");
            string directory_Screenshots = Environment.GetEnvironmentVariable("SAM_ENGINE_COMPARISON_SCREENSHOTS");

            if (string.IsNullOrWhiteSpace(directory_Run) || string.IsNullOrWhiteSpace(directory_Screenshots) || !Directory.Exists(directory_Run))
            {
                return;
            }

            string tas = Path.Combine(directory_Run, Query.TasBenchmarkFileName);
            string openStudio = Path.Combine(directory_Run, Query.OpenStudioBenchmarkFileName);
            if (!File.Exists(tas) || !File.Exists(openStudio))
            {
                return;
            }

            Directory.CreateDirectory(directory_Screenshots);

            EngineComparisonWindow window = new EngineComparisonWindow(null)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = 0,
                Top = 0,
                ShowActivated = false,
            };

            try
            {
                Assert.True(window.Compare(tas, openStudio, null));
                window.Show();
                Flush(window);

                TabControl tabControl = (TabControl)window.FindName("TabControl_Main");
                DataGrid model = (DataGrid)window.FindName("DataGrid_Model");
                DataGrid spaces = (DataGrid)window.FindName("DataGrid_Spaces");

                tabControl.SelectedIndex = 0;
                Flush(window);
                PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory_Screenshots, "compare-model-1180.png"), 1180, 780);

                //At 820 px the columns keep their readable widths and the grid scrolls horizontally.
                tabControl.SelectedIndex = 0;
                Flush(window);
                PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory_Screenshots, "compare-model-820.png"), 820, 780);
                ScrollRight(model);
                PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory_Screenshots, "compare-model-820-scrolled.png"), 820, 780);

                tabControl.SelectedIndex = 1;
                Flush(window);
                PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory_Screenshots, "compare-spaces-1180.png"), 1180, 780);

                tabControl.SelectedIndex = 1;
                Flush(window);
                PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory_Screenshots, "compare-spaces-820.png"), 820, 780);
                ScrollRight(spaces);
                PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory_Screenshots, "compare-spaces-820-scrolled.png"), 820, 780);
            }
            finally
            {
                window.Close();
            }
        }

        private static void ScrollRight(DataGrid dataGrid)
        {
            ScrollViewer scrollViewer = VisualChildren<ScrollViewer>(dataGrid).First();
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.ScrollableWidth);
            scrollViewer.UpdateLayout();
        }

        private static void Flush(System.Windows.Window window)
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static System.Collections.Generic.IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in VisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
