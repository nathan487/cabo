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
        Button _btnReady, _btnStart;
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

            var title = new Label("Cabo — Waiting Room");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _container.Add(title);

            _roomCode = new Label();
            _roomCode.style.fontSize = 18;
            _roomCode.style.marginTop = 10;
            _roomCode.style.unityTextAlign = TextAnchor.MiddleCenter;
            _container.Add(_roomCode);

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

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.Center;
            btnRow.style.marginTop = 20;
            _container.Add(btnRow);

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

        public void Render()
        {
            var s = _flow.State;
            _roomCode.text = s.RoomCode.Length > 0 ? $"Room: {s.RoomCode}" : "";

            // Build player list with ready status
            string list = "";
            int readyCount = 0;
            foreach (var p in s.Players)
            {
                string tag = "";
                if (p.PlayerId == s.MyPlayerId) tag += " (You)";
                if (p.IsHost) tag += " [Host]";
                string ready = p.IsReady ? " ✅" : " ⬜";
                list += $"{p.Nickname}{tag}:{ready}\n";
                if (p.IsReady) readyCount++;
            }
            _playerList.text = list;

            // Enable start only for host when all ready
            bool isHost = false;
            bool allReady = readyCount == s.Players.Count && s.Players.Count == 4;
            foreach (var p in s.Players)
                if (p.PlayerId == s.MyPlayerId && p.IsHost) { isHost = true; break; }

            _btnStart.SetEnabled(isHost && allReady);
            _status.text = $"{readyCount}/{s.Players.Count} ready" + (allReady && isHost ? " — You can start!" : "");
        }
    }
}
