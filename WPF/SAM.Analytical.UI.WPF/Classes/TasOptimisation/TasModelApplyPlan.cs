// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>Where "Apply best design" writes.</summary>
    public enum TasModelApplyMode
    {
        /// <summary>The open SAM model and its Tas files (the Tas project is the model's own folder).</summary>
        ModelAndTasFiles,

        /// <summary>The Tas files only: no model is open, or the Tas project is not the open model's folder.</summary>
        TasFilesOnly,
    }

    /// <summary>One design variable of "Apply best design": what changes, from what, to what, and where.</summary>
    public sealed class TasModelApplyItem
    {
        internal TasModelApplyItem(TasModelDesignChange change, string item, string current, string best, string bestRaw, string where, bool unchanged, TasModelSamTarget? samTarget, string? problem)
        {
            Change = change;
            Item = item;
            Current = current;
            Best = best;
            BestRaw = bestRaw;
            Where = where;
            Unchanged = unchanged;
            SamTarget = samTarget;
            Problem = problem;
        }

        public TasModelDesignChange Change { get; }

        /// <summary>The design variable's name.</summary>
        public string Name => Change.VariableName;

        /// <summary>The model item, in words ("Cooling setpoint of “Studio 1_0”").</summary>
        public string Item { get; }

        /// <summary>The value the Tas model holds now, as the window shows values.</summary>
        public string Current { get; }

        /// <summary>The best value, as the window shows values.</summary>
        public string Best { get; }

        /// <summary>The best value at full precision (invariant round-trip text; for a choice, the option number).</summary>
        public string BestRaw { get; }

        /// <summary>Where it is written ("SAM model and TBD", "TPD only: …").</summary>
        public string Where { get; }

        /// <summary>The Tas model already holds the best value: nothing is written for it.</summary>
        public bool Unchanged { get; }

        /// <summary>Where it lands in the open SAM model; null in "Tas files only" mode, for a controller, or with a problem.</summary>
        public TasModelSamTarget? SamTarget { get; }

        /// <summary>Why it cannot be applied; null when it can.</summary>
        public string? Problem { get; }

        /// <summary>The item as a line of the window (glyph, title, detail; the tooltip has the full-precision value).</summary>
        public TasOptimisationCheck Line
        {
            get
            {
                TasOptimisationCheckStatus status = Problem != null ? TasOptimisationCheckStatus.Blocked : TasOptimisationCheckStatus.Ready;
                string detail = Item + ": " + (Unchanged ? "already " + Best + ", nothing to write" : Current + " → " + Best) + ". " + (Problem ?? Where);
                string raw = Item + ": " + Current + " → " + BestRaw + ". " + (Problem ?? Where);
                return new TasOptimisationCheck(status, Name, detail, raw);
            }
        }
    }

    /// <summary>
    /// What "Apply best design" (PR9) would do with the best point of a "tas-model" run, shown before anything is written:
    /// every design variable with the model's current value, the best value and where it goes, and whether it can be
    /// applied. Built only for a run whose result rules give a best point (<see cref="TasOptimisationReport.Successful"/>,
    /// SAM_Tas' <c>NativeGenOptOutcome</c>): a cancelled, withheld or failed run has nothing to apply.
    /// <para>
    /// Where the Tas project is the open model's own folder, the model is changed too, and every building item must be
    /// found in it as the Tas model has it (<see cref="Query.TasModelSamTarget"/>); one that is not blocks the whole
    /// application, because the next Energy Simulation rebuilds the TBD from the model. Elsewhere only the Tas files change.
    /// Plant controller setpoints always go to the TPD only: SAM does not hold plant controllers.
    /// </para>
    /// </summary>
    public sealed class TasModelApplyPlan
    {
        private TasModelApplyPlan(string headline, string? refusal)
        {
            Headline = headline;
            Refusal = refusal;
            Items = new List<TasModelApplyItem>();
            Notes = new List<string>();
            Changes = new List<TasModelDesignChange>();
            ProjectFolder = string.Empty;
        }

        public TasModelApplyMode Mode { get; private set; }

        public string Headline { get; private set; }

        /// <summary>Why nothing can be applied at all; null when the items say it.</summary>
        public string? Refusal { get; private set; }

        public IReadOnlyList<TasModelApplyItem> Items { get; private set; }

        /// <summary>What the engineer needs to know before applying (in order).</summary>
        public IReadOnlyList<string> Notes { get; private set; }

        /// <summary>True when every item can be applied and at least one changes something.</summary>
        public bool CanApply => Refusal == null && Items.Count != 0 && Items.All(x => x.Problem == null) && Items.Any(x => !x.Unchanged);

        /// <summary>True when the open model will change (so Energy Simulation is worth offering afterwards).</summary>
        public bool ChangesModel => Mode == TasModelApplyMode.ModelAndTasFiles && Items.Any(x => x.SamTarget != null && !x.Unchanged);

        internal IReadOnlyList<TasModelDesignChange> Changes { get; private set; }

        internal TasModelRunner? Runner { get; private set; }

        internal string ProjectFolder { get; private set; }

        internal AnalyticalModel? AnalyticalModel { get; private set; }

        /// <summary>The plan's lines for the window.</summary>
        public IReadOnlyList<TasOptimisationCheck> Lines => Items.Select(x => x.Line).ToList();

        /// <param name="tasOptimisationReport">How the run ended.</param>
        /// <param name="optimisationDefinition">The run's definition.</param>
        /// <param name="tasModelRunner">The run's runner (inventory, glazing options, the hashes of the files it evaluated).</param>
        /// <param name="projectFolder">The Tas project folder the run used.</param>
        /// <param name="analyticalModel">The open model; null when none is open.</param>
        /// <param name="modelPath">The open model's file; null when it was never saved.</param>
        /// <param name="tasOptimisationFormatter">How the window shows values; null for full precision.</param>
        public static TasModelApplyPlan Create(TasOptimisationReport? tasOptimisationReport, OptimisationDefinition? optimisationDefinition, TasModelRunner? tasModelRunner, string? projectFolder, AnalyticalModel? analyticalModel, string? modelPath, TasOptimisationFormatter? tasOptimisationFormatter)
        {
            if (tasOptimisationReport == null || !tasOptimisationReport.Successful || tasOptimisationReport.BestPoint.Count == 0)
            {
                return Refused("There is no best design to apply: " + (tasOptimisationReport?.Headline ?? "no run has finished") + ".");
            }

            if (optimisationDefinition == null || tasModelRunner == null || tasModelRunner.SourceHashes == null || string.IsNullOrWhiteSpace(projectFolder))
            {
                return Refused("Only a run of the “Tas model” engine in this window can be applied.");
            }

            List<TasModelDesignChange> changes;
            try
            {
                changes = Analytical.Tas.GenOpt.Query.TasModelDesignChanges(optimisationDefinition, tasOptimisationReport.BestPoint, tasModelRunner.GlazingOptions);
            }
            catch (ArgumentException exception)
            {
                return Refused(exception.Message);
            }

            TasModelInventory inventory = tasModelRunner.Inventory;
            string folder = Path.GetFullPath(projectFolder!.Trim());
            bool linked = analyticalModel != null && !string.IsNullOrWhiteSpace(modelPath) && SameFolder(Path.GetDirectoryName(modelPath!), folder);
            string? tasOnlyReason = linked ? null : analyticalModel == null ? "No SAM model is open" : string.IsNullOrWhiteSpace(modelPath) ? "The open model has not been saved, so it has no folder" : "The Tas project folder is not the open model's folder";

            List<TasModelApplyItem> items = new List<TasModelApplyItem>();
            for (int i = 0; i < changes.Count; i++)
            {
                TasModelDesignChange change = changes[i];
                TasOptimisationColumn? column = tasOptimisationFormatter != null && i < tasOptimisationFormatter.Variables.Count ? tasOptimisationFormatter.Variables[i] : null;
                bool unchanged = TasModelDesignApplier.IsUnchanged(change, inventory);
                string current = CurrentText(change, inventory, column, tasOptimisationFormatter);
                string best = column == null || tasOptimisationFormatter == null ? Raw(change) : tasOptimisationFormatter.Text(column, change.Value);

                TasModelSamTarget? samTarget = null;
                string? problem = null;
                string where;
                if (change.IsController)
                {
                    where = "TPD only: SAM does not hold plant controllers";
                }
                else if (!linked)
                {
                    where = "TBD only";
                }
                else
                {
                    samTarget = Query.TasModelSamTarget(analyticalModel!, change, inventory, out problem);
                    where = samTarget == null ? string.Empty : SamWhere(change, samTarget);
                }

                items.Add(new TasModelApplyItem(change, ItemText(change), current, best, Raw(change), where, unchanged, samTarget, problem));
            }

            TasModelApplyPlan result = new TasModelApplyPlan(string.Empty, null)
            {
                Mode = linked ? TasModelApplyMode.ModelAndTasFiles : TasModelApplyMode.TasFilesOnly,
                Items = items,
                Changes = changes,
                Runner = tasModelRunner,
                ProjectFolder = folder,
                AnalyticalModel = linked ? analyticalModel : null,
            };

            List<string> notes = new List<string>();
            if (tasOptimisationReport.Status == TasOptimisationCheckStatus.Warning)
            {
                notes.Add("The run ended with a warning (" + tasOptimisationReport.Headline + "); its best design so far is the one applied.");
            }

            if (!linked)
            {
                notes.Add(tasOnlyReason + ": the open model is not changed. Only the Tas files in " + folder + " are changed; simulate them in Tas. An Energy Simulation of an open model would not use them.");
            }

            if (items.Any(x => x.Change.IsController && !x.Unchanged))
            {
                notes.Add("A plant controller setpoint is kept in the TPD only, because SAM does not hold plant controllers. Simulate the plant in Tas to see its effect. An Energy Simulation with “Create TPD” ticked writes SAM's own plant into the TPD and may not keep it.");
            }

            if (linked && items.Any(x => x.Change.IsSetpoint && x.Change.IsHeating && !x.Unchanged && x.Problem == null))
            {
                notes.Add("A space's heating design-day condition (“<space> - HDD”) follows its new heating setpoint at the next Energy Simulation.");
            }

            if (items.Any(x => !x.Unchanged))
            {
                notes.Add("The TBD and TPD are written as copies, read back and only then replace the originals; the originals are kept in " + Path.Combine(folder, TasModelDesignApplier.WorkFolderName) + ". The TSD still holds the previous design's results until the next simulation.");
            }

            if (result.ChangesModel)
            {
                notes.Add("The model change is one Undo step and is not saved: save the model to keep it. Energy Simulation is offered afterwards.");
            }

            result.Notes = notes;

            if (items.Any(x => x.Problem != null))
            {
                result.Headline = "The best design cannot be applied: the open model is not the model the Tas files were made from.";
                result.Notes = new[] { "Nothing will be written. Each ✕ says what does not match." }.Concat(notes.Where(x => !x.StartsWith("The TBD and TPD", StringComparison.Ordinal) && !x.StartsWith("The model change", StringComparison.Ordinal))).ToList();
            }
            else if (items.All(x => x.Unchanged))
            {
                result.Headline = "The model already holds the best design: there is nothing to apply.";
            }
            else
            {
                result.Headline = linked ? "Apply the best design to the open model and its Tas files:" : "Apply the best design to the Tas files only:";
            }

            return result;
        }

        private static TasModelApplyPlan Refused(string refusal)
        {
            return new TasModelApplyPlan("The best design cannot be applied.", refusal)
            {
                Notes = new[] { refusal },
            };
        }

        private static string ItemText(TasModelDesignChange change)
        {
            if (change.IsSetpoint)
            {
                return (change.IsHeating ? "Heating" : "Cooling") + " setpoint of “" + change.InternalCondition + "”";
            }

            if (change.IsGlazing)
            {
                return "Glazing of “" + change.GlazingConstruction + "”";
            }

            return "Setpoint of controller “" + change.Controller + "” (" + change.PlantRoom + ")";
        }

        private static string CurrentText(TasModelDesignChange change, TasModelInventory inventory, TasOptimisationColumn? column, TasOptimisationFormatter? formatter)
        {
            if (change.IsGlazing)
            {
                return "option 1 " + change.GlazingConstruction;
            }

            string? text = TasModelDesignApplier.CurrentText(change, inventory);
            if (text == null)
            {
                return "unknown";
            }

            if (column == null || formatter == null || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return text;
            }

            return formatter.Text(column, value);
        }

        private static string Raw(TasModelDesignChange change)
        {
            if (change.IsGlazing && change.GlazingOption != null)
            {
                return change.OptionNumber.ToString(CultureInfo.InvariantCulture) + ": " + change.GlazingOption.Text;
            }

            return change.Value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string SamWhere(TasModelDesignChange change, TasModelSamTarget samTarget)
        {
            if (change.IsSetpoint)
            {
                string profile = (change.IsHeating ? "heating" : "cooling") + " profile “" + samTarget.Profile?.Name + "”";
                return samTarget.SharedProfile
                    ? "SAM model (space “" + samTarget.Space?.Name + "” gets its own copy of the " + profile + "; the other spaces keep it) and TBD"
                    : "SAM model (space “" + samTarget.Space?.Name + "”, " + profile + ") and TBD";
            }

            return string.Format(CultureInfo.InvariantCulture, "SAM model ({0} aperture{1} of {2}; the pane changes, the frame{3} kept) and TBD",
                samTarget.ApertureCount,
                samTarget.ApertureCount == 1 ? string.Empty : "s",
                string.Join(", ", samTarget.ApertureConstructions.Select(x => "“" + x.Name + "”")),
                samTarget.ApertureConstructions.Count == 1 ? " is" : "s are");
        }

        private static bool SameFolder(string? x, string y)
        {
            if (string.IsNullOrWhiteSpace(x))
            {
                return false;
            }

            return string.Equals(Path.GetFullPath(x).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(y).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
    }
}
