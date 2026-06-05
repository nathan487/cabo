# 修复步骤：删除重复的GameSceneController

## ✅ 代码层防御已完成

已在 `GameSceneController.cs` 中添加单例保护：
- `Awake()` 中检测重复实例并自动销毁
- 即使场景中有2个GameObject，也只有第一个会运行
- 这是**临时防御措施**，但不是最佳方案

## 🎯 推荐操作：在Unity Editor中删除重复GameObject

### 步骤

1. **打开Unity Editor**
   ```
   打开项目: unity dev/Client
   ```

2. **打开GameScene**
   ```
   Assets/Scenes/GameScene.unity
   ```

3. **在Hierarchy窗口中搜索**
   ```
   在Hierarchy搜索框输入: GameSceneController
   ```

4. **应该看到两个同名GameObject**
   ```
   - GameSceneController  ← 第一个
   - GameSceneController  ← 第二个（重复）
   ```

5. **选中其中一个并删除**
   - 右键 → Delete
   - 或按 Delete 键

6. **保存场景**
   - File → Save Scene
   - 或 Ctrl+S

### 验证

删除后，再次搜索应该只看到**一个** GameSceneController。

## 🧪 测试验证

### 方法1：查看Console日志

运行游戏后，检查Console：

**修复前（有问题）：**
```
[GameSceneController] Singleton instance set on 'GameSceneController'
[GameSceneController] ===== Start() 开始 =====
[GameSceneController] ⚠️ Duplicate instance detected on 'GameSceneController'. Destroying to prevent conflicts.
```

**修复后（理想）：**
```
[GameSceneController] Singleton instance set on 'GameSceneController'
[GameSceneController] ===== Start() 开始 =====
```
（只出现一次，没有警告）

### 方法2：验证事件订阅者数量

在两个客户端测试游戏时，查看日志：

**修复前：**
```
[ProtoGateway] OnTurnStart subscribers: 2  ← 错误！
```

**修复后：**
```
[ProtoGateway] OnTurnStart subscribers: 1  ← 正确！
```

### 方法3：验证不再crash

1. 启动两个客户端（PlayerA 和 PlayerB）
2. 创建房间并开始游戏
3. PlayerA抽牌 → 替换，结束回合
4. PlayerB应该看到 ">>> YOUR TURN <<<" 并可以正常操作
5. **不应该crash或卡住**

## 📊 当前状态

| 防护层 | 状态 | 说明 |
|--------|------|------|
| 代码层单例保护 | ✅ 已添加 | 自动销毁重复实例 |
| 场景中删除重复 | ⚠️ 待操作 | 需要在Unity Editor中手动删除 |

## 💡 为什么需要手动删除？

虽然代码层已经添加了防护，但：

1. **性能考虑**
   - 重复GameObject仍会被实例化
   - 然后在Awake中被销毁
   - 这是不必要的开销

2. **清洁度考虑**
   - Scene文件中不应该有重复的单例GameObject
   - 可能是复制粘贴或合并冲突导致的

3. **最佳实践**
   - 单例GameObject应该在场景中唯一
   - 代码保护只是防御措施，不是常态

## ⚠️ 如果无法在Unity中操作

如果当前无法打开Unity Editor，当前的代码防护已经足够让游戏正常运行。但建议：

- 下次打开Unity时记得删除重复GameObject
- 或者通知有Unity访问权限的团队成员处理

## 🔗 相关文档

- [修复记录-重复GameSceneController导致crash.md](./修复记录-重复GameSceneController导致crash.md) - 问题详细分析
- [诊断指南-PendingGameStart为null问题.md](./诊断指南-PendingGameStart为null问题.md) - 相关问题

---

**当前修复状态：代码层防御已完成 ✅ | Unity场景清理待操作 ⚠️**
