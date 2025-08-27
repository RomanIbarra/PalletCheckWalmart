using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Markup;

namespace PalletCheck
{
    /// <summary>
    /// This part of the PLC code was rewritten by Jack Hou to enable the software to function as both a server and a client.s 09/20/2024
    /// </summary>
    public class PLCComms
    {
        public bool IsServerMode;
        public bool Connected = false;
        public int Port;
        public string IP;
        public TcpListener Listener;
        public TcpListener CameraListener;
        public TcpClient Client;
        public Thread ListenerThread = null;
        public bool KillThread = false;
        public int ReconnectDelay = 5000; // Jack Note: Reconnection delay in milliseconds

        // Jack Note: Store the connected clients
        private List<TcpClient> connectedClients = new List<TcpClient>();

        public PLCComms(string ConnectIP, int ConnectPort)
        {
            this.IP = ConnectIP;
            this.Port = ConnectPort;
        }

        public PLCComms(string ConnectIP, int ConnectPort, bool isServer)
        {
            Port = ConnectPort;
            IP = ConnectIP;
            IsServerMode = isServer;

            if (IsServerMode)
            {
                // Jack Note: Initialize as server
                //Listener = new TcpListener(System.Net.IPAddress.Any, ConnectPort);
                Listener = new TcpListener(IPAddress.Parse(ConnectIP), ConnectPort);
            }
            else
            {
                // Jack Note: Initialize as client
                Client = new TcpClient();
            }
        }

        // Jack Note: Start communication (perform different actions depending on the mode)
        public void Start()
        {
            if (IsServerMode)
            {
                StartServer();
            }
            else
            {
                Task.Run(() => StartClientAsync()); // Jack Note: Asynchronously start the client
            }
        }

        // Jack Note: Start the server mode
        public void StartServer()
        {
            Logger.WriteLine($"Starting server at IP: {IP}, Port: {Port}");

            if (ListenerThread == null)
            {
                KillThread = false;
                ListenerThread = new Thread(new ThreadStart(ListenerThreadFunc));
                ListenerThread.Name = "Server Thread";
                ListenerThread.Start();
            }
        }

        // Server's listening thread
        public void ListenerThreadFunc()
        {
            Listener.Start();
            Logger.WriteLine("Server started, waiting for clients...");

            while (!KillThread)
            {
                if (Listener.Pending())
                {
                    // When a new client connects, accept the connection
                    TcpClient client = Listener.AcceptTcpClient();
                    Logger.WriteLine("Client connected.");

                    // Add the connected client to the list
                    lock (connectedClients)
                    {
                        connectedClients.Add(client);
                    }

                    // Handle each client's connection logic here
                    Task.Run(() => HandleClient(client)); // Asynchronously handle the client
                }
                Thread.Sleep(250);
            }

            // If the thread exits, we do need to stop the server and call Stop()
            Listener.Stop();
        }

