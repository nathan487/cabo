# 对局桌面 UI 设计文档

> 日期：2026-06-03
> 范围：网络版客户端对局界面 MVP

## 1. 概述

在现有房间流程基础上，新增独立的 `GameScene` 对局场景。收到服务端 `GameStartNotify` 后场景切换，桌面 UI 根据玩家数量自动适配布局。

## 2. 架构

```
ProtoGateway (跨场景 DontDestroyOnLoad)
    │
    ▼
GameTableUI (GameScene 主控制器)
    │
    ├── PlayerAreaView[]   (根据人数 2-4 个，自适应位置)
    │   └── CardView[4]    (每个玩家 4 张卡牌，已知/未知两种状态)
    ├── PileView (抽牌堆+弃牌堆)
    ├── GameActionPanel (操作按钮面板)
    └── GameOverPanel (结算/游戏结束)
```

### 数据流

```
ProtoGateway 收到消息 → 触发 GameClientController 事件 → GameTableUI 更新

GameTableUI 不直接发请求 → 通过 event/callback 让 GameClientController 调 ProtoGateway.Send()
```

## 3. 场景与文件

### 新增场景
- `Assets/Scenes/GameScene.unity` — 新对局场景（SceneBuilder 构建）

### 新增脚本 (Assets/Scripts/ClientCore/Game/)

| 文件 | 职责 |
|------|------|
| `GameTableUI.cs` | 主控制器，初始化桌面、响应服务端消息、更新所有视图 |
| `CardView.cs` | 单张卡牌显示。数据驱动：cardValue, isKnown → 渲染数值或 "?" |
| `PlayerAreaView.cs` | 玩家区域。包含名称、分数、4个CardView、回合高亮边框 |
| `PileView.cs` | 牌堆/弃牌堆显示。数据驱动：count, topCardValue → 更新显示 |
| `GameActionPanel.cs` | 操作按钮：抽牌/从弃牌堆拿/弃掉/换槽位/喊稳态，根据回合状态启用 |
| `GameOverPanel.cs` | 亮牌结算 + 游戏结束排名 |
| `GameTableLayout.cs` | 纯计算：根据玩家总数和自身座号计算每个玩家的 UI 位置 |
| `GameSceneBootstrap.cs` | (修改) 网络模式启动游戏时加载 GameScene |

### 新增 Prefab (Assets/Prefabs/Game/)

| Prefab | 内容 |
|--------|------|
| `CardView.prefab` | Image(背景) + TMP_Text(数值) + Image(技能图标) |
| `PlayerArea.prefab` | TMP(名称) + TMP(分数) + 4xCardView容器 + Image(高亮框) |
| `PileDisplay.prefab` | Image(牌堆外观) + TMP(数量) |

### 修改文件
- `GameSceneBootstrap.cs` — 网络模式下监听 `GameStartNotify`，加载 GameScene
- `GameClientController.cs` — 转发服务端消息到 GameTableUI

## 4. 布局算法 (GameTableLayout)

画布参考分辨率 800×600。每个 PlayerArea 约 260×100px。

### 2人

```
         [对手A: 顶部居中]

              [牌堆区]

         [自己: 底部居中]
```

### 3人 (斗地主式)

```
    [对手A: 左上]    [对手B: 右上]

               [牌堆区]

          [自己: 底部居中]
```

### 4人

```
         [对手A: 顶部居中]

 [对手B: 左侧]  [牌堆区]  [对手C: 右侧]

         [自己: 底部居中]
```

## 5. 消息处理

| 服务端消息 | 客户端响应 |
|-----------|-----------|
| `GameStartNotify` | 创建 PlayerArea×N、CardView×4N、PileView。初始化已知/未知卡牌状态 |
| `TurnStartNotify` | 高亮 currentPlayer 的 PlayerArea。启用/禁用操作按钮 |
| `DrawCardRsp` | 显示抽到的牌预览。进入"决定"模式（弃/换/技能） |
| `DiscardDrawnRsp` | 隐藏预览，弃牌堆+1，回合切换 |
| `ReplaceWithDrawnRsp` | 根据 ExchangeResult 更新卡牌，失败时显示加牌 |
| `ActionResultNotify` | 公开信息更新（牌堆数量、弃牌堆顶牌、回合切换） |
| `RoundRevealNotify` | 全场亮牌，显示结算面板 |
| `ScoreUpdateNotify` | 更新所有玩家累计分，提示翻大浪 |
| `GameOverNotify` | 显示最终排名，返回大厅按钮 |

