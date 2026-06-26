# Cabo Multiplayer Card Game

多人在线 Cabo/Kabo 卡牌游戏。当前项目已经从早期“C++ 服务端 + CLI 客户端”的原型，演进为“C++ 权威服务端 + Unity 客户端 + Protobuf 协议 + WebSocket 网络层”的完整多人游戏项目。

本文档按当前 `codex/special-effects` 分支和 `main` 分支提交历史更新，日期为 2026-06-25。

## Current Status

| Module | Current state |
| --- | --- |
| Server | C++ 权威服务端已实现房间、游戏、结算、断线重连、房间浏览/申请/邀请等核心逻辑 |
| Unity Client | 已实现主流程 UI、房间大厅、游戏交互、动画、WebSocket 网络、重连恢复和移动端/Windows 适配 |
| CLI Client | 早期调试/参考客户端，保留用于协议和服务端调试，不再是主要交付形态 |
| Protocol | Protobuf 消息 envelope；当前主链路为 WebSocket binary frame 承载 Protobuf payload |
| Transport | 本地 `ws://127.0.0.1:8888`；可配合 Cloudflare Tunnel 走公网 `wss://...` |
| Room model | 支持 2-6 人房间，包含房主、准备、开始、房间列表、申请加入、邀请等流程 |

## Tech Stack

| Layer | Technology | Role |
| --- | --- | --- |
| Server core | C++ / CMake / Muduo-style Reactor | 处理 TCP 连接、事件循环、房间与游戏权威状态 |
| Network | epoll / eventfd / non-blocking socket / WebSocket / Protobuf | 多连接 IO、跨线程唤醒、二进制协议编解码 |
| Client | Unity C# / `ClientWebSocket` | 游戏 UI、交互动画、网络收发、断线重连 |
| Serialization | Protocol Buffers | 统一客户端与服务端消息结构 |
| Tests | C++ regression tests / Unity EditMode tests | 覆盖协议、并发、鉴权、状态恢复、UI 状态机等问题 |

## Current Architecture at a Glance

```text
Unity Client
  ├─ WebSocketNetworkClient
  │   ├─ background ReceiveLoop
  │   ├─ sendLock: serialize ClientWebSocket sends
  │   └─ stateLock: synchronize connect / disconnect / reconnect state
  └─ NetworkGateway
      └─ main-thread DrainMessages -> GameFlow / UI

WebSocket binary protobuf payload

C++ Server
  ├─ GameServer
  │   ├─ TcpServer / Reactor IO threads
  │   ├─ WebSocketCodec per connection
  │   └─ MessageDispatcher
  ├─ RoomService
  │   ├─ lobby / room / ready / access request / invite
  │   └─ reconnect session mapping
  └─ GameService
      ├─ authoritative GameRoom state
      ├─ per-room state mutex
      └─ reconnect StateSync snapshot
```

## Git-derived Implementation Highlights

这部分来自提交历史，适合后续写简历或项目介绍：

- 网络协议升级：从早期 TCP length-prefix Protobuf 原型，升级到 RFC6455 WebSocket + Protobuf binary payload，解决 Unity/公网/Cloudflare Tunnel 访问问题。
- TCP 长连接健壮性：在早期 length-prefix 协议中修复 partial send、接收超时、异常长度头阻塞缓冲区、通知消息与响应消息穿插导致的状态卡住等问题。
- 服务端权威状态：服务端不信任客户端传入的 `player_id`，通过连接与玩家会话绑定校验，阻断伪造 ready/start/draw/discard/skill 等操作。
- 并发与生命周期修复：为房间状态、游戏状态、WebSocket codec map、Unity WebSocket 状态机等关键共享状态补锁，修复竞态、死锁和断线生命周期问题。
- 断线重连：断线后保留玩家会话与游戏状态，重连时返回 `StateSyncNotify` 快照，恢复当前回合、待处理抽牌/技能/弃牌等状态。
- 性能优化：广播消息从“每个接收者重复编码 frame”优化为“一次编码，多连接复用发送”；临时发送 buffer 使用线程本地池复用，减少高频广播分配。
- 稳定性修复：socket 写入使用 `MSG_NOSIGNAL` 避免断线写触发进程信号；异步发送/关闭捕获 `shared_from_this()` 避免连接对象生命周期悬空。

## Branch Audit Notes

截至 2026-06-25：

- `main` 在与当前分支分叉后主要只有 `.gitignore` 类维护提交。
- `codex/special-effects` 在分叉后继续包含 Unity 表现层、房间浏览、断线重连、动画和跨平台适配等提交。
- 多数后台/系统能力来自两条分支共有历史中的服务端提交，包括 `544c7e9` 这类早期 P0 网络/状态一致性修复；新的重连与房间浏览流程来自 `codex/special-effects` 分支。

## Documentation Entry Points

- 文档索引：[README.md](README.md)
- 强案例排序：[HIGH_IMPACT_BACKEND_PROBLEM_CASES.md](HIGH_IMPACT_BACKEND_PROBLEM_CASES.md)
- 简历成稿：[TENCENT_BACKEND_RESUME_DRAFT.md](TENCENT_BACKEND_RESUME_DRAFT.md)
- 完整问题日志：[BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md)

更完整的“问题-原因-解决-验证-简历表述”见 [BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md)。
