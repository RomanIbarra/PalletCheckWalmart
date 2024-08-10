using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace PalletCheck
{
    public class StorageWatchdog
    {
        public Thread WatchdogThread = null;
        public bool KillThread = false;

        public StorageWatchdog()
        {
        }

        public void WatchdogThreadFunc()
        {
            string RootDir = MainWindow.RecordingRootDir;


            while (!KillThread)
            {
                Thread.Sleep(5000);

                int MaxDirs = ParamStorage.GetInt("Max Recording Directories");

                try
                {
                    string[] folders = System.IO.Directory.GetDirectories(RootDir, "*", System.IO.SearchOption.AllDirectories);
                    Array.Sort(folders);

                    if (folders.Length > MaxDirs)
                    {
                        string DelFolder = folders[0];
                        Logger.WriteLine("StorageWatchdog - Removing:" + DelFolder);
                        Directory.Delete(DelFolder, true);
                        Thread.Sleep(10000);
                    }
                }
                catch (Exception)
                { }

                //for(int i=0; i<folders.Length; i++)
                //{
                //    Logger.WriteLine("FOLDER: " + i.ToString() + "  " + folders[i]);
                //}
            }
            //Listener.Start();

            //while(!KillThread)
            //{
            //    Socket s = Listener.AcceptSocket();
            //    Logger.WriteLine("ACCEPTING NEW CLIENT CONNECTION!");
            //    Clients.Add(s);
            //    Logger.WriteLine("NUM CLIENTS:" + Clients.Count.ToString());
            //}

            //Listener.Stop();
        }

        public void Start()
        {
            Logger.WriteLine("Starting Storage Watchdog");

            if (WatchdogThread == null)
            {
                KillThread = false;
                WatchdogThread = new Thread(new ThreadStart(WatchdogThreadFunc));
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

    }
}
