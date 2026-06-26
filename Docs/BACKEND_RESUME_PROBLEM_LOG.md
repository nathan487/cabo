# Backend Resume Problem Log

这份文档专门用于把本项目中“发现问题、定位原因、完成修复/优化、用测试验证”的经历整理成简历素材。目标岗位：腾讯后台开发实习生。

内容来自 `main` 与 `codex/special-effects` 两条 branch 的提交历史。需要注意：`main` 在分叉后主要是 `.gitignore` 维护提交；更完整的功能演进集中在当前 `codex/special-effects` 分支和两条分支共有历史中。

如果只想看最能代表后台能力的版本，优先看 [HIGH_IMPACT_BACKEND_PROBLEM_CASES.md](HIGH_IMPACT_BACKEND_PROBLEM_CASES.md)。本文档保留更完整的证据链。

## Most Competitive Resume Angles

如果简历篇幅有限，优先写这 3-4 条：

1. 完善早期 TCP length-prefix Protobuf 长连接的工程健壮性：修复 partial send、select 超时、半包/粘包、异常长度头导致接收缓冲区永久阻塞、通知消息与响应消息穿插导致状态卡住等问题。
2. 基于 C++ Reactor 网络模型实现 WebSocket + Protobuf 多人游戏服务端，将早期 TCP length-prefix 协议升级为支持 Unity/公网 `wss` 的二进制通信链路，并覆盖 handshake、mask、控制帧、分片重组等协议细节。
3. 设计服务端权威的连接-玩家会话绑定、并发同步、断线重连和状态快照恢复机制，修复伪造玩家操作、断线状态丢失、连接生命周期竞态、潜在死锁等问题。
4. 针对多人广播链路优化 WebSocket frame 编码和临时发送 buffer 分配，将同一条广播从多次重复编码改为一次编码多连接复用，并通过回归测试验证。

## Problem Stories

### 1. TCP stream framing and receive-buffer recovery

- Problem: 早期 CLI/TCP 长连接原型中，客户端偶发卡在等待房间或游戏开始；损坏的 length-prefix 头会卡住 `recvBuffer_`，后续正常数据也无法被解析。
- Root cause: TCP 是字节流，存在 partial send、半包/粘包；`decodeFrame` 将“数据还没收完整”和“长度字段非法”都返回 false，调用方无法清理坏帧；客户端还假设请求后下一个包就是对应响应，忽略了 `RoomStateNotify` 等异步通知可能先到。
- Solution: `sendRaw` 改为循环发送；`recvRaw` 增加 `select` 超时；protobuf 解析失败只移除当前错误帧；在 `extractOneMessage` 预检查 4-byte length，超过 10MB 时清空损坏缓冲区恢复连接；waiting room 使用非阻塞轮询 drain pending messages，并把 ready/start 状态收敛到服务端广播通知。
- Verification: 提交中的复盘和针对性修复覆盖 partial send、timeout、multi-message waiting、ready/start 状态同步、异常长度头恢复等路径。
- Commits / files: `1556e33`, `1209080`, `436f641`, `581896e`, `420bac6`, `65516a4`, `aa3b288`, `544c7e9`, `bddb5cb`, `MuduoBaseGameServer/cli_client/src/NetworkClient.cpp`, `ClientApp.cpp`, `RoomService.cc`.
- Resume phrasing: “定位并修复 TCP length-prefix 协议在部分发送、半包解析、异常长度头和异步通知穿插下的卡死问题，完善循环发送、超时控制、坏帧恢复和消息 drain 机制。”

### 2. WebSocket + Protobuf transport upgrade

