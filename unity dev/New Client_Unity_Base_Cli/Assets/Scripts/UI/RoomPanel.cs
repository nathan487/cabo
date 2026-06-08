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
        Label _title, _roomCode, _playerList, _status;
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
            _container.Add(_title);

            _roomCode = new Label();
            _roomCode.style.fontSize = 18;
            _roomCode.style.marginTop = 10;
            _roomCode.style.unityTextAlign = TextAnchor.MiddleCenter;
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

            _status = new Label();
            _status.style.fontSize = 14;
            _status.style.marginTop = 10;
            _status.style.unityTextAlign = TextAnchor.MiddleCenter;
            _status.style.color = new Color(0.5f, 0.5f, 0.5f);
            _container.Add(_status);

            _nicknameInput = new TextField("昵称");
            _nicknameInput.style.marginTop = 16;
            _nicknameInput.style.width = 260;
            _nicknameInput.style.maxWidth = 260;
            _nicknameInput.style.alignSelf = Align.Center;
            _nicknameInput.maxLength = 20;
            _container.Add(_nicknameInput);

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
            _homeButtonRow.style.display = !inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            _joinFormRow.style.display = (!inRoom && connected && _joinFormVisible) ? DisplayStyle.Flex : DisplayStyle.None;
            _roomButtonRow.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;

            _btnConnect.SetEnabled(!connecting);
            _btnConnect.text = connecting ? "连接中..." : (connected ? "重新连接" : "连接");
            _btnCreate.SetEnabled(connected);
            _btnShowJoin.SetEnabled(connected);
            _btnConfirmJoin.SetEnabled(connected);

            if (!inRoom)
            {
                _playerList.text = "";
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
            foreach (var p in s.Players)
            {
                string tag = "";
                if (p.PlayerId == s.MyPlayerId) tag += "（你）";
                if (p.IsHost) tag += " [房主]";
                string ready = p.IsReady ? " 已准备" : " 未准备";
                list += $"{p.Nickname}{tag}: {ready}\n";
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
        }
    }
}