        // Handle client connections
        private void HandleClient(TcpClient client)
        {
            try
            {
                string clientIP = client.Client.LocalEndPoint.ToString();
                TelegramStreamDecoder decoder = new TelegramStreamDecoder();

                // Keep the connection with the client
                while (client.Connected)
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        foreach (var receivedMessage in decoder.ProcessBytes(buffer, bytesRead))
                        {
                            // Extract values
                            string[] parts = receivedMessage.Split('_');
                            string cameraNumber = parts[0];
                            bool crackBool = parts[1] == "1"; // Set a boolean by comparison
                            string imageName = parts[2];

                            if (crackBool)
                            {
                                DateTime timeStamp = DateTime.Now;
                                Logger.WriteLine($"Received message from client {clientIP}: {receivedMessage}, at: {timeStamp}");

                                // If <ImageName> itself can contain underscores, join the rest back
                                if (parts.Length > 3) { imageName = string.Join("_", parts.Skip(2)); }

                                RemoteCameraCrackDetection detection = new RemoteCameraCrackDetection(cameraNumber, timeStamp, imageName);
                                MainWindow.remoteCameraCrackDetectionList.Add(detection);
                                MainWindow.tcpCrackExists = true;
                                StorageWatchdog watchdog = new StorageWatchdog();
                                watchdog.WatchFolder(imageName);
                            }
                        }

                        Thread.Sleep(1000); // Maintain a persistent connection
                    }

                    //Reply to the client
                    //byte[] replyMessage = Encoding.ASCII.GetBytes("Message received");
                    //stream.Write(replyMessage, 0, replyMessage.Length);
                }

                
            }
            catch (Exception e)
            {
                Logger.WriteLine($"Error handling client: {e.Message}");
            }
            finally
            {
                client.Close(); // Jack Note: Close the connection when the client disconnects

                // Jack Note: Remove the client from the connected clients list
                lock (connectedClients)
                {
                    connectedClients.Remove(client);
                }
            }
        }

        // Jack Note: Asynchronously start the client
        public async Task StartClientAsync()
        {
            while (!KillThread && !Connected)
            {
                try
                {
                    // Jack Note: Close existing connections (if any)
                    if (Client != null)
                    {
                        try
                        {
                            // Jack Note: Ensure it's connected and not closed
                            if (Client.Connected)
                            {
                                Logger.WriteLine("Closing existing connection before reconnecting...");
                            }

                            Client.Close();  // Jack Note: Safely close the connection
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine($"Error while closing the connection: {ex.Message}");
                        }
                        finally
                        {
                            Client = null; // Jack Note: Ensure the connection is released
                        }
                    }

                    // Jack Note: Create a new TcpClient instance
                    Client = new TcpClient();

                    Logger.WriteLine($"Connecting to server at IP: {IP}, Port: {Port}");
                    await Client.ConnectAsync(IP, Port); // Jack Note: Asynchronously connect
                    Connected = true;
                    Logger.WriteLine("Connected to server successfully.");
                }
                catch (OperationCanceledException oce)
                {
                    Logger.WriteLine($"Connection was canceled: {oce.Message}");
                    await Task.Delay(ReconnectDelay); // Jack Note: Wait before trying to reconnect
                }
                catch (SocketException se)
                {
                    Logger.WriteLine($"Socket error during connection: {se.Message}");
                    await Task.Delay(ReconnectDelay); // Jack Note: Network error, retry after a delay
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"Failed to connect to server: {e.Message}. Retrying in {ReconnectDelay / 1000} seconds...");
                    await Task.Delay(ReconnectDelay); // Jack Note: Wait for 5 seconds before retrying
                }
            }
        }

        // Jack Note: Send a message
        public void SendMessage(string message)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);

            if (IsServerMode)
            {
                Logger.WriteLine("Sending message from server.");

                // Jack Note: Send the message to all connected clients
                lock (connectedClients)
                {
                    foreach (var client in connectedClients)
                    {
                        try
                        {
                            NetworkStream stream = client.GetStream();
                            stream.Write(data, 0, data.Length);
                            Logger.WriteLine("Message sent to client.");
                        }
                        catch (Exception e)
                        {
                            Logger.WriteLine($"Error sending message to client: {e.Message}");
                        }
                    }
                }
            }
            else if (Connected)
            {
                // Jack Note: Client sending to the server
                try
                {
                    NetworkStream stream = Client.GetStream();
                    stream.Write(data, 0, data.Length);
                    Logger.WriteLine($"Message sent to server: {message}");
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"Error sending message to server: {e.Message}");
                    Connected = false;
                    Task.Run(() => ReconnectClientAsync()); // Jack Note: Asynchronously reconnect
                }
            }
        }

        // Jack Note: Asynchronously reconnect the client
        public async Task ReconnectClientAsync()
        {
            Logger.WriteLine("Attempting to reconnect to server...");
            Connected = false;
            await StartClientAsync(); // Jack Note: Attempt to reconnect
        }

        // Jack Note: Stop communication
        public void Stop()
        {
            if (IsServerMode)
            {
                Logger.WriteLine("Stopping server...");
                KillThread = true;

                // Jack Note: Stop the server, wait for the listening thread to finish before calling Stop()
                if (ListenerThread != null)
                {
                    ListenerThread.Join();  // Jack Note: Wait for the listening thread to end
                }

                if (Listener != null)
                {
                    Listener.Stop();  // Jack Note: Stop the server listening
                }

                // Jack Note: Close all connected clients
                lock (connectedClients)
                {
                    foreach (var client in connectedClients)
                    {
                        try
                        {
                            client.Close(); // Jack Note: Close the client connection
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine($"Error closing client: {ex.Message}");
                        }
                    }
                    connectedClients.Clear();  // Jack Note: Clear the client list
                }
            }
            else
            {
                Logger.WriteLine("Disconnecting from server...");
                Connected = false;

                // Jack Note: Close the client connection
                if (Client != null)
                {
                    try
                    {
                        if (Client.Connected)
                        {
                            Logger.WriteLine("Closing client connection...");
                            Client.Close(); // Jack Note: Safely close the client connection
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"Error closing client connection: {ex.Message}");
                    }
                    finally
                    {
                        Client = null; // Jack Note: Ensure the resources are released
                    }
                }
                else
                {
                    Logger.WriteLine("Client was not connected, no need to close.");
                }
            }

            Logger.WriteLine("Software closed successfully.");
        }

        public void Listen()
        {
            Logger.WriteLine($"Starting TCP server for camera at IP: {IP}, Port: {Port}");
            CameraListener = new TcpListener(IPAddress.Parse(IP), this.Port);
            Thread TCPThread = new Thread(new ThreadStart(CameraListenerThreadFunc));
            TCPThread.Start();
        }

        public void CameraListenerThreadFunc()
        {
            CameraListener.Start();
            Logger.WriteLine("Server started, waiting for clients...");

            while (!KillThread)
            {
                if (CameraListener.Pending())
                {
                    // When a new client connects, accept the connection
                    TcpClient client = CameraListener.AcceptTcpClient();
                    Logger.WriteLine("Client connected to CameraListener");

                    // Add the connected client to the list
                    lock (connectedClients)
                    {
                        connectedClients.Add(client);
                    }

                    // Handle each client's connection logic here
                    Task.Run(() => HandleClient(client)); // Asynchronously handle the client
                }
                Thread.Sleep(250);
            }

            // If the thread exits, we do need to stop the server and call Stop()
            Listener.Stop();
        }
    }

    public sealed class ButtonChanger
    {
        // --- Singleton instance (lazy-loaded, thread-safe by CLR guarantees) ---
        private static readonly ButtonChanger _instance = new ButtonChanger();
        public static ButtonChanger Instance => _instance;

        // Private constructor to enforce singleton
        private ButtonChanger() { }

        // Event for UI subscribers
        public event Action<Brush> BackgroundChanged;
        public event Action<bool> ButtonEnableChanged;

        // Thread-safe trigger: back to UI thread
        public void ChangeBackground(Brush brush)
        {
            var app = Application.Current;
            if (app != null)
            {
                // Ensure raise happens on the UI thread
                app.Dispatcher.Invoke(() =>
                {
                    BackgroundChanged?.Invoke(brush);
                });
            }
        }

        public void EnableButton(bool isEnable)
        {
            var app = Application.Current;
            if (app != null)
            {
                // Ensure raise happens on the UI thread
                app.Dispatcher.Invoke(() =>
                {
                    ButtonEnableChanged?.Invoke(isEnable);
                });
            }
        }
    }

    public sealed class RemoteCameraCrackDetection
    {
        public DateTime timeStamp;
        public PalletDefect.DefectLocation location;
        public string imageName;

        public RemoteCameraCrackDetection(string cameraNumber, DateTime timeStamp, string imageName)
        {
            this.timeStamp = timeStamp;
            this.location = (PalletDefect.DefectLocation)Enum.Parse(typeof(PalletDefect.DefectLocation), GetLocation(cameraNumber));
            this.imageName = imageName;
        }

        private string GetLocation(string cameraNumber) // Get the location based on the port number: specific port for each camera
        {
            switch (cameraNumber)
            {
                case "Cam0": return "SP_V1";
                case "Cam1": return "SP_V2";
                case "Cam2": return "SP_V3";
                default: return "?";
            }
        }
    }

    public class TelegramStreamDecoder
    {
        private bool inTelegram = false;
        private List<byte> currentTelegram = new List<byte>();

        public IEnumerable<string> ProcessBytes(byte[] buffer, int count) // Split the teelgrams by looking fot STX and ETX
        {
            for (int i = 0; i < count; i++)
            {
                byte b = buffer[i];

                if (b == 0x02) // STX
                {
                    inTelegram = true;
                    currentTelegram.Clear();
                }
                else if (b == 0x03 && inTelegram) // ETX
                {
                    // End of telegram → emit result
                    string telegram = Encoding.UTF8.GetString(currentTelegram.ToArray());
                    yield return telegram;

                    inTelegram = false;
                    currentTelegram.Clear();
                }
                else if (inTelegram)
                {
                    currentTelegram.Add(b);
                }
            }
        }
    }
}
