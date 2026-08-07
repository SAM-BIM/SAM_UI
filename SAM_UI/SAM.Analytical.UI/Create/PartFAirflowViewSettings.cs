// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    public static partial class Create
    {
        /// <summary>
        /// The Part F airflow preset a NEW view gets when its colour scheme is set to Part F data: a usable
        /// Part F drawing straight away.
        /// <para>
        /// The point is that an engineer who asks for a Part F view should get one. Before this, they had to
        /// know that the colour scheme was only half of it and that nine more options were behind a separate
        /// dialog - so the obvious action produced a coloured plan with no airflow on it and no hint that
        /// anything was missing.
        /// </para>
        /// <para>
        /// <b>Only ever applied when a view is created.</b> It is never applied to a view that already exists,
        /// never re-applied when somebody reopens View Settings, and never applied to a duplicate - a
        /// duplicated view keeps the presentation of the view it came from, including any label somebody
        /// moved. This builds a preset; deciding that a preset is wanted is the caller's job.
        /// </para>
        /// </summary>
        /// <param name="adjacencyCluster">
        /// The model, for resolving which zone category holds the dwellings. See
        /// <see cref="Analytical.Query.PartFDwellingZoneCategories"/>.
        /// </param>
        /// <returns>
        /// Settings with the annotation on, everything visible, at 1:50 and the continuous design condition.
        /// </returns>
        public static PartFAirflowViewSettings PartFAirflowViewSettings(AdjacencyCluster adjacencyCluster)
        {
            return new PartFAirflowViewSettings()
            {
                Enabled = true,

                //The Approved Document F sizing case, which is the condition a drawing is normally issued at.
                OperatingMode = PartFOperatingMode.ContinuousDesign,

                //A floor plan is a drawing of a floor: every dwelling on it, not one flat with the rest blank.
                DwellingFilter = PartFDwellingFilter.AllDwellingsOnLevel,
                DwellingGuid = System.Guid.Empty,

                ZoneCategoryName = ZoneCategoryName(adjacencyCluster),

                AnnotationScale = PartFTagPlacement.DefaultAnnotationScale,

                //Everything on. A drawing that silently omitted a dwelling's extract would be worse than a
                //crowded one, and an engineer can always turn a layer off; they cannot turn on something they
                //do not know is there.
                ShowSupply = true,
                ShowGeneralExtract = true,
                ShowLocalKitchenExtract = true,
                ShowTransfer = true,
                ShowUnresolved = true,
                ShowValues = true,
                ShowCompliance = true,
                ShowDoorRequirements = true,
                ShowContextGeometry = true,
            };
        }

        /// <summary>
        /// The dwelling zone category to start a new Part F view on, or null to leave it unset.
        /// <para>
        /// Resolved from the model rather than assumed - nothing here knows the word "Flats". One unambiguous
        /// dwelling category is chosen automatically, because that is the case where a person would only be
        /// retyping what the model already says. Several are left unset ON PURPOSE: which flats a drawing
        /// reports on is an engineering decision, and guessing it would produce a confident drawing of the
        /// wrong half of a mixed-use building. None means the model has no dwelling-zone structure, and the
        /// calculation's whole-house mode - which an empty category selects - is the right answer.
        /// </para>
        /// </summary>
        private static string ZoneCategoryName(AdjacencyCluster adjacencyCluster)
        {
            List<string> names = adjacencyCluster?.PartFDwellingZoneCategories();

            return names != null && names.Count == 1 ? names[0] : null;
        }
    }
}
