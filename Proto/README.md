# 《血糖卡波》Protobuf 协议文档

> 生成日期：2026-06-01 | 基于 GDD v1.1

---

## 一、Proto 文件清单

| 文件 | 包名 | C# 命名空间 | 职责 |
|------|------|-------------|------|
| `common.proto` | `game.common` | `Game.Common` | 共享枚举、基础类型、ErrorInfo、技能参数 |
| `room.proto` | `game.room` | `Game.Room` | 房间创建/加入/离开/准备/开始、房间状态广播 |
| `game.proto` | `game.game` | `Game.Game` | 回合流程、抽牌/替换/技能/喊稳态、亮牌计分 |
| `sync.proto` | `game.sync` | `Game.Sync` | 心跳、重连、全量状态同步 |
| `messages.proto` | `game.messages` | `Game.Messages` | ClientMessage / ServerMessage 统一信封 |

---

## 二、传输层

### 2.1 连接方式

- **Transport**: TCP 长连接 / WebSocket（推荐 WebSocket 兼容 Cloudflare Tunnel）
- **帧格式**:
    - TCP：`[4字节大端长度] + [protobuf 序列化数据]`
    - WebSocket：单条二进制帧 payload = protobuf（无需额外长度前缀）
- **序列化对象**: `messages.ClientMessage`（上行）/ `messages.ServerMessage`（下行）

### 2.2 解包流程

```
TCP 解包流程:
    1. 读取前4字节 → uint32 big-endian → 消息长度 N
    2. 读取后续 N 字节
    3. 反序列化为 ServerMessage
    4. 检查 server_seq（排序/去重）
    5. switch(oneof payload) → 分发到对应处理器

WebSocket 解包流程:
    1. 读取一条 binary frame（payload 即 protobuf）
    2. 反序列化为 ServerMessage
    3. 检查 server_seq（排序/去重）
    4. switch(oneof payload) → 分发到对应处理器

服务端收到数据:
    1. 按 TCP/WS 对应方式拿到 payload
    2. 反序列化为 ClientMessage
    3. switch(oneof payload) → 分发到对应业务逻辑
```

### 2.3 序号机制

| 字段 | 方向 | 用途 |
|------|------|------|
| `ClientMessage.seq` | 上行 | 客户端递增序号，服务端用于幂等处理和请求追踪 |
| `ServerMessage.server_seq` | 下行 | 服务端全局递增序号，客户端用于排序和丢包检测 |
| `ServerMessage.server_time_ms` | 下行 | 服务端毫秒时间戳，客户端用于时间同步 |

---

## 三、消息流详解

### 3.1 房间生命周期

```
┌─────────┐     ┌─────────┐     ┌─────────┐
│ 客户端A  │     │ 服务端   │     │ 客户端B  │
│ (房主)   │     │         │     │         │
└────┬────┘     └────┬────┘     └────┬────┘
     │               │               │
     │ CreateRoomReq │               │
     │──────────────>│               │
     │               │               │
     │ CreateRoomRsp │               │
     │<──────────────│               │
     │ (room_id,     │               │
     │  room_code,   │               │
     │  player_id,   │               │
     │  session_token)│              │
     │               │               │
     │               │  JoinRoomReq  │
     │               │<──────────────│
     │               │               │
     │               │  JoinRoomRsp  │
     │               │──────────────>│
     │               │               │
     │               │PlayerJoinNotify (广播)
     │<──────────────│──────────────>│
     │               │               │
     │  ReadyReq     │               │
     │──────────────>│               │
     │               │               │
     │               │PlayerReadyNotify (广播)
     │<──────────────│──────────────>│
     │               │               │
     │StartGameReq   │               │  (全员准备后，房主操作)
     │──────────────>│               │
     │               │               │
     │               │RoomStartNotify (广播)
     │<──────────────│──────────────>│
     │               │               │
     │               │GameStartNotify (广播)
     │<──────────────│──────────────>│
     │ (包含 PlayerGameView:       │
     │  自己当前牌区状态)            │
```

### 3.2 选项A：抽牌堆暗摸（两步操作）

