using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Windows.Media;

namespace PalletCheck
{
    public class StorageWatchdog
    {
        public Thread WatchdogThread = null;
        public bool KillThread = false;
        public static List<string> DoNotDeleteDirs = new List<string>();
        ParamStorage ParamStorageGeneral = MainWindow.ParamStorageGeneral;
        public static List<String> crackImagesList = new List<string>();
        private string _crackImageName = null;
        private FileSystemWatcher watcher;

        public StorageWatchdog()
        {
        }

        public static void AddDoNotDeleteDirs(string dir)
        {
            dir = dir.ToUpper();
            DoNotDeleteDirs.Add(dir);
            Logger.WriteLine("StorageWatchdog: AddDoNotDeleteDirs " + dir);
        }

        public static FolderSizeInfo GetDirectorySizeRecursive(DirectoryInfo root, int levels = 0)
        {

            var currentDirectory = new FolderSizeInfo();

            // Add file sizes.
            FileInfo[] fis = root.GetFiles();
            currentDirectory.Size = 0;
            currentDirectory.Files = new List<FileInfo>();
            currentDirectory.Files.AddRange(fis);
            currentDirectory.FilesWithChildren = new List<FileInfo>();
            currentDirectory.FilesWithChildren.AddRange(fis);
            foreach (FileInfo fi in fis)
            {
                currentDirectory.Size += fi.Length;
            }

            // Add subdirectory sizes.
            DirectoryInfo[] dis = root.GetDirectories();

            currentDirectory.Path = root;
            currentDirectory.SizeWithChildren = currentDirectory.Size;
            currentDirectory.DirectoryCount = dis.Length;
            currentDirectory.DirectoryCountWithChildren = dis.Length;
            currentDirectory.FileCount = fis.Length;
            currentDirectory.FileCountWithChildren = fis.Length;
            currentDirectory.AllDirectories = new List<DirectoryInfo>();
            currentDirectory.AllDirectories.AddRange(dis);

            if (levels >= 0)
                currentDirectory.Children = new List<FolderSizeInfo>();

            foreach (DirectoryInfo di in dis)
            {
                var dd = GetDirectorySizeRecursive(di, levels - 1);
                if (levels >= 0)
                    currentDirectory.Children.Add(dd);

                currentDirectory.SizeWithChildren += dd.SizeWithChildren;
                currentDirectory.DirectoryCountWithChildren += dd.DirectoryCountWithChildren;
                currentDirectory.FileCountWithChildren += dd.FileCountWithChildren;
                currentDirectory.FilesWithChildren.AddRange(dd.FilesWithChildren);
                currentDirectory.AllDirectories.AddRange(dd.AllDirectories);
            }

            return currentDirectory;
        }

        public static FolderSizeInfo GetDirectorySize(string rootPath)
        {
            return GetDirectorySizeRecursive(new DirectoryInfo(rootPath));
        }

        public class FolderSizeInfo
        {
            public DirectoryInfo Path { get; set; }
            public long SizeWithChildren { get; set; }
            public long Size { get; set; }
            public int DirectoryCount { get; set; }
            public int DirectoryCountWithChildren { get; set; }
            public int FileCount { get; set; }
            public int FileCountWithChildren { get; set; }
            public List<FolderSizeInfo> Children { get; set; }
            public List<FileInfo> Files { get; set; }
            public List<FileInfo> FilesWithChildren { get; set; }
            public List<DirectoryInfo> AllDirectories { get; set; }
        }
        //================================================================================================================

        static void DeleteEmptyDirs(string dir)
        {
            if (String.IsNullOrEmpty(dir))
                throw new ArgumentException(
                    "Starting directory is a null reference or an empty string",
                    "dir");

            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    DeleteEmptyDirs(d);
                }

                var entries = Directory.EnumerateFileSystemEntries(dir);

