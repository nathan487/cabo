using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// WebSocket transport for Cabo. It only emits complete binary messages;
    /// callers must decode the protobuf payload on the main-thread drain path.
    /// </summary>
    public sealed class WebSocketNetworkClient : IDisposable
    {
        private ClientWebSocket ws;
        private CancellationTokenSource receiveCts;
        private readonly Uri url;
        private const int ReceiveBufferSize = 8192;

        public NetworkClientState State { get; private set; } = NetworkClientState.Disconnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> DataReceived;
        public event Action<string> ErrorOccurred;

        public WebSocketNetworkClient(string url)
        {
            this.url = new Uri(url);
        }

        public async Task ConnectAsync()
        {
            if (State == NetworkClientState.Connected || State == NetworkClientState.Connecting)
                return;

            State = NetworkClientState.Connecting;
            try
            {
                ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                await ws.ConnectAsync(url, CancellationToken.None);
                State = NetworkClientState.Connected;
                receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoop(receiveCts.Token));
                Connected?.Invoke();
                Debug.Log($"[WebSocketNetworkClient] Connected to {url}");
            }
            catch (Exception ex)
            {
                State = NetworkClientState.Disconnected;
                ErrorOccurred?.Invoke($"Connect failed: {ex.Message}");
                Debug.LogError($"[WebSocketNetworkClient] Connect error: {ex}");
                try { ws?.Dispose(); } catch { }
                ws = null;
            }
        }

        public void Disconnect()
        {
            var cts = receiveCts;
            receiveCts = null;
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }

            var socket = ws;
            ws = null;
            if (socket != null)
            {
                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                          "Client disconnect",
                                          CancellationToken.None).Wait(500);
                    }
                }
                catch { }
                try { socket.Dispose(); } catch { }
            }

            if (State != NetworkClientState.Disconnected)
            {
                State = NetworkClientState.Disconnected;
                Disconnected?.Invoke();
            }
            Debug.Log("[WebSocketNetworkClient] Disconnected");
        }

        public async Task SendAsync(byte[] data)
        {
            if (State != NetworkClientState.Connected || ws == null || ws.State != WebSocketState.Open)
            {
                Debug.LogWarning("[WebSocketNetworkClient] Cannot send - not connected");
                return;
            }

            try
            {
                await ws.SendAsync(new ArraySegment<byte>(data),
                                   WebSocketMessageType.Binary,
                                   true,
                                   CancellationToken.None);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Send failed: {ex.Message}");
                Debug.LogError($"[WebSocketNetworkClient] Send error: {ex}");
                Disconnect();
            }
        }

        public void Send(byte[] data)
        {
            _ = SendAsync(data);
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuffer = new List<byte>(ReceiveBufferSize);
            try
            {
                while (!ct.IsCancellationRequested && ws != null && ws.State == WebSocketState.Open)
                {
                    messageBuffer.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                        {
                            for (int i = 0; i < result.Count; ++i)
                                messageBuffer.Add(buffer[i]);
                        }
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (messageBuffer.Count > 0)
                        DataReceived?.Invoke(messageBuffer.ToArray());
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (System.IO.IOException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketNetworkClient] Receive error: {ex}");
            }
            finally
            {
                if (State == NetworkClientState.Connected)
                {
                    State = NetworkClientState.Disconnected;
                    Disconnected?.Invoke();
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            try { receiveCts?.Dispose(); } catch { }
        }
    }
}
