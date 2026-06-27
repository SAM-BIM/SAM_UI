// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Object;
using SAM.Geometry.Object.Spatial;
using System.Windows.Media.Media3D;

namespace SAM.Geometry.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Rebuilds the surface Model3Ds of the visual from their attached Face3DObjects, whose
        /// SurfaceAppearance has been updated in place (attribute-only edits - see
        /// AnalyticalWindow/AttributeModification). Text and curve Model3Ds are left untouched, so the
        /// expensive 3D text meshes are reused; the geometry itself is unchanged by construction.
        /// </summary>
        public static void RefreshAppearance(this Visual3D visual3D)
        {
            ModelVisual3D modelVisual3D = visual3D as ModelVisual3D;
            if (modelVisual3D == null)
            {
                return;
            }

            foreach (Visual3D visual3D_Child in modelVisual3D.Children)
            {
                RefreshAppearance(visual3D_Child);
            }

            Model3D model3D = modelVisual3D.Content;
            if (model3D == null)
            {
                return;
            }

            ISAMGeometryObject sAMGeometryObject = null;

            if (model3D is Model3DGroup)
            {
                Model3DGroup model3DGroup = (Model3DGroup)model3D;

                for (int i = 0; i < model3DGroup.Children.Count; i++)
                {
                    sAMGeometryObject = Core.UI.WPF.Query.JSAMObject<ISAMGeometryObject>(model3DGroup.Children[i]);
                    if (sAMGeometryObject is Face3DObject)
                    {
                        Model3D model3D_Temp = Create.Model3D((Face3DObject)sAMGeometryObject);
                        if (model3D_Temp != null)
                        {
                            Core.UI.WPF.Modify.SetIJSAMObject(model3D_Temp, sAMGeometryObject);
                            model3DGroup.Children[i] = model3D_Temp;
                        }
                    }
                }
            }

            sAMGeometryObject = Core.UI.WPF.Query.JSAMObject<ISAMGeometryObject>(model3D);
            if (sAMGeometryObject is Face3DObject)
            {
                Model3D model3D_Temp = Create.Model3D((Face3DObject)sAMGeometryObject);
                if (model3D_Temp != null)
                {
                    Core.UI.WPF.Modify.SetIJSAMObject(model3D_Temp, sAMGeometryObject);
                    modelVisual3D.Content = model3D_Temp;
                }
            }
        }
    }
}
