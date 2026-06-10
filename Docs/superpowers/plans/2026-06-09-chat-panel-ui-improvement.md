# 聊天面板UI优化实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标：** 优化聊天面板的消息滚动、表情包选择和底部输入栏布局，提升用户体验

**架构：** 改进现有 RoomChatPanel.cs 实现，将始终显示的表情包托盘改为弹出式面板，优化消息渲染性能（增量更新而非全量重建），调整底部输入栏布局使其更清晰。

**技术栈：** Unity UI Toolkit (VisualElement, ScrollView, Button)

**当前问题分析：**
1. **消息渲染问题**：每次 `Render()` 都调用 `_messages.Clear()` 重建所有消息，导致滚动位置丢失和性能问题
2. **表情包托盘占用空间**：`_stickerScroll` 始终显示（最大高度 98-108px），占用大量垂直空间
3. **底部输入栏不够清晰**：缺少 emoji 图标按钮，发送按钮不够突出
4. **滚动体验**：虽然有 ScrollView，但因为频繁 Clear() 重建，用户滚动位置经常被重置

**改进策略：**
- 增量更新消息列表（只添加新消息，不重建已有消息）
- 表情包托盘改为弹出式面板（点击 emoji 按钮显示/隐藏）
- 底部输入栏重构：文本框 + emoji按钮 + 发送按钮

---

## 文件结构

**修改的文件：**
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs` - 核心聊天面板逻辑

**涉及的关键类和字段：**
```csharp
// RoomChatPanel.cs 新增字段
readonly Button _emojiButton;              // emoji按钮
readonly VisualElement _stickerPopup;      // 表情包弹出面板
bool _stickerPopupVisible;                 // 表情包面板显示状态
Dictionary<long, VisualElement> _messageCache; // 消息元素缓存
```

---

## Task 1: 添加表情包弹出面板控制字段

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:10-20`

- [ ] **Step 1: 在字段声明区域添加新字段**

在 `RoomChatPanel.cs` 的字段声明部分（第 17 行 `readonly Button _sendButton;` 之后）添加：

```csharp
readonly Button _emojiButton;
readonly VisualElement _stickerPopup;
bool _stickerPopupVisible;
readonly Dictionary<long, VisualElement> _messageCache = new();
```

- [ ] **Step 2: 添加必要的 using 指令**

在文件顶部（第 1-3 行）确认是否有 `System.Collections.Generic`，如果没有则添加：

```csharp
using System.Collections.Generic;
```

- [ ] **Step 3: 验证编译**

在 Unity 编辑器中等待自动编译完成，检查 Console 是否有错误。

预期：无编译错误（会有 emojiButton 未初始化警告，后续步骤会修复）

---

## Task 2: 重构底部输入栏布局 - 添加emoji按钮

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:64-100`

- [ ] **Step 1: 修改输入栏布局代码**

找到构造函数中创建 `inputRow` 的部分（约第 64-90 行），将整个 inputRow 创建代码替换为：

```csharp
var inputRow = new VisualElement();
inputRow.style.flexDirection = FlexDirection.Row;
inputRow.style.alignItems = Align.Center;
inputRow.style.flexShrink = 0;
inputRow.style.marginBottom = 4;
Root.Add(inputRow);

_input = new TextField();
_input.style.flexGrow = 1;
_input.style.minWidth = 0;
_input.style.marginRight = 4;
_input.maxLength = 120;
_input.RegisterCallback<KeyDownEvent>(evt =>
{
    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
        return;
    SendText();
    evt.StopPropagation();
});
inputRow.Add(_input);

_emojiButton = new Button(ToggleStickerPopup) { text = "😊" };
_emojiButton.style.minWidth = _compact ? 32 : 36;
_emojiButton.style.width = _compact ? 32 : 36;
_emojiButton.style.height = _compact ? 28 : 32;
_emojiButton.style.flexShrink = 0;
_emojiButton.style.marginRight = 4;
_emojiButton.style.paddingLeft = 2;
_emojiButton.style.paddingRight = 2;
_emojiButton.style.fontSize = 16;
inputRow.Add(_emojiButton);

_sendButton = new Button(SendText) { text = "发送" };
_sendButton.style.minWidth = _compact ? 48 : 58;
_sendButton.style.flexShrink = 0;
_sendButton.style.height = _compact ? 28 : 32;
_sendButton.style.paddingLeft = 8;
_sendButton.style.paddingRight = 8;
inputRow.Add(_sendButton);

