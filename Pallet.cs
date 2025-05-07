using System.Windows;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Sick.EasyRanger.Base;
using static PalletCheck.MainWindow;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Linq;

namespace PalletCheck
{

    public struct PalletPoint
    {
        public int X;
        public int Y;
        public PalletPoint(int x, int y)
        {
            X = x; Y = y;
        }
    }

    public class PalletProcessor
    {
        List<Pallet> HighInputQueue = new List<Pallet>();
        List<Pallet> HighInProgressList = new List<Pallet>();
        List<Pallet> LowInputQueue = new List<Pallet>();
        List<Pallet> LowInProgressList = new List<Pallet>();

        int MaxHighThreads;
        int MaxLowThreads;
        object LockObject = new object();
        public PalletProcessor(int MaxHighThreads, int MaxLowThreads)
        {
            this.MaxHighThreads = MaxHighThreads;
            this.MaxLowThreads = MaxLowThreads;
        }


        public void ProcessPalletHighPriority(Pallet P, Pallet.palletAnalysisCompleteCB Callback,PositionOfPallet position)
        {
            P.OnAnalysisComplete_User += Callback;
            P.UseBackgroundThread = false;

            lock (LockObject)
            {
                HighInputQueue.Add(P);
                UpdateHigh(position);
            }
        }

        public void OnHighPalletCompleteCB(Pallet P,PositionOfPallet position)
        {
            // This runs in the MAIN THREAD

            // Cleanup High Queue
            lock (LockObject)
            {
                if (HighInProgressList.Contains(P))
                    HighInProgressList.Remove(P);

                UpdateHigh(position);
            }

            // Call pallet complete callback for user
            P.CallUserCallback();
        }

