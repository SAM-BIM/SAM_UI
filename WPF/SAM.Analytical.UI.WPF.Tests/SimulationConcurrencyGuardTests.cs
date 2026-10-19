// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI.WPF.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// S02: one simulation at a time for an open model. Every simulation entry point - Energy Simulation, Run Simulation &gt; Tas,
    /// OpenStudio and Both, the OpenStudio Simulation button and the comparison window's Run - takes <see cref="SimulationGuard"/>
    /// before it starts anything. The simulations are fakes (the windows' S02 seams); no Tas, OpenStudio or licence is needed.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class SimulationConcurrencyGuardTests
    {
        // ---- the guard -----------------------------------------------------------------------------------

        [Fact]
        public void A_second_acquire_for_the_same_model_is_refused_and_names_what_runs()
        {
            UIAnalyticalModel uIAnalyticalModel = new UIAnalyticalModel();

            using SimulationGuard? first = SimulationGuard.TryAcquire(uIAnalyticalModel, SimulationGuard.OpenStudio, out string? running_First);
            Assert.NotNull(first);
            Assert.Null(running_First);

            SimulationGuard? second = SimulationGuard.TryAcquire(uIAnalyticalModel, SimulationGuard.Tas, out string? running_Second);
            Assert.Null(second);
            Assert.Equal(SimulationGuard.OpenStudio, running_Second);
            Assert.Equal(SimulationGuard.OpenStudio, SimulationGuard.Running(uIAnalyticalModel));
            Assert.StartsWith("An OpenStudio simulation is already running for this model, so nothing was started.", SimulationGuard.RefusalMessage(running_Second!));
        }

        [Fact]
        public void Dispose_releases_once_and_a_late_second_dispose_cannot_release_the_next_run()
        {
            UIAnalyticalModel uIAnalyticalModel = new UIAnalyticalModel();

            SimulationGuard first = SimulationGuard.TryAcquire(uIAnalyticalModel, SimulationGuard.Tas, out _)!;
            first.Dispose();
            Assert.Null(SimulationGuard.Running(uIAnalyticalModel));

            using SimulationGuard? next = SimulationGuard.TryAcquire(uIAnalyticalModel, SimulationGuard.Both, out _);
            Assert.NotNull(next);

            first.Dispose();
            Assert.Equal(SimulationGuard.Both, SimulationGuard.Running(uIAnalyticalModel));
        }

        [Fact]
        public void Another_open_model_is_not_blocked()
        {
            UIAnalyticalModel uIAnalyticalModel = new UIAnalyticalModel();
            UIAnalyticalModel uIAnalyticalModel_Other = new UIAnalyticalModel();

            using SimulationGuard? first = SimulationGuard.TryAcquire(uIAnalyticalModel, SimulationGuard.Both, out _);
            using SimulationGuard? other = SimulationGuard.TryAcquire(uIAnalyticalModel_Other, SimulationGuard.Both, out _);

            Assert.NotNull(first);
            Assert.NotNull(other);
        }

        // ---- the entry points --------------------------------------------------------------------------

        public enum EntryPoint
        {
            EnergySimulation,
            RunSimulationTas,
            RunSimulationOpenStudio,
            RunSimulationBoth,
            OpenStudioSimulation,
            ComparisonWindowRun,
        }

        private static readonly EntryPoint[] entryPoints = Enum.GetValues<EntryPoint>();

        [WpfFact]
        public async Task While_any_simulation_runs_every_entry_point_is_refused_and_starts_nothing()
        {
            //Each asynchronous first run, held open, against every entry point.
            foreach (EntryPoint first in new[] { EntryPoint.OpenStudioSimulation, EntryPoint.RunSimulationOpenStudio, EntryPoint.RunSimulationBoth, EntryPoint.ComparisonWindowRun })
            {
                using Harness harness = new Harness();
                TaskCompletionSource gate = new TaskCompletionSource();
                harness.Gate = gate.Task;

                Task task_First = harness.Start(first);
                Assert.False(task_First.IsCompleted, first + " should still be running");
                string running = SimulationGuard.Running(harness.UIAnalyticalModel)!;
                Calls calls_First = harness.Calls;

                foreach (EntryPoint second in entryPoints)
                {
                    Task task_Second = harness.Start(second);
                    Assert.True(task_Second.IsCompleted, second + " should be refused at once while " + first + " runs");
                    await task_Second;
                }

                //The Run Simulation button says so before its dialog opens.
                harness.ClickRunSimulation();

                Assert.Equal(calls_First, harness.Calls);
                Assert.Equal(entryPoints.Length + 1, harness.Refusals.Count);
                Assert.All(harness.Refusals, x => Assert.StartsWith(char.ToUpperInvariant(running[0]) + running.Substring(1) + " is already running for this model", x));
                Assert.Equal(running, SimulationGuard.Running(harness.UIAnalyticalModel));
                if (running == SimulationGuard.OpenStudio)
                {
                    Assert.False(harness.AnalyticalWindow.RibbonButton_OpenStudioSimulation.IsEnabled);
                }

                gate.SetResult();
                await task_First;

                Assert.Null(SimulationGuard.Running(harness.UIAnalyticalModel));
                Assert.Equal(calls_First, harness.Calls);
            }
        }

        [WpfFact]
        public async Task While_a_Tas_simulation_runs_every_entry_point_is_refused_and_starts_nothing()
        {
            //Tas runs on the UI thread; the second starts are made from inside it, as a re-entered window would make them.
            foreach (EntryPoint first in new[] { EntryPoint.EnergySimulation, EntryPoint.RunSimulationTas })
            {
                using Harness harness = new Harness();
                List<Task> seconds = new List<Task>();
                harness.DuringTas = () =>
                {
                    Assert.Equal(SimulationGuard.Tas, SimulationGuard.Running(harness.UIAnalyticalModel));
                    foreach (EntryPoint second in entryPoints)
                    {
                        seconds.Add(harness.Start(second));
                    }

                    harness.ClickRunSimulation();
                };

                await harness.Start(first);

                Assert.All(seconds, x => Assert.True(x.IsCompletedSuccessfully));
                Assert.Equal(new Calls(1, 0, 0, 0), harness.Calls);
                Assert.Equal(entryPoints.Length + 1, harness.Refusals.Count);
                Assert.All(harness.Refusals, x => Assert.StartsWith("A Tas simulation is already running for this model", x));
                Assert.Null(SimulationGuard.Running(harness.UIAnalyticalModel));
            }
        }

        [WpfFact]
        public async Task The_guard_is_released_on_success_failure_cancellation_and_exception()
        {
            foreach (EntryPoint entryPoint in entryPoints)
            {
                foreach (Outcome outcome in Enum.GetValues<Outcome>())
                {
                    using Harness harness = new Harness();
                    harness.Outcome = outcome;

                    Task task = harness.Start(entryPoint);
                    if (outcome == Outcome.Exception)
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
                    }
                    else if (outcome == Outcome.Cancelled)
                    {
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
                    }
                    else
                    {
                        await task;
                    }

                    Assert.True(SimulationGuard.Running(harness.UIAnalyticalModel) == null, entryPoint + " kept the guard after " + outcome);

                    //The next start is not refused.
                    harness.Outcome = Outcome.Success;
                    await harness.Start(entryPoint);
                    Assert.Empty(harness.Refusals);
                    Assert.Equal(2, harness.Calls.Total);
                }
            }
        }

        [WpfFact]
        public async Task Sequential_runs_through_every_entry_point_all_start()
        {
            using Harness harness = new Harness();

            foreach (EntryPoint entryPoint in entryPoints)
            {
                await harness.Start(entryPoint);
            }

            Assert.Empty(harness.Refusals);
            Assert.Equal(new Calls(2, 2, 1, 1), harness.Calls);
            Assert.True(harness.AnalyticalWindow.RibbonButton_OpenStudioSimulation.IsEnabled);
        }

        [WpfFact]
        public async Task Both_runs_its_own_Tas_and_OpenStudio_producers_inside_its_guard()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SAM-UI-S02-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using Harness harness = new Harness();
                List<ComparisonEngine> producers = new List<ComparisonEngine>();
                EngineComparisonRun? engineComparisonRun = null;

                harness.AnalyticalWindow.BothEnginesSimulation = async () =>
                {
                    Assert.Equal(SimulationGuard.Both, SimulationGuard.Running(harness.UIAnalyticalModel));

                    //The real orchestration, with the producer processes faked as in EngineComparisonTests.
                    engineComparisonRun = await Task.Run(() => Modify.RunEngineComparison(Model(), directory, Epw(directory), ProducerPath(directory, "benchmark-tas.exe"), ProducerPath(directory, "benchmark-openstudio.exe"), null, null, CancellationToken.None, (producerRun, workDirectory, cancellationToken) =>
                    {
                        Assert.Equal(SimulationGuard.Both, SimulationGuard.Running(harness.UIAnalyticalModel));
                        producers.Add(producerRun.Engine);
                        return 5;
                    }));

                    return null;
                };

                await harness.AnalyticalWindow.RunSimulationAsync(SimulationEngineChoice.Both);

                Assert.Equal(new[] { ComparisonEngine.Tas, ComparisonEngine.OpenStudio }, producers);
                Assert.Equal(2, engineComparisonRun!.ProducerRuns.Count);
                Assert.Empty(harness.Refusals);
                Assert.Null(SimulationGuard.Running(harness.UIAnalyticalModel));
            }
            finally
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                Directory.Delete(directory, true);
            }
        }

        // ---- helpers -----------------------------------------------------------------------------------

        public enum Outcome
        {
            Success,
            Failure,
            Cancelled,
            Exception,
        }

        private sealed record Calls(int Tas, int OpenStudio, int Both, int ComparisonRun)
        {
            public int Total => Tas + OpenStudio + Both + ComparisonRun;
        }

        /// <summary>An AnalyticalWindow and a comparison window on the same open model, with every simulation faked.</summary>
        private sealed class Harness : IDisposable
        {
            private int tas;
            private int openStudio;
            private int both;
            private int comparisonRun;

            public Harness()
            {
                AnalyticalWindow = new AnalyticalWindow();
                UIAnalyticalModel = (UIAnalyticalModel)typeof(AnalyticalWindow).GetField("uIAnalyticalModel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(AnalyticalWindow)!;
                EngineComparisonWindow = new EngineComparisonWindow(UIAnalyticalModel);

                AnalyticalWindow.SimulationRefused = Refusals.Add;
                EngineComparisonWindow.SimulationRefused = Refusals.Add;

                AnalyticalWindow.TasSimulation = () =>
                {
                    tas++;
                    DuringTas?.Invoke();
                    Finish();
                };

                AnalyticalWindow.OpenStudioSimulation = async () =>
                {
                    openStudio++;
                    await Wait();
                };

                AnalyticalWindow.BothEnginesSimulation = async () =>
                {
                    both++;
                    await Wait();
                    return null;
                };

                EngineComparisonWindow.BothEnginesSimulation = async () =>
                {
                    comparisonRun++;
                    await Wait();
                    return null;
                };
            }

            public AnalyticalWindow AnalyticalWindow { get; }

            public EngineComparisonWindow EngineComparisonWindow { get; }

            public UIAnalyticalModel UIAnalyticalModel { get; }

            public List<string> Refusals { get; } = new List<string>();

            public Task? Gate { get; set; }

            public Action? DuringTas { get; set; }

            public Outcome Outcome { get; set; }

            public Calls Calls => new Calls(tas, openStudio, both, comparisonRun);

            public Task Start(EntryPoint entryPoint)
            {
                switch (entryPoint)
                {
                    case EntryPoint.EnergySimulation:
                        try
                        {
                            AnalyticalWindow.SimulateTas();
                            return Task.CompletedTask;
                        }
                        catch (Exception exception)
                        {
                            return Task.FromException(exception);
                        }
                    case EntryPoint.RunSimulationTas:
                        return AnalyticalWindow.RunSimulationAsync(SimulationEngineChoice.Tas);
                    case EntryPoint.RunSimulationOpenStudio:
                        return AnalyticalWindow.RunSimulationAsync(SimulationEngineChoice.OpenStudio);
                    case EntryPoint.RunSimulationBoth:
                        return AnalyticalWindow.RunSimulationAsync(SimulationEngineChoice.Both);
                    case EntryPoint.OpenStudioSimulation:
                        return AnalyticalWindow.RunOpenStudioSimulationAsync();
                    case EntryPoint.ComparisonWindowRun:
                        return EngineComparisonWindow.RunAsync();
                    default:
                        throw new ArgumentOutOfRangeException(nameof(entryPoint));
                }
            }

            /// <summary>The Run Simulation ribbon button; with a run going it refuses before its dialog would open.</summary>
            public void ClickRunSimulation()
            {
                typeof(AnalyticalWindow).GetMethod("RibbonButton_RunSimulation_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(AnalyticalWindow, [null, null]);
            }

            private async Task Wait()
            {
                if (Gate != null)
                {
                    await Gate;
                }
                else
                {
                    await Task.Yield();
                }

                Finish();
            }

            private void Finish()
            {
                switch (Outcome)
                {
                    case Outcome.Cancelled:
                        throw new OperationCanceledException();
                    case Outcome.Exception:
                        throw new InvalidOperationException("simulation failed with an exception");
                    default:
                        //Success and a reported failure both return normally; the run's own result says which.
                        return;
                }
            }

            public void Dispose()
            {
                //The comparison window is never shown, so it is not closed: closing it during a run would wait on its message box.
                AnalyticalWindow.Close();
            }
        }

        private static AnalyticalModel Model()
        {
            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), "Test model");
            Assert.True(analyticalModel.AddSpace(new Space("Room 1", new Geometry.Spatial.Point3D(1, 1, 1))));
            return analyticalModel;
        }

        private static string Epw(string directory)
        {
            string path = Path.Combine(directory, "test.epw");
            File.WriteAllText(path, "LOCATION,Test,,,TMY,000000,0,0,0,0\n");
            return path;
        }

        private static string ProducerPath(string directory, string fileName)
        {
            string path = Path.Combine(directory, "producers", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }
    }
}
