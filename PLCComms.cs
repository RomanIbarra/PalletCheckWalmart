using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace PalletCheck
{
    public class PLCComms
    {
        //static Socket _sock;

        public bool Connected = false;
        public int Port;
        public string IP;
        public IPAddress IPAddress;
        public TcpListener Listener;
        public Thread ListenerThread = null;
        public bool KillThread = false;
        public List<Socket> Clients = new List<Socket>();

        public PLCComms(string ConnectIP, int ConnectPort)
        {
            Port = ConnectPort;
            IP = ConnectIP;
            IPAddress = IPAddress.Parse(ConnectIP);
            Listener = new TcpListener(IPAddress, Port);
        }

        public void ListenerThreadFunc()
        {
            Listener.Start();

            while(!KillThread)
            {
                if (Listener.Pending())
                {
                    Socket s = Listener.AcceptSocket();
                    Logger.WriteLine("ACCEPTING NEW CLIENT CONNECTION!");
                    Clients.Add(s);
                    Logger.WriteLine("NUM CLIENTS:" + Clients.Count.ToString());
                }
                else
                {
                    Thread.Sleep(250);
                }
            }

            Listener.Stop();

            // Close all clients
            for (int i = 0; i < Clients.Count; i++)
            {
                try
                {
                    Clients[i].Close();
                }
                catch (Exception)
                {

                }
            }
        }

        public void Start()
        {
            Logger.WriteLine(string.Format("PLCComms::Start()  IP:{0}  Port:{1}", IPAddress, Port));

            if (ListenerThread == null)
            {
                KillThread = false;
                ListenerThread = new Thread(new ThreadStart(ListenerThreadFunc));
                ListenerThread.Start();
            }
        }

        public void Stop()
        {
            Logger.WriteLine(string.Format("PLCComms::Stop()  IP:{0}  Port:{1}", IPAddress, Port));
            if (ListenerThread != null)
            {
                KillThread = true;
            }
        }

        public void SendMessage(string values)
        {
            Logger.WriteLine("PLC ==> |   " + values + "   |");
            Logger.WriteLine("NUM CLIENTS:" + Clients.Count.ToString());

            byte[] info = Encoding.ASCII.GetBytes(values);
            for (int i=0; i< Clients.Count; i++)
            {
                try
                {
                    Logger.WriteLine("Sending to client " + i.ToString());
                    Clients[i].Send(info);
                }
                catch(Exception)
                {

                }
            }
            //if (Connected)
            //{
            //    byte[] info = Encoding.ASCII.GetBytes(values);
            //    _sock.Send(info);
            //}
            //else
            //    System.Console.WriteLine(values);
        }


    }
}