_input.RegisterValueChangedCallback(_ =>
    _sendButton.SetEnabled(_flow.CanSendRoomChat && !string.IsNullOrWhiteSpace(_input.value)));
```

- [ ] **Step 2: 验证编译**

等待 Unity 编译完成，检查 Console。

预期：会有编译错误 "ToggleStickerPopup 未定义"（预期行为，下一个 Task 会添加）

---

## Task 3: 创建表情包弹出面板UI结构

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:45-63`

- [ ] **Step 1: 替换原有的 _stickerScroll 初始化代码**

找到构造函数中 `_stickerScroll` 的初始化代码（约第 51-62 行），替换为以下代码：

```csharp
// 创建弹出面板容器
_stickerPopup = new VisualElement { name = "StickerPopup" };
_stickerPopup.style.position = Position.Absolute;
_stickerPopup.style.bottom = _compact ? 38 : 42;
_stickerPopup.style.left = 4;
_stickerPopup.style.right = 4;
_stickerPopup.style.maxHeight = _compact ? 140 : 160;
_stickerPopup.style.backgroundColor = new Color(0.02f, 0.11f, 0.095f);
_stickerPopup.style.borderTopLeftRadius = 6;
_stickerPopup.style.borderTopRightRadius = 6;
_stickerPopup.style.borderBottomLeftRadius = 6;
_stickerPopup.style.borderBottomRightRadius = 6;
_stickerPopup.style.borderTopWidth = 1;
_stickerPopup.style.borderRightWidth = 1;
_stickerPopup.style.borderBottomWidth = 1;
_stickerPopup.style.borderLeftWidth = 1;
_stickerPopup.style.borderTopColor = new Color(0.14f, 0.38f, 0.32f);
_stickerPopup.style.borderRightColor = new Color(0.14f, 0.38f, 0.32f);
_stickerPopup.style.borderBottomColor = new Color(0.14f, 0.38f, 0.32f);
_stickerPopup.style.borderLeftColor = new Color(0.14f, 0.38f, 0.32f);
_stickerPopup.style.paddingLeft = 6;
_stickerPopup.style.paddingRight = 6;
_stickerPopup.style.paddingTop = 6;
_stickerPopup.style.paddingBottom = 6;
_stickerPopup.style.display = DisplayStyle.None;
Root.Add(_stickerPopup);

_stickerScroll = new ScrollView(ScrollViewMode.Vertical);
_stickerScroll.style.flexGrow = 1;
_stickerScroll.style.flexShrink = 1;
_stickerScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
_stickerScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
_stickerPopup.Add(_stickerScroll);

_stickerTray = _stickerScroll.contentContainer;
_stickerTray.style.flexDirection = FlexDirection.Row;
_stickerTray.style.flexWrap = Wrap.Wrap;
_stickerTray.style.paddingRight = 4;
```

- [ ] **Step 2: 验证编译**

等待编译完成，检查 Console。

预期：仍有 ToggleStickerPopup 未定义错误

---

## Task 4: 实现表情包弹出面板切换逻辑

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:150-165`

- [ ] **Step 1: 添加 ToggleStickerPopup 方法**

在 `SendText()` 方法之后（约第 152 行之后），添加以下方法：

```csharp
void ToggleStickerPopup()
{
    _stickerPopupVisible = !_stickerPopupVisible;
    _stickerPopup.style.display = _stickerPopupVisible ? DisplayStyle.Flex : DisplayStyle.None;
    
    if (_stickerPopupVisible)
    {
        _emojiButton.style.backgroundColor = new Color(0.12f, 0.32f, 0.27f);
        _emojiButton.text = "✖";
    }
    else
    {
        _emojiButton.style.backgroundColor = StyleKeyword.Null;
        _emojiButton.text = "😊";
    }
}
```

- [ ] **Step 2: 验证编译**

等待编译完成，检查 Console。

预期：无编译错误

---

## Task 5: 优化消息渲染 - 实现增量更新

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:103-139`

- [ ] **Step 1: 重构 Render 方法实现增量更新**

找到 `Render()` 方法（约第 103-139 行），完全替换为以下代码：

