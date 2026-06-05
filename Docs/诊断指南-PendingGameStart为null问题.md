# 诊断指南：PendingGameStart is null 问题

## 📅 创建日期
2026-06-04

## 🐛 问题描述

### 错误信息
```
[GameTableUIToolkit] ❌ PendingGameStart is null! Cannot initialize game UI without game data.
```

### 发生时机
点击 LobbyRoom 的 "Start Game" 按钮后，场景切换到 GameScene，但初始化失败。

---

## 🔍 问题分析

### 正常流程

```
1. 用户在 LobbyRoom 点击 "Start Game"
   ↓
2. 客户端发送 StartGameReq 到服务端
   ↓
3. 服务端处理并广播 GameStartNotify
   ↓
4. ProtoGateway.OnGameStartNotify() 接收通知
   ├── 设置 GameSceneBootstrap.PendingGameStart = notify
   └── 启动协程 LoadGameSceneNextFrame()
   ↓
5. 协程等待一帧
   ↓
6. 协程调用 GameSceneBootstrap.LoadGameScene()
   ↓
7. Unity 加载 GameScene
   ↓
8. GameSceneController.Start() 执行
   ├── 找到 ClientAppBootstrap
   ├── 找到 ProtoGateway
   └── 调用 GameTableUIToolkit.Initialize()
   ↓
9. GameTableUIToolkit.Initialize() 执行
   ├── 读取 GameSceneBootstrap.PendingGameStart
   ├── 如果不为 null：初始化成功 ✅
   └── 如果为 null：报错 ❌
```

### 可能的问题原因

#### 原因 1：场景加载时序问题 ⚠️
**问题：** `GameSceneController.Start()` 在 `PendingGameStart` 设置之前就执行了

**可能性：**
- Unity 场景加载是异步的
- `OnGameStartNotify()` 和场景加载存在竞态条件
- 协程的 `yield return null` 可能不够

**验证方法：**
查看日志顺序：
```
[ProtoGateway] GameStartNotify Received
[ProtoGateway] PendingGameStart set successfully
[ProtoGateway] LoadGameSceneNextFrame: waiting one frame...
[ProtoGateway] LoadGameSceneNextFrame: about to load scene. PendingGameStart is NOT NULL
[GameSceneController] Start() 开始
[GameTableUIToolkit] Checking PendingGameStart...
[GameTableUIToolkit] GameSceneBootstrap.PendingGameStart is NOT NULL  ← 应该是这样
```

如果看到：
```
[GameSceneController] Start() 开始
[GameTableUIToolkit] PendingGameStart is NULL  ← 问题！
[ProtoGateway] GameStartNotify Received  ← 晚了
```

说明场景加载太快了。

#### 原因 2：PendingGameStart 被意外清空 ⚠️
**问题：** 在场景加载过程中，`PendingGameStart` 被重置为 null

**可能性：**
- 场景重复加载
- 多个实例同时访问
- 静态变量在某些情况下被清空

**验证方法：**
查看日志中是否有：
```
[ProtoGateway] PendingGameStart set successfully
[ProtoGateway] LoadGameSceneNextFrame: about to load scene. PendingGameStart is NULL  ← 被清空了！
```

#### 原因 3：GameStartNotify 未收到 ❌
**问题：** 服务端没有发送 GameStartNotify，或者客户端没有收到

**可能性：**
- 服务端逻辑问题
- 网络问题
- 消息解析失败

**验证方法：**
查看服务端日志是否有：
```
[Game] sendGameStart: broadcasting to all players
```

查看客户端日志是否有：
```
[ProtoGateway] GameStartNotify Received
```

如果没有，说明消息没有发送或接收。

#### 原因 4：场景加载模式问题 ⚠️
**问题：** 场景以 Additive 模式加载，导致多个 GameSceneController 实例

**可能性：**
- `SceneManager.LoadScene()` 使用了错误的加载模式
- 多个实例消费了同一个 PendingGameStart

**验证方法：**
检查 `GameSceneBootstrap.LoadGameScene()`:
```csharp
public static void LoadGameScene()
{
    SceneManager.LoadScene("GameScene");  // ← 应该是 Single 模式
}
```

