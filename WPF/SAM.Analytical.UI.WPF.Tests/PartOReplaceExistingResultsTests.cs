// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.UI;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Prepare &amp; Run meeting another run's results in the case folder: it is ASKED (replace / cancel) instead of refused
    /// after the review, nothing is deleted until the person confirms, and a confirmed replacement removes this case's
    /// generated files and nothing else. The decision and the file handling are tested without a window; the window's
    /// words have their own tests.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class PartOReplaceExistingResultsTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "SAM_PartOReplace_" + Guid.NewGuid().ToString("N"));

        public PartOReplaceExistingResultsTests()
        {
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
        }

        /// <summary>An Iteration 2 case folder an earlier run left behind, claimed by <paramref name="owner"/>.</summary>
        private PartOOutputPaths EarlierRun(Guid owner, string caseKey = "case")
        {
            PartOOutputPaths paths = PartOOutputPaths.Create(directory, PartOOutputCase.Iteration2)!;
            Assert.Null(paths.TryClaimRun(owner, caseKey: caseKey));

            File.WriteAllText(Path.Combine(paths.Directory_Tas, "model.tsd"), "results");
            File.WriteAllText(Path.Combine(paths.Directory_Tas, "model.sam"), "run model");
            File.WriteAllText(Path.Combine(paths.Directory_Reports, "model-TM59.txt"), "report");
            File.WriteAllText(Path.Combine(paths.Directory_Diagnostics, "model.timing.csv"), "timing");

            return paths;
        }

        private static string[] Files(string directory) => Directory.Exists(directory) ? [.. Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order()] : [];

        // ---- No existing results ------------------------------------------------------------------------------------

        [Fact]
        public void NothingThere_AsksNothing_AndPromptsNever()
        {
            PartOOutputPaths paths = PartOOutputPaths.Create(directory, PartOOutputCase.Iteration2)!;
            bool asked = false;

            Assert.Equal(PartOExistingResultsDecision.None, Modify.ResolveExistingPartOResults(paths, Guid.NewGuid(), "case", x => { asked = true; return true; }));
            Assert.False(asked);
            Assert.False(Directory.Exists(paths.Directory_Case));

            //A case marker alone is not evidence of a run.
            Assert.Null(paths.TryCreateDirectories());
            Assert.Equal(PartOExistingResultsDecision.None, Modify.ResolveExistingPartOResults(paths, Guid.NewGuid(), "case", x => { asked = true; return true; }));
            Assert.False(asked);
        }

        [Fact]
        public void ARetryByTheSameRunAndCase_IsNeverAskedAbout()
        {
            Guid run = Guid.NewGuid();
            PartOOutputPaths paths = EarlierRun(run);
            bool asked = false;

            Assert.Equal(PartOExistingResultsDecision.None, Modify.ResolveExistingPartOResults(paths, run, "case", x => { asked = true; return true; }));
            Assert.False(asked);
        }

        [Fact]
        public void ANoOutputFolder_IsNothingToAskAbout()
        {
            Assert.Equal(PartOExistingResultsDecision.None, Modify.ResolveExistingPartOResults(null, Guid.NewGuid(), "case", x => throw new InvalidOperationException()));
        }

        // ---- Another run's results: confirm, cancel ------------------------------------------------------------------

        [Fact]
        public void AnotherRunsResults_AreAskedAbout_WithTheFolderAndWhatIsInIt()
        {
            PartOOutputPaths paths = EarlierRun(Guid.NewGuid());
            File.WriteAllText(Path.Combine(paths.Directory_Case, "my-notes.txt"), "mine");
            PartOOutputOccupancy? seen = null;

            PartOExistingResultsDecision decision = Modify.ResolveExistingPartOResults(paths, Guid.NewGuid(), "case", x => { seen = x; return true; });

            Assert.Equal(PartOExistingResultsDecision.Replace, decision);
            Assert.NotNull(seen);
            Assert.Equal(paths.Directory_Case, seen!.Directory_Case);
            Assert.Equal(4, seen.Count_GeneratedFiles);
            Assert.Equal(1, seen.Count_OtherFiles);
            Assert.NotNull(seen.LastWriteUtc);
            Assert.True(seen.NeedsReplacement);
        }

        [Fact]
        public void AChangedTasCase_IsAskedAbout_EvenForTheSameRun()
        {
            Guid run = Guid.NewGuid();
            PartOOutputPaths paths = EarlierRun(run, "weather A");

            Assert.Equal(PartOExistingResultsDecision.Cancel, Modify.ResolveExistingPartOResults(paths, run, "weather B", x => false));
        }

        [Fact]
        public void Cancel_ChangesNothing_NotAFileNotTheMarker()
        {
            Guid earlier = Guid.NewGuid();
            PartOOutputPaths paths = EarlierRun(earlier);
            string[] before = Files(directory);
            string marker = File.ReadAllText(paths.Path_Marker);

            Assert.Equal(PartOExistingResultsDecision.Cancel, Modify.ResolveExistingPartOResults(paths, Guid.NewGuid(), "case", x => false));

            Assert.Equal(before, Files(directory));
            Assert.Equal(marker, File.ReadAllText(paths.Path_Marker));

            //Still the earlier run's: a later run is refused exactly as before.
            Assert.Contains("already contains run evidence", paths.TryClaimRun(Guid.NewGuid(), caseKey: "case"));
        }

        [Fact]
        public void Confirming_DeletesNothingByItself_TheRunDoesItWhenTasIsAboutToStart()
        {
            PartOOutputPaths paths = EarlierRun(Guid.NewGuid());
            string[] before = Files(directory);

            Assert.Equal(PartOExistingResultsDecision.Replace, Modify.ResolveExistingPartOResults(paths, Guid.NewGuid(), "case", x => true));

            Assert.Equal(before, Files(directory));
        }

        // ---- What a confirmed replacement removes ----------------------------------------------------------------------

        [Fact]
        public void Replace_RemovesTheCasesGeneratedFilesAndMarker_AndNothingElse()
        {
            PartOOutputPaths paths = EarlierRun(Guid.NewGuid());

            //Everything that must survive: another file in this case folder, another case, the design model and a neighbour of the root.
            string path_Other = Path.Combine(paths.Directory_Case, "my-notes.txt");
            File.WriteAllText(path_Other, "mine");
            PartOOutputPaths paths_Iteration3 = PartOOutputPaths.Create(directory, PartOOutputCase.Iteration3)!;
            Assert.Null(paths_Iteration3.TryCreateDirectories());
            string path_Iteration3 = Path.Combine(paths_Iteration3.Directory_Reports, "record.json");
            File.WriteAllText(path_Iteration3, "iteration 3 record");
            string path_Design = Path.Combine(directory, "design.sam");
            File.WriteAllText(path_Design, "design");

            Assert.Null(paths.TryReplaceGenerated(out int removed));

            Assert.Equal(4, removed);
            Assert.False(Directory.Exists(paths.Directory_Tas));
            Assert.False(Directory.Exists(paths.Directory_Reports));
            Assert.False(Directory.Exists(paths.Directory_Diagnostics));
            Assert.False(File.Exists(paths.Path_Marker));
            Assert.True(File.Exists(path_Other));
            Assert.True(File.Exists(path_Iteration3));
            Assert.True(File.Exists(path_Design));
            Assert.Equal("design", File.ReadAllText(path_Design));
        }

        [Fact]
        public void AfterReplace_TheNewRunClaimsTheSameFolder_NoNewFolderIsNeeded()
        {
            PartOOutputPaths paths = EarlierRun(Guid.NewGuid());
            File.WriteAllText(Path.Combine(paths.Directory_Case, "my-notes.txt"), "mine");
            Guid later = Guid.NewGuid();

            Assert.NotNull(paths.TryClaimRun(later, caseKey: "case"));

            Assert.Null(paths.TryReplaceGenerated(out int _));
            //A file that is not SAM's is still "evidence", so the claim is the confirmed one.
            Assert.Null(paths.TryClaimRun(later, replaceExisting: true, caseKey: "case"));

            Assert.True(Directory.Exists(paths.Directory_Tas));
            Assert.Empty(Files(paths.Directory_Tas));
            Assert.Equal(["Iteration2"], Directory.GetDirectories(directory).Select(Path.GetFileName).ToArray());

            //The new run is now the owner: its own retries are not asked about.
            Assert.Equal(PartOExistingResultsDecision.None, Modify.ResolveExistingPartOResults(paths, later, "case", x => throw new InvalidOperationException()));
        }

        [Fact]
        public void Replace_ReportsAFileInUse_AndSaysNothingWasRun()
        {
            PartOOutputPaths paths = EarlierRun(Guid.NewGuid());

            using FileStream locked = new(Path.Combine(paths.Directory_Tas, "model.tsd"), FileMode.Open, FileAccess.Read, FileShare.None);

            string refusal = paths.TryReplaceGenerated(out int _)!;

            Assert.NotNull(refusal);
            Assert.Contains("could not be removed", refusal);
            Assert.Contains("nothing was run", refusal);
        }

        // ---- The scenario names the folder before anything is prepared ---------------------------------------------------

        [Theory]
        [InlineData(PartOWorkflowScenario.Text_Iteration1a, PartOOutputCase.Iteration1a)]
        [InlineData(PartOWorkflowScenario.Text_Iteration1b, PartOOutputCase.Iteration1b)]
        [InlineData(PartOWorkflowScenario.Text_Iteration2, PartOOutputCase.Iteration2)]
        public void TheScenarioNamesItsCaseFolder_BeforeAnythingIsPrepared(string text, PartOOutputCase expected)
        {
            PartOWorkflowScenario scenario = PartOWorkflowScenario.Scenarios.Single(x => x.Text == text);

            Assert.Equal(expected, PartOOutputPaths.CaseOfScenario(scenario));
            Assert.Null(PartOOutputPaths.CaseOfScenario(null));
        }

        // ---- The window ------------------------------------------------------------------------------------------------------

        [WpfFact]
        public void TheWindow_NamesTheCaseAndFolder_OffersReplaceAndCancel_AndSaysWhatIsAndIsNotTouched()
        {
            PartOOutputPaths paths = EarlierRun(Guid.NewGuid());
            File.WriteAllText(Path.Combine(paths.Directory_Case, "my-notes.txt"), "mine");
            PartOOutputOccupancy occupancy = paths.Occupancy(Guid.NewGuid(), "case");

            PartOReplaceResultsWindow window = new("Iteration 2", occupancy, true);

            Assert.Contains("Iteration 2", window.Heading);
            Assert.Equal(paths.Directory_Case, window.Folder);
            Assert.Equal("Replace existing results", ((System.Windows.Controls.Button)window.FindName("button_Replace")).Content);
            Assert.Equal("Cancel", ((System.Windows.Controls.Button)window.FindName("button_Cancel")).Content);
            Assert.True(((System.Windows.Controls.Button)window.FindName("button_Cancel")).IsCancel);

            string contents = PartOReplaceResultsWindow.Contents(occupancy);
            Assert.Contains("4 generated files", contents);

            string explanation = PartOReplaceResultsWindow.Explanation(true, occupancy);
            Assert.Contains("removes this case's generated run files", explanation);
            Assert.Contains("design model is not changed", explanation);
            Assert.Contains("1 other file", explanation);
            Assert.Contains("Nothing is removed until you press Replace", explanation);

            //Iteration 3 shares the window but states its own, gentler meaning.
            string explanation_Iteration3 = PartOReplaceResultsWindow.Explanation(false, occupancy);
            Assert.DoesNotContain("removes", explanation_Iteration3);
            Assert.Contains("other methods' records - is kept", explanation_Iteration3);
        }

        [Fact]
        public void TheHubLineAfterCancelling_SaysNothingWasRunOrChanged()
        {
            PartOWorkflowOutcome partOWorkflowOutcome = Modify.ReplaceDeclinedOutcome(PartOWorkflowScenario.Text_Iteration2);

            Assert.Contains("Iteration 2 not run", partOWorkflowOutcome.Headline);
            Assert.Contains("existing results were kept", partOWorkflowOutcome.Headline);
            Assert.Contains("nothing was changed", partOWorkflowOutcome.Detail);
        }
    }
}
