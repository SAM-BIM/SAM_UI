// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.UI.WPF;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Tests for <see cref="Modify.BuildReportText(PartFCalculator)"/>: pure text generation, with no
    /// WPF dependency, so a model with thousands of spaces can be exercised as a plain string check
    /// rather than through a live report window.
    /// </summary>
    public class PartFReportTextTests
    {
        [Fact]
        public void BuildReportText_RetainsHeadingAndDwellingSummary()
        {
            PartFCalculator partFCalculator = new(null);

            partFCalculator.DwellingResults.Add(new PartFDwellingResult("Flat 1")
            {
                SpaceNames = ["Bedroom 1", "Bathroom 1"],
                HabitableRoomCount = 2,
                BedroomCount = 1,
                InternalFloorArea_M2 = 45.5,
                ContinuousDesignSystemRate_Lps = 13,
                SetbackSystemRate_Lps = 3.9,
                SetbackFlowRateFactor = 0.3,
                OneHabitableRoomRuleApplied = false,
            });

            string text = Modify.BuildReportText(partFCalculator);

            //Hard wrapping (see the 10,000-name and wrap tests below) can legitimately break this line
            //mid-phrase at the column width - a valid line break, not a content bug. Content-presence
            //and ordering checks read the unwrapped line so a wrap point can't fail them.
            string text_Unwrapped = text.Replace("\r", "").Replace("\n", " ");

            Assert.Contains("1 dwelling(s) sized.", text_Unwrapped);
            Assert.Contains("Flat 1:", text_Unwrapped);
            Assert.Contains("2 space(s)", text_Unwrapped);
            Assert.Contains("2 habitable room(s)", text_Unwrapped);
            Assert.Contains("1 bedroom(s)", text_Unwrapped);
            Assert.Contains("45.5 m2", text_Unwrapped);
            Assert.Contains("continuous design 13 l/s", text_Unwrapped);
            Assert.Contains("setback 3.9 l/s", text_Unwrapped);

            //Locks the summary line's field order: name, space count, area, habitable rooms, bedrooms,
            //then rates - so a future edit can't silently reorder it again.
            int nameIndex = text_Unwrapped.IndexOf("Flat 1:");
            int spaceIndex = text_Unwrapped.IndexOf("2 space(s)");
            int areaIndex = text_Unwrapped.IndexOf("45.5 m2");
            int habitableIndex = text_Unwrapped.IndexOf("2 habitable room(s)");
            int bedroomIndex = text_Unwrapped.IndexOf("1 bedroom(s)");
            int continuousIndex = text_Unwrapped.IndexOf("continuous design 13 l/s");

            Assert.True(nameIndex < spaceIndex);
            Assert.True(spaceIndex < areaIndex);
            Assert.True(areaIndex < habitableIndex);
            Assert.True(habitableIndex < bedroomIndex);
            Assert.True(bedroomIndex < continuousIndex);
        }

        [Fact]
        public void BuildReportText_NoDwellingSized_RetainsExplicitHeading()
        {
            PartFCalculator partFCalculator = new(null);

            string text = Modify.BuildReportText(partFCalculator);

            Assert.Contains("No dwelling was sized.", text);
        }

        [Fact]
        public void BuildReportText_IncludesWarningsNotesAndLocalKitchenExtractCheck_InOrder()
        {
            PartFCalculator partFCalculator = new(null);

            partFCalculator.DwellingResults.Add(new PartFDwellingResult("Flat 1") { HabitableRoomCount = 1, BedroomCount = 0 });
            partFCalculator.ExcludedZoneNames.Add("Corridor");
            partFCalculator.UnclassifiedSpaceNames.Add("Store");
            partFCalculator.Warnings.Add("Flat 1: ENGINEERING CHECK REQUIRED: This dwelling contains a cooking space, but no explicit local kitchen or cooker extract is represented.");
            partFCalculator.Remarks.Add("'Corridor' excluded from the Part F calculation because Is Dwelling is set to No.");

            string text = Modify.BuildReportText(partFCalculator);

            Assert.Contains("Zones not sized as dwellings: Corridor", text);
            Assert.Contains("Unclassified space(s): Store", text);
            Assert.Contains("WARNINGS", text);
            Assert.Contains("ENGINEERING CHECK REQUIRED", text);
            Assert.Contains("NOTES", text);
            Assert.Contains("excluded from the Part F calculation because Is Dwelling is set to No", text);
            Assert.Contains("LOCAL KITCHEN EXTRACT:", text);
            Assert.Contains("Using this tool does not by itself demonstrate compliance", text);

            //Section order must match the pre-existing MessageBox report exactly: summary, excluded
            //zones, unclassified spaces, warnings, notes, local kitchen extract, disclaimer.
            int dwellingIndex = text.IndexOf("1 dwelling(s) sized.");
            int excludedIndex = text.IndexOf("Zones not sized as dwellings:");
            int unclassifiedIndex = text.IndexOf("Unclassified space(s):");
            int warningsIndex = text.IndexOf("WARNINGS");
            int notesIndex = text.IndexOf("NOTES");
            int kitchenIndex = text.IndexOf("LOCAL KITCHEN EXTRACT:");
            int disclaimerIndex = text.IndexOf("Using this tool does not by itself demonstrate compliance");

            Assert.True(dwellingIndex < excludedIndex);
            Assert.True(excludedIndex < unclassifiedIndex);
            Assert.True(unclassifiedIndex < warningsIndex);
            Assert.True(warningsIndex < notesIndex);
            Assert.True(notesIndex < kitchenIndex);
            Assert.True(kitchenIndex < disclaimerIndex);
        }

        [Fact]
        public void BuildReportText_WithoutEngineeringCheckWarning_OmitsLocalKitchenExtractSection()
        {
            PartFCalculator partFCalculator = new(null);
            partFCalculator.Warnings.Add("Some other warning, unrelated to kitchen extract.");

            string text = Modify.BuildReportText(partFCalculator);

            Assert.DoesNotContain("LOCAL KITCHEN EXTRACT:", text);
        }

        /// <summary>
        /// The Approved Document warnings are full paragraphs, so without hard wrapping a single line
        /// runs several screens wide and has to be read by scrolling sideways. Every line must fit a
        /// sensible column width.
        /// </summary>
        [Fact]
        public void BuildReportText_WrapsLongLines()
        {
            PartFCalculator partFCalculator = new(null);
            partFCalculator.Warnings.Add("Flat 1: ENGINEERING CHECK REQUIRED: This dwelling contains a cooking space, but no explicit local kitchen or cooker extract is represented. Extract from a bathroom, ensuite or other wet room may balance the dwelling airflow but does not demonstrate compliance with the local kitchen-extract requirement.");

            string text = Modify.BuildReportText(partFCalculator);

            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                Assert.True(line.TrimEnd('\r').Length <= 100, string.Format("Line exceeds the wrap width: '{0}'", line));
            }

            //Wrapping must not lose or corrupt the content it breaks up.
            Assert.Contains("ENGINEERING CHECK REQUIRED", text);
            Assert.Contains("cooker extract", text);
        }

        /// <summary>
        /// Wrapping breaks on whitespace only. A space name is a single word, so it must survive whole -
        /// a report naming "Bedroo" and "m 1" would be worse than no report at all.
        /// </summary>
        [Fact]
        public void BuildReportText_DoesNotSplitLongSpaceNamesInHalf()
        {
            string spaceName = "A_Very_Long_Space_Name_That_On_Its_Own_Comfortably_Exceeds_The_Wrap_Column_Width_And_Then_Some_More";

            PartFCalculator partFCalculator = new(null);
            partFCalculator.UnclassifiedSpaceNames.Add(spaceName);

            string text = Modify.BuildReportText(partFCalculator);

            Assert.Contains(spaceName, text);
        }

        /// <summary>
        /// A model with roughly 10,000 spaces produces a report with thousands of space names in a
        /// single unzoned-space line, exactly as PartFCalculator.Calculate(string) does today. This must
        /// build without throwing or hanging - the scale that motivated moving off MessageBox in the
        /// first place.
        /// </summary>
        [Fact]
        public void BuildReportText_WithTenThousandSpaceNames_GeneratesWithoutFailure()
        {
            PartFCalculator partFCalculator = new(null);

            List<string> spaceNames = Enumerable.Range(1, 10_000).Select(i => string.Format("Space_{0}", i)).ToList();

            partFCalculator.UnclassifiedSpaceNames.AddRange(spaceNames);
            partFCalculator.Warnings.Add(string.Format(
                "{0} space(s) do not belong to any dwelling zone of category 'Flats' and were given no ventilation properties: {1}. Shared and landlord areas are expected here; any dwelling space in this list is missing from its flat.",
                spaceNames.Count,
                string.Join(", ", spaceNames)));

            string text = Modify.BuildReportText(partFCalculator);

            Assert.Contains("Unclassified space(s):", text);
            Assert.Contains("Space_1,", text);
            Assert.Contains("Space_10000", text);
            Assert.Contains("10000 space(s) do not belong to any dwelling zone", text);

            //No truncation: every generated name must survive into the report text.
            foreach (string spaceName in spaceNames)
            {
                Assert.Contains(spaceName, text);
            }
        }
    }
}
