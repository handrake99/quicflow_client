using System;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace QuicFlowClient.Services
{
    public class QuicClientService
    {
        private QuicConnection? _connection;
        private QuicStream? _chatStream;
        private CancellationTokenSource? _cts;

        public event Action<string>? OnMessageReceived;
        public event Action<string>? OnLog;
        public event Action? OnConnectionLost;

        public bool IsConnected => _connection != null;

        public async Task ConnectAsync(string host, int port)
        {
            try
            {
                string alpnProtocol = "quicflow";

                Log($"Connecting to {host}:{port}(ALPN: {alpnProtocol})...");
                
                Log($"Running on: {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})");
                Log($"DYLD_LIBRARY_PATH: {Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH")}");
                Log($"Checking QuicConnection.IsSupported...");

                if (!QuicConnection.IsSupported)
                {
                    Log("Error: QuicConnection.IsSupported returned false.");
                    Log("Possible causes: missing libmsquic, wrong architecture, or SSL issues.");
                    
                    // Try to debug library loading
                    try 
                    {
                        IntPtr handle = System.Runtime.InteropServices.NativeLibrary.Load("libmsquic");
                        Log($"Diagnostic: Successfully loaded libmsquic explicitly. Handle: {handle}");
                        System.Runtime.InteropServices.NativeLibrary.Free(handle);
                    }
                    catch (Exception libEx)
                    {
                        Log($"Diagnostic: Failed to load libmsquic explicitly. Error: {libEx.Message}");
                    }

                    return;
                }
                else 
                {
                    Log("QuicConnection.IsSupported returned true. MSQuic should be available.");
                }

                //var endPoint = new IPEndPoint(IPAddress.Parse(host), port);
                
                var clientConnectionOptions = new QuicClientConnectionOptions
                {
                    //RemoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port),
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port),
                    
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions
                    {
                        ApplicationProtocols = new List<SslApplicationProtocol> 
                        { 
                            new SslApplicationProtocol("h3"), // HTTP/3
                            new SslApplicationProtocol(alpnProtocol) 
                        },
                        //TargetHost = host,
                        // For testing purposes only, ignore certificate validation errors
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                    },
                    MaxInboundBidirectionalStreams = 10,
                    //DefaultStreamErrorCode = 0x101, // Protocol specific error code
                    //DefaultCloseErrorCode = 0x200 // Protocol specific error code
                    DefaultStreamErrorCode = 0, // Protocol specific error code
                    DefaultCloseErrorCode = 0 // Protocol specific error code
                };

                _connection = await QuicConnection.ConnectAsync(clientConnectionOptions);
                Log($"Connected to {host}:{port}");

                _cts = new CancellationTokenSource();
                
                // Open a bidirectional stream for chat
                _chatStream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, _cts.Token);
                Log("Chat stream opened.");

                // Start reading loop
                _ = ReadLoopAsync(_chatStream, _cts.Token);

                // Send Test Message
                var myMessage = new {
                    Type = "Login",
                    UserID = "User_1",
                    Message = "Hello I'm User_1",
                    Timestamp = DateTime.UtcNow
                };

                var jsonMessage = System.Text.Json.JsonSerializer.Serialize(myMessage);
                
                await SendMessageAsync(jsonMessage);
                Log($"Sent test message: {jsonMessage}");
            }
            catch (Exception ex)
            {
                Log($"Connection failed: {ex.Message}");
                Disconnect();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_chatStream == null)
            {
                Log("Error: Not connected or stream not open.");
                return;
            }

            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await _chatStream.WriteAsync(buffer, _cts?.Token ?? CancellationToken.None);
                // Depending on protocol framing, might need to send connection end or length prefix. 
                // For this simple test, we assume raw stream or handling by checking Read behavior.
                // However, TCP/QUIC is stream based. For a simple chat, usually we delimit messages.
                // Let's assume newline delimiter for this simple client.
                await _chatStream.WriteAsync(Encoding.UTF8.GetBytes("\n"), _cts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log($"Send failed: {ex.Message}");
                Disconnect();
            }
        }

        public async void Disconnect()
        {
            if (_connection != null)
            {
                try
                {
                    _cts?.Cancel();
                    if (_chatStream != null)
                    {
                        await _chatStream.DisposeAsync();
                    }
                    await _connection.DisposeAsync();
                    Log("Disconnected.");
                }
                catch (Exception ex)
                {
                    Log($"Error during disconnect: {ex.Message}");
                }
                finally
                {
                    _connection = null;
                    _chatStream = null;
                    _cts = null;
                    OnConnectionLost?.Invoke();
                }
            }
        }

        private async Task ReadLoopAsync(QuicStream stream, CancellationToken token)
        {
            try
            {
                byte[] buffer = new byte[1024];
                while (!token.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, token);
                    if (read == 0)
                    {
                        Log("Server closed the stream.");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, read);
                    OnMessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Log($"Read error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
