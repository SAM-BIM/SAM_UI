// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Ported from the retired SAM.Analytical.Windows.Modify.UpdateAreaPerPerson: refreshes the
        /// InternalCondition AreaPerPerson from the Space's stored Occupancy.
        /// </summary>
        public static void UpdateAreaPerPerson(this Space space)
        {
            if (space == null)
            {
                return;
            }

            if (!space.TryGetValue(SpaceParameter.Occupancy, out double occupancy))
            {
                return;
            }

            UpdateOccupancy(space, occupancy);
        }

        // Ported from the retired SAM.Analytical.Windows.Modify.UpdateOccupancy (also inlined in
        // OccupancyWindow): sets Space occupancy and, when an area is available, the InternalCondition
        // AreaPerPerson.
        private static void UpdateOccupancy(Space space, double occupancy)
        {
            if (space == null || occupancy < 0)
            {
                return;
            }

            if (double.IsNaN(occupancy))
            {
                space.RemoveValue(SpaceParameter.Occupancy);
            }
            else
            {
                space.SetValue(SpaceParameter.Occupancy, occupancy);

                if (space.TryGetValue(SpaceParameter.Area, out double area) && !double.IsNaN(area) && area > 0)
                {
                    InternalCondition internalCondition = space.InternalCondition;
                    if (internalCondition != null)
                    {
                        internalCondition.SetValue(InternalConditionParameter.AreaPerPerson, occupancy == 0 ? 0 : area / occupancy);
                        space.InternalCondition = internalCondition;
                    }
                }
            }
        }
    }
}