                if (!entries.Any())
                {
                    try
                    {
                        string matchDir = dir.ToUpper();
                        bool dodeletion = !DoNotDeleteDirs.Contains(matchDir);
                        Logger.WriteLine("DeleteEmptyDirs " + dodeletion + "  " + matchDir);

                        if (dodeletion)
                            Directory.Delete(dir);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        //================================================================================================================
        public void EnforceStorageSize(string path, string settingsStr, double maxMB)
        {
            
            try
            {
                if (!ParamStorageGeneral.Contains(settingsStr))
                {
                    ParamStorageGeneral.Set("Storage", settingsStr, maxMB.ToString());
                }

                maxMB = ParamStorageGeneral.GetInt(settingsStr);

                if (maxMB < 1) return;

                double margin = (maxMB > 1000) ? (20) : (5);
                double SizeToTriggerDeletion = (maxMB + margin) * 1048576;
                double SizeToStopDeletion = (maxMB - margin) * 1048576;
                FolderSizeInfo fsi = GetDirectorySize(path);
                double CurrentSize = fsi.SizeWithChildren;

                Logger.WriteLine(String.Format("EnforceStorageSize {0} : is {1} vs {2} with {3}/{4} files", path, CurrentSize, maxMB * 1048576, fsi.FileCountWithChildren, fsi.FilesWithChildren.Count));

                if (CurrentSize > SizeToTriggerDeletion)
                {
                    Logger.WriteLine(String.Format("EnforceStorageSize {0} EXCEEDS LIMIT: is {1} vs {2}", path, CurrentSize, maxMB * 1048576));

                    // Get sorted list of files
                    FileInfo[] files = fsi.FilesWithChildren.ToArray();
                    Array.Sort(files, (x, y) => Comparer<DateTime>.Default.Compare(x.LastWriteTime, y.LastWriteTime));
                    //Logger.WriteLine(String.Format("FILES {0} | {1}  {2}  {3}  {4}", files.Length, files[0].Name, files[1].Name, files[files.Length - 2].Name, files[files.Length - 1].Name));

                    // Decide which files to delete
                    double estTotalSizeAfterDeletions = CurrentSize;
                    List<FileInfo> deleteList = new List<FileInfo>();
                    foreach (FileInfo fi in files)
                    {
                        deleteList.Add(fi);
                        estTotalSizeAfterDeletions -= fi.Length;
                        if (estTotalSizeAfterDeletions <= SizeToStopDeletion)
                            break;

                        // Only willing to delete up to 100 files in one shot
                        if (deleteList.Count >= 500)
                            break;
                    }

                    Logger.WriteLine(String.Format("EnforceStorageSize {0} found {1} of {2} files to delete", path, deleteList.Count, files.Length));

                    // Apply deletions
                    foreach (FileInfo fi in deleteList)
                    {
                        try
                        {
                            Logger.WriteLine("EnforceStorageSize DELETING " + fi.FullName + "   " + fi.Length);
                            fi.Delete();
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine("Failed to delete file: " + fi.FullName + "  :  " + ex.GetType());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
            }


            // Check for directories to delete
            try
            {
                DeleteEmptyDirs(path);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
            }
        }

        public void EnforceStorageSize()
        {
            Logger.WriteBorder("EnforceStorageSize");
            EnforceStorageSize(MainWindow.RecordingRootDir, "Recording Root Dir Max (MB)", 200000);
            EnforceStorageSize(MainWindow.SegmentationErrorRootDir, "Segmentation Error Dir Max (MB)", 50000);
            EnforceStorageSize(MainWindow.HistoryRootDir, "History Dir Max (MB)", 50000);
            EnforceStorageSize(MainWindow.CameraConfigHistoryRootDir, "Camera Config History Dir Max (MB)", 1000);
            EnforceStorageSize(MainWindow.SettingsHistoryRootDir, "Settings History Dir Max (MB)", 1000);
            EnforceStorageSize(MainWindow.SnapshotsRootDir, "Snapshots Dir Max (MB)", 10000);
            EnforceStorageSize(MainWindow.LoggingRootDir, "Logging Dir Max (MB)", 10000);
            EnforceStorageSize(MainWindow.ExceptionsRootDir, "Exceptions Dir Max (MB)", 1000);
            Logger.WriteLine("");
        }
        //================================================================================================================

        public string GetStorageReportString(bool doLogging, string path, string title, string budgetName)
        {
            string info = "";
            try
            {
                FolderSizeInfo fsi = GetDirectorySize(path);

                double budgetGB = 0;
                if (ParamStorageGeneral.Contains(budgetName))
                    budgetGB = Math.Round((double)ParamStorageGeneral.GetInt(budgetName) / (1000), 3);

                double currentGB = Math.Round((double)fsi.SizeWithChildren / (1048576 * 1024), 3);

                info = string.Format("{0:0.000} of {1:0.000} (GB)  |  {2} files,  {3} folders", currentGB, budgetGB, fsi.FileCountWithChildren, fsi.DirectoryCount);
            }
            catch (Exception ex)
            {
                info = ex.GetType().ToString();
            }
            StatusStorage.Set(title, info);
            if (doLogging) Logger.WriteLine(title + "  :  " + info);
            return info;
        }

        public void UpdateStorageStatus(bool DoLogging)
        {
            try
            {
                if (DoLogging) Logger.WriteBorder("STORAGE UPDATE");

                Stopwatch sw = new Stopwatch();
                sw.Start();
                GetStorageReportString(DoLogging, MainWindow.RootDir, "Storage.Root", "");
                GetStorageReportString(DoLogging, MainWindow.RecordingRootDir, "Storage.Recording", "Recording Root Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.SegmentationErrorRootDir, "Storage.Recording.SegErrors", "Segmentation Error Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.ConfigRootDir, "Storage.Config", "");
                GetStorageReportString(DoLogging, MainWindow.HistoryRootDir, "Storage.History", "History Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.CameraConfigHistoryRootDir, "Storage.History.CameraConfig", "Camera Config History Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.SettingsHistoryRootDir, "Storage.History.Settings", "Settings History Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.SnapshotsRootDir, "Storage.History.Snapshots", "Snapshots Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.LoggingRootDir, "Storage.Logging", "Logging Dir Max (MB)");
                GetStorageReportString(DoLogging, MainWindow.ExceptionsRootDir, "Storage.Logging.Exceptions", "Exceptions Dir Max (MB)");

                {
                    DriveInfo[] allDrives = DriveInfo.GetDrives();
                    foreach (DriveInfo d in allDrives)
                    {
                        double freeGB = Math.Round((double)d.AvailableFreeSpace / (1048576 * 1024.0), 0);
                        double totalGB = Math.Round((double)d.TotalSize / (1048576 * 1024.0), 0);
                        double usedGB = totalGB - freeGB;
                        string info = string.Format("{0} free(GB)  {1} total(GB)  {2} used(GB)", freeGB, totalGB, usedGB);
                        StatusStorage.Set("Storage.Drive." + d.Name, info);
                        if (DoLogging) Logger.WriteLine("Storage.Drive." + d.Name + "  :  " + info);
                    }
                }

                sw.Stop();
                StatusStorage.Set("Storage.LastCheckTime", DateTime.Now.ToString());
                StatusStorage.Set("Storage.ExecTime", sw.Elapsed.ToString());
                if (DoLogging)
                {
                    Logger.WriteLine("Storage.ExecTime  :  " + sw.Elapsed.ToString());
                    Logger.WriteLine("");
                }
            }
            catch (Exception)
            {

            }
        }

        public void WatchdogThreadFunc()
        {
            string RootDir = MainWindow.RecordingRootDir;

            Thread.Sleep(5000);

            UpdateStorageStatus(true);
            EnforceStorageSize();

            DateTime NextEnforceStorageDT = DateTime.Now.AddMinutes(5);
            DateTime NextUpdateStatusDT = DateTime.Now.AddSeconds(10);

            while (!KillThread)
            {
                Thread.Sleep(1000);

                if (DateTime.Now >= NextEnforceStorageDT)
                {
                    NextEnforceStorageDT = DateTime.Now.AddMinutes(5);
                    NextUpdateStatusDT = DateTime.Now.AddMinutes(15);
                    EnforceStorageSize();
                    UpdateStorageStatus(true);
                }

                if (DateTime.Now >= NextUpdateStatusDT)
                {
                    NextUpdateStatusDT = DateTime.Now.AddMinutes(15);
                    UpdateStorageStatus(true);
                }

                //int MaxDirs = ParamStorage.GetInt("Max Recording Directories");

                //try
                //{
                //    FolderSizeInfo fsi = GetDirectorySize(RootDir);
                //    Logger.WriteLine(String.Format("StorageWatchdog Found: {0}  {1}  {2}", fsi.SizeWithChildren, fsi.FileCountWithChildren, fsi.DirectoryCount));
                //    //StatusStorage.Set("StorageSize", fsi.SizeWithChildren.ToString());

                //    string[] folders = System.IO.Directory.GetDirectories(RootDir, "*", System.IO.SearchOption.AllDirectories);
                //    Array.Sort(folders);
                //    Logger.WriteLine("StorageWatchdog Found: " + folders.Length.ToString() + "  folders.");

                //    //for (int i = 0; i < folders.Length; i++)
                //    //{
                //    //    Logger.WriteLine("StorageWatchdog Found: " + i.ToString() + "  " + folders[i]);
                //    //}

                //    if (folders.Length > MaxDirs)
                //    {
                //        string DelFolder = folders[0];
                //        Logger.WriteLine("StorageWatchdog - Removing:" + DelFolder);
                //        Directory.Delete(DelFolder, true);
                //        Thread.Sleep(10000);
                //    }
                //}
                //catch (Exception)
                //{ }

            }
        }

        public void Start()
        {
            Logger.WriteLine("Starting Storage Watchdog");

            if (WatchdogThread == null)
            {
                KillThread = false;
                WatchdogThread = new Thread(new ThreadStart(WatchdogThreadFunc));
                WatchdogThread.Name = "Watchdog Thread";
                WatchdogThread.Start();
            }
        }

        public void Stop()
        {
            Logger.WriteLine("Stop Storage Watchdog");
            if (WatchdogThread != null)
            {
                KillThread = true;
            }
        }

        public void WatchFolder(string imageName)
        {
            _crackImageName = imageName;     
            string watchPath = "C:\\PalletCheck\\CrackWatchFolder"; // Replace this in the future

            if (!Directory.Exists(watchPath)) // Make sure path exists
            {
                Directory.CreateDirectory(watchPath);
            }

            watcher = new FileSystemWatcher
            {
                Path = watchPath,
                Filter = "*.png",
                NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.CreationTime
            };

            watcher.EnableRaisingEvents = true;
            watcher.Created += OnCreated;  
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (string.Equals(Path.GetFileName(e.Name), _crackImageName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.WriteLine($"Crack Image received: {e.FullPath}");
                crackImagesList.Add(e.FullPath);
                //ButtonChanger.Instance.EnableButton(true);

                // Stop the watcher and clean up
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
            
        }

    }
}
