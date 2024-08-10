using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

namespace PalletCheck
{
    internal class Logger
    {
        private static string m_OutputRootDirectory = null;
        private static string m_ExceptionsRootDirectory = null;
        public static Thread m_InboxThread = null;
        public static bool m_KillInboxThread = false;
        public static List<string> m_InboxString = new List<string>();
        public static List<DateTime> m_InboxTimestamp = new List<DateTime>();
        public static DateTime m_LastLoggedDT = DateTime.Now;

        public Logger()
        {
            Trace.WriteLine("Logger()");
        }


        public void Startup(string OutputRootDirectory)
        {
            m_OutputRootDirectory = OutputRootDirectory;
            m_ExceptionsRootDirectory = OutputRootDirectory + "\\Exceptions";
            System.IO.Directory.CreateDirectory(m_OutputRootDirectory);
            System.IO.Directory.CreateDirectory(m_ExceptionsRootDirectory);

            // Start thread
            if (m_InboxThread == null)
            {
                m_KillInboxThread = false;
                m_InboxThread = new Thread(new ThreadStart(LoggerInboxThreadFunc));
                m_InboxThread.Start();
            }
        }


        public void Shutdown()
        {
            if (m_InboxThread != null)
            {
                if (m_InboxString.Count > 0)
                    Thread.Sleep(1000);

                m_KillInboxThread = true;
                int Countdown = 100;
                while (m_InboxThread.ThreadState != System.Threading.ThreadState.Stopped)
                {
                    Thread.Sleep(50);
                    Countdown--;
                    if (Countdown == 0)
                        break;
                }
                m_InboxThread = null;
            }
        }



        private static void LoggerInboxThreadFunc()
        {
            string PrevFilename = "";
            DateTime PrevDT = DateTime.Now;

            Trace.WriteLine("STARTING LoggerInboxThreadFunc");
            while (!m_KillInboxThread)
            {
                // Is there something to write?
                if (m_InboxString.Count > 0)
                {
                    while ((m_InboxString.Count > 0) && (!m_KillInboxThread))
                    {
                        string InboxStr = null;
                        DateTime InboxTimestamp = DateTime.Now;
                        lock (m_InboxString)
                        {
                            if (m_InboxString.Count > 0)
                            {
                                InboxStr = m_InboxString[0];
                                m_InboxString.RemoveAt(0);

                                InboxTimestamp = m_InboxTimestamp[0];
                                m_InboxTimestamp.RemoveAt(0);
                            }
                        }

                        if (InboxStr != null)
                        {
                            m_LastLoggedDT = InboxTimestamp;

                            // Build final string
                            string TimeStr = String.Format("|{0:0000}/{1:00}/{2:00} {3:00}:{4:00}:{5:00}:{6:000}|",
                                             InboxTimestamp.Year, InboxTimestamp.Month, InboxTimestamp.Day, InboxTimestamp.Hour, InboxTimestamp.Minute, InboxTimestamp.Second, InboxTimestamp.Millisecond);
                            string BlankTimeStr = "|                       |";

                            // Break up string into individual lines
                            int I;
                            while ((I = InboxStr.IndexOf('\r')) != -1)
                                InboxStr = InboxStr.Remove(I, 1);

                            string[] Lines = InboxStr.Split('\n');
                            Lines[0] = TimeStr + "  " + Lines[0];
                            for (int i = 1; i < Lines.Length; i++)
                                Lines[i] = BlankTimeStr + "  " + Lines[i];

                            string LogFilename = PrevFilename;
                            if (PrevFilename == "" || (PrevDT.Hour != InboxTimestamp.Hour))
                            {
                                LogFilename = m_OutputRootDirectory + String.Format("\\{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}_LOG_{6}.txt",
                                    InboxTimestamp.Year, InboxTimestamp.Month, InboxTimestamp.Day, InboxTimestamp.Hour, InboxTimestamp.Minute, InboxTimestamp.Second,
                                    ((PrevFilename == "") ? ("STARTUP") : ("CONTINUE")));

                                PrevFilename = LogFilename;
                                PrevDT = InboxTimestamp;
                            }

                            foreach (string Line in Lines)
                            {
                                System.IO.File.AppendAllText(LogFilename, Line + "\r\n");
                                Trace.WriteLine(Line);
                            }
                        }
                    }
                }
                Thread.Sleep(100);
            }
        }

        //private delegate void delLogWriteLine(string[] Strs);
        //public void InternalWriteLine(string[] Strs)
        //{
        //    foreach (string STR in Strs)
        //    {
        //        string Str = STR;
        //        Trace.WriteLine(Str);

