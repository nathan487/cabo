# Protobuf + TCP 链路 MVP 设计文档

> 日期：2026-06-03
> 范围：打通 Unity 客户端 ↔ C++ Muduo 服务端的 TCP + protobuf 通信链路

## 1. 目标

- 客户端能通过 TCP 连接到服务端，按 `[4byte_len][proto]` 帧格式收发消息
- 实现房间创建/加入/离开/准备完整闭环
- 端到端可验证完整流程

## 2. 架构

```
Unity Client (C#)                          Server (C++/Muduo)
─────────────────                          ──────────────────
UI Layer                                    TcpServer
  │                                           │
App Controller (RoomClientController)         MessageCodec (帧解析)
  │                                           │
ProtoGateway (IBackendGateway)               MessageDispatcher (oneof 路由)
  │                                           │
TcpNetworkClient + MessageCodec              RoomService
  │                                           │
Generated Protobuf (C#)                     GameService [stub]
     │                                         │
     └──────── TCP [len][proto] ───────────────┘
```

## 3. 协议格式

```
[4 bytes big-endian length][protobuf serialized bytes]
```

- 上行：`ClientMessage`（oneof: create_room_req, join_room_req, ...）
- 下行：`ServerMessage`（oneof: create_room_rsp, room_state_notify, ...）

## 4. 客户端组件

### 4.1 新增文件

| 文件 | 作用 |
|------|------|
| `Assets/Scripts/Proto/Generated/*.cs` | protoc 生成的 C# protobuf 类 |
| `Assets/Scripts/Network/TcpNetworkClient.cs` | TCP Socket 连接、收发、状态管理 |
| `Assets/Scripts/Network/MessageCodec.cs` | `[len][proto_bytes]` ↔ ServerMessage/ClientMessage |
| `Assets/Scripts/Network/MessageDispatcher.cs` | ServerMessage oneof → 注册的 handler 分发 |
| `Assets/Scripts/Network/RequestTracker.cs` | request_id → TCS，超时处理 |
| `Assets/Scripts/ClientCore/Network/ProtoGateway.cs` | IBackendGateway 真实实现 |
| `Assets/Scripts/ClientCore/Game/GameClientController.cs` | 对局流程控制器 |

### 4.2 修改文件

| 文件 | 修改 |
|------|------|
| `ClientAppBootstrap.cs` | 支持选择 Proto 模式 |
| `RoomClientController.cs` | 不需改，通过 IBackendGateway 多态 |

## 5. 服务端组件（C++）

### 5.1 新增文件

| 文件 | 作用 |
|------|------|
| `src/common/MessageCodec.h/.cc` | [len][proto] 帧编解码 |
| `src/common/MessageDispatcher.h/.cc` | ClientMessage oneof → handler 路由 |
| `src/room/RoomService.h/.cc` | 房间创建/加入/离开/准备 |
| `src/server/GameServer.cc` | 主入口，注册 handler |

### 5.2 房间管理（内存）

- `unordered_map<int64_t, Room>`：房间
- `unordered_map<int64_t, PlayerSession>`：玩家会话
- 房间码：6位字母数字随机码
- 准备：所有玩家准备后房主可开始

## 6. 关键流程

### 6.1 客户端发送

```
Controller.SendRequest(msg) → ProtoGateway.Send(ClientMessage)
    → ClientMessage.seq++ → MessageCodec.Encode → byte[]
    → TcpNetworkClient.Send(bytes) → TCP
```

### 6.2 客户端接收

```
TCP → TcpNetworkClient.OnBytesReceived → MessageCodec.TryDecode
    → ServerMessage → MessageDispatcher.Dispatch(oneof)
    → registered handler(rsp/notify) → Controller.UpdateState
    → UI Event → UI 刷新
```

### 6.3 房间完整流程

```
Client A: CreateRoom → Server: 创建房间, 分配 room_code
    → Client A: CreateRoomRsp (room_code, player_id, session_token)

Client B: JoinRoom(room_code) → Server: 加入
    → Client B: JoinRoomRsp
    → 广播 PlayerJoinNotify 给房间所有人

Client A/B: Ready → Server: 更新状态
    → 广播 PlayerReadyNotify

Client A (房主): StartGame → Server: 校验全员准备
    → 广播 RoomStartNotify
```

## 7. 错误处理

| 场景 | 客户端处理 | 服务端处理 |
|------|-----------|-----------|
| 发送时未连接 | 入队或立即回调失败 | - |
| 请求超时（5秒） | RequestTracker 回调 timeout error | - |
| server_seq 重复 | Log warning，跳过 | - |
| 未知 oneof | Log warning，不崩溃 | Log warning，不崩溃 |
| TCP 断线 | 状态→Reconnecting | 清理连接，通知房间 |
| 房间码无效 | - | 返回 ErrorInfo(code, msg) |

## 8. 暂缓（不在此 MVP）

- 游戏回合/技能/计分（服务端 + 客户端 GameClientController 细节）
- WebSocket 传输层
- 重连恢复（ReconnectReq/StateSyncNotify）
- 心跳保活
- 5-6 人扩展
- 掉线托管

## 9. 验收清单

- [ ] protoc 生成 C# 代码，Unity 编译通过
- [ ] 服务端编译运行，监听端口
- [ ] 客户端连接服务端成功
- [ ] CreateRoom → 收到 CreateRoomRsp（含 room_code）
- [ ] JoinRoom → 两个客户端在同一房间，收到 PlayerJoinNotify
- [ ] Ready → 收到 PlayerReadyNotify
- [ ] 全员准备 + 房主 StartGame → 收到 RoomStartNotify
- [ ] 错误码正确返回和展示
