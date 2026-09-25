// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Part O UX pass 4: the one progress language every Part O operation shares - Prepare &amp; Run, Review
    /// Results, Iteration 2B and Iteration 3. A percentage appears only from a real count, which no Part O
    /// operation reports today; the stages, the elapsed time and what Cancel does read the same everywhere.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class PartOProgressConsistencyTests
    {
        private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

        //-------------------------------------------------------------------------------------------------
        //Determinate only on a real count
        //-------------------------------------------------------------------------------------------------

        [Fact]
        public void A_percentage_comes_only_from_a_reported_count_and_is_rounded_down()
        {
            PartOProgressState partOProgressState = new(["Read", "Assess"], () => Now);

            //Nothing running: a report has nothing to belong to, so no bar moves.
            partOProgressState.Report(1, 2);
            Assert.Null(partOProgressState.Fraction);

            partOProgressState.Start(0);

            partOProgressState.Report(63, 100);
            Assert.True(partOProgressState.IsDeterminate);
            Assert.Equal("63%", partOProgressState.Percent);

            //Never 100% before all of it.
            partOProgressState.Report(999, 1000);
            Assert.Equal("99%", partOProgressState.Percent);

            partOProgressState.Report(5, 4);
            Assert.Equal("100%", partOProgressState.Percent);

            //No total is no percentage.
            partOProgressState.Report(3, 0);
            Assert.Null(partOProgressState.Percent);

            //A count belongs to its stage: the next stage starts unknown.
            partOProgressState.Report(1, 4);
            partOProgressState.Start(1);
            Assert.False(partOProgressState.IsDeterminate);
            Assert.Null(partOProgressState.Percent);
        }

        [Fact]
        public void Without_a_count_the_progress_is_indeterminate_whatever_else_happens()
        {
            DateTime now = Now;
            PartOProgressState partOProgressState = new(Modify.PartOOptimisationPhases(new PartOOptimisationSettings { CapacityEnvelope = true }), () => now);

            //Everything a TAS-style operation does to the state: stages, rounds, step names, time passing.
            partOProgressState.Start(Modify.PartOOptimisationPhase_Starting);
            now = now.AddSeconds(20);
            partOProgressState.Start(Modify.PartOOptimisationPhase_Rounds);

            for (int round = 1; round <= 3; round++)
            {
                partOProgressState.Activity(string.Format("round {0}", round));
                partOProgressState.Detail = "Simulating Model";
                now = now.AddMinutes(8);

                Assert.Null(partOProgressState.Fraction);
                Assert.Null(partOProgressState.Percent);
            }

            partOProgressState.Start(Modify.PartOOptimisationPhase_CapacityEnvelope);
            partOProgressState.Complete();

            Assert.Null(partOProgressState.Percent);
            Assert.DoesNotContain(partOProgressState.Lines(), x => x.Contains('%'));
        }

        [Fact]
        public void The_iteration_2B_stages_name_no_round_total_and_list_the_envelope_only_when_asked_for()
        {
            List<string> phases = Modify.PartOOptimisationPhases(new PartOOptimisationSettings { CapacityEnvelope = false });
            List<string> phases_Envelope = Modify.PartOOptimisationPhases(new PartOOptimisationSettings { CapacityEnvelope = true });

            Assert.Equal(["Assess the Iteration 2 results (TM59)", "Optimisation rounds"], phases);
            Assert.Equal(3, phases_Envelope.Count);
            Assert.Equal("Capacity envelope (diagnostic)", phases_Envelope[Modify.PartOOptimisationPhase_CapacityEnvelope]);

            //The rounds are one stage: how many there will be is not known until the optimisation stops.
            Assert.Equal("Optimisation rounds", phases[Modify.PartOOptimisationPhase_Rounds]);
            Assert.DoesNotContain(phases_Envelope, x => x.Contains(" of ") || x.Contains('/'));

            //The round limit is the run's own setting, said as a limit rather than a total.
            string subheading = Modify.PartOOptimisationProgressSubheading(new PartOOptimisationSettings { MaximumIterations = 1 });
            Assert.Contains("The limit is 1 round;", subheading);
            Assert.Contains("how many run depends on the results", subheading);
            Assert.Contains("The limit is 4 rounds;", Modify.PartOOptimisationProgressSubheading(new PartOOptimisationSettings { MaximumIterations = 4 }));
        }

        //-------------------------------------------------------------------------------------------------
        //Stages, elapsed time, completion, failure
        //-------------------------------------------------------------------------------------------------

        [Fact]
        public void Each_stage_says_its_status_in_words_as_well_as_by_glyph()
        {
            DateTime now = Now;
            PartOProgressState partOProgressState = new(["Prepare and review the iteration", "TAS simulation (full year)", "TM59 assessment", "Unused"], () => now);

            partOProgressState.Start(1);
            now = now.AddMinutes(7).AddSeconds(48);

            //Stage 0 never ran; stage 1 runs; 2 and 3 are still to come.
            Assert.Equal("Not needed: Prepare and review the iteration", partOProgressState.AccessibleLine(0));
            Assert.Equal("Running now: TAS simulation (full year), 7m 48s", partOProgressState.AccessibleLine(1));
            Assert.Equal("Upcoming: TM59 assessment", partOProgressState.AccessibleLine(2));

            //The window reads its duration column on its own, so its row name leaves the time out.
            Assert.Equal("Running now: TAS simulation (full year)", partOProgressState.AccessibleLine(1, false));

            partOProgressState.Start(2);

            Assert.Equal("Completed: TAS simulation (full year), 7m 48s", partOProgressState.AccessibleLine(1));
            Assert.Equal("✓", PartOProgressState.Glyph(partOProgressState.Status(1)));
            Assert.Equal("●", PartOProgressState.Glyph(partOProgressState.Status(2)));
            Assert.Equal("○", PartOProgressState.Glyph(partOProgressState.Status(3)));
        }

        [Fact]
        public void A_repeating_stage_carries_its_round_and_keeps_it_once_it_ends()
        {
            PartOProgressState partOProgressState = new(Modify.PartOOptimisationPhases(new PartOOptimisationSettings { CapacityEnvelope = true }), () => Now);

            //Nothing running: nothing to label.
            partOProgressState.Activity("round 1");
            Assert.Equal("Optimisation rounds", partOProgressState.Label(Modify.PartOOptimisationPhase_Rounds));

            partOProgressState.Start(Modify.PartOOptimisationPhase_Rounds);
            partOProgressState.Activity("round 1");

            //Starting a running stage again does not reset it: the next round simply relabels it.
            partOProgressState.Start(Modify.PartOOptimisationPhase_Rounds);
            partOProgressState.Activity("round 2");

            Assert.Equal("Optimisation rounds · round 2", partOProgressState.Label(Modify.PartOOptimisationPhase_Rounds));

            partOProgressState.Start(Modify.PartOOptimisationPhase_CapacityEnvelope);

            Assert.Equal(PartOProgressStageStatus.Completed, partOProgressState.Status(Modify.PartOOptimisationPhase_Rounds));
            Assert.StartsWith("✓ Optimisation rounds · round 2", partOProgressState.Lines()[Modify.PartOOptimisationPhase_Rounds]);
        }

        [Fact]
        public void Elapsed_time_counts_up_and_stops_when_the_operation_completes()
        {
            DateTime now = Now;
            PartOProgressState partOProgressState = new(["TM59 assessment"], () => now);

            partOProgressState.Start(0);
            now = now.AddSeconds(42);

            Assert.Equal("Elapsed 42s", partOProgressState.ElapsedText);
            Assert.False(partOProgressState.IsFinished);

            partOProgressState.Complete();
            now = now.AddHours(2);

            Assert.True(partOProgressState.IsFinished);
            Assert.Equal("Elapsed 42s", partOProgressState.ElapsedText);
            Assert.Equal(PartOProgressStageStatus.Completed, partOProgressState.Status(0));

            //Never a time remaining: nothing here knows one.
            Assert.DoesNotContain("remaining", partOProgressState.ElapsedText);
        }

        [Fact]
        public void A_failure_marks_the_stage_that_was_running_and_says_so_in_words()
        {
            DateTime now = Now;
            PartOProgressState partOProgressState = new(["TAS simulation (full year)", "TM59 assessment"], () => now);

            partOProgressState.Start(0);
            partOProgressState.Report(1, 2);
            now = now.AddMinutes(3);

            partOProgressState.Fail("The simulation did not produce a results file.");

            Assert.Equal(PartOProgressStageStatus.Failed, partOProgressState.Status(0));
            Assert.Equal("✕", PartOProgressState.Glyph(partOProgressState.Status(0)));
            Assert.Equal("Did not complete: TAS simulation (full year), 3m 00s", partOProgressState.AccessibleLine(0));
            Assert.Equal("The simulation did not produce a results file.", partOProgressState.Detail);
            Assert.Null(partOProgressState.Percent);
            Assert.True(partOProgressState.IsFinished);
        }

        //-------------------------------------------------------------------------------------------------
        //What the note says: is the number real, and what does Cancel do
        //-------------------------------------------------------------------------------------------------

        [Fact]
        public void The_note_says_why_there_is_no_percentage_and_what_cancel_really_does()
        {
            string tas = PartOProgressState.Note(false, true, false);
            Assert.Contains("No percentage is shown: TAS does not report progress", tas);
            Assert.Contains("next safe point between steps", tas);
            Assert.Contains("already running finishes first", tas);

            //Cancel is never presented as immediate.
            Assert.DoesNotContain("immediately", tas);

            string review = PartOProgressState.Note(false, false, false);
            Assert.Contains("No percentage is shown: this step does not report one.", review);
            Assert.Contains("cannot be cancelled", review);
            Assert.DoesNotContain("TAS", review);

            //A real count needs no apology.
            Assert.DoesNotContain("No percentage", PartOProgressState.Note(true, true, false));

            string requested = PartOProgressState.Note(false, true, true);
            Assert.StartsWith("Cancel requested.", requested);
            Assert.Contains("can take minutes", requested);
        }

        //-------------------------------------------------------------------------------------------------
        //The window renders exactly that
        //-------------------------------------------------------------------------------------------------

        private static T Find<T>(FrameworkElement frameworkElement, string name) where T : class
        {
            return (T)frameworkElement.FindName(name);
        }

        [WpfFact]
        public void The_window_is_indeterminate_with_no_percentage_for_a_TAS_operation()
        {
            PartOProgressState partOProgressState = new(Modify.PartOOptimisationPhases(new PartOOptimisationSettings { CapacityEnvelope = false }));
            partOProgressState.Start(Modify.PartOOptimisationPhase_Rounds);
            partOProgressState.Activity("round 2");
            partOProgressState.Detail = "Simulating Model";

            PartOProgressWindow partOProgressWindow = new()
            {
                Heading = "Iteration 2B — TM59 optimisation",
                Cancellable = true,
                State = partOProgressState,
            };

            try
            {
                Assert.True(Find<ProgressBar>(partOProgressWindow, "progressBar").IsIndeterminate);
                Assert.Equal(Visibility.Collapsed, Find<TextBlock>(partOProgressWindow, "textBlock_Percent").Visibility);
                Assert.StartsWith("Elapsed ", Find<TextBlock>(partOProgressWindow, "textBlock_Elapsed").Text);
                Assert.Contains("TAS does not report progress", Find<TextBlock>(partOProgressWindow, "textBlock_Note").Text);
                Assert.Equal("Simulating Model", Find<TextBlock>(partOProgressWindow, "textBlock_Detail").Text);

                ItemsControl itemsControl = Find<ItemsControl>(partOProgressWindow, "itemsControl_Stages");
                Assert.Equal(2, itemsControl.Items.Count);
            }
            finally
            {
                partOProgressWindow.Close();
            }
        }

        [WpfFact]
        public void The_window_shows_a_determinate_bar_and_its_percentage_only_for_a_real_count()
        {
            PartOProgressState partOProgressState = new(["Read"]);
            partOProgressState.Start(0);
            partOProgressState.Report(1, 4);

            PartOProgressWindow partOProgressWindow = new()
            {
                Heading = "A counted operation",
                Cancellable = false,
                State = partOProgressState,
            };

            try
            {
                ProgressBar progressBar = Find<ProgressBar>(partOProgressWindow, "progressBar");
                TextBlock textBlock_Percent = Find<TextBlock>(partOProgressWindow, "textBlock_Percent");

                Assert.False(progressBar.IsIndeterminate);
                Assert.Equal(0.25, progressBar.Value, 6);
                Assert.Equal("25%", textBlock_Percent.Text);
                Assert.Equal(Visibility.Visible, textBlock_Percent.Visibility);

                //The count ends with its stage; the bar goes back to claiming nothing.
                partOProgressState.Complete();
                partOProgressWindow.Render();

                Assert.True(progressBar.IsIndeterminate);
                Assert.Equal(Visibility.Collapsed, textBlock_Percent.Visibility);
            }
            finally
            {
                partOProgressWindow.Close();
            }
        }

        [WpfFact]
        public void A_window_without_cancel_still_says_it_cannot_be_cancelled()
        {
            PartOProgressState partOProgressState = new(["TM59 assessment"]);
            partOProgressState.Start(0);

            PartOProgressWindow partOProgressWindow = new()
            {
                Heading = "Checking TM59 results",
                Cancellable = false,
                State = partOProgressState,
            };

            try
            {
                Assert.Equal(Visibility.Collapsed, Find<Button>(partOProgressWindow, "button_Cancel").Visibility);
                Assert.Contains("cannot be cancelled", Find<TextBlock>(partOProgressWindow, "textBlock_Note").Text);
            }
            finally
            {
                partOProgressWindow.Close();
            }
        }

        [WpfFact]
        public void Cancel_is_requested_once_and_the_window_says_what_happens_next()
        {
            PartOProgressState partOProgressState = new(["TAS simulation (full year)"]);
            partOProgressState.Start(0);

            PartOProgressWindow partOProgressWindow = new()
            {
                Heading = "Prepare & Run — Iteration 1a",
                Cancellable = true,
                State = partOProgressState,
            };

            int requests = 0;
            partOProgressWindow.CancelRequested += (s, e) => requests++;

            try
            {
                Button button_Cancel = Find<Button>(partOProgressWindow, "button_Cancel");

                button_Cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                button_Cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.Equal(1, requests);
                Assert.False(button_Cancel.IsEnabled);
                Assert.Equal("Cancelling…", button_Cancel.Content);

                //The timer's next render keeps the cancel wording rather than reverting it.
                partOProgressWindow.Render();
                Assert.StartsWith("Cancel requested.", Find<TextBlock>(partOProgressWindow, "textBlock_Note").Text);
            }
            finally
            {
                partOProgressWindow.Close();
            }
        }

        /// <summary>
        /// Evidence only: renders the real window as it stands part-way through an Iteration 3 run and an
        /// Iteration 2B round - synthetic states, no TAS - into <c>SAM_PARTO_PROGRESS_EVIDENCE</c>. Without that
        /// variable it does nothing.
        /// </summary>
        [WpfFact]
        public void Evidence_renders_the_iteration_3_and_iteration_2B_windows_mid_run()
        {
            string directory = Environment.GetEnvironmentVariable("SAM_PARTO_PROGRESS_EVIDENCE");

            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            System.IO.Directory.CreateDirectory(directory);

            DateTime now = DateTime.UtcNow.AddMinutes(-9).AddSeconds(-14);
            PartOProgressState partOProgressState_Iteration3 = new(Modify.PartOIteration3Phases, () => now);
            partOProgressState_Iteration3.Start(0);
            now = now.AddSeconds(9);
            partOProgressState_Iteration3.Start(1);
            now = now.AddSeconds(3);
            partOProgressState_Iteration3.Start(2);
            partOProgressState_Iteration3.Detail = "Simulating Model";
            now = DateTime.UtcNow;

            PartOProgressWindow partOProgressWindow_Iteration3 = new()
            {
                Heading = "Iteration 3 — " + Query.PartOIteration3MethodLabel(PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance),
                Subheading = "Reference case: Iteration 1a — MVHR design duty (no manufacturer unit)",
                Cancellable = true,
                State = partOProgressState_Iteration3,
            };

            PartOWorkflowEvidenceHarness.Render(partOProgressWindow_Iteration3, System.IO.Path.Combine(directory, "iteration3-running.png"), 520, 0);
            partOProgressWindow_Iteration3.Close();

            now = DateTime.UtcNow.AddMinutes(-19).AddSeconds(-40);
            PartOOptimisationSettings partOOptimisationSettings = new() { CapacityEnvelope = true };
            PartOProgressState partOProgressState_2B = new(Modify.PartOOptimisationPhases(partOOptimisationSettings), () => now);
            partOProgressState_2B.Start(Modify.PartOOptimisationPhase_Starting);
            now = now.AddSeconds(6);
            partOProgressState_2B.Start(Modify.PartOOptimisationPhase_Rounds);
            partOProgressState_2B.Activity("round 2");
            partOProgressState_2B.Detail = "Simulating Model";
            now = DateTime.UtcNow;

            PartOProgressWindow partOProgressWindow_2B = new()
            {
                Heading = "Iteration 2B — TM59 optimisation",
                Subheading = Modify.PartOOptimisationProgressSubheading(partOOptimisationSettings),
                Cancellable = true,
                State = partOProgressState_2B,
            };

            PartOWorkflowEvidenceHarness.Render(partOProgressWindow_2B, System.IO.Path.Combine(directory, "iteration2B-round.png"), 520, 0);
            partOProgressWindow_2B.Close();
        }

        [Fact]
        public void The_host_Cancel_latches_its_token_which_the_nested_TAS_steps_link_to()
        {
            using PartOProgressHost partOProgressHost = new("Iteration 2B — TM59 optimisation", null, Modify.PartOOptimisationPhases(new PartOOptimisationSettings()), true, false);

            Assert.Same(partOProgressHost, PartOProgressHost.Current);

            //What RunPartOSimulation and RunWorkflow do while a host is current.
            using System.Threading.CancellationTokenSource cancellationTokenSource = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(System.Threading.CancellationToken.None, partOProgressHost.Token);

            Assert.False(cancellationTokenSource.IsCancellationRequested);

            partOProgressHost.Cancel();

            Assert.True(cancellationTokenSource.IsCancellationRequested);
        }
    }
}
