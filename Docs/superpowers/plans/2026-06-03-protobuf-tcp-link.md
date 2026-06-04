# Protobuf + TCP Link MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity 客户端通过 TCP + protobuf 连接到 C++ Muduo 服务端，实现房间创建/加入/离开/准备完整闭环。

**Architecture:** 客户端 TCP NetworkClient → MessageCodec([len][proto]) → ProtoGateway(IBackendGateway impl) → RoomClientController；服务端 TcpServer → MessageCodec → MessageDispatcher → RoomService。protobuf 生成 C# 和 C++ 代码从同一 Proto/ 源文件。

**Tech Stack:** C# (Unity 2022.3), C++11 (Muduo), protobuf 3.x, protoc, dotnet (for Google.Protobuf DLL)

---

## File Structure

```
Proto/                           # proto 源文件 (已有)
├── common.proto
├── messages.proto
├── room.proto
├── game.proto
└── sync.proto

# === Client (Unity C#) ===
unity dev/Client/Assets/
├── Plugins/
│   └── Google.Protobuf.dll      # NEW: NuGet DLL
├── Scripts/
│   ├── Proto/Generated/         # NEW: protoc 生成的 C# 代码
│   │   ├── Common.cs
│   │   ├── Messages.cs
│   │   ├── Room.cs
│   │   ├── Game.cs
│   │   └── Sync.cs
│   ├── Network/                 # NEW: 网络层
│   │   ├── TcpNetworkClient.cs
│   │   ├── MessageCodec.cs
│   │   ├── MessageDispatcher.cs
│   │   └── RequestTracker.cs
│   ├── ClientCore/
│   │   ├── Network/
│   │   │   └── ProtoGateway.cs  # NEW: IBackendGateway 真实实现
│   │   ├── Game/
│   │   │   └── GameClientController.cs  # NEW
│   │   └── Runtime/
│   │       └── ClientAppBootstrap.cs   # MODIFY
│   └── ...
└── ...

# === Server (C++ Muduo) ===
MuduoBaseGameServer/
├── CMakeLists.txt               # MODIFY: 添加 GameServer target
├── src/
│   ├── common/
│   │   ├── MessageCodec.h       # NEW
│   │   ├── MessageCodec.cc      # NEW
│   │   ├── MessageDispatcher.h  # NEW
│   │   └── MessageDispatcher.cc # NEW
│   ├── room/
│   │   ├── RoomService.h        # NEW
│   │   └── RoomService.cc       # NEW
│   ├── server/
│   │   └── GameServer.cc        # NEW
│   └── proto/                   # NEW: protoc 生成 C++ 代码
│       ├── common.pb.h
│       ├── common.pb.cc
│       ├── messages.pb.h
│       ├── messages.pb.cc
│       ├── room.pb.h
│       ├── room.pb.cc
│       ├── game.pb.h
│       ├── game.pb.cc
│       ├── sync.pb.h
│       └── sync.pb.cc
└── ...
```

---

## Task 1: Install Dependencies

**Files:** None (system-level)

- [ ] **Step 1: Install protobuf C++ runtime and dev headers**

```bash
sudo apt-get install -y libprotobuf-dev protobuf-compiler
protoc --version
```

Expected: `libprotoc 3.x.x`

- [ ] **Step 2: Get Google.Protobuf DLL for Unity (.NET Standard 2.0)**

```bash
mkdir -p /tmp/protobuf-dll
cd /tmp/protobuf-dll
dotnet new console --force
dotnet add package Google.Protobuf --version 3.21.12
dotnet publish -c Release -o out
ls out/Google.Protobuf.dll
```

Expected: `out/Google.Protobuf.dll` exists

- [ ] **Step 3: Copy DLL to Unity project**

```bash
DEST="/mnt/c/Users/Admin/Desktop/Cabo GameObject/unity dev/Client/Assets/Plugins"
mkdir -p "$DEST"
cp /tmp/protobuf-dll/out/Google.Protobuf.dll "$DEST/"
cp /tmp/protobuf-dll/out/Google.Protobuf.pdb "$DEST/" 2>/dev/null || true
ls "$DEST/Google.Protobuf.dll"
```

Clean up: `rm -rf /tmp/protobuf-dll`

---

## Task 2: Generate C# Protobuf Code

**Files:**
- Create: `unity dev/Client/Assets/Scripts/Proto/Generated/Common.cs`
- Create: `unity dev/Client/Assets/Scripts/Proto/Generated/Messages.cs`
- Create: `unity dev/Client/Assets/Scripts/Proto/Generated/Room.cs`
- Create: `unity dev/Client/Assets/Scripts/Proto/Generated/Game.cs`
- Create: `unity dev/Client/Assets/Scripts/Proto/Generated/Sync.cs`

- [ ] **Step 1: Run protoc to generate C# code**

```bash
PROTO_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/Proto"
OUT_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/unity dev/Client/Assets/Scripts/Proto/Generated"
mkdir -p "$OUT_DIR"

protoc \
  --csharp_out="$OUT_DIR" \
  --proto_path="$PROTO_DIR" \
  "$PROTO_DIR/common.proto" \
  "$PROTO_DIR/room.proto" \
  "$PROTO_DIR/game.proto" \
  "$PROTO_DIR/sync.proto" \
  "$PROTO_DIR/messages.proto"

ls "$OUT_DIR"/
```

Expected: 5 .cs files generated

- [ ] **Step 2: Create a .asmdef for the Generated folder to reference Google.Protobuf.dll**

Since the generated code references `Google.Protobuf` namespace, we need the DLL to be discoverable. The generated .cs files will have:
```csharp
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
```

Create `unity dev/Client/Assets/Scripts/Proto/Generated/Cabo.Proto.Generated.asmdef`:

