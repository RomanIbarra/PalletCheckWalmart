using ScottPlot;
using ScottPlot.WPF;
using System;
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

namespace PalletCheck
{
    /// <summary>
    /// Interaction logic for ProcessingWindow.xaml
    /// </summary>
    public partial class ProcessRecordingControl : Window
    {
        Dictionary<string, int> CatCount = new Dictionary<string, int>();

        int MaxPal;

        public delegate void CancelHandler();
        CancelHandler OnCancel;

        public ProcessRecordingControl( string FolderName, int PalletCount, CancelHandler CancelFn )
        {
            InitializeComponent();

            OnCancel = CancelFn;
            btnCancel.Click += BtnCancel_Click;

            CatCount.Add("Passed", 0);
            CatCount.Add("Failed", 0);
            MaxPal = PalletCount;
            Title = "Review of " + FolderName;
            tbCount.Text = string.Format("Processing {0} of {1}", CatCount["Passed"] + CatCount["Failed"], MaxPal);

        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            OnCancel();
        }

        public void LogPallet( bool Pass )
        {
            if (Pass)
                CatCount["Passed"]++;
            else
                CatCount["Failed"]++;

            tbCount.Text = string.Format("Processing {0} of {1}", CatCount["Passed"]+CatCount["Failed"], MaxPal);

            UpdateChart();
        }

        public void IncrementValue(string Category )
        {
            if (!CatCount.ContainsKey(Category))
                CatCount.Add(Category, 0);

            CatCount[Category]++;
        }

        private void UpdateChart()
        {
            //List<double> values     = new List<double>();
            //List<double> positions  = new List<double>();
            //List<string> labels     = new List<string>();

            //foreach( KeyValuePair<string,int> kvp in CatCount )
            //{
            //    positions.Add(values.Count);
            //    labels.Add(kvp.Key);
            //    values.Add(kvp.Value);
            //}

            //if (Stats.Plot.GetPlottables().Length > 0)
            //    Stats.Plot.RemoveAt(0);

            //var bar = Stats.Plot.AddBar(values.ToArray(), positions.ToArray() );

            //bar.ShowValuesAboveBars = true;
            //Stats.Plot.XTicks(positions.ToArray(), labels.ToArray());
            //Stats.Plot.SetAxisLimits(yMin: 0);

            //Stats.Refresh();
        }
    }
}
