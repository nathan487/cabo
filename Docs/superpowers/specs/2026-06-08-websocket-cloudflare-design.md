# WebSocket + Cloudflare Tunnel 协议升级设计

**日期**: 2026-06-08  
**状态**: 设计中  
**目标**: 将 Cabo 游戏从 raw TCP + protobuf 自定义帧升级为 WebSocket 协议，并通过 Cloudflare Tunnel 实现公网访问。

---

## 1. 背景与动机

当前 Cabo 游戏使用 raw TCP + `[4字节大端长度][protobuf载荷]` 自定义帧协议。该协议难以做内网穿透，无法让异地玩家通过公网连接。

升级为 WebSocket 协议后：
- 可以通过 Cloudflare Tunnel（免费 `trycloudflare.com` 临时域名）直接暴露到公网
- 客户端无需安装任何额外软件（如 Tailscale、ngrok 客户端）
- WebSocket 是 HTTP 兼容协议，穿透防火墙/代理天然友好

## 2. 整体架构

```
当前:  Unity C# ──raw TCP──▶ muduo C++ Server
       自定义帧 [4B len][protobuf]

目标:  Unity C# ──WSS──▶ cloudflared ──▶ muduo C++ Server
       WS Binary Frame      (tunnel)       WS Binary Frame
       内嵌 protobuf                       内嵌 protobuf
```

### 2.1 cloudflared 部署

服务端主机运行：
```bash
cloudflared tunnel --url http://localhost:8888
```
每次启动生成随机 `https://xxx.trycloudflare.com` 域名，Unity 客户端连接 `wss://xxx.trycloudflare.com`。Cloudflare 自动提供 TLS 终止。

### 2.2 约束条件

- 玩家数 ≤ 20
- 临时域名每次随机生成（免费）
- Unity 客户端（非 WebGL）
- 所有 protobuf 协议定义不变

---

## 3. C++ 服务端改造

### 3.1 新增文件

| 文件 | 用途 | 预估行数 |
|---|---|---|
| `src/common/WebSocketCodec.h` | WebSocket 握手 + 帧编解码头文件 | ~50行 |
| `src/common/WebSocketCodec.cc` | WebSocket 握手 + 帧编解码实现 | ~200行 |

### 3.2 WebSocketCodec 设计

`WebSocketCodec` 是**有状态**的编解码器，每个连接一个实例。状态机：

```
                    ┌──────────────────────┐
                    │   HANDSHAKE 状态     │
                    │   解析 HTTP Upgrade   │
                    │   验证 WebSocket 头   │
                    │   发送 101 响应       │
                    └──────────┬───────────┘
                               │ 握手完成
                               ▼
                    ┌──────────────────────┐
                    │   FRAME 状态         │
                    │   解析 WS 帧头       │
                    │   提取 payload       │
                    │   回调 onMessage()   │
                    │   编码发送帧          │
                    └──────────────────────┘
```

#### 握手阶段

客户端发送的 HTTP Upgrade 请求形状：
```http
GET / HTTP/1.1
Host: localhost:8888
Upgrade: websocket
Connection: Upgrade
Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==
Sec-WebSocket-Version: 13
```

服务端：
1. 在接收缓冲区中查找 `\r\n\r\n` 标记（HTTP 头结束）
2. 提取 `Sec-WebSocket-Key`
3. 按 RFC 6455 计算 accept key：`base64(sha1(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))`
4. 返回：
```http
HTTP/1.1 101 Switching Protocols
Upgrade: websocket
Connection: Upgrade
Sec-WebSocket-Accept: <computed_accept_key>

```
5. 状态切换为 FRAME

#### 帧解码（接收）

WebSocket 帧格式（RFC 6455 §5.2）：
```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-------+-+-------------+-------------------------------+
|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
|I|S|S|S|  (4)  |A|     (7)     |             (0/16/64)        |
|N|V|V|V|       |S|             |                               |
| |1|2|3|       |K|             |                               |
+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - -+
|     Extended payload length continued, if payload len == 126   |
+ - - - - - - - - - - - - - - - +-------------------------------+
|                               |  Masking-key (if MASK set)    |
+-------------------------------+-------------------------------+
| Masking-key (continued)       |    Payload Data               |
+-------------------------------- - - - - - - - - - - - - - - -+
:                     Payload Data continued ...                 :
+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -+
```

关键参数：
- opcode: 只处理 `0x2`（Binary Frame），收到 `0x8`（Close）时关闭连接，`0x9`（Ping）回复 `0xA`（Pong）
- MASK: 客户端发送的帧 **必须** mask（RFC 6455 要求）
- Payload len: 支持 7位 / 7+16位 / 7+64位 三种长度

解码流程：
1. 读取前 2 字节，获取 FIN、opcode、MASK、base payload length
2. 按需读取 extended payload length
3. 读取 4 字节 mask key
4. 读取 payload，逐字节 XOR mask key 解码
5. 回调 `onMessage(payload)` — 传递裸 protobuf 字节

#### 帧编码（发送）

服务端发送给客户端 **不需要 mask**（RFC 6455 规定仅客户端帧需 mask）。格式简化：

```
[0x82] [payload len (1/3/9 bytes)] [payload bytes]
```

- `0x82` = FIN(0x80) | Binary Opcode(0x2)
- Payload len 编码：<126 直接用，126-65535 用 2 字节扩展，≥65536 用 8 字节扩展

### 3.3 GameServer.cc 改动

每个连接的 `MessageCodec` 替换为 `WebSocketCodec`（仅用于接收解码+握手状态）：

```cpp
// 之前
std::unordered_map<const TcpConnection*, game::MessageCodec> codecs_;

// 之后
std::unordered_map<const TcpConnection*, game::WebSocketCodec> codecs_;
```

`onMessage` 回调改为：
```cpp
void onMessage(const TcpConnectionPtr& conn, Buffer* buf, Timestamp) {
    std::string raw = buf->retrieveAllAsString();
    auto it = codecs_.find(conn.get());
    if (it == codecs_.end()) return;

    it->second.feedBytes(raw.data(), raw.size(),
        [this, conn](const std::vector<uint8_t>& payload) {
            dispatcher_.dispatch(conn, payload);  // 不变
        });
}
```

### 3.4 发送端改动（极简）

`WebSocketCodec::encode()` 是**静态方法**（服务端→客户端帧无需 mask，编码完全无状态）。因此 `RoomService::sendTo()` 和 `GameService::sendToPlayer()` 只需改一行：

```cpp
// 之前
auto frame = MessageCodec::encode(payload);
// 之后
auto frame = WebSocketCodec::encode(payload);
```

GameServer 中的 send lambda **完全不变**：
```cpp
roomService_.setSendFunc(
    [](const TcpConnectionPtr& conn, const std::string& framedData) {
        conn->send(framedData);  // 不变！framedData 现在由 WebSocketCodec 编码
    });
```

### 3.5 不改动的文件

| 文件 | 原因 |
|---|---|
| `TcpServer.h/cc` | TCP 层完全不变 |
| `TcpConnection.h/cc` | 连接管理不变 |
| `Buffer.h/cc` | 不变 |
| `Acceptor.h/cc` | 不变 |
| `EventLoop*` | 事件循环不变 |
| `MessageDispatcher.h/cc` | 消息路由不变 |
| `RoomService.h/cc` | 业务逻辑不变 |
| `GameService.h/cc` | 业务逻辑不变 |
| `MessageCodec.h/cc` | 可保留或移除（不再使用） |
| 所有 proto 文件 | 协议定义不变 |

---

## 4. Unity C# 客户端改造

### 4.1 新增/修改文件

| 文件 | 动作 | 预估行数 |
|---|---|---|
| `Network/WebSocketNetworkClient.cs` | 新增，替换 `TcpNetworkClient` | ~120行 |
| `Core/NetworkGateway.cs` | 修改，使用 `WebSocketNetworkClient` | ~30行改动 |

### 4.2 WebSocketNetworkClient 设计

使用 .NET 内置 `System.Net.WebSockets.ClientWebSocket`（Unity 2018.3+ .NET 4.x 支持）。

```csharp
public sealed class WebSocketNetworkClient : IDisposable
{
    private ClientWebSocket ws;
    private readonly string url;  // e.g. "wss://xxx.trycloudflare.com"
    private CancellationTokenSource receiveCts;
    
    public event Action Connected;
    public event Action Disconnected;
    public event Action<byte[]> DataReceived;
    public event Action<string> ErrorOccurred;
    
    public async Task ConnectAsync();   // ws.ConnectAsync()
    public void Disconnect();            // ws.CloseAsync()
    public Task SendAsync(byte[] data);  // ws.SendAsync(..., WebSocketMessageType.Binary)
    private async Task ReceiveLoop();    // ws.ReceiveAsync() loop, 处理分片
}
```

关键细节：
- 发送：`new ArraySegment<byte>(data)` + `WebSocketMessageType.Binary`
- 接收：循环 `ReceiveAsync`，累积分片帧直到 `EndOfMessage = true`，然后回调 `DataReceived`
- Ping/Pong：`ClientWebSocket` 内置 `KeepAliveInterval` 自动处理
- 重连：`CancellationTokenSource` + `ConnectAsync` 循环

### 4.3 NetworkGateway 改动

- `TcpClient` 字段替换为 `WebSocketNetworkClient`
- `ConnectAsync(string host, int port)` 改为 `ConnectAsync(string url)` — 接受完整 WebSocket URL
- `Send()` 方法改为 `await ws.SendAsync(frame, WebSocketMessageType.Binary, ...)`
- `MessageCodec` 去掉 `[4B len]` 帧编码部分，**只保留 protobuf 序列化**（因为 WebSocket 已提供消息边界）
- 剩余所有 `SendXxx()` 方法、`DrainMessages()`、handler 注册完全不变

---

## 5. 消息流示例

以"抽牌"为例：

```
1. Unity Client:
   ClientMessage { draw_card_req { playerId:1, roomId:42 } }
   → protobuf 序列化 → 24 bytes payload
   → ClientWebSocket.SendAsync(payload, Binary) 
   → WebSocket 自动添加 frame header + mask
   → TLS → cloudflared → 服务端

2. C++ Server:
   TcpConnection 收到 raw bytes
   → WebSocketCodec::feedBytes()
   → 解析 WS frame header, unmask payload
   → 提取 24 bytes protobuf payload
   → MessageDispatcher::dispatch() → GameService::handleDrawCard()

3. C++ Server 响应:
   GameService 构建 ServerMessage { draw_card_rsp { ... } }
   → protobuf 序列化 → payload
   → WebSocketCodec::encode(payload) → [0x82][len][payload]
   → conn->send()

4. Unity Client:
   WebSocketNetworkClient.ReceiveLoop()
   → ClientWebSocket.ReceiveAsync() → Binary frame
   → MessageCodec.Decode(payload) → ServerMessage
   → 入队 → DrainMessages() → MessageDispatcher.Dispatch()
   → UI 更新
```

---

## 6. 错误处理

### 6.1 WebSocket 握手失败
- 服务端在 HANDSHAKE 状态收到非法请求 → 返回 400 Bad Request（纯文本），关闭连接
- 客户端 `ConnectAsync` 抛异常 → `ErrorOccurred` 事件 → UI 提示

### 6.2 连接断开
- 服务端收到 Close Frame（opcode=0x8）→ 发送 Close 回复 → 关闭连接
- 客户端 `ReceiveAsync` 返回 Close 消息 → `Disconnected` 事件
- 两端的重连机制不变（客户端已有 `Reconnecting` 状态）

### 6.3 帧错误
- 非法 opcode → 发送 Close Frame (1002 Protocol Error) → 关闭
- Payload 超大（>10MB） → 同上
- Mask 缺失（客户端帧必须mask） → 同上

---

## 7. Cloudflare Tunnel 部署

### 7.1 一次性安装
```bash
# Ubuntu/WSL
wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
sudo dpkg -i cloudflared-linux-amd64.deb
```

### 7.2 每次启动
```bash
# 终端1：启动游戏服务器
cd MuduoBaseGameServer/build
LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer 8888

# 终端2：启动 Cloudflare Tunnel
cloudflared tunnel --url http://localhost:8888
```

输出示例：
```
2026-06-08T12:00:00Z INF Requesting new quick Tunnel on trycloudflare.com...
2026-06-08T12:00:05Z INF +--------------------------------------------------------------------------------------------+
2026-06-08T12:00:05Z INF |  Your quick Tunnel has been created! Visit it at (it may take some time to be reachable):  |
2026-06-08T12:00:05Z INF |  https://random-words.trycloudflare.com                                                   |
2026-06-08T12:00:05Z INF +--------------------------------------------------------------------------------------------+
```

### 7.3 客户端使用
将 `https://random-words.trycloudflare.com` 填入 Unity 客户端连接框，程序自动转换为 `wss://random-words.trycloudflare.com`。

---

## 8. 不改动的部分（完整列表）

- ✅ muduo 网络库全部 (13 个文件)
- ✅ 所有 protobuf 协议定义 (5 个 .proto)
- ✅ `MessageDispatcher`（C++ 和 C# 两边）
- ✅ `RoomService`、`GameService`（C++）
- ✅ Unity UI 层（`UIManager`、`RoomPanel`、`GameTablePanel`）
- ✅ Unity 游戏状态管理（`GameFlow`、`GameState`、`GameBootstrap`）
- ✅ Unity `RequestTracker`（请求追踪）
- ✅ `CMakeLists.txt`（依赖不变）

---

## 9. 风险与缓解

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| `ClientWebSocket` 在 Unity IL2CPP 下有问题 | 低 | 高 | 先在 Editor 测试，有必要时降级到 `WebSocketSharp`（纯 C# 实现） |
| `cloudflared` 延迟过高 | 低 | 中 | 实测；如不可接受可改用国内 frp 替代 |
| 大消息分片 | 低 | 低 | WebSocket 帧支持分片，`ClientWebSocket` 自动重组 |

---

## 10. 测试验证

1. **单元测试**: 服务端 `WebSocketCodec` 握手编解码测试（`mytest/` 目录已有测试框架）
2. **集成测试**: 启动服务端 + cloudflared，Unity 客户端连接 → 创建房间 → 抽牌完整流程
3. **断线重连**: 杀死 cloudflared 再重启，确认客户端重连
4. **多玩家**: 2 个 Unity 客户端同时在线，完成一局游戏
