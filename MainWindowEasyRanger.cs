using Sick.EasyRanger;
using Sick.EasyRanger.Base;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static PalletCheck.Pallet;
using GDII = System.Drawing.Imaging;
using System.Drawing;
using Path = System.IO.Path;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Sick.StreamUI.FrameSave.Ply;
using System.Xml.Linq;
using Sick.EasyRanger.Controls.Converter;

namespace PalletCheck
{
    public partial class MainWindow : Window
    {
        public ProcessingEnvironment _envLeft { get; set; }
        public ProcessingEnvironment _envRight { get; set; }
        public static  ProcessingEnvironment _envTop { get; set; }
        public static ProcessingEnvironment _envBottom { get; set; }
        public ProcessingEnvironment _envFront {  get; set; }
        public ProcessingEnvironment _envBack { get; set; }

        System.Windows.Media.Color red = System.Windows.Media.Color.FromRgb(255, 0, 0);
        System.Windows.Media.Color green = System.Windows.Media.Color.FromRgb(0, 255, 0);
        System.Windows.Media.Color yellow = System.Windows.Media.Color.FromRgb(255, 255, 0);
        System.Windows.Media.Color blue = System.Windows.Media.Color.FromRgb(0, 0, 255);
        System.Windows.Media.Color white = System.Windows.Media.Color.FromRgb(255, 255, 255);      