```json
{
    "name": "Cabo.Proto.Generated",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Wait — we actually don't need an .asmdef unless we're using assembly definitions. The Google.Protobuf.dll in Plugins/ is auto-referenced. Let me skip the .asmdef.

Actually, let's just verify the generated files have the right namespace. The proto files specify:
- `package game.common; option csharp_namespace = "Game.Common";`
- `package game.messages; option csharp_namespace = "Game.Messages";`
- `package game.room; option csharp_namespace = "Game.Room";`
- `package game.game; option csharp_namespace = "Game.Game";`
- `package game.sync; option csharp_namespace = "Game.Sync";`

These should match the generated code. Let's verify after generation.

---

## Task 3: Client — TcpNetworkClient

**Files:**
- Create: `unity dev/Client/Assets/Scripts/Network/TcpNetworkClient.cs`

- [ ] **Step 1: Create TcpNetworkClient.cs**

This is the low-level TCP socket manager. Runs on the main thread, uses `System.Net.Sockets.TcpClient`. No protobuf dependency — works with raw byte[].

```csharp
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

    public sealed class TcpNetworkClient : IDisposable
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private CancellationTokenSource receiveCts;
        private readonly string host;
        private readonly int port;
        private readonly int receiveBufferSize = 8192;

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
            stream?.Close();
            tcpClient?.Close();
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
            var buffer = new byte[receiveBufferSize];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0)
                    {
                        // Server closed connection
                        Debug.Log("[TcpNetworkClient] Server closed connection");
                        break;
                    }

                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException) { }
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
```

- [ ] **Step 2: Verify file compiles conceptually**

Check: the class uses `System.Net.Sockets.TcpClient`, `System.Threading.Tasks` — both available in Unity 2022.3 (.NET Standard 2.1).

---

## Task 4: Client — MessageCodec

**Files:**
- Create: `unity dev/Client/Assets/Scripts/Network/MessageCodec.cs`

- [ ] **Step 1: Create MessageCodec.cs**

Handles [4byte big-endian length][protobuf data] framing. Accumulates received bytes, extracts complete frames, deserializes ServerMessage. Also serializes ClientMessage to bytes for sending.

```csharp
using System;
using System.Collections.Generic;
using Game.Messages;
using Google.Protobuf;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Handles [4-byte big-endian length][protobuf bytes] framing.
    /// Thread-safe for receive buffer accumulation; all public methods
    /// should be called from the Unity main thread.
    /// </summary>
    public sealed class MessageCodec
    {
        private byte[] receiveBuffer = new byte[0];

        /// <summary>
        /// Feed received raw bytes into the codec.
        /// Calls onMessage for each complete ServerMessage decoded.
        /// </summary>
        public void FeedBytes(byte[] data, Action<ServerMessage> onMessage)
        {
            // Append new data to buffer
            var newBuffer = new byte[receiveBuffer.Length + data.Length];
            if (receiveBuffer.Length > 0)
                Array.Copy(receiveBuffer, 0, newBuffer, 0, receiveBuffer.Length);
            Array.Copy(data, 0, newBuffer, receiveBuffer.Length, data.Length);
            receiveBuffer = newBuffer;

            // Try to extract complete frames
            while (receiveBuffer.Length >= 4)
            {
                int payloadLength = ReadBigEndianInt32(receiveBuffer, 0);
                int frameLength = 4 + payloadLength;

                if (receiveBuffer.Length < frameLength)
                    break; // Incomplete frame — wait for more data

                // Extract payload
                var payload = new byte[payloadLength];
                Array.Copy(receiveBuffer, 4, payload, 0, payloadLength);

                // Deserialize
                try
                {
                    var message = ServerMessage.Parser.ParseFrom(payload);
                    onMessage?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MessageCodec] Parse error: {ex.Message}");
                }

                // Remove processed frame from buffer
                int remaining = receiveBuffer.Length - frameLength;
                if (remaining > 0)
                {
                    var truncated = new byte[remaining];
                    Array.Copy(receiveBuffer, frameLength, truncated, 0, remaining);
                    receiveBuffer = truncated;
                }
                else
                {
                    receiveBuffer = new byte[0];
                    break;
                }
            }
        }

        /// <summary>
        /// Serialize a ClientMessage to bytes ready for TCP send.
        /// Returns [4-byte big-endian length][protobuf bytes]
        /// </summary>
        public static byte[] Encode(ClientMessage message)
        {
            var payload = message.ToByteArray();
            var frame = new byte[4 + payload.Length];
            WriteBigEndianInt32(frame, 0, payload.Length);
            Array.Copy(payload, 0, frame, 4, payload.Length);
            return frame;
        }

        /// <summary>
        /// Deserialize raw bytes (full frame payload, without length prefix)
        /// into a ServerMessage. Used by RequestTracker for direct deserialization.
        /// </summary>
        public static ServerMessage Decode(byte[] payload)
        {
            return ServerMessage.Parser.ParseFrom(payload);
        }

        private static int ReadBigEndianInt32(byte[] buf, int offset)
        {
            return (buf[offset] << 24)
                 | (buf[offset + 1] << 16)
                 | (buf[offset + 2] << 8)
                 | buf[offset + 3];
        }

        private static void WriteBigEndianInt32(byte[] buf, int offset, int value)
        {
            buf[offset]     = (byte)((value >> 24) & 0xFF);
            buf[offset + 1] = (byte)((value >> 16) & 0xFF);
            buf[offset + 2] = (byte)((value >> 8) & 0xFF);
            buf[offset + 3] = (byte)(value & 0xFF);
        }

        public void Reset()
        {
            receiveBuffer = new byte[0];
        }
    }
}
```

---

## Task 5: Client — MessageDispatcher

**Files:**
- Create: `unity dev/Client/Assets/Scripts/Network/MessageDispatcher.cs`

- [ ] **Step 1: Create MessageDispatcher.cs**

Dispatches ServerMessage payloads by oneof type. Handlers register for specific message types. Unknown payloads log a warning.

```csharp
using System;
using System.Collections.Generic;
using Game.Messages;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Routes ServerMessage oneof payload to registered handlers.
    /// </summary>
    public sealed class MessageDispatcher
    {
        private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();
        private long lastServerSeq;
        private readonly HashSet<long> seenServerSeqs = new HashSet<long>();

        public event Action<long> ServerSeqSkipped; // duplicate/out-of-order

        /// <summary>
        /// Register a typed handler for a specific ServerMessage payload.
        /// </summary>
        public void Register<T>(Action<T> handler) where T : class
        {
            handlers[typeof(T)] = handler;
        }

        /// <summary>
        /// Dispatch a ServerMessage to the appropriate handler.
        /// Checks server_seq for ordering/duplicates.
        /// </summary>
        public void Dispatch(ServerMessage message)
        {
            // seq check
            if (message.ServerSeq > 0)
            {
                if (seenServerSeqs.Contains(message.ServerSeq))
                {
                    Debug.LogWarning($"[MessageDispatcher] Duplicate server_seq={message.ServerSeq}, skipping");
                    ServerSeqSkipped?.Invoke(message.ServerSeq);
                    return;
                }
                if (message.ServerSeq < lastServerSeq)
                {
                    Debug.LogWarning($"[MessageDispatcher] Out-of-order server_seq={message.ServerSeq} (last={lastServerSeq})");
                }
                lastServerSeq = message.ServerSeq;
                seenServerSeqs.Add(message.ServerSeq);
            }

            // Dispatch by oneof case
            switch (message.PayloadCase)
            {
                case ServerMessage.PayloadOneofCase.CreateRoomRsp:
                    InvokeHandler(message.CreateRoomRsp);
                    break;
                case ServerMessage.PayloadOneofCase.JoinRoomRsp:
                    InvokeHandler(message.JoinRoomRsp);
                    break;
                case ServerMessage.PayloadOneofCase.LeaveRoomRsp:
                    InvokeHandler(message.LeaveRoomRsp);
                    break;
                case ServerMessage.PayloadOneofCase.ReadyRsp:
                    InvokeHandler(message.ReadyRsp);
                    break;
                case ServerMessage.PayloadOneofCase.StartGameRsp:
                    InvokeHandler(message.StartGameRsp);
                    break;
                case ServerMessage.PayloadOneofCase.RoomStateNotify:
                    InvokeHandler(message.RoomStateNotify);
                    break;
                case ServerMessage.PayloadOneofCase.PlayerJoinNotify:
                    InvokeHandler(message.PlayerJoinNotify);
                    break;
                case ServerMessage.PayloadOneofCase.PlayerLeaveNotify:
                    InvokeHandler(message.PlayerLeaveNotify);
                    break;
                case ServerMessage.PayloadOneofCase.PlayerReadyNotify:
                    InvokeHandler(message.PlayerReadyNotify);
                    break;
                case ServerMessage.PayloadOneofCase.RoomStartNotify:
                    InvokeHandler(message.RoomStartNotify);
                    break;
                case ServerMessage.PayloadOneofCase.GameStartNotify:
                    InvokeHandler(message.GameStartNotify);
                    break;
                case ServerMessage.PayloadOneofCase.TurnStartNotify:
                    InvokeHandler(message.TurnStartNotify);
                    break;
                case ServerMessage.PayloadOneofCase.ActionResultNotify:
                    InvokeHandler(message.ActionResultNotify);
                    break;
                case ServerMessage.PayloadOneofCase.HeartbeatRsp:
                    InvokeHandler(message.HeartbeatRsp);
                    break;
                case ServerMessage.PayloadOneofCase.ReconnectRsp:
                    InvokeHandler(message.ReconnectRsp);
                    break;
                case ServerMessage.PayloadOneofCase.StateSyncNotify:
                    InvokeHandler(message.StateSyncNotify);
                    break;
                case ServerMessage.PayloadOneofCase.None:
                    Debug.LogWarning("[MessageDispatcher] Received ServerMessage with no payload");
                    break;
                default:
                    Debug.LogWarning($"[MessageDispatcher] Unhandled payload type: {message.PayloadCase}");
                    break;
            }
        }

        private void InvokeHandler<T>(T payload) where T : class
        {
            if (payload == null) return;
            if (handlers.TryGetValue(typeof(T), out var handler))
            {
                ((Action<T>)handler)?.Invoke(payload);
            }
            else
            {
                Debug.LogWarning($"[MessageDispatcher] No handler registered for {typeof(T).Name}");
            }
        }

        public void Reset()
        {
            lastServerSeq = 0;
            seenServerSeqs.Clear();
        }
    }
}
```

---

## Task 6: Client — RequestTracker

**Files:**
- Create: `unity dev/Client/Assets/Scripts/Network/RequestTracker.cs`

- [ ] **Step 1: Create RequestTracker.cs**

Maps request_id to waiting callbacks. Supports timeout. Used by ProtoGateway to match responses to pending requests.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Tracks in-flight requests by request_id. Used for Rsp matching and timeout.
    /// </summary>
    public sealed class RequestTracker
    {
        private const float DefaultTimeoutSeconds = 5f;

        public struct PendingRequest
        {
            public long RequestId;
            public float SentTime;
            public Action<byte[]> OnResponse;  // raw payload bytes for deserialization
            public Action<string> OnTimeout;
        }

        private readonly Dictionary<long, PendingRequest> pending = new Dictionary<long, PendingRequest>();
        private readonly List<long> toRemove = new List<long>();

        /// <summary>
        /// Register a new pending request.
        /// </summary>
        public void Register(long requestId, Action<byte[]> onResponse, Action<string> onTimeout)
        {
            pending[requestId] = new PendingRequest
            {
                RequestId = requestId,
                SentTime = Time.time,
                OnResponse = onResponse,
                OnTimeout = onTimeout
            };
        }

        /// <summary>
        /// Resolve a pending request with response payload bytes.
        /// Returns true if a matching request was found.
        /// </summary>
        public bool Resolve(long requestId, byte[] payload)
        {
            if (pending.TryGetValue(requestId, out var req))
            {
                pending.Remove(requestId);
                req.OnResponse?.Invoke(payload);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Call periodically (e.g. in MonoBehaviour.Update) to timeout stale requests.
        /// </summary>
        public void Tick(float currentTime)
        {
            toRemove.Clear();
            foreach (var kv in pending)
            {
                if (currentTime - kv.Value.SentTime > DefaultTimeoutSeconds)
                {
                    kv.Value.OnTimeout?.Invoke($"Request {kv.Key} timed out");
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var id in toRemove)
                pending.Remove(id);
        }

        public void Clear()
        {
            foreach (var kv in pending)
                kv.Value.OnTimeout?.Invoke("Connection lost");
            pending.Clear();
        }
    }
}
```

---

## Task 7: Client — ProtoGateway (IBackendGateway implementation)

**Files:**
- Create: `unity dev/Client/Assets/Scripts/ClientCore/Network/ProtoGateway.cs`

- [ ] **Step 1: Create ProtoGateway.cs**

Implements IBackendGateway. Uses TcpNetworkClient, MessageCodec, MessageDispatcher, RequestTracker internally. Converts RoomSnapshot/BackendModels to/from proto types.

```csharp
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Messages;
using Game.Room;
using UnityEngine;

namespace Cabo.Client.Network
{
    public sealed class ProtoGateway : IBackendGateway, IDisposable
    {
        private readonly string host;
        private readonly int port;
        private readonly TcpNetworkClient client;
        private readonly MessageCodec codec = new MessageCodec();
        private readonly MessageDispatcher dispatcher = new MessageDispatcher();
        private readonly RequestTracker requestTracker = new RequestTracker();

        private long nextSeq;
        private long nextRequestId;
        private readonly MonoBehaviour coroutineOwner;

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public string LocalPlayerId { get; private set; } = string.Empty;
        public string SessionToken { get; private set; } = string.Empty;

        public event Action<ConnectionStatus> ConnectionStatusChanged;
        public event Action<RoomSnapshot> RoomUpdated;
        public event Action<string> RoomStarted;
        public event Action<BackendError> OperationFailed;

        private RoomSnapshot currentRoomSnapshot;

        public ProtoGateway(string host, int port, MonoBehaviour coroutineOwner)
        {
            this.host = host;
            this.port = port;
            this.coroutineOwner = coroutineOwner;
            client = new TcpNetworkClient(host, port);

            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;
            client.DataReceived += OnDataReceived;
            client.ErrorOccurred += OnClientError;

            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            dispatcher.Register<CreateRoomRsp>(OnCreateRoomRsp);
            dispatcher.Register<JoinRoomRsp>(OnJoinRoomRsp);
            dispatcher.Register<LeaveRoomRsp>(OnLeaveRoomRsp);
            dispatcher.Register<Game.Room.ReadyRsp>(OnReadyRsp);
            dispatcher.Register<StartGameRsp>(OnStartGameRsp);
            dispatcher.Register<RoomStateNotify>(OnRoomStateNotify);
            dispatcher.Register<PlayerJoinNotify>(OnPlayerJoinNotify);
            dispatcher.Register<PlayerLeaveNotify>(OnPlayerLeaveNotify);
            dispatcher.Register<PlayerReadyNotify>(OnPlayerReadyNotify);
            dispatcher.Register<Game.Room.RoomStartNotify>(OnRoomStartNotify);
        }

        // ── IBackendGateway ──

        public void Connect(string nickname)
        {
            Status = ConnectionStatus.Connecting;
            ConnectionStatusChanged?.Invoke(Status);
            // nickname stored for CreateRoom/JoinRoom
            LocalPlayerId = "pending"; // will be set by server response
            _ = client.ConnectAsync();
        }

        public void Disconnect()
        {
            client.Disconnect();
            requestTracker.Clear();
            Status = ConnectionStatus.Disconnected;
            ConnectionStatusChanged?.Invoke(Status);
        }

        public void CreateRoom(int maxPlayers)
        {
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                CreateRoomReq = new CreateRoomReq
                {
                    RequestId = requestId,
                    MaxPlayers = maxPlayers,
                    Nickname = "Player" // TODO: pass real nickname
                }
            };
            SendWithTracking(requestId, msg);
        }

        public void JoinRoom(string roomCode)
        {
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                JoinRoomReq = new JoinRoomReq
                {
                    RequestId = requestId,
                    RoomCode = roomCode,
                    Nickname = "Player"
                }
            };
            SendWithTracking(requestId, msg);
        }

        public void SetReady(bool isReady)
        {
            if (string.IsNullOrEmpty(LocalPlayerId)) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                ReadyReq = new Game.Room.ReadyReq
                {
                    RequestId = requestId,
                    PlayerId = long.Parse(LocalPlayerId),
                    IsReady = isReady
                }
            };
            SendWithTracking(requestId, msg);
        }

        public void StartGame()
        {
            if (string.IsNullOrEmpty(LocalPlayerId)) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                StartGameReq = new Game.Room.StartGameReq
                {
                    RequestId = requestId,
                    PlayerId = long.Parse(LocalPlayerId)
                }
            };
            SendWithTracking(requestId, msg);
        }

        private void SendWithTracking(long requestId, ClientMessage msg)
        {
            requestTracker.Register(requestId,
                onResponse: payload =>
                {
                    // Response matching is handled by the typed response handlers
                    Debug.Log($"[ProtoGateway] Request {requestId} resolved");
                },
                onTimeout: error =>
                {
                    OperationFailed?.Invoke(new BackendError { Code = 9002, Message = error });
                });

            Send(msg);
        }

        public void Send(ClientMessage msg)
        {
            var frame = MessageCodec.Encode(msg);
            client.Send(frame);
        }

        // ── Internal events ──

        private void OnClientConnected()
        {
            Status = ConnectionStatus.Connected;
            ConnectionStatusChanged?.Invoke(Status);
            // Start tick coroutine for timeout checking
            coroutineOwner.StartCoroutine(TickCoroutine());
        }

        private void OnClientDisconnected()
        {
            Status = ConnectionStatus.Disconnected;
            ConnectionStatusChanged?.Invoke(Status);
            requestTracker.Clear();
        }

        private void OnDataReceived(byte[] data)
        {
            codec.FeedBytes(data, message =>
            {
                dispatcher.Dispatch(message);
            });
        }

        private void OnClientError(string error)
        {
            OperationFailed?.Invoke(new BackendError { Code = 9003, Message = error });
        }

        private System.Collections.IEnumerator TickCoroutine()
        {
            while (Status == ConnectionStatus.Connected)
            {
                requestTracker.Tick(Time.time);
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Response handlers ──

        private void OnCreateRoomRsp(CreateRoomRsp rsp)
        {
            if (rsp.Error != null && rsp.Error.Code != 0)
            {
                OperationFailed?.Invoke(new BackendError { Code = rsp.Error.Code, Message = rsp.Error.Message });
                return;
            }
            LocalPlayerId = rsp.PlayerId.ToString();
            SessionToken = rsp.SessionToken;
            requestTracker.Resolve(rsp.RequestId, null);
            Debug.Log($"[ProtoGateway] Room created: {rsp.RoomCode}, playerId={rsp.PlayerId}");
        }

        private void OnJoinRoomRsp(JoinRoomRsp rsp)
        {
            if (rsp.Error != null && rsp.Error.Code != 0)
            {
                OperationFailed?.Invoke(new BackendError { Code = rsp.Error.Code, Message = rsp.Error.Message });
                return;
            }
            LocalPlayerId = rsp.PlayerId.ToString();
            SessionToken = rsp.SessionToken;
            requestTracker.Resolve(rsp.RequestId, null);
            Debug.Log($"[ProtoGateway] Joined room: {rsp.RoomId}, seat={rsp.SeatId}");
        }

        private void OnLeaveRoomRsp(LeaveRoomRsp rsp)
        {
            requestTracker.Resolve(rsp.RequestId, null);
            currentRoomSnapshot = null;
        }

        private void OnReadyRsp(Game.Room.ReadyRsp rsp)
        {
            requestTracker.Resolve(rsp.RequestId, null);
        }

        private void OnStartGameRsp(StartGameRsp rsp)
        {
            if (rsp.Error != null && rsp.Error.Code != 0)
            {
                OperationFailed?.Invoke(new BackendError { Code = rsp.Error.Code, Message = rsp.Error.Message });
                return;
            }
            requestTracker.Resolve(rsp.RequestId, null);
        }

        private void OnRoomStateNotify(RoomStateNotify notify)
        {
            var room = notify.Room;
            if (room == null) return;

            currentRoomSnapshot = ConvertRoomState(room);
            RoomUpdated?.Invoke(currentRoomSnapshot);
        }

        private void OnPlayerJoinNotify(PlayerJoinNotify notify)
        {
            Debug.Log($"[ProtoGateway] Player joined: {notify.Player?.PlayerId}");
            // Full state will follow via RoomStateNotify
        }

        private void OnPlayerLeaveNotify(PlayerLeaveNotify notify)
        {
            Debug.Log($"[ProtoGateway] Player left: {notify.PlayerId}");
        }

        private void OnPlayerReadyNotify(PlayerReadyNotify notify)
        {
            Debug.Log($"[ProtoGateway] Player ready: {notify.PlayerId} = {notify.IsReady}");
        }

        private void OnRoomStartNotify(Game.Room.RoomStartNotify notify)
        {
            Debug.Log($"[ProtoGateway] Room {notify.RoomId} started!");
            RoomStarted?.Invoke(currentRoomSnapshot?.RoomCode ?? "");
        }

        // ── Conversion ──

        private static RoomSnapshot ConvertRoomState(Game.Room.RoomState room)
        {
            var snapshot = new RoomSnapshot
            {
                RoomId = room.RoomId,
                RoomCode = room.RoomCode,
                MaxPlayers = room.MaxPlayers,
                HostPlayerId = room.HostPlayerId.ToString(),
                InGame = room.State == RoomStateType.Playing,
                Players = new List<PlayerPublicInfoModel>()
            };

            foreach (var p in room.Players)
            {
                snapshot.Players.Add(new PlayerPublicInfoModel
                {
                    PlayerId = p.PlayerId.ToString(),
                    Nickname = p.Nickname,
                    SeatId = p.SeatId,
                    IsReady = p.IsReady,
                    IsHost = p.IsHost,
                    IsConnected = p.IsConnected,
                    TotalScore = p.TotalScore
                });
            }

            return snapshot;
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }
}
```

---

## Task 8: Client — GameClientController

**Files:**
- Create: `unity dev/Client/Assets/Scripts/ClientCore/Game/GameClientController.cs`

- [ ] **Step 1: Create GameClientController.cs**

Handles game-phase message routing. Stub for now — will be expanded in later phases.

```csharp
using System;
using Game.Game;
using UnityEngine;

namespace Cabo.Client.Game
{
    /// <summary>
    /// Receives game-phase protocol messages (TurnStart, ActionResult, etc.)
    /// and updates the local ClientViewState for UI rendering.
    /// MVP: stubs that log and update minimal state.
    /// </summary>
    public sealed class GameClientController : MonoBehaviour
    {
        public event Action<TurnStartNotify> TurnStarted;
        public event Action<ActionResultNotify> ActionResulted;
        public event Action<RoundRevealNotify> RoundRevealed;
        public event Action<ScoreUpdateNotify> ScoreUpdated;
        public event Action<GameOverNotify> GameOvered;
        public event Action<GameStartNotify> GameStarted;

        public void HandleGameStart(GameStartNotify notify)
        {
            Debug.Log($"[GameClientController] Game started, round={notify.RoundNumber}, firstPlayer={notify.FirstPlayerId}");
            GameStarted?.Invoke(notify);
        }

        public void HandleTurnStart(TurnStartNotify notify)
        {
            Debug.Log($"[GameClientController] Turn start: player={notify.CurrentPlayerId}, turn={notify.TurnNumber}");
            TurnStarted?.Invoke(notify);
        }

        public void HandleActionResult(ActionResultNotify notify)
        {
            Debug.Log($"[GameClientController] Action: type={notify.ActionType}, player={notify.SourcePlayerId}");
            ActionResulted?.Invoke(notify);
        }

        public void HandleRoundReveal(RoundRevealNotify notify)
        {
            Debug.Log($"[GameClientController] Round reveal: round={notify.RoundNumber}, caller={notify.SteadyCallerId}");
            RoundRevealed?.Invoke(notify);
        }

        public void HandleScoreUpdate(ScoreUpdateNotify notify)
        {
            Debug.Log($"[GameClientController] Score update: round={notify.RoundNumber}");
            ScoreUpdated?.Invoke(notify);
        }

        public void HandleGameOver(GameOverNotify notify)
        {
            Debug.Log($"[GameClientController] Game over! Total rounds={notify.TotalRounds}");
            GameOvered?.Invoke(notify);
        }
    }
}
```

---

## Task 9: Client — Update ClientAppBootstrap

**Files:**
- Modify: `unity dev/Client/Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs`

- [ ] **Step 1: Add ProtoGateway creation support**

Read the current file, then modify to support a "Proto" mode that connects to a configured server address.

```csharp
using Cabo.Client.Room;
using UnityEngine;

namespace Cabo.Client.Runtime
{
    public sealed class ClientAppBootstrap : MonoBehaviour
    {
        [SerializeField] private RoomClientController.BackendMode backendMode = RoomClientController.BackendMode.Mock;
        [SerializeField] private bool autoCreateRoomUi = true;

        // Proto mode settings
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 8888;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            var roomController = GetComponent<RoomClientController>();
            if (roomController == null)
            {
                roomController = gameObject.AddComponent<RoomClientController>();
            }

            roomController.Initialize(backendMode);

            // If Proto mode, create the real gateway (it replaces the placeholder
            // that Initialize() created when mode == ProtoPlaceholder)
            if (backendMode == RoomClientController.BackendMode.ProtoPlaceholder)
            {
                roomController.SetProtoGateway(new Cabo.Client.Network.ProtoGateway(
                    serverHost, serverPort, this));
            }

            if (autoCreateRoomUi && GetComponent<Cabo.Client.UI.LobbyRoomDemoUI>() == null)
            {
                gameObject.AddComponent<Cabo.Client.UI.LobbyRoomDemoUI>();
            }
        }
    }
}
```

---

## Task 10: Client — Add SetProtoGateway to RoomClientController

**Files:**
- Modify: `unity dev/Client/Assets/Scripts/ClientCore/Room/RoomClientController.cs`

- [ ] **Step 1: Add SetProtoGateway method**

Add a method that allows injecting an externally-created ProtoGateway. This avoids modifying the existing BuildGateway() flow.

```csharp
// Add this method to RoomClientController class:

/// <summary>
/// Inject an externally-created ProtoGateway (with host/port config).
/// Replaces the placeholder created by BuildGateway().
/// </summary>
public void SetProtoGateway(Cabo.Client.Network.ProtoGateway realGateway)
{
    if (gateway != null)
    {
        Unsubscribe(gateway);
        if (gateway is Cabo.Client.Network.ProtoGateway old)
            old.Dispose();
    }

    gateway = realGateway;
    Subscribe(gateway);
    Debug.Log("[RoomClientController] Switched to real ProtoGateway");
}
```

---

## Task 11: Server — Install Protobuf C++ and Generate Code

**Files:**
- Create: `MuduoBaseGameServer/src/proto/*.pb.h`, `MuduoBaseGameServer/src/proto/*.pb.cc`

- [ ] **Step 1: Install protobuf C++ development package**

```bash
sudo apt-get install -y libprotobuf-dev protobuf-compiler
```

- [ ] **Step 2: Generate C++ protobuf code**

```bash
PROTO_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/Proto"
OUT_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/src/proto"
mkdir -p "$OUT_DIR"

protoc \
  --cpp_out="$OUT_DIR" \
  --proto_path="$PROTO_DIR" \
  "$PROTO_DIR/common.proto" \
  "$PROTO_DIR/room.proto" \
  "$PROTO_DIR/game.proto" \
  "$PROTO_DIR/sync.proto" \
  "$PROTO_DIR/messages.proto"

ls "$OUT_DIR"/
```

Expected: 10 files (5 `.pb.h` + 5 `.pb.cc`)

---

## Task 12: Server — MessageCodec

**Files:**
- Create: `MuduoBaseGameServer/src/common/MessageCodec.h`
- Create: `MuduoBaseGameServer/src/common/MessageCodec.cc`

- [ ] **Step 1: Create MessageCodec.h**

```cpp
#pragma once
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

namespace game {

// [4-byte big-endian length][protobuf payload]
// Thread-compatible — each TcpConnection has its own codec instance.

class MessageCodec {
public:
    using MessageCallback = std::function<void(const std::vector<uint8_t>& payload)>;

    MessageCodec() = default;

    // Feed raw bytes from TCP. Calls onMessage for each complete frame.
    void feedBytes(const char* data, size_t len, const MessageCallback& onMessage);

    // Encode a payload into a framed message ready for TCP send.
    static std::string encode(const std::string& payload);

    // Reset internal buffer (e.g., on connection close).
    void reset();

private:
    std::vector<uint8_t> buffer_;
};

} // namespace game
```

- [ ] **Step 2: Create MessageCodec.cc**

```cpp
#include "common/MessageCodec.h"
#include <cstring>
#include <arpa/inet.h>

namespace game {

static int32_t readBigEndianInt32(const uint8_t* buf) {
    return (static_cast<int32_t>(buf[0]) << 24)
         | (static_cast<int32_t>(buf[1]) << 16)
         | (static_cast<int32_t>(buf[2]) << 8)
         | static_cast<int32_t>(buf[3]);
}

static void writeBigEndianInt32(uint8_t* buf, int32_t value) {
    buf[0] = static_cast<uint8_t>((value >> 24) & 0xFF);
    buf[1] = static_cast<uint8_t>((value >> 16) & 0xFF);
    buf[2] = static_cast<uint8_t>((value >> 8)  & 0xFF);
    buf[3] = static_cast<uint8_t>(value & 0xFF);
}

void MessageCodec::feedBytes(const char* data, size_t len, const MessageCallback& onMessage) {
    // Append to buffer
    buffer_.insert(buffer_.end(), reinterpret_cast<const uint8_t*>(data),
                   reinterpret_cast<const uint8_t*>(data) + len);

    // Extract complete frames
    while (buffer_.size() >= 4) {
        int32_t payloadLen = readBigEndianInt32(buffer_.data());
        if (payloadLen < 0) {
            // Invalid frame, reset
            buffer_.clear();
            return;
        }
        size_t frameLen = 4 + static_cast<size_t>(payloadLen);
        if (buffer_.size() < frameLen) {
            break; // Incomplete frame
        }

        // Extract payload
        std::vector<uint8_t> payload(buffer_.begin() + 4, buffer_.begin() + frameLen);

        // Call handler
        if (onMessage) {
            onMessage(payload);
        }

        // Remove processed frame
        buffer_.erase(buffer_.begin(), buffer_.begin() + frameLen);
    }
}

std::string MessageCodec::encode(const std::string& payload) {
    std::string result;
    int32_t len = static_cast<int32_t>(payload.size());
    uint8_t header[4];
    writeBigEndianInt32(header, len);
    result.append(reinterpret_cast<const char*>(header), 4);
    result.append(payload);
    return result;
}

void MessageCodec::reset() {
    buffer_.clear();
}

} // namespace game
```

---

## Task 13: Server — MessageDispatcher

**Files:**
- Create: `MuduoBaseGameServer/src/common/MessageDispatcher.h`
- Create: `MuduoBaseGameServer/src/common/MessageDispatcher.cc`

- [ ] **Step 1: Create MessageDispatcher.h**

