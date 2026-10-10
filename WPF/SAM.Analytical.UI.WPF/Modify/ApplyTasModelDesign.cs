// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Analytical.Tas.GenOpt;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>How "Apply best design" ended.</summary>
    public sealed class TasModelApplyOutcome
    {
        internal TasModelApplyOutcome(bool succeeded, string headline, IEnumerable<TasOptimisationCheck>? lines, AnalyticalModel? analyticalModel = null, TasModelApplyResult? tasModelApplyResult = null, bool projectChanged = false)
        {
            Succeeded = succeeded;
            Headline = headline;
            Lines = (lines ?? Enumerable.Empty<TasOptimisationCheck>()).ToList();
            AnalyticalModel = analyticalModel;
            TasResult = tasModelApplyResult;
            ProjectChanged = projectChanged;
        }

        public bool Succeeded { get; }

        public string Headline { get; }

        public IReadOnlyList<TasOptimisationCheck> Lines { get; }

        /// <summary>The changed model to put in the window (one Undo step); null when the model is not changed.</summary>
        public AnalyticalModel? AnalyticalModel { get; }

        /// <summary>What was written to the Tas files; null when nothing was.</summary>
        public TasModelApplyResult? TasResult { get; }

        /// <summary>A failure left a Tas file not restored: the headline says which and where the backup is.</summary>
        public bool ProjectChanged { get; }
    }

    public static partial class Modify
    {
        /// <summary>
        /// "Apply best design" (PR9): carries out a plan the engineer has seen (<see cref="TasModelApplyPlan"/>).
        /// <list type="number">
        /// <item>The open model's change is made first, in memory, on copies (nothing is written yet).</item>
        /// <item>Then SAM_Tas writes the Tas files (<see cref="TasModelDesignApplier"/>: the run's hashes checked, staging
        /// copies written and read back, originals backed up, files replaced with rollback). If that fails, the model
        /// change is dropped, so the model and the Tas files never disagree.</item>
        /// <item>The changed model is returned for the caller to put in the window as one Undo step.</item>
        /// </list>
        /// Runs on the calling thread; Tas COM and TCD need an STA thread.
        /// </summary>
        /// <param name="configure">Replaces the applier's Tas writers and reader (tests); null for licensed Tas.</param>
        /// <param name="calculate">Tas' calculation of a new glazing system's aperture parameters (TCD); null for licensed Tas.</param>
        public static TasModelApplyOutcome ApplyTasModelBestDesign(TasModelApplyPlan tasModelApplyPlan, Action<TasModelDesignApplier>? configure = null, Func<ApertureConstruction, MaterialLibrary, ThermalTransmittanceCalculationResult?>? calculate = null)
        {
            if (tasModelApplyPlan == null || !tasModelApplyPlan.CanApply || tasModelApplyPlan.Runner == null)
            {
                return new TasModelApplyOutcome(false, tasModelApplyPlan?.Refusal ?? tasModelApplyPlan?.Headline ?? "There is nothing to apply.", null);
            }

            // 1. The model, in memory.
            AnalyticalModel? analyticalModel = null;
            List<TasOptimisationCheck> lines_Model = new List<TasOptimisationCheck>();
            if (tasModelApplyPlan.ChangesModel && tasModelApplyPlan.AnalyticalModel != null)
            {
                analyticalModel = ApplyTasModelDesign(tasModelApplyPlan.AnalyticalModel, tasModelApplyPlan.Items, calculate ?? CalculateTasGlazing, lines_Model, out string? error);
                if (analyticalModel == null)
                {
                    return new TasModelApplyOutcome(false, "The best design could not be applied to the model: " + error + " Nothing was changed.", null);
                }
            }

            // 2. The Tas files.
            TasModelApplyResult tasModelApplyResult;
            try
            {
                TasModelDesignApplier tasModelDesignApplier = new TasModelDesignApplier(tasModelApplyPlan.ProjectFolder, tasModelApplyPlan.Runner.Inventory, tasModelApplyPlan.Runner.SourceHashes);
                configure?.Invoke(tasModelDesignApplier);
                tasModelApplyResult = tasModelDesignApplier.Apply(tasModelApplyPlan.Changes);
            }
            catch (TasModelApplyException exception)
            {
                string model = tasModelApplyPlan.ChangesModel ? " The open model was not changed." : string.Empty;
                return new TasModelApplyOutcome(false, exception.Message + model, null, null, null, exception.ProjectChanged);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                return new TasModelApplyOutcome(false, "The Tas files could not be written: " + exception.Message + " Nothing was changed.", null);
            }

            // 3. What was done.
            List<TasOptimisationCheck> lines = new List<TasOptimisationCheck>();
            foreach (TasModelAppliedValue value in tasModelApplyResult.Values)
            {
                TasModelApplyItem? item = tasModelApplyPlan.Items.FirstOrDefault(x => ReferenceEquals(x.Change, value.Change));
                string title = value.Change.VariableName;
                string detail = value.Changed
                    ? value.File + ": " + value.Before + " → " + value.After + (string.IsNullOrWhiteSpace(value.Detail) ? string.Empty : " (" + value.Detail + ")")
                    : value.File + ": already " + value.After + ", not written";
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, title, detail, item == null ? detail : detail + "; best value " + item.BestRaw));
            }

            lines.AddRange(lines_Model);
            if (tasModelApplyResult.BackupFolder != null)
            {
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Original Tas files", tasModelApplyResult.BackupFolder));
            }

            if (analyticalModel != null)
            {
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Undo", "Undo restores the open model only, not the Tas files: after an Undo, run Energy Simulation (or copy the original Tas files back) before using the Tas files or their results."));
            }

            string headline = analyticalModel != null
                ? "The best design is in the open model and its Tas files. Save the model to keep it; run Energy Simulation to see its results."
                : tasModelApplyResult.FilesReplaced.Count == 0 ? "Nothing needed writing: the model already held the best design." : "The best design is in the Tas files (" + string.Join(", ", tasModelApplyResult.FilesReplaced) + ").";
            return new TasModelApplyOutcome(true, headline, lines, analyticalModel, tasModelApplyResult);
        }

        /// <summary>
        /// The open model with the plan's building items applied (no Tas, except <paramref name="calculate"/> for a new
        /// glazing system's aperture parameters); null with <paramref name="error"/> when one cannot be. The model given is
        /// not modified.
        /// <list type="bullet">
        /// <item>Setpoint: the space's heating or cooling profile gets the best value exactly (a double) where SAM_Tas
        /// writes the TBD's setpoint from: the profile's value, or the hours of a 24-hour profile at the setpoint (heating:
        /// the highest, cooling: the lowest, compared as floats as in the TBD); the other hours are kept. A profile used by
        /// anything else is copied for this space ("&lt;profile&gt; - &lt;space&gt;"), so only this space changes.</item>
        /// <item>Glazing choice: each aperture construction is replaced, through the Glazing window's
        /// <c>SetGlazing</c>, by a new one with the chosen system's pane layers and its own frame layers and frame
        /// parameters (the TBD kept the frames too), named after the system.</item>
        /// </list>
        /// </summary>
        internal static AnalyticalModel? ApplyTasModelDesign(AnalyticalModel analyticalModel, IEnumerable<TasModelApplyItem> items, Func<ApertureConstruction, MaterialLibrary, ThermalTransmittanceCalculationResult?>? calculate, List<TasOptimisationCheck> lines, out string? error)
        {
            error = null;
            AnalyticalModel result = analyticalModel;
            foreach (TasModelApplyItem item in items.Where(x => x.SamTarget != null && !x.Unchanged))
            {
                TasModelDesignChange change = item.Change;
                TasModelSamTarget target = item.SamTarget!;
                AnalyticalModel? next = change.IsSetpoint
                    ? ApplyTasSetpoint(result, change, target, lines, out error)
                    : change.IsGlazing ? ApplyTasGlazing(result, change, target, calculate, lines, out error) : null;
                if (next == null)
                {
                    error ??= "“" + change.VariableName + "” is not a model change.";
                    return null;
                }

                result = next;
            }

            return result;
        }

        /// <summary>
        /// The profile values SAM_Tas will write as the TBD setpoint <paramref name="value"/>: a one-value profile's value;
        /// otherwise hours 0-23, those at the setpoint (heating: highest, cooling: lowest; as floats) set to the value.
        /// </summary>
        internal static double[] TasSetpointValues(Profile profile, double value, bool heating)
        {
            if (profile.Count == 1)
            {
                double[] values = profile.GetValues();
                values[0] = value;
                return values;
            }

            double[] hours = Query.TasHours(profile);
            float setpoint = heating ? hours.Max(x => (float)x) : hours.Min(x => (float)x);
            for (int i = 0; i < hours.Length; i++)
            {
                if ((float)hours[i] == setpoint)
                {
                    hours[i] = value;
                }
            }

            return hours;
        }

        private static AnalyticalModel? ApplyTasSetpoint(AnalyticalModel analyticalModel, TasModelDesignChange change, TasModelSamTarget target, List<TasOptimisationCheck> lines, out string? error)
        {
            error = null;
            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            ProfileLibrary profileLibrary = analyticalModel.ProfileLibrary ?? new ProfileLibrary("Default ProfileLibrary");
            Space? space = adjacencyCluster?.GetObject<Space>(target.Space!.Guid);
            InternalCondition? internalCondition = space?.InternalCondition;
            Profile? profile = internalCondition?.GetProfile(target.ProfileType, profileLibrary);
            if (adjacencyCluster == null || space == null || internalCondition == null || profile == null)
            {
                error = "Space “" + target.Space?.Name + "” or its profile is no longer in the model.";
                return null;
            }

            double[] values = TasSetpointValues(profile, change.Value, change.IsHeating);
            string value = change.Value.ToString("R", CultureInfo.InvariantCulture);
            if (!target.SharedProfile)
            {
                profileLibrary.Add(new Profile(profile, values, profile.Category));
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, change.VariableName, "SAM model: profile “" + profile.Name + "” of space “" + space.Name + "” now " + value));
            }
            else
            {
                string name = UniqueProfileName(profileLibrary, profile.Name + " - " + space.Name, profile.Category);
                Profile copy = new Profile(Guid.NewGuid(), new Profile(profile, values, profile.Category), name, profile.Category);
                profileLibrary.Add(copy);
                internalCondition.SetProfileName(target.ProfileType, name);
                Space space_New = new Space(space)
                {
                    InternalCondition = internalCondition,
                };

                adjacencyCluster.AddObject(space_New);
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, change.VariableName, "SAM model: space “" + space.Name + "” now uses “" + name + "”, a copy of “" + profile.Name + "” at " + value + " (the other spaces keep “" + profile.Name + "”)"));
            }

            return new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, profileLibrary);
        }

        private static string UniqueProfileName(ProfileLibrary profileLibrary, string name, string category)
        {
            List<Profile> profiles = profileLibrary.GetProfiles() ?? new List<Profile>();
            string result = name;
            for (int i = 2; profiles.Any(x => x.Name == result && x.Category == category); i++)
            {
                result = string.Format(CultureInfo.InvariantCulture, "{0} ({1})", name, i);
            }

            return result;
        }

        private static AnalyticalModel? ApplyTasGlazing(AnalyticalModel analyticalModel, TasModelDesignChange change, TasModelSamTarget target, Func<ApertureConstruction, MaterialLibrary, ThermalTransmittanceCalculationResult?>? calculate, List<TasOptimisationCheck> lines, out string? error)
        {
            error = null;
            TasGlazingOption? option = change.GlazingOption;
            ApertureConstruction? system = option?.System?.ApertureConstruction;
            if (option == null || system == null)
            {
                error = "Glazing option " + change.OptionNumber + " of “" + change.VariableName + "” has no glazing system.";
                return null;
            }

            // The pane's materials as evaluated: every one defined by the system's source, and any model material of the
            // same name the same thermal input (checked again here, before anything is changed, whatever the plan said).
            string? conflict = Query.TasGlazingMaterialProblem(option, analyticalModel.MaterialLibrary);
            if (conflict != null)
            {
                error = conflict;
                return null;
            }

            AnalyticalModel result = analyticalModel;
            foreach (ApertureConstruction current in target.ApertureConstructions)
            {
                ApertureConstruction composite = TasGlazingComposite(system, current);

                // The pane's materials come from the system's source where the model lacks them (the model's own, the
                // same, otherwise); the frame's are the model's own.
                MaterialLibrary materialLibrary = result.MaterialLibrary ?? new MaterialLibrary("Default MaterialLibrary");
                List<IMaterial> materials = new List<IMaterial>();
                foreach (ConstructionLayer constructionLayer in composite.PaneConstructionLayers ?? new List<ConstructionLayer>())
                {
                    IMaterial? material = option.System!.MaterialLibrary?.GetMaterial(constructionLayer.Name);
                    if (material != null && materialLibrary.GetMaterial(constructionLayer.Name) == null && !materials.Any(x => x.Name == material.Name))
                    {
                        materials.Add(material);
                    }
                }

                MaterialLibrary materialLibrary_Calculation = new MaterialLibrary(materialLibrary);
                materials.ForEach(x => materialLibrary_Calculation.Add(x));

                SetGlazingRequest request = new SetGlazingRequest()
                {
                    SourceApertureConstructionGuid = current.Guid,
                    ApertureConstruction = composite,
                    MaterialsToAdd = materials,
                    Scope = ThermalApplyScope.AllUsing,
                    Values = new GlazingValues(option.U, option.G, option.Light, double.NaN),
                };

                AnalyticalModel? next = SetGlazing(result, request, calculate?.Invoke(composite, materialLibrary_Calculation)!, out SetGlazingResult setGlazingResult);
                if (next == null || setGlazingResult == null || !setGlazingResult.Succeeded)
                {
                    error = setGlazingResult?.Error ?? "The glazing could not be changed.";
                    return null;
                }

                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, change.VariableName, string.Format(CultureInfo.InvariantCulture, "SAM model: {0} aperture{1} of “{2}” now use “{3}” (pane of {4}, frame of “{2}”)", setGlazingResult.ApertureCount, setGlazingResult.ApertureCount == 1 ? string.Empty : "s", current.Name, setGlazingResult.ApertureConstruction?.Name, option.Text)));
                result = next;
            }

            return result;
        }

        /// <summary>
        /// The chosen system with <paramref name="current"/>'s frame: the system's pane layers and pane-side parameters, the
        /// current construction's aperture type, frame layers and frame-side parameters (frame width, frame additional heat
        /// transfer, default panel type). A new identity, named after the system.
        /// </summary>
        internal static ApertureConstruction TasGlazingComposite(ApertureConstruction system, ApertureConstruction current)
        {
            ApertureConstruction typed = new ApertureConstruction(system, current.ApertureType);
            ApertureConstruction layered = new ApertureConstruction(typed, system.PaneConstructionLayers, current.FrameConstructionLayers);
            ApertureConstruction result = new ApertureConstruction(Guid.NewGuid(), layered, system.Name);

            // The frame side is the model's: its value, or none when it has none.
            foreach (ApertureConstructionParameter parameter in new[] { ApertureConstructionParameter.DefaultFrameWidth, ApertureConstructionParameter.FrameAdditionalHeatTransfer })
            {
                result.RemoveValue(parameter);
                if (current.TryGetValue(parameter, out double value))
                {
                    result.SetValue(parameter, value);
                }
            }

            result.RemoveValue(ApertureConstructionParameter.DefaultPanelType);
            if (current.TryGetValue(ApertureConstructionParameter.DefaultPanelType, out string? panelType) && panelType != null)
            {
                result.SetValue(ApertureConstructionParameter.DefaultPanelType, panelType);
            }

            return result;
        }

        private static ThermalTransmittanceCalculationResult? CalculateTasGlazing(ApertureConstruction apertureConstruction, MaterialLibrary materialLibrary)
        {
            try
            {
                return new ThermalTransmittanceCalculator(new ConstructionManager(new ApertureConstruction[] { apertureConstruction }, null, materialLibrary)).Calculate(new Guid[] { apertureConstruction.Guid })?.FirstOrDefault();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
