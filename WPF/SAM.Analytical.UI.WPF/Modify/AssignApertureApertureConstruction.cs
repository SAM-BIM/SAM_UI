using SAM.Core;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void AssignApertureApertureConstruction(this UIAnalyticalModel uIAnalyticalModel, IEnumerable<Aperture> apertures)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null || apertures == null || apertures.Count() == 0)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;

            List<ApertureConstruction> apertureConstructions = adjacencyCluster?.GetApertureConstructions();
            if(apertureConstructions == null || apertureConstructions.Count == 0)
            {
                MessageBox.Show("ApertureConstructions missing.");
                return;
            }

            HashSet<string> names = new HashSet<string>();
            foreach(Aperture aperture in apertures)
            {
                names.Add(aperture?.ApertureConstruction?.Name);
            }

            ApertureConstruction apertureConstruction = null;
            SAM.Core.UI.WPF.SearchWindow searchWindow = new SAM.Core.UI.WPF.SearchWindow(apertureConstructions, x => (x as ApertureConstruction)?.Name)
            {
                SelectionMode = System.Windows.Controls.SelectionMode.Single,
                Title = "Select ApertureConstruction"
            };
            if (names != null && names.Count == 1)
            {
                searchWindow.SearchText = names.First();
            }

            if (searchWindow.ShowDialog() != true)
            {
                return;
            }

            apertureConstruction = searchWindow.GetSelectedItems<ApertureConstruction>()?.FirstOrDefault();

            if(apertureConstruction == null)
            {
                return;
            }

            List<SAMObject> sAMObjects = new List<SAMObject>();
            foreach(Aperture aperture in apertures)
            {
                Panel panel = adjacencyCluster.GetPanel(aperture);
                if(panel == null)
                {
                    continue;
                }

                panel = Analytical.Create.Panel(panel);

                Aperture aperture_New = new Aperture(aperture, apertureConstruction);

                panel.RemoveAperture(aperture.Guid);

                panel.AddAperture(aperture_New);

                adjacencyCluster.AddObject(panel);
                if(sAMObjects.Find(x => x.Guid == panel.Guid) == null)
                {
                    sAMObjects.Add(panel);
                }
            }

            uIAnalyticalModel.SetJSAMObject(new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, analyticalModel.ProfileLibrary), new AnalyticalModelModification(sAMObjects));
        }
    }
}