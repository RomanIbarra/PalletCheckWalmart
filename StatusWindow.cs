using PalletCheck;
using System;
using System.Windows;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.IO.Compression;
using Sick.GenIStream;

namespace PalletCheck
{
    public partial class StatusWindow : Form
    {
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();

        Dictionary<string, DataGridViewRow> rowDict = new Dictionary<string, DataGridViewRow>();
        System.Windows.Forms.Timer ScreenshotTimer = new System.Windows.Forms.Timer();
        string SnapshotDir;
        ParamStorage ParamStorageGeneral = MainWindow.ParamStorageGeneral;
        ParamStorage ParamStorageTop = MainWindow.ParamStorageTop;
        ParamStorage ParamStorageBottom = MainWindow.ParamStorageBottom;
        public StatusWindow()
        {
            InitializeComponent();

            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;

            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;


            BuildPagesFromStatusStorage();

            StatusStorage.OnStatusChanged_Callback += StatusChangedCB;

            ScreenshotTimer.Interval = 1000;
            ScreenshotTimer.Tick += ScreenshotTimerCB;
        }

        private void StatusChangedCB(string category, string field, string value)
        {
            string full_key = category + field;
            if (rowDict.ContainsKey(full_key))
            {
                DataGridViewRow row = rowDict[full_key];
                row.Cells[1].Value = value; 
            }
        }


        private void BuildPagesFromStatusStorage()
        {
            tabCategories.TabPages.Clear();
            rowDict.Clear();

            // Build controls
            foreach (KeyValuePair<string, Dictionary<string, string>> kvp in StatusStorage.Categories)
            {
                TabPage TP = new TabPage(kvp.Key);
                TP.Tag = kvp.Value;
                tabCategories.TabPages.Add(TP);

                DataGridView DGV = new DataGridView();
                DGV.Columns.Clear();
                DGV.ReadOnly = true;
                DGV.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                DGV.Columns.Add("Description", "Description");
                DGV.Columns[0].ReadOnly = true;
                DGV.Columns.Add("Value", "Value");
                DGV.AllowUserToAddRows = false;
                DGV.AllowUserToOrderColumns = false;
                DGV.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
                DGV.DefaultCellStyle = dataGridViewCellStyle2;
                DGV.RowHeadersVisible = false;
                TP.Controls.Add(DGV);
                DGV.Dock = DockStyle.Fill;
                DGV.Rows.Clear();
                foreach (KeyValuePair<string, string> Params in kvp.Value)
                {
                    if (string.IsNullOrEmpty(Params.Key))
                        continue;

                    string category_key = kvp.Key;
                    string params_key = Params.Key;
                    string params_value = Params.Value;
                    string full_key = category_key + params_key;

                    int iRow = DGV.Rows.Add();
                    DataGridViewRow row = DGV.Rows[iRow];
                    row.SetValues(new string[] { Params.Key, Params.Value });
                    row.Tag = kvp.Value;

                    rowDict.Add(full_key, row);
                }

                DGV.CellValueChanged += DGV_CellValueChanged;
            }
        }

        private void DGV_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            //DataGridView DGV = (DataGridView)sender;
            //DataGridViewRow DVR = DGV.Rows[e.RowIndex];

            //Dictionary<string, string> Param = DVR.Tag as Dictionary<string, string>;

            //Param[DVR.Cells[0].Value as string] = DVR.Cells[1].Value as string;

