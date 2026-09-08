// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// One scenario the Prepare and Run dialog offers: Iteration 1a, Iteration 1b, or Iteration 2.
    /// <para>
    /// <b>Two things, not three.</b> A scenario is a base provision - <see cref="PartOVentilationStrategyOption"/>,
    /// which is what SAM's preparation actually takes - plus whether a real manufacturer unit is selected
    /// against the design it realizes. Iteration 2 is not a third base provision: it is Iteration 1a with the
    /// catalogue offered, and the analytical API has no separate value for it. This class states that
    /// relationship once instead of leaving each caller to reconstruct it.
    /// </para>
    /// <para>
    /// <b>Iteration 2B is deliberately absent.</b> It is an optimisation performed ON a completed Iteration 2
    /// run, never a base provision - offering it here would let somebody start a run that
    /// <c>Modify.CanOptimise</c> refuses. It is reached as a follow-on action once a baseline has results.
    /// </para>
    /// <para>
    /// <b>Nothing is invented.</b> The list is built from
    /// <see cref="PartOVentilationStrategyOption.Options"/>, which asks SAM which route each iteration is
    /// defined over; Iteration 2 appears only if the mechanical base provision does. A route SAM stops
    /// offering drops out of this list rather than appearing with a guessed pairing.
    /// </para>
    /// </summary>
    public class PartOWorkflowScenario
    {
        /// <summary>
        /// The one spelling of each scenario the Part O family uses.
        ///
        /// <para><b>Why they are constants</b></para>
        /// <para>
        /// Because the same four scenarios are named in a scenario picker, a base-provision picker, a status
        /// line, a tooltip and a follow-on action, and each place that spelled them for itself spelled them
        /// slightly differently - "mechanical ventilation, design MVHR" here, "base MVHR (MVHR)" there.
        /// Named once, an engineer reading two Part O dialogs reads the same four names. Display text only:
        /// no enum, persisted value or API member carries any of these words.
        /// </para>
        /// </summary>
        public const string Text_Iteration1a = "Iteration 1a — MVHR design duty (no manufacturer unit)";

        /// <summary>The natural-ventilation base provision. See <see cref="Text_Iteration1a"/>.</summary>
        public const string Text_Iteration1b = "Iteration 1b — Natural ventilation (no mechanical system)";

        /// <summary>Iteration 1a with a real manufacturer unit selected. See <see cref="Text_Iteration1a"/>.</summary>
        public const string Text_Iteration2 = "Iteration 2 — MVHR with manufacturer unit";

        /// <summary>
        /// The follow-on optimisation. Deliberately not one of <see cref="Scenarios"/> - it is performed ON a
        /// completed Iteration 2 run - but it is named here so that every window naming it agrees.
        /// </summary>
        public const string Text_Iteration2B = "Iteration 2B — TM59 optimisation";

        private PartOWorkflowScenario(PartOVentilationStrategyOption partOVentilationStrategyOption, bool selectVentilationUnit, string text)
        {
            Option = partOVentilationStrategyOption;
            SelectVentilationUnit = selectVentilationUnit;
            Text = text;
        }

        /// <summary>The base provision this scenario prepares, and the canonical route word it states.</summary>
        public PartOVentilationStrategyOption Option { get; }

        /// <summary>Whether a manufacturer product is selected against the realized design - the 1a / 2 difference.</summary>
        public bool SelectVentilationUnit { get; }

        /// <summary>What the picker shows.</summary>
        public string Text { get; }

        /// <summary>
        /// Whether Iteration 2B could follow a run of this scenario at all: 2B raises mechanical design
        /// airflow inside a selected unit's capacity, so it needs both. The same pair
        /// <c>Modify.CanOptimise</c> refuses on, asked before a run rather than after one.
        /// </summary>
        public bool SupportsOptimisation => SelectVentilationUnit && Option is not null && Option.PartOVentilationMode == PartOVentilationMode.MVHR;

        public override string ToString()
        {
            return Text;
        }

        /// <summary>
        /// The scenarios offered, in assessment order: 1a, 1b, then 2.
        /// </summary>
        public static List<PartOWorkflowScenario> Scenarios
        {
            get
            {
                List<PartOWorkflowScenario> result = [];

                PartOVentilationStrategyOption option_Mechanical = null;

                foreach (PartOVentilationStrategyOption partOVentilationStrategyOption in PartOVentilationStrategyOption.Options)
                {
                    bool mechanical = partOVentilationStrategyOption.PartOVentilationMode == PartOVentilationMode.MVHR;

                    result.Add(new PartOWorkflowScenario(
                        partOVentilationStrategyOption,
                        false,
                        mechanical
                            ? Text_Iteration1a
                            : Text_Iteration1b));

                    if (mechanical)
                    {
                        option_Mechanical = partOVentilationStrategyOption;
                    }
                }

                if (option_Mechanical is not null)
                {
                    result.Add(new PartOWorkflowScenario(option_Mechanical, true, Text_Iteration2));
                }

                return result;
            }
        }
    }
}
