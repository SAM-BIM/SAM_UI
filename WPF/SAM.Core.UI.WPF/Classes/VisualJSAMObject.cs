﻿using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SAM.Core.UI.WPF
{
    public class VisualJSAMObject<T> : ModelVisual3D, IVisualJSAMObject where T : IJSAMObject
    {
        private Model3D model3D;
        
        protected T jSAMObject;

        public VisualJSAMObject(T jSAMObject)
        {
            this.jSAMObject = jSAMObject;
        }

        public GeometryModel3D GeometryModel3D
        {
            get
            {
                return Content as GeometryModel3D;
            }
        }

        public Model3DGroup Model3DGroup
        {
            get
            {
                return Content as Model3DGroup;
            }
        }

        public T JSAMObject
        {
            get
            {
                return jSAMObject;
            }
        }

        public double Opacity
        {
            get
            {
                DiffuseMaterial diffuseMaterial = GeometryModel3D?.Material as DiffuseMaterial;
                if (diffuseMaterial == null)
                {
                    return double.NaN;
                }

                SolidColorBrush solidColorBrush = diffuseMaterial.Brush as SolidColorBrush;
                if (solidColorBrush == null)
                {
                    return double.NaN;
                }

                return solidColorBrush.Opacity;
            }

            set
            {
                List<DiffuseMaterial> diffuseMaterials = new List<DiffuseMaterial>();
                if (Content is GeometryModel3D)
                {
                    DiffuseMaterial diffuseMaterial = GeometryModel3D.Material as DiffuseMaterial;
                    if (diffuseMaterial != null)
                    {
                        diffuseMaterials.Add(diffuseMaterial);
                    }

                }
                else if (Content is Model3DGroup)
                {
                    Model3DGroup model3DGroup = Model3DGroup;
                    if (model3DGroup != null && model3DGroup.Children != null)
                    {
                        Modify.Opacity(model3DGroup, value);
                    }

                }
                else if (Children != null)
                {
                    foreach (Visual3D visual3D in Children)
                    {
                        if (!(visual3D is IVisualJSAMObject))
                        {
                            continue;
                        }

                        ((IVisualJSAMObject)visual3D).Opacity = value;
                    }
                }

                if(diffuseMaterials == null || diffuseMaterials.Count == 0)
                {
                    return;
                }

                foreach(DiffuseMaterial diffuseMaterial in diffuseMaterials)
                {
                    SolidColorBrush solidColorBrush = diffuseMaterial.Brush as SolidColorBrush;
                    if (solidColorBrush == null)
                    {
                        continue;
                    }

                    if (solidColorBrush.Opacity != value)
                    {
                        solidColorBrush.Opacity = value;
                    }
                }
            }
        }

        public new Model3D Content
        {
            get
            {
                return base.Content;
            }

            set
            {
                base.Content = value;
                if(model3D == null)
                {
                    model3D = value?.Clone();
                }
            }
        }

        public void Restore()
        {
            base.Content = model3D;
        }

        public virtual bool SetHighlight(bool highlight)
        {
            double opacity = highlight ? 0.80 : 1;
            if(Opacity != opacity)
            {
                Opacity = opacity;
                return true;
            }

            return false;
        }

        public virtual bool Similar(IJSAMObject jSAMObject)
        {
            if(jSAMObject == null)
            {
                return false;
            }

            System.Type type = this.jSAMObject?.GetType();
            if(type == null)
            {
                return false;
            }

            if(!type.Equals(jSAMObject.GetType()))
            {
                return false;
            }

            if(!(jSAMObject is SAMObject))
            {
                return true;
            }

            return ((SAMObject)jSAMObject).Guid == ((SAMObject)(object)this.jSAMObject).Guid;
        }

        public virtual bool SetSelected(bool selected)
        {
            if(!selected)
            {
                Restore();
                return true;
            }

            List<DiffuseMaterial> diffuseMaterials = new List<DiffuseMaterial>();
            if (Content is GeometryModel3D)
            {
                DiffuseMaterial diffuseMaterial = GeometryModel3D.Material as DiffuseMaterial;
                if (diffuseMaterial != null)
                {
                    diffuseMaterials.Add(diffuseMaterial);
                }

            }
            else if (Content is Model3DGroup)
            {
                Model3DGroup model3DGroup = Model3DGroup;
                if (model3DGroup != null && model3DGroup.Children != null)
                {
                    foreach (Model3D model3D in model3DGroup.Children)
                    {
                        if (!(model3D is IVisualJSAMObject))
                        {
                            DiffuseMaterial diffuseMaterial = (model3D as GeometryModel3D)?.Material as DiffuseMaterial;
                            if (diffuseMaterial != null)
                            {
                                diffuseMaterials.Add(diffuseMaterial);
                            }
                            continue;
                        }

                        ((IVisualJSAMObject)model3D).SetSelected(selected);
                    }
                }

            }
            else if (Children != null)
            {
                foreach (Visual3D visual3D in Children)
                {
                    if (!(visual3D is IVisualJSAMObject))
                    {
                        continue;
                    }

                    ((IVisualJSAMObject)visual3D).SetSelected(selected);
                }
            }

            if (diffuseMaterials == null || diffuseMaterials.Count == 0)
            {
                return false;
            }

            foreach (DiffuseMaterial diffuseMaterial in diffuseMaterials)
            {
                SolidColorBrush solidColorBrush = diffuseMaterial.Brush as SolidColorBrush;
                if (solidColorBrush == null)
                {
                    continue;
                }

                //solidColorBrush.Color = Color.FromRgb(0,0, 255);
            }

            return true;
        }
    }
}