        private void ProcessMeasurement(Action<bool> callback, PositionOfPallet position)
        {
            DateTime AnalysisStartTime;
            DateTime AnalysisStopTime;
            double AnalysisTotalSec;
            // Select environment and view based on position
            var env = (position == PositionOfPallet.Left) ? _envLeft : _envRight;
            var viewer = (position == PositionOfPallet.Left) ? ViewerLeft : ViewerRight;
            var paramStorage = position == PositionOfPallet.Left ? ParamStorageLeft : ParamStorageRight;
            Pallet P = new Pallet(null, null, position);
            P.BList = new List<Board>();

            if (position == PositionOfPallet.Left)
            {
                //env.GetStepProgram("Main").StepList[1].Enabled = true;
                //env.GetStepProgram("Main").StepList[2].Enabled = false;
                //env.GetStepProgram("Main").StepList[3].Enabled = false;
                P.BList.Add(new Board("Block1", PalletDefect.DefectLocation.L_B1, true, 0, 0));
                P.BList.Add(new Board("Block2", PalletDefect.DefectLocation.L_B2, true, 0, 0));
                P.BList.Add(new Board("Block3", PalletDefect.DefectLocation.L_B3, true, 0, 0));
            }

            else
            {
                //env.GetStepProgram("Main").StepList[1].Enabled = false;
                //env.GetStepProgram("Main").StepList[2].Enabled = true;
                //env.GetStepProgram("Main").StepList[3].Enabled = true;
                P.BList.Add(new Board("Block1", PalletDefect.DefectLocation.R_B1, true, 0, 0));
                P.BList.Add(new Board("Block2", PalletDefect.DefectLocation.R_B2, true, 0, 0));
                P.BList.Add(new Board("Block3", PalletDefect.DefectLocation.R_B3, true, 0, 0));
            }

            double[] BlockHeight = new double[3];
            double[] BlockWidth = new double[3];
            BlockHeight[0] = paramStorage.GetFloat(StringsLocalization.BlockHeight1);
            BlockHeight[1] = paramStorage.GetFloat(StringsLocalization.BlockHeight2);
            BlockHeight[2] = paramStorage.GetFloat(StringsLocalization.BlockHeight3);
            BlockWidth[0] = paramStorage.GetFloat(StringsLocalization.BlockWidth1);
            BlockWidth[1] = paramStorage.GetFloat(StringsLocalization.BlockWidth2);
            BlockWidth[2] = paramStorage.GetFloat(StringsLocalization.BlockWidth3);
            double SideLength = paramStorage.GetFloat(StringsLocalization.SideLength);
            double AngleThresholdDegree = paramStorage.GetFloat(StringsLocalization.AngleThresholdDegree);
            double BlockProtrudingfromPalletMaximum = paramStorage.GetFloat(StringsLocalization.BlockProtrudingfromPalletMaximum);
            double MissingBlockAllowedPercentage = paramStorage.GetFloat(StringsLocalization.BlockMissingMaximum);
            double MiddleBoardMissingWoodMaxPercentage = paramStorage.GetFloat(StringsLocalization.LinearSupportBoardMissingWoodPercentageMaximum);
            double NailHeight = paramStorage.GetFloat(StringsLocalization.NailProtrudingfromSideofPalletMaximum);
            int ForkClearancePercentage = paramStorage.GetInt(StringsLocalization.ForkClearancePercentageMinimum);
            int RefTop = paramStorage.GetInt(StringsLocalization.Top);
            int RefBottom = paramStorage.GetInt(StringsLocalization.Bottom);
            int StartCropX = paramStorage.GetInt(StringsLocalization.StartCropX);
            int EndCropX = paramStorage.GetInt(StringsLocalization.EndCropX);
            int StartCropY = paramStorage.GetInt(StringsLocalization.StartCropY);
            int EndCropY = paramStorage.GetInt(StringsLocalization.EndCropY);
            
            try
            {
                env.SetInteger("InputRefTop", RefTop);
                env.SetInteger("InputRefBottom", RefBottom);
                env.SetInteger("InputStartCropX", StartCropX);
                env.SetInteger("InputEndCropX", EndCropX);
                env.SetInteger("InputStartCropY", StartCropY);
                env.SetInteger("InputEndCropY", EndCropY);
                env.SetDouble("NailHeight", NailHeight);
                //Get Exec time
                AnalysisStartTime = DateTime.Now;
                env.GetStepProgram("Main").RunFromBeginning();
                AnalysisStopTime = DateTime.Now;
                AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
                Logger.WriteLine(String.Format("EasyRanger environment FINISHED for {0} -  {1:0.000} sec", position.ToString(), AnalysisTotalSec));

                double[] missingBlocks = env.GetDouble("OutputMissBlock");
                double[] missingChunks = env.GetDouble("OutputMissChunk");
                double[] RotateResult = env.GetDouble("OutputAngle");
                double[] blockHeight = env.GetDouble("OutputHeights");
                double[] sortedHeights = env.GetDouble("OutputSortedHeights");
                int[] sortedHeightsLen = env.GetInteger("OutputSortedHeightsLen");
                double XResolutin = env.GetDouble("M_XResolution", 0);
                double YResolutin = env.GetDouble("M_YResolution", 0);
                int[] LeftClearancePct = env.GetInteger("ClearanceLeftPctInt");
                int[] RightClearancePct = env.GetInteger("ClearanceRightPctInt");
                int NailLength = env.GetInteger("NailLength", 0);
                int[] HasNails = env.GetInteger("OutputNails");
                int[] middleBoardHasBlobs = env.GetInteger("MiddleBoardHasBlobs");
                bool isFail = false;
                System.Windows.Point[] PointForDisplay = env.GetPoint2D("OutputCenters");

                //Take only peak values for block protruding
                blockHeight[0] = sortedHeights[0];
                blockHeight[1] = sortedHeights[sortedHeightsLen[0]];
                blockHeight[2] = sortedHeights[sortedHeightsLen[0] + sortedHeightsLen[1]];

                //To extract Dataset 
                float[] floatArray = env.GetImageBuffer("FilteredImage")._range;
                byte[] byteArray = env.GetImageBuffer("FilteredImage")._intensity;

                ushort[] ushortArray = ConvertFloatArrayToUshortArray(floatArray);
                ushort[] ushortArrayRef = ConvertByteArrayToUshortArray(byteArray);
                int[] centroids = new int[]{ };

                CaptureBuffer rangeBuffer = CreateCaptureBuffer(ushortArray, env); // Create and store CaptureBuffer
                CaptureBuffer ReflBuf = CreateReferenceBuffer(ushortArrayRef, env);// Create a reference buffer              

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewer.ClearAll();
                    viewer.DrawImage("FilteredImage", SubComponent.Intensity);
                    viewer.DrawRoi("OutputRegionsForNails", -1, red, 250);
                    Logger.WriteLine(String.Format("Image updated for {0}", position.ToString() ));

                    if (enableDatasetExtraction) { ExtractDataset(floatArray, byteArray, env, position); }

                    if (isDeepLActive) { centroids = GetCentroidsDL(floatArray, byteArray, rangeBuffer, ReflBuf, position, NailHeight);}

                    if (LeftClearancePct[0] < ForkClearancePercentage)
                    {
                        viewer.DrawRoi("ClearanceROILeft", 0, red, 100);
                        P.AddDefect(P.BList[0], PalletDefect.DefectType.clearance, "Fork Clearance is " + LeftClearancePct[0] + "% less than " + ForkClearancePercentage + "%");
                    }

                    else if (RightClearancePct[0] < ForkClearancePercentage)
                    {
                        viewer.DrawRoi("ClearanceROIRight", 0, red, 100);
                        P.AddDefect(P.BList[2], PalletDefect.DefectType.clearance, "Fork Clearance is " + RightClearancePct[0] + "% less than " + ForkClearancePercentage + "%");
                    }

                    else
                    {
                        viewer.DrawRoi("ClearanceROILeft", 0, green, 100);
                        viewer.DrawRoi("ClearanceROIRight", 0, green, 100);
                    }

                    viewer.DrawText("ClearanceLeftText", white);
                    viewer.DrawText("ClearanceRightText", white);

                    if (middleBoardHasBlobs[0] == 1)
                    {
                        int[] middleBoardBlobsPixels = env.GetInteger("MB_Blobs_Pixels");
                        int[] middleBoardROIPixels = env.GetInteger("MB_ROI_Pixels");
                        int middleBoardBlobsPixelsSum = middleBoardBlobsPixels.Sum();
                        int middleBoardROIPixelsSum = middleBoardROIPixels.Sum();
                        float blobPercentage = ((float)middleBoardBlobsPixelsSum / (float)middleBoardROIPixelsSum) * 100;

                        if (blobPercentage < MiddleBoardMissingWoodMaxPercentage)
                        {
                            viewer.DrawRoi("MB_ROI", -1, green, 100);
                        }

                        else
                        {
                            P.AddDefect(P.BList[0], PalletDefect.DefectType.middle_board_missing_wood, "Linear Suppport Missing Wood");
                            viewer.DrawRoi("MB_ROI", -1, red, 100);
                        }
                    }

                    if (centroids.Length == 2)
                    {
                        int Cx1 = centroids[0];
                        int Cy1 = centroids[1];

                        float maxHeight = GetMaxRangeInCircle(centroids[0], centroids[1], 40, rangeBuffer, floatArray);
                        Logger.WriteLine("MaxHeight Obj1 " + MaxHeight);
                        float Object1 = GetMaxRangeInSquare(Cx1, Cy1, 10, rangeBuffer, floatArray);
                        int pos1 = GetHorizontalPosition(Cx1, rangeBuffer);

                        if (Object1 > NailHeight)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (pos1 == i)
                                {
                                    P.AddDefect(P.BList[i], PalletDefect.DefectType.side_nails_protruding, "Side Nail: " + (int)Object1 + "mm protruding out sides of pallet > " + NailHeight + "mm");
                                    viewer.DrawCircleFeedback(centroids[0], centroids[1], 40, 40, red);
                                    isFail = true;
                                }
                            }
                        }
                    }

