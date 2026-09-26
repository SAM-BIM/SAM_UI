// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Reporting;
using SAM.Core.Reporting.Pdf;
using SAM.Units;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// The Space Assumptions PDF command: resolves the one selected Space, asks where to save, writes the PDF
        /// and offers to open it. Every calculation, unit, placeholder and layout decision belongs to
        /// SAM.Analytical.Reporting and SAM.Core.Reporting.Pdf; this is orchestration only.
        /// <para>
        /// Units: SI (the reporting default, air flow in l/s). SAM_UI has no unit-system preference to follow, and
        /// Phase 1 adds none; <see cref="WriteSpaceAssumptionsPdf"/> takes the unit system for callers that have one.
        /// </para>
        /// </summary>
        /// <returns>The result, or null when nothing was attempted (no model, a refused selection, or Save cancelled).</returns>
        public static SpaceAssumptionsPdfResult? CreateSpaceAssumptionsPdf(this UIAnalyticalModel? uIAnalyticalModel, IEnumerable<Space>? spaces, System.Windows.Window? owner = null)
        {
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;

            Space? space = Query.SpaceAssumptionsPdfSpace(analyticalModel, spaces, out string? refusal);
            if (space == null || analyticalModel == null || uIAnalyticalModel == null)
            {
                ShowSpaceAssumptionsPdfMessage(owner, refusal ?? "Select one Space.", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Title = "Save Space Assumptions PDF",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = Query.SpaceAssumptionsPdfFileName(space),
            };

            string? directory = null;
            try
            {
                directory = string.IsNullOrWhiteSpace(uIAnalyticalModel.Path) ? null : Path.GetDirectoryName(uIAnalyticalModel.Path);
            }
            catch (ArgumentException)
            {
            }

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                saveFileDialog.InitialDirectory = directory;
            }

            bool? dialogResult = owner == null ? saveFileDialog.ShowDialog() : saveFileDialog.ShowDialog(owner);
            if (dialogResult != true || string.IsNullOrWhiteSpace(saveFileDialog.FileName))
            {
                return null;
            }

            SpaceAssumptionsPdfResult result;

            Cursor? cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                result = WriteSpaceAssumptionsPdf(analyticalModel, space, saveFileDialog.FileName);
            }
            finally
            {
                Mouse.OverrideCursor = cursor;
            }

            if (!result.Succeeded)
            {
                System.Diagnostics.Trace.TraceError("Space Assumptions PDF ({0}) failed for '{1}': {2}", result.Failure, result.Path, result.Exception);

                ShowSpaceAssumptionsPdfMessage(owner, result.Message ?? "The Space Assumptions PDF could not be created.", MessageBoxButton.OK, MessageBoxImage.Error);
                return result;
            }

            MessageBoxResult messageBoxResult = ShowSpaceAssumptionsPdfMessage(owner, string.Format("Space Assumptions PDF saved:\n{0}\n\nOpen it now?", result.Path), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.Path!) { UseShellExecute = true });
                }
                catch (Exception exception)
                {
                    ShowSpaceAssumptionsPdfMessage(owner, string.Format("The PDF was saved, but it could not be opened: {0}", exception.Message), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            return result;
        }

        /// <summary>
        /// Builds the Space Assumptions document for <paramref name="space"/> with the SAM reporting API, renders
        /// it and writes it to <paramref name="path"/>. Missing engineering data is not a failure: the document
        /// prints it. A software failure is returned, with its exception, as a failed result naming the stage.
        /// <para>
        /// The PDF is rendered in memory and staged beside the destination before it replaces it, so a failure
        /// never leaves a partial file and never destroys a PDF already at the path.
        /// </para>
        /// </summary>
        /// <param name="unitStyle">Passed to the reporting framework unchanged; SI is its default.</param>
        /// <param name="documentRenderer">The renderer; null means <see cref="PdfRenderer"/>. Tests pass a stand-in.</param>
        public static SpaceAssumptionsPdfResult WriteSpaceAssumptionsPdf(AnalyticalModel analyticalModel, Space space, string? path, UnitStyle unitStyle = UnitStyle.SI, IDocumentRenderer? documentRenderer = null)
        {
            if (analyticalModel == null)
            {
                throw new ArgumentNullException(nameof(analyticalModel));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return SpaceAssumptionsPdfResult.Failed(path, SpaceAssumptionsPdfFailure.Output, "No file was chosen for the PDF.", null);
            }

            Document document;
            List<string> notes;
            try
            {
                Analytical.Reporting.DocumentContext documentContext = Analytical.Reporting.Create.DocumentContext(analyticalModel, new DocumentOptions() { UnitSystem = unitStyle });

                document = Analytical.Reporting.Create.SpaceAssumptions(documentContext, space);

                notes = documentContext.Diagnostics.Select(x => x.Text).ToList();
            }
            catch (Exception exception)
            {
                return SpaceAssumptionsPdfResult.Failed(path, SpaceAssumptionsPdfFailure.Document, string.Format("The Space Assumptions report could not be built for this Space: {0}", exception.Message), exception);
            }

            byte[] bytes;
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    (documentRenderer ?? new PdfRenderer()).Render(document, memoryStream);
                    bytes = memoryStream.ToArray();
                }

                if (bytes.Length == 0)
                {
                    throw new InvalidOperationException("The renderer produced an empty PDF.");
                }
            }
            catch (Exception exception)
            {
                return SpaceAssumptionsPdfResult.Failed(path, SpaceAssumptionsPdfFailure.Rendering, string.Format("The PDF could not be rendered: {0}", exception.Message), exception);
            }

            string? path_Temp = null;
            try
            {
                path = Path.GetFullPath(path);
                path_Temp = path + ".tmp";

                File.WriteAllBytes(path_Temp, bytes);
                File.Move(path_Temp, path, true);
            }
            catch (Exception exception)
            {
                DeleteSpaceAssumptionsPdfTemp(path_Temp);

                return SpaceAssumptionsPdfResult.Failed(path, SpaceAssumptionsPdfFailure.Output, string.Format("The PDF could not be saved to '{0}': {1}\n\nChoose another folder, or close the file if it is open in a PDF viewer.", path, exception.Message), exception);
            }

            return SpaceAssumptionsPdfResult.Created(path, bytes.LongLength, notes);
        }

        private static MessageBoxResult ShowSpaceAssumptionsPdfMessage(System.Windows.Window? owner, string text, MessageBoxButton messageBoxButton, MessageBoxImage messageBoxImage)
        {
            return owner == null
                ? MessageBox.Show(text, Query.SpaceAssumptionsPdfTitle, messageBoxButton, messageBoxImage)
                : MessageBox.Show(owner, text, Query.SpaceAssumptionsPdfTitle, messageBoxButton, messageBoxImage);
        }

        private static void DeleteSpaceAssumptionsPdfTemp(string? path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                //Best effort: a staged file left behind is housekeeping, not a wrong PDF.
            }
        }
    }
}
