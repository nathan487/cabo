using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cabo.Client.Network
{
    public enum NetworkClientState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// Low-level TCP socket client. Runs receive on a background task,
    /// fires events on the thread that calls ConnectAsync/Disconnect.
    /// No Unity API dependency except Debug.Log.
    /// </summary>
    public sealed class TcpNetworkClient : IDisposable
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private CancellationTokenSource receiveCts;
        private readonly string host;
        private readonly int port;
        private const int ReceiveBufferSize = 8192;

        public NetworkClientState State { get; private set; } = NetworkClientState.Disconnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> DataReceived;
        public event Action<string> ErrorOccurred;

        public TcpNetworkClient(string host, int port)
        {
            this.host = host;
            this.port = port;
        }

        public async Task ConnectAsync()
        {
            if (State == NetworkClientState.Connected || State == NetworkClientState.Connecting)
                return;

            State = NetworkClientState.Connecting;
            try
            {
                tcpClient = new TcpClient { NoDelay = true };
                await tcpClient.ConnectAsync(host, port);
                stream = tcpClient.GetStream();
                State = NetworkClientState.Connected;
                receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoop(receiveCts.Token));
                Connected?.Invoke();
                Debug.Log($"[TcpNetworkClient] Connected to {host}:{port}");
            }
            catch (Exception ex)
            {
                State = NetworkClientState.Disconnected;
                ErrorOccurred?.Invoke($"Connect failed: {ex.Message}");
                Debug.LogError($"[TcpNetworkClient] Connect error: {ex}");
            }
        }

        public void Disconnect()
        {
            receiveCts?.Cancel();
            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            stream = null;
            tcpClient = null;
            if (State != NetworkClientState.Disconnected)
            {
                State = NetworkClientState.Disconnected;
                Disconnected?.Invoke();
            }
            Debug.Log("[TcpNetworkClient] Disconnected");
        }

        public void Send(byte[] data)
        {
            if (State != NetworkClientState.Connected || stream == null)
            {
                Debug.LogWarning("[TcpNetworkClient] Cannot send — not connected");
                return;
            }

            try
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TcpNetworkClient] Send error: {ex}");
                Disconnect();
            }
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[ReceiveBufferSize];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0)
                    {
                        Debug.Log("[TcpNetworkClient] Server closed connection (0 bytes)");
                        break;
                    }

                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (System.IO.IOException) { } // Expected when connection closed
            catch (Exception ex)
            {
                Debug.LogError($"[TcpNetworkClient] Receive error: {ex}");
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
            receiveCts?.Dispose();
        }
    }
}
