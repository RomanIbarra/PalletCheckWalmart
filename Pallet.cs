using System.Windows;
using System;
using System.Collections.Generic;
//using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using PalletCheck.Controls;

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


        public void ProcessPalletHighPriority(Pallet P, Pallet.palletAnalysisCompleteCB Callback)
        {
            P.OnAnalysisComplete_User += Callback;
            P.UseBackgroundThread = false;

            lock (LockObject)
            {
                HighInputQueue.Add(P);
                UpdateHigh();
            }
        }

        public void OnHighPalletCompleteCB(Pallet P)
        {
            // This runs in the MAIN THREAD

            // Cleanup High Queue
            lock (LockObject)
            {
                if (HighInProgressList.Contains(P))
                    HighInProgressList.Remove(P);

                UpdateHigh();
            }

            // Call pallet complete callback for user
            P.CallUserCallback();
        }

        public void UpdateHigh()
        {
            lock (LockObject)
            {
                // Check if we can launch threads for items in the queues
                if ((HighInputQueue.Count>0) && (HighInProgressList.Count < MaxHighThreads))
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
                            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

                            try
                            {
                                P.DoAnalysisBlocking();
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
                                    OnHighPalletCompleteCB(P);
                                });
                            }
                        });
                    }
                }
            }
        }



        public void ProcessPalletLowPriority(Pallet P, Pallet.palletAnalysisCompleteCB Callback)
        {
            P.OnAnalysisComplete_User += Callback;
            P.UseBackgroundThread = true;

            lock (LockObject)
            {
                LowInputQueue.Add(P);
                UpdateLow();
            }
        }

        public void OnLowPalletCompleteCB(Pallet P)
        {
            // This runs in the MAIN THREAD

            // Cleanup Low Queue
            lock (LockObject)
            {
                if (LowInProgressList.Contains(P))
                    LowInProgressList.Remove(P);

                UpdateLow();
            }

            // Call pallet complete callback for user
            P.CallUserCallback();
        }

        public void UpdateLow()
        {
            lock (LockObject)
            {
                // Check if we can launch threads for items in the queues
                if ((LowInputQueue.Count>0) && (LowInProgressList.Count < MaxLowThreads))
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
                            P.DoAnalysisBlocking();
                            Thread.CurrentThread.Priority = TPBackup;
                        }
                        catch (Exception e)
                        {
                            Thread.CurrentThread.Priority = TPBackup;
                            Logger.WriteException(e);
                        }


                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            OnLowPalletCompleteCB(P);
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

        //public bool NotLive = false;
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

        public CaptureBuffer ReflectanceCB;

        public Dictionary<string, CaptureBuffer> CBDict = new Dictionary<string, CaptureBuffer>();
        public List<CaptureBuffer> CBList = new List<CaptureBuffer>();

        delegate CaptureBuffer addBufferCB(string s, UInt16[] buf);
        Thread[] BoardThreads;

        public delegate void palletAnalysisCompleteCB(Pallet P);
        public event palletAnalysisCompleteCB OnAnalysisComplete_User;

        public void CallUserCallback()
        {
            if(OnAnalysisComplete_User != null)
                OnAnalysisComplete_User(this);
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

            public List<PalletDefect> AllDefects;


            public Board(string Name, PalletDefect.DefectLocation Location, bool Horiz, int ExpectedLength, int ExpectedWidth)
            {
                Edges[0] = new List<PalletPoint>();
                Edges[1] = new List<PalletPoint>();
                Nails = new List<PalletPoint>();
                Cracks = new List<PalletPoint>();
                BoardName = Name;
                this.Location = Location;
                ExpectedAreaPix = ExpectedWidth * ExpectedLength;
                AllDefects = new List<PalletDefect>();
            }
        }

        public List<Board> BList;
        public List<PalletDefect> PalletLevelDefects = new List<PalletDefect>();


        //=====================================================================
        public Pallet(CaptureBuffer RangeCV, CaptureBuffer ReflCB)
        {
            Original = RangeCV;
            ReflectanceCB = ReflCB;
            CreateTime = DateTime.Now;
            Filename = String.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}.r3",
                                             CreateTime.Year, CreateTime.Month, CreateTime.Day, CreateTime.Hour, CreateTime.Minute, CreateTime.Second);

            Directory = String.Format("{0:0000}{1:00}{2:00}_{3:00}",
                                             CreateTime.Year, CreateTime.Month, CreateTime.Day, CreateTime.Hour);
        }

        public Pallet(string Filename)
        {
            Original = null;
            CreateTime = DateTime.Now;
            this.Filename = Filename;
        }

        ~Pallet()
        {
            Original = null;
            Denoised = null;
        }

        //=====================================================================

        //public void DoAsynchronousAnalysis()
        //{
        //    // Launches new thread then executes callback on caller thread

        //    Task.Factory.StartNew(() =>
        //    {
        //        try
        //        {
        //            DoAnalysis();
        //        }
        //        catch(Exception e)
        //        {
        //            Logger.WriteException(e);
        //        }

        //        Application.Current.Dispatcher.Invoke(() =>
        //        {
        //            if (this.OnAnalysisComplete_Processor != null)
        //                this.OnAnalysisComplete_Processor(this);
        //        });
        //    });

        //}

        //public void DoSynchronousAnalysis()
        //{
        //    // Stays on calling thread and blocks
        //    try
        //    {
        //        DoAnalysis();
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.WriteException(e);
        //    }

        //    if (OnAnalysisComplete_Processor != null)
        //        OnAnalysisComplete_Processor(this);
        //}
        ///
        ///Jack Added the stitch Method
        ///

        public void ProcessAndStitchBuffers(
    CaptureBuffer Original1, int startX1, int endX1,
    CaptureBuffer Original2, int startX2, int endX2,
    CaptureBuffer Original3, int startX3, int endX3,
    CaptureBuffer Reflectance1, CaptureBuffer Reflectance2, CaptureBuffer Reflectance3,
    int offset1, int offset2, int offset3,
    out CaptureBuffer StitchRange, out CaptureBuffer StitchRef)
        {
            // Calculate the new width and height
            int newWidth = (endX1 - startX1) + (endX2 - startX2) + (endX3 - startX3);
            int height = Original1.Height; // Assume all inputs have the same height

            UInt16[] buf = new UInt16[newWidth * height];
            ushort[] bufref = new ushort[newWidth * height];

            StitchRange = new CaptureBuffer(buf, newWidth, height);
            StitchRef = new CaptureBuffer(bufref, newWidth, height);

            StitchRange.Width = newWidth;
            StitchRange.Height = height;
            StitchRange.PaletteType = CaptureBuffer.PaletteTypes.Auto;

            StitchRef.Width = newWidth;
            StitchRef.Height = height;
            StitchRef.PaletteType = CaptureBuffer.PaletteTypes.Gray;

            int newIndex = 0;
            for (int i = 0; i < height; i++)
            {
                // Process the first input buffer
                for (int j = startX1; j < endX1; j++)
                {
                    ushort value = Original1.Buf[i * Original1.Width + j];
                    StitchRange.Buf[i * newWidth + newIndex] = value == 0 ? value : (UInt16)(value + offset1);
                    StitchRef.Buf[i * newWidth + newIndex] = Reflectance1.Buf[i * Reflectance1.Width + j];
                    newIndex++;
                }

                // Process the second input buffer
                for (int j = startX2; j < endX2; j++)
                {
                    ushort value = Original2.Buf[i * Original2.Width + j];
                    StitchRange.Buf[i * newWidth + newIndex] = value == 0 ? value : (UInt16)(value + offset2);
                    StitchRef.Buf[i * newWidth + newIndex] = Reflectance2.Buf[i * Reflectance2.Width + j];
                    newIndex++;
                }

                // Process the third input buffer
                for (int j = startX3; j < endX3; j++)
                {
                    ushort value = Original3.Buf[i * Original3.Width + j];
                    StitchRange.Buf[i * newWidth + newIndex] = value == 0 ? value : (UInt16)(value + offset3);
                    StitchRef.Buf[i * newWidth + newIndex] = Reflectance3.Buf[i * Reflectance3.Width + j];
                    newIndex++;
                }

                // Reset newIndex
                newIndex = 0;
            }
        }
        // Jack Add the method for calculate the baseline data
        public void SaveColumnAverages(CaptureBuffer StitchRange, string filePath)
        {
            int width = StitchRange.Width;
            int height = StitchRange.Height;
            int[] columnAverages = new int[width];

            // Calculate the average value for each column and round to the nearest integer
            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                int count = 0;
                for (int y = 0; y < height; y++)
                {
                    int value = StitchRange.Buf[y * width + x];
                    if (value != 0)
                    {
                        sum += value;
                        count++;
                    }
                }

                // Avoid division by zero by checking the count
                if (count > 0)
                {
                    columnAverages[x] = (int)Math.Round((double)sum / count);
                }
                else
                {
                    columnAverages[x] = 0; // Or any other default value if there are no non-zero values
                }
                columnAverages[x] = 5700;
            }

            // Create a comma-separated string of averages
            string averagesString = string.Join(",", columnAverages);

            // Write the string to a text file
            File.WriteAllText(filePath, averagesString);
        }

        /// Jack add the method to find


        //=====================================================================
        /// <summary>
        /// DoAnalysis
        /// </summary>
        public void DoAnalysisBlocking()
        {
            AnalysisStartTime = DateTime.Now;
            Busy = true;
            Logger.WriteLine("Pallet::DoAnalysis START");

            if (Original == null)
            {
                Original = new CaptureBuffer();
                Original.Load(Filename);
            }

            if (ReflectanceCB == null)
            {
                string reflFilename = Filename.Replace("_rng.r3", "_rfl.r3");
                if (File.Exists(reflFilename))
                {
                    ReflectanceCB = new CaptureBuffer();
                    ReflectanceCB.PaletteType = CaptureBuffer.PaletteTypes.Gray;
                    ReflectanceCB.Load(reflFilename);
                }
            }

            SaveColumnAverages(Original, "D:\\Test.txt");

            //       UInt16[] buf = new UInt16[(Original.Width * 2 -1000) * Original.Height];
            //       ushort[] bufref = new ushort[(Original.Width * 2 - 1000) * Original.Height];




            //       // Jack Test the stitching without EasyRanger
            //       CaptureBuffer originalBuffer1 = Original;// initialize your first original buffer
            //       int startX1 = 0; // example startX for the first buffer
            //       int endX1 = 1800; // example endX for the first buffer

            //       CaptureBuffer originalBuffer2 = Original;// initialize your second original buffer
            //       int startX2 =1800; // example startX for the second buffer
            //       int endX2 = 2200; // example endX for the second buffer

            //       CaptureBuffer originalBuffer3 = Original;// initialize your third original buffer
            //       int startX3 = 2200; // example startX for the third buffer
            //       int endX3 = 2560; // example endX for the third buffer

            //       CaptureBuffer reflectanceBuffer1 = ReflectanceCB;// initialize your first reflectance buffer
            //       CaptureBuffer reflectanceBuffer2 = ReflectanceCB;// initialize your second reflectance buffer
            //       CaptureBuffer reflectanceBuffer3 = ReflectanceCB;// initialize your third reflectance buffer

            //       int offset1 = 0; // example offset for the first buffer
            //       int offset2 = 0; // example offset for the second buffer
            //       int offset3 = 0; // example offset for the third buffer
            //       CaptureBuffer stitchRange;
            //       CaptureBuffer stitchRef;

            //       ProcessAndStitchBuffers(
            //originalBuffer1, startX1, endX1,
            //originalBuffer2, startX2, endX2,
            //originalBuffer3, startX3, endX3,
            //reflectanceBuffer1, reflectanceBuffer2, reflectanceBuffer3,
            //offset1, offset2, offset3,
            //out stitchRange, out stitchRef);
            //       //Pallet Pstitch = new Pallet(StitchRange, StitchRef);
            //       SaveColumnAverages(stitchRange, @"D:\column_averages.txt");


            //       //if(false)
            //       //{
            //       //    if (Original != null) Original.FlipTopBottom();
            //       //    if (ReflectanceCB != null) ReflectanceCB.FlipTopBottom();
            //       //}

            //       AddCaptureBuffer("Image", stitchRef);

            //       Logger.WriteLine(String.Format("DoAnalysisBlocking: Original W,H  {0}  {0}", Original.Width, Original.Height));
            //       AddCaptureBuffer("Original", stitchRange);

            //       Denoised = DeNoise(stitchRange);


            AddCaptureBuffer("Image", ReflectanceCB);

            Logger.WriteLine(String.Format("DoAnalysisBlocking: Original W,H  {0}  {0}", Original.Width, Original.Height));
            AddCaptureBuffer("Original", Original);

            Denoised = DeNoise(Original);
            if (Denoised == null)
            {
                Logger.WriteLine("DeNoise() returned a NULL capturebuffer!");
                return;
            }

            Logger.WriteLine(String.Format("DoAnalysisBlocking: Denoised W,H  {0}  {0}", Denoised.Width, Denoised.Height));
            //AddCaptureBuffer("DenoisedA", Denoised);


            IsolateAndAnalyzeSurfaces(Denoised);

            if ( State == InspectionState.Fail )
            {
                Logger.WriteLine("Pallet::DoAnalysis INSPECTION FAILED");
                AnalysisStopTime = DateTime.Now;
                AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
                return;
            }

            // Final board size sanity check
            int ExpWidH  = (int)ParamStorage.GetPixY("H Board Width (in)");
            int ExpWidV1 = (int)ParamStorage.GetPixX("V1 Board Width (in)");
            int ExpWidV2 = (int)ParamStorage.GetPixX("V2 Board Width (in)");


            // Check for debris causing unusually wide boards
            if (true)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (BList[i].AllDefects.Count == 0)
                        continue;

                    int H_Wid = BList[i].BoundsP2.Y - BList[i].BoundsP1.Y;
                    if (H_Wid > (ExpWidH * 1.3))
                    {
                        AddDefect(BList[i], PalletDefect.DefectType.possible_debris, "Unusually wide board");
                        SetDefectMarker(BList[i]);
                    }
                }

                if (BList[3].AllDefects.Count > 0)
                {
                    int V1_Wid = BList[3].BoundsP2.X - BList[3].BoundsP1.X;
                    if (V1_Wid > (ExpWidV1 * 1.3))
                    {
                        AddDefect(BList[3], PalletDefect.DefectType.possible_debris, "Unusually wide board");
                        SetDefectMarker(BList[3]);
                    }
                }

                if (BList[4].AllDefects.Count > 0)
                {
                    int V1_Wid = BList[4].BoundsP2.X - BList[4].BoundsP1.X;
                    if (V1_Wid > (ExpWidV1 * 1.3))
                    {
                        AddDefect(BList[4], PalletDefect.DefectType.possible_debris, "Unusually wide board");
                        SetDefectMarker(BList[4]);
                    }
                }
            }


            if (AllDefects.Count > 0)
                CheckForInteriorDebris();


            State = AllDefects.Count > 0 ? InspectionState.Fail : InspectionState.Pass;


            Benchmark.Stop("Total");

            string Res = Benchmark.Report();





            //System.Windows.MessageBox.Show(Res);

            //int nDefects = 0;
            //for (int i = 0; i < BList.Count; i++)
            //{
            //    StringBuilder SB = new StringBuilder();

            //    foreach (PalletDefect BD in BList[i].AllDefects)
            //    {
            //        string DefectName = BD.Name.ToString();
            //        DefectName = DefectName.Replace('_', ' ');
            //        DefectName = DefectName.ToUpper();
            //        SB.AppendLine(DefectName);
            //        //MainWindow.WriteLine("[" + BList[i].BoardName + "] " + DefectName);
            //        nDefects++;
            //    }

            //    string Def = SB.ToString();
            //    if ( !string.IsNullOrEmpty(Def) )
            //    {
            //        int CX = (BList[i].BoundsP1.X + BList[i].BoundsP2.X) / 2;
            //        int CY = (BList[i].BoundsP1.Y + BList[i].BoundsP2.Y) / 2;
            //        System.Windows.Point C = new System.Windows.Point(CX, CY);
            //        int W = Math.Abs(BList[i].BoundsP1.X - BList[i].BoundsP2.X);
            //        int H = Math.Abs(BList[i].BoundsP1.Y - BList[i].BoundsP2.Y); 

            //        //Original.AddMarker_Rect(C, W, H, Def);
            //        //Denoised.AddMarker_Rect(C, W, H, Def);
            //        //Original.MarkDefect(C, W, H, Def);
            //        //Denoised.MarkDefect(C, W, H, Def);

            //    }
            //}


            //MainWindow.SetResult(nDefects == 0 ? "P" : "F", nDefects == 0 ? Color.Green : Color.Red);



            //MainWindow.WriteLine(string.Format("Analysis took {0} ms\n\n", (Stop - Start).TotalMilliseconds));
            Busy = false;
            AnalysisStopTime = DateTime.Now;
            AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
            Logger.WriteLine(String.Format("Pallet::DoAnalysis FINISHED  -  {0:0.000} sec", AnalysisTotalSec));

        }

        //=====================================================================




        //=====================================================================
        private void CheckForInteriorDebris()
        {
            // Check inside top area
            int PixCount = 0;
            int TotalCount = 0;
            for ( int x = BList[0].BoundsP1.X + 50; x < BList[1].BoundsP2.X-50; x++ )
            {
                for ( int y = BList[0].BoundsP2.Y + 50; y < BList[1].BoundsP1.Y - 50; y++)
                {
                    UInt16 V = Denoised.Buf[y * Denoised.Width + x];
                    if (V > 0)
                        PixCount++;
                    TotalCount++;
                }
            }
            if ( (PixCount/((float)TotalCount)) > 0.02f )
            {
                AddDefect(BList[0], PalletDefect.DefectType.possible_debris,"Debris detected between H1 and H2");
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

        //=====================================================================
        private CaptureBuffer DeNoise(CaptureBuffer SourceCB)
        {
            UInt16[] Source = SourceCB.Buf;
            UInt16[] Res = (UInt16[])Source.Clone();

            UInt16[] BaselineProf;
            if (ParamStorage.Contains("BaselineData"))
            {
                BaselineProf = ParamStorage.GetArray("BaselineData");
                
            }
            else 
            {
                return null;
            }



            int leftEdge = ParamStorage.GetInt("Raw Capture ROI Left (px)");
            int rightEdge = ParamStorage.GetInt("Raw Capture ROI Right (px)");
            int clipMin = ParamStorage.GetInt("Raw Capture ROI Min Z (px)");
            int clipMax = ParamStorage.GetInt("Raw Capture ROI Max Z (px)");

            if (true)
            {
                for (int y = 0; y < SourceCB.Height; y++)
                    for (int x = 0; x < SourceCB.Width; x++)
                    {
                        int i = x + y * SourceCB.Width;
                        int v = Res[i];
                        if ((v > clipMax) || (v < clipMin)) Res[i] = 0;
                        if ((x< leftEdge) || (x > rightEdge)) Res[i] = 0;

                            if (Res[i] > 0)
                        {
                            if (BaselineProf[x] != 0)
                                Res[i] = (UInt16)(1000 + ((int)Res[i] - (int)BaselineProf[x]));
                            //Res[i] = (UInt16)(1000 - ((int)Res[i]));

                            if (Res[i] < 900)
                                Res[i] = 0;

                            if (Res[i] > 1200)
                                Res[i] = 0;
                        }
                    }
            }

            CaptureBuffer CB = new CaptureBuffer(Res, SourceCB.Width, SourceCB.Height);
            CB.PaletteType = CaptureBuffer.PaletteTypes.Baselined;

            if (true)
            {
                int topY = FindTopY(CB);
                for (int y = 0; y < topY; y++)
                    for (int x = 0; x < CB.Width; x++)
                    {
                        CB.Buf[y * CB.Width + x] = 0;
                    }
            }

            AddCaptureBuffer("Filtered", CB);
            return CB;
        }

        //=====================================================================
        void AddCaptureBuffer(string Name, CaptureBuffer CB)
        {
            CB.Name = Name;
            CBDict.Add(Name, CB);
            CBList.Add(CB);
        }


        //=====================================================================
        //void AddCaptureBuffer(string Name, UInt16[] Buf, out CaptureBufferBrowser CBB )
        //{
        //    CaptureBufferBrowser CB = (CaptureBufferBrowser)
        //        MainWindow.Singleton.Dispatcher.Invoke(new addBufferCB(MainWindow.AddCaptureBufferBrowser), new object[] { Name, Buf });

        //    CBB = CB;

        //    return CB.Buf;
        //}

        //=====================================================================
        private void DeNoiseInPlace(CaptureBuffer CB)
        {
            UInt16[] Source = CB.Buf;
            UInt16[] Res = (UInt16[])CB.Buf.Clone();

            int leftEdge = ParamStorage.GetInt("Raw Capture ROI Left (px)");
            int rightEdge = ParamStorage.GetInt("Raw Capture ROI Right (px)");

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
        private int FindTopY(CaptureBuffer SourceCB)
        {
            //
            // !!! Needs to work with Original and Denoised Buffers
            //

            UInt16[] Source = SourceCB.Buf;
            int LROI = ParamStorage.GetInt("Raw Capture ROI Left (px)");
            int RROI = ParamStorage.GetInt("Raw Capture ROI Right (px)");
            //int MinZTh = ParamStorage.GetInt("Raw Capture ROI Min Z (px)");
            //int MaxZTh = ParamStorage.GetInt("Raw Capture ROI Max Z (px)");

            int[] Hist = new int[100];

            int ylimit = Math.Min(999,(int)(SourceCB.Height / 2));
            for (int x = LROI; x < RROI; x += 10)
            {
                for (int y = 0; y < ylimit; y += 10)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];

                    if (Val!=0)//(Val >= MinZTh) && (Val <= MaxZTh))
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
            for(int y= InitialY; y>0; y--)
            {
                bool NotClear = true;
                for (int x = 0; x < SourceCB.Width; x++)
                {
                    UInt16 Val = Source[y * SourceCB.Width + x];

                    //if ((Val >= MinZTh) && (Val <= MaxZTh))
                    if(Val != 0)
                    {
                        NotClear = true;
                    }
                }
                if (!NotClear)
                {
                    return y;
                }
            }
            return Math.Max(0,InitialY-40);
        }

        //=====================================================================
        private void IsolateAndAnalyzeSurfaces(CaptureBuffer SourceCB)
        {
            int nRows = SourceCB.Height;
            int nCols = SourceCB.Width;

            int LROI = ParamStorage.GetInt("Raw Capture ROI Left (px)");//260px
            int RROI = ParamStorage.GetInt("Raw Capture ROI Right (px)"); //2440px
            int LWid = ParamStorage.GetPixX("V1 Board Width (in)");  //140mm
            int RWid = ParamStorage.GetPixX("V2 Board Width (in)");  //140mm
            int HBoardWid = ParamStorage.GetPixY("H Board Width (in)"); //140mm
            int LBL = ParamStorage.GetPixY("V Board Length (in)"); //1018mm

            //UInt16[] Isolated = (UInt16[])(SourceCB.Buf.Clone());

            int SX = LROI + (LWid / 2);    // Jack Note: Get the Position of the start X 
            int EX = RROI - (RWid / 2);

            int TopY = FindTopY(SourceCB);  // Jack Note: Find the first row that is not all black 

            BList = new List<Board>();

            if (TopY < 0)
            {
                AddDefect(null, PalletDefect.DefectType.board_segmentation_error, "Could not lock onto top board");
                State = InspectionState.Fail;
                return;
            }

            /// Find the Horizontal Region (Jack)
            ///Jack Note: Set the ROI for each board, Not very accurate

            int H1_T = Math.Max(0, TopY-20);// - 100);
            int H1_B = TopY + HBoardWid + 360;
            Logger.WriteLine(string.Format("TopY:{0}  H1_T:{1}  H1_B:{2}", TopY, H1_T, H1_B));

            int H2_T = TopY + (LBL / 2) - 400;
            int H2_B = TopY + (LBL / 2) + 400;
            //Logger.WriteLine(string.Format("ISOLATE H2_T,H2_B {0} {1}", H2_T, H2_B));

            int H3_T = TopY + LBL - HBoardWid - 250;
            int H3_B = TopY + LBL + 200;

            H3_B = Math.Min(SourceCB.Height - 1, H3_B);

            // The ProbeCB gets modified as boards are found and subtracted...so need a unique copy
            // Jack Note: Get all boards with edge and so on
            CaptureBuffer ProbeCB = new CaptureBuffer(SourceCB);
            Board H1 = ProbeVertically(ProbeCB, "H1", PalletDefect.DefectLocation.H1, SX, EX, 1, H1_T, H1_B);
            Board H2 = ProbeVertically(ProbeCB, "H2", PalletDefect.DefectLocation.H2, SX, EX, 1, H2_T, H2_B);
            Board H3 = ProbeVertically(ProbeCB, "H3", PalletDefect.DefectLocation.H3, SX, EX, 1, H3_T, H3_B);
            Board V1 = ProbeHorizontally(ProbeCB, "V1", PalletDefect.DefectLocation.V1,H1_T, H3_B, 1, LROI, LROI + (int)(LWid * 2));
            Board V2 = ProbeHorizontally(ProbeCB, "V2", PalletDefect.DefectLocation.V2,H1_T, H3_B, 1, RROI, RROI - (int)(RWid * 2));

            BList.Add(H1);
            BList.Add(H2);
            BList.Add(H3);
            BList.Add(V1);
            BList.Add(V2);


            // Sanity check edges
            for ( int i = 0; i < 3; i++ )
            {
                Board B = BList[i];
                // Jack Note:Check there are enough edges points or not, If not, must be defect
                if ( B.Edges[0].Count < 30 )
                {
                    AddDefect(null, PalletDefect.DefectType.board_segmentation_error, "Too little edge info on " + B.BoardName);
                    return;
                }
                // Jack Note: If fisrt width is larger than the next width * 1.2, Then set First value = Next value
                for ( int j = 30; j >= 0; j-- )
                {
                    if ( (B.Edges[1][j].Y - B.Edges[0][j].Y) > ((B.Edges[1][j+1].Y - B.Edges[0][j+1].Y)*1.2f) )
                    {
                        B.Edges[0][j] = B.Edges[0][j + 1];
                        B.Edges[1][j] = B.Edges[1][j + 1];
                    }
                }
                // Jack Note: The same as above
                for (int j = 30; j > 0; j--)
                {
                    int w = B.Edges[0].Count;
                    if ((B.Edges[1][w-j].Y - B.Edges[0][w-j].Y) > ((B.Edges[1][w-j-1].Y - B.Edges[0][w-j-1].Y) * 1.2f))
                    {
                        B.Edges[0][w-j] = B.Edges[0][w-j-1];
                        B.Edges[1][w-j] = B.Edges[1][w-j-1];
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Board B = BList[i]; // Get the current board from the list

                // Check if there are enough edge points in the first set
                if (B.Edges[0].Count < 30)
                {
                    // If not, mark the board as defective due to insufficient edge information
                    AddDefect(null, PalletDefect.DefectType.board_segmentation_error, "Too little edge info on " + B.BoardName);
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





            // Analyze the boards 
            // Jack Note: One board on thread
            if (BoardThreads == null)
                BoardThreads = new Thread[5];

            for (int i = 0; i < BList.Count; i++)
            {
                if (BoardThreads[i] == null)
                    BoardThreads[i] = new Thread(ProcessBoard);

                BoardThreads[i].Start(BList[i]);
            }

            bool AllDone = false;

            while (!AllDone)
            {
                Thread.Sleep(1);
                AllDone = true;
                for (int i = 0; i < BoardThreads.Length; i++)
                {
                    if (BoardThreads[i].ThreadState != ThreadState.Stopped)
                    {
                        AllDone = false;
                        break;
                    }
                }
            }

            for (int i = 0; i < BList.Count; i++)
            {
                Board B = BList[i];

                CaptureBuffer CB = new CaptureBuffer(BList[i].CrackCB);
                AddCaptureBuffer(B.BoardName + " Crack M", CB);

                // Draw the cracks
                int ny = B.CrackTracker.GetLength(0);
                int nx = B.CrackTracker.GetLength(1);
                float MaxVal = B.CrackBlockSize * B.CrackBlockSize;
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        if (B.CrackTracker[y, x] != 0)
                        {
                            int V = B.CrackTracker[y, x];
                            int x1 = B.BoundsP1.X + (x * B.CrackBlockSize);
                            int y1 = B.BoundsP1.Y + (y * B.CrackBlockSize);

                            float pct = B.CrackTracker[y, x] / MaxVal;
                            //if (pct > BlockCrackMinPct)
                            {
                                //DrawBlock(B.CrackBuf, x1, y1, x1 + B.CrackBlockSize, y1 + B.CrackBlockSize, 16 );

                                // TODO-UNCOMMENT
                                //MakeBlock(B.CrackCB, x1 + 8, y1 + 8, 16, (UInt16)(B.CrackTracker[y, x] * 100));
                                MakeBlock(B.CrackCB, x1 + 8, y1 + 8, 16, (UInt16)(B.CrackTracker[y, x] * 100));
                            }
                        }

                        if (B.BoundaryBlocks[y, x] != 0)
                        {
                            int x1 = B.BoundsP1.X + (x * B.CrackBlockSize);
                            int y1 = B.BoundsP1.Y + (y * B.CrackBlockSize);

                            //DrawBlock(B.CrackCB, x1, y1, x1 + B.CrackBlockSize, y1 + B.CrackBlockSize, 5500);
                            MakeBlock(B.CrackCB, x1 + 8, y1 + 8, 16, 5500);
                        }

                    }
                }

                AddCaptureBuffer(B.BoardName + " Crack T", BList[i].CrackCB);
            }
        }

        //=====================================================================
        //private void ClearAboveH1(ushort[] isolated, Board h1)
        //{
        //    int minY = int.MaxValue;
        //    //B.Edges[0][i].Y - B.Edges[1][i].Y

        //    if ((h1.Edges[0].Count == 0) || (h1.Edges[1].Count == 0))
        //        return;


        //    for (int x = 0; x < h1.Edges.Length; x++)
        //    {
        //        if (h1.Edges[0][x].Y < minY)
        //            minY = h1.Edges[0][x].Y;
        //    }

        //    for (int y = 0; y < minY; y++)
        //    {
        //        for (int x = 0; x < MainWindow.SensorWidth; x++)
        //            isolated[y * MainWindow.SensorWidth + x] = 0;
        //    }
        //}

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

        //=====================================================================
        private void ProcessBoard(object _B)
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
                FindCracks(B);
                FindRaisedBoard(B);
                FindRaisedNails(B);
                CalculateMissingWood(B);
                CheckForBreaks(B);


                // Jack Note: Retrieve the minimum width for a board to be considered too narrow
                float FailWidIn = ParamStorage.GetFloat("(BN) Narrow Board Minimum Width (in)");

                // Jack Note: Check if the board is too narrow based on the minimum width parameter
                bool TooNarrow = CheckNarrowBoard(B, FailWidIn, 0.5f, true);
                if (TooNarrow)
                {
                    // Jack Note: Mark the board as defective for being too narrow and set defect marker
                    AddDefect(B, PalletDefect.DefectType.board_too_narrow, "Min width was less than " + FailWidIn.ToString());
                    SetDefectMarker(B);
                }

                // Jack Note: Retrieve the minimum width for a missing chunk of wood
                FailWidIn = ParamStorage.GetFloat("(BN) Missing Chunk Minimum Width (in)");

                // Jack Note: Retrieve the minimum length for a missing chunk of wood
                float MinLengthIn = ParamStorage.GetFloat("(BN) Missing Chunk Minimum Length (in)");

                // Jack Note: Check if the board has a missing chunk of wood based on the minimum width and length parameters
                bool MissingWoodChunk = CheckNarrowBoard(B, FailWidIn, MinLengthIn, true);
                if (MissingWoodChunk)
                {
                    // Jack Note: Mark the board as defective for having a missing chunk and set defect marker
                    AddDefect(B, PalletDefect.DefectType.missing_wood, "Missing chunk too deep and too long");
                    SetDefectMarker(B);
                }


            }
            catch (Exception E)
            {
                Logger.WriteException(E);
                AddDefect(B, PalletDefect.DefectType.board_segmentation_error,"Exception thrown in ProcessBoard()");
                return;
            }

        }

        //=====================================================================

        
        // Jack Note: Count the Number of the distance between the Edge0 and Edge1 < FailWidth
        private bool CheckNarrowBoard(Board B, float FailWidIn, float MinMissingWoodLengthIn, bool ExcludeEnds = false)
        {
            // Jack Note: Initialize variables for pixels per inch (PPI) and measured length
            float PPIX;
            float PPIY;
            float MeasuredLength = 0;

            // Jack Note: Retrieve PPI values from parameter storage
            PPIY = ParamStorage.GetPPIY();
            PPIX = ParamStorage.GetPPIX();

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
                // Jack Note: Calculate the measured length in inches for horizontal orientation
                MeasuredLength = nBadEdges / PPIX;
            }
            else
            {
                // Jack Note: Calculate the fail width in pixels for vertical orientation
                int FailWid = (int)(FailWidIn * PPIX);

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
        private void FindCracks(Board B)
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

            B.CrackBlockSize = ParamStorage.GetInt("Crack Tracker Block Size");

            int MinDelta = ParamStorage.GetInt("Crack Tracker Min Delta");

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


                        for ( int yy = y-7; yy < (y+7); yy++ )
                        {
                            for ( int xx = x-7; xx < (x+7); xx++ )
                            {
                                UInt16 testVal = (UInt16)(Math.Abs((int)B.CB.Buf[yy * B.CB.Width + xx] - Val1) );
                                MaxDif = testVal > MaxDif ? testVal : MaxDif;
                            }
                        }

                        //UInt16 Val2x = B.CB.Buf[y * MainWindow.SensorWidth + (x + 5)];
                        //UInt16 Val2y = B.CB.Buf[(y + 5) * MainWindow.SensorWidth + x];
                        //UInt16 Val3x = (UInt16)(Math.Abs((int)Val1 - (int)Val2x));
                        //UInt16 Val3y = (UInt16)(Math.Abs((int)Val1 - (int)Val2y));

                        // if ((Val3x > MinDelta) || (Val3y > MinDelta))
                        if ( MaxDif > MinDelta )
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


            NullifyNonROIAreas(B);

            // Remove speckle
            // Jack Note: Filter
            DeNoiseInPlace(B.CrackCB);


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
            float BlockCrackMinPct = ParamStorage.GetFloat("Crack Tracker Min Pct");
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
            IsCrackABrokenBoard(B);

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

            //Console.WriteLine(B.BoardName + " has " + FloodCode.ToString() + " unique cracks");
        }

        //=====================================================================
        void NullifyNonROIAreas(Board B)
        {
            float EdgeWidH = ParamStorage.GetFloat("HBoard Edge Crack Exclusion Zone Percentage");
            float EdgeWidV = ParamStorage.GetFloat("VBoard Edge Crack Exclusion Zone Percentage");
            float CenWid = ParamStorage.GetFloat("Board Center Crack Exclusion Zone Percentage");
            // float CrackBlockValueThreshold = ParamStorage.GetFloat("Crack Block Value Threshold");

            int w = B.CrackTracker.GetLength(1);
            int h = B.CrackTracker.GetLength(0);
            int CBW = B.CrackCB.Width;

            bool isHoriz = w > h;

            if (isHoriz)
            {
                float Len = ParamStorage.GetPixX("H Board Length (in)");

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
                float Len = ParamStorage.GetPixY("V Board Length (in)");

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
                    for (int x = B.BoundsP1.X; x < B.BoundsP2.X; x++ )
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
        void IsCrackABrokenBoard(Board B)
        {
            /// Jack Note
            /// The code checks for cracks that span the entire width or height 
            /// of the board by looking for connections between the edges.
            /// If a crack is found that connects one edge to the opposite edge 
            /// and meets certain conditions, it marks the board as defective.
            int BlockSize = ParamStorage.GetInt("Crack Tracker Block Size");

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
                for (int y = 2; y < h-2; y++)
                {
                    for (int x = 2; x < w-2; x++)
                    {
                        if (B.CrackTracker[y, x] > 0)
                        {
                            int cid = B.CrackTracker[y, x];

                            //if ((x == 0) || (x == 1) || (x == (B.CrackTracker.GetLength(1) - 1)) || (x == (B.CrackTracker.GetLength(1) - 2)))
                            if ((x == 0))
                                TouchesEdge1[cid] = x;
                            else
                            if(x == (w - 1))
                                TouchesEdge2[cid] = x;
                            else
                            {
                                if ((B.BoundaryBlocks[y, x-1] == 1) || (B.BoundaryBlocks[y, x - 2] == 1))// && (B.BoundaryBlocks[y, x + 1] == 0) && (B.BoundaryBlocks[y, x + 1] == 0))
                                    TouchesEdge1[cid] = x;

                                if ((B.BoundaryBlocks[y, x+1] == 1) || (B.BoundaryBlocks[y, x + 2] == 1))// && (B.BoundaryBlocks[y, x - 1] == 0) && (B.BoundaryBlocks[y, x - 1] == 0))
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
                    if (Math.Abs(TouchesEdge1[i] - TouchesEdge2[i]) > 5)
                    {
                        AddDefect(B, PalletDefect.DefectType.broken_across_width, String.Format("Crack touches both edges."));
                        SetDefectMarker(B);
                        return;
                    }
                }
            }

            return;

            ////// i = crack_id
            ////for (int i = 2; i < 99; i++)
            ////{
            ////    int minx = int.MaxValue;
            ////    int miny = int.MaxValue;
            ////    int maxx = int.MinValue;
            ////    int maxy = int.MinValue;

            ////    for (int y = 0; y < h; y++)
            ////    {
            ////        for (int x = 0; x < w; x++)
            ////        {
            ////            if (B.CrackTracker[y, x] == i)
            ////            {
            ////                minx = Math.Min(minx, x);
            ////                maxx = Math.Max(maxx, x);
            ////                miny = Math.Min(miny, y);
            ////                maxy = Math.Max(maxy, y);
            ////            }
            ////        }
            ////    }

            ////    // No cracks with this id == we're done
            ////    if (minx == int.MaxValue)
            ////        break;                

            ////    if (isHoriz)
            ////    {
            ////        int sx = minx * BlockSize;
            ////        int ex = maxx * BlockSize;
            ////        int sy = miny * BlockSize;
            ////        int ey = maxy * BlockSize;
            ////        int ylen = (maxy - miny + 1) * BlockSize;

            ////        //// Quick debris check
            ////        //int HyperHighPix = 0;
            ////        //int TotalPix = 0;
            ////        //for (int x = sx; x <= ex; x++)
            ////        //{
            ////        //    int SX = B.BoundsP1.X;
            ////        //    int SY = B.BoundsP1.Y;
            ////        //    for ( int y = SY+sy; y <= (SY+ey); y++ )
            ////        //    {
            ////        //        TotalPix++;
            ////        //        if (B.CB.Buf[y * MainWindow.SensorWidth + (SX+x)] > 1100)
            ////        //            HyperHighPix++;
            ////        //    }
            ////        //}

            ////        for (int x = sx; x <= ex; x++)
            ////        {
            ////            int Wid = (int)((B.Edges[1][x].Y - B.Edges[0][x].Y) * MaxCrackWid);
            ////            if (ylen > Wid)
            ////            {
            ////                AddDefect(B, PalletDefect.DefectType.broken_across_width);

            ////                // Only now should I flag debris
            ////                //if ((((float)HyperHighPix) / TotalPix) > MaxDebrisPc)
            ////                //    AddDefect(B, PalletDefect.DefectType.possible_debris);
            ////            }
            ////        }
            ////    }
            ////    else
            ////    {
            ////        int sy = miny * BlockSize;
            ////        int ey = maxy * BlockSize;
            ////        int xlen = (maxx - minx + 1) * BlockSize;

            ////        for (int y = sy; y <= ey; y++)
            ////        {
            ////            int Wid = (int)((B.Edges[1][y].X - B.Edges[0][y].X) * MaxCrackWid);
            ////            if (xlen > Wid)
            ////            {
            ////                AddDefect(B, PalletDefect.DefectType.broken_across_width);
            ////            }
            ////        }
            ////    }
            ////}
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
        private void CheckForBreaks(Board B)
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
            float BoardLenPerc = ParamStorage.GetFloat("(SH) Min Board Len (%)") / 100.0f;

            // Jack Note: Check if the board is oriented horizontally
            if (B.Edges[0][0].X == B.Edges[1][0].X)
            {
                // Jack Note: Retrieve the expected horizontal board length and calculate minimum acceptable length
                int Len = (int)ParamStorage.GetPixX("H Board Length (in)");
                int MinLen = (int)(Len * BoardLenPerc);

                // Jack Note: Calculate the measured length of the board
                int MeasLen = B.Edges[0][B.Edges[0].Count - 1].X - B.Edges[0][0].X;

                // Jack Note: Check if the measured length is less than the minimum acceptable length
                if (MeasLen < MinLen)
                {
                    // Jack Note: Mark the board as too short and set defect marker
                    AddDefect(B, PalletDefect.DefectType.board_too_short, "Board len " + MeasLen.ToString() + " shorter than " + MinLen.ToString());
                    SetDefectMarker(B);
                    return;
                }
            }
            else
            {
                // Jack Note: Board is vertically oriented
                // Jack Note: Retrieve the expected vertical board length and calculate minimum acceptable length
                int Len = (int)ParamStorage.GetPixY("V Board Length (in)");
                int MinLen = (int)(Len * BoardLenPerc);

                // Jack Note: Calculate the measured length of the board
                int MeasLen = B.Edges[0][B.Edges[0].Count - 1].Y - B.Edges[0][0].Y;

                // Jack Note: Check if the measured length is less than the minimum acceptable length
                if (MeasLen < MinLen)
                {
                    // Jack Note: Mark the board as too short and set defect marker
                    AddDefect(B, PalletDefect.DefectType.board_too_short, "Board len " + MeasLen.ToString() + " shorter than " + MinLen.ToString());
                    SetDefectMarker(B);
                    return;
                }
            }

        }

        //=====================================================================
        private void CalculateMissingWood(Board B)
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
            float Allowable = ParamStorage.GetFloat("(MW) Max Allowed Missing Wood (%)");

            // Jack Note: Check if the missing wood percentage exceeds the allowable threshold
            if (MissingWoodPct > Allowable)
            {
                // Jack Note: Add defect for missing too much wood and set defect marker
                AddDefect(B, PalletDefect.DefectType.missing_wood, "Missing too much wood. " + MissingWoodPct.ToString() + "%");
                SetDefectMarker(B);
            }


            // Jack Note: It is very simple, Just calculate the percentage of the Missing Pixels
        }

        //=====================================================================
        private void FindRaisedBoard(Board B)
        {
            // Jack Note: Define width (W1) and height (W2) of the board based on boundaries
            int W1 = B.BoundsP2.X - B.BoundsP1.X;
            int W2 = B.BoundsP2.Y - B.BoundsP1.Y;

            // Jack Note: Capture buffer width
            int CBW = B.CB.Width;

            // Jack Note: Calculate the test value for identifying raised boards using the specified maximum height
            int RBTestVal = (int)(1000 + ParamStorage.GetPixZ("(RB) Raised Board Maximum Height (in)"));

            // Jack Note: Get the percentage of the board that must be raised to consider it a defect
            float RBPercentage = ParamStorage.GetFloat("(RB) Raised Board Width (%)") / 100f;

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
        //private void FindRaisedNails(Board B)
        //{
        //    if (B.Edges[0].Count < 5)
        //    {
        //        return;
        //    }

        //    //if (ParamStorage.GetInt("(RN) Max Nail Height (mm)") < 3) return;

        //    int MNH = (int)ParamStorage.GetPixZ("(RN) Max Nail Height (mm)");

        //    int NSP = ParamStorage.GetInt("(RN) Nail Neighbor Sample Points");
        //    int NSR = ParamStorage.GetInt("(RN) Nail Neighbor Sample Radius");



        //    PalletPoint P1 = FindBoardMinXY(B);
        //    PalletPoint P2 = FindBoardMaxXY(B);


        //    int W = B.CB.Width;
        //    int H = B.CB.Height;

        //    List<PalletPoint> Raw = new List<PalletPoint>();

        //    for (int y = P1.Y; y <= P2.Y; y++)
        //    {
        //        if ((y < NSR+2) || (y >= H - NSR - 8)) continue;

        //        for (int x = P1.X; x <= P2.X; x++)
        //        {
        //            if ((x < NSR+2) || (x >= W - NSR - 8)) continue;

        //            UInt16 Val = B.CB.Buf[y * W + x];

        //            if (Val == 0)
        //                continue;

        //            if (Val < 1000)// + MNH))
        //                continue;

        //            int RaisedCount = 0;
        //            int MaxCount = 0;

        //            for (int a = 0; a < NSP; a++)
        //            {
        //                double ang = ((Math.PI * 2) / NSP) * a;
        //                double px = x + (Math.Sin(ang) * NSR);
        //                double py = y + (Math.Cos(ang) * NSR);
        //                int ix = (int)px;
        //                int iy = (int)py;
        //                if ((ix < 0) || (iy < 0) || (ix >= W) || (iy >= H)) continue;

        //                UInt16 Samp = B.CB.Buf[iy * W + ix];

        //                if (Samp == 0)
        //                    continue;

        //                MaxCount++;

        //                int Delta = ((int)Val - (int)Samp);

        //                if (Delta > MNH)
        //                    RaisedCount++;
        //            }

        //            if (MaxCount == 0)
        //                continue;

        //            // Can't rely on a bunch of zeros
        //            if ((NSP - MaxCount) > 2)
        //                continue;

        //            if (MaxCount == RaisedCount)
        //            {
        //                Raw.Add(new PalletPoint(x, y));

        //                // Nail found, skip ahead
        //                x += NSR;
        //            }
        //        }
        //    }

        //    int RequiredConf = ParamStorage.GetInt("Nail Confirmations Required");

        //    foreach (PalletPoint P in Raw)
        //    {

        //        int nRawNearbyConfirms = 0;
        //        foreach (PalletPoint NP in Raw)
        //        {
        //            if ((Math.Abs(P.X - NP.X) < 4) && (Math.Abs(P.Y - NP.Y) < 4))
        //                nRawNearbyConfirms++;
        //        }

        //        // Count nearby
        //        if (nRawNearbyConfirms >= RequiredConf)
        //        {

        //            // Check if we already have an RN listsed as a defect
        //            bool foundExisting = false;
        //            foreach (PalletPoint ExistingP in B.Nails)
        //            {
        //                if ((Math.Abs(P.X - ExistingP.X) < 10) && (Math.Abs(P.Y - ExistingP.Y) < 20))
        //                {
        //                    foundExisting = true;
        //                    break;
        //                }
        //            }

        //            if (!foundExisting)
        //            {
        //                B.Nails.Add(P);
        //                AddDefect(B, PalletDefect.DefectType.raised_nail, "raised nail");
        //                SetDefectMarker(P.X, P.Y, 30);
        //            }
        //        }

        //    }
        //}

        //=====================================================================

        private bool ConfirmItIsANail(Board B, int CX, int CY)
        {
            Logger.WriteLine(String.Format("ConfirmItIsANail  X:{0}  Y:{1}", CX,CY));

            // Check if it is near the board edge
            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                PalletPoint P1 = B.Edges[0][i];
                int dx = P1.X - CX;
                int dy = P1.Y - CY;
                if (Math.Abs(dx)<40 && Math.Abs(dy)<40)
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
            double MNH = ParamStorage.GetPixZ("(RN) Max Nail Height (mm)");
            int NSP = ParamStorage.GetInt("(RN) Nail Neighbor Sample Points");
            int NSR = ParamStorage.GetInt("(RN) Nail Neighbor Sample Radius");
            int NearTh = NSR / 2;
            int NCSR = ParamStorage.GetInt("(RN) Nail Confirm Surface Radius");
            int NCHIR = ParamStorage.GetInt("(RN) Nail Confirm Head Inner Radius");
            int NCHOR = ParamStorage.GetInt("(RN) Nail Confirm Head Outer Radius");

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
            if (sumC < (NCHIR*2) * (NCHIR*2) * 0.7)
            {
                Logger.WriteLine("ConfirmItIsANail C");
                return false;
            }

            int NailZ = (int)Math.Round(sumZ / sumC,0);
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
                if ((y < 0) || (y >= H) || ((dy>=-NCHOR) && (dy<= NCHOR))) continue;
                for (int dx = -NCSR; dx < +NCSR; dx++)
                {
                    int x = CX + dx;
                    if ((x < 0) || (x >= W) || ((dx >= -NCHOR) && (dx <= NCHOR))) continue;
                    int z = B.CB.Buf[y * W + x];
                    if(z!=0)
                    {
                        sumZ += (double)z;
                        sumC += 1;
                    }
                }
            }

            if (sumC < (NCSR*2) * (NCSR*2) * 0.25)
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

                if (Math.Abs(Samp - NailZ) < (MNH/2))
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

        private void FindRaisedNails(Board B)
        {
            // Jack Note: Check if there are enough edges detected to proceed
            if (B.Edges[0].Count < 5)
            {
                return; // Jack Note: Not enough edges, exit function
            }

            // Jack Note: Retrieve the maximum height a nail can have to be considered raised
            int MNH = (int)ParamStorage.GetPixZ("(RN) Max Nail Height (mm)");

            // Jack Note: Get parameters for nail detection algorithm
            int NSP = ParamStorage.GetInt("(RN) Nail Neighbor Sample Points");
            int NSR = ParamStorage.GetInt("(RN) Nail Neighbor Sample Radius");

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

                        if (Delta > MNH)
                            RaisedCount++;
                    }

                    // Jack Note: Skip point if not enough valid samples or if not all samples indicate a raised nail
                    if (MaxCount == 0 || (NSP - MaxCount) > 2 || MaxCount != RaisedCount)
                        continue;

                    // Jack Note: Add the point to potential nails list and skip ahead by the radius
                    Raw.Add(new PalletPoint(x, y));
                    x += NSR;
                }
            }

            // Jack Note: Required confirmations for a nail to be considered valid
            int RequiredConf = ParamStorage.GetInt("Nail Confirmations Required");

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

                    if (!foundExisting && ConfirmItIsANail(B, P.X, P.Y))
                    {
                        B.Nails.Add(P);
                        AddDefect(B, PalletDefect.DefectType.raised_nail, "raised nail");
                        SetDefectMarker(P.X, P.Y, 30);
                    }
                }
            }

        }

        //=====================================================================
        PalletDefect AddDefect(Board B, PalletDefect.DefectType type, string Comment)
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

        void SetDefectMarker(Board B, string Tag="")
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

        //=====================================================================
        Board ProbeVertically(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location,  int StartCol, int EndCol, int Step, int StartRow, int EndRow)
        {
            UInt16[] Buf = SourceCB.Buf;
            List<PalletPoint> P = new List<PalletPoint>();
            int CBW = SourceCB.Width;

            int ExpLen = ParamStorage.GetInt("Short Board Length");
            int ExpWid = ParamStorage.GetInt("Board Width Y");   // Jack Note: Note used

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
            for (int i = 1; i < B.Edges[0].Count-2; i++)
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
                        if((B.Edges[0][x].Y< avgY0-20) || (B.Edges[1][x].Y > avgY1 + 20))
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
                    for (int x = B.Edges[0].Count-20; x < B.Edges[0].Count; x++)
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


            //Jack Note: Copy all the edge point into a new buffer for futher process
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
        //int FindVerticalSpan(CaptureBuffer CB, int Col, int StartRow, int EndRow)
        //{
        //    UInt16[] Buf = CB.Buf;
        //    int CBW = CB.Width;
        //    int Dir = Math.Sign(EndRow - StartRow);

        //    //int HitCount = 0;
        //    //int HitRow = -1;

        //    int MinPPix = ParamStorage.GetInt("VerticalHitCountMinPositivePixels");
        //    int MinZPix = ParamStorage.GetInt("VerticalHitCountMinZeroPixels");
        //    int ExpWid = ParamStorage.GetInt("Board Width Y");

        //    int nRows = Math.Abs(EndRow - StartRow) + 1;

        //    int[] ColData = new int[nRows];
        //    int[] PixCnt = new int[nRows];


        //    int Idx = 0;
        //    int FirstIdx = -1;
        //    int LastIdx = -1;
            
        //    for (int y = StartRow; y != EndRow; y += Dir)
        //    {
        //        UInt16 V = Buf[y * CBW + Col];

        //        if ( V > 0 )
        //        {
        //            if (FirstIdx == -1)
        //                FirstIdx = Idx;
        //            LastIdx = Idx;
        //            ColData[Idx] = 1;
        //        }
        //        Idx++;
        //    }

        //    if (LastIdx == -1)
        //        return -1;

        //    if ((LastIdx - FirstIdx) < ExpWid)
        //        return StartRow + FirstIdx;

        //    int CenterIdx = (FirstIdx + LastIdx) / 2;
        //    int StartIdx = CenterIdx - (ExpWid / 2);

        //    // find max pixels
        //    for ( int y = StartIdx; y > FirstIdx; y-- )
        //    {
        //        for ( int yy = y; yy < ExpWid; yy++ )
        //        {
        //            try
        //            {
        //                PixCnt[y] += ColData[yy];
        //            }
        //            catch
        //            {

        //            }
        //        }
        //    }

        //    int Max = 0;
        //    int MaxRow = -1;

        //    for (int i = 0; i < PixCnt.Length; i++)
        //    {
        //        if (PixCnt[i] > Max)
        //        {
        //            Max = PixCnt[i];
        //            MaxRow = (i * Dir) + StartRow;
        //        }
        //    }

        //    //if (Col == 940)
        //    //{
        //    //    System.Console.WriteLine(string.Format("S:{0}   E:{1}   Max:{2}", StartRow, EndRow, MaxRow));
        //    //}

        //    return MaxRow;
        //}





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
        Board ProbeHorizontally(CaptureBuffer SourceCB, string Name, PalletDefect.DefectLocation Location, int StartRow, int EndRow, int Step, int StartCol, int EndCol)
        {
            UInt16[] Buf = SourceCB.Buf;
            int CBW = SourceCB.Width;
            List<PalletPoint> P = new List<PalletPoint>();

            int ExpLen = ParamStorage.GetInt("Long Board Length");
            int ExpWid = ParamStorage.GetInt("Board Width X Left");

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
                    B.Edges[0].Insert(i, new PalletPoint(B.Edges[0][i - 1].X, B.Edges[0][i - 1].Y+1));
                    B.Edges[1].Insert(i, new PalletPoint(B.Edges[1][i - 1].X, B.Edges[1][i - 1].Y+1));
                    //i--;
                }
                else
                    LastWasGood = true;
            }



            for (int i = 0; i < B.Edges[0].Count; i++)
            {
                // Sanity checks
                //if (Math.Abs(B.Edges[0][i].X - B.Edges[1][i].X) > FailWid)
                //{
                //    B.Edges[0].RemoveAt(i);
                //    B.Edges[1].RemoveAt(i);
                //    i--;
                //    continue;
                //}

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


            //for (int i = 0; i < B.Edges[0].Count; i++)
            //{
            //    MakeBlock(Buf, B.Edges[0][i].X, B.Edges[0][i].Y, 2, 5000);
            //    MakeBlock(Buf, B.Edges[1][i].X, B.Edges[1][i].Y, 2, 5000);
            //}

            return B;
        }



        //=====================================================================
        //int FindHorizontalSpan(CaptureBuffer SourceCB, int Row, int StartCol, int EndCol)
        //{
        //    UInt16[] Buf = SourceCB.Buf;
        //    int CBW = SourceCB.Width;
        //    int Dir = Math.Sign(EndCol - StartCol);

        //    //int HitCount = 0;
        //    //int HitCol = -1;

        //    int MinPPix = ParamStorage.GetInt("VerticalHitCountMinPositivePixels");
        //    int MinZPix = ParamStorage.GetInt("VerticalHitCountMinZeroPixels");
        //    int ExpWid = 0;
            
        //    if ( StartCol < (CBW / 2))
        //        ExpWid = ParamStorage.GetInt("Board Width X Left");
        //    else
        //        ExpWid = ParamStorage.GetInt("Board Width X Right");

        //    ExpWid /= 2;

        //    int nCols = Math.Abs(EndCol - StartCol) + 1;

        //    int[] RowData = new int[nCols];
        //    int[] PixCnt = new int[nCols];


        //    int Idx = 0;
        //    for (int x = StartCol; x != EndCol; x += Dir)
        //    {
        //        UInt16 V = Buf[Row * CBW + x];

        //        RowData[Idx++] = V != 0 ? 1 : 0;
        //    }

        //    // find max pixels
        //    int EX = RowData.Length - ExpWid ;
        //    bool SomePixFound = false;
        //    for (int x = 0; x < RowData.Length; x++)
        //    {
        //        if (SomePixFound && (x > EX))
        //            break;

        //        if (RowData[x] != 0)
        //        {
        //            SomePixFound = true;

        //            for (int xx = x; xx < (x+ExpWid); xx++)
        //            {
        //                //try
        //                //{
        //                    PixCnt[x] += RowData[xx];
        //                //}
        //                //catch
        //                //{
        //                //    int adsfsdf = 0;
        //                //}
        //            }
        //        }
        //    }

        //    int Max = 0;
        //    int MaxCol = -1;

        //    for (int i = 0; i < PixCnt.Length; i++)
        //    {
        //        if (PixCnt[i] > Max)
        //        {
        //            Max = PixCnt[i];
        //            MaxCol = (i * Dir) + StartCol;
        //        }
        //    }

        //    return MaxCol;
        //}


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

        //=====================================================================
        public static float DistancePointToLine(PalletPoint p, PalletPoint a, PalletPoint b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return Math.Abs((b.X - a.X) * (a.Y - p.Y) - (a.X - p.X) * (b.Y - a.Y)) / distance;
        }

    }
}