- Problem: 早期服务端使用裸 TCP length-prefixed Protobuf，CLI 调试可用，但 Unity 客户端、公网部署和 Cloudflare Tunnel `wss` 接入不顺畅。
- Root cause: 业务协议绑定在自定义 TCP framing 上，缺少标准 WebSocket 握手、mask、控制帧和 frame 重组能力。
- Solution: 新增 `WebSocketCodec`，在原有 TcpServer/Reactor 基础上实现 RFC6455 握手和二进制 frame 编解码；Protobuf payload 保持不变，减少业务层迁移成本。
- Verification: `websocket_codec_test` 覆盖 handshake、mask、partial read、ping/pong、close、fragmented binary 等 case；本地和 Cloudflare `wss` 链路握手成功。
- Commits / files: `0bfaa33`, `MuduoBaseGameServer/src/common/WebSocketCodec.*`, `MuduoBaseGameServer/mytest/websocket_codec_test.cpp`.
- Resume phrasing: “实现 WebSocket + Protobuf 二进制通信层，支持 Unity 客户端和公网 `wss` 接入，覆盖 frame 编解码、控制帧处理和半包/分片重组。”

### 3. Forged player action protection

- Problem: 客户端可以在请求中填入其他玩家的 `player_id`，伪造 ready/start/leave/draw/discard/skill 等操作。
- Root cause: 服务端早期更信任请求体字段，没有强制校验“发出该请求的 TCP/WebSocket 连接是否属于该玩家”。
- Solution: 在 `RoomService` 与 `GameService` 中增加玩家连接绑定校验，只允许当前绑定连接操作自己的玩家状态。
- Verification: 增加 forged ready/start/leave/draw 等回归测试。
- Commits / files: `59918ea`, `cc7ca49`, `RoomService`, `GameService`.
- Resume phrasing: “将玩家身份校验从客户端字段升级为服务端连接会话绑定，阻断伪造操作，提升多人游戏状态安全性。”

### 4. Hidden-information filtering

- Problem: 抽牌等消息广播时，对手可能看到当前玩家刚抽到的牌值。
- Root cause: 服务端广播没有按接收者视角过滤隐藏信息。
- Solution: 对当前玩家发送真实 incoming card，对其他玩家发送隐藏标记，仍保持同一套服务端权威状态。
- Verification: `hidesDrawnIncomingValueFromOtherPlayers`.
- Commit: `40e4ebd`.
- Resume phrasing: “实现按接收者视角过滤的状态广播，保证多人卡牌游戏隐藏信息不泄漏。”

### 5. Skill request validation

- Problem: 客户端可以提交不匹配的 skill type 或 card id，导致服务端状态被错误推进。
- Root cause: 请求没有和服务端 pending drawn card / pending skill 状态强绑定。
- Solution: 服务端校验 `card_id`、skill type 必须匹配当前等待状态，否则拒绝请求并保持回合状态不变。
- Verification: `rejectsSkillTypeMismatch`.
- Commit: `f78f386`.
- Resume phrasing: “为技能请求建立服务端状态机校验，避免客户端异常请求破坏回合推进。”

### 6. Unity WebSocket concurrency and reconnect races

- Problem: Unity 侧并发发送、断线重连、ReceiveLoop 退出时偶发状态错乱。
- Root cause: `ClientWebSocket.SendAsync` 不适合并发调用；连接对象和连接状态在 send/receive/disconnect/reconnect 多路径间共享。
- Solution: 增加 `sendLock` 串行化发送；增加 `stateLock` 保护 socket 状态切换；ReceiveLoop 持有启动时 socket 引用，避免重连替换 socket 后旧循环误改新状态。
- Verification: Unity 网络层和重连状态机测试。
- Commits / files: `8e10f5d`, `f5c3372`, `WebSocketNetworkClient.cs`, `NetworkGateway`.
- Resume phrasing: “修复 Unity WebSocket 多线程收发竞态，使用发送锁和状态锁保证断线重连过程中的连接状态一致性。”

### 7. Server WebSocket codec lifetime race

- Problem: 连接关闭时，服务端可能删除该连接对应的 codec；与此同时 IO 消息回调还在解析 frame。
- Root cause: `codecs_` map 的 erase 与 onMessage 读取缺少同步，且对象生命周期可能早于解析结束。
- Solution: codec 改为 `shared_ptr` 管理，并用 mutex 保护 map；onMessage 在锁内复制 shared pointer 后再解析。
- Verification: 代码级并发修复，避免 use-after-erase / data race。
- Commit: `14e8a68`.
- Resume phrasing: “修复连接关闭与 WebSocket frame 解析并发导致的生命周期竞态，使用 mutex + shared_ptr 稳定 codec 生命周期。”

