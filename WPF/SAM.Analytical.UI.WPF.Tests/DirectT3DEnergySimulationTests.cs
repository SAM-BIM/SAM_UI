// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core;
using SAM.Geometry.Spatial;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// "Direct T3D" on the Simulate - Energy Simulation dialog. It is carried as <see cref="SimulateOptions.DirectT3D"/>
    /// (false by default and for options saved before it existed) and, for the TAS solar calculation only, becomes
    /// <see cref="WorkflowSettings.T3DRoute"/> = <see cref="T3DRoute.Direct"/> with no gbXML written. The Part O case carries it too (see PartODirectT3DTests).
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class DirectT3DEnergySimulationTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "SAM_DirectT3DEnergySimulationTests_" + Guid.NewGuid().ToString("N"));

        public DirectT3DEnergySimulationTests()
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

        // ---- the remembered options -------------------------------------------------------------------

        [Fact]
        public void SimulateOptions_DefaultToGbXML_AndRoundTrip()
        {
            Assert.False(new SimulateOptions().DirectT3D);
            Assert.False(new SimulateOptions(new SimulateOptions()).DirectT3D);

            SimulateOptions direct = new() { DirectT3D = true };
            Assert.True(new SimulateOptions(direct).DirectT3D);
            Assert.True(new SimulateOptions(direct.ToJsonObject()).DirectT3D);
            Assert.False(new SimulateOptions(new SimulateOptions().ToJsonObject()).DirectT3D);
        }

        /// <summary>
        /// Options saved before the setting existed (no key), or with a null / non-boolean value, are the gbXML route.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("\"true\"")]
        [InlineData("\"Direct\"")]
        [InlineData("1")]
        public void LegacyOrUnrecognisedStoredValue_LoadsAsGbXML(string stored)
        {
            JsonObject jObject = new SimulateOptions() { DirectT3D = true }.ToJsonObject();
            jObject.Remove("DirectT3D");
            if (stored != null)
            {
                jObject.Add("DirectT3D", JsonNode.Parse(stored));
            }

            Assert.False(new SimulateOptions(jObject).DirectT3D);
        }

        [Fact]
        public void PartOSimulationContext_DefaultsToGbXML_AndCopyCarriesIt()
        {
            PartOSimulationContext context = new(directory, "Flat", null, SolarCalculationMethod.TAS, 1, 365);

            Assert.False(context.DirectT3D);
            Assert.False(context.Copy("Other").DirectT3D);

            context.DirectT3D = true;
            Assert.True(context.Copy("Other").DirectT3D);
        }

        // ---- the dialog -------------------------------------------------------------------------------

        [WpfFact]
        public void NewDialog_IsUnchecked_AndOptionsAreGbXML()
        {
            SimulateControl control = new();

            Assert.False(Box(control).IsChecked);
            Assert.False(control.DirectT3D);
            Assert.False(control.SimulateOptions.DirectT3D);
        }

        [WpfFact]
        public void CheckingTheBox_IsCarriedInTheOptions_AndReopensChecked()
        {
            SimulateControl control = new() { Simulate = true, SolarCalculationMethod = SolarCalculationMethod.TAS };

            Box(control).IsChecked = true;
            SimulateOptions options = control.SimulateOptions;
            Assert.True(options.DirectT3D);
            Assert.Equal(SolarCalculationMethod.TAS, options.SolarCalculationMethod);

            SimulateControl reopened = new();
            reopened.SimulateOptions = new SimulateOptions(options.ToJsonObject());
            Assert.True(Box(reopened).IsChecked);
            Assert.True(reopened.DirectT3D);
            Assert.True(Box(reopened).IsEnabled);

            Box(reopened).IsChecked = false;
            Assert.False(reopened.SimulateOptions.DirectT3D);
        }

        /// <summary>
        /// Remembered options saved before the setting existed open unchecked, even over a dialog that showed Direct.
        /// </summary>
        [WpfFact]
        public void LegacyOptions_OpenUnchecked()
        {
            SimulateControl control = new() { DirectT3D = true };

            JsonObject jObject = new SimulateOptions() { DirectT3D = true }.ToJsonObject();
            jObject.Remove("DirectT3D");
            control.SimulateOptions = new SimulateOptions(jObject);

            Assert.False(Box(control).IsChecked);
        }

        /// <summary>
        /// Direct only replaces the gbXML import into a T3D, which only the TAS solar calculation does; with another method,
        /// or with Simulate unticked, the box is not offered.
        /// </summary>
        [WpfFact]
        public void Box_IsOfferedOnlyForTheTASMethodWhileSimulating()
        {
            SimulateControl control = new() { Simulate = true };

            control.SolarCalculationMethod = SolarCalculationMethod.TAS;
            Assert.True(Box(control).IsEnabled);

            control.SolarCalculationMethod = SolarCalculationMethod.SAM;
            Assert.False(Box(control).IsEnabled);

            control.SolarCalculationMethod = SolarCalculationMethod.None;
            Assert.False(Box(control).IsEnabled);

            control.SolarCalculationMethod = SolarCalculationMethod.TAS;
            Assert.True(Box(control).IsEnabled);

            control.Simulate = false;
            Assert.False(Box(control).IsEnabled);

            control.Simulate = true;
            Assert.True(Box(control).IsEnabled);
        }

        /// <summary>
        /// The Part O route does not lock the box: which conversion produced a run is part of its case, so it stays a choice.
        /// </summary>
        [WpfFact]
        public void PartORoute_KeepsTheBoxAvailable()
        {
            SimulateControl control = new();
            control.SimulateOptions = new SimulateOptions() { DirectT3D = true, SolarCalculationMethod = SolarCalculationMethod.TAS };

            control.LockPartOSettings();

            Assert.True(Box(control).IsChecked);
            Assert.True(Box(control).IsEnabled);
            Assert.True(control.SimulateOptions.DirectT3D);
        }

        // ---- the run ----------------------------------------------------------------------------------

        /// <summary>
        /// What reaches the TAS workflow, through the real <c>RunPartOSimulation</c> with TAS replaced at its seam: the route,
        /// the gbXML path, and whether a gbXML was written.
        /// </summary>
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

        [Fact]
        public void Run_Direct_WithTAS_UsesDirectRoute_AndWritesNoGbXML()
        {
            (WorkflowSettings settings, string refusal, bool xml) = Run(new PartOSimulationContext(directory, "Flat1", null, SolarCalculationMethod.TAS, 1, 365) { DirectT3D = true });

            Assert.Null(refusal);
            Assert.NotNull(settings);
            Assert.Equal(T3DRoute.Direct, settings.T3DRoute);
            Assert.Null(settings.Path_gbXML);
            Assert.False(xml);
        }

        [Fact]
        public void Run_Default_WithTAS_IsGbXML_AndWritesTheGbXML()
        {
            (WorkflowSettings settings, string refusal, bool xml) = Run(new PartOSimulationContext(directory, "Flat1", null, SolarCalculationMethod.TAS, 1, 365));

            Assert.Null(refusal);
            Assert.NotNull(settings);
            Assert.Equal(T3DRoute.GbXML, settings.T3DRoute);
            Assert.Equal(Path.Combine(directory, "Flat1.xml"), settings.Path_gbXML);
            Assert.True(xml);
        }

        /// <summary>
        /// Direct is not applied to a method that does not convert the geometry into a T3D.
        /// </summary>
        [Theory]
        [InlineData(SolarCalculationMethod.SAM)]
        [InlineData(SolarCalculationMethod.None)]
        public void Run_Direct_WithoutTAS_StaysGbXML(SolarCalculationMethod solarCalculationMethod)
        {
            (WorkflowSettings settings, string refusal, bool xml) = Run(new PartOSimulationContext(directory, "Flat1", null, solarCalculationMethod, 1, 365) { DirectT3D = true });

            Assert.Null(refusal);
            Assert.NotNull(settings);
            Assert.Equal(T3DRoute.GbXML, settings.T3DRoute);
            Assert.False(xml);
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

        private static CheckBox Box(SimulateControl control)
        {
            return typeof(SimulateControl)
                .GetField("checkBox_DirectT3D", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(control) as CheckBox;
        }
    }
}
