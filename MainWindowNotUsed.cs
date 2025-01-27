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

namespace PalletCheck
{
    public partial class MainWindow : Window
    {


        private void OnNewFrameReceived(R3Cam cam, R3Cam.Frame frame)
        {
            lock (_lock)
            {
                FrameBuffer[cam.CameraName] = frame;  // 保存帧到缓冲区
                framesReceivedCount++;

                int cameraIndex = Cameras.IndexOf(cam); // Get the index of the camera in the list

                // Update the status to '1' for this camera
                if (cameraIndex >= 0)
                {
                    char[] statusArray = cameraStatus.ToCharArray(); // Convert to char array
                    statusArray[cameraIndex] = '√'; // Mark this camera as received
                    cameraStatus = new string(statusArray); // Convert back to string
                }

                // Update the UI to reflect the current camera status
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Assuming you have a Label or TextBlock on the UI to show the camera status
                    btnStatusControl.Content = $"Status: {cameraStatus}";  // Update the label
                });

                // If all camera frames have been received
                if (framesReceivedCount == Cameras.Count)
                {
                    framesReceivedCount = 0;  // Reset counter
                    OnAllFramesReceived?.Invoke();  // Trigger the merge event
                    cameraStatus = new string('X', Cameras.Count); // Reset the status to "000...0" based on the number of cameras
                }
            }
        }

        private void ProcessTop(R3Cam.Frame frame)
        {

            CaptureBuffer NewBuf = new CaptureBuffer(frame.Range, frame.Width, frame.Height);
            CaptureBuffer ReflBuf = new CaptureBuffer(frame.Reflectance, frame.Width, frame.Height);
            ReflBuf.PaletteType = CaptureBuffer.PaletteTypes.Gray;

            Pallet P = new Pallet(NewBuf, ReflBuf, PositionOfPallet.Top);
            PProcessorTop.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete, PositionOfPallet.Top);
        }
        private void StitchFrames(ParamStorage paramStorage)
        {
            int totalWidth = 0;
            int totalHeight = FrameBuffer[Cameras[0].CameraName].Height;

            List<ushort[]> ranges = new List<ushort[]>();
            List<byte[]> reflectances = new List<byte[]>();
            int currentXOffset = 0;

            // Jack Note: Traverse cameras and calculate total width using StartX and EndX arrays
            for (int i = 0; i < 3; i++)
            {
                var frame = FrameBuffer[Cameras[i].CameraName];
                ranges.Add(frame.Range);
                reflectances.Add(frame.Reflectance);

                // Jack Note: Calculate the stitching width of the current camera using StartX and EndX
                totalWidth += (EndX[i] - StartX[i]);
            }

            ushort[] combinedRange = new ushort[totalWidth * totalHeight];
            byte[] combinedReflectance = new byte[totalWidth * totalHeight];

            // Jack Note: Traverse each camera and perform stitching using parameters in arrays
            for (int i = 0; i < 3; i++)
            {
                var frame = FrameBuffer[Cameras[i].CameraName];

                // Jack Note: Use StartX, EndX, and OffsetZ from arrays
                CopyFrameData(frame, combinedRange, combinedReflectance, StartX[i], EndX[i], totalWidth, currentXOffset, OffsetZ[i]);

                currentXOffset += (EndX[i] - StartX[i]); // Jack Note: Update the current X offset
            }

            CaptureBuffer NewBuf = new CaptureBuffer(combinedRange, totalWidth, totalHeight);
            CaptureBuffer ReflBuf = new CaptureBuffer(combinedReflectance, totalWidth, totalHeight);
            ReflBuf.PaletteType = CaptureBuffer.PaletteTypes.Gray;

            Pallet P = new Pallet(NewBuf, ReflBuf, PositionOfPallet.Bottom);
            PProcessorBottom.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete, PositionOfPallet.Bottom);

        }

        private void CopyFrameData(R3Cam.Frame frame, ushort[] combinedRange, byte[] combinedReflectance, int startX, int endX, int totalWidth, int targetStartX, float offsetZ)
        {
            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int sourceIndex = y * frame.Width + x;
                    int targetIndex = y * totalWidth + (targetStartX + (x - startX));

                    // 对 range 数据进行偏移处理，加入 offsetZ
                    if (frame.Range[sourceIndex] != 0)
                    {
                        combinedRange[targetIndex] = (ushort)((frame.Range[sourceIndex] * frame.zScale + +frame.zOffset + offsetZ) * 20); //By *20 Make the mm per pixel = 0.05
                    }

                    // 直接拷贝 reflectance 数据
                    combinedReflectance[targetIndex] = frame.Reflectance[sourceIndex];
                }
            }
        }

        private void ProcessFrame(string cameraName, IFrame frame)
        {
            try
            {
                FrameBuffer[cameraName] = R3Cam.ConvertToFrame(frame, cameraName);  // 保存帧到缓冲区
                framesReceivedCount++;
                if (framesReceivedCount == 3)
                {
                    framesReceivedCount = 0;  // Reset counter
                    OnAllFramesReceived?.Invoke();  // Trigger the merge event
                                                    // cameraStatus = new string('X', Cameras.Count); // Reset the status to "000...0" based on the number of cameras
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{cameraName}] Error processing frame: {ex.Message}");
            }
        }


        public static string GetTargetFilePath(string currentFilePath, string targetFolderName)
        {
            if (string.IsNullOrEmpty(currentFilePath) || string.IsNullOrEmpty(targetFolderName))
            {
                throw new ArgumentException("当前文件路径和目标文件夹名称不能为空。");
            }

            // 获取当前文件所在文件夹路径
            string currentFolderPath = System.IO.Path.GetDirectoryName(currentFilePath);
            if (currentFolderPath == null)
            {
                throw new InvalidOperationException("无法获取当前文件的文件夹路径。");
            }

            // 获取上一级文件夹路径
            string parentFolderPath = System.IO.Directory.GetParent(currentFolderPath)?.FullName;
            if (parentFolderPath == null)
            {
                throw new InvalidOperationException("无法获取上一级文件夹路径。");
            }

            // 拼接目标文件夹路径
            string targetFolderPath = System.IO.Path.Combine(parentFolderPath, targetFolderName);

            // 获取当前文件名
            string fileName = System.IO.Path.GetFileName(currentFilePath);

            // 拼接目标文件的完整路径
            return System.IO.Path.Combine(targetFolderPath, fileName);
        }

        public string ConvertWindowsPathToUnixStyle(string windowsPath)
        {
            // 去掉驱动器号部分，例如 "C:" 或 "D:"
            string withoutDrive = windowsPath.Substring(windowsPath.IndexOf('\\'));  // 从第一个反斜杠开始截取

            // 将所有反斜杠替换为正斜杠
            string unixPath = withoutDrive.Replace("\\", "/");

            return unixPath;
        }


        private void EzRSaveImage(IFrame frame, string status, PositionOfPallet position)
        {
            DateTime CreateTime = DateTime.Now;
            //string status = Pass ? "Pass" : "Fail";

            string Filename = String.Format("{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}_{6}",
                                             CreateTime.Year, CreateTime.Month, CreateTime.Day,
                                             CreateTime.Hour, CreateTime.Minute, CreateTime.Second,
                                             status);

            string Directory = String.Format("{0:0000}{1:00}{2:00}_{3:00}",
                                             CreateTime.Year, CreateTime.Month, CreateTime.Day, CreateTime.Hour);

            string FullDirectory = RecordingRootDir + "\\" + Directory + "\\" + position;

            if (RecordModeEnabled && (RecordDir != ""))
                FullDirectory = RecordingRootDir + "\\" + RecordDir;

            System.IO.Directory.CreateDirectory(FullDirectory);
            string FullFilename = FullDirectory + "\\" + Filename;
            frame.Save(FullFilename);
        }


        private void LoadAndProcessCaptureFileFrame(PositionOfPallet position, TextBlock palletTextName)
        {
            //Logger.WriteLine("Loading CaptureBuffer from: " + FileName);


            OpenFileDialog OFD = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*", // 设置文件过滤器
                InitialDirectory = MainWindow.RecordingRootDir,       // 设置初始目录
                Title = "Select an XML File"                          // 设置对话框标题
            };

            // 判断对话框结果
            bool? result = OFD.ShowDialog();

            // 如果用户点击了 Cancel 或没有选择文件，直接退出方法
            if (result != true)
            {
                // 记录取消操作（可选）
                Logger.WriteLine("User cancelled the file dialog.");
                return; // 直接退出
            }

            try
            {
                // 加载 XML 文件
                string filePath = OFD.FileName;

                // 进一步检查是否有有效的文件路径
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    UpdateTextBlock(palletTextName, filePath);
                }
                else
                {
                    Logger.WriteLine("No valid file selected.");
                }
            }
            catch (Exception ex)
            {
                // 捕获和记录异常
                Logger.WriteLine($"Error loading file: {ex.Message}");
            }

            ClearAnalysisResults(position);

            SickFrames[0] = IFrame.Load(OFD.FileName);

            Pallet P = CreateTopPallet();


            //P.NotLive = true;
            PProcessorTop.ProcessPalletHighPriority(P, Pallet_OnProcessSinglePalletAnalysisComplete, position);
            //PProcessor.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete);


        }

        private void LoadAndProcessCapture3FileFrame(PositionOfPallet position, TextBlock palletTextName)
        {
            //Logger.WriteLine("Loading CaptureBuffer from: " + FileName);


            OpenFileDialog OFD = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*", // 设置文件过滤器
                InitialDirectory = MainWindow.RecordingRootDir,        // 设置初始目录
                Title = "Select Bottom Images"                           // 设置对话框标题
            };

            // 检查对话框结果
            if (OFD.ShowDialog() != true)
            {
                // 用户点击取消或未选择文件，记录并退出
                Logger.WriteLine("File dialog cancelled by user.");
                return; // 直接退出
            }

            try
            {
                // 加载 XML 文件
                string filePath = OFD.FileName;

                // 检查路径是否有效
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    Logger.WriteLine("No valid file selected.");
                    return; // 退出方法
                }

                // 更新 UI 或执行文件处理逻辑
                UpdateTextBlock(palletTextName, filePath);
            }
            catch (Exception ex)
            {
                // 捕获并记录异常
                Logger.WriteLine($"Error loading file: {ex.Message}");
            }

            ClearAnalysisResults(position);

            // 获取选中文件的完整路径
            string selectedFile = OFD.FileName;

            // 获取文件目录和文件名
            string directory = System.IO.Path.GetDirectoryName(selectedFile);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(selectedFile);

            // 提取公共部分
            string baseName = fileName.Substring(0, fileName.LastIndexOf('_') + 1);

            // 生成三个文件的完整路径
            string file1 = System.IO.Path.Combine(directory, baseName + "B1.xml");
            string file2 = System.IO.Path.Combine(directory, baseName + "B2.xml");
            string file3 = System.IO.Path.Combine(directory, baseName + "B3.xml");

            // 加载文件
            try
            {
                SickFrames[1] = IFrame.Load(file1);
                SickFrames[2] = IFrame.Load(file2);
                SickFrames[3] = IFrame.Load(file3);

                Logger.WriteLine("三个文件加载成功！");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("加载文件时出错：" + ex.Message);
            }

            Pallet P = CreateBottomPallet();

            //P.NotLive = true;
            PProcessorBottom.ProcessPalletHighPriority(P, Pallet_OnProcessSinglePalletAnalysisComplete, position);
            //PProcessor.ProcessPalletHighPriority(P, Pallet_OnLivePalletAnalysisComplete);

        }


        //// 生成显示字符串
        //string strDisplay2 = "Position: " + position.ToString() +
        //                     " | Missing Block: " + string.Join(", ", missingBlocks ?? Array.Empty<double>()) +
        //                     " | Missing Chunk: " + string.Join(", ", missingChunks ?? Array.Empty<double>()) +
        //                     " | Rotated Blocks: " + string.Join(", ", RotateResult ?? Array.Empty<double>());



        //bool isRotate90 = Convert.ToBoolean(ParamStorage.GetInt("Rotate90")); ;
        //if (!isRotate90)
        //{
        //    int SX = LROI + (LWid / 2);    // Jack Note: Get the Position of the start X 
        //    int EX = RROI - (RWid / 2);



        //    if (TopY < 0)
        //    {
        //        AddDefect(null, PalletDefect.DefectType.board_segmentation_error, "Could not lock onto top board");
        //        State = InspectionState.Fail;
        //        return;
        //    }

        //    /// Find the Horizontal Region (Jack)
        //    ///Jack Note: Set the ROI for each board, Not very accurate

        //    int H1_T = Math.Max(0, TopY - 20);// - 100);
        //    int H1_B = TopY + HBoardWid + 360;
        //    Logger.WriteLine(string.Format("TopY:{0}  H1_T:{1}  H1_B:{2}", TopY, H1_T, H1_B));

        //    int H2_T = TopY + (LBL / 2) - 400;
        //    int H2_B = TopY + (LBL / 2) + 400;
        //    //Logger.WriteLine(string.Format("ISOLATE H2_T,H2_B {0} {1}", H2_T, H2_B));

        //    int H3_T = TopY + LBL - HBoardWid - 250;
        //    int H3_B = TopY + LBL + 200;

        //    H3_B = Math.Min(SourceCB.Height - 1, H3_B);
        //    Board H1 = ProbeVertically(ProbeCB, "H1", PalletDefect.DefectLocation.H1, SX, EX, 1, H1_T, H1_B);
        //    Board H2 = ProbeVertically(ProbeCB, "H2", PalletDefect.DefectLocation.H2, SX, EX, 1, H2_T, H2_B);
        //    Board H3 = ProbeVertically(ProbeCB, "H3", PalletDefect.DefectLocation.H3, SX, EX, 1, H3_T, H3_B);
        //    Board V1 = ProbeHorizontally(ProbeCB, "V1", PalletDefect.DefectLocation.V1, H1_T, H3_B, 1, LROI, LROI + (int)(LWid * 2));
        //    Board V2 = ProbeHorizontally(ProbeCB, "V2", PalletDefect.DefectLocation.V2, H1_T, H3_B, 1, RROI, RROI - (int)(RWid * 2));

        //    BList.Add(H1);
        //    BList.Add(H2);
        //    BList.Add(H3);
        //    BList.Add(V1);
        //    BList.Add(V2);
        //}
        //else

        //private void Pallet_OnLivePalletAnalysisComplete(Pallet P, PositionOfPallet position)
        //{
        //    Logger.WriteLine("Pallet_OnLivePalletAnalysisComplete Start");
        //    if (!BypassModeEnabled)
        //    {
        //        // Update production stats
        //        UpdateProductivityStats(P);
        //    }

        //    // Notify PLC of pallet results
        //    //SendPLCResults(P);
        //    //SendPLCTotalResult(P);
        //    UpdateUIWithPalletResults(P, position);

        //    // Save Results to Disk
        //    //{
        //    //    try
        //    //    {

        //    //        //// Set FTP server credentials
        //    //        //string ftpServer = ParamStorageGeneral.GetString("FTP Server IP");
        //    //        //string username = ParamStorageGeneral.GetString("FTP UserName");
        //    //        //string password = ParamStorageGeneral.GetString("FTP Password");
        //    //        //bool isFTPUpload = Convert.ToBoolean(ParamStorageGeneral.GetInt("UploadOrNot")); ;

        //    //        string FullDirectory = RecordingRootDir + "\\" + P.Directory;

        //    //        if (RecordModeEnabled && (RecordDir != ""))
        //    //            FullDirectory = RecordingRootDir + "\\" + RecordDir;

        //    //        System.IO.Directory.CreateDirectory(FullDirectory);
        //    //        string FullFilename = FullDirectory + "\\" + P.Filename;
        //    //        //string FullFilename = FullDirectory + "Test.r3";
        //    //        Logger.WriteLine("Saving Pallet to " + FullFilename);



        //    //        // 判断是否存在缺陷
        //    //        if (P.AllDefects.Count > 0)
        //    //        {
        //    //            // 保存带缺陷的文件
        //    //            P.Original.Save(FullFilename.Replace(".r3", "_F_rng.r3"));
        //    //            P.Original.SaveImage(FullFilename.Replace(".r3", "_F_rng.png"), false);
        //    //            P.ReflectanceCB.Save(FullFilename.Replace(".r3", "_F_rfl.r3"));
        //    //            P.ReflectanceCB.SaveImage(FullFilename.Replace(".r3", "_F_rfl.png"), true);


        //    //            // 上传带缺陷的文件到 FTP
        //    //            if (isFTPUpload)
        //    //            {
        //    //                bool resultR3Fng = P.Original.UploadToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_F_rng.r3")));
        //    //                bool resultPngFng = P.Original.UploadPngToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_F_rng.png")));
        //    //                bool resultR3Rfl = P.ReflectanceCB.UploadToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_F_rfl.r3")));
        //    //                bool resultPngRfl = P.ReflectanceCB.UploadPngToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_F_rfl.png")));

        //    //                if (resultR3Fng && resultPngFng && resultR3Rfl && resultPngRfl)
        //    //                {
        //    //                    Logger.WriteLine("All defect files uploaded to FTP successfully.");
        //    //                }
        //    //                else
        //    //                {
        //    //                    Logger.WriteLine("Error uploading defect files to FTP.");
        //    //                }
        //    //            }

        //    //        }
        //    //        else
        //    //        {
        //    //            // 保存没有缺陷的文件
        //    //            P.Original.Save(FullFilename.Replace(".r3", "_P_rng.r3"));
        //    //            P.Original.SaveImage(FullFilename.Replace(".r3", "_P_rng.png"), false);
        //    //            P.ReflectanceCB.Save(FullFilename.Replace(".r3", "_P_rfl.r3"));
        //    //            P.ReflectanceCB.SaveImage(FullFilename.Replace(".r3", "_P_rfl.png"), true);

        //    //            //上传没有缺陷的文件到 FTP
        //    //            if (isFTPUpload)
        //    //            {
        //    //                bool resultR3Png = P.Original.UploadToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_P_rng.r3")));
        //    //                bool resultPngPng = P.Original.UploadPngToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_P_rng.png")));
        //    //                bool resultR3Rfl = P.ReflectanceCB.UploadToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_P_rfl.r3")));
        //    //                bool resultPngRfl = P.ReflectanceCB.UploadPngToFtp(ftpServer, username, password, ConvertWindowsPathToUnixStyle(FullFilename.Replace(".r3", "_P_rfl.png")));

        //    //                if (resultR3Png && resultPngPng && resultR3Rfl && resultPngRfl)
        //    //                {
        //    //                    Logger.WriteLine("All non-defect files uploaded  to FTP successfully.");
        //    //                }
        //    //                else
        //    //                {
        //    //                    Logger.WriteLine("Error uploading non-defect files to FTP.");
        //    //                }
        //    //            }

        //    //        }



        //    //        bool HasSegmentationError = false;
        //    //        foreach (PalletDefect PD in P.AllDefects)
        //    //            if (PD.Code == "ER")
        //    //                HasSegmentationError = true;

        //    //        if (HasSegmentationError)
        //    //        {
        //    //            FullFilename = SegmentationErrorRootDir + "\\" + P.Filename;
        //    //            Logger.WriteLine("Saving Pallet to " + FullFilename);
        //    //            P.Original.Save(FullFilename.Replace(".r3", "_rng.r3"));
        //    //            P.Original.SaveImage(FullFilename.Replace(".r3", "_rng.png"), false);
        //    //            P.ReflectanceCB.Save(FullFilename.Replace(".r3", "_rfl.r3"));
        //    //            P.ReflectanceCB.SaveImage(FullFilename.Replace(".r3", "_rfl.png"), true);
        //    //        }
        //    //    }
        //    //    catch (Exception e)
        //    //    {
        //    //        Logger.WriteException(e);
        //    //    }
        //    //}

        //    Logger.WriteLine("Pallet_OnLivePalletAnalysisComplete Complete");
        //    //Logger.WriteBorder("PALLET COMPLETE: " + P.Filename);

        //}
    }
}
