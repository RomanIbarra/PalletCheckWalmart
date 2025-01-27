using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



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
        public TcpClient Client;
        public Thread ListenerThread = null;
        public bool KillThread = false;
        public int ReconnectDelay = 5000; // Jack Note: Reconnection delay in milliseconds

        // Jack Note: Store the connected clients
        private List<TcpClient> connectedClients = new List<TcpClient>();

        public PLCComms(string ConnectIP, int ConnectPort, bool isServer)
        {
            Port = ConnectPort;
            IP = ConnectIP;
            IsServerMode = isServer;

            if (IsServerMode)
            {
                // Jack Note: Initialize as server
                Listener = new TcpListener(System.Net.IPAddress.Any, ConnectPort);
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
                ListenerThread.Start();
            }
        }

        // Jack Note: Server's listening thread
        public void ListenerThreadFunc()
        {
            Listener.Start();
            Logger.WriteLine("Server started, waiting for clients...");

            while (!KillThread)
            {
                if (Listener.Pending())
                {
                    // Jack Note: When a new client connects, accept the connection
                    TcpClient client = Listener.AcceptTcpClient();
                    Logger.WriteLine("Client connected.");

                    // Jack Note: Add the connected client to the list
                    lock (connectedClients)
                    {
                        connectedClients.Add(client);
                    }

                    // Jack Note: Handle each client's connection logic here
                    Task.Run(() => HandleClient(client)); // Jack Note: Asynchronously handle the client
                }
                Thread.Sleep(250);
            }

            // Jack Note: If the thread exits, we do need to stop the server and call Stop()
            Listener.Stop();
        }

        // Jack Note: Handle client connections
        private void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    string receivedMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Logger.WriteLine($"Received message from client: {receivedMessage}");

                    // Jack Note: Reply to the client
                    byte[] replyMessage = Encoding.ASCII.GetBytes("Message received");
                    stream.Write(replyMessage, 0, replyMessage.Length);
                }

                // Jack Note: Keep the connection with the client until they disconnect
                while (client.Connected)
                {
                    Thread.Sleep(1000); // Jack Note: Maintain a persistent connection
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
    }
}
