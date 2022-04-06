﻿using SAM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public partial class AnalyticalForm : Form
    {
        private UIAnalyticalModel uIAnalyticalModel;

        public AnalyticalForm()
        {
            InitializeComponent();
        }

        private void AnalyticalForm_Load(object sender, EventArgs e)
        {
            RibbonPanel_Tools_Hydra.Visible = false;

            uIAnalyticalModel = new UIAnalyticalModel();
            uIAnalyticalModel.Opened += UIAnalyticalModel_Refresh;
            uIAnalyticalModel.Closed += UIAnalyticalModel_Refresh;
            uIAnalyticalModel.Modified += UIAnalyticalModel_Modified;

            Refresh();
        }

        private void UIAnalyticalModel_Modified(object sender, EventArgs e)
        {
            Refresh_TreeView();
        }

        private void UIAnalyticalModel_Refresh(object sender, EventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            RibbonTab_Edit.Enabled = true;
            RibbonTab_File.Enabled = true;
            RibbonTab_Help.Enabled = true;
            RibbonTab_Library.Enabled = true;
            RibbonTab_Tools.Enabled = true;
            RibbonTab_Simulate.Enabled = true;

            RibbonButton_File_SaveAs.Enabled = true;
            RibbonButton_File_Save.Enabled = true;
            RibbonButton_File_Close.Enabled = true;

            if (uIAnalyticalModel?.JSAMObject == null)
            {
                RibbonTab_Edit.Enabled = false;
                RibbonTab_Library.Enabled = false;
                RibbonButton_File_SaveAs.Enabled = false;
                RibbonButton_File_Save.Enabled = false;
                RibbonButton_File_Close.Enabled = false;
                RibbonTab_Simulate.Enabled = false;
            }

            Refresh_TreeView();
        }

        private List<object> GetExpandedTags(TreeNodeCollection treeNodeCollection)
        {
            if (treeNodeCollection == null)
            {
                return null;
            }

            List<object> result = new List<object>();

            foreach (TreeNode treeNode in treeNodeCollection)
            {
                if (treeNode.IsExpanded)
                {
                    result.Add(treeNode.Tag);
                    List<object> expandedTags = GetExpandedTags(treeNode.Nodes);
                    if (expandedTags != null)
                    {
                        result.AddRange(expandedTags);
                    }
                }
            }

            return result;
        }
        
        private void Refresh_TreeView()
        {
            List<object> expandedTags = GetExpandedTags(TreeView_AnalyticalModel.Nodes);
            
            TreeView_AnalyticalModel.Nodes.Clear();

            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }


            string name = analyticalModel.Name;
            if(string.IsNullOrWhiteSpace(name))
            {
                name = "???";
            }

            TreeNode treeNode_AnalyticalModel = TreeView_AnalyticalModel.Nodes.Add(name);
            treeNode_AnalyticalModel.Tag = analyticalModel;

            TreeNode treeNode_Spaces = treeNode_AnalyticalModel.Nodes.Add("Spaces");
            treeNode_Spaces.Tag = typeof(Space);
            TreeNode treeNode_Shades = treeNode_AnalyticalModel.Nodes.Add("Shades");
            treeNode_Shades.Tag = typeof(Panel);
            TreeNode treeNode_Profiles = treeNode_AnalyticalModel.Nodes.Add("Profiles");
            treeNode_Profiles.Tag = typeof(Profile);
            TreeNode treeNode_Materials = treeNode_AnalyticalModel.Nodes.Add("Materials");
            treeNode_Materials.Tag = typeof(IMaterial);

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            if(adjacencyCluster != null)
            {
                List<Space> spaces = adjacencyCluster.GetSpaces();
                if(spaces != null)
                {
                    foreach(Space space in spaces)
                    {
                        TreeNode treeNode_Space = treeNode_Spaces.Nodes.Add(space.Name);
                        treeNode_Space.Tag = space;
                        List<Panel> panels_Space = adjacencyCluster.GetPanels(space);
                        if(panels_Space != null)
                        {
                            foreach(Panel panel in panels_Space)
                            {
                                TreeNode treeNode_Panel = treeNode_Space.Nodes.Add(panel.Name);
                                treeNode_Panel.Tag = panel;

                                List<Aperture> apertures = panel.Apertures;
                                if(apertures != null)
                                {
                                    foreach(Aperture aperture in apertures)
                                    {
                                        TreeNode treeNode_Aperture = treeNode_Panel.Nodes.Add(aperture.Name);
                                        treeNode_Aperture.Tag = aperture;

                                        if (expandedTags.Find(x => x is Aperture && ((Aperture)x).Guid == aperture.Guid) != null)
                                        {
                                            treeNode_Aperture.Expand();
                                        }
                                    }
                                }

                                if (expandedTags.Find(x => x is Panel && ((Panel)x).Guid == panel.Guid) != null)
                                {
                                    treeNode_Panel.Expand();
                                }
                            }
                        }

                        if (expandedTags.Find(x => x is Space && ((Space)x).Guid == space.Guid) != null)
                        {
                            treeNode_Space.Expand();
                        }
                    }
                }

                List<Panel> panels = adjacencyCluster.GetPanels();
                if(panels != null)
                {
                    foreach(Panel panel in panels)
                    {
                        List<Space> spaces_Panel = adjacencyCluster.GetSpaces(panel);
                        if(spaces_Panel == null || spaces_Panel.Count == 0)
                        {
                            TreeNode treeNode_Shade = treeNode_Shades.Nodes.Add(panel.Name);
                            treeNode_Shade.Tag = panel;

                            if (expandedTags.Find(x => x is Panel && ((Panel)x).Guid == panel.Guid) != null)
                            {
                                treeNode_Shade.Expand();
                            }
                        }
                    }
                }

            }

            List<Profile> profiles = analyticalModel.ProfileLibrary?.GetProfiles();
            if(profiles != null)
            {
                foreach(Profile profile in profiles)
                {
                    TreeNode treeNode_Profile = treeNode_Profiles.Nodes.Add(profile.Name);
                    treeNode_Profile.Tag = profile;
                }
            }

            List<IMaterial> materials = analyticalModel.MaterialLibrary?.GetMaterials();
            if (materials != null)
            {
                foreach (IMaterial material in materials)
                {
                    TreeNode treeNode_Material = treeNode_Materials.Nodes.Add(material.Name);
                    treeNode_Material.Tag = material;
                }
            }

            treeNode_AnalyticalModel.Expand();
            if(expandedTags != null && expandedTags.Count != 0)
            {
                if (expandedTags.Contains(treeNode_Spaces.Tag))
                {
                    treeNode_Spaces.Expand();
                }

                if (expandedTags.Contains(treeNode_Shades.Tag))
                {
                    treeNode_Shades.Expand();
                }

                if (expandedTags.Contains(treeNode_Profiles.Tag))
                {
                    treeNode_Profiles.Expand();
                }

                if (expandedTags.Contains(treeNode_Materials.Tag))
                {
                    treeNode_Materials.Expand();
                }


            }


        }

        private void AnalyticalForm_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void RibbonButton_Tools_EditLibrary_Click(object sender, EventArgs e)
        {
            string path = null;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SAM");
                if(System.IO.Directory.Exists(directory))
                {
                    openFileDialog.InitialDirectory = directory;
                }

                openFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                path = openFileDialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            List<ISAMLibrary> libraries = null;
            try
            {
                libraries = Core.Convert.ToSAM<ISAMLibrary>(path);
            }
            catch (Exception exception)
            {
                MessageBox.Show("Could not load file");
            }

            if (libraries == null || libraries.Count == 0)
            {
                return;
            }

            ISAMLibrary library = libraries.FirstOrDefault();

            if (library is ConstructionLibrary)
            {
                DialogResult dialogResult = MessageBox.Show(this, "Use default Material Library?", "Material Library", MessageBoxButtons.YesNo);

                MaterialLibrary materialLibrary = null;
                if (dialogResult == DialogResult.Yes)
                {
                    materialLibrary = Query.DefaultMaterialLibrary();
                }
                else if (dialogResult == DialogResult.No)
                {
                    using (OpenFileDialog openFileDialog = new OpenFileDialog())
                    {
                        string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SAM");
                        if (System.IO.Directory.Exists(directory))
                        {
                            openFileDialog.InitialDirectory = directory;
                        }

                        openFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
                        openFileDialog.FilterIndex = 2;
                        openFileDialog.RestoreDirectory = true;
                        if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                        {
                            return;
                        }

                        List<MaterialLibrary> materialLibraries = null;
                        try
                        {
                            materialLibraries = Core.Convert.ToSAM<MaterialLibrary>(openFileDialog.FileName);
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show("Could not load file");
                        }

                        if (materialLibraries == null || materialLibraries.Count == 0)
                        {
                            return;
                        }

                        materialLibrary = materialLibraries.FirstOrDefault();
                    }
                }
                else
                {
                    return;
                }

                using (Windows.Forms.ConstructionLibraryForm constructionLibraryForm = new Windows.Forms.ConstructionLibraryForm(materialLibrary, (ConstructionLibrary)library))
                {
                    if (constructionLibraryForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    library = constructionLibraryForm.ConstructionLibrary;
                }
            }
            else if (library is MaterialLibrary)
            {
                using (Core.Windows.Forms.MaterialLibraryForm materialLibraryForm = new Core.Windows.Forms.MaterialLibraryForm((MaterialLibrary)library, SAM.Core.Query.Enums(typeof(OpaqueMaterialParameter), typeof(TransparentMaterialParameter))))
                {
                    if (materialLibraryForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    library = materialLibraryForm.MaterialLibrary;
                }
            }

            if (library == null)
            {
                return;
            }

            Core.Convert.ToFile(new IJSAMObject[] { library }, path);
        }

        private void RibbonButton_File_Open_Click(object sender, EventArgs e)
        {
            string path = null;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SAM");
                if (System.IO.Directory.Exists(directory))
                {
                    openFileDialog.InitialDirectory = directory;
                }
                openFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                path = openFileDialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            uIAnalyticalModel.Path = path;

            uIAnalyticalModel.Open();
        }

        private void RibbonButton_Tools_OpenTSD_Click(object sender, EventArgs e)
        {
            string path = Core.Tas.Query.TSDPath();

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            System.Diagnostics.Process.Start(path);
        }

        private void RibbonButton_Tools_OpenTBD_Click(object sender, EventArgs e)
        {
            string path = Core.Tas.Query.TBDPath();

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            System.Diagnostics.Process.Start(path);
        }

        private void RibbonButton_Tools_OpenT3D_Click(object sender, EventArgs e)
        {
            string path = Core.Tas.Query.TAS3DPath();

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            System.Diagnostics.Process.Start(path);
        }

        private void RibbonButton_Tools_OpenTPD_Click(object sender, EventArgs e)
        {
            string path = Core.Tas.Query.TPDPath();

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            System.Diagnostics.Process.Start(path);
        }

        private void RibbonButton_File_Close_Click(object sender, EventArgs e)
        {
            uIAnalyticalModel?.Close();
        }

        private void RibbonButton_File_SaveAs_Click(object sender, EventArgs e)
        {
            string path = null;
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;
                if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                path = saveFileDialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            uIAnalyticalModel.Path = path;

            uIAnalyticalModel.Save();
        }

        private void AnalyticalForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(uIAnalyticalModel != null && !uIAnalyticalModel.Close())
            {
                e.Cancel = true;
            }
        }

        private void RibbonButton_Library_MaterialLibrary_Click(object sender, EventArgs e)
        {
            if(uIAnalyticalModel?.JSAMObject == null)
            {
                return;
            }

            MaterialLibrary materialLibrary = uIAnalyticalModel.JSAMObject.MaterialLibrary;

            using (Core.Windows.Forms.MaterialLibraryForm materialLibraryForm = new Core.Windows.Forms.MaterialLibraryForm(materialLibrary, Core.Query.Enums(typeof(IMaterial))))
            {
                if(materialLibraryForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                materialLibrary = materialLibraryForm.MaterialLibrary;
            }

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(uIAnalyticalModel.JSAMObject, uIAnalyticalModel.JSAMObject.AdjacencyCluster, materialLibrary, uIAnalyticalModel.JSAMObject.ProfileLibrary);
        }

        private void RibbonButton_Library_ApertureConstruction_Click(object sender, EventArgs e)
        {
            AdjacencyCluster adjacencyCluster = uIAnalyticalModel?.JSAMObject?.AdjacencyCluster;
            if(adjacencyCluster == null)
            {
                return;
            }

            List<ApertureConstruction> apertureConstructions = adjacencyCluster.GetApertureConstructions();
            ApertureConstructionLibrary apertureConstructionLibrary = new ApertureConstructionLibrary(uIAnalyticalModel.JSAMObject.Name);
            apertureConstructions?.ForEach(x => apertureConstructionLibrary.Add(x));

            MaterialLibrary materialLibrary = uIAnalyticalModel.JSAMObject.MaterialLibrary;

            using (Windows.Forms.ApertureConstructionLibraryForm apertureConstructionLibraryForm = new Windows.Forms.ApertureConstructionLibraryForm(materialLibrary, apertureConstructionLibrary))
            {
                apertureConstructionLibraryForm.Text = "Aperture Constructions";
                if (apertureConstructionLibraryForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                apertureConstructionLibrary = apertureConstructionLibraryForm.ApertureConstructionLibrary;
            }

            adjacencyCluster.ReplaceApertureConstructions(apertureConstructionLibrary);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(uIAnalyticalModel.JSAMObject, adjacencyCluster);
        }

        private void RibbonButton_Library_ConstructionLibrary_Click(object sender, EventArgs e)
        {
            AdjacencyCluster adjacencyCluster = uIAnalyticalModel.JSAMObject.AdjacencyCluster;
            if(adjacencyCluster == null)
            {
                return;
            }

            List<Construction> constructions = adjacencyCluster.GetConstructions();

            ConstructionLibrary constructionLibrary = new ConstructionLibrary(uIAnalyticalModel.JSAMObject.Name);
            constructions?.ForEach(x => constructionLibrary.Add(x));

            MaterialLibrary materialLibrary = uIAnalyticalModel.JSAMObject.MaterialLibrary;

            using (Windows.Forms.ConstructionLibraryForm constructionLibraryForm = new Windows.Forms.ConstructionLibraryForm(materialLibrary, constructionLibrary))
            {
                constructionLibraryForm.Text = "Constructions";
                if (constructionLibraryForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                constructionLibrary = constructionLibraryForm.ConstructionLibrary;
            }

            adjacencyCluster.ReplaceConstructions(constructionLibrary);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(uIAnalyticalModel.JSAMObject, adjacencyCluster);

        }

        private void RibbonButton_Help_Wiki_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/HoareLea/SAM/wiki/00-Home");
        }

        private void RibbonButton_Library_InternalConditionLibrary_Click(object sender, EventArgs e)
        {
            MessageBox.Show("To be implemented soon...");
        }

        private void RibbonButton_Tools_Hydra_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://hlhydra.azurewebsites.net/index.html");
        }

        private void RibbonButton_Library_WeatherData_Click(object sender, EventArgs e)
        {
            MessageBox.Show("To be implemented soon...");
        }

        private void RibbonButton_File_New_Click(object sender, EventArgs e)
        {
            if (uIAnalyticalModel == null)
            {
                uIAnalyticalModel = new UIAnalyticalModel();
            }

            if (uIAnalyticalModel.JSAMObject != null)
            {
                if (!uIAnalyticalModel.Close())
                {
                    return;
                }
            }

            string name = null;
            using (Core.Windows.Forms.TextBoxForm<string> textBoxForm = new Core.Windows.Forms.TextBoxForm<string>("Analytical Model", "Project Name"))
            {
                textBoxForm.Value = "New Project";
                if (textBoxForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                name = textBoxForm.Value;
            }

            AnalyticalModel analyticalModel = new AnalyticalModel(Guid.NewGuid(), name);

            uIAnalyticalModel.JSAMObject = analyticalModel;

            Refresh();
        }

        private void RibbonButton_Edit_SAMImport_Click(object sender, EventArgs e)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if(analyticalModel == null)
            {
                return;
            }

            string path = null;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SAM");
                if (System.IO.Directory.Exists(directory))
                {
                    openFileDialog.InitialDirectory = directory;
                }

                openFileDialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                path = openFileDialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return;
            }

            List<IJSAMObject> jSAMObjects = null;
            try
            {
                jSAMObjects = Core.Convert.ToSAM<IJSAMObject>(path);
            }
            catch(Exception exception)
            {
                MessageBox.Show("Cannot open file specified");
                return;
            }

            if(jSAMObjects == null || jSAMObjects.Count == 0)
            {
                MessageBox.Show("No objects to import");
                return;
            }


            List<Tuple<string, string, IJSAMObject>> tuples_All = new List<Tuple<string, string, IJSAMObject>>();
            foreach(IJSAMObject jSAMObject in jSAMObjects)
            {
                if(jSAMObject == null)
                {
                    continue;
                }

                if(jSAMObject is AnalyticalModel)
                {
                    AnalyticalModel analyticalModel_Temp = (AnalyticalModel)jSAMObject;

                    List<IMaterial> materials = analyticalModel_Temp.MaterialLibrary?.GetMaterials();
                    if(materials != null)
                    {
                        foreach(IMaterial material in materials)
                        {
                            tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(Material).Name, material.Name, material));
                        }
                    }

                    List<Construction> constructions_Temp = analyticalModel_Temp.AdjacencyCluster.GetConstructions();
                    if(constructions_Temp != null)
                    {
                        foreach(Construction construction in constructions_Temp)
                        {
                            tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(Construction).Name, construction.Name, construction));
                        }
                    }

                }
                else if (jSAMObject is MaterialLibrary)
                {
                    List<IMaterial> materials = ((MaterialLibrary)jSAMObject).GetMaterials();
                    if (materials != null)
                    {
                        foreach (IMaterial material in materials)
                        {
                            tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(Material).Name, material.Name, material));
                        }
                    }

                }
                else if (jSAMObject is ConstructionLibrary)
                {
                    List<Construction> constructions_Temp = ((ConstructionLibrary)jSAMObject).GetConstructions();
                    if (constructions_Temp != null)
                    {
                        foreach (Construction construction in constructions_Temp)
                        {
                            tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(Construction).Name, construction.Name, construction));
                        }
                    }

                }
                else if (jSAMObject is ApertureConstructionLibrary)
                {
                    List<ApertureConstruction> apertureConstructions_Temp = ((ApertureConstructionLibrary)jSAMObject).GetApertureConstructions();
                    if (apertureConstructions_Temp != null)
                    {
                        foreach (ApertureConstruction apertureConstruction in apertureConstructions_Temp)
                        {
                            tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(ApertureConstruction).Name, apertureConstruction.Name, apertureConstruction));
                        }
                    }

                }
                else if(jSAMObject is IMaterial)
                {
                    tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(Material).Name, ((IMaterial)jSAMObject).Name, jSAMObject));
                }
                else if (jSAMObject is Construction)
                {
                    tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(Construction).Name, ((Construction)jSAMObject).Name, jSAMObject));
                }
                else if(jSAMObject is ApertureConstruction)
                {
                    tuples_All.Add(new Tuple<string, string, IJSAMObject>(typeof(ApertureConstruction).Name, ((ApertureConstruction)jSAMObject).Name, jSAMObject));
                }
            }

            List<Tuple<string, string, IJSAMObject>> tuples_Selected = null;
            using (Core.Windows.Forms.TreeViewForm<Tuple<string, string, IJSAMObject>> treeViewForm = new Core.Windows.Forms.TreeViewForm<Tuple<string, string, IJSAMObject>>("Select Objects", tuples_All, (Tuple<string, string, IJSAMObject> x) => x.Item2, (Tuple<string, string, IJSAMObject> x) => x.Item1))
            {
                if (treeViewForm.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                tuples_Selected = treeViewForm?.SelectedItems;
            }

            if(tuples_Selected == null || tuples_Selected.Count == 0)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;

            List<Construction> constructions = new List<Construction>();
            List<ApertureConstruction> apertureConstructions = new List<ApertureConstruction>();
            foreach (Tuple<string, string, IJSAMObject> tuple in tuples_Selected)
            {
                IJSAMObject jSAMObject = tuple?.Item3;

                if(jSAMObject == null)
                {
                    continue;
                }

                if(jSAMObject is IMaterial)
                {
                    analyticalModel.AddMaterial((IMaterial)jSAMObject);
                }
                else if(jSAMObject is Construction)
                {
                    constructions.Add((Construction)jSAMObject);
                }
                else if (jSAMObject is ApertureConstruction)
                {
                    apertureConstructions.Add((ApertureConstruction)jSAMObject);
                }
            }

            if(constructions != null && constructions.Count != 0)
            {
                adjacencyCluster.UpdateConstructions(constructions);
            }

            if (apertureConstructions != null && apertureConstructions.Count != 0)
            {
                adjacencyCluster.UpdateConstructions(constructions);
            }

            if(apertureConstructions != null || constructions != null)
            {
                HashSet<string> names = new HashSet<string>();

                if(constructions != null)
                {
                    foreach (Construction construction in constructions)
                    {
                        List<ConstructionLayer> constructionLayers = construction?.ConstructionLayers;
                        if(constructionLayers != null)
                        {
                            foreach(ConstructionLayer constructionLayer in constructionLayers)
                            {
                                names.Add(constructionLayer.Name);
                            }
                        }
                    }
                }

                if (apertureConstructions != null)
                {
                    foreach (ApertureConstruction apertureConstruction in apertureConstructions)
                    {
                        List<ConstructionLayer> constructionLayers = null;

                        constructionLayers = apertureConstruction?.PaneConstructionLayers;
                        if (constructionLayers != null)
                        {
                            foreach (ConstructionLayer constructionLayer in constructionLayers)
                            {
                                names.Add(constructionLayer.Name);
                            }
                        }

                        constructionLayers = apertureConstruction?.FrameConstructionLayers;
                        if (constructionLayers != null)
                        {
                            foreach (ConstructionLayer constructionLayer in constructionLayers)
                            {
                                names.Add(constructionLayer.Name);
                            }
                        }
                    }
                }

                List<IMaterial> materials = tuples_All.FindAll(x => x.Item3 is IMaterial).ConvertAll(x => (IMaterial)x.Item3);
                if(materials != null && materials.Count != 0)
                {
                    MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;

                    HashSet<string> names_Missing = new HashSet<string>();
                    foreach(string name in names)
                    {
                        if(materialLibrary.GetMaterial(name) == null)
                        {
                            names_Missing.Add(name);
                        }
                    }

                    if(names_Missing != null && names_Missing.Count != 0)
                    {
                        DialogResult dialogResult = MessageBox.Show(this, "Try to import missing materials?", "Materials", MessageBoxButtons.YesNo);
                        if (dialogResult == DialogResult.Yes)
                        {
                            foreach(string name in names_Missing)
                            {
                                IMaterial material = materials.Find(x => x.Name == name);
                                if(material != null)
                                {
                                    analyticalModel.AddMaterial(material);
                                }
                            }
                        }
                    }
                }
            }

            analyticalModel = new AnalyticalModel(analyticalModel);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster);
        }

        private void RibbonButton_Edit_Location_Click(object sender, EventArgs e)
        {
            MessageBox.Show("To be implemented soon...");
        }

        private void TreeView_AnalyticalModel_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode treeNode = e.Node;

            IJSAMObject jSAMObject = treeNode.Tag as IJSAMObject;
            if(jSAMObject == null)
            {
                return;
            }

            if(jSAMObject is Panel)
            {
                AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
                if(analyticalModel == null)
                {
                    return;
                }

                AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
                if(adjacencyCluster == null)
                {
                    return;
                }

                MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;

                ConstructionLibrary constructionLibrary = null;

                List<Construction> constructions = adjacencyCluster?.GetConstructions();
                if(constructions != null)
                {
                    constructionLibrary = new ConstructionLibrary(analyticalModel.Name);
                    constructions.ForEach(x => constructionLibrary.Add(x));
                }

                Panel panel = (Panel)jSAMObject;

                using (Windows.Forms.PanelForm panelForm = new Windows.Forms.PanelForm(panel, materialLibrary, constructionLibrary, Core.Query.Enums(typeof(Panel))))
                {
                    if(panelForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    panel = panelForm.Panel;
                    constructionLibrary = panelForm.ConstructionLibrary;
                }

                adjacencyCluster.AddObject(panel);

                adjacencyCluster.ReplaceConstructions(constructionLibrary);

                uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster);
            }

            if(jSAMObject is IMaterial)
            {
                IMaterial material = (IMaterial)jSAMObject;

                AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
                if (analyticalModel == null)
                {
                    return;
                }

                MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;
                if(materialLibrary == null)
                {
                    return;
                }

                string uniqueId = materialLibrary.GetUniqueId(material);


                using (Core.Windows.Forms.MaterialForm materialForm = new Core.Windows.Forms.MaterialForm(material, Core.Query.Enums(typeof(IMaterial))))
                {
                    if(materialForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    material = materialForm.Material;
                }

                materialLibrary.Replace(uniqueId, material);

                uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, analyticalModel.AdjacencyCluster, materialLibrary, analyticalModel.ProfileLibrary);
            }

            if (jSAMObject is Aperture)
            {
                AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
                if (analyticalModel == null)
                {
                    return;
                }

                AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
                if (adjacencyCluster == null)
                {
                    return;
                }

                MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;

                ApertureConstructionLibrary apertureConstructionLibrary = null;

                List<ApertureConstruction> apertureConstructions = adjacencyCluster?.GetApertureConstructions();
                if (apertureConstructions != null)
                {
                    apertureConstructionLibrary = new ApertureConstructionLibrary(analyticalModel.Name);
                    apertureConstructions.ForEach(x => apertureConstructionLibrary.Add(x));
                }

                Aperture aperture = (Aperture)jSAMObject;

                using (Windows.Forms.ApertureForm apertureForm = new Windows.Forms.ApertureForm(aperture, materialLibrary, apertureConstructionLibrary, Core.Query.Enums(typeof(Aperture))))
                {
                    if (apertureForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    aperture = apertureForm.Aperture;
                    apertureConstructionLibrary = apertureForm.ApertureConstructionLibrary;
                }

                Panel panel = adjacencyCluster.GetPanel(aperture);
                if(panel != null)
                {
                    panel.RemoveAperture(aperture.Guid);
                    panel.AddAperture(aperture);
                    adjacencyCluster.AddObject(aperture);
                }

                adjacencyCluster.ReplaceApertureConstructions(apertureConstructionLibrary);

                uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster);
            }
        }

        private void RibbonButton_Simulate_ImportWeatherData_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "EPW files (*.epw)|*.epw|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if(openFileDialog.FileName.ToLower().EndsWith("epw"))
                {

                }

                //SAM.Weather.Convert.ToSAM();
            }
        }

        private void RibbonButton_Edit_ModelCheck_Click(object sender, EventArgs e)
        {
            MessageBox.Show("To be implemented soon...");
        }

        private void RibbonPanel_Tools_Tas_Click(object sender, EventArgs e)
        {

        }
    }
}
