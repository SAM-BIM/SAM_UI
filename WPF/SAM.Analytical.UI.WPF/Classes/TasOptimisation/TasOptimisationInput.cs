// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The Simulate &gt; Optimisation form: the Tas project folder, the script, the algorithm, its settings, the
    /// parameters and the objectives, kept as entered. It is remembered for the current application session only.
    /// <para>
    /// Default values come from the SAM_Tas objects themselves (<see cref="GoldenSectionAlgorithm"/>,
    /// <see cref="GPSHookeJeevesAlgorithm"/>, <see cref="OptimizationSettings"/>); the parameter and objective values
    /// come from the proven Systems Demo examples (<see cref="TasOptimisationExample"/>). No other value is supplied.
    /// </para>
    /// </summary>
    public sealed class TasOptimisationInput
    {
        /// <summary>The algorithms the native optimiser runs (SAM_Tas <c>Convert.ToSAM_Optimiser</c>; GPSCoordinateSearch is refused there, D3).</summary>
        public static readonly IReadOnlyList<AlgorithmType> AlgorithmTypes = Array.AsReadOnly(new[] { AlgorithmType.GoldenSection, AlgorithmType.GPSHookeJeeves });

        /// <summary>The window opens on this example's form values.</summary>
        public const TasOptimisationExample DefaultExample = TasOptimisationExample.SystemsDemoGoldenSection;

        public TasOptimisationInput()
        {
            SetAlgorithmDefaults();
        }

        public TasOptimisationInput(TasOptimisationInput tasOptimisationInput)
        {
            if (tasOptimisationInput == null)
            {
                SetAlgorithmDefaults();
                return;
            }

            Directory = tasOptimisationInput.Directory;
            ScriptPath = tasOptimisationInput.ScriptPath;
            RunsDirectory = tasOptimisationInput.RunsDirectory;
            AlgorithmType = tasOptimisationInput.AlgorithmType;
            AbsDiffFunction = tasOptimisationInput.AbsDiffFunction;
            MeshSizeDivider = tasOptimisationInput.MeshSizeDivider;
            InitialMeshSizeExponent = tasOptimisationInput.InitialMeshSizeExponent;
            MeshSizeExponentIncrement = tasOptimisationInput.MeshSizeExponentIncrement;
            NumberOfStepReduction = tasOptimisationInput.NumberOfStepReduction;
            MaxIterations = tasOptimisationInput.MaxIterations;
            MaxEqualResults = tasOptimisationInput.MaxEqualResults;
            Parameters = tasOptimisationInput.Parameters.ConvertAll(x => new TasOptimisationParameterRow(x));
            Objectives = tasOptimisationInput.Objectives.ConvertAll(x => new TasOptimisationObjectiveRow(x));
        }

        /// <summary>The Tas project folder: its top-level T3D/TBD/TPD/TSD/TWD files are what TasGenExecute receives.</summary>
        public string Directory { get; set; } = string.Empty;

        /// <summary>The TasGenExecute C# script.</summary>
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>Parent folder of the run folders; empty for SAM_Tas' default (<c>SAM_NativeGenOpt</c> in the Tas project folder).</summary>
        public string RunsDirectory { get; set; } = string.Empty;

        public AlgorithmType AlgorithmType { get; set; } = AlgorithmType.GoldenSection;

        public string AbsDiffFunction { get; set; } = string.Empty;

        public string MeshSizeDivider { get; set; } = string.Empty;

        public string InitialMeshSizeExponent { get; set; } = string.Empty;

        public string MeshSizeExponentIncrement { get; set; } = string.Empty;

        public string NumberOfStepReduction { get; set; } = string.Empty;

        /// <summary>GenOpt MaxIte: the simulation limit.</summary>
        public string MaxIterations { get; set; } = string.Empty;

        /// <summary>GenOpt MaxEqualResults: must be at least 2; no effect on the native algorithms.</summary>
        public string MaxEqualResults { get; set; } = string.Empty;

        public List<TasOptimisationParameterRow> Parameters { get; set; } = new List<TasOptimisationParameterRow>();

        public List<TasOptimisationObjectiveRow> Objectives { get; set; } = new List<TasOptimisationObjectiveRow>();

        /// <summary>Golden section reads only the bounds; Start (Ini) and Step apply to pattern search only.</summary>
        public bool StartAndStepApplicable => AlgorithmType != AlgorithmType.GoldenSection;

        public TasOptimisationObjectiveRow? PrimaryObjective => Objectives.Find(x => x.Primary);

        /// <summary>A new form holding an example's values; the folder, script and runs folder are empty.</summary>
        public static TasOptimisationInput Create(TasOptimisationExample tasOptimisationExample)
        {
            TasOptimisationInput result = new TasOptimisationInput();
            result.Load(tasOptimisationExample);
            return result;
        }

        /// <summary>
        /// Replaces the algorithm, its settings, the parameters and the objectives with an example's values. The Tas
        /// project folder, the script and the runs folder are the user's and are kept.
        /// </summary>
        public void Load(TasOptimisationExample tasOptimisationExample)
        {
            SetAlgorithmDefaults();

            switch (tasOptimisationExample)
            {
                case TasOptimisationExample.SystemsDemoHookeJeeves:
                    AlgorithmType = AlgorithmType.GPSHookeJeeves;
                    Parameters = new List<TasOptimisationParameterRow> { new TasOptimisationParameterRow("Setpoint", "10", "-5", "35", "2") };
                    break;

                default:
                    AlgorithmType = AlgorithmType.GoldenSection;
                    Parameters = new List<TasOptimisationParameterRow> { new TasOptimisationParameterRow("Setpoint", "3", "-5", "35", "1") };
                    break;
            }

            Objectives = new List<TasOptimisationObjectiveRow>
            {
                new TasOptimisationObjectiveRow("Result", true),
                new TasOptimisationObjectiveRow("Cost", false),
                new TasOptimisationObjectiveRow("CO2", false),
            };

            UpdateApplicability();
        }

        /// <summary>Tells every parameter row whether Start and Step apply to the selected algorithm.</summary>
        public void UpdateApplicability()
        {
            foreach (TasOptimisationParameterRow tasOptimisationParameterRow in Parameters)
            {
                tasOptimisationParameterRow.StartAndStepApplicable = StartAndStepApplicable;
            }
        }

        /// <summary>
        /// Reads the form into the SAM_Tas objects. <paramref name="problems"/> lists what cannot be read: a value that is
        /// not a number, an unnamed or repeated output, no primary output. Everything else (names, counts, the algorithm's
        /// domain) is left to SAM_Tas (<see cref="TasOptimisationDefinition.Validate"/>).
        /// <para>
        /// For golden section, Start and Step are excluded: the algorithm ignores them, and they are passed through as
        /// entered when they are numbers (so an example reproduces the proven inputs exactly), otherwise as the lower bound
        /// and 0. SAM_Tas only requires them to be finite.
        /// </para>
        /// </summary>
        public bool TryGetDefinition(out TasOptimisationDefinition? tasOptimisationDefinition, out List<string> problems)
        {
            tasOptimisationDefinition = null;
            problems = new List<string>();

            Algorithm algorithm;
            if (AlgorithmType == AlgorithmType.GPSHookeJeeves)
            {
                GPSHookeJeevesAlgorithm gPSHookeJeevesAlgorithm = new GPSHookeJeevesAlgorithm();
                gPSHookeJeevesAlgorithm.MeshSizeDivider = Number(MeshSizeDivider, "MeshSizeDivider", problems);
                gPSHookeJeevesAlgorithm.InitialMeshSizeExponent = Number(InitialMeshSizeExponent, "InitialMeshSizeExponent", problems);
                gPSHookeJeevesAlgorithm.MeshSizeExponentIncrement = Number(MeshSizeExponentIncrement, "MeshSizeExponentIncrement", problems);
                gPSHookeJeevesAlgorithm.NumberOfStepReduction = Number(NumberOfStepReduction, "NumberOfStepReduction", problems);
                algorithm = gPSHookeJeevesAlgorithm;
            }
            else if (AlgorithmType == AlgorithmType.GoldenSection)
            {
                algorithm = new GoldenSectionAlgorithm() { AbsDiffFunction = Number(AbsDiffFunction, "AbsDiffFunction", problems) };
            }
            else
            {
                problems.Add(string.Format(CultureInfo.InvariantCulture, "The algorithm {0} is not offered here. Choose {1}.", AlgorithmType, string.Join(" or ", AlgorithmTypes)));
                return false;
            }

            OptimizationSettings optimizationSettings = new OptimizationSettings()
            {
                MaxIterations = Integer(MaxIterations, "Max simulations (MaxIte)", problems),
                MaxEqualResults = Integer(MaxEqualResults, "MaxEqualResults", problems),
            };

            List<NumberParameter> numberParameters = new List<NumberParameter>();
            for (int i = 0; i < Parameters.Count; i++)
            {
                TasOptimisationParameterRow row = Parameters[i];
                string label = string.Format(CultureInfo.InvariantCulture, "Parameter {0} ({1})", i + 1, string.IsNullOrWhiteSpace(row.Name) ? "no name" : row.Name.Trim());

                double minimum = Number(row.Minimum, label + " Min", problems);
                double maximum = Number(row.Maximum, label + " Max", problems);

                double start;
                double step;
                if (StartAndStepApplicable)
                {
                    start = Number(row.Start, label + " Start", problems);
                    step = Number(row.Step, label + " Step", problems);
                }
                else
                {
                    start = TryNumber(row.Start, out double value_Start) ? value_Start : minimum;
                    step = TryNumber(row.Step, out double value_Step) ? value_Step : 0;
                }

                numberParameters.Add(new NumberParameter() { Name = row.Name?.Trim(), Initial = start, Min = minimum, Max = maximum, Step = step });
            }

            List<Objective> objectives = new List<Objective>();
            List<TasOptimisationObjectiveRow> rows_Objective = Objectives.Where(x => x != null).ToList();
            for (int i = 0; i < rows_Objective.Count; i++)
            {
                string name = rows_Objective[i].Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "Output {0} has no name: enter the name the script writes, or remove the row.", i + 1));
                    continue;
                }

                if (objectives.Any(x => x.Name == name))
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture, "The output '{0}' is listed twice.", name));
                    continue;
                }

                objectives.Add(new Objective(name));
            }

            List<TasOptimisationObjectiveRow> rows_Primary = rows_Objective.FindAll(x => x.Primary);
            if (rows_Objective.Count != 0 && rows_Primary.Count != 1)
            {
                problems.Add("Choose one output as the primary objective (the one that is minimised).");
            }
            else if (rows_Primary.Count == 1)
            {
                // The native optimiser minimises the first objective, so the primary one goes first.
                int index = objectives.FindIndex(x => x.Name == rows_Primary[0].Name?.Trim());
                if (index > 0)
                {
                    Objective objective = objectives[index];
                    objectives.RemoveAt(index);
                    objectives.Insert(0, objective);
                }
            }

            if (problems.Count != 0)
            {
                return false;
            }

            tasOptimisationDefinition = new TasOptimisationDefinition(algorithm, optimizationSettings, numberParameters, objectives);
            return true;
        }

        /// <summary>The text a number is shown as: invariant round-trip, so a SAM_Tas default appears exactly.</summary>
        public static string Text(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static bool TryNumber(string? text, out double value)
        {
            return double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void SetAlgorithmDefaults()
        {
            GoldenSectionAlgorithm goldenSectionAlgorithm = new GoldenSectionAlgorithm();
            GPSHookeJeevesAlgorithm gPSHookeJeevesAlgorithm = new GPSHookeJeevesAlgorithm();
            OptimizationSettings optimizationSettings = new OptimizationSettings();

            AbsDiffFunction = Text(goldenSectionAlgorithm.AbsDiffFunction);
            MeshSizeDivider = Text(gPSHookeJeevesAlgorithm.MeshSizeDivider);
            InitialMeshSizeExponent = Text(gPSHookeJeevesAlgorithm.InitialMeshSizeExponent);
            MeshSizeExponentIncrement = Text(gPSHookeJeevesAlgorithm.MeshSizeExponentIncrement);
            NumberOfStepReduction = Text(gPSHookeJeevesAlgorithm.NumberOfStepReduction);
            MaxIterations = optimizationSettings.MaxIterations.ToString(CultureInfo.InvariantCulture);
            MaxEqualResults = optimizationSettings.MaxEqualResults.ToString(CultureInfo.InvariantCulture);
        }

        private static double Number(string? text, string label, List<string> problems)
        {
            if (TryNumber(text, out double result))
            {
                return result;
            }

            problems.Add(string.Format(CultureInfo.InvariantCulture, "{0}: '{1}' is not a number. Use '.' as the decimal separator.", label, text?.Trim()));
            return double.NaN;
        }

        private static int Integer(string? text, string label, List<string> problems)
        {
            if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }

            problems.Add(string.Format(CultureInfo.InvariantCulture, "{0}: '{1}' is not a whole number.", label, text?.Trim()));
            return 0;
        }
    }
}
