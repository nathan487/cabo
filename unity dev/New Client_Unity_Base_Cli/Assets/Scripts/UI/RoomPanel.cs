using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Room waiting UI. Shows player list, ready button, room code.
    /// </summary>
    public class RoomPanel
    {
        VisualElement _root, _container;
        Label _roomCode, _playerList, _status;
        TextField _nicknameInput, _joinCodeInput;
        Button _btnCreate, _btnJoin, _btnReady, _btnStart, _btnCopyRoomCode;
        GameFlow _flow;

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

            var title = new Label("Cabo - Waiting Room");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _container.Add(title);

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
                _status.text = $"Copied room code: {roomCode}";
            });
            _btnCopyRoomCode.text = "Copy Code";
            _btnCopyRoomCode.style.alignSelf = Align.Center;
            _btnCopyRoomCode.style.marginTop = 8;
            _btnCopyRoomCode.style.fontSize = 14;
            _container.Add(_btnCopyRoomCode);

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

            _nicknameInput = new TextField("Nickname");
            _nicknameInput.style.marginTop = 16;
            _nicknameInput.style.width = 260;
            _nicknameInput.style.maxWidth = 260;
            _nicknameInput.style.alignSelf = Align.Center;
            _nicknameInput.maxLength = 20;
            _container.Add(_nicknameInput);

            _joinCodeInput = new TextField("Room Code");
            _joinCodeInput.style.marginTop = 16;
            _joinCodeInput.style.width = 260;
            _joinCodeInput.style.maxWidth = 260;
            _joinCodeInput.style.alignSelf = Align.Center;
            _joinCodeInput.maxLength = 16;
            _container.Add(_joinCodeInput);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.Center;
            btnRow.style.marginTop = 20;
            _container.Add(btnRow);

            // Create/Join buttons (room flow)
            var btnCreate = new Button(() =>
            {
                var nickname = GetNicknameOrShowError();
                if (nickname == null) return;
                _flow.CreateRoom(nickname);
            });
            btnCreate.text = "Create Room";
            btnCreate.style.marginRight = 10;
            btnCreate.style.fontSize = 18;
            btnRow.Add(btnCreate);
            _btnCreate = btnCreate;

            var btnJoin = new Button(() =>
            {
                var nickname = GetNicknameOrShowError();
                if (nickname == null) return;

                var code = _joinCodeInput.value?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(code))
                {
                    _status.text = "Enter a room code first.";
                    return;
                }
                _flow.JoinRoom(code, nickname);
            });
            btnJoin.text = "Join Room";
            btnJoin.style.fontSize = 18;
            btnRow.Add(btnJoin);
            _btnJoin = btnJoin;

            // Ready/Start buttons (waiting room, hidden initially)
            _btnReady = new Button(() => { _flow.SendReady(); _status.text = "Ready sent!"; });
            _btnReady.text = "Ready";
            _btnReady.style.marginRight = 10;
            _btnReady.style.fontSize = 18;
            btnRow.Add(_btnReady);

            _btnStart = new Button(() => { _flow.SendStartGame(); _status.text = "Starting game..."; });
            _btnStart.text = "Start Game";
            _btnStart.style.fontSize = 18;
            btnRow.Add(_btnStart);
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
                _status.text = "Enter a nickname first.";
                return null;
            }
            if (nickname.Length > 20)
            {
                _status.text = "Nickname must be 1-20 characters.";
                return null;
            }
            return nickname;
        }

        public void Render()
        {
            var s = _flow.State;
            bool inRoom = s.RoomId > 0 || s.Players.Count > 0;
            bool hasRoomCode = !string.IsNullOrEmpty(s.RoomCode);
            _roomCode.text = hasRoomCode ? $"Room Code: {s.RoomCode}" : (inRoom ? "Joining..." : "");
            if (_btnCopyRoomCode != null) _btnCopyRoomCode.style.display = hasRoomCode ? DisplayStyle.Flex : DisplayStyle.None;

            // Show create/join only when NOT in room
            if (_btnCreate != null) _btnCreate.style.display = !inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnJoin != null) _btnJoin.style.display = !inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            if (_nicknameInput != null) _nicknameInput.style.display = !inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            if (_joinCodeInput != null) _joinCodeInput.style.display = !inRoom ? DisplayStyle.Flex : DisplayStyle.None;

            // Show ready/start only when in room
            if (_btnReady != null) _btnReady.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnStart != null) _btnStart.style.display = inRoom ? DisplayStyle.Flex : DisplayStyle.None;

            // Build player list with ready status
            string list = "";
            int readyCount = 0;
            foreach (var p in s.Players)
            {
                string tag = "";
                if (p.PlayerId == s.MyPlayerId) tag += " (You)";
                if (p.IsHost) tag += " [Host]";
                string ready = p.IsReady ? " Ready" : " Not ready";
                list += $"{p.Nickname}{tag}:{ready}\n";
                if (p.IsReady) readyCount++;
            }
            _playerList.text = list;

            bool isHost = false;
            bool allReady = readyCount == s.Players.Count && s.Players.Count >= 2;
            foreach (var p in s.Players)
                if (p.PlayerId == s.MyPlayerId && p.IsHost) { isHost = true; break; }

            _btnStart.SetEnabled(isHost && allReady);
            _status.text = $"{readyCount}/{s.Players.Count} ready" + (allReady && isHost ? " - You can start!" : "");
        }
    }
}