```csharp
public void Render()
{
    var messages = _flow.State.RoomChatMessages;
    var shouldScrollToBottom = false;
    
    if (messages.Count == 0)
    {
        _messages.Clear();
        _messageCache.Clear();
        AddPlaceholder("暂无房间交流");
        _lastRenderedLastMessageId = 0;
        _lastRenderedMessageCount = 0;
    }
    else
    {
        var lastMessageId = messages[messages.Count - 1].MessageId;
        shouldScrollToBottom = lastMessageId != _lastRenderedLastMessageId
            || messages.Count != _lastRenderedMessageCount;
        
        // 增量更新：只添加新消息
        if (messages.Count > _lastRenderedMessageCount)
        {
            for (var i = _lastRenderedMessageCount; i < messages.Count; i++)
            {
                var message = messages[i];
                var messageRow = CreateMessageRow(message);
                _messageCache[message.MessageId] = messageRow;
                _messages.Add(messageRow);
            }
        }
        // 消息数量减少（可能是清空或重连），重建
        else if (messages.Count < _lastRenderedMessageCount)
        {
            _messages.Clear();
            _messageCache.Clear();
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                var messageRow = CreateMessageRow(message);
                _messageCache[message.MessageId] = messageRow;
                _messages.Add(messageRow);
            }
        }
        
        _lastRenderedLastMessageId = lastMessageId;
        _lastRenderedMessageCount = messages.Count;
    }

    // 只在首次渲染时构建表情包托盘
    if (_stickerTray.childCount == 0)
    {
        RenderStickerTray();
    }
    
    var canSend = _flow.CanSendRoomChat;
    _input.SetEnabled(canSend);
    _sendButton.SetEnabled(canSend && !string.IsNullOrWhiteSpace(_input.value));
    _emojiButton.SetEnabled(canSend);
    _status.text = string.IsNullOrWhiteSpace(_flow.State.LastRoomChatError)
        ? (canSend ? "" : "进入房间后可发送消息")
        : _flow.State.LastRoomChatError;
    _status.style.display = string.IsNullOrWhiteSpace(_status.text) ? DisplayStyle.None : DisplayStyle.Flex;

    if (shouldScrollToBottom)
        ScrollMessagesToBottom();
}
```

- [ ] **Step 2: 验证编译**

等待编译完成，检查 Console。

预期：无编译错误

---

## Task 6: 更新 SendSticker 方法以关闭弹出面板

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:154-158`

- [ ] **Step 1: 修改 SendSticker 方法**

找到 `SendSticker()` 方法（约第 154-158 行），替换为以下代码：

```csharp
void SendSticker(ArtAsset sticker)
{
    _flow.SendRoomChatSticker(sticker.PackName, sticker.DisplayName);
    
    // 发送后关闭表情包面板
    _stickerPopupVisible = false;
    _stickerPopup.style.display = DisplayStyle.None;
    _emojiButton.style.backgroundColor = StyleKeyword.Null;
    _emojiButton.text = "😊";
    
    Render();
}
```

- [ ] **Step 2: 验证编译**

等待编译完成，检查 Console。

预期：无编译错误

---

## Task 7: 优化 RenderStickerTray 方法

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:245-280`

- [ ] **Step 1: 修改 RenderStickerTray 方法**

找到 `RenderStickerTray()` 方法（约第 245-280 行），替换为以下代码：

```csharp
void RenderStickerTray()
{
    _stickerTray.Clear();
    
    var stickers = PlayerProfileStore.GetStickerAssets();
    if (stickers.Count == 0)
    {
        var none = new Label("暂无表情资源");
        none.style.fontSize = 11;
        none.style.color = new Color(0.62f, 0.70f, 0.67f);
        none.style.unityTextAlign = TextAnchor.MiddleCenter;
        none.style.marginTop = 10;
        _stickerTray.Add(none);
        return;
    }

    // 显示所有表情包（弹出面板有足够空间）
    for (var i = 0; i < stickers.Count; i++)
    {
        var sticker = stickers[i];
        var button = new Button(() => SendSticker(sticker));
        button.text = "";
        button.style.width = _compact ? 40 : 44;
        button.style.height = _compact ? 40 : 44;
        button.style.minWidth = _compact ? 40 : 44;
        button.style.flexShrink = 0;
        button.style.marginLeft = 2;
        button.style.marginRight = 2;
        button.style.marginTop = 2;
        button.style.marginBottom = 2;
        button.style.paddingLeft = 3;
        button.style.paddingRight = 3;
        button.style.paddingTop = 3;
        button.style.paddingBottom = 3;
        button.SetEnabled(_flow.CanSendRoomChat);
        button.Add(PlayerProfileStore.CreateStickerVisual(sticker.AssetPath, _compact ? 28 : 32));
        _stickerTray.Add(button);
    }
}
```

- [ ] **Step 2: 验证编译**

等待编译完成，检查 Console。

预期：无编译错误

---