## 6. 素材替换指南

所有美术/音效资源通过 Prefab 上的 `SerializeField` 和项目约定管理。**替换素材时无需改代码。**

### 卡牌素材

```
CardView.prefab:
  ┌──────────────────────────────────────┐
  │ [Sprite] CardBackground              │  ← 拖入卡牌背景图
  │   └─ 已知: 正面卡牌图               │
  │   └─ 未知: 卡牌背面图               │
  │                                       │
  │ [Sprite] CardFrame                   │  ← 可选边框
  │                                       │
  │ [TMP_Text] CardValue                 │  ← 字体/大小/颜色渐变可配
  │   └─ Font Asset: 后续可替换字体      │
  │   └─ Color Gradient: Asset(可配)     │
  │                                       │
  │ [Image] SkillIcon                    │  ← 3种技能各一张图标
  │   └─ PeekSelfIcon.png               │
  │   └─ SpyIcon.png                     │
  │   └─ SwapIcon.png                    │
  └──────────────────────────────────────┘

替换步骤:
  1. 准备素材图 (PNG, 建议 256x360 或类似比例)
  2. 导入 Unity: 拖入 Assets/Sprites/Cards/
  3. 打开 Assets/Prefabs/Game/CardView.prefab
  4. Inspector 中拖入新 Sprite 到 CardBackground / CardFrame 字段
  5. 调整 TMP_Text 位置/大小适配新图
  6. 保存 Prefab → 所有场景中的卡牌自动更新
```

### 卡牌数值颜色配置

```
CardView.cs 上有配置:
  [SerializeField] private Gradient valueColorGradient;
  // 0=绿色 ←→ 13=红色, Inspector 中可拖拽色标

  后续如果要自定义:
  1. 在 Inspector 中展开 Gradient
  2. 拖拽色标调整颜色
  3. 或创建 GradientAsset (右键 Create → Gradient)
```

### 玩家区域素材

```
PlayerArea.prefab:
  ┌──────────────────────────────────────┐
  │ [Image] Background                   │  ← 玩家区域背景
  │ [Image] TurnHighlight                │  ← 回合高亮边框/光效
  │ [TMP_Text] PlayerName               │  ← 名字
  │ [TMP_Text] ScoreText                │  ← 分数
  │ [Transform] CardsContainer          │  ← CardView 实例化目标
  └──────────────────────────────────────┘
```

### 牌堆素材

```
PileDisplay.prefab:
  ┌──────────────────────────────────────┐
  │ [Image] PileImage                    │  ← 牌堆外观
  │ [TMP_Text] CountText                │  ← "剩余: XX"
  │ [Image] TopCardPreview              │  ← 弃牌堆顶牌(小图)
  └──────────────────────────────────────┘
```

### 桌面背景

GameScene 中的 Canvas 背景 Image → 替换 Sprite 即可。

### 音乐/音效

```
推荐结构: Assets/Audio/
  ├── BGM/
  │   ├── lobby_bgm.mp3      (大厅)
  │   └── game_bgm.mp3       (对局)
  └── SFX/
      ├── draw_card.wav      (抽牌)
      ├── discard_card.wav   (弃牌)
      ├── replace_card.wav   (替换)
      ├── skill_peek.wav     (偷看)
      ├── skill_spy.wav      (间谍)
      ├── skill_swap.wav     (交换)
      ├── call_steady.wav    (喊稳态)
      ├── round_end.wav      (结算)
      ├── game_win.wav       (胜利)
      └── game_lose.wav      (失败)

替换步骤:
  1. 导入音频文件到对应文件夹
  2. 在 GameTableUI 的 Inspector 中拖入 AudioClip
  3. 或创建 AudioManager 单例，统一管理所有音效引用
```

### 动画

```
使用 Unity Animator + AnimationClip:

CardView Animator:
  - FlipToKnown:   卡牌翻转 → 显示正面  (Trigger: "Reveal")
  - FlipToUnknown: 卡牌翻转 → 显示背面  (Trigger: "Hide")
  - DealIn:        发牌飞入动画           (Trigger: "Deal")
  - DiscardOut:    弃牌飞出动画           (Trigger: "Discard")

PlayerArea Animator:
  - TurnHighlight: 高亮边框闪烁           (Bool: "IsCurrentTurn")
  - WinCelebration: 胜利粒子/缩放         (Trigger: "Win")

替换步骤:
  1. 双击 AnimationClip 打开 Animation 窗口
  2. 调整关键帧、时长、曲线
  3. 或替换整个 AnimationClip 文件
```

