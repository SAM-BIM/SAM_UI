// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.UI;
using SAM.Geometry.Object;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.UI;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// In-place appearance refresh for attribute-only edits (see AttributeModification): recomputes
        /// the legend and the space fill colors of an existing floor-plan GeometryObjectModel against the
        /// current AnalyticalModel, without re-sectioning, label solving or rebuilding anything. Mirrors
        /// the legend/color logic of ToSAM_GeometryObjectModel(AnalyticalModel, TwoDimensionalViewSettings)
        /// - keep the two in sync. Returns false when the result could differ from a full regeneration
        /// (caller must then fall back to ToSAM_GeometryObjectModel); on success spaceGuids contains the
        /// guids of spaces whose appearance actually changed. No state is modified on the false path.
        /// </summary>
        public static bool TryRefreshSpaceAppearances(this GeometryObjectModel geometryObjectModel, AnalyticalModel analyticalModel, TwoDimensionalViewSettings twoDimensionalViewSettings, out HashSet<Guid> spaceGuids)
        {
            spaceGuids = null;

            if (geometryObjectModel == null || analyticalModel == null || twoDimensionalViewSettings == null)
            {
                return false;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                return false;
            }

            List<ISAMGeometryObject> sAMGeometryObjects = geometryObjectModel.GetSAMGeometryObjects<ISAMGeometryObject>();
            if (sAMGeometryObjects == null || sAMGeometryObjects.Count == 0)
            {
                return false;
            }

            List<Tuple<Space, Geometry3DObjectCollection>> tuples = new List<Tuple<Space, Geometry3DObjectCollection>>();
            foreach (ISAMGeometryObject sAMGeometryObject in sAMGeometryObjects)
            {
                Geometry3DObjectCollection geometry3DObjectCollection = sAMGeometryObject as Geometry3DObjectCollection;

                Space space = geometry3DObjectCollection?.Tag?.Value as Space;
                if (space == null)
                {
                    continue;
                }

                space = adjacencyCluster.GetObject<Space>(space.Guid);
                if (space == null)
                {
                    // Space no longer exists - not an attribute-only state, regenerate.
                    return false;
                }

                tuples.Add(new Tuple<Space, Geometry3DObjectCollection>(space, geometry3DObjectCollection));
            }

            if (tuples.Count == 0)
            {
                return false;
            }

            // A full regeneration feeds every sectioned space into the legend, including spaces hidden by a
            // view appearance override - which are absent from this model (skipped at build time), so their
            // legend contribution cannot be reproduced here. Bail out to the full path when any space is
            // hidden; visibility itself cannot change under an AttributeModification.
            List<Space> spaces = adjacencyCluster.GetSpaces();
            if (spaces != null)
            {
                foreach (Space space in spaces)
                {
                    SurfaceAppearance surfaceAppearance = Query.SurfaceAppearance(space, twoDimensionalViewSettings);
                    if (surfaceAppearance == null || surfaceAppearance.Opacity == 0 || !surfaceAppearance.Visible)
                    {
                        return false;
                    }
                }
            }

            List<LegendItemData> legendItemDatas = new List<LegendItemData>();
            foreach (Tuple<Space, Geometry3DObjectCollection> tuple in tuples)
            {
                if (Query.TryGetValue(tuple.Item1, adjacencyCluster, twoDimensionalViewSettings, out object value, out string text, out _))
                {
                    legendItemDatas.Add(new LegendItemData(tuple.Item1, value, text));
                }
            }

            bool editable = Query.Editable<SpaceAppearanceSettings>(twoDimensionalViewSettings);

            Dictionary<Guid, LegendItem> dictionary_LegendItem = Query.LegendItemDictionary(legendItemDatas, editable, Query.UndefinedLegendItem());
            if (dictionary_LegendItem == null)
            {
                return false;
            }

            Legend legend = twoDimensionalViewSettings.Legend;
            if (legend != null)
            {
                if (dictionary_LegendItem.Count != 0)
                {
                    legend.Refresh(dictionary_LegendItem.Values, true, true);
                }
                else
                {
                    legend = null;
                }
            }

            spaceGuids = new HashSet<Guid>();

            foreach (Tuple<Space, Geometry3DObjectCollection> tuple in tuples)
            {
                Space space = tuple.Item1;

                Color? color = null;
                if (dictionary_LegendItem.TryGetValue(space.Guid, out LegendItem legendItem) && legendItem != null)
                {
                    if (legend != null)
                    {
                        LegendItem legendItem_Legend = legend.Find(legendItem.Text);
                        if (legendItem_Legend != null)
                        {
                            legendItem = legendItem_Legend;
                        }
                    }

                    color = Core.UI.Convert.ToMedia(legendItem.Color);
                }

                if (color == null || !color.HasValue)
                {
                    color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightGray).ToMedia();
                }

                SurfaceAppearance surfaceAppearance = Query.SurfaceAppearance(space, twoDimensionalViewSettings, color);
                if (surfaceAppearance == null)
                {
                    continue;
                }

                foreach (ISAMGeometry3DObject sAMGeometry3DObject in tuple.Item2)
                {
                    Face3DObject face3DObject = sAMGeometry3DObject as Face3DObject;
                    if (face3DObject == null)
                    {
                        continue;
                    }

                    if (SameAppearance(face3DObject.SurfaceAppearance, surfaceAppearance))
                    {
                        continue;
                    }

                    face3DObject.SurfaceAppearance = surfaceAppearance;
                    spaceGuids.Add(space.Guid);
                }
            }

            if (legend != null)
            {
                twoDimensionalViewSettings.Legend = legend;
            }
            else if (dictionary_LegendItem.Count != 0)
            {
                twoDimensionalViewSettings.Legend = new Legend(Query.LegendName<Space>(twoDimensionalViewSettings), dictionary_LegendItem.Values);
            }
            else
            {
                twoDimensionalViewSettings.Legend = null;
            }

            geometryObjectModel.SetValue(GeometryObjectModelParameter.ViewSettings, twoDimensionalViewSettings);

            return true;
        }

        private static bool SameAppearance(SurfaceAppearance surfaceAppearance_1, SurfaceAppearance surfaceAppearance_2)
        {
            if (surfaceAppearance_1 == null || surfaceAppearance_2 == null)
            {
                return surfaceAppearance_1 == surfaceAppearance_2;
            }

            if (surfaceAppearance_1.Color.ToArgb() != surfaceAppearance_2.Color.ToArgb() || surfaceAppearance_1.Opacity != surfaceAppearance_2.Opacity)
            {
                return false;
            }

            CurveAppearance curveAppearance_1 = surfaceAppearance_1.CurveAppearance;
            CurveAppearance curveAppearance_2 = surfaceAppearance_2.CurveAppearance;
            if (curveAppearance_1 == null || curveAppearance_2 == null)
            {
                return curveAppearance_1 == curveAppearance_2;
            }

            return curveAppearance_1.Color.ToArgb() == curveAppearance_2.Color.ToArgb() && curveAppearance_1.Opacity == curveAppearance_2.Opacity && curveAppearance_1.Thickness == curveAppearance_2.Thickness;
        }
    }
}
