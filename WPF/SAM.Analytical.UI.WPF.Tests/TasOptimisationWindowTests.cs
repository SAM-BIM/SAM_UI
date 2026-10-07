// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.IO;
using File = System.IO.File;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Simulate &gt; Optimisation end to end: the ribbon command, the window's opening state, and real runs of
    /// GenOptDocument.RunNative through SAM_Tas' StubTasGenExecute - a child process that follows the TasGenExecute
    /// protocol - so the SAM.Math kernel, the SAM_Tas evaluator and the window's background run, progress and
    /// cancellation are all the production code. No Tas, licence or COM is needed.
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasOptimisationWindowTests : IDisposable
    {
        public TasOptimisationWindowTests()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
        }

        private static T Control<T>(FrameworkElement frameworkElement, string name) where T : class
        {
            return Assert.IsAssignableFrom<T>(frameworkElement.FindName(name));
        }

        private static TasOptimisationWindow Window(TasOptimisationWorkspace workspace, TasOptimisationExample tasOptimisationExample = TasOptimisationExample.SystemsDemoGoldenSection)
        {
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(workspace.Input(tasOptimisationExample));
            return window;
        }

        private static async Task Until(Func<bool> condition, string message, int seconds = 60)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, message);
                await Task.Delay(20);
            }
        }

        // ---- the ribbon command --------------------------------------------------------------------------

        [WpfFact]
        public void The_ribbon_button_sits_right_after_Energy_Simulation_and_needs_no_model()
        {
            Windows.AnalyticalWindow analyticalWindow = new Windows.AnalyticalWindow();
            try
            {
                RibbonGroup ribbonGroup = Assert.IsType<RibbonGroup>(analyticalWindow.FindName("RibbonGroup_Simulate_Simulate"));
                List<RibbonButton> ribbonButtons = ribbonGroup.Items.OfType<RibbonButton>().ToList();

                Assert.Equal(["RibbonButton_SolarSimulation", "RibbonButton_EnergySimulation", "RibbonButton_Optimisation"], ribbonButtons.Select(x => x.Name));

                RibbonButton ribbonButton = ribbonButtons[2];
                Assert.Equal("Optimisation", ribbonButton.Label);
                Assert.NotNull(ribbonButton.LargeImageSource);
                Assert.Contains("Part O Optimise (2B)", ribbonButton.ToolTipDescription);

                //Energy Simulation needs an open model; Optimisation works on a Tas project folder.
                Assert.False(ribbonButtons[1].IsEnabled);
                Assert.True(ribbonButton.IsEnabled);
            }
            finally
            {
                analyticalWindow.Close();
            }
        }

        [Fact]
        public void The_icon_is_its_own_not_Energy_Simulations()
        {
            using System.Drawing.Bitmap optimisation = Properties.Resources.SAM_Optimisation;
            using System.Drawing.Bitmap energySimulation = Properties.Resources.SAM_EnergySimulation;

            Assert.Equal((32, 32), (optimisation.Width, optimisation.Height));

            bool different = false;
            for (int x = 0; x < 32 && !different; x++)
            {
                for (int y = 0; y < 32 && !different; y++)
                {
                    different = optimisation.GetPixel(x, y) != energySimulation.GetPixel(x, y);
                }
            }

            Assert.True(different);
        }

        // ---- opening state -------------------------------------------------------------------------------

        [WpfFact]
        public void The_window_opens_on_the_golden_section_example_with_Start_and_Step_not_applicable()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            TasOptimisationInput input = window.Input;
            Assert.Equal(AlgorithmType.GoldenSection, input.AlgorithmType);
            Assert.Equal((string.Empty, string.Empty), (input.Directory, input.ScriptPath));
            Assert.Equal("Setpoint", Assert.Single(input.Parameters).Name);
            Assert.False(input.Parameters[0].StartAndStepApplicable);
            Assert.Equal("Result", input.PrimaryObjective?.Name);

            Assert.Equal(Visibility.Visible, Control<Grid>(window, "grid_GoldenSection").Visibility);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_HookeJeeves").Visibility);
            Assert.Equal("0.1", Control<TextBox>(window, "textBox_AbsDiffFunction").Text);
            Assert.Equal("2000", Control<TextBox>(window, "textBox_MaxIterations").Text);
            Assert.Contains("Minimises Result; also records Cost, CO2", Control<TextBlock>(window, "textBlock_Objectives").Text);

            //Nothing to run yet: the folder and script are the user's.
            Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            Assert.Contains(window.Checks, x => x.Title == "Tas project folder" && x.Status == TasOptimisationCheckStatus.Blocked);
            Assert.StartsWith("Before running - Tas project folder", Control<TextBlock>(window, "textBlock_NextStep").Text);
        }

        [WpfFact]
        public void Choosing_Hooke_Jeeves_makes_Start_and_Step_applicable_and_shows_its_settings()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            Control<ComboBox>(window, "comboBox_Algorithm").SelectedItem = AlgorithmType.GPSHookeJeeves;

            Assert.Equal(AlgorithmType.GPSHookeJeeves, window.Input.AlgorithmType);
            Assert.True(window.Input.Parameters[0].StartAndStepApplicable);
            Assert.Equal(Visibility.Collapsed, Control<Grid>(window, "grid_GoldenSection").Visibility);
            Assert.Equal(Visibility.Visible, Control<Grid>(window, "grid_HookeJeeves").Visibility);
            Assert.Equal("4", Control<TextBox>(window, "textBox_NumberOfStepReduction").Text);
        }

        [WpfFact]
        public void Loading_the_Hooke_Jeeves_example_keeps_the_folder_and_script()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);

            ComboBox comboBox = Control<ComboBox>(window, "comboBox_Example");
            comboBox.SelectedIndex = 1;
            Control<Button>(window, "button_LoadExample").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            TasOptimisationInput input = window.Input;
            Assert.Equal(AlgorithmType.GPSHookeJeeves, input.AlgorithmType);
            Assert.Equal(("10", "2"), (input.Parameters[0].Start, input.Parameters[0].Step));
            Assert.Equal((workspace.Directory, workspace.ScriptPath), (input.Directory, input.ScriptPath));
        }

        [WpfFact]
        public void The_open_models_folder_is_proposed_only_when_it_holds_Tas_files()
        {
            using TasOptimisationWorkspace withTas = new TasOptimisationWorkspace();
            using TasOptimisationWorkspace withoutTas = new TasOptimisationWorkspace(tasFile: false);

            Assert.Equal(withTas.Directory, new TasOptimisationWindow(withTas.Directory).Input.Directory);
            Assert.Equal(string.Empty, new TasOptimisationWindow(withoutTas.Directory).Input.Directory);
        }

        [WpfFact]
        public void A_Tas_folder_that_cannot_be_listed_blocks_the_run_instead_of_failing_the_window()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            using (workspace.DenyListing())
            {
                //Proposed as the open model's folder (the constructor), then chosen in the form (Refresh).
                TasOptimisationWindow window = new TasOptimisationWindow(workspace.Directory) { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
                Assert.Equal(string.Empty, window.Input.Directory);

                window.SetInput(workspace.Input());

                Assert.Contains(window.Checks, x => x.Title == "Tas project folder" && x.Status == TasOptimisationCheckStatus.Blocked && x.Detail.StartsWith("The folder cannot be read: "));
                Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            }
        }

        [WpfFact]
        public void The_form_is_remembered_for_the_session_only()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();

            TasOptimisationWindow window = Window(workspace, TasOptimisationExample.SystemsDemoHookeJeeves);
            window.Input.Parameters[0].Maximum = "30";
            window.Show();
            window.Close();

            TasOptimisationInput remembered = new TasOptimisationWindow().Input;
            Assert.Equal((workspace.Directory, AlgorithmType.GPSHookeJeeves, "30"), (remembered.Directory, remembered.AlgorithmType, remembered.Parameters[0].Maximum));

            TasOptimisationWindow.ResetSession();
            Assert.Equal(AlgorithmType.GoldenSection, new TasOptimisationWindow().Input.AlgorithmType);
        }

        [WpfFact]
        public void A_missing_TasGenExecute_blocks_the_run_and_nothing_starts()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = Path.Combine(workspace.Directory, "missing.exe") };
            window.SetInput(workspace.Input());

            Assert.False(Control<Button>(window, "button_Run").IsEnabled);
            Assert.Contains(window.Checks, x => x.Title == "TasGenExecute" && x.Status == TasOptimisationCheckStatus.Blocked);

            window.RunAsync().GetAwaiter().GetResult();

            Assert.Null(window.Report);
            Assert.False(Directory.Exists(workspace.RunsDirectory));
        }

        // ---- runs through the stub -----------------------------------------------------------------------

        [WpfFact]
        public async Task A_golden_section_run_shows_the_kernels_trace_and_result_and_matches_a_direct_RunNative()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.Success, report.Outcome);
            Assert.True(report.Successful);
            Assert.False(window.Running);
            Assert.NotNull(report.Interval);

            //The live trace is the kernel's OutputListingAll, row for row.
            Assert.Equal(report.Result.Entries, window.TraceRows.Select(x => x.Entry));
            Assert.Equal(report.Result.Entries.Count, workspace.EvaluationFolders().Count);

            //Golden section: the lowest entry, first on a tie.
            OptimisationTraceEntry lowest = report.Result.Entries.Where(x => !double.IsNaN(x.Objective)).OrderBy(x => x.Objective).First();
            Assert.Equal(lowest.Coordinates, report.BestPoint);
            Assert.Equal(["Result", "Cost", "CO2"], report.ObjectiveNames);
            Assert.StartsWith(workspace.RunsDirectory, report.RunDirectory);
            Assert.True(Directory.Exists(report.RunDirectory));

            //The window adds nothing: the same document run directly gives the same trace, bit for bit.
            Assert.True(workspace.Input().TryGetDefinition(out TasOptimisationDefinition definition, out _));
            NativeGenOptRun direct = definition.ToGenOptDocument(workspace.Directory, File.ReadAllText(workspace.ScriptPath)).RunNative(Path.Combine(workspace.Directory, "direct"), TasOptimisationWorkspace.StubExecutable);
            Assert.Equal(direct.Result.Outcome, report.Outcome);
            Assert.Equal(direct.Result.Simulations, report.Simulations);
            Assert.Equal(direct.Result.Entries.Select(x => (x.Simulation, x.Coordinates[0], x.Outputs[0], x.Outputs[1], x.Outputs[2], x.Event)), report.Result.Entries.Select(x => (x.Simulation, x.Coordinates[0], x.Outputs[0], x.Outputs[1], x.Outputs[2], x.Event)));

            //Shown: the result panel with the outcome and the best point, and Run available again.
            Assert.Equal(Visibility.Visible, Control<Border>(window, "border_Result").Visibility);
            Assert.Equal(report.Headline, Control<TextBlock>(window, "textBlock_ResultHeadline").Text);
            Assert.True(Control<Button>(window, "button_OpenRunFolder").IsEnabled);
            Assert.True(Control<Button>(window, "button_Run").IsEnabled);
            Assert.StartsWith("Lowest so far: Simulation ", Control<TextBlock>(window, "textBlock_RunLowest").Text);
        }

        [WpfFact]
        public async Task A_Hooke_Jeeves_run_reports_the_kernels_minimum()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 5.0 }));
            TasOptimisationWindow window = Window(workspace, TasOptimisationExample.SystemsDemoHookeJeeves);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.Success, report.Outcome);
            Assert.NotNull(report.Result.Minimum);
            Assert.Equal(report.Result.Minimum.Coordinates, report.BestPoint);
            Assert.Equal(5.0, report.BestPoint[0]);
            Assert.Equal(report.Result.Entries, window.TraceRows.Select(x => x.Entry));
        }

        [WpfFact]
        public async Task The_primary_objective_chosen_in_the_window_is_the_one_minimised()
        {
            //The stub writes Result = (x - 4)^2 and Cost = 1 * x: minimising Cost drives Setpoint to its lower bound.
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.0 }, ["Result", "Cost"]));
            TasOptimisationInput input = workspace.Input(TasOptimisationExample.SystemsDemoHookeJeeves);
            input.Objectives = [new TasOptimisationObjectiveRow("Result", false), new TasOptimisationObjectiveRow("Cost", true)];

            TasOptimisationWindow window = new TasOptimisationWindow() { TasGenExecutePath = TasOptimisationWorkspace.StubExecutable };
            window.SetInput(input);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(["Cost", "Result"], report.ObjectiveNames);
            Assert.Equal(-5.0, report.BestPoint[0]);
        }

        [WpfFact]
        public async Task Cancel_lets_the_running_evaluation_finish_and_starts_no_other()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.968943799848584 }, sleepMs: new Dictionary<string, int> { ["2"] = 2500 }));
            TasOptimisationWindow window = Window(workspace);

            Task task = window.RunAsync();
            Assert.True(window.Running);
            Assert.Equal(Visibility.Visible, Control<Button>(window, "button_Cancel").Visibility);
            Assert.False(Control<TextBox>(window, "textBox_Directory").IsEnabled);

            //Cancel only once evaluation 2's process is running (it records its invocation, then sleeps).
            await Until(() => workspace.EvaluationFolder("0002") is string folder && File.Exists(Path.Combine(folder, "stub-invocation.txt")), "evaluation 2 never started");
            string evaluation2 = workspace.EvaluationFolder("0002");
            Assert.False(File.Exists(Path.Combine(evaluation2, "stub-finished.txt")), "cancel must happen while the process runs");

            window.RequestCancel();
            Assert.False(Control<Button>(window, "button_Cancel").IsEnabled);

            await task;

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.Cancelled, report.Outcome);
            Assert.False(report.Successful);
            Assert.Empty(report.BestPoint);
            Assert.True(File.Exists(Path.Combine(evaluation2, "stub-finished.txt")), "the running evaluation was not allowed to finish");
            Assert.Contains("Result::", File.ReadAllText(Path.Combine(evaluation2, "Output.txt")));
            Assert.Equal(["0001", "0002"], workspace.EvaluationFolders());
            Assert.False(window.Running);
            Assert.True(Control<TextBox>(window, "textBox_Directory").IsEnabled);
        }

        [WpfFact]
        public async Task Closing_during_a_run_asks_then_stops_after_the_current_evaluation_and_closes()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.968943799848584 }, sleepMs: new Dictionary<string, int> { ["1"] = 1500 }));
            TasOptimisationWindow window = Window(workspace);
            int asked = 0;
            window.ConfirmStop = () =>
            {
                asked++;
                return true;
            };

            bool closed = false;
            window.Closed += (s, e) => closed = true;
            window.Show();

            Task task = window.RunAsync();
            await Until(() => workspace.EvaluationFolder("0001") is string folder && File.Exists(Path.Combine(folder, "stub-invocation.txt")), "evaluation 1 never started");

            window.Close();
            Assert.Equal(1, asked);
            Assert.False(closed, "the window must stay open while TasGenExecute runs");
            Assert.True(window.Running);

            //A second close while stopping does not ask again.
            window.Close();
            Assert.Equal(1, asked);

            await task;

            Assert.True(closed);
            Assert.Equal(OptimisationOutcome.Cancelled, window.Report?.Outcome);
            Assert.True(File.Exists(Path.Combine(workspace.EvaluationFolder("0001"), "stub-finished.txt")));
            Assert.Equal(["0001"], workspace.EvaluationFolders());
        }

        [WpfFact]
        public async Task Declining_to_stop_keeps_the_run_and_the_window()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace();
            TasOptimisationWindow window = Window(workspace);
            window.ConfirmStop = () => false;
            window.Show();
            try
            {
                Task task = window.RunAsync();
                window.Close();

                Assert.True(window.IsVisible);
                await task;
                Assert.Equal(OptimisationOutcome.Success, window.Report?.Outcome);
                Assert.True(window.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public async Task An_evaluation_failure_is_reported_with_the_evaluators_message()
        {
            using TasOptimisationWorkspace workspace = new TasOptimisationWorkspace(TasOptimisationWorkspace.StubScript(new[] { 4.0 }, modes: new Dictionary<string, string> { ["1"] = "errorFile" }));
            TasOptimisationWindow window = Window(workspace);

            await window.RunAsync();

            TasOptimisationReport report = Assert.IsType<TasOptimisationReport>(window.Report);
            Assert.Equal(OptimisationOutcome.EvaluationFailed, report.Outcome);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, report.Status);
            Assert.Contains(report.Lines, x => x.Detail.Contains("injected script exception"));
            Assert.Equal("✕", Control<TextBlock>(window, "textBlock_ResultGlyph").Text);
        }

        // ---- layout --------------------------------------------------------------------------------------

        [WpfFact]
        public void At_its_minimum_size_the_window_keeps_its_actions_inside_the_client_area()
        {
            TasOptimisationWindow window = new TasOptimisationWindow()
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 0,
                Top = 0,
            };

            window.Width = window.MinWidth;
            window.Height = window.MinHeight;

            try
            {
                window.Show();
                window.UpdateLayout();

                Size client = Client(window);
                Button button_Close = Control<Button>(window, "button_Close");
                Point point = button_Close.TranslatePoint(new Point(button_Close.ActualWidth, button_Close.ActualHeight), (FrameworkElement)window.Content);

                Assert.True(point.Y > 0, "the window was not laid out, so this assertion would pass without testing anything");
                Assert.True(point.Y <= client.Height + 0.5, string.Format("the Close button's bottom edge is at {0}, below the client area of {1}", point.Y, client.Height));
                Assert.True(point.X <= client.Width + 0.5, string.Format("the Close button's right edge is at {0}, outside the client area of {1}", point.X, client.Width));

                Button button_Run = Control<Button>(window, "button_Run");
                Point point_Run = button_Run.TranslatePoint(new Point(button_Run.ActualWidth, button_Run.ActualHeight), (FrameworkElement)window.Content);
                Assert.True(point_Run.Y <= client.Height + 0.5);
            }
            finally
            {
                window.Close();
            }
        }

        [WpfFact]
        public void The_window_is_never_taller_than_the_work_area()
        {
            TasOptimisationWindow window = new TasOptimisationWindow();

            Assert.True(window.Height <= SystemParameters.WorkArea.Height + 0.5);
            Assert.True(window.MinHeight <= 688, "a 1080p display at 150% scaling gives 688 DIP of work area");
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private static Size Client(System.Windows.Window window)
        {
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero || !GetClientRect(handle, out RECT rect))
            {
                return new Size(double.NaN, double.NaN);
            }

            System.Windows.Media.Matrix matrix = PresentationSource.FromVisual(window).CompositionTarget.TransformFromDevice;
            return new Size((rect.Right - rect.Left) * matrix.M11, (rect.Bottom - rect.Top) * matrix.M22);
        }

        // ---- no Java route -------------------------------------------------------------------------------

        /// <summary>
        /// SAM_UI reaches GenOpt only through RunNative: the built assembly references no member of the legacy Java route
        /// (Run, the GenOpt.bat/Java executable file, the simulation command, the Java path, the Tas Manager registry).
        /// </summary>
        [Fact]
        public void SAM_UI_calls_RunNative_and_no_member_of_the_legacy_Java_route()
        {
            string path = typeof(TasOptimisationWindow).Assembly.Location;

            using FileStream fileStream = File.OpenRead(path);
            using PEReader pEReader = new PEReader(fileStream);
            MetadataReader metadataReader = pEReader.GetMetadataReader();

            List<string> members = new List<string>();
            foreach (MemberReferenceHandle memberReferenceHandle in metadataReader.MemberReferences)
            {
                MemberReference memberReference = metadataReader.GetMemberReference(memberReferenceHandle);
                if (memberReference.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                TypeReference typeReference = metadataReader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
                members.Add(metadataReader.GetString(typeReference.Namespace) + "." + metadataReader.GetString(typeReference.Name) + "::" + metadataReader.GetString(memberReference.Name));
            }

            Assert.Contains("SAM.Analytical.Tas.GenOpt.GenOptDocument::RunNative", members);

            string[] forbidden =
            [
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::Run",
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::get_ExecutableFile",
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::get_Command",
                "SAM.Analytical.Tas.GenOpt.GenOptDocument::set_Command",
                "SAM.Analytical.Tas.GenOpt.Query::TasGenOptJavaPath",
                "SAM.Analytical.Tas.GenOpt.Create::Command",
                "SAM.Core.Tas.Modify::SetProjectDirectory",
            ];

            Assert.Empty(members.Intersect(forbidden));

            // PR6: success, withholding, the best point, the running lowest and the refusal wording are SAM_Tas' rules.
            Assert.Contains("SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome::.ctor", members);
            Assert.Contains("SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome::IsLower", members);
            Assert.Contains("SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome::RefusalMessage", members);
        }

        [Fact]
        public void A_SAM_Analytical_Tas_GenOpt_without_the_shared_result_rules_is_reported_as_a_load_failure()
        {
            // The probe reaches NativeGenOptOutcome, so a pre-PR6 assembly fails it like a stale SAM.Math does.
            Assert.Null(Query.TasOptimisationAssemblyFailure());
            TypeLoadException stale = new TypeLoadException("Could not load type 'SAM.Analytical.Tas.GenOpt.NativeGenOptOutcome'.");
            Assert.True(Query.IsTasOptimisationLoadFailure(stale));
            Assert.StartsWith("Simulate > Optimisation could not load its assemblies.", TasOptimisationReport.Message(stale));
        }
    }
}
