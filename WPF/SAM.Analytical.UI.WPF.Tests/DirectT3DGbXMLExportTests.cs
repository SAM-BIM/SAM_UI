// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using System;
using System.IO;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// The multiple-case TAS workflow wrapper exports a gbXML before invoking SAM_Tas. That export belongs to
    /// the <see cref="T3DRoute.GbXML"/> route only: <see cref="T3DRoute.Direct"/> builds the T3D from the SAM
    /// geometry and never reads it, so on Direct nothing may be written and no export failure may skip a case.
    /// </summary>
    public class DirectT3DGbXMLExportTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "SAM_DirectT3D_" + Guid.NewGuid().ToString("N"));

        public DirectT3DGbXMLExportTests()
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

        private static AnalyticalModel Model()
        {
            return new AnalyticalModel("Empty", null, null, null, new AdjacencyCluster(), Helpers.GlazingFixture.ModelMaterials(), new ProfileLibrary("Profiles"));
        }

        [Fact]
        public void Direct_WritesNoGbXML_AndReportsNoPath()
        {
            string path = Path.Combine(directory, "direct.gbXML");

            bool result = Modify.TryExportGbXML(Model(), new WorkflowSettings() { T3DRoute = T3DRoute.Direct }, path, out string path_gbXML);

            Assert.True(result);
            Assert.Null(path_gbXML);
            Assert.False(File.Exists(path));
        }

        /// <summary>
        /// An export that could not have been made (no model to convert) cannot fail or skip a Direct case.
        /// </summary>
        [Fact]
        public void Direct_DoesNotDependOnTheExportSucceeding()
        {
            string path = Path.Combine(directory, "direct_null.gbXML");

            bool result = Modify.TryExportGbXML(null, new WorkflowSettings() { T3DRoute = T3DRoute.Direct }, path, out string path_gbXML);

            Assert.True(result);
            Assert.Null(path_gbXML);
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void GbXML_WritesTheFile_AndReportsItsPath()
        {
            string path = Path.Combine(directory, "gbxml.gbXML");

            bool result = Modify.TryExportGbXML(Model(), new WorkflowSettings() { T3DRoute = T3DRoute.GbXML }, path, out string path_gbXML);

            Assert.True(result);
            Assert.Equal(path, path_gbXML);
            Assert.True(File.Exists(path));
        }

        /// <summary>
        /// Default settings, and no settings at all, are the established gbXML route: the export is still made.
        /// </summary>
        [Fact]
        public void DefaultSettings_AreGbXML_AndStillExport()
        {
            foreach (WorkflowSettings workflowSettings in new[] { new WorkflowSettings(), Query.DefaultWorkflowSettings(), null })
            {
                string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".gbXML");

                bool result = Modify.TryExportGbXML(Model(), workflowSettings, path, out string path_gbXML);

                Assert.True(result);
                Assert.Equal(path, path_gbXML);
                Assert.True(File.Exists(path));
            }
        }

        /// <summary>
        /// The gbXML route keeps its existing failure behaviour: no export, no case.
        /// </summary>
        [Fact]
        public void GbXML_WithoutAConvertibleModel_ReportsFailureAndWritesNothing()
        {
            string path = Path.Combine(directory, "gbxml_null.gbXML");

            bool result = Modify.TryExportGbXML(null, new WorkflowSettings() { T3DRoute = T3DRoute.GbXML }, path, out string path_gbXML);

            Assert.False(result);
            Assert.Null(path_gbXML);
            Assert.False(File.Exists(path));
        }
    }
}