                    // Variables for Unsecured Horizontal Block
                    System.Windows.Point[] BlobCentroidsB1Point = env.GetPoint2D("CentroidsB1");
                    System.Windows.Point[] BlobCentroidsB2Point = env.GetPoint2D("CentroidsB2");
                    System.Windows.Point[] BlobCentroidsB3Point = env.GetPoint2D("CentroidsB3");

                    // Variables needed for missing volume  feature
                    int[] ExpectedDepth = new int[3]; //mm2
                    float[] ExpectedSurface = new float[3];
                    float[] ExpectedVolume = new float[3];

                    ExpectedSurface[0] = paramStorage.GetInt(StringsLocalization.BlockWidth1) * paramStorage.GetInt(StringsLocalization.BlockHeight1);
                    ExpectedSurface[1] = paramStorage.GetInt(StringsLocalization.BlockWidth2) * paramStorage.GetInt(StringsLocalization.BlockHeight2);
                    ExpectedSurface[2] = paramStorage.GetInt(StringsLocalization.BlockWidth3) * paramStorage.GetInt(StringsLocalization.BlockHeight3);
                    ExpectedDepth[0]   = paramStorage.GetInt(StringsLocalization.BlockDepth1);
                    ExpectedDepth[1]   = paramStorage.GetInt(StringsLocalization.BlockDepth2);
                    ExpectedDepth[2]   = paramStorage.GetInt(StringsLocalization.BlockDepth3);
                    ExpectedVolume[0]  = ExpectedSurface[0]* paramStorage.GetFloat(StringsLocalization.BlockDepth1);
                    ExpectedVolume[1]  = ExpectedSurface[1] * paramStorage.GetFloat(StringsLocalization.BlockDepth2);
                    ExpectedVolume[2]  = ExpectedSurface[2] * paramStorage.GetFloat(StringsLocalization.BlockDepth3);

                    double XResolution = env.GetDouble("M_XResolution", 0);
                    double YResolution = env.GetDouble("M_YResolution", 0);
                    float customXResolution = paramStorage.GetPixX(StringsLocalization.MMPerPixelX);
                    float customYResolution = paramStorage.GetPixY(StringsLocalization.MMPerPixelY);
                    float Xres = 0;
                    float Yres = 0;
                    if (customXResolution == 0) { Xres = (float)env.GetDouble("M_XResolution", 0); }
                         else { Xres = customXResolution; }
                    if (customYResolution == 0) { Yres = (float)env.GetDouble("M_YResolution", 0); }
                         else { Yres = customYResolution; }                 

                    for (int i = 0; i < 3; i++)
                    {
                        string[] textArray = new string[] {Math.Abs (RotateResult[i]).ToString() + "°" };
                        float[] xArray = new float[] { (float)PointForDisplay[i].X };
                        float[] yArray = new float[] { (float)PointForDisplay[i].Y-200 };
                        float[] sizeArray = new float[] { 150f };

                        if (missingBlocks[i] == 1)
                        {
                            /*  Missing volume feature:
                             *  Description: this segment of code evaluates the volume of each existent block and compares 
                             *  it against (expected volume * percentage parameter), if missing volume is greater than the 
                             *  expected volume, it will mark the defect */
                            double ExpectedSurfacemm2 = ExpectedSurface[i];
                            double ExpectedVolumemm3 = ExpectedVolume[i];
                            int Depthmm = ExpectedDepth[i];
                            double MissingROIVolumemm3 = env.GetDouble("Volume", i);
                            ulong ROISurfacepp2;
                            env.GetRoiArea("OutputRegions", i, out ROISurfacepp2);
                            double ROISurfacemm2= ROISurfacepp2*Xres*Yres;
                            double ComplementarySurface= ExpectedSurfacemm2 - ROISurfacemm2;
                            double TotalMissingVolume =((MissingROIVolumemm3 + (ComplementarySurface * Depthmm*-1)))*-1;
                            double minimumExpectedVolume=((100- MissingBlockAllowedPercentage)/100)* ExpectedVolumemm3;

                            if ((TotalMissingVolume) > minimumExpectedVolume)
                            {
                                P.AddDefect(P.BList[i], PalletDefect.DefectType.missing_blocks, "Missing block volume: " + Math.Round((TotalMissingVolume), 2) + "mm³ > " + Math.Round(minimumExpectedVolume, 2) + "mm³(minimum expected volume)");
                                viewer.DrawRoi("OutputRegions", i, red, 128);
                                isFail = true;
                            }

                            else 
                            {
                                viewer.DrawRoi("OutputRegions", i, green, 50);

                                if (i == 0)
                                {                                  
                                    if (BlobCentroidsB1Point.Length > 1)
                                    {
                                        if (Math.Abs(BlobCentroidsB1Point[0].Y - BlobCentroidsB1Point[1].Y) > 40)
                                        {
                                            P.AddDefect(P.BList[i], PalletDefect.DefectType.unsecured_horizontal_block, "Unsecured Horizontal Block");
                                            viewer.DrawRoi("OutputRegions", i, red, 128);
                                            isFail = true;
                                        }
                                    }
                                }

                                if (i == 1)
                                {
                                    if (BlobCentroidsB2Point.Length > 1)
                                    {
                                        if (Math.Abs(BlobCentroidsB2Point[0].Y - BlobCentroidsB2Point[1].Y) > 40)
                                        {
                                            P.AddDefect(P.BList[i], PalletDefect.DefectType.unsecured_horizontal_block, "Unsecured Horizontal Block");
                                            viewer.DrawRoi("OutputRegions", i, red, 128);
                                            isFail = true;
                                        }
                                    }
                                }

                                if (i == 2)
                                {
                                    if (BlobCentroidsB3Point.Length > 1)
                                    {
                                        if (Math.Abs(BlobCentroidsB3Point[0].Y - BlobCentroidsB3Point[1].Y) > 40)
                                        {
                                            P.AddDefect(P.BList[i], PalletDefect.DefectType.unsecured_horizontal_block, "Unsecured Horizontal Block");
                                            viewer.DrawRoi("OutputRegions", i, red, 128);
                                            isFail = true;
                                        }
                                    }
                                }
                            }
                        }

                        else
                        {
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.missing_blocks, "Missing Blocks");
                            viewer.DrawRoi("OutputRegions", i, red, 128);
                            isFail = true;     
                        }

