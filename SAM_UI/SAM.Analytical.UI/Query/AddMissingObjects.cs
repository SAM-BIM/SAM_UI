// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI
{
    public static partial class Query
    {
        /// <summary>
        /// WPF replacement for SAM.Analytical.Windows.Query.AddMissingObjects: scans the given
        /// resource paths for internal conditions / materials / profiles referenced by the model but
        /// not present in its libraries, and adds them. The legacy ProgressForms are dropped.
        /// </summary>
        public static AnalyticalModel AddMissingObjects(AnalyticalModel analyticalModel, IEnumerable<string> paths, out List<IJSAMObject> jSAMObjects, System.Windows.Window owner = null)
        {
            jSAMObjects = null;

            if (analyticalModel == null)
            {
                return null;
            }

            if (paths == null || paths.Count() == 0)
            {
                return new AnalyticalModel(analyticalModel);
            }

            List<string> names_InternalCondition = analyticalModel.MissingInternalConditionsNames();
            List<string> names_Material = analyticalModel.MissingMaterialsNames();
            Dictionary<ProfileType, List<string>> dictionary_ProfileName = analyticalModel.MissingProfileNameDictionary();

            names_InternalCondition = names_InternalCondition == null || names_InternalCondition.Count == 0 ? null : names_InternalCondition;
            names_Material = names_Material == null || names_Material.Count == 0 ? null : names_Material;
            dictionary_ProfileName = dictionary_ProfileName == null || dictionary_ProfileName.Count == 0 ? null : dictionary_ProfileName;

            if (names_InternalCondition == null && names_Material == null && dictionary_ProfileName == null)
            {
                MessageBox.Show("Nothing to be added");
                return null;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;
            ProfileLibrary profileLibrary = analyticalModel.ProfileLibrary;

            jSAMObjects = new List<IJSAMObject>();

            HashSet<string> paths_Temp = new HashSet<string>();
            foreach (string path in paths)
            {
                if (System.IO.File.Exists(path))
                {
                    paths_Temp.Add(path);
                }

                if (System.IO.Directory.Exists(path))
                {
                    string[] paths_Temp_Temp = System.IO.Directory.GetFiles(path, "*.*");
                    if (paths_Temp_Temp != null)
                    {
                        foreach (string path_Temp in paths_Temp_Temp)
                        {
                            paths_Temp.Add(path_Temp);
                        }
                    }
                }
            }

            List<Tuple<string, IJSAMObject>> jSAMObjects_Temp = new List<Tuple<string, IJSAMObject>>();
            foreach (string path in paths_Temp)
            {
                try
                {
                    Import<IJSAMObject>(path, out List<IJSAMObject> jSAMObjects_Temp_Temp, null, new ImportOptions() { SuppressMessages = true, UserSelection = false }, owner);
                    if (jSAMObjects_Temp_Temp != null)
                    {
                        jSAMObjects_Temp.AddRange(jSAMObjects_Temp_Temp.ConvertAll(x => new Tuple<string, IJSAMObject>(path, x)));
                    }
                }
                catch
                {
                }
            }

            if (names_InternalCondition != null)
            {
                List<InternalCondition> internalConditions = jSAMObjects_Temp.FindAll(x => x.Item2 is InternalCondition).ConvertAll(x => (InternalCondition)x.Item2);
                foreach (string name in names_InternalCondition)
                {
                    InternalCondition internalCondition = internalConditions.Find(x => x?.Name == name);
                    if (internalCondition != null && adjacencyCluster.AddObject(internalCondition))
                    {
                        jSAMObjects.Add(internalCondition);
                    }
                }
            }

            if (names_Material != null)
            {
                List<IMaterial> materials = jSAMObjects_Temp.FindAll(x => x.Item2 is IMaterial).ConvertAll(x => (IMaterial)x.Item2);
                foreach (string name in names_Material)
                {
                    IMaterial material = materials.Find(x => x?.Name == name);
                    if (material != null && materialLibrary.Add(material))
                    {
                        jSAMObjects.Add(material);
                    }
                }
            }

            if (dictionary_ProfileName != null)
            {
                List<Profile> profiles = jSAMObjects_Temp.FindAll(x => x.Item2 is Profile).ConvertAll(x => (Profile)x.Item2);
                if (profiles.Count != 0)
                {
                    ProfileLibrary profileLibrary_Temp = new ProfileLibrary("Temp ProfileLibrary", profiles);
                    foreach (KeyValuePair<ProfileType, List<string>> keyValuePair in dictionary_ProfileName)
                    {
                        foreach (string name in keyValuePair.Value)
                        {
                            if (string.IsNullOrEmpty(name))
                            {
                                continue;
                            }

                            Profile profile = profileLibrary_Temp.GetProfile(name, keyValuePair.Key, false) ?? profileLibrary_Temp.GetProfile(name, keyValuePair.Key, true);
                            if (profile != null && profileLibrary.Add(profile))
                            {
                                jSAMObjects.Add(profile);
                            }
                        }
                    }
                }
            }

            return new AnalyticalModel(analyticalModel, adjacencyCluster, materialLibrary, profileLibrary);
        }
    }
}