## 7. 游戏状态机 (客户端)

```
WaitingForGame  → 收到 GameStartNotify
    ↓
Playing         → 收到 TurnStartNotify (轮到你了)
    ↓
DecidingDrawn   → 已抽牌，等待选择（弃/换/技能）
    ↓
ChoosingReplace → 已选替换模式，等待选槽位
    ↓
SkillActive     → 技能面板激活
    ↓
RoundReveal     → 亮牌结算
    ↓
GameOver        → 最终排名
```

## 8. MVP 范围

### 实现
- [x] GameScene + GameTableUI 骨架
- [x] 2/3/4 人自适应布局
- [x] 卡牌显示（已知值/未知?）
- [x] 操作按钮面板
- [x] 回合高亮
- [x] 结算面板 + 游戏结束面板
- [x] 素材替换方式文档化

### 暂缓
- 动画（发牌/翻牌/弃牌特效）
- 音效
- 技能图标
- 3人/4人实际测试（服务端+客户端）
- 断线重连 UI

---

## 附录A：卡牌可见性规则（服务端实现）

### 自己的牌

| 场景 | 可见性 | 服务端 knownSlots |
|------|--------|------------------|
| 开局 | 前2张正面，后2张背面 | `[0]=true, [1]=true, [2]=false, [3]=false` |
| 抽牌替换 | 新牌正面（看到了值） | `knownSlots[slot]=true` |
| 从弃牌堆替换 | 新牌正面（弃牌是明的） | `knownSlots[slot]=true` |
| 偷看自己 (7-8) | 被看牌永久正面 | `knownSlots[slot]=true` |
| 交换 (11-12) | **换进来的是背面**（盲换） | `knownSlots[slot]=false` |
| 失败加牌 | 新牌正面 | `knownSlots.push_back(true)` |

### 对手的牌

| 场景 | 可见性 |
|------|--------|
| 正常 | 全部背面，只能看到数量 |
| 间谍 (9-10) | 目标牌短暂正面 1-2 秒 → 恢复背面 |
| 亮牌结算 | 全部公开（RoundRevealNotify） |

### 弃牌堆

| 场景 | 可见性 |
|------|--------|
| 顶牌 | 所有人可见（面朝上） |
| 下面牌 | 只显示总数 |

---

## 附录B：操作广播规范

| 操作 | 公开信息 | 私密信息 |
|------|---------|---------|
| 抽牌 | PlayerA 从牌库抽了1张，牌库-1 | 牌值（DrawCardRsp 私发） |
| 弃牌 | PlayerA 弃掉牌 [值]（进弃牌堆顶） | 是否为技能牌 |
| 替换成功 | PlayerA 与槽位 [n] 替换，旧牌 [值] 进弃牌 | 换入牌值（抽牌来源时） |
| 替换失败 | PlayerA 尝试换槽位 [0,1] 失败 +1牌 | — |
| 弃牌堆拿 | PlayerA 拿走弃牌 [值]，与槽位 [n] 替换 | — |
| 偷看自己 | PlayerA 看了自己第 [n] 张 | 牌值 |
| 间谍 | PlayerA 看了 PlayerB 第 [n] 张 | 牌值（私发给A，显示1-2秒） |
| 交换 | PlayerA(槽[n]) 与 PlayerB(槽[m]) 交换 | 双方都不知道换进来的是什么 |
| 喊稳态 | PlayerA 喊了稳态！剩余 N 回合 | — |

## 附录C：Proto 改动

```proto
// game.proto ActionResultNotify 新增字段
int32 source_slot = 13;  // 己方操作槽位
int32 target_slot = 14;  // 目标槽位（spy/swap）
```

## 附录D：服务端改动点

| 文件 | 改动 |
|------|------|
| `game.proto:202-203` | 新增 source_slot=13, target_slot=14 |
| `GameService.h:130-131` | sendActionResult 新增 srcSlot/dstSlot 参数 |
| `GameService.cc:236-261` | sendActionResult 填写槽位字段 |
| `GameService.cc:765-766` | 交换后 knownSlots=false（盲换） |
| 所有 sendActionResult 调用点 | 补齐槽位参数 |
