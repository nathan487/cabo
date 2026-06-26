# High-impact Backend Problem Cases

本文档重新筛选项目中更能体现后台开发能力的问题解决案例。来源为 `main`、`codex/special-effects` 以及两条分支共有历史的提交记录；重点不是“做了哪些功能”，而是“发现了什么真实问题、为什么难、怎么修、如何验证”。

目标岗位：腾讯后台开发实习生。

特别注意：`544c7e9` 是 `main` 与 `codex/special-effects` 都包含的公共历史提交，不是当前分支独有提交。它修复的“网络缓冲区损坏后无法恢复/缓冲区死锁”和多项 P0 状态一致性问题，已经被提升到本文档的最高优先级。

## S-tier：最建议写进简历或面试重点展开

### 1. TCP 流式协议的粘包/半包与坏帧恢复

- 代表提交：`1556e33`, `1209080`, `436f641`, `581896e`, `420bac6`, `65516a4`, `aa3b288`, `544c7e9`, `bddb5cb`。
- 问题现象：
  - TCP 是字节流，不保证一次 `send` 对应一次 `recv`，早期客户端需要自己做 `[4-byte length][protobuf payload]` framing。
  - `send` 可能只写出部分数据，如果只判断一次 `send` 返回值，长消息可能被截断。
  - `decodeFrame` 遇到长度字段异常时和“半包等待更多数据”一样返回 false，调用方无法区分，坏长度头会永久卡在 `recvBuffer_` 前部，后续正常数据全部堆在后面，表现为“缓冲区死锁/连接卡死”。
  - 客户端等待 `ReadyRsp` / `JoinRoomRsp` 时可能先收到 `RoomStateNotify`，如果只按“请求后下一个包就是响应”的同步 RPC 思路处理，会误判失败。
  - waiting room 中 `hasMessage(50ms)`、额外 sleep、phase check 放错位置，会让 `GameStartNotify` 已经到达但客户端迟迟不进入游戏。
- 根因：
  - 早期把 TCP 当成“消息通道”使用，缺少对流式 IO、部分写、半包、坏包、消息乱序/穿插通知的完整处理。
  - `decodeFrame` 的返回值语义过粗，无法区分“数据还不够”和“数据已经损坏”。
- 解决：
  - `sendRaw` 改为循环发送直到所有 bytes 写完；socket 资源禁止拷贝。
  - `recvRaw` 增加 `select` 超时；protobuf 解析失败时只移除当前错误帧，保留后续数据。
  - 在 `extractOneMessage` 中预检查 4-byte length，如果超过 10MB 上限，清空/丢弃损坏缓冲区以恢复连接。
  - waiting room 入口和循环中主动 drain pending messages，用非阻塞轮询批量处理所有已到达消息。
  - ready/start 状态以服务端 `RoomStateNotify` 广播作为最终状态来源，而不是依赖客户端同步等待某一个响应包。
- 体现能力：
  - 理解 TCP 字节流、应用层 framing、半包/粘包、部分写、异常帧恢复。
  - 能从“玩家 2/3 卡在等待房间”这类业务现象反推到底层消息处理和状态机问题。
  - 能把同步请求-响应模型改成更接近真实长连接服务的异步消息模型。
- 简历写法：
  - “定位早期 TCP length-prefix 协议在部分发送、半包解析、异常长度头下的卡死问题，完善循环发送、select 超时、坏帧恢复和消息队列 drain 机制，提升长连接消息处理健壮性。”
- 面试展开关键词：
  - TCP stream vs message boundary；
  - length-prefix framing；
  - partial send；
  - malformed frame 和 incomplete frame 为什么不能混用同一个返回语义；
  - 长连接下 response 和 notify 可能乱序到达。

### 2. 从裸 TCP framing 升级到 WebSocket + Protobuf

- 代表提交：`0bfaa33`。
- 问题现象：
  - 裸 TCP length-prefix Protobuf 适合 CLI 调试，但 Unity 客户端、公网 `wss`、Cloudflare Tunnel 接入成本高。
- 根因：
  - 业务层协议和自定义 TCP framing 绑定，缺少标准 WebSocket 握手、mask、控制帧、分片重组能力。
- 解决：
  - 新增 `WebSocketCodec`，保留 Protobuf payload，只替换 transport framing。
  - 支持 HTTP Upgrade、`Sec-WebSocket-Accept`、大小写 header、Connection token、client mask 校验、binary frame、ping/pong/close、partial read、fragmented binary reassembly。
- 验证：
  - `websocket_codec_test` 覆盖 10 个协议 case，包括 handshake、非法握手、未 mask frame、partial TCP reads、分片重组、控制帧。
- 体现能力：
  - 能在不推翻业务协议的情况下做网络层演进，降低迁移成本。
  - 能理解 WebSocket 协议细节，而不是只调用库。