```
当前玩家                    服务端                     其他玩家
    │                         │                         │
    │  TurnStartNotify        │  TurnStartNotify        │
    │<────────────────────────│────────────────────────>│
    │                         │                         │
    │  DrawCardReq            │                         │
    │────────────────────────>│                         │
    │                         │                         │
    │  DrawCardRsp            │                         │
    │<────────────────────────│                         │
    │  (card_id, value,       │  (仅发给当前玩家)         │
    │   skill_type)           │                         │
    │                         │                         │
    │  ├─ 决策 A1: 直接弃掉   │                         │
    │  │  DiscardDrawnReq     │                         │
    │  │─────────────────────>│                         │
    │  │                      │  ActionResultNotify     │
    │  │<─────────────────────│────────────────────────>│
    │  │                      │  (discard_pile 更新；   │
    │  │                      │   若为7-12可触发技能)  │
    │  │                      │                         │
    │  ├─ 决策 A2: 替换       │                         │
    │  │  ReplaceWithDrawnReq │                         │
    │  │  (slot_indices)      │                         │
    │  │─────────────────────>│                         │
    │  │                      │  ActionResultNotify     │
    │  │<─────────────────────│────────────────────────>│
    │  │                      │  (discard_pile 更新,    │
    │  │                      │   exchange_result,      │
    │  │                      │   不暴露旧牌数值)        │
    │  │                      │                         │
    │  └─ 决策 A3: 使用技能    │                         │
    │     UseSkillReq         │                         │
    │     (skill_params)      │                         │
    │    ────────────────────>│                         │
    │     (仅直接弃掉7-12后)   │                         │
    │     → 见 3.3 技能流程    │                         │
```

多张替换校验由服务端完成：`slot_indices` 为 1 张时直接成功；为多张时，被换出的牌点数必须完全相同。失败时，玩家保留原选择的牌，换入牌加入自己的牌区；如果尝试换出 3 张及以上且失败，额外从牌库抽 1 张加入自己的牌区。

### 3.3 技能子流程

#### 3.3.1 偷看自己（7-8）/ 间谍（9-10）

```
当前玩家                    服务端                     其他玩家
    │                         │                         │
    │  UseSkillReq            │                         │
    │  (peek_self/skill_params)│                        │
    │────────────────────────>│                         │
    │                         │                         │
    │  UseSkillRsp            │                         │
    │  (peeked_value)         │  (仅发给当前玩家)         │
    │<────────────────────────│                         │
    │                         │                         │
    │                         │  ActionResultNotify     │
    │<────────────────────────│────────────────────────>│
    │                         │  (skill_used=类型,      │
    │                         │   discard_pile 更新,    │
    │                         │   turn_ended=true)      │
```

#### 3.3.2 交换（11-12）

```
当前玩家          服务端            目标玩家          其他玩家
    │               │                 │                 │
    │ UseSkillReq   │                 │                 │
    │ (swap:        │                 │                 │
    │  target_player│                 │                 │
    │  own_slot,    │                 │                 │
    │  target_slot) │                 │                 │
    │──────────────>│                 │                 │
    │               │                 │                 │
    │ UseSkillRsp   │                 │                 │
    │ (swap_occurred│                 │                 │
    │  =true)       │                 │                 │
    │<──────────────│────────────────>│                 │
    │               │                 │                 │
    │               │ ActionResultNotify                │
    │<──────────────│────────────────│────────────────>│
    │               │ (swap_occurred=true,              │
    │               │  不暴露交换的牌值)                  │
```

> 13 没有技能，不能触发看+换流程。

### 3.4 选项B：从弃牌堆明拿

```
当前玩家                    服务端                     其他玩家
    │                         │                         │
    │  TakeFromDiscardReq     │                         │
    │  (slot_indices)         │                         │
    │────────────────────────>│                         │
    │                         │                         │
    │  TakeFromDiscardRsp     │                         │
    │<────────────────────────│                         │
    │                         │                         │
    │                         │  ActionResultNotify     │
    │<────────────────────────│────────────────────────>│
    │                         │  (action_type=TAKE_FROM_DISCARD,
    │                         │   discard_pile 更新,    │
    │                         │   exchange_result,      │
    │                         │   turn_ended=true)      │
```

### 3.5 选项C：喊稳态

