// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Series;

namespace SAM.Core.Mollier.UI
{
    /// <summary>
    /// A LineSeries that keeps a per-vertex Tag aligned by index with <see cref="LineSeries.Points"/>.
    /// OxyPlot's DataPoint is a value type with no Tag, but the WinForms chart attached objects to
    /// individual line vertices (e.g. process start/end carry a UIMollierProcessPoint / MollierPoint).
    /// This preserves that payload natively so 2d (hit-test, label placement) can recover it.
    /// </summary>
    public class MollierLineSeries : LineSeries
    {
        public List<object> PointTags { get; } = new List<object>();

        /// <summary>Adds a vertex together with its optional tag, keeping Points and PointTags in lockstep.</summary>
        public void AddPoint(double x, double y, object tag = null)
        {
            Points.Add(new DataPoint(x, y));
            PointTags.Add(tag);
        }

        /// <summary>Returns the tag attached to the vertex at <paramref name="index"/>, or null.</summary>
        public object GetPointTag(int index)
        {
            return index >= 0 && index < PointTags.Count ? PointTags[index] : null;
        }
    }
}
