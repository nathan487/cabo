using UnityEngine;
using UnityEngine.UIElements;
using Cabo.Client.Art;
using Game.Room;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Home and waiting-room UI. The home page now owns server connection input.
    /// </summary>
    public class RoomPanel
    {
        VisualElement _root, _container;
        ScrollView _homeScroll, _browserRoomsScroll, _browserInboxScroll, _onlinePlayersScroll, _waitingInboxScroll;
        VisualElement _serverRow, _nicknameRow, _homeButtonRow, _joinFormRow, _roomButtonRow;
        VisualElement _avatarSection, _avatarPreview, _avatarChoices, _playerListView, _roomContent;
        VisualElement _browserContent, _browserRoomsView, _browserInboxView, _waitingAccessColumn, _onlinePlayersView, _waitingInboxView;
        ScrollView _playerListScroll;
        RoomChatPanel _chatPanel;
        Label _title, _roomCode, _playerList, _avatarStatus, _status;
        TextField _serverAddressInput, _nicknameInput, _joinCodeInput, _browserCodeInput;
        Button _btnConnect, _btnCreate, _btnShowJoin, _btnConfirmJoin, _btnExitGame;
        Button _btnBrowserBack, _btnBrowserRefresh, _btnBrowserApplyCode;
        Button _btnReady, _btnStart, _btnLeaveRoom, _btnCopyRoomCode;
        GameFlow _flow;
        bool _joinFormVisible;

        public RoomPanel(VisualElement root, GameFlow flow)
        {
            _flow = flow;
            _root = root;

            _homeScroll = new ScrollView(ScrollViewMode.Vertical);
            _homeScroll.style.flexGrow = 1;
            _homeScroll.style.width = Length.Percent(100);
            _homeScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _homeScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            root.Add(_homeScroll);

            _container = new VisualElement();
            _container.style.flexGrow = 1;
            _container.style.width = Length.Percent(100);
            _container.style.paddingTop = 20;
            _container.style.paddingBottom = 20;
            _container.style.paddingLeft = 40;
            _container.style.paddingRight = 40;
            _container.style.alignItems = Align.Center;
            _container.style.backgroundColor = Color.clear;
            _homeScroll.Add(_container);

            _title = new Label("\u7CD6\u7CD6 CABO");
            _title.style.fontSize = 34;
            _title.style.letterSpacing = 1.5f;
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
            UITheme.SetButtonRole(_btnCopyRoomCode, UITheme.SoftButtonClass);
            _btnCopyRoomCode.style.alignSelf = Align.Center;
            _btnCopyRoomCode.style.marginTop = 8;
            _btnCopyRoomCode.style.fontSize = 14;
            _container.Add(_btnCopyRoomCode);

            _serverRow = new VisualElement();
            _serverRow.style.flexDirection = FlexDirection.Row;
            _serverRow.style.justifyContent = Justify.Center;
            _serverRow.style.alignItems = Align.Center;
            _serverRow.style.marginTop = 26;
            _serverRow.style.paddingLeft = 18;
            _serverRow.style.paddingRight = 18;
            _serverRow.style.paddingTop = 4;
            _serverRow.style.paddingBottom = 4;
            UITheme.ApplyPanel(_serverRow, UITheme.PanelGlassStrong, UITheme.PanelBorder, 16f);
            _container.Add(_serverRow);

            _serverRow.Add(CreateHomeFormLabel("服务器地址"));

            _serverAddressInput = new TextField();
            _serverAddressInput.value = _flow.GetCachedServerAddress();
            StyleHomeTextField(_serverAddressInput, 300);
            _serverAddressInput.style.marginRight = 12;
            _serverAddressInput.maxLength = 128;
            _serverRow.Add(_serverAddressInput);

            _btnConnect = new Button(() =>
            {
                _status.text = "正在连接服务器...";
                _flow.ConnectToServerAddress(_serverAddressInput.value);
            });
            _btnConnect.text = "连接";
            UITheme.SetButtonRole(_btnConnect, UITheme.SecondaryButtonClass);
            _btnConnect.style.fontSize = 16;
            _btnConnect.style.height = 44;
            _btnConnect.style.minWidth = 132;
            _serverRow.Add(_btnConnect);

            _playerList = new Label();
            _playerList.style.fontSize = 16;
            _playerList.style.marginTop = 20;
            _playerList.style.whiteSpace = WhiteSpace.Normal;
            _container.Add(_playerList);

            _roomContent = new VisualElement { name = "WaitingRoomContent" };
            _roomContent.style.flexDirection = FlexDirection.Row;
            _roomContent.style.flexWrap = Wrap.Wrap;
            _roomContent.style.justifyContent = Justify.Center;
            _roomContent.style.alignItems = Align.Stretch;
            _roomContent.style.alignSelf = Align.Center;
            _roomContent.style.width = 1160;
            _roomContent.style.maxWidth = Length.Percent(100);
            _roomContent.style.minHeight = 392;
            _roomContent.style.marginTop = 12;
            _roomContent.style.overflow = Overflow.Hidden;
            _container.Add(_roomContent);

            _playerListScroll = new ScrollView(ScrollViewMode.Vertical);
            _playerListScroll.style.flexShrink = 0;
            _playerListScroll.style.width = 320;
            _playerListScroll.style.maxWidth = Length.Percent(100);
            _playerListScroll.style.height = 360;
            _playerListScroll.style.minHeight = 360;
            _playerListScroll.style.maxHeight = 360;
            _playerListScroll.style.marginRight = 16;
            _playerListScroll.style.overflow = Overflow.Hidden;
            _playerListScroll.style.paddingLeft = 10;
            _playerListScroll.style.paddingRight = 10;
            _playerListScroll.style.paddingTop = 10;
            _playerListScroll.style.paddingBottom = 10;
            UITheme.ApplyPanel(_playerListScroll, UITheme.PanelGlass, UITheme.PanelBorder, 16f);
            _playerListScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _playerListScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _roomContent.Add(_playerListScroll);

            _playerListView = _playerListScroll.contentContainer;

            _waitingAccessColumn = new VisualElement { name = "WaitingRoomAccessColumn" };
            _waitingAccessColumn.style.flexDirection = FlexDirection.Column;
            _waitingAccessColumn.style.width = 280;
            _waitingAccessColumn.style.height = 360;
            _waitingAccessColumn.style.minHeight = 360;
            _waitingAccessColumn.style.maxHeight = 360;
            _waitingAccessColumn.style.marginRight = 16;
            _waitingAccessColumn.style.flexShrink = 0;
            _waitingAccessColumn.style.overflow = Overflow.Hidden;
            _roomContent.Add(_waitingAccessColumn);

            _onlinePlayersScroll = CreateFixedScroll("WaitingOnlineLobbyPlayers", 280, 172);
            _onlinePlayersScroll.style.marginBottom = 12;
            _waitingAccessColumn.Add(_onlinePlayersScroll);
            _onlinePlayersView = _onlinePlayersScroll.contentContainer;

            _waitingInboxScroll = CreateFixedScroll("WaitingAccessInbox", 280, 176);
            _waitingAccessColumn.Add(_waitingInboxScroll);
            _waitingInboxView = _waitingInboxScroll.contentContainer;

            _status = new Label();
            _status.style.fontSize = 14;
            _status.style.marginTop = 10;
            _status.style.unityTextAlign = TextAnchor.MiddleCenter;
            _status.style.color = UITheme.TextMuted;
            _container.Add(_status);

            _nicknameRow = new VisualElement();
            _nicknameRow.style.flexDirection = FlexDirection.Row;
            _nicknameRow.style.justifyContent = Justify.Center;
            _nicknameRow.style.alignItems = Align.Center;
            _nicknameRow.style.marginTop = 16;
            _nicknameRow.style.paddingLeft = 18;
            _nicknameRow.style.paddingRight = 18;
            _nicknameRow.style.paddingTop = 4;
            _nicknameRow.style.paddingBottom = 4;
            UITheme.ApplyPanel(_nicknameRow, UITheme.PanelGlassStrong, UITheme.PanelBorder, 16f);
            _container.Add(_nicknameRow);

            _nicknameRow.Add(CreateHomeFormLabel("昵称"));

            _nicknameInput = new TextField();
            StyleHomeTextField(_nicknameInput, 180);
            _nicknameInput.maxLength = 20;
            _nicknameRow.Add(_nicknameInput);

            _avatarSection = new VisualElement();
            _avatarSection.style.alignSelf = Align.Center;
            _avatarSection.style.width = 520;
            _avatarSection.style.maxWidth = Length.Percent(100);
            _avatarSection.style.marginTop = 14;
            _avatarSection.style.paddingLeft = 12;
            _avatarSection.style.paddingRight = 12;
            _avatarSection.style.paddingTop = 10;
            _avatarSection.style.paddingBottom = 10;
            UITheme.ApplyPanel(_avatarSection, UITheme.PanelGlassStrong, UITheme.PanelBorder, 16f);
            _container.Add(_avatarSection);

            var avatarTitle = new Label("角色");
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
                _flow.CreateRoom(nickname, PlayerProfileStore.SelectedCharacterId);
            });
            _btnCreate.text = "创建房间";
            UITheme.SetButtonRole(_btnCreate, UITheme.PrimaryButtonClass);
            _btnCreate.style.marginRight = 10;
            _btnCreate.style.fontSize = 18;
            _homeButtonRow.Add(_btnCreate);

            _btnShowJoin = new Button(() =>
            {
                var nickname = GetNicknameOrShowError();
                if (nickname == null) return;
                _joinFormVisible = false;
                _flow.EnterRoomBrowser(nickname, PlayerProfileStore.SelectedCharacterId);
            });
            _btnShowJoin.text = "加入房间";
            UITheme.SetButtonRole(_btnShowJoin, UITheme.SecondaryButtonClass);
            _btnShowJoin.style.marginRight = 10;
            _btnShowJoin.style.fontSize = 18;
            _homeButtonRow.Add(_btnShowJoin);

            _btnExitGame = new Button(() => _flow.ExitGame());
            _btnExitGame.text = "退出游戏";
            UITheme.SetButtonRole(_btnExitGame, UITheme.DangerButtonClass);
            _btnExitGame.style.fontSize = 18;
            _homeButtonRow.Add(_btnExitGame);

            // Keep primary room actions visible before the optional character selector.
            _homeButtonRow.RemoveFromHierarchy();
            _container.Insert(_container.IndexOf(_avatarSection), _homeButtonRow);

            _joinFormRow = new VisualElement();
            _joinFormRow.style.flexDirection = FlexDirection.Row;
            _joinFormRow.style.justifyContent = Justify.Center;
            _joinFormRow.style.alignItems = Align.Center;
            _joinFormRow.style.marginTop = 16;
            _container.Add(_joinFormRow);

            _joinFormRow.Add(CreateHomeFormLabel("房间码"));

            _joinCodeInput = new TextField();
            StyleHomeTextField(_joinCodeInput, 180);
            _joinCodeInput.style.marginRight = 12;
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

                _flow.JoinRoom(code, nickname, PlayerProfileStore.SelectedCharacterId);
            });
            _btnConfirmJoin.text = "确认加入";
            UITheme.SetButtonRole(_btnConfirmJoin, UITheme.PrimaryButtonClass);
            _btnConfirmJoin.style.fontSize = 16;
            _btnConfirmJoin.style.height = 44;
            _joinFormRow.Add(_btnConfirmJoin);

            _joinFormRow.RemoveFromHierarchy();
            _container.Insert(_container.IndexOf(_avatarSection), _joinFormRow);

            _browserContent = new VisualElement { name = "RoomBrowserContent" };
            _browserContent.style.alignSelf = Align.Center;
            _browserContent.style.width = 1040;
            _browserContent.style.maxWidth = Length.Percent(100);
            _browserContent.style.marginTop = 16;
            _browserContent.style.flexDirection = FlexDirection.Column;
            _browserContent.style.display = DisplayStyle.None;
            _container.Add(_browserContent);

            var browserTopRow = new VisualElement { name = "RoomBrowserTopRow" };
            browserTopRow.style.flexDirection = FlexDirection.Row;
            browserTopRow.style.flexWrap = Wrap.Wrap;
            browserTopRow.style.alignItems = Align.Center;
            browserTopRow.style.justifyContent = Justify.Center;
            browserTopRow.style.marginBottom = 12;
            _browserContent.Add(browserTopRow);

            _btnBrowserBack = new Button(() =>
            {
                _joinFormVisible = false;
                _flow.ReturnHomeFromRoomBrowser();
            });
            _btnBrowserBack.text = "返回首页";
            UITheme.SetButtonRole(_btnBrowserBack, UITheme.SoftButtonClass);
            _btnBrowserBack.style.height = 40;
            _btnBrowserBack.style.marginRight = 8;
            browserTopRow.Add(_btnBrowserBack);

            browserTopRow.Add(CreateHomeFormLabel("房间码"));

            _browserCodeInput = new TextField();
            StyleHomeTextField(_browserCodeInput, 190);
            _browserCodeInput.maxLength = 16;
            _browserCodeInput.style.marginRight = 8;
            browserTopRow.Add(_browserCodeInput);

            _btnBrowserApplyCode = new Button(() =>
            {
                var code = _browserCodeInput.value?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                {
                    _status.text = "请输入房间码。";
                    return;
                }
                _flow.JoinRoomFromBrowser(code);
            });
            _btnBrowserApplyCode.text = "直接加入";
            UITheme.SetButtonRole(_btnBrowserApplyCode, UITheme.PrimaryButtonClass);
            _btnBrowserApplyCode.style.height = 40;
            _btnBrowserApplyCode.style.marginRight = 8;
            browserTopRow.Add(_btnBrowserApplyCode);

            _btnBrowserRefresh = new Button(() => _flow.RefreshRooms());
            _btnBrowserRefresh.text = "刷新";
            UITheme.SetButtonRole(_btnBrowserRefresh, UITheme.SecondaryButtonClass);
            _btnBrowserRefresh.style.height = 40;
            browserTopRow.Add(_btnBrowserRefresh);

            var browserLists = new VisualElement { name = "RoomBrowserLists" };
            browserLists.style.flexDirection = FlexDirection.Row;
            browserLists.style.flexWrap = Wrap.Wrap;
            browserLists.style.justifyContent = Justify.Center;
            browserLists.style.alignItems = Align.Stretch;
            _browserContent.Add(browserLists);

            _browserRoomsScroll = CreateFixedScroll("RoomBrowserRooms", 560, 360);
            _browserRoomsScroll.style.marginRight = 14;
            browserLists.Add(_browserRoomsScroll);
            _browserRoomsView = _browserRoomsScroll.contentContainer;

            _browserInboxScroll = CreateFixedScroll("RoomBrowserInbox", 430, 360);
            browserLists.Add(_browserInboxScroll);
            _browserInboxView = _browserInboxScroll.contentContainer;

            _roomButtonRow = new VisualElement();
            _roomButtonRow.style.flexDirection = FlexDirection.Row;
            _roomButtonRow.style.justifyContent = Justify.Center;
            _roomButtonRow.style.marginTop = 20;
            _container.Add(_roomButtonRow);

            _btnReady = new Button(() => { _flow.SendReady(); _status.text = "已发送准备。"; });
            _btnReady.text = "准备";
            UITheme.SetButtonRole(_btnReady, UITheme.SecondaryButtonClass);
            _btnReady.style.marginRight = 10;
            _btnReady.style.fontSize = 18;
            _roomButtonRow.Add(_btnReady);

            _btnStart = new Button(() => { _flow.SendStartGame(); _status.text = "正在开始游戏..."; });
            _btnStart.text = "开始游戏";
            UITheme.SetButtonRole(_btnStart, UITheme.PrimaryButtonClass);
            _btnStart.style.marginRight = 10;
            _btnStart.style.fontSize = 18;
            _roomButtonRow.Add(_btnStart);

            _btnLeaveRoom = new Button(() =>
            {
                _joinFormVisible = false;
                _flow.LeaveRoomToHome();
            });
            _btnLeaveRoom.text = "退出房间";
            UITheme.SetButtonRole(_btnLeaveRoom, UITheme.DangerButtonClass);
            _btnLeaveRoom.style.fontSize = 18;
            _roomButtonRow.Add(_btnLeaveRoom);

            _chatPanel = new RoomChatPanel(_flow);
            _chatPanel.Root.style.flexGrow = 0;
            _chatPanel.Root.style.flexShrink = 0;
            _chatPanel.Root.style.width = 500;
            _chatPanel.Root.style.minWidth = 360;
            _chatPanel.Root.style.maxWidth = Length.Percent(100);
            _chatPanel.Root.style.paddingLeft = 12;
            _chatPanel.Root.style.paddingRight = 12;
            _chatPanel.Root.style.paddingTop = 10;
            _chatPanel.Root.style.paddingBottom = 10;
            UITheme.ApplyPanel(_chatPanel.Root, UITheme.PanelGlass, UITheme.PanelBorder, 16f);
            _roomContent.Add(_chatPanel.Root);
        }

        public void SetVisible(bool visible)
        {
            _homeScroll.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

        static Label CreateHomeFormLabel(string text)
        {
            var label = new Label(text);
            label.style.width = 112;
            label.style.minWidth = 112;
            label.style.marginRight = 12;
            label.style.fontSize = 15;
            label.style.color = UITheme.TextSecondary;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
        }

        static void StyleHomeTextField(TextField field, float width)
        {
            if (field == null)
                return;

            field.style.width = width;
            field.style.minWidth = width;
            field.style.maxWidth = width;
            field.style.height = 44;
            field.style.fontSize = 16;
            field.style.marginLeft = 0;
            field.style.marginRight = 0;
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
        }

        public void Render()
        {
            var state = _flow.State;
            var inRoom = state.RoomId > 0 || state.Players.Count > 0;
            var browsing = _flow.Flow == FlowState.RoomBrowser && !inRoom;

            if (browsing)
            {
                RenderBrowser();
                return;
            }

            if (inRoom)
            {
                RenderWaitingRoom();
                return;
            }

            RenderHome();
        }

        void RenderHome()
        {
            var connected = _flow.IsConnected;
            var connecting = _flow.Flow == FlowState.Connecting;

            _title.text = "\u7CD6\u7CD6 CABO";
            _roomCode.text = "";
            _btnCopyRoomCode.style.display = DisplayStyle.None;

            _serverRow.style.display = DisplayStyle.Flex;
            _nicknameRow.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;
            _avatarSection.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;
            _homeButtonRow.style.display = DisplayStyle.Flex;
            _joinFormRow.style.display = DisplayStyle.None;
            _browserContent.style.display = DisplayStyle.None;
            _roomButtonRow.style.display = DisplayStyle.None;
            _roomContent.style.display = DisplayStyle.None;
            _playerList.style.display = DisplayStyle.None;
            _playerListView.Clear();
            _browserRoomsView?.Clear();
            _browserInboxView?.Clear();
            _onlinePlayersView?.Clear();
            _waitingInboxView?.Clear();
            _chatPanel.Root.style.display = DisplayStyle.None;

            _btnConnect.SetEnabled(!connecting);
            _btnConnect.text = connecting ? "连接中..." : (connected ? "重新连接" : "连接");
            _btnCreate.SetEnabled(connected);
            _btnShowJoin.SetEnabled(connected);
            _btnConfirmJoin.SetEnabled(connected);

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
        }

        void RenderBrowser()
        {
            var state = _flow.State;
            var connected = _flow.IsConnected;

            _title.text = "\u7CD6\u7CD6 CABO · 房间大厅";
            _roomCode.text = state.LobbyPlayerId > 0 ? $"大厅玩家 ID：{state.LobbyPlayerId}" : "正在进入房间大厅...";
            _btnCopyRoomCode.style.display = DisplayStyle.None;

            _serverRow.style.display = DisplayStyle.None;
            _nicknameRow.style.display = DisplayStyle.None;
            _avatarSection.style.display = DisplayStyle.None;
            _homeButtonRow.style.display = DisplayStyle.None;
            _joinFormRow.style.display = DisplayStyle.None;
            _roomButtonRow.style.display = DisplayStyle.None;
            _roomContent.style.display = DisplayStyle.None;
            _playerList.style.display = DisplayStyle.None;
            _browserContent.style.display = DisplayStyle.Flex;
            _chatPanel.Root.style.display = DisplayStyle.None;

            _btnBrowserBack.SetEnabled(true);
            _btnBrowserApplyCode.SetEnabled(connected && state.LobbyPlayerId > 0);
            _btnBrowserRefresh.SetEnabled(connected);

            RenderRoomSummaryList(_browserRoomsView, state, true);
            RenderInboxList(_browserInboxView, state, true);
            _status.text = BuildAccessStatus(state, connected ? "选择房间后发送申请，或处理收到的邀请。" : "已断开服务器连接。");
        }

        void RenderWaitingRoom()
        {
            var state = _flow.State;
            var hasRoomCode = !string.IsNullOrEmpty(state.RoomCode);

            _title.text = "\u7CD6\u7CD6 CABO · 等待房间";
            _roomCode.text = hasRoomCode ? $"房间码：{state.RoomCode}" : "正在加入...";
            _btnCopyRoomCode.style.display = hasRoomCode ? DisplayStyle.Flex : DisplayStyle.None;

            _serverRow.style.display = DisplayStyle.None;
            _nicknameRow.style.display = DisplayStyle.None;
            _avatarSection.style.display = DisplayStyle.None;
            _homeButtonRow.style.display = DisplayStyle.None;
            _joinFormRow.style.display = DisplayStyle.None;
            _browserContent.style.display = DisplayStyle.None;
            _roomButtonRow.style.display = DisplayStyle.Flex;
            _roomContent.style.display = DisplayStyle.Flex;
            _playerListScroll.style.display = DisplayStyle.Flex;
            _waitingAccessColumn.style.display = DisplayStyle.Flex;
            _chatPanel.Root.style.display = DisplayStyle.Flex;
            _playerList.style.display = DisplayStyle.None;

            var readyCount = 0;
            _playerListView.Clear();
            AddSectionTitle(_playerListView, $"房内玩家 {state.Players.Count}/{state.MaxPlayers}");
            foreach (var player in state.Players)
            {
                _playerListView.Add(CreatePlayerListRow(player, player.PlayerId == state.MyPlayerId));
                if (player.IsReady)
                    readyCount++;
            }
            if (state.Players.Count == 0)
                _playerListView.Add(CreateEmptyLabel("等待房间状态同步。"));

            RenderOnlineLobbyPlayers(state);
            RenderInboxList(_waitingInboxView, state, false);

            var allReady = readyCount == state.Players.Count && state.Players.Count >= 2;
            _btnStart.SetEnabled(state.IsMyselfHost && allReady);
            _status.text = BuildAccessStatus(state,
                $"{readyCount}/{state.Players.Count} 已准备" + (allReady && state.IsMyselfHost ? " · 可以开始" : ""));
            _chatPanel.Render();
        }

        void RenderLegacy()
        {
            var s = _flow.State;
            bool connected = _flow.IsConnected;
            bool connecting = _flow.Flow == FlowState.Connecting;
            bool inRoom = s.RoomId > 0 || s.Players.Count > 0;
            bool hasRoomCode = !string.IsNullOrEmpty(s.RoomCode);

            _title.text = inRoom ? "\u7CD6\u7CD6 CABO \u00B7 \u7B49\u5F85\u9910\u5385" : "\u7CD6\u7CD6 CABO";
            _roomCode.text = hasRoomCode ? $"房间码：{s.RoomCode}" : (inRoom ? "正在加入..." : "");
            _btnCopyRoomCode.style.display = hasRoomCode ? DisplayStyle.Flex : DisplayStyle.None;

            _serverRow.style.display = inRoom ? DisplayStyle.None : DisplayStyle.Flex;
            _nicknameRow.style.display = (!inRoom && connected) ? DisplayStyle.Flex : DisplayStyle.None;
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
            var selectedId = PlayerProfileStore.SelectedCharacterId;
            _avatarPreview.Add(PlayerProfileStore.CreateAvatarVisual(
                nickname, PlayerProfileStore.GetCharacterVisualPath(selectedId), 56));

            var characters = CaboArt.Catalog?.characters;
            if (characters != null)
            {
                foreach (var character in characters)
                {
                    if (character == null || string.IsNullOrWhiteSpace(character.characterId))
                        continue;
                    AddAvatarChoice(character.displayName, character.characterId,
                        string.Equals(character.characterId, selectedId, System.StringComparison.OrdinalIgnoreCase), nickname);
                }
            }

            _avatarStatus.text = characters == null || characters.Length == 0
                ? "暂无角色资源，使用默认角色柚柚"
                : $"已选择：{CaboArt.GetCharacter(selectedId)?.displayName ?? selectedId}";
        }

        void AddAvatarChoice(string label, string characterId, bool selected, string nickname)
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
            choice.Add(PlayerProfileStore.CreateAvatarVisual(
                nickname, PlayerProfileStore.GetCharacterVisualPath(characterId), 34));

            var text = new Label(label);
            text.style.fontSize = 10;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            text.style.marginTop = 3;
            text.style.color = UITheme.TextPrimary;
            choice.Add(text);

            choice.RegisterCallback<ClickEvent>(_ =>
            {
                PlayerProfileStore.SelectedCharacterId = characterId;
                RenderAvatarSelector();
            });
            _avatarChoices.Add(choice);
        }

        ScrollView CreateFixedScroll(string name, float width, float height)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = name };
            scroll.style.width = width;
            scroll.style.maxWidth = Length.Percent(100);
            scroll.style.height = height;
            scroll.style.minHeight = height;
            scroll.style.maxHeight = height;
            scroll.style.flexShrink = 0;
            scroll.style.overflow = Overflow.Hidden;
            scroll.style.paddingLeft = 10;
            scroll.style.paddingRight = 10;
            scroll.style.paddingTop = 10;
            scroll.style.paddingBottom = 10;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            UITheme.ApplyPanel(scroll, UITheme.PanelGlass, UITheme.PanelBorder, 16f);
            return scroll;
        }

        void RenderRoomSummaryList(VisualElement target, GameState state, bool allowApply)
        {
            target.Clear();
            AddSectionTitle(target, $"已有房间 {state.RoomSummaries.Count}");
            if (state.RoomSummaries.Count == 0)
            {
                target.Add(CreateEmptyLabel("暂无等待中的房间。"));
                return;
            }

            foreach (var room in state.RoomSummaries)
                target.Add(CreateRoomSummaryRow(room, state, allowApply));
        }

        VisualElement CreateRoomSummaryRow(RoomSummaryInfo room, GameState state, bool allowApply)
        {
            var row = CreateListCard(room.IsFull ? UITheme.WaitingSurface : UITheme.PanelSurfaceAlt);

            var title = new Label($"{SafeName(room.HostNickname)}的房间");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            row.Add(title);

            var meta = new Label($"房间码 {room.RoomCode} · {room.PlayerCount}/{room.MaxPlayers}");
            meta.style.fontSize = 12;
            meta.style.color = UITheme.TextSecondary;
            meta.style.marginTop = 3;
            row.Add(meta);

            var pending = HasPendingJoinApplication(state, room.RoomId, room.RoomCode);
            var button = CreateSmallButton(room.IsFull ? "已满" : pending ? "已申请" : "申请加入", UITheme.PrimaryButtonClass);
            button.style.alignSelf = Align.FlexEnd;
            button.style.marginTop = 7;
            button.SetEnabled(allowApply && state.LobbyPlayerId > 0 && !room.IsFull && !pending);
            button.clicked += () => _flow.ApplyJoinRoom(room.RoomId, room.RoomCode);
            row.Add(button);
            return row;
        }

        void RenderOnlineLobbyPlayers(GameState state)
        {
            _onlinePlayersView.Clear();
            AddSectionTitle(_onlinePlayersView, $"在线玩家 {state.OnlineLobbyPlayers.Count}");
            if (state.OnlineLobbyPlayers.Count == 0)
            {
                _onlinePlayersView.Add(CreateEmptyLabel("暂无可邀请玩家。"));
                return;
            }

            var full = state.MaxPlayers > 0 && state.Players.Count >= state.MaxPlayers;
            foreach (var player in state.OnlineLobbyPlayers)
                _onlinePlayersView.Add(CreateOnlineLobbyPlayerRow(player, state, full));
        }

        VisualElement CreateOnlineLobbyPlayerRow(OnlineLobbyPlayerInfo player, GameState state, bool full)
        {
            var row = CreateListCard(full ? UITheme.WaitingSurface : UITheme.PanelSurfaceAlt);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var avatar = PlayerProfileStore.CreateAvatarVisual(
                player.Nickname,
                PlayerProfileStore.GetCharacterVisualPath(player.CharacterId),
                32);
            avatar.style.marginRight = 8;
            row.Add(avatar);

            var name = new Label(SafeName(player.Nickname));
            name.style.flexGrow = 1;
            name.style.minWidth = 0;
            name.style.fontSize = 13;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.whiteSpace = WhiteSpace.Normal;
            row.Add(name);

            var pending = HasPendingInvitation(state, player.LobbyPlayerId);
            var button = CreateSmallButton(full ? "已满" : pending ? "已邀请" : "邀请", UITheme.SecondaryButtonClass);
            button.SetEnabled(!full && !pending && state.MyPlayerId > 0 && state.RoomId > 0);
            button.clicked += () => _flow.InviteLobbyPlayer(player.LobbyPlayerId);
            row.Add(button);
            return row;
        }

        void RenderInboxList(VisualElement target, GameState state, bool browserMode)
        {
            target.Clear();
            AddSectionTitle(target, browserMode ? "我的邀请" : "申请与邀请");

            var visible = 0;
            foreach (var item in state.AccessInboxItems)
            {
                if (browserMode && item.Type != RoomAccessType.RoomInvitation)
                    continue;
                if (!browserMode && item.Type != RoomAccessType.JoinApplication && item.Type != RoomAccessType.RoomInvitation)
                    continue;

                target.Add(CreateInboxRow(item, state, browserMode));
                visible++;
            }

            if (visible == 0)
                target.Add(CreateEmptyLabel(browserMode ? "暂无房间邀请。" : "暂无待处理申请或邀请。"));
        }

        VisualElement CreateInboxRow(RoomAccessInboxItem item, GameState state, bool browserMode)
        {
            var row = CreateListCard(UITheme.PanelSurfaceAlt);
            var isInvite = item.Type == RoomAccessType.RoomInvitation;
            var titleText = isInvite
                ? browserMode
                    ? $"{SafeName(item.RequesterNickname)}邀请你加入 {SafeName(item.HostNickname)}的房间"
                    : $"已邀请 {SafeName(item.LobbyNickname)} 加入房间"
                : $"{SafeName(item.LobbyNickname)}申请加入房间";
            var title = new Label(titleText);
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            row.Add(title);

            var meta = new Label($"房间码 {item.RoomCode} · {AccessStatusText(item.Status)}");
            meta.style.fontSize = 11;
            meta.style.color = UITheme.TextSecondary;
            meta.style.marginTop = 3;
            row.Add(meta);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginTop = 8;
            row.Add(actions);

            var canRespond = item.Status == RoomAccessStatus.Pending
                && ((isInvite && browserMode && state.LobbyPlayerId == item.LobbyPlayerId)
                    || (!isInvite && state.IsMyselfHost));
            var accept = CreateSmallButton("同意", UITheme.PrimaryButtonClass);
            accept.style.marginRight = 6;
            accept.SetEnabled(canRespond);
            accept.clicked += () =>
            {
                if (isInvite)
                    _flow.RespondRoomInvitation(item.AccessId, true);
                else
                    _flow.RespondJoinApplication(item.AccessId, true);
            };
            actions.Add(accept);

            var reject = CreateSmallButton("拒绝", UITheme.DangerButtonClass);
            reject.SetEnabled(canRespond);
            reject.clicked += () =>
            {
                if (isInvite)
                    _flow.RespondRoomInvitation(item.AccessId, false);
                else
                    _flow.RespondJoinApplication(item.AccessId, false);
            };
            actions.Add(reject);
            return row;
        }

        void AddSectionTitle(VisualElement target, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 8;
            label.style.color = UITheme.TextPrimary;
            target.Add(label);
        }

        Label CreateEmptyLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.color = UITheme.TextMuted;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 4;
            return label;
        }

        VisualElement CreateListCard(Color surface)
        {
            var card = new VisualElement();
            card.style.marginBottom = 8;
            card.style.paddingLeft = 9;
            card.style.paddingRight = 9;
            card.style.paddingTop = 7;
            card.style.paddingBottom = 7;
            card.style.backgroundColor = surface;
            card.style.flexShrink = 0;
            UITheme.SetRadius(card, 7f);
            UITheme.SetBorderWidth(card, 1f);
            UITheme.SetBorderColor(card, UITheme.PanelBorder);
            return card;
        }

        Button CreateSmallButton(string text, string roleClass)
        {
            var button = new Button();
            button.text = text;
            UITheme.SetButtonRole(button, roleClass);
            button.style.height = 30;
            button.style.minWidth = 62;
            button.style.fontSize = 12;
            button.style.paddingLeft = 9;
            button.style.paddingRight = 9;
            return button;
        }

        static string BuildAccessStatus(GameState state, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(state.LastRoomAccessError))
                return state.LastRoomAccessError;
            if (!string.IsNullOrWhiteSpace(state.LastRoomAccessMessage))
                return state.LastRoomAccessMessage;
            return fallback;
        }

        static bool HasPendingJoinApplication(GameState state, long roomId, string roomCode)
        {
            foreach (var item in state.AccessInboxItems)
            {
                if (item.Type == RoomAccessType.JoinApplication
                    && item.Status == RoomAccessStatus.Pending
                    && (item.RoomId == roomId || string.Equals(item.RoomCode, roomCode, System.StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        static bool HasPendingInvitation(GameState state, long lobbyPlayerId)
        {
            foreach (var item in state.AccessInboxItems)
            {
                if (item.Type == RoomAccessType.RoomInvitation
                    && item.Status == RoomAccessStatus.Pending
                    && item.LobbyPlayerId == lobbyPlayerId)
                    return true;
            }
            return false;
        }

        static string AccessStatusText(RoomAccessStatus status)
        {
            return status switch
            {
                RoomAccessStatus.Pending => "待处理",
                RoomAccessStatus.Approved => "已同意",
                RoomAccessStatus.Rejected => "已拒绝",
                RoomAccessStatus.Expired => "已过期",
                _ => "未知"
            };
        }

        static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "玩家" : value.Trim();
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

            var avatarPath = PlayerProfileStore.GetCharacterVisualPath(player.CharacterId);
            var avatar = PlayerProfileStore.CreateAvatarVisual(player.Nickname, avatarPath, 38);
            avatar.style.marginRight = 10;
            row.Add(avatar);

            var name = new Label(player.Nickname + (isSelf ? "（你）" : ""));
            name.style.flexGrow = 1;
            name.style.fontSize = 14;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            var tag = new VisualElement();
            tag.style.width = 78;
            tag.style.height = 24;
            tag.style.flexDirection = FlexDirection.Row;
            tag.style.alignItems = Align.Center;
            tag.style.justifyContent = Justify.Center;
            tag.style.backgroundColor = player.IsHost ? UITheme.HostBadgeSurface : Color.clear;
            UITheme.SetRadius(tag, 6f);
            UITheme.SetBorderWidth(tag, player.IsHost ? 1f : 0f);
            UITheme.SetBorderColor(tag, UITheme.HostBadgeBorder);
            if (player.IsHost)
            {
                var crown = new Image
                {
                    image = UITheme.HostCrownIcon,
                    scaleMode = ScaleMode.ScaleToFit
                };
                crown.style.width = 15;
                crown.style.height = 15;
                crown.style.marginRight = 4;
                tag.Add(crown);

                var tagText = new Label("房主");
                tagText.style.fontSize = 12;
                tagText.style.unityFontStyleAndWeight = FontStyle.Bold;
                tagText.style.color = UITheme.TextPrimary;
                tagText.style.unityTextAlign = TextAnchor.MiddleCenter;
                tag.Add(tagText);
            }
            row.Add(tag);

            var ready = new Label(player.IsReady ? "已准备" : "未准备");
            ready.style.width = 62;
            ready.style.fontSize = 12;
            ready.style.color = UITheme.TextPrimary;
            ready.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(ready);
            return row;
        }
    }
}
