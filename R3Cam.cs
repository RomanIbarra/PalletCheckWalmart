using Sick.GenIStream;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.IO;
using static Sick.GenIStream.FrameGrabber;
using static Sick.GenIStream.ICamera;
using System.Net;

//using System.Threading.Tasks;


//public void ExportParameters(string filePath)
//{
//    genistreamPINVOKE.ICamera_ExportParameters(swigCPtr, filePath);
//    if (genistreamPINVOKE.SWIGPendingException.Pending)
//    {
//        throw genistreamPINVOKE.SWIGPendingException.Retrieve();
//    }
//}

//public ConfigurationResult ImportParameters(string filePath)
//{
//    ConfigurationResult result = new ConfigurationResult(genistreamPINVOKE.ICamera_ImportParameters(swigCPtr, filePath), cMemoryOwn: true);
//    if (genistreamPINVOKE.SWIGPendingException.Pending)
//    {
//        throw genistreamPINVOKE.SWIGPendingException.Retrieve();
//    }

//    return result;
//}


namespace PalletCheck
{
    public class R3Cam
    {
        ParamStorage ParamStorageGeneral = MainWindow.ParamStorageGeneral;
        public enum ConnectionState
        {
            Shutdown,
            Searching,
            Connected,
            BadParameters,
            Disconnected,
        };

        public enum CaptureState
        {
            Stopped,
            Capturing,
        };

        public class Frame
        {
            public DateTime Time { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public ulong FrameID { get; set; }
            public UInt16[] Range { get; set; }
            public byte[] Reflectance { get; set; }
            public R3Cam Camera { get; set; }
            // Jack Added zScale and zOffset
            public double xScale { get; set; }
            public double xOffset { get; set; }
            public double yScale { get; set; }
            public double yOffset { get; set; }
            public double zScale { get; set; }
            public double zOffset { get; set; }

            public override string ToString()
            {
                return $"R3Cam.Frame: cam:{Camera.CameraName} w:{Width} h:{Height} range:{Range != null} reflectance:{Reflectance != null} scatter:false";
            }
        }


        public ICamera Camera { get; private set; }
        public string CameraName { get; private set; }
        public string IPAddressStr { get; private set; }
        public ConnectionState CameraConnectionState { get; private set; }
        public CaptureState CameraCaptureState { get; private set; }
        public Frame LastFrame { get; private set; }

        private FrameGrabber frameGrabber;
        private Thread CamThread;
        private bool StopCamThread = false;
        private bool DoCaptureFrames;
        private bool InSimMode;
        private FileInfo[] SimModeFileList;
        private int SimModeFileIndex;
        private DateTime SimModeNextDT;



        public delegate void NewFrameReceivedCB(R3Cam Cam, R3Cam.Frame Frame);
        public delegate void ConnectionStateChangeCB(R3Cam Cam, ConnectionState NewState);
        public delegate void CaptureStateChangeCB(R3Cam Cam, CaptureState NewState);

        private NewFrameReceivedCB OnFrameReceived;
        private ConnectionStateChangeCB OnConnectionStateChange;
        private CaptureStateChangeCB OnCaptureStateChange;

        /// <summary>
        /// Jack New for API
        /// </summary>
        GrabResultCallback OnNext;
        DisconnectCallback OnDisconnect;

        public void StartupNew(
                             string name,
                             string ipAddress,
                             CameraDiscovery discovery,
                             GrabResultCallback grabResultCallback,
                             DisconnectCallback disconnectCallback,
                             ConnectionStateChangeCB connectionStateChangeCallback,
                             CaptureStateChangeCB captureStateChangeCallback)
        {
            CameraName = name;
            IPAddressStr = ipAddress;
            OnNext = grabResultCallback;
            OnDisconnect = disconnectCallback;
            OnConnectionStateChange = connectionStateChangeCallback;
            OnCaptureStateChange = captureStateChangeCallback;

            // 启动连接线程
            Thread connectionThread = new Thread(() => ConnectToCamera(discovery));
            connectionThread.IsBackground = true;
            connectionThread.Start();
        }