```cpp
#pragma once
#include "messages.pb.h"
#include <functional>
#include <memory>
#include <unordered_map>
#include <string>

namespace game {

class TcpConnection;
using TcpConnectionPtr = std::shared_ptr<class TcpConnection>;

// Routes ClientMessage oneof payload to registered handlers.
class MessageDispatcher {
public:
    // Handler receives: (connection, payload bytes, request_id if present)
    using HandlerFunc = std::function<void(
        const TcpConnectionPtr& conn,
        const game::messages::ClientMessage& msg)>;

    MessageDispatcher() = default;

    // Register a handler for a specific oneof field number.
    void registerHandler(int fieldNumber, HandlerFunc handler);

    // Deserialize and dispatch. payload does NOT include the 4-byte length prefix.
    void dispatch(const TcpConnectionPtr& conn,
                  const std::vector<uint8_t>& payload);

private:
    std::unordered_map<int, HandlerFunc> handlers_;
};

} // namespace game
```

Note: The server's TcpConnection is from Muduo (`mymuduo::TcpConnection`). We create a type alias.

- [ ] **Step 2: Create MessageDispatcher.cc**

```cpp
#include "common/MessageDispatcher.h"
#include <google/protobuf/message.h>

namespace game {

void MessageDispatcher::registerHandler(int fieldNumber, HandlerFunc handler) {
    handlers_[fieldNumber] = std::move(handler);
}

void MessageDispatcher::dispatch(const TcpConnectionPtr& conn,
                                  const std::vector<uint8_t>& payload) {
    game::messages::ClientMessage msg;
    if (!msg.ParseFromArray(payload.data(), static_cast<int>(payload.size()))) {
        // Failed to parse — could log
        return;
    }

    // Determine which oneof field is set
    auto desc = msg.GetDescriptor();
    auto ref = msg.GetReflection();
    auto oneofDesc = desc->FindOneofByName("payload");
    if (oneofDesc) {
        auto field = ref->GetOneofFieldDescriptor(msg, oneofDesc);
        if (field) {
            auto it = handlers_.find(field->number());
            if (it != handlers_.end()) {
                it->second(conn, msg);
            }
        }
    }
}

} // namespace game
```

---

## Task 14: Server — RoomService

**Files:**
- Create: `MuduoBaseGameServer/src/room/RoomService.h`
- Create: `MuduoBaseGameServer/src/room/RoomService.cc`

- [ ] **Step 1: Create RoomService.h**

```cpp
#pragma once
#include "room.pb.h"
#include "common.pb.h"
#include "messages.pb.h"
#include <memory>
#include <string>
#include <unordered_map>
#include <mutex>
#include <random>

namespace game {

class TcpConnection;
using TcpConnectionPtr = std::shared_ptr<class TcpConnection>;

struct PlayerSession {
    int64_t playerId;
    std::string nickname;
    int32_t seatId;
    bool isReady;
    bool isHost;
    bool isConnected;
    int32_t totalScore;
    TcpConnectionPtr conn;
    std::string sessionToken;
};

struct Room {
    int64_t roomId;
    std::string roomCode;
    int32_t maxPlayers;
    game::common::RoomStateType state;
    int64_t hostPlayerId;
    std::vector<std::shared_ptr<PlayerSession>> players;
};

class RoomService {
public:
    RoomService();

    // Event callbacks: send ServerMessage to all players in a room
    using BroadcastFunc = std::function<void(const TcpConnectionPtr&,
                                              const game::messages::ServerMessage&)>;

    void setBroadcastFunc(BroadcastFunc func) { broadcastFunc_ = std::move(func); }

    // Handle incoming requests
    void handleCreateRoom(const TcpConnectionPtr& conn,
                          const game::messages::ClientMessage& msg);
    void handleJoinRoom(const TcpConnectionPtr& conn,
                        const game::messages::ClientMessage& msg);
    void handleLeaveRoom(const TcpConnectionPtr& conn,
                         const game::messages::ClientMessage& msg);
    void handleReady(const TcpConnectionPtr& conn,
                     const game::messages::ClientMessage& msg);
    void handleStartGame(const TcpConnectionPtr& conn,
                         const game::messages::ClientMessage& msg);

private:
    void sendTo(const TcpConnectionPtr& conn,
                const game::messages::ServerMessage& msg);
    void broadcastToRoom(int64_t roomId,
                         const game::messages::ServerMessage& msg,
                         int64_t excludePlayerId = 0);
    std::string generateRoomCode();
    int64_t nextPlayerId();

    std::unordered_map<int64_t, std::shared_ptr<Room>> rooms_;
    std::unordered_map<int64_t, std::shared_ptr<Room>> connToRoom_; // conn addr -> room

    BroadcastFunc broadcastFunc_;
    std::mutex mutex_;
    std::mt19937 rng_;
    int64_t nextPlayerId_ = 1000;
    int64_t nextRoomId_ = 1;
};

} // namespace game
```

- [ ] **Step 2: Create RoomService.cc**

