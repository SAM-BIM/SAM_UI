// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The Design Optimisation (Simulate &gt; Optimisation) form over a SAM.Core.Optimisation
    /// <see cref="OptimisationDefinition"/>: the local settings (the Tas project folder, the script, the runs folder) and
    /// the definition's form fields (the method and its settings, the design variables (<see cref="Parameters"/>) and the
    /// outputs (<see cref="Objectives"/>)), kept as entered text, so a value that is not a number stays an inline form
    /// problem. It is remembered for the current application session only.
    /// <para>
    /// <see cref="Base"/> is the definition the form was last loaded from (an example). Everything the form does not show
    /// comes from it unchanged: the name, description, notes and model, each variable's type and quantity, each output's
    /// quantity and aggregation (matched by name), the objective's sense and the constraints. <see cref="TryGetDefinition"/>
    /// overlays the form onto a copy of it; every rule beyond "is this a number" is SAM.Core.Optimisation's and SAM_Tas'.
    /// </para>
    /// <para>
    /// A setting the form shows comes from the definition, or, when the definition leaves it to the engine, from the
    /// SAM_Tas objects themselves (<see cref="GoldenSectionAlgorithm"/>, <see cref="GPSHookeJeevesAlgorithm"/>,
    /// <see cref="OptimizationSettings"/>). No other value is supplied.
    /// </para>
    /// </summary>
    public sealed class TasOptimisationInput
    {
        /// <summary>The window opens on this example's form values.</summary>
        public const TasOptimisationExample DefaultExample = TasOptimisationExample.SystemsDemoGoldenSection;

        public TasOptimisationInput()
        {
            SetMethodDefaults();
        }

        public TasOptimisationInput(TasOptimisationInput tasOptimisationInput)
        {
            if (tasOptimisationInput == null)
            {
                SetMethodDefaults();
                return;
            }

            Base = tasOptimisationInput.Base == null ? null : new OptimisationDefinition(tasOptimisationInput.Base);
            Directory = tasOptimisationInput.Directory;
            ScriptPath = tasOptimisationInput.ScriptPath;
            RunsDirectory = tasOptimisationInput.RunsDirectory;
            OptimisationAlgorithm = tasOptimisationInput.OptimisationAlgorithm;
            Tolerance = tasOptimisationInput.Tolerance;
            StepReductionFactor = tasOptimisationInput.StepReductionFactor;
            InitialStepExponent = tasOptimisationInput.InitialStepExponent;
            StepExponentIncrement = tasOptimisationInput.StepExponentIncrement;
            StepReductions = tasOptimisationInput.StepReductions;
            MaximumSimulations = tasOptimisationInput.MaximumSimulations;
            Parameters = tasOptimisationInput.Parameters.ConvertAll(x => new TasOptimisationParameterRow(x));
            Objectives = tasOptimisationInput.Objectives.ConvertAll(x => new TasOptimisationObjectiveRow(x));
        }

        /// <summary>
        /// The methods the Tas engine runs (SAM_Tas <c>Query.TasOptimisationCapabilities</c>). A new list each time: no
        /// static field of a SAM.Core.Optimisation type, so this class loads without that assembly until it is used.
        /// </summary>
        public static IReadOnlyList<OptimisationAlgorithm> OptimisationAlgorithms => Array.AsReadOnly(new[] { OptimisationAlgorithm.GoldenSection, OptimisationAlgorithm.HookeJeeves });

        /// <summary>
        /// The definition the form was last loaded from; null for a form that was never loaded. It keeps everything the form
        /// does not show. A copy: editing the form never changes it.
        /// </summary>
        public OptimisationDefinition? Base { get; private set; }

        /// <summary>The Tas project folder: its top-level T3D/TBD/TPD/TSD/TWD files are what TasGenExecute receives. A local setting, never part of the definition.</summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>The TasGenExecute C# script. A local setting.</summary>
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>Parent folder of the run folders; empty for SAM_Tas' default (<c>SAM_NativeGenOpt</c> in the Tas project folder). A local setting.</summary>
        public string RunsDirectory { get; set; } = string.Empty;

        public OptimisationAlgorithm OptimisationAlgorithm { get; set; } = OptimisationAlgorithm.GoldenSection;

        /// <summary>Golden section: the objective tolerance (GenOpt: AbsDiffFunction).</summary>
        public string Tolerance { get; set; } = string.Empty;

        /// <summary>Hooke–Jeeves: the step reduction factor, a whole number (GenOpt: MeshSizeDivider).</summary>
        public string StepReductionFactor { get; set; } = string.Empty;

        /// <summary>Hooke–Jeeves: the initial step exponent, a whole number (GenOpt: InitialMeshSizeExponent).</summary>
        public string InitialStepExponent { get; set; } = string.Empty;

        /// <summary>Hooke–Jeeves: the step exponent increment, a whole number (GenOpt: MeshSizeExponentIncrement).</summary>
        public string StepExponentIncrement { get; set; } = string.Empty;

        /// <summary>Hooke–Jeeves: the number of step reductions, a whole number (GenOpt: NumberOfStepReduction).</summary>
        public string StepReductions { get; set; } = string.Empty;

        /// <summary>The simulation limit, shown as Maximum simulations (GenOpt: MaxIte).</summary>
        public string MaximumSimulations { get; set; } = string.Empty;

        public List<TasOptimisationParameterRow> Parameters { get; set; } = new List<TasOptimisationParameterRow>();

        public List<TasOptimisationObjectiveRow> Objectives { get; set; } = new List<TasOptimisationObjectiveRow>();

        /// <summary>Golden section reads only the bounds; Start and Step apply to pattern search only.</summary>
        public bool StartAndStepApplicable => OptimisationAlgorithm != OptimisationAlgorithm.GoldenSection;

        public TasOptimisationObjectiveRow? PrimaryObjective => Objectives.Find(x => x.Primary);

        /// <summary>A new form holding an example's values; the folder, script and runs folder are empty.</summary>
        public static TasOptimisationInput Create(TasOptimisationExample tasOptimisationExample)
        {
            TasOptimisationInput result = new TasOptimisationInput();
            result.Load(tasOptimisationExample);
            return result;
        }

        /// <summary>
        /// Loads an example's definition (<see cref="Query.TasOptimisationExampleDefinition"/>). The Tas project folder,
        /// the script and the runs folder are the user's and are kept.
        /// </summary>
        public void Load(TasOptimisationExample tasOptimisationExample)
        {
            Load(Query.TasOptimisationExampleDefinition(tasOptimisationExample));
        }

        /// <summary>
        /// Fills the form from <paramref name="optimisationDefinition"/> and makes a copy of it the <see cref="Base"/>. The
        /// method's settings the definition leaves to the engine show the SAM_Tas defaults; the other method's settings show
        /// their defaults too. The Tas project folder, the script and the runs folder are local settings and are kept.
        /// </summary>
        public void Load(OptimisationDefinition optimisationDefinition)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            Base = new OptimisationDefinition(optimisationDefinition);

            SetMethodDefaults();

            switch (Base.Method)
            {
                case HookeJeevesMethod hookeJeevesMethod:
                    OptimisationAlgorithm = OptimisationAlgorithm.HookeJeeves;
                    StepReductionFactor = Text_Integer(hookeJeevesMethod.StepReductionFactor) ?? StepReductionFactor;
                    InitialStepExponent = Text_Integer(hookeJeevesMethod.InitialStepExponent) ?? InitialStepExponent;
                    StepExponentIncrement = Text_Integer(hookeJeevesMethod.StepExponentIncrement) ?? StepExponentIncrement;
                    StepReductions = Text_Integer(hookeJeevesMethod.StepReductions) ?? StepReductions;
                    break;

                case GoldenSectionMethod goldenSectionMethod:
                    OptimisationAlgorithm = OptimisationAlgorithm.GoldenSection;
                    if (goldenSectionMethod.Tolerance != null)
                    {
                        Tolerance = Text(goldenSectionMethod.Tolerance.Value);
                    }

                    break;
            }

            MaximumSimulations = Text_Integer(Base.Stopping?.MaximumSimulations) ?? MaximumSimulations;

            Parameters = (Base.Variables ?? new List<DesignVariable>())
                .Where(x => x != null)
                .Select(x => new TasOptimisationParameterRow(x.Name, x.Start == null ? string.Empty : Text(x.Start.Value), Text(x.Minimum), Text(x.Maximum), x.Step == null ? string.Empty : Text(x.Step.Value), x.Description, x.Unit))
                .ToList();

            string? objective = Base.Objective?.Output;
            bool primary = false;
            Objectives = new List<TasOptimisationObjectiveRow>();
            foreach (OptimisationOutput optimisationOutput in (Base.Outputs ?? new List<OptimisationOutput>()).Where(x => x != null))
            {
                bool primary_Output = !primary && objective != null && optimisationOutput.Name == objective;
                primary |= primary_Output;
                Objectives.Add(new TasOptimisationObjectiveRow(optimisationOutput.Name, primary_Output, optimisationOutput.Description, optimisationOutput.Unit));
            }

            UpdateApplicability();
        }

        /// <summary>Tells every parameter row whether Start and Step apply to the selected method.</summary>
        public void UpdateApplicability()
        {
            foreach (TasOptimisationParameterRow tasOptimisationParameterRow in Parameters)
            {
                tasOptimisationParameterRow.StartAndStepApplicable = StartAndStepApplicable;
            }
        }

        /// <summary>
        /// Reads the form into a definition: a copy of <see cref="Base"/> (or a new "tas-script" definition) with the form's
        /// method, settings, design variables and outputs. Variables and outputs keep their non-form fields from the
        /// <see cref="Base"/> entry of the same name; a declared quantity is kept only while the unit is unchanged.
        /// <paramref name="problems"/> lists only what the form cannot read: a value that is not a number (or not a whole
        /// number), an unnamed or repeated output, no objective. Every other rule is checked on the result
        /// (<see cref="Query.Diagnostics"/>, then SAM_Tas), never here.
        /// <para>
        /// Start and step: empty is "not given". For golden section, which uses only the bounds, they are passed through
        /// when they are numbers that the definition accepts (so an example reproduces the proven inputs exactly), and
        /// otherwise left out, which SAM_Tas runs as the lower bound and 0: a value the method does not use, shown as "not
        /// used", never blocks a run.
        /// </para>
        /// <para>
        /// A method setting or the simulation limit that equals the SAM_Tas default, where <see cref="Base"/> left it to the
        /// engine, stays left to the engine; the run is the same either way, and a loaded definition reads back unchanged.
        /// </para>
        /// </summary>
        public bool TryGetDefinition(out OptimisationDefinition? optimisationDefinition, out List<string> problems)
        {
            optimisationDefinition = null;
            problems = new List<string>();

            OptimisationDefinition result = Base == null ? new OptimisationDefinition() : new OptimisationDefinition(Base);
            if (result.Model == null)
            {
                result.Model = new OptimisationModel(Analytical.Tas.GenOpt.Query.TasOptimisationEngine);
            }

            switch (OptimisationAlgorithm)
            {
                case OptimisationAlgorithm.HookeJeeves:
                    HookeJeevesMethod? hookeJeevesMethod_Base = Base?.Method as HookeJeevesMethod;
                    GPSHookeJeevesAlgorithm gPSHookeJeevesAlgorithm = new GPSHookeJeevesAlgorithm();
                    result.Method = new HookeJeevesMethod()
                    {
                        StepReductionFactor = Setting(Integer(StepReductionFactor, "Step reduction factor", problems), hookeJeevesMethod_Base?.StepReductionFactor, gPSHookeJeevesAlgorithm.MeshSizeDivider),
                        InitialStepExponent = Setting(Integer(InitialStepExponent, "Initial step exponent", problems), hookeJeevesMethod_Base?.InitialStepExponent, gPSHookeJeevesAlgorithm.InitialMeshSizeExponent),
                        StepExponentIncrement = Setting(Integer(StepExponentIncrement, "Step exponent increment", problems), hookeJeevesMethod_Base?.StepExponentIncrement, gPSHookeJeevesAlgorithm.MeshSizeExponentIncrement),
                        StepReductions = Setting(Integer(StepReductions, "Step reductions", problems), hookeJeevesMethod_Base?.StepReductions, gPSHookeJeevesAlgorithm.NumberOfStepReduction),
                    };
                    break;

                case OptimisationAlgorithm.GoldenSection:
                    GoldenSectionMethod? goldenSectionMethod_Base = Base?.Method as GoldenSectionMethod;
                    double? tolerance = Number(Tolerance, "Objective tolerance", problems);
                    result.Method = new GoldenSectionMethod()
                    {
                        Tolerance = tolerance != null && goldenSectionMethod_Base?.Tolerance == null && tolerance.Value == new GoldenSectionAlgorithm().AbsDiffFunction ? null : tolerance,
                    };
                    break;

                default:
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "The method {0} is not offered here. Choose {1}.", OptimisationAlgorithm, string.Join(" or ", OptimisationAlgorithms.Select(x => x.TasOptimisationAlgorithmName()))));
                    return false;
            }

            int? maximumSimulations = Setting(Integer(MaximumSimulations, "Maximum simulations", problems), Base?.Stopping?.MaximumSimulations, new OptimizationSettings().MaxIterations);
            if (maximumSimulations != null)
            {
                result.Stopping = new StoppingCriteria(maximumSimulations);
            }
            else if (result.Stopping != null)
            {
                result.Stopping.MaximumSimulations = null;
            }

            result.Variables = new List<DesignVariable>();
            for (int i = 0; i < Parameters.Count; i++)
            {
                TasOptimisationParameterRow row = Parameters[i];
                string name = row.Name?.Trim() ?? string.Empty;
                string label = string.Format(CultureInfo.InvariantCulture, "Design variable {0} ({1})", i + 1, name.Length == 0 ? "no name" : name);

                double? minimum = Number(row.Minimum, label + " minimum", problems);
                double? maximum = Number(row.Maximum, label + " maximum", problems);

                double? start;
                double? step;
                if (StartAndStepApplicable)
                {
                    start = Number_Optional(row.Start, label + " start", problems);
                    step = Number_Optional(row.Step, label + " step", problems);
                }
                else
                {
                    start = TryNumber(row.Start, out double value_Start) && minimum != null && maximum != null && value_Start >= minimum.Value && value_Start <= maximum.Value ? value_Start : (double?)null;
                    step = TryNumber(row.Step, out double value_Step) && value_Step > 0 ? value_Step : (double?)null;
                }

                DesignVariable? designVariable_Base = Base?.Variable(name);
                DesignVariable designVariable = designVariable_Base == null ? new DesignVariable() : new DesignVariable(designVariable_Base);
                designVariable.Name = name;
                designVariable.Description = Optional(row.Description);
                designVariable.Unit = Optional(row.Unit);
                if (designVariable.Unit != designVariable_Base?.Unit)
                {
                    designVariable.Quantity = OptimisationQuantity.Unspecified;
                }

                designVariable.Minimum = minimum ?? double.NaN;
                designVariable.Maximum = maximum ?? double.NaN;
                designVariable.Start = start;
                designVariable.Step = step;

                result.Variables.Add(designVariable);
            }

            result.Outputs = new List<OptimisationOutput>();
            List<TasOptimisationObjectiveRow> rows_Objective = Objectives.Where(x => x != null).ToList();
            for (int i = 0; i < rows_Objective.Count; i++)
            {
                TasOptimisationObjectiveRow row = rows_Objective[i];
                string name = row.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "Output {0} has no name: enter the name the script writes, or remove the row.", i + 1));
                    continue;
                }

                if (result.Outputs.Any(x => x.Name == name))
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "The output '{0}' is listed twice.", name));
                    continue;
                }

                OptimisationOutput? optimisationOutput_Base = Base?.Output(name);
                OptimisationOutput optimisationOutput = optimisationOutput_Base == null ? new OptimisationOutput() : new OptimisationOutput(optimisationOutput_Base);
                optimisationOutput.Name = name;
                optimisationOutput.Description = Optional(row.Description);
                optimisationOutput.Unit = Optional(row.Unit);
                if (optimisationOutput.Unit != optimisationOutput_Base?.Unit)
                {
                    optimisationOutput.Quantity = OptimisationQuantity.Unspecified;
                }

                result.Outputs.Add(optimisationOutput);
            }

            // The definition names its objective; SAM_Tas passes it first, as the native optimiser minimises the first output.
            List<TasOptimisationObjectiveRow> rows_Primary = rows_Objective.FindAll(x => x.Primary);
            if (rows_Objective.Count != 0 && rows_Primary.Count != 1)
            {
                problems.Add("Choose the output to minimise (the objective).");
            }

            result.Objective = new OptimisationObjective(rows_Primary.Count == 1 ? rows_Primary[0].Name?.Trim() : null, Base?.Objective?.Sense ?? ObjectiveSense.Minimise);

            if (problems.Count != 0)
            {
                return false;
            }

            optimisationDefinition = result;
            return true;
        }

        /// <summary>The text a number is shown as: invariant round-trip, so a definition's or a SAM_Tas default's value appears exactly.</summary>
        public static string Text(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static bool TryNumber(string? text, out double value)
        {
            return double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string? Text_Integer(int? value)
        {
            return value?.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Trimmed; null when empty, as the definition leaves an optional text out.</summary>
        private static string? Optional(string? text)
        {
            string value = text?.Trim() ?? string.Empty;
            return value.Length == 0 ? null : value;
        }

        private void SetMethodDefaults()
        {
            GoldenSectionAlgorithm goldenSectionAlgorithm = new GoldenSectionAlgorithm();
            GPSHookeJeevesAlgorithm gPSHookeJeevesAlgorithm = new GPSHookeJeevesAlgorithm();
            OptimizationSettings optimizationSettings = new OptimizationSettings();

            Tolerance = Text(goldenSectionAlgorithm.AbsDiffFunction);
            StepReductionFactor = Text(gPSHookeJeevesAlgorithm.MeshSizeDivider);
            InitialStepExponent = Text(gPSHookeJeevesAlgorithm.InitialMeshSizeExponent);
            StepExponentIncrement = Text(gPSHookeJeevesAlgorithm.MeshSizeExponentIncrement);
            StepReductions = Text(gPSHookeJeevesAlgorithm.NumberOfStepReduction);
            MaximumSimulations = optimizationSettings.MaxIterations.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The value to write: null (the engine's default) when <paramref name="value_Base"/> left it to the engine and the form still shows that default.</summary>
        private static int? Setting(int? value, int? value_Base, double value_Default)
        {
            return value != null && value_Base == null && value.Value == value_Default ? null : value;
        }

        private static double? Number(string? text, string label, List<string> problems)
        {
            if (TryNumber(text, out double result))
            {
                return result;
            }

            problems.Add(string.Format(CultureInfo.InvariantCulture, "{0}: '{1}' is not a number. Use '.' as the decimal separator.", label, text?.Trim()));
            return null;
        }

        /// <summary>Empty is "not given" (null); anything else must be a number.</summary>
        private static double? Number_Optional(string? text, string label, List<string> problems)
        {
            return string.IsNullOrWhiteSpace(text) ? null : Number(text, label, problems);
        }

        /// <summary>A whole number, also when written with a decimal point ("2.0").</summary>
        private static int? Integer(string? text, string label, List<string> problems)
        {
            if (TryNumber(text, out double value) && value == System.Math.Floor(value) && value >= int.MinValue && value <= int.MaxValue)
            {
                return (int)value;
            }

            problems.Add(string.Format(CultureInfo.InvariantCulture, "{0}: '{1}' is not a whole number.", label, text?.Trim()));
            return null;
        }
    }
}
