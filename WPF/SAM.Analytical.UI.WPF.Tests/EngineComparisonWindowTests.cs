// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The Tas/OpenStudio comparison window as a person meets it: the banner's counts and the two grids at the wide and the narrow
    /// reviewed widths. The documents are SAM_Validation's FABRICATED regression documents, not engine results; the grid shows what
    /// SAM_Validation computed, so the only product decisions here are labels and layout.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class EngineComparisonWindowTests
    {
        private static readonly double[] ModelWidths = { 160, 85, 80, 90, 100, 135, 55, 140, 275 };

        private static readonly double[] SpaceWidths = { 100, 80, 125, 85, 60, 75, 90, 140, 45, 140, 160 };

        private static string Resource(string name, [CallerFilePath] string sourceFilePath = null)
        {
            return Path.Combine(Path.GetDirectoryName(sourceFilePath), "Resources", "EngineComparison", name);
        }

        private static string Tas => Resource("scenario-benchmark-TAS.json");

        private static string OpenStudio => Resource("scenario-benchmark-OpenStudio.json");

        [WpfFact]
        public void Banner_CountsInformationalRowsSeparatelyFromUnitMismatch()
        {
            //Informational rows (peak hours, whole-model peaks) are excluded from the numerical status. They are not "not equivalent"
            //in the incompatible sense, so the banner must not describe them that way.
            EngineComparisonWindow window = Window(1180);
            try
            {
                Assert.True(window.Compare(Tas, OpenStudio, null));
                Flush(window);

                EngineComparison comparison = Query.LoadEngineComparison(Tas, OpenStudio);
                string banner = Control<TextBlock>(window, "TextBlock_Banner").Text;

                Assert.Contains(comparison.ComparisonResult.FailCount + " fail", banner);
                Assert.Contains(comparison.ContributingFailCount + " contributing to numerical status", banner);
                Assert.Contains(comparison.InformationalFailCount + " informational and excluded", banner);
                Assert.Contains(comparison.UnitMismatchCount + " unit mismatch", banner);
                Assert.DoesNotContain("not equivalent", banner);

                //The counts themselves are SAM_Validation's: contributing + informational is every Fail band.
                Assert.Equal(comparison.ComparisonResult.FailCount, comparison.ContributingFailCount + comparison.InformationalFailCount);
                Assert.Equal(comparison.ComparisonResult.ContributingFailCount, comparison.ContributingFailCount);
                Assert.Equal(comparison.ComparisonResult.InformationalFailCount, comparison.InformationalFailCount);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void Grids_KeepReadableColumns_AndScrollHorizontallyWhenNarrow()
        {
            EngineComparisonWindow wide = Window(1180);
            try
            {
                Assert.True(wide.Compare(Tas, OpenStudio, null));
                Flush(wide);

                AssertGrid(wide, "DataGrid_Model", ModelWidths, expectHorizontalScroll: false);
                ((TabControl)wide.FindName("TabControl_Main")).SelectedIndex = 1;
                Flush(wide);
                AssertGrid(wide, "DataGrid_Spaces", SpaceWidths, expectHorizontalScroll: false);
            }
            finally
            {
                wide.Close();
            }

            EngineComparisonWindow narrow = Window(820);
            try
            {
                Assert.True(narrow.Compare(Tas, OpenStudio, null));
                Flush(narrow);

                //At 820 px every column keeps its readable width; the grid scrolls instead of compressing the columns.
                AssertGrid(narrow, "DataGrid_Model", ModelWidths, expectHorizontalScroll: true);
                ((TabControl)narrow.FindName("TabControl_Main")).SelectedIndex = 1;
                Flush(narrow);
                AssertGrid(narrow, "DataGrid_Spaces", SpaceWidths, expectHorizontalScroll: true);
            }
            finally
            {
                narrow.Close();
            }
        }

        private static void AssertGrid(EngineComparisonWindow window, string name, double[] widths, bool expectHorizontalScroll)
        {
            DataGrid dataGrid = Control<DataGrid>(window, name);

            Assert.Equal(widths.Length, dataGrid.Columns.Count);
            for (int index = 0; index < widths.Length; index++)
            {
                //Fixed widths are preserved: a compressed column was the defect this guards against.
                Assert.True(
                    dataGrid.Columns[index].ActualWidth >= widths[index] - 1,
                    string.Format("{0} column {1} ({2}) compressed to {3:0.#} px", name, index, dataGrid.Columns[index].Header, dataGrid.Columns[index].ActualWidth));
            }

            ScrollViewer scrollViewer = VisualChildren<ScrollViewer>(dataGrid).First();
            if (expectHorizontalScroll)
            {
                Assert.True(scrollViewer.ScrollableWidth > 0, name + " should scroll horizontally at a narrow width");
                Assert.Equal(Visibility.Visible, scrollViewer.ComputedHorizontalScrollBarVisibility);
            }
            else
            {
                Assert.True(scrollViewer.ScrollableWidth < 1, name + " should fit at the reviewed wide width");
            }

            //The Note column stays readable at both widths (its full text is also on the cell tooltip).
            DataGridColumn note = dataGrid.Columns.Last();
            Assert.Equal("Note", note.Header);
            Assert.True(note.ActualWidth >= widths.Last() - 1);
        }

        private static EngineComparisonWindow Window(double width)
        {
            EngineComparisonWindow window = new EngineComparisonWindow(null)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0,
                Top = 0,
                Width = width,
                ShowActivated = false,
            };

            window.Show();
            Flush(window);

            return window;
        }

        private static T Control<T>(EngineComparisonWindow window, string name) where T : FrameworkElement
        {
            return (T)window.FindName(name);
        }

        private static void Flush(System.Windows.Window window)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        private static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
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
