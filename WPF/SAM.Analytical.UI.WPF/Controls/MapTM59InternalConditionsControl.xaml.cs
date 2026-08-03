using SAM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for MapTM59InternalConditionsControl.xaml
    /// </summary>
    public partial class MapTM59InternalConditionsControl : UserControl
    {
        // Preferred Zone Type categories to auto-select when more than one is available and the
        // user has not chosen one yet - "Flats" is what Modify.MapInternalConditionsByTM59 assumes.
        private static readonly string[] PreferredZoneCategories = { "Flats", "Flat", "Apartments", "Dwellings", "Units" };

        private AdjacencyCluster adjacencyCluster;
        private bool resourceFailureReported = false;

        public MapTM59InternalConditionsControl()
        {
            InitializeComponent();

            mapInternalConditionsControl.AutoMapOnLoad = true;
            mapInternalConditionsControl.MapSourceChanged += MapInternalConditionsControl_MapSourceChanged;

            Load();
        }

        public MapTM59InternalConditionsControl(IEnumerable<Space> spaces, AdjacencyCluster adjacencyCluster, TextMap textMap = null, InternalConditionLibrary internalConditionLibrary = null)
        {
            InitializeComponent();

            mapInternalConditionsControl.AutoMapOnLoad = true;
            mapInternalConditionsControl.MapSourceChanged += MapInternalConditionsControl_MapSourceChanged;

            this.adjacencyCluster = adjacencyCluster;

            mapInternalConditionsControl.TextMap = textMap;
            mapInternalConditionsControl.InternalConditionLibrary = internalConditionLibrary;

            mapInternalConditionsControl.Spaces = spaces?.ToList();

            Load();
        }

        private void MapInternalConditionsControl_MapSourceChanged(object sender, EventArgs e)
        {
            SetMapFunc();
        }

        private void Load()
        {
            LoadZones();
            SetMapFunc();
        }

        private void LoadZones()
        {
            string value = comboBox_ZoneType.Text;

            comboBox_ZoneType.Items.Clear();

            List<Zone> zones = adjacencyCluster?.GetZones();
            if(zones == null || zones.Count == 0)
            {
                return;
            }

            HashSet<string> categories = new HashSet<string>();
            foreach(Zone zone in zones)
            {
                if(zone.TryGetValue(ZoneParameter.ZoneCategory, out string category) && !string.IsNullOrWhiteSpace(category))
                {
                    categories.Add(category);
                }
            }

            foreach(string category in categories)
            {
                comboBox_ZoneType.Items.Add(category);
            }

            if(!string.IsNullOrWhiteSpace(value))
            {
                comboBox_ZoneType.Text = value;
                return;
            }

            // No prior selection to restore - pick a sensible default so preselection (AutoMapOnLoad)
            // has a Zone Type to work with as soon as the dialog opens, instead of requiring the user
            // to pick one first.
            string defaultCategory = SelectDefaultZoneCategory(categories);
            if (defaultCategory != null)
            {
                comboBox_ZoneType.SelectedItem = defaultCategory;
            }
        }

        private static string SelectDefaultZoneCategory(HashSet<string> categories)
        {
            if (categories == null || categories.Count == 0)
            {
                return null;
            }

            if (categories.Count == 1)
            {
                return categories.First();
            }

            foreach (string preferred in PreferredZoneCategories)
            {
                string match = categories.FirstOrDefault(x => string.Equals(x, preferred, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            // More than one category and none of them look like a residential-unit category -
            // leave unselected rather than guessing; the user must pick one.
            return null;
        }

        public TextMap TextMap
        {
            get
            {
                return mapInternalConditionsControl.TextMap;
            }

            set
            {
                mapInternalConditionsControl.TextMap = value;
                SetMapFunc();
            }
        }

        public InternalConditionLibrary InternalConditionLibrary
        {
            get
            {
                return mapInternalConditionsControl.InternalConditionLibrary;
            }

            set
            {
                mapInternalConditionsControl.InternalConditionLibrary = value;
                SetMapFunc();
            }
        }

        public List<Space> Spaces
        {
            get
            {
                return mapInternalConditionsControl.Spaces;
            }

            set
            {
                mapInternalConditionsControl.Spaces = value;
            }
        }

        public AdjacencyCluster AdjacencyCluster
        {
            get
            {
                return adjacencyCluster;
            }

            set
            {
                SetAdjacencyCluster(value);
            }
        }

        private void SetAdjacencyCluster(AdjacencyCluster adjacencyCluster)
        {
            this.adjacencyCluster = adjacencyCluster;
            Load();
        }

        private void comboBox_ZoneType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SetMapFunc must run BEFORE GroupFunc is assigned: the GroupFunc setter immediately
            // triggers a row rebuild (SetSpaces), which would otherwise preselect using the previous
            // MapFunc's stale Zone Type closure.
            SetMapFunc();

            mapInternalConditionsControl.GroupFunc = new Func<Space, string>(x =>
            {
                Zone zone = adjacencyCluster?.GetZones(x, comboBox_ZoneType.SelectedItem as string)?.FirstOrDefault();
                if(zone == null)
                {
                    return null;
                }

                return zone.Name;
            });
        }

        public void SetMapFunc()
        {
            TextMap textMap = TextMap;
            InternalConditionLibrary internalConditionLibrary = InternalConditionLibrary;
            string zoneType = comboBox_ZoneType.SelectedItem as string;

            // TM59Manager's TextMap ctor falls back to the TM59 default resource when textMap is null,
            // so a missing selection here does not silently produce an inert (always-blank) manager.
            TM59Manager tM59Manager = new TM59Manager(textMap);

            if (!ResourcesAreUsable(tM59Manager, internalConditionLibrary))
            {
                ReportResourceFailure();

                mapInternalConditionsControl.MapFunc = new Func<Space, InternalCondition>(x => null);
                return;
            }

            mapInternalConditionsControl.MapFunc = new Func<Space, InternalCondition>(x =>
            {
                return tM59Manager.GetInternalConditionResult(adjacencyCluster, internalConditionLibrary, x, zoneType)?.InternalCondition;
            });

            mapInternalConditionsControl.MapDiagnosticFunc = new Func<Space, string>(x =>
            {
                return tM59Manager.GetInternalConditionResult(adjacencyCluster, internalConditionLibrary, x, zoneType)?.Diagnostic;
            });
        }

        private static bool ResourcesAreUsable(TM59Manager tM59Manager, InternalConditionLibrary internalConditionLibrary)
        {
            if (tM59Manager == null || internalConditionLibrary == null)
            {
                return false;
            }

            List<InternalCondition> internalConditions = internalConditionLibrary.GetInternalConditions();
            return internalConditions != null && internalConditions.Count != 0;
        }

        private void ReportResourceFailure()
        {
            if (resourceFailureReported)
            {
                return;
            }

            resourceFailureReported = true;
            System.Windows.MessageBox.Show(
                "The TM59 InternalCondition TextMap or InternalConditionLibrary could not be loaded, " +
                "so spaces cannot be mapped automatically. Select a valid Text Map and InternalCondition " +
                "Library above, or assign conditions manually.",
                "TM59 - Map Internal Conditions");
        }

        public List<Space> GetSpaces(bool selected = false)
        {
            return mapInternalConditionsControl.GetSpaces(selected);
        }
    }
}