- 简历写法：
  - “在 C++ Reactor 服务端实现 WebSocket binary Protobuf 通信层，支持 Unity 与公网 `wss` 接入，覆盖 handshake、mask 校验、控制帧、半包和分片重组。”

### 3. 多连接并发下的共享状态和连接生命周期治理

- 代表提交：`14e8a68`, `34061a4`, `5588e8e`, `a57b1f2`, `84f2d6e`, `a4d2815`, `8e10f5d`, `f5c3372`。
- 问题现象：
  - 连接关闭时 erase `codecs_`，同时 IO 回调可能还在解析 WebSocket frame。
  - 对端断开后继续写 socket 可能触发 `SIGPIPE`，异步任务捕获 raw `this` 存在生命周期风险。
  - 多连接同时 ready/join/start/draw/discard 时，房间 map、玩家会话、GameRoom 状态可能竞态。
  - GameService 持有房间锁时回调 RoomService，存在服务间锁顺序导致死锁风险。
  - 服务端曾为了动画节奏在业务回合切换中 `sleep_for(1500ms)`，会阻塞 IO/业务处理路径。
- 根因：
  - 原型阶段按单连接/顺序流程写业务，演进到多连接长连接后，锁边界、生命周期、回调顺序没有统一设计。
- 解决：
  - `codecs_` 改为 mutex + `shared_ptr<WebSocketCodec>`，解析前复制 shared pointer。
  - socket 写入改用 `send(..., MSG_NOSIGNAL)`；异步 send/shutdown 捕获 `shared_from_this()`。
  - RoomService 用锁保护房间、玩家、会话、访问记录；GameService 使用全局 mutex + per-room `stateMutex`。
  - 使用 `unique_lock` 控制锁生命周期，在 `notifyGameFinished` 前释放 room lock。
  - 去掉服务端动画等待的 `sleep_for`，由客户端根据 `ActionResultNotify` 自己播放动画。
  - 开始游戏时从 RoomService 获取玩家快照，避免启动过程中房间成员变化影响 GameService。
- 体现能力：
  - 能识别共享资源、对象生命周期和锁顺序问题。
  - 能区分服务端权威逻辑与客户端表现层，不把动画等待放进服务端阻塞路径。
- 简历写法：
  - “梳理多人长连接服务的并发边界，为 codec map、房间状态、游戏状态和 Unity WebSocket 状态机建立同步机制，修复连接关闭解析竞态、SIGPIPE、潜在死锁和服务端阻塞问题。”

### 4. 服务端权威会话绑定与越权操作防护

- 代表提交：`544c7e9`, `59918ea`, `cc7ca49`, `40e4ebd`, `f78f386`, `4882d72`。
- 问题现象：
  - 客户端请求体可携带任意 `player_id`，早期可以伪造他人 ready/start/leave/draw/discard/skill。
  - 抽牌后缺少当前玩家校验，非当前玩家可劫持他人 pending drawn card。
  - 技能请求可以提交不匹配的 skill type/card id，推动错误状态。
  - 对手可能通过广播看到当前玩家刚抽到的隐藏牌值。
  - 房间邀请/申请流程也需要校验“发起连接是否真的是房间成员/房主/大厅玩家”。
- 根因：
  - 服务端早期更信任请求字段，而不是把权限绑定到连接、玩家会话和服务端状态机。
- 解决：
  - 在 RoomService/GameService 中统一检查 `isPlayerConnection`，所有会改变状态的请求都校验连接-玩家绑定。
  - draw/discard/replace/use skill 等操作校验当前玩家、pending card、skill type、card id。
  - 广播按接收者视角过滤隐藏信息。
  - 房间 access flow 校验申请者、邀请者、审批者身份，过期/满房/已开局时拒绝或 expire。
- 验证：
  - 回归测试覆盖 forged draw/ready/start/leave/invite、hidden card filtering、skill mismatch。
- 体现能力：
  - 有“服务端权威”的安全意识，知道客户端字段不可信。
  - 能把权限校验落到每条状态变更路径，而不是只做入口检查。
- 简历写法：
  - “将玩家权限从客户端 `player_id` 字段升级为服务端连接会话绑定，覆盖房间、游戏、技能和大厅访问流程，阻断伪造操作和隐藏信息泄漏。”

### 5. 断线重连与 StateSync 状态快照恢复

- 代表提交：`07dac11`, `7b9fc47`, `8be77ef`, `39f5190`。
- 问题现象：
  - 玩家短暂断线后，不能仅仅重新连上 socket；还要恢复“我当前在服务端状态机的哪个位置”。
  - 抽牌后未决策、弃技能牌后未选目标、非当前玩家旁观、最终轮断线等场景容易卡 UI 或破坏回合计数。
- 根因：
  - connection lifecycle 和 player/session lifecycle 绑定过紧；早期断线更像离开房间。
  - 服务端快照和客户端 UI 子状态没有完整映射。
