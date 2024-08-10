using Sick.GenIStream;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.IO;
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

        public void StartCapturingFrames()
        {
            DoCaptureFrames = true;
        }

        public void StopCapturingFrames()
        {
            DoCaptureFrames = false;
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
            if (ParamStorage.GetInt("Camera Enabled") == 0)
            {
                Thread.Sleep(100);
                return true;
            }

            if (Camera == null)
            {
                Logger.WriteLine("Searching for camera " + CameraName + " at " + IPAddressStr);
                SetConnectionState(ConnectionState.Searching);

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
                    SetConnectionState(ConnectionState.Connected);
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
                        SetCaptureState(CaptureState.Capturing);
                        Logger.WriteLine("Started FrameGrabber for " + CameraName + " at " + IPAddressStr);
                        Thread.Sleep(100);
                    }

                    if (frameGrabber.IsStarted)
                    {
                        try
                        {
                            Logger.WriteLine("frameGrabber.Grab()...");
                            IFrameWaitResult res = frameGrabber.Grab(new TimeSpan(0, 0, 120));
                            if (res != null)
                            {
                                res.IfCompleted(OnNewFrame)
                                    .IfAborted(OnAborted)
                                    .IfTimedOut(OnTimedOut);
                            }
                        }
                        catch (Exception E)
                        {
                            Logger.WriteException(E);
                        }

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
                    if (ParamStorage.GetInt("Camera Sim Enabled") == 1)
                    {
                        InSimMode = true;
                        Logger.WriteLine("Camera SIM MODE Enabled");
                        UpdateSimCamera(true);
                    }
                    else
                    {
                        ProcessRealCamera(discovery);
                    }
                }
                else
                {
                    if (ParamStorage.GetInt("Camera Sim Enabled") == 0)
                    {
                        InSimMode = false;
                        Logger.WriteLine("Camera SIM MODE Disabled");
                        ProcessRealCamera(discovery);
                    }
                    else
                    {
                        UpdateSimCamera(false);
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
                    Camera.Dispose();
                    Camera = null;
                }
            }
            catch (Exception e) { Logger.WriteException(e); }

            Camera = null;
            frameGrabber = null;
            LastFrame = null;
            SetConnectionState(ConnectionState.Shutdown);
        }

        private void UpdateSimCamera(bool restart)
        {
            if (restart)
            {
                Logger.WriteLine("UpdateSimCamera::RESTARTING");
                string RootDir = Environment.GetEnvironmentVariable("PALLETCHECK_ROOT_DIR");
                DirectoryInfo info = new DirectoryInfo(RootDir + '\\' + ParamStorage.GetString("Camera Sim Directory"));
                SimModeFileList = info.GetFiles("*.r3").OrderBy(p => p.CreationTime).ToArray();
                SimModeFileIndex = 0;
                SimModeNextDT = DateTime.Now;
            }

            if (DateTime.Now >= SimModeNextDT)
            {
                float DT = ParamStorage.GetFloat("Camera Sim Sec Per Pallet");
                SimModeNextDT = DateTime.Now + TimeSpan.FromSeconds(DT);

                if (SimModeFileList.Length > 0)
                {
                    FileInfo FI = SimModeFileList[SimModeFileIndex % SimModeFileList.Length];
                    Logger.WriteLine("UpdateSimCamera::New frame! " + SimModeFileIndex.ToString());
                    Logger.WriteLine(FI.FullName);

                    CaptureBuffer CB = new CaptureBuffer();
                    CB.Load(FI.FullName);

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
                F.Width = component.GetWidth();
                F.Height = component.GetHeight();
                IntPtr data = component.GetData();
                int DataSize = (int)component.GetDataSize();
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
                F.Width = component.GetWidth();
                F.Height = component.GetHeight();
                IntPtr data = component.GetData();
                int DataSize = (int)component.GetDataSize();
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
                    Camera.ExportParameters(FileName);
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
                ConfigurationResult Result = Camera.ImportParameters(FileName);
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

