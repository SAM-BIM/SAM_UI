﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for NCMDataWindow.xaml
    /// </summary>
    public partial class NCMDataWindow : System.Windows.Window
    {
        public NCMDataWindow()
        {
            InitializeComponent();
        }

        public List<NCMData> NCMDatas
        {
            get
            {
                return NCMDataControl_Main.NCMDatas;
            }

            set
            {
                NCMDataControl_Main.NCMDatas = value;
            }
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}