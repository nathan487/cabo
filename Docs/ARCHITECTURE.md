# Architecture

本文档描述当前项目实现，不再沿用早期“TCP + CLI Client only”的架构说明。当前主路径是 Unity 客户端通过 WebSocket binary frame 发送 Protobuf 消息到 C++ 权威服务端。

## Runtime Topology

```text
Unity Client
  |
  |  WebSocket binary frame
  |  payload = serialized Protobuf ClientMessage
  v
C++ GameServer
  |
  +-- WebSocketCodec
  |     - HTTP Upgrade handshake
  |     - mask / frame validation
  |     - ping / pong / close
  |     - fragmented binary payload reassembly
  |
  +-- MessageDispatcher
  |
  +-- RoomService
  |     - lobby player identity
  |     - room create / join / leave / ready / start
  |     - room browser list
  |     - apply / approve / reject / invite access flow
  |     - reconnect session lookup
  |
  +-- GameService
        - deck / turn / draw / discard / skill / scoring
        - hidden-information filtering
        - game-over / restart checks
        - reconnect state snapshot
```

## Server Components

| Component | Responsibility |
| --- | --- |
| `GameServer` | 网络入口，维护连接到 `WebSocketCodec` 的映射，完成 frame 解码后分发 Protobuf 消息 |
| `WebSocketCodec` | WebSocket 握手、mask 校验、帧解析、控制帧处理、二进制 payload 编解码 |
| `MessageDispatcher` | 将 Protobuf `ClientMessage` 路由到房间服务或游戏服务 |
| `RoomService` | 管理大厅、房间、准备状态、房间访问申请/邀请、玩家与连接的绑定、重连会话 |
| `GameService` | 管理权威游戏状态、回合推进、技能效果、计分、隐藏信息过滤、断线状态同步 |
| `Buffer` / `TcpConnection` | 非阻塞 socket 读写、跨线程发送队列、连接生命周期管理 |

## Network Layer

当前生产路径：

```text
[WebSocket frame][Protobuf ClientMessage / ServerMessage]
```

早期 CLI/debug 路径仍保留 length-prefixed Protobuf：

```text
[4-byte big-endian length][Protobuf message]
```

这条早期路径曾暴露出 partial send、半包/粘包、异常长度头阻塞接收缓冲区、响应与通知穿插到达等问题；这些修复记录现在主要作为网络工程经验和回归参考保留。

WebSocket 层解决了几个早期 TCP 原型无法覆盖的问题：

- Unity `ClientWebSocket` 可直接接入；
- 公网访问可以通过 Cloudflare Tunnel 转成 `wss://`；
- 浏览器/移动端生态更友好；
- 服务端仍复用原有 TCP/Reactor 基础设施，不需要重写业务协议。

## Threading and Synchronization

服务端基于 Muduo-style Reactor：

- IO 事件由事件循环线程处理；
- 跨线程任务通过 `runInLoop` / `queueInLoop` 投递；
- `eventfd` 用于唤醒事件循环；
- socket 使用非阻塞读写。

当前关键同步点：

| Area | Synchronization |
| --- | --- |
| `GameServer::codecs_` | 使用 mutex 保护连接到 codec 的 map，并在读消息时复制 `shared_ptr`，避免 connection down 与 frame 解码并发 |
| `RoomService` | 使用 recursive mutex 保护房间、玩家、会话、房间访问记录等共享状态 |
| `GameService` | 使用全局 mutex 与 `GameRoom::stateMutex` 保护房间查找和单局游戏状态 |
| Game finish callback | 释放房间锁后再回调 RoomService，避免服务间锁顺序导致死锁 |
| Unity `WebSocketNetworkClient` | `sendLock` 串行化 `ClientWebSocket.SendAsync`；`stateLock` 保护连接/断开/重连状态切换 |
| Unity `NetworkGateway` | 使用线程安全队列接收后台网络消息，并在 Unity 主线程 `DrainMessages` |

## Connection and Session Model

服务端不再信任客户端请求中的 `player_id`。当前模型是：

1. 连接建立后，玩家通过大厅/房间流程绑定身份；
2. 服务端记录玩家对应的连接对象；
3. 收到 ready/start/leave/draw/discard/skill 等请求时，先验证“发请求的连接是否就是该玩家当前连接”；
4. 断线时清空当前连接并标记 offline，但保留短时间可恢复的 session；
5. 重连成功后重新绑定连接，并发送 `StateSyncNotify` 快照恢复客户端状态。

这条设计同时解决了安全性、断线恢复和多客户端串线的问题。

## Game State Lifecycle

```text
Lobby
  -> Room created
  -> Players join / ready
  -> Host starts game
  -> GameService snapshots room players
  -> Active round
      -> turn / draw / discard / skill
      -> hidden-information filtered broadcast
      -> disconnect -> offline marker + reconnect window
  -> final round / scoring
  -> restart allowed only when round is not active
```

关键点：

- 开始游戏时使用房间玩家快照，避免房间状态在启动过程中变化导致游戏人数不一致；
- 活跃回合中禁止重启，避免 deck、turn index、round state 被重置；
- 结算和最终轮断线计数由服务端统一处理；
- 玩家可见信息按接收者过滤，例如对手不能看到当前玩家刚抽到的牌面值。

## Send Path Optimization

广播发送当前采用两层优化：

1. 对同一条 `ServerMessage` 只编码一次 WebSocket frame；
2. 使用临时发送 buffer 池复用字符串容量，降低高频广播时的内存分配压力。

因此多人房间广播不再是“每个玩家重复序列化 + 重复 frame 编码”，而是“业务消息生成一次，frame 编码一次，再发送给多个连接”。

## Testing Strategy

项目中的回归测试覆盖过以下类型：

- WebSocket 握手、mask 校验、ping/pong/close、分片重组；
- 伪造连接请求拦截；
- 隐藏信息过滤；
- 技能请求合法性校验；
- 房间/游戏状态并发锁；
- 断线重连后的状态恢复；
- 广播 frame 复用和 buffer 池复用；
- Unity 侧重连状态机与 UI 行为。

更具体的问题记录见 [BUG_LOG.md](BUG_LOG.md) 和 [BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md)。
