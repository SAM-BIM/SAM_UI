// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Enums;
using SAM.Analytical.UI.WPF.Tests.Helpers;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The preset a NEW view gets when its colour scheme is set to Part F data.
    /// <para>
    /// The problem it solves is discoverability: choosing the Part F colour scheme is the obvious way to ask
    /// for a Part F drawing, and before this it produced a coloured plan with no airflow on it and no
    /// indication that nine more options existed behind a separate dialog.
    /// </para>
    /// <para>
    /// The risk it introduces is the opposite one - a preset that reaches views it was not asked about - so
    /// half of these tests are about what it must NOT touch.
    /// </para>
    /// </summary>
    public class PartFAirflowPresetTests
    {
        // ------------------------------------------------------------------
        // The preset itself
        // ------------------------------------------------------------------

        /// <summary>
        /// Every value of the agreed preset, asserted one by one. A preset is a promise about what an engineer
        /// gets without asking, so it is worth spelling out rather than trusting to the defaults of a class
        /// somebody may change for another reason.
        /// </summary>
        [Fact]
        public void Preset_IsTheAgreedDrawing()
        {
            PartFAirflowViewSettings partFAirflowViewSettings = Analytical.UI.Create.PartFAirflowViewSettings(Model().AdjacencyCluster);

            Assert.True(partFAirflowViewSettings.Enabled);
            Assert.Equal(PartFDwellingFilter.AllDwellingsOnLevel, partFAirflowViewSettings.DwellingFilter);
            Assert.Equal(Guid.Empty, partFAirflowViewSettings.DwellingGuid);
            Assert.Equal(PartFOperatingMode.ContinuousDesign, partFAirflowViewSettings.OperatingMode);
            Assert.Equal(50, partFAirflowViewSettings.AnnotationScale);

            Assert.True(partFAirflowViewSettings.ShowSupply);
            Assert.True(partFAirflowViewSettings.ShowGeneralExtract);
            Assert.True(partFAirflowViewSettings.ShowLocalKitchenExtract);
            Assert.True(partFAirflowViewSettings.ShowTransfer);
            Assert.True(partFAirflowViewSettings.ShowUnresolved);
            Assert.True(partFAirflowViewSettings.ShowValues);
            Assert.True(partFAirflowViewSettings.ShowCompliance);
            Assert.True(partFAirflowViewSettings.ShowDoorRequirements);
            Assert.True(partFAirflowViewSettings.ShowContextGeometry);

            //A brand-new view has nothing anybody has moved.
            Assert.Empty(partFAirflowViewSettings.AnnotationOverrides);
        }

        /// <summary>The preset survives being saved and reopened, like any other view presentation.</summary>
        [Fact]
        public void Preset_RoundTrips()
        {
            PartFAirflowViewSettings partFAirflowViewSettings = new(Analytical.UI.Create.PartFAirflowViewSettings(Model().AdjacencyCluster).ToJsonObject());

            Assert.True(partFAirflowViewSettings.Enabled);
            Assert.Equal("Flats", partFAirflowViewSettings.ZoneCategoryName);
            Assert.Equal(50, partFAirflowViewSettings.AnnotationScale);
        }

        // ------------------------------------------------------------------
        // Resolving the dwelling category, without knowing the word "Flats"
        // ------------------------------------------------------------------

        /// <summary>
        /// One unambiguous dwelling category is selected automatically. Nothing hard-codes the name - the
        /// fixture calls it "Flats", and the resolution would find "Apartments" or "Dwellings" just the same.
        /// </summary>
        [Fact]
        public void Preset_OneDwellingCategory_IsSelectedAutomatically()
        {
            Assert.Equal("Flats", Analytical.UI.Create.PartFAirflowViewSettings(Model().AdjacencyCluster).ZoneCategoryName);
        }

        /// <summary>
        /// Several valid dwelling categories are left UNSET, for the user to choose. Which flats a drawing
        /// reports on is an engineering decision, and guessing it would produce a confident drawing of the
        /// wrong half of a mixed-use building.
        /// </summary>
        [Fact]
        public void Preset_SeveralDwellingCategories_AreLeftForTheUser()
        {
            PartFPlanModel model = Model();

            //A second, equally valid dwelling category.
            model.Zone("House 1", "Houses", true, "Bedroom");

            Assert.Equal(2, model.AdjacencyCluster.PartFDwellingZoneCategories().Count);
            Assert.Null(Analytical.UI.Create.PartFAirflowViewSettings(model.AdjacencyCluster).ZoneCategoryName);
        }

        /// <summary>
        /// A model with no zones at all gets no category, which selects the calculation's whole-house mode -
        /// the correct answer for a single dwelling that was never zoned.
        /// </summary>
        [Fact]
        public void Preset_NoDwellingStructure_UsesWholeHouseMode()
        {
            PartFPlanModel model = new PartFPlanModel()
                .Room("Studio", 8)
                .Room("Bathroom", 3)
                .Partition("Studio", "Bathroom", "D01")
                .Close();

            Assert.Empty(model.AdjacencyCluster.PartFDwellingZoneCategories());
            Assert.Null(Analytical.UI.Create.PartFAirflowViewSettings(model.AdjacencyCluster).ZoneCategoryName);
        }

        /// <summary>
        /// A category whose zones are all marked Is Dwelling = false is not a dwelling category - that is how a
        /// shared corridor or a landlord area is kept out - and a category alongside it that does hold a
        /// dwelling still resolves unambiguously.
        /// </summary>
        [Fact]
        public void DwellingCategories_ExcludeCategoriesWithNoDwellingZone()
        {
            PartFPlanModel model = Model();

            model.Zone("Corridor Zone", "Communal", false, "Corridor");

            List<string> names = model.AdjacencyCluster.PartFDwellingZoneCategories();

            Assert.Equal(["Flats"], names);
            Assert.Equal("Flats", Analytical.UI.Create.PartFAirflowViewSettings(model.AdjacencyCluster).ZoneCategoryName);
        }

        /// <summary>
        /// A category where NO zone carries Is Dwelling at all still counts, because that is what the
        /// calculation does with it: the parameter postdates the models, so every zone in such a category is
        /// sized as a dwelling rather than a legacy model silently sizing nothing.
        /// </summary>
        [Fact]
        public void DwellingCategories_IncludeAnUnmarkedLegacyCategory()
        {
            PartFPlanModel model = new PartFPlanModel()
                .Room("Studio", 8)
                .Room("Bathroom", 3)
                .Partition("Studio", "Bathroom", "D01")
                .ZoneWithoutIsDwelling("Flat 1", "Flats", "Studio", "Bathroom");

            Assert.Equal(["Flats"], model.AdjacencyCluster.PartFDwellingZoneCategories());
        }

        /// <summary>
        /// <b>The resolution agrees with the calculator.</b> A category this offers really does size at least
        /// one dwelling, and one it rejects sizes none - asserted by running the real calculation, so the
        /// duplicated rule cannot drift away from the one that matters.
        /// </summary>
        [Fact]
        public void DwellingCategories_AgreeWithTheCalculator()
        {
            PartFPlanModel model = Model();

            model.Zone("Corridor Zone", "Communal", false, "Corridor");

            List<string> names = model.AdjacencyCluster.PartFDwellingZoneCategories();

            Assert.Equal(["Flats"], names);

            //Offered: the calculation finds dwellings in it.
            Assert.NotEmpty(Calculate(model, "Flats"));

            //Rejected: it does not.
            Assert.Empty(Calculate(model, "Communal"));
        }

        // ------------------------------------------------------------------
        // What the preset must NOT touch
        // ------------------------------------------------------------------

        /// <summary>
        /// A view that already carries Part F settings keeps them. This is the case where an engineer has
        /// configured a drawing and then reopens View Settings - or opens them and changes something else
        /// entirely - and finding their nine choices reset to a preset would be worse than never having had
        /// one.
        /// </summary>
        [Fact]
        public void Preset_IsNotAppliedWhereSettingsAlreadyExist()
        {
            PartFAirflowViewSettings partFAirflowViewSettings_Existing = new()
            {
                Enabled = true,
                ShowTransfer = false,
                ShowCompliance = false,
                AnnotationScale = 100,
                OperatingMode = PartFOperatingMode.HighBoost,
            };

            partFAirflowViewSettings_Existing.AnnotationOverrides.Add(new PartFAnnotationOverride(Guid.NewGuid(), PartFAnnotationType.Terminal, new Point2D(1, 2)));

            //What the control does: the preset is offered only where there is nothing there.
            PartFAirflowViewSettings partFAirflowViewSettings = Applied(partFAirflowViewSettings_Existing, isNew: true, isPartFColorScheme: true, Model().AdjacencyCluster);

            Assert.Same(partFAirflowViewSettings_Existing, partFAirflowViewSettings);
            Assert.False(partFAirflowViewSettings.ShowTransfer);
            Assert.Equal(100, partFAirflowViewSettings.AnnotationScale);
            Assert.Equal(PartFOperatingMode.HighBoost, partFAirflowViewSettings.OperatingMode);

            //And the label somebody moved is still where they put it.
            Assert.Single(partFAirflowViewSettings.AnnotationOverrides);
        }

        /// <summary>
        /// An EXISTING view never gets the preset, whatever colour scheme it carries. Turning the annotation on
        /// across a project's saved drawings because somebody opened a settings dialog would be a change nobody
        /// asked for, made silently, to work that is already issued.
        /// </summary>
        [Fact]
        public void Preset_IsNotAppliedToAnExistingView()
        {
            Assert.Null(Applied(null, isNew: false, isPartFColorScheme: true, Model().AdjacencyCluster));
        }

        /// <summary>Another colour scheme gets no Part F settings at all - not even disabled ones.</summary>
        [Fact]
        public void Preset_IsNotAppliedToAnotherColorScheme()
        {
            Assert.Null(Applied(null, isNew: true, isPartFColorScheme: false, Model().AdjacencyCluster));
        }

        /// <summary>
        /// Duplicating a Part F view carries the source view's presentation across - including the labels
        /// somebody moved - rather than re-presetting it. A duplicate is a copy of a drawing, not a new one.
        /// </summary>
        [Fact]
        public void Duplicate_KeepsTheSourcePresentation()
        {
            PartFAirflowViewSettings partFAirflowViewSettings = Analytical.UI.Create.PartFAirflowViewSettings(Model().AdjacencyCluster);

            partFAirflowViewSettings.ShowTransfer = false;
            partFAirflowViewSettings.AnnotationScale = 200;
            partFAirflowViewSettings.AnnotationOverrides.Add(new PartFAnnotationOverride(Guid.NewGuid(), PartFAnnotationType.Transfer, new Point2D(3, 4)));

            //The copy constructor is what view duplication goes through.
            PartFAirflowViewSettings partFAirflowViewSettings_Duplicate = new(partFAirflowViewSettings);

            Assert.False(partFAirflowViewSettings_Duplicate.ShowTransfer);
            Assert.Equal(200, partFAirflowViewSettings_Duplicate.AnnotationScale);

            PartFAnnotationOverride partFAnnotationOverride = Assert.Single(partFAirflowViewSettings_Duplicate.AnnotationOverrides);

            Assert.Equal(3, partFAnnotationOverride.Position2D.X);
            Assert.Equal(4, partFAnnotationOverride.Position2D.Y);

            //A copy, not the same instance: moving a label on the duplicate must not move it on the original.
            Assert.NotSame(partFAirflowViewSettings.AnnotationOverrides[0], partFAnnotationOverride);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// The rule the view settings panel applies, in one place so a test can state it: the preset is used
        /// only for a new view, only where the Part F colour scheme is chosen, and only where the view has no
        /// Part F settings of its own.
        /// </summary>
        private static PartFAirflowViewSettings Applied(PartFAirflowViewSettings partFAirflowViewSettings, bool isNew, bool isPartFColorScheme, AdjacencyCluster adjacencyCluster)
        {
            return !isNew || partFAirflowViewSettings is not null || !isPartFColorScheme
                ? partFAirflowViewSettings
                : Analytical.UI.Create.PartFAirflowViewSettings(adjacencyCluster);
        }

        /// <summary>A block of two flats and a communal corridor, the corridor in no dwelling zone.</summary>
        private static PartFPlanModel Model()
        {
            return new PartFPlanModel()
                .Room("Studio", 8)
                .Room("Bathroom", 3)
                .Room("Corridor", 2)
                .Room("Bedroom", 7)
                .Room("Kitchen", 5)
                .Partition("Studio", "Bathroom", "D01")
                .Partition("Bathroom", "Corridor")
                .Partition("Corridor", "Bedroom")
                .Partition("Bedroom", "Kitchen", "D02")
                .Zone("Flat 1", "Flats", true, "Studio", "Bathroom")
                .Zone("Flat 2", "Flats", true, "Bedroom", "Kitchen")
                .LocalExtractMethod("Studio", PartFExtractMethod.MVHRContinuousTerminal)
                .LocalExtractMethod("Kitchen", PartFExtractMethod.MVHRContinuousTerminal);
        }

        private static List<PartFComplianceResult> Calculate(PartFPlanModel model, string zoneCategoryName)
        {
            PartFCalculator partFCalculator = new(Analytical.Create.PartFData(RuleSetPath())) { AdjacencyCluster = model.AdjacencyCluster };

            partFCalculator.Calculate(zoneCategoryName);

            return [.. (partFCalculator.DwellingResults ?? [])
                .Where(x => x?.ComplianceResult is not null)
                .Select(x => x.ComplianceResult)];
        }

        /// <summary>
        /// The shipped Part F rule set, found relative to this repository rather than copied into the test
        /// output: a stale copy of a rule set is exactly the kind of drift these tests exist to catch.
        /// </summary>
        private static string RuleSetPath()
        {
            System.IO.DirectoryInfo directoryInfo = new(AppDomain.CurrentDomain.BaseDirectory);

            while (directoryInfo is not null)
            {
                string path = System.IO.Path.Combine(directoryInfo.FullName, "SAM", "files", "resources", "Analytical", "SAM_PartFSpaceRulesUKDwellingsMVHR.json");
                if (System.IO.File.Exists(path))
                {
                    return path;
                }

                directoryInfo = directoryInfo.Parent;
            }

            throw new System.IO.FileNotFoundException("The shipped Part F rule set was not found above the test output directory.");
        }
    }
}
