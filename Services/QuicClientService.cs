using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using QuicFlowClient.Models;

namespace QuicFlowClient.Services
{
    public class QuicClientService
    {
        // 보안을 위한 최대 메시지 크기 제한 (예: 1MB)
        // C++ 서버의 MAX_MESSAGE_SIZE와 맞춰주세요.
        private const int MAX_MESSAGE_SIZE = 1024 * 1024;
            
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
                
                await SendMessageAsync("Hello I'm User_1");

                // Start reading loop
                _ = ReadLoopAsync(_chatStream, _cts.Token);

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
            
            // Send Test Message
            var myMessage = new ChatData("chat", 0, "User_1", message); 
            try
            {
                var jsonMessage = JsonSerializer.Serialize(myMessage);
                
                await SendMessageAsync(_chatStream, jsonMessage);
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
                while (true)
                {
                    try 
                    {
                        string? msg = await ReadMessageAsync(stream);
        
                        if (msg == null) 
                        {
                            Console.WriteLine("Server disconnected.");
                            break; 
                        }

                        Console.WriteLine($"[Recv] {msg}");
        
                        // 받은 메시지가 JSON이라면 여기서 파싱
                        OnMessage(msg);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        break;
                    }
                    
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

        private void OnMessage(string jsonMessage)
        {
            if (jsonMessage.Length < 4)
            {
                // not enough data
                return;
            }
            
            var chatData = JsonSerializer.Deserialize<ChatData>(jsonMessage);
            OnMessageReceived?.Invoke(chatData.Message);
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
        
    /// <summary>
    /// [쓰기] 메시지를 길이(4byte) + 본문 형태로 변환하여 전송합니다.
    /// </summary>
    private static async Task SendMessageAsync(QuicStream stream, string message)
    {
        // 1. 문자열을 바이트로 변환
        byte[] bodyBytes = Encoding.UTF8.GetBytes(message);
        int bodyLength = bodyBytes.Length;

        // 2. 헤더(길이) 생성 (4바이트, Little Endian)
        byte[] headerBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(headerBytes, bodyLength);

        byte[] combined = [..headerBytes, ..bodyBytes];

        // 3. 전송 (헤더 -> 본문 순서)
        // 따로 보내도 되지만, 하나의 패킷으로 뭉쳐 보내는 것이 성능상 유리할 수 있음
        // 여기서는 명확성을 위해 순차 전송
        await stream.WriteAsync(combined);
        //await stream.WriteAsync(bodyBytes);
        
        // 필요하다면 즉시 전송 강제 (QUIC은 보통 알아서 잘 보냄)
        // await stream.FlushAsync(); 
        
        Console.WriteLine($"[Send] Length: {bodyLength} | Msg: {message}");
    }

    /// <summary>
    /// [읽기] 스트림에서 4바이트 길이를 먼저 읽고, 그만큼의 본문을 읽어 문자열로 반환합니다.
    /// </summary>
    /// <returns>읽은 메시지 (스트림이 끊겼으면 null 반환)</returns>
    public static async Task<string?> ReadMessageAsync(QuicStream stream)
    {
        // 1. 헤더 읽기 (4바이트)
        byte[] headerBuffer = new byte[4];
        bool headerReadSuccess = await ReadExactlyAsync(stream, headerBuffer, 4);
        
        if (!headerReadSuccess) return null; // 스트림이 닫힘

        // 2. 길이 파싱 (Little Endian)
        int bodyLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer);
        Console.WriteLine($"Read header from Stream : {bodyLength}");

        // [검증] 메시지 크기가 비정상적으로 크거나 음수인지 체크
        if (bodyLength < 0 || bodyLength > MAX_MESSAGE_SIZE)
        {
            throw new InvalidDataException($"Message size invalid or too large: {bodyLength}");
        }

        if (bodyLength == 0) return string.Empty; // 빈 메시지 처리

        // 3. 본문 읽기 (bodyLength 만큼)
        byte[] bodyBuffer = new byte[bodyLength];
        bool bodyReadSuccess = await ReadExactlyAsync(stream, bodyBuffer, bodyLength);

        if (!bodyReadSuccess)
        {
            // 헤더는 왔는데 본문이 오다가 끊긴 경우 (에러 처리)
            throw new EndOfStreamException("Stream closed while reading message body.");
        }

        // 4. 문자열 변환
        return Encoding.UTF8.GetString(bodyBuffer);
    }

    /// <summary>
    /// [핵심] 원하는 바이트 수만큼 꽉 채워서 읽을 때까지 반복하는 헬퍼 함수
    /// </summary>
    private static async Task<bool> ReadExactlyAsync(QuicStream stream, byte[] buffer, int count)
    {
        int totalBytesRead = 0;
        
        while (totalBytesRead < count)
        {
            // 남은 만큼만 읽기 시도
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalBytesRead, count - totalBytesRead));
            
            if (bytesRead == 0)
            {
                // 더 이상 읽을 데이터가 없음 (상대방이 연결 끊음)
                return false; 
            }
            
            totalBytesRead += bytesRead;
        }
        /*Console.WriteLine("[DEBUG] 🕵️ Raw Byte Inspection Started...");
        byte[] debugBuffer = new byte[1024]; // 넉넉하게 잡음

        try
        {
            while (true)
            {
                // 1. 읽기 시도 (크기 지정 없이 오는 대로 다 받음)
                int bytesRead = await stream.ReadAsync(debugBuffer);
            
                // 2. 연결 종료 체크
                if (bytesRead == 0)
                {
                    Console.WriteLine("[DEBUG] ❌ Stream Closed (EOF) by Server.");
                    break;
                }

                // 3. 받은 데이터 분석 출력
                Console.WriteLine($"[DEBUG] 📥 Received Packet! Size: {bytesRead} bytes");
                Console.Write($"[HEX] ");
            
                for (int i = 0; i < bytesRead; i++)
                {
                    // 보기 좋게 00 0A FF 형태로 출력
                    Console.Write($"{debugBuffer[i]:X2} "); 
                }
                Console.WriteLine(); // 줄바꿈
            
                // 4. 아스키 문자열로도 찍어보기 (혹시 에러 메시지가 텍스트로 왔는지 확인)
                string asciiView = Encoding.ASCII.GetString(debugBuffer, 0, bytesRead);
                // 제어 문자(0) 등은 점(.)으로 치환해서 출력
                //string safeAscii = new string(asciiView.Select(c => char.IsControl(c) ? '.' : c).ToArray());
                Console.WriteLine($"[STR] {asciiView}");
                Console.WriteLine("------------------------------------------------");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] 💥 Error: {ex.Message}");
        }*/

        return true; // 목표량을 다 채움
    }
    }
}
