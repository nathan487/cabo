# Bug Log

本文档合并早期 CLI/Server bug 记录，以及从 git 提交历史梳理出的后台、网络、并发、状态一致性问题。后半部分更适合用于简历项目经历整理。

## Early CLI / Server Issues

> 早期原型阶段，主要问题集中在 CLI 客户端交互、TCP 粘包处理和服务端状态广播。

| Area | Problem | Fix |
| --- | --- | --- |
| Server room creation | 房主创建房间后未自动 ready，导致 4 人满员后仍显示未全部准备 | 创建房间时将房主 ready 状态初始化为 true |
| Server start-game validation | 未严格校验人数/准备状态时可能误启动 | 增加人数与 ready 状态检查 |
| Server broadcast | 某些 ready/join 状态只发给部分玩家 | 统一 room update 广播 |
| CLI input | 阻塞 `cin` 导致网络消息不能及时处理 | 拆分网络接收与输入处理，增加消息 drain |
| CLI local cache | 房间列表、玩家状态未及时刷新 | 服务端广播后更新本地缓存 |
| Protocol framing | TCP 流式读取可能出现半包/粘包 | 使用 length-prefixed Protobuf codec |

## Backend / System Issues from Git History

| Commit | Problem | Root cause | Solution | Verification / evidence |
| --- | --- | --- | --- | --- |
| `1556e33`, `436f641`, `544c7e9` | TCP length-prefix 客户端存在 partial send、接收超时与异常长度头卡住缓冲区的问题 | TCP 是字节流，`send` 可能只写出部分数据；`decodeFrame=false` 同时代表半包和坏帧，调用方无法清理损坏数据 | 循环发送完整 frame；`select` 控制接收超时；预检查 4-byte length，超过 10MB 判定为损坏帧并清理缓冲区恢复连接 | `544c7e9` P0 bug 记录明确描述“网络缓冲区损坏后无法恢复”；代码修复 `NetworkClient::extractOneMessage` |
| `581896e`, `420bac6`, `65516a4`, `aa3b288`, `bddb5cb` | waiting room/ready/join/start 流程中客户端偶发卡住或状态延迟 | 长连接上响应包与 `RoomStateNotify` 等异步通知可能穿插到达；客户端只等单个响应或轮询/sleep 策略不合理 | 进入 waiting room 时 drain pending messages；等待 Join/Ready 时处理多条消息；非阻塞轮询并批量处理；ready 状态以服务端广播为准 | 提交说明记录玩家 2/3 卡在 GameStartNotify/Ready 状态同步问题及对应修复 |
| `544c7e9` | 弃牌丢失、回合劫持、卡牌数不同步等 P0 状态一致性问题 | 部分 handler 在输入校验前修改状态；抽牌后决策缺少当前玩家校验；广播缺少玩家手牌数量更新 | 先校验 slot 再 pop 弃牌堆；discard/replace drawn 增加 `isCurrentPlayer`；`ActionResultNotify` 增加 `player_hands` | `Docs/Bug记录-2026-06-05-CLI客户端与服务端.md` 在该提交中记录 15 个问题和 P0 修复 |
| `0bfaa33` | Unity 与公网访问不适合继续使用裸 TCP 协议 | 早期协议是 `[length][protobuf]`，无法直接被 Unity WebSocket、Cloudflare `wss` 链路复用 | 新增 `WebSocketCodec`，实现 handshake、mask 校验、binary frame、ping/pong/close、分片重组，并让 GameServer 通过 WebSocket 承载 Protobuf | `websocket_codec_test` 覆盖 10 个 case；本地和 Cloudflare 链路握手返回 101 |
| `59918ea`, `cc7ca49` | 客户端可伪造其他玩家的 ready/start/draw/discard/skill 等操作 | 服务端曾信任请求体里的 `player_id`，没有校验连接与玩家会话绑定 | `RoomService` / `GameService` 增加 `isPlayerConnection`，只接受当前玩家连接发起的操作 | 回归测试覆盖 forged ready/start/leave/draw 等请求 |
| `40e4ebd` | 对手可能看到当前玩家刚抽到的牌值 | 广播消息未按接收者过滤隐藏信息 | 对当前玩家发送真实 incoming card，对其他玩家发送 hidden marker | `hidesDrawnIncomingValueFromOtherPlayers` |
| `f78f386` | 技能请求可携带不匹配的牌或技能类型 | 服务端没有把客户端请求与 pending skill/drawn card 状态绑定校验 | 校验 `card_id` 和 skill type 必须匹配服务端等待状态，否则拒绝 | `rejectsSkillTypeMismatch` |
| `8e10f5d`, `f5c3372` | Unity WebSocket 并发发送、断线重连时偶发状态错乱 | `ClientWebSocket.SendAsync` 不适合并发调用；连接状态在接收/断开/重连之间存在竞态 | 增加 `sendLock` 串行化发送，增加 `stateLock` 与 `Volatile` 同步连接状态，ReceiveLoop 持有启动时 socket 引用 | Unity 网络层回归测试与手动重连验证 |
| `14e8a68` | 服务端连接关闭时可能与消息解析并发访问同一个 codec map | connection down 删除 codec，onMessage 同时取 codec/解析 frame | `codecs_` 改为 `shared_ptr<WebSocketCodec>`，并用 mutex 保护 map；解析前复制 shared pointer | 代码级并发修复，避免 use-after-erase |
| `34061a4` | 断线后写 socket 可能触发 SIGPIPE 或异步任务访问已销毁连接 | 普通 `write` 对断开 socket 不屏蔽信号；异步 lambda 捕获 raw `this` | 使用 `send(..., MSG_NOSIGNAL)`；异步 send/shutdown 捕获 `shared_from_this()` | socket 写入与连接生命周期修复 |
| `5588e8e`, `a57b1f2` | 房间/游戏共享状态在多连接并发请求下可能竞态 | 房间 map、id generator、GameRoom 状态缺少统一锁边界 | RoomService 增加锁；GameService 增加全局 mutex 与 per-room `stateMutex` | 多个房间/游戏回归测试通过 |
| `84f2d6e` | 游戏结束回调房间服务时存在死锁风险 | GameService 持有 room lock 时回调 RoomService，回调路径又可能查询游戏状态 | 使用 `unique_lock`，在 `notifyGameFinished` 前释放 room lock | `gameFinishedCallbackRunsAfterRoomLockReleased` |
| `192ab9f` | 活跃回合中可能被重新 start game，破坏 deck/turn/round 状态 | 房间 start 与游戏 active round 状态没有互斥校验 | 增加 `canRestartRound`，活跃回合拒绝重启 | `rejectsRestartRoundWhileRoundIsActive` |
| `bd86423`, `1d3dc39` | 多人广播时重复编码 WebSocket frame、频繁分配临时 buffer | 每个接收者各自编码 frame；临时 string 反复申请容量 | 广播 frame 一次编码多连接复用；引入 thread-local send buffer pool | 测试断言 broadcast encoded frame count 为 1，并覆盖 buffer 复用/超大 buffer 丢弃 |
| `07dac11`, `7b9fc47` | 玩家短暂断线后无法恢复当前游戏上下文 | 旧流程更偏向连接断开即离开/清理状态 | 保留 session token、offline 状态和 reconnect window；重连后发送 StateSync 快照恢复 draw/turn/skill 等上下文 | `GameFlowReconnectStateTests` 覆盖多个恢复场景 |
| `4882d72` | 房间发现与加入流程过于粗糙，不适合真实大厅 | 早期 join 流程缺少房间浏览、申请、邀请、过期、在线身份校验 | 新增 lobby player、room browser、access application/invitation、approval/expiry 流程 | `RoomBrowserStateTests` |

## Resume-oriented Takeaways

这些问题可以包装成后台开发项目亮点：

- 网络协议工程化：从裸 TCP 原型演进到 WebSocket + Protobuf，兼顾 Unity 客户端、公网 `wss`、二进制协议和服务端 Reactor 复用。
- TCP 长连接健壮性：修复 partial send、半包/粘包、异常长度头阻塞缓冲区、异步通知与响应穿插导致的状态卡住问题。
- 连接与会话安全：把“玩家身份”从不可信请求字段改为服务端连接绑定，阻断伪造操作。
- 并发问题定位与修复：针对 socket 写入、codec map、房间状态、游戏状态、Unity WebSocket 状态机分别建立锁边界和生命周期管理。
- 断线重连：以 session token + offline marker + StateSync snapshot 恢复玩家上下文，解决多人在线游戏中高频出现的弱网问题。
- 性能优化：广播 frame 复用和 buffer 池复用，减少多人房间高频广播时的重复编码与内存分配。