## Task 8: 调整消息滚动区域高度以适应新布局

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs:36-44`

- [ ] **Step 1: 调整消息滚动区域的样式**

找到 `_messageScroll` 的初始化代码（约第 36-44 行），修改高度限制：

```csharp
_messageScroll = new ScrollView(ScrollViewMode.Vertical);
_messageScroll.style.flexGrow = 1;
_messageScroll.style.flexShrink = 1;
_messageScroll.style.minHeight = compact ? 150 : 200;
_messageScroll.style.maxHeight = compact ? 420 : 450;
_messageScroll.style.marginBottom = 6;
_messageScroll.style.overflow = Overflow.Hidden;
_messageScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
_messageScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
Root.Add(_messageScroll);
```

- [ ] **Step 2: 验证编译**

等待编译完成，检查 Console。

预期：无编译错误

---

## Task 9: 在Unity中验证和测试新布局

**Files:**
- Test: Unity Editor Play Mode

- [ ] **Step 1: 启动 Unity 并进入 Play 模式**

1. 打开 Unity 编辑器
2. 确保没有编译错误
3. 点击 Play 按钮进入游戏

- [ ] **Step 2: 测试聊天面板基本功能**

验证以下功能：
1. 消息区域可以正常滚动查看历史消息
2. 底部输入栏布局清晰：文本框 + 😊按钮 + 发送按钮
3. 点击 😊 按钮，表情包弹出面板从底部展开
4. 弹出面板中可以看到所有表情包
5. 点击表情包可以发送，发送后面板自动关闭
6. 再次点击按钮（显示为 ✖）可以关闭面板

- [ ] **Step 3: 测试消息增量更新**

验证以下场景：
1. 发送多条消息，确认不会跳回顶部
2. 手动滚动到历史消息，新消息到达时滚动位置保持（如果在查看历史）
3. 滚动到底部时，发送新消息后自动保持在底部

- [ ] **Step 4: 测试 compact 模式（游戏桌面）**

1. 创建或加入房间，进入游戏场景
2. 切换到"房间交流"标签
3. 验证在窄面板（286px）中布局是否正常
4. 验证表情包弹出面板是否适配窄宽度

- [ ] **Step 5: 截图验证和反馈**

如果有问题，记录具体现象并调整。常见问题及解决方案：
- **表情包面板位置不对**：调整 `bottom` 值（约 38-42px）
- **按钮大小不合适**：调整 `width`/`height` 值
- **滚动条不显示**：检查 `maxHeight` 和 `minHeight` 设置
- **emoji 符号显示为方块**：可改用文本 "🎭" 或 "[😊]"

---

## 验收标准

完成后，聊天面板应满足以下要求：

1. ✅ 消息区域固定高度，支持鼠标滚轮滑动查看历史
2. ✅ 新消息到达时，如果用户在查看历史，不会强制跳到底部
3. ✅ 底部输入栏布局清晰：文本框 + emoji图标 + 发送按钮
4. ✅ 点击emoji图标，表情包面板从底部弹出
5. ✅ 表情包面板可以滚动查看所有表情
6. ✅ 点击表情包发送后，面板自动关闭
7. ✅ 再次点击emoji图标（显示为 ✖）可以关闭面板
8. ✅ 消息渲染使用增量更新，不会全量重建
9. ✅ compact 和非 compact 模式都正常工作
10. ✅ 无编译错误或运行时异常

---

## 提交说明

**测试通过后，一次性提交所有改动：**

```bash
cd "unity dev/New Client_Unity_Base_Cli"
git add Assets/Scripts/UI/RoomChatPanel.cs
git commit -m "feat(chat): 优化聊天面板UI布局和交互体验

改进内容：
- 将始终显示的表情包托盘改为弹出式面板，节省垂直空间
- 添加emoji按钮切换表情包面板显示/隐藏
- 优化消息渲染为增量更新模式，提升性能并保持滚动位置
- 调整底部输入栏布局：文本框 + emoji按钮 + 发送按钮
- 调整消息滚动区域高度以适应新布局
- 支持鼠标滚轮查看历史消息，用户体验更流畅"
```

---

## 后续优化建议

完成基本功能后，可以考虑以下改进（非必需）：

1. **表情包分类**：如果表情包数量很多，添加分类标签
2. **最近使用表情**：缓存最近使用的表情包，优先显示
3. **点击外部关闭面板**：点击表情包面板外的区域自动关闭
4. **表情包预览**：鼠标悬停显示表情包名称
5. **消息虚拟滚动**：如果消息数量超过 100 条，使用虚拟滚动优化性能
