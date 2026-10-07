// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core;
using SAM.Geometry.Spatial;
using SAM.Weather;
using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Direct T3D on the Part O case. Which conversion produced a run is part of its case: it is in the case key, the scenario
    /// fingerprint, the canonical TBD's fingerprint and the saved sidecar - and only where it is Direct, so every gbXML case keeps
    /// exactly the key, fingerprint and file it always had.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class PartODirectT3DTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "SAM_PartODirectT3DTests_" + Guid.NewGuid().ToString("N"));

        private static readonly WeatherData weatherData = new("Leeds", "Leeds", 53.8, -1.5, 50);

        public PartODirectT3DTests()
        {
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch
            {
            }
        }

        private PartOSimulationContext Context(SolarCalculationMethod solarCalculationMethod = SolarCalculationMethod.TAS, bool directT3D = false)
        {
            return new PartOSimulationContext(directory, "Flat1", weatherData, solarCalculationMethod, 1, 365) { DirectT3D = directT3D };
        }

        // ---- the case ---------------------------------------------------------------------------------

        [Fact]
        public void TheCase_DefaultsToGbXML_AndIsNeverCarriedFromAnEarlierSession()
        {
            Assert.False(new PartOSimulationCase().DirectT3D);

            AnalyticalModel analyticalModel = new("Flat", null, null, null, new AdjacencyCluster(), new MaterialLibrary("Materials"), new ProfileLibrary("Profiles"));
            analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);

            SimulateOptions remembered = new() { DirectT3D = true, SolarCalculationMethod = SolarCalculationMethod.TAS };

            Assert.False(PartOSimulationCase.Create(analyticalModel, Path.Combine(directory, "Flat.sam"), remembered).DirectT3D);
        }

        [Fact]
        public void CaseKey_OfAGbXMLCase_IsUnchanged_AndDirectIsADifferentCase()
        {
            string key_GbXML = Query.PartOSimulationCaseKey(Context());

            Assert.EndsWith("|TAS", key_GbXML);
            Assert.DoesNotContain("T3D", key_GbXML);

            string key_Direct = Query.PartOSimulationCaseKey(Context(directT3D: true));

            Assert.Equal(key_GbXML + "|T3D=Direct", key_Direct);
            Assert.NotEqual(key_GbXML, key_Direct);

            //The case and the context it becomes give the same key - what the Hub compares evidence against.
            Assert.Equal(key_Direct, Query.PartOSimulationCaseKey(new PartOSimulationCase { WeatherData = weatherData, SolarCalculationMethod = SolarCalculationMethod.TAS, DirectT3D = true }));
            Assert.Equal(key_GbXML, Query.PartOSimulationCaseKey(new PartOSimulationCase { WeatherData = weatherData, SolarCalculationMethod = SolarCalculationMethod.TAS }));
        }

        /// <summary>
        /// Only the TAS solar calculation converts through a T3D, so Direct with another method is the same case as without it.
        /// </summary>
        [Theory]
        [InlineData(SolarCalculationMethod.SAM)]
        [InlineData(SolarCalculationMethod.None)]
        public void Direct_WithoutTAS_IsTheSameCase(SolarCalculationMethod solarCalculationMethod)
        {
            Assert.False(Context(solarCalculationMethod, true).UsesDirectT3D);
            Assert.Equal(Query.PartOSimulationCaseKey(Context(solarCalculationMethod)), Query.PartOSimulationCaseKey(Context(solarCalculationMethod, true)));
            Assert.Equal(PartOIteration3ScenarioFingerprint(Context(solarCalculationMethod)), PartOIteration3ScenarioFingerprint(Context(solarCalculationMethod, true)));
        }

        [Fact]
        public void ScenarioFingerprint_OfAGbXMLCase_IsUnchanged_AndNamesDirect()
        {
            string fingerprint_GbXML = PartOIteration3ScenarioFingerprint(Context());

            Assert.DoesNotContain("t3d", fingerprint_GbXML);
            Assert.EndsWith("updateConstructionLayersByPanelType=True", fingerprint_GbXML);

            Assert.Equal(fingerprint_GbXML + " | t3d=Direct", PartOIteration3ScenarioFingerprint(Context(directT3D: true)));
        }

        private static string PartOIteration3ScenarioFingerprint(PartOSimulationContext partOSimulationContext)
        {
            return Query.PartOIteration3ScenarioFingerprint(partOSimulationContext);
        }

        /// <summary>
        /// Reference A and Candidate B are copies of one case: they cannot end up on different routes.
        /// </summary>
        [Fact]
        public void ACopyOfTheContext_StaysOnItsRoute()
        {
            Assert.True(Context(directT3D: true).Copy("Flat1-It3B").DirectT3D);
            Assert.False(Context().Copy("Flat1-It3B").DirectT3D);
        }

        // ---- the canonical TBD ------------------------------------------------------------------------

        /// <summary>
        /// A conversion made by one route is never the baseline of a case on the other.
        /// </summary>
        [Fact]
        public void CanonicalTBD_IsOnlyValidForItsOwnRoute()
        {
            AnalyticalModel analyticalModel = Model();

            string path_Direct = Path.Combine(directory, "Direct.tbd");
            File.WriteAllText(path_Direct, "canonical");

            PartOCanonicalTBD canonical_Direct = PartOCanonicalTBD.Adopt(path_Direct, analyticalModel, Context(directT3D: true), out string refusal);
            Assert.NotNull(canonical_Direct);
            Assert.Null(refusal);

            Assert.True(canonical_Direct.IsValidFor(analyticalModel, Context(directT3D: true), out string refusal_Same), refusal_Same);
            Assert.False(canonical_Direct.IsValidFor(analyticalModel, Context(), out string refusal_Other));
            Assert.NotNull(refusal_Other);

            string path_GbXML = Path.Combine(directory, "GbXML.tbd");
            File.WriteAllText(path_GbXML, "canonical");

            PartOCanonicalTBD canonical_GbXML = PartOCanonicalTBD.Adopt(path_GbXML, analyticalModel, Context(), out _);
            Assert.True(canonical_GbXML.IsValidFor(analyticalModel, Context(), out string refusal_GbXML), refusal_GbXML);
            Assert.False(canonical_GbXML.IsValidFor(analyticalModel, Context(directT3D: true), out _));
        }

        /// <summary>
        /// The canonical fingerprint names Direct only where it is used, so a gbXML case keeps the fingerprint it always had.
        /// </summary>
        [Fact]
        public void CanonicalFingerprint_StatesDirectOnlyWhereItIsUsed()
        {
            AnalyticalModel analyticalModel = Model();

            string path = Path.Combine(directory, "Canonical.tbd");
            File.WriteAllText(path, "canonical");

            string fingerprint = PartOCanonicalTBD.Adopt(path, analyticalModel, Context(), out _).Fingerprint;
            string fingerprint_Direct = PartOCanonicalTBD.Adopt(path, analyticalModel, Context(directT3D: true), out _).Fingerprint;

            Assert.NotEqual(fingerprint, fingerprint_Direct);

            //Direct with a method that does not convert through a T3D is not a different conversion.
            Assert.Equal(
                PartOCanonicalTBD.Adopt(path, analyticalModel, Context(SolarCalculationMethod.SAM), out _).Fingerprint,
                PartOCanonicalTBD.Adopt(path, analyticalModel, Context(SolarCalculationMethod.SAM, true), out _).Fingerprint);
        }

        // ---- the saved sidecar ------------------------------------------------------------------------

        [Fact]
        public void Sidecar_RoundTripsDirect_AndALegacyOrGarbageValueReadsAsGbXML()
        {
            string path = Path.Combine(directory, "Flat1" + PartORunResume.Suffix_Resume);

            File.WriteAllText(path, new PartORunResume { DirectT3D = true }.ToJsonObject().ToJsonString());
            Assert.True(PartORunResume.Read(path).DirectT3D);

            File.WriteAllText(path, new PartORunResume().ToJsonObject().ToJsonString());
            Assert.False(PartORunResume.Read(path).DirectT3D);

            foreach (string stored in new[] { null, "null", "false", "\"true\"", "\"Direct\"", "1" })
            {
                JsonObject jObject = new PartORunResume { DirectT3D = true }.ToJsonObject();
                jObject.Remove("DirectT3D");
                if (stored != null)
                {
                    jObject.Add("DirectT3D", JsonNode.Parse(stored));
                }

                File.WriteAllText(path, jObject.ToJsonString());

                Assert.False(PartORunResume.Read(path).DirectT3D);
            }
        }

        // ---- the run ----------------------------------------------------------------------------------

        private (WorkflowSettings Settings, string Refusal, bool Xml) Run(PartOSimulationContext context, PartOCanonicalTBD canonical = null)
        {
            WorkflowSettings captured = null;

            Modify.RunPartOSimulation(
                Model(),
                context,
                "Flat1",
                null,
                CancellationToken.None,
                out _,
                out _,
                out _,
                out _,
                out _,
                out string refusal,
                canonical,
                (AnalyticalModel model, WorkflowSettings workflowSettings, CancellationToken _, out bool cancelled) =>
                {
                    cancelled = false;
                    captured = workflowSettings;
                    return model;
                });

            return (captured, refusal, File.Exists(Path.Combine(directory, "Flat1.xml")));
        }

        /// <summary>
        /// A warm-started round starts from the canonical TBD and converts nothing - SAM_Tas refuses Direct together with one - so
        /// it runs on the gbXML settings whichever route made the canonical, and writes no gbXML either way.
        /// </summary>
        [Fact]
        public void WarmStartedRound_OfADirectCase_DoesNotAskForAConversion()
        {
            AnalyticalModel analyticalModel = Model();
            string path = Path.Combine(directory, "Canonical.tbd");
            File.WriteAllText(path, "canonical");

            PartOSimulationContext context = Context(directT3D: true);
            PartOCanonicalTBD canonical = PartOCanonicalTBD.Adopt(path, analyticalModel, context, out _);

            (WorkflowSettings settings, string refusal, bool xml) = Run(context, canonical);

            Assert.Null(refusal);
            Assert.NotNull(settings);
            Assert.Equal(T3DRoute.GbXML, settings.T3DRoute);
            Assert.Equal(path, settings.Path_TBD_Canonical);
            Assert.Null(settings.Path_gbXML);
            Assert.False(xml);
        }

        [Fact]
        public void Run_OfADirectCase_UsesDirect_WithNoGbXML_AndAGbXMLCaseIsUnchanged()
        {
            (WorkflowSettings settings_Direct, string refusal_Direct, bool xml_Direct) = Run(Context(directT3D: true));

            Assert.Null(refusal_Direct);
            Assert.Equal(T3DRoute.Direct, settings_Direct.T3DRoute);
            Assert.Null(settings_Direct.Path_gbXML);
            Assert.False(xml_Direct);

            (WorkflowSettings settings, string refusal, bool xml) = Run(Context());

            Assert.Null(refusal);
            Assert.Equal(T3DRoute.GbXML, settings.T3DRoute);
            Assert.Equal(Path.Combine(directory, "Flat1.xml"), settings.Path_gbXML);
            Assert.True(xml);
        }

        // ---- the Mixed Design case, and the two windows ------------------------------------------------

        [Fact]
        public void MixedDesignContext_CarriesTheCaseRoute()
        {
            PartOSimulationCase partOSimulationCase = new() { WeatherData = weatherData, OutputDirectory = directory, SolarCalculationMethod = SolarCalculationMethod.TAS, DirectT3D = true };

            Assert.True(Create.PartOMixedSimulationContext(Model(), null, partOSimulationCase, "Flat1_Mixed").DirectT3D);

            partOSimulationCase.DirectT3D = false;

            Assert.False(Create.PartOMixedSimulationContext(Model(), null, partOSimulationCase, "Flat1_Mixed").DirectT3D);
        }

        [WpfFact]
        public void HubWindow_ShowsTheCaseRoute_AndOffersItOnlyWithTAS()
        {
            PartOWorkflowWindow window = new()
            {
                SimulationCase = new PartOSimulationCase { WeatherData = weatherData, OutputDirectory = directory, DirectT3D = true },
            };

            window.CompleteInitialisation();

            CheckBox checkBox = (CheckBox)window.FindName("checkBox_DirectT3D");

            Assert.True(checkBox.IsChecked);
            Assert.True(checkBox.IsEnabled);
            Assert.True(window.SimulationCase.DirectT3D);
            Assert.Contains("Direct T3D", window.SimulationCaseSummary);

            window.SimulationCase = new PartOSimulationCase { WeatherData = weatherData, OutputDirectory = directory, SolarCalculationMethod = SolarCalculationMethod.SAM, DirectT3D = true };

            Assert.False(checkBox.IsEnabled);
            Assert.False(window.SimulationCase.DirectT3D);
            Assert.DoesNotContain("Direct T3D", window.SimulationCaseSummary);

            window.SimulationCase = new PartOSimulationCase { WeatherData = weatherData, OutputDirectory = directory };

            Assert.False(checkBox.IsChecked);
            Assert.False(window.SimulationCase.DirectT3D);
            Assert.DoesNotContain("Direct T3D", window.SimulationCaseSummary);

            window.Close();
        }

        // ---- helpers ----------------------------------------------------------------------------------

        private static AnalyticalModel Model()
        {
            AdjacencyCluster adjacencyCluster = new();

            Space space = new("Bedroom 1", new Point3D(5, 5, 1.5))
            {
                InternalCondition = new InternalCondition("Double Bedroom - Bedroom 1"),
            };

            Face3D face3D = new(new Polygon3D([new Point3D(0, 0, 0), new Point3D(10, 0, 0), new Point3D(10, 0, 3), new Point3D(0, 0, 3)]));
            Panel panel = Analytical.Create.Panel(new Construction(Guid.NewGuid(), "External Wall"), PanelType.WallExternal, face3D);

            adjacencyCluster.AddObject(space);
            adjacencyCluster.AddObject(panel);
            adjacencyCluster.AddRelation(space, panel);

            return new AnalyticalModel("Flat1", null, null, null, adjacencyCluster, new MaterialLibrary("Materials"), new ProfileLibrary("Profiles"));
        }
    }
}
