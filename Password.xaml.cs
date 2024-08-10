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
    /// Interaction logic for Password.xaml
    /// </summary>
    public partial class Password : Window
    {

        public static bool Passed { get; set; }

        public static bool DlgOpen = false;

        public Password()
        {
            InitializeComponent();
            DlgOpen = true;
            Passed = false;
            pbPassword.Focus();

            Logger.WriteLine("PASSWORD CONTROL STARTUP");

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //string CorrectPassword = Environment.GetEnvironmentVariable("PALLETCHECK_SETTINGS_PASSWORD");
            //if (CorrectPassword == null) CorrectPassword = "1234";
            //Passed = (pbPassword.Password.ToLower() == CorrectPassword);
            //DlgOpen = false;
            //Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //do my stuff before closing
            DlgOpen = false;

            base.OnClosing(e);
            Logger.WriteLine("PASSWORD CONTROL SHUTDOWN");
        }




        private void pbPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                //Console.WriteLine("Entered the password");
                string CorrectPassword = Environment.GetEnvironmentVariable("PALLETCHECK_SETTINGS_PASSWORD");
                if (CorrectPassword == null) CorrectPassword = "1234";
                Passed = (pbPassword.Password.ToLower() == CorrectPassword);
                DlgOpen = false;
                if(Passed)
                    Logger.WriteLine("PASSWORD PASSED");
                else
                    Logger.WriteLine("PASSWORD FAILED");
                Close();
            }
        }
    }
}
