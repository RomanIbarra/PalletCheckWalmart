using Microsoft.Win32;
using Sick.EasyRanger;
using Sick.EasyRanger.Base;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static PalletCheck.Pallet;

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
            // Select environment and view based on position
            var env = (position == PositionOfPallet.Left) ? _envLeft : _envRight;
            var viewer = (position == PositionOfPallet.Left) ? ViewerLeft : ViewerRight;
            
            //List<Board> CurrentBlist = (position == PositionOfPallet.Left) ? BListLeft.ToList() : BListRight.ToList();            
            Pallet P = new Pallet(null, null, position);
            P.BList = new List<Board>();

            if (position == PositionOfPallet.Left)
            {
                env.GetStepProgram("Main").StepList[1].Enabled = true;
                env.GetStepProgram("Main").StepList[2].Enabled = false;
                env.GetStepProgram("Main").StepList[3].Enabled = false;
                P.BList.Add(new Board("Block1", PalletDefect.DefectLocation.L_B1, true, 0, 0));
                P.BList.Add(new Board("Block2", PalletDefect.DefectLocation.L_B2, true, 0, 0));
                P.BList.Add(new Board("Block3", PalletDefect.DefectLocation.L_B3, true, 0, 0));
            }

            else
            {
                env.GetStepProgram("Main").StepList[1].Enabled = false;
                env.GetStepProgram("Main").StepList[2].Enabled = true;
                env.GetStepProgram("Main").StepList[3].Enabled = true;
                P.BList.Add(new Board("Block1", PalletDefect.DefectLocation.R_B1, true, 0, 0));
                P.BList.Add(new Board("Block2", PalletDefect.DefectLocation.R_B2, true, 0, 0));
                P.BList.Add(new Board("Block3", PalletDefect.DefectLocation.R_B3, true, 0, 0));
            }

            var paramStorage = position == PositionOfPallet.Left ? ParamStorageLeft : ParamStorageRight;
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
            double MissingChunkThreshold = paramStorage.GetFloat(StringsLocalization.MissingChunkHeightDeviationThreshold);
            double MissingBlockPercentage = paramStorage.GetFloat(StringsLocalization.MissingBlockMinimumRequiredAreaPercentage);
            double MiddleBoardMissingWoodMaxPercentage = paramStorage.GetFloat(StringsLocalization.MiddleBoardMissingWoodMaxPercentage);
            int ForkClearancePercentage = paramStorage.GetInt(StringsLocalization.ForkClearancePercentage);
            int RefTop = paramStorage.GetInt(StringsLocalization.Top);
            int RefBottom = paramStorage.GetInt(StringsLocalization.Bottom);
            int CropX = paramStorage.GetInt(StringsLocalization.CropX);  //new for Clearance
            double NailHeight = paramStorage.GetFloat(StringsLocalization.ProtrudingNailHeight);

            try
            {
                env.SetInteger("InputRefTop", RefTop);
                env.SetInteger("InputRefBottom", RefBottom);
                env.SetInteger("InputCropX", CropX);
                env.SetDouble("NailHeight", NailHeight);
                env.GetStepProgram("Main").RunFromBeginning();
                double[] missingBlocks = env.GetDouble("OutputMissBlock");
                double[] missingChunks = env.GetDouble("OutputMissChunk");
                double[] RotateResult = env.GetDouble("OutputAngle");
                double[] blockHeight = env.GetDouble("OutputHeights");
                Point[] PointForDisplay = env.GetPoint2D("OutputCenters");
                double XResolutin = env.GetDouble("M_XResolution", 0);
                double YResolutin = env.GetDouble("M_YResolution", 0);
                int [] intClearance = env.GetInteger("ClearanceInt");
                int NailLength = env.GetInteger("NailLength", 0);
                int[] HasNails = env.GetInteger("OutputNails");
                int[] middleBoardHasBlobs = env.GetInteger("MiddleBoardHasBlobs");
                bool isFail = false;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewer.ClearAll();
                    viewer.DrawImage("FilteredImage", SubComponent.Intensity);
                    viewer.DrawRoi("OutputRegionsForNails", -1, red, 250);

                    for (int i = 0; i < 3; i++)
                    {
                        if (HasNails[i] == 1)
                        {
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.raised_nail, "Side Nails protruding out sides of pallet > " + NailHeight + "mm");
                        }
                    }

                    for (int i = 0; i < 2; i++)
                    {
                        if(intClearance[i] < ForkClearancePercentage)
                        {
                            viewer.DrawRoi("ClearanceRegions", i, red, 100);
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.clearance, "Fork Clearance is " + intClearance[i] + "% less than " + ForkClearancePercentage + "%");
                        }

                        else
                        {
                            viewer.DrawRoi("ClearanceRegions", i, green, 100);
                        }
                    }
                    viewer.DrawText("ClearanceText", white);

                    for (int i = 0; i < 3; i++)
                    {
                        string[] textArray = new string[] {Math.Abs (RotateResult[i]).ToString() + "°" };
                        float[] xArray = new float[] { (float)PointForDisplay[i].X };
                        float[] yArray = new float[] { (float)PointForDisplay[i].Y-200 };
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

                        if (missingChunks[i] < (BlockWidth[i] / XResolutin) * (BlockHeight[i] / YResolutin) * MissingBlockPercentage / 100 ||blockHeight[i] > MissingChunkThreshold)
                        {
                            if (blockHeight[i] < -MissingChunkThreshold)
                            { P.AddDefect(P.BList[i], PalletDefect.DefectType.missing_chunks, "Missing Chunks: Average Height " + Math.Round((blockHeight[i]), 2) + "mm < -" + MissingChunkThreshold + "mm"); }
                            if (blockHeight[i] > MissingChunkThreshold)
                            { P.AddDefect(P.BList[i], PalletDefect.DefectType.blocks_protuded_from_pallet, "Block protrude from pallet " + Math.Round(Math.Abs(blockHeight[i]), 2) + "mm > " + MissingChunkThreshold + "mm"); }

                            viewer.DrawRoi("OutputRegions", i, red, 100);
                            isFail = true;
                        }
                     
                        env.SetText("MyText", textArray , xArray, yArray, sizeArray);

                        if (Math.Abs(RotateResult[i]) > AngleThresholdDegree)
                        {
                            P.AddDefect(P.BList[i], PalletDefect.DefectType.excessive_angle, "Twisted Blocks Angle: " + Math.Abs(RotateResult[i]).ToString() +  "° > " + AngleThresholdDegree.ToString() +"°");
                            viewer.DrawText("MyText" , red);
                            isFail = true;
                        }

                        else
                        {
                            viewer.DrawText("MyText", green);
                        }

                        lock (LockObjectCombine)
                        {
                            CombinedBoards.Add(P.BList[i]);
                            CombinedDefects.AddRange(P.BList[i].AllDefects);
                        }                     
                    }
                    
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
                            P.AddDefect(null, PalletDefect.DefectType.missing_wood, "Missing Wood");
                            viewer.DrawRoi("MB_ROI", -1, red, 100);
                        }
                    }

                    P.BList.Clear();              
                });

                ProcessCameraResult((int)position, !isFail ? InspectionResult.PASS : InspectionResult.FAIL);
                callback?.Invoke(!isFail);
            }

            catch (Exception ex)
            {
                UpdateTextBlock(LogText,ex.Message, MessageState.Normal);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    viewer.ClearAll();
                    viewer.DrawImage("ImageFinal", SubComponent.Intensity);
                    MessageBox.Show(ex.Message);
                });
                ProcessCameraResult((int)position, false ? InspectionResult.PASS : InspectionResult.FAIL);
                callback?.Invoke(false);
            }        
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


        private void LoadFile(string buttonContent, Action<string> loadAction, TextBlock palletTextName, PositionOfPallet position)
        {
            Logger.ButtonPressed(buttonContent);
            OpenFileDialog OFD = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                InitialDirectory = MainWindow.RecordingRootDir,       // Setting the initial catalog
                Title = "Select an XML File"           
            };

            if (OFD.ShowDialog() == true)
            {
                try
                {
                    string filePath = OFD.FileName;
                    loadAction(filePath);
                    UpdateTextBlock(palletTextName, filePath);               
                }

                catch (Exception ex)
                {
                    Logger.WriteLine($"Error loading file: {ex.Message}");
                }
            }

            else
            {
                Logger.ButtonPressed("File dialog cancelled by user.");
            }

            Task.Run(() => ProcessMeasurement((result) =>
            {
            }, position));
        }
    }
}
