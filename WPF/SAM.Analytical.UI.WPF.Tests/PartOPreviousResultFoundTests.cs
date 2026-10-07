// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.UI;
using SAM.Core;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>"Previous result found": open design -> previous result found -> Open previous result / Run again.</b>
    /// <para>
    /// Where the case folder a Part O run is about to write into already holds a saved result of <b>this</b> design - proven by the
    /// stable semantic <c>PartODesignKey</c> the result recorded, never by a name, a path or a UI-made fingerprint - the person is
    /// offered the result instead of being sent straight to "replace existing results". Pinned here with a real result model written
    /// through the native <c>.sam</c> writer beside a real results file, so the compatibility test is the production one:
    /// matching key offered; different key, legacy result with no key, a result of a result, an optimisation round and a stale
    /// results file not offered; Open previous opens that <c>.sam</c> and stops; Run again continues into the existing
    /// replacement question; Cancel changes nothing.
    /// </para>
    /// </summary>
    public class PartOPreviousResultFoundTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "SAM_PartOPreviousResultFoundTests_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
            }
        }

        private static AnalyticalModel Design(string spaceName = "Bedroom")
        {
            AdjacencyCluster adjacencyCluster = new();
            adjacencyCluster.AddObject(new Space(spaceName, new Point3D(0, 0, 1.5)));

            return new AnalyticalModel("design", null, null, null, adjacencyCluster, null, null);
        }

        private PartOOutputPaths Paths()
        {
            PartOOutputPaths partOOutputPaths = PartOOutputPaths.Create(root, PartOOutputCase.Iteration2);
            partOOutputPaths.CreateDirectories();

            return partOOutputPaths;
        }

        /// <summary>
        /// Writes a saved result exactly as a run leaves it: the results file, then the run model beside it carrying the
        /// provenance, the scenarios and the baseline reference of the design it was derived from.
        /// </summary>
        private static string SaveResult(PartOOutputPaths partOOutputPaths, AnalyticalModel design, bool recordKey = true, string name = "000000_SAM_AnalyticalModel", bool derivedFromResult = false)
        {
            string path_TSD = Path.Combine(partOOutputPaths.Directory_Tas, name + ".tsd");
            File.WriteAllText(path_TSD, "results - " + Guid.NewGuid());

            string path_Model = Query.Path_PartORunModel(path_TSD);

            AnalyticalModel result = new("run", null, null, null, new AdjacencyCluster(), null, null);

            PartOBaselineReference partOBaselineReference = Analytical.Create.PartOBaselineReferenceFromDesign(PartODerivedCase.Iteration2, design, null, partOOutputPaths.Directory_Case);
            if (!recordKey)
            {
                //A result saved before the key existed.
                partOBaselineReference.Design.DesignKey = null;
            }

            if (derivedFromResult)
            {
                partOBaselineReference.Source = new PartOModelReference(PartOModelReferenceKind.Result, Guid.NewGuid(), "source", "fingerprint", null);
            }

            result.SetValue(Analytical.AnalyticalModelParameter.PartOBaselineReference, partOBaselineReference);
            result.SetValue(Analytical.AnalyticalModelParameter.OverheatingScenarios, new SAMCollection<OverheatingScenario>([new OverheatingScenario(PartOAssessmentScope.Dwelling, Guid.NewGuid(), PartOIteration.BasePassive)]));
            result.SetValue(Analytical.AnalyticalModelParameter.SimulationResultProvenance, new SimulationResultProvenance(result, path_TSD));

            Assert.True(Core.Convert.ToFile(result, path_Model, SAMFileType.SAM));

            return path_Model;
        }

        private static string Snapshot(string directory)
        {
            return string.Join("\n", Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => string.Format("{0}|{1}|{2}", x, new FileInfo(x).Length, File.GetLastWriteTimeUtc(x).Ticks)));
        }

        // 1 ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void AMatchingDesignKey_OffersThePreviousResult()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            string path_Model = SaveResult(partOOutputPaths, design);

            PartOPreviousResult? seen = null;
            PartOPreviousResultDecision decision = Modify.OfferPreviousPartOResult(partOOutputPaths, design, x => { seen = x; return PartOPreviousResultChoice.Cancel; }, out string? _);

            Assert.NotNull(seen);
            Assert.Equal(path_Model, seen!.Path_Model, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(PartOPreviousResultDecision.Cancel, decision);
        }

        [Fact]
        public void TheKey_IsToleratedAcrossAReopenedDesign_ThatCarriesNewSessionGuids()
        {
            //The reason the key exists: the same design written and read back again is the same design, even though the
            //Part O selection objects mint fresh guids each time they are rebuilt.
            AnalyticalModel design = Design();
            design.SetValue(Analytical.AnalyticalModelParameter.PartOEquipmentSelection, new PartOEquipmentSelection());
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design);

            AnalyticalModel design_Later = new(design);
            design_Later.SetValue(Analytical.AnalyticalModelParameter.PartOEquipmentSelection, new PartOEquipmentSelection());

            Assert.NotNull(Modify.FindPreviousPartOResult(partOOutputPaths, design_Later));
        }

        // 2 ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void ADifferentDesignKey_IsNotOffered()
        {
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, Design("Bedroom"));

            bool asked = false;
            PartOPreviousResultDecision decision = Modify.OfferPreviousPartOResult(partOOutputPaths, Design("Living room"), x => { asked = true; return PartOPreviousResultChoice.OpenPrevious; }, out string? path_Open);

            Assert.False(asked);
            Assert.Equal(PartOPreviousResultDecision.None, decision);
            Assert.Null(path_Open);
        }

        // 3 ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void ALegacyResultWithNoDesignKey_IsNeverClaimedCompatible()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design, recordKey: false);

            bool asked = false;

            Assert.Equal(PartOPreviousResultDecision.None, Modify.OfferPreviousPartOResult(partOOutputPaths, design, x => { asked = true; return PartOPreviousResultChoice.OpenPrevious; }, out string? _));
            Assert.False(asked);
        }

        [Fact]
        public void AResultOfAResult_AnOptimisationRound_AndAStaleResultsFile_AreNotOffered()
        {
            AnalyticalModel design = Design();

            //Derived from another result (2B / Iteration 3 shape).
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design, derivedFromResult: true);
            Assert.Null(Modify.FindPreviousPartOResult(partOOutputPaths, design));

            //An optimisation round is a result of the baseline, not of the design.
            Directory.Delete(partOOutputPaths.Directory_Tas, true);
            partOOutputPaths.CreateDirectories();
            SaveResult(partOOutputPaths, design, name: "run-Opt01");
            SaveResult(partOOutputPaths, design, name: "run-OptMax");
            Assert.Null(Modify.FindPreviousPartOResult(partOOutputPaths, design));

            //Results rewritten since the model was saved: the existing reopen would refuse it, so it is not offered.
            Directory.Delete(partOOutputPaths.Directory_Tas, true);
            partOOutputPaths.CreateDirectories();
            string path_Model = SaveResult(partOOutputPaths, design);
            Assert.NotNull(Modify.FindPreviousPartOResult(partOOutputPaths, design));
            File.WriteAllText(Path.ChangeExtension(path_Model, ".tsd"), "rewritten by a later run - and longer");
            Assert.Null(Modify.FindPreviousPartOResult(partOOutputPaths, design));
        }

        [Fact]
        public void NothingInTheFolder_OrNoFolder_AsksNothing()
        {
            AnalyticalModel design = Design();
            Func<PartOPreviousResult, PartOPreviousResultChoice> ask = _ => throw new InvalidOperationException();

            Assert.Equal(PartOPreviousResultDecision.None, Modify.OfferPreviousPartOResult(null, design, ask, out string? _));
            Assert.Equal(PartOPreviousResultDecision.None, Modify.OfferPreviousPartOResult(Paths(), design, ask, out string? _));
            Assert.Equal(PartOPreviousResultDecision.None, Modify.OfferPreviousPartOResult(Paths(), null, ask, out string? _));
        }

        // 4 ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void OpenPreviousResult_OpensTheSavedSam_AndStops_WithoutRunningAnything()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            string path_Model = SaveResult(partOOutputPaths, design);
            string before = Snapshot(root);

            List<string> opened = [];
            bool goOn = Modify.HandlePreviousPartOResult(partOOutputPaths, design, "Iteration 2", x => { opened.Add(x); return true; }, _ => PartOPreviousResultChoice.OpenPrevious, out bool previousOpened, out PartOWorkflowOutcome? outcome);

            Assert.False(goOn);
            Assert.True(previousOpened);
            Assert.Null(outcome);
            Assert.Equal(path_Model, Assert.Single(opened), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(".sam", Path.GetExtension(opened[0]));
            Assert.Equal(before, Snapshot(root));
        }

        [Fact]
        public void OpenPreviousResult_ThatCannotBeOpened_SaysSo_AndChangesNothing()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design);
            string before = Snapshot(root);

            bool goOn = Modify.HandlePreviousPartOResult(partOOutputPaths, design, "Iteration 2", _ => false, _ => PartOPreviousResultChoice.OpenPrevious, out bool previousOpened, out PartOWorkflowOutcome? outcome);

            Assert.False(goOn);
            Assert.False(previousOpened);
            Assert.NotNull(outcome);
            Assert.Equal(before, Snapshot(root));
        }

        // 5 ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void RunAgain_ContinuesIntoTheExistingReplacementQuestion()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design);
            string before = Snapshot(root);

            bool opened_Called = false;
            bool goOn = Modify.HandlePreviousPartOResult(partOOutputPaths, design, "Iteration 2", _ => { opened_Called = true; return true; }, _ => PartOPreviousResultChoice.RunAgain, out bool previousOpened, out PartOWorkflowOutcome? outcome);

            Assert.True(goOn);
            Assert.False(previousOpened);
            Assert.False(opened_Called);
            Assert.Null(outcome);

            //Nothing was replaced by choosing to run again...
            Assert.Equal(before, Snapshot(root));

            //...and the very next step is the unchanged #207 question, which still decides replacement.
            PartOOutputOccupancy? asked = null;
            PartOExistingResultsDecision partOExistingResultsDecision = Modify.ResolveExistingPartOResults(partOOutputPaths, Guid.Empty, "case", x => { asked = x; return true; });

            Assert.Equal(PartOExistingResultsDecision.Replace, partOExistingResultsDecision);
            Assert.NotNull(asked);
            Assert.True(asked!.Count_GeneratedFiles > 0);
        }

        // 6 ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void Cancel_ChangesNothing_AndRunsNothing()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design);
            string before = Snapshot(root);

            bool opened_Called = false;
            bool goOn = Modify.HandlePreviousPartOResult(partOOutputPaths, design, "Iteration 2", _ => { opened_Called = true; return true; }, _ => PartOPreviousResultChoice.Cancel, out bool previousOpened, out PartOWorkflowOutcome? outcome);

            Assert.False(goOn);
            Assert.False(previousOpened);
            Assert.False(opened_Called);
            Assert.Null(outcome);
            Assert.Equal(before, Snapshot(root));
        }

        [Fact]
        public void WhereTheHostCannotOpenAModel_NothingIsOffered_AndTheRunGoesOn()
        {
            AnalyticalModel design = Design();
            PartOOutputPaths partOOutputPaths = Paths();
            SaveResult(partOOutputPaths, design);

            bool goOn = Modify.HandlePreviousPartOResult(partOOutputPaths, design, "Iteration 2", null, _ => throw new InvalidOperationException(), out bool previousOpened, out PartOWorkflowOutcome? outcome);

            Assert.True(goOn);
            Assert.False(previousOpened);
            Assert.Null(outcome);
        }

        [Fact]
        public void TheWindowWording_NamesTheCase_AndSaysWhatEachButtonDoes()
        {
            Assert.Equal("Previous Iteration 2 result found", PartOPreviousResultWindow.Heading_Text("Iteration 2"));
            Assert.Contains("without simulating again", PartOPreviousResultWindow.Explanation);
            Assert.Contains("Run again", PartOPreviousResultWindow.Explanation);
        }
    }
}