### 8. Socket write robustness on disconnect

- Problem: 对端断开后服务端继续写 socket，可能触发 SIGPIPE；异步发送/关闭任务中捕获 raw `this` 也有生命周期风险。
- Root cause: 普通 `write` 没有屏蔽断线信号；异步任务执行时连接对象可能已释放。
- Solution: socket 写入改用 `send(..., MSG_NOSIGNAL)`；异步 send/shutdown lambda 捕获 `shared_from_this()`。
- Verification: 断线写入路径和连接生命周期修复。
- Commit: `34061a4`.
- Resume phrasing: “加固断线场景 socket 写入路径，使用 `MSG_NOSIGNAL` 与 `shared_from_this()` 避免 SIGPIPE 和连接对象悬空。”

### 9. Room and game state synchronization

- Problem: 多连接同时 ready/join/start/draw/discard 时，房间 map、id generator、GameRoom 状态可能竞态。
- Root cause: 服务端业务状态从单连接原型演进到多连接并发后，锁边界不完整。
- Solution: RoomService 增加锁保护房间、玩家、会话、访问记录；GameService 增加全局 mutex 与 per-room `stateMutex`，明确房间查找和单局状态的同步边界。
- Verification: 房间、游戏状态回归测试；抽牌/弃牌 handler 保持非阻塞。
- Commits: `5588e8e`, `a57b1f2`.
- Resume phrasing: “为房间服务和游戏服务设计锁粒度，修复多连接并发请求下的状态竞态。”

### 10. Deadlock prevention between GameService and RoomService

- Problem: 游戏结束时 GameService 回调 RoomService，存在服务间互相持锁导致死锁的风险。
- Root cause: 回调发生在 GameRoom 锁持有期间，而回调路径可能反向查询游戏状态。
- Solution: 使用 `unique_lock` 显式控制锁生命周期，在 `notifyGameFinished` 前释放 room lock。
- Verification: `gameFinishedCallbackRunsAfterRoomLockReleased`.
- Commit: `84f2d6e`.
- Resume phrasing: “定位并修复服务间回调持锁导致的潜在死锁，通过调整锁释放时机保证游戏结算流程稳定。”

### 11. Reconnect recovery flow

- Problem: 玩家短暂断线后，旧流程无法可靠恢复当前回合、待抽牌选择、技能目标等上下文。
- Root cause: 连接生命周期和玩家会话生命周期绑定过紧，断线后状态容易被清理或客户端本地状态丢失。
- Solution: 引入 session token、offline marker、reconnect window；重连后重新绑定连接并返回 `StateSyncNotify`，恢复服务端权威快照。
- Verification: Unity `GameFlowReconnectStateTests` 覆盖当前玩家待处理抽牌、普通回合、他人回合、弃牌技能选择等恢复场景。
- Commits / files: `07dac11`, `7b9fc47`, `ReconnectReq`, `ReconnectRsp`, `StateSyncNotify`.
- Resume phrasing: “实现多人游戏断线重连恢复机制，通过 session token 与 StateSync 快照恢复回合上下文和客户端 UI 状态。”

### 12. Active round restart protection

- Problem: 活跃回合中再次 start game 会重置 deck、turn index 和 round state，破坏当前局。
- Root cause: 房间 start 逻辑没有与 GameService active round 状态联动。
- Solution: 增加 `canRestartRound`，只有非活跃回合允许重新开始。
- Verification: `rejectsRestartRoundWhileRoundIsActive`.
- Commit: `192ab9f`.
- Resume phrasing: “完善游戏生命周期校验，禁止活跃回合重复启动，保证局内状态一致性。”

### 13. Broadcast encode and send-buffer optimization

