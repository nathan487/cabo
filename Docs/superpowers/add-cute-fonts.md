# UI Toolkit 添加可爱中文字体指南

## 推荐的可爱中文字体

### 免费可商用字体

1. **站酷小薇LOGO体**
   - 风格：圆润、可爱、清新
   - 下载：https://www.zcool.com.cn/special/zcoolfonts/
   - 文件名：`zcool-xiaowe.ttf`

2. **阿里巴巴普惠体**
   - 风格：现代、友好、易读
   - 下载：https://www.alibabafonts.com/
   - 文件名：`Alibaba-PuHuiTi-Regular.ttf`

3. **思源黑体（Source Han Sans）**
   - 风格：清晰、现代、专业
   - 下载：https://github.com/adobe-fonts/source-han-sans/releases
   - 文件名：`SourceHanSansCN-Regular.otf`

4. **站酷文艺体**
   - 风格：手写、可爱、活泼
   - 下载：https://www.zcool.com.cn/special/zcoolfonts/
   - 文件名：`zcool-wenyi.ttf`

5. **优设标题黑**
   - 风格：醒目、圆润、可爱
   - 下载：https://www.uisdc.com/uisdc-first-free-font
   - 文件名：`YouSheBiaoTiHei.ttf`

---

## 添加字体到 Unity 的步骤

### 步骤 1：下载字体
1. 从上面的链接下载你喜欢的字体
2. 推荐：**站酷小薇LOGO体** 或 **优设标题黑**（最可爱）

### 步骤 2：导入字体到 Unity
1. 将字体文件（`.ttf` 或 `.otf`）复制到：
   ```
   Assets/UI/Fonts/
   ```
2. Unity 会自动识别字体

### 步骤 3：创建 Font Asset（UI Toolkit 专用）

#### 方法 A：使用 TextMesh Pro 字体资源（推荐）
1. 窗口菜单：`Window → TextMeshPro → Font Asset Creator`
2. 设置：
   - **Source Font File**: 选择你导入的字体
   - **Character Set**: `Custom Characters`
   - **Custom Character List**: 粘贴常用中文字符（见下方）
   - **Sampling Point Size**: `Auto Sizing`
   - **Padding**: `5`
   - **Packing Method**: `Optimum`
3. 点击 `Generate Font Atlas`
4. 点击 `Save` 保存为 `.asset` 文件

#### 方法 B：直接使用 TTF/OTF（简单但性能略低）
- 直接在 USS 中引用 TTF/OTF 文件

---

## 常用中文字符列表（复制使用）

```
第轮回合等待游戏开始你对手分抽牌拿弃稳态换卡结束返回大厅成功失败玩家行动已知未连接服务器房间创建加入准备开始离开剩余张点击选择技能使用取消确定继续下一轮总计得赢输平局排名胜利
0123456789★☆✓✗！？()（）：:，,。.、
```

---

## 在 USS 中使用字体

### 方法 1：全局字体（推荐）

编辑 `Assets/UI/Styles/GameScene.uss`：

```css
/* 根元素 - 设置全局字体 */
.game-root {
    flex-grow: 1;
    background-color: rgb(25, 35, 55);
    justify-content: space-between;
    align-items: center;
    -unity-font-definition: url('project://database/Assets/UI/Fonts/zcool-xiaowe.ttf');
}
```

### 方法 2：特定元素字体

```css
/* 只为标题设置可爱字体 */
.round-info,
.phase-text {
    -unity-font-definition: url('project://database/Assets/UI/Fonts/zcool-xiaowe.ttf');
    -unity-font-style: bold;
}

/* 按钮使用不同字体 */
.action-btn {
    -unity-font-definition: url('project://database/Assets/UI/Fonts/YouSheBiaoTiHei.ttf');
}
```

### 方法 3：使用 Font Asset

如果创建了 TextMesh Pro Font Asset：

```css
.game-root {
    -unity-font-definition: url('project://database/Assets/UI/Fonts/zcool-xiaowe SDF.asset');
}
```

---

## 快速测试步骤

1. **下载字体**（推荐站酷小薇LOGO体）
2. **放到** `Assets/UI/Fonts/` 文件夹
3. **编辑** `GameScene.uss`，在 `.game-root` 中添加：
   ```css
   -unity-font-definition: url('project://database/Assets/UI/Fonts/你的字体文件名.ttf');
   ```
4. **保存** USS 文件
5. **Unity 中重新 Play** 查看效果

---

## 字体大小和样式调整

### 调整字体大小
```css
.round-info {
    font-size: 20px;  /* 增大 */
}

.player-score {
    font-size: 14px;  /* 保持小一点 */
}
```

### 字体样式
```css
-unity-font-style: normal;        /* 正常 */
-unity-font-style: bold;          /* 粗体 */
-unity-font-style: italic;        /* 斜体 */
-unity-font-style: bold-and-italic; /* 粗斜体 */
```

### 字体颜色
```css
color: rgb(255, 220, 100);  /* 温暖的黄色 */
color: rgb(255, 180, 200);  /* 粉色 */
color: rgb(150, 220, 255);  /* 淡蓝色 */
```

---

## 性能优化建议

1. **使用 Font Asset**：比直接使用 TTF 性能更好
2. **只包含需要的字符**：减小 Atlas 大小
3. **避免过多字体**：整个游戏使用 2-3 个字体即可
4. **字体备选**：可以设置多个字体，Unity 会按顺序查找

---

## 推荐配置（可爱风格）

```css
/* 全局使用站酷小薇LOGO体 */
.game-root {
    -unity-font-definition: url('project://database/Assets/UI/Fonts/zcool-xiaowe.ttf');
}

/* 标题使用粗体 */
.round-info {
    -unity-font-style: bold;
    font-size: 20px;
    color: rgb(255, 230, 150);
}

/* 阶段文本使用亮色 */
.phase-text {
    font-size: 16px;
    color: rgb(255, 200, 100);
}

/* 玩家名称 */
.player-name {
    font-size: 16px;
    color: rgb(200, 230, 255);
}

/* 按钮文字 */
.action-btn {
    font-size: 15px;
    -unity-font-style: bold;
}
```

---

## 故障排除

### 问题：字体不显示
- 检查字体文件路径是否正确
- 检查 Unity Console 是否有 `MissingAssetReference` 错误
- 确认字体文件已导入到 Unity（在 Project 窗口可见）

### 问题：中文显示为方块
- 确认字体包含中文字符
- 如果使用 Font Asset，确认字符集包含中文

### 问题：字体太大/太小
- 调整 `font-size` 值
- 或在 Panel Settings 中调整 Scale

---

## 最佳实践

1. ✅ 先测试默认字体，确保 UI 正常工作
2. ✅ 选择 1-2 个主要字体，保持一致性
3. ✅ 使用免费可商用字体，避免版权问题
4. ✅ 优先使用 Font Asset 而不是直接使用 TTF
5. ✅ 保持字体文件大小合理（< 5MB）

---

现在就可以开始添加可爱的字体啦！推荐从**站酷小薇LOGO体**开始尝试！🎨✨
