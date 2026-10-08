// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Design Optimisation (Simulate &gt; Optimisation): the native SAM optimisation of a Tas project. The window is a form
    /// over a SAM.Core.Optimisation definition (<see cref="TasOptimisationInput"/>) plus the local Tas settings; the
    /// definition's own diagnostics and SAM_Tas judge it, SAM_Tas builds the document (<c>ToGenOptDocument</c>), and
    /// <see cref="GenOptDocument.RunNative"/> runs on a background thread: the SAM.Math kernel drives TasGenExecute.exe, one
    /// simulation at a time. The window shows the kernel's own progress and result; it implements no optimisation,
    /// mapping, file protocol, retry or parsing of its own. Java GenOpt is never used.
    /// <para>
    /// Two tabs: Setup (what is optimised, and whether it is ready) and Run &amp; Results (progress, trace, result and
    /// Diagnostics). Pressing Run switches to Run &amp; Results. The window is presentation only: the definition, its
    /// diagnostics and the Tas mapping are SAM.Core.Optimisation's and SAM_Tas' (native Optimisation PR3/PR4); the run
    /// path and the outcome rules are those of the Java-free GenOpt PR5/PR6.
    /// </para>
    /// <para>
    /// Cancellation is cooperative: Cancel (or closing the window) asks the kernel to stop; the running TasGenExecute
    /// finishes, no further simulation starts, and no process is killed.
    /// </para>
    /// <para>
    /// The form is remembered for the current application session only.
    /// </para>
    /// </summary>
    public partial class TasOptimisationWindow : System.Windows.Window
    {
        private static TasOptimisationInput? session;

        private TasOptimisationInput tasOptimisationInput;
        private readonly ObservableCollection<TasOptimisationParameterRow> parameterRows = new ObservableCollection<TasOptimisationParameterRow>();
        private readonly ObservableCollection<TasOptimisationObjectiveRow> objectiveRows = new ObservableCollection<TasOptimisationObjectiveRow>();
        private readonly ObservableCollection<TasOptimisationObjectiveRow> recordedRows = new ObservableCollection<TasOptimisationObjectiveRow>();
        private readonly ObservableCollection<string> objectiveChoices = new ObservableCollection<string>();
        private readonly ObservableCollection<TasOptimisationTraceRow> traceRows = new ObservableCollection<TasOptimisationTraceRow>();
        private readonly DispatcherTimer dispatcherTimer_Refresh;
        private readonly DispatcherTimer dispatcherTimer_Elapsed;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private List<TasOptimisationCheck> tasOptimisationChecks = new List<TasOptimisationCheck>();
        private CancellationTokenSource? cancellationTokenSource;
        private TasOptimisationProgressState? tasOptimisationProgressState;
        private bool loading;
        private bool syncingObjective;
        private bool running;
        private bool closeWhenFinished;

        public TasOptimisationWindow()
            : this(null)
        {
        }

        /// <param name="modelDirectory">
        /// The open model's folder, where Energy Simulation writes its Tas files by default. It becomes the Tas project
        /// folder when nothing is remembered from this session and it holds Tas files.
        /// </param>
        public TasOptimisationWindow(string? modelDirectory)
        {
            InitializeComponent();

            //The content scrolls, so on a small or scaled display (1080p at 150% leaves 688 DIP) the window simply starts
            //no taller than the work area, with the actions still in view.
            Height = System.Math.Max(MinHeight, System.Math.Min(Height, SystemParameters.WorkArea.Height));

            comboBox_Example.Items.Add(new ComboBoxItem() { Content = "Systems Demo – golden section", Tag = TasOptimisationExample.SystemsDemoGoldenSection });
            comboBox_Example.Items.Add(new ComboBoxItem() { Content = "Systems Demo – Hooke–Jeeves", Tag = TasOptimisationExample.SystemsDemoHookeJeeves });
            comboBox_Example.SelectedIndex = 0;

            comboBox_Algorithm.ItemsSource = TasOptimisationInput.OptimisationAlgorithms;

            itemsControl_Parameters.ItemsSource = parameterRows;
            itemsControl_Objectives.ItemsSource = recordedRows;
            comboBox_Objective.ItemsSource = objectiveChoices;
            dataGrid_Trace.ItemsSource = traceRows;

            if (session != null)
            {
                tasOptimisationInput = new TasOptimisationInput(session);
            }
            else
            {
                try
                {
                    tasOptimisationInput = TasOptimisationInput.Create(TasOptimisationInput.DefaultExample);
                }
                catch (Exception exception) when (Query.IsTasOptimisationLoadFailure(exception))
                {
                    //A stale optimisation assembly: the window still opens, and its readiness list says what is wrong.
                    tasOptimisationInput = new TasOptimisationInput();
                }

                if (!string.IsNullOrWhiteSpace(modelDirectory) && Query.TasOptimisationTasFiles(modelDirectory!).Count != 0)
                {
                    tasOptimisationInput.Directory = modelDirectory!;
                }
            }

            dispatcherTimer_Refresh = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
            dispatcherTimer_Refresh.Tick += (s, e) =>
            {
                dispatcherTimer_Refresh.Stop();
                Refresh();
            };

            dispatcherTimer_Elapsed = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
            dispatcherTimer_Elapsed.Tick += (s, e) => UpdateRunText();

            Load(tasOptimisationInput);
            Refresh();

            Closing += TasOptimisationWindow_Closing;
        }

        /// <summary>TasGenExecute.exe; null for the installed one. Tests point it at a protocol stand-in.</summary>
        internal string? TasGenExecutePath { get; set; }

        /// <summary>Asked when the window is closed during a run; true stops the run. Tests replace the message box.</summary>
        internal Func<bool>? ConfirmStop { get; set; }

        /// <summary>How the last run ended; null before the first run.</summary>
        public TasOptimisationReport? Report { get; private set; }

        internal IReadOnlyList<TasOptimisationCheck> Checks => tasOptimisationChecks;

        internal IReadOnlyList<TasOptimisationTraceRow> TraceRows => traceRows;

        internal bool Running => running;

        /// <summary>True while the Run &amp; Results tab is showing.</summary>
        internal bool ShowingRunAndResults => ReferenceEquals(tabControl_Main.SelectedItem, tabItem_Run);

        /// <summary>The form as it stands now.</summary>
        internal TasOptimisationInput Input => Read();

        /// <summary>Forgets the session's form (tests).</summary>
        internal static void ResetSession()
        {
            session = null;
        }

        /// <summary>Replaces the form, its <see cref="TasOptimisationInput.Base"/> definition included (tests).</summary>
        internal void SetInput(TasOptimisationInput input)
        {
            tasOptimisationInput = new TasOptimisationInput(input);

            Load(tasOptimisationInput);
            Refresh();
        }

        /// <summary>
        /// Runs the optimisation when the readiness list allows it, and returns when the run has ended and its result is
        /// shown. The run itself is <see cref="GenOptDocument.RunNative"/> on a background thread; progress comes back
        /// to this thread through <see cref="Progress{T}"/>.
        /// </summary>
        internal async Task RunAsync()
        {
            if (running)
            {
                return;
            }

            Refresh();
            if (!tasOptimisationChecks.CanRun())
            {
                return;
            }

            TasOptimisationInput input = Read();
            if (!input.TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out _) || optimisationDefinition == null)
            {
                return;
            }

            string directory = input.Directory.Trim();
            string? runsDirectory = string.IsNullOrWhiteSpace(input.RunsDirectory) ? null : input.RunsDirectory.Trim();
            string? tasGenExecutePath = TasGenExecutePath;

            session = new TasOptimisationInput(input);

            //SAM_Tas maps the definition to the document it runs (objective first, the method's settings, the variables).
            GenOptDocument genOptDocument;
            try
            {
                genOptDocument = optimisationDefinition.ToGenOptDocument(directory, System.IO.File.ReadAllText(input.ScriptPath.Trim()));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException)
            {
                ShowResult(new TasOptimisationReport(exception));
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            TasOptimisationProgressState tasOptimisationProgressState = new TasOptimisationProgressState(optimisationDefinition.TasOptimisationVariableNames(), optimisationDefinition.TasOptimisationObjectiveNames());
            this.tasOptimisationProgressState = tasOptimisationProgressState;
            BeginRun(optimisationDefinition);

            Progress<OptimisationProgress> progress = new Progress<OptimisationProgress>(x =>
            {
                TasOptimisationTraceRow? tasOptimisationTraceRow = tasOptimisationProgressState.Add(x);
                if (tasOptimisationTraceRow != null)
                {
                    traceRows.Add(tasOptimisationTraceRow);
                    dataGrid_Trace.ScrollIntoView(tasOptimisationTraceRow);
                }

                UpdateRunText();
            });

            TasOptimisationReport tasOptimisationReport;
            try
            {
                NativeGenOptRun nativeGenOptRun = await Task.Run(() => genOptDocument.RunNative(runsDirectory, tasGenExecutePath, progress, cancellationToken));
                tasOptimisationReport = new TasOptimisationReport(nativeGenOptRun, cancellationToken.IsCancellationRequested);
            }
            catch (Exception exception)
            {
                tasOptimisationReport = new TasOptimisationReport(exception);
            }
            finally
            {
                stopwatch.Stop();
                dispatcherTimer_Elapsed.Stop();

                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;

                SetRunning(false);
            }

            ShowResult(tasOptimisationReport);

            if (closeWhenFinished)
            {
                Close();
            }
        }

        /// <summary>Asks the running optimisation to stop after the current evaluation.</summary>
        internal void RequestCancel()
        {
            if (!running || cancellationTokenSource == null)
            {
                return;
            }

            cancellationTokenSource.Cancel();

            button_Cancel.IsEnabled = false;
            button_Cancel.Content = "Cancelling…";
            textBlock_RunStatus.Text = "Cancelling: the running Tas simulation finishes, then the run stops. No further simulation starts.";
        }

        private TasOptimisationInput Read()
        {
            if (loading)
            {
                return tasOptimisationInput;
            }

            tasOptimisationInput.Directory = textBox_Directory.Text ?? string.Empty;
            tasOptimisationInput.ScriptPath = textBox_ScriptPath.Text ?? string.Empty;
            tasOptimisationInput.RunsDirectory = textBox_RunsDirectory.Text ?? string.Empty;
            if (comboBox_Algorithm.SelectedItem is OptimisationAlgorithm optimisationAlgorithm)
            {
                tasOptimisationInput.OptimisationAlgorithm = optimisationAlgorithm;
            }

            tasOptimisationInput.Tolerance = textBox_Tolerance.Text ?? string.Empty;
            tasOptimisationInput.StepReductionFactor = textBox_StepReductionFactor.Text ?? string.Empty;
            tasOptimisationInput.InitialStepExponent = textBox_InitialStepExponent.Text ?? string.Empty;
            tasOptimisationInput.StepExponentIncrement = textBox_StepExponentIncrement.Text ?? string.Empty;
            tasOptimisationInput.StepReductions = textBox_StepReductions.Text ?? string.Empty;
            tasOptimisationInput.MaximumSimulations = textBox_MaximumSimulations.Text ?? string.Empty;
            //MaxEqualResults is not part of the definition: SAM_Tas keeps its default.
            tasOptimisationInput.Parameters = parameterRows.ToList();
            tasOptimisationInput.Objectives = objectiveRows.ToList();

            return tasOptimisationInput;
        }

        private void Load(TasOptimisationInput input)
        {
            loading = true;
            try
            {
                textBox_Directory.Text = input.Directory;
                textBox_ScriptPath.Text = input.ScriptPath;
                textBox_RunsDirectory.Text = input.RunsDirectory;
                comboBox_Algorithm.SelectedItem = input.OptimisationAlgorithm;
                textBox_Tolerance.Text = input.Tolerance;
                textBox_StepReductionFactor.Text = input.StepReductionFactor;
                textBox_InitialStepExponent.Text = input.InitialStepExponent;
                textBox_StepExponentIncrement.Text = input.StepExponentIncrement;
                textBox_StepReductions.Text = input.StepReductions;
                textBox_MaximumSimulations.Text = input.MaximumSimulations;

                parameterRows.Clear();
                foreach (TasOptimisationParameterRow tasOptimisationParameterRow in input.Parameters)
                {
                    parameterRows.Add(tasOptimisationParameterRow);
                }

                objectiveRows.Clear();
                foreach (TasOptimisationObjectiveRow tasOptimisationObjectiveRow in input.Objectives)
                {
                    objectiveRows.Add(tasOptimisationObjectiveRow);
                }
            }
            finally
            {
                loading = false;
            }

            SyncObjectives();
            UpdateAlgorithm();
        }

        /// <summary>
        /// Rebuilds the Objective box, the objective's description and unit, and the Recorded outputs list from the output
        /// rows: the row flagged <see cref="TasOptimisationObjectiveRow.Primary"/> is the objective, the others are recorded.
        /// </summary>
        private void SyncObjectives()
        {
            recordedRows.Clear();
            foreach (TasOptimisationObjectiveRow tasOptimisationObjectiveRow in objectiveRows.Where(x => !x.Primary))
            {
                recordedRows.Add(tasOptimisationObjectiveRow);
            }

            TasOptimisationObjectiveRow? primary = objectiveRows.FirstOrDefault(x => x.Primary);
            grid_Objective.DataContext = primary;
            grid_Objective.IsEnabled = primary != null && !running;

            UpdateObjectiveChoices(true);
        }

        /// <summary>
        /// Brings the Objective box up to date with the output names: its list is the named outputs and its text the
        /// objective's name. Without <paramref name="force"/> nothing is touched while the list is already right.
        /// </summary>
        private void UpdateObjectiveChoices(bool force = false)
        {
            List<string> names = objectiveRows.Select(x => x.Name?.Trim() ?? string.Empty).Where(x => x.Length != 0).Distinct().ToList();
            string objective = objectiveRows.FirstOrDefault(x => x.Primary)?.Name?.Trim() ?? string.Empty;

            if (!force && names.SequenceEqual(objectiveChoices) && comboBox_Objective.Text == objective)
            {
                return;
            }

            syncingObjective = true;
            try
            {
                objectiveChoices.Clear();
                foreach (string name in names)
                {
                    objectiveChoices.Add(name);
                }

                comboBox_Objective.SelectedItem = objective.Length != 0 && names.Contains(objective) ? objective : null;
                comboBox_Objective.Text = objective;
            }
            finally
            {
                syncingObjective = false;
            }
        }

        /// <summary>
        /// The Objective box names the output to minimise. A name that matches one of the outputs, ignoring case, makes
        /// that output the objective (under its own spelling). Any other name is a new output: it becomes the objective
        /// and the previous objective stays, as a recorded output. Nothing is renamed or removed.
        /// </summary>
        internal void CommitObjective(string? text)
        {
            if (loading || running || syncingObjective)
            {
                return;
            }

            string name = text?.Trim() ?? string.Empty;
            if (name.Length != 0)
            {
                TasOptimisationObjectiveRow? match = objectiveRows.FirstOrDefault(x => string.Equals(x.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    match = new TasOptimisationObjectiveRow(name, true);
                    objectiveRows.Add(match);
                }

                foreach (TasOptimisationObjectiveRow tasOptimisationObjectiveRow in objectiveRows)
                {
                    tasOptimisationObjectiveRow.Primary = tasOptimisationObjectiveRow == match;
                }
            }

            SyncObjectives();
            Refresh();
        }

        /// <summary>Shows the selected algorithm's settings and marks Start and Step as not applicable for golden section.</summary>
        private void UpdateAlgorithm()
        {
            TasOptimisationInput input = Read();
            input.UpdateApplicability();

            bool goldenSection = input.OptimisationAlgorithm == OptimisationAlgorithm.GoldenSection;
            grid_GoldenSection.Visibility = goldenSection ? Visibility.Visible : Visibility.Collapsed;
            grid_HookeJeeves.Visibility = goldenSection ? Visibility.Collapsed : Visibility.Visible;
            grid_HookeJeevesAdvanced.Visibility = goldenSection ? Visibility.Collapsed : Visibility.Visible;

            textBlock_Algorithm.Text = goldenSection
                ? "Narrows the range of one design variable until the objective values differ by less than the tolerance."
                : "Starts at the start values and moves in steps, reducing the step as it converges.";

            textBlock_Parameters.Text = goldenSection
                ? "Golden section uses only the minimum and maximum; start and step are not used."
                : "The search starts at the start value and moves by the step, staying within the minimum and maximum.";
        }

        private void Refresh()
        {
            if (loading)
            {
                return;
            }

            TasOptimisationInput input = Read();

            Exception? exception = Query.TasOptimisationAssemblyFailure();
            if (exception != null)
            {
                tasOptimisationChecks = new List<TasOptimisationCheck> { new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "SAM optimiser", Query.TasOptimisationLoadFailure(exception)) };
            }
            else
            {
                tasOptimisationChecks = input.TasOptimisationChecks(TasGenExecutePath);
            }

            itemsControl_Checks.ItemsSource = tasOptimisationChecks;
            UpdateDiagnostics();

            UpdateObjectiveChoices();
            TasOptimisationObjectiveRow? primary = input.PrimaryObjective;
            textBlock_Objectives.Text = primary == null || string.IsNullOrWhiteSpace(primary.Name)
                ? "Pick one of the outputs, or type the name of the output the Tas script writes."
                : "The optimiser looks for the design-variable values that give the lowest " + primary.Name.Trim() + ".";

            bool canRun = tasOptimisationChecks.CanRun();
            button_Run.IsEnabled = !running && canRun;

            if (running)
            {
                return;
            }

            TasOptimisationCheck? blocked = tasOptimisationChecks.FirstOrDefault(x => x.Status == TasOptimisationCheckStatus.Blocked);
            if (blocked != null)
            {
                string detail = blocked.Detail.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                textBlock_NextStep.Text = "✕ Before running - " + blocked.Title + ": " + detail;
                button_Run.ToolTip = textBlock_NextStep.Text;
            }
            else
            {
                string summary = tasOptimisationChecks.FirstOrDefault(x => x.Title == "Setup")?.Detail ?? string.Empty;
                textBlock_NextStep.Text = tasOptimisationChecks.Any(x => x.Status == TasOptimisationCheckStatus.Warning)
                    ? "⚠ Ready, with warnings: " + summary + " See Checks on the Setup tab."
                    : "✓ Ready: " + summary;
                button_Run.ToolTip = "Run the optimisation. Each simulation is a full Tas run, so this can take many minutes.";
            }
        }

        /// <summary>
        /// The implementation detail behind the window, for support and for a stale installation: the Tas optimisation
        /// engine's path, the assemblies that were loaded, how simulations are run, and the kernel's own notes on the last
        /// run. It is not part of a readiness line or of a result.
        /// </summary>
        private void UpdateDiagnostics()
        {
            string path_TasGenExecute = string.IsNullOrWhiteSpace(TasGenExecutePath) ? Analytical.Tas.GenOpt.Query.TasGenOptExecutePath() : TasGenExecutePath!;

            List<string> lines = new List<string>
            {
                "Tas optimisation engine (TasGenExecute): " + path_TasGenExecute,
                Query.TasOptimisationAssemblyLocations(),
                "How simulations run: the SAM optimiser runs one Tas simulation at a time, each with TasGenExecute.exe in a fresh folder of the run, and retries a failed simulation once. Each output is read from the line 'name::value' that TasGenExecute writes to Output.txt.",
            };

            if (!string.IsNullOrWhiteSpace(Report?.DiagnosticsText))
            {
                lines.Add("Last run: " + Report!.DiagnosticsText.Replace(Environment.NewLine, "; "));
            }

            textBox_Diagnostics.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
        }

        private void BeginRun(OptimisationDefinition optimisationDefinition)
        {
            traceRows.Clear();
            Report = null;

            dataGrid_Trace.Columns.Clear();
            dataGrid_Trace.Columns.Add(Column("Simulation", "Simulation"));

            //The search's own bookkeeping: shown on request only.
            dataGrid_Trace.Columns.Add(Column("Iteration", "Iteration"));
            dataGrid_Trace.Columns.Add(Column("Event", "Event"));
            UpdateSearchDetails();

            List<string> names_Parameter = optimisationDefinition.TasOptimisationVariableNames();
            for (int i = 0; i < names_Parameter.Count; i++)
            {
                dataGrid_Trace.Columns.Add(Column(names_Parameter[i], string.Format(CultureInfo.InvariantCulture, "Coordinates[{0}]", i)));
            }

            List<string> names_Objective = optimisationDefinition.TasOptimisationObjectiveNames();
            for (int i = 0; i < names_Objective.Count; i++)
            {
                dataGrid_Trace.Columns.Add(Column(i == 0 ? names_Objective[i] + " (objective)" : names_Objective[i], string.Format(CultureInfo.InvariantCulture, "Outputs[{0}]", i)));
            }

            stackPanel_Run.Visibility = Visibility.Visible;
            textBlock_RunEmpty.Visibility = Visibility.Collapsed;
            border_Result.Visibility = Visibility.Collapsed;
            progressBar_Run.IsIndeterminate = true;
            textBlock_RunStatus.Text = "Running: " + optimisationDefinition.Method.Algorithm.TasOptimisationAlgorithmName() + " on " + string.Join(", ", names_Parameter) + ", minimising " + names_Objective.FirstOrDefault() + ".";
            textBlock_RunNote.Visibility = Visibility.Visible;

            stopwatch.Restart();
            dispatcherTimer_Elapsed.Start();

            SetRunning(true);
            UpdateRunText();

            //The progress is on its own tab: show it.
            tabControl_Main.SelectedItem = tabItem_Run;
        }

        private void UpdateSearchDetails()
        {
            Visibility visibility = checkBox_SearchDetails.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            foreach (DataGridColumn dataGridColumn in dataGrid_Trace.Columns.Where(x => (x.Header as string) == "Iteration" || (x.Header as string) == "Event"))
            {
                dataGridColumn.Visibility = visibility;
            }
        }

        private void UpdateRunText()
        {
            TasOptimisationProgressState? state = tasOptimisationProgressState;
            if (state == null)
            {
                return;
            }

            string elapsed = stopwatch.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

            if (state.Last == null)
            {
                textBlock_RunSimulation.Text = (running ? "Running the first Tas simulations" : "No point was reported") + " · " + elapsed;
                textBlock_RunLast.Text = string.Empty;
                textBlock_RunLowest.Text = string.Empty;
                return;
            }

            textBlock_RunSimulation.Text = state.SimulationText() + " · " + elapsed + (running ? " · Tas simulation running" : string.Empty);
            textBlock_RunLast.Text = "Last reported: " + state.LastText();
            textBlock_RunLowest.Text = "Best so far: " + state.LowestText();
        }

        private void ShowResult(TasOptimisationReport tasOptimisationReport)
        {
            Report = tasOptimisationReport;

            stackPanel_Run.Visibility = tasOptimisationProgressState == null ? Visibility.Collapsed : Visibility.Visible;
            textBlock_RunEmpty.Visibility = Visibility.Collapsed;
            progressBar_Run.IsIndeterminate = false;
            progressBar_Run.Value = progressBar_Run.Maximum;
            textBlock_RunNote.Visibility = Visibility.Collapsed;
            textBlock_RunStatus.Text = tasOptimisationReport.Headline;
            UpdateRunText();

            border_Result.Visibility = Visibility.Visible;
            textBlock_ResultHeadline.Text = tasOptimisationReport.Headline;
            textBlock_ResultGlyph.Text = new TasOptimisationCheck(tasOptimisationReport.Status, string.Empty, string.Empty).Glyph;
            textBlock_ResultGlyph.Foreground = (System.Windows.Media.Brush)FindResource(tasOptimisationReport.Status switch
            {
                TasOptimisationCheckStatus.Ready => "PartO.Brush.Success",
                TasOptimisationCheckStatus.Warning => "PartO.Brush.Warning",
                _ => "PartO.Brush.Danger",
            });
            itemsControl_Result.ItemsSource = tasOptimisationReport.Lines;
            button_OpenRunFolder.IsEnabled = !string.IsNullOrWhiteSpace(tasOptimisationReport.RunDirectory) && Directory.Exists(tasOptimisationReport.RunDirectory);

            //The result is on the Run & Results tab, also when the run never started: show it.
            tabControl_Main.SelectedItem = tabItem_Run;

            Refresh();
        }

        private void SetRunning(bool value)
        {
            running = value;

            foreach (UIElement uIElement in new UIElement[]
            {
                comboBox_Example, button_LoadExample, textBox_Directory, button_Directory, textBox_ScriptPath, button_ScriptPath,
                comboBox_Algorithm, grid_GoldenSection, grid_HookeJeeves, grid_HookeJeevesAdvanced, textBox_MaximumSimulations,
                button_AddParameter, itemsControl_Parameters, comboBox_Objective, grid_Objective, button_AddObjective, itemsControl_Objectives,
                textBox_RunsDirectory, button_RunsDirectory,
            })
            {
                uIElement.IsEnabled = !value;
            }

            grid_Objective.IsEnabled = !value && grid_Objective.DataContext != null;

            button_Cancel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            button_Cancel.IsEnabled = value;
            button_Cancel.Content = "Cancel run";
            button_Run.IsEnabled = !value && tasOptimisationChecks.CanRun();
            textBlock_NextStep.Text = value ? "Running. Each simulation is a full Tas run; Cancel stops after the current one." : textBlock_NextStep.Text;
        }

        private static DataGridTextColumn Column(string header, string path)
        {
            return new DataGridTextColumn()
            {
                Header = header,
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
            };
        }

        private void TasOptimisationWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!running)
            {
                session = new TasOptimisationInput(Read());
                return;
            }

            e.Cancel = true;
            if (closeWhenFinished)
            {
                return;
            }

            bool stop = (ConfirmStop ?? ConfirmStop_Default)();
            if (stop && running)
            {
                closeWhenFinished = true;
                RequestCancel();
            }
        }

        private bool ConfirmStop_Default()
        {
            return MessageBox.Show(
                this,
                "Stop the optimisation?\n\nThe running Tas simulation finishes first - it is never interrupted - then the run stops and this window closes. The completed simulations stay in the run folder.",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private void Input_Changed(object sender, RoutedEventArgs e)
        {
            if (loading || running)
            {
                return;
            }

            dispatcherTimer_Refresh.Stop();
            dispatcherTimer_Refresh.Start();
        }

        private void comboBox_Algorithm_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loading)
            {
                return;
            }

            UpdateAlgorithm();
            Refresh();
        }

        private void comboBox_Objective_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (syncingObjective || !(comboBox_Objective.SelectedItem is string name))
            {
                return;
            }

            CommitObjective(name);
        }

        private void comboBox_Objective_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            //Opening the list moves the focus into it: that is not the end of an edit.
            if (comboBox_Objective.IsDropDownOpen)
            {
                return;
            }

            CommitObjective(comboBox_Objective.Text);
        }

        private void comboBox_Objective_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                //Enter ends the edit; it must not start the run through the default button.
                e.Handled = true;
                CommitObjective(comboBox_Objective.Text);
            }
        }

        private void checkBox_SearchDetails_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSearchDetails();
        }

        private void button_LoadExample_Click(object sender, RoutedEventArgs e)
        {
            if (running || !(comboBox_Example.SelectedItem is ComboBoxItem comboBoxItem) || !(comboBoxItem.Tag is TasOptimisationExample tasOptimisationExample))
            {
                return;
            }

            TasOptimisationInput input = Read();
            try
            {
                input.Load(tasOptimisationExample);
            }
            catch (Exception exception) when (Query.IsTasOptimisationLoadFailure(exception))
            {
                ShowResult(new TasOptimisationReport(exception));
                return;
            }

            Load(input);
            Refresh();
        }

        private void button_Directory_Click(object sender, RoutedEventArgs e)
        {
            string? path = SelectFolder(textBox_Directory.Text, "Select the Tas project folder (the folder that holds the Tas files the script works on)");
            if (path != null)
            {
                textBox_Directory.Text = path;
            }
        }

        private void button_RunsDirectory_Click(object sender, RoutedEventArgs e)
        {
            string? path = SelectFolder(string.IsNullOrWhiteSpace(textBox_RunsDirectory.Text) ? textBox_Directory.Text : textBox_RunsDirectory.Text, "Select the folder for the run folders (keep the path short)");
            if (path != null)
            {
                textBox_RunsDirectory.Text = path;
            }
        }

        private void button_ScriptPath_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Select the Tas script",
                Filter = "Script (*.txt;*.cs)|*.txt;*.cs|All files (*.*)|*.*",
            };

            string directory = !string.IsNullOrWhiteSpace(textBox_ScriptPath.Text) ? Path.GetDirectoryName(textBox_ScriptPath.Text.Trim()) ?? string.Empty : textBox_Directory.Text.Trim();
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                openFileDialog.InitialDirectory = directory;
            }

            if (openFileDialog.ShowDialog(this) == true)
            {
                textBox_ScriptPath.Text = openFileDialog.FileName;
            }
        }

        private static string? SelectFolder(string? current, string description)
        {
            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowserDialog.Description = description;
                folderBrowserDialog.UseDescriptionForTitle = true;
                if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current!.Trim()))
                {
                    folderBrowserDialog.SelectedPath = current.Trim();
                }

                return folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? folderBrowserDialog.SelectedPath : null;
            }
        }

        private void button_AddParameter_Click(object sender, RoutedEventArgs e)
        {
            parameterRows.Add(new TasOptimisationParameterRow() { StartAndStepApplicable = Read().StartAndStepApplicable });
            Refresh();
        }

        private void button_RemoveParameter_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TasOptimisationParameterRow tasOptimisationParameterRow)
            {
                parameterRows.Remove(tasOptimisationParameterRow);
                Refresh();
            }
        }

        private void button_AddObjective_Click(object sender, RoutedEventArgs e)
        {
            //A new output is recorded; the Objective box says which output is minimised.
            TasOptimisationObjectiveRow tasOptimisationObjectiveRow = new TasOptimisationObjectiveRow(string.Empty, false);
            objectiveRows.Add(tasOptimisationObjectiveRow);
            recordedRows.Add(tasOptimisationObjectiveRow);
            Refresh();
        }

        private void button_RemoveObjective_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TasOptimisationObjectiveRow tasOptimisationObjectiveRow)
            {
                RemoveOutput(tasOptimisationObjectiveRow);
            }
        }

        /// <summary>
        /// Removes an output. Removing the objective makes the first remaining output the objective, so a form that had
        /// one still has one.
        /// </summary>
        internal void RemoveOutput(TasOptimisationObjectiveRow tasOptimisationObjectiveRow)
        {
            if (running || !objectiveRows.Remove(tasOptimisationObjectiveRow))
            {
                return;
            }

            if (tasOptimisationObjectiveRow.Primary && objectiveRows.Count != 0)
            {
                objectiveRows[0].Primary = true;
            }

            SyncObjectives();
            Refresh();
        }

        private async void button_Run_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RunAsync();
            }
            catch (Exception exception) when (Query.IsTasOptimisationLoadFailure(exception))
            {
                SetRunning(false);
                ShowResult(new TasOptimisationReport(exception));
            }
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            RequestCancel();
        }

        private void button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void button_OpenRunFolder_Click(object sender, RoutedEventArgs e)
        {
            string? directory = Report?.RunDirectory;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(directory!) { UseShellExecute = true });
            }
            catch (Exception exception) when (exception is Win32Exception || exception is InvalidOperationException)
            {
                MessageBox.Show(this, "The run folder could not be opened: " + exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void button_CopySummary_Click(object sender, RoutedEventArgs e)
        {
            if (Report == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(Report.ToText());
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is busy; nothing is lost, the report stays on screen.
            }
        }
    }
}
