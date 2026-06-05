# 修复记录：重复GameSceneController导致crash

## 📅 修复日期
2026-06-04

## 🐛 问题描述

### 用户反馈
游戏运行时，第二个客户端（Player 10001）看起来一直在等待轮到自己的回合，实际上客户端已经crash了。

### 症状
1. 玩家10001进入游戏后，显示"Opponent's turn"（正常）
2. 玩家10000完成抽牌→替换操作
3. 玩家10001看起来仍在等待
4. 用户手动关闭了客户端

### 关键日志

#### 第二个客户端日志（Player 10001）

**异常1：GameSceneController.Start() 被调用了两次**
```
第74行：[GameSceneController] ===== Start() 开始 =====
         [GameTableUIToolkit] PendingGameStart is NOT NULL ✅
         [GameTableUIToolkit] ✅ Initialize complete

第101行：[GameSceneController] ===== Start() 开始 =====  ← 重复！
          [GameTableUIToolkit] PendingGameStart is NULL ❌
          [GameTableUIToolkit] ❌ PendingGameStart is null!
```

**异常2：收到Turn 2通知后立即crash**
```
第126行：[ProtoGateway] ========== TurnStartNotify Received ==========
第128行：[ProtoGateway] Current Player: 10001  ← 确实轮到10001了
第130行：[ProtoGateway] Round: 1
第132行：[ProtoGateway] OnTurnStart subscribers: 2  ← 有两个订阅者！
第133行：[MessageCodec] Parse error  ← crash
第134行：Input System module state changed to: ShutdownInProgress
```

---

## 🔍 问题分析

### Root Cause

**GameScene.unity 中存在两个 GameSceneController 实例**

通过以下命令验证：
```bash
grep -i "GameSceneController" "unity dev/Client/Assets/Scenes/GameScene.unity"
```

结果：
```
m_Name: GameSceneController
m_Name: GameSceneController  ← 重复！
```

### 影响链

```
GameScene 有两个 GameSceneController
    ↓
两个实例都调用 Start()
    ↓
两个实例都调用 GameTableUIToolkit.Initialize()
    ↓
第一个实例：成功初始化，消费 PendingGameStart
第二个实例：初始化失败，PendingGameStart 已经为 null
    ↓
两个实例都订阅了 ProtoGateway 的事件
    ↓
ProtoGateway.OnTurnStart subscribers: 2
    ↓
收到 TurnStartNotify 时
    ↓
两个实例都尝试处理同一个消息
    ↓
可能的冲突导致 MessageCodec Parse error
    ↓
客户端 crash
```

### 为什么第一个客户端（Player 10000）没有crash？

**猜测：**
- 第一个客户端也有同样的重复实例问题
- 但玩家10000在自己的回合操作后，没有收到新的TurnStartNotify就切换到了对方回合
- 在对方回合（Player 10001）时，两个实例都调用了`HideAllButtons()`，这个操作是幂等的，不会引发冲突
- 所以第一个客户端表面上运行正常

**而第二个客户端：**
- 收到TurnStartNotify（Turn 2, Current Player: 10001）
- 两个实例都尝试调用`ShowMainButtons()`
- 可能在UI操作或状态更新时产生冲突
- 触发了Parse error并crash

---

## ✅ 修复方案

### 方案1：删除重复的GameSceneController（推荐）

**步骤：**
1. 在Unity Editor中打开 `Assets/Scenes/GameScene.unity`
2. 在Hierarchy窗口中查找 "GameSceneController"
3. 应该看到两个同名的GameObject
4. **删除其中一个**
5. 保存场景

**验证：**
- Hierarchy中只应该有一个GameSceneController
- 运行游戏，检查日志中`[GameSceneController] Start() 开始`只出现一次

### 方案2：在代码中添加单例保护（防御性）

即使删除了重复实例，也应该添加保护机制。

---

## 🎯 修复效果

### 修复前

| 方面 | 表现 |
|------|------|
| GameSceneController实例 | ❌ 2个实例 |
| 事件订阅者数量 | ❌ 2个订阅者 |
| TurnStartNotify处理 | ❌ 两个实例都处理，冲突crash |
| 玩家体验 | ❌ 看起来在等待，实际已crash |

### 修复后

| 方面 | 表现 |
|------|------|
| GameSceneController实例 | ✅ 1个实例 |
| 事件订阅者数量 | ✅ 1个订阅者 |
| TurnStartNotify处理 | ✅ 正确处理，不冲突 |
| 玩家体验 | ✅ 正常显示按钮，可以操作 |

---

## 🧪 测试验证

### 测试步骤

1. **验证场景中只有一个Controller**
   - 打开GameScene.unity
   - Hierarchy中搜索"GameSceneController"
   - 确认只有一个结果

2. **验证初始化只执行一次**
   - 运行游戏，检查Console日志
   - 确认`[GameSceneController] Start() 开始`只出现1次

3. **验证事件订阅者数量**
   - 两个客户端连接
   - 查看日志：`OnTurnStart subscribers: 1`（不是2）

4. **验证回合切换正常**
   - 玩家10000：抽牌 → 替换
   - 玩家10001：应该显示">>> YOUR TURN <<<"并可以操作

---

## 📊 日志对比

### 修复前

```
[GameSceneController] Start() 开始  ← 第1次
[GameSceneController] Start() 开始  ← 第2次，重复！
[ProtoGateway] OnTurnStart subscribers: 2  ← 两个订阅者
[MessageCodec] Parse error  ← crash
```

### 修复后（期望）

```
[GameSceneController] Start() 开始  ← 只有1次
[ProtoGateway] OnTurnStart subscribers: 1  ← 只有1个订阅者
[GameTableUIToolkit] ✅ Buttons shown for my turn  ← 正常工作
```

---

## 🔗 相关文档

- [修复记录-抽牌后操作按钮问题](./修复记录-抽牌后操作按钮问题.md)
- [诊断指南-PendingGameStart为null问题](./诊断指南-PendingGameStart为null问题.md)

---

## 💡 经验教训

1. **Unity场景管理** - 定期检查Hierarchy中是否有重复的单例对象
2. **单例模式最佳实践** - 对于应该唯一的GameObject，在代码层面添加保护
3. **事件订阅调试** - 订阅者数量日志帮助快速发现问题
4. **日志的重要性** - 详细的日志帮助远程诊断问题

---

## ✅ 修复清单

- [ ] 打开GameScene.unity
- [ ] 删除重复的GameSceneController（保留一个）
- [ ] 保存场景
- [ ] 运行两个客户端测试
- [ ] 验证日志中只有一个Start()调用
- [ ] 验证回合切换正常
- [ ] 验证不再crash
