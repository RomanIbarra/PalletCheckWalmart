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
using Sick.GenIStream;
using IFrame = Sick.GenIStream.IFrame;
using Frame = Sick.GenIStream.Frame;
using System.Threading;
using System.Collections.Concurrent;
using System.Data;
using System.Runtime.CompilerServices;
using static ScottPlot.Plottable.PopulationPlot;
using Sick.EasyRanger;
using Sick.EasyRanger.Controls;
using Sick.EasyRanger.Base;
using Sick.EasyRanger.Controls.Common;
using Sick.StreamUI.ImageFormat;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Runtime.InteropServices.ComTypes;
using static Sick.GenIStream.FrameGrabber;
using static System.Windows.Forms.AxHost;
using MahApps.Metro.Controls;
namespace PalletCheck
{
    public partial class MainWindow : Window
    {
        //public static int SensorWidth = 2560;
        public List<R3Cam> Cameras = new List<R3Cam>();  // Dynamic list of cameras

        int cameraCount = 0;
        private CameraDiscovery _discovery;

        ConcurrentDictionary<string, R3Cam.Frame> FrameBuffer = new ConcurrentDictionary<string, R3Cam.Frame>();

        IFrame[] SickFrames = new IFrame[6];

        private int framesReceivedCount = 0;  // Used to track the number of frames received
        private object _lock = new object();  // Lock for thread safety
        public event Action OnAllFramesReceived;
        private string cameraStatus;

        // Define variables to store StartX, EndX, and OffsetZ parameters for each camera
        int[] StartX;
        int[] EndX;
        float[] OffsetZ;

        private async void InitializeCamerasAndParameters()
        {
            StartX = new int[3];
            EndX = new int[3];
            OffsetZ = new float[3];
            for (int i = 0; i < 3; i++)
            {
                StartX[i] = ParamStorageBottom.GetInt($"StartX{i}");
                EndX[i] = ParamStorageBottom.GetInt($"EndX{i}");
                OffsetZ[i] = ParamStorageBottom.GetFloat($"OffsetZ{i}");

                // Output the parameters for each camera
                Logger.WriteLine($"Camera {i}: StartX = {StartX[i]}, EndX = {EndX[i]}, OffsetZ = {OffsetZ[i]}");
            }

            for (int i = 0; i < SickFrames.Length; i++)
            {
                SickFrames[i] = Frame.Create();
            }

            // EasyRanger
            _envLeft = new ProcessingEnvironment();
            _envTop = new ProcessingEnvironment();
            _envBottom = new ProcessingEnvironment();
            _envRight = new ProcessingEnvironment();
            _envLeft.Load("SICK ENV\\Sides.env");
            _envRight.Load("SICK ENV\\Sides.env");
            _envTop.Load("SICK ENV\\Top.env");
            _envBottom.Load("SICK ENV\\Bottom.env");

            // Define a fixed callback array (placed outside the loop)
            GrabResultCallback[] callbacks =
            {
        ProcessFrameForCameraTopCallback,
        ProcessFrameForCameraBottomLeftCallback,
        ProcessFrameForCameraBottomMidCallback,
        ProcessFrameForCameraBottomRightCallback,
        ProcessFrameForCameraLeftCallback,
        ProcessFrameForCameraRightCallback
    };

            // Initialize cameras in a loop
            for (int i = 0; i < callbacks.Length; i++)
            {
                string cameraName = $"Camera{i}";
                string cameraIPKey = $"Camera{i}_IP";
                string cameraIP = ParamStorageGeneral.GetString(cameraIPKey);

                // Ensure the index is valid
                if (i < 0 || i >= callbacks.Length)
                {
                    throw new InvalidOperationException("Unexpected camera index");
                }

                // Initialize the camera
                InitializeCamera(cameraName, cameraIPKey, callbacks[i], OnConnectionStateChange, OnCaptureStateChange);
            }

            // Subscribe to the event to perform stitching when all frames are received
            OnAllFramesReceived += () =>
            {
                Logger.WriteLine("All frames received, starting stitch...");

                // StitchFrames(ParamStorageBottom);  // Perform frame stitching

                Pallet P = CreateBottomPallet();
                PProcessorBottom.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete, PositionOfPallet.Bottom);
            };

            if (ParamStorageGeneral.GetInt("Auto Start Capturing") == 1)
            {
                await Task.Delay(2000); // Wait for 2 seconds
                btnStart_Click(null, null);
            }
        }

        private void InitializeCamera(
                    string cameraName,
                    string cameraIPKey,
                    GrabResultCallback onNext, // Specify a unique callback for each camera
                    R3Cam.ConnectionStateChangeCB onConnectionStateChange,
                    R3Cam.CaptureStateChangeCB onCaptureStateChange)
        {
            try
            {
                // Get the camera's IP address
                string cameraIP = ParamStorageGeneral.GetString(cameraIPKey);

                //if (string.IsNullOrEmpty(cameraIP))
                //{
                //    Logger.WriteLine($"Error: Camera IP for {cameraName} is null or empty. Key: {cameraIPKey}");
                //    return;
                //}

                // Create and initialize the camera instance
                R3Cam camera = new R3Cam();
                camera.StartupUpdate(
                    cameraName,
                    cameraIP,
                    _discovery,
                    onNext,
                    onConnectionStateChange,
                    onCaptureStateChange
                );

                // Add the camera to the list
                Cameras.Add(camera);
                Logger.WriteLine($"Camera {cameraName} initialized and added to list.");
                UpdateTextBlock(LogText, $"Camera {cameraName} initialized and added to list.", MessageState.Normal);

                // Ensure initialization is complete
                Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error initializing camera {cameraName}: {ex.Message}");
                UpdateTextBlock(LogText, $"Error initializing camera {cameraName}: {ex.Message}", MessageState.Critical);
            }
        }


        private void OnConnectionStateChange(R3Cam Cam, R3Cam.ConnectionState NewState)
        {
            // !!! THIS IS CALLED FROM THE R3CAM THREAD !!!!
            Logger.WriteLine(Cam.CameraName + "  ConnectionState: " + NewState.ToString());
            StatusStorage.Set("Camera." + Cam.CameraName + ".ConnState", NewState.ToString());
        }

        private void OnCaptureStateChange(R3Cam Cam, R3Cam.CaptureState NewState)
        {
            // !!! THIS IS CALLED FROM THE R3CAM THREAD !!!!
            Logger.WriteLine(Cam.CameraName + "  CaptureState: " + NewState.ToString());
            StatusStorage.Set("Camera." + Cam.CameraName + ".CaptureState", NewState.ToString());
        }

    }
}