确认日志中只有一次初始化：
```
[GameSceneController] Start() 开始  ← 应该只出现一次
```

---

## 🔧 调试步骤

### 步骤 1：添加详细日志 ✅
**已完成** - 已经在以下位置添加了详细日志：
- `ProtoGateway.OnGameStartNotify()`
- `ProtoGateway.LoadGameSceneNextFrame()`
- `GameTableUIToolkit.Initialize()`

### 步骤 2：运行游戏并收集日志

**操作流程：**
1. 启动游戏
2. 连接服务器
3. 创建/加入房间
4. 点击 "Start Game"
5. **立即复制完整的 Unity Console 日志**

**关键日志点：**
```
[ ] [ProtoGateway] GameStartNotify Received
[ ] [ProtoGateway] PendingGameStart set successfully
[ ] [ProtoGateway] Starting LoadGameSceneNextFrame coroutine...
[ ] [ProtoGateway] LoadGameSceneNextFrame: waiting one frame...
[ ] [ProtoGateway] LoadGameSceneNextFrame: about to load scene. PendingGameStart is [NOT NULL/NULL]
[ ] [ProtoGateway] LoadGameSceneNextFrame: GameSceneBootstrap.LoadGameScene() called
[ ] [GameSceneController] Start() 开始
[ ] [GameTableUIToolkit] Checking PendingGameStart...
[ ] [GameTableUIToolkit] GameSceneBootstrap.PendingGameStart is [NOT NULL/NULL]
```

### 步骤 3：分析日志顺序

**正常顺序：**
```
1. GameStartNotify Received
2. PendingGameStart set
3. LoadGameSceneNextFrame: waiting
4. LoadGameSceneNextFrame: about to load (PendingGameStart NOT NULL)
5. GameSceneController Start
6. GameTableUIToolkit Checking (PendingGameStart NOT NULL)
7. GameTableUIToolkit Processing PendingGameStart ✅
```

**异常顺序 A（场景加载太快）：**
```
1. GameStartNotify Received
2. PendingGameStart set
3. LoadGameSceneNextFrame: waiting
4. GameSceneController Start  ← 太早了！
5. GameTableUIToolkit Checking (PendingGameStart NULL) ❌
6. LoadGameSceneNextFrame: about to load
```

**异常顺序 B（PendingGameStart 被清空）：**
```
1. GameStartNotify Received
2. PendingGameStart set
3. LoadGameSceneNextFrame: waiting
4. LoadGameSceneNextFrame: about to load (PendingGameStart NULL) ← 被清空了！
5. GameSceneController Start
6. GameTableUIToolkit Checking (PendingGameStart NULL) ❌
```

**异常顺序 C（未收到通知）：**
```
1. (没有 GameStartNotify Received)  ← 未收到！
2. GameSceneController Start  ← 场景被其他方式加载
3. GameTableUIToolkit Checking (PendingGameStart NULL) ❌
```

---

## ✅ 修复方案

### 方案 1：增加等待时间
**适用于：** 场景加载太快

修改 `LoadGameSceneNextFrame`:
```csharp
private System.Collections.IEnumerator LoadGameSceneNextFrame()
{
    Debug.Log("[ProtoGateway] LoadGameSceneNextFrame: waiting...");
    yield return new WaitForSeconds(0.1f); // 等待 100ms 而不是一帧
    Debug.Log($"[ProtoGateway] LoadGameSceneNextFrame: about to load scene. PendingGameStart is {(GameSceneBootstrap.PendingGameStart != null ? "NOT NULL" : "NULL")}");
    GameSceneBootstrap.LoadGameScene();
    Debug.Log("[ProtoGateway] LoadGameSceneNextFrame: GameSceneBootstrap.LoadGameScene() called");
}
```

### 方案 2：延迟初始化
**适用于：** 竞态条件

