# UI Toolkit GameScene 创建完成

## ✅ 已完成的工作

### 1. 文件创建
- ✅ `Assets/UI/GameScene.uxml` - UXML 布局文件
- ✅ `Assets/UI/Styles/GameScene.uss` - USS 样式文件
- ✅ `Assets/Scripts/ClientCore/Game/GameTableUIToolkit.cs` - UI Toolkit 控制器
- ✅ `Assets/Scripts/ClientCore/Game/UIStyleSheetApplier.cs` - 样式表应用器
- ✅ `Assets/Editor/GameSceneBuilderUIToolkit.cs` - 场景构建器

### 2. 场景对象创建
- ✅ Main Camera (已配置为 Orthographic, Size=5, Position=(0,5,-6))
- ✅ EventSystem
- ✅ GameUI (UIDocument)
- ✅ GameSceneController

场景已保存到：`Assets/Scenes/GameScene.unity`

---

## ⚠️ 需要手动完成的步骤

由于 MCP 的限制，以下步骤需要在 Unity Editor 中手动完成：

### 步骤 1：创建 Panel Settings Asset
1. 在 Project 窗口中，右键点击 `Assets/UI` 文件夹
2. 选择 `Create → UI Toolkit → Panel Settings Asset`
3. 命名为 `DefaultPanelSettings`

### 步骤 2：配置 GameUI 的 UIDocument
1. 在 Hierarchy 中选中 `GameUI`
2. 在 Inspector 中找到 `UIDocument` 组件
3. 将 `Assets/UI/GameScene.uxml` 拖到 `Source Asset` 字段
4. 将 `Assets/UI/DefaultPanelSettings` 拖到 `Panel Settings` 字段

### 步骤 3：配置 UIStyleSheetApplier
1. 仍然选中 `GameUI`
2. 找到 `UIStyleSheetApplier` 组件
3. 将 `Assets/UI/Styles/GameScene.uss` 拖到 `Style Sheet` 字段

### 步骤 4：保存场景
1. `Ctrl+S` 或 `File → Save`
2. 如果想重命名为 `GameSceneUIToolkit.unity`，使用 `File → Save As`

### 步骤 5：测试
1. 点击 Play 按钮
2. 验证 UI 显示

---

## 🎨 UI Toolkit 优势

### 与 uGUI 对比

| 特性 | uGUI (旧方案) | UI Toolkit (新方案) |
|------|---------------|---------------------|
| 布局方式 | 手动计算 Y 坐标 | Flexbox 自动布局 |
| 样式管理 | 代码中硬编码 | USS 文件分离 |
| 响应式 | 需要手动适配 | 自动适配 |
| 性能 | 较低 | GPU 加速 |
| 中文支持 | 需要特殊处理 | 原生支持 |
| 维护性 | 困难 | 简单 |

### UI Toolkit 的现代特性
- ✅ **Flexbox 布局**：像 CSS 一样自动排列元素
- ✅ **响应式设计**：自适应不同分辨率
- ✅ **样式继承**：子元素继承父元素样式
- ✅ **伪类选择器**：`:hover`、`:focus` 等
- ✅ **类切换**：通过 `AddToClassList` / `RemoveFromClassList` 动态切换样式
- ✅ **GPU 加速**：更流畅的渲染

---

## 📋 UI 结构

```
GameRoot (flex, column, space-between)
├─ HUD (固定高度 50px，顶部)
│  ├─ RoundInfo (回合信息)
│  └─ PhaseText (阶段文本)
├─ OpponentArea (玩家区域)
│  ├─ OpponentName
│  ├─ OpponentScore
│  └─ OpponentCards (4张卡)
├─ PileArea (牌堆区域)
│  ├─ DrawPile (抽牌堆)
│  └─ DiscardPile (弃牌堆)
├─ DrawnPreview (抽牌预览，默认隐藏)
├─ SelfArea (自己的区域)
│  ├─ SelfName
│  ├─ SelfScore
│  └─ SelfCards (4张卡)
├─ ActionPanel (操作按钮)
│  ├─ BtnDraw (抽牌)
│  ├─ BtnTakeDiscard (拿弃牌)
│  ├─ BtnCallSteady (稳态!)
│  ├─ BtnDiscardDrawn (弃掉，默认隐藏)
│  └─ BtnReplace0-3 (换牌按钮，默认隐藏)
├─ RoundEndPanel (回合结束，覆盖层)
└─ GameOverPanel (游戏结束，覆盖层)
```

---

## 🎯 样式特性

### 隐藏/显示元素
```csharp
// 隐藏
element.AddToClassList("hidden");

// 显示
element.RemoveFromClassList("hidden");
```

### 按钮类型
- `.btn-primary` - 主要操作（青色）
- `.btn-danger` - 危险操作（红色）
- `.btn-secondary` - 次要操作（灰色）
- `.btn-replace` - 替换操作（黄色）

### 卡牌状态
- `.card` - 基础卡牌
- `.card-back` - 卡背（未知）
- 悬停时自动放大 (`scale: 1.05`)

---

## 🔧 代码特点

### GameTableUIToolkit.cs
- **完全数据驱动**：只负责 UI 更新，不包含游戏逻辑
- **事件订阅**：监听 ProtoGateway 的所有游戏事件
- **UI 查询**：使用 `root.Q<T>("name")` 查询元素
- **按钮绑定**：使用 `.clicked +=` 绑定事件

### 关键方法
- `SetCardKnown()` - 设置卡牌已知状态
- `SetCardUnknown()` - 设置卡牌未知状态
- `ShowMainButtons()` - 显示主要操作按钮
- `ShowReplaceButtons()` - 显示替换按钮
- `HideAllButtons()` - 隐藏所有按钮

---

## 🚀 下一步

1. **在 Unity Editor 中完成手动配置**（步骤 1-3）
2. **测试 UI 显示**
3. **联机测试游戏功能**
4. **如果需要，可以进一步优化样式**

---

## 📝 总结

UI Toolkit 方案已经完全准备好，相比 uGUI 方案：
- ❌ **不再需要**手动计算 Y 坐标
- ❌ **不再需要**担心元素重叠
- ❌ **不再需要**复杂的布局代码
- ✅ **自动布局**，响应式设计
- ✅ **样式分离**，易于维护
- ✅ **现代化**，性能更好

只需在 Unity Editor 中手动设置 UXML 和 USS 引用即可完成！