                        if (blockHeight[i] > BlockProtrudingfromPalletMaximum)
                        {                        
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.blocks_protuded_from_pallet, "Block protrude from pallet " + Math.Round(Math.Abs(blockHeight[i]), 2) + "mm > " + BlockProtrudingfromPalletMaximum + "mm");
                            viewer.DrawRoi("OutputRegions", i, red, 100);
                            isFail = true;
                        }
                     
                        env.SetText("MyText", textArray , xArray, yArray, sizeArray);

                        //Previous evaluation of Missing chunks on blocks (not required) and block protruding (average measure)
                        /*if (missingChunks[i] < (BlockWidth[i] / XResolutin) * (BlockHeight[i] / YResolutin) * ((100 - MissingBlockAllowedPercentage) / 100) || blockHeight[i] > BlockProtrudingfromPalletMaximum)
                        {
                            /*if (blockHeight[i] < -MissingChunkThreshold)
                            { P.AddDefect(P.BList[i], PalletDefect.DefectType.missing_chunks, "Missing Chunks: Average Height " + Math.Round((blockHeight[i]), 2) + "mm < -" + MissingChunkThreshold + "mm"); }
                            if (blockHeight[i] > MissingChunkThreshold)
                        }*/

                        // Excessive Angle Inspection commented out cause it's not a requirement
                        /*if (Math.Abs(RotateResult[i]) > AngleThresholdDegree)
                        {
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.excessive_angle, "Twisted Blocks Angle: " + Math.Abs(RotateResult[i]).ToString() +  "° > " + AngleThresholdDegree.ToString() +"°");
                            viewer.DrawText("MyText" , red);
                            isFail = true;
                        }

                        else
                        {
                            viewer.DrawText("MyText", green);
                        }*/