```cpp
#include "room/RoomService.h"
#include <sstream>

namespace game {

RoomService::RoomService() : rng_(std::random_device{}()) {}

// ── Helpers ──

std::string RoomService::generateRoomCode() {
    static const char chars[] = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    std::uniform_int_distribution<int> dist(0, sizeof(chars) - 2);
    std::string code(6, '\0');
    for (int i = 0; i < 6; ++i) {
        code[i] = chars[dist(rng_)];
    }
    return code;
}

int64_t RoomService::nextPlayerId() {
    return nextPlayerId_++;
}

void RoomService::sendTo(const TcpConnectionPtr& conn,
                          const game::messages::ServerMessage& msg) {
    if (broadcastFunc_) {
        broadcastFunc_(conn, msg);
    }
}

void RoomService::broadcastToRoom(int64_t roomId,
                                   const game::messages::ServerMessage& msg,
                                   int64_t excludePlayerId) {
    auto it = rooms_.find(roomId);
    if (it == rooms_.end()) return;
    for (auto& player : it->second->players) {
        if (player->playerId == excludePlayerId) continue;
        if (player->conn && player->isConnected) {
            sendTo(player->conn, msg);
        }
    }
}

// ── Handle CreateRoom ──

void RoomService::handleCreateRoom(const TcpConnectionPtr& conn,
                                    const game::messages::ClientMessage& msg) {
    const auto& req = msg.create_room_req();

    // Create player session
    auto player = std::make_shared<PlayerSession>();
    player->playerId = nextPlayerId();
    player->nickname = req.nickname();
    player->seatId = 0;
    player->isReady = false;
    player->isHost = true;
    player->isConnected = true;
    player->totalScore = 0;
    player->conn = conn;
    player->sessionToken = generateRoomCode() + generateRoomCode(); // 12-char token

    // Create room
    auto room = std::make_shared<Room>();
    room->roomId = nextRoomId_++;
    room->roomCode = generateRoomCode();
    room->maxPlayers = req.max_players();
    room->state = game::common::RoomStateType::ROOM_STATE_WAITING;
    room->hostPlayerId = player->playerId;
    room->players.push_back(player);

    {
        std::lock_guard<std::mutex> lock(mutex_);
        rooms_[room->roomId] = room;
        connToRoom_[player->playerId] = room;
    }

    // Response
    game::messages::ServerMessage rspMsg;
    rspMsg.set_server_seq(0); // TODO: global seq
    auto* rsp = rspMsg.mutable_create_room_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    rsp->set_room_id(room->roomId);
    rsp->set_room_code(room->roomCode);
    rsp->set_player_id(player->playerId);
    rsp->set_session_token(player->sessionToken);
    sendTo(conn, rspMsg);

    // Broadcast room state
    game::messages::ServerMessage notifyMsg;
    auto* notify = notifyMsg.mutable_room_state_notify();
    notify->set_room_id(room->roomId);
    auto* state = notify->mutable_room();
    state->set_room_id(room->roomId);
    state->set_room_code(room->roomCode);
    state->set_max_players(room->maxPlayers);
    state->set_state(game::common::RoomStateType::ROOM_STATE_WAITING);
    state->set_host_player_id(room->hostPlayerId);
    auto* pi = state->add_players();
    pi->set_player_id(player->playerId);
    pi->set_nickname(player->nickname);
    pi->set_seat_id(0);
    pi->set_is_ready(false);
    pi->set_is_host(true);
    pi->set_is_connected(true);
    pi->set_total_score(0);
    sendTo(conn, notifyMsg);
}

// ── Handle JoinRoom ──

void RoomService::handleJoinRoom(const TcpConnectionPtr& conn,
                                  const game::messages::ClientMessage& msg) {
    const auto& req = msg.join_room_req();

    // Find room by code
    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& kv : rooms_) {
            if (kv.second->roomCode == req.room_code()) {
                room = kv.second;
                break;
            }
        }
    }

    game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_join_room_rsp();
    rsp->set_request_id(req.request_id());

    if (!room) {
        rsp->mutable_error()->set_code(1001);
        rsp->mutable_error()->set_message("Room not found");
        sendTo(conn, rspMsg);
        return;
    }

    if (static_cast<int>(room->players.size()) >= room->maxPlayers) {
        rsp->mutable_error()->set_code(1002);
        rsp->mutable_error()->set_message("Room is full");
        sendTo(conn, rspMsg);
        return;
    }

    // Create player
    auto player = std::make_shared<PlayerSession>();
    player->playerId = nextPlayerId();
    player->nickname = req.nickname();
    player->seatId = static_cast<int32_t>(room->players.size());
    player->isReady = false;
    player->isHost = false;
    player->isConnected = true;
    player->totalScore = 0;
    player->conn = conn;
    player->sessionToken = generateRoomCode() + generateRoomCode();

    {
        std::lock_guard<std::mutex> lock(mutex_);
        room->players.push_back(player);
        connToRoom_[player->playerId] = room;
    }

    // Response
    rsp->mutable_error()->set_code(0);
    rsp->set_room_id(room->roomId);
    rsp->set_player_id(player->playerId);
    rsp->set_session_token(player->sessionToken);
    rsp->set_seat_id(player->seatId);
    sendTo(conn, rspMsg);

    // Notify others: PlayerJoinNotify
    game::messages::ServerMessage joinNotify;
    auto* jn = joinNotify.mutable_player_join_notify();
    jn->set_room_id(room->roomId);
    auto* jpi = jn->mutable_player();
    jpi->set_player_id(player->playerId);
    jpi->set_nickname(player->nickname);
    jpi->set_seat_id(player->seatId);
    jpi->set_is_ready(false);
    jpi->set_is_host(false);
    jpi->set_is_connected(true);
    jpi->set_total_score(0);
    broadcastToRoom(room->roomId, joinNotify, player->playerId);

    // Send full room state to new player
    game::messages::ServerMessage stateNotify;
    auto* sn = stateNotify.mutable_room_state_notify();
    sn->set_room_id(room->roomId);
    auto* rs = sn->mutable_room();
    rs->set_room_id(room->roomId);
    rs->set_room_code(room->roomCode);
    rs->set_max_players(room->maxPlayers);
    rs->set_state(room->state);
    rs->set_host_player_id(room->hostPlayerId);
    for (auto& p : room->players) {
        auto* ppi = rs->add_players();
        ppi->set_player_id(p->playerId);
        ppi->set_nickname(p->nickname);
        ppi->set_seat_id(p->seatId);
        ppi->set_is_ready(p->isReady);
        ppi->set_is_host(p->isHost);
        ppi->set_is_connected(p->isConnected);
        ppi->set_total_score(p->totalScore);
    }
    sendTo(conn, stateNotify);
}

// ── Handle LeaveRoom ──

void RoomService::handleLeaveRoom(const TcpConnectionPtr& conn,
                                   const game::messages::ClientMessage& msg) {
    const auto& req = msg.leave_room_req();
    std::shared_ptr<Room> room;
    int64_t playerId = req.player_id();

    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = connToRoom_.find(playerId);
        if (it == connToRoom_.end()) return;
        room = it->second;
    }

    // Remove player from room
    int64_t newHostId = 0;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto& players = room->players;
        players.erase(
            std::remove_if(players.begin(), players.end(),
                           [playerId](const auto& p) { return p->playerId == playerId; }),
            players.end());
        connToRoom_.erase(playerId);

        // If host left, assign new host
        if (playerId == room->hostPlayerId && !players.empty()) {
            players[0]->isHost = true;
            room->hostPlayerId = players[0]->playerId;
            newHostId = players[0]->playerId;
        }
    }

    // Response
    game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_leave_room_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);

    // Notify others
    game::messages::ServerMessage leaveNotify;
    auto* ln = leaveNotify.mutable_player_leave_notify();
    ln->set_room_id(room->roomId);
    ln->set_player_id(playerId);
    ln->set_new_host_player_id(newHostId);
    broadcastToRoom(room->roomId, leaveNotify);
}

// ── Handle Ready ──

void RoomService::handleReady(const TcpConnectionPtr& conn,
                               const game::messages::ClientMessage& msg) {
    const auto& req = msg.ready_req();
    std::shared_ptr<Room> room;

    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = connToRoom_.find(req.player_id());
        if (it == connToRoom_.end()) return;
        room = it->second;
    }

    // Update ready state
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& p : room->players) {
            if (p->playerId == req.player_id()) {
                p->isReady = req.is_ready();
                break;
            }
        }
    }

    // Response
    game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_ready_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    rsp->set_is_ready(req.is_ready());
    sendTo(conn, rspMsg);

    // Notify others
    game::messages::ServerMessage notify;
    auto* pn = notify.mutable_player_ready_notify();
    pn->set_room_id(room->roomId);
    pn->set_player_id(req.player_id());
    pn->set_is_ready(req.is_ready());
    broadcastToRoom(room->roomId, notify);
}

// ── Handle StartGame ──

void RoomService::handleStartGame(const TcpConnectionPtr& conn,
                                   const game::messages::ClientMessage& msg) {
    const auto& req = msg.start_game_req();
    std::shared_ptr<Room> room;

    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = connToRoom_.find(req.player_id());
        if (it == connToRoom_.end()) return;
        room = it->second;
    }

    game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_start_game_rsp();
    rsp->set_request_id(req.request_id());

    // Validate: must be host
    if (req.player_id() != room->hostPlayerId) {
        rsp->mutable_error()->set_code(2001);
        rsp->mutable_error()->set_message("Only host can start game");
        sendTo(conn, rspMsg);
        return;
    }

    // Validate: all players ready (min 2)
    int readyCount = 0;
    for (auto& p : room->players) {
        if (p->isReady) readyCount++;
    }
    if (readyCount < 2 || readyCount != static_cast<int>(room->players.size())) {
        rsp->mutable_error()->set_code(2002);
        rsp->mutable_error()->set_message("All players must be ready (min 2)");
        sendTo(conn, rspMsg);
        return;
    }

    // Start!
    room->state = game::common::RoomStateType::ROOM_STATE_PLAYING;

    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);

    // Notify room start
    game::messages::ServerMessage startNotify;
    auto* sn = startNotify.mutable_room_start_notify();
    sn->set_room_id(room->roomId);
    broadcastToRoom(room->roomId, startNotify);
}

} // namespace game
```

---

## Task 15: Server — GameServer Main Entry

**Files:**
- Create: `MuduoBaseGameServer/src/server/GameServer.cc`

