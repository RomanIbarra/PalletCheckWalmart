using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PalletCheck
{
    public static class FileUtils
    {
        private static readonly object logLock = new object();

        // 获取当前目录
        public static string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        // 检查目录是否存在
        public static bool FolderExists(string path)
        {
            return Directory.Exists(path);
        }

        // 检查磁盘分区是否存在
        public static bool DiskExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return Directory.Exists(Path.GetPathRoot(path));
        }

        // 创建目录
        public static void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        // 检查文件是否存在
        public static bool FileExists(string fileName)
        {
            return File.Exists(fileName);
        }

        // 删除目录
        public static bool DeleteDirectory(string dirName)
        {
            if (Directory.Exists(dirName))
            {
                Directory.Delete(dirName, true); // true 递归删除
                return true;
            }
            return false;
        }

        // 写入日志文件（线程安全）
        public static void WriteLog(string filePath, string content, bool includeHeader = false, string header = "")
        {
            lock (logLock)
            {
                try
                {
                    if (!File.Exists(filePath) && includeHeader)
                    {
                        File.AppendAllText(filePath, header + Environment.NewLine, Encoding.UTF8);
                    }
                    File.AppendAllText(filePath, content + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing log to {filePath}: {ex.Message}");
                }
            }
        }

        // 获取当前时间字符串
        public static string GetCurrentTimeAsString()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmss");
        }
    }

    public static class IniFileUtils
    {
        // 读取INI文件
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);

        // 写入INI文件
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);

        public static string Read(string section, string key, string defaultValue, string filePath)
        {
            var result = new StringBuilder(1024);
            GetPrivateProfileString(section, key, defaultValue, result, (uint)result.Capacity, filePath);
            return result.ToString();
        }

        public static void Write(string section, string key, string value, string filePath)
        {
            if (!WritePrivateProfileString(section, key, value, filePath))
            {
                throw new Exception($"Failed to write INI file: {filePath}");
            }
        }
    }

    public class JackSaveLog
    {
        private static JackSaveLog _instance;
        private static readonly object lockObj = new object();

        // 单例模式
        public static JackSaveLog Instance()
        {
            if (_instance == null)
            {
                lock (lockObj)
                {
                    if (_instance == null)
                    {
                        _instance = new JackSaveLog();
                    }
                }
            }
            return _instance;
        }

        // 日志记录
        public void Log(string message)
        {
            string filePath = Path.Combine(FileUtils.GetCurrentDirectory(), "Logs", "ErrorLog.csv");
            FileUtils.CreateDirectoryIfNotExists(Path.GetDirectoryName(filePath));
            FileUtils.WriteLog(filePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}", false);
        }

        public void ErrorLog(string message)
        {
            string filePath = Path.Combine(FileUtils.GetCurrentDirectory(), "Logs", $"Error_{DateTime.Now:yyyyMMdd}.csv");
            FileUtils.CreateDirectoryIfNotExists(Path.GetDirectoryName(filePath));
            FileUtils.WriteLog(filePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}", false);
        }

        public void ProcedureLog(string message)
        {
            string filePath = Path.Combine(FileUtils.GetCurrentDirectory(), "Logs", $"Procedure_{DateTime.Now:yyyyMMdd}.csv");
            FileUtils.CreateDirectoryIfNotExists(Path.GetDirectoryName(filePath));
            FileUtils.WriteLog(filePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}", false, "Time, Description");
        }

        public void DataLog(string path, string title, string content)
        {
            if (!FileUtils.FileExists(path))
            {
                FileUtils.CreateDirectoryIfNotExists(Path.GetDirectoryName(path));
                FileUtils.WriteLog(path, content, true, title);
            }
            else
            {
                FileUtils.WriteLog(path, content);
            }
        }

        public string GetCurrentTimeString()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }
    }
}
