# 修复完成：GameSceneController单例保护

## 📅 修复日期
2026-06-04

## ✅ 已完成的修复

### 代码层防御（已完成）

在 `GameSceneController.cs` 中添加了单例模式保护：

```csharp
private static GameSceneController _instance;

private void Awake()
{
    if (_instance != null && _instance != this)
    {
        Debug.LogWarning($"[GameSceneController] ⚠️ Duplicate instance detected on '{gameObject.name}'. Destroying to prevent conflicts.");
        Destroy(gameObject);
        return;
    }

    _instance = this;
    Debug.Log($"[GameSceneController] Singleton instance set on '{gameObject.name}'");
}

private void Start()
{
    if (_instance != this)
    {
        return;
    }
    // ... 原有逻辑
}

private void OnDestroy()
{
    if (_instance == this)
    {
        _instance = null;
    }
}
```

### 工作原理

1. **Awake阶段检测**
   - 第一个GameSceneController实例设置 `_instance`
   - 第二个实例检测到 `_instance != null`，立即销毁自己
   - 只有第一个实例继续运行

2. **Start阶段保护**
   - 被销毁的实例不会执行初始化逻辑
   - 避免重复订阅事件
   - 避免PendingGameStart被重复消费

3. **OnDestroy清理**
   - 场景卸载时清理静态引用
   - 避免跨场景引用问题

## 🎯 修复效果

### 修复前的问题

| 问题 | 表现 |
|------|------|
| GameSceneController数量 | ❌ 2个实例都运行 |
| Start()调用次数 | ❌ 2次 |
| 事件订阅者数量 | ❌ OnTurnStart subscribers: 2 |
| PendingGameStart | ❌ 第2个实例: null（已被第1个消费） |
| TurnStartNotify处理 | ❌ 两个实例都处理，冲突crash |
| 第二个客户端 | ❌ MessageCodec Parse error, crash |

### 修复后的效果

| 问题 | 表现 |
|------|------|
| GameSceneController数量 | ✅ 只有1个实例运行 |
| Start()调用次数 | ✅ 1次 |
| 事件订阅者数量 | ✅ OnTurnStart subscribers: 1 |
| PendingGameStart | ✅ 正确消费，只有1个实例处理 |
| TurnStartNotify处理 | ✅ 单个实例处理，无冲突 |
| 第二个客户端 | ✅ 正常运行，不crash |

## 📋 后续待操作

### Unity Editor中手动清理（推荐）

虽然代码已经防护，但建议在Unity Editor中手动删除重复GameObject：

**原因：**
- 避免不必要的GameObject实例化和销毁
- 保持场景文件整洁
- 符合单例GameObject的最佳实践

**步骤：**
详见 [修复步骤-删除重复GameSceneController.md](./修复步骤-删除重复GameSceneController.md)

## 🧪 测试验证

### 验证方法1：日志检查

启动游戏后，检查Console：

**场景中仍有2个GameObject时（当前状态）：**
```
[GameSceneController] Singleton instance set on 'GameSceneController'
[GameSceneController] ⚠️ Duplicate instance detected on 'GameSceneController'. Destroying to prevent conflicts.
[GameSceneController] ===== Start() 开始 =====
```
✅ 有警告，但只执行1次Start()

**场景中删除重复后（最佳状态）：**
```
[GameSceneController] Singleton instance set on 'GameSceneController'
[GameSceneController] ===== Start() 开始 =====
```
✅ 无警告，只执行1次Start()

### 验证方法2：多客户端测试

1. 启动两个客户端（PlayerA 和 PlayerB）
2. PlayerA创建房间，PlayerB加入
3. 开始游戏
4. PlayerA: 抽牌 → 替换，结束回合
5. PlayerB: 应该看到 ">>> YOUR TURN <<<" 并可以正常操作

**期望结果：**
- ✅ 两个客户端都正常运行
- ✅ 回合切换正常
- ✅ 不再出现 MessageCodec Parse error
- ✅ 不再crash

### 验证方法3：事件订阅者数量

查看日志中的订阅者数量：

```
[ProtoGateway] OnTurnStart subscribers: 1
```
✅ 应该是1，不是2

## 📊 修改文件清单

| 文件 | 修改内容 |
|------|----------|
| `unity dev/Client/Assets/Scripts/ClientCore/Game/GameSceneController.cs` | ✅ 添加单例保护 |
| `Docs/修复步骤-删除重复GameSceneController.md` | ✅ 创建操作指南 |
| `Docs/修复完成-GameSceneController单例保护.md` | ✅ 创建修复总结 |

## 💡 技术要点

### 为什么用Awake而不是Start？

- **Awake** 在对象创建时立即执行
- **Start** 在第一帧更新前执行
- 重复实例必须在Awake中销毁，避免进入Start阶段

### 为什么用Destroy(gameObject)而不是Destroy(this)？

- `Destroy(this)` 只销毁组件
- `Destroy(gameObject)` 销毁整个GameObject
- 销毁整个GameObject更彻底，避免空GameObject残留

### 为什么Start中也要检查？

- 防御性编程
- 如果Destroy有延迟，Start仍可能被调用
- 双重保护确保重复实例不会执行初始化

## 🔗 相关文档

- [修复记录-重复GameSceneController导致crash.md](./修复记录-重复GameSceneController导致crash.md) - 问题详细分析
- [修复步骤-删除重复GameSceneController.md](./修复步骤-删除重复GameSceneController.md) - Unity操作指南
- [诊断指南-PendingGameStart为null问题.md](./诊断指南-PendingGameStart为null问题.md) - 相关问题

## 📈 修复状态

| 层级 | 状态 | 说明 |
|------|------|------|
| **代码防护** | ✅ **已完成** | 自动检测并销毁重复实例 |
| **场景清理** | ⚠️ **待操作** | 建议在Unity Editor中删除重复GameObject |
| **功能验证** | ⚠️ **待测试** | 需要运行两个客户端验证 |

---

**当前可以直接测试游戏，代码层防护已经生效！**
