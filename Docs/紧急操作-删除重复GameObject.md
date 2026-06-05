# 🚨 紧急操作：删除 Unity 场景中重复的 GameSceneController

## 📅 日期
2026-06-04 23:26

## 🔍 问题确认

从客户端日志可以看到：

```
[GameSceneController] ✅ Singleton instance set on 'GameSceneController'
[GameSceneController] ⚠️ Duplicate instance detected on 'GameSceneController'. Destroying to prevent conflicts.
```

**问题：** Unity 场景中有**两个** `GameSceneController` GameObject，导致初始化混乱。

**后果：** 
- 第二个实例被销毁
- 但 `Start()` 方法没有被正确调用
- `GameTableUIToolkit.Initialize()` 永远不会执行
- 游戏停留在 "waiting for game" 状态

---

## ✅ 解决方案：在 Unity Editor 中删除重复的 GameObject

### 步骤 1：打开 Unity Editor

```bash
cd "unity dev/Client"
# 打开 Unity Hub 或直接打开项目
```

### 步骤 2：打开 GameScene

1. 在 Project 窗口中导航到 `Assets/Scenes/`
2. 双击 `GameScene.unity` 打开场景

### 步骤 3：在 Hierarchy 中查找重复的 GameObject

1. 点击 Hierarchy 窗口顶部的搜索框
2. 输入：`GameSceneController`
3. 按 Enter

**你应该看到两个 GameObject：**
```
▼ GameScene
  ├── GameSceneController    ← 第一个
  ├── GameSceneController    ← 第二个（重复！）
  └── ...
```

### 步骤 4：删除其中一个

**方法 A：查看哪个有正确的组件**
1. 选中第一个 `GameSceneController`
2. 查看 Inspector 面板，确认有 `GameSceneController` 组件
3. 如果正确，保留这个，删除另一个
4. 如果不确定，随便删除一个（因为代码会保护单例）

**方法 B：直接删除一个**
1. 在 Hierarchy 中**右键点击**其中一个 `GameSceneController`
2. 选择 **Delete**
3. 或者选中后按 **Delete** 键

### 步骤 5：验证只剩一个

在 Hierarchy 搜索框中再次搜索 `GameSceneController`，应该只看到**一个**结果。

### 步骤 6：保存场景

1. **File → Save** (或 Ctrl+S)
2. 确认场景已保存（标题栏的 * 号消失）

### 步骤 7：重新构建

1. **File → Build Settings**
2. 点击 **Build** 按钮
3. 等待构建完成

---

## 🎯 预期结果

删除重复 GameObject 后，重新构建并运行，日志应该显示：

```
✅ [GameSceneController] ✅ Singleton instance set on 'GameSceneController'
✅ [GameSceneController] ===== Start() 开始 =====
✅ [GameSceneController] PendingGameStart status: AVAILABLE
✅ [GameSceneController] Found GameTableUIToolkit, about to initialize...
✅ [GameTableUIToolkit] Subscribing to gateway events...
✅ [GameTableUIToolkit] GameSceneBootstrap.PendingGameStart is NOT NULL
✅ [GameTableUIToolkit] ✅ Buttons shown for my turn
```

**不应该看到：**
```
❌ [GameSceneController] ⚠️ Duplicate instance detected
```

---

## 🔧 故障排查

### 问题1：找不到 GameSceneController

**可能原因：** GameObject 名字不同

**解决方案：**
1. 在 Hierarchy 中查找包含 `GameSceneController` 组件的 GameObject
2. 或者在 Hierarchy 中展开所有节点，手动查找
3. 或者使用 **Edit → Select All** 然后在 Inspector 中查看每个对象

### 问题2：删除后游戏仍然不工作

**可能原因：** 删除了错误的那个，或者两个都有问题

**解决方案：**
1. 确保留下的 GameObject 有 `GameSceneController` 组件
2. 在 Inspector 中检查组件配置
3. 如果不确定，可以删除两个，然后重新添加一个新的

### 问题3：不知道删除哪一个

**解决方案：随便删除一个！**
- 因为代码有单例保护
- 只要留一个就行
- 如果删错了，删除另一个再试

---

## 📋 快速检查清单

删除前：
- [ ] Unity Editor 已打开 GameScene
- [ ] Hierarchy 中搜索到 2 个 GameSceneController

删除后：
- [ ] Hierarchy 中只有 1 个 GameSceneController
- [ ] 场景已保存 (Ctrl+S)
- [ ] 重新构建可执行文件

测试：
- [ ] 启动两个客户端
- [ ] 创建房间、加入、准备
- [ ] 点击 Start Game
- [ ] 进入正常游戏界面（不是 waiting）
- [ ] 第一个玩家看到按钮
- [ ] 日志没有 "Duplicate instance" 警告

---

## 🎯 为什么会有重复的 GameObject？

可能的原因：
1. **复制粘贴** - 在场景中复制了 GameObject
2. **合并冲突** - Git 合并时产生了重复
3. **手动添加** - 不小心添加了多次
4. **Prefab 实例化** - 如果 GameSceneController 是 Prefab

**预防措施：**
- 确保 GameSceneController 在场景中只有一个实例
- 不要复制这个 GameObject
- 如果需要测试，使用 Prefab 而不是复制场景中的对象

---

## ⏰ 预计时间

- 打开 Unity Editor: 30秒
- 查找并删除重复 GameObject: 1分钟
- 保存场景: 5秒
- 重新构建: 2-5分钟
- 测试: 2分钟

**总计: 约 5-10 分钟**

---

**现在立即打开 Unity Editor 并删除重复的 GameObject！** 🎮
