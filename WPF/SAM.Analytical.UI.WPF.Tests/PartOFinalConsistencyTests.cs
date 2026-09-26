// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>Part O UX pass 6 - final consistency.</b> Only what that pass found and changed: Iteration 3's verdict
    /// words, the TM59 window opened as a bare report, and the Iteration 2B result window's height.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class PartOFinalConsistencyTests
    {
        /// <summary>
        /// Iteration 3 said "Pass" / "Fail" / "Undefined" where the Hub, the TM59 window and the 2B result say
        /// PASS / FAIL / NOT ASSESSED. One word per status now, and for a pass or a fail it is the TM59 window's.
        /// </summary>
        [Theory]
        [InlineData(TM59ComplianceStatus.Pass, "PASS")]
        [InlineData(TM59ComplianceStatus.Fail, "FAIL")]
        [InlineData(TM59ComplianceStatus.Undefined, "NOT ASSESSED")]
        [InlineData(TM59ComplianceStatus.NotApplicable, "NOT ASSESSED")]
        public void AnIteration3Status_IsWordedAsEveryOtherPartOVerdict(TM59ComplianceStatus tM59ComplianceStatus, string expected)
        {
            Assert.Equal(expected, Query.PartOVerdictText(tM59ComplianceStatus));
        }

        /// <summary>
        /// Iteration 3's "TM59 report" buttons open the shared TM59 window over the report text alone - the
        /// Iteration 3 assessment holds no summary to head it with, and none may be parsed out of the text. It
        /// used to show an empty verdict band and an empty facts box above the report. Nothing is drawn now
        /// until a summary is given; given one, even an unavailable one, the band is back.
        /// </summary>
        [WpfFact]
        public void AReportWithNoSummary_IsNotShownUnderAnEmptyVerdict()
        {
            PartOTM59ResultWindow partOTM59ResultWindow = new()
            {
                Report = "REPORT",
            };

            Assert.Equal(Visibility.Collapsed, partOTM59ResultWindow.border_Verdict.Visibility);
            Assert.Equal(Visibility.Collapsed, partOTM59ResultWindow.border_Facts.Visibility);

            partOTM59ResultWindow.ResultSummary = PartOTM59ResultSummary.Unavailable("gone", null);

            Assert.Equal(Visibility.Visible, partOTM59ResultWindow.border_Verdict.Visibility);
            Assert.Equal("TM59 assessment — UNAVAILABLE", partOTM59ResultWindow.VerdictHeading);

            //No run, so no facts - and no empty box for them.
            Assert.Equal(Visibility.Collapsed, partOTM59ResultWindow.border_Facts.Visibility);

            partOTM59ResultWindow.Close();
        }

        /// <summary>
        /// The 2B result window was the one Part O result with no height ceiling: opening Engineering detail
        /// (a fixed-height tab set) could push Copy All and Close below the taskbar. It now takes the Start
        /// window's cap, and its content scrolls with the buttons outside the scroll.
        /// </summary>
        [WpfFact]
        public void The2BResultWindow_KeepsItsButtonsOnScreen()
        {
            PartOOptimisationResultWindow partOOptimisationResultWindow = new();

            Assert.Equal(SystemParameters.WorkArea.Height * 0.92, partOOptimisationResultWindow.MaxHeight, 3);

            Assert.True(InsideScrollViewer(partOOptimisationResultWindow.expander_Detail));
            Assert.False(InsideScrollViewer(partOOptimisationResultWindow.button_Close));
            Assert.False(InsideScrollViewer(partOOptimisationResultWindow.button_CopyAll));

            partOOptimisationResultWindow.Close();
        }

        private static bool InsideScrollViewer(DependencyObject dependencyObject)
        {
            for (DependencyObject? parent = LogicalTreeHelper.GetParent(dependencyObject); parent is not null; parent = LogicalTreeHelper.GetParent(parent))
            {
                if (parent is ScrollViewer)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