        public void ConnectToCamera(CameraDiscovery discovery)
        {
            if (discovery == null)
            {
                Logger.WriteLine("Camera discovery instance is null. Aborting connection attempt.");
                throw new ArgumentNullException(nameof(discovery), "Camera discovery instance cannot be null.");
            }

            while (true)
            {
                try
                {
                    Logger.WriteLine($"Attempting to connect to camera {CameraName} at {IPAddressStr}...");
                    SetConnectionState(ConnectionState.Searching);
                    SetCaptureState(CaptureState.Stopped);



                    // 尝试连接相机
                    Camera = discovery.ConnectTo(System.Net.IPAddress.Parse(IPAddressStr));

                    // 验证相机连接状态
                    if (Camera == null || !Camera.IsConnected)
                    {
                        Logger.WriteLine($"Failed to connect to camera {CameraName} at {IPAddressStr}. Retrying in 5 seconds...");
                        SetConnectionState(ConnectionState.Disconnected);
                        Thread.Sleep(5000); // 等待 5 秒后重试
                        continue;
                    }

                    // 注册断开连接回调
                    Camera.Disconnected += OnDisconnect;

                    SetConnectionState(ConnectionState.Connected);
                    Logger.WriteLine($"Camera {CameraName} connected successfully.");

                    // 配置 FrameGrabber
                    frameGrabber = Camera.CreateFrameGrabber();
                    if (frameGrabber == null)
                    {
                        Logger.WriteLine("Failed to create FrameGrabber. Disconnecting camera and retrying.");
                        Camera.Disconnect();
                        SetConnectionState(ConnectionState.Disconnected);
                        Thread.Sleep(5000);
                        continue;
                    }

                    frameGrabber.Next += OnNext;

                    // 启动 FrameGrabber
                    frameGrabber.Start();

                    // 验证 FrameGrabber 状态
                    if (!frameGrabber.IsStarted)
                    {
                        Logger.WriteLine($"Failed to start capturing for camera {CameraName}. Retrying...");
                        SetCaptureState(CaptureState.Stopped);
                        Thread.Sleep(5000);
                        continue;
                    }

                    CameraCaptureState = CaptureState.Capturing;
                    OnCaptureStateChange?.Invoke(this, CaptureState.Capturing);
                    Logger.WriteLine($"Camera {CameraName} started capturing successfully.");
                    break; // 连接成功，退出循环
                }

                catch (Exception ex)
                {
                    Logger.WriteLine($"Error while connecting to camera {CameraName}: {ex.Message}");
                    OnConnectionStateChange?.Invoke(this, ConnectionState.Disconnected);
                    Thread.Sleep(5000); // 等待 5 秒后重试
                }
            }
        }





        public void Startup(string Name, string IPAddress, CameraDiscovery discovery,
                            NewFrameReceivedCB NewFrameReceivedCallback,
                            ConnectionStateChangeCB ConnectionStateChangeCallback,
                            CaptureStateChangeCB CaptureStateChangeCallback)
        {
            Camera = null;
            CameraName = Name;
            IPAddressStr = IPAddress;
            OnFrameReceived += NewFrameReceivedCallback;
            OnConnectionStateChange += ConnectionStateChangeCallback;
            OnCaptureStateChange += CaptureStateChangeCallback;

            DoCaptureFrames = false;

            StopCamThread = false;

            CamThread = new Thread(() => CameraProcessingThread(discovery));
            CamThread.Start();
        }

        public void StartupUpdate(string Name, string IPAddress, CameraDiscovery discovery,
                           GrabResultCallback NewFrameReceivedCallback,
                           ConnectionStateChangeCB ConnectionStateChangeCallback,
                           CaptureStateChangeCB CaptureStateChangeCallback)
        {
            Camera = null;
            CameraName = Name;
            IPAddressStr = IPAddress;
            OnNext += NewFrameReceivedCallback;
            OnConnectionStateChange += ConnectionStateChangeCallback;
            OnCaptureStateChange += CaptureStateChangeCallback;

            DoCaptureFrames = true;

            StopCamThread = false;

            CamThread = new Thread(() => CameraProcessingThread(discovery));
            CamThread.Start();
        }

