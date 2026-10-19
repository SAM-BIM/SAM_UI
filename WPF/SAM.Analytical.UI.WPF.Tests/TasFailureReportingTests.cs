// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// S01b: a TAS refusal reported by SAM_Tas (S01a: <see cref="SimulationEvidence"/>, <c>TAS-SIM-001</c> diagnostics, the B1a failure
    /// document of benchmark-tas) reaches what SAM_UI shows - Energy Simulation and Run Simulation &gt; Tas (one shared core) and
    /// Run Simulation &gt; Both. No TAS here: the evidence object is SAM_Tas' own, driven over real files as S01a's tests drive it, and
    /// the producers are faked as in <see cref="EngineComparisonTests"/>. The real engines are the licensed check in the PR record.
    /// </summary>
    public class TasFailureReportingTests : IDisposable
    {
        private readonly string directory;

        public TasFailureReportingTests()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM-UI-S01b-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string Resource(string name, [CallerFilePath] string sourceFilePath = null)
        {
            return Path.Combine(Path.GetDirectoryName(sourceFilePath), "Resources", "EngineComparison", name);
        }

        /// <summary>
        /// A refused run as S01a measured it: TAS returns 0, leaves a 22-byte stub TSD and writes <c>&lt;name&gt;_error_log.txt</c>. Returns
        /// the evidence and the Error diagnostics <c>WorkflowCalculator</c> writes for it (one <c>TAS-SIM-001</c> per refusal).
        /// </summary>
        private (SimulationEvidence SimulationEvidence, List<Diagnostic> Diagnostics) Refused(string errorLog = "Simulation Failed")
        {
            string path_TBD = Path.Combine(directory, "box.tbd");
            string path_TSD = Path.Combine(directory, "box.tsd");
            File.WriteAllBytes(path_TBD, new byte[4096]);

            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TSD, new byte[22]);
            File.WriteAllText(simulationEvidence.Path_ErrorLog, errorLog);
            simulationEvidence.RecordCallReturned((object)0);
            simulationEvidence.Conclude();

            Assert.False(simulationEvidence.Completed);

            List<Diagnostic> diagnostics = simulationEvidence.Refusals
                .Select((x, i) => new Diagnostic(DiagnosticSeverity.Error, DiagnosticDetail.Summary, "Simulating Model", x, TasDiagnosticCodes.SimulationRefusal, i))
                .ToList();

            return (simulationEvidence, diagnostics);
        }

        // ---- Energy Simulation / Run Simulation > Tas: what the workflow reported -------------------------------------------------

        [Fact]
        public void Failure_OfARefusedRun_NamesTheReturnCode_TheErrorLog_AndEveryTasSim001()
        {
            (SimulationEvidence simulationEvidence, List<Diagnostic> diagnostics) = Refused();

            string failure = Query.TasWorkflowFailure(diagnostics, simulationEvidence);

            string[] lines = failure.Split('\n');
            Assert.Equal("TAS return code: 0", lines[0]);
            Assert.Equal("TAS error log: " + simulationEvidence.Path_ErrorLog, lines[1]);
            //One entry per refusal; the error-log refusal carries the log's own text on a line of its own.
            Assert.Equal(3, diagnostics.Count);
            Assert.Equal(diagnostics.Count, lines.Count(x => x.StartsWith("Error: Simulating Model: ", StringComparison.Ordinal)));
            Assert.Equal(diagnostics.Count, lines.Count(x => x.EndsWith("(TAS-SIM-001)", StringComparison.Ordinal)));
            Assert.Contains(lines, x => x.Contains("TAS refused the simulation: TBDDocument.simulate returned 0."));
            Assert.Contains(lines, x => x.Contains("Simulation Failed"));
        }

        [Fact]
        public void Failure_ListsErrorsOnly_AndIsNullWhenNothingWasReported()
        {
            List<Diagnostic> notErrors =
            [
                new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Summary, "Workflow", "Converted.", TasDiagnosticCodes.WorkflowNote),
                new Diagnostic(DiagnosticSeverity.Warning, DiagnosticDetail.Summary, "Workflow", "Skipped one aperture.", TasDiagnosticCodes.WorkflowIssue),
            ];

            Assert.Null(Query.TasWorkflowFailure(null, null));
            Assert.Null(Query.TasWorkflowFailure([], null));
            Assert.Null(Query.TasWorkflowFailure(notErrors, null));

            string failure = Query.TasWorkflowFailure([.. notErrors, new Diagnostic(DiagnosticSeverity.Error, DiagnosticDetail.Summary, "Adding Results", "No zone results.", TasDiagnosticCodes.SimulationRefusal)], null);
            Assert.Equal("Error: Adding Results: No zone results. (TAS-SIM-001)", failure);
        }

        [Fact]
        public void Failure_NamesTheErrorLogOnlyWhenItIsThere_AndReadsNothingElseFromTheFolder()
        {
            (SimulationEvidence simulationEvidence, List<Diagnostic> diagnostics) = Refused();
            File.Delete(simulationEvidence.Path_ErrorLog);

            //An earlier run's results beside this one change nothing: the text is what the workflow reported.
            File.WriteAllBytes(Path.Combine(directory, "box.tsd"), new byte[1024 * 1024]);
            File.WriteAllText(Path.Combine(directory, "box.json"), "{}");

            string failure = Query.TasWorkflowFailure(diagnostics, simulationEvidence);

            Assert.StartsWith("TAS return code: 0\n", failure);
            Assert.DoesNotContain("\nTAS error log: ", "\n" + failure);
            Assert.Contains("(TAS-SIM-001)", failure);
        }

        [Fact]
        public void Failure_CountsTheErrorsItDoesNotList()
        {
            List<Diagnostic> errors = Enumerable.Range(1, Query.TasWorkflowFailureErrorsShown + 2)
                .Select(x => new Diagnostic(DiagnosticSeverity.Error, DiagnosticDetail.Summary, "Workflow", "Refusal " + x + ".", TasDiagnosticCodes.SimulationRefusal))
                .ToList();

            string[] lines = Query.TasWorkflowFailure(errors, null).Split('\n');

            Assert.Equal(Query.TasWorkflowFailureErrorsShown + 1, lines.Length);
            Assert.Equal("... and 2 more error(s).", lines[^1]);
        }

        [Fact]
        public void FailedMessage_AlwaysOpensWithSimulationFailed_AndNeverClaimsSuccess()
        {
            (SimulationEvidence simulationEvidence, List<Diagnostic> diagnostics) = Refused();
            string failure = Query.TasWorkflowFailure(diagnostics, simulationEvidence);

            foreach (bool exportsRequested in new[] { false, true })
            {
                foreach (bool exportsWritten in new[] { false, true })
                {
                    foreach (bool tpdRequested in new[] { false, true })
                    {
                        foreach (string detail in new[] { failure, null })
                        {
                            string message = Query.TasSimulationFailedMessage(detail, exportsRequested, exportsWritten, tpdRequested, true);

                            Assert.StartsWith("Simulation failed", message);
                            Assert.DoesNotContain("successfuly converted", message);
                            Assert.Contains(detail ?? Query.TasSimulationFailedNoDetail, message);
                            Assert.Equal(tpdRequested, message.Contains("No TPD was created"));
                            Assert.Equal(exportsRequested, message.Contains("SAP/TM59/Part L exports"));
                            Assert.Contains("The open model was not changed.", message);
                        }
                    }
                }
            }

            Assert.DoesNotContain("open model", Query.TasSimulationFailedMessage(failure, false, false, false, false));
        }

        // ---- Run Simulation > Both: the failed producer's own document --------------------------------------------------------------

        private const string TasSim001 = @"TAS-SIM-001: TAS refused the simulation: TBDDocument.simulate returned 0. TAS return code: 0; TAS error log: C:\run\tas\box_error_log.txt.";

        /// <summary>The B1a document benchmark-tas writes for a refused run (S01a A9): state Failure, the TAS-SIM-001 errors as warnings, one note.</summary>
        private string FailureDocument(string path, params string[] warnings)
        {
            JsonNode document = JsonNode.Parse(File.ReadAllText(Resource("scenario-benchmark-TAS.json")));
            document["provenance"]["state"] = "Failure";
            JsonArray jsonArray = new JsonArray();
            foreach (string warning in warnings)
            {
                jsonArray.Add(JsonValue.Create(warning));
            }

            document["provenance"]["warnings"] = jsonArray;
            document["provenance"]["notes"] = new JsonArray(JsonValue.Create("The TAS workflow refused the run (see warnings: the simulation failure, with the TAS return code and error log where TAS gave one); measurements are unavailable."));
            File.WriteAllText(path, document.ToJsonString());
            return path;
        }

        private EngineComparisonRun Run(Func<EngineComparisonProducerRun, int> producer)
        {
            string epwPath = Path.Combine(directory, "test.epw");
            File.WriteAllText(epwPath, "LOCATION,Test,,,TMY,000000,0,0,0,0\n");
            string producers = Path.Combine(directory, "producers");
            Directory.CreateDirectory(producers);
            string tas = Path.Combine(producers, "benchmark-tas.exe");
            string openStudio = Path.Combine(producers, "benchmark-openstudio.exe");
            File.WriteAllText(tas, string.Empty);
            File.WriteAllText(openStudio, string.Empty);

            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), "S01b model");
            Assert.True(analyticalModel.AddSpace(new Space("Room 1", new Geometry.Spatial.Point3D(1, 1, 1))));

            return Modify.RunEngineComparison(analyticalModel, directory, epwPath, tas, openStudio, null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) => producer(producerRun));
        }

        private static string Out(EngineComparisonProducerRun producerRun)
        {
            int index = producerRun.Arguments.IndexOf("--out");
            return producerRun.Arguments[index + 1];
        }

        [Fact]
        public void Both_WithTasRefused_IsNotCompared_KeepsOpenStudio_AndShowsTheTasSim001Detail()
        {
            EngineComparisonRun run = Run(producerRun =>
            {
                if (producerRun.Engine == ComparisonEngine.Tas)
                {
                    FailureDocument(Out(producerRun), TasSim001);
                    return 5;
                }

                File.Copy(Resource("scenario-benchmark-OpenStudio.json"), Out(producerRun));
                return 0;
            });

            //Never Compared, and the successful OpenStudio leg is kept as it was.
            Assert.Equal(EngineComparisonRunStatus.PartiallyCompleted, run.Status);
            Assert.Null(run.Gate);
            Assert.Empty(run.ReportPaths);
            Assert.False(run.ProducerRuns[0].Succeeded);
            Assert.Contains("exited with code 5", run.ProducerRuns[0].Error);
            Assert.True(run.ProducerRuns[1].Succeeded);
            Assert.Equal(File.ReadAllText(Resource("scenario-benchmark-OpenStudio.json")), File.ReadAllText(run.ProducerRuns[1].BenchmarkPath));

            //The refusal detail - return code and error log - as the producer wrote it.
            string line = "Tas warning: " + TasSim001;
            Assert.Contains(line, run.Diagnostics);
            Assert.Contains(run.Diagnostics, x => x.StartsWith("Tas note: The TAS workflow refused the run", StringComparison.Ordinal));
            Assert.Contains(line, Query.EngineComparisonRunLines(run));

            //What the window shows for a partial run: the OpenStudio results' diagnostics followed by the run's lines.
            EngineResult engineResult = Query.LoadEngineResult(ComparisonEngine.OpenStudio, run.ProducerRuns[1].BenchmarkPath, ComparisonEngine.Tas);
            Assert.Contains(line, Query.EngineResultDiagnostics(engineResult, run));

            //And the run record.
            JsonNode manifest = JsonNode.Parse(File.ReadAllText(run.ManifestPath));
            Assert.Equal("PartiallyCompleted", (string)manifest["status"]);
            Assert.Contains(line, manifest["diagnostics"].AsArray().Select(x => (string)x));
        }

        [Fact]
        public void Both_WithBothFailed_IsFailed_AndStillSaysWhyTasFailed()
        {
            EngineComparisonRun run = Run(producerRun =>
            {
                if (producerRun.Engine == ComparisonEngine.Tas)
                {
                    FailureDocument(Out(producerRun), TasSim001);
                }

                return 5;
            });

            Assert.Equal(EngineComparisonRunStatus.Failed, run.Status);
            Assert.Contains("Tas warning: " + TasSim001, run.Diagnostics);
            Assert.DoesNotContain(run.Diagnostics, x => x.StartsWith("OpenStudio ", StringComparison.Ordinal));
        }

        [Fact]
        public void Both_Succeeded_AddsNoProducerLines()
        {
            EngineComparisonRun run = Run(producerRun =>
            {
                File.Copy(Resource(producerRun.Engine == ComparisonEngine.Tas ? "scenario-benchmark-TAS.json" : "scenario-benchmark-OpenStudio.json"), Out(producerRun));
                return 0;
            });

            Assert.Equal(EngineComparisonRunStatus.Compared, run.Status);
            Assert.Empty(run.Diagnostics);
        }

        [Fact]
        public void FailureLines_OfAnUnreadableDocument_SayItCouldNotBeRead_AndAnEmptyOneSaysNothing()
        {
            string unreadable = Path.Combine(directory, "unreadable.json");
            File.WriteAllText(unreadable, "{ not json");
            string empty = FailureDocument(Path.Combine(directory, "empty.json"));
            JsonNode document = JsonNode.Parse(File.ReadAllText(empty));
            document["provenance"]["notes"] = new JsonArray();
            File.WriteAllText(empty, document.ToJsonString());

            string line = Assert.Single(Query.BenchmarkFailureLines(ComparisonEngine.Tas, unreadable));
            Assert.StartsWith("Tas: the failed producer's document could not be read", line);
            Assert.Empty(Query.BenchmarkFailureLines(ComparisonEngine.Tas, empty));
        }
    }
}
