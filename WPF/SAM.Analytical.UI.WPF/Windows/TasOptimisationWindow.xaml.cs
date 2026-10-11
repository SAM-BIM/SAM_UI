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

        /// <summary>How the current run's values are shown, copied and exported (PR5b); null before the first run.</summary>
        private TasOptimisationFormatter? tasOptimisationFormatter;

        /// <summary>The name of the current run's definition, for the exported file name.</summary>
        private string? definitionName;
        private bool loading;
        private bool syncingObjective;
        private bool syncingAlgorithm;
        private bool running;
        private bool testing;
        private bool closeWhenFinished;

        // "Tas model" engine (PR8): what the window has read of the model, the glazing pool's default sources, the lists
        // built from the model's catalogue, the last AI reply, and the form of the other engine (kept while switching).
        private readonly TasModelSession tasModelSession;
        private readonly Func<IEnumerable<GlazingSource>> defaultGlazingSources;
        private readonly ObservableCollection<TasModelCatalogueItem> canChangeItems = new ObservableCollection<TasModelCatalogueItem>();
        private readonly ObservableCollection<TasModelCatalogueItem> canMeasureItems = new ObservableCollection<TasModelCatalogueItem>();
        private readonly Dictionary<string, TasOptimisationInput> engineForms = new Dictionary<string, TasOptimisationInput>(StringComparer.Ordinal);
        private OptimisationCatalogue? listedCatalogue;
        private bool glazingPoolRequested;
        private TasOptimisationAIReply? aiReply;

        public TasOptimisationWindow()
            : this(null)
        {
        }

        /// <param name="modelDirectory">
        /// The open model's folder, where Energy Simulation writes its Tas files by default. It becomes the Tas project
        /// folder when nothing is remembered from this session and it holds Tas files.
        /// </param>
        public TasOptimisationWindow(string? modelDirectory)
            : this(modelDirectory, null)
        {
        }

        /// <param name="modelDirectory">The open model's folder (see <see cref="TasOptimisationWindow(string)"/>).</param>
        /// <param name="analyticalModel">The open model: its glazing systems join the glazing pool of a glazing choice.</param>
        public TasOptimisationWindow(string? modelDirectory, AnalyticalModel? analyticalModel)
            : this(modelDirectory, analyticalModel, null, null)
        {
        }

        /// <param name="modelDirectory">The open model's folder.</param>
        /// <param name="analyticalModel">The open model, for the glazing pool.</param>
        /// <param name="tasModelSession">What is read of the Tas model (tests give one with stand-in readers); null for the licensed readers.</param>
        /// <param name="defaultGlazingSources">The glazing pool's sources (tests); null for the model, the default library and "My glazing systems".</param>
        internal TasOptimisationWindow(string? modelDirectory, AnalyticalModel? analyticalModel, TasModelSession? tasModelSession, Func<IEnumerable<GlazingSource>>? defaultGlazingSources)
        {
            InitializeComponent();

            //The content scrolls, so on a small or scaled display (1080p at 150% leaves 688 DIP) the window simply starts
            //no taller than the work area, with the actions still in view.
            Height = System.Math.Max(MinHeight, System.Math.Min(Height, SystemParameters.WorkArea.Height));

            this.tasModelSession = tasModelSession ?? new TasModelSession();
            this.defaultGlazingSources = defaultGlazingSources ?? (() => DefaultGlazingSources(analyticalModel));

            comboBox_Example.Items.Add(new ComboBoxItem() { Content = "Systems Demo – golden section", Tag = TasOptimisationExample.SystemsDemoGoldenSection });
            comboBox_Example.Items.Add(new ComboBoxItem() { Content = "Systems Demo – Hooke–Jeeves", Tag = TasOptimisationExample.SystemsDemoHookeJeeves });
            comboBox_Example.SelectedIndex = 0;

            foreach (string engine in Query.TasOptimisationEngines)
            {
                comboBox_Engine.Items.Add(new ComboBoxItem() { Content = Query.TasOptimisationEngineName(engine), Tag = engine });
            }

            comboBox_Algorithm.ItemsSource = TasOptimisationInput.OptimisationAlgorithms;

            itemsControl_Parameters.ItemsSource = parameterRows;
            itemsControl_Objectives.ItemsSource = recordedRows;
            comboBox_Objective.ItemsSource = objectiveChoices;
            dataGrid_Trace.ItemsSource = traceRows;
            itemsControl_CanChange.ItemsSource = canChangeItems;
            itemsControl_CanMeasure.ItemsSource = canMeasureItems;

            if (session != null)
            {
                tasOptimisationInput = new TasOptimisationInput(session);
            }
            else
            {
                bool proposed = !string.IsNullOrWhiteSpace(modelDirectory) && Query.TasOptimisationTasFiles(modelDirectory!).Count != 0;
                bool tbd = proposed && Query.TasOptimisationTasFiles(modelDirectory!).Any(x => string.Equals(Path.GetExtension(x), ".tbd", StringComparison.OrdinalIgnoreCase));

                try
                {
                    //A model simulated by Energy Simulation (it has a TBD) opens on the "Tas model" engine: nothing to
                    //write. Otherwise the proven Systems Demo script example, as before.
                    tasOptimisationInput = tbd ? TasOptimisationInput.CreateTasModel() : TasOptimisationInput.Create(TasOptimisationInput.DefaultExample);
                }
                catch (Exception exception) when (Query.IsTasOptimisationLoadFailure(exception))
                {
                    //A stale optimisation assembly: the window still opens, and its readiness list says what is wrong.
                    tasOptimisationInput = new TasOptimisationInput();
                }

                if (proposed)
                {
                    tasOptimisationInput.Directory = modelDirectory!;
                }
            }

            UpdateGlazingFilterFields();
            this.tasModelSession.Changed += TasModelSession_Changed;

            //The model is read (licensed Tas) only once the window is on screen.
            Loaded += (s, e) => Refresh();

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

        /// <summary>What the window has read of the Tas model ("tas-model" engine).</summary>
        internal TasModelSession ModelSession => tasModelSession;

        /// <summary>The last model read the window started (with the glazing pool after it); completed when none.</summary>
        internal Task ModelTask { get; private set; } = Task.CompletedTask;

        /// <summary>The "Can change" list.</summary>
        internal IReadOnlyList<TasModelCatalogueItem> CanChangeItems => canChangeItems;

        /// <summary>The "Can measure" list.</summary>
        internal IReadOnlyList<TasModelCatalogueItem> CanMeasureItems => canMeasureItems;

        /// <summary>The last "Test one simulation"; null before the first.</summary>
        public TasOptimisationTestReport? TestReport { get; private set; }

        /// <summary>The last AI reply read; null before the first.</summary>
        internal TasOptimisationAIReply? AIReply => aiReply;

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

            Func<IProgress<OptimisationProgress>, CancellationToken, Task<NativeGenOptRun>> run;
            if (input.IsTasModel)
            {
                //"Tas model": SAM_Tas' runner, on the model as read (inventory, glazing pool and filter); it checks the
                //definition, resolves the options and generates the script before any folder is created. The run writes
                //the glazing systems into its snapshot TBD (Tas COM), so it runs on its own STA thread.
                TasModelRunner tasModelRunner;
                try
                {
                    tasModelRunner = tasModelSession.CreateRunner(optimisationDefinition, directory, runsDirectory, tasGenExecutePath);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException)
                {
                    ShowResult(new TasOptimisationReport(exception));
                    return;
                }

                run = (progress_Run, cancellationToken_Run) => TasModelSession.RunSta(() => tasModelRunner.Run(progress_Run, cancellationToken_Run), "SAM optimisation run");
            }
            else
            {
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

                run = (progress_Run, cancellationToken_Run) => Task.Run(() => genOptDocument.RunNative(runsDirectory, tasGenExecutePath, progress_Run, cancellationToken_Run));
            }

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            //Display only: the definition's declared units and SAM's engineering formatting; every value stays full precision.
            TasOptimisationFormatter tasOptimisationFormatter = new TasOptimisationFormatter(optimisationDefinition);
            this.tasOptimisationFormatter = tasOptimisationFormatter;
            definitionName = optimisationDefinition.Name;

            TasOptimisationProgressState tasOptimisationProgressState = new TasOptimisationProgressState(optimisationDefinition.TasOptimisationVariableNames(), optimisationDefinition.TasOptimisationObjectiveNames(), tasOptimisationFormatter);
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
                NativeGenOptRun nativeGenOptRun = await run(progress, cancellationToken);
                tasOptimisationReport = new TasOptimisationReport(nativeGenOptRun, cancellationToken.IsCancellationRequested, tasOptimisationFormatter);
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
                comboBox_Engine.SelectedItem = comboBox_Engine.Items.OfType<ComboBoxItem>().FirstOrDefault(x => (x.Tag as string) == input.Engine);
                textBox_Directory.Text = input.Directory;
                textBox_ScriptPath.Text = input.ScriptPath;
                textBox_RunsDirectory.Text = input.RunsDirectory;
                comboBox_Algorithm.ItemsSource = input.OfferedAlgorithms.Contains(input.OptimisationAlgorithm) ? input.OfferedAlgorithms : input.OfferedAlgorithms.Concat(new[] { input.OptimisationAlgorithm }).ToList();
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
            UpdateEngine();
            UpdateAlgorithm();
        }

        /// <summary>
        /// Shows what the engine needs: "Tas model" has the project folder only (no script), the lists read from the model,
        /// the AI assistant, the generated script and Test one simulation; "Tas script" has the script and the examples,
        /// exactly as before.
        /// </summary>
        private void UpdateEngine()
        {
            bool tasModel = tasOptimisationInput.IsTasModel;
            Visibility visibility_TasModel = tasModel ? Visibility.Visible : Visibility.Collapsed;
            Visibility visibility_TasScript = tasModel ? Visibility.Collapsed : Visibility.Visible;

            grid_Example.Visibility = visibility_TasScript;
            textBlock_ScriptPath.Visibility = visibility_TasScript;
            textBox_ScriptPath.Visibility = visibility_TasScript;
            button_ScriptPath.Visibility = visibility_TasScript;
            stackPanel_TasModel.Visibility = visibility_TasModel;
            stackPanel_GeneratedScript.Visibility = visibility_TasModel;
            button_TestSimulation.Visibility = visibility_TasModel;
            if (!tasModel)
            {
                border_Test.Visibility = Visibility.Collapsed;
            }

            textBlock_Engine.Text = tasModel
                ? "Simulation engine: Tas. SAM changes the model and reads the results itself (it writes the Tas script from tested blocks); pick below what may change and what to measure."
                : "Simulation engine: Tas (script)";
            textBlock_Intro.Text = tasModel
                ? "Finds the design-variable values that give the lowest objective result. Each simulation changes a copy of the Tas model and simulates it; your files are not changed."
                : "Finds the design-variable values that give the lowest objective result. Each simulation runs your Tas script on a copy of the Tas project; your files are not changed.";

            UpdateModelSection();
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
            bool tryEveryOption = input.OptimisationAlgorithm == OptimisationAlgorithm.TryEveryOption;
            grid_GoldenSection.Visibility = goldenSection ? Visibility.Visible : Visibility.Collapsed;
            grid_HookeJeeves.Visibility = goldenSection || tryEveryOption ? Visibility.Collapsed : Visibility.Visible;
            grid_HookeJeevesAdvanced.Visibility = goldenSection || tryEveryOption ? Visibility.Collapsed : Visibility.Visible;

            if (tryEveryOption)
            {
                textBlock_Algorithm.Text = "Runs one simulation per option and keeps the option with the lowest objective. It has no settings.";
                textBlock_Parameters.Text = "A choice is numbered 1 to the number of options (1 is the model as it is); start and step are not used.";
                return;
            }

            textBlock_Algorithm.Text = goldenSection
                ? "Narrows the range of one design variable until the objective values differ by less than the tolerance."
                : "Starts at the start values and moves in steps, reducing the step as it converges.";

            textBlock_Parameters.Text = goldenSection
                ? "Golden section uses only the minimum and maximum; start and step are not used."
                : "The search starts at the start value and moves by the step, staying within the minimum and maximum.";
        }

        /// <summary>
        /// Brings the Method list up to date with the methods the engine runs for the design variables now in the form
        /// (<see cref="TasOptimisationInput.OfferedAlgorithms"/>) and keeps the selection one of them: a choice gives try
        /// every option, values give golden section (one variable) and Hooke–Jeeves.
        /// </summary>
        private void UpdateAlgorithmList()
        {
            TasOptimisationInput input = Read();
            IReadOnlyList<OptimisationAlgorithm> offered = input.OfferedAlgorithms;
            input.EnsureOfferedAlgorithm();

            syncingAlgorithm = true;
            try
            {
                if (!(comboBox_Algorithm.ItemsSource is IEnumerable<OptimisationAlgorithm> current) || !current.SequenceEqual(offered))
                {
                    comboBox_Algorithm.ItemsSource = offered;
                }

                comboBox_Algorithm.SelectedItem = input.OptimisationAlgorithm;
            }
            finally
            {
                syncingAlgorithm = false;
            }

            UpdateAlgorithm();
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
                if (input.IsTasModel)
                {
                    ApplyGlazingFilterFields();
                    ReadModelWhenNeeded(input);
                }

                tasOptimisationChecks = input.TasOptimisationChecks(TasGenExecutePath, input.IsTasModel ? tasModelSession : null);
            }

            itemsControl_Checks.ItemsSource = tasOptimisationChecks;
            UpdateDiagnostics();

            UpdateObjectiveChoices();
            TasOptimisationObjectiveRow? primary = input.PrimaryObjective;
            textBlock_Objectives.Text = primary == null || string.IsNullOrWhiteSpace(primary.Name)
                ? "Pick one of the outputs, or type the name of the output the Tas script writes."
                : "The optimiser looks for the design-variable values that give the lowest " + primary.Name.Trim() + ".";

            bool canRun = tasOptimisationChecks.CanRun();
            button_Run.IsEnabled = !running && !testing && canRun;
            button_TestSimulation.IsEnabled = input.IsTasModel && !running && !testing && canRun;
            button_TestSimulation.ToolTip = button_TestSimulation.IsEnabled || testing
                ? "Run one Tas simulation at the start values (option 1 for a choice): its outputs and how long it takes, so the run's duration can be estimated."
                : "Available when the checks allow a run.";
            button_UseReply.IsEnabled = !running && aiReply?.CanUse == true;

            if (running || testing)
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

            //Each value shown rounded with its unit; its tooltip and its copied cell are the full-precision value.
            List<string> names_Parameter = optimisationDefinition.TasOptimisationVariableNames();
            for (int i = 0; i < names_Parameter.Count; i++)
            {
                dataGrid_Trace.Columns.Add(ValueColumn(names_Parameter[i], string.Format(CultureInfo.InvariantCulture, "CoordinateTexts[{0}]", i), string.Format(CultureInfo.InvariantCulture, "CoordinateRaw[{0}]", i)));
            }

            List<string> names_Objective = optimisationDefinition.TasOptimisationObjectiveNames();
            for (int i = 0; i < names_Objective.Count; i++)
            {
                dataGrid_Trace.Columns.Add(ValueColumn(i == 0 ? names_Objective[i] + " (objective)" : names_Objective[i], string.Format(CultureInfo.InvariantCulture, "OutputTexts[{0}]", i), string.Format(CultureInfo.InvariantCulture, "OutputRaw[{0}]", i)));
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
            button_CopyTrace.IsEnabled = traceRows.Count > 0 && tasOptimisationFormatter != null;
            button_ExportTrace.IsEnabled = button_CopyTrace.IsEnabled;

            //The result is on the Run & Results tab, also when the run never started: show it.
            tabControl_Main.SelectedItem = tabItem_Run;

            Refresh();
        }

        private void SetRunning(bool value)
        {
            running = value;

            foreach (UIElement uIElement in new UIElement[]
            {
                comboBox_Engine, button_OpenDefinition, stackPanel_TasModel, button_TestSimulation,
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

        /// <summary>A number column: the engineering text, right-aligned, with the full-precision value as its tooltip and its copied content.</summary>
        private static DataGridTextColumn ValueColumn(string header, string textPath, string rawPath)
        {
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Right));
            style.Setters.Add(new Setter(ToolTipProperty, new Binding(rawPath) { Mode = BindingMode.OneWay }));

            return new DataGridTextColumn()
            {
                Header = header,
                Binding = new Binding(textPath) { Mode = BindingMode.OneWay },
                ClipboardContentBinding = new Binding(rawPath) { Mode = BindingMode.OneWay },
                ElementStyle = style,
            };
        }

        /// <summary>The whole trace as tab-separated full-precision text with a header (Copy trace); empty before a run.</summary>
        internal string TraceText()
        {
            return tasOptimisationFormatter == null ? string.Empty : tasOptimisationFormatter.TabText(traceRows.Select(x => x.Entry));
        }

        /// <summary>Writes the whole trace as CSV (UTF-8 with a byte order mark, so Excel reads units such as °C).</summary>
        internal void ExportTrace(string path)
        {
            if (tasOptimisationFormatter == null)
            {
                return;
            }

            System.IO.File.WriteAllText(path, tasOptimisationFormatter.CsvText(traceRows.Select(x => x.Entry)), new System.Text.UTF8Encoding(true));
        }

        private void TasOptimisationWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (testing)
            {
                //The test simulation cannot be interrupted; its result would have nowhere to go.
                e.Cancel = true;
                MessageBox.Show(this, "A test simulation is running. Close the window when it has finished.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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
            if (loading || syncingAlgorithm)
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
            UpdateAlgorithmList();
            Refresh();
        }

        private void button_RemoveParameter_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TasOptimisationParameterRow tasOptimisationParameterRow)
            {
                RemoveParameter(tasOptimisationParameterRow);
            }
        }

        /// <summary>Removes a design variable; the Method list then offers what the remaining variables can run.</summary>
        internal void RemoveParameter(TasOptimisationParameterRow tasOptimisationParameterRow)
        {
            if (running || !parameterRows.Remove(tasOptimisationParameterRow))
            {
                return;
            }

            UpdateAlgorithmList();
            Refresh();
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

        // ============================== "TAS MODEL" ENGINE (PR8) ==============================

        /// <summary>The glazing pool's sources, as the Glazing window has them: the open model, the default library and "My glazing systems".</summary>
        private static IEnumerable<GlazingSource> DefaultGlazingSources(AnalyticalModel? analyticalModel)
        {
            List<GlazingSource> result = new List<GlazingSource>();
            if (analyticalModel != null)
            {
                result.Add(GlazingSource.FromModel(analyticalModel));
            }

            try
            {
                result.Add(GlazingSource.FromDefaultLibrary());
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                result.Add(new GlazingSource(GlazingSourceKind.Library, "Default library", new ConstructionManager()) { Note = "The default library could not be read: " + exception.Message });
            }

            result.Add(GlazingSource.FromUserLibrary(UserGlazingLibrary.Shared));
            return result;
        }

        /// <summary>
        /// Starts reading the Tas model in the background when the "Tas model" engine has a project folder with a TBD or
        /// TSD that has not been read yet, and only while the window is on screen (Tas starts its own processes). A folder
        /// that failed is not read again until Read model is pressed.
        /// </summary>
        private void ReadModelWhenNeeded(TasOptimisationInput input)
        {
            if (!IsLoaded || running)
            {
                return;
            }

            string directory = input.Directory?.Trim() ?? string.Empty;
            if (directory.Length == 0 || !Directory.Exists(directory))
            {
                return;
            }

            if (tasModelSession.Folder != null && string.Equals(FullPath(tasModelSession.Folder), FullPath(directory), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Query.TasOptimisationTasFiles(directory).Any(x => string.Equals(Path.GetExtension(x), ".tbd", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(x), ".tsd", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            ReadModel(directory);
        }

        /// <summary>Reads the model of <paramref name="directory"/> (licensed Tas, in the background), then the glazing pool when the model has glazing.</summary>
        internal Task ReadModel(string directory)
        {
            ModelTask = ReadModelAsync(directory);
            return ModelTask;
        }

        private async Task ReadModelAsync(string directory)
        {
            await tasModelSession.ReadAsync(directory);

            if (tasModelSession.State == TasModelReadState.Ready && !glazingPoolRequested && (tasModelSession.Inventory?.GlazingConstructions.Count ?? 0) != 0)
            {
                glazingPoolRequested = true;
                await tasModelSession.AddGlazingSourcesAsync(defaultGlazingSources());
            }
        }

        private static string FullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException || exception is System.Security.SecurityException)
            {
                return path;
            }
        }

        private void TasModelSession_Changed(object? sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => TasModelSession_Changed(sender, e)));
                return;
            }

            UpdateModelSection();
            if (!loading)
            {
                dispatcherTimer_Refresh.Stop();
                dispatcherTimer_Refresh.Start();
            }
        }

        /// <summary>The "From the model" section: the read state, the Can change / Can measure lists and the glazing pool.</summary>
        private void UpdateModelSection()
        {
            if (!tasOptimisationInput.IsTasModel)
            {
                return;
            }

            TasModelReadState state = tasModelSession.State;
            progressBar_Model.Visibility = state == TasModelReadState.Reading || tasModelSession.PoolBusy ? Visibility.Visible : Visibility.Collapsed;
            progressBar_Model.IsIndeterminate = progressBar_Model.Visibility == Visibility.Visible;
            button_ReadModel.IsEnabled = state != TasModelReadState.Reading && !running;

            switch (state)
            {
                case TasModelReadState.NotRead:
                    textBlock_ModelStatus.Text = "Choose the Tas project folder: the model is read from its Tas files (a licensed Tas is needed), and what can change and what can be measured are listed here.";
                    break;

                case TasModelReadState.Reading:
                    textBlock_ModelStatus.Text = "Reading the Tas model (Tas opens the files read-only)…";
                    break;

                case TasModelReadState.Failed:
                    textBlock_ModelStatus.Text = "✕ " + (tasModelSession.Error ?? "The Tas model could not be read.");
                    break;

                default:
                    textBlock_ModelStatus.Text = Query.TasModelText(tasModelSession) + " Read in " + TasOptimisationTestReport.DurationText(tasModelSession.ReadDuration) + ".";
                    break;
            }

            OptimisationCatalogue? catalogue = state == TasModelReadState.Ready ? tasModelSession.Catalogue : null;
            if (!ReferenceEquals(catalogue, listedCatalogue))
            {
                listedCatalogue = catalogue;

                Dictionary<string, List<TasGlazingOption>> glazingOptions = new Dictionary<string, List<TasGlazingOption>>(StringComparer.Ordinal);
                foreach (KeyValuePair<TasGlazingConstructionInfo, List<TasGlazingOption>> keyValuePair in tasModelSession.GlazingOptions())
                {
                    glazingOptions[keyValuePair.Key.Name] = keyValuePair.Value;
                }

                // A typed threshold survives a new catalogue (another filter, the pool growing).
                Dictionary<string, string> parameterTexts = canMeasureItems.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First().ParameterText);

                canChangeItems.Clear();
                foreach (OptimisationCatalogueEntry optimisationCatalogueEntry in catalogue?.Variables ?? new List<OptimisationCatalogueEntry>())
                {
                    List<TasGlazingOption>? options = null;
                    if (optimisationCatalogueEntry.Target?.Kind == TasModelKind.GlazingChoice && optimisationCatalogueEntry.Target.Reference.TryGetValue(TasModelKind.GlazingConstructionKey, out string? glazingConstruction) && glazingConstruction != null)
                    {
                        glazingOptions.TryGetValue(glazingConstruction, out options);
                    }

                    canChangeItems.Add(new TasModelCatalogueItem(optimisationCatalogueEntry, options));
                }

                canMeasureItems.Clear();
                foreach (OptimisationCatalogueEntry optimisationCatalogueEntry in catalogue?.Outputs ?? new List<OptimisationCatalogueEntry>())
                {
                    TasModelCatalogueItem tasModelCatalogueItem = new TasModelCatalogueItem(optimisationCatalogueEntry);
                    if (parameterTexts.TryGetValue(tasModelCatalogueItem.Name, out string? parameterText))
                    {
                        tasModelCatalogueItem.ParameterText = parameterText;
                    }

                    canMeasureItems.Add(tasModelCatalogueItem);
                }
            }

            textBlock_CanChangeEmpty.Visibility = state == TasModelReadState.Ready && canChangeItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            textBlock_CanChangeEmpty.Text = "The model offers nothing to change: it has no internal condition with a heating or cooling setpoint, no glazing with options in the pool, and no plant controller.";
            textBlock_CanMeasureEmpty.Visibility = state == TasModelReadState.Ready && canMeasureItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            textBlock_CanMeasureEmpty.Text = "The model offers nothing to measure: it has no TBD or TSD.";

            UpdateGlazingPoolText();
        }

        private void UpdateGlazingPoolText()
        {
            List<string> lines = new List<string>();
            if (tasModelSession.Sources.Count == 0)
            {
                lines.Add(tasModelSession.State == TasModelReadState.Ready && (tasModelSession.Inventory?.GlazingConstructions.Count ?? 0) == 0
                    ? "The model has no glazing, so there is no glazing choice."
                    : "The pool is read with the model: the open model's glazing systems, the default library and My glazing systems.");
            }
            else
            {
                lines.Add("Pool: " + string.Join(", ", tasModelSession.Sources.Select(x => x.Label).Distinct()) + (tasModelSession.PoolBusy ? " (calculating with Tas…)" : string.Empty) + string.Format(CultureInfo.InvariantCulture, "; {0} glazing systems.", tasModelSession.Pool.Count));
            }

            lines.AddRange(tasModelSession.PoolNotes);
            textBlock_GlazingPool.Text = string.Join(Environment.NewLine, lines);

            List<string> filter = new List<string>();
            foreach (KeyValuePair<TasGlazingConstructionInfo, List<TasGlazingOption>> keyValuePair in tasModelSession.GlazingOptions())
            {
                filter.Add(keyValuePair.Value.Count < 2
                    ? string.Format(CultureInfo.InvariantCulture, "{0}: no pool system passes the filter, so it is not offered.", keyValuePair.Key.Name.Trim())
                    : string.Format(CultureInfo.InvariantCulture, "{0}: {1} options (the model's glazing and {2} from the pool).", keyValuePair.Key.Name.Trim(), keyValuePair.Value.Count, keyValuePair.Value.Count - 1));
            }

            if (glazingFilterProblem != null)
            {
                filter.Insert(0, "✕ " + glazingFilterProblem);
            }

            textBlock_GlazingFilter.Text = string.Join(Environment.NewLine, filter);
        }

        private string? glazingFilterProblem;

        /// <summary>Shows the session's glazing filter in its fields.</summary>
        private void UpdateGlazingFilterFields()
        {
            TasGlazingFilter filter = tasModelSession.GlazingFilter;
            bool loading_Previous = loading;
            loading = true;
            try
            {
                textBox_GlazingMinimumG.Text = filter.MinimumG == null ? string.Empty : TasOptimisationInput.Text(filter.MinimumG.Value);
                textBox_GlazingMaximumG.Text = filter.MaximumG == null ? string.Empty : TasOptimisationInput.Text(filter.MaximumG.Value);
                textBox_GlazingUgAllowance.Text = TasOptimisationInput.Text(filter.UgAllowance);
                textBox_GlazingLightAllowance.Text = TasOptimisationInput.Text(filter.LightAllowance);
                textBox_GlazingMaximumOptions.Text = filter.MaximumOptions.ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                loading = loading_Previous;
            }
        }

        /// <summary>
        /// Applies the glazing filter fields to the session when they read as a filter that differs from the current one
        /// (the catalogue's options change with it). A field that does not read leaves the filter as it was and says why.
        /// </summary>
        private void ApplyGlazingFilterFields()
        {
            List<string> problems = new List<string>();
            double? minimumG = OptionalNumber(textBox_GlazingMinimumG.Text, "g value from", problems);
            double? maximumG = OptionalNumber(textBox_GlazingMaximumG.Text, "g value to", problems);
            double? ugAllowance = OptionalNumber(textBox_GlazingUgAllowance.Text, "Ug allowance", problems);
            double? lightAllowance = OptionalNumber(textBox_GlazingLightAllowance.Text, "light allowance", problems);
            int maximumOptions = TasModelKind.MaximumGlazingOptions;
            if (!int.TryParse(textBox_GlazingMaximumOptions.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out maximumOptions) || maximumOptions < 2 || maximumOptions > TasModelKind.MaximumGlazingOptions)
            {
                problems.Add(string.Format(CultureInfo.InvariantCulture, "Most options must be a whole number from 2 to {0}.", TasModelKind.MaximumGlazingOptions));
            }

            if (minimumG != null && maximumG != null && minimumG > maximumG)
            {
                problems.Add("The lowest g value is above the highest.");
            }

            glazingFilterProblem = problems.Count == 0 ? null : string.Join(" ", problems) + " The filter is unchanged.";
            if (problems.Count == 0)
            {
                TasGlazingFilter current = tasModelSession.GlazingFilter;
                TasGlazingFilter filter = new TasGlazingFilter()
                {
                    MinimumG = minimumG,
                    MaximumG = maximumG,
                    UgAllowance = ugAllowance ?? new TasGlazingFilter().UgAllowance,
                    LightAllowance = lightAllowance ?? new TasGlazingFilter().LightAllowance,
                    MaximumOptions = maximumOptions,
                    ApertureType = current.ApertureType,
                };

                if (filter.MinimumG != current.MinimumG || filter.MaximumG != current.MaximumG || filter.UgAllowance != current.UgAllowance || filter.LightAllowance != current.LightAllowance || filter.MaximumOptions != current.MaximumOptions)
                {
                    tasModelSession.GlazingFilter = filter;
                    return;
                }
            }

            UpdateGlazingPoolText();
        }

        private static double? OptionalNumber(string? text, string label, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (TasOptimisationInput.TryNumber(text, out double value))
            {
                return value;
            }

            problems.Add(string.Format(CultureInfo.InvariantCulture, "The {0} '{1}' is not a number.", label, text!.Trim()));
            return null;
        }

        private void button_ReadModel_Click(object sender, RoutedEventArgs e)
        {
            string directory = textBox_Directory.Text?.Trim() ?? string.Empty;
            if (running || directory.Length == 0 || !Directory.Exists(directory))
            {
                return;
            }

            _ = ReadModel(directory);
        }

        private async void button_LoadGlazing_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Load glazing systems",
                Filter = "Glazing systems (*.tcd;*.json)|*.tcd;*.json|All files (*.*)|*.*",
            };

            if (openFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            GlazingSource glazingSource = await Query.ReadGlazingSourceAsync(openFileDialog.FileName, ApertureType.Window);
            await tasModelSession.AddGlazingSourcesAsync(new[] { glazingSource });
        }

        /// <summary>Adds a design variable bound to a "Can change" item; the Method list then offers what it can run.</summary>
        internal TasOptimisationParameterRow? AddTarget(TasModelCatalogueItem tasModelCatalogueItem)
        {
            if (running || tasModelCatalogueItem?.Entry?.Target == null)
            {
                return null;
            }

            TasOptimisationInput input = Read();
            TasOptimisationParameterRow result = input.AddTarget(tasModelCatalogueItem.Entry);
            parameterRows.Add(result);

            syncingAlgorithm = true;
            try
            {
                comboBox_Algorithm.ItemsSource = input.OfferedAlgorithms;
                comboBox_Algorithm.SelectedItem = input.OptimisationAlgorithm;
            }
            finally
            {
                syncingAlgorithm = false;
            }

            UpdateAlgorithm();
            Refresh();
            return result;
        }

        /// <summary>Adds an output bound to a "Can measure" item, with its typed parameter; the first output is the objective.</summary>
        internal TasOptimisationObjectiveRow? AddMeasure(TasModelCatalogueItem tasModelCatalogueItem)
        {
            if (running || tasModelCatalogueItem?.Entry?.Measure == null)
            {
                return null;
            }

            TasOptimisationInput input = Read();
            TasOptimisationObjectiveRow result = input.AddMeasure(tasModelCatalogueItem.Entry, tasModelCatalogueItem.Parameters());
            objectiveRows.Add(result);

            SyncObjectives();
            Refresh();
            return result;
        }

        private void button_AddTarget_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TasModelCatalogueItem tasModelCatalogueItem)
            {
                AddTarget(tasModelCatalogueItem);
            }
        }

        private void button_AddMeasure_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TasModelCatalogueItem tasModelCatalogueItem)
            {
                AddMeasure(tasModelCatalogueItem);
            }
        }

        /// <summary>
        /// Switches the engine. Each engine keeps its own form while the window is open, so switching back restores it;
        /// the first time, "Tas model" starts from an empty definition and "Tas script" from the default example. The Tas
        /// project folder, the runs folder and the script path are kept.
        /// </summary>
        internal void SwitchEngine(string engine)
        {
            TasOptimisationInput current = Read();
            if (running || string.IsNullOrWhiteSpace(engine) || current.Engine == engine)
            {
                return;
            }

            engineForms[current.Engine] = new TasOptimisationInput(current);

            TasOptimisationInput next;
            if (engineForms.TryGetValue(engine, out TasOptimisationInput? remembered))
            {
                next = new TasOptimisationInput(remembered);
            }
            else
            {
                try
                {
                    next = Query.IsTasModelEngine(engine) ? TasOptimisationInput.CreateTasModel() : TasOptimisationInput.Create(TasOptimisationInput.DefaultExample);
                }
                catch (Exception exception) when (Query.IsTasOptimisationLoadFailure(exception))
                {
                    next = new TasOptimisationInput() { Engine = engine };
                }
            }

            next.Directory = current.Directory;
            next.ScriptPath = current.ScriptPath;
            next.RunsDirectory = current.RunsDirectory;

            tasOptimisationInput = next;
            Load(tasOptimisationInput);
            Refresh();
        }

        private void comboBox_Engine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (loading || !(comboBox_Engine.SelectedItem is ComboBoxItem comboBoxItem) || !(comboBoxItem.Tag is string engine))
            {
                return;
            }

            SwitchEngine(engine);
        }

        /// <summary>
        /// Replaces the form with <paramref name="optimisationDefinition"/> (an opened file or an AI reply): its engine,
        /// variables, outputs, method and stopping. The Tas project folder, the runs folder and the script path are kept;
        /// the other engine's form is remembered.
        /// </summary>
        private void LoadDefinition(OptimisationDefinition optimisationDefinition)
        {
            TasOptimisationInput current = Read();
            engineForms[current.Engine] = new TasOptimisationInput(current);

            TasOptimisationInput next = new TasOptimisationInput();
            next.Load(optimisationDefinition);
            next.Directory = current.Directory;
            next.ScriptPath = current.ScriptPath;
            next.RunsDirectory = current.RunsDirectory;

            tasOptimisationInput = next;
            Load(tasOptimisationInput);
            UpdateAlgorithmList();
            Refresh();
        }

        /// <summary>
        /// Saves the form's definition as canonical JSON (UTF-8, no byte order mark). Only the definition is written: the
        /// Tas project folder, the runs folder and the script path are local settings, never in the file. Returns null, or
        /// why it could not be saved.
        /// </summary>
        internal string? SaveDefinition(string path)
        {
            if (!Read().TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out List<string> problems) || optimisationDefinition == null)
            {
                return "The form cannot be saved as a definition yet: " + string.Join(" ", problems);
            }

            try
            {
                System.IO.File.WriteAllText(path, optimisationDefinition.ToJson(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException || exception is ArgumentException)
            {
                return "The definition could not be saved: " + exception.Message;
            }

            textBlock_DefinitionFile.Text = "Saved as " + Path.GetFileName(path) + ".";
            return null;
        }

        /// <summary>
        /// Opens a definition file with the strict reader. A file that cannot be read, or that names an engine this window
        /// does not run, changes nothing; otherwise it replaces the form (its findings appear in Checks). Returns null, or
        /// why it was not opened.
        /// </summary>
        internal string? OpenDefinition(string path)
        {
            string text;
            try
            {
                text = System.IO.File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException || exception is ArgumentException)
            {
                return "The file could not be read: " + exception.Message;
            }

            OptimisationDefinition? optimisationDefinition = global::SAM.Core.Optimisation.Create.OptimisationDefinition(text, out List<OptimisationDiagnostic> diagnostics);
            if (optimisationDefinition == null)
            {
                return "The file is not an optimisation definition this version reads: " + string.Join(" ", diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => (x.Line == null ? string.Empty : "Line " + x.Line.Value.ToString(CultureInfo.InvariantCulture) + ": ") + x.Message));
            }

            string engine = optimisationDefinition.Model?.Engine ?? string.Empty;
            if (!Query.TasOptimisationEngines.Contains(engine))
            {
                return "The definition is for the engine '" + engine + "', which this window does not run (it runs " + string.Join(" and ", Query.TasOptimisationEngines.Select(x => "'" + x + "'")) + ").";
            }

            LoadDefinition(optimisationDefinition);
            textBlock_DefinitionFile.Text = "Opened " + Path.GetFileName(path) + ".";
            return null;
        }

        private void button_SaveDefinition_Click(object sender, RoutedEventArgs e)
        {
            string name = Read().Base?.Name ?? TasOptimisationInput.NewDefinitionName;
            char[] invalid = Path.GetInvalidFileNameChars();
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Title = "Save the optimisation definition",
                Filter = "Optimisation definition (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = new string(name.Select(x => invalid.Contains(x) ? '_' : x).ToArray()) + ".json",
            };

            if (saveFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            string? problem = SaveDefinition(saveFileDialog.FileName);
            if (problem != null)
            {
                MessageBox.Show(this, problem, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void button_OpenDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (running)
            {
                return;
            }

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Title = "Open an optimisation definition",
                Filter = "Optimisation definition (*.json)|*.json|All files (*.*)|*.*",
            };

            if (openFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            string? problem = OpenDefinition(openFileDialog.FileName);
            if (problem != null)
            {
                MessageBox.Show(this, problem, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// The text for an AI assistant (SAM.Core.Optimisation <c>AIExchangeText</c>): the engine's capabilities, the
        /// model's catalogue when it has been read, the current definition (or none when the form cannot be read) and the
        /// request typed in the window.
        /// </summary>
        internal string AIPromptText()
        {
            TasOptimisationInput input = Read();
            OptimisationDefinition? optimisationDefinition = input.TryGetDefinition(out OptimisationDefinition? definition, out _) ? definition : input.Base;
            OptimisationCatalogue? catalogue = input.IsTasModel && tasModelSession.State == TasModelReadState.Ready ? tasModelSession.Catalogue : null;
            string task = textBox_AITask.Text?.Trim() ?? string.Empty;
            return global::SAM.Core.Optimisation.Query.AIExchangeText(optimisationDefinition, Query.TasOptimisationCapabilities(input.Engine), catalogue, task.Length == 0 ? null : task);
        }

        /// <summary>Reads a pasted reply and shows its findings; the form is not changed until the reply is used.</summary>
        internal TasOptimisationAIReply? ReadReply(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                aiReply = null;
                textBlock_AIReply.Text = string.Empty;
                itemsControl_AIReply.ItemsSource = null;
                button_UseReply.IsEnabled = false;
                return null;
            }

            TasOptimisationInput input = Read();
            OptimisationCatalogue? catalogue = input.IsTasModel && tasModelSession.State == TasModelReadState.Ready ? tasModelSession.Catalogue : null;
            aiReply = new TasOptimisationAIReply(text, input.Engine, catalogue);

            textBlock_AIReply.Text = aiReply.Summary + (catalogue == null && input.IsTasModel ? " The model has not been read, so its names were not checked." : string.Empty);
            itemsControl_AIReply.ItemsSource = aiReply.Checks;
            button_UseReply.IsEnabled = aiReply.CanUse && !running;
            return aiReply;
        }

        /// <summary>Replaces the form with the last reply's definition; false when there is none to use.</summary>
        internal bool UseReply()
        {
            if (running || aiReply?.Definition == null)
            {
                return false;
            }

            LoadDefinition(aiReply.Definition);
            textBlock_DefinitionFile.Text = "From the AI assistant's reply.";
            return true;
        }

        private void button_CopyPrompt_Click(object sender, RoutedEventArgs e)
        {
            string text = AIPromptText();
            try
            {
                Clipboard.SetText(text);
                textBlock_AIPrompt.Text = "Copied. Paste it into the AI chat.";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                textBlock_AIPrompt.Text = "The clipboard is busy: press Copy prompt again.";
            }
        }

        private void button_PasteReply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                textBox_AIReply.Text = Clipboard.GetText();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                textBlock_AIReply.Text = "The clipboard is busy: press Paste reply again.";
            }
        }

        private void textBox_AIReply_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReadReply(textBox_AIReply.Text);
        }

        private void button_UseReply_Click(object sender, RoutedEventArgs e)
        {
            UseReply();
        }

        /// <summary>The script SAM_Tas generates for the form's definition (<c>TasModelRunner.ScriptText</c>); null with <paramref name="problem"/> when it cannot.</summary>
        internal string? GeneratedScript(out string? problem)
        {
            problem = null;
            TasOptimisationInput input = Read();
            if (!input.TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out List<string> problems) || optimisationDefinition == null)
            {
                problem = string.Join(" ", problems);
                return null;
            }

            try
            {
                return tasModelSession.CreateRunner(optimisationDefinition, input.Directory.Trim(), input.RunsDirectory, TasGenExecutePath).ScriptText;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException || exception is IOException)
            {
                problem = exception.Message;
                return null;
            }
        }

        private void button_ShowScript_Click(object sender, RoutedEventArgs e)
        {
            string? text = GeneratedScript(out string? problem);
            if (text == null)
            {
                MessageBox.Show(this, "The script cannot be written yet: " + problem, Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            System.Windows.Window window = new System.Windows.Window()
            {
                Title = "Generated Tas script (read-only)",
                Owner = this,
                Width = 820,
                Height = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Content = new TextBox()
                {
                    Text = text,
                    IsReadOnly = true,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(6),
                },
            };

            window.ShowDialog();
        }

        /// <summary>
        /// "Test one simulation": one Tas simulation at the start values (option 1 for a choice) through SAM_Tas'
        /// runner, in its own run folder, then its outputs, time and the run's duration estimate on the Setup tab.
        /// </summary>
        internal async Task TestAsync()
        {
            if (running || testing)
            {
                return;
            }

            Refresh();
            TasOptimisationInput input = Read();
            if (!input.IsTasModel || !tasOptimisationChecks.CanRun() || !input.TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out _) || optimisationDefinition == null)
            {
                return;
            }

            TasOptimisationTestReport tasOptimisationTestReport;
            TasModelRunner tasModelRunner;
            try
            {
                tasModelRunner = tasModelSession.CreateRunner(optimisationDefinition, input.Directory.Trim(), input.RunsDirectory, TasGenExecutePath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidOperationException || exception is NotSupportedException || exception is ArgumentException)
            {
                ShowTest(new TasOptimisationTestReport(exception));
                return;
            }

            testing = true;
            button_Run.IsEnabled = false;
            button_TestSimulation.IsEnabled = false;
            button_TestSimulation.Content = "Testing…";
            textBlock_NextStep.Text = "Testing one simulation: a full Tas simulation, which can take minutes.";
            border_Test.Visibility = Visibility.Visible;
            textBlock_TestGlyph.Text = "…";
            textBlock_TestHeadline.Text = "Running one Tas simulation…";
            itemsControl_Test.ItemsSource = null;

            try
            {
                TasModelTest tasModelTest = await TasModelSession.RunSta(() => tasModelRunner.Test(), "SAM test simulation");
                tasOptimisationTestReport = new TasOptimisationTestReport(
                    tasModelTest.Coordinates,
                    tasModelTest.Evaluation.Succeeded,
                    tasModelTest.Evaluation.Outputs,
                    tasModelTest.Evaluation.Message,
                    tasModelTest.Duration,
                    tasModelTest.EvaluationDirectory,
                    optimisationDefinition,
                    new TasOptimisationFormatter(optimisationDefinition),
                    tasModelRunner.Kernel.Optimiser.MaximumSimulations);
            }
            catch (Exception exception)
            {
                tasOptimisationTestReport = new TasOptimisationTestReport(exception);
            }
            finally
            {
                testing = false;
                button_TestSimulation.Content = "Test one simulation";
            }

            ShowTest(tasOptimisationTestReport);
        }

        private void ShowTest(TasOptimisationTestReport tasOptimisationTestReport)
        {
            TestReport = tasOptimisationTestReport;

            border_Test.Visibility = Visibility.Visible;
            textBlock_TestGlyph.Text = new TasOptimisationCheck(tasOptimisationTestReport.Status, string.Empty, string.Empty).Glyph;
            textBlock_TestGlyph.Foreground = (System.Windows.Media.Brush)FindResource(tasOptimisationTestReport.Status == TasOptimisationCheckStatus.Ready ? "PartO.Brush.Success" : "PartO.Brush.Danger");
            textBlock_TestHeadline.Text = tasOptimisationTestReport.Headline;
            itemsControl_Test.ItemsSource = tasOptimisationTestReport.Lines;

            tabControl_Main.SelectedItem = tabItem_Setup;
            border_Test.BringIntoView();
            Refresh();
        }

        private async void button_TestSimulation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await TestAsync();
            }
            catch (Exception exception) when (Query.IsTasOptimisationLoadFailure(exception))
            {
                testing = false;
                ShowTest(new TasOptimisationTestReport(exception));
            }
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

        private void button_CopyTrace_Click(object sender, RoutedEventArgs e)
        {
            string text = TraceText();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is busy; nothing is lost, the trace stays on screen.
            }
        }

        private void button_ExportTrace_Click(object sender, RoutedEventArgs e)
        {
            if (tasOptimisationFormatter == null || traceRows.Count == 0)
            {
                return;
            }

            //The file goes where the user chooses; nothing about it is stored in the definition.
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog()
            {
                Title = "Export the trace",
                Filter = "CSV (comma-separated values) (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = TasOptimisationFormatter.CsvFileName(definitionName),
            };

            string? runDirectory = Report?.RunDirectory;
            if (!string.IsNullOrWhiteSpace(runDirectory) && Directory.Exists(runDirectory))
            {
                saveFileDialog.InitialDirectory = runDirectory;
            }

            if (saveFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                ExportTrace(saveFileDialog.FileName);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "The trace could not be saved: " + exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
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
