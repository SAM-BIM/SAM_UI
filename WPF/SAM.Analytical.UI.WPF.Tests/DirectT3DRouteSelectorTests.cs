// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The "Direct T3D" check box on the Case Simulation and Multiple Case Simulation panels only configures
    /// <see cref="WorkflowSettings.T3DRoute"/>: unchecked is <see cref="T3DRoute.GbXML"/> (the default and the
    /// established route), checked is <see cref="T3DRoute.Direct"/>. Nothing here generates a T3D; the route
    /// itself lives in SAM_Tas. What has to hold is that <see cref="T3DRoute.Direct"/> is only ever reached by
    /// an explicit check - never by a new panel, legacy settings, or a missing/garbage stored value.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class DirectT3DRouteSelectorTests
    {
        [WpfFact]
        public void CaseSimulationControl_NewPanel_IsUncheckedAndGbXML()
        {
            CaseSimulationControl control = new();

            Assert.False(Box(control).IsChecked);
            Assert.Equal(T3DRoute.GbXML, control.WorkflowSettings.T3DRoute);
        }

        [WpfFact]
        public void MultipleCaseSimulationControl_NewPanel_IsUncheckedAndGbXML()
        {
            MultipleCaseSimulationControl control = new();

            Assert.False(Box(control).IsChecked);
            Assert.Equal(T3DRoute.GbXML, control.WorkflowSettings.T3DRoute);
        }

        [Fact]
        public void DefaultWorkflowSettings_AreGbXML()
        {
            Assert.Equal(T3DRoute.GbXML, Query.DefaultWorkflowSettings().T3DRoute);
            Assert.Equal(T3DRoute.GbXML, new WorkflowSettings().T3DRoute);
        }

        [WpfFact]
        public void CaseSimulationControl_CheckAndUncheck_TogglesRouteOnly()
        {
            CaseSimulationControl control = new();
            bool sizing = control.WorkflowSettings.Sizing;
            bool simulate = control.WorkflowSettings.Simulate;
            bool removeTBD = control.WorkflowSettings.RemoveExistingTBD;

            Box(control).IsChecked = true;
            Assert.Equal(T3DRoute.Direct, control.WorkflowSettings.T3DRoute);

            Box(control).IsChecked = false;
            Assert.Equal(T3DRoute.GbXML, control.WorkflowSettings.T3DRoute);

            Assert.Equal(sizing, control.WorkflowSettings.Sizing);
            Assert.Equal(simulate, control.WorkflowSettings.Simulate);
            Assert.Equal(removeTBD, control.WorkflowSettings.RemoveExistingTBD);
        }

        [WpfFact]
        public void MultipleCaseSimulationControl_CheckAndUncheck_TogglesRouteOnly()
        {
            MultipleCaseSimulationControl control = new();
            bool sizing = control.WorkflowSettings.Sizing;
            bool simulate = control.WorkflowSettings.Simulate;
            bool removeTBD = control.WorkflowSettings.RemoveExistingTBD;

            Box(control).IsChecked = true;
            Assert.Equal(T3DRoute.Direct, control.WorkflowSettings.T3DRoute);

            Box(control).IsChecked = false;
            Assert.Equal(T3DRoute.GbXML, control.WorkflowSettings.T3DRoute);

            Assert.Equal(sizing, control.WorkflowSettings.Sizing);
            Assert.Equal(simulate, control.WorkflowSettings.Simulate);
            Assert.Equal(removeTBD, control.WorkflowSettings.RemoveExistingTBD);
        }

        /// <summary>
        /// An indeterminate (null) box is not a choice of Direct.
        /// </summary>
        [WpfFact]
        public void IndeterminateBox_IsGbXML()
        {
            CaseSimulationControl control = new();
            Box(control).IsThreeState = true;
            Box(control).IsChecked = null;

            Assert.Equal(T3DRoute.GbXML, control.WorkflowSettings.T3DRoute);
        }

        [WpfFact]
        public void SettingTheSettings_ShowsTheRouteInTheBox_AndBackAgain()
        {
            CaseSimulationControl single = new();
            MultipleCaseSimulationControl multiple = new();

            foreach (T3DRoute route in new[] { T3DRoute.Direct, T3DRoute.GbXML })
            {
                bool expected = route == T3DRoute.Direct;

                single.WorkflowSettings = new WorkflowSettings() { T3DRoute = route };
                multiple.WorkflowSettings = new WorkflowSettings() { T3DRoute = route };

                Assert.Equal(expected, Box(single).IsChecked);
                Assert.Equal(expected, Box(multiple).IsChecked);
                Assert.Equal(route, single.WorkflowSettings.T3DRoute);
                Assert.Equal(route, multiple.WorkflowSettings.T3DRoute);
            }
        }

        /// <summary>
        /// Settings serialized before <c>T3DRoute</c> existed (no key), or carrying a null / unrecognised
        /// value, load as GbXML and show an unchecked box - even on a panel that previously showed Direct.
        /// </summary>
        [WpfTheory]
        [InlineData(null)]
        [InlineData("null")]
        [InlineData("\"GbXML\"")]
        [InlineData("\"\"")]
        [InlineData("\"Banana\"")]
        [InlineData("0")]
        [InlineData("7")]
        [InlineData("true")]
        public void LegacyOrUnrecognisedStoredValue_LoadsAsGbXML_AndUnchecked(string stored)
        {
            JsonObject jObject = new WorkflowSettings() { T3DRoute = T3DRoute.Direct }.ToJsonObject();
            jObject.Remove("T3DRoute");
            if (stored != null)
            {
                jObject.Add("T3DRoute", JsonNode.Parse(stored));
            }

            WorkflowSettings loaded = new(jObject);
            Assert.Equal(T3DRoute.GbXML, loaded.T3DRoute);

            CaseSimulationControl single = new() { WorkflowSettings = new WorkflowSettings() { T3DRoute = T3DRoute.Direct } };
            single.WorkflowSettings = loaded;
            Assert.False(Box(single).IsChecked);
            Assert.Equal(T3DRoute.GbXML, single.WorkflowSettings.T3DRoute);

            MultipleCaseSimulationControl multiple = new() { WorkflowSettings = new WorkflowSettings() { T3DRoute = T3DRoute.Direct } };
            multiple.WorkflowSettings = loaded;
            Assert.False(Box(multiple).IsChecked);
            Assert.Equal(T3DRoute.GbXML, multiple.WorkflowSettings.T3DRoute);
        }

        /// <summary>
        /// An explicit Direct survives a save/load of the settings and reopens as a checked box.
        /// </summary>
        [WpfFact]
        public void DirectSurvivesSerializationRoundTrip_AndReopensChecked()
        {
            CaseSimulationControl control = new();
            Box(control).IsChecked = true;

            WorkflowSettings reloaded = new(control.WorkflowSettings.ToJsonObject());
            Assert.Equal(T3DRoute.Direct, reloaded.T3DRoute);

            CaseSimulationControl reopened = new() { WorkflowSettings = reloaded };
            Assert.True(Box(reopened).IsChecked);
        }

        /// <summary>
        /// RunWorkflow copies the panel's settings per model (<c>new(workflowSettings)</c>); the route must
        /// travel with that copy.
        /// </summary>
        [Fact]
        public void CopyOfSettings_KeepsTheRoute()
        {
            Assert.Equal(T3DRoute.Direct, new WorkflowSettings(new WorkflowSettings() { T3DRoute = T3DRoute.Direct }).T3DRoute);
            Assert.Equal(T3DRoute.GbXML, new WorkflowSettings(Query.DefaultWorkflowSettings()).T3DRoute);
        }

        private static CheckBox Box(object control)
        {
            return control.GetType()
                .GetField("checkBox_DirectT3D", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(control) as CheckBox;
        }
    }
}