```
当前玩家                    服务端                     其他玩家
    │                         │                         │
    │  CallSteadyReq          │                         │
    │────────────────────────>│                         │
    │                         │                         │
    │  CallSteadyRsp          │                         │
    │<────────────────────────│                         │
    │                         │                         │
    │                         │  ActionResultNotify     │
    │<────────────────────────│────────────────────────>│
    │                         │  (action_type=CALL_STEADY,
    │                         │   phase→FINAL_ROUND)    │
    │                         │                         │
    │  (其他玩家各走最后一回合, 不能再喊稳态)              │
    │                         │                         │
    │                         │  RoundRevealNotify      │
    │<────────────────────────│────────────────────────>│
    │                         │  (全员亮牌, 计分)        │
    │                         │                         │
    │                         │  ScoreUpdateNotify      │
    │<────────────────────────│────────────────────────>│
    │                         │                         │
    │  ┌── 如果游戏结束 ──┐    │                         │
    │  │ GameOverNotify   │    │                         │
    │  │<─────────────────│────────────────────────>│   │
    │  └──────────────────┘    │                         │
    │  ┌── 如果继续下一轮 ──┐  │                         │
    │  │ TurnStartNotify   │   │                         │
    │  │<──────────────────│────────────────────────>│  │
    │  └───────────────────┘   │                         │
```

### 3.6 重连流程

```
客户端                        服务端
    │                           │
    │  (断线，TCP 连接断开)       │
    │                           │  (启动 60s 倒计时)
    │                           │
    │  (重新建立 TCP/WS 连接)     │
    │                           │
    │  ReconnectReq             │
    │  (session_token,          │
    │   last_server_seq)        │
    │──────────────────────────>│
    │                           │  (验证 session_token)
    │                           │  (检查是否在 60s 窗口)
    │  ReconnectRsp             │
    │  (player_id, room_id,     │
    │   is_in_game)             │
    │<──────────────────────────│
    │                           │
    │  StateSyncNotify          │  (全量状态快照)
    │  (room_state,             │
    │   game_state:             │
    │   - round_number          │
    │   - phase                 │
    │   - current_turn_player   │
    │   - player_view (自己的牌) │
    │   - scores                │
    │   - pending_step)         │
    │<──────────────────────────│
    │                           │
    │  (客户端覆盖本地状态,        │
    │   恢复至当前游戏进度)        │
```

**超时处理**:
- 60s 内重连: 恢复全部状态，继续游戏
- 60-120s: 进入托管模式（自动结束回合）
- >120s: 判定离局，从游戏中移除

---

## 四、服务端/客户端职责

### 4.1 服务端（C++ / muduo）

| 职责 | 说明 |
|------|------|
| **权威随机** | 洗牌、抽牌、所有随机结果由服务端生成，客户端不可信任 |
| **状态管理** | 维护每个房间的完整游戏状态（RoomState + GameState） |
| **信息过滤** | 向不同玩家下发不同视图：自己的牌（部分已知）、对手的牌（仅数量） |
| **规则校验** | 校验每个客户端操作是否合法（是否轮到、操作是否有效） |
| **状态广播** | 关键操作后广播 ActionResultNotify；计分后广播分数 |
| **序号管理** | 维护 server_seq，每条消息递增，支持客户端排序和去重 |
| **掉线检测** | 心跳超时检测，管理重连窗口和托管逻辑 |
| **房间生命周期** | 创建、销毁、空房间回收 |

### 4.2 客户端（Unity / C#）

| 职责 | 说明 |
|------|------|
| **输入收集** | 将玩家操作（点击牌堆、点击卡牌、点击技能按钮）序列化为 Req |
| **状态渲染** | 根据服务端下发的 PlayerGameView 渲染 UI |
| **本地记忆** | 客户端本地缓存已见过的牌值（如初始2张、偷看过的牌）——服务端也会下发给重连恢复，客户端可以做乐观UI |
| **序号追踪** | 维护 ClientMessage.seq（递增），追踪 last_received_server_seq |
| **断线处理** | 检测断线→自动重连→发送 ReconnectReq→应用 StateSyncNotify |
| **表现层** | 动画、音效、UI 过渡——所有视觉表现与协议解耦 |

---

## 五、信息隐藏策略

### 5.1 原则

> 服务端权威，客户端只看到"该看到的"。

