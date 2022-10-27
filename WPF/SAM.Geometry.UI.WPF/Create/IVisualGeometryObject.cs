﻿using SAM.Geometry.Spatial;
using System.Windows.Media.Media3D;

namespace SAM.Geometry.UI.WPF
{
    public static partial class Create
    {
        public static IVisualGeometryObject IVisualGeometryObject(this ISAMGeometryObject sAMGeometryObject, Material material, double thickness = 0.001)
        {
            if(sAMGeometryObject == null || material == null)
            {
                return null;
            }

            if(sAMGeometryObject is IFace3DObject)
            {
                return VisualGeometryObject((IFace3DObject)sAMGeometryObject, material, thickness);
            }
            else if(sAMGeometryObject is ISegmentable3DObject)
            {
                return VisualGeometryObject((ISegmentable3DObject)sAMGeometryObject, material, thickness);
            }

            return null;
        }
    }
}
