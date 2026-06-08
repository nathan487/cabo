using System;
using Game.Room;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    public class RoomChatPanel
    {
        readonly GameFlow _flow;
        readonly bool _compact;
        readonly VisualElement _messages;
        readonly VisualElement _stickerTray;
        readonly TextField _input;
        readonly Button _sendButton;
        readonly Label _status;

        public VisualElement Root { get; }

        public RoomChatPanel(GameFlow flow, bool compact = false)
        {
            _flow = flow;
            _compact = compact;

            Root = new VisualElement { name = compact ? "TableRoomChatPanel" : "RoomChatPanel" };
            Root.style.flexGrow = 1;
            Root.style.minHeight = compact ? 230 : 280;

            _messages = new VisualElement();
            _messages.style.flexGrow = 1;
            _messages.style.minHeight = compact ? 132 : 170;
            _messages.style.maxHeight = compact ? 240 : 310;
            _messages.style.paddingLeft = 2;
            _messages.style.paddingRight = 2;
            Root.Add(_messages);

            _stickerTray = new VisualElement();
            _stickerTray.style.flexDirection = FlexDirection.Row;
            _stickerTray.style.flexWrap = Wrap.Wrap;
            _stickerTray.style.marginTop = 8;
            Root.Add(_stickerTray);

            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.FlexEnd;
            inputRow.style.marginTop = 8;
            Root.Add(inputRow);

            _input = new TextField();
            _input.style.flexGrow = 1;
            _input.style.marginRight = 6;
            _input.maxLength = 120;
            _input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                    return;
                SendText();
                evt.StopPropagation();
            });
            inputRow.Add(_input);

            _sendButton = new Button(SendText) { text = "发送" };
            _sendButton.style.minWidth = 58;
            _sendButton.style.height = 32;
            _sendButton.style.paddingLeft = 8;
            _sendButton.style.paddingRight = 8;
            inputRow.Add(_sendButton);
            _input.RegisterValueChangedCallback(_ =>
                _sendButton.SetEnabled(_flow.CanSendRoomChat && !string.IsNullOrWhiteSpace(_input.value)));

            _status = new Label();
            _status.style.fontSize = 11;
            _status.style.marginTop = 5;
            _status.style.color = new Color(0.86f, 0.72f, 0.42f);
            _status.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_status);
        }

        public void Render()
        {
            _messages.Clear();
            _stickerTray.Clear();

            var messages = _flow.State.RoomChatMessages;
            if (messages.Count == 0)
            {
                AddPlaceholder("暂无房间交流");
            }
            else
            {
                int visibleCount = _compact ? 8 : 12;
                int start = Mathf.Max(0, messages.Count - visibleCount);
                for (int i = start; i < messages.Count; i++)
                    _messages.Add(CreateMessageRow(messages[i]));
            }

            RenderStickerTray();
            bool canSend = _flow.CanSendRoomChat;
            _input.SetEnabled(canSend);
            _sendButton.SetEnabled(canSend && !string.IsNullOrWhiteSpace(_input.value));
            _status.text = string.IsNullOrWhiteSpace(_flow.State.LastRoomChatError)
                ? (canSend ? "" : "进入房间后可发送消息")
                : _flow.State.LastRoomChatError;
            _status.style.display = string.IsNullOrWhiteSpace(_status.text) ? DisplayStyle.None : DisplayStyle.Flex;
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

        void SendSticker(ArtAsset sticker)
        {
            _flow.SendRoomChatSticker(sticker.PackName, sticker.DisplayName);
            Render();
        }

        VisualElement CreateMessageRow(RoomChatMessage message)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 7;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = message.SenderPlayerId == _flow.State.MyPlayerId
                ? new Color(0.055f, 0.19f, 0.145f)
                : new Color(0.035f, 0.16f, 0.135f);
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;

            var senderName = string.IsNullOrWhiteSpace(message.SenderNickname) ? "玩家" : message.SenderNickname;
            var avatar = PlayerProfileStore.CreateAvatarVisual(
                senderName,
                PlayerProfileStore.GetAvatarPathForPlayer(message.SenderPlayerId, message.SenderPlayerId == _flow.State.MyPlayerId),
                _compact ? 26 : 30);
            avatar.style.marginRight = 7;
            row.Add(avatar);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            row.Add(body);

            var name = new Label(senderName + (message.SenderPlayerId == _flow.State.MyPlayerId ? "（你）" : ""));
            name.style.fontSize = 11;
            name.style.color = new Color(0.84f, 0.91f, 0.86f);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            body.Add(name);

            if (message.Type == RoomChatType.Sticker)
            {
                var stickerPath = PlayerProfileStore.GetStickerAssetPath(message.StickerPack, message.StickerName);
                if (!string.IsNullOrEmpty(stickerPath))
                {
                    var sticker = PlayerProfileStore.CreateStickerVisual(stickerPath, _compact ? 46 : 58);
                    sticker.style.marginTop = 2;
                    body.Add(sticker);
                }
                else
                {
                    AddMessageText(body, $"表情：{message.StickerPack}/{message.StickerName}");
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
            var stickers = PlayerProfileStore.GetStickerAssets();
            if (stickers.Count == 0)
            {
                var none = new Label("暂无表情资源");
                none.style.fontSize = 11;
                none.style.color = new Color(0.62f, 0.70f, 0.67f);
                none.style.unityTextAlign = TextAnchor.MiddleCenter;
                _stickerTray.Add(none);
                return;
            }

            int count = Mathf.Min(stickers.Count, _compact ? 12 : 18);
            for (int i = 0; i < count; i++)
            {
                var sticker = stickers[i];
                var button = new Button(() => SendSticker(sticker));
                button.text = "";
                button.style.width = _compact ? 42 : 46;
                button.style.height = _compact ? 42 : 46;
                button.style.minWidth = _compact ? 42 : 46;
                button.style.marginLeft = 2;
                button.style.marginRight = 2;
                button.style.marginTop = 2;
                button.style.marginBottom = 2;
                button.style.paddingLeft = 3;
                button.style.paddingRight = 3;
                button.style.paddingTop = 3;
                button.style.paddingBottom = 3;
                button.SetEnabled(_flow.CanSendRoomChat);
                button.Add(PlayerProfileStore.CreateStickerVisual(sticker.AssetPath, _compact ? 30 : 34));
                _stickerTray.Add(button);
            }
        }
    }
}
