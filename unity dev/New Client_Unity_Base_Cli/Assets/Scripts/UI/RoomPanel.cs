using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Home and waiting-room UI. The home page now owns server connection input.
    /// </summary>
    public class RoomPanel
    {
        VisualElement _root, _container;
        VisualElement _serverRow, _homeButtonRow, _joinFormRow, _roomButtonRow;
        VisualElement _avatarSection, _avatarPreview, _avatarChoices, _playerListView, _roomContent;
        ScrollView _playerListScroll;
        RoomChatPanel _chatPanel;
        Label _title, _roomCode, _playerList, _avatarStatus, _status;
        TextField _serverAddressInput, _nicknameInput, _joinCodeInput;
        Button _btnConnect, _btnCreate, _btnShowJoin, _btnConfirmJoin, _btnExitGame;
        Button _btnReady, _btnStart, _btnLeaveRoom, _btnCopyRoomCode;
        GameFlow _flow;
        bool _joinFormVisible;

        public RoomPanel(VisualElement root, GameFlow flow)
        {
            _flow = flow;
            _root = root;

            _container = new VisualElement();
            _container.style.flexGrow = 1;
            _container.style.paddingTop = 20;
            _container.style.paddingBottom = 20;
            _container.style.paddingLeft = 40;
            _container.style.paddingRight = 40;
            root.Add(_container);

            _title = new Label("CABO");
            _title.style.fontSize = 28;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.color = UITheme.TextPrimary;
            _container.Add(_title);

            _roomCode = new Label();
            _roomCode.style.fontSize = 18;
            _roomCode.style.marginTop = 10;
            _roomCode.style.unityTextAlign = TextAnchor.MiddleCenter;
            _roomCode.style.color = UITheme.TextSecondary;
            _container.Add(_roomCode);

            _btnCopyRoomCode = new Button(() =>
            {
                var roomCode = _flow.State.RoomCode;
                if (string.IsNullOrEmpty(roomCode)) return;
                GUIUtility.systemCopyBuffer = roomCode;
                _status.text = $"已复制房间码：{roomCode}";
            });
            _btnCopyRoomCode.text = "复制房间码";
            _btnCopyRoomCode.style.alignSelf = Align.Center;
            _btnCopyRoomCode.style.marginTop = 8;
            _btnCopyRoomCode.style.fontSize = 14;
            _container.Add(_btnCopyRoomCode);

            _serverRow = new VisualElement();
            _serverRow.style.flexDirection = FlexDirection.Row;
            _serverRow.style.justifyContent = Justify.Center;
            _serverRow.style.alignItems = Align.FlexEnd;
            _serverRow.style.marginTop = 18;
            _container.Add(_serverRow);

            _serverAddressInput = new TextField("服务器地址");
            _serverAddressInput.value = _flow.GetCachedServerAddress();
            _serverAddressInput.style.width = 320;
            _serverAddressInput.style.maxWidth = 320;
            _serverAddressInput.style.marginRight = 10;
            _serverAddressInput.maxLength = 128;
            _serverRow.Add(_serverAddressInput);

            _btnConnect = new Button(() =>
            {
                _status.text = "正在连接服务器...";
                _flow.ConnectToServerAddress(_serverAddressInput.value);
            });
            _btnConnect.text = "连接";
            _btnConnect.style.fontSize = 16;
            _serverRow.Add(_btnConnect);

            _playerList = new Label();
            _playerList.style.fontSize = 16;
            _playerList.style.marginTop = 20;
            _playerList.style.whiteSpace = WhiteSpace.Normal;
            _container.Add(_playerList);

            _roomContent = new VisualElement { name = "WaitingRoomContent" };
            _roomContent.style.flexDirection = FlexDirection.Row;
            _roomContent.style.justifyContent = Justify.Center;
            _roomContent.style.alignItems = Align.Stretch;
            _roomContent.style.alignSelf = Align.Center;
            _roomContent.style.width = 980;
            _roomContent.style.maxWidth = Length.Percent(100);
            _roomContent.style.height = 392;
            _roomContent.style.minHeight = 392;
            _roomContent.style.maxHeight = 392;
            _roomContent.style.marginTop = 12;
            _roomContent.style.overflow = Overflow.Hidden;
            _container.Add(_roomContent);

            _playerListScroll = new ScrollView(ScrollViewMode.Vertical);
            _playerListScroll.style.flexShrink = 0;
            _playerListScroll.style.width = 360;
            _playerListScroll.style.maxWidth = Length.Percent(100);
            _playerListScroll.style.height = 360;
            _playerListScroll.style.minHeight = 360;
            _playerListScroll.style.maxHeight = 360;
            _playerListScroll.style.marginRight = 16;
            _playerListScroll.style.overflow = Overflow.Hidden;
            _playerListScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _playerListScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _roomContent.Add(_playerListScroll);

            _playerListView = _playerListScroll.contentContainer;

            _status = new Label();
            _status.style.fontSize = 14;
            _status.style.marginTop = 10;
            _status.style.unityTextAlign = TextAnchor.MiddleCenter;
            _status.style.color = UITheme.TextMuted;
            _container.Add(_status);

            _nicknameInput = new TextField("昵称");
            _nicknameInput.style.marginTop = 16;
            _nicknameInput.style.width = 260;
            _nicknameInput.style.maxWidth = 260;
            _nicknameInput.style.alignSelf = Align.Center;
            _nicknameInput.maxLength = 20;
            _container.Add(_nicknameInput);

            _avatarSection = new VisualElement();
            _avatarSection.style.alignSelf = Align.Center;
            _avatarSection.style.width = 520;
            _avatarSection.style.maxWidth = Length.Percent(100);
            _avatarSection.style.marginTop = 14;
            _avatarSection.style.paddingLeft = 12;
            _avatarSection.style.paddingRight = 12;
            _avatarSection.style.paddingTop = 10;
            _avatarSection.style.paddingBottom = 10;
            UITheme.ApplyPanel(_avatarSection, UITheme.PanelSurface, UITheme.PanelBorder);
            _container.Add(_avatarSection);

            var avatarTitle = new Label("头像");
            avatarTitle.style.fontSize = 14;
            avatarTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            avatarTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _avatarSection.Add(avatarTitle);

            _avatarPreview = new VisualElement();
            _avatarPreview.style.alignSelf = Align.Center;
            _avatarPreview.style.marginTop = 6;
            _avatarSection.Add(_avatarPreview);

            _avatarChoices = new VisualElement();
            _avatarChoices.style.flexDirection = FlexDirection.Row;
            _avatarChoices.style.flexWrap = Wrap.Wrap;
            _avatarChoices.style.justifyContent = Justify.Center;
            _avatarChoices.style.marginTop = 8;
            _avatarSection.Add(_avatarChoices);

            _avatarStatus = new Label();
            _avatarStatus.style.fontSize = 11;
            _avatarStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
            _avatarStatus.style.marginTop = 5;
            _avatarStatus.style.color = UITheme.TextSecondary;
            _avatarSection.Add(_avatarStatus);

            _homeButtonRow = new VisualElement();
            _homeButtonRow.style.flexDirection = FlexDirection.Row;
            _homeButtonRow.style.justifyContent = Justify.Center;
            _homeButtonRow.style.marginTop = 20;
            _container.Add(_homeButtonRow);

            _btnCreate = new Button(() =>
            {
                var nickname = GetNicknameOrShowError();
                if (nickname == null) return;
                _flow.CreateRoom(nickname);
            });
            _btnCreate.text = "创建房间";
            _btnCreate.style.marginRight = 10;
            _btnCreate.style.fontSize = 18;
            _homeButtonRow.Add(_btnCreate);

            _btnShowJoin = new Button(() =>
            {
                _joinFormVisible = true;
                Render();
            });
            _btnShowJoin.text = "加入房间";
            _btnShowJoin.style.marginRight = 10;
            _btnShowJoin.style.fontSize = 18;
            _homeButtonRow.Add(_btnShowJoin);

            _btnExitGame = new Button(() => _flow.ExitGame());
            _btnExitGame.text = "退出游戏";
            _btnExitGame.style.fontSize = 18;
            _homeButtonRow.Add(_btnExitGame);

            _joinFormRow = new VisualElement();
            _joinFormRow.style.flexDirection = FlexDirection.Row;
            _joinFormRow.style.justifyContent = Justify.Center;
            _joinFormRow.style.alignItems = Align.FlexEnd;
            _joinFormRow.style.marginTop = 16;
            _container.Add(_joinFormRow);

            var joinCodeLabel = new Label("房间码");
            joinCodeLabel.style.fontSize = 14;
            joinCodeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            joinCodeLabel.style.marginRight = 8;
            joinCodeLabel.style.marginBottom = 7;
            _joinFormRow.Add(joinCodeLabel);

            _joinCodeInput = new TextField();
            _joinCodeInput.style.width = 260;
            _joinCodeInput.style.maxWidth = 260;
            _joinCodeInput.style.minWidth = 260;
            _joinCodeInput.style.fontSize = 16;
            _joinCodeInput.style.marginRight = 10;
            _joinCodeInput.maxLength = 16;
            _joinFormRow.Add(_joinCodeInput);

            _btnConfirmJoin = new Button(() =>
            {
                var nickname = GetNicknameOrShowError();
                if (nickname == null) return;

                var code = _joinCodeInput.value?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                {
                    _status.text = "请先输入房间码。";
                    return;
                }

                _flow.JoinRoom(code, nickname);
            });
            _btnConfirmJoin.text = "确认加入";
            _btnConfirmJoin.style.fontSize = 16;
            _joinFormRow.Add(_btnConfirmJoin);

            _roomButtonRow = new VisualElement();
            _roomButtonRow.style.flexDirection = FlexDirection.Row;
            _roomButtonRow.style.justifyContent = Justify.Center;
            _roomButtonRow.style.marginTop = 20;
            _container.Add(_roomButtonRow);

            _btnReady = new Button(() => { _flow.SendReady(); _status.text = "已发送准备。"; });
            _btnReady.text = "准备";
            _btnReady.style.marginRight = 10;
            _btnReady.style.fontSize = 18;
            _roomButtonRow.Add(_btnReady);

            _btnStart = new Button(() => { _flow.SendStartGame(); _status.text = "正在开始游戏..."; });
            _btnStart.text = "开始游戏";
            _btnStart.style.marginRight = 10;
            _btnStart.style.fontSize = 18;
            _roomButtonRow.Add(_btnStart);

            _btnLeaveRoom = new Button(() =>
            {
                _joinFormVisible = false;
                _flow.LeaveRoomToHome();
            });
            _btnLeaveRoom.text = "退出房间";
            _btnLeaveRoom.style.fontSize = 18;
            _roomButtonRow.Add(_btnLeaveRoom);

            _chatPanel = new RoomChatPanel(_flow);
            _chatPanel.Root.style.flexGrow = 1;
            _chatPanel.Root.style.flexShrink = 1;
            _chatPanel.Root.style.width = 600;
            _chatPanel.Root.style.maxWidth = Length.Percent(100);
            _chatPanel.Root.style.paddingLeft = 12;
            _chatPanel.Root.style.paddingRight = 12;
            _chatPanel.Root.style.paddingTop = 10;
            _chatPanel.Root.style.paddingBottom = 10;
            UITheme.ApplyPanel(_chatPanel.Root, UITheme.PanelSurface, UITheme.PanelBorder);
            _roomContent.Add(_chatPanel.Root);
        }

        public void SetVisible(bool visible)
        {
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        string GetNicknameOrShowError()
        {
            var nickname = _nicknameInput.value?.Trim();
            if (string.IsNullOrEmpty(nickname))
            {
                _status.text = "请先输入昵称。";
                return null;
            }
            if (nickname.Length > 20)
            {
                _status.text = "昵称长度需为 1-20 个字符。";
                return null;
            }
            return nickname;
        }

        public void Render()
        {
            var s = _flow.State;
            bool connected = _flow.IsConnected;
            bool connecting = _flow.Flow == FlowState.Connecting;
            bool inRoom = s.RoomId > 0 || s.Players.Count > 0;
            bool hasRoomCode = !string.IsNullOrEmpty(s.RoomCode);

            _title.text = inRoom ? "CABO - 等待房间" : "CABO";
            _roomCode.text = hasRoomCode ? $"房间码：{s.RoomCode}" : (inRoom ? "正在加入..." : "");
            _btnCopyRoomCode.style.display = hasRoomCode ? DisplayStyle.Flex : DisplayStyle.None;

            _serverRow.style.display = inRoom ? DisplayStyle.None : DisplayStyle.Flex;
            _nicknameInput.style.display = (!inRoom && connected) ? DisplayStyle.Flex : DisplayStyle.None;
            _avatarSection.style.display = (!inRoom && connected) ? DisplayStyle.Flex : DisplayStyle.None;
            _homeButtonRow.style.display = !inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            _joinFormRow.style.display = (!inRoom && connected && _joinFormVisible) ? DisplayStyle.Flex : DisplayStyle.None;
            _roomButtonRow.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            _chatPanel.Root.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            _playerListScroll.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            _roomContent.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            _playerList.style.display = DisplayStyle.None;

            _btnConnect.SetEnabled(!connecting);
            _btnConnect.text = connecting ? "连接中..." : (connected ? "重新连接" : "连接");
            _btnCreate.SetEnabled(connected);
            _btnShowJoin.SetEnabled(connected);
            _btnConfirmJoin.SetEnabled(connected);

            if (!inRoom)
            {
                _playerList.text = "";
                _playerListView.Clear();
                _chatPanel.Root.style.display = DisplayStyle.None;
                RenderAvatarSelector();
                if (connecting)
                    _status.text = "正在连接服务器...";
                else if (connected)
                    _status.text = string.IsNullOrEmpty(_flow.ConnectedAddress)
                        ? "服务器已连接。"
                        : $"服务器已连接：{_flow.ConnectedAddress}";
                else if (!string.IsNullOrEmpty(_flow.LastConnectError))
                    _status.text = $"未连接服务器：{_flow.LastConnectError}";
                else
                    _status.text = "未连接服务器。请先输入服务器地址并连接。";
                return;
            }

            string list = "";
            int readyCount = 0;
            _playerListView.Clear();
            foreach (var p in s.Players)
            {
                string tag = "";
                if (p.PlayerId == s.MyPlayerId) tag += "（你）";
                if (p.IsHost) tag += " [房主]";
                string ready = p.IsReady ? " 已准备" : " 未准备";
                list += $"{p.Nickname}{tag}: {ready}\n";
                _playerListView.Add(CreatePlayerListRow(p, p.PlayerId == s.MyPlayerId));
                if (p.IsReady) readyCount++;
            }
            _playerList.text = list;

            bool isHost = false;
            bool allReady = readyCount == s.Players.Count && s.Players.Count >= 2;
            foreach (var p in s.Players)
            {
                if (p.PlayerId == s.MyPlayerId && p.IsHost)
                {
                    isHost = true;
                    break;
                }
            }

            _btnStart.SetEnabled(isHost && allReady);
            _status.text = $"{readyCount}/{s.Players.Count} 已准备" + (allReady && isHost ? " - 可以开始" : "");
            _chatPanel.Render();
        }

        void RenderAvatarSelector()
        {
            _avatarPreview.Clear();
            _avatarChoices.Clear();

            var nickname = string.IsNullOrWhiteSpace(_nicknameInput.value) ? "你" : _nicknameInput.value.Trim();
            _avatarPreview.Add(PlayerProfileStore.CreateAvatarVisual(nickname, PlayerProfileStore.SelectedAvatarPath, 56));

            AddAvatarChoice("默认", "", string.IsNullOrEmpty(PlayerProfileStore.SelectedAvatarPath), nickname);

            var avatars = PlayerProfileStore.GetAvatarAssets();
            foreach (var avatar in avatars)
                AddAvatarChoice(avatar.DisplayName, avatar.AssetPath, avatar.AssetPath == PlayerProfileStore.SelectedAvatarPath, nickname);

            _avatarStatus.text = avatars.Count == 0 ? "暂无可选头像，使用默认头像" : $"已找到 {avatars.Count} 个头像";
        }

        void AddAvatarChoice(string label, string avatarPath, bool selected, string nickname)
        {
            var choice = new VisualElement();
            choice.style.width = 78;
            choice.style.height = 72;
            choice.style.alignItems = Align.Center;
            choice.style.justifyContent = Justify.Center;
            choice.style.marginLeft = 4;
            choice.style.marginRight = 4;
            choice.style.marginTop = 4;
            choice.style.marginBottom = 4;
            choice.style.paddingLeft = 4;
            choice.style.paddingRight = 4;
            choice.style.paddingTop = 4;
            choice.style.paddingBottom = 4;
            choice.style.backgroundColor = selected ? UITheme.SelectedSurface : UITheme.PanelSurfaceAlt;
            choice.style.borderTopLeftRadius = 7;
            choice.style.borderTopRightRadius = 7;
            choice.style.borderBottomLeftRadius = 7;
            choice.style.borderBottomRightRadius = 7;
            choice.style.borderTopWidth = selected ? 2 : 1;
            choice.style.borderRightWidth = selected ? 2 : 1;
            choice.style.borderBottomWidth = selected ? 2 : 1;
            choice.style.borderLeftWidth = selected ? 2 : 1;
            var border = selected ? UITheme.SelectedBorder : UITheme.PanelBorder;
            choice.style.borderTopColor = border;
            choice.style.borderRightColor = border;
            choice.style.borderBottomColor = border;
            choice.style.borderLeftColor = border;
            choice.Add(PlayerProfileStore.CreateAvatarVisual(nickname, avatarPath, 34));

            var text = new Label(label);
            text.style.fontSize = 10;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 3;
            text.style.color = UITheme.TextPrimary;
            choice.Add(text);

            choice.RegisterCallback<ClickEvent>(_ =>
            {
                PlayerProfileStore.SelectedAvatarPath = avatarPath;
                RenderAvatarSelector();
            });
            _avatarChoices.Add(choice);
        }

        VisualElement CreatePlayerListRow(PlayerInfo player, bool isSelf)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;
            row.style.marginBottom = 4;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = player.IsReady ? UITheme.ReadySurface : UITheme.WaitingSurface;
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;
            row.style.borderTopWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            var border = player.IsReady ? UITheme.ReadyBorder : UITheme.WaitingBorder;
            row.style.borderTopColor = border;
            row.style.borderRightColor = border;
            row.style.borderBottomColor = border;
            row.style.borderLeftColor = border;

            var avatarPath = PlayerProfileStore.GetAvatarPathForPlayer(player.PlayerId, isSelf);
            var avatar = PlayerProfileStore.CreateAvatarVisual(player.Nickname, avatarPath, 38);
            avatar.style.marginRight = 10;
            row.Add(avatar);

            var name = new Label(player.Nickname + (isSelf ? "（你）" : ""));
            name.style.flexGrow = 1;
            name.style.fontSize = 14;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            var tag = new Label(player.IsHost ? "房主" : "");
            tag.style.width = 44;
            tag.style.fontSize = 12;
            tag.style.color = UITheme.TurnHighlight;
            tag.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(tag);

            var ready = new Label(player.IsReady ? "已准备" : "未准备");
            ready.style.width = 62;
            ready.style.fontSize = 12;
            ready.style.color = player.IsReady ? UITheme.ReadyBorder : UITheme.TextSecondary;
            ready.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(ready);
            return row;
        }
    }
}