- [ ] **Step 1: Create GameServer.cc**

```cpp
#include <mymuduo/TcpServer.h>
#include <mymuduo/EventLoop.h>
#include <mymuduo/InetAddress.h>
#include <mymuduo/logger.h>
#include <mymuduo/TcpConnection.h>

#include "common/MessageCodec.h"
#include "common/MessageDispatcher.h"
#include "room/RoomService.h"

#include <string>
#include <unordered_map>

using namespace mymuduo;

class GameServer {
public:
    GameServer(EventLoop* loop, const InetAddress& addr, const std::string& name)
        : server_(loop, addr, name), loop_(loop)
    {
        server_.setConnectionCallback(
            [this](const TcpConnectionPtr& conn) { onConnection(conn); });
        server_.setMessageCallback(
            [this](const TcpConnectionPtr& conn, Buffer* buf, Timestamp time) {
                onMessage(conn, buf, time);
            });
        server_.setThreadNum(4);

        // Register dispatcher handlers
        dispatcher_.registerHandler(
            10, // create_room_req field number
            [this](const TcpConnectionPtr& conn,
                   const game::messages::ClientMessage& msg) {
                roomService_.handleCreateRoom(conn, msg);
            });
        dispatcher_.registerHandler(
            11, // join_room_req field number
            [this](const TcpConnectionPtr& conn,
                   const game::messages::ClientMessage& msg) {
                roomService_.handleJoinRoom(conn, msg);
            });
        dispatcher_.registerHandler(
            12, // leave_room_req field number
            [this](const TcpConnectionPtr& conn,
                   const game::messages::ClientMessage& msg) {
                roomService_.handleLeaveRoom(conn, msg);
            });
        dispatcher_.registerHandler(
            13, // ready_req field number
            [this](const TcpConnectionPtr& conn,
                   const game::messages::ClientMessage& msg) {
                roomService_.handleReady(conn, msg);
            });
        dispatcher_.registerHandler(
            14, // start_game_req field number
            [this](const TcpConnectionPtr& conn,
                   const game::messages::ClientMessage& msg) {
                roomService_.handleStartGame(conn, msg);
            });

        // Set broadcast function
        roomService_.setBroadcastFunc(
            [this](const TcpConnectionPtr& conn,
                   const game::messages::ServerMessage& msg) {
                std::string payload;
                msg.SerializeToString(&payload);
                auto frame = game::MessageCodec::encode(payload);
                conn->send(frame);
            });
    }

    void start() { server_.start(); }

private:
    void onConnection(const TcpConnectionPtr& conn) {
        if (conn->connected()) {
            LOG_INFO("GameServer - connection UP : %s\n",
                     conn->peerAddress().toIpPort().c_str());
            // Create per-connection codec
            codecs_[conn.get()] = game::MessageCodec();
        } else {
            LOG_INFO("GameServer - connection DOWN : %s\n",
                     conn->peerAddress().toIpPort().c_str());
            codecs_.erase(conn.get());
        }
    }

    void onMessage(const TcpConnectionPtr& conn, Buffer* buf, Timestamp time) {
        std::string raw = buf->retrieveAllAsString();

        auto& codec = codecs_[conn.get()];
        codec.feedBytes(raw.data(), raw.size(),
            [this, &conn](const std::vector<uint8_t>& payload) {
                dispatcher_.dispatch(conn, payload);
            });
    }

    TcpServer server_;
    EventLoop* loop_;
    game::MessageDispatcher dispatcher_;
    game::RoomService roomService_;
    std::unordered_map<TcpConnection*, game::MessageCodec> codecs_;
};

int main(int argc, char* argv[]) {
    LOG_INFO("GameServer starting...\n");

    EventLoop loop;
    InetAddress addr(8888); // Listen on port 8888
    GameServer server(&loop, addr, "GameServer");
    server.start();

    LOG_INFO("GameServer listening on port 8888\n");
    loop.loop();

    return 0;
}
```

---

## Task 16: Server — Update CMakeLists.txt

**Files:**
- Modify: `MuduoBaseGameServer/CMakeLists.txt`

- [ ] **Step 1: Add GameServer target**

Read the existing CMakeLists.txt, then modify it:

```cmake
cmake_minimum_required(VERSION 3.5)
project(mymoduo)

# mymuduo 最终编译成so动态库，设置动态库的路径
set(LIBRARY_OUTPUT_PATH ${PROJECT_SOURCE_DIR}/lib)
# 设置调试信息
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -std=c++11")

aux_source_directory(. SRC_LIST)
# 编译动态库
add_library(mymuduo SHARED ${SRC_LIST})

# ============================================================
# GameServer target (NEW)
# ============================================================
set(PROTO_DIR "${CMAKE_SOURCE_DIR}/src/proto")

# Protobuf generated sources
set(PROTO_SRCS
    ${PROTO_DIR}/common.pb.cc
    ${PROTO_DIR}/messages.pb.cc
    ${PROTO_DIR}/room.pb.cc
    ${PROTO_DIR}/game.pb.cc
    ${PROTO_DIR}/sync.pb.cc
)

# Game server sources
set(GAME_SERVER_SRCS
    src/common/MessageCodec.cc
    src/common/MessageDispatcher.cc
    src/room/RoomService.cc
    src/server/GameServer.cc
    ${PROTO_SRCS}
)

add_executable(GameServer ${GAME_SERVER_SRCS})

# Include dirs
target_include_directories(GameServer PRIVATE
    ${CMAKE_SOURCE_DIR}
    ${PROTO_DIR}
)

# Link: muduo shared lib + protobuf
target_link_libraries(GameServer
    mymuduo
    protobuf
)

# RPATH so the executable finds libmymuduo.so at runtime
set_target_properties(GameServer PROPERTIES
    BUILD_RPATH "${CMAKE_SOURCE_DIR}/lib"
    INSTALL_RPATH "${CMAKE_SOURCE_DIR}/lib"
)
```

---

## Task 17: Build and Verify

**Files:** None (build verification)

- [ ] **Step 1: Build the server**

```bash
cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer"
mkdir -p build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Debug
make -j$(nproc)
```

Expected: `GameServer` binary compiled successfully in `build/`

- [ ] **Step 2: Start the server in background**

```bash
cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/build"
./GameServer &
```

Expected: Log output: "GameServer listening on port 8888"

- [ ] **Step 3: Quick TCP test with netcat**

```bash
# Send a test message (empty create_room_req with length prefix)
# The server should parse the frame and attempt to process it
echo "test" | nc -w 1 127.0.0.1 8888
```

Expected: No crash, log shows parsing attempt

- [ ] **Step 4: Kill test server**

```bash
pkill GameServer
```

---

## Verification Checklist

After all tasks complete:

- [ ] `protoc --csharp_out` generates 5 .cs files without error
- [ ] `protoc --cpp_out` generates 10 .pb.h/.pb.cc files without error
- [ ] `Google.Protobuf.dll` in `Assets/Plugins/`
- [ ] Unity project compiles (open in Unity Editor, check Console for errors)
- [ ] `cmake .. && make` builds GameServer without error
- [ ] `./GameServer` starts and listens on port 8888
- [ ] TCP client can connect to the server (ConnectAsync succeeds)
- [ ] CreateRoomReq → CreateRoomRsp round-trip works
- [ ] JoinRoomReq → JoinRoomRsp + PlayerJoinNotify works
- [ ] ReadyReq → ReadyRsp + PlayerReadyNotify works
- [ ] StartGameReq → StartGameRsp + RoomStartNotify works (all ready)
- [ ] Error codes correctly returned and displayed
