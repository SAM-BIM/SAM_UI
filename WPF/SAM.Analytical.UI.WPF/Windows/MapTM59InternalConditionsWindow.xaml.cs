using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for MapTM59InternalConditionsWindow.xaml
    /// </summary>
    public partial class MapTM59InternalConditionsWindow : System.Windows.Window
    {
        public MapTM59InternalConditionsWindow()
        {
            InitializeComponent();
        }

        public MapTM59InternalConditionsWindow(IEnumerable<Space> spaces, AdjacencyCluster adjacencyCluster, TextMap textMap = null, InternalConditionLibrary internalConditionLibrary = null)
        {
            InitializeComponent();

            mapTM59InternalConditionsControl.AdjacencyCluster = adjacencyCluster;

            mapTM59InternalConditionsControl.TextMap = textMap;
            mapTM59InternalConditionsControl.InternalConditionLibrary = internalConditionLibrary;
            mapTM59InternalConditionsControl.Spaces = spaces == null ? null : new List<Space>(spaces);

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            List<Space> spaces = mapTM59InternalConditionsControl.GetSpaces();
            int unassigned = spaces?.FindAll(x => x.InternalCondition == null).Count ?? 0;

            textBlock_Status.Text = unassigned == 0
                ? string.Empty
                : $"{unassigned} space(s) need manual review - hover the blank rows for why.";
        }

        public List<Space> Spaces
        {
            get
            {
                return mapTM59InternalConditionsControl.Spaces;
            }
        }

        public List<Space> GetSpaces(bool selected = false)
        {
            return mapTM59InternalConditionsControl.GetSpaces(selected);
        }

        private void button_OK_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void button_Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