| 信息 | 自己可见 | 对手可见 | 说明 |
|------|---------|---------|------|
| 自己初始2张暗牌 | ✅ 数值 | ❌ | 开局秘密查看任意2张 |
| 自己另外2张暗牌 | ❌（直到被偷看/替换） | ❌ | 全程盲 |
| 通过"偷看自己"看到的牌 | ✅ 数值 | ❌ | skill 7-8 |
| 通过"间谍"看到的对手牌 | ✅ 数值 | ❌ | skill 9-10，但对手不知道你看到了什么 |
| 抽到的暗牌 | ✅ 数值（在决策期间） | ❌ | 仅当前玩家在 DrawCardRsp 中看到 |
| 弃牌堆顶部 | ✅ 数值 | ✅ 数值 | 公开信息 |
| 抽牌堆剩余数 | ✅ 数量 | ✅ 数量 | 公开信息 |
| 交换结果 | ❌（不自动揭示牌值） | ❌ | 11-12 交换只公开发生过交换 |
| 亮牌 | ✅ 全部 | ✅ 全部 | 每轮结束 |

### 5.2 实现方式

服务端为每个玩家构造独立的 `PlayerGameView`:
- `own_cards` 仅包含该玩家自己的4张牌，`is_known` 标记是否可见
- `opponent_hands` 仅包含 `player_id` + `card_count`，永远不暴露对手的牌值
- 所有技能结果的广播（`ActionResultNotify`）只公开"谁对谁做了什么"，不公开牌值

---

## 六、错误码定义

| code | 含义 |
|------|------|
| 0 | 成功 |
| 1001 | 房间不存在 |
| 1002 | 房间已满 |
| 1003 | 房间码无效 |
| 1004 | 玩家不在房间中 |
| 1005 | 不是你的回合 |
| 1006 | 无效的操作 |
| 1007 | 游戏已开始 |
| 1008 | 游戏未开始 |
| 1009 | 人数不足（至少2人） |
| 1010 | 不是房主 |
| 1011 | 已在准备状态 |
| 1012 | 当前阶段不能执行此操作 |
| 1013 | 无此技能 |
| 1014 | 目标无效 |
| 1015 | 槽位无效 |
| 1016 | session_token 无效或过期 |
| 1017 | 重连超时 |
| 1018 | 最终轮不能喊稳态 |
| 1099 | 服务端内部错误 |

---

## 七、帧格式参考代码

### 7.1 编码（C++ 服务端，TCP 示例）

```cpp
// 发送 ServerMessage
std::string serialized;
server_msg.SerializeToString(&serialized);

uint32_t len = htonl(static_cast<uint32_t>(serialized.size()));
conn->send(std::string_view(reinterpret_cast<const char*>(&len), 4));
conn->send(serialized);
```

### 7.2 解码（C# 客户端，TCP 示例）

```csharp
// 从 NetworkStream 读取
byte[] lenBuf = new byte[4];
await stream.ReadAsync(lenBuf, 0, 4);
if (BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
uint length = BitConverter.ToUInt32(lenBuf, 0);

byte[] msgBuf = new byte[length];
await stream.ReadAsync(msgBuf, 0, (int)length);

var serverMsg = ServerMessage.Parser.ParseFrom(msgBuf);
// 分发到处理器
switch (serverMsg.PayloadCase) { ... }
```

---

## 八、验证清单

- [x] 所有 field number 唯一且不重复
- [x] 所有 enum 以 0 = UNKNOWN 开始
- [x] 消息名使用 PascalCase + Req/Rsp/Notify 后缀
- [x] 字段名使用 snake_case，id 字段以 _id 结尾
- [x] 服务端权威：客户端只发送操作意图
- [x] 所有下行消息通过 ServerMessage.server_seq 排序/去重
- [x] ReplaceWithDrawnReq / TakeFromDiscardReq 支持多张替换与失败加牌结果
- [x] ScoreUpdateNotify 支持同轮多个玩家触发 100 分减半
- [x] RoundScoreDetail 支持神风特攻队标记
- [x] 状态结构可直接渲染 UI 无需额外计算
- [x] 隐藏信息保护：对手手牌不暴露数值
- [x] 重连支持：StateSyncNotify 包含全量快照和挂起步骤
- [x] 掉线托管：心跳检测 + TurnStepState 可在重连后恢复中断的操作
- [x] 没有发明 GDD 中不存在的功能

---

## 九、包依赖关系

```
messages.proto
├── common.proto (基础类型、枚举)
├── room.proto   ──── common.proto
├── game.proto   ──── common.proto
└── sync.proto   ──── common.proto + room.proto + game.proto
```
