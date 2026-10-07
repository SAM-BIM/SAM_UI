// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>Where one step of the journey to a runnable Iteration 3 product method stands.</summary>
    public enum PartOIteration3StepState
    {
        /// <summary>Nothing left to do for it.</summary>
        Done,

        /// <summary>The next thing to do - the only step that offers a button.</summary>
        Current,

        /// <summary>Waits for the steps before it.</summary>
        Later,
    }

    /// <summary>
    /// One step of the journey to a runnable Iteration 3 product method. <see cref="Action"/> is the Hub action that
    /// does it, where one exists; <see cref="PartOWorkflowAction.None"/> where it is done with the Hub's own buttons.
    /// </summary>
    public sealed class PartOIteration3Step(string text, PartOIteration3StepState state, PartOWorkflowAction action, string? buttonText)
    {
        public string Text { get; } = text;

        public PartOIteration3StepState State { get; } = state;

        public PartOWorkflowAction Action { get; } = action;

        /// <summary>The label of the button that does this step, or null.</summary>
        public string? ButtonText { get; } = buttonText;
    }

    public static partial class Query
    {
        /// <summary>
        /// The refusal the manufacturer-guidance method gives where the prepared design carries no valid saved
        /// dwelling strategy selections. One constant, so the resolution that states it and the Hub that turns it
        /// into steps cannot drift apart.
        /// </summary>
        public const string PartOIteration3NoDwellingStrategiesRefusal = "The prepared design has no valid saved Part O dwelling strategy selections, so its cooling control rooms cannot be resolved.";

        /// <summary>
        /// What a person has to do, in order, for the manufacturer-guidance method to run - as steps, each with where it
        /// stands - written from the open design model and the reference run, and nothing else. Advice only: it decides
        /// nothing and changes nothing; the method's own pre-flight is still the authority on whether it runs.
        ///
        /// <para>
        /// The cooling control room (the room whose thermostat switches cooling) is a per-dwelling engineering
        /// choice, saved on the model by Mixed Design - which accepts only a model that carries no simulation results.
        /// The prepared design - which Iteration 3 reads - carries the rooms only when the iteration was prepared AFTER
        /// they were saved, hence the last step. Only the first step that is not done offers a button.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel_Design">The open design model.</param>
        /// <param name="partORun">The reference run.</param>
        /// <param name="lead">One sentence saying what the method needs.</param>
        public static List<PartOIteration3Step> PartOIteration3ProductMethodSteps(AnalyticalModel? analyticalModel_Design, PartORun? partORun, out string lead)
        {
            bool clean = analyticalModel_Design is null || (Analytical.Query.PartOBaselineFindings(analyticalModel_Design)?.Count ?? 0) == 0;
            bool saved = analyticalModel_Design?.GetValue<PartODwellingStrategySet>(Analytical.AnalyticalModelParameter.PartODwellingStrategies)?.IsValid == true;

            PartOPreparationContext? partOPreparationContext = partORun?.PreparationContext;
            bool iteration2 = partOPreparationContext is not null && partOPreparationContext.PartOIteration == PartOIteration.BasePassive && partOPreparationContext.VentilationUnitCatalogueOffered == true;

            lead = saved
                ? "This method needs the cooling control rooms saved on the model to be part of the reference case, and they are not yet."
                : iteration2
                    ? "This method needs a cooling control room - the room whose thermostat switches the cooling - saved for each dwelling."
                    : "This method needs a manufacturer unit and a cooling control room - the room whose thermostat switches the cooling - for each dwelling.";

            List<(string Text, bool Done, PartOWorkflowAction Action, string? Button)> definitions = [];

            if (saved)
            {
                definitions.Add(("The cooling control rooms are saved on the model.", true, PartOWorkflowAction.None, null));
            }
            else
            {
                if (!clean)
                {
                    definitions.Add(("Save a copy of the model without its simulation results and open it. Mixed Design does not accept a model that carries results, and the open model is not changed. Opening the copy starts a new session, so Iteration 1a / 2 is run again afterwards.", false, PartOWorkflowAction.RemoveResults, "Remove Results…"));
                }

                definitions.Add(("Choose a cooling control room for each dwelling in Mixed Design, then Save selection.", false, clean ? PartOWorkflowAction.MixedDesign : PartOWorkflowAction.None, clean ? "Choose cooling control rooms (Mixed Design)…" : null));
            }

            definitions.Add((iteration2
                ? "Select Prepare & Run for Iteration 2 again, so this reference case carries the saved rooms."
                : "Choose Iteration 2 (MVHR with manufacturer unit) and press Prepare & Run: it selects a product for each dwelling and carries the saved rooms. Iteration 3 then runs against that result.", false, PartOWorkflowAction.None, null));

            //The first step not done is the current one, and the only one that offers a button.
            List<PartOIteration3Step> result = [];
            bool current_Assigned = false;
            foreach (var definition in definitions)
            {
                PartOIteration3StepState state = definition.Done ? PartOIteration3StepState.Done : !current_Assigned ? PartOIteration3StepState.Current : PartOIteration3StepState.Later;
                current_Assigned |= state == PartOIteration3StepState.Current;

                result.Add(new PartOIteration3Step(definition.Text, state, state == PartOIteration3StepState.Current ? definition.Action : PartOWorkflowAction.None, state == PartOIteration3StepState.Current ? definition.Button : null));
            }

            return result;
        }

        /// <summary>
        /// The same steps as one text, for a notice or a test: the sentence, then the steps in order. See
        /// <see cref="PartOIteration3ProductMethodSteps"/>.
        /// </summary>
        /// <param name="offerMixedDesign">True where the current step is choosing the rooms in Mixed Design.</param>
        public static string PartOIteration3ProductMethodAdvice(AnalyticalModel? analyticalModel_Design, PartORun? partORun, out bool offerMixedDesign)
        {
            List<PartOIteration3Step> steps = PartOIteration3ProductMethodSteps(analyticalModel_Design, partORun, out string lead);

            offerMixedDesign = steps.Exists(x => x.Action == PartOWorkflowAction.MixedDesign);

            return string.Join("\n", new[] { lead }.Concat(steps.Select((x, i) => string.Format("{0}. {1}", i + 1, x.Text))));
        }
    }
}
