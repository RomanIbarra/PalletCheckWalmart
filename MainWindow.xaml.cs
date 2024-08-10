using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; 
using System.Windows.Media;
using PalletCheck.Controls;
using System.IO;
using System.Diagnostics;
using static PalletCheck.Pallet;
using Sick.EasyRanger;
using Sick.EasyRanger.Base;
using Sick.EasyRanger.Controls;
using Sick.GenIStream;
using Sick.StreamUI.ImageFormat;
using Sick.StreamUI.UIImage;
using IFrame = Sick.GenIStream.IFrame;
using System.Threading;
using System.Collections.Concurrent;

namespace PalletCheck
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ProcessingEnvironment EzREnvironment;
        //public static int           SensorWidth = 2560;
        public R3Cam Camera1;
        public R3Cam Camera2;
        public R3Cam Camera3;
        static CameraDiscovery discovery;
        public static MainWindow    Singleton;
        List<UInt16[]>              IncomingScan = new List<UInt16[]>();

        public bool RecordModeEnabled { get; private set; }
        public string RecordDir = "";

        InspectionReport IR;
        static PalletProcessor PProcessor;

        Statistics Stats = new Statistics();

        Brush SaveSettingsButtonBrush;

        string[] AllFilesInFolder;
        int LastFileIdx = -1;

        bool BypassModeEnabled = false;
        //static string LastPalletSavedName = "";

        public static string Version = "v2.0";
        public static string RootDir = "";
        public static string ConfigRootDir = "";
        public static string HistoryRootDir = "";
        public static string RecordingRootDir = "";
        public static string SegmentationErrorRootDir = "";
        public static string LoggingRootDir = "";
        public static string ExceptionsRootDir = "";
        public static string SettingsHistoryRootDir = "";
        public static string CameraConfigHistoryRootDir = "";
        public static string SnapshotsRootDir = "";
        public static string ExeRootDir = "";
        public static string SiteName = "";

       // UInt16[] IncomingBuffer;
        delegate CaptureBufferBrowser addBufferCB(string s, CaptureBuffer CB);

        PLCComms PLC;
        StorageWatchdog Storage;
        Logger Log = new Logger();

        static bool cam_error = false;
        static bool segmentation_error = false;
        static bool file_storage_error = false;
        static bool unknown_error = false;

        int ProcessRecordingTotalPalletCount = 0;
        int ProcessRecordingFinishedPalletCount = 0;

        Brush ButtonBackgroundBrush;

        public static string _LastUsedParamFile = "";
        public static string LastUsedParamFile
        {
            get
            {
                return _LastUsedParamFile;
            }
            set
            {
                _LastUsedParamFile = value;
                System.IO.File.WriteAllText(HistoryRootDir + "\\LastUsedParamFile.txt", _LastUsedParamFile);
            }
        }

        // Production stats
        public class PalletStat
        {
            public DateTime Start;
            public DateTime End;
            public int Total;
            public int Fail;

            public PalletStat()
            {
                Start = DateTime.Now;
                Total = 0;
                Fail = 0;
            }
        }

        public List<PalletStat> PalletStats = new List<PalletStat>();

        public static Process PriorProcess()
        // Returns a System.Diagnostics.Process pointing to
        // a pre-existing process with the same name as the
        // current one, if any; or null if the current process
        // is unique.
        {
            Process curr = Process.GetCurrentProcess();
            Process[] procs = Process.GetProcessesByName(curr.ProcessName);
            foreach (Process p in procs)
            {
                if ((p.Id != curr.Id) &&
                    (p.MainModule.FileName == curr.MainModule.FileName))
                    return p;
            }
            return null;
        }

        //=====================================================================
        public MainWindow()
        {
            Process prior = PriorProcess();
            if (prior != null)
            {
                if(MessageBox.Show("Another instance of PalletCheck is already running!\n\nDo you want to start a NEW instance?","PALLETCHECK Already Running",MessageBoxButton.YesNo)==MessageBoxResult.Yes)
                {
                    prior.Kill();
                    System.Threading.Thread.Sleep(2000);
                }
                else
                {
                    return;
                }
            }


            Trace.WriteLine("MainWindow");
            Singleton = this;

            // Global exception handling  
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => CurrentDomainOnUnhandledException(args);


            // Setup root directories
            RootDir = Environment.GetEnvironmentVariable("PALLETCHECK_ROOT_DIR");
            if (RootDir == null)
            {
                MessageBox.Show("Could not find PALLETCHECK_ROOT_DIR env var");
                Close();
                return;
            }

            ConfigRootDir = RootDir + "\\Config";
            HistoryRootDir = RootDir + "\\History";
            RecordingRootDir = RootDir + "\\Recordings";
            SegmentationErrorRootDir = RecordingRootDir + "\\SegmentationErrors";
            LoggingRootDir = RootDir + "\\Logging";
            ExceptionsRootDir = RootDir + "\\Logging\\Exceptions";
            SettingsHistoryRootDir = HistoryRootDir + "\\SettingsHistory";
            CameraConfigHistoryRootDir = HistoryRootDir + "\\CameraConfigHistory";
            SnapshotsRootDir = HistoryRootDir + "\\Snapshots";
            ExeRootDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location).Trim();

            System.IO.Directory.CreateDirectory(ConfigRootDir);
            System.IO.Directory.CreateDirectory(HistoryRootDir);
            System.IO.Directory.CreateDirectory(RecordingRootDir);
            System.IO.Directory.CreateDirectory(SegmentationErrorRootDir);
            System.IO.Directory.CreateDirectory(LoggingRootDir);
            System.IO.Directory.CreateDirectory(ExceptionsRootDir);
            System.IO.Directory.CreateDirectory(SettingsHistoryRootDir);
            System.IO.Directory.CreateDirectory(SnapshotsRootDir);
            System.IO.Directory.CreateDirectory(CameraConfigHistoryRootDir);

            Log.Startup(LoggingRootDir);
            Logger.WriteBorder("PalletCheck STARTUP");
            Logger.WriteLine("ExeRootDir:                    " + ExeRootDir);
            Logger.WriteLine("ConfigRootDir:                 " + ConfigRootDir);
            Logger.WriteLine("HistoryRootDir:                " + HistoryRootDir);
            Logger.WriteLine("RecordingRootDir:              " + RecordingRootDir);
            Logger.WriteLine("SegmentationErrorRootDir:      " + SegmentationErrorRootDir);
            Logger.WriteLine("LoggingRootDir:                " + LoggingRootDir);
            Logger.WriteLine("ExceptionsRootDir:             " + ExceptionsRootDir);
            Logger.WriteLine("SettingsHistoryRootDir:        " + SettingsHistoryRootDir);
            Logger.WriteLine("CameraConfigHistoryRootDir:    " + CameraConfigHistoryRootDir);
            Logger.WriteLine("SnapshotsRootDir:              " + SnapshotsRootDir);

            SiteName = Environment.GetEnvironmentVariable("PALLETCHECK_SITE_NAME");
            if (SiteName == null)
                SiteName = "";

            InitializeComponent();

            //MainWindow.Icon = BitmapFrame.Create(Application.GetResourceStream(new Uri("LiveJewel.png", UriKind.RelativeOrAbsolute)).Stream);
        }

        private static void CurrentDomainOnUnhandledException(UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;

            Logger.WriteBorder("CurrentDomainOnUnhandledException");
            Logger.WriteException(exception);

            string errorMessage = string.Format("An application error occurred.\nPlease check whether your data is correct and repeat the action. If this error occurs again there seems to be a more serious malfunction in the application, and you better close it.\n\nError: {0}\n\nDo you want to continue?\n(if you click Yes you will continue with your work, if you click No the application will close)",

            exception.Message + (exception.InnerException != null ? "\n" +exception.InnerException.Message : null));

            if (MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.YesNoCancel, MessageBoxImage.Error) == MessageBoxResult.No)
            {
                if (MessageBox.Show("WARNING: The application will close. Any changes will not be saved!\nDo you really want to close it?", "Close the application!", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
                }
            }
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            this.Title = "PalletCheck  " + Version + " - " + SiteName;
            StatusStorage.Set("Version Number", Version);

            ButtonBackgroundBrush = btnStart.Background;


            // Try to load starting parameters
            if (File.Exists(HistoryRootDir + "\\LastUsedParamFile.txt"))
            {
                _LastUsedParamFile = File.ReadAllText(HistoryRootDir + "\\LastUsedParamFile.txt");
                if (File.Exists(_LastUsedParamFile))
                {
                    ParamStorage.Load(_LastUsedParamFile);
                }
                else
                    MessageBox.Show("Can't open the last known config file. It was supposed to be at " + _LastUsedParamFile);
            }
            else
            {
                ParamStorage.Load(ConfigRootDir + "\\DefaultParams.txt");
            }

            //int PPIX = ParamStorage.GetInt("Pixels Per Inch X");


            // Open PLC connection port
            int Port = ParamStorage.GetInt("TCP Server Port");
            string IP = ParamStorage.GetString("TCP Server IP");
            PLC = new PLCComms(IP,Port);
            PLC.Start();

            Storage = new StorageWatchdog();
            Storage.Start();

            System.Windows.Forms.Timer T = new System.Windows.Forms.Timer();
            T.Interval = 500;
            T.Tick += TimerUpdate500ms;
            T.Start();

            imgPassSymbol.Visibility = Visibility.Hidden;
            imgPassText.Visibility = Visibility.Hidden;
            imgFailSymbol.Visibility = Visibility.Hidden;
            imgFailText.Visibility = Visibility.Hidden;

            System.Windows.Forms.Timer T2 = new System.Windows.Forms.Timer();
            T2.Interval = 5000;
            T2.Tick += Timer5Sec;
            T2.Start();
            ClearAnalysisResults();
            ProgressBar_Clear();

            PProcessor = new PalletProcessor(3, 3);


            ParamStorage.HasChangedSinceLastSave = false;
            SaveSettingsButtonBrush = btnSettingsControl.Background;
            System.Windows.Forms.Timer T3 = new System.Windows.Forms.Timer();
            T3.Interval = 500;
            T3.Tick += SaveSettingsColorUpdate;
            T3.Start();

            //if (Camera1 == null)
            //{
            //    string CameraIP = ParamStorage.GetString("Camera1 IP");
            //    Camera1 = new R3Cam();
            //    Camera1.Startup("Camera1", CameraIP, OnNewFrameReceived, OnConnectionStateChange, OnCaptureStateChange);
            //    System.Threading.Thread.Sleep(1000);
            //}
            //ParamStorage.SaveInHistory("STARTUP");
            //Camera1.SaveInHistory("STARTUP");

            discovery = CameraDiscovery.CreateFromProducerFile("SICKGigEVisionTL.cti");
            InitializeCamera(ref Camera1, "Camera1", "Camera1 IP", OnNewFrameReceived1, OnConnectionStateChange, OnCaptureStateChange);
            InitializeCamera(ref Camera2, "Camera2", "Camera2 IP", OnNewFrameReceived2, OnConnectionStateChange, OnCaptureStateChange);
            InitializeCamera(ref Camera3, "Camera3", "Camera3 IP", OnNewFrameReceived3, OnConnectionStateChange, OnCaptureStateChange);
        

            if (ParamStorage.GetInt("Auto Start Capturing")==1)
            {
                System.Threading.Thread.Sleep(2000);
                btnStart_Click(null, null);
            }
        }


        static void InitializeCamera(ref R3Cam camera, string cameraName, string cameraIPKey, R3Cam.NewFrameReceivedCB onNewFrameReceived, R3Cam.ConnectionStateChangeCB onConnectionStateChange, R3Cam.CaptureStateChangeCB onCaptureStateChange)
        {
            if (camera == null)
            {
                string CameraIP = ParamStorage.GetString(cameraIPKey);
                camera = new R3Cam();
                camera.Startup(cameraName, CameraIP, discovery, onNewFrameReceived, onConnectionStateChange, onCaptureStateChange);
                Thread.Sleep(1000);
            }
        }


        private void OnConnectionStateChange(R3Cam Cam, R3Cam.ConnectionState NewState)
        {
            // !!! THIS IS CALLED FROM THE R3CAM THREAD !!!!
            Logger.WriteLine(Cam.CameraName + "  ConnectionState: " + NewState.ToString());
            StatusStorage.Set("Camera."+Cam.CameraName+".ConnState", NewState.ToString());
        }

        private void OnCaptureStateChange(R3Cam Cam, R3Cam.CaptureState NewState)
        {
            // !!! THIS IS CALLED FROM THE R3CAM THREAD !!!!
            Logger.WriteLine(Cam.CameraName + "  CaptureState: " + NewState.ToString());
            StatusStorage.Set("Camera." + Cam.CameraName + ".CaptureState", NewState.ToString());
        }

        private void OnNewFrameReceived1(R3Cam cam, R3Cam.Frame frame)
        {
            ProcessFrame(cam, frame, "Camera1");
        }

        private void OnNewFrameReceived2(R3Cam cam, R3Cam.Frame frame)
        {
            ProcessFrame(cam, frame, "Camera2");
        }

        private void OnNewFrameReceived3(R3Cam cam, R3Cam.Frame frame)
        {
            ProcessFrame(cam, frame, "Camera3");
        }
        private ConcurrentDictionary<string, R3Cam.Frame> FrameBuffer = new ConcurrentDictionary<string, R3Cam.Frame>();
        private void ProcessFrame(R3Cam cam, R3Cam.Frame frame, string cameraName)
        {
            Logger.WriteBorder($"NEW SCAN RECEIVED from {cameraName} Frame: {frame.FrameID}");
            FrameBuffer[cameraName] = frame;

            if (FrameBuffer.Count == 3)
            {
                MergeFrames();
                FrameBuffer.Clear();
            }

            Logger.WriteLine("OnNewFrameReceived is finished.");
        }

        private void MergeFrames()
        {
            R3Cam.Frame frame1, frame2, frame3;
            if (FrameBuffer.TryGetValue("Camera1", out frame1) &&
                FrameBuffer.TryGetValue("Camera2", out frame2) &&
                FrameBuffer.TryGetValue("Camera3", out frame3))
            {
                // Specify the start and end columns for each image
                int startX1 = 0, endX1 = 1111; // Assume Camera1 starts at column 0 and ends at column 1111
                int startX2 = 186, endX2 = 1225; // Assume Camera2 starts at column 186 and ends at column 1225
                int startX3 = 485, endX3 = 1471; // Assume Camera3 starts at column 485 and ends at column 1471

                // Calculate the total width and height of the merged image
                int totalWidth = (endX1 - startX1) + (endX2 - startX2) + (endX3 - startX3); // Calculate total width
                int totalHeight = frame1.Height; // Assume height is the same for all frames

                ushort[] combinedRange = new ushort[totalWidth * totalHeight];
                byte[] combinedReflectance = new byte[totalWidth * totalHeight];

                // Copy image data and apply offsets
                CopyFrameData(frame1, combinedRange, combinedReflectance, startX1, endX1, totalWidth, 0, 0); // targetStartX is 0
                CopyFrameData(frame2, combinedRange, combinedReflectance, startX2, endX2, totalWidth, 6, endX1 - startX1); // targetStartX is the width of the first image
                CopyFrameData(frame3, combinedRange, combinedReflectance, startX3, endX3, totalWidth, 3, (endX1 - startX1) + (endX2 - startX2)); // targetStartX is the sum of the widths of the first two images

                // Process the merged image data
                CaptureBuffer NewBuf = new CaptureBuffer(combinedRange, totalWidth, totalHeight);
                CaptureBuffer ReflBuf = new CaptureBuffer(combinedReflectance, totalWidth, totalHeight);
                ReflBuf.PaletteType = CaptureBuffer.PaletteTypes.Gray;

                Pallet P = new Pallet(NewBuf, ReflBuf);
                PProcessor.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete);
            }
        }

        private void CopyFrameData(R3Cam.Frame frame, ushort[] combinedRange, byte[] combinedReflectance, int startX, int endX, int totalWidth, int offset, int targetStartX)
        {
            int frameWidth = endX - startX;
            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int sourceIndex = y * frame.Width + x;
                    int targetIndex = y * totalWidth + (targetStartX + (x - startX));

                    // Offset and copy non-zero range data
                    if (frame.Range[sourceIndex] != 0)
                    {
                        combinedRange[targetIndex] = (ushort)((frame.Range[sourceIndex] * frame.zScale + (offset + frame.zOffset)) * 20);
                    }

                    // Copy non-zero reflectance data
                  
                        combinedReflectance[targetIndex] = frame.Reflectance[sourceIndex];
                    
                }
            }
        }



        private void OnNewFrameReceived(R3Cam Cam, R3Cam.Frame Frame)
        {
            Logger.WriteBorder("NEW SCAN RECEIVED    Frame: " + Frame.FrameID.ToString());

            CaptureBuffer NewBuf = new CaptureBuffer(Frame.Range,Frame.Width,Frame.Height);
            CaptureBuffer ReflBuf = new CaptureBuffer(Frame.Reflectance, Frame.Width, Frame.Height);
            ReflBuf.PaletteType = CaptureBuffer.PaletteTypes.Gray;

            Pallet P = new Pallet(NewBuf, ReflBuf);
            //P.NotLive = true;
            PProcessor.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete);

            Logger.WriteLine("OnNewFrameReceived is finished.");
        }



        private void SaveSettingsColorUpdate(object sender, EventArgs e)
        {
            if (ParamStorage.HasChangedSinceLastSave)
            {
                if (btnSettingsControl.Background == SaveSettingsButtonBrush)
                    btnSettingsControl.Background = new SolidColorBrush(Color.FromArgb(255,196, 196, 0));
                else
                    btnSettingsControl.Background = SaveSettingsButtonBrush;
            }
            else
            {
                btnSettingsControl.Background = SaveSettingsButtonBrush;
            }
        }

        //private void UpdatePalletProcessQueue(object sender, EventArgs e)
        //{
        //    int MaxThreads = 2;

        //    if(PalletProcessInputQueue.Count > 0)
        //    {
        //        // Can we kick off another thread
        //        if(PalletInProgressList.Count < MaxThreads)
        //        {
        //            Pallet P = PalletProcessInputQueue[0];
        //            PalletProcessInputQueue.RemoveAt(0);
        //            PalletInProgressList.Add(P);
        //            P.DoAsynchronousAnalysis();
        //        }
        //    }

        //}

        private void Timer5Sec(object sender, EventArgs e)
        {
            Logger.WriteHeartbeat();

            if (ParamStorage.GetInt("TCP Server Heartbeat") == 1)
            {
                DateTime D = DateTime.Now;
                string Text = D.ToShortDateString() + " " + D.ToLongTimeString();
                PLC.SendMessage(Text + " --- TCP Server Heartbeat\n");
            }

            // Keep a handle on garbage collection so we keep a steady footprint
            GC.Collect();
            //GC.WaitForPendingFinalizers();

            //Logger.WriteLine(String.Format("Current Memory Usage: {0:N0}", GC.GetTotalMemory(false)));
        }

        private void TimerUpdate500ms(object sender, EventArgs e)
        {
            DateTime D = DateTime.Now;
            string Text = D.ToShortDateString() + " " + D.ToLongTimeString();
            CurDateTime.Text = Text;
            StatusStorage.Set("Date/Time", Text);
            StatusStorage.Set("SettingsChanged", ParamStorage.HasChangedSinceLastSave.ToString());


            if (Camera1.CameraConnectionState != R3Cam.ConnectionState.Connected)
            {
                if (btnStatusControl.Background == SaveSettingsButtonBrush)
                    btnStatusControl.Background = new SolidColorBrush(Color.FromArgb(255, 196, 196, 0));
                else
                    btnStatusControl.Background = SaveSettingsButtonBrush;
            }
            else
            {
                btnStatusControl.Background = SaveSettingsButtonBrush;
            }
        }

        public void ProgressBar_SetText(string text)
        {
            ProgressBarText.Text = text;
        }

        public void ProgressBar_SetProgress( double Current, double Total )
        {
            ProgressBar.Value = Math.Round(100 * (Current / Total), 0);
        }
        public void ProgressBar_Clear()
        {
            ProgressBar.Value = 0;
            ProgressBarText.Text = "";
        }


        //=====================================================================

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {

            Logger.ButtonPressed(btnStart.Content.ToString());

            ParamStorage.SaveInHistory("LINESTART");
            Camera1.SaveInHistory("LINESTART");
            Camera2.SaveInHistory("LINESTART");
            Camera3.SaveInHistory("LINESTART");

            if (btnStart.Content.ToString() == "START")
            {
                Camera1.StartCapturingFrames();
                Camera2.StartCapturingFrames();
                Camera3.StartCapturingFrames();
                btnStart.Content = "STOP";
                ModeStatus.Text = "RUNNING";
                btnStart.Background = Brushes.Green;
            }
            else if (btnStart.Content.ToString() == "STOP")
            {
                Camera1.StopCapturingFrames();
                Camera2.StopCapturingFrames();
                Camera3.StopCapturingFrames();
                btnStart.Content = "START";
                ModeStatus.Text = "IDLING";
                btnStart.Background = ButtonBackgroundBrush;
            }

        }


        //=====================================================================
        public static CaptureBufferBrowser AddCaptureBufferBrowser(string Name, CaptureBuffer CB, string SelectName)
        {
            //bool isLive = (string.Compare(Name, "Live") == 0);
            CB.Name = Name;

            CaptureBufferBrowser CBB = new CaptureBufferBrowser();
            CBB.SetCB(CB);

            Button B = new Button();
            B.Content = Name;
            B.Click += Singleton.ImageTab_Click;
            B.Margin = new Thickness(5);
            B.Background = Singleton.btnProcessPallet.Background;
            B.Foreground = Singleton.btnProcessPallet.Foreground;
            B.FontSize = 16;
            B.Tag = CBB;

            Singleton.CBB_Button_List.Children.Add(B);
            Singleton.CBB_Container.Children.Add(CBB);

            CBB.Refresh();
            CBB.Visibility = Visibility.Hidden;

            if ((Name == SelectName))
                Singleton.ImageTab_Click(B, new RoutedEventArgs());

            return CBB;
        }

        //=====================================================================

        void ClearAnalysisResults()
        {
            imgPassSymbol.Visibility = Visibility.Hidden;
            imgPassText.Visibility = Visibility.Hidden;
            imgFailSymbol.Visibility = Visibility.Hidden;
            imgFailText.Visibility = Visibility.Hidden;

            foreach (CaptureBufferBrowser CBC in CBB_Container.Children)
            {
                CBC.Clear();
            }
            CBB_Container.Children.Clear();
            CBB_Button_List.Children.Clear();
            PalletName.Text = "";

            defectTable.Items.Clear();
        }

        //=====================================================================
        private void LoadAndProcessCaptureFile(string FileName, bool RebuildList = false)
        {
            Logger.WriteLine("Loading CaptureBuffer from: " + FileName);

            ClearAnalysisResults();

            if (RebuildList)
            {
                string Search = System.IO.Path.GetDirectoryName(FileName);

                DirectoryInfo info = new DirectoryInfo(Search);
                FileInfo[] files = info.GetFiles("*rng.r3").OrderBy(p => p.CreationTime).ToArray();

                List<string> AF = new List<string>();
                foreach (FileInfo F in files)
                    AF.Add(F.FullName);
                AllFilesInFolder = AF.ToArray();
                LastFileIdx = AllFilesInFolder.ToList().IndexOf(FileName);
            }

            ////Jack Try Stitch
            ///
            

            Pallet P = new Pallet(FileName);

            


            //P.NotLive = true;
            PProcessor.ProcessPalletHighPriority(P, Pallet_OnProcessSinglePalletAnalysisComplete);
        }


        //=====================================================================
        private void UpdateProductivityStats(Pallet P)
        {
            Stats.OnNewPallet(P);
            statisticsTable.Items.Clear();

            // Send defects to the defectTable
            for (int i = 0; i < Stats.Entries.Count; i++)
            {
                statisticsTable.Items.Add(Stats.Entries[i]);
            }
        }


        //=====================================================================
        private void ImageTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button B = (Button)sender;
                CaptureBufferBrowser CBB = (CaptureBufferBrowser)B.Tag;

                Logger.ButtonPressed("CaptureBuffer." + B.Content.ToString());

                if (CBB.FB == null)
                    CBB.FB = CBB.CB.BuildFastBitmap();

                for (int i = 0; i < CBB_Container.Children.Count; i++)
                    CBB_Container.Children[i].Visibility = Visibility.Hidden;

                CBB.Visibility = Visibility.Visible;
            }
            catch(Exception exp)
            {
                Logger.WriteException(exp);
            }
        }


        private void btn_LoadEzR_Click(object sender, RoutedEventArgs e)
        {
            ClearAnalysisResults();

            EzREnvironment = new ProcessingEnvironment();
            EzREnvironment.Load("Env\\Pallet Stitching.env");

            CaptureBuffer NewBuf = new CaptureBuffer();
            CaptureBuffer ReflBuf = new CaptureBuffer();
            float[] floats = EzREnvironment.GetImageBuffer("Pallet")._range;
            ushort[] uintArray = Array.ConvertAll(EzREnvironment.GetImageBuffer("Pallet")._range, element => (ushort)element);
            NewBuf = new CaptureBuffer(uintArray, EzREnvironment.GetImageProperties("Pallet").Width, EzREnvironment.GetImageProperties("Pallet").Height);

            ushort[] uintArrayRef = Array.ConvertAll(EzREnvironment.GetImageBuffer("Pallet")._intensity, element => (ushort)element);
            ReflBuf = new CaptureBuffer(uintArrayRef, EzREnvironment.GetImageProperties("Pallet").Width, EzREnvironment.GetImageProperties("Pallet").Height);


            ReflBuf.PaletteType = CaptureBuffer.PaletteTypes.Gray;

            Pallet P = new Pallet(NewBuf, ReflBuf);
            //P.NotLive = true;
            PProcessor.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete);
            //PProcessor.ProcessPalletHighPriority(P, Pallet_OnProcessSinglePalletAnalysisComplete);

            //Dispatcher.Invoke(new addBufferCB(MainWindow.AddCaptureBufferBrowser), new object[] { "Live", IncomingBuffer });
            // Console.WriteLine("BUFFER RECEIVED! " + DateTime.Now.ToString());
            Logger.WriteLine("OnNewFrameReceived is finished.");
        }
        //=====================================================================
        private void btnProcessPallet_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnProcessPallet.Content.ToString());

            OpenFileDialog OFD = new OpenFileDialog();

            OFD.Filter = "R3 Files|*rng.r3";
            OFD.InitialDirectory = MainWindow.RecordingRootDir;
            if (OFD.ShowDialog() == true)
            {
                LoadAndProcessCaptureFile(OFD.FileName, true);
            }
            else
            {
                Logger.ButtonPressed("btnProcessPallet_Click open file dialog cancelled");
            }

        }

        //=====================================================================
        private void SendPLCResults(Pallet P)
        {
            segmentation_error = P.BList.Count == 5 ? false : true;

            string PLCString = "";

            DateTime DT = P.CreateTime;
            PLCString += string.Format("{0:00}:{1:00}:{2:0000} {3:00}:{4:00}:{5:00} ",
                                        DT.Month, DT.Day, DT.Year,
                                        DT.Hour, DT.Minute, DT.Second);

            PLCString += BypassModeEnabled ? "B" : "N";
            PLCString += (RecordModeEnabled ? "A" : "N");
            PLCString += cam_error ? "T" : "F";
            PLCString += segmentation_error ? "T" : "F";
            PLCString += file_storage_error ? "T" : "F";
            PLCString += unknown_error ? "T" : "F";

            PLC.SendMessage(PLCString);

            string[] defectCodes = PalletDefect.GetPLCCodes();
            for (int i = 0; i < P.BList.Count; i++)
            {
                string boardDefList = "";

                for (int j = 0; j < defectCodes.Length; j++)
                {
                    string DefectCode = defectCodes[j];
                    int nDefs = 0;

                    for (int k = 0; k < P.BList[i].AllDefects.Count; k++)
                        if (P.BList[i].AllDefects[k].Code == DefectCode)
                            nDefs += 1;

                    if (nDefs < 9)
                        DefectCode += nDefs.ToString();
                    else
                        DefectCode += "9";

                    boardDefList += DefectCode;
                }

                PLC.SendMessage(P.BList[i].BoardName + boardDefList);
            }
        }

        //=====================================================================

        private void Pallet_OnProcessSinglePalletAnalysisComplete(Pallet P)
        {
            Logger.WriteLine("Pallet_OnProcessSinglePalletAnalysisComplete");


            UpdateUIWithPalletResults(P);
        }

        private void Pallet_OnProcessMultiplePalletAnalysisComplete(Pallet P)
        {
            Logger.WriteLine("Pallet_OnProcessMultiplePalletAnalysisComplete");

            ProcessRecordingFinishedPalletCount += 1;
            ProgressBar_SetText(string.Format("Processing Recording... Finished {0} of {1}", ProcessRecordingFinishedPalletCount, ProcessRecordingTotalPalletCount));
            ProgressBar_SetProgress(ProcessRecordingFinishedPalletCount, ProcessRecordingTotalPalletCount);

            IR.AddPallet(P);

            if (ProcessRecordingFinishedPalletCount >= ProcessRecordingTotalPalletCount)
            {
                ProgressBar_SetText("Processing Recording... COMPLETE");
                IR.Save();
            }
        }

        
        //=====================================================================

        private void UpdateUIWithPalletResults(Pallet P)
        {
            ClearAnalysisResults();

            string fullDir = System.IO.Path.GetDirectoryName(P.Filename);
            string lastDir = fullDir.Split(System.IO.Path.DirectorySeparatorChar).Last();
            Logger.WriteLine("pallet filename: "+ P.Filename);
            //Logger.WriteLine("fullDir: "+ fullDir);
            //Logger.WriteLine("lastDir: "+ lastDir);
            PalletName.Text = lastDir + "\\" + System.IO.Path.GetFileName(P.Filename);


            // Send defects to the defectTable
            int c = 0;
            if (P.BList != null)
            {
                for (int i = 0; i < P.BList.Count; i++)
                {
                    List<PalletDefect> BoardDefects = P.BList[i].AllDefects;
                    for (int j = 0; j < BoardDefects.Count; j++)
                    {
                        PalletDefect PD = BoardDefects[j];
                        defectTable.Items.Add(PD);
                        c += 1;
                        Logger.WriteLine(string.Format("DEFECT | {0:>2} | {1:>2}  {2:>4}  {3:>8}  {4}", c, P.BList[i].BoardName, PD.Code, PD.Name, PD.Comment));
                    }
                }
            }

            List<PalletDefect> FullPalletDefects = P.PalletLevelDefects;
            for (int j = 0; j < FullPalletDefects.Count; j++)
            {
                PalletDefect PD = FullPalletDefects[j];
                defectTable.Items.Add(PD);
                c += 1;
                Logger.WriteLine(string.Format("DEFECT | {0:>2} | {1:>2}  {2:>4}  {3:>8}  {4}", c,"Pallet", PD.Code, PD.Name, PD.Comment));
            }

            Logger.WriteLine(string.Format("DEFECT COUNT {0}", c));

            // Show Pass/Fail Icons
            if (P.AllDefects.Count > 0)
            {
                imgPassSymbol.Visibility = Visibility.Hidden;
                imgPassText.Visibility = Visibility.Hidden;
                imgFailSymbol.Visibility = Visibility.Visible;
                imgFailText.Visibility = Visibility.Visible;
            }
            else
            {
                imgPassSymbol.Visibility = Visibility.Visible;
                imgPassText.Visibility = Visibility.Visible;
                imgFailSymbol.Visibility = Visibility.Hidden;
                imgFailText.Visibility = Visibility.Hidden;
            }


            // Build CaptureBufferBrowsers
            if (true)
            {
                // Check if segmentation error
                bool HasSegmentationError = false;
                if((P.PalletLevelDefects.Count>0) && (P.PalletLevelDefects[0].Type == PalletDefect.DefectType.board_segmentation_error))
                {
                    HasSegmentationError = true;
                }


                foreach (CaptureBuffer CB in P.CBList)
                {
                    CaptureBufferBrowser CBB = AddCaptureBufferBrowser(CB.Name, CB, HasSegmentationError ? "Original" : "Filtered");

                    foreach (PalletDefect PD in P.AllDefects)
                    {
                        if (PD.MarkerRadius > 0)
                        {
                            CBB.MarkDefect(new Point(PD.MarkerX1, PD.MarkerY1), PD.MarkerRadius, PD.MarkerTag);
                        }
                        else
                        if ((PD.MarkerRadius == 0) && (PD.MarkerX1 != 0))
                        {
                            CBB.MarkDefect(new Point(PD.MarkerX1, PD.MarkerY1), new Point(PD.MarkerX2, PD.MarkerY2), PD.MarkerTag);
                        }
                    }

                    //CBB.RedrawDefects();
                    //CBB.MarkDefect()
                }
            }

        }

         private void Pallet_OnLivePalletAnalysisComplete(Pallet P)
        {
            Logger.WriteLine("Pallet_OnLivePalletAnalysisComplete Start");
            if (!BypassModeEnabled)
            {
                // Update production stats
                UpdateProductivityStats(P);
            }

            // Notify PLC of pallet results
            SendPLCResults(P);

            UpdateUIWithPalletResults(P);

            // Save Results to Disk
            {
                try
                {
                    string FullDirectory = RecordingRootDir + "\\" + P.Directory;

                    if (RecordModeEnabled && (RecordDir != ""))
                        FullDirectory = RecordingRootDir + "\\" + RecordDir;

                    System.IO.Directory.CreateDirectory(FullDirectory);
                    string FullFilename = FullDirectory + "\\" + P.Filename;
                    Logger.WriteLine("Saving Pallet to " + FullFilename);

                    if( P.AllDefects.Count > 0 )
                    {
                        P.Original.Save(FullFilename.Replace(".r3", "_F_rng.r3"));
                        P.Original.SaveImage(FullFilename.Replace(".r3", "_F_rng.png"), false);
                        P.ReflectanceCB.Save(FullFilename.Replace(".r3", "_F_rfl.r3"));
                        P.ReflectanceCB.SaveImage(FullFilename.Replace(".r3", "_F_rfl.png"), true);
                    }
                    else
                    {
                        P.Original.Save(FullFilename.Replace(".r3", "_P_rng.r3"));
                        P.Original.SaveImage(FullFilename.Replace(".r3", "_P_rng.png"), false);
                        P.ReflectanceCB.Save(FullFilename.Replace(".r3", "_P_rfl.r3"));
                        P.ReflectanceCB.SaveImage(FullFilename.Replace(".r3", "_P_rfl.png"), true);
                    }


                    bool HasSegmentationError = false;
                    foreach (PalletDefect PD in P.AllDefects)
                        if (PD.Code == "ER")
                            HasSegmentationError = true;

                    if (HasSegmentationError)
                    {
                        FullFilename = SegmentationErrorRootDir + "\\" + P.Filename;
                        Logger.WriteLine("Saving Pallet to " + FullFilename);
                        P.Original.Save(FullFilename.Replace(".r3", "_rng.r3"));
                        P.Original.SaveImage(FullFilename.Replace(".r3", "_rng.png"), false);
                        P.ReflectanceCB.Save(FullFilename.Replace(".r3", "_rfl.r3"));
                        P.ReflectanceCB.SaveImage(FullFilename.Replace(".r3", "_rfl.png"), true);
                    }
                }
                catch(Exception e)
                {
                    Logger.WriteException(e);
                }
            }

            Logger.WriteLine("Pallet_OnLivePalletAnalysisComplete Complete");
            Logger.WriteBorder("PALLET COMPLETE: " + P.Filename);

        }


        //=====================================================================
        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnRecord.Content.ToString());

            if(RecordModeEnabled)
            {
                RecordModeEnabled = false;
                btnRecord.Background = ButtonBackgroundBrush;
                
            }
            else
            {
                RecordModeEnabled = true;
                btnRecord.Background = Brushes.Green;

                DateTime DT = DateTime.Now;
                RecordDir = String.Format("RECORDING_{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}",DT.Year, DT.Month, DT.Day, DT.Hour, DT.Minute, DT.Second);
            }
        }

        private void btnBypassClick(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnBypass.Content.ToString());


            BypassModeEnabled = !BypassModeEnabled;

            if (BypassModeEnabled) btnBypass.Background = Brushes.Green;
            if (!BypassModeEnabled) btnBypass.Background = ButtonBackgroundBrush;
        }

        private void btnDefects_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnDefects.Content.ToString());
        }

        private void btnStatistics_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnStatistics.Content.ToString());
        }

        //=====================================================================

        private void btnProcessRecording_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnProcessRecording.Content.ToString());

            //if (ProcessRecordingWindow != null)
            //    return;

            IR = new InspectionReport(RootDir+"\\InspectionReport.csv");

            System.Windows.Forms.FolderBrowserDialog OFD = new System.Windows.Forms.FolderBrowserDialog();
            OFD.RootFolder = Environment.SpecialFolder.MyComputer;
            OFD.SelectedPath = RecordingRootDir;
            OFD.ShowNewFolderButton = false;
            if (OFD.ShowDialog() == System.Windows.Forms.DialogResult.OK )
            {
                if (System.IO.Directory.Exists(OFD.SelectedPath))
                {
                    DirectoryInfo info = new DirectoryInfo(OFD.SelectedPath);
                    FileInfo[] files = info.GetFiles("*_rng.r3", SearchOption.AllDirectories).OrderBy(p => p.LastWriteTime).ToArray();

                    List<string> ProcessFileList = new List<string>();
                    ProcessFileList.Clear();
                    foreach (FileInfo F in files)
                        ProcessFileList.Add(F.FullName);

                    if ( ProcessFileList.Count == 0 )
                    {
                        MessageBox.Show("No pallets were found in this folder.");
                        return;
                    }

                    System.Windows.Forms.Timer T = new System.Windows.Forms.Timer();

                    Logger.WriteLine("ProcessRecordingWindow_ProcessPalletGroup START");
                    //StartProcessingRecordedFile(ProcessFileList);

                    ProgressBar_Clear();
                    ProgressBar_SetText("Processing Recording");
                    ProcessRecordingTotalPalletCount = ProcessFileList.Count;
                    ProcessRecordingFinishedPalletCount = 0;

                    for (int iFile = 0; iFile < ProcessFileList.Count; iFile++)
                    {
                        string Filename = ProcessFileList[iFile];
                        
                        string ImageFilename = Filename.Replace(".r3", ".png");

                        if (true)//(!File.Exists(ImageFilename))
                        {
                            Pallet P = new Pallet(Filename);
                            PProcessor.ProcessPalletLowPriority(P, Pallet_OnProcessMultiplePalletAnalysisComplete);
                        }
                    }

                    ProcessFileList.Clear();

                    Logger.WriteLine("ProcessRecordingWindow_ProcessPalletGroup STOP");
                }
            }
            else
            {
                Logger.WriteLine("btnProcessRecording_Click cancelled directory selection");
            }
        }

        //=====================================================================
        private void btnSettingsControl_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnSettingsControl.Content.ToString());

            if (Password.DlgOpen)
                return;

            Password PB = new Password();
            PB.Closed += (sender2, e2) => 
            {
                if (Password.Passed)
                {
                    ParamConfig PC = new ParamConfig();
                    PC.Show();
                }
            };
            PB.Show();

        }

        //=====================================================================
        private void btnStatusControl_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnStatusControl.Content.ToString());
            StatusWindow SW = new StatusWindow();
            SW.Show();
        }

        //=====================================================================
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if ( (AllFilesInFolder != null) && (AllFilesInFolder.Length > 0) )
            {

                if ( e.Key == Key.Left )
                {
                    if ( LastFileIdx < AllFilesInFolder.Length-1 )
                    {
                        LastFileIdx++;
                        LoadAndProcessCaptureFile(AllFilesInFolder[LastFileIdx]);
                    }
                }
                if (e.Key == Key.Right)
                {
                    if (LastFileIdx > 0)
                    {
                        LastFileIdx--;
                        LoadAndProcessCaptureFile(AllFilesInFolder[LastFileIdx]);
                    }
                }
            }
        }

        //=====================================================================

        private void Window_Closed(object sender, EventArgs e)
        {
            //Logger.WriteBorder("PalletCheck SHUTDOWN");
            //Log.Shutdown();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                Pallet.Shutdown = true;
                PLC.Stop();
                Storage.Stop();
                Camera1.Shutdown();
                Camera2.Shutdown();
                Camera3.Shutdown();
                Logger.WriteBorder("PalletCheck SHUTDOWN");
                Log.Shutdown();
            }
            catch(Exception exp)
            {
                Logger.WriteException(exp);
            }
        }


    }
}
