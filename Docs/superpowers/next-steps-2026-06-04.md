# 下一步行动计划 - 2026-06-04

## 📋 当前状态总结

### ✅ 已完成
1. **服务端**：完整游戏逻辑（`GameService.cc`）✅
2. **网络层**：TCP + protobuf 双端打通 ✅
3. **房间系统**：创建/加入/准备/开始 ✅
4. **UI Toolkit**：GameScene 使用 UI Toolkit 重构完成 ✅
5. **ProtoGateway**：所有游戏操作接口已实现 ✅

### 🎯 验收目标
**启动服务端和两个客户端，完成一场完整的游戏对局**

---

## 🔧 需要完成的工作

### 1. ✅ GameSceneController 修复
**状态**: 已完成

**修改内容**:
- 修改 `GameSceneController.cs` 支持 `GameTableUIToolkit`
- 优先查找 UI Toolkit 版本，回退到 uGUI 版本

**文件**: `Assets/Scripts/ClientCore/Game/GameSceneController.cs`

---

### 2. 🔴 UI Toolkit 场景配置（需要手动操作）

**需要在 Unity Editor 中完成**:

#### 步骤 A：检查场景中的 GameUI 对象
1. 打开 `Assets/Scenes/GameScene.unity`
2. 确认 Hierarchy 中存在 `GameUI` GameObject
3. 确认 `GameUI` 有以下组件：
   - `UIDocument`
   - `GameTableUIToolkit` (脚本)
   - `UIStyleSheetApplier` (脚本)

#### 步骤 B：配置 UIDocument
1. 选中 `GameUI` GameObject
2. 在 Inspector → `UIDocument` 组件：
   - **Source Asset**: 拖入 `Assets/UI/GameScene.uxml`
   - **Panel Settings**: 需要创建 Panel Settings Asset

#### 步骤 C：创建 Panel Settings Asset
1. 在 Project 窗口，右键 `Assets/UI` 文件夹
2. 选择 `Create → UI Toolkit → Panel Settings Asset`
3. 命名为 `DefaultPanelSettings`
4. 将其拖到 UIDocument 的 `Panel Settings` 字段

#### 步骤 D：配置 UIStyleSheetApplier
1. 仍然选中 `GameUI`
2. 在 Inspector → `UIStyleSheetApplier` 组件：
   - **Style Sheet**: 拖入 `Assets/UI/Styles/GameScene.uss`

#### 步骤 E：保存场景
- `Ctrl+S` 保存场景

---

### 3. 🟡 验证 UI 显示

**方法 A：使用现有的 Tester 脚本**
- 文件已存在：`GameTableUIToolkitTester.cs`
- 检查是否能正确显示所有 UI 元素

**方法 B：直接运行 MainMenuScene**
1. 打开 `Assets/Scenes/MainMenuScene.unity`
2. 点击 Play
3. 连接服务器 → 创建房间 → 开始游戏
4. 观察 GameScene 是否正确加载和显示

---

### 4. 🔴 当前缺失的功能

根据代码审查，以下功能**可能**需要补充：

#### A. 技能系统 UI
**当前状态**: `GameTableUIToolkit.cs` 没有技能面板相关代码

**需要添加**:
- 技能弹窗 UI（偷看/间谍/交换）
- 技能目标选择（选择自己的卡/对手的卡）
- `UseSkillReq` 的发送逻辑

**参考**: 旧版 `GameUI.cs` (L658-808) 的技能系统实现

#### B. 多张交换 UI
**当前状态**: `ReplaceWithDrawn` 和 `TakeFromDiscard` 只支持单张

**需要添加**:
- 多张卡牌选择 UI（可以选择多张同值卡）
- 修改 `ProtoGateway.ReplaceWithDrawn` 和 `TakeFromDiscard` 支持多个 `slotIndex`

**协议支持**: 
```protobuf
message ReplaceWithDrawnReq {
    repeated int32 slot_indices = 5; // 已支持
}
```

#### C. 对手信息显示
**当前状态**: 只显示一个对手

**需要支持**: 
- 2-4 人游戏
- 动态生成多个对手区域
- 根据 `GameStartNotify.YourView.OpponentHands` 动态创建

---

### 5. 🟢 推荐的测试顺序

#### 第一阶段：UI 显示验证
1. ✅ 完成 Unity Editor 中的手动配置
2. 运行 GameScene，检查 UI 元素是否正确显示
3. 验证所有按钮、卡牌、牌堆是否可见

