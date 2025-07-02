using Microsoft.Win32;
using PalletCheck.Controls;
using Sick.EasyRanger;
using Sick.EasyRanger.Base;
using Sick.GenIStream;
using Sick.StreamUI.ImageFormat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using static PalletCheck.Pallet;
using IFrame = Sick.GenIStream.IFrame;

namespace PalletCheck
{
    using static FromGenIStreamFrameConverter;
    using static PalletCheck.PalletDefect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public enum PositionOfPallet
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right=3,
        Front = 4,
        Back = 5,
        TopInternational = 6
    }

    public enum InspectionResult
    {
        PASS,
        FAIL,
        ERROR,
        TIMEOUT
    }

    public partial class MainWindow : Window
    {
        public static MainWindow Singleton;
        public bool RecordModeEnabled { get; private set; }
        public string RecordDir = "";
        public static MainWindow Instance { get; private set; }
        public static ParamStorage ParamStorageGeneral { get; set; } = new ParamStorage("StorageGeneral");
        public static ParamStorage ParamStorageTop { get; set; } = new ParamStorage("Top");
        public static ParamStorage ParamStorageTopInternational { get; set; } = new ParamStorage("TopInternational");
        public static ParamStorage ParamStorageBottom { get; set; } = new ParamStorage("Bottom");
        public static ParamStorage ParamStorageLeft { get; set; } = new ParamStorage("Left");
        public static ParamStorage ParamStorageRight { get; set; } = new ParamStorage("Right");
        public static ParamStorage ParamStorageFront { get; set; } = new ParamStorage("Front");
        public static ParamStorage ParamStorageBack { get; set; } = new ParamStorage("Back");

        string _FilenameDateTime { get; set; }
        string _DirectoryDateHour { get; set; }

        private readonly object _lockFileName = new object();// Define camera names
        string[] cameraNames = { "T", "B1", "B2", "B3", "L", "R", "F", "B" };

        /*Dataset Extraction*/
        public static bool enableDatasetExtraction = false;
        public static int cntr=0;
        public static int SelectPositionForExtraction = 1; //0: Top, 1: Bottom, 2: Left, 3: Right, 4: Front, 5: Back, 6: Top Split boards, 7: Bottom Split boards

        /*Is resize is needed for inference or for sabing images use:  EX. Bitmap resizedImage = model.ResizeBitmap(bitmapClassifier, 512, 512);*/

        /*Save results Top Split images*/
        public static bool isSaveTopSplitResults = true;
        /*Save results Sides nails protruding outside of pallet*/
        public static bool isSaveSideNailsProtrudingResults = false;
        /*Save results for pallet classifier*/
        public static bool isSavePalletClassifierResults = true;
        /*Save results Top nails with head cutoff*/
        public static bool isSaveTopRNWHCO = false;
        /*Buffer for saving the defects*/
        public static bool defectRNWHCO = false;
        /*Save results Bottom nails with head cutoff*/
        public static bool isSaveBottomRNWHCO = false;
        /*Save results for Front*/
        public static bool isSaveFrontResults = false;
        /*Save resutls for Back*/
        public static bool isSaveBackResults = false;



        /*Deep Learning activaion for Right and Left*/
        public static bool isDeepLActive = false;

        /*Pallet Classifier*/
        public static int PalletClassifier = 5;


        public static ParamStorage GetParamStorage(PositionOfPallet position)
        {
            // Returns the corresponding ParamStorage according to the value of the PositionOfPallet enum.
            switch (position)
            {
                case PositionOfPallet.Top:
                    return ParamStorageTop;
                case PositionOfPallet.Bottom:
                    return ParamStorageBottom;
                case PositionOfPallet.Left:
                    return ParamStorageLeft;
                case PositionOfPallet.Right:
                    return ParamStorageRight;
                case PositionOfPallet.Front:
                    return ParamStorageFront;
                case PositionOfPallet.Back:
                    return ParamStorageBack;
                case PositionOfPallet.TopInternational:
                    return ParamStorageTopInternational;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), "Unknown position");
            }
        }
        private TextBlock GetPalletNameTextBlock(PositionOfPallet position)
        {
            switch (position)
            {
                case PositionOfPallet.Top:
                    return PalletName0;
                case PositionOfPallet.Bottom:
                    return PalletName1;
                case PositionOfPallet.Left:
                    return PalletName2;
                case PositionOfPallet.Right:
                    return PalletName3;
                case PositionOfPallet.Front:
                    return PalletName4;
                case PositionOfPallet.Back:
                    return PalletName5;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), "Unknown position");
            }
        }

        public StackPanel GetButtonList(PositionOfPallet position)
        {
            switch (position)
            {
                case PositionOfPallet.Top:
                    return CBB_Button_List;
                case PositionOfPallet.Bottom:
                    return CBB_Button_List1;
                case PositionOfPallet.Left:
                    return CBB_Button_List2;
                case PositionOfPallet.Right:
                    return CBB_Button_List3;
                case PositionOfPallet.Front:
                    return CBB_Button_List4;
                case PositionOfPallet.Back:
                    return CBB_Button_List5;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), "Unknown position");
            }
        }
        // 静态方法2：根据 PositionOfPallet 返回对应的 Grid
        public Grid GetContainer(PositionOfPallet position)
        {
            switch (position)
            {
                case PositionOfPallet.Top:
                    return CBB_Container;
                case PositionOfPallet.Bottom:
                    return CBB_Container1;
                case PositionOfPallet.Left:
                    return CBB_Container;
                case PositionOfPallet.Right:
                    return CBB_Container;
                case PositionOfPallet.Front:
                    return CBB_Container;
                case PositionOfPallet.Back:
                    return CBB_Container;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), "Unknown position");
            }
        }
        InspectionReport IR;
        JackSaveLog JackSaveLog = JackSaveLog.Instance();
        private bool[] loadFrameFlags = new bool[8];

        bool isSaveFrames = true;

        string[] stringNameOfResultTitle = new string[] 
        { "File Name",
            "Top Raised Nail", "Top Missing Planks", "Top Crack across width", "Top Missing Wood", 
            "Bottom Raised Nail", "Bottom Missing Planks", "Bottom Crack across width", "Bottom Missing Wood", 
            "Left Missing Block", "Left Rotated Block(s)", "Left Missing Wood Chunk", 
            "Right Missing Block", "Right Rotated Block(s)", "Right Missing Wood Chunk",
            "Fork Clearance",
            "Left - Side Nails Protruded From pallet",
            "Left - Block Protruded From Pallet ",
            "Right - Side Nails Protruded From Pallet",
            "Right - Block Protuded From Pallet",
            "Top - Missing wood for the full length",
            "Result"};

        string[] csvReportHeader = new string[]
        {
            "File Name",
            "Top Missing Wood Across Lenght",
            "Top Raised Nail Fastener Cut off",
            "Bottom Raised Nail Fastener Cut off",
            "Right Fork Clearance",
            "Right Block Protruding From Pallet",
            "Right Side Nail Protruding",
            "Left Fork Clearance",
            "Left Block Protruding From Pallet",
            "Left Side Nail Protruding",
            "Front Nail Protruding",
            "Front Missing Block",
            "Back Nail Protruding",
            "Back Missing Block",
            "Left-Middle Board Missing Wood",
            "Right-Middle Board Missing Wood",
            "Result"
        };

        HashSet<string> bottomLocations = new HashSet<string>
                {
                    "B_V1", "B_V2", "B_V3", "_H1", "B_H2"
                };

        HashSet<string> topLocations = new HashSet<string>
                {
                    "T_H1", "T_H2", "T_H3", "T_H4", "T_H5", "T_H6", "T_H7"
                };

        HashSet<string> leftLocations = new HashSet<string>
                {
                    "L_B1", "L_B2", "L_B3"
                };

        HashSet<string> rightLocations = new HashSet<string>
                {
                    "R_B1", "R_B2", "R_B3"
                };
        static PalletProcessor PProcessorTop;
        static PalletProcessor PProcessorBottom;

        Statistics Stats = new Statistics();
        Brush SaveSettingsButtonBrush;
        string[] AllFilesInFolder;
        int LastFileIdx = -1;
        bool BypassModeEnabled = false;
        //static string LastPalletSavedName = "";

        public static string Version = "V3.0";
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
        public static string SiteName = "Walmart";

        // UInt16[] IncomingBuffer;
        delegate CaptureBufferBrowser addBufferCB(string s, CaptureBuffer CB);

        PLCComms PLC;
        PLCComms PLCShort;

        // Calculate the Process Time
        Stopwatch stopwatchProcess = new Stopwatch();

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
        private static string _lastUsedParamFileName = "LastUsedParamFile.txt";
        public static string lastUsedConfigFile;
        public static string LastUsedParamFile
        {
            get
            {
                return _LastUsedParamFile;
            }
            set
            {
                _LastUsedParamFile = value;
                System.IO.File.WriteAllText(HistoryRootDir + "\\" + _lastUsedParamFileName, _LastUsedParamFile);
            }
        }

        public static void SetLastUsedParamFileName(string lastName)
        {
            _lastUsedParamFileName = lastName;
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
                if (MessageBox.Show("Another instance of PalletCheck is already running!\n\nDo you want to start a NEW instance?", "PALLETCHECK Already Running", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
            RootDir = Environment.GetEnvironmentVariable("SICK_PALLETCHECK_ROOT_DIR");
            if (RootDir == null)
            {
                RootDir = "C:\\PalletCheck";
                //MessageBox.Show("Could not find SICK_PALLETCHECK_ROOT_DIR env var");
                //Close();
                //return;
            }

            ConfigRootDir = RootDir + "\\Config";
            HistoryRootDir = RootDir + "\\History";
            RecordingRootDir = "C:\\PalletCheck" + "\\Recordings";
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
                SiteName = "Walmart";


            InitializeComponent();
            Instance = this;       
            for (int i = 0; i < 8; i++)
            {
                string statusTextName = $"Camera{i + 1}StatusText";
                string indicatorName = $"Camera{i + 1}StatusIndicator";

                // Get the status indicator and status text control for the camera
                var statusText = (TextBlock)this.FindName(statusTextName);
                var indicator = (Ellipse)this.FindName(indicatorName);
                statusText.Text = $"{cameraNames[i]}: Searching"; // Use camera name
                indicator.Fill = Brushes.Yellow; // Green indicates the camera has started

            }
            //MainWindow.Icon = BitmapFrame.Create(Application.GetResourceStream(new Uri("LiveJewel.png", UriKind.RelativeOrAbsolute)).Stream);
        }

        private static void CurrentDomainOnUnhandledException(UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;

            Logger.WriteBorder("CurrentDomainOnUnhandledException");
            Logger.WriteException(exception);

            string errorMessage = string.Format("An application error occurred.\nPlease check whether your data is correct and repeat the action. If this error occurs again there seems to be a more serious malfunction in the application, and you better close it.\n\nError: {0}\n\nDo you want to continue?\n(if you click Yes you will continue with your work, if you click No the application will close)",

            exception.Message + (exception.InnerException != null ? "\n" + exception.InnerException.Message : null));

            if (MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.YesNoCancel, MessageBoxImage.Error) == MessageBoxResult.No)
            {
                if (MessageBox.Show("WARNING: The application will close. Any changes will not be saved!\nDo you really want to close it?", "Close the application!", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
                }
            }
        }

        private void LoadParameters(string paramFilePath)
        {
            foreach (PositionOfPallet position in Enum.GetValues(typeof(PositionOfPallet)))
            {
                GetParamStorageByPosition(position).LoadXMLParameters(paramFilePath, position.ToString());
            }        
        }


        private ParamStorage GetParamStorageByPosition(PositionOfPallet position)
        {
            // Return the corresponding parameter storage object based on the enumeration value.
            switch (position)
            {
                case PositionOfPallet.Left:
                    return ParamStorageLeft;
                case PositionOfPallet.Right:
                    return ParamStorageRight;
                case PositionOfPallet.Top:
                    return ParamStorageTop;
                case PositionOfPallet.Bottom:
                    return ParamStorageBottom;
                case PositionOfPallet.Front:
                    return ParamStorageFront;
                case PositionOfPallet.Back:
                    return ParamStorageBack;
                case PositionOfPallet.TopInternational:
                    return ParamStorageTopInternational;
                default:
                    throw new ArgumentException("Invalid position", nameof(position));
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = StringsLocalization.UI_Title;
            StatusStorage.Set("Version Number", Version);
            ButtonBackgroundBrush = btnStart.Background;
            
            try
            {
                string configFilePath = ConfigRootDir + "\\Config.xml";
                string paramFilePath = ConfigRootDir + "\\Parameters.xml";

                if (File.Exists(configFilePath))
                {
                    ParamStorageGeneral.LoadXML(configFilePath);

                    if (File.Exists(paramFilePath))
                    {
                        LoadParameters(paramFilePath);
                    }                
                }

                else
                {
                    MessageBox.Show("Config file not found at " + ConfigRootDir);
                    Logger.WriteLine("Config file not found at" + ConfigRootDir);
                }            
            }

            catch (Exception)
            {
                throw;
            }
            

            // Open PLC connection port
            int Port = ParamStorageGeneral.GetInt("TCP Server Port");
            int PortShort = ParamStorageGeneral.GetInt("TCP Server Port For Short Result");
            string IP = ParamStorageGeneral.GetString("TCP Server IP");
            bool isServer = Convert.ToBoolean(ParamStorageGeneral.GetInt("Server"));
            PLC = new PLCComms(IP, Port, isServer);
            PLC.Start();
            PLCShort = new PLCComms(IP, PortShort, isServer);
            PLCShort.Start();

            Storage = new StorageWatchdog();
            Storage.Start();

            System.Windows.Forms.Timer T = new System.Windows.Forms.Timer();
            T.Interval = 1000;
            T.Tick += TimerUpdate500ms;
            T.Start();


            System.Windows.Forms.Timer T2 = new System.Windows.Forms.Timer();
            T2.Interval = 5000;
            T2.Tick += Timer5Sec;
            T2.Start();
            ClearAnalysisResults(PositionOfPallet.Top);
            //ProgressBar_Clear();

            PProcessorTop = new PalletProcessor(7, 7);
            PProcessorBottom = new PalletProcessor(5, 5);


            ParamStorageGeneral.HasChangedSinceLastSave = false;
            SaveSettingsButtonBrush = btnSettingsControl.Background;
            System.Windows.Forms.Timer T3 = new System.Windows.Forms.Timer();
            T3.Interval = 500;
            T3.Tick += SaveSettingsColorUpdate;
            T3.Start();

            Singleton = this;



            _discovery = CameraDiscovery.CreateFromProducerFile("SICKGigEVisionTL.cti");

            // Read the number of cameras from the configuration file
            cameraCount = ParamStorageGeneral.GetInt("CameraCount");
            StatusStorage.Set("SICK Cameras Count", cameraCount.ToString());

            await Task.Run(() =>
            {
                InitializeCamerasAndParameters();
            });
            ViewerLeft.Environment = _envLeft;
            ViewerRight.Environment = _envRight;
            ViewerFront.Environment = _envFront;
            ViewerBack.Environment = _envBack;
           
            btnLoad_Right.IsEnabled = true; btnLoad_Left.IsEnabled = true;
            btnLoad_Top.IsEnabled = true; btnLoad_Bottom.IsEnabled = true;
            btnLoad_Front.IsEnabled = true; btnLoad_Back.IsEnabled = true;
            btnProcessRecording.IsEnabled = true;
            btnProcessPallet.IsEnabled = true;
        }



        private void ProcessFrameForCameraTopCallback(GrabResult result)
        {
            stopwatchProcess.Reset();
            stopwatchProcess.Start();
            UpdateTextBlock(LogText, "Top New Frame", MessageState.Normal);
            UpdateTextBlock(PalletName0, "▲");
            //_FilenameDateTime
            //TryGenerateFilenameDateTime();
            result.IfCompleteFrame(frame =>
            {
                try
                {
                    SickFrames[0] = frame.Copy();

                    var pallet = CreateTopPallet();

                    PProcessorTop.ProcessPalletHighPriority(pallet, Pallet_OnLivePalletAnalysisComplete, PositionOfPallet.Top);
                    // ProcessTop(FrameBuffer[cameraName]);
                }
                catch (Exception ex)
                {
                    UpdateTextBlock(PalletName0, $"Error: {ex.Message}", MessageState.Critical);
                }
            });


        }

        private void ProcessFrameForCameraBottomLeftCallback(GrabResult result)
        {

            UpdateTextBlock(LogText, "BottomLeft New Frame", MessageState.Normal);
            UpdateTextBlock(PalletName1, "▲");
            //TryGenerateFilenameDateTime();
            result.IfCompleteFrame(frame =>
            {
                try
                {

                    SickFrames[1] = frame.Copy();  // Bottom Left
                    framesReceivedCount++;

                    if (framesReceivedCount == 3)
                    {
                        framesReceivedCount = 0;  // Reset counter
                        OnAllFramesReceived?.Invoke();  // Trigger the merge event
                    }
                }
                catch (Exception ex)
                {
                    UpdateTextBlock(LogText, $"Bottom1 Error processing frame: {ex.Message}", MessageState.Critical);
                }
            });

        }
        private void ProcessFrameForCameraBottomMidCallback(GrabResult result)
        {
            //TryGenerateFilenameDateTime();


            UpdateTextBlock(LogText, "BottomMiddle New Frame", MessageState.Normal);
            result.IfCompleteFrame(frame =>
            {
                try
                {
                    SickFrames[2] = frame.Copy(); // Bottom Middle
                    framesReceivedCount++;

                    if (framesReceivedCount == 3)
                    {
                        framesReceivedCount = 0;  // Reset counter
                        OnAllFramesReceived?.Invoke();  // Trigger the merge event
                    }
                }
                catch (Exception ex)
                {
                    UpdateTextBlock(LogText, $"Bottom2 Error processing frame: {ex.Message}", MessageState.Critical);
                }
            });
        }
        private void ProcessFrameForCameraBottomRightCallback(GrabResult result)
        {
            //TryGenerateFilenameDateTime();

            UpdateTextBlock(LogText, "BottomRight New Frame", MessageState.Normal);
            result.IfCompleteFrame(frame =>
            {
                try
                {

                    SickFrames[3] = frame.Copy();  // Bottom Right
                    framesReceivedCount++;

                    if (framesReceivedCount == 3)
                    {
                        framesReceivedCount = 0;  // Reset counter
                        OnAllFramesReceived?.Invoke();  // Trigger the merge event
                    }
                }
                catch (Exception ex)
                {
                    // 记录异常日志，防止程序崩溃
                    UpdateTextBlock(LogText, $"Bottom3 Error processing frame: {ex.Message}", MessageState.Critical);
                }
            });
        }

        private readonly object _envLeftLock = new object();
        private readonly object _envRightLock = new object();
        private readonly object _envFrontLock = new object();
        private readonly object _envBackLock = new object();
        /// <summary>
        /// For the Left
        /// </summary>
        /// <param name="result"></param>
        private void ProcessFrameForCameraLeftCallback(GrabResult result)
        {
            //TryGenerateFilenameDateTime();

            UpdateTextBlock(LogText, "Left New Frame", MessageState.Normal);
            UpdateTextBlock(PalletName2, "▲");
            result.IfCompleteFrame(frame =>
            {
                SickFrames[4] = frame.Copy();
         
                lock (_envLeftLock)
                {

                    AddFrameToEnvironment(frame, "Image", _envLeft);
                }

               
                Task.Run(() =>
                {
                    try
                    {
                        ProcessMeasurement(_status =>
                        {
                        }, PositionOfPallet.Left);
                    }
                    catch (Exception ex)
                    {

                        Logger.WriteLine($"Error: {ex.Message}");
                    }
                });

            });
        }

        private void ProcessFrameForCameraRightCallback(GrabResult result)
        {
            //TryGenerateFilenameDateTime();
            UpdateTextBlock(LogText, "Right New Frame", MessageState.Normal);
            UpdateTextBlock(PalletName3, "▲");
            result.IfCompleteFrame(frame =>
            {
                SickFrames[5] = frame.Copy();
                
                lock (_envRightLock)
                {
                    AddFrameToEnvironment(frame, "Image", _envRight);

                }

                try
                {
                    ProcessMeasurement(_status =>
                    {
                    }, PositionOfPallet.Right);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error: {ex.Message}");
                }
            });
        }

        private void ProcessFrameForCameraFrontCallback(GrabResult result)
        {
            UpdateTextBlock(LogText, "Front New Frame", MessageState.Normal);
            UpdateTextBlock(PalletName4, "▲");
            result.IfCompleteFrame(frame =>
            {
                SickFrames[6] = frame.Copy();

                lock (_envFrontLock)
                {
                    AddFrameToEnvironment(frame, "Image", _envFront);

                }

                try
                {
                    ProcessMeasurementFrontBack(_status =>
                    {
                    }, PositionOfPallet.Front);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error: {ex.Message}");
                }
            });
        }
        private void ProcessFrameForCameraBackCallback(GrabResult result)
        {
            UpdateTextBlock(LogText, "Back New Frame", MessageState.Normal);
            UpdateTextBlock(PalletName5, "▲");
            result.IfCompleteFrame(frame =>
            {
                SickFrames[7] = frame.Copy();

                lock (_envBackLock)
                {
                    AddFrameToEnvironment(frame, "Image", _envBack);

                }

                try
                {
                    ProcessMeasurementFrontBack(_status =>
                    {
                    }, PositionOfPallet.Back);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Error: {ex.Message}");
                }
            });
        }


        private void SaveSettingsColorUpdate(object sender, EventArgs e)
        {
            if (ParamStorageGeneral.HasChangedSinceLastSave)
            {
                if (btnSettingsControl.Background == SaveSettingsButtonBrush)
                    btnSettingsControl.Background = new SolidColorBrush(Color.FromArgb(255, 196, 196, 0));
                else
                    btnSettingsControl.Background = SaveSettingsButtonBrush;
            }
            else
            {
                btnSettingsControl.Background = SaveSettingsButtonBrush;
            }
        }

        private void Timer5Sec(object sender, EventArgs e)
        {
            Logger.WriteHeartbeat();

            if (ParamStorageGeneral.GetInt("TCP Server Heartbeat") == 1)
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

        private bool flashState = true; //  Global state used to control red-black flashing
        private bool _colonVisible = true;
        private void TimerUpdate500ms(object sender, EventArgs e)
        {
            // Update the current date and time
            DateTime now = DateTime.Now;
            string currentDateTime = $"{now.ToShortDateString()} {now.ToLongTimeString()}";
            _colonVisible = !_colonVisible; // 切换冒号显示状态
            CurDateTime.Text = currentDateTime;
            //UpdateDigitalClock(CurDateTime, _colonVisible);
            //CurDateTime.Text = currentDateTime;
            StatusStorage.Set("Date/Time", currentDateTime);
            StatusStorage.Set("SettingsChanged", ParamStorageGeneral.HasChangedSinceLastSave.ToString());

            bool allCamerasConnected = true; // Assume all cameras are connected

            // Iterate through all cameras and check their status
            for (int i = 0; i < Cameras.Count; i++)
            {
                var camera = Cameras[i];
                bool isConnected = camera.CameraConnectionState == R3Cam.ConnectionState.Connected;
                bool isStarted = camera.CameraCaptureState == R3Cam.CaptureState.Capturing;
                string statusTextName = $"Camera{i + 1}StatusText";
                string indicatorName = $"Camera{i + 1}StatusIndicator";

                // Get the status indicator and status text control for the camera
                var statusText = (TextBlock)this.FindName(statusTextName);
                var indicator = (Ellipse)this.FindName(indicatorName);



                if (statusText != null && indicator != null)
                {
                    // Update the status text and indicator color
                    if (isConnected)
                    {
                        if (isStarted)
                        {
                            statusText.Text = $"{cameraNames[i]}: Started"; // Use camera name
                            indicator.Fill = Brushes.Green; // Green indicates the camera has started
                        }
                        else
                        {
                            statusText.Text = $"{cameraNames[i]}: Stopped"; // Use camera name
                            indicator.Fill = Brushes.Blue; // Blue indicates the camera is connected but not started
                        }
                    }
                    else
                    {
                        // Red and black flashing indicates the camera is disconnected
                        statusText.Text = $"{cameraNames[i]}: Disconnected"; // Use camera name
                        indicator.Fill = flashState ? Brushes.Red : Brushes.Black;
                        allCamerasConnected = false; // Set to false if any camera is disconnected
                    }
                }

            }

            // Toggle the red-black flashing state
            flashState = !flashState;

            // Update the button background color based on the overall connection status
            if (!allCamerasConnected)
            {
                // Switch between two colors when there are disconnected cameras
                btnStatusControl.Background =
                    (btnStatusControl.Background == SaveSettingsButtonBrush)
                        ? new SolidColorBrush(Color.FromArgb(255, 196, 196, 0)) // Yellow indicates some cameras are disconnected
                        : SaveSettingsButtonBrush;
                btnStatusControl.Foreground = (btnStatusControl.Background == SaveSettingsButtonBrush)
                        ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Black);
            }
            else
            {
                // Restore the default background color when all cameras are connected
                btnStatusControl.Background = SaveSettingsButtonBrush;
                btnStatusControl.Foreground = new SolidColorBrush(Colors.White);
            }

        }



        //=====================================================================

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {

            Logger.ButtonPressed(btnStart.Content.ToString());

            ParamStorageGeneral.SaveInHistory("LINESTART");


            if (btnStart.Content.ToString() == "START")
            {
                foreach (var cam in Cameras)
                {
                    cam.StartCapturingFrames();
                }
                btnStart.Content = "STOP";
                ModeStatus.Text = "RUNNING";
                btnStart.Background = System.Windows.Media.Brushes.Green;
            }
            else if (btnStart.Content.ToString() == "STOP")
            {
                foreach (var cam in Cameras)
                {
                    cam.StopCapturingFrames();
                }
                btnStart.Content = "START";
                ModeStatus.Text = "IDLING";
                btnStart.Background = ButtonBackgroundBrush;
            }

        }

        //=====================================================================
        public CaptureBufferBrowser AddCaptureBufferBrowser(string Name, CaptureBuffer CB, string SelectName, PositionOfPallet position)
        {
            // Set the name of the CaptureBuffer
            CB.Name = Name;

            // Create an instance of CaptureBufferBrowser
            CaptureBufferBrowser CBB = new CaptureBufferBrowser();
            CBB.SetParamStorage(ParamStorageTop);
            CBB.SetCB(CB);
            //CBB.SetScale(CB.xScale, CB.yScale);

            // Create a button and set its properties
            Button B = new Button
            {
                Content = Name,
                Margin = new Thickness(1),
                Height = 20,
                Background = Singleton.btnProcessPallet.Background,
                Foreground = Singleton.btnProcessPallet.Foreground,
                FontSize = 14,
                Tag = CBB // Keep Tag as CBB
            };

            // Use a Lambda expression to pass the position to the Click event
            B.Click += (s, e) => Singleton.ImageTab_Click(s, e, position);

            // Dynamically retrieve the Button List and Container based on position and add controls
            GetButtonList(position).Children.Add(B);
            GetContainer(position).Children.Add(CBB);

            // Refresh the CaptureBufferBrowser and set its visibility to hidden
            CBB.Refresh();
            CBB.Visibility = Visibility.Hidden;

            // If the name matches SelectName, trigger the ImageTab_Click event
            if (Name == SelectName)
                Singleton.ImageTab_Click(B, new RoutedEventArgs(), position);

            // Return the instance of CaptureBufferBrowser
            return CBB;
        }

        //=====================================================================
        void ClearAnalysisResults(PositionOfPallet position)
        {
            try
            {
                TotalResultTextBlock.Text = "";
                PalletType.Text = "Pallet Class";
                //// Ensure controls are initialized and updated on the UI thread
                //if (imgPassSymbol == null || imgPassText == null || imgFailSymbol == null || imgFailText == null)
                //{
                //    throw new InvalidOperationException("One or more visibility-related UI elements are null.");
                //}

                var container = this.GetContainer(position);
                if (container == null)
                {
                    throw new InvalidOperationException("Container is null.");
                }

                var buttonList = this.GetButtonList(position);
                if (buttonList == null)
                {
                    throw new InvalidOperationException("Button list container is null.");
                }

                var palletNameTextBlock = this.GetPalletNameTextBlock(position);
                //if (palletNameTextBlock == null)
                //{
                //    throw new InvalidOperationException("PalletName TextBlock is null.");
                //}

                if (defectTable == null)
                {
                    throw new InvalidOperationException("Defect table is null.");
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Clear child controls
                    foreach (var child in container.Children)
                    {
                        var CBC = child as CaptureBufferBrowser;
                        if (CBC != null)
                        {
                            CBC.Clear();
                        }
                        else
                        {
                            Logger.WriteLine($"Unexpected child type in container: {child.GetType()}");
                        }
                    }

                    // Clear child controls in button list and container
                    buttonList.Children.Clear();
                    container.Children.Clear();

                    //// Clear PalletName
                    //palletNameTextBlock.Text = "";

                    // Clear items in DefectTable
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error clearing UI elements: {ex.Message}");
                throw;
            }
        }

        //=====================================================================
        private void LoadAndProcessCaptureFile(PositionOfPallet position, string FileName, bool RebuildList = false)
        {
            Logger.WriteLine("Loading CaptureBuffer from: " + FileName);

            ClearAnalysisResults(position);


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

            Pallet P = new Pallet(FileName, position);

            //P.NotLive = true;
            PProcessorTop.ProcessPalletHighPriority(P, Pallet_OnProcessSinglePalletAnalysisComplete, position);
            //PProcessor.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete);

        }


        public Pallet CreateTopPallet()
        {
            try
            {
                // Add frame to the environment
                AddFrameToEnvironment(SickFrames[0], "Image", _envTop);
                _envTop.GetStepProgram("Main").RunFromBeginning();

                // Retrieve image data
                float[] floatArray = MainWindow._envTop.GetImageBuffer("CropImage")._range;
                byte[] byteArray = MainWindow._envTop.GetImageBuffer("CropImage")._intensity;

                // Convert to ushort array (float -> ushort)
                ushort[] ushortArray = new ushort[floatArray.Length];
                for (int i = 0; i < floatArray.Length; i++)
                {
                    ushortArray[i] = (ushort)(floatArray[i] + 1000);
                }

                // Create a new buffer
                CaptureBuffer NewBuf = new CaptureBuffer
                {
                    Buf = ushortArray,
                    Width = MainWindow._envTop.GetImageBuffer("CropImage").Info.Width,
                    Height = MainWindow._envTop.GetImageBuffer("CropImage").Info.Height,
                    xScale = MainWindow._envTop.GetImageBuffer("CropImage").Info.XResolution,
                    yScale = MainWindow._envTop.GetImageBuffer("CropImage").Info.YResolution
                };

                // Convert to ushort array (byte -> ushort)
                ushort[] ushortArrayRef = new ushort[byteArray.Length];
                for (int i = 0; i < ushortArrayRef.Length; i++)
                {
                    ushortArrayRef[i] = byteArray[i];
                }

                // Create a reference buffer
                CaptureBuffer ReflBuf = new CaptureBuffer
                {
                    Buf = ushortArrayRef,
                    PaletteType = CaptureBuffer.PaletteTypes.Gray,
                    Width = MainWindow._envTop.GetImageBuffer("CropImage").Info.Width,
                    Height = MainWindow._envTop.GetImageBuffer("CropImage").Info.Height,
                    xScale = MainWindow._envTop.GetImageBuffer("CropImage").Info.XResolution,
                    yScale = MainWindow._envTop.GetImageBuffer("CropImage").Info.YResolution
                };

                // Return the created Pallet object
                return new Pallet(NewBuf, ReflBuf, PositionOfPallet.Top);
            }
            catch (NullReferenceException ex)
            {
                Logger.WriteLine("Null reference error: " + ex.Message);
                throw new InvalidOperationException("A null reference occurred while processing the pallet.", ex);
            }
            catch (IndexOutOfRangeException ex)
            {
                Logger.WriteLine("Index out of range error: " + ex.Message);
                throw new InvalidOperationException("An index was out of range during pallet processing.", ex);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("General error: " + ex.Message);
                UpdateTextBlock(LogText, ex.Message, MessageState.Warning);
                throw new InvalidOperationException("An unexpected error occurred while processing the pallet.", ex);
            }
        }

        public Pallet CreateBottomPallet()
        {
            // Add frames to the environment
            AddFrameToEnvironment(SickFrames[1], "Left", _envBottom);
            AddFrameToEnvironment(SickFrames[2], "Mid", _envBottom);
            AddFrameToEnvironment(SickFrames[3], "Right", _envBottom);

            // Set environment variables
            for (int i = 0; i < 3; i++)
            {
                string startXName = $"StartX{i}";
                string endXName = $"EndX{i}";
                string offsetZName = $"OffsetZ{i}";

                _envBottom.SetInteger(startXName, StartX[i]);
                _envBottom.SetInteger(endXName, EndX[i]);
                _envBottom.SetDouble(offsetZName, OffsetZ[i]);
            }
            // Run the program and save the environment
            _envBottom.GetStepProgram("Main").RunFromBeginning();

            // Retrieve image data
            float[] floatArray = MainWindow._envBottom.GetImageBuffer("ComImage")._range;
            byte[] byteArray = MainWindow._envBottom.GetImageBuffer("ComImage")._intensity;

            // Convert to ushort array (float -> ushort)
            ushort[] ushortArray = new ushort[floatArray.Length];
            for (int i = 0; i < floatArray.Length; i++)
            {
                ushortArray[i] = (ushort)(floatArray[i] + 1000);
            }

            // Create a new buffer
            CaptureBuffer NewBuf = new CaptureBuffer
            {
                Buf = ushortArray,
                Width = MainWindow._envBottom.GetImageBuffer("Image").Info.Width,
                Height = MainWindow._envBottom.GetImageBuffer("Image").Info.Height,
                xScale = MainWindow._envBottom.GetImageBuffer("Image").Info.XResolution,
                yScale = MainWindow._envBottom.GetImageBuffer("Image").Info.YResolution
            };

            // Convert to ushort array (byte -> ushort)
            ushort[] ushortArrayRef = new ushort[byteArray.Length];
            for (int i = 0; i < ushortArrayRef.Length; i++)
            {
                ushortArrayRef[i] = byteArray[i];
            }

            // Create a reference buffer
            CaptureBuffer ReflBuf = new CaptureBuffer
            {
                Buf = ushortArrayRef,
                PaletteType = CaptureBuffer.PaletteTypes.Gray,
                Width = MainWindow._envBottom.GetImageBuffer("Image").Info.Width,
                Height = MainWindow._envBottom.GetImageBuffer("Image").Info.Height,
                xScale = MainWindow._envBottom.GetImageBuffer("Image").Info.XResolution,
                yScale = MainWindow._envBottom.GetImageBuffer("Image").Info.YResolution
            };

            // Create and return the Pallet object
            return new Pallet(NewBuf, ReflBuf, PositionOfPallet.Bottom);
        }


        private void LoadAndProcessCapture6FileFrame()
        {
            isSaveFrames = false;
            //Logger.WriteLine("Loading CaptureBuffer from: " + FileName);
            OpenFileDialog OFD = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*", 
                InitialDirectory = MainWindow.RecordingRootDir,        
                Title = "Select Bottom Images"                           
            };

            if (OFD.ShowDialog() != true)
            {

                Logger.WriteLine("File dialog cancelled by user.");
                return; 
            }

            try
            {

                string filePath = OFD.FileName;

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    Logger.WriteLine("No valid file selected.");
                    return; 
                }

            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading file: {ex.Message}");
            }

            string selectedFile = OFD.FileName;
            string directory = System.IO.Path.GetDirectoryName(selectedFile);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(selectedFile);           
            string baseName = fileName.Substring(0, fileName.LastIndexOf('_') + 1);
            UpdateTextBlock(ModeStatus, "Loading: " + fileName.Substring(0, fileName.LastIndexOf('_')));
            _FilenameDateTime = fileName.Substring(0, fileName.LastIndexOf('_'));
            Pallet.folderName = Directory.GetParent(selectedFile).Name;
            string file0 = System.IO.Path.Combine(directory, baseName + "T.xml");
            string file1 = System.IO.Path.Combine(directory, baseName + "B1.xml");
            string file2 = System.IO.Path.Combine(directory, baseName + "B2.xml");
            string file3 = System.IO.Path.Combine(directory, baseName + "B3.xml");
            string file4 = System.IO.Path.Combine(directory, baseName + "L.xml");
            string file5 = System.IO.Path.Combine(directory, baseName + "R.xml");
            string file6 = System.IO.Path.Combine(directory, baseName + "F.xml");
            string file7 = System.IO.Path.Combine(directory, baseName + "B.xml");
            try
            {
                if (loadFrameFlags[0])
                {

                    ProcessFrameForCameraTopCallback(GrabResult.CreateWithFrame(IFrame.Load(file0)));
                    Logger.WriteLine("Loaded file0");
                }

                if (loadFrameFlags[1])
                {

                    ProcessFrameForCameraBottomLeftCallback(GrabResult.CreateWithFrame(IFrame.Load(file1)));
                    Logger.WriteLine("Loaded file1");
                }

                if (loadFrameFlags[2])
                {

                    ProcessFrameForCameraBottomMidCallback(GrabResult.CreateWithFrame(IFrame.Load(file2)));
                    Logger.WriteLine("Loaded file2");
                }

                if (loadFrameFlags[3])
                {
                    ProcessFrameForCameraBottomRightCallback(GrabResult.CreateWithFrame(IFrame.Load(file3)));
                    Logger.WriteLine("Loaded file3");
                }

                if (loadFrameFlags[4])
                {
                    ProcessFrameForCameraLeftCallback(GrabResult.CreateWithFrame(IFrame.Load(file4)));
                    Logger.WriteLine("Loaded file4");
                }

                if (loadFrameFlags[5])
                {
                    ProcessFrameForCameraRightCallback(GrabResult.CreateWithFrame(IFrame.Load(file5)));
                    Logger.WriteLine("Loaded file5");
                }

                if (loadFrameFlags[6])
                {
                    ProcessFrameForCameraFrontCallback(GrabResult.CreateWithFrame(IFrame.Load(file6)));
                    Logger.WriteLine("Loaded file6");
                }

                if (loadFrameFlags[7])
                {
                    ProcessFrameForCameraBackCallback(GrabResult.CreateWithFrame(IFrame.Load(file7)));
                    Logger.WriteLine("Loaded file7");
                }

                Logger.WriteLine("Load Offline Images Completed");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Error loading files: " + ex.Message);
            }


        }
        private void SetLoadFlags(params int[] indices)
        {
            for (int i = 0; i < loadFrameFlags.Length; i++)
            {
                loadFrameFlags[i] = indices.Contains(i); // If the index is in the passed-in array, set true
            }
        }
        //=====================================================================
        private void UpdateProductivityStats(bool isPass)
        {
            Stats.OnNewPallet(isPass);
            statisticsTable.Items.Clear();

            // Send defects to the defectTable
            for (int i = 0; i < Stats.Entries.Count; i++)
            {
                statisticsTable.Items.Add(Stats.Entries[i]);
            }
        }

        //=====================================================================
        private void ImageTab_Click(object sender, RoutedEventArgs e, PositionOfPallet position)
        {
            try
            {
                // Get the button that triggered the event
                Button B = (Button)sender;

                // Retrieve the button's Tag and convert it to a CaptureBufferBrowser
                CaptureBufferBrowser CBB = (CaptureBufferBrowser)B.Tag;

                // Log the button press event
                Logger.ButtonPressed("CaptureBuffer." + B.Content.ToString() + " at position " + position.ToString());

                // If FastBitmap is null, create it
                if (CBB.FB == null)
                    CBB.FB = CBB.CB.BuildFastBitmap();

                // Hide all child elements of the Container
                var container = GetContainer(position); // Retrieve the Container based on the position
                for (int i = 0; i < container.Children.Count; i++)
                    container.Children[i].Visibility = Visibility.Hidden;

                // Make the current CaptureBufferBrowser visible
                CBB.Visibility = Visibility.Visible;
            }
            catch (Exception exp)
            {
                // Log the exception details
                Logger.WriteException(exp);
            }

        }

        private void btnProcessPallet_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnProcessPallet.Content.ToString());
            //TryGenerateFilenameDateTime();
            SetLoadFlags(0,1,2,3,4,5,6,7);
            LoadAndProcessCapture6FileFrame();
        }

        //=============Jack Note: Jack Added sending short result using another Port1========================================================
        private void SendPLCTotalResult(Pallet P)
        {
            if (P.AllDefects.Count > 0)
            {
                PLCShort.SendMessage("FAIL");
            }
            else
            {
                PLCShort.SendMessage("PASS");
            }

        }

        private void SendPLCTotalResult(string result)
        {
                PLCShort.SendMessage(result);

        }
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

        private void Pallet_OnProcessSinglePalletAnalysisComplete(Pallet P, PositionOfPallet position)
        {
            Logger.WriteLine("Pallet_OnProcessSinglePalletAnalysisComplete");
          //  UpdateProductivityStats(P);
            //SendPLCResults(P);
            //SendPLCTotalResult(P);

            UpdateUIWithPalletResults(P, position);
        }

        private void Pallet_OnProcessMultiplePalletAnalysisComplete(Pallet P, PositionOfPallet position)
        {
            Logger.WriteLine("Pallet_OnProcessMultiplePalletAnalysisComplete");

            //ProcessRecordingFinishedPalletCount += 1;
            //ProgressBar_SetText(string.Format("Processing Recording... Finished {0} of {1}", ProcessRecordingFinishedPalletCount, ProcessRecordingTotalPalletCount));
            //ProgressBar_SetProgress(ProcessRecordingFinishedPalletCount, ProcessRecordingTotalPalletCount);

            //IR.AddPallet(P);

            //if (ProcessRecordingFinishedPalletCount >= ProcessRecordingTotalPalletCount)
            //{
            //    ProgressBar_SetText("Processing Recording... COMPLETE");
            //    IR.Save();
            //    ShowCsvData(RootDir + "\\InspectionReport.csv");
            //}
        }

        private void Pallet_OnLivePalletAnalysisComplete(Pallet P, PositionOfPallet position)
        {
            Logger.WriteLine("Pallet_OnLivePalletAnalysisComplete Start");
            if (!BypassModeEnabled)
            {
                // Update production stats
          //      UpdateProductivityStats(P);
            }

            // Notify PLC of pallet results
            //SendPLCResults(P);
            //SendPLCTotalResult(P);
            UpdateUIWithPalletResults(P, position);

            Logger.WriteLine("Pallet_OnLivePalletAnalysisComplete Complete");
            //Logger.WriteBorder("PALLET COMPLETE: " + P.Filename);

        }
        //=====================================================================
        private void UpdateUIWithPalletResults(Pallet P, PositionOfPallet position)
        {

            ClearAnalysisResults(position);

            Logger.WriteLine("pallet filename: " + P.Filename);

            int c = 0;
            List<PalletDefect> FullPalletDefects = P.PalletLevelDefects;
            for (int j = 0; j < FullPalletDefects.Count; j++)
            {
                PalletDefect PD = FullPalletDefects[j];
                defectTable.Items.Add(PD);
                c += 1;
                Logger.WriteLine(string.Format("DEFECT | {0:>2} | {1:>2}  {2:>4}  {3:>8}  {4}", c, "Pallet", PD.Code, PD.Name, PD.Comment));
            }

            Logger.WriteLine(string.Format("DEFECT COUNT {0}", c));

             ProcessCameraResult((int)position, result: P.AllDefects.Count == 0 ? InspectionResult.PASS : InspectionResult.FAIL);

            // Build CaptureBufferBrowsers
            if (true)
            {
                // Check if segmentation error
                bool HasSegmentationError = false;
                if ((P.PalletLevelDefects.Count > 0) && (P.PalletLevelDefects[0].Type == PalletDefect.DefectType.board_segmentation_error))
                {
                    HasSegmentationError = true;
                }

                foreach (CaptureBuffer CB in P.CBList)
                {
                    CaptureBufferBrowser CBB = AddCaptureBufferBrowser(CB.Name, CB, HasSegmentationError ? "Original" : "Filtered", position);

                    foreach (PalletDefect PD in P.AllDefects)
                    {
                        if (PD.MarkerRadius > 0)
                        {
                            CBB.MarkDefect(new Point(PD.MarkerX1, PD.MarkerY1), PD.MarkerRadius, PD.MarkerTag);
                            IRoi roi0 = new Roi(
                               (uint)PD.MarkerX1,
                               (uint)PD.MarkerY1,
                               (uint)(PD.MarkerX1 + PD.MarkerRadius),
                               (uint)(PD.MarkerY1 + PD.MarkerRadius),
                               RoiType.Ellipse
                           );

                        }
                        else if ((PD.MarkerRadius == 0) && (PD.MarkerX1 != 0))
                        {
                            CBB.MarkDefect(new Point(PD.MarkerX1, PD.MarkerY1), new Point(PD.MarkerX2, PD.MarkerY2), PD.MarkerTag);
                            IRoi roi = new Roi(
                                (uint)PD.MarkerX1,
                                (uint)PD.MarkerY1,
                                (uint)PD.MarkerX2,
                                (uint)PD.MarkerY2,
                                RoiType.Rectangle
                            );
                        }
                    }
                }


            }

        }



        //=====================================================================
        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnRecord.Content.ToString());

            if (RecordModeEnabled)
            {
                RecordModeEnabled = false;
                btnRecord.Background = ButtonBackgroundBrush;

            }
            else
            {
                RecordModeEnabled = true;
                btnRecord.Background = Brushes.Green;

                DateTime DT = DateTime.Now;
                RecordDir = String.Format("RECORDING_{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}", DT.Year, DT.Month, DT.Day, DT.Hour, DT.Minute, DT.Second);
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
            Stats.Reset();
            statisticsTable.Items.Clear();
            for (int i = 0; i < Stats.Entries.Count; i++)
            {
                statisticsTable.Items.Add(Stats.Entries[i]);
            }
            Logger.ButtonPressed(btnStatistics.Content.ToString());
        }

        //=====================================================================

        private async void btnProcessRecording_Click(object sender, RoutedEventArgs e)
        {
            ProgressBar.Visibility = Visibility.Visible;
            Logger.ButtonPressed(btnProcessRecording.Content.ToString());

            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
                folderBrowserDialog.SelectedPath = RecordingRootDir; // Default selected path
                folderBrowserDialog.ShowNewFolderButton = false;

                if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    Console.WriteLine("User canceled folder selection.");
                }
                else
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;

                    if (Directory.Exists(selectedPath))
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(selectedPath);

                        // Get all subfolders
                        DirectoryInfo[] subDirectories = directoryInfo.GetDirectories();

                        if (subDirectories.Length == 0)
                        {
                            System.Windows.MessageBox.Show("No subfolders found.");
                            return;
                        }

                        Console.WriteLine("Processing subfolders...");
                        int totalSubDirs = subDirectories.Length;
                        int processedCount = 0;

                        foreach (var subDirectory in subDirectories)
                        {
                            Console.WriteLine($"Processing subfolder: {subDirectory.FullName}");
                            UpdateTextBlock(ModeStatus, "Loading: " + subDirectory.Name);
                            _FilenameDateTime = subDirectory.Name;

                            // Search for files matching the pattern
                            FileInfo[] files = subDirectory.GetFiles("*_T.xml")
                                                            .Union(subDirectory.GetFiles("*_B1.xml"))
                                                            .Union(subDirectory.GetFiles("*_B2.xml"))
                                                            .Union(subDirectory.GetFiles("*_B3.xml"))
                                                            .Union(subDirectory.GetFiles("*_L.xml"))
                                                            .Union(subDirectory.GetFiles("*_R.xml"))
                                                            .Union(subDirectory.GetFiles("*_F.xml"))
                                                            .Union(subDirectory.GetFiles("*_B.xml"))
                                                            .ToArray();

                            if (files.Length > 0)
                            {
                                Console.WriteLine($"Found {files.Length} files in subfolder {subDirectory.Name}...");
                                ProcessFiles(files, _FilenameDateTime);
                                isSaveFrames = false;
                            }
                            else
                            {
                                Console.WriteLine($"No matching files found in subfolder {subDirectory.Name}.");
                            }

                            // Update progress bar
                            processedCount++;
                            double progress = (double)processedCount / totalSubDirs * 100;
                            UpdateProgressBar(progress, processedCount, totalSubDirs);

                            // Wait 10 seconds
                            Console.WriteLine("Waiting 10 seconds...");
                            await Task.Delay(6000);
                        }

                        Console.WriteLine("All subfolders processed.");
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("The selected path does not exist!");
                    }
                }
            }
            ProgressBar.Visibility = Visibility.Hidden;
        }

        private void UpdateProgressBar(double progress, int current, int total)
        {
            Dispatcher.Invoke(() =>
            {
                // Update progress bar value
                ProgressBar.Value = progress;

                // Access ProgressText inside the ProgressBar.Template
                var progressText = (TextBlock)ProgressBar.Template.FindName("ProgressText", ProgressBar);
                if (progressText != null)
                {
                    progressText.Text = $"{progress:F1}% ({current} of {total})";
                }
            });
        }

        private void ProcessFiles(FileInfo[] files, string folderName)
        {
            Pallet.folderName = folderName;
            foreach (var file in files)
            {
                try
                {
                    string fileName = file.Name.ToUpper(); // Get the filename and convert to uppercase

                    if (fileName.Contains("_T.XML"))
                    {
                        ProcessFrameForCameraTopCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded Top file: " + file.FullName);
                    }
                    else if (fileName.Contains("_B1.XML"))
                    {
                        ProcessFrameForCameraBottomLeftCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded BottomLeft file: " + file.FullName);
                    }
                    else if (fileName.Contains("_B2.XML"))
                    {
                        ProcessFrameForCameraBottomMidCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded BottomMid file: " + file.FullName);
                    }
                    else if (fileName.Contains("_B3.XML"))
                    {
                        ProcessFrameForCameraBottomRightCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded BottomRight file: " + file.FullName);
                    }
                    else if (fileName.Contains("_L.XML"))
                    {
                        ProcessFrameForCameraLeftCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded Left file: " + file.FullName);
                    }
                    else if (fileName.Contains("_R.XML"))
                    {
                        ProcessFrameForCameraRightCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded Right file: " + file.FullName);
                    }
                    else if (fileName.Contains("_F.XML"))
                    {
                        ProcessFrameForCameraFrontCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded Front file: " + file.FullName);
                    }
                    else if (fileName.Contains("_B.XML"))
                    {
                        ProcessFrameForCameraBackCallback(GrabResult.CreateWithFrame(IFrame.Load(file.FullName)));
                        Logger.WriteLine("Loaded Back file: " + file.FullName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Processing of document {file.FullName} error: {ex.Message}");
                }
            }
        }

        private void SaveDataToFile(string totalResult)
        {
            string strWrite = string.Empty;

            try
            {
                string szTime = DateTime.Now.ToString("yyyyMMdd");
                string path = RecordingRootDir + "\\" + szTime + ".csv";
                string[] reportHeader = Enum.GetNames(typeof(DefectReport))
                            .Select(PalletDefect.CodeToName)
                            .ToArray();
                reportHeader[0] = "Name";
                reportHeader[1] = "Result";

                //string strNewHeader = null;
                //for (int i = 0; i < csvReportHeader.Length; i++) { strNewHeader += csvReportHeader[i] + ","; }

                new System.Threading.Thread(() =>
                {
                    JackSaveLog.DataLog(path, String.Join(",", reportHeader), totalResult);
                })
                { IsBackground = true }.Start();
            }

            catch (Exception ex)
            {
                Logger.WriteLine("Saving Result:" + ex.Message);
            }
        }


        // Jack Note: New added method to read the CSV file and display the results.
        public static void ShowCsvData(string filePath)
        {
            // Check if the file exists
            if (!File.Exists(filePath))
            {
                MessageBox.Show("CSV file does not exist!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create a window and set it to be centered on the screen
            Window window = new Window
            {
                Title = "CSV Data Viewer",
                Width = 1200, // Adjust window width to accommodate more columns
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen // Set to center display
            };

            // Create a DataGrid
            DataGrid dataGrid = new DataGrid
            {
                AutoGenerateColumns = true,  // Automatically generate columns
                //HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Read CSV data and bind it to the DataGrid
            DataTable dataTable = ReadCsvToDataTable(filePath);

            // Remove invalid empty columns
            var columnsToRemove = dataTable.Columns.Cast<DataColumn>()
                .Where(c => c.ColumnName.Contains("Unnamed")).ToList();
            foreach (var column in columnsToRemove)
            {
                dataTable.Columns.Remove(column);
            }

            dataGrid.ItemsSource = dataTable.DefaultView;

            // Add the DataGrid to the window content
            window.Content = dataGrid;

            // Show the window
            window.ShowDialog();
        }

        // Read CSV file and convert it to DataTable
        private static DataTable ReadCsvToDataTable(string filePath)
        {
            DataTable dt = new DataTable();
            using (StreamReader sr = new StreamReader(filePath))
            {
                // Read the first line as header
                string[] headers = sr.ReadLine().Split(',');
                foreach (string header in headers)
                {
                    dt.Columns.Add(header.Trim()); // Add column names and trim extra spaces
                }

                // Read each subsequent line of data
                while (!sr.EndOfStream)
                {
                    string[] rows = sr.ReadLine().Split(',');

                    // If the number of columns is inconsistent, add empty columns
                    while (rows.Length < dt.Columns.Count)
                    {
                        Array.Resize(ref rows, rows.Length + 1); // Increase column count
                        rows[rows.Length - 1] = string.Empty;    // Fill empty column with empty string
                    }

                    dt.Rows.Add(rows);
                }
            }
            return dt;
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
                    ParamConfig PC = new ParamConfig(ParamStorageGeneral, "LastUsedParamFileGeneral.txt", null);
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
            if ((AllFilesInFolder != null) && (AllFilesInFolder.Length > 0))
            {

                if (e.Key == Key.Left)
                {
                    if (LastFileIdx < AllFilesInFolder.Length - 1)
                    {
                        LastFileIdx++;
                        LoadAndProcessCaptureFile(PositionOfPallet.Top, AllFilesInFolder[LastFileIdx], false);
                    }
                }
                if (e.Key == Key.Right)
                {
                    if (LastFileIdx > 0)
                    {
                        LastFileIdx--;
                        LoadAndProcessCaptureFile(PositionOfPallet.Top, AllFilesInFolder[LastFileIdx], false);
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
            Pallet.Shutdown = true;
            PLC?.Stop();
            PLCShort?.Stop();
            Storage.Stop();
            Logger.WriteBorder("PalletCheck SHUTDOWN");
            Log.Shutdown();
            try
            {
                foreach (var cam in Cameras)
                {
                    cam.Shutdown(); // Stop each camera
                }
            }
            catch (Exception exp)
            {
                Logger.WriteException(exp);
            }
            Environment.Exit(0); // Force terminate all threads
        }

        private void Load_Top_Click(object sender, RoutedEventArgs e)
        {
            SetLoadFlags(0);
            LoadAndProcessCapture6FileFrame();
            //Logger.ButtonPressed(btnLoad_Top.Content.ToString());

            //LoadAndProcessCaptureFileFrame(PositionOfPallet.Top, PalletName0);
        }
        private void Load_Bottom_Click(object sender, RoutedEventArgs e)
        {

            SetLoadFlags(1,2,3);
            LoadAndProcessCapture6FileFrame();
        }
        private void Load_Left_Click(object sender, RoutedEventArgs e)
        {
            SetLoadFlags(4);
            LoadAndProcessCapture6FileFrame();

        }
        private void Load_Right_Click(object sender, RoutedEventArgs e)
        {
            SetLoadFlags(5);
            LoadAndProcessCapture6FileFrame();

        }
        private void Load_Front_Click(object sender, RoutedEventArgs e)
        {
            SetLoadFlags(6);
            LoadAndProcessCapture6FileFrame();

        }
        private void Load_Back_Click(object sender, RoutedEventArgs e)
        {
            SetLoadFlags(7);
            LoadAndProcessCapture6FileFrame();

        }
        private void btnSettingEach_Click(object sender, RoutedEventArgs e)
        {
            Logger.ButtonPressed(btnSettingsControl.Content.ToString());

            if (Password.DlgOpen)
                return;

            // Use traditional type casting
            Button clickedButton = sender as Button;
            if (clickedButton == null)
            {
                MessageBox.Show("The sender is not a Button.");
                return;
            }

            // Select the corresponding ParamStorage based on the button name.
            ParamStorage selectedParamStorage = null;
            string LastUsedParamName = null;
            string position = null;

            switch (clickedButton.Name)
            {
                case "btnSetting_Top":
                    selectedParamStorage = PalletClassifier == 0 ? ParamStorageTopInternational : ParamStorageTop;
                    position = "Top";
                    break;

                case "btnSetting_Bottom":
                    selectedParamStorage = ParamStorageBottom;
                    position = "Bottom";
                    break;

                case "btnSetting_Left":
                    selectedParamStorage = ParamStorageLeft;
                    position = "Left";
                    break;

                case "btnSetting_Right":
                    selectedParamStorage = ParamStorageRight;
                    position = "Right";
                    break;

                case "btnSetting_Front":
                    position = "Front";
                    selectedParamStorage = ParamStorageFront;
                    LastUsedParamName = "";
                    break;

                case "btnSetting_Back":
                    position = "Back";
                    selectedParamStorage = ParamStorageBack;
                    LastUsedParamName = "";
                    break;

                default:
                    MessageBox.Show("Unknown button, unable to select ParamStorage.");
                    return;
            }

            Password PB = new Password();
            PB.Closed += (sender2, e2) =>
            {
                if (Password.Passed && selectedParamStorage != null)
                {
                    ParamConfig PC = new ParamConfig(selectedParamStorage, LastUsedParamName, position);
                    PC.Text = clickedButton.Name;
                    PC.Show();
                }
            };
            PB.Show();
        }

        private readonly ConcurrentDictionary<int, InspectionResult> _results = new ConcurrentDictionary<int, InspectionResult>();

        private int _completedCount = 0; // Counter for completed cameras
        private readonly object _lockResult = new object();
        private readonly int _totalCameras = ParamStorageGeneral.GetInt("CameraCount");
        

        public void ProcessCameraResult(int cameraId, InspectionResult result)
        {
            int cameras = ParamStorageGeneral.GetInt(StringsLocalization.CameraCount);
            int _totalViews = cameras - 2; // Minus 2 because Bottom View uses 3 cameras
            // Store the result of the current camera
            lock (_lock)
            {
                _results[cameraId] = result; // Store the camera result
                _completedCount++; // Increment the counter
            }
            
            // Check if all cameras are completed
            if (_completedCount == _totalViews)
            {
                // Invoke final logic
                FinalizeResults();
                if (isSaveFrames)
                {
                    SaveAllFrames();
                }
                else
                {
                    isSaveFrames = true;
                }
            }
        }

        private void FinalizeResults()
        {
            var finalResult = CalculateOverallResult(_results.Values);

            if (BypassModeEnabled)
            {
                SendPLCTotalResult("BypassModeEnabled");
            }

            else
            {
                SendPLCTotalResult(finalResult.ToString());
            }

            // Update screen
            Console.WriteLine($"Total Results: {finalResult}");
            stopwatchProcess.Stop();
            //UpdateTextBlock(LogText, $"Process time: {stopwatchProcess.Elapsed.TotalSeconds:F2} seconds", Colors.White, 30);


            Dispatcher.Invoke(() =>
            {
                if (PalletClassifier == 0) PalletType.Text = "Pallet Class: International";
                if (PalletClassifier == 1) PalletType.Text = "Pallet Class: Standard";
            });
          //  if (PalletClassifier == 0) PalletType.Text = "Pallet Class: International";
          //  if (PalletClassifier == 1) PalletType.Text = "Pallet Class: Standard";

            // Save Results To CSV File
            Application.Current.Dispatcher.Invoke(() =>
            {
                defectTable.Items.Clear();
                UpdateTextBlock(
                    TotalResultTextBlock,
                    finalResult.ToString(),
                    finalResult == InspectionResult.PASS ? Colors.Green :
                    finalResult == InspectionResult.FAIL ? Colors.Red :
                    finalResult == InspectionResult.ERROR ? Colors.Orange :
                    Colors.Gray,
                    80);

                UpdateProductivityStats(finalResult == InspectionResult.PASS);
                defectTable.Items.Clear();

                // Prepare string[] to save results in CSV
                string[] reportHeader = Enum.GetNames(typeof(DefectReport));
                string[] finalString = new string[Enum.GetNames(typeof(DefectReport)).Length];

                finalString[0] = Pallet.folderName;
                finalString[1] = finalResult.ToString();

                foreach (var defect in Pallet.CombinedDefects ?? new List<PalletDefect>())
                {        
                    defectTable.Items.Add(defect);

                    if (Enum.IsDefined(typeof(DefectReport), defect.Code))
                    {
                        int columIndex = (int)Enum.Parse(typeof(DefectReport), defect.Code); //Gets the correct report column to write matching the defect code and the DefectReport Enum
                        finalString[columIndex] += string.Format("{0}: {1}; ", defect.Location, defect.Comment);
                    }
                }
                
                SaveDataToFile(string.Join(",", finalString));
                
            });
            lock (LockObjectCombine)
            {
                CombinedBoards.Clear();
                CombinedDefects.Clear();
            }
            // Reset status
            Reset();
        }

        private InspectionResult CalculateOverallResult(IEnumerable<InspectionResult> results)
        {
            if (results.Contains(InspectionResult.FAIL))
                return InspectionResult.FAIL;
            if (results.Contains(InspectionResult.TIMEOUT))
                return InspectionResult.TIMEOUT;
            return InspectionResult.PASS;
        }

        private void Reset()
        {
            lock (_lockResult)
            {
                _results.Clear();
                _completedCount = 0;
            }
          
        }

        private void btnShowDl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isDeepLActive = !isDeepLActive; // Cambia el estado al presionar
                btnShow_DL.Content = isDeepLActive ? "DeepL ✓" : "DeepL"; // Cambia el texto del botón
                Console.WriteLine($"DeepL Activo: {isDeepLActive}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        private void btnShow3D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Determine the environment based on the button source.
                ProcessingEnvironment environment = null;
                string title = "";

                if (sender == btnShow_Top)
                {
                    environment = _envTop;
                    title = "Top View";
                }
                else if (sender == btnShow_Bottom)
                {
                    environment = _envBottom;
                    title = "Bottom View";
                }
                else if (sender == btnShow_Left)
                {
                    environment = _envLeft;
                    title = "Left View";
                }
                else if (sender == btnShow_Right)
                {
                    environment = _envRight;
                    title = "Right View";
                }

                else if (sender == btnShow_Front)
                {
                    environment = _envFront;
                    title = "Front View";
                }

                else if (sender == btnShow_Back) 
                { 
                    environment = _envBack;
                    title = "Back View";
                }
   
                if (environment == null)
                {
                    Console.WriteLine("No environment found for this button.");
                    return;
                }

                // 打开 Viewer3D
                Viewer3D viewer3D = new Viewer3D(environment)
                {
                    Title = title // Set the window title.
                };

                viewer3D.SICKViewer3D.Draw("FilteredImage"); // Draw the image.
                viewer3D.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                viewer3D.ShowDialog(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
    }
}