        //        if (Str.Length > 200)
        //        {
        //            Str = Str.Substring(0, 70) + "  <===>  " + Str.Substring(Str.Length - 35, 35);
        //        }
        //        lbxLogging.Items.Add(Str);
        //        lbxLogging.SelectedIndex = lbxLogging.Items.Count - 1;

        //        while (lbxLogging.Items.Count > m_MaxLines)
        //            lbxLogging.Items.RemoveAt(0);
        //    }
        //}


        //public static void SetOutputDirectory(string Str)
        //{
        //    if (System.IO.Directory.Exists(Str) == false)
        //    {
        //        System.IO.Directory.CreateDirectory(Str);
        //    }

        //    OutputDirectory = Str;
        //}


        public static void WriteLine(string Str)
        {
            DateTime DT = DateTime.Now;
            lock (m_InboxString)
            {
                m_InboxTimestamp.Add(DT);
                m_InboxString.Add(Str);
            }
        }

        public static void ButtonPressed(string Str)
        {
            Logger.WriteLine("BUTTON PRESSED: " + Str);
        }

        public static void WriteHeartbeat()
        {
            DateTime Now = DateTime.Now;
            if ((Now - m_LastLoggedDT).TotalSeconds > 15)
                WriteLine("...still alive...");
        }

        public static void WriteBorder(string Str)
        {
            string strBorder = "======================================================================";

            int SideLen1 = (strBorder.Length - Str.Length) / 2;
            int SideLen2 = strBorder.Length - Str.Length - SideLen1;
            if (SideLen1 < 4)
            {
                //lock( m_lockobject )
                {
                    WriteLine("");
                    WriteLine(strBorder);
                    WriteLine(Str);
                    WriteLine(strBorder);
                    WriteLine("");
                }
            }
            else
            {
                int SpaceGap1 = SideLen1 - 3;
                int SpaceGap2 = SideLen2 - 3;

                string strSpace1 = "";
                for (int i = 0; i < SpaceGap1; i++)
                    strSpace1 += " ";

                string strSpace2 = "";
                for (int i = 0; i < SpaceGap2; i++)
                    strSpace2 += " ";

                //lock( m_lockobject )
                {
                    WriteLine("");
                    WriteLine(strBorder);
                    WriteLine("===" + strSpace1 + Str + strSpace2 + "===");
                    WriteLine(strBorder);
                    WriteLine("");
                }
            }
        }

        public static void WriteGap()
        {
            WriteLine("");
            WriteLine("");
        }

        public static void WriteException(Exception exp)
        {
            WriteBorder("!!! EXCEPTION HAS OCCURRED !!!");

            Exception exp1 = exp;
            while (exp1 != null)
            {
                WriteLine("\n\n");
                WriteLine("Message:\n" + exp1.Message);
                WriteLine("Source:\n" + exp1.Source);
                WriteLine("StackTrace:\n" + exp1.StackTrace);
                WriteLine("TargetSite:\n" + exp1.TargetSite);

                Trace.WriteLine("\n\n");
                Trace.WriteLine("Message:\n" + exp1.Message);
                Trace.WriteLine("Source:\n" + exp1.Source);
                Trace.WriteLine("StackTrace:\n" + exp1.StackTrace);
                Trace.WriteLine("TargetSite:\n" + exp1.TargetSite);

                exp1 = exp1.InnerException;
            }

            // Save directly to a file on disk
            try
            {
                DateTime DT = DateTime.Now;
                string ExpFilename = m_ExceptionsRootDirectory + String.Format("\\{0:0000}{1:00}{2:00}_{3:00}{4:00}{5:00}_EXCEPTION.txt",
                                        DT.Year, DT.Month, DT.Day, DT.Hour, DT.Minute, DT.Second);
                Exception exp2 = exp;
                while(exp2 != null)
                {
                    System.IO.File.AppendAllText(ExpFilename, "\n\n");
                    System.IO.File.AppendAllText(ExpFilename, "Message:\n" + exp2.Message);
                    System.IO.File.AppendAllText(ExpFilename, "Source:\n" + exp2.Source);
                    System.IO.File.AppendAllText(ExpFilename, "StackTrace:\n" + exp2.StackTrace);
                    System.IO.File.AppendAllText(ExpFilename, "TargetSite:\n" + exp2.TargetSite);

                    exp2 = exp2.InnerException;
                }
            }
            catch( Exception ) { }

        }
    }
}