- Problem: 多人房间广播时，每个接收者都会重复编码同一条 WebSocket frame，并频繁创建临时 buffer。
- Root cause: 发送路径以单连接 send 为核心，没有针对广播场景复用编码结果。
- Solution: 增加 `encodeServerMessage` / `sendFrame`，广播时 frame 一次编码、多连接复用；引入 thread-local `SendBufferPool` 复用临时 string 容量，并丢弃过大的 buffer 防止内存膨胀。
- Verification: 测试断言游戏/房间广播 encoded frame count 为 1；覆盖 buffer 复用和超大 buffer 丢弃。
- Commits / files: `bd86423`, `1d3dc39`, `WebSocketCodec`, `SendBufferPool`.
- Resume phrasing: “优化多人广播发送链路，将 WebSocket frame 从 N 次重复编码降为 1 次编码，并用线程本地 buffer 池降低高频广播内存分配。”

### 14. Room browser and access flow

- Problem: 早期房间加入流程只适合 demo，不适合真实大厅：缺少在线玩家、房间可见性、申请加入、邀请、过期与房主审批。
- Root cause: 房间状态模型只覆盖 create/join/ready/start，缺少 lobby-level identity 和 access record。
- Solution: 新增 lobby player、room list、online lobby players、application/invitation record、approve/reject/expire 流程，并校验房间等待中、未满、房主在线、连接未在其他房间等条件。
- Verification: `RoomBrowserStateTests`.
- Commit: `4882d72`.
- Resume phrasing: “设计房间大厅与访问控制流程，实现房间浏览、加入申请、邀请审批和过期处理，补齐多人在线产品的真实业务闭环。”

## Suggested Resume Bullets

可以按简历篇幅选 3-5 条，不建议全部堆上去：

- 定位并修复 TCP length-prefix 长连接中的 partial send、异常长度头阻塞接收缓冲区、异步通知与响应穿插等问题，完善循环发送、超时控制、坏帧恢复和消息 drain 机制。
- 基于 C++ Reactor 网络模型实现 WebSocket + Protobuf 多人游戏服务端，完成 handshake、mask 校验、控制帧处理、二进制分片重组，并支持 Unity 客户端与公网 `wss` 接入。
- 设计服务端权威的连接-玩家会话绑定机制，修复客户端伪造 `player_id` 操作 ready/start/draw/discard/skill 的问题，提升多人状态安全性。
- 实现断线重连恢复流程，通过 session token、offline marker 和 StateSync 快照恢复当前回合、待抽牌选择、技能目标等上下文。
- 梳理房间服务和游戏服务的并发边界，为房间 map、玩家会话、GameRoom 状态、WebSocket codec map 增加锁和生命周期管理，修复竞态与潜在死锁。
- 优化多人广播发送路径，将同一条 WebSocket 广播从多次 frame 编码降为一次编码多连接复用，并使用线程本地 buffer 池减少临时内存分配。

## Interview Talking Points

面试时可以按下面的顺序展开，听起来会更像真实做过项目，而不是背八股：

1. 早期 TCP 长连接踩过哪些坑：partial send、半包/粘包、坏 length 头、响应和通知消息穿插，分别如何修。
2. 为什么不用裸 TCP 继续做：Unity 和公网 `wss` 接入成本高，所以保留 Protobuf payload，替换 transport framing 为 WebSocket。
3. 服务端为什么必须权威：客户端传来的 `player_id` 不可信，因此身份要绑定到连接和 session，而不是只看请求参数。
4. 多线程问题怎么发现：多连接并发、断线重连和广播都暴露了共享状态访问；分别用锁粒度、shared pointer 生命周期和主线程消息队列解决。
5. 断线重连难点是什么：不是重新连上 socket，而是恢复“玩家此刻在游戏状态机里的位置”。
6. 性能优化做了什么：优先优化广播链路中最明显的重复工作，即 frame 重复编码和临时 buffer 分配。

## Truthful Wording Guardrails

为了避免简历写得过火，建议这样表述：

- 可以写“实现/设计/优化/修复”，因为提交历史里有对应代码和测试。
- 可以写“覆盖回归测试”，因为多处 commit 增加了针对性测试。
- 谨慎写“高并发”“百万连接”“QPS 提升 xx%”，除非后续补充压测数据。
- 可以写“多人房间广播编码次数从 N 次降为 1 次”，因为测试里验证了 encoded frame count。