            //ParamStorage.Save(ParamStorage.LastFilename);
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            BuildPagesFromStatusStorage();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Logger.ButtonPressed("StatusWindow.btnSave");
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text files|*.txt";
            sfd.FileName = "";
            sfd.InitialDirectory = MainWindow.RootDir;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                StatusStorage.Save(sfd.FileName);
            }
        }


        private void CopyLastNFiles(string pattern, int n, string srcdir, string dstdir)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dstdir);

                string[] filePaths = Directory.GetFiles(srcdir, pattern, SearchOption.TopDirectoryOnly);
                if(filePaths.Length > 0)
                {
                    Array.Sort(filePaths);
                    Array.Reverse(filePaths);
                    n = Math.Min(n, filePaths.Length);
                    for (int i = 0; i < n; i++)
                    {
                        string srcfilename = filePaths[i];
                        string dstfilename = srcfilename.Replace(srcdir, dstdir);
                        Logger.WriteLine(string.Format("Snapshot Copy: {0} to {1}", srcfilename, dstfilename));
                        File.Copy(srcfilename, dstfilename, true);
                    }
                }
            }
            catch(Exception e)
            {
                Logger.WriteException(e);
            }
        }


        private Sick.GenIStream.ConfigurationStatus TryParameters(ICamera Camera, string Filename)
        {
            if (Camera != null && Camera.IsConnected)
            {
                ConfigurationResult Result = Camera.ImportParametersFromFile(Filename);
                return Result.Status;
            }
            return Sick.GenIStream.ConfigurationStatus.ERROR_GENERIC;
        }


        private string[] ReplaceConfigLine(string[] Lines, string Name, string Value)
        {
            string[] newLines = (string[])Lines.Clone();
            for(int i=0; i<newLines.Length; i++)
            {
                string[] parts = newLines[i].Split(',');
                if (parts[0]==Name)
                {
                    newLines[i] = Name + "," + Value;
                    return newLines;
                }
            }
            Logger.WriteLine("ReplaceConfigLine:: COULD NOT FIND VALUE IN CONFIG: " + Name);
            return null;
        }

        private int FindHighestHz(ICamera Camera, string[] SourceConfigLines)
        {
            string ConfigDir = "F:\\LucidCloud\\Projects\\SICK_PalletCheck\\Ranger3Benchmarking";
            int[] StepSizes = { 1000, 200, 100, 50 };
            int BestHz = 100;
            int HzH = 10000;
            Sick.GenIStream.ConfigurationStatus Status = ConfigurationStatus.OK;

            foreach (int StepSize in StepSizes)
            {
                Logger.WriteLine("COnfiguration search stepsize = " + StepSize.ToString());
                int Hz = BestHz;
                while (Hz <= HzH)
                {
                    string[] ConfigLines = ReplaceConfigLine(SourceConfigLines, "AcquisitionLineRate", Hz.ToString());
                    File.WriteAllLines(ConfigDir + "\\TweakedConfiguration.csv", ConfigLines);
                    Status = TryParameters(Camera, ConfigDir + "\\TweakedConfiguration.csv");

                    if (Status == ConfigurationStatus.OK)
                    {
                        if (Hz > BestHz) BestHz = Hz;
                        Logger.WriteLine("Configuration was ok with Hz = " + Hz.ToString());
                    }
                    else
                    {
                        Logger.WriteLine("Configuration FAILED with Hz = " + Hz.ToString());
                        break;
                    }

                    Hz += StepSize;
                }
            }

            Logger.WriteLine("Best Hz = " + BestHz.ToString());
            return BestHz;
        }

        /*
        private void DoCameraParameterScan()
        {
            R3Cam R3Camera = MainWindow.Singleton.Camera1;
            ICamera Camera = R3Camera.Camera;
            string ConfigDir = "F:\\LucidCloud\\Projects\\SICK_PalletCheck\\Ranger3Benchmarking";

            Sick.GenIStream.ConfigurationStatus Status = ConfigurationStatus.OK;

            string[] BaseConfigLines = File.ReadAllLines(ConfigDir + "\\BaseConfiguration.csv");


            for(int ExposureTime=25; ExposureTime<=1250; ExposureTime+=25)
            //for (int ExposureTime = 100; ExposureTime <= 1000; ExposureTime += 100)
            {
                Logger.WriteBorder("ExposureTime = " + ExposureTime.ToString());
                string OutputCSVPath = string.Format("F:\\LucidCloud\\Projects\\SICK_PalletCheck\\Ranger3Benchmarking\\Ranger3Rates_{0:0000}.csv",ExposureTime);
                if(File.Exists(OutputCSVPath))
                {
                    Logger.WriteLine("File already exists...");
                    continue;
                }

                File.WriteAllText(OutputCSVPath, "ExposureTime,WindowHeight,MaxHz\n");

                string[] ExpConfigLines = ReplaceConfigLine(BaseConfigLines, "ExposureTime_RegionSelector_Region0", ExposureTime.ToString());
                         ExpConfigLines = ReplaceConfigLine(ExpConfigLines, "ExposureTime_RegionSelector_Region1", ExposureTime.ToString());

                
                for (int WindowHeight = 16; WindowHeight <= 832; WindowHeight += 16)
                {
                    Logger.WriteBorder("WindowHeight = " + WindowHeight.ToString());

                    string[] ConfigLines = ReplaceConfigLine(ExpConfigLines, "Height_RegionSelector_Region1", WindowHeight.ToString());
                    int BestHz = FindHighestHz(Camera, ConfigLines);

                    List<string> Lines = new List<string>();
                    Lines.Add(ExposureTime.ToString() + "," + WindowHeight.ToString() + "," + BestHz.ToString());
                    File.AppendAllLines(OutputCSVPath, Lines);
                }
            }






            //Status = TryParameters(Camera, ConfigDir + "\\BaseConfiguration.csv");
            //Logger.WriteLine("BaseConfiguration: " + Status.ToString());

            //Status = TryParameters(Camera, ConfigDir + "\\ConfigurationA.csv");
            //Logger.WriteLine("ConfigurationA: " + Status.ToString());



        }*/


        private void btnSnapshot_Click(object sender, EventArgs e)
        {
            //DoCameraParameterScan();
            //return;

            //public static string RootDir = "";
            //public static string ConfigRootDir = "";
            //public static string HistoryRootDir = "";
            //public static string RecordingRootDir = "";
            //public static string LoggingRootDir = "";
            //public static string ExeRootDir = "";
            //public static string SiteName = "";

            DateTime DT = DateTime.Now;
            string dt_str = String.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}",DT.Year, DT.Month, DT.Day, DT.Hour, DT.Minute, DT.Second);

            SnapshotDir = MainWindow.SnapshotsRootDir + "\\" + dt_str + "_SNAPSHOT";
            System.IO.Directory.CreateDirectory(SnapshotDir);

            CopyLastNFiles("*.txt", 100, MainWindow.LoggingRootDir, SnapshotDir + "\\Logs");
            CopyLastNFiles("*.txt", 10, MainWindow.ExceptionsRootDir, SnapshotDir + "\\Exceptions");
            CopyLastNFiles("*", 1000, MainWindow.ConfigRootDir, SnapshotDir + "\\Config");
            CopyLastNFiles("*", 1000, MainWindow.ExeRootDir, SnapshotDir + "\\Application");
            CopyLastNFiles("*", 100, MainWindow.SettingsHistoryRootDir, SnapshotDir + "\\SettingsHistory");
            CopyLastNFiles("*", 10, MainWindow.CameraConfigHistoryRootDir, SnapshotDir + "\\CameraConfigHistory");


            ParamStorageGeneral.Save(SnapshotDir + "\\SettingsSnapshotGeneral.txt", false);
            ParamStorageTop.Save(SnapshotDir + "\\SettingsSnapshotTop.txt", false);
            ParamStorageGeneral.Save(SnapshotDir + "\\SettingsSnapshotBottom.txt", false);
            StatusStorage.Save(SnapshotDir + "\\StatusSnapshot.txt");
            // Iterate through all cameras and save settings for each camera

            foreach (var camera in MainWindow.Singleton.Cameras)
            {
                string cameraName = camera.CameraName; // Get the camera name
                string filePath = SnapshotDir + "\\" + cameraName + "_ConfigSnapshot.csv"; // Create a separate file for each camera

                camera.SaveSettings(filePath); // Save the settings to the corresponding file
            }


            this.Visible = false;
            ScreenshotTimer.Enabled = true;

        }

        private void SaveScreenshot(string dstfilename)
        {
            try
            {
                double screenLeft = SystemParameters.VirtualScreenLeft;
                double screenTop = SystemParameters.VirtualScreenTop;
                double screenWidth = SystemParameters.VirtualScreenWidth;
                double screenHeight = SystemParameters.VirtualScreenHeight;

                using (Bitmap bmp = new Bitmap((int)screenWidth, (int)screenHeight))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        Opacity = .0;
                        g.CopyFromScreen((int)screenLeft, (int)screenTop, 0, 0, bmp.Size);
                        bmp.Save(dstfilename);
                        Opacity = 1;
                    }

                }
            }
            catch(Exception e)
            {
                Logger.WriteException(e);
            }
        }

        private void ScreenshotTimerCB(object sender, EventArgs e)
        {
            SaveScreenshot(SnapshotDir + "\\Screenshot.png");

            this.Visible = true;
            ScreenshotTimer.Enabled = false;

            ZipFile.CreateFromDirectory(SnapshotDir, SnapshotDir + ".zip");

            // Set FTP server credentials
            string ftpServer = ParamStorageGeneral.GetString("FTP Server IP");
            string username = ParamStorageGeneral.GetString("FTP UserName");
            string password = ParamStorageGeneral.GetString("FTP Password");
            bool isFTPUpload = Convert.ToBoolean(ParamStorageGeneral.GetInt("UploadOrNot")); ;

            CaptureBuffer captureBuffer = new CaptureBuffer();
            captureBuffer.UploadZipFileToFtp(ftpServer, username, password, SnapshotDir + ".zip", ConvertWindowsPathToUnixStyle(SnapshotDir + ".zip"));

            System.Windows.Forms.MessageBox.Show("Snapshot has been created at:\n\n" + SnapshotDir, "SNAPSHOT COMPLETED!");
        }

        public string ConvertWindowsPathToUnixStyle(string windowsPath)
        {
            // 去掉驱动器号部分，例如 "C:" 或 "D:"
            string withoutDrive = windowsPath.Substring(windowsPath.IndexOf('\\'));  // 从第一个反斜杠开始截取

            // 将所有反斜杠替换为正斜杠
            string unixPath = withoutDrive.Replace("\\", "/");

            return unixPath;
        }
    }
}
