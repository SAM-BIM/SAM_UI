// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI.WPF;
using SAM.Weather;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>The hour count a Part O assessment requires a room's hourly series to reach.</b>
    ///
    /// <para><b>The gap this closes</b></para>
    /// <para>
    /// Approved Document O's dynamic method assesses annual and summer criteria, so a verdict produced from
    /// part of a year is not the verdict the document asks for. Part O checked that the simulation's
    /// nominal DATE RANGE was a full year - <c>PartOSimulationContext.IsFullYear</c>, and the
    /// <c>fullYear</c> flag <c>RunPartOSimulation</c> hands back - which states what was asked of TAS and
    /// not what the results file actually contains. A damaged or partially written TSD therefore reached
    /// the assessment, where <c>TMOverheatingCalculator</c> walked whatever hours the two series had and
    /// reported the answer as the room's.
    /// </para>
    /// <para>
    /// The calculator now refuses a room whose series are absent, empty or of unequal length on its own
    /// account, because none of those is assessable at any length. Whether an equal-length pair is long
    /// enough to be a YEAR is a question about the run rather than the calculation, so the calculator asks
    /// it only where a caller states the answer - and this is the number Part O states.
    /// </para>
    ///
    /// <para><b>Why the weather year</b></para>
    /// <para>
    /// It is already the authority for the comfort band the TM59 criteria are measured against:
    /// <c>TMOverheatingCalculator</c> derives that band from the same <c>WeatherYear</c>, and
    /// <c>Collect</c> refuses any hour the band does not cover. A requirement taken from anywhere else -
    /// a literal 8760, say - could disagree with the band the same run is judged by, and a year is however
    /// many hours its own weather data holds.
    /// </para>
    /// </summary>
    public class PartOFullYearSeriesTests
    {
        /// <summary>A populated year of flat hourly dry-bulb values - 365 days of 24.</summary>
        private static WeatherYear WeatherYear(int days = 365)
        {
            WeatherYear result = new(2018);

            for (int day = 0; day < days; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    result.Add(day, hour, new System.Collections.Generic.Dictionary<string, double> { { WeatherDataType.DryBulbTemperature.ToString(), 20.0 } });
                }
            }

            return result;
        }

        private static AnalyticalModel Model(WeatherData weatherData)
        {
            AdjacencyCluster adjacencyCluster = new();
            adjacencyCluster.AddObject(new Space("Bedroom 1"));

            AnalyticalModel result = new("Flat1", null, null, null, adjacencyCluster);

            if (weatherData is not null)
            {
                result.SetValue(SAM.Analytical.AnalyticalModelParameter.WeatherData, weatherData);
            }

            return result;
        }

        /// <summary>
        /// A populated 365-day weather year requires 8760 hourly values - the count a complete annual TSD
        /// series carries, derived rather than written down.
        /// </summary>
        [Fact]
        public void AFullWeatherYear_RequiresItsOwnHourCount()
        {
            AnalyticalModel analyticalModel = Model(new WeatherData("Test", "Test", 51.5, -0.1, 0, WeatherYear()));

            Assert.Equal(8760, PartOTM59Assessment.HourCount_WeatherYear(analyticalModel));
        }

        /// <summary>
        /// The count is <b>read off the year</b> rather than written down, so it is the same number the
        /// comfort band the criteria are measured against is derived from and the two cannot disagree.
        /// <para>
        /// Shown with a year the fixture only partly populates: the requirement follows what the data
        /// actually holds. A <c>WeatherYear</c> is 365 days by construction - a 366th day is not carried,
        /// which is why the full case above is 8760 - so the leap-year surplus arrives as a longer SERIES
        /// against this shorter band, and that direction is handled where it belongs, by
        /// <c>TMOverheatingCalculator</c> excluding any hour the band does not cover.
        /// </para>
        /// </summary>
        [Fact]
        public void TheRequirement_IsReadOffTheYearRatherThanWrittenDown()
        {
            WeatherYear weatherYear = WeatherYear(100);

            AnalyticalModel analyticalModel = Model(new WeatherData("Test", "Test", 51.5, -0.1, 0, weatherYear));

            Assert.Equal(2400, PartOTM59Assessment.HourCount_WeatherYear(analyticalModel));

            //And it IS the year's own hour count, not a formula that happens to agree with it.
            Assert.Equal(weatherYear.GetWeatherHours().Count, PartOTM59Assessment.HourCount_WeatherYear(analyticalModel));
        }

        /// <summary>
        /// <b>0 - enforce nothing - where there is no weather year to count, rather than a guess.</b>
        /// <para>
        /// A guessed requirement would refuse rooms on the strength of a number nothing in the run stated.
        /// It is not the case that decides anything either way: the comfort lookup needs a weather year, so
        /// a TSD carrying none cannot be assessed at all - and the mismatched and empty series the
        /// calculator refuses on its own account are still refused here.
        /// </para>
        /// </summary>
        [Fact]
        public void NoWeatherYear_StatesNoRequirementRatherThanGuessingOne()
        {
            Assert.Equal(0, PartOTM59Assessment.HourCount_WeatherYear(Model(null)));

            //A model carrying weather data with no years in it is the same case. This one used to throw a
            //NullReferenceException out of WeatherData.WeatherYears, which every caller in the repository
            //already reads as `WeatherYears?.FirstOrDefault()` and so was written expecting null from.
            Assert.Equal(0, PartOTM59Assessment.HourCount_WeatherYear(Model(new WeatherData("Test", "Test", 51.5, -0.1, 0))));

            Assert.Equal(0, PartOTM59Assessment.HourCount_WeatherYear(null));
        }

        /// <summary>
        /// The requirement, once stated, is what the calculation actually refuses on - the two halves joined
        /// up. A full-year pair proceeds; an equal-length pair short of the year does not, and says so.
        /// </summary>
        [Fact]
        public void TheStatedRequirement_IsWhatTheCalculationRefusesOn()
        {
            WeatherData weatherData = new("Test", "Test", 51.5, -0.1, 0, WeatherYear());

            int hourCount_Expected = PartOTM59Assessment.HourCount_WeatherYear(Model(weatherData));

            Assert.Equal(8760, hourCount_Expected);

            //A room whose series are a full year of paired values.
            AnalyticalModel analyticalModel_Full = SeriesModel(weatherData, hourCount_Expected);

            TMOverheatingCalculator tMOverheatingCalculator_Full = Calculator(analyticalModel_Full, hourCount_Expected);

            Assert.Single(tMOverheatingCalculator_Full.Calculate_TM59(analyticalModel_Full.GetSpaces()));
            Assert.Empty(tMOverheatingCalculator_Full.HourlySeriesRefusals);

            //And the same room with a results file that stops short of the year - both series equally, so
            //nothing here is a mismatch.
            AnalyticalModel analyticalModel_Partial = SeriesModel(weatherData, 5000);

            TMOverheatingCalculator tMOverheatingCalculator_Partial = Calculator(analyticalModel_Partial, hourCount_Expected);

            Assert.Empty(tMOverheatingCalculator_Partial.Calculate_TM59(analyticalModel_Partial.GetSpaces()));

            string refusal = Assert.Single(tMOverheatingCalculator_Partial.HourlySeriesRefusals);
            Assert.Contains("Bedroom 1", refusal);
            Assert.Contains("only 5000 of the 8760", refusal);
        }

        /// <summary>One space carrying <paramref name="count"/> paired hourly values.</summary>
        private static AnalyticalModel SeriesModel(WeatherData weatherData, int count)
        {
            Space space = new("Bedroom 1");

            System.Text.Json.Nodes.JsonArray jsonArray_ResultantTemperature = [];
            System.Text.Json.Nodes.JsonArray jsonArray_OccupancySensibleGain = [];

            for (int i = 0; i < count; i++)
            {
                jsonArray_ResultantTemperature.Add(24.0);
                jsonArray_OccupancySensibleGain.Add(80.0);
            }

            Core.ParameterSet parameterSet = new("SAM.Analytical.Tas.dll");
            parameterSet.Add(Core.Query.Name(SpaceSimulationResultParameter.ResultantTemperature), jsonArray_ResultantTemperature);
            parameterSet.Add(Core.Query.Name(SpaceSimulationResultParameter.OccupancySensibleGain), jsonArray_OccupancySensibleGain);
            space.Add(parameterSet);

            AdjacencyCluster adjacencyCluster = new();
            adjacencyCluster.AddObject(space);

            AnalyticalModel result = new("Flat1", null, null, null, adjacencyCluster);
            result.SetValue(SAM.Analytical.AnalyticalModelParameter.WeatherData, weatherData);

            return result;
        }

        /// <summary>
        /// A calculator with an explicit empty <c>TextMap</c>, so the space resolves to no TM59 application
        /// and the criterion selection is deterministic without depending on a shipped resource file being
        /// installed on the machine running the tests.
        /// </summary>
        private static TMOverheatingCalculator Calculator(AnalyticalModel analyticalModel, int hourCount_Expected)
        {
            return new TMOverheatingCalculator(analyticalModel)
            {
                TextMap = Core.Create.TextMap("TM59"),
                HourCount_Expected = hourCount_Expected
            };
        }
    }
}