#### 第二阶段：房间流程测试
1. 启动服务端
2. 两个客户端：创建房间 + 加入房间
3. 准备 → 开始游戏
4. 验证是否能进入 GameScene

#### 第三阶段：基础游戏流程
1. 验证初始状态显示（4张卡，2张已知）
2. 测试抽牌 → 弃牌
3. 测试抽牌 → 替换单张
4. 测试回合切换

#### 第四阶段：完整功能
1. 实现技能系统 UI
2. 测试 7-12 技能牌
3. 实现多张交换
4. 测试喊稳态 → 结算

---

## 📝 待办任务清单

### 紧急（阻塞测试）
- [ ] 在 Unity Editor 中配置 UIDocument 和 Panel Settings
- [ ] 验证 UI Toolkit 场景能否正确显示

### 高优先级（核心功能）
- [ ] 实现技能系统 UI（偷看/间谍/交换）
- [ ] 实现多张卡牌选择 UI
- [ ] 支持多对手显示（3-4人游戏）

### 中优先级（完善体验）
- [ ] 技能卡高亮提示（7-12 弃牌时显示"可触发技能"）
- [ ] 卡牌动画（抽牌/弃牌/翻牌动画）
- [ ] 错误提示（操作失败时的 Toast）

### 低优先级（锦上添花）
- [ ] 音效（抽牌/弃牌/技能/稳态）
- [ ] 粒子特效
- [ ] 结算动画优化

---

## 🚀 快速启动验证

### 启动服务端
```bash
cd "MuduoBaseGameServer/build"
LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer
```

### 启动客户端
1. Unity Editor 打开项目
2. 打开 MainMenuScene
3. 点击 Play
4. 或者 Build 一个 exe 文件用于第二个客户端

### 测试流程
1. Client A: Connect → Create Room → 记下房间码 → Ready
2. Client B: Connect → Join Room(输入码) → Ready
3. Client A: Start Game
4. 观察是否正常进入 GameScene 并显示 UI

---

## 📂 关键文件位置

### Unity 客户端
| 文件 | 路径 | 说明 |
|------|------|------|
| GameScene | `Assets/Scenes/GameScene.unity` | UI Toolkit 版本的游戏场景 |
| UXML | `Assets/UI/GameScene.uxml` | UI 布局文件 |
| USS | `Assets/UI/Styles/GameScene.uss` | 样式文件 |
| GameTableUIToolkit | `Assets/Scripts/ClientCore/Game/GameTableUIToolkit.cs` | UI 控制器 |
| ProtoGateway | `Assets/Scripts/ClientCore/Network/ProtoGateway.cs` | 网络层 |
| GameSceneController | `Assets/Scripts/ClientCore/Game/GameSceneController.cs` | 场景控制器 |

### 服务端
| 文件 | 路径 | 说明 |
|------|------|------|
| GameService | `MuduoBaseGameServer/src/game/GameService.cc` | 游戏逻辑 |
| RoomService | `MuduoBaseGameServer/src/room/RoomService.cc` | 房间管理 |

### 协议
| 文件 | 路径 | 说明 |
|------|------|------|
| game.proto | `Proto/game.proto` | 游戏协议定义 |
| messages.proto | `Proto/messages.proto` | 消息信封 |

---

## ⚠️ 已知问题

1. **UI Toolkit 需要手动配置**：MCP 无法自动配置 UIDocument 引用
2. **技能系统 UI 缺失**：需要补充技能面板和交互逻辑
3. **多张交换 UI 缺失**：当前只支持单张替换
4. **只显示一个对手**：需要动态支持 2-4 人

---

## 💡 建议

1. **先验证 UI 显示**：确保 UI Toolkit 配置正确，所有元素可见
2. **小步快跑**：每完成一个功能就测试一次
3. **参考旧代码**：`GameUI.cs` 中的技能系统实现可以作为参考
4. **使用 Debug.Log**：在关键位置添加日志，方便排查问题
5. **保持服务端日志**：观察服务端输出，了解请求是否正确处理

---

## 📞 下一步行动

**立即行动**:
1. 在 Unity Editor 中完成 UI Toolkit 配置（Panel Settings + UXML + USS）
2. 运行 MainMenuScene，尝试创建房间并开始游戏
3. 观察 GameScene 是否正确加载

**如果 UI 显示正常**:
→ 开始实现技能系统 UI

**如果 UI 显示异常**:
→ 检查 Console 错误信息
→ 确认 UIDocument 引用是否正确
→ 尝试使用 Tester 脚本验证

---

生成时间: 2026-06-04
