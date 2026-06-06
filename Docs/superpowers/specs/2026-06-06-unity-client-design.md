# Unity Client Design — 基于 CLI 客户端迁移

> 2026-06-06 | Phase 递进开发

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| UI 框架 | UI Toolkit (UXML/USS) | 代码驱动范式，直接翻译 CLI 状态机 |
| 代码组织 | 纯 C# + 薄 MonoBehaviour 壳 | 可独立测试，结构 1:1 对应 CLI |
| 开发节奏 | Phase 递进，每 Phase 可联调验收 | 风险在协议同步，需实时验证 |

## 架构全景

```
GameBootstrap (MonoBehaviour)
  └── Update() → GameFlow.Tick()
        ├── NetworkClient  (TCP + protobuf 帧编解码)
        ├── GameState      (状态 + UpdateFromMessage)
        ├── MessageRouter  (消息分发)
        └── UIManager      (UI Toolkit)
              ├── RoomPanel
              ├── GameTablePanel (PlayerArea×4, PileArea, ActionMenu)
              ├── SkillPanel
              └── RevealPanel
```

## 核心循环（翻译自 CLI 的 gameLoop）

```
每次 Tick():
1. drainMessages() — read all TCP → deserialize → MessageRouter
2. CheckTransitions() — GameSubState state machine
3. UIManager.Render(state) — refresh UI Toolkit
4. HandleInput() — process button clicks
```

## Phase 1: 连接 → 房间 → 看到手牌

**目标**：1 Unity + 3 CLI 联调，走完 create/join → ready → start → 4 人手牌布局

**文件清单**：

| 文件 | CLI 对应 | 职责 |
|------|---------|------|
| `NetworkClient.cs` | NetworkClient.cpp | TCP 连接 + `[4字节len][pb]` 编解码 + send/receive |
| `GameState.cs` | GameState.h/cpp | 状态字段 + `UpdateFromMessage(ServerMessage)` |
| `GameFlow.cs` | ClientApp.cpp | 连接 → roomFlow → waitingRoom → gameLoop 状态机 |
| `GameBootstrap.cs` | main.cpp | MonoBehaviour 入口，`Update()` 调 `Tick()` |
| `UIManager.cs` | UIRenderer.cpp | 根 VisualElement，管理面板切换 |
| `RoomPanel.cs` | waitingRoomLoop 渲染 | 房间 UI（玩家列表、ready 按钮） |
| `GameTablePanel.cs` | gameLoop 渲染 | 桌面 UI（4 人手牌 + 牌堆 + 操作菜单） |

**数据流**：

```
TCP → NetworkClient.receive() → ServerMessage
  → MessageRouter → GameState.UpdateFromMessage()
    → UIManager.Render() → UI Toolkit refresh
```

**drain-then-decide**：每 Tick 先 drain 所有 pending 消息，再根据最新 GameState 渲染。

## Phase 2: 游戏操作

- Draw / Discard / Replace (单+多) / Take from discard (单+多)
- Skill (PeekSelf / Spy / Swap) + 2s 结果展示
- 对手操作广播显示
- 手牌数变化 + 卡牌状态同步

## Phase 3: 结算 + 多轮

- CABO → 最终轮 → Round Reveal 面板
- 回合间 ready/start
- 多轮游戏
- GameOver 排名

## 消息处理（直接翻译 GameState.cpp）

所有 `GameState::updateFromMessage` 的 20+ 个 handler 直接翻译为 C#。核心要处理的 8 个 sync/display 挑战见 `Docs/CURRENT_TASK.md`。

## UI Toolkit 布局

```
┌─────────────────────────────────┐
│        Draw Pile / Discard       │
│    ┌─────────────────────┐      │
│    │   对手2 (对面)       │      │
│    └─────────────────────┘      │
│  ┌─────────┐       ┌─────────┐  │
│  │ 对手3   │       │ 对手1   │  │
│  └─────────┘       └─────────┘  │
│    ┌─────────────────────┐      │
│    │   自己 (底部)        │      │
│    └─────────────────────┘      │
│      [Draw] [Discard] [CABO]    │
└─────────────────────────────────┘
```

对手顺序：`(myIndex+2)%4` 对面, `(myIndex+3)%4` 左, `(myIndex+1)%4` 右
