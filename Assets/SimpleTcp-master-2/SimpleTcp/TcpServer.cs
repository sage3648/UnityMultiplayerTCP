﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleTcp
{
    /// <summary>
    /// TCP server with SSL support.  
    /// Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  
    /// Once set, use Start() to begin listening for connections.
    /// </summary>
    public class TcpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Callback to call when a client connects.  A string containing the client IP:port will be passed.
        /// </summary>
        public Func<string, Task> ClientConnected = null;

        /// <summary>
        /// Callback to call when a client disconnects.  A string containing the client IP:port will be passed.
        /// </summary>
        public Func<string, DisconnectReason, Task> ClientDisconnected = null;

        /// <summary>
        /// Callback to call when byte data has become available from the client.  A string containing the client IP:port and a byte array containing the data will be passed.
        /// </summary>
        public Func<string, byte[], Task> DataReceived = null;

        /// <summary>
        /// Receive buffer size to use while reading from connected TCP clients.
        /// </summary>
        public int ReceiveBufferSize
        {
            get
            {
                return _ReceiveBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ReceiveBuffer must be one or greater.");
                if (value > 65536) throw new ArgumentException("ReceiveBuffer must be less than 65,536.");
                _ReceiveBufferSize = value;
            }
        }

        /// <summary>
        /// Maximum amount of time to wait before considering a client idle and disconnecting them. 
        /// By default, this value is set to 0, which will never disconnect a client due to inactivity.
        /// The timeout is reset any time a message is received from a client or a message is sent to a client.
        /// For instance, if you set this value to 30, the client will be disconnected if the server has not received a message from the client within 30 seconds or if a message has not been sent to the client in 30 seconds.
        /// </summary>
        public int IdleClientTimeoutSeconds
        {
            get
            {
                return _IdleClientTimeoutSeconds;
            }
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutSeconds must be zero or greater.");
                _IdleClientTimeoutSeconds = value;
            }
        }

        /// <summary>
        /// Enable or disable logging to the console.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Enable or disable acceptance of invalid SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates = true;

        /// <summary>
        /// Enable or disable mutual authentication of SSL client and server.
        /// </summary>
        public bool MutuallyAuthenticate = true;

        #endregion

        #region Private-Members

        private int _ReceiveBufferSize = 4096;
        private int _IdleClientTimeoutSeconds = 0;

        private string _ListenerIp;
        private IPAddress _IPAddress;
        private int _Port;
        private bool _Ssl;
        private string _PfxCertFilename;
        private string _PfxPassword;

        private X509Certificate2 _SslCertificate = null;
        private X509Certificate2Collection _SslCertificateCollection = null;

        private ConcurrentDictionary<string, ClientMetadata> _Clients = new ConcurrentDictionary<string, ClientMetadata>();
        private ConcurrentDictionary<string, DateTime> _ClientsLastSeen = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, DateTime> _ClientsKicked = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, DateTime> _ClientsTimedout = new ConcurrentDictionary<string, DateTime>();

        private TcpListener _Listener;
        private bool _Running;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP server.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public TcpServer(string listenerIp, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (String.IsNullOrEmpty(listenerIp)) throw new ArgumentNullException(nameof(listenerIp));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
             
            _ListenerIp = listenerIp;
            _IPAddress = IPAddress.Parse(_ListenerIp);
            _Port = port;
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
            _Running = false;  
            _Token = _TokenSource.Token;

            Debug = false;
             
            if (_Ssl)
            {
                if (String.IsNullOrEmpty(pfxPassword))
                {
                    _SslCertificate = new X509Certificate2(pfxCertFilename);
                }
                else
                {
                    _SslCertificate = new X509Certificate2(pfxCertFilename, pfxPassword);
                }

                _SslCertificateCollection = new X509Certificate2Collection
                {
                    _SslCertificate
                };
            }

            Task.Run(() => MonitorForIdleClients(), _Token);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the TCP server.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_Clients != null && _Clients.Count > 0)
                {
                    foreach (KeyValuePair<string, ClientMetadata> curr in _Clients)
                    {
                        curr.Value.Dispose();
                        Log("Disconnected client " + curr.Key);
                    }
                }

                _TokenSource.Cancel();
                _TokenSource.Dispose();

                if (_Listener != null && _Listener.Server != null)
                {
                    _Listener.Server.Close();
                    _Listener.Server.Dispose();
                }

                if (_Listener != null)
                {
                    _Listener.Stop();
                }
            }
            catch (Exception e)
            {
                Log(Environment.NewLine +
                    "Dispose exception:" +
                    Environment.NewLine +
                    e.ToString() +
                    Environment.NewLine);
            }
        }

        /// <summary>
        /// Start the TCP server and begin accepting connections.
        /// </summary>
        public void Start()
        {
            if (_Running) throw new InvalidOperationException("TcpServer is already running.");

            _Listener = new TcpListener(_IPAddress, _Port);
            _Listener.Start();

            _Clients = new ConcurrentDictionary<string, ClientMetadata>();

            Task.Run(() => AcceptConnections(), _Token);
        }

        /// <summary>
        /// Retrieve a list of client IP:port connected to the server.
        /// </summary>
        /// <returns>IEnumerable of strings, each containing client IP:port.</returns>
        public IEnumerable<string> GetClients()
        {
            List<string> clients = new List<string>(_Clients.Keys);
            return clients;
        }

        /// <summary>
        /// Determines if a client is connected by its IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <returns>True if connected.</returns>
        public bool IsConnected(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ClientMetadata client = null;
            return (_Clients.TryGetValue(ipPort, out client));
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(string ipPort, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            ClientMetadata client = null;
            if (!_Clients.TryGetValue(ipPort, out client)) return;
            if (client == null) return;

            lock (client.SendLock)
            {
                if (!_Ssl)
                {
                    client.NetworkStream.Write(data, 0, data.Length);
                    client.NetworkStream.Flush();
                }
                else
                {
                    client.SslStream.Write(data, 0, data.Length);
                    client.SslStream.Flush();
                }
            } 
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">String containing data to send.</param>
        public void Send(string ipPort, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            Send(ipPort, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Disconnects the specified client.
        /// </summary>
        /// <param name="ipPort">IP:port of the client.</param>
        public void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            if (!_Clients.TryGetValue(ipPort, out ClientMetadata client))
            {
                Log("*** DisconnectClient unable to find client " + ipPort);
            }
            else
            {
                if (!_ClientsTimedout.ContainsKey(ipPort))
                {
                    Log("[" + ipPort + "] kicking");
                    _ClientsKicked.TryAdd(ipPort, DateTime.Now);
                }

                _Clients.TryRemove(client.IpPort, out ClientMetadata destroyed);
                client.Dispose();
                Log("[" + ipPort + "] disposed");
            }
        }

        #endregion

        #region Private-Methods
         
        private void Log(string msg)
        {
            if (Debug) Console.WriteLine(msg);
        }

        private bool IsClientConnected(System.Net.Sockets.TcpClient client)
        {
            if (client.Connected)
            {
                if ((client.Client.Poll(0, SelectMode.SelectWrite)) && (!client.Client.Poll(0, SelectMode.SelectError)))
                {
                    byte[] buffer = new byte[1];
                    if (client.Client.Receive(buffer, SocketFlags.Peek) == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            } 
        }

        private async void AcceptConnections()
        {
            while (!_Token.IsCancellationRequested)
            {
                ClientMetadata client = null;

                try
                {
                    System.Net.Sockets.TcpClient tcpClient = await _Listener.AcceptTcpClientAsync(); 
                    string clientIp = tcpClient.Client.RemoteEndPoint.ToString();

                    client = new ClientMetadata(tcpClient);

                    if (_Ssl)
                    {
                        if (AcceptInvalidCertificates)
                        { 
                            client.SslStream = new SslStream(client.NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                        }
                        else
                        { 
                            client.SslStream = new SslStream(client.NetworkStream, false);
                        }

                        bool success = await StartTls(client);
                        if (!success)
                        {
                            client.Dispose();
                            continue;
                        }
                    }

                    _Clients.TryAdd(clientIp, client); 
                    _ClientsLastSeen.TryAdd(clientIp, DateTime.Now); 

                    Log("[" + clientIp + "] starting data receiver");

                    if (ClientConnected != null)
                        await Task.Run(() => ClientConnected(clientIp));

                    Task dataRecv = Task.Run(() => DataReceiver(client), _Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (client != null) client.Dispose();
                    continue;
                }
                catch (Exception e)
                {
                    if (client != null) client.Dispose();
                    Log("*** AcceptConnections exception: " + e.ToString());
                    continue;
                }
                finally
                { 
                }
            }
        }

        private async Task<bool> StartTls(ClientMetadata client)
        {
            try
            { 
                await client.SslStream.AuthenticateAsServerAsync(
                    _SslCertificate, 
                    MutuallyAuthenticate, 
                    SslProtocols.Tls12, 
                    !AcceptInvalidCertificates);

                if (!client.SslStream.IsEncrypted)
                {
                    Log("[" + client.IpPort + "] not encrypted");
                    client.Dispose();
                    return false;
                }

                if (!client.SslStream.IsAuthenticated)
                {
                    Log("[" + client.IpPort + "] stream not authenticated");
                    client.Dispose();
                    return false;
                }

                if (MutuallyAuthenticate && !client.SslStream.IsMutuallyAuthenticated)
                {
                    Log("[" + client.IpPort + "] failed mutual authentication");
                    client.Dispose();
                    return false;
                }
            }
            catch (Exception e)
            {
                Log("[" + client.IpPort + "] TLS exception" + Environment.NewLine + e.ToString());
                client.Dispose();
                return false;
            }

            return true;
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // return true; // Allow untrusted certificates.
            return AcceptInvalidCertificates;
        }

        private async Task DataReceiver(ClientMetadata client)
        {
            string header = "[" + client.IpPort + "]";
            Log(header + " data receiver started");
            
            while (true)
            {
                try
                { 
                    if (!IsClientConnected(client.Client))
                    {
                        Log(header + " client no longer connected");
                        break;
                    }

                    if (client.Token.IsCancellationRequested)
                    {
                        Log(header + " cancellation requested");
                        break;
                    } 

                    byte[] data = await DataReadAsync(client);
                    if (data == null)
                    { 
                        await Task.Delay(30);
                        continue;
                    }

                    if (DataReceived != null)
                    {
                        Task unawaited = Task.Run(() => DataReceived(client.IpPort, data));
                    }

                    UpdateClientLastSeen(client.IpPort);
                }
                catch (Exception e)
                {
                    Log(
                        Environment.NewLine +
                        header + " data receiver exception:" +
                        Environment.NewLine +
                        e.ToString() +
                        Environment.NewLine);

                    break;
                }
            }  

            Log(header + " data receiver terminated");

            if (ClientDisconnected != null)
            {
                Task unawaited = null;

                if (_ClientsKicked.ContainsKey(client.IpPort))
                {
                    unawaited = Task.Run(() => ClientDisconnected(client.IpPort, DisconnectReason.Kicked));
                }
                else if (_ClientsTimedout.ContainsKey(client.IpPort))
                {
                    unawaited = Task.Run(() => ClientDisconnected(client.IpPort, DisconnectReason.Timeout));
                }
                else
                {
                    unawaited = Task.Run(() => ClientDisconnected(client.IpPort, DisconnectReason.Normal));
                }
            }

            DateTime removedTs;
            _Clients.TryRemove(client.IpPort, out ClientMetadata destroyed);
            _ClientsLastSeen.TryRemove(client.IpPort, out removedTs);
            _ClientsKicked.TryRemove(client.IpPort, out removedTs);
            _ClientsTimedout.TryRemove(client.IpPort, out removedTs);

            client.Dispose();
        }
           
        private async Task<byte[]> DataReadAsync(ClientMetadata client)
        { 
            if (client.Token.IsCancellationRequested) throw new OperationCanceledException();
            if (!client.NetworkStream.CanRead) return null;
            if (!client.NetworkStream.DataAvailable) return null;
            if (_Ssl && !client.SslStream.CanRead) return null;

            byte[] buffer = new byte[_ReceiveBufferSize];
            int read = 0;

            if (!_Ssl)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        read = await client.NetworkStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read > 0)
                        {
                            ms.Write(buffer, 0, read);
                            return ms.ToArray();
                        }
                        else
                        {
                            throw new SocketException();
                        }
                    }
                }
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        read = await client.SslStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read > 0)
                        {
                            ms.Write(buffer, 0, read);
                            return ms.ToArray();
                        }
                        else
                        {
                            throw new SocketException();
                        }
                    }
                }
            } 
        }

        private async Task MonitorForIdleClients()
        {
            while (!_Token.IsCancellationRequested)
            {
                if (_IdleClientTimeoutSeconds > 0 && _ClientsLastSeen.Count > 0)
                {
                    MonitorForIdleClientsTask();
                }
                await Task.Delay(5000, _Token);
            }
        }

        private void MonitorForIdleClientsTask()
        {
            DateTime idleTimestamp = DateTime.Now.AddSeconds(-1 * _IdleClientTimeoutSeconds);

            foreach (KeyValuePair<string, DateTime> curr in _ClientsLastSeen)
            {
                if (curr.Value < idleTimestamp)
                {
                    _ClientsTimedout.TryAdd(curr.Key, DateTime.Now);
                    Log("Disconnecting client " + curr.Key + " due to idle timeout");
                    DisconnectClient(curr.Key);
                }
            }
        }

        private void UpdateClientLastSeen(string ipPort)
        {
            if (_ClientsLastSeen.ContainsKey(ipPort))
            {
                DateTime ts;
                _ClientsLastSeen.TryRemove(ipPort, out ts);
            }

            _ClientsLastSeen.TryAdd(ipPort, DateTime.Now);
        }

        #endregion
    }
}
