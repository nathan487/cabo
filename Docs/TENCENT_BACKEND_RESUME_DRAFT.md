# Tencent Backend Internship Resume Draft

这份文档是把项目经历直接整理成“可放进简历/面试讲述”的版本。强案例排序见 [HIGH_IMPACT_BACKEND_PROBLEM_CASES.md](HIGH_IMPACT_BACKEND_PROBLEM_CASES.md)，完整证据链见 [BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md)。

## Project Name

多人在线 Cabo 卡牌游戏服务端

## One-line Project Description

基于 C++ Reactor 网络模型实现多人在线卡牌游戏服务端，使用 WebSocket + Protobuf 与 Unity 客户端通信，支持房间大厅、服务端权威游戏状态、断线重连、状态同步和多人广播；项目过程中重点解决了 TCP/WebSocket 长连接、并发同步、会话鉴权和状态一致性问题。

## Recommended Resume Version

如果简历只放 3 条，推荐这样写：

- 基于 C++ Reactor / 非阻塞 socket 实现多人游戏服务端网络层，完善 TCP length-prefix Protobuf 长连接的 partial send、半包/粘包、异常长度头恢复和异步消息 drain 机制，并升级为 WebSocket binary Protobuf 支持 Unity 与公网 `wss` 接入。
- 设计服务端权威的连接-玩家会话绑定与状态校验机制，修复客户端伪造 `player_id` 操作 ready/start/draw/discard/skill、回合劫持、隐藏牌值泄漏、活跃回合重复启动等一致性和安全问题。
- 梳理多连接并发和连接生命周期边界，修复 WebSocket codec map 竞态、断线写入 `SIGPIPE`、服务间回调潜在死锁；实现断线重连与 StateSync 快照恢复，并将多人广播由多次 frame 编码优化为一次编码多连接复用。

## Stronger Technical Version

如果你的简历项目区域能放 4-6 条，可以用这个版本：

- 基于 C++ Muduo-style Reactor、epoll、eventfd 和非阻塞 socket 搭建多人游戏服务端，承载房间、回合、技能、计分、重连等权威状态。
- 完善早期 TCP length-prefix Protobuf 通信链路，修复部分发送、接收超时、半包/粘包、坏长度头阻塞缓冲区、通知消息与响应消息穿插导致的状态卡住问题。
- 实现 WebSocket + Protobuf 二进制通信层，完成 HTTP Upgrade、`Sec-WebSocket-Accept`、client mask 校验、ping/pong/close、binary continuation frame 重组，并复用原有 Protobuf 业务消息。
- 将玩家身份从客户端请求字段升级为服务端连接会话绑定，拦截伪造 ready/start/leave/draw/discard/skill 请求，并对隐藏牌值按接收者视角过滤。
- 梳理多连接并发下的同步边界，为 RoomService、GameService、WebSocket codec map、Unity WebSocket 状态机增加锁和生命周期管理，修复竞态、潜在死锁、SIGPIPE 和断线写入问题。
- 优化多人广播发送路径，对同一条 ServerMessage 只编码一次 WebSocket frame，并使用线程本地 send buffer pool 复用临时内存，降低高频广播下的重复编码和分配开销。

## Interview Problem Stories

### Story 1 — 早期 TCP 长连接为什么会“卡死”？

项目早期 CLI 客户端使用 `[4-byte length][protobuf]` 的自定义 framing。调试时发现有些客户端会卡在等待房间或游戏开始，看起来像业务状态没同步，但根因在网络消息处理：TCP 是字节流，可能 partial send、半包/粘包；客户端还把“数据没收完整”和“长度头已经损坏”都当成 `decodeFrame=false`，导致坏长度头永远留在 `recvBuffer_` 前面，后续正常消息也解析不到。

我的处理方式是把发送、接收、解析三层拆清楚：发送端循环 `send` 直到完整写出；接收端用 `select` 做超时；解析前先检查 4-byte length，超过上限就认为是坏帧并清理缓冲区；等待房间流程改成 drain 所有 pending messages，允许 `RoomStateNotify` 这类通知先于 `ReadyRsp` / `JoinRoomRsp` 到达。

面试展开点：

- TCP 是流，为什么应用层必须自己做 framing；
- partial send 为什么不能只调用一次 `send`；
- malformed frame 和 incomplete frame 为什么要区分；
- 长连接里 response 和 notify 为什么可能穿插到达；
- 为什么“卡在 UI”最后可能是网络缓冲区状态机问题。

### Story 2 — 为什么从 TCP framing 升级到 WebSocket？

项目早期是 CLI 客户端，所以使用 `[4-byte length][protobuf]` 的自定义 TCP framing 足够。但 Unity 客户端和公网访问接入后，裸 TCP 在客户端生态、Cloudflare Tunnel、`wss` 访问上都不方便。

我的处理方式是保留 Protobuf payload，不改业务消息结构，只把 transport framing 换成 WebSocket binary frame。服务端新增 `WebSocketCodec`，负责 handshake、mask 校验、控制帧、分片重组和半包解析。这样业务层仍然收到完整 Protobuf bytes，迁移成本比较低。

面试展开点：

- TCP 是流，为什么需要 framing；
- WebSocket frame 和 Protobuf payload 的边界；
- 客户端到服务端为什么必须 mask；
- ping/pong/close 控制帧和 binary frame 如何区分；
- partial read / fragmented frame 怎么处理。

### Story 3 — 如何防止客户端伪造其他玩家操作？

早期请求体里有 `player_id`，如果服务端直接信这个字段，恶意客户端就可以伪造别人 ready、start game、draw、discard 或 use skill。

修复思路是把权限判断从“请求字段”迁移到“服务端会话”。玩家进入房间后，服务端记录玩家与连接对象的绑定关系；之后所有会改变状态的请求，都必须满足“请求连接就是该玩家当前连接”。这类校验分别加在 RoomService 和 GameService。

面试展开点：

- 为什么客户端字段不可信；
- 连接、玩家、session token 的关系；
- 断线重连后如何更新绑定；
- ready/start 和 draw/discard 这两类权限校验有什么差异。

### Story 4 — 断线重连真正难在哪里？

断线重连不是简单地重新建立 socket。多人游戏里更难的是恢复玩家在服务端状态机中的位置，例如当前是不是我的回合、我是否已经抽了一张牌但还没决定弃掉还是替换、是否处于弃牌后等待选择技能目标。

当前实现用 session token 保留玩家身份，断线后服务端把玩家标记为 offline 并保留 reconnect window。重连成功后重新绑定连接，服务端下发 StateSync 快照，让 Unity 恢复房间、游戏、当前回合、pending draw/skill 等上下文。

面试展开点：

- connection lifecycle 和 player lifecycle 为什么不能绑定太死；
- 重连快照应该包含哪些最小状态；
- 客户端 UI 状态如何从服务端状态机恢复；
- 为什么不能只靠客户端本地缓存。

### Story 5 — 多线程和连接生命周期修过哪些问题？

项目里比较典型的系统问题有几类：

- 早期服务端曾为了动画节奏在回合切换里 `sleep_for(1500ms)`，这会阻塞服务端处理路径，后来改成由客户端根据通知播放动画；
- Unity `ClientWebSocket.SendAsync` 并发发送不安全，用 `sendLock` 串行化；
- connect/disconnect/reconnect 和 ReceiveLoop 同时改连接状态，用 `stateLock` 和 socket 引用隔离旧循环；
- 服务端连接关闭时会 erase codec map，而 onMessage 可能还在解析，改成 mutex + `shared_ptr<WebSocketCodec>`；
- 对端断开后继续写 socket 可能触发 SIGPIPE，改用 `send(..., MSG_NOSIGNAL)`，异步任务捕获 `shared_from_this()`。

面试展开点：

- 锁保护的是哪个共享资源；
- 为什么不能锁太大；
- `shared_from_this()` 解决的是什么生命周期问题；
- SIGPIPE 在 Linux socket 写入中怎么出现。

### Story 6 — 广播性能优化做了什么？

多人房间里，一次玩家动作通常要广播给多个连接。早期发送路径偏单连接视角，同一条消息可能为每个玩家重复编码 WebSocket frame，还会频繁分配临时 string buffer。

优化后，同一条 `ServerMessage` 先序列化成 Protobuf payload，再编码一次 WebSocket frame，然后把同一份 frame bytes 发送给多个连接。同时引入线程本地 send buffer pool 复用临时字符串容量，并丢弃超大 buffer，避免内存长期膨胀。

面试展开点：

- 广播场景和单播场景的性能差异；
- 为什么 frame 可以复用；
- buffer pool 为什么要丢弃 oversized buffer；
- 没有压测数据时，简历如何诚实描述优化。

## Compact Project Pitch

面试官问“介绍一下这个项目”时，可以这样讲：

> 这是一个多人在线 Cabo 卡牌游戏。我主要关注服务端和网络层：服务端用 C++ Reactor 模型处理多连接，业务上维护房间和游戏的权威状态；客户端用 Unity，通过 WebSocket binary frame 传 Protobuf 消息。项目过程中我先完善了早期 TCP length-prefix 长连接的半包、坏帧恢复和异步消息处理，之后把传输层升级到 WebSocket；同时做了连接会话绑定、并发状态同步、断线重连和广播发送优化。比较有代表性的问题是：异常长度头导致接收缓冲区卡死、客户端伪造 player_id、连接关闭与 frame 解析竞态、StateSync 恢复断线玩家上下文，以及多人广播从每个连接重复编码优化成一次编码多连接复用。

## What Not to Overclaim

建议不要写：

- “支持百万连接”；
- “高并发压测 QPS 提升 xx%”；
- “分布式服务端架构”；
- “服务端集群/网关/负载均衡”；
- “数据库事务/缓存一致性”。

除非后续真的补了压测、分布式部署、数据库或缓存模块，否则这些会让面试官追问到项目没有覆盖的范围。更稳的策略是把 C++ 网络、并发同步、协议设计和多人状态一致性讲扎实。