        //private void ReconnectCamera(string cameraName)
        //{
        //    try
        //    {
        //        // 假设 discovery 已全局可用
        //        Camera = _discovery.Reconnect(Camera);
        //        if (Camera != null && Camera.IsConnected)
        //        {
        //            Console.WriteLine($"Camera {cameraName} reconnected successfully.");
        //            frameGrabber = Camera.CreateFrameGrabber();
        //            frameGrabber.Next += result => OnNewFrame(cameraName, result);
        //            frameGrabber.Start();
        //        }
        //        else
        //        {
        //            Console.WriteLine($"Failed to reconnect camera: {cameraName}. Retrying...");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error during reconnecting camera {cameraName}: {ex.Message}");
        //    }
        //}
        public void StartCapturingFrames()
        {
            if (frameGrabber == null)
            {
                Logger.WriteLine($"[{CameraName}] Cannot start capturing frames: FrameGrabber is null.");
                return;
            }

            if (frameGrabber.IsStarted)
            {
                Logger.WriteLine($"[{CameraName}] FrameGrabber is already started. Skipping start operation.");
                return;
            }

            try
            {
                Logger.WriteLine($"[{CameraName}] Attempting to start FrameGrabber...");
                frameGrabber.Start();
                DoCaptureFrames = true;

                if (frameGrabber.IsStarted)
                {
                    Logger.WriteLine($"[{CameraName}] FrameGrabber started successfully. Capturing frames...");
                }
                else
                {
                    Logger.WriteLine($"[{CameraName}] FrameGrabber failed to start for unknown reasons.");
                    DoCaptureFrames = false; // Ensure capturing flag is consistent with the actual state.
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[{CameraName}] Error while starting FrameGrabber: {ex.Message}");
                DoCaptureFrames = false; // Prevent capturing if an exception occurred.
            }
        }


        public void StopCapturingFrames()
        {

            if (frameGrabber == null)
            {
                Logger.WriteLine($"[{CameraName}] Cannot stop capturing frames: FrameGrabber is null.");
                return;
            }

            if (!frameGrabber.IsStarted)
            {
                Logger.WriteLine($"[{CameraName}] FrameGrabber is not started. Skipping stop operation.");
                DoCaptureFrames = false; // Ensure capturing flag is consistent with the actual state.
                return;
            }

            try
            {
                Logger.WriteLine($"[{CameraName}] Stopping FrameGrabber...");
                frameGrabber.Stop();
                DoCaptureFrames = false;

                if (!frameGrabber.IsStarted)
                {
                    Logger.WriteLine($"[{CameraName}] FrameGrabber stopped successfully.");
                }
                else
                {
                    Logger.WriteLine($"[{CameraName}] FrameGrabber failed to stop for unknown reasons.");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[{CameraName}] Error while stopping FrameGrabber: {ex.Message}");
            }
        }



        public void Shutdown()
        {
            StopCamThread = true;
            Thread.Sleep(100);
            try
            {
                if (Camera != null)
                {
                    if (Camera.IsConnected) Camera.Disconnect();
                    Camera.Dispose();
                    Camera = null;
                }
            }
            catch (Exception exp)
            {
                Logger.WriteException(exp);
            }
        }

        private void SetConnectionState(ConnectionState newState, bool forceCallback = false)
        {
            if ((newState != CameraConnectionState) || forceCallback)
            {
                CameraConnectionState = newState;
                OnConnectionStateChange?.Invoke(this, CameraConnectionState);
            }
        }

        private void SetCaptureState(CaptureState newState, bool forceCallback = false)
        {
            if ((newState != CameraCaptureState) || forceCallback)
            {
                CameraCaptureState = newState;
                OnCaptureStateChange?.Invoke(this, CameraCaptureState);
            }
        }

        private bool ProcessRealCamera(CameraDiscovery discovery)
        {
            if (ParamStorageGeneral.GetInt(StringsLocalization.CameraEnabled) == 0)
            {
                Thread.Sleep(100);
                return true;
            }

            if (Camera == null)
            {
                Logger.WriteLine("Searching for camera " + CameraName + " at " + IPAddressStr);
                SetConnectionState(ConnectionState.Searching);
                SetCaptureState(CaptureState.Capturing);

                try
                {
                    Camera = discovery.ConnectTo(System.Net.IPAddress.Parse(IPAddressStr));
                }
                catch (Exception) { }

                if (Camera == null)
                {
                    Logger.WriteLine("Cannot find camera " + CameraName + " at " + IPAddressStr);
                    Thread.Sleep(1000);
                    return false;
                }

                Logger.WriteLine("Camera Connected " + CameraName + " at " + IPAddressStr + "  " + Camera.IsConnected);
                Logger.WriteLine("Setting Camera Parameters " + CameraName + " at " + IPAddressStr);
                if (SetupParameters_Scan(Camera) == false)
                {
                    Logger.WriteLine("Camera Parameter Setup Failed " + CameraName + " at " + IPAddressStr);
                    SetConnectionState(ConnectionState.BadParameters);
                    frameGrabber = null;
                }
                else
                {
                    Camera
                    .GetCameraParameters()
                    .DeviceScanType.Set(DeviceScanType.LINESCAN_3D);
                    Camera.GetCameraParameters().Scan3dExtraction(Scan3dExtractionId.SCAN_3D_EXTRACTION_1).Scan3dOutputMode.Set(Scan3dOutputMode.RECTIFIED_C);
                    SetConnectionState(ConnectionState.Connected);
                    SetCaptureState(CaptureState.Stopped);
                    Logger.WriteLine("Camera Parameter Setup Succeeded " + CameraName + " at " + IPAddressStr);
                    frameGrabber = Camera.CreateFrameGrabber();
                }
            }

            if (Camera != null && !Camera.IsConnected)
            {
                Logger.WriteLine("Camera no longer connected " + CameraName + " at " + IPAddressStr + "  " + Camera.IsConnected);

                try
                {
                    Camera.Dispose();
                }
                catch (Exception e) { Logger.WriteException(e); }

                Camera = null;
                frameGrabber = null;
                LastFrame = null;
                SetConnectionState(ConnectionState.Disconnected);
                Thread.Sleep(1000);
                return false;
            }

            if (Camera != null && Camera.IsConnected && CameraConnectionState == ConnectionState.BadParameters)
            {
                Logger.WriteLine("Camera Parameter Setup Failed " + CameraName + " at " + IPAddressStr);
                Thread.Sleep(1000);
                return false;
            }

            if (frameGrabber != null)
            {
                if (DoCaptureFrames)
                {
                    if (!frameGrabber.IsStarted)
                    {

                        frameGrabber.Start();
                        frameGrabber.Next += OnNext;
                        SetCaptureState(CaptureState.Capturing);
                        Logger.WriteLine("Started FrameGrabber for " + CameraName + " at " + IPAddressStr);
                        Thread.Sleep(100);
                    }

                    if (frameGrabber.IsStarted)
                    {
                        SetCaptureState(CaptureState.Capturing);
                        //Logger.WriteLine("frameGrabber Start Capturing - " + CameraName);
                        Thread.Sleep(1000);
                        //try
                        //{
                        //    Logger.WriteLine("frameGrabber.Grab()...");
                        //    IFrameWaitResult res = frameGrabber.Grab(new TimeSpan(0, 0, 120));
                        //    if (res != null)
                        //    {
                        //        res.IfCompleted(OnNewFrame)
                        //            .IfAborted(OnAborted)
                        //            .IfTimedOut(OnTimedOut);
                        //    }
                        //}
                        //catch (Exception E)
                        //{
                        //    Logger.WriteException(E);
                        //}

                        return false;
                    }
                }

                if (!DoCaptureFrames && frameGrabber.IsStarted)
                {
                    frameGrabber.Stop();
                    SetCaptureState(CaptureState.Stopped);
                    Logger.WriteLine("Stopped FrameGrabber for " + CameraName + " at " + IPAddressStr);
                    Thread.Sleep(100);
                    return false;
                }
            }

            return true;
        }

        private void CameraProcessingThread(CameraDiscovery discovery)
        {
            SetConnectionState(ConnectionState.Shutdown, true);
            SetCaptureState(CaptureState.Stopped, true);
            Camera = null;

            while (true)
            {
                if (StopCamThread) break;

                if (!InSimMode)
                {
                    if (ParamStorageGeneral.GetInt("Camera Sim Enabled") == 1)
                    {
                        InSimMode = true;
                        Logger.WriteLine("Camera SIM MODE Enabled");
                        UpdateSimCamera(true, ParamStorageGeneral);
                    }
                    else
                    {
                        ProcessRealCamera(discovery);
                    }
                }
                else
                {
                    if (ParamStorageGeneral.GetInt("Camera Sim Enabled") == 0)
                    {
                        InSimMode = false;
                        Logger.WriteLine("Camera SIM MODE Disabled");
                        ProcessRealCamera(discovery);
                    }
                    else
                    {
                        UpdateSimCamera(false, ParamStorageGeneral);
                    }
                }
            }

            try
            {
                if (frameGrabber != null)
                {
                    if (frameGrabber.IsStarted) frameGrabber.Stop();
                    frameGrabber.Dispose();
                    frameGrabber = null;
                }
            }
            catch (Exception e) { Logger.WriteException(e); }

            try
            {
                if (Camera != null)
                {
                    if (Camera.IsConnected) Camera.Disconnect();
                    Camera?.Dispose();
                    Camera = null;
                }
            }
            catch (Exception e) { Logger.WriteException(e); }

            Camera = null;
            frameGrabber = null;
            LastFrame = null;
            SetConnectionState(ConnectionState.Shutdown);
        }

        private void UpdateSimCamera(bool restart, ParamStorage paramStorage)
        {
            if (restart)
            {
                Logger.WriteLine("UpdateSimCamera::RESTARTING");
                string RootDir = Environment.GetEnvironmentVariable("SICK_PALLETCHECK_ROOT_DIR");
                DirectoryInfo info = new DirectoryInfo(RootDir + '\\' + ParamStorageGeneral.GetString("Camera Sim Directory"));
                SimModeFileList = info.GetFiles("*.r3").OrderBy(p => p.CreationTime).ToArray();
                SimModeFileIndex = 0;
                SimModeNextDT = DateTime.Now;
            }

            if (DateTime.Now >= SimModeNextDT)
            {
                float DT = ParamStorageGeneral.GetFloat("Camera Sim Sec Per Pallet");
                SimModeNextDT = DateTime.Now + TimeSpan.FromSeconds(DT);

                if (SimModeFileList.Length > 0)
                {
                    FileInfo FI = SimModeFileList[SimModeFileIndex % SimModeFileList.Length];
                    Logger.WriteLine("UpdateSimCamera::New frame! " + SimModeFileIndex.ToString());
                    Logger.WriteLine(FI.FullName);

                    CaptureBuffer CB = new CaptureBuffer();
                    CB.Load(FI.FullName, paramStorage);

                    Frame F = new Frame
                    {
                        Time = DateTime.Now,
                        Width = CB.Width,
                        Height = CB.Height,
                        FrameID = (ulong)SimModeFileIndex,
                        Camera = this,
                        Range = CB.Buf,
                        Reflectance = null
                    };

                    SimModeFileIndex += 1;

                    if (OnFrameReceived != null)
                    {
                        Logger.WriteLine("--------------------------------------------");
                        Logger.WriteLine("UpdateSimCamera::Calling OnFrameReceived");
                        OnFrameReceived(this, F);
                        Logger.WriteLine("UpdateSimCamera::Calling OnFrameReceived...called");
                        Logger.WriteLine("--------------------------------------------");
                    }
                    else
                    {
                        Logger.WriteLine("UpdateSimCamera::Calling OnFrameReceived == NULL!");
                    }
                }
            }

            Thread.Sleep(100);
        }

        private static bool SetupParameters_Scan(ICamera camera)
        {
            return true;
        }

        private void OnNewFrame(IFrame frame)
        {
            Logger.WriteLine("NEW FRAME FROM CAMERA! " + frame.GetFrameId().ToString() + "  " + CameraName + " at " + IPAddressStr);

            Frame F = new Frame
            {
                Time = DateTime.Now,
                Width = 0,
                Height = 0,
                FrameID = frame.GetFrameId(),
                Camera = this,
                Range = null,
                Reflectance = null,


            };

            if (frame.GetRange() != null)
            {
                IComponent component = frame.GetRange();
                F.Width = (int)component.GetWidth();
                F.Height = (int)component.GetConfiguredHeight();
                IntPtr data = component.GetData();
                int DataSize = (int)component.GetConfiguredDataSize();
                int BPP = (int)component.GetBitsPerPixel();
                var count = (int)(DataSize / (int)Math.Ceiling(BPP / 8.0));
                Logger.WriteLine($"OnNewFrame  RANGE  W:{F.Width} H:{F.Height} DS:{DataSize} BPP:{BPP} count:{count}");
                F.Range = new UInt16[count];
                Copy(data, F.Range, count);
                F.zScale = frame.GetRange().GetCoordinateSystem().C.Transform.Scale;
                F.zOffset = frame.GetRange().GetCoordinateSystem().C.Transform.Offset;

            }
            if (frame.GetReflectance() != null)
            {
                IComponent component = frame.GetReflectance();
                F.Width = (int)component.GetWidth();
                F.Height = (int)component.GetConfiguredHeight();
                IntPtr data = component.GetData();
                int DataSize = (int)component.GetConfiguredDataSize();
                int BPP = (int)component.GetBitsPerPixel();
                var count = (int)(DataSize / (int)Math.Ceiling(BPP / 8.0));
                Logger.WriteLine($"OnNewFrame  REFLC  W:{F.Width} H:{F.Height} DS:{DataSize} BPP:{BPP} count:{count}");
                F.Reflectance = new byte[count];
                Copy(data, F.Reflectance, count);
            }

            if (F.Range != null)
            {
                bool ValidScan = true;
                int mny = 0, mxy = 0;

                if (ValidScan)
                {
                    try
                    {
                        OnFrameReceived?.Invoke(this, F);
                    }
                    catch (Exception exp)
                    {
                        Logger.WriteException(exp);
                    }
                }
                else
                {
                    Logger.WriteLine($"INVALID SCAN.  MNY,MXY = {mny},{mxy}");
                }
            }
            else
            {
                Logger.WriteLine("INVALID SCAN.  NO RANGE DATA");
            }
        }



        private void OnAborted()
        {
            Logger.WriteLine("FRAME GRABBER: OnAborted");
            frameGrabber = null;
        }

        private void OnTimedOut()
        {
            Logger.WriteLine("FRAME GRABBER: OnTimedOut");
        }

        public void SaveSettings(string FileName)
        {
            if (Camera != null && Camera.IsConnected)
            {
                try
                {
                    Camera.ExportParametersToFile(FileName);
                }
                catch (Exception)
                {
                    Logger.WriteLine("Failed to save camera settings.");
                }
            }
        }

        public bool LoadSettings(string FileName)
        {
            if (Camera != null && Camera.IsConnected)
            {
                ConfigurationResult Result = Camera.ImportParametersFromFile(FileName);
                if (Result.Status == ConfigurationStatus.OK)
                {
                    return true;
                }
                else
                {
                    Logger.WriteLine("CAMERA LOAD SETTINGS IMPORT BAD PARAMETERS: " + Result.Status.ToString());
                    return false;
                }
            }
            return true;
        }

        public static Frame ConvertToFrame(IFrame frame, string cameraName)
        {
            Logger.WriteLine($"Converting IFrame to Frame! Frame ID: {frame.GetFrameId()} from Camera: {cameraName}");

            // 创建 Frame 实例
            Frame convertedFrame = new Frame
            {
                Time = DateTime.Now,
                Width = 0,
                Height = 0,
                FrameID = frame.GetFrameId(),
                Camera = null, // 假设 'this' 指的是当前相机实例
                Range = null,
                Reflectance = null,
            };

            // 处理 Range 数据
            if (frame.GetRange() != null)
            {
                Sick.GenIStream.IComponent component = frame.GetRange();
                convertedFrame.Width = (int)component.GetWidth();
                convertedFrame.Height = (int)component.GetConfiguredHeight();
                IntPtr data = component.GetData();
                int dataSize = (int)component.GetConfiguredDataSize();
                int bitsPerPixel = (int)component.GetBitsPerPixel();
                int count = dataSize / (int)Math.Ceiling(bitsPerPixel / 8.0);

                Logger.WriteLine($"Processing RANGE Data: W={convertedFrame.Width}, H={convertedFrame.Height}, DS={dataSize}, BPP={bitsPerPixel}, Count={count}");

                convertedFrame.Range = new ushort[count];
                Copy(data, convertedFrame.Range, count);

                convertedFrame.xScale = component.GetCoordinateSystem().A.Transform.Scale;
                convertedFrame.xOffset = component.GetCoordinateSystem().A.Transform.Offset;
                convertedFrame.yScale = component.GetCoordinateSystem().B.Transform.Scale;
                convertedFrame.yOffset = component.GetCoordinateSystem().B.Transform.Offset;
                convertedFrame.zScale = component.GetCoordinateSystem().C.Transform.Scale;
                convertedFrame.zOffset = component.GetCoordinateSystem().C.Transform.Offset;

            }

            // 处理 Reflectance 数据
            if (frame.GetReflectance() != null)
            {
                IComponent component = frame.GetReflectance();
                convertedFrame.Width = (int)component.GetWidth();
                convertedFrame.Height = (int)component.GetConfiguredHeight();
                IntPtr data = component.GetData();
                int dataSize = (int)component.GetConfiguredDataSize();
                int bitsPerPixel = (int)component.GetBitsPerPixel();
                int count = dataSize / (int)Math.Ceiling(bitsPerPixel / 8.0);

                Logger.WriteLine($"Processing REFLECTANCE Data: W={convertedFrame.Width}, H={convertedFrame.Height}, DS={dataSize}, BPP={bitsPerPixel}, Count={count}");

                convertedFrame.Reflectance = new byte[count];
                Copy(data, convertedFrame.Reflectance, count);
            }

            return convertedFrame;
        }
        public void SaveInHistory(string Reason)
        {
            DateTime DT = DateTime.Now;
            string dt_str = $"{DT:yyyyMMdd_HHmmss}";

            string SaveDir = MainWindow.CameraConfigHistoryRootDir;

            SaveSettings(Path.Combine(SaveDir, $"{dt_str}_CAMCONFIG_{Reason}.csv"));
        }

        public static unsafe void Copy(IntPtr ptrSource, UInt16[] dest, int elements)
        {
            fixed (UInt16* ptrDest = &dest[0])
            {
                CopyMemory((IntPtr)ptrDest, ptrSource, (uint)(elements * 2));    // 2 bytes per element
            }
        }

        public static unsafe void Copy(IntPtr ptrSource, byte[] dest, int elements)
        {
            fixed (byte* ptrDest = &dest[0])
            {
                CopyMemory((IntPtr)ptrDest, ptrSource, (uint)(elements));    // 1 byte per element
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        static extern void CopyMemory(IntPtr Destination, IntPtr Source, uint NumBytes);
    }
}

