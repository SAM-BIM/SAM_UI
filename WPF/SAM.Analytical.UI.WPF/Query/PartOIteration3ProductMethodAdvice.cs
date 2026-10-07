// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// The refusal the manufacturer-guidance method gives where the prepared design carries no valid saved
        /// dwelling strategy selections. One constant, so the resolution that states it and the Hub that turns it
        /// into steps cannot drift apart.
        /// </summary>
        public const string PartOIteration3NoDwellingStrategiesRefusal = "The prepared design has no valid saved Part O dwelling strategy selections, so its cooling control rooms cannot be resolved.";

        /// <summary>
        /// What a person has to do, in order, for the manufacturer-guidance method to run - written from the
        /// open design model and the reference run, and nothing else. Advice only: it decides nothing and
        /// changes nothing; the method's own pre-flight is still the authority on whether it runs.
        ///
        /// <para>
        /// The cooling control room (the room whose thermostat switches cooling) is a per-dwelling engineering
        /// choice, saved on the model by Mixed Design. The prepared design - which Iteration 3 reads - carries it
        /// only when the iteration was prepared AFTER it was saved, hence the closing step.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel_Design">The open design model.</param>
        /// <param name="partORun">The reference run.</param>
        /// <param name="offerMixedDesign">True where Mixed Design can be opened to take the step - the model is a clean baseline and has no saved selection yet.</param>
        public static string PartOIteration3ProductMethodAdvice(AnalyticalModel? analyticalModel_Design, PartORun? partORun, out bool offerMixedDesign)
        {
            offerMixedDesign = false;

            if (analyticalModel_Design is null)
            {
                return PartOIteration3NoDwellingStrategiesRefusal;
            }

            bool clean = (Analytical.Query.PartOBaselineFindings(analyticalModel_Design)?.Count ?? 0) == 0;
            bool saved = analyticalModel_Design.GetValue<PartODwellingStrategySet>(Analytical.AnalyticalModelParameter.PartODwellingStrategies)?.IsValid == true;

            PartOPreparationContext? partOPreparationContext = partORun?.PreparationContext;
            bool iteration2 = partOPreparationContext is not null && partOPreparationContext.PartOIteration == PartOIteration.BasePassive && partOPreparationContext.VentilationUnitCatalogueOffered == true;

            List<string> steps = [];

            if (!saved)
            {
                if (!clean)
                {
                    steps.Add("Results > Part O > Remove Results: save a cleaned copy and open it. Mixed Design does not accept a model that carries simulation results.");
                }

                steps.Add(clean
                    ? "Choose a cooling control room for each dwelling: press the button below (Mixed Design), then Save selection."
                    : "Choose a cooling control room for each dwelling: Simulate > Part O > Mixed Design, then Save selection.");

                offerMixedDesign = clean;
            }

            steps.Add(iteration2
                ? "Select Prepare & Run for Iteration 2 again, so this reference case carries the saved rooms."
                : "Choose Iteration 2 (MVHR with manufacturer unit) and press Prepare & Run: it selects a product for each dwelling and carries the saved rooms. Iteration 3 then runs against that result.");

            string lead = saved
                ? "This method needs the cooling control rooms saved on the model to be part of the reference case, and they are not yet."
                : iteration2
                    ? "This method needs a cooling control room - the room whose thermostat switches the cooling - saved for each dwelling."
                    : "This method needs a manufacturer unit and a cooling control room - the room whose thermostat switches the cooling - for each dwelling.";

            List<string> lines = [lead];
            for (int i = 0; i < steps.Count; i++)
            {
                lines.Add(string.Format("{0}. {1}", i + 1, steps[i]));
            }

            return string.Join("\n", lines);
        }
    }
}