        public void UpdateHigh(PositionOfPallet position)
        {

            lock (LockObject)
            {
                // Check if we can launch threads for items in the queues
                if ((HighInputQueue.Count > 0) && (HighInProgressList.Count < MaxHighThreads))
                {
                    Pallet P = HighInputQueue[0];
                    HighInputQueue.RemoveAt(0);
                    HighInProgressList.Add(P);

                    if (!Pallet.Shutdown)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ThreadPriority TPBackup = Thread.CurrentThread.Priority;
                            Logger.WriteLine("HIGH THREAD PRIORITY: " + TPBackup.ToString());
                            Thread.CurrentThread.Priority = ThreadPriority.Highest;

                            try
                            {
                                P.DoAnalysisBlocking(position);
                                Thread.CurrentThread.Priority = TPBackup;
                            }
                            catch (Exception e)
                            {
                                Thread.CurrentThread.Priority = TPBackup;
                                Logger.WriteException(e);
                            }

                            if (!Pallet.Shutdown)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    OnHighPalletCompleteCB(P,position);
                                });
                            }
                        });
                    }
                }
            }
        }

        public void ProcessPalletLowPriority(Pallet P, Pallet.palletAnalysisCompleteCB Callback, PositionOfPallet position)
        {
            P.OnAnalysisComplete_User += Callback;
            P.UseBackgroundThread = true;

            lock (LockObject)
            {
                LowInputQueue.Add(P);
                UpdateLow(position);
            }
        }

        public void OnLowPalletCompleteCB(Pallet P,PositionOfPallet position)
        {
            // This runs in the MAIN THREAD

            // Cleanup Low Queue
            lock (LockObject)
            {
                if (LowInProgressList.Contains(P))
                    LowInProgressList.Remove(P);

                UpdateLow(position);
            }

            // Call pallet complete callback for user
            P.CallUserCallback();
        }

        public void UpdateLow(PositionOfPallet position)
        {
            lock (LockObject)
            {
                // Check if we can launch threads for items in the queues
                if ((LowInputQueue.Count > 0) && (LowInProgressList.Count < MaxLowThreads))
                {
                    Pallet P = LowInputQueue[0];
                    LowInputQueue.RemoveAt(0);
                    LowInProgressList.Add(P);

                    Task.Factory.StartNew(() =>
                    {
                        ThreadPriority TPBackup = Thread.CurrentThread.Priority;
                        Logger.WriteLine("LOW THREAD PRIORITY: " + TPBackup.ToString());
                        Thread.CurrentThread.Priority = ThreadPriority.Lowest;

                        try
                        {
                            P.DoAnalysisBlocking(position);
                            Thread.CurrentThread.Priority = TPBackup;
                        }
                        catch (Exception e)
                        {
                            Thread.CurrentThread.Priority = TPBackup;
                            Logger.WriteException(e);
                        }


                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            OnLowPalletCompleteCB(P, position);
                        });
                    });
                }
            }
        }
    }


    public class Pallet
    {
        public static bool Busy = false;
        public static bool Shutdown = false;
        public DateTime CreateTime;
        public DateTime AnalysisStartTime;
        public DateTime AnalysisStopTime;
        public double AnalysisTotalSec;
        public bool UseBackgroundThread;

        public enum InspectionState
        {
            Unprocessed,
            Pass,
            Fail
        }

        public InspectionState State = InspectionState.Unprocessed;
        public string Directory;
        public string Filename;
        public CaptureBuffer Original;
        public CaptureBuffer Denoised;
        public List<PalletDefect> AllDefects = new List<PalletDefect>();
        public static List<Board> CombinedBoards = new List<Board>();
        public static string folderName;

        // Variables estáticas: se utilizan para almacenar todos los defectos fusionados
        public static List<PalletDefect> CombinedDefects = new List<PalletDefect>();

        // Bloqueos estáticos: seguridad para operaciones multihilo
        public static object LockObjectCombine = new object();

        // Añadir defectos a la lista de fusión
        public static void AddToCombinedDefects(List<PalletDefect> defects)
        {
            lock (LockObjectCombine)
            {
                CombinedDefects.AddRange(defects);
            }
        }

        // 获取合并后的缺陷
        public static List<PalletDefect> GetCombinedDefects()
        {
            lock (LockObjectCombine)
            {
                return new List<PalletDefect>(CombinedDefects); // 返回副本，避免外部直接修改
            }
        }

        // 清空合并的缺陷
        public static void ClearCombinedDefects()
        {
            lock (LockObjectCombine)
            {
                CombinedDefects.Clear();
            }
        }
        public PositionOfPallet PoistionOfThisPallet { get; set; }

        public CaptureBuffer ReflectanceCB;

        public Dictionary<string, CaptureBuffer> CBDict = new Dictionary<string, CaptureBuffer>();
        public List<CaptureBuffer> CBList = new List<CaptureBuffer>();

        delegate CaptureBuffer addBufferCB(string s, UInt16[] buf);

        public delegate void palletAnalysisCompleteCB(Pallet P,PositionOfPallet position);
        public event palletAnalysisCompleteCB OnAnalysisComplete_User;

        public void CallUserCallback()
        {
            if (OnAnalysisComplete_User != null)
                OnAnalysisComplete_User(this,this.PoistionOfThisPallet);
        }

        public class Board
        {
            // Jack Note: Extract the board for every part. The board includes Name/Location/Buffer/Edges/ExpetectedPix/MeasuredPix/
            public string BoardName;
            public PalletDefect.DefectLocation Location;
            public CaptureBuffer CB;
            public CaptureBuffer CrackCB;
            public List<PalletPoint>[] Edges = new List<PalletPoint>[2];
            public int ExpectedAreaPix;
            public int MeasuredAreaPix;
            public List<PalletPoint> Nails;
            public List<PalletPoint> Cracks;
            public int[,] CrackTracker;
            public int[,] BoundaryBlocks;
            public int CrackBlockSize;
            public PalletPoint BoundsP1;
            public PalletPoint BoundsP2;
            public double ExpWidth;
            public double ExpLength;
            public double MinWidthTooNarrow;
            public double MinWidthForChunk;
            public double MinLengthForChunk;
            public double MinWidthForChunkAcrossLength =0;
            public float MaxAllowable;
            public float XResolution;
            public float YResolution;
            public int startY;
            public int endY;
            public int startX;
            public int endX;
            public List<PalletDefect> AllDefects;

            public Board(string Name, PalletDefect.DefectLocation Location, bool Horiz, int ExpectedLength, int ExpectedWidth)
            {
                Edges[0] = new List<PalletPoint>();
                Edges[1] = new List<PalletPoint>();
                Nails = new List<PalletPoint>();
                Cracks = new List<PalletPoint>();
                BoardName = Name;
                this.Location = Location;
                ExpWidth = ExpectedWidth;
                //ExpectedAreaPix = ExpectedWidth * ExpectedLength;
                AllDefects = new List<PalletDefect>();
            }
        }

        public List<Board> BList;
        public bool Pos= false;
        public int[] startY = new int[9];
        public int[] endY= new int[9];
        public List<PalletDefect> PalletLevelDefects = new List<PalletDefect>();

        public Pallet(CaptureBuffer RangeCV, CaptureBuffer ReflCB,PositionOfPallet positionOfPallet)
        {
            Original = RangeCV;
            ReflectanceCB = ReflCB;
            PoistionOfThisPallet = positionOfPallet;
            CreateTime = DateTime.Now;
        }

        public Pallet(string Filename, PositionOfPallet position)
        {
            Original = null;
            CreateTime = DateTime.Now;
            this.Filename = Filename;
            PoistionOfThisPallet = position;
        }

        ~Pallet()
        {
            Original = null;
            Denoised = null;
        }

        public void DoAnalysisBlocking(PositionOfPallet position)
        {
            AnalysisStartTime = DateTime.Now;
            Busy = true;
            Logger.WriteLine("Pallet::DoAnalysis START");
            AddCaptureBuffer("Image", ReflectanceCB);
            Logger.WriteLine(String.Format("DoAnalysisBlocking: Original W,H  {0}  {0}", Original.Width, Original.Height));
            AddCaptureBuffer("Original", Original);
            //Denoised = DeNoise(Original, MainWindow.GetParamStorage(position));
            Denoised = DeNoiseEasyRanger(Original, position);
            if (Denoised == null)
            {
                Logger.WriteLine("DeNoise() returned a NULL capturebuffer!");
                return;
            }

            Logger.WriteLine(String.Format("DoAnalysisBlocking: Denoised W,H  {0}  {0}", Denoised.Width, Denoised.Height));
   
            
            IsolateAndAnalyzeSurfaces(Denoised, position);
           
            if (State == InspectionState.Fail)
            {
                Logger.WriteLine("Pallet::DoAnalysis INSPECTION FAILED");
                AnalysisStopTime = DateTime.Now;
                AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
                return;
            }

            // Final board size sanity check
            int ExpWidH = (int)MainWindow.GetParamStorage(position).GetPixY(StringsLocalization.HBoardWidth_in);
            int ExpWidV1 = (int)MainWindow.GetParamStorage(position).GetPixX(StringsLocalization.VBoardWidth_in);

            // Check for debris causing unusually wide boards
            if (position == PositionOfPallet.Top)
            {
                for (int i = 0; i < 7; i++)
                {
                    string paramName = string.Format(StringsLocalization.TopHXBoardWidth_in, i + 1);
                    ExpWidH = MainWindow.GetParamStorage(position).GetPixY(paramName);                 

                    if (BList[i].AllDefects.Count == 0)
                        continue;

                    int H_Wid = BList[i].BoundsP2.Y - BList[i].BoundsP1.Y;
                    if (H_Wid > (ExpWidH * 1.8))
                    {
                        AddDefect(BList[i], PalletDefect.DefectType.possible_debris, "Unusually wide board");
                        SetDefectMarker(BList[i]);
                    }

                    // Add the current Board to the CombinedBoards
                    lock (LockObjectCombine)
                    {
                        CombinedBoards.Add(BList[i]);
                        CombinedDefects.AddRange(BList[i].AllDefects);
                    }
                }
            }

            if (position == PositionOfPallet.Bottom)
            {
                Pos = false;
                ExpWidH = MainWindow.GetParamStorage(position).GetPixY(StringsLocalization.HBoardWidth_in);
                ExpWidV1 = (int)MainWindow.GetParamStorage(position).GetPixX(StringsLocalization.VBoardWidth_in);
                for (int i = 3; i < 5; i++)
                {                 
                    if (BList[i].AllDefects.Count == 0)
                        continue;

                    int H_Wid = BList[i].BoundsP2.Y - BList[i].BoundsP1.Y;

                    if (H_Wid > (ExpWidH * 1.8))
                    {
                        AddDefect(BList[i], PalletDefect.DefectType.possible_debris, "Unusually wide board");
                        SetDefectMarker(BList[i]);
                    }

                    // Add the current Board to the CombinedBoards
                    lock (LockObjectCombine)
                    {
                        CombinedBoards.Add(BList[i]);
                        CombinedDefects.AddRange(BList[i].AllDefects);
                    }
                }

                for (int i = 0; i < 3; i++) 
                {
                    if (BList[i].AllDefects.Count > 0)
                    {
                        int V1_Wid = BList[i].BoundsP2.X - BList[i].BoundsP1.X;
                        if (V1_Wid > (ExpWidV1 * 1.8))
                        {
                            AddDefect(BList[i], PalletDefect.DefectType.possible_debris, "Unusually wide board");
                            SetDefectMarker(BList[i]);
                        }

                        // Add the current Board to the CombinedBoards
                        lock (LockObjectCombine)
                        {
                            CombinedBoards.Add(BList[i]);
                            CombinedDefects.AddRange(BList[i].AllDefects);
                        }
                    }
                }
            }

            State = AllDefects.Count > 0 ? InspectionState.Fail : InspectionState.Pass;
            Benchmark.Stop("Total");
            string Res = Benchmark.Report();

            //MainWindow.WriteLine(string.Format("Analysis took {0} ms\n\n", (Stop - Start).TotalMilliseconds));
            Busy = false;
            AnalysisStopTime = DateTime.Now;
            AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
            Logger.WriteLine(String.Format("Pallet::DoAnalysis FINISHED  -  {0:0.000} sec", AnalysisTotalSec));      
        }      

        private CaptureBuffer DeNoiseEasyRanger(CaptureBuffer SourceCB, PositionOfPallet position)
        {
            CaptureBuffer NewBuf = new CaptureBuffer();
           if (position == PositionOfPallet.Bottom)
            {
                float[] floatArray = MainWindow._envBottom.GetImageBuffer("FilteredImage")._range;
                byte[] byteArray = MainWindow._envBottom.GetImageBuffer("FilteredImage")._intensity;

                // 转换为 ushort 数组
                ushort[] ushortArray = new ushort[floatArray.Length];
                for (int i = 0; i < floatArray.Length; i++)
                {
                    ushortArray[i] = (ushort)(floatArray[i] + 1000);
                }

                NewBuf.Buf = ushortArray;
                NewBuf.Width = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.Width;
                NewBuf.Height = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.Height;
                NewBuf.xScale = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.XResolution;
                NewBuf.yScale = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.YResolution;

                AddCaptureBuffer("Filtered", NewBuf);
               
            }

            if (position == PositionOfPallet.Top)
            {
                float[] floatArray = MainWindow._envTop.GetImageBuffer("FilteredImage")._range;
                byte[] byteArray = MainWindow._envTop.GetImageBuffer("FilteredImage")._intensity;

                // 转换为 ushort 数组
                ushort[] ushortArray = new ushort[floatArray.Length];
                for (int i = 0; i < floatArray.Length; i++)
                {
                    ushortArray[i] = (ushort)(floatArray[i] + 1000);
                }

                NewBuf.Buf = ushortArray;
                NewBuf.Width = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Width;
                NewBuf.Height = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Height;
                NewBuf.xScale = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.XResolution;
                NewBuf.yScale = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.YResolution;

                AddCaptureBuffer("Filtered", NewBuf);

            }
            return NewBuf;
        }

        void AddCaptureBuffer(string Name, CaptureBuffer CB)
        {
            CB.Name = Name;
            CBDict.Add(Name, CB);
            CBList.Add(CB);
        }

        public void ByteArray2bitmap(byte[] byteArray, int width, int height, Bitmap bitmap)
        {
            try
            {
                if (byteArray == null || byteArray.Length != width * height)
                {
                    throw new ArgumentException("El tamaño del byteArray no coincide con las dimensiones de la imagen.");
                }

                // Establecer paleta de grises
                ColorPalette palette = bitmap.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                bitmap.Palette = palette;

                // Bloquear bits y copiar datos
                BitmapData bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format8bppIndexed
                );

                Marshal.Copy(byteArray, 0, bmpData.Scan0, byteArray.Length);
                bitmap.UnlockBits(bmpData);

                // Return image



            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar la imagen: {ex.Message}");
            }
        }

        public void UShortArray2Bitmap(ushort[] ushortArray, int width, int height, Bitmap bitmap)
        {
            try
            {
                if (ushortArray == null || ushortArray.Length != width * height)
                {
                    throw new ArgumentException("El tamaño del ushortArray no coincide con las dimensiones de la imagen.");
                }

                // Convertir ushort[] a byte[], normalizando a 8 bits (0-255)
                byte[] byteArray = new byte[ushortArray.Length];
                ushort maxVal = ushortArray.Max();

                if (maxVal == 0) maxVal = 1; // evitar división entre cero

                for (int i = 0; i < ushortArray.Length; i++)
                {
                    byteArray[i] = (byte)(ushortArray[i] * 255 / maxVal);
                }

                // Establecer paleta de grises
                ColorPalette palette = bitmap.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                bitmap.Palette = palette;

                // Bloquear bits y copiar datos
                BitmapData bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format8bppIndexed
                );

                Marshal.Copy(byteArray, 0, bmpData.Scan0, byteArray.Length);
                bitmap.UnlockBits(bmpData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar la imagen: {ex.Message}");
            }
        }

        private void DeNoiseInPlace(CaptureBuffer CB,ParamStorage paramStorage)
        {
            UInt16[] Source = CB.Buf;
            UInt16[] Res = (UInt16[])CB.Buf.Clone();

            int leftEdge = paramStorage.GetInt(StringsLocalization.RawCaptureROILeft_px); 
            int rightEdge = paramStorage.GetInt(StringsLocalization.RawCaptureROIRight_px); 

            for (int i = 1; i < Res.Length - 1; i++)
            {
                int x = i % CB.Width;
                if ((x <= leftEdge) || (x >= rightEdge))
                {
                    Res[i] = 0;
                    continue;
                }

                if (Source[i] != 0)
                {
                    if ((Source[i - 1] == 0) &&
                        (Source[i + 1] == 0))
                        Res[i] = 0;
                    else
                        Res[i] = Source[i];
                }
                else
                {
                    if ((Source[i - 1] != 0) &&
                        (Source[i + 1] != 0))
                        Res[i] = (UInt16)((Source[i - 1] + (Source[i + 1])) / 2);
                }
            }

            for (int i = 0; i < Res.Length; i++)
                Source[i] = Res[i];
        }


        //=====================================================================
        private int FindTopY(CaptureBuffer SourceCB,ParamStorage paramStorage)
        {
            //
            // !!! Needs to work with Original and Denoised Buffers
            //

             UInt16[] Source = SourceCB.Buf;
            int LROI = paramStorage.GetInt(StringsLocalization.RawCaptureROILeft_px);
            int RROI = paramStorage.GetInt(StringsLocalization.RawCaptureROIRight_px);
            //int MinZTh = ParamStorage.GetInt("Raw Capture ROI Min Z (px)");
            //int MaxZTh = ParamStorage.GetInt("Raw Capture ROI Max Z (px)");

            int[] Hist = new int[500];

            int ylimit = Math.Min(1500, (int)(SourceCB.Height / 2));
            for (int x = LROI; x < RROI; x += 10)
            {
                for (int y = 50; y < ylimit; y += 10)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];

                    if (Val != 0)//(Val >= MinZTh) && (Val <= MaxZTh))
                    {
                        Hist[y / 10]++;
                        break;
                    }
                }
            }

            int HighVal = 0;
            int HighIndex = -1;

            for (int i = 0; i < Hist.Length; i++)
            {
                if (Hist[i] > HighVal)
                {
                    HighVal = Hist[i];
                    HighIndex = i;
                }
            }

            int InitialY = HighIndex * 10;

            // Check upward and see if there is a row completely black
            for (int y = InitialY;  y > 0; y--)
            {
                bool NotClear = true;
                for (int x = 0; x < SourceCB.Width; x++)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];

                    //if ((Val >= MinZTh) && (Val <= MaxZTh))
                    if (Val != 0)
                    {
                        NotClear = true;
                    }
                }
                if (!NotClear)
                {
                    return y;
                }
            }
            return Math.Max(0, InitialY - 40);
        }

        private int FindBottomY(CaptureBuffer SourceCB, ParamStorage paramStorage)
        {
            // Jack Note // !!! Works directly with the buffer, starts from the bottom
            //
            if (SourceCB.Buf == null)
            {
                throw new ArgumentNullException("SourceCB.Buf", "Source buffer cannot be null.");
            }

            UInt16[] Source = SourceCB.Buf;
            int LROI = paramStorage.GetInt(StringsLocalization.RawCaptureROILeft_px);  // Left ROI boundary
            int RROI = paramStorage.GetInt(StringsLocalization.RawCaptureROIRight_px); // Right ROI boundary
            int RequiredNonZeroCount = 10;  // Minimum number of non-zero pixels required

            // Traverse the image from the last row to the top
            for (int y = SourceCB.Height - 1; y >= 0; y--)
            {
                int NonZeroCount = 0; // Count of non-zero pixels in the current row

                // Traverse each pixel between the left and right ROI
                for (int x = LROI; x < RROI; x++)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];  // Get the value of the current pixel

                    if (Val != 0)  // Increment count if the pixel is non-zero
                    {
                        NonZeroCount++;
                    }

                    // Stop checking further if the required count is reached
                    if (NonZeroCount >= RequiredNonZeroCount)
                    {
                        return y;  // Return the current row if it meets the requirement
                    }
                }
            }
            // If no row satisfies the condition, return 0
            return 0;
        }

        //=====================================================================
        // Jack Note: Get the top X
        private int FindTopX(CaptureBuffer SourceCB,ParamStorage paramStorage)
        {
            //
            // !!! Needs to work with Original and Denoised Buffers
            //

            UInt16[] Source = SourceCB.Buf;
            int TROI = paramStorage.GetInt(StringsLocalization.RawCaptureROITop_px);
            int BROI = paramStorage.GetInt(StringsLocalization.RawCaptureROIBottom_px);
            //int MinZTh = ParamStorage.GetInt("Raw Capture ROI Min Z (px)");
            //int MaxZTh = ParamStorage.GetInt("Raw Capture ROI Max Z (px)");

            int[] Hist = new int[100];
            int xlimit = Math.Min(999, (int)(SourceCB.Width / 2));
            for (int y = TROI; y < BROI; y += 10)
            {
                for (int x = 0; x < xlimit; x += 10)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];

                    if (Val != 0) // (Val >= MinZTh) && (Val <= MaxZTh))
                    {
                        Hist[x / 10]++;
                        break;
                    }
                }
            }

            int HighVal = 0;
            int HighIndex = -1;

            for (int i = 0; i < Hist.Length; i++)
            {
                if (Hist[i] > HighVal)
                {
                    HighVal = Hist[i];
                    HighIndex = i;
                }
            }

            int InitialX = HighIndex * 10;

            // Check leftward and see if there is a column completely black
            for (int x = InitialX; x > 0; x--)
            {
                bool NotClear = true;
                for (int y = 0; y < SourceCB.Height; y++)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];

                    // if ((Val >= MinZTh) && (Val <= MaxZTh))
                    if (Val != 0)
                    {
                        NotClear = true;
                    }
                }
                if (!NotClear)
                {
                    return x;
                }
            }

            return Math.Max(0, InitialX - 40);
        }

        //=====================================================================
        private void IsolateAndAnalyzeSurfaces(CaptureBuffer SourceCB,PositionOfPallet position)
        {
            int nRows = SourceCB.Height;
            int nCols = SourceCB.Width;
            ParamStorage paramStorage;
            paramStorage = MainWindow.GetParamStorage(position);
            int LROI = paramStorage.GetInt(StringsLocalization.RawCaptureROILeft_px);//260px
            int RROI = paramStorage.GetInt(StringsLocalization.RawCaptureROIRight_px); //2440px
            int LWid = paramStorage.GetPixX(StringsLocalization.VBoardWidth_in);  //140mm
            int RWid = paramStorage.GetPixX(StringsLocalization.VBoardWidth_in);  //140mm
            int HBoardWid = paramStorage.GetPixY(StringsLocalization.HBoardWidth_in); //140mm
            int LBL = paramStorage.GetPixY(StringsLocalization.VBoardLength_in); //1018mm
            int TopY = FindTopY(SourceCB, paramStorage);  // Jack Note: Find the first row that is not all black 

            BList = new List<Board>();
            int iCount = 0;

            // The ProbeCB gets modified as boards are found and subtracted...so need a unique copy
            // Jack Note: Get all boards with edge and so on
            CaptureBuffer ProbeCB = new CaptureBuffer(SourceCB);
            
            if(position == PositionOfPallet.Bottom)
            {
                //to extract datasaet 
                if (enableDatasetExtraction)
                {
                    if (SelectPositionForExtraction == 1)
                    {
                        float[] floatA = MainWindow._envBottom.GetImageBuffer("FilteredImage")._range;
                        byte[] byteA = MainWindow._envBottom.GetImageBuffer("FilteredImage")._intensity;
                        int width = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.Width;
                        int height = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.Height;
                        float xScale = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.XResolution;
                        float yScale = MainWindow._envBottom.GetImageBuffer("FilteredImage").Info.YResolution;
                        cntr = cntr + 1;
                        DatasetExtraction.ExtractRangeForDataset((int)PositionOfPallet.Bottom, byteA, floatA, width, height, xScale, yScale, cntr, enableDatasetExtraction);
                    }
                }

                iCount = 5;
                int TopX = FindTopX(ProbeCB,paramStorage);
                int BottomY  = FindBottomY(ProbeCB,paramStorage);
                //int LeftY = FindTopY(ProbeCB, paramStorage);
                //int HBL = paramStorage.GetPixY("V Board Length (in)"); //1018mm
                //int VBoardWid = paramStorage.GetPixX("H Board Width (in)");

                int StartX1 = TopX - (int)(10/ SourceCB.xScale);
                int EndX1 = StartX1 + (int)(220/ SourceCB.xScale);

                int StartX2 = StartX1 + (int)(410 / SourceCB.xScale);
                int EndX2 = StartX2 + (int)(220 / SourceCB.xScale);

                int StartX3 = StartX2 + (int)(420 / SourceCB.xScale);;
                int EndX3 = StartX3 + (int)(220 / SourceCB.xScale);


                //int StartY = TopY + (int)(120 / SourceCB.yScale);
                int StartY = 370;
                int EndY = BottomY - (int)(120 / SourceCB.yScale);

                int ExpWidH = (int)MainWindow.GetParamStorage(position).GetPixY(StringsLocalization.HBoardWidth_in);
                int ExpLengthH = (int)MainWindow.GetParamStorage(position).GetPixX(StringsLocalization.HBoardLength_in);
                int ExpWidV = (int)MainWindow.GetParamStorage(position).GetPixX(StringsLocalization.VBoardWidth_in);
                int ExpLengthV = (int)MainWindow.GetParamStorage(position).GetPixY(StringsLocalization.VBoardLength_in);

                Board V1 = ProbeHorizontallyRotate90(ProbeCB, "V1", PalletDefect.DefectLocation.B_V1, StartY, EndY, 1, StartX1, EndX1, paramStorage);
                Board V2 = ProbeHorizontallyRotate90(ProbeCB, "V2", PalletDefect.DefectLocation.B_V2, StartY, EndY, 1, StartX2, EndX2, paramStorage);
                Board V3 = ProbeHorizontallyRotate90(ProbeCB, "V3", PalletDefect.DefectLocation.B_V3, StartY, EndY, 1, StartX3, EndX3, paramStorage);
                Board H1 = ProbeVerticallyRotate90(ProbeCB, "H1", PalletDefect.DefectLocation.B_H1, StartX1, EndX3, 1, TopY - 20, TopY + 330, paramStorage);
                Board H2 = ProbeVerticallyRotate90(ProbeCB, "H2", PalletDefect.DefectLocation.B_H2, StartX1, EndX3, 1, BottomY + 20, BottomY - 300, paramStorage);

                V1.startX = StartX1;
                V1.endX = EndX1;    
                V1.startY = StartY;
                V1.endY = EndY;
                
                V2.startX = StartX2;
                V2.endX = EndX2;
                V2.startY = StartY;
                V2.endY = EndY;

                V3.startX = StartX3;
                V3.endX = EndX3;
                V3.startY = StartY;
                V3.endY = EndY;

                H1.startX = StartX1;
                H1.endX = EndX3;
                H1.startY = TopY - 20;
                H1.endY = TopY + 330;

                H2.startX = StartX1;
                H2.endX = EndX3;
                H2.startY = BottomY + 20;
                H2.endY = BottomY - 300;

                V1.ExpectedAreaPix = ExpWidV * ExpLengthV;
                V2.ExpectedAreaPix = ExpWidV * ExpLengthV;
                V3.ExpectedAreaPix = ExpWidV * ExpLengthV;
                H1.ExpectedAreaPix = ExpLengthH * ExpWidH;
                H2.ExpectedAreaPix = ExpLengthH * ExpWidH;
                V1.ExpWidth = ExpWidV;
                V2.ExpWidth = ExpWidV ;
                V3.ExpWidth = ExpWidV ;
                H1.ExpWidth = ExpWidH ;
                H2.ExpWidth = ExpWidH;

                //Parameters used for CheckNarrowBoard() function commented out cause it's not a requirement
                /*V1.MinWidthTooNarrow = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V1NarrowBoardMinimumWidth);
                V2.MinWidthTooNarrow = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V2NarrowBoardMinimumWidth);
                V3.MinWidthTooNarrow = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V3NarrowBoardMinimumWidth);
                H1.MinWidthTooNarrow = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.H1NarrowBoardMinimumWidth);
                H2.MinWidthTooNarrow = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.H2NarrowBoardMinimumWidth);*/

                V1.MinWidthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V1MissingChunkMaximumWidth);
                V2.MinWidthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V2MissingChunkMaximumWidth);
                V3.MinWidthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V3MissingChunkMaximumWidth);
                H1.MinWidthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.H1MissingChunkMinimumWidth);
                H2.MinWidthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.H2MissingChunkMinimumWidth);

                V1.MinLengthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V1MissingChunkMaximumLength);
                V2.MinLengthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V2MissingChunkMaximumLength);
                V3.MinLengthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V3MissingChunkMaximumLength);
                H1.MinLengthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.H1MissingChunkMinimumLength);
                H2.MinLengthForChunk = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.H2MissingChunkMinimumLength);

                float V1maxallowabe = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V1MaxAllowedMissingWood);
                float V2maxallowabe = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V2MaxAllowedMissingWood);
                float V3maxallowabe = MainWindow.GetParamStorage(position).GetFloat(StringsLocalization.V3MaxAllowedMissingWood);
                float H1maxallowabe = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.HXMaxAllowedMissingWood,"1", null));
                float H2maxallowabe = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.HXMaxAllowedMissingWood, "2", null));

                V1.MaxAllowable = V1maxallowabe;
                V2.MaxAllowable = V2maxallowabe; 
                V3.MaxAllowable = V3maxallowabe; 
                H1.MaxAllowable = H1maxallowabe; 
                H2.MaxAllowable = H2maxallowabe;

                V1.XResolution= SourceCB.xScale;
                V2.XResolution= SourceCB.xScale;
                V3.XResolution = SourceCB.xScale;
                H1.XResolution = SourceCB.xScale;
                H2.XResolution = SourceCB.xScale;

                V1.YResolution = SourceCB.yScale;
                V2.YResolution = SourceCB.yScale;
                V3.YResolution = SourceCB.yScale;
                H1.YResolution = SourceCB.yScale;
                H2.YResolution = SourceCB.xScale;
              
                BList.Add(V1);
                BList.Add(V2);
                BList.Add(V3);
                BList.Add(H1);
                BList.Add(H2);
            }

            if(position == PositionOfPallet.Top)
            {
                Pos = true;

                //to extract datasaet 
                if (enableDatasetExtraction) {
                    if (SelectPositionForExtraction == 0)
                    {
                        float[] floatA = MainWindow._envTop.GetImageBuffer("FilteredImage")._range;
                        byte[] byteA = MainWindow._envTop.GetImageBuffer("FilteredImage")._intensity;
                        int width = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Width;
                        int height = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Height;
                        float xScale = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.XResolution;
                        float yScale = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.YResolution;
                        cntr = cntr + 1;
                        DatasetExtraction.ExtractRangeForDataset((int)PositionOfPallet.Top, byteA, floatA, width, height, xScale, yScale, cntr, enableDatasetExtraction);
                    }
                }

                if (isDeepLActive)
                {
                    /*Classifier inference---------------------------------------------------------------------------------------------------------------------------------*/
                    int widthC = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Width;
                    int heightC = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Height;
                    byte[] byteArrayTop = MainWindow._envTop.GetImageBuffer("FilteredImage")._intensity;
                    Bitmap bitmapTop = new Bitmap(widthC, heightC, PixelFormat.Format8bppIndexed);

                    //Convert the byte array to bitmap
                    ByteArray2bitmap(byteArrayTop, widthC, heightC, bitmapTop);
                    Bitmap resizedImage = model.ResizeBitmap(bitmapTop, 512, 512);

                    if (isSavePalletClassifierResults)
                    {
                        CheckIfDirectoryExists("VizDL/Classifier");
                        resizedImage.Save("VizDL/Classifier/classification.png", ImageFormat.Png);
                    }

                    /*************************************
                     *Critical time functions to improve *
                     *************************************/

                    //Process the image for inference
                    float[] imageDataClass = model.ProcessBitmapForInference(resizedImage, resizedImage.Width, resizedImage.Height);
                    int resultClass = model.RunInferenceClassifier(imageDataClass, resizedImage.Height, resizedImage.Width);
                    PalletClassifier = resultClass;
                    //Console.WriteLine($"Predicción: Clase {resultClass} con de confianza");

                    /*TopRNWHCO Iference-----------------------------------------------------------------------------------------------------------------------------------*/
                    //Process the image for inference
                  /*  defectRNWHCO = false;
                    float[] rangeArray = MainWindow._envTop.GetImageBuffer("FilteredImage")._range;
                    byte[] reflectanceArray = MainWindow._envTop.GetImageBuffer("FilteredImage")._intensity;
                    int width = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Width;
                    int height = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.Height;
                    float xScale = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.XResolution;
                    float yScale = MainWindow._envTop.GetImageBuffer("FilteredImage").Info.YResolution;

                    //Convert Range to ushort 
                    ushort[] ushortArray = new ushort[rangeArray.Length];
                    for (int i = 0; i < rangeArray.Length; i++)
                    {
                        ushortArray[i] = (ushort)(rangeArray[i] + 1000);
                    }
                    CaptureBuffer rangeBuffer = new CaptureBuffer
                    {
                        Buf = ushortArray,

                        Width = width,
                        Height = height,
                        xScale = xScale,
                        yScale = yScale

                    };

                    float[] imageDataTop = model.ProcessBitmapForInference(bitmapTop, bitmapTop.Width, bitmapTop.Height);
                    float[][] resultsTop = model.RunInferenceTop(imageDataTop, bitmapTop.Height, bitmapTop.Width);

                    //Get the results
                    float[] boxesTop = resultsTop[0];
                    float[] labelsTop = resultsTop[1];
                    float[] scoresTop = resultsTop[2];
                    float[] masksTop = resultsTop[3];
                    //Vector for post evaluation
                    int[] centroids;

                    //Vizualice BB Results
                    if (isSaveTopRNWHCO)
                    {  
                        string TopRNWHCO = "VizDL/TopRNHCO/TopRNWHCO.png";
                        bitmapTop.Save(TopRNWHCO, ImageFormat.Png);
                        centroids = model.DrawTopBoxes4(TopRNWHCO, boxesTop, scoresTop, "TopRNWHCO_bb.png", 0.7);
                    }
                    else {

                        centroids = model.getCentroids4(boxesTop, scoresTop, 0.7);
                    }

                    if (centroids != null)
                    {
                        float MNH = (float)paramStorage.GetPixZ(StringsLocalization.MaxNailHeight_mm);
                        int defectsfound = centroids.Length / 2;
                        if (defectsfound > 0)
                        {
                            int ii = 0;
                            for (int i = 0; i < defectsfound; i++)
                            {

                                int Cx1 = centroids[ii];
                                int Cy1 = centroids[ii + 1];
                                ii = ii + 1;

                                float Object1 = model.GetMaxRangeInSquare1(Cx1, Cy1, 10, rangeBuffer, rangeArray);
                                if (Object1 > MNH)
                                {
                                    //Add defect here

                                }

                            }
                        }
                     }*/                 
                }

                RoiType[] roiTypes = new RoiType[7];
                int[] widths = new int[7];
                int[] heights = new int[7];
                float[] xcs = new float[7];
                float[] ycs = new float[7];
                float[] rotations = new float[7];

                for (int i = 0;i< 7;i++)
                {
                    MainWindow._envTop.GetRegionType("PalletRegions", ref roiTypes[i], ref widths[i], ref heights[i], ref xcs[i], ref ycs[i], ref rotations[i], i);
                }

                iCount = 7;               
                List<CaptureBuffer> captureBuffers = new List<CaptureBuffer>();
                List<byte[]> bytesIntensity = new List<byte[]>();
                List<int> W = new List<int>();
                List<int> Y = new List<int>();

                for (int boardIndex = 0; boardIndex < 7; boardIndex++)
                {                 
                    string boardName = $"Board_{boardIndex}";           
                    float[] floatArray = MainWindow._envTop.GetImageBuffer(boardName)._range;
                    byte[] byteArray = MainWindow._envTop.GetImageBuffer(boardName)._intensity;              
                    bytesIntensity.Add(byteArray);
                    W.Add(MainWindow._envTop.GetImageBuffer(boardName).Info.Width);
                    Y.Add(MainWindow._envTop.GetImageBuffer(boardName).Info.Height);

                    //Convert Range to ushort 
                    ushort[] ushortArray = new ushort[floatArray.Length];

                    for (int i = 0; i < floatArray.Length; i++)
                    {
                        ushortArray[i] = (ushort)(floatArray[i] +1000);
                    }

                    //Convert Reflectance to ushort
                    ushort[] ushortArrayRef = new ushort[byteArray.Length];

                    for (int i = 0; i < ushortArrayRef.Length; i++)
                    {
                        ushortArrayRef[i] = byteArray[i];
                    }

                    // Creating and Storing a CaptureBuffer Range
                    CaptureBuffer captureBuffer = new CaptureBuffer
                    {
                        Buf = ushortArray
                        
                    };

                    captureBuffer.Width = MainWindow._envTop.GetImageBuffer(boardName).Info.Width;
                    captureBuffer.Height = MainWindow._envTop.GetImageBuffer(boardName).Info.Height;
                    captureBuffers.Add(captureBuffer);

                    //Capture Buffer Reflectance        
                    CaptureBuffer ReflBuf = new CaptureBuffer
                    {
                        Buf = ushortArrayRef,
                        PaletteType = CaptureBuffer.PaletteTypes.Gray,
                        Width = MainWindow._envTop.GetImageBuffer(boardName).Info.Width,
                        Height = MainWindow._envTop.GetImageBuffer(boardName).Info.Height,

                    };
                }

                try
                {
                    //Leading split inference

                    //define the bitmap
                    Bitmap bitmap = new Bitmap(W[0], Y[0], PixelFormat.Format8bppIndexed);

                    //Convert the byte array to bitmap
                    ByteArray2bitmap(bytesIntensity[0], W[0], Y[0], bitmap);
                    // Define area of interest and cropp
                    //int startC = 0; int endC = W[0]; int startR = (int)(ycs[0] - heights[0] / 2); int endR = (int)(ycs[0] + heights[0] / 2);
                    int startC = 49; int endC = 2400; int startR = (int)(ycs[0] - heights[0] / 2); int endR = (int)(ycs[0] + heights[0] / 2);
                    Rectangle cropRect = new Rectangle(startC, startR, endC, endR);
                    Bitmap croppedBitmap = bitmap.Clone(cropRect, bitmap.PixelFormat);
                    Bitmap croppedBitmapR = model.ResizeBitmap(croppedBitmap, 2560, 688);
                    //Process the image for inference
                    float[] imageData = model.ProcessBitmapForInference(croppedBitmapR, croppedBitmapR.Width, croppedBitmapR.Height);
                    float[][] results = model.RunInference(imageData, croppedBitmapR.Height, croppedBitmapR.Width);

                    //Get the results
                    float[] boxes = results[0];
                    float[] labels = results[1];
                    float[] scores = results[2];
                    float[] masks = results[3];
                    //Vizualice BB Results
                    string LeadingMainImage = "VizDL/TopSplit/Leading.png";

                    //Condition to save the results as png Images
                    if (isSaveTopSplitResults) {
                        croppedBitmapR.Save(LeadingMainImage, ImageFormat.Png);
                        model.DrawTopBoxes2(LeadingMainImage, boxes, scores, "BB_Leading.png");

                    }

                    // Convert to32bppArgb to draw results and display it 
                    Bitmap tempBitmap = new Bitmap(croppedBitmapR.Width, croppedBitmapR.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(tempBitmap))
                    {
                        g.DrawImage(croppedBitmap, new Rectangle(0, 0, croppedBitmapR.Width, croppedBitmapR.Height));
                    }
                    croppedBitmap.Dispose(); // Liberar el anterior
                    croppedBitmap = tempBitmap; // Usar el nuevo bitmap compatible

                    // Draw the segmented objects
                    List<bool[,]> binaryMasks = model.ConvertMasksToBinary(masks, croppedBitmapR.Width, croppedBitmapR.Height, scores.Length);
                    List<bool[]> binaryMasks2=model.ConvertMasksToBinary2(masks, croppedBitmapR.Width, croppedBitmapR.Height, scores.Length);
                    
                    string name = "Leading";
                    bool img1Exist= false;
                    bool img2Exist= false;
                    int upperY1 = 0;
                    int upperY2 = 0;
                    int lowerY1 = 0;
                    int lowerY2 = 0;

                    model.SplitBoard2(croppedBitmapR, boxes, scores, binaryMasks, name,
                      ref img1Exist,ref img2Exist, ref upperY1, ref upperY2, ref lowerY1, ref lowerY2, isSaveTopSplitResults);

                    Board H1 = null;
                    Board H2 = null;
                    
                    ushort[] Clone =captureBuffers[0].Buf.Clone() as ushort[];

                   CaptureBuffer Clon = new CaptureBuffer
                    {
                        Buf = Clone,
                        PaletteType = CaptureBuffer.PaletteTypes.Gray,
                        Width = croppedBitmap.Width,
                        Height = croppedBitmap.Height,

                   };

                    //Old dataset X crops
                    //int X1 = 49;         
                    //int X2 = 2400;
                    
                    int X1 = MainWindow.GetParamStorage(position).GetInt(StringsLocalization.SplitDL_StartX);
                    int X2 = MainWindow.GetParamStorage(position).GetInt(StringsLocalization.SplitDL_EndX);
                    int ScaleVal = MainWindow.GetParamStorage(position).GetInt(StringsLocalization.SplitDL_ScaleY_Leading);
                    upperY2 = model.ScaleVal(upperY2, 688, ScaleVal);
                    lowerY1 = model.ScaleVal(lowerY1, 688, ScaleVal); 

                    if (img1Exist && img2Exist)
                    {//Both images exist Upper Y taked intop account
                        H1 = ProbeVerticallyRotate90(captureBuffers[0], "H1", PalletDefect.DefectLocation.T_H1, X1, X2, 1,
                              startR, upperY2 + startR, paramStorage);
                        //captureBuffers[0].SaveImage("H1.png");
                        H2 = ProbeVerticallyRotate90(captureBuffers[0], "H2", PalletDefect.DefectLocation.T_H2, X1, X2, 1,
                               lowerY1 + startR, endR, paramStorage);
                        startY[0] = startR;
                        endY[0] = upperY2 + startR;
                        startY[1] = lowerY1 + startR;
                        endY[1] = endR;
                    }

                    else if (img1Exist && !img2Exist)
                    {//Only Upper exist Y taked intop account  
                        H1 = ProbeVerticallyRotate90(captureBuffers[0], "H1", PalletDefect.DefectLocation.T_H1, X1, X2, 1,
                              startR, upperY2 + startR, paramStorage);
                        H2 = ProbeVerticallyRotate90(captureBuffers[0], "H2", PalletDefect.DefectLocation.T_H2, X1, X2, 1,
                               upperY2 + startR, endR, paramStorage);

                        startY[0] = startR;
                        endY[0] = upperY2 + startR;
                        startY[1] = upperY2 + startR;
                        endY[1] = endR;
                    }

                    else if (!img1Exist && img2Exist)
                    {//Only Lower exist lowerY taked intop account  
                        H1 = ProbeVerticallyRotate90(captureBuffers[0], "H1", PalletDefect.DefectLocation.T_H1, X1, X2, 1,
                              startR, lowerY1 + startR, paramStorage);
                        H2 = ProbeVerticallyRotate90(captureBuffers[0], "H2", PalletDefect.DefectLocation.T_H2, X1, X2, 1,
                                lowerY1 + startR, endR, paramStorage);
                        startY[0] = startR;
                        endY[0] = lowerY1 + startR;
                        startY[1] = lowerY1 + startR;
                        endY[1] = endR;
                    }

                    else
                    {//No boards found then split the Main ROI at the middle
                     //No image

                        H1 = ProbeVerticallyRotate90(captureBuffers[0], "H1", PalletDefect.DefectLocation.T_H1, X1, X2, 1,
                                startR, ((startR + endR) / 2), paramStorage);
                        H2 = ProbeVerticallyRotate90(captureBuffers[0], "H2", PalletDefect.DefectLocation.T_H2, X1, X2 , 1,
                                ((startR + endR) / 2), endR, paramStorage);

                        startY[0] = startR;
                        endY[0] = ((startR + endR) / 2);
                        startY[1] = ((startR + endR) / 2);
                        endY[1] = endR;
                    }
                    
                    Board H3 = ProbeVerticallyRotate90(captureBuffers[1], "H3", PalletDefect.DefectLocation.T_H3, X1, X2, 1,
                        (int)(ycs[1] - heights[1] / 2), (int)(ycs[1] + heights[1] / 2), paramStorage);
                    Board H4 = ProbeVerticallyRotate90(captureBuffers[2], "H4", PalletDefect.DefectLocation.T_H4, X1, X2, 1,
                        (int)(ycs[2] - heights[2] / 2), (int)(ycs[2] + heights[2] / 2), paramStorage);
                    Board H5 = ProbeVerticallyRotate90(captureBuffers[3], "H5", PalletDefect.DefectLocation.T_H5, X1, X2, 1,
                        (int)(ycs[3] - heights[3] / 2), (int)(ycs[3] + heights[3] / 2), paramStorage);
                    Board H6 = ProbeVerticallyRotate90(captureBuffers[4], "H6", PalletDefect.DefectLocation.T_H6, X1, X2, 1,
                        (int)(ycs[4] - heights[4] / 2), (int)(ycs[4] + heights[4] / 2), paramStorage);
                    Board H7 = ProbeVerticallyRotate90(captureBuffers[5], "H7", PalletDefect.DefectLocation.T_H7, X1, X2, 1,
                        (int)(ycs[5] - heights[5] / 2), (int)(ycs[5] + heights[5] / 2), paramStorage);

                    startY[2] = (int)(ycs[1] - heights[1] / 2);
                    endY[2] = (int)(ycs[1] + heights[1] / 2);
                    startY[3] = (int)(ycs[2] - heights[2] / 2);
                    endY[3] = (int)(ycs[2] + heights[2] / 2);
                    startY[4] = (int)(ycs[3] - heights[3] / 2);
                    endY[4] = (int)(ycs[3] + heights[3] / 2);
                    startY[5] = (int)(ycs[4] - heights[4] / 2);
                    endY[5] = (int)(ycs[4] + heights[4] / 2);
                    startY[6] = (int)(ycs[5] - heights[5] / 2);
                    endY[6] = (int)(ycs[5] + heights[5] / 2);

                    //define the bitmap
                    bitmap = new Bitmap(W[6], Y[6], PixelFormat.Format8bppIndexed);
                    //Convert the byte array to bitmap
                    ByteArray2bitmap(bytesIntensity[6], W[6], Y[6], bitmap);

                    // Define area of interest and cropp
                    startR = (int)(ycs[6] - heights[6] / 2);
                    endR = (int)(ycs[6] + heights[6] / 2);
                    cropRect = new Rectangle(startC, startR, endC - startC, endR - startR);
                    croppedBitmap = bitmap.Clone(cropRect, bitmap.PixelFormat);
                    Bitmap croppedBitmapT = model.ResizeBitmap(croppedBitmap, 2560, 688);

                    //Process the image for inference
                    imageData = model.ProcessBitmapForInference(croppedBitmapT, croppedBitmapT.Width, croppedBitmapT.Height);
                    results = model.RunInference(imageData, croppedBitmapT.Height, croppedBitmapT.Width);

                    //Get the results
                    boxes = results[0];
                    labels = results[1];
                    scores = results[2];
                    masks = results[3];

                    //Vizualice BB Results
                    string TrailingMainImage = "vizDL/TopSplit/Trailing.png";
                    if (isSaveTopSplitResults) {
                        croppedBitmapT.Save(TrailingMainImage, ImageFormat.Png);
                        model.DrawTopBoxes2(TrailingMainImage, boxes, scores, "BB_Trailing.png");
                    }

                    // Convert to32bppArgb to draw results and display it 
                    tempBitmap = new Bitmap(croppedBitmapT.Width, croppedBitmapT.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(tempBitmap))
                    {
                        g.DrawImage(croppedBitmap, new Rectangle(0, 0, croppedBitmap.Width, croppedBitmap.Height));
                    }
                    croppedBitmap.Dispose(); // Liberar el anterior
                    croppedBitmap = tempBitmap; // Usar el nuevo bitmap compatible

                    // Draw the segmented objects
                    binaryMasks = model.ConvertMasksToBinary(masks, croppedBitmap.Width, croppedBitmap.Height, scores.Length);
                    name = "Trailing";

                    img1Exist = false;
                    img2Exist = false;
                    upperY1 = 0;
                    upperY2 = 0;
                    lowerY1 = 0;
                    lowerY2 = 0;

                    model.SplitBoard2(croppedBitmap, boxes, scores, binaryMasks, name,
                      ref img1Exist, ref img2Exist, ref upperY1, ref upperY2, ref lowerY1, ref lowerY2,isSaveTopSplitResults);
                    Board H8 = null;
                    Board H9 = null;

                    ushort[] Clone2 = captureBuffers[6].Buf.Clone() as ushort[];

                    CaptureBuffer Clon2 = new CaptureBuffer
                    {
                        Buf = Clone2,
                        PaletteType = CaptureBuffer.PaletteTypes.Gray,
                        Width = croppedBitmap.Width,
                        Height = croppedBitmap.Height,

                    };

                    int ScaleValT = MainWindow.GetParamStorage(position).GetInt(StringsLocalization.SplitDL_ScaleY_Trailing);
                    upperY2 = model.ScaleVal(upperY2, 688, ScaleValT);
                    lowerY1 = model.ScaleVal(lowerY1, 688, ScaleValT);

                    if (img1Exist && img2Exist)
                    {//Both images exist Upper Y taked intop account
                        H8 = ProbeVerticallyRotate90(captureBuffers[6], "H8", PalletDefect.DefectLocation.T_H8, X1, X2, 1,
                              startR, upperY2 + startR, paramStorage);
                        H9 = ProbeVerticallyRotate90(captureBuffers[6], "H9", PalletDefect.DefectLocation.T_H9, X1, X2, 1,
                               lowerY1 + startR, endR, paramStorage);
                        startY[7] = startR;
                        endY[7] = upperY2 + startR;
                        startY[8] = lowerY1 + startR;
                        endY[8] = endR;
                    }

                    else if (img1Exist && !img2Exist)
                    {//Only Upper exist Y taked intop account  
                        H8 = ProbeVerticallyRotate90(captureBuffers[6], "H8", PalletDefect.DefectLocation.T_H8, X1, X2, 1,
                              startR, upperY2 + startR, paramStorage);
                        H9 = ProbeVerticallyRotate90(captureBuffers[6], "H9", PalletDefect.DefectLocation.T_H9, X1, X2, 1,
                               upperY2 + startR, endR, paramStorage);

                        startY[7] = startR;
                        endY[7] = upperY2 + startR;
                        startY[8] = upperY2 + startR;
                        endY[8] = endR;
                    }

                    else if (!img1Exist && img2Exist)
                    {//Only Lower exist lowerY taked intop account  
                        H8 = ProbeVerticallyRotate90(captureBuffers[6], "H8", PalletDefect.DefectLocation.T_H8, X1, X2, 1,
                              startR, lowerY1 + startR, paramStorage);
                        H9 = ProbeVerticallyRotate90(captureBuffers[6], "H9", PalletDefect.DefectLocation.T_H9, X1, X2, 1,
                                lowerY1 + startR, endR, paramStorage);

                        startY[7] = startR;
                        endY[7] = lowerY1 + startR;
                        startY[8] = lowerY1 + startR;
                        endY[8] = endR;
                    }

                    else
                    {//No boards found then split the Main ROI at the middle
                     //No image

                        H8 = ProbeVerticallyRotate90(captureBuffers[6], "H8", PalletDefect.DefectLocation.T_H8, X1, X2, 1,
                                startR, ((startR + endR) / 2), paramStorage);
                        H9 = ProbeVerticallyRotate90(captureBuffers[6], "H9", PalletDefect.DefectLocation.T_H9, X1, X2 , 1,
                                ((startR + endR) / 2), endR, paramStorage);

                        startY[7] = startR;
                        endY[7] = ((startR + endR) / 2);
                        startY[8] = ((startR + endR) / 2);
                        endY[8] = endR;
                    }

                    int numberOfWidths = 9; // Total Board
                    int[] expWidths = new int[numberOfWidths]; // for expected width
                    int ExpHeight = (int)MainWindow.GetParamStorage(position).GetPixX(StringsLocalization.HBoardLength_in);

                    for (int i = 1; i+2 <= numberOfWidths; i++)
                    {
                        string paramName = string.Format(StringsLocalization.TopHXBoardWidth_in, i);
                        expWidths[i - 1] = (int)MainWindow.GetParamStorage(position).GetPixY(paramName);
                    }

                    H1.ExpectedAreaPix = expWidths[0] * ExpHeight;
                    H2.ExpectedAreaPix = expWidths[1] * ExpHeight;
                    H3.ExpectedAreaPix = expWidths[2] * ExpHeight;
                    H4.ExpectedAreaPix = expWidths[3] * ExpHeight;
                    H5.ExpectedAreaPix = expWidths[4] * ExpHeight;
                    H6.ExpectedAreaPix = expWidths[5] * ExpHeight;
                    H7.ExpectedAreaPix = expWidths[6] * ExpHeight;
                    H8.ExpectedAreaPix = expWidths[7] * ExpHeight;
                    H9.ExpectedAreaPix = expWidths[8] * ExpHeight;

                    H1.XResolution = SourceCB.xScale;
                    H2.XResolution = SourceCB.xScale;
                    H2.XResolution = SourceCB.xScale;
                    H4.XResolution = SourceCB.xScale;
                    H5.XResolution = SourceCB.xScale;
                    H6.XResolution = SourceCB.xScale;
                    H7.XResolution = SourceCB.xScale;
                    H8.XResolution = SourceCB.xScale;
                    H9.XResolution = SourceCB.xScale;

                    H1.YResolution = SourceCB.yScale;
                    H2.YResolution = SourceCB.yScale;
                    H2.YResolution = SourceCB.yScale;
                    H4.YResolution = SourceCB.yScale;
                    H5.YResolution = SourceCB.yScale;
                    H6.YResolution = SourceCB.yScale;
                    H7.YResolution = SourceCB.yScale;
                    H8.YResolution = SourceCB.yScale;
                    H9.YResolution = SourceCB.yScale;

                    List<Board> boardsList = new List<Board>() { H1, H2 ,H3, H4, H5, H6, H7, H8, H9};
                    string palletClass = null;

                    if (MainWindow.PalletClassifier == 0) { palletClass = "International"; }

                    AssignHBoardsParameters(boardsList, position, palletClass);

                    H1.startY = startY[0];
                    H1.endY = endY[0];
                    H2.startY = startY[1];  
                    H2.endY = endY[1];
                    H3.startY = startY[2];
                    H3.endY = endY[2];
                    H4.startY = startY[3];
                    H4.endY = endY[3];
                    H5.startY = startY[4];
                    H5.endY = endY[4];
                    H6.startY = startY[5];
                    H6.endY = endY[5];
                    H7.startY = startY[6];
                    H7.endY = endY[6];
                    H8.startY = startY[7];
                    H8.endY = endY[7];
                    H9.startY = startY[8];
                    H9.endY = endY[8];

                    BList.Add(H1);
                    BList.Add(H2);
                    BList.Add(H3);
                    BList.Add(H4);
                    BList.Add(H5);
                    BList.Add(H6);
                    BList.Add(H7);
                    BList.Add(H8);
                    BList.Add(H9);
                }

                catch (Exception ex)
                {
                    MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
                    UpdateTextBlock(mainWindow.LogText, ex.Message, MessageState.Warning);
                    MessageBox.Show(ex.Message);
                    Logger.WriteLine("Error in processing boards: " + ex.Message);
                    throw new InvalidOperationException("An error occurred while processing the boards.", ex);
                }              
            }

            for (int i = 0; i < iCount+2; i++)
            {
                Board B = BList[i]; // Get the current board from the list

                // Check if there are enough edge points in the first set
                if (B.Edges[0].Count < 30)
                {
                    // If not, mark the board as defective due to insufficient edge information
                    AddDefect(B, PalletDefect.DefectType.board_segmentation_error, "Too little edge info on " + B.BoardName);
                    return; // Exit the loop and function
                }

                // Loop to adjust edge points if the width difference exceeds the threshold
                for (int j = 30; j >= 0; j--)
                {
                    // Check if the width difference at index j is greater than 1.2 times the width difference at the next index
                    if ((B.Edges[1][j].Y - B.Edges[0][j].Y) > ((B.Edges[1][j + 1].Y - B.Edges[0][j + 1].Y) * 1.2f))
                    {
                        // Adjust the edge points at index j to match the next index (j + 1)
                        B.Edges[0][j] = B.Edges[0][j + 1];
                        B.Edges[1][j] = B.Edges[1][j + 1];
                    }
                }

                // Another loop to adjust edge points, similar to the previous one
                for (int j = 30; j > 0; j--)
                {
                    int w = B.Edges[0].Count; // Get the number of edge points in the first set
                                              // Check if the width difference at index (w - j) is greater than 1.2 times the width difference at the previous index (w - j - 1)
                    if ((B.Edges[1][w - j].Y - B.Edges[0][w - j].Y) > ((B.Edges[1][w - j - 1].Y - B.Edges[0][w - j - 1].Y) * 1.2f))
                    {
                        // Adjust the edge points at index (w - j) to match the previous index (w - j - 1)
                        B.Edges[0][w - j] = B.Edges[0][w - j - 1];
                        B.Edges[1][w - j] = B.Edges[1][w - j - 1];
                    }
                }
            }

            // Analyze the boards using Task instead of Thread
            List<Task> boardTasks = new List<Task>();

            for (int i = 0; i < BList.Count; i++)
            {
                // 为每个板子创建一个Task，并启动ProcessBoard
                var board = BList[i];
                boardTasks.Add(Task.Run(() => ProcessBoard(board, paramStorage, i, Pos)));
            }

            // 等待所有任务完成
            Task.WaitAll(boardTasks.ToArray()); // 同步等待所有任务完成


            //for (int i = 0; i < BList.Count; i++)
            //{
            //    Board B = BList[i];

            //    CaptureBuffer CB = new CaptureBuffer(BList[i].CrackCB);
            //    AddCaptureBuffer(B.BoardName + " Crack M", CB);

            //    // Draw the cracks
            //    int ny = B.CrackTracker.GetLength(0);
            //    int nx = B.CrackTracker.GetLength(1);
            //    float MaxVal = B.CrackBlockSize * B.CrackBlockSize;
            //    for (int y = 0; y < ny; y++)
            //    {
            //        for (int x = 0; x < nx; x++)
            //        {
            //            if (B.CrackTracker[y, x] != 0)
            //            {
            //                int V = B.CrackTracker[y, x];
            //                int x1 = B.BoundsP1.X + (x * B.CrackBlockSize);
            //                int y1 = B.BoundsP1.Y + (y * B.CrackBlockSize);

            //                float pct = B.CrackTracker[y, x] / MaxVal;
            //                //if (pct > BlockCrackMinPct)
            //                {
            //                    //DrawBlock(B.CrackBuf, x1, y1, x1 + B.CrackBlockSize, y1 + B.CrackBlockSize, 16 );

            //                    // TODO-UNCOMMENT
            //                    //MakeBlock(B.CrackCB, x1 + 8, y1 + 8, 16, (UInt16)(B.CrackTracker[y, x] * 100));
            //                    MakeBlock(B.CrackCB, x1 + 8, y1 + 8, 16, (UInt16)(B.CrackTracker[y, x] * 100));
            //                }
            //            }

            //            if (B.BoundaryBlocks[y, x] != 0)
            //            {
            //                int x1 = B.BoundsP1.X + (x * B.CrackBlockSize);
            //                int y1 = B.BoundsP1.Y + (y * B.CrackBlockSize);

            //                //DrawBlock(B.CrackCB, x1, y1, x1 + B.CrackBlockSize, y1 + B.CrackBlockSize, 5500);
            //                MakeBlock(B.CrackCB, x1 + 8, y1 + 8, 16, 5500);
            //            }

            //        }
            //    }

            //    AddCaptureBuffer(B.BoardName + " Crack T", BList[i].CrackCB);
            //}
        }


        //=====================================================================
        private void DrawBlock(CaptureBuffer CB, int x1, int y1, int x2, int y2, UInt16 Val)
        {
            UInt16[] buf = CB.Buf;
            int W = CB.Width;


            try
            {
                for (int y = y1; y <= y2; y++)
                {
                    //V = buf[y * W + x1];
                    //if (V < 32) buf[y * W + x1] = (UInt16)(Val);
                    buf[y * W + x1] = (UInt16)(Val);

                    //V = buf[y * W + x2];
                    //if (V < 32) buf[y * W + x2] = (UInt16)(Val);
                    buf[y * W + x2] = (UInt16)(Val);
                }
                for (int x = x1; x <= x2; x++)
                {
                    //V = buf[y1 * W + x];
                    //if (V < 32) buf[y1 * W + x] = (UInt16)(Val);
                    buf[y1 * W + x] = (UInt16)(Val);

                    //V = buf[y2 * W + x];
                    //if (V < 32) buf[y2 * W + x] = (UInt16)(Val);
                    buf[y2 * W + x] = (UInt16)(Val);
                }
            }
            catch
            {
            }
        }

        private void AssignHBoardsParameters(List<Board> boardsList, PositionOfPallet position, string palletClass)
        {
            int i = 1;
            foreach (var board in boardsList)
            {               
                board.MinWidthForChunk = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.HXMissingChunkMaximumWidth, i, palletClass));
                board.MinLengthForChunk = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.HXMissingChunkMaximumLength, i, palletClass));
                board.MaxAllowable = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.HXMaxAllowedMissingWood, i, palletClass));
                board.ExpLength = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.HBoardLength_in, palletClass));
                i++;
            }

            boardsList[0].MinWidthForChunkAcrossLength = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.H1BoardMinimumWidthAcrossLength, palletClass));
            boardsList[boardsList.Count - 1].MinWidthForChunkAcrossLength = MainWindow.GetParamStorage(position).GetFloat(String.Format(StringsLocalization.H9BoardMinimumWidthAcrossLength, palletClass));
        }

        private void ProcessBoard(object _B,ParamStorage paramStorage, int i,bool Pos)
        {
            Board B = (Board)_B;

            if (UseBackgroundThread)
            {
                ThreadPriority TPBackup = Thread.CurrentThread.Priority;
                //Logger.WriteLine("ProcessBoard PRIORITY: " + TPBackup.ToString());
                Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            }

            try
            {
                FindCracks(B, paramStorage);
                if (!isDeepLActive)
                {
                    FindRaisedNails(B, paramStorage);
                }
                else
                {
                    //FindRaisedNailsDL(B, paramStorage,defectRNWHCO, i, Pos);
                }


                CalculateMissingWood(B, paramStorage);
                CheckForBreaks(B, paramStorage);
                FindRaisedBoard(B, paramStorage);  

                // CheckNarrowBoard commented out since is not required by client
                // Jack Note: Check if the board is too narrow based on the minimum width parameter
                /*bool TooNarrow = CheckNarrowBoard(paramStorage,B, (float)(B.MinWidthTooNarrow), 0.5f, true);
                if (TooNarrow)
                {
                    // Jack Note: Mark the board as defective for being too narrow and set defect marker
                    AddDefect(B, PalletDefect.DefectType.board_too_narrow, "Min width was less than expected width " + B.MinWidthTooNarrow + "(in)");
                    SetDefectMarker(B);
                }*/
                // Jack Note: Check if the board has a missing chunk of wood based on the minimum width and length parameters
                bool MissingWoodChunk = CheckNarrowBoard(paramStorage,B, (float)(B.MinWidthForChunk ), (float)(B.MinLengthForChunk), true);
                if (MissingWoodChunk)
                {
                    //  Mark the board as defective for having a missing chunk and set defect marker
                    AddDefect(B, PalletDefect.DefectType.missing_wood, "Missing wood too deep <" + B.MinWidthForChunk + "(in) and too long >"  + B.MinLengthForChunk+"(in)");
                    SetDefectMarker(B);
                }

                // New 2025 for missing wood across the length (set 90% of the total length here)///////
                // It depend on the calibr 
                if (B.MinWidthForChunkAcrossLength != 0)
                {
                    double percentage;
                    bool MissingWoodAcrossLength = CheckNarrowBoardHUB(paramStorage, B, (float)(B.MinWidthForChunkAcrossLength), (float)(B.ExpLength), true);
                    if (MissingWoodAcrossLength)
                    {                  
                        AddDefect(B, PalletDefect.DefectType.missing_wood_width_across_length, "Missing chunk width <" + B.MinWidthForChunkAcrossLength + "(in) Across the lengh ");
                        SetDefectMarker(B);
                    }
                }           

            }

            catch (Exception E)
            {
                Logger.WriteException(E);            
                AddDefect(B, PalletDefect.DefectType.board_segmentation_error, "Exception thrown in ProcessBoard()");
                return;
            }
        }

        //=====================================================================
        private void DrawContours(Board B, string filename)
        {
            int imgWidth = 2500; // Ajusta al tamaño real de la tabla
            int imgHeight = 2700;

            Bitmap img = new Bitmap(imgWidth, imgHeight);
            using (Graphics g = Graphics.FromImage(img))
            {
                g.Clear(Color.White);
                Pen pen = new Pen(Color.Red, 2);

                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    int x0 = B.Edges[0][i].X;
                    int y0 = B.Edges[0][i].Y;
                    int x1 = B.Edges[1][i].X;
                    int y1 = B.Edges[1][i].Y;

                    g.DrawLine(pen, x0, y0, x1, y1);
                }
            }

            img.Save(filename);
           // Console.WriteLine($"Imagen de contornos guardada como {filename}");
        }
        private bool CheckNarrowBoardHUB(ParamStorage paramStorage, Board B, float FailWidIn, float WoodLengthIn, bool ExcludeEnds = false)
        {
            // Jack Note: Initialize variables for pixels per inch (PPI) and measured length
            float PPIX;
            float PPIY;
            float MissingWoodLength = 0;

            //  Retrieve PPI values from parameter storage
            PPIY = paramStorage.GetPPIY();
            PPIX = paramStorage.GetPPIX();

            //  Get dimensions of the crack tracker array
            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);

            //  Initialize the counter for bad edges
            int nBadEdges = 0;

            // Determine exclusion length based on whether ends should be excluded
            int Exclusion = ExcludeEnds ? 60 : 0;

            //  Check if the board is oriented horizontally
            if (w > h)
            {
                Console.WriteLine("Horizontally oriented");
                // Jack Note: Fail width is already in pixels (not converting to pixels)
                Console.WriteLine("FailWidIn: " + FailWidIn);
                // Jack Note: Calculate the fail width in pixels for horizontal orientation
                int FailWid = (int)(FailWidIn * PPIY);

                // Jack Note: Iterate through the edges and count bad edges
                for (int i = Exclusion; i < B.Edges[0].Count - Exclusion; i++)
                {
                    int DY = B.Edges[1][i].Y - B.Edges[0][i].Y;

                    // Jack Note: Increment the bad edges counter if the width is less than the fail width
                    if (DY < FailWid)
                    {
                        nBadEdges++;
                      
                    }
                }

                //  Calculate the measured length in inches for horizontal orientation
                MissingWoodLength = nBadEdges / PPIX;
                Console.WriteLine("nBadEdges: " + nBadEdges);
                Console.WriteLine("MeasuredLength: " + MissingWoodLength);
            }

            else
            {
                //  Calculate the fail width in pixels for vertical orientation
                int FailWid = (int)(FailWidIn * PPIX);

                //  Iterate through the edges and count bad edges
                for (int i = Exclusion; i < B.Edges[0].Count - Exclusion; i++)
                {
                    int DX = B.Edges[1][i].X - B.Edges[0][i].X;

                    //  Increment the bad edges counter if the width is less than the fail width
                    if (DX < FailWid)
                    {
                        nBadEdges++;                      
                    }
                }
                // Jack Note: Calculate the measured length in inches for vertical orientation
                MissingWoodLength = nBadEdges / PPIY;
            }
            //DrawContours(B, "contours.png");
            // this offset has been used because calibration values aren\t good
            MissingWoodLength = MissingWoodLength +  (float)(3);
            return (MissingWoodLength >= WoodLengthIn);
            // Console.WriteLine("MeasuredLength: " + WoodLengthIn);
        }

        // Jack Note: Count the Number of the distance between the Edge0 and Edge1 < FailWidth
        private bool CheckNarrowBoard(ParamStorage paramStorage,Board B, float FailWidIn, float MinMissingWoodLengthIn, bool ExcludeEnds = false)
        {
            // Jack Note: Initialize variables for pixels per inch (PPI) and measured length
            float PPIX;
            float PPIY;
            float MeasuredLength = 0;

            // Jack Note: Retrieve PPI values from parameter storage
            PPIY = paramStorage.GetPPIY();
            PPIX = paramStorage.GetPPIX();

            // Jack Note: Get dimensions of the crack tracker array
            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);

            // Jack Note: Initialize the counter for bad edges
            int nBadEdges = 0;

            // Jack Note: Determine exclusion length based on whether ends should be excluded
            int Exclusion = ExcludeEnds ? 60 : 0;

            // Jack Note: Check if the board is oriented horizontally
            if (w > h)
            {
                // Jack Note: Calculate the fail width in pixels for horizontal orientation
                int FailWid = (int)(FailWidIn* PPIY);

                // Jack Note: Iterate through the edges and count bad edges
                for (int i = Exclusion; i < B.Edges[0].Count - Exclusion; i++)
                {
                    int DY = B.Edges[1][i].Y - B.Edges[0][i].Y;

                    // Jack Note: Increment the bad edges counter if the width is less than the fail width
                    if (DY < FailWid)
                    {
                        nBadEdges++;
                    }
                }
                // Jack Note: Calculate the measured length in inches for horizontal orientation
                MeasuredLength = nBadEdges / PPIX;
            }
            else
            {
                // Jack Note: Calculate the fail width in pixels for vertical orientation
                int FailWid = (int)(FailWidIn* PPIX);

                // Jack Note: Iterate through the edges and count bad edges
                for (int i = Exclusion; i < B.Edges[0].Count - Exclusion; i++)
                {
                    int DX = B.Edges[1][i].X - B.Edges[0][i].X;

                    // Jack Note: Increment the bad edges counter if the width is less than the fail width
                    if (DX < FailWid)
                    {
                        nBadEdges++;
                    }
                }
                // Jack Note: Calculate the measured length in inches for vertical orientation
                MeasuredLength = nBadEdges / PPIY;
            }

            // Jack Note: Return true if the measured length of bad edges exceeds the minimum missing wood length
            return (MeasuredLength > MinMissingWoodLengthIn);

        }

        //=====================================================================
        private void FindCracks(Board B,ParamStorage paramStorage)
        {
            PalletPoint P1 = FindBoardMinXY(B);
            PalletPoint P2 = FindBoardMaxXY(B);
            P1.X--;
            P1.Y--;
            P2.X++;
            P2.Y++;
            B.BoundsP1 = P1;
            B.BoundsP2 = P2;

            B.CrackCB = new CaptureBuffer(B.CB);
            B.CrackCB.ClearBuffer();

            B.CrackBlockSize = paramStorage.GetInt(StringsLocalization.CrackTrackerBlockSize);

            int MinDelta = paramStorage.GetInt(StringsLocalization.CrackTrackerMinDelta);

            // Divide into blocks
            int nx = ((P2.X - P1.X) / B.CrackBlockSize) + 1;
            int ny = ((P2.Y - P1.Y) / B.CrackBlockSize) + 1;
            B.CrackTracker = new int[ny, nx];
            B.BoundaryBlocks = new int[ny, nx];

            int cx;
            int cy;

            // Jack Note: Find the pixels that much different from the Surrounding 7*7 pixles 
            try
            {
                for (int y = P1.Y; y <= P2.Y; y++)
                {
                    for (int x = P1.X; x <= P2.X; x++)
                    {
                        cx = (x - P1.X) / B.CrackBlockSize;
                        cy = (y - P1.Y) / B.CrackBlockSize;

                        //UInt16 Val1 = B.CB.Buf[y * MainWindow.SensorWidth + x];
                        //UInt16 Val2x = B.CB.Buf[y * MainWindow.SensorWidth + (x + 2)];
                        //UInt16 Val2y = B.CB.Buf[(y + 2) * MainWindow.SensorWidth + x];
                        //UInt16 Val3x = (UInt16)(Math.Abs((int)Val1 - (int)Val2x));
                        //UInt16 Val3y = (UInt16)(Math.Abs((int)Val1 - (int)Val2y));
                        //if ((Val3x > MinDelta) || (Val3y > MinDelta))
                        //{
                        //    B.CrackBuf[y * MainWindow.SensorWidth + x] = Math.Max(Val3x, Val3y);
                        //    B.BoundaryBlocks[cy, cx] = 1;
                        //}

                        UInt16 Val1 = B.CB.Buf[y * B.CB.Width + x];
                        UInt16 MaxDif = 0;

                        for (int yy = y - 7; yy < (y + 7); yy++)
                        {
                            for (int xx = x - 7; xx < (x + 7); xx++)
                            {
                                UInt16 testVal = (UInt16)(Math.Abs((int)B.CB.Buf[yy * B.CB.Width + xx] - Val1));
                                MaxDif = testVal > MaxDif ? testVal : MaxDif;
                            }
                        }

                        //UInt16 Val2x = B.CB.Buf[y * MainWindow.SensorWidth + (x + 5)];
                        //UInt16 Val2y = B.CB.Buf[(y + 5) * MainWindow.SensorWidth + x];
                        //UInt16 Val3x = (UInt16)(Math.Abs((int)Val1 - (int)Val2x));
                        //UInt16 Val3y = (UInt16)(Math.Abs((int)Val1 - (int)Val2y));

                        // if ((Val3x > MinDelta) || (Val3y > MinDelta))
                        if (MaxDif > MinDelta)
                        {
                            //B.CrackBuf[y * MainWindow.SensorWidth + x] = Math.Max(Val3x, Val3y);
                            B.CrackCB.Buf[y * B.CrackCB.Width + x] = MaxDif;
                            B.BoundaryBlocks[cy, cx] = 1;
                        }
                    }
                }
            }

            catch (Exception)
            {
                return;
            }
            // P1
            //+----------------------------------+
            //|                                  |
            //|    (x-7, y-7)         (x+7, y-7) |
            //|       +------------------+       |
            //|       |                  |       |
            //|       |     (x, y)       |       |
            //|       |       *          |       |
            //|       |                  |       |
            //|       +------------------+       |
            //|    (x-7, y+7)         (x+7, y+7) |
            //|                                  |
            //+----------------------------------+
            //                                    P2

            NullifyNonROIAreas(B, paramStorage);
            // Remove speckle
            // Jack Note: Filter
            DeNoiseInPlace(B.CrackCB,paramStorage);

            // Jack Note: Set the edge to zero
            for (int y = P1.Y; y <= P2.Y; y++)
            {
                for (int x = P1.X; x <= P2.X; x++)
                {
                    cx = (x - P1.X) / B.CrackBlockSize;
                    cy = (y - P1.Y) / B.CrackBlockSize;

                    UInt16 Val1 = B.CrackCB.Buf[y * B.CrackCB.Width + x];
                    if (Val1 > 0)
                    {
                        B.CrackTracker[cy, cx]++;
                    }
                }
            }

            // Clean up
            if (ny > nx)    // Vertical board
            {
                for (int x = 0; x < nx; x++)
                {
                    B.CrackTracker[0, x] = 0;
                    B.CrackTracker[ny - 1, x] = 0;
                    B.CrackTracker[1, x] = 0;
                    B.CrackTracker[ny - 2, x] = 0;
                }

                for (int y = 0; y < ny; y++)
                {
                    int sx = -1;
                    int ex = -1;
                    for (int x = 0; x < nx; x++)
                    {
                        if (B.BoundaryBlocks[y, x] == 1)
                        {
                            sx = x;
                            break;
                        }
                    }
                    for (int x = nx - 1; x >= 0; x--)
                    {
                        if (B.BoundaryBlocks[y, x] == 1)
                        {
                            ex = x;
                            break;
                        }
                    }

                    if ((sx != -1) && (ex != -1))
                    {

                        for (int i = sx + 1; i < ex; i++)
                        {
                            B.BoundaryBlocks[y, i] = 0;
                        }

                        B.CrackTracker[y, sx] = 0;
                        B.CrackTracker[y, ex] = 0;
                    }
                }
            }
            else // Horizontal board
            {
                for (int y = 0; y < ny; y++)
                {
                    B.CrackTracker[y, 0] = 0;
                    B.CrackTracker[y, nx - 1] = 0;
                    B.CrackTracker[y, 1] = 0;
                    B.CrackTracker[y, nx - 2] = 0;
                }

                for (int x = 0; x < nx; x++)
                {
                    int sy = -1;
                    int ey = -1;
                    for (int y = 0; y < ny; y++)
                    {
                        if (B.BoundaryBlocks[y, x] == 1)
                        {
                            sy = y;
                            break;
                        }
                    }
                    for (int y = ny - 1; y >= 0; y--)
                    {
                        if (B.BoundaryBlocks[y, x] == 1)
                        {
                            ey = y;
                            break;
                        }
                    }

                    if ((sy != -1) && (ey != -1))
                    {
                        for (int i = sy + 1; i < ey; i++)
                        {
                            B.BoundaryBlocks[i, x] = 0;
                        }

                        B.CrackTracker[sy, x] = 0;
                        B.CrackTracker[ey, x] = 0;
                    }
                }
            }

            // Cement the crack blocks based on the params
            /// Jack Note
            /// This code iterates through each crack block, calculates the percentage of crack pixels in each block,
            /// and determines which blocks are valid based on a preset minimum crack percentage parameter. 
            /// Valid crack blocks are marked as 1, and invalid crack blocks are marked as 0.
            float BlockCrackMinPct = paramStorage.GetFloat(StringsLocalization.CrackTrackerMinPct);
            float MaxVal = B.CrackBlockSize * B.CrackBlockSize;
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float pct = B.CrackTracker[y, x] / MaxVal;
                    if (pct > BlockCrackMinPct)
                        B.CrackTracker[y, x] = 1;
                    else
                        B.CrackTracker[y, x] = 0;
                }
            }

            // Now flood fill each crack and measure 
            FloodFillCracks(B, nx > ny);

            // Now check to see if it's broken
            IsCrackABrokenBoard(B, paramStorage);

        }

        //=====================================================================
        private void FloodFillCracks(Board B, bool IsHorizontal)
        {

            ///Jack Note
            ///This code first initializes all crack cells to 1, indicating they have not been processed. 
            ///It then performs a flood fill operation, assigning a unique code to each connected region of cracks.
            ///The FloodFillCrack function is called to mark all connected crack cells with the same flood code, 
            ///allowing for identification and differentiation of separate crack regions.
            int FloodCode = 2;

            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);

            // Initialize all crack cells to '1' -- not yet flooded
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (B.CrackTracker[y, x] > 0)
                        B.CrackTracker[y, x] = 1;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (B.CrackTracker[y, x] == 1)
                    {
                        B.CrackTracker[y, x] = FloodCode++;
                        FloodFillCrack(B, x, y);
                    }
                }
            }

            // Logger.WriteLine(B.BoardName + " has " + FloodCode.ToString() + " unique cracks");
        }

        //=====================================================================
        void NullifyNonROIAreas(Board B,ParamStorage paramStorage)
        {
            float EdgeWidH = paramStorage.GetFloat(StringsLocalization.HBoardEdgeCrackExclusionZonePercentage);
            float EdgeWidV = paramStorage.GetFloat(StringsLocalization.VBoardEdgeCrackExclusionZonePercentage);
            float CenWid = paramStorage.GetFloat(StringsLocalization.BoardCenterCrackExclusionZonePercentage);
            // float CrackBlockValueThreshold = ParamStorage.GetFloat("Crack Block Value Threshold");

            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);
            int CBW = B.CrackCB.Width;

            bool isHoriz = w > h;

            if (isHoriz)
            {
                float Len = paramStorage.GetPixX(StringsLocalization.HBoardLength_in);

                int x1 = 0;// B.Edges[0][0].X;
                int x2 = B.Edges[0].Count;// B.Edges[0][B.Edges[0].Count - 1].X;
                int xc = (x1 + x2) / 2;

                int edge1 = (int)(x1 + (Len * EdgeWidH));
                int edge2 = (int)(xc - ((Len * CenWid) / 2));
                int edge3 = (int)(xc + ((Len * CenWid) / 2));
                int edge4 = (int)(x2 - (Len * EdgeWidH));

                for (int x = x1; x < edge1; x++)
                {
                    for (int y = B.BoundsP1.Y; y <= B.BoundsP2.Y; y++)
                    {
                        B.CrackCB.Buf[y * CBW + B.Edges[0][x].X] = 0;
                    }
                }
                for (int x = edge2; x <= edge3; x++)
                {
                    for (int y = B.BoundsP1.Y; y <= B.BoundsP2.Y; y++)
                    {
                        B.CrackCB.Buf[y * CBW + B.Edges[0][x].X] = 0;
                    }
                }
                for (int x = edge4; x < x2; x++)
                {
                    for (int y = B.BoundsP1.Y; y <= B.BoundsP2.Y; y++)
                    {
                        B.CrackCB.Buf[y * CBW + B.Edges[0][x].X] = 0;
                    }
                }
            }
            else
            {
                float Len = paramStorage.GetPixY(StringsLocalization.VBoardLength_in);

                int y1 = 0;// B.Edges[0][0].X;
                int y2 = B.Edges[0].Count;// B.Edges[0][B.Edges[0].Count - 1].X;
                int yc = (y1 + y2) / 2;

                int edge1 = (int)(y1 + (Len * EdgeWidV));
                int edge2 = (int)(yc - ((Len * CenWid) / 2));
                int edge3 = (int)(yc + ((Len * CenWid) / 2));
                int edge4 = (int)(y2 - (Len * EdgeWidV));

                for (int y = y1; y < edge1; y++)
                {
                    //for (int x = B.Edges[0][y].X; x <= B.Edges[1][y].X; x++)
                    for (int x = B.BoundsP1.X; x < B.BoundsP2.X; x++)
                    {
                        B.CrackCB.Buf[(B.BoundsP1.Y + y) * CBW + x] = 0;
                    }
                }
                for (int y = edge2; y <= edge3; y++)
                {
                    //for (int x = B.Edges[0][y].X; x <= B.Edges[1][y].X; x++)
                    for (int x = B.BoundsP1.X; x < B.BoundsP2.X; x++)
                    {
                        B.CrackCB.Buf[B.Edges[0][y].Y * CBW + x] = 0;
                    }
                }
                for (int y = edge4; y < y2; y++)
                {
                    //for (int x = B.Edges[0][y].X; x <= B.Edges[1][y].X; x++)
                    for (int x = B.BoundsP1.X; x < B.BoundsP2.X; x++)
                    {
                        B.CrackCB.Buf[B.Edges[0][y].Y * CBW + x] = 0;
                    }
                }
            }
        }

        //=====================================================================
        void IsCrackABrokenBoard(Board B,ParamStorage paramStorage)
        {
            /// Jack Note
            /// The code checks for cracks that span the entire width or height 
            /// of the board by looking for connections between the edges.
            /// If a crack is found that connects one edge to the opposite edge 
            /// and meets certain conditions, it marks the board as defective.
            int BlockSize = paramStorage.GetInt(StringsLocalization.CrackTrackerBlockSize);
            //float MaxDebrisPc = ParamStorage.GetFloat("Crack Block Debris Check Pct");
            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);
            bool isHoriz = w > h;
            int[] TouchesEdge1 = new int[100];
            int[] TouchesEdge2 = new int[100];

            // New approach...look for cracks that connect edge to edge
            if (isHoriz)
            {
                // HACK
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (B.CrackTracker[y, x] > 0)
                        {
                            int cid = B.CrackTracker[y, x];

                            //if ((y == 0) || (y == 1) || (y == (B.CrackTracker.GetLength(0) - 1)) || (y == (B.CrackTracker.GetLength(0) - 2)))
                            if ((y == 0))
                                TouchesEdge1[cid] = y;
                            else
                            if ((y == (h - 1)))
                                TouchesEdge2[cid] = y;
                            else
                            {
                                if ((B.BoundaryBlocks[y - 1, x] == 1))// && (B.BoundaryBlocks[y + 1, x] == 0) && (B.BoundaryBlocks[y + 1, x] == 0))
                                    TouchesEdge1[cid] = y;

                                if ((B.BoundaryBlocks[y + 1, x] == 1))// && (B.BoundaryBlocks[y - 1, x] == 0) && (B.BoundaryBlocks[y - 1, x] == 0))
                                    TouchesEdge2[cid] = y;
                            }
                        }
                    }
                }
            }

            else
            {
                for (int y = 2; y < h - 2; y++)
                {
                    for (int x = 2; x < w - 2; x++)
                    {
                        if (B.CrackTracker[y, x] > 0)
                        {
                            int cid = B.CrackTracker[y, x];

                            //if ((x == 0) || (x == 1) || (x == (B.CrackTracker.GetLength(1) - 1)) || (x == (B.CrackTracker.GetLength(1) - 2)))
                            if ((x == 0))
                                TouchesEdge1[cid] = x;
                            else
                            if (x == (w - 1))
                                TouchesEdge2[cid] = x;
                            else
                            {
                                if ((B.BoundaryBlocks[y, x - 1] == 1) || (B.BoundaryBlocks[y, x - 2] == 1))// && (B.BoundaryBlocks[y, x + 1] == 0) && (B.BoundaryBlocks[y, x + 1] == 0))
                                    TouchesEdge1[cid] = x;

                                if ((B.BoundaryBlocks[y, x + 1] == 1) || (B.BoundaryBlocks[y, x + 2] == 1))// && (B.BoundaryBlocks[y, x - 1] == 0) && (B.BoundaryBlocks[y, x - 1] == 0))
                                    TouchesEdge2[cid] = x;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < TouchesEdge1.Length; i++)
            {
                if ((TouchesEdge1[i] != 0) && (TouchesEdge2[i] != 0))
                {
                    if (Math.Abs(TouchesEdge1[i] - TouchesEdge2[i]) > 5)  // Jack change the value from 5 to 25
                    {
                        AddDefect(B, PalletDefect.DefectType.broken_across_width, String.Format("Crack touches both edges."));
                        SetDefectMarker(B);
                        return;
                    }
                }
            }

            return;    
        }


        //=====================================================================
        // Recursive flood fill
        private void FloodFillCrack(Board B, int x, int y)
        {
            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);

            // Keep in bounds
            if ((x < 0) || (x >= w) ||
                 (y < 0) || (y >= h))
                return;

            int floodCode = B.CrackTracker[y, x];

            for (int i = 0; i < 8; i++)
            {
                int tx = 0, ty = 0;

                switch (i)
                {
                    case 0: tx = x - 1; ty = y - 1; break;
                    case 1: tx = x - 0; ty = y - 1; break;
                    case 2: tx = x + 1; ty = y - 1; break;
                    case 3: tx = x - 1; ty = y - 0; break;
                    case 4: tx = x + 1; ty = y - 0; break;
                    case 5: tx = x - 1; ty = y + 1; break;
                    case 6: tx = x - 0; ty = y + 1; break;
                    case 7: tx = x + 1; ty = y + 1; break;
                }

                if ((tx < 0) || (tx >= w) ||
                     (ty < 0) || (ty >= h))
                    continue;

                if ((B.CrackTracker[ty, tx] > 0) && (B.CrackTracker[ty, tx] != floodCode))
                {
                    B.CrackTracker[ty, tx] = floodCode;
                    FloodFillCrack(B, tx, ty);
                }
            }
        }


        //=====================================================================
        private void CheckForBreaks(Board B,ParamStorage paramStorage)
        {

            // Jack Note: Check if there are enough edges detected to proceed
            if (B.Edges[0].Count < 5)
            {
                // Jack Note: Not enough edges, mark the board as missing and set defect marker
                AddDefect(B, PalletDefect.DefectType.missing_board, "Segmented board far too short");
                SetDefectMarker(B);
                return;
            }

            // Jack Note: Retrieve the minimum board length percentage from parameter storage
            float BoardLenPerc = paramStorage.GetFloat(StringsLocalization.MinBoardLength) / 100.0f;
            float BoardLenPerc100 = (int)(BoardLenPerc * 100);
            // Jack Note: Check if the board is oriented horizontally
            if (B.Edges[0][0].X == B.Edges[1][0].X)
            {
                // Jack Note: Retrieve the expected horizontal board length and calculate minimum acceptable length
                int Len = (int)paramStorage.GetPixX(StringsLocalization.HBoardLength_in);
                int MinLen = (int)(Len * BoardLenPerc);

                // Jack Note: Calculate the measured length of the board
                int MeasLen = B.Edges[0][B.Edges[0].Count - 1].X - B.Edges[0][0].X;

                // Jack Note: Check if the measured length is less than the minimum acceptable length

                if (MeasLen < MinLen)
                {
                    // Jack Note: Mark the board as too short and set defect marker
                    AddDefect(B, PalletDefect.DefectType.board_too_short, "Board len " + ((int)(MeasLen * B.XResolution)).ToString() +
                        "(mm) shorter than " + ((int)(MinLen * B.XResolution)).ToString() + "(mm) (" + BoardLenPerc100.ToString() + "% of the Lenth)");
                    SetDefectMarker(B);
                    return;
                }
            }
            else
            {
                // Jack Note: Board is vertically oriented
                // Jack Note: Retrieve the expected vertical board length and calculate minimum acceptable length
                int Len = (int)paramStorage.GetPixY(StringsLocalization.VBoardLength_in);
                int MinLen = (int)(Len * BoardLenPerc);

                // Jack Note: Calculate the measured length of the board
                int MeasLen = B.Edges[0][B.Edges[0].Count - 1].Y - B.Edges[0][0].Y;

                // Jack Note: Check if the measured length is less than the minimum acceptable length
                if (MeasLen < MinLen)
                {
                    // Jack Note: Mark the board as too short and set defect marker
                    AddDefect(B, PalletDefect.DefectType.board_too_short, "Board len " + ((int)(MeasLen * B.YResolution)).ToString() + 
                        "(mm) shorter than " + ((int)(MinLen * B.YResolution)).ToString()+ "(mm) (" + BoardLenPerc100.ToString() + "% of the Length)");
                    SetDefectMarker(B);
                    return;
                }
            }

        }

        //=====================================================================
        private void CalculateMissingWood(Board B,ParamStorage paramStorage)
        {

            // Jack Note: Initialize variables for total area and capture buffer width
            int TotalArea = 0;
            int CBW = B.CB.Width;

            // Jack Note: Check if there are enough edges detected to proceed
            if (B.Edges[0].Count < 5)
            {
                // Jack Note: Not enough edges, set measured area to 0 and exit function
                B.MeasuredAreaPix = 0;
                return;
            }

            // Jack Note: Check if the board is horizontal
            if (B.Edges[0][0].X == B.Edges[1][0].X)
            {
                // Jack Note: Iterate through the detected edges to calculate the area for a horizontal board
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    for (int y = B.Edges[0][i].Y; y <= B.Edges[1][i].Y; y++)
                    {
                        // Jack Note: Calculate the index in the buffer and retrieve the pixel value
                        int Idx = y * CBW + B.Edges[0][i].X;
                        UInt16 Val = B.CB.Buf[Idx];

                        // Jack Note: Increment the total area if the pixel value is greater than 0
                        if (Val > 0)
                            TotalArea++;
                    }
                }

                // Jack Note: Set the measured area of the board
                B.MeasuredAreaPix = TotalArea;
            }
            else
            {
                // Jack Note: Iterate through the detected edges to calculate the area for a vertical board
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    for (int x = B.Edges[0][i].X; x <= B.Edges[1][i].X; x++)
                    {
                        // Jack Note: Calculate the index in the buffer and retrieve the pixel value
                        int Idx = B.Edges[0][i].Y * CBW + x;
                        UInt16 Val = B.CB.Buf[Idx];

                        // Jack Note: Increment the total area if the pixel value is greater than 0
                        if (Val > 0)
                            TotalArea++;
                    }
                }
                // Jack Note: Set the measured area of the board
                B.MeasuredAreaPix = TotalArea;
            }

            // Jack Note: Calculate the percentage of intact and missing wood
            float IntactWoodPct = (float)Math.Round(100.0f * Math.Min(1f, (float)B.MeasuredAreaPix / (float)B.ExpectedAreaPix), 0);
            float MissingWoodPct = (int)(100 - IntactWoodPct);

            // Jack Note: Retrieve the allowable percentage of missing wood from parameter storage
            //float Allowable = paramStorage.GetFloat("(MW) Max Allowed Missing Wood (%)");

            // Jack Note: Check if the missing wood percentage exceeds the allowable threshold
            if (MissingWoodPct > B.MaxAllowable)
            {
                // Jack Note: Add defect for missing too much wood and set defect marker
                AddDefect(B, PalletDefect.DefectType.missing_wood, "Missing too much materials: " + MissingWoodPct.ToString() + "% > " + B.MaxAllowable + "%");
                SetDefectMarker(B);
            }


            // Jack Note: It is very simple, Just calculate the percentage of the Missing Pixels
        }

        //=====================================================================
        private void FindRaisedBoard(Board B, ParamStorage paramStorage)
        {
            // Jack Note: Define width (W1) and height (W2) of the board based on boundaries
            int W1 = B.BoundsP2.X - B.BoundsP1.X;
            int W2 = B.BoundsP2.Y - B.BoundsP1.Y;

            // Jack Note: Capture buffer width
            int CBW = B.CB.Width;

            // Jack Note: Calculate the test value for identifying raised boards using the specified maximum height
            int RBTestVal = (int)(1000 + paramStorage.GetPixZ(StringsLocalization.RaisedBoardMaximumHeight));

            // Jack Note: Get the percentage of the board that must be raised to consider it a defect
            float RBPercentage = paramStorage.GetFloat(StringsLocalization.RaisedBoardMaximumWidth) / 100f;

            // Jack Note: Initialize counters for raised pixel count and total pixel count
            int RBCount = 0;
            int RBTotal = 0;

            // Jack Note: Determine if the board is placed horizontally based on its dimensions
            bool isHoriz = (W1 > W2);

            // Jack Note: Process the board if it is horizontal
            if (isHoriz)
            {
                // Jack Note: Analyze the left 200 pixels of the board for raised areas
                for (int x = 0; x < 200; x++)
                {
                    for (int y = 0; y < W2; y++)
                    {
                        int tx = B.BoundsP1.X + x;
                        int ty = B.BoundsP1.Y + y;

                        // Jack Note: Retrieve the value from the capture buffer at the current position
                        UInt16 Val = B.CB.Buf[ty * CBW + tx];

                        // Jack Note: Increment the raised count if the value exceeds the threshold
                        if (Val > RBTestVal)
                            RBCount++;

                        // Jack Note: Always increment the total pixel count
                        RBTotal++;
                    }
                }

                // Jack Note: Log the results of the left side analysis
                Logger.WriteLine(string.Format("FindRaisedBoard L {0} {1} {2} {3} {4}", B.BoardName, RBCount, RBTotal, (float)RBCount / RBTotal, RBPercentage));

                // Jack Note: If the percentage of raised pixels is greater than the threshold, mark it as a defect
                if (((float)RBCount / RBTotal) > RBPercentage)
                {
                    AddDefect(B, PalletDefect.DefectType.raised_board, "Left side of board raised");
                    SetDefectMarker(B);
                }

                // Jack Note: Reset counts for the next analysis
                RBCount = 0;
                RBTotal = 0;

                // Jack Note: Analyze the right 200 pixels of the board for raised areas
                for (int x = W1 - 200; x < W1; x++)
                {
                    for (int y = 0; y < W2; y++)
                    {
                        int tx = B.BoundsP1.X + x;
                        int ty = B.BoundsP1.Y + y;

                        // Jack Note: Retrieve the value from the capture buffer at the current position
                        UInt16 Val = B.CB.Buf[ty * CBW + tx];

                        // Jack Note: Increment the raised count if the value exceeds the threshold
                        if (Val > RBTestVal)
                            RBCount++;

                        // Jack Note: Always increment the total pixel count
                        RBTotal++;
                    }
                }

                // Jack Note: Log the results of the right side analysis
                Logger.WriteLine(string.Format("FindRaisedBoard R {0} {1} {2} {3} {4}", B.BoardName, RBCount, RBTotal, (float)RBCount / RBTotal, RBPercentage));

                // Jack Note: If the percentage of raised pixels is greater than the threshold, mark it as a defect
                if (((float)RBCount / RBTotal) > RBPercentage)
                {
                    AddDefect(B, PalletDefect.DefectType.raised_board, "Right side of board raised");
                    SetDefectMarker(B);
                }
            }

        }



        //=====================================================================

        private bool ConfirmItIsANail(Board B, int CX, int CY,ParamStorage paramStorage)
        {
            Logger.WriteLine(String.Format("ConfirmItIsANail  X:{0}  Y:{1}", CX, CY));

            // Check if it is near the board edge
            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                int dx = P1.X - CX;
                int dy = P1.Y - CY;
                if (Math.Abs(dx) < 40 && Math.Abs(dy) < 40)
                {
                    // Too close to the edge
                    Logger.WriteLine("ConfirmItIsANail A");
                    return false;
                }

                PalletPoint P2 = B.Edges[1][i];
                dx = P2.X - CX;
                dy = P2.Y - CY;
                if (Math.Abs(dx) < 40 && Math.Abs(dy) < 40)
                {
                    // Too close to the edge
                    Logger.WriteLine("ConfirmItIsANail B");
                    return false;
                }
            }


            // Do a deeper check that this is a nail
            int W = B.CB.Width;
            int H = B.CB.Height;
            double MNH = paramStorage.GetPixZ(StringsLocalization.MaxExposedNailHeight_mm);
            int NSP = paramStorage.GetInt(StringsLocalization.NailNeighborSamplePoints);
            int NSR = paramStorage.GetInt(StringsLocalization.NailNeighborSamplePoints);
            int NearTh = NSR / 2;
            int NCSR = paramStorage.GetInt(StringsLocalization.NailConfirmSurfaceRadius);
            int NCHIR = paramStorage.GetInt(StringsLocalization.NailConfirmHeadInnerRadius);
            int NCHOR = paramStorage.GetInt(StringsLocalization.NailConfirmHeadOuterRadius);

            // Get average height of nail head
            double sumZ = 0;
            double sumC = 0;
            for (int dy = -NCHIR; dy < +NCHIR; dy++)
            {
                int y = CY + dy;
                if ((y < 0) || (y >= H)) continue;
                for (int dx = -NCHIR; dx < +NCHIR; dx++)
                {
                    int x = CX + dx;
                    if ((x < 0) || (x >= W)) continue;
                    int z = B.CB.Buf[y * W + x];
                    if (z != 0)
                    {
                        sumZ += (double)z;
                        sumC += 1;
                    }
                }
            }
            if (sumC < (NCHIR * 2) * (NCHIR * 2) * 0.7)
            {
                Logger.WriteLine("ConfirmItIsANail C");
                return false;
            }

            int NailZ = (int)Math.Round(sumZ / sumC, 0);
            Logger.WriteLine(String.Format("ConfirmItIsANail NailZ:{0}  sumZ:{1}  sumC:{2}", NailZ, sumZ, sumC));

            //int NailZ = (B.CB.Buf[CY * W + CX] +
            //                B.CB.Buf[CY * W + CX + 1] +
            //                B.CB.Buf[CY * W + CX - 1] +
            //                B.CB.Buf[CY * W + CX + W] +
            //                B.CB.Buf[CY * W + CX - W]);


            // Get average height of pallet 50px radius around the nail
            sumZ = 0;
            sumC = 0;
            for (int dy = -NCSR; dy < +NCSR; dy++)
            {
                int y = CY + dy;
                if ((y < 0) || (y >= H) || ((dy >= -NCHOR) && (dy <= NCHOR))) continue;
                for (int dx = -NCSR; dx < +NCSR; dx++)
                {
                    int x = CX + dx;
                    if ((x < 0) || (x >= W) || ((dx >= -NCHOR) && (dx <= NCHOR))) continue;
                    int z = B.CB.Buf[y * W + x];
                    if (z != 0)
                    {
                        sumZ += (double)z;
                        sumC += 1;
                    }
                }
            }

            if (sumC < (NCSR * 2) * (NCSR * 2) * 0.25)
            {
                Logger.WriteLine("ConfirmItIsANail D");
                return false;
            }

            int SurfaceAvgZ = (int)(sumZ / sumC);

            Logger.WriteLine(String.Format("ConfirmItIsANail  NailZ:{0}  SurfZ:{1}  MNH:{2}", NailZ, SurfaceAvgZ, MNH));

            // Check that nail head is above surrounding surface
            if (NailZ < SurfaceAvgZ + MNH)
            {
                Logger.WriteLine("ConfirmItIsANail E");
                return false;
            }

            // Check that nail head does not have a long tail....a wooden sliver
            int FailedCount = 0;
            int CheckedCount = 0;
            for (int a = 0; a < 75; a++)
            {
                double ang = ((Math.PI * 2) / 75) * a;
                double px = CX + (Math.Sin(ang) * NCHOR);
                double py = CY + (Math.Cos(ang) * NCHOR);
                int ix = (int)px;
                int iy = (int)py;
                if ((ix < 0) || (iy < 0) || (ix >= W) || (iy >= H)) continue;

                int Samp = B.CB.Buf[iy * W + ix];
                if (Samp == 0)
                    continue;

                CheckedCount++;

                if (Math.Abs(Samp - NailZ) < (MNH / 2))
                {
                    FailedCount++;

                }
                //B.CB.Buf[iy * W + ix] = 800;
            }

            Logger.WriteLine(String.Format("ConfirmItIsANail  CheckedCount:{0}  FailedCount:{1}", CheckedCount, FailedCount));

            if (CheckedCount < 10)
            {
                Logger.WriteLine("ConfirmItIsANail F");
                return false;
            }

            if (FailedCount > 0)
            {
                Logger.WriteLine("ConfirmItIsANail G");
                return false;
            }

            return true;
        }

        //=====================================================================
        private void FindRaisedNailsDL(Board B, ParamStorage paramStorage, bool defecActivated, int i,bool Pos) {


       


            /*inference---------------------------------------------------------------------------------------------------------------------------------*/
            int widthC = B.CB.Width;
            int heightC = B.CB.Height;
           
            Bitmap bitmapB = new Bitmap(widthC, heightC, PixelFormat.Format8bppIndexed);
            UShortArray2Bitmap(B.CB.Buf, widthC, heightC, bitmapB);
            Rectangle cropRect  = new Rectangle(0, 0, widthC, heightC);
            if (Pos)
            {
                 cropRect = new Rectangle(700, B.startY, 2800, B.endY - B.startY);
            }
            else {
                 cropRect = new Rectangle(B.startX, B.startY, B.endX-B.startX, B.endY - B.startY);
            }
            
            Bitmap croppedBitmap = bitmapB.Clone(cropRect, bitmapB.PixelFormat);
            Bitmap croppedBitmapR = model.ResizeBitmap(croppedBitmap, 1350, 150);
            //need to crop it before apply rezise
            if (Pos)
            {
               // croppedBitmapR.Save($"Top{i}.png");
            }
            else {
               // croppedBitmapR.Save($"Bottom{i}.png");
            }
            
            float[] imageDataTop = model.ProcessBitmapForInference(croppedBitmapR, croppedBitmapR.Width, croppedBitmapR.Height);
            float[][] resultsTop = model.RunInferenceTop(imageDataTop, croppedBitmapR.Height, croppedBitmapR.Width);

            //Get the results
            float[] boxesTop = resultsTop[0];
            float[] labelsTop = resultsTop[1];
            float[] scoresTop = resultsTop[2];
            float[] masksTop = resultsTop[3];
            //Vector for post evaluation
            int[] centroids;

            //Vizualice BB Results
            if (isSaveTopRNWHCO)
            {
                string TopRNWHCO = "VizDL/TopRNHCO/TopRNWHCO.png";
                croppedBitmapR.Save(TopRNWHCO, ImageFormat.Png);
                centroids = model.DrawTopBoxes4(TopRNWHCO, boxesTop, scoresTop, "TopRNWHCO_bb.png", 0.7);
            }
            else
            {

                centroids = model.getCentroids4(boxesTop, scoresTop, 0.7);
            }

            if (centroids != null)
            {
                float MNH = (float)paramStorage.GetPixZ(StringsLocalization.MaxExposedNailHeight_mm);
                int defectsfound = centroids.Length / 2;
                if (defectsfound > 0)
                {
                    int ii = 0;
                    for (int j = 0; j < defectsfound; j++)
                    {

                        int Cx1 = centroids[ii];
                        int Cy1 = centroids[ii + 1];
                        ii = ii + 1;

                        /*float Object1 = model.GetMaxRangeInSquare1(Cx1, Cy1, 10, rangeBuffer, rangeArray);
                        if (Object1 > MNH)
                        {
                            //Add defect here

                        }*/

                    }
                }
            }

        }
        private void FindRaisedNails(Board B,ParamStorage paramStorage)
        {
            // Jack Note: Check if there are enough edges detected to proceed
            double AverageDelta = 0;
            if (B.Edges[0].Count < 5)
            {
                return; // Jack Note: Not enough edges, exit function
            }

            // Jack Note: Retrieve the maximum height a nail can have to be considered raised
            float MNH = (float)paramStorage.GetPixZ(StringsLocalization.MaxExposedNailHeight_mm);

            // Jack Note: Get parameters for nail detection algorithm
            int NSP = paramStorage.GetInt(StringsLocalization.NailNeighborSamplePoints);
            int NSR = paramStorage.GetInt(StringsLocalization.NailNeighborSampleRadius);

            // Jack Note: Find the minimum and maximum coordinates of the board
            PalletPoint P1 = FindBoardMinXY(B);
            PalletPoint P2 = FindBoardMaxXY(B);

            // Jack Note: Dimensions of the capture buffer, width and height
            int W = B.CB.Width;
            int H = B.CB.Height;

            // Jack Note: Prepare list to hold potential nail points
            List<PalletPoint> Raw = new List<PalletPoint>();

            // Jack Note: Iterate through each point within the boundary of the board
            for (int y = P1.Y; y <= P2.Y; y++)
            {
                // Jack Note: Skip points too close to the edge to prevent out-of-bounds during sampling
                if ((y < NSR + 2) || (y >= H - NSR - 8)) continue;

                for (int x = P1.X; x <= P2.X; x++)
                {
                    if ((x < NSR + 2) || (x >= W - NSR - 8)) continue;

                    // Jack Note: Average height calculation from the current point and its immediate neighbors
                    int NailZ = (B.CB.Buf[y * W + x] +
                                 B.CB.Buf[y * W + x + 2] +
                                 B.CB.Buf[y * W + x - 2] +
                                 B.CB.Buf[y * W + x + W * 2] +
                                 B.CB.Buf[y * W + x - W * 2]) / 5;

                    // Jack Note: Skip if the point is not significantly raised
                    if (NailZ == 0 || NailZ < 1000)
                        continue;

                    // Jack Note: Initialize counts for assessing the nail candidate
                    int RaisedCount = 0;
                    int MaxCount = 0;
                    double TotalDelta = 0; // 计算 Delta 累积值
                    // Jack Note: Sample around the point based on defined radius and sample points
                    for (int a = 0; a < NSP; a++)
                    {
                        double ang = ((Math.PI * 2) / NSP) * a;
                        double px = x + (Math.Sin(ang) * NSR);
                        double py = y + (Math.Cos(ang) * NSR);
                        int ix = (int)px;
                        int iy = (int)py;
                        if ((ix < 0) || (iy < 0) || (ix >= W) || (iy >= H)) continue;

                        UInt16 Samp = B.CB.Buf[iy * W + ix];

                        if (Samp == 0)
                            continue;

                        MaxCount++;

                        int Delta = ((int)NailZ - (int)Samp);
                        TotalDelta += Delta;
                        if (Delta > MNH)
                            RaisedCount++;
                    }

                    // Jack Note: Skip point if not enough valid samples or if not all samples indicate a raised nail
                    if (MaxCount == 0 || (NSP - MaxCount) > 2 || MaxCount != RaisedCount)
                        continue;
                    AverageDelta = TotalDelta / MaxCount;
                    // Jack Note: Add the point to potential nails list and skip ahead by the radius
                    Raw.Add(new PalletPoint(x, y));
                    x += NSR;
                }
            }

            // Jack Note: Required confirmations for a nail to be considered valid
            int RequiredConf = paramStorage.GetInt(StringsLocalization.NailConfirmationsRequired);

            // Jack Note: Validate each potential nail based on nearby confirmations and existing records
            foreach (PalletPoint P in Raw)
            {
                int nRawNearbyConfirms = 0;
                foreach (PalletPoint NP in Raw)
                {
                    if ((Math.Abs(P.X - NP.X) < 4) && (Math.Abs(P.Y - NP.Y) < 4))
                        nRawNearbyConfirms++;
                }

                if (nRawNearbyConfirms >= RequiredConf)
                {
                    bool foundExisting = false;
                    foreach (PalletPoint ExistingP in B.Nails)
                    {
                        if ((Math.Abs(P.X - ExistingP.X) < 10) && (Math.Abs(P.Y - ExistingP.Y) < 20))
                        {
                            foundExisting = true;
                            break;
                        }
                    }

                    if (!foundExisting && ConfirmItIsANail(B, P.X, P.Y,paramStorage))
                    {
                        B.Nails.Add(P);
                        AddDefect(B, PalletDefect.DefectType.raised_nail,
                      $"Raised Nail > {MNH}mm, Height: {Math.Round( AverageDelta,2)}mm");

                        SetDefectMarker(P.X, P.Y, 30);                      
                    }
                }
            }
        }

        //=====================================================================
       public PalletDefect AddDefect(Board B, PalletDefect.DefectType type, string Comment)
        {
            if (B == null)
            {
                PalletDefect Defect = new PalletDefect(PalletDefect.DefectLocation.Pallet, type, Comment);
                PalletLevelDefects.Add(Defect);
                AllDefects.Add(Defect);
                return Defect;
            }
            else
            {
                PalletDefect Defect = new PalletDefect(B.Location, type, Comment);
                B.AllDefects.Add(Defect);
                AllDefects.Add(Defect);
                return Defect;
            }
        }

        //public PalletDefect AddDefectToBlock(Block B, PalletDefect.DefectType type, string Comment)
        //{
        //    if (B == null)
        //    {
        //        PalletDefect Defect = new PalletDefect(PalletDefect.DefectLocation.Pallet, type, Comment);
        //        PalletLevelDefects.Add(Defect);
        //        AllDefects.Add(Defect);
        //        return Defect;
        //    }
        //    else
        //    {
        //        PalletDefect Defect = new PalletDefect(B.Location, type, Comment);
        //        B.AllDefects.Add(Defect);
        //        AllDefects.Add(Defect);
        //        return Defect;
        //    }
        //}

        void SetDefectMarker(Board B, string Tag = "")
        {
            if (AllDefects.Count > 0)
            {
                PalletDefect PD = AllDefects[AllDefects.Count - 1];
                if (Tag == "") Tag = PD.Code;
                PD.SetRectMarker(B.BoundsP1.X, B.BoundsP1.Y, B.BoundsP2.X, B.BoundsP2.Y, Tag);
            }
        }

        void SetDefectMarker(int X, int Y, int R, string Tag = "")
        {
            if (AllDefects.Count > 0)
            {
                PalletDefect PD = AllDefects[AllDefects.Count - 1];
                if (Tag == "") Tag = PD.Code;
                PD.SetCircleMarker(X, Y, R, Tag);
                
            }
        }

        void SetDefectMarker(int X1, int Y1, int X2, int Y2, string Tag = "")
        {
            if (AllDefects.Count > 0)
            {
                PalletDefect PD = AllDefects[AllDefects.Count - 1];
                if (Tag == "") Tag = PD.Code;
                PD.SetRectMarker(X1, Y1, X2, Y2, Tag);
            }
        }

        //=====================================================================
        //private int CountNearbyNails(PalletPoint p, List<PalletPoint> raw)
        //{
        //    int Count = 0;
        //    foreach (PalletPoint P in raw)
        //    {
        //        if ((Math.Abs(P.X - p.X) < 3) &&
        //             (Math.Abs(P.Y - p.Y) < 3))
        //            Count++;
        //    }

        //    return Count;
        //}

        //=====================================================================
        PalletPoint FindBoardMinXY(Board B)
        {
            if (B.Edges[0].Count < 5)
            {
                return new PalletPoint(0, 0);
            }

            int MinX = int.MaxValue;
            int MinY = int.MaxValue;

            // Is Horizontal?
            if (B.Edges[0][0].X == B.Edges[1][0].X)
            {
                MinX = B.Edges[0][0].X;
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    MinY = Math.Min(MinY, B.Edges[0][i].Y);
                }
            }
            else
            {
                MinY = B.Edges[0][0].Y;
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    MinX = Math.Min(MinX, B.Edges[0][i].X);
                }
            }

            return new PalletPoint(MinX, MinY);
        }

        //=====================================================================
        PalletPoint FindBoardMaxXY(Board B)
        {
            if (B.Edges[0].Count < 5)
            {
                return new PalletPoint(0, 0);
            }

            int MaxX = int.MinValue;
            int MaxY = int.MinValue;

            // Is Horizontal?
            if (B.Edges[0][0].X == B.Edges[1][0].X)
            {
                MaxX = B.Edges[0][B.Edges[0].Count - 1].X;
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    MaxY = Math.Max(MaxY, B.Edges[1][i].Y);
                }
            }
            else
            {
                MaxY = B.Edges[0][B.Edges[0].Count - 1].Y;
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    MaxX = Math.Max(MaxX, B.Edges[1][i].X);
                }
            }

            return new PalletPoint(MaxX, MaxY);
        }

        //=====================================================================
        void MakeBlocks(CaptureBuffer CB, List<PalletPoint> P, int Size, UInt16 Val)
        {
            foreach (PalletPoint Pt in P)
            {
                MakeBlock(CB, Pt.X, Pt.Y, Size, Val);
            }
        }

        //=====================================================================
        void MakeBlock(CaptureBuffer CB, int x1, int y1, int x2, int y2, UInt16 Val)
        {
            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    CB.Buf[y * CB.Width + x] = Val;
                }
            }
        }

        //=====================================================================
        void MakeBlock(CaptureBuffer CB, int x, int y, int size, UInt16 Val)
        {
            MakeBlock(CB, x - (size / 2), y - (size / 2), x + (size / 2), y + (size / 2), Val);
        }


        // Jack Note: This is new added method with more parameters 09/19/2024
        Board ProbeVertically(
                ParamStorage paramStorage,
                CaptureBuffer sourceCB,
                string name,
                PalletDefect.DefectLocation location,
                int startCol,
                int endCol,
                int step,
                int startRow,
                int endRow,
                float widthThresholdMultiplier = 1.3f, // 可配置的宽度阈值倍数
                int edgeTrimLimit = 300,              // 可配置的边缘点限制
                int minSpanDifference = 20  // 可配置的最小跨度差
)               
        {
            UInt16[] buf = sourceCB.Buf;
            int cbWidth = sourceCB.Width;
            List<PalletPoint> points = new List<PalletPoint>();

            int expLen = paramStorage.GetInt("Short Board Length");
            int expWid = paramStorage.GetInt("Board Width Y");

            Board board = new Board(name, location, true, expLen, expWid);

            int dir = Math.Sign(endRow - startRow);

            // 遍历相机，使用 StartX 和 EndX 数组计算总宽度
            for (int i = startCol; i < endCol; i += step)
            {
                int row0 = FindVerticalSpan_Old(sourceCB, i, endRow, startRow);
                int row1 = FindVerticalSpan_Old(sourceCB, i, startRow, endRow);

                if ((row0 != -1) && (row1 != -1))
                {
                    board.Edges[0].Add(new PalletPoint(i, Math.Min(row0, row1)));
                    board.Edges[1].Add(new PalletPoint(i, Math.Max(row0, row1)));
                }
            }

            int failWidth = (int)(expWid * widthThresholdMultiplier); // 使用参数

            // 插入边缘点
            bool lastWasGood = false;
            for (int i = 1; i < board.Edges[0].Count - 2; i++)
            {
                int delta1 = board.Edges[0][i].X - board.Edges[0][i - 1].X;
                int delta2 = board.Edges[1][i].X - board.Edges[1][i - 1].X;

                if (lastWasGood && (delta1 > 1) && (delta1 < 100) && (delta2 == delta1))
                {
                    lastWasGood = false;
                    board.Edges[0].Insert(i, new PalletPoint(board.Edges[0][i - 1].X + 1, board.Edges[0][i - 1].Y));
                    board.Edges[1].Insert(i, new PalletPoint(board.Edges[1][i - 1].X + 1, board.Edges[1][i - 1].Y));
                    i--;
                }
                else
                {
                    lastWasGood = true;
                }
            }

            // 清理不合格的边缘点
            for (int i = 0; i < board.Edges[0].Count; i++)
            {
                if (Math.Abs(board.Edges[0][i].Y - board.Edges[1][i].Y) < minSpanDifference) // 使用参数
                {
                    board.Edges[0].RemoveAt(i);
                    board.Edges[1].RemoveAt(i);
                    i--;
                }
            }

            // 边缘修整逻辑，使用配置的边缘限制
            if (board.Edges[0].Count > edgeTrimLimit)
            {
                // 修整左侧
                for (int x = edgeTrimLimit / 2; x > 0; x--)
                {
                    if ((board.Edges[0][x].X - board.Edges[0][x - 1].X) > 10)
                    {
                        board.Edges[0].RemoveRange(0, x);
                        board.Edges[1].RemoveRange(0, x);
                        break;
                    }
                }
                // 修整右侧
                for (int x = board.Edges[0].Count - edgeTrimLimit / 2; x < board.Edges[0].Count - 1; x++)
                {
                    if ((board.Edges[0][x + 1].X - board.Edges[0][x].X) > 10)
                    {
                        board.Edges[0].RemoveRange(x + 1, board.Edges[0].Count - (x + 1));
                        board.Edges[1].RemoveRange(x + 1, board.Edges[1].Count - (x + 1));
                        break;
                    }
                }
            }

            // 拷贝所有边缘点到新的缓冲区
            UInt16[] newBoard = new UInt16[buf.Length];
            for (int i = 0; i < board.Edges[0].Count; i++)
            {
                PalletPoint p1 = board.Edges[0][i];
                PalletPoint p2 = board.Edges[1][i];
                int spanDir = Math.Sign(p2.Y - p1.Y);

                for (int y = p1.Y; y != (p2.Y + 1); y += spanDir)
                {
                    int src = y * cbWidth + p1.X;
                    newBoard[src] = buf[src];
                    buf[src] = 0;
                }
            }

            // 清理操作
            try
            {
                for (int i = 0; i < board.Edges[0].Count; i++)
                {
                    PalletPoint p1 = board.Edges[0][i];
                    PalletPoint p2 = board.Edges[1][i];
                    int spanDir = Math.Sign(p2.Y - p1.Y);

                    for (int y = 1; y < 20; y++)
                    {
                        int src = (p1.Y - y) * cbWidth + p1.X;
                        buf[src] = 0;
                        src = (p2.Y + y) * cbWidth + p1.X;
                        buf[src] = 0;
                    }
                }
            }
            catch (Exception)
            {
                // 处理异常
            }

            board.CB = new CaptureBuffer(newBoard, sourceCB.Width, sourceCB.Height);
            AddCaptureBuffer(name, board.CB);

            return board;
        }


        //=====================================================================
        Board ProbeVertically(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location, int StartCol, int EndCol, int Step, int StartRow, int EndRow,ParamStorage paramStorage)
        {
            UInt16[] Buf = SourceCB.Buf;
            List<PalletPoint> P = new List<PalletPoint>();
            int CBW = SourceCB.Width;

            int ExpLen = paramStorage.GetInt("Short Board Length");
            int ExpWid = paramStorage.GetInt("Board Width Y");   // Jack Note: Note used

            Board B = new Board(Name, Location, true, ExpLen, ExpWid);

            int Dir = Math.Sign(EndRow - StartRow);
            int Len = Buf.Length;

            List<int> Vals = new List<int>();

            /// Jack Note: Add new probe
            for (int i = StartCol; i < EndCol; i += Step)
            {
                int Row0 = FindVerticalSpan_Old(SourceCB, i, EndRow, StartRow);
                int Row1 = FindVerticalSpan_Old(SourceCB, i, StartRow, EndRow);

                // Jack Note: find the intersection between the vertical line and the board

                if ((Row0 != -1) && (Row1 != -1))
                {
                    B.Edges[0].Add(new PalletPoint(i, Math.Min(Row0, Row1)));
                    B.Edges[1].Add(new PalletPoint(i, Math.Max(Row0, Row1)));
                }
            }


            int FailWid = (int)(ExpWid * 1.3f);


            // Jack Note: Insert a element and the other existed elments will be move a position forward
            bool LastWasGood = false;
            for (int i = 1; i < B.Edges[0].Count - 2; i++)
            {
                int Delta1 = B.Edges[0][i].X - B.Edges[0][i - 1].X;
                int Delta2 = B.Edges[1][i].X - B.Edges[1][i - 1].X;

                if (LastWasGood && ((Delta1 > 1) && (Delta1 < 100) && (Delta2 == Delta1)))
                {
                    LastWasGood = false;
                    B.Edges[0].Insert(i, new PalletPoint(B.Edges[0][i - 1].X + 1, B.Edges[0][i - 1].Y));
                    B.Edges[1].Insert(i, new PalletPoint(B.Edges[1][i - 1].X + 1, B.Edges[1][i - 1].Y));
                    i--;
                }
                else
                    LastWasGood = true;
            }


            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                // Sanity checks
                //if (Math.Abs(B.Edges[0][i].Y - B.Edges[1][i].Y) > FailWid)
                //{
                //    B.Edges[0].RemoveAt(i);
                //    B.Edges[1].RemoveAt(i);
                //    i--;
                //    continue;
                //}

                // Sanity checks
                if (Math.Abs(B.Edges[0][i].Y - B.Edges[1][i].Y) < 20)
                {
                    B.Edges[0].RemoveAt(i);
                    B.Edges[1].RemoveAt(i);
                    i--;
                    continue;
                }
            }

            if (B.Edges[0].Count > 300)
            {
                for (int x = 149; x > 0; x--)
                {
                    if ((B.Edges[0][x].X - B.Edges[0][x - 1].X) > 10)
                    {
                        // trim the rest
                        B.Edges[0].RemoveRange(0, x);
                        B.Edges[1].RemoveRange(0, x);
                        break;
                    }
                }
                for (int x = B.Edges[0].Count - 150; x < B.Edges[0].Count - 1; x++)
                {
                    if ((B.Edges[0][x + 1].X - B.Edges[0][x].X) > 10)
                    {
                        // trim the rest
                        B.Edges[0].RemoveRange(x + 1, B.Edges[0].Count - (x + 1));
                        B.Edges[1].RemoveRange(x + 1, B.Edges[1].Count - (x + 1));
                        break;
                    }
                }
            }


            // Check for end slivers of the V1 boards
            // Jack Note: If edge more than 300, Delete the point.Y is much smaller or bigger than the average Y 
            // Average is from 50 to 100
            if (B.Edges[0].Count > 300)
            {
                int avgY0 = 0;
                int avgY1 = 0;
                int cntY = 0;
                for (int x = 100; x > 50; x--)
                {
                    avgY0 += B.Edges[0][x].Y;
                    avgY1 += B.Edges[1][x].Y;
                    cntY += 1;
                }
                if (cntY > 0)
                {
                    avgY0 /= cntY;
                    avgY1 /= cntY;
                    Logger.WriteLine(string.Format("SLIVERS L {0} {1} {2}", Name, avgY0, avgY1));
                    for (int x = 20; x > 0; x--)
                    {
                        if ((B.Edges[0][x].Y < avgY0 - 20) || (B.Edges[1][x].Y > avgY1 + 20))
                        {
                            // trim the rest
                            Logger.WriteLine("trimming sliver L");
                            B.Edges[0].RemoveRange(0, x);
                            B.Edges[1].RemoveRange(0, x);
                            break;
                        }
                    }
                }

                // Jack Note: If edge more than 300, Delete the point.Y is much smaller or bigger than the average Y 
                // Average is from count - 50 to count - 100
                avgY0 = 0;
                avgY1 = 0;
                cntY = 0;
                for (int x = B.Edges[0].Count - 100; x < B.Edges[0].Count - 50; x++)
                {
                    avgY0 += B.Edges[0][x].Y;
                    avgY1 += B.Edges[1][x].Y;
                    cntY += 1;
                }
                if (cntY > 0)
                {
                    avgY0 /= cntY;
                    avgY1 /= cntY;
                    Logger.WriteLine(string.Format("SLIVERS R {0} {1} {2}", Name, avgY0, avgY1));
                    for (int x = B.Edges[0].Count - 20; x < B.Edges[0].Count; x++)
                    {
                        if ((B.Edges[0][x].Y < avgY0 - 20) || (B.Edges[1][x].Y > avgY1 + 20))
                        {
                            // trim the rest
                            Logger.WriteLine("trimming sliver R");
                            B.Edges[0].RemoveRange(x + 1, B.Edges[0].Count - (x + 1));
                            B.Edges[1].RemoveRange(x + 1, B.Edges[1].Count - (x + 1));
                            break;
                        }
                    }
                }
            }


            //Jack Note: Copy all the edge point into a new buffer for futher process and Clear the board area of the src buffer
            UInt16[] NewBoard = new UInt16[Buf.Length];

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                PalletPoint P2 = B.Edges[1][i];
                int SpanDir = Math.Sign(P2.Y - P1.Y);

                for (int y = P1.Y; y != (P2.Y + 1); y += SpanDir)
                {
                    int Src = y * CBW + P1.X;
                    NewBoard[Src] = Buf[Src];
                    Buf[Src] = 0;
                }
            }

            // Clean up
            try
            {
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    PalletPoint P1 = B.Edges[0][i];
                    PalletPoint P2 = B.Edges[1][i];
                    int SpanDir = Math.Sign(P2.Y - P1.Y);

                    for (int y = 1; y < 20; y++)
                    {
                        int Src = (P1.Y - y) * CBW + P1.X;
                        Buf[Src] = 0;
                        Src = (P2.Y + y) * CBW + P1.X;
                        Buf[Src] = 0;
                    }
                }
            }
            catch (Exception)
            {
            }

            B.CB = new CaptureBuffer(NewBoard, SourceCB.Width, SourceCB.Height);
            AddCaptureBuffer(Name, B.CB);


            //for (int i = 0; i < B.Edges[0].Count; i++)
            //{
            //    MakeBlock(Buf, B.Edges[0][i].X, B.Edges[0][i].Y, 2, 5000);
            //    MakeBlock(Buf, B.Edges[1][i].X, B.Edges[1][i].Y, 2, 5000);
            //}

            return B;
        }





        //=====================================================================
        int FindVerticalSpan_Old(CaptureBuffer CB, int Col, int StartRow, int EndRow)
        {
            UInt16[] Buf = CB.Buf;
            int CBW = CB.Width;
            int Dir = Math.Sign(EndRow - StartRow);

            int HitCount = 0;
            int HitRow = -1;

            int MinPPix = 5;// ParamStorage.GetInt("VerticalHitCountMinPositivePixels");
            //int MinZPix = 15;// ParamStorage.GetInt("VerticalHitCountMinZeroPixels");

            HitCount = 0;

            // Check to make sure we didn't start inside pixels
            for (int i = 0; i < 15; i++)
            {
                UInt16 V = Buf[(StartRow + (i * Dir)) * CBW + Col];
                if (V != 0)
                    HitCount++;
            }

            if (HitCount > 5)
                return -1;


            for (int y = StartRow; y != EndRow; y += Dir)
            {
                UInt16 V = Buf[y * CBW + Col];

                if (V != 0)
                {
                    //if (y == StartRow)
                    //    return -1;

                    if (HitCount == 0)
                        HitRow = y;
                    else
                    {
                        if (HitCount >= MinPPix)
                        {
                            return HitRow;
                        }
                    }

                    HitCount++;
                }
                else
                {
                    HitCount = 0;
                }
            }


            return -1;
        }

        //=====================================================================
        int FindHorizontalSpan_Old(CaptureBuffer SourceCB, int Row, int StartCol, int EndCol)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            int Dir = Math.Sign(EndCol - StartCol);

            int HitCount = 0;
            int HitCol = -1;

            int MinPPix = 15;// ParamStorage.GetInt("HorizontalHitCountMinPositivePixels");
            //int MinZPix = 15;// ParamStorage.GetInt("HorizontalHitCountMinZeroPixels");

            HitCount = 0;


            // TEST TEST TEST 
            //if ((Row == 957) && (StartCol < 600))
            //{
            //    BinaryWriter BW = new BinaryWriter(new FileStream("D:/Chep/TestBuf.r3", FileMode.Create));
            //    foreach (ushort V in Buf)
            //        BW.Write(V);
            //    BW.Close();
            //}

            for (int x = StartCol; x != EndCol; x += Dir)
            {
                UInt16 V = Buf[Row * CBW + x];

                if (V != 0)
                {
                    if (HitCount == 0)
                        HitCol = x;
                    else
                    {
                        if (HitCount >= MinPPix)
                        {
                            return HitCol;
                        }
                    }

                    HitCount++;
                }
                else
                {
                    HitCount = 0;
                }
            }

            return -1;
        }

        Board ProbeHorizontallyRotate90(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location, int StartRow, int EndRow, int Step, int StartCol, int EndCol,ParamStorage paramStorage)
        {
            UInt16[] Buf = SourceCB.Buf;
            List<PalletPoint> P = new List<PalletPoint>();
            int CBW = SourceCB.Width;

            int ExpLen = paramStorage.GetInt("Short Board Length");
            int ExpWid = paramStorage.GetInt("Board Width X");   // Adjusted for horizontal scanning

            Board B = new Board(Name, Location, true, ExpLen, ExpWid);

            int Dir = Math.Sign(EndCol - StartCol);
            int Len = Buf.Length;

            List<int> Vals = new List<int>();

            // Jack Note: Add new probe
            for (int i = StartRow; i < EndRow; i += Step)
            {
                int Col0 = FindHorizontalSpan_OldRotate90(SourceCB, i, StartCol, EndCol);
                int Col1 = FindHorizontalSpan_OldRotate90(SourceCB, i, EndCol, StartCol);

                // Find the intersection between the horizontal line and the board
                if ((Col0 != -1) && (Col1 != -1))
                {
                    B.Edges[0].Add(new PalletPoint(Math.Min(Col0, Col1), i));
                    B.Edges[1].Add(new PalletPoint(Math.Max(Col0, Col1), i));
                }
            }

            int FailWid = (int)(ExpWid * 1.3f);

            // Adjust insertion logic for horizontal direction
            bool LastWasGood = false;
            for (int i = 1; i < B.Edges[0].Count - 2; i++)
            {
                int Delta1 = B.Edges[0][i].Y - B.Edges[0][i - 1].Y;
                int Delta2 = B.Edges[1][i].Y - B.Edges[1][i - 1].Y;

                if (LastWasGood && ((Delta1 > 1) && (Delta1 < 100) && (Delta2 == Delta1)))
                {
                    LastWasGood = false;
                    B.Edges[0].Insert(i, new PalletPoint(B.Edges[0][i - 1].X, B.Edges[0][i - 1].Y + 1));
                    B.Edges[1].Insert(i, new PalletPoint(B.Edges[1][i - 1].X, B.Edges[1][i - 1].Y + 1));
                    i--;
                }
                else
                    LastWasGood = true;
            }
            int TrimThreshold = 30;

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                // Sanity checks
                if (Math.Abs(B.Edges[0][i].X - B.Edges[1][i].X) < TrimThreshold)
                {
                    B.Edges[0].RemoveAt(i);
                    B.Edges[1].RemoveAt(i);
                    i--;
                    continue;
                }
            }

            // Check if the edge count exceeds 300 and trim if necessary
            if (B.Edges[0].Count > 300)
            {
                for (int x = 149; x > 0; x--)
                {
                    if ((B.Edges[0][x].X - B.Edges[0][x - 1].X) > TrimThreshold)
                    {
                        // Trim the rest
                        B.Edges[0].RemoveRange(0, x);
                        B.Edges[1].RemoveRange(0, x);
                        break;
                    }
                }
                for (int x = B.Edges[0].Count - 200; x < B.Edges[0].Count - 1; x++)
                {
                    if ((B.Edges[0][x + 1].X - B.Edges[0][x].X) > TrimThreshold)
                    {
                        // Trim the rest
                        B.Edges[0].RemoveRange(x + 1, B.Edges[0].Count - (x + 1));
                        B.Edges[1].RemoveRange(x + 1, B.Edges[1].Count - (x + 1));
                        break;
                    }
                }

                // Jack Note: Calculate and handle slivers on the top side
                int avgX0 = 0;
                int avgX1 = 0;
                int cntX = 0;
                for (int x = 500; x > 150; x--)
                {
                    avgX0 += B.Edges[0][x].X;
                    avgX1 += B.Edges[1][x].X;
                    cntX += 1;
                }
                if (cntX > 0)
                {
                    avgX0 /= cntX;
                    avgX1 /= cntX;
                    Logger.WriteLine(string.Format("SLIVERS T {0} {1} {2}", Name, avgX0, avgX1));
                    for (int x = 350; x > 0; x--)
                    {
                        if ((B.Edges[0][x].X < avgX0 - TrimThreshold) || (B.Edges[1][x].X > avgX1 + TrimThreshold))
                        {
                            // Trim the rest
                            Logger.WriteLine("trimming sliver T");
                            B.Edges[0].RemoveRange(0, x);
                            B.Edges[1].RemoveRange(0, x);
                            break;
                        }
                    }
                }

                // Jack Note: Calculate and handle slivers on the bottom side
                avgX0 = 0;
                avgX1 = 0;
                cntX = 0;
                for (int x = B.Edges[0].Count - 300; x < B.Edges[0].Count - 150; x++)
                {
                    avgX0 += B.Edges[0][x].X;
                    avgX1 += B.Edges[1][x].X;
                    cntX += 1;
                }
                if (cntX > 0)
                {
                    avgX0 /= cntX;
                    avgX1 /= cntX;
                    Logger.WriteLine(string.Format("SLIVERS B {0} {1} {2}", Name, avgX0, avgX1));
                    for (int x = B.Edges[0].Count - 350; x < B.Edges[0].Count; x++)
                    {
                        if ((B.Edges[0][x].X < avgX0 - TrimThreshold) || (B.Edges[1][x].X > avgX1 + TrimThreshold))
                        {
                            // Trim the rest
                            Logger.WriteLine("trimming sliver B");
                            B.Edges[0].RemoveRange(x + 1, B.Edges[0].Count - (x + 1));
                            B.Edges[1].RemoveRange(x + 1, B.Edges[1].Count - (x + 1));
                            break;
                        }
                    }
                }
            }

            // Copy all the edge points into a new buffer for further processing
            UInt16[] NewBoard = new UInt16[Buf.Length];

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                PalletPoint P2 = B.Edges[1][i];
                int SpanDir = Math.Sign(P2.X - P1.X);

                for (int x = P1.X; x != (P2.X + 1); x += SpanDir)
                {
                    int Src = P1.Y * CBW + x;
                    NewBoard[Src] = Buf[Src];
                    Buf[Src] = 0;
                }
            }

            // Clean up remaining parts (same as the original code)
            try
            {
                for (int i = 0; i < B.Edges[0].Count; i++)
                {
                    PalletPoint P1 = B.Edges[0][i];
                    PalletPoint P2 = B.Edges[1][i];
                    int SpanDir = Math.Sign(P2.X - P1.X);

                    for (int y = 1; y < 20; y++)
                    {
                        int Src = (P1.Y - y) * CBW + P1.X;
                        Buf[Src] = 0;
                        Src = (P2.Y + y) * CBW + P1.X;
                        Buf[Src] = 0;
                    }
                }
            }
            catch (Exception)
            {
            }

            B.CB = new CaptureBuffer(NewBoard, SourceCB.Width, SourceCB.Height);
            AddCaptureBuffer(Name, B.CB);

            return B;
        }

        //=====================================================================
        int FindHorizontalSpan_OldRotate90(CaptureBuffer SourceCB, int Row, int StartCol, int EndCol)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            int Dir = Math.Sign(EndCol - StartCol);

            int HitCount = 0;
            int HitCol = -1;

            int MinPPix = 15;  // ParamStorage.GetInt("HorizontalHitCountMinPositivePixels");

            HitCount = 0;

            for (int x = StartCol; x != EndCol; x += Dir)
            {
                UInt16 V = Buf[Row * CBW + x];

                if (V != 0)
                {
                    if (HitCount == 0)
                        HitCol = x;
                    else
                    {
                        if (HitCount >= MinPPix)
                        {
                            return HitCol;
                        }
                    }

                    HitCount++;
                }
                else
                {
                    HitCount = 0;
                }
            }

            return -1;
        }

        Board ProbeVerticallyRotate90(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location, int StartCol, int EndCol, int Step, int StartRow, int EndRow, ParamStorage paramStorage)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            List<PalletPoint> P = new List<PalletPoint>();

            int ExpLen = paramStorage.GetInt("Long Board Length");
            int ExpWid = paramStorage.GetInt("Board Width Y Left");  // Adjusted for vertical scanning

            Board B = new Board(Name, Location, true, ExpLen, ExpWid);

            int Dir = Math.Sign(EndRow - StartRow);
            int Len = Buf.Length;

            List<int> Vals = new List<int>();

            for (int i = StartCol; i < EndCol; i += Step)
            {
                int Row0 = FindVerticalSpan_OldRotate90(SourceCB, i, EndRow, StartRow);
                int Row1 = FindVerticalSpan_OldRotate90(SourceCB, i, StartRow, EndRow);

                if ((Row0 != -1) && (Row1 != -1))
                {
                    B.Edges[0].Add(new PalletPoint(i, Math.Min(Row0, Row1)));
                    B.Edges[1].Add(new PalletPoint(i, Math.Max(Row0, Row1)));
                }
            }

            int FailWid = (int)(ExpWid * 1.2f);

            bool LastWasGood = false;
            for (int i = 1; i < B.Edges[0].Count - 1; i++)
            {
                int Delta1 = B.Edges[0][i].X - B.Edges[0][i - 1].X;
                int Delta2 = B.Edges[1][i].X - B.Edges[1][i - 1].X;

                if (LastWasGood && ((Delta1 > 1) && (Delta1 < 100) && (Delta2 == Delta1)))
                {
                    B.Edges[0].Insert(i, new PalletPoint(B.Edges[0][i - 1].X + 1, B.Edges[0][i - 1].Y));
                    B.Edges[1].Insert(i, new PalletPoint(B.Edges[1][i - 1].X + 1, B.Edges[1][i - 1].Y));
                }
                else
                    LastWasGood = true;
            }

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                // Sanity checks
                if (Math.Abs(B.Edges[0][i].Y - B.Edges[1][i].Y) < 20)
                {
                    B.Edges[0].RemoveAt(i);
                    B.Edges[1].RemoveAt(i);
                    i--;
                    continue;
                }
            }

            UInt16[] NewBoard = new UInt16[Buf.Length];

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                PalletPoint P2 = B.Edges[1][i];
                int SpanDir = Math.Sign(P2.Y - P1.Y);

                for (int y = P1.Y; y != (P2.Y + 1); y += SpanDir)
                {
                    int Src = y * CBW + P1.X;
                    NewBoard[Src] = Buf[Src];
                    Buf[Src] = 0;
                }
            }

            B.CB = new CaptureBuffer(NewBoard, SourceCB.Width, SourceCB.Height);
            AddCaptureBuffer(Name, B.CB);

            return B;
        }

        int FindVerticalSpan_OldRotate90(CaptureBuffer SourceCB, int Col, int StartRow, int EndRow)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            int Dir = Math.Sign(EndRow - StartRow);

            int HitCount = 0;
            int HitRow = -1;

            int MinPPix = 15;

            HitCount = 0;

            for (int y = StartRow; y != EndRow; y += Dir)
            {
                UInt16 V = Buf[y * CBW + Col];

                if (V != 0)
                {
                    if (HitCount == 0)
                        HitRow = y;
                    else
                    {
                        if (HitCount >= MinPPix)
                        {
                            return HitRow;
                        }
                    }

                    HitCount++;
                }
                else
                {
                    HitCount = 0;
                }
            }

            return -1;
        }
        Board EzRBoard(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location, int StartCol, int EndCol, int Step, int StartRow, int EndRow, ParamStorage paramStorage)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            List<PalletPoint> P = new List<PalletPoint>();

            int ExpLen = paramStorage.GetInt("Long Board Length");
            int ExpWid = paramStorage.GetInt("Board Width Y Left");  // Adjusted for vertical scanning

            Board B = new Board(Name, Location, true, ExpLen, ExpWid);

            int Dir = Math.Sign(EndRow - StartRow);
            int Len = Buf.Length;

            List<int> Vals = new List<int>();

            for (int i = StartCol; i < EndCol; i += Step)
            {
                int Row0 = FindVerticalSpan_OldRotate90(SourceCB, i, EndRow, StartRow);
                int Row1 = FindVerticalSpan_OldRotate90(SourceCB, i, StartRow, EndRow);

                if ((Row0 != -1) && (Row1 != -1))
                {
                    B.Edges[0].Add(new PalletPoint(i, Math.Min(Row0, Row1)));
                    B.Edges[1].Add(new PalletPoint(i, Math.Max(Row0, Row1)));
                }
            }

            int FailWid = (int)(ExpWid * 1.2f);

            bool LastWasGood = false;
            for (int i = 1; i < B.Edges[0].Count - 1; i++)
            {
                int Delta1 = B.Edges[0][i].X - B.Edges[0][i - 1].X;
                int Delta2 = B.Edges[1][i].X - B.Edges[1][i - 1].X;

                if (LastWasGood && ((Delta1 > 1) && (Delta1 < 100) && (Delta2 == Delta1)))
                {
                    B.Edges[0].Insert(i, new PalletPoint(B.Edges[0][i - 1].X + 1, B.Edges[0][i - 1].Y));
                    B.Edges[1].Insert(i, new PalletPoint(B.Edges[1][i - 1].X + 1, B.Edges[1][i - 1].Y));
                }
                else
                    LastWasGood = true;
            }

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                // Sanity checks
                if (Math.Abs(B.Edges[0][i].Y - B.Edges[1][i].Y) < 20)
                {
                    B.Edges[0].RemoveAt(i);
                    B.Edges[1].RemoveAt(i);
                    i--;
                    continue;
                }
            }

            UInt16[] NewBoard = new UInt16[Buf.Length];

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                PalletPoint P2 = B.Edges[1][i];
                int SpanDir = Math.Sign(P2.Y - P1.Y);

                for (int y = P1.Y; y != (P2.Y + 1); y += SpanDir)
                {
                    int Src = y * CBW + P1.X;
                    NewBoard[Src] = Buf[Src];
                    Buf[Src] = 0;
                }
            }

            B.CB = new CaptureBuffer(NewBoard, SourceCB.Width, SourceCB.Height);
            AddCaptureBuffer(Name, B.CB);

            return B;
        }

        //=====================================================================
        private UInt16 FindModeVal(UInt16[] Modes, UInt16 MinVal = 1, UInt16 MaxVal = 65535)
        {
            UInt16 Mode = 0;
            UInt16 ModeVal = 0;


            // Ignore zero
            for (int i = MinVal; i < MaxVal; i++)
            {
                if (Modes[i] > Mode)
                {
                    Mode = Modes[i];
                    ModeVal = (UInt16)i;
                }
            }

            return ModeVal;
        }

        //=====================================================================
        private void CheckForRaisedNails(string BoardName)
        {
            throw new NotImplementedException();
        }

        //=====================================================================
        private void FindBoards()
        {
            throw new NotImplementedException();
        }


        //=====================================================================
        void RANSACLines(List<PalletPoint> Points, out PalletPoint bestPointA, out PalletPoint bestPointB)
        {
            // get transform child count
            int amount = Points.Count;
            Random R = new Random();

            // maximum distance to the line, to be considered as an inlier point
            float threshold = 25f;
            float bestScore = float.MaxValue;

            // results array (all the points within threshold distance to line)
            PalletPoint[] bestInliers = new PalletPoint[0];
            bestPointA = new PalletPoint();
            bestPointB = new PalletPoint();

            // how many search iterations we should do
            int iterations = 30;
            for (int i = 0; i < iterations; i++)
            {
                // take 2 points randomly selected from dataset
                int indexA = R.Next(0, amount);
                int indexB = R.Next(0, amount);
                PalletPoint pointA = Points[indexA];
                PalletPoint pointB = Points[indexB];

                // reset score and list for this round of iteration
                float currentScore = 0;
                // temporary list for points found in one search
                List<PalletPoint> currentInliers = new List<PalletPoint>();

                // loop all points in the dataset
                for (int n = 0; n < amount; n++)
                {
                    // take one PalletPoint form all points
                    PalletPoint p = Points[n];

                    // get distance to line, NOTE using editor only helper method
                    var currentError = DistancePointToLine(p, pointA, pointB);

                    // distance is within threshold, add to current inliers PalletPoint list
                    if (currentError < threshold)
                    {
                        currentScore += currentError;
                        currentInliers.Add(p);
                    }
                    else // outliers
                    {
                        currentScore += threshold;
                    }
                } // for-all points

                // check score for the best line found
                if (currentScore < bestScore)
                {
                    bestScore = currentScore;
                    bestInliers = currentInliers.ToArray();
                    bestPointA = pointA;
                    bestPointB = pointB;
                }
            } // for-iterations
        } // Start

        Board ProbeHorizontally(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location, int StartRow, int EndRow, int Step, int StartCol, int EndCol,ParamStorage paramStorage)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            List<PalletPoint> P = new List<PalletPoint>();

            int ExpLen = paramStorage.GetInt("Long Board Length");
            int ExpWid = paramStorage.GetInt("Board Width X Left");

            Board B = new Board(Name, Location, true, ExpLen, ExpWid);

            int Dir = Math.Sign(EndRow - StartRow);
            int Len = Buf.Length;

            List<int> Vals = new List<int>();

            for (int i = StartRow; i < EndRow; i += Step)
            {
                int Col0 = FindHorizontalSpan_Old(SourceCB, i, EndCol, StartCol);
                int Col1 = FindHorizontalSpan_Old(SourceCB, i, StartCol, EndCol);

                if ((Col0 != -1) && (Col1 != -1))
                {
                    B.Edges[0].Add(new PalletPoint(Math.Min(Col0, Col1), i));
                    B.Edges[1].Add(new PalletPoint(Math.Max(Col0, Col1), i));
                }
            }


            int FailWid = (int)(ExpWid * 1.2f);

            bool LastWasGood = false;
            for (int i = 1; i < B.Edges[0].Count - 1; i++)
            {
                int Delta1 = B.Edges[0][i].Y - B.Edges[0][i - 1].Y;
                int Delta2 = B.Edges[1][i].Y - B.Edges[1][i - 1].Y;

                if (LastWasGood && ((Delta1 > 1) && (Delta1 < 100) && (Delta2 == Delta1)))
                {
                    //LastWasGood = false;
                    B.Edges[0].Insert(i, new PalletPoint(B.Edges[0][i - 1].X, B.Edges[0][i - 1].Y + 1));
                    B.Edges[1].Insert(i, new PalletPoint(B.Edges[1][i - 1].X, B.Edges[1][i - 1].Y + 1));
                    //i--;
                }
                else
                    LastWasGood = true;
            }



            for (int i = 0; i < B.Edges[0].Count; i++)
            {

                // Sanity checks
                if (Math.Abs(B.Edges[0][i].X - B.Edges[1][i].X) < 20)
                {
                    B.Edges[0].RemoveAt(i);
                    B.Edges[1].RemoveAt(i);
                    i--;
                    continue;
                }
            }

            UInt16[] NewBoard = new UInt16[Buf.Length];

            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                PalletPoint P2 = B.Edges[1][i];
                int SpanDir = Math.Sign(P2.X - P1.X);

                for (int x = P1.X; x != (P2.X + 1); x += SpanDir)
                {
                    int Src = P1.Y * CBW + x;
                    NewBoard[Src] = Buf[Src];
                    Buf[Src] = 0;
                }
            }

            B.CB = new CaptureBuffer(NewBoard, SourceCB.Width, SourceCB.Height);
            AddCaptureBuffer(Name, B.CB);


            return B;
        }

        //=====================================================================
        public static float DistancePointToLine(PalletPoint p, PalletPoint a, PalletPoint b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return Math.Abs((b.X - a.X) * (a.Y - p.Y) - (a.X - p.X) * (b.Y - a.Y)) / distance;
        }

        private void CheckForInteriorDebris()
        {
            // Check inside top area
            int PixCount = 0;
            int TotalCount = 0;
            for (int x = BList[0].BoundsP1.X + 50; x < BList[1].BoundsP2.X - 50; x++)
            {
                for (int y = BList[0].BoundsP2.Y + 50; y < BList[1].BoundsP1.Y - 50; y++)
                {
                    UInt16 V = Denoised.Buf[y * Denoised.Width + x];
                    if (V > 0)
                        PixCount++;
                    TotalCount++;
                }
            }
            if ((PixCount / ((float)TotalCount)) > 0.02f)
            {
                AddDefect(BList[0], PalletDefect.DefectType.possible_debris, "Debris detected between H1 and H2");
                SetDefectMarker(BList[0].BoundsP1.X + 50, BList[0].BoundsP2.Y + 50, BList[1].BoundsP2.X - 50, BList[1].BoundsP1.Y - 50);
            }

            PixCount = 0;
            TotalCount = 0;
            for (int x = BList[1].BoundsP1.X + 50; x < BList[2].BoundsP2.X - 50; x++)
            {
                for (int y = BList[1].BoundsP2.Y + 50; y < BList[2].BoundsP1.Y - 50; y++)
                {
                    UInt16 V = Denoised.Buf[y * Denoised.Width + x];
                    if (V > 0)
                        PixCount++;
                    TotalCount++;
                }
            }
            if ((PixCount / ((float)TotalCount)) > 0.02f)
            {
                AddDefect(BList[1], PalletDefect.DefectType.possible_debris, "Debris detected between H2 and H3");
                SetDefectMarker(BList[1].BoundsP1.X + 50, BList[1].BoundsP2.Y + 50, BList[2].BoundsP2.X - 50, BList[2].BoundsP1.Y - 50);
            }
        }

        private CaptureBuffer DeNoise(CaptureBuffer SourceCB, ParamStorage paramStorage)
        {
            UInt16[] Source = SourceCB.Buf;
            UInt16[] Res = (UInt16[])Source.Clone();

            UInt16[] BaselineProf;
            if (paramStorage.Contains("BaselineData"))
            {
                BaselineProf = paramStorage.GetArray("BaselineData");

            }
            else
            {
                return null;
            }

            int leftEdge = paramStorage.GetInt("Raw Capture ROI Left (px)");
            int rightEdge = paramStorage.GetInt("Raw Capture ROI Right (px)");
            int clipMin = paramStorage.GetInt("Raw Capture ROI Min Z (px)");
            int clipMax = paramStorage.GetInt("Raw Capture ROI Max Z (px)");

            if (true)
            {
                for (int y = 0; y < SourceCB.Height; y++)
                    for (int x = 0; x < SourceCB.Width; x++)
                    {
                        int i = x + y * SourceCB.Width;
                        int v = Res[i];
                        if ((v > clipMax) || (v < clipMin)) Res[i] = 0;
                        if ((x < leftEdge) || (x > rightEdge)) Res[i] = 0;

                        if (Res[i] > 0)
                        {
                            if (BaselineProf[x] != 0)
                                Res[i] = (UInt16)(1000 + ((int)Res[i] - (int)BaselineProf[x]));
                            //Res[i] = (UInt16)(1000 - ((int)Res[i]));

                            if (Res[i] < 400)
                                Res[i] = 0;

                            if (Res[i] > 1500)
                                Res[i] = 0;
                        }
                    }
            }

            CaptureBuffer CB = new CaptureBuffer(Res, SourceCB.Width, SourceCB.Height);
            CB.PaletteType = CaptureBuffer.PaletteTypes.Baselined;

            if (true)
            {
                int topY = FindTopY(CB, paramStorage);
                for (int y = 0; y < topY; y++)
                    for (int x = 0; x < CB.Width; x++)
                    {
                        CB.Buf[y * CB.Width + x] = 0;
                    }
            }

            AddCaptureBuffer("Filtered", CB);
            return CB;
        }

        private void CheckIfDirectoryExists(string path)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                Console.WriteLine($"Directory created: {path}");
            }
        }
    }
}