using System.Collections.Generic;
using Game.Room;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    public class RoomChatPanel
    {
        readonly GameFlow _flow;
        readonly bool _compact;
        readonly bool _fillHeight;
        readonly ScrollView _messageScroll;
        readonly ScrollView _stickerScroll;
        readonly VisualElement _messages;
        readonly VisualElement _stickerTray;
        readonly TextField _input;
        readonly VisualElement _inputRow;
        readonly Button _sendButton;
        readonly Button _emojiButton;
        readonly VisualElement _stickerPopup;
        bool _stickerPopupVisible;
        readonly Dictionary<long, VisualElement> _messageCache = new();
        readonly Label _status;

        long _lastRenderedLastMessageId;
        int _lastRenderedMessageCount;

        public VisualElement Root { get; }

        public RoomChatPanel(GameFlow flow, bool compact = false, bool fillHeight = false)
        {
            _flow = flow;
            _compact = compact;
            _fillHeight = fillHeight;

            Root = new VisualElement { name = compact ? "TableRoomChatPanel" : "RoomChatPanel" };
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.flexGrow = 1;
            Root.style.flexShrink = 1;
            Root.style.minHeight = compact || fillHeight ? 0 : 318;
            Root.style.overflow = Overflow.Hidden;
            Root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_stickerPopupVisible)
                    PositionStickerPopup();
            });
            if (!fillHeight && !compact)
            {
                Root.style.height = 318;
                Root.style.maxHeight = 318;
            }

            _messageScroll = new ScrollView(ScrollViewMode.Vertical) { name = "RoomChatMessageScroll" };
            _messageScroll.style.flexGrow = 1;
            _messageScroll.style.flexShrink = 1;
            if (fillHeight)
            {
                _messageScroll.style.minHeight = 0;
            }
            else
            {
                _messageScroll.style.height = compact ? 150 : 236;
                _messageScroll.style.minHeight = compact ? 150 : 236;
                _messageScroll.style.maxHeight = compact ? 150 : 236;
            }
            _messageScroll.style.marginBottom = 6;
            _messageScroll.style.overflow = Overflow.Hidden;
            _messageScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _messageScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Root.Add(_messageScroll);

            _messages = _messageScroll.contentContainer;
            _messages.style.paddingLeft = 2;
            _messages.style.paddingRight = 2;

            _stickerPopup = new VisualElement { name = "StickerPopup" };
            _stickerPopup.style.position = Position.Absolute;
            _stickerPopup.style.bottom = _compact ? 48 : 56;
            _stickerPopup.style.left = 4;
            _stickerPopup.style.right = 4;
            _stickerPopup.style.maxHeight = _compact ? 124 : 172;
            _stickerPopup.style.backgroundColor = new Color(0.02f, 0.11f, 0.095f);
            SetRadius(_stickerPopup, 6);
            SetBorderWidth(_stickerPopup, 1);
            SetBorderColor(_stickerPopup, new Color(0.14f, 0.38f, 0.32f));
            _stickerPopup.style.paddingLeft = 8;
            _stickerPopup.style.paddingRight = 8;
            _stickerPopup.style.paddingTop = 8;
            _stickerPopup.style.paddingBottom = 8;
            _stickerPopup.style.display = DisplayStyle.None;
            Root.Add(_stickerPopup);

            _stickerScroll = new ScrollView(ScrollViewMode.Vertical) { name = "RoomChatStickerScroll" };
            _stickerScroll.style.flexGrow = 1;
            _stickerScroll.style.flexShrink = 1;
            _stickerScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _stickerScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _stickerPopup.Add(_stickerScroll);

            _stickerTray = _stickerScroll.contentContainer;
            _stickerTray.style.flexDirection = FlexDirection.Row;
            _stickerTray.style.flexWrap = Wrap.Wrap;
            _stickerTray.style.paddingRight = 4;
            _stickerTray.style.justifyContent = Justify.FlexStart;

            _inputRow = new VisualElement { name = "RoomChatInputRow" };
            _inputRow.style.flexDirection = FlexDirection.Row;
            _inputRow.style.alignItems = Align.Center;
            _inputRow.style.flexShrink = 0;
            _inputRow.style.minHeight = _compact ? 32 : 42;
            _inputRow.style.marginBottom = _fillHeight ? 0 : 4;
            _inputRow.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_stickerPopupVisible)
                    PositionStickerPopup();
            });
            Root.Add(_inputRow);

            _input = new TextField { name = "RoomChatTextInput" };
            _input.style.flexGrow = 1;
            _input.style.flexShrink = 1;
            _input.style.minWidth = 0;
            if (!_fillHeight)
                _input.style.maxWidth = _compact ? 198 : 438;
            _input.style.height = _compact ? 28 : 36;
            _input.style.minHeight = _compact ? 28 : 36;
            _input.style.fontSize = _compact ? 12 : 14;
            _input.style.marginRight = 4;
            _input.maxLength = 120;
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;
                SendText();
                evt.StopPropagation();
            });
            _inputRow.Add(_input);

            _emojiButton = new Button(ToggleStickerPopup) { name = "RoomChatEmojiButton", text = _compact ? "E" : "Emoji" };
            _emojiButton.style.minWidth = _compact ? 32 : 68;
            _emojiButton.style.width = _compact ? 32 : 68;
            _emojiButton.style.maxWidth = _compact ? 32 : 68;
            _emojiButton.style.height = _compact ? 28 : 36;
            _emojiButton.style.flexShrink = 0;
            _emojiButton.style.marginRight = 4;
            _emojiButton.style.paddingLeft = 2;
            _emojiButton.style.paddingRight = 2;
            _emojiButton.style.fontSize = _compact ? 14 : 13;
            _inputRow.Add(_emojiButton);

            _sendButton = new Button(SendText) { name = "RoomChatSendButton", text = _compact ? ">" : "Send" };
            _sendButton.style.minWidth = _compact ? 48 : 72;
            _sendButton.style.width = _compact ? 48 : 72;
            _sendButton.style.maxWidth = _compact ? 48 : 72;
            _sendButton.style.flexShrink = 0;
            _sendButton.style.height = _compact ? 28 : 36;
            _sendButton.style.paddingLeft = 8;
            _sendButton.style.paddingRight = 8;
            _sendButton.style.fontSize = _compact ? 12 : 13;
            _inputRow.Add(_sendButton);

            _input.RegisterValueChangedCallback(_ =>
                _sendButton.SetEnabled(_flow.CanSendRoomChat && !string.IsNullOrWhiteSpace(_input.value)));

            _status = new Label { name = "RoomChatStatus" };
            _status.style.fontSize = 11;
            _status.style.marginTop = 5;
            _status.style.flexShrink = 0;
            _status.style.color = new Color(0.86f, 0.72f, 0.42f);
            _status.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_status);
        }

        public void Render()
        {
            var messages = _flow.State.RoomChatMessages;
            var shouldScrollToBottom = false;

            if (messages.Count == 0)
            {
                _messages.Clear();
                _messageCache.Clear();
                AddPlaceholder("No room messages yet");
                _lastRenderedLastMessageId = 0;
                _lastRenderedMessageCount = 0;
            }
            else
            {
                var lastMessageId = messages[messages.Count - 1].MessageId;
                shouldScrollToBottom = lastMessageId != _lastRenderedLastMessageId
                    || messages.Count != _lastRenderedMessageCount;

                var shouldRebuild = _lastRenderedMessageCount == 0
                    || messages.Count < _lastRenderedMessageCount
                    || (messages.Count == _lastRenderedMessageCount
                        && lastMessageId != _lastRenderedLastMessageId
                        && !_messageCache.ContainsKey(lastMessageId));

                if (shouldRebuild)
                {
                    _messages.Clear();
                    _messageCache.Clear();
                    for (var i = 0; i < messages.Count; i++)
                        AddMessage(messages[i]);
                }
                else if (messages.Count > _lastRenderedMessageCount)
                {
                    for (var i = _lastRenderedMessageCount; i < messages.Count; i++)
                        AddMessage(messages[i]);
                }

                _lastRenderedLastMessageId = lastMessageId;
                _lastRenderedMessageCount = messages.Count;
            }

            if (_stickerTray.childCount == 0)
                RenderStickerTray();

            var canSend = _flow.CanSendRoomChat;
            _input.SetEnabled(canSend);
            _sendButton.SetEnabled(canSend && !string.IsNullOrWhiteSpace(_input.value));
            _emojiButton.SetEnabled(canSend);
            _status.text = string.IsNullOrWhiteSpace(_flow.State.LastRoomChatError)
                ? ""
                : _flow.State.LastRoomChatError;
            _status.style.display = string.IsNullOrWhiteSpace(_status.text) ? DisplayStyle.None : DisplayStyle.Flex;

            if (shouldScrollToBottom)
                ScrollMessagesToBottom();
        }

        void AddMessage(RoomChatMessage message)
        {
            var messageRow = CreateMessageRow(message);
            _messageCache[message.MessageId] = messageRow;
            _messages.Add(messageRow);
        }

        void SendText()
        {
            var text = _input.value?.Trim();
            if (string.IsNullOrEmpty(text))
                return;
            if (text.Length > 120)
                text = text.Substring(0, 120);

            _flow.SendRoomChatText(text);
            _input.value = "";
            Render();
        }

        void ToggleStickerPopup()
        {
            _stickerPopupVisible = !_stickerPopupVisible;
            _stickerPopup.style.display = _stickerPopupVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_stickerPopupVisible)
            {
                PositionStickerPopup();
                _emojiButton.style.backgroundColor = new Color(0.12f, 0.32f, 0.27f);
                _emojiButton.text = _compact ? "X" : "Close";
                Root.schedule.Execute(PositionStickerPopup).ExecuteLater(0);
                Root.schedule.Execute(PositionStickerPopup).ExecuteLater(80);
            }
            else
            {
                _emojiButton.style.backgroundColor = StyleKeyword.Null;
                _emojiButton.text = _compact ? "E" : "Emoji";
            }
        }

        void SendSticker(ArtAsset sticker)
        {
            _flow.SendRoomChatSticker(sticker.PackName, sticker.DisplayName);
            _stickerPopupVisible = false;
            _stickerPopup.style.display = DisplayStyle.None;
            _emojiButton.style.backgroundColor = StyleKeyword.Null;
            _emojiButton.text = _compact ? "E" : "Emoji";
            Render();
        }

        void PositionStickerPopup()
        {
            if (Root.panel == null || _inputRow.panel == null)
                return;

            var rootBounds = Root.worldBound;
            var inputBounds = _inputRow.worldBound;
            if (rootBounds.height <= 0f || inputBounds.height <= 0f)
                return;

            const float Gap = 8f;
            const float SideInset = 4f;
            var bottomAboveInput = Mathf.Max(0f, rootBounds.yMax - inputBounds.yMin + Gap);
            var availableHeight = Mathf.Max(64f, inputBounds.yMin - rootBounds.yMin - Gap);
            var popupWidth = Mathf.Max(1f, rootBounds.width - SideInset * 2f);
            var preferredHeight = CalculateStickerPopupHeight(popupWidth);
            var popupHeight = Mathf.Min(preferredHeight, availableHeight);
            _stickerScroll.verticalScrollerVisibility = NeedsStickerPopupScroll(popupWidth)
                ? ScrollerVisibility.Auto
                : ScrollerVisibility.Hidden;

            _stickerPopup.style.left = SideInset;
            _stickerPopup.style.right = SideInset;
            _stickerPopup.style.bottom = bottomAboveInput;
            _stickerPopup.style.height = popupHeight;
            _stickerPopup.style.maxHeight = popupHeight;
        }

        float CalculateStickerPopupHeight(float popupWidth)
        {
            var stickerCount = PlayerProfileStore.GetStickerAssets().Count;
            if (stickerCount <= 0)
                return 56f;

            var buttonOuterSize = GetStickerButtonSize() + 6f;
            var horizontalPadding = 20f;
            var verticalPadding = 16f;
            var usableWidth = Mathf.Max(buttonOuterSize, popupWidth - horizontalPadding);
            var itemsPerRow = Mathf.Max(1, Mathf.FloorToInt(usableWidth / buttonOuterSize));
            var rowCount = Mathf.CeilToInt(stickerCount / (float)itemsPerRow);
            var visibleRows = Mathf.Clamp(rowCount, 1, 2);
            return verticalPadding + visibleRows * buttonOuterSize;
        }

        bool NeedsStickerPopupScroll(float popupWidth)
        {
            var stickerCount = PlayerProfileStore.GetStickerAssets().Count;
            if (stickerCount <= 0)
                return false;

            var buttonOuterSize = GetStickerButtonSize() + 6f;
            var usableWidth = Mathf.Max(buttonOuterSize, popupWidth - 20f);
            var itemsPerRow = Mathf.Max(1, Mathf.FloorToInt(usableWidth / buttonOuterSize));
            var rowCount = Mathf.CeilToInt(stickerCount / (float)itemsPerRow);
            return rowCount > 2;
        }

        float GetStickerButtonSize()
        {
            return _fillHeight ? 50f : _compact ? 46f : 72f;
        }

        int GetStickerImageSize()
        {
            return _fillHeight ? 40 : _compact ? 36 : 58;
        }

        VisualElement CreateMessageRow(RoomChatMessage message)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.flexShrink = 0;
            row.style.marginBottom = 7;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = message.SenderPlayerId == _flow.State.MyPlayerId
                ? new Color(0.055f, 0.19f, 0.145f)
                : new Color(0.035f, 0.16f, 0.135f);
            SetRadius(row, 6);
            row.style.overflow = Overflow.Hidden;

            var senderName = string.IsNullOrWhiteSpace(message.SenderNickname) ? "Player" : message.SenderNickname;
            var avatar = PlayerProfileStore.CreateAvatarVisual(
                senderName,
                PlayerProfileStore.GetAvatarPathForPlayer(message.SenderPlayerId, message.SenderPlayerId == _flow.State.MyPlayerId),
                _compact ? 26 : 30);
            avatar.style.marginRight = 7;
            row.Add(avatar);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.minWidth = 0;
            row.Add(body);

            var name = new Label(senderName + (message.SenderPlayerId == _flow.State.MyPlayerId ? " (You)" : ""));
            name.style.fontSize = 11;
            name.style.color = new Color(0.84f, 0.91f, 0.86f);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(name);

            if (message.Type == RoomChatType.Sticker)
            {
                var stickerPath = PlayerProfileStore.GetStickerAssetPath(message.StickerPack, message.StickerName);
                if (!string.IsNullOrEmpty(stickerPath))
                {
                    var stickerSize = _compact ? 54 : 72;
                    row.style.minHeight = stickerSize + 28;
                    var sticker = PlayerProfileStore.CreateStickerVisual(stickerPath, stickerSize);
                    sticker.style.marginTop = 2;
                    sticker.style.marginBottom = 2;
                    body.Add(sticker);
                }
                else
                {
                    AddMessageText(body, $"Sticker: {message.StickerPack}/{message.StickerName}");
                }
            }
            else
            {
                AddMessageText(body, message.Text);
            }

            return row;
        }

        void AddMessageText(VisualElement parent, string text)
        {
            var label = new Label(text ?? "");
            label.style.fontSize = 12;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(0.88f, 0.90f, 0.86f);
            label.style.flexShrink = 1;
            label.style.minWidth = 0;
            parent.Add(label);
        }

        void AddPlaceholder(string text)
        {
            var empty = new Label(text);
            empty.style.fontSize = 12;
            empty.style.color = new Color(0.62f, 0.70f, 0.67f);
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.marginTop = 18;
            _messages.Add(empty);
        }

        void RenderStickerTray()
        {
            _stickerTray.Clear();

            var stickers = PlayerProfileStore.GetStickerAssets();
            if (stickers.Count == 0)
            {
                var none = new Label("No sticker assets");
                none.style.fontSize = 11;
                none.style.color = new Color(0.62f, 0.70f, 0.67f);
                none.style.unityTextAlign = TextAnchor.MiddleCenter;
                none.style.marginTop = 10;
                _stickerTray.Add(none);
                return;
            }

            for (var i = 0; i < stickers.Count; i++)
            {
                var sticker = stickers[i];
                var buttonSize = GetStickerButtonSize();
                var button = new Button(() => SendSticker(sticker));
                button.text = "";
                button.style.width = buttonSize;
                button.style.height = buttonSize;
                button.style.minWidth = buttonSize;
                button.style.flexShrink = 0;
                button.style.marginLeft = 3;
                button.style.marginRight = 3;
                button.style.marginTop = 3;
                button.style.marginBottom = 3;
                button.style.paddingLeft = 4;
                button.style.paddingRight = 4;
                button.style.paddingTop = 4;
                button.style.paddingBottom = 4;
                button.SetEnabled(_flow.CanSendRoomChat);
                button.Add(PlayerProfileStore.CreateStickerVisual(sticker.AssetPath, GetStickerImageSize()));
                _stickerTray.Add(button);
            }

            if (_stickerPopupVisible)
                PositionStickerPopup();
        }

        void ScrollMessagesToBottom()
        {
            if (_messages.childCount == 0)
                return;

            var last = _messages[_messages.childCount - 1];
            ScheduleScrollToBottom(last, 0);
            ScheduleScrollToBottom(last, 80);
        }

        void ScheduleScrollToBottom(VisualElement target, int delayMs)
        {
            _messageScroll.schedule.Execute(() =>
            {
                if (target == null || target.panel == null || _messageScroll.panel == null)
                    return;

                _messageScroll.ScrollTo(target);
                var maxY = Mathf.Max(0f, _messageScroll.contentContainer.resolvedStyle.height - _messageScroll.resolvedStyle.height);
                if (maxY > _messageScroll.scrollOffset.y)
                    _messageScroll.scrollOffset = new Vector2(_messageScroll.scrollOffset.x, maxY);
            }).ExecuteLater(delayMs);
        }

        static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        static void SetBorderWidth(VisualElement element, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
        }

        static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }
    }
}