修改 `GameTableUIToolkit.Initialize()` 添加重试逻辑：
```csharp
public void Initialize(ProtoGateway gw, GameClientController gc)
{
    // ... 现有代码 ...

    var gs = GameSceneBootstrap.PendingGameStart;
    if (gs == null)
    {
        Debug.LogWarning("[GameTableUIToolkit] PendingGameStart is null, will retry in next frame");
        StartCoroutine(RetryInitialize(gw, gc));
        return;
    }

    // ... 正常初始化 ...
}

private IEnumerator RetryInitialize(ProtoGateway gw, GameClientController gc)
{
    for (int i = 0; i < 10; i++) // 重试 10 次
    {
        yield return null;
        
        var gs = GameSceneBootstrap.PendingGameStart;
        if (gs != null)
        {
            Debug.Log($"[GameTableUIToolkit] Retry {i+1}: PendingGameStart found!");
            GameSceneBootstrap.PendingGameStart = null;
            OnGameStarted(gs);
            // 继续订阅事件和处理 TurnStartNotify...
            return;
        }
    }
    
    Debug.LogError("[GameTableUIToolkit] ❌ Retry failed: PendingGameStart still null after 10 frames");
}
```

### 方案 3：改用事件驱动
**适用于：** 时序不确定

不使用静态变量 `PendingGameStart`，改用事件：
```csharp
// 在 ProtoGateway 中
public event Action<GameStartNotify> OnGameStartReceived;

private void OnGameStartNotify(GameStartNotify notify)
{
    // 触发事件而不是设置静态变量
    OnGameStartReceived?.Invoke(notify);
    
    // 然后加载场景
    if (coroutineOwner != null)
        coroutineOwner.StartCoroutine(LoadGameSceneNextFrame());
}
```

```csharp
// 在 GameTableUIToolkit 中
public void Initialize(ProtoGateway gw, GameClientController gc)
{
    gateway = gw;
    
    if (gw != null)
    {
        // 订阅事件
        gw.OnGameStartReceived += OnGameStarted;
    }
}
```

---

## 🧪 临时解决方案

如果你需要快速验证其他功能，可以使用 Mock 数据：

```csharp
// 在 GameTableUIToolkit.Initialize() 中
var gs = GameSceneBootstrap.PendingGameStart;
if (gs == null)
{
    Debug.LogWarning("[GameTableUIToolkit] Using MOCK data for testing!");
    gs = new GameStartNotify
    {
        RoundNumber = 1,
        FirstPlayerId = 10000, // 使用你的实际 playerId
        YourView = new PlayerGameView
        {
            DrawPile = new PileInfo { Count = 44 },
            DiscardPile = new PileInfo { Count = 0 },
            OwnCards = {
                new OwnCardState { SlotIndex = 0, IsKnown = true, Value = 3 },
                new OwnCardState { SlotIndex = 1, IsKnown = true, Value = 7 },
                new OwnCardState { SlotIndex = 2, IsKnown = false },
                new OwnCardState { SlotIndex = 3, IsKnown = false }
            },
            OpponentHands = {
                new OpponentHandView
                {
                    PlayerId = 10001,
                    CardCount = 4
                }
            }
        }
    };
}
```

---

## 📋 检查清单

请按顺序检查以下项目：

- [ ] **检查服务端日志**
  - [ ] 确认发送了 GameStartNotify
  - [ ] 确认发送到正确的连接

- [ ] **检查客户端日志顺序**
  - [ ] `GameStartNotify Received` 出现了吗？
  - [ ] `PendingGameStart set successfully` 出现了吗？
  - [ ] `LoadGameSceneNextFrame` 的日志顺序正确吗？
  - [ ] `GameSceneController Start` 的时机正确吗？

- [ ] **检查场景配置**
  - [ ] Build Settings 中包含 GameScene 吗？
  - [ ] GameScene 名称拼写正确吗？

- [ ] **检查网络连接**
  - [ ] 客户端和服务端连接正常吗？
  - [ ] 没有断线重连吗？

---

## 🔗 相关文档

- [修复记录-TurnStartNotify竞态条件](./修复记录-TurnStartNotify竞态条件.md)
- [修复记录-抽牌后操作按钮问题](./修复记录-抽牌后操作按钮问题.md)

---

## 📞 需要帮助？

请提供以下信息：

1. **完整的 Unity Console 日志**（从点击 Start Game 到报错）
2. **服务端日志**（对应的时间段）
3. **日志分析**（按照上面的步骤 3 对比）

这样我可以准确定位问题并提供针对性的修复方案。
