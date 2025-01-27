using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Sick.EasyRanger;
using Sick.EasyRanger.Base;

namespace PalletCheck
{
    /// <summary>
    /// Interaction logic for Viewer3D.xaml
    /// </summary>
    public partial class Viewer3D : Window
    {
        public ProcessingEnvironment Environment { get; set; }
        
        public Viewer3D(ProcessingEnvironment environment = null)
        {
            InitializeComponent();
            DataContext = this;
            Environment = environment ?? new ProcessingEnvironment();

            if (SICKViewer3D == null)
            {
                throw new InvalidOperationException("SICKViewer3D is not initialized in the XAML.");
            }

            SICKViewer3D.Init(null, Environment);
            
        }
    }

}
