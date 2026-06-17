using SAM.Core;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void AssignPanelConstruction(this UIAnalyticalModel uIAnalyticalModel, IEnumerable<Panel> panels)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null || panels == null || panels.Count() == 0)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;

            List<Construction> constructions = adjacencyCluster?.GetConstructions();
            if(constructions == null || constructions.Count == 0)
            {
                MessageBox.Show("Constructions missing.");
                return;
            }

            HashSet<string> names = new HashSet<string>();
            foreach(Panel panel in panels)
            {
                names.Add(panel?.Construction?.Name);
            }

            Construction construction = null;
            SAM.Core.UI.WPF.SearchWindow searchWindow = new SAM.Core.UI.WPF.SearchWindow(constructions, x => (x as Construction)?.Name)
            {
                SelectionMode = System.Windows.Controls.SelectionMode.Single,
                Title = "Select Construction"
            };
            if (names != null && names.Count == 1)
            {
                searchWindow.SearchText = names.First();
            }

            if (searchWindow.ShowDialog() != true)
            {
                return;
            }

            construction = searchWindow.GetSelectedItems<Construction>()?.FirstOrDefault();

            if(construction == null)
            {
                return;
            }

            List<SAMObject> sAMObjects = new List<SAMObject>();
            foreach(Panel panel_Temp in panels)
            {
                if(panel_Temp == null)
                {
                    continue;
                }

                Panel panel = adjacencyCluster.GetObject<Panel>(panel_Temp.Guid);
                if(panel == null)
                {
                    continue;
                }

                panel = Analytical.Create.Panel(panel, construction);
                adjacencyCluster.AddObject(panel);
                sAMObjects.Add(panel);
            }

            uIAnalyticalModel.SetJSAMObject(new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, analyticalModel.ProfileLibrary), new AnalyticalModelModification(sAMObjects));
        }
    }
}