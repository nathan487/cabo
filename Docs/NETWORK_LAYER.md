# Network Layer Reference

本文档描述当前 Unity 客户端与 C++ 服务端之间的主网络链路。更新时间：2026-06-25。

## Current Transport

当前主链路是：

```text
WebSocket binary message
  -> payload: serialized protobuf ClientMessage / ServerMessage
```

早期 CLI/debug 链路仍保留：

```text
[4-byte big-endian length][protobuf payload]
```

差异说明：

- Unity 主客户端使用 WebSocket，不再在 Unity 网络层使用自定义 4-byte length framing；
- Protobuf schema 和业务消息结构继续复用；
- CLI 客户端和部分调试工具仍可参考 legacy TCP codec；
- 本地默认 URL：`ws://127.0.0.1:8888`；
- 公网临时访问可使用 Cloudflare Tunnel，将 `https://...trycloudflare.com` 作为 `wss://...trycloudflare.com` 连接。

## Legacy TCP Lessons Worth Keeping

虽然 Unity 主链路已经迁移到 WebSocket，但早期 TCP length-prefix 客户端留下了几类很有价值的网络问题修复记录：

- `send` 可能只写出部分数据，因此 `sendRaw` 需要循环发送完整 frame；
- `recv` 需要超时控制，避免交互流程永久阻塞；
- length-prefix decoder 必须区分“半包，继续等待”和“长度头损坏，应该丢弃/清理”，否则坏长度头会让接收缓冲区永久无法推进；
- 长连接上的业务响应和广播通知可能穿插到达，客户端不能假设“请求后的第一个包一定是响应”；
- waiting room / ready / start 这类状态同步应以服务端权威广播为准，并主动 drain pending messages。

这些问题主要对应 `1556e33`, `436f641`, `544c7e9`, `581896e`, `420bac6`, `65516a4`, `aa3b288`, `bddb5cb`。整理简历时，可以把它们概括为“TCP 长连接消息处理健壮性修复”。

## Server Implementation

`MuduoBaseGameServer/src/common/WebSocketCodec.*` 负责 WebSocket 协议层：

- HTTP Upgrade handshake；
- `Sec-WebSocket-Accept` 生成；
- client-to-server mask 校验；
- binary frame 解码与 server-to-client frame 编码；
- ping / pong / close 控制帧；
- continuation frame / fragmented binary payload 重组；
- partial TCP read 场景下的增量解析。

`GameServer` 为每个连接维护一个 `WebSocketCodec`，解出完整 protobuf payload 后交给 dispatcher 和业务服务。

并发修复后的关键点：

- `codecs_` map 由 mutex 保护；
- codec 对象使用 `shared_ptr` 管理，避免连接关闭时被 erase，而 onMessage 仍在解析；
- 断线写 socket 使用 `send(..., MSG_NOSIGNAL)`，避免 SIGPIPE；
- 异步 send/shutdown 捕获 `shared_from_this()`，避免连接对象生命周期悬空。

## Unity Implementation

Unity 侧主组件：

| Component | Responsibility |
| --- | --- |
| `WebSocketNetworkClient` | 持有 `ClientWebSocket`，完成连接、发送、后台接收、断开与重连 |
| `NetworkGateway` | 负责 protobuf encode/decode、消息队列、主线程 drain |
| `GameFlow` | 根据网络消息推进房间、游戏、重连和 UI 状态 |

线程模型：

- 接收循环在后台运行，只把完整 `ServerMessage` 放入队列；
- Unity 状态和 UI 更新只在主线程 `DrainMessages` 中执行；
- `sendLock` 串行化 `ClientWebSocket.SendAsync`；
- `stateLock` 保护 connect / disconnect / reconnect 状态切换；
- `NetworkGateway.IsConnected` 使用同步/可见性控制，避免 UI 读取旧连接状态。

## Message Categories

所有消息都通过 Protobuf envelope 承载。主要类别：

| Category | Typical messages | Notes |
| --- | --- | --- |
| Room base flow | create/join/ready/start/leave, `RoomStateNotify` | 基础房间生命周期 |
| Lobby / room browser | lobby login, room list, online players | 用于发现房间和在线玩家 |
| Access flow | apply / approve / reject / invite / expire | 房间申请加入与邀请流程 |
| Game flow | draw/discard/replace/take/use skill/call CABO | 服务端权威校验和回合推进 |
| Sync / reconnect | `ReconnectReq`, `ReconnectRsp`, `StateSyncNotify` | 断线后恢复房间与游戏上下文 |
| Broadcast result | `ActionResultNotify`, reveal, score, game over | 客户端动画和 UI 刷新的主要驱动 |

## Reconnect Flow

```text
connection lost
  -> server marks player offline and keeps session for reconnect window
  -> Unity shows reconnect state / attempts reconnect
  -> client sends ReconnectReq(session token)
  -> server verifies session and binds new connection
  -> server sends ReconnectRsp + StateSyncNotify
  -> Unity restores room/game phase, current turn, pending draw/skill state
```

重连的核心不是“重新连上 socket”，而是恢复玩家在服务端状态机中的位置：

- 当前是否在房间或游戏中；
- 当前是谁的回合；
- 本地玩家是否有待处理抽牌决策；
- 是否处于弃牌后等待技能目标选择；
- 牌堆、弃牌堆、玩家手牌可见信息、比分等状态。

## Security and Authority Rules

服务端不信任客户端传入的 `player_id` 作为操作权限来源。所有会改变状态的请求都需要满足：

1. 该玩家存在于房间/游戏中；
2. 该玩家当前绑定连接与发起请求的连接一致；
3. 请求符合当前服务端状态机，例如当前回合、pending card、pending skill 等；
4. 广播给不同玩家前要过滤隐藏信息。

这避免了伪造玩家操作、越权开始游戏、对手看到隐藏牌值等问题。

## Broadcast Send Path

广播发送优化后的路径：

```text
ServerMessage
  -> serialize protobuf payload once
  -> encode WebSocket frame once
  -> send same frame bytes to multiple connections
```

临时发送 buffer 使用线程本地池复用容量。过大的 buffer 会被丢弃，避免偶发大消息让线程长期持有过多内存。

## Test Coverage

已有回归测试覆盖的网络/系统问题包括：

- WebSocket handshake、mask、control frame、fragmentation、partial read；
- forged room/game actions；
- hidden card value filtering；
- Unity WebSocket concurrent send 和 reconnect 状态同步；
- server codec map 生命周期竞态；
- room/game shared state synchronization；
- reconnect state restore；
- broadcast frame reuse 和 send buffer reuse。

相关背景文档：

- [WEBSOCKET_CLOUDFLARE_RUNBOOK.md](WEBSOCKET_CLOUDFLARE_RUNBOOK.md)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [BUG_LOG.md](BUG_LOG.md)
- [BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md)
