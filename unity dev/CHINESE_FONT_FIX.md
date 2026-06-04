# 中文字体显示问题修复指南

## 问题描述
Build 出来的 exe 文件无法显示中文字符。

## 根本原因
UI Toolkit 默认使用的字体不包含中文字符集，在 Editor 中可以回退到系统字体，但 Build 后没有这个回退机制。

## 解决方案

### 方案 A：使用 Unity 自带字体（推荐）
1. 下载并导入支持中文的字体（如 Noto Sans SC）
2. 将字体文件放入 `Assets/Resources/Fonts/`
3. 在 USS 中引用该字体

### 方案 B：使用 SDF 字体资源
1. 创建 TextMesh Pro Font Asset（包含中文字符集）
2. 在 USS 中引用 TMP 字体

### 方案 C：回退到 uGUI Text（临时方案）
如果 UI Toolkit 字体问题难以解决，可以暂时使用 uGUI 的 Text 组件。

---

## 详细步骤（方案 A）

### 1. 下载中文字体
推荐使用 Google Noto Sans SC（开源免费）：
- 下载地址：https://fonts.google.com/noto/specimen/Noto+Sans+SC
- 或使用系统字体：`C:\Windows\Fonts\msyh.ttc`（微软雅黑）

### 2. 导入字体到 Unity
```
Assets/
  Resources/
    Fonts/
      NotoSansSC-Regular.ttf  (或 msyh.ttc)
```

**重要**：字体必须放在 `Resources` 文件夹中才能在 Build 后动态加载！

### 3. 在 USS 中引用字体
在 `GameScene.uss` 的开头添加：

```css
/* 全局字体设置 */
* {
    -unity-font: resource('Fonts/NotoSansSC-Regular');
}
```

或者只对特定元素设置：

```css
.unity-label {
    -unity-font: resource('Fonts/NotoSansSC-Regular');
}

.unity-button {
    -unity-font: resource('Fonts/NotoSansSC-Regular');
}
```

### 4. 重新 Build
- `File → Build Settings → Build`
- 测试 exe 文件是否正确显示中文

---

## 方案 B 详细步骤（使用 TextMesh Pro）

### 1. 创建 TMP Font Asset
1. `Window → TextMeshPro → Font Asset Creator`
2. **Source Font File**: 选择支持中文的 TTF 字体
3. **Character Set**: 选择 `Custom Characters`
4. **Custom Character List**: 输入游戏中使用的所有中文字符，例如：
   ```
   你对手等待游戏开始回合轮稳态抽牌拿弃换卡确认取消分本轮结束继续返回大厅
   玩家行动选择一张替换成功失败间谍交换偷看技能发动查看
   0123456789
   ```
5. **Render Mode**: `SDFAA` (推荐)
6. **Atlas Resolution**: `1024x1024`（根据字符数量调整）
7. 点击 `Generate Font Atlas`
8. 保存为 `Assets/Resources/Fonts/ChineseFont SDF.asset`

### 2. 在 USS 中引用 TMP 字体
```css
* {
    -unity-font-definition: resource('Fonts/ChineseFont SDF');
}
```

---

## 快速测试方法

### 测试 1：在 Editor 中验证
1. 打开 GameScene
2. Play 模式下观察中文是否正常显示

### 测试 2：Build 后验证
1. `File → Build Settings → Build`
2. 运行生成的 exe
3. 确认中文是否正常显示

---

## 常见问题

### Q1: 字体文件必须放在 Resources 文件夹吗？
**A**: 是的。UI Toolkit 的 `resource()` 函数只能加载 Resources 文件夹中的资源。

### Q2: 可以使用系统字体吗？
**A**: 不推荐。系统字体路径在不同机器上可能不同，且 Build 后无法访问。

### Q3: 字体文件会增加 Build 大小吗？
**A**: 会。一个完整的中文字体约 5-20MB。如果使用 TMP Font Asset 只包含需要的字符，可以减小到 1-2MB。

### Q4: UI Toolkit 不显示字体怎么办？
**A**: 检查以下几点：
1. 字体文件是否在 `Assets/Resources/Fonts/` 目录
2. USS 中的路径是否正确（不需要 `Assets/Resources/` 前缀，只写 `Fonts/xxx`）
3. 字体文件是否包含所需的中文字符
4. 重新 Build（有时需要清理缓存：`Edit → Preferences → GI Cache → Clear Cache`）

---

## 当前项目使用的字符集

根据 `GameScene.uxml` 和 `GameTableUIToolkit.cs`，游戏中使用的中文字符包括：

```
等待游戏开始第轮回合你对手抽牌拿弃稳态换卡确认取消分本结束继续返回大厅
玩家行动选择一张替换成功失败间谍交换偷看技能发动查看到
0123456789
```

如果使用 TMP Font Asset，将以上字符作为 Custom Character List 即可。

---

## 推荐配置

### 开发阶段
- 使用完整的 TTF 字体文件（方便添加新文本）
- 字体大小：~10-15MB

### 发布阶段
- 使用 TMP Font Asset，只包含需要的字符
- 字体大小：~1-2MB
- 提升加载速度

---

生成时间: 2026-06-04
