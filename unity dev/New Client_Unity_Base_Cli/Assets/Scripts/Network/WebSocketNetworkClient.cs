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
        private readonly object stateLock = new object();
        private ClientWebSocket ws;
        private CancellationTokenSource receiveCts;
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private readonly Uri url;
        private const int ReceiveBufferSize = 8192;

        private NetworkClientState state = NetworkClientState.Disconnected;
        public NetworkClientState State
        {
            get
            {
                lock (stateLock)
                    return state;
            }
        }

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
            ClientWebSocket socket;
            CancellationTokenSource cts;
            lock (stateLock)
            {
                if (state == NetworkClientState.Connected || state == NetworkClientState.Connecting)
                    return;

                state = NetworkClientState.Connecting;
                socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                ws = socket;
            }

            try
            {
                await socket.ConnectAsync(url, CancellationToken.None);
                lock (stateLock)
                {
                    if (ws != socket)
                        return;

                    state = NetworkClientState.Connected;
                    cts = new CancellationTokenSource();
                    receiveCts = cts;
                }

                _ = Task.Run(() => ReceiveLoop(socket, cts.Token));
                Connected?.Invoke();
                Debug.Log($"[WebSocketNetworkClient] Connected to {url}");
            }
            catch (Exception ex)
            {
                lock (stateLock)
                {
                    if (ws == socket)
                        ws = null;
                    state = NetworkClientState.Disconnected;
                }
                ErrorOccurred?.Invoke($"Connect failed: {ex.Message}");
                Debug.LogError($"[WebSocketNetworkClient] Connect error: {ex}");
                try { socket.Dispose(); } catch { }
            }
        }

        public void Disconnect()
        {
            ClientWebSocket socket;
            CancellationTokenSource cts;
            bool notifyDisconnected;
            lock (stateLock)
            {
                cts = receiveCts;
                receiveCts = null;
                socket = ws;
                ws = null;
                notifyDisconnected = state != NetworkClientState.Disconnected;
                state = NetworkClientState.Disconnected;
            }

            try { cts?.Cancel(); } catch (ObjectDisposedException) { }

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

            if (notifyDisconnected)
                Disconnected?.Invoke();
            Debug.Log("[WebSocketNetworkClient] Disconnected");
        }

        public async Task SendAsync(byte[] data)
        {
            await sendLock.WaitAsync();
            try
            {
                ClientWebSocket socket;
                NetworkClientState currentState;
                lock (stateLock)
                {
                    socket = ws;
                    currentState = state;
                }

                if (currentState != NetworkClientState.Connected || socket == null || socket.State != WebSocketState.Open)
                {
                    Debug.LogWarning("[WebSocketNetworkClient] Cannot send - not connected");
                    return;
                }

                await socket.SendAsync(new ArraySegment<byte>(data),
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
            finally
            {
                sendLock.Release();
            }
        }

        public void Send(byte[] data)
        {
            _ = SendAsync(data);
        }

        private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuffer = new List<byte>(ReceiveBufferSize);
            try
            {
                while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
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
                bool notifyDisconnected = false;
                lock (stateLock)
                {
                    if (ws == socket)
                    {
                        ws = null;
                        receiveCts = null;
                    }

                    if (state == NetworkClientState.Connected)
                    {
                        state = NetworkClientState.Disconnected;
                        notifyDisconnected = true;
                    }
                }

                if (notifyDisconnected)
                    Disconnected?.Invoke();
            }
        }

        public void Dispose()
        {
            Disconnect();
            try { receiveCts?.Dispose(); } catch { }
            try { sendLock.Dispose(); } catch { }
        }
    }
}