- 解决：
  - RoomService 保留 `session_token`、offline marker、`disconnectedAtMs` 和 60 秒 reconnect window。
  - 重连后重新绑定连接，服务端返回 `ReconnectRsp` + `StateSyncNotify`。
  - GameService 提供 player-specific snapshot：当前阶段、当前回合玩家、pending draw/skill、牌堆/弃牌堆、可见手牌、分数。
  - Unity 根据 `StateSyncNotify` 推导 `GameFlow.SubState`，恢复抽牌决策、技能目标选择、主操作或等待状态。
  - 显式离开房间与短暂断线分离：离开才移除玩家，断线只标记 offline。
- 验证：
  - `GameFlowReconnectStateTests`、`GameStateSkillCardTests`、GameService/RoomService reconnect tests 覆盖多种恢复场景。
- 体现能力：
  - 能处理长连接服务中“连接断了但业务身份不能立即消失”的典型后台问题。
  - 能设计最小状态快照，而不是依赖客户端本地缓存。
- 简历写法：
  - “实现断线重连和 StateSync 快照恢复机制，通过 session token、offline marker 和服务端权威快照恢复房间、回合、待抽牌/技能选择等上下文。”

### 6. 多人广播链路性能优化

- 代表提交：`bd86423`, `1d3dc39`。
- 问题现象：
  - 房间广播时，同一条 `ServerMessage` 对每个接收者重复序列化/编码 WebSocket frame，并频繁分配临时 string buffer。
- 根因：
  - 发送路径按单连接设计，没有利用广播消息对多个接收者相同的事实。
- 解决：
  - 提取 `encodeServerMessage` / `sendFrame`，公共广播中 frame 一次编码、多连接复用。
  - 引入 thread-local `SendBufferPool`，复用 payload/frame 临时 buffer；超大 buffer 不缓存，避免内存长期膨胀。
- 验证：
  - 测试断言 room/game broadcast 的 `encodedFrames == 1`；`send_buffer_pool_test` 覆盖容量复用与 oversized buffer 丢弃。
- 体现能力：
  - 能识别多人在线服务中广播路径的重复工作。
  - 优化点具体且可验证，避免空泛写“提高性能”。
- 简历写法：
  - “优化多人广播发送路径，将同一条 WebSocket 广播从 N 次 frame 编码降为 1 次编码多连接复用，并用线程本地 buffer 池降低临时内存分配。”

## A-tier：适合作为追问补充

### 7. 游戏状态机一致性与边界条件修复

- 代表提交：`544c7e9`, `192ab9f`, `b558eda`, `6adb52a`, `a1940f5`, `39f5190`。
- 典型问题：
  - 弃牌堆 pop 在输入校验之前执行，导致无效 slot 时卡牌永久丢失。
  - 活跃回合中重复 start game 会重置 deck/turn/round。
  - 空牌堆抽牌需要返回错误并进入合理结算路径。
  - 平局比较应基于本轮实际分数而不是累计/错误字段。
  - 最终轮中玩家断线/离开时，需要正确扣减 pending final turn 并维护 caller index。
- 适合讲法：
  - 这些不如网络/并发硬，但能体现“先校验再改状态”“状态机不可逆操作要谨慎”“边界条件要写回归测试”。

### 8. 房间大厅与访问控制闭环

- 代表提交：`4882d72`, `71cdbe2`。
- 典型问题：
  - 早期 join room 只适合 demo，缺少真实在线大厅、房间列表、申请、邀请、审批、过期、满房/开局校验。
  - 房间销毁/移动端适配后，要同步维护 lobby player、room list、access inbox。
- 适合讲法：
  - 如果面试官追问业务复杂度，可以讲如何用 RoomAccessRecord 建模申请/邀请状态流转，但简历主 bullets 不建议优先放它。

## 推荐简历排序

如果简历只能写 3 条，建议这样压缩：

1. 网络协议健壮性 + WebSocket 升级：TCP framing、partial send、坏帧恢复、WebSocket binary Protobuf。
2. 服务端权威 + 并发同步：连接会话绑定、状态机校验、codec 生命周期、锁边界、SIGPIPE/deadlock 修复。
3. 断线重连 + 广播优化：StateSync 快照恢复玩家上下文；广播 frame 一次编码多连接复用。

如果有 4-5 条，再补：

- 游戏状态机边界修复：先校验再修改状态、活跃回合防重启、最终轮断线计数。
- 大厅访问控制：申请/邀请/审批/过期流程和越权校验。

## 不建议优先写的内容

- 纯 Unity 动画/音效/素材修复；
- “页面跳转 bug”“按钮布局”等表现层问题；
- 没有压测数据支撑的 QPS/高并发数字；
- 分布式、数据库、缓存一致性等项目没有覆盖的能力。