                        lock (LockObjectCombine)
                        {
                            CombinedBoards.Add(P.BList[i]);
                            CombinedDefects.AddRange(P.BList[i].AllDefects);
                        }                     
                    }                  

                    P.BList.Clear();              
                });

                ProcessCameraResult((int)position, !isFail ? InspectionResult.PASS : InspectionResult.FAIL);
                callback?.Invoke(!isFail);
            }

            catch (Exception ex)
            {
                Logger.WriteException(ex);
                UpdateTextBlock(LogText,ex.Message, MessageState.Normal);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewer.ClearAll();
                    viewer.DrawImage("ImageFinal", SubComponent.Intensity);
                    Logger.WriteLine(String.Format("Image updated with an exception for {0}", position.ToString()));
                    System.Windows.MessageBox.Show(ex.Message);
                });
                ProcessCameraResult((int)position, false ? InspectionResult.PASS : InspectionResult.FAIL);
                callback?.Invoke(false);
            }        
        }
        private void ProcessMeasurementFrontBack(Action<bool> callback, PositionOfPallet position)
        {
            DateTime AnalysisStartTime;
            DateTime AnalysisStopTime;
            double AnalysisTotalSec;
            // Select environment and view based on position
            var env = (position == PositionOfPallet.Front) ? _envFront : _envBack;
            var viewer = (position == PositionOfPallet.Front) ? ViewerFront : ViewerBack;
            var paramStorage = position == PositionOfPallet.Front ? ParamStorageFront : ParamStorageBack;
            Pallet P = new Pallet(null, null, position);
            P.BList = new List<Board>();
            

            if (position == PositionOfPallet.Back)
            {
                //env.GetStepProgram("Main").StepList[1].Enabled = true;
                //env.GetStepProgram("Main").StepList[2].Enabled = false;
                //env.GetStepProgram("Main").StepList[3].Enabled = false;
                P.BList.Add(new Board("Block1", PalletDefect.DefectLocation.BK_B1, true, 0, 0));
                P.BList.Add(new Board("Block2", PalletDefect.DefectLocation.BK_B2, true, 0, 0));
                P.BList.Add(new Board("Block3", PalletDefect.DefectLocation.BK_B3, true, 0, 0));
            }

            else
            {
                //env.GetStepProgram("Main").StepList[1].Enabled = false;
                //env.GetStepProgram("Main").StepList[2].Enabled = true;
                //env.GetStepProgram("Main").StepList[3].Enabled = true;
                P.BList.Add(new Board("Block1", PalletDefect.DefectLocation.F_B1, true, 0, 0));
                P.BList.Add(new Board("Block2", PalletDefect.DefectLocation.F_B2, true, 0, 0));
                P.BList.Add(new Board("Block3", PalletDefect.DefectLocation.F_B3, true, 0, 0));
            }

            double[] BlockHeight = new double[3];
            double[] BlockWidth = new double[3];
            BlockHeight[0] = paramStorage.GetFloat(StringsLocalization.BlockHeight1);
            BlockHeight[1] = paramStorage.GetFloat(StringsLocalization.BlockHeight2);
            BlockHeight[2] = paramStorage.GetFloat(StringsLocalization.BlockHeight3);
            BlockWidth[0] = paramStorage.GetFloat(StringsLocalization.BlockWidth1);
            BlockWidth[1] = paramStorage.GetFloat(StringsLocalization.BlockWidth2);
            BlockWidth[2] = paramStorage.GetFloat(StringsLocalization.BlockWidth3);
            double SideLength = paramStorage.GetFloat(StringsLocalization.SideLength);
            double AngleThresholdDegree = paramStorage.GetFloat(StringsLocalization.AngleThresholdDegree);
            double BlockProtrudingfromPalletMaximum = paramStorage.GetFloat(StringsLocalization.BlockProtrudingfromPalletMaximum);
            double MissingBlockAllowedPercentage = paramStorage.GetFloat(StringsLocalization.BlockMissingMaximum);
            double MiddleBoardMissingWoodMaxPercentage = paramStorage.GetFloat(StringsLocalization.LinearSupportBoardMissingWoodPercentageMaximum);
            double NailHeight = paramStorage.GetFloat(StringsLocalization.NailProtrudingfromSideofPalletMaximum);
            int ForkClearancePercentage = paramStorage.GetInt(StringsLocalization.ForkClearancePercentageMinimum);
            int RefTop = paramStorage.GetInt(StringsLocalization.Top);
            int RefBottom = paramStorage.GetInt(StringsLocalization.Bottom);
            int StartCropX = paramStorage.GetInt(StringsLocalization.StartCropX);
            int EndCropX = paramStorage.GetInt(StringsLocalization.EndCropX);
            int StartCropY = paramStorage.GetInt(StringsLocalization.StartCropY);
            int EndCropY = paramStorage.GetInt(StringsLocalization.EndCropY);

            try
            {
                env.SetInteger("InputRefTop", RefTop);
                env.SetInteger("InputRefBottom", RefBottom);
                env.SetInteger("InputStartCropX", StartCropX);
                env.SetInteger("InputEndCropX", EndCropX);
                env.SetInteger("InputStartCropY", StartCropY);
                env.SetInteger("InputEndCropY", EndCropY);
                env.SetDouble("NailHeight", NailHeight);

                //Get Exec time
                AnalysisStartTime = DateTime.Now;
                env.GetStepProgram("Main").RunFromBeginning();
                AnalysisStopTime = DateTime.Now;
                AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
                Logger.WriteLine(String.Format("EasyRanger environment FINISHED for {0} -  {1:0.000} sec", position.ToString(), AnalysisTotalSec));

                double[] missingBlocks = env.GetDouble("OutputMissBlock");
                double[] missingChunks = env.GetDouble("OutputMissChunk");
                double[] RotateResult = env.GetDouble("OutputAngle");
                double[] blockHeight = env.GetDouble("OutputHeights");
                double[] sortedHeights = env.GetDouble("OutputSortedHeights");
                int[] sortedHeightsLen = env.GetInteger("OutputSortedHeightsLen");
                double XResolutin = env.GetDouble("M_XResolution", 0);
                double YResolutin = env.GetDouble("M_YResolution", 0);
                int[] LeftClearancePct = env.GetInteger("ClearanceLeftPctInt");
                int[] RightClearancePct = env.GetInteger("ClearanceRightPctInt");
                int NailLength = env.GetInteger("NailLength", 0);
                int[] HasNails = env.GetInteger("OutputNails");
                bool isFail = false;
                System.Windows.Point[] PointForDisplay = env.GetPoint2D("OutputCenters");

                //Take only peak values for block protruding
                blockHeight[0] = sortedHeights[0];
                blockHeight[1] = sortedHeights[sortedHeightsLen[0]];
                blockHeight[2] = sortedHeights[sortedHeightsLen[0] + sortedHeightsLen[1]];

                //To extract Dataset 
                float[] floatArray = env.GetImageBuffer("FilteredImage")._range;
                byte[] byteArray = env.GetImageBuffer("FilteredImage")._intensity;

                ushort[] ushortArray = ConvertFloatArrayToUshortArray(floatArray);
                ushort[] ushortArrayRef = ConvertByteArrayToUshortArray(byteArray);
                int[] centroids = new int[] { };

                CaptureBuffer rangeBuffer = CreateCaptureBuffer(ushortArray, env); // Create and store CaptureBuffer
                CaptureBuffer ReflBuf = CreateReferenceBuffer(ushortArrayRef, env);// Create a reference buffer 

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewer.ClearAll();
                    viewer.DrawImage("FilteredImage", SubComponent.Intensity);
                    viewer.DrawRoi("OutputRegionsForNails", -1, red, 250);
                    Logger.WriteLine(String.Format("Image updated for {0}", position.ToString()));

                    if (enableDatasetExtraction) { ExtractDataset(floatArray, byteArray, env, position); }

                    if (isDeepLActive) { centroids = GetCentroidsDL(floatArray, byteArray, rangeBuffer, ReflBuf, position, NailHeight);}

                    if (LeftClearancePct[0] < ForkClearancePercentage)
                    {
                        viewer.DrawRoi("ClearanceROILeft", 0, red, 100);
                        P.AddDefect(P.BList[0], PalletDefect.DefectType.clearance, "Fork Clearance is " + LeftClearancePct[0] + "% less than " + ForkClearancePercentage + "%");
                    }

                    else if (RightClearancePct[0] < ForkClearancePercentage)
                    {
                        viewer.DrawRoi("ClearanceROIRight", 0, red, 100);
                        P.AddDefect(P.BList[2], PalletDefect.DefectType.clearance, "Fork Clearance is " + RightClearancePct[0] + "% less than " + ForkClearancePercentage + "%");
                    }

                    else
                    {
                        viewer.DrawRoi("ClearanceROILeft", 0, green, 100);
                        viewer.DrawRoi("ClearanceROIRight", 0, green, 100);
                    }

                    viewer.DrawText("ClearanceLeftText", white);
                    viewer.DrawText("ClearanceRightText", white);

                    int defectsfound = centroids.Length / 2;

                    if (defectsfound > 0)
                    {
                        int ii = 0;
                        for (int i = 0; i < defectsfound; i++)
                        {

                            int Cx1 = model.ScaleVal(centroids[ii], 2500, ReflBuf.Width);
                            int Cy1 = model.ScaleVal(centroids[ii + 1], 400, ReflBuf.Height);
                            ii = ii + 1;
                            float maxHeight = GetMaxRangeInCircle(Cx1, Cy1, 40, rangeBuffer, floatArray);
                            float Object1 = GetMaxRangeInSquare(Cx1, Cy1, 10, rangeBuffer, floatArray);
                            int pos1 = GetHorizontalPosition(Cx1, rangeBuffer);

                            if (Object1 > NailHeight)
                            {
                                for (int j = 0; j < 3; j++)
                                {
                                    if (pos1 == j)
                                    {
                                        P.AddDefect(P.BList[j], PalletDefect.DefectType.side_nails_protruding, "Side Nail: " + (int)Object1 + "mm protruding out sides of pallet > " + NailHeight + "mm");
                                        viewer.DrawCircleFeedback(Cx1, Cy1, 40, 40, red);
                                        isFail = true;
                                    }
                                }
                            }
                        }
                    }
                  
                    for (int i = 0; i < 3; i++)
                    {
                        string[] textArray = new string[] { Math.Abs(RotateResult[i]).ToString() + "°" };
                        float[] xArray = new float[] { (float)PointForDisplay[i].X };
                        float[] yArray = new float[] { (float)PointForDisplay[i].Y - 200 };
                        float[] sizeArray = new float[] { 150f };

                        if (missingBlocks[i] == 1)
                        {
                            viewer.DrawRoi("OutputRegions", i, green, 50);
                        }

                        else
                        {
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.missing_blocks, "Missing Blocks");
                            viewer.DrawRoi("OutputRegions", i, red, 128);
                            isFail = true;
                        }

                        if (blockHeight[i] > BlockProtrudingfromPalletMaximum)
                        {
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.blocks_protuded_from_pallet, "Block protrude from pallet " + Math.Round(Math.Abs(blockHeight[i]), 2) + "mm > " + BlockProtrudingfromPalletMaximum + "mm");
                            viewer.DrawRoi("OutputRegions", i, red, 100);
                            isFail = true;
                        }

                        env.SetText("MyText", textArray, xArray, yArray, sizeArray);

                        //lock (LockObjectCombine)
                        {
                            CombinedBoards.Add(P.BList[i]);
                            CombinedDefects.AddRange(P.BList[i].AllDefects);
                        }
                    }
                 
                    P.BList.Clear();
                });

                ProcessCameraResult((int)position, !isFail ? InspectionResult.PASS : InspectionResult.FAIL);
                callback?.Invoke(!isFail);
            }

            catch (Exception ex)
            {
                Logger.WriteException(ex);
                UpdateTextBlock(LogText, ex.Message, MessageState.Normal);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewer.ClearAll();
                    viewer.DrawImage("ImageFinal", SubComponent.Intensity);
                    Logger.WriteLine(String.Format("Image updated with an exception for {0}", position.ToString()));
                    System.Windows.MessageBox.Show(ex.Message);
                });
                ProcessCameraResult((int)position, false ? InspectionResult.PASS : InspectionResult.FAIL);
                callback?.Invoke(false);
            }
        }

        private ushort[] ConvertFloatArrayToUshortArray(float[] floatArray)
        {
            ushort[] ushortArray = new ushort[floatArray.Length];

            for (int i = 0; i < floatArray.Length; i++)
            {
                ushortArray[i] = (ushort)(floatArray[i] + 1000);
            }

            return ushortArray;
        }

        private ushort[] ConvertByteArrayToUshortArray(byte[] byteArray)
        {
            ushort[] ushortArrayRef = new ushort[byteArray.Length];

            for (int i = 0; i < ushortArrayRef.Length; i++)
            {
                ushortArrayRef[i] = byteArray[i];
            }

            return ushortArrayRef;
        }

        private CaptureBuffer CreateCaptureBuffer(ushort[] ushortArray, ProcessingEnvironment env)
        {
            CaptureBuffer rangeBuffer = new CaptureBuffer
            {
                Buf = ushortArray,
                Width = env.GetImageBuffer("FilteredImage").Info.Width,
                Height = env.GetImageBuffer("FilteredImage").Info.Height,
                xScale = env.GetImageBuffer("FilteredImage").Info.XResolution,
                yScale = env.GetImageBuffer("FilteredImage").Info.YResolution
            };
            
            return rangeBuffer;
        } 

        private CaptureBuffer CreateReferenceBuffer(ushort[] ushortArrayRef, ProcessingEnvironment env)
        {
            CaptureBuffer ReflBuf = new CaptureBuffer
            {
                Buf = ushortArrayRef,
                PaletteType = CaptureBuffer.PaletteTypes.Gray,
                Width = env.GetImageBuffer("FilteredImage").Info.Width,
                Height = env.GetImageBuffer("FilteredImage").Info.Height,
                xScale = env.GetImageBuffer("FilteredImage").Info.XResolution,
                yScale = env.GetImageBuffer("FilteredImage").Info.YResolution
            };

            return ReflBuf;
        }

        private void ExtractDataset(float[] floatArray, byte[] byteArray, ProcessingEnvironment env, PositionOfPallet position)
        {
            floatArray = env.GetImageBuffer("FilteredImage")._range;
            byteArray = env.GetImageBuffer("FilteredImage")._intensity;
            int width = env.GetImageBuffer("FilteredImage").Info.Width;
            int height = env.GetImageBuffer("FilteredImage").Info.Height;
            float xScale = env.GetImageBuffer("FilteredImage").Info.XResolution;
            float yScale = env.GetImageBuffer("FilteredImage").Info.YResolution;
            cntr = cntr + 1;
            DatasetExtraction.ExtractRangeForDataset((int)position, byteArray, floatArray, width, height, xScale, yScale, cntr, enableDatasetExtraction);
        }

        private int[] GetCentroidsDL(float[] floatArray, byte[] byteArray, CaptureBuffer rangeBuffer, CaptureBuffer ReflBuf, PositionOfPallet position, double NailHeight)
        {
            DateTime AnalysisStartTime;
            DateTime AnalysisStopTime;
            double AnalysisTotalSec;
            Bitmap bitmap = new Bitmap(ReflBuf.Width, ReflBuf.Height, GDII.PixelFormat.Format8bppIndexed);
            model model = new model();
            model.ByteArray2bitmap(byteArray, bitmap.Width, bitmap.Height, bitmap);
            

            string vizDLFolder = String.Format("VizDL/{0}", position);
            string fullImagePath = String.Format("VizDL/{0}/{0}.png", position);

            CheckIfDirectoryExists(vizDLFolder);          
            ReflBuf.SaveImage(fullImagePath, true);

            //Get Exec time
            AnalysisStartTime = DateTime.Now;

            //Process the image for inference
            float[] imageData = model.ProcessBitmapForInference(bitmap, bitmap.Width, bitmap.Height);
            float[][] results = model.RunInference2(imageData, bitmap.Height, bitmap.Width);

            AnalysisStopTime = DateTime.Now;
            AnalysisTotalSec = (AnalysisStopTime - AnalysisStartTime).TotalSeconds;
            Logger.WriteLine(String.Format("DeepLearning getCentroids FINISHED  -  {0:0.000} sec", AnalysisTotalSec));

            //Get the results
            float[] boxes = results[0];
            float[] labels = results[1];
            float[] scores = results[2];
            float[] masks = results[3];

            //Vizualice BB Results
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            string saveAt = Path.Combine(exePath, fullImagePath); 
            string resultsPath = String.Format("VizDL/{0}/{0}Results.png", position);
            int[] centroids = model.DrawCentroids(saveAt, boxes, scores, resultsPath, isSaveSideNailsProtrudingResults);          

            return centroids;
        }

        /// <summary>
        /// Attempts to generate and assign _FilenameDateTime. If it is null, a value is assigned.
        /// </summary>
        /// <returns>Indicates whether the assignment was successful</returns>
        public bool TryGenerateFilenameDateTime()
        {
            //if (_FilenameDateTime == null)
            //{
            lock (_lockFileName) // Ensure thread safety
            {
                //if (_FilenameDateTime == null) // Double-check to prevent concurrency issues
                //{
                DateTime createTime = DateTime.Now;

                // Set _DirectoryDateHour
                _DirectoryDateHour = string.Format("{0:0000}{1:00}{2:00}_{3:00}",
                                                   createTime.Year, createTime.Month, createTime.Day,
                                                   createTime.Hour);

                // Set _FilenameDateTime
                _FilenameDateTime = string.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}",
                                                  createTime.Year, createTime.Month, createTime.Day,
                                                  createTime.Hour, createTime.Minute, createTime.Second);

                return true; // Indicates successful assignment
                             //    }
                             //}
            }
            return false; // Indicates no assignment because a value already exists
        }


        public void TryNullFilenameDateTime()
        {
            _FilenameDateTime = null;
            _DirectoryDateHour = null;
        }

        private void SaveAllFrames()
        {
            TryGenerateFilenameDateTime();
            isSaveFrames = true;

            string FullDirectory = RecordingRootDir + "\\" + _DirectoryDateHour + "\\" + _FilenameDateTime;

            System.IO.Directory.CreateDirectory(FullDirectory);

            CopyCrackImagesToRecordings(FullDirectory, _FilenameDateTime);

            // Check if SickFrames is null or has a length of 0
            if (SickFrames == null || SickFrames.Length == 0)
            {
                throw new InvalidOperationException("SickFrames is null or uninitialized. Please check the data source.");
            }

            UpdateTextBlock(ModeStatus, "File Name: " + _FilenameDateTime);
            // Define the suffix for image files
            string[] suffixes = { "_T", "_B1", "_B2", "_B3", "_L", "_R", "_F", "_B" };

            // Check if the number of SickFrames matches the number of suffixes
            if (SickFrames.Length != suffixes.Length)
            {
                throw new InvalidOperationException("The number of SickFrames does not match the number of suffixes. Please check the input data.");
            }

            // Iterate through SickFrames and save each image
            object lockObjectSaveImage = new object();

            if (!Directory.Exists(FullDirectory))
            {
                Directory.CreateDirectory(FullDirectory);
            }

            SemaphoreSlim semaphore = new SemaphoreSlim(4); // Limit the number of concurrent threads

            Parallel.For(0, SickFrames.Length, i =>
            {
                semaphore.Wait();
                try
                {
                    if (SickFrames[i].HasLineCount())
                    {
                        string fullFilename = System.IO.Path.Combine(FullDirectory, _FilenameDateTime + suffixes[i]);
                        Logger.WriteLine($"Saving file: {fullFilename}");

                        try
                        {
                            lock (lockObjectSaveImage)
                            {
                                SickFrames[i].Save(fullFilename);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine($"Error saving file: {fullFilename}. Exception: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            TryNullFilenameDateTime();
        }        

        public float GetRangeValueAt(int x, int y, CaptureBuffer rangeBuffer, float[] floatArray)
        {
            // Validar que la coordenada esté dentro de los límites de la imagen
            if (x < 0 || x >= rangeBuffer.Width || y < 0 || y >= rangeBuffer.Height)
            {
                throw new ArgumentOutOfRangeException("Las coordenadas están fuera del rango de la imagen.");
            }

            // Calcular el índice en el buffer unidimensional
            int index = y * rangeBuffer.Width + x;
            return floatArray[index];
        }

        public float GetMaxRangeInSquare(int x, int y, int l, CaptureBuffer rangeBuffer, float[] floatArray)
        {
            // Definir los límites de la región de interés
            int startX = Math.Max(0, x - l);
            int endX = Math.Min(rangeBuffer.Width - 1, x + l);
            int startY = Math.Max(0, y - l);
            int endY = Math.Min(rangeBuffer.Height - 1, y + l);
            float maxVal = float.MinValue;
            Console.WriteLine("Cuadrado de valores en la región de interés:");

            // Recorrer la región de interés
            for (int i = startY; i <= endY; i++)
            {
                string rowValues = "";
                for (int j = startX; j <= endX; j++)
                {
                    int index = i * rangeBuffer.Width + j;
                    float value = floatArray[index];
                    if (!float.IsNaN(value))
                    {
                        maxVal = Math.Max(maxVal, value);
                    }
                    rowValues += value.ToString("F2") + " ";
                }
                Console.WriteLine(rowValues.Trim());
            }

            return maxVal;
        }

        public int GetHorizontalPosition(int x, CaptureBuffer rangeBuffer)
        {
            int third = rangeBuffer.Width / 3;
            if (x < third)
            {
                return 0;
            }
            else if (x < 2 * third)
            {
                return 1;
            }
            else
            {
                return 2;
            }
        }

        public float GetMaxRangeInCircle(int x, int y, int diameter, CaptureBuffer rangeBuffer, float[] floatArray)
        {
            int radius = diameter / 2;
            float maxVal = 0;

            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    int newX = x + i;
                    int newY = y + j;

                    // Verificar si el punto está dentro del círculo y dentro de los límites de la imagen
                    if (newX >= 0 && newX < rangeBuffer.Width && newY >= 0 && newY < rangeBuffer.Height &&
                        (i * i + j * j) <= (radius * radius))
                    {
                        float value = GetRangeValueAt(newX, newY, rangeBuffer, floatArray);
                        if (value > maxVal)
                        {
                            maxVal = value;
                        }
                    }
                }
            }
            return maxVal;
        }

        private void CheckIfDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Directory created: {path}");
            }
        }

        private void CopyCrackImagesToRecordings(string destinationFolder, string imageNewName)
        {
            try
            {
                foreach (var detection in remoteCameraCrackDetectionList)
                {
                    // Find index of the matching imageName in crackImagesList
                    int index = StorageWatchdog.crackImagesList.FindIndex(img => Path.GetFileName(img).Equals(detection.imageName, StringComparison.OrdinalIgnoreCase));

                    if (index != -1)
                    {
                        string destinationFullPath = $"{destinationFolder}\\{imageNewName}_{detection.location}.jpg";
                        File.Copy(StorageWatchdog.crackImagesList[index], destinationFullPath, true);
                        Logger.WriteLine($"Image '{StorageWatchdog.crackImagesList[index]}' copied successfully to '{destinationFullPath}'.");
                    }
                }
            }

            catch (IOException ex)
            {
                Logger.WriteLine($"Error copying image: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.WriteLine($"Access denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}
