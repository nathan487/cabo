using Cabo.Client.Network;
using Cabo.Client.Room;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.UI
{
    public sealed class LobbyRoomDemoUI : MonoBehaviour
    {
        private RoomClientController controller;

        private Text titleText;
        private Text connectionText;
        private Text roomInfoText;
        private Text eventLogText;
        private InputField nicknameInput;
        private InputField roomCodeInput;
        private Toggle readyToggle;
        private GameObject gameActionPanel;
        private Text turnInfoText;
        private long myPlayerId;

        private void Awake()
        {
            controller = GetComponent<RoomClientController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<RoomClientController>();
            }

            BuildUi();
        }

        private void OnEnable()
        {
            controller.ConnectionStatusChanged += HandleConnectionChanged;
            controller.RoomUpdated += HandleRoomUpdated;
            controller.RoomStarted += HandleRoomStarted;
            controller.ErrorRaised += HandleError;
        }

        private void OnDisable()
        {
            controller.ConnectionStatusChanged -= HandleConnectionChanged;
            controller.RoomUpdated -= HandleRoomUpdated;
            controller.RoomStarted -= HandleRoomStarted;
            controller.ErrorRaised -= HandleError;
        }

        private void BuildUi()
        {
            var canvasObj = new GameObject("ClientCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            var root = CreatePanel(canvasObj.transform, "Root", new Vector2(780, 560), new Color(0.08f, 0.12f, 0.18f, 0.95f));
            var font = ResolveFont();

            // Shift all content down by ~50px so title is fully visible
            // Title — centered near top
            titleText = CreateText(root.transform, "Title", "Cabo Network Test", font, 24, Vector2.zero, new Vector2(400, 36));
            titleText.rectTransform.anchoredPosition = new Vector2(0, 245);
            titleText.alignment = TextAnchor.MiddleCenter;

            // Connection status
            connectionText = CreateText(root.transform, "ConnStatus", "Status: Disconnected", font, 16, Vector2.zero, new Vector2(360, 28));
            connectionText.rectTransform.anchoredPosition = new Vector2(-180, 205);

            // --- Input row ---
            float inputRowY = 155;
            // Nickname
            CreateText(root.transform, "NickLbl", "Name:", font, 14, Vector2.zero, new Vector2(60, 28))
                .rectTransform.anchoredPosition = new Vector2(-340, inputRowY);
            nicknameInput = CreateInput(root.transform, "NickInput", font, "PlayerA", new Vector2(-230, inputRowY), new Vector2(160, 34));

            // Room code
            CreateText(root.transform, "CodeLbl", "Code:", font, 14, Vector2.zero, new Vector2(60, 28))
                .rectTransform.anchoredPosition = new Vector2(-30, inputRowY);
            roomCodeInput = CreateInput(root.transform, "CodeInput", font, "ABC123", new Vector2(80, inputRowY), new Vector2(140, 34));

            // Ready toggle + Copy code
            readyToggle = CreateToggle(root.transform, font, "Ready", new Vector2(230, inputRowY));
            readyToggle.onValueChanged.AddListener(value => controller.SetReady(value));
            CreateButton(root.transform, font, "Copy Code", new Vector2(320, inputRowY),
                new Vector2(100, 34), new Color(0.3f, 0.4f, 0.5f), OnCopyRoomCodeClick);

            // --- Row 1: main actions (4 buttons, 140px wide) ---
            float btnY = 90;
            CreateButton(root.transform, font, "Connect", new Vector2(-260, btnY), OnConnectClick);
            CreateButton(root.transform, font, "Create Room", new Vector2(-90, btnY), OnCreateRoomClick);
            CreateButton(root.transform, font, "Join Room", new Vector2(80, btnY), OnJoinRoomClick);
            CreateButton(root.transform, font, "Start Game", new Vector2(250, btnY), controller.StartGame);

            // --- Row 2: disconnect + leave ---
            float btnY2 = 28;
            CreateButton(root.transform, font, "Disconnect", new Vector2(-90, btnY2), OnDisconnectClick);
            CreateButton(root.transform, font, "Leave Room", new Vector2(80, btnY2), OnLeaveRoomClick);

            // --- Game Action Panel (hidden until game starts) ---
            gameActionPanel = new GameObject("GameActions", typeof(RectTransform));
            gameActionPanel.transform.SetParent(root.transform, false);
            gameActionPanel.SetActive(false);
            AddGameActionButtons(gameActionPanel.transform, font);

            // --- Info area ---
            roomInfoText = CreateText(root.transform, "RoomInfo", "Not in room.\nConnect, then Create Room.", font, 15, Vector2.zero, new Vector2(720, 220));
            roomInfoText.rectTransform.anchoredPosition = new Vector2(0, -70);
            roomInfoText.alignment = TextAnchor.UpperLeft;
            roomInfoText.raycastTarget = false; // Don't block button clicks

            // Event log
            eventLogText = CreateText(root.transform, "EventLog", "", font, 13, Vector2.zero, new Vector2(720, 80));
            eventLogText.rectTransform.anchoredPosition = new Vector2(0, -220);
            eventLogText.alignment = TextAnchor.UpperLeft;
            eventLogText.color = new Color(0.7f, 0.9f, 0.95f);
            eventLogText.raycastTarget = false; // Don't block button clicks

            AppendLog("UI Ready. Click Connect first.");
        }

        private void OnConnectClick()
        {
            var nick = string.IsNullOrWhiteSpace(nicknameInput.text) ? "PlayerA" : nicknameInput.text.Trim();
            Debug.Log("[LobbyUI] Connect button clicked");
            controller.Connect(nick);
        }

        private void OnCreateRoomClick()
        {
            Debug.Log("[LobbyUI] Create Room button clicked");
            controller.CreateRoom(4);
        }

        private void OnJoinRoomClick()
        {
            Debug.Log("[LobbyUI] Join Room button clicked, code=" + roomCodeInput.text);
            controller.JoinRoom(roomCodeInput.text);
        }

        private void OnCopyRoomCodeClick()
        {
            if (controller.CurrentRoom != null && !string.IsNullOrEmpty(controller.CurrentRoom.RoomCode))
            {
                GUIUtility.systemCopyBuffer = controller.CurrentRoom.RoomCode;
                AppendLog("Room code copied: " + controller.CurrentRoom.RoomCode);
            }
        }

        private void OnLeaveRoomClick()
        {
            Debug.Log("[LobbyUI] Leave Room button clicked");
            // Immediately clear UI
            roomInfoText.text = "Left room.\nReconnect / Create Room.";
            connectionText.text = "Status: Disconnected";
            connectionText.color = Color.white;
            readyToggle.isOn = false;
            controller.Disconnect();
            // Delay then reconnect so user can join another room
            StartCoroutine(ReconnectAfterDelay());
        }

        private System.Collections.IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSeconds(0.3f);
            if (controller.ConnectionStatus != ConnectionStatus.Connected)
                controller.Connect(nicknameInput.text);
        }

        private void OnDisconnectClick()
        {
            Debug.Log("[LobbyUI] Disconnect button clicked");
            controller.Disconnect();
            connectionText.text = "Status: Disconnected";
            connectionText.color = Color.white;
            roomInfoText.text = "Disconnected.\nConnect, then Create Room.";
            AppendLog("Disconnected");
        }

        private void HandleConnectionChanged(ConnectionStatus status)
        {
            string[] statusNames = { "Disconnected", "Connecting", "Connected", "Reconnecting" };
            string cnName = ((int)status < statusNames.Length) ? statusNames[(int)status] : status.ToString();
            connectionText.text = "Status: " + cnName;
            connectionText.color = status == ConnectionStatus.Connected
                ? new Color(0.3f, 0.9f, 0.4f)
                : (status == ConnectionStatus.Connecting ? new Color(1f, 0.8f, 0.2f) : Color.white);
            AppendLog("Connection -> " + cnName);
        }

        private void HandleRoomUpdated(RoomSnapshot _)
        {
            roomInfoText.text = controller.BuildRoomSummary();
            AppendLog("Room updated.");
        }

        private void HandleRoomStarted(string roomCode)
        {
            gameActionPanel.SetActive(true);

            var gw = GetGw();
            if (gw != null && long.TryParse(gw.LocalPlayerId, out var pid))
                myPlayerId = pid;

            // Subscribe to game state changes
            if (gw != null)
                gw.GameStateChanged += UpdateTurnDisplay;

            UpdateTurnDisplay();
            AppendLog("Game started! Your ID: " + myPlayerId);
        }

        private void UpdateTurnDisplay()
        {
            var gw = GetGw();
            if (gw == null) return;
            bool myTurn = (myPlayerId == gw.CurrentTurnPlayerId);
            turnInfoText.text = myTurn
                ? $"YOUR TURN! (Player {myPlayerId})"
                : $"Waiting... Current: Player {gw.CurrentTurnPlayerId} (You: {myPlayerId})";
            turnInfoText.color = myTurn ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.8f, 0.3f);
        }

        private Cabo.Client.Network.ProtoGateway GetGw()
        {
            return controller.GetGateway<Cabo.Client.Network.ProtoGateway>();
        }

        private void AddGameActionButtons(Transform parent, Font font)
        {
            // Turn info text
            turnInfoText = CreateText(parent, "TurnInfo", "Waiting for turn...", font, 16, Vector2.zero, new Vector2(500, 30));
            turnInfoText.rectTransform.anchoredPosition = new Vector2(0, -5);
            turnInfoText.alignment = TextAnchor.MiddleCenter;
            turnInfoText.color = new Color(1f, 0.9f, 0.3f);
            turnInfoText.raycastTarget = false;

            float y = -35;
            var gw = GetGw();
            CreateButton(parent, font, "Draw", new Vector2(-280, y),
                new Vector2(100, 30), new Color(0.15f, 0.5f, 0.6f), () => gw?.DrawCard());
            CreateButton(parent, font, "Discard", new Vector2(-170, y),
                new Vector2(80, 30), new Color(0.3f, 0.3f, 0.3f), () => gw?.DiscardDrawn());
            CreateButton(parent, font, "Swap0", new Vector2(-85, y),
                new Vector2(55, 30), new Color(0.4f, 0.35f, 0.15f), () => gw?.ReplaceWithDrawn(0));
            CreateButton(parent, font, "Swap1", new Vector2(-25, y),
                new Vector2(55, 30), new Color(0.4f, 0.35f, 0.15f), () => gw?.ReplaceWithDrawn(1));
            CreateButton(parent, font, "Swap2", new Vector2(35, y),
                new Vector2(55, 30), new Color(0.4f, 0.35f, 0.15f), () => gw?.ReplaceWithDrawn(2));
            CreateButton(parent, font, "Swap3", new Vector2(95, y),
                new Vector2(55, 30), new Color(0.4f, 0.35f, 0.15f), () => gw?.ReplaceWithDrawn(3));
            CreateButton(parent, font, "Take Disc", new Vector2(160, y),
                new Vector2(80, 30), new Color(0.3f, 0.35f, 0.5f), () => gw?.TakeFromDiscard(0));
            CreateButton(parent, font, "Cabo!", new Vector2(260, y),
                new Vector2(80, 30), new Color(0.5f, 0.15f, 0.2f), () => gw?.CallSteady());
        }

        private void HandleError(string message)
        {
            AppendLog("Error: " + message);
        }

        private void AppendLog(string message)
        {
            var line = "- " + message;
            if (string.IsNullOrEmpty(eventLogText.text))
            {
                eventLogText.text = line;
                return;
            }

            eventLogText.text = line + "\n" + eventLogText.text;
            if (eventLogText.text.Length > 1400)
            {
                eventLogText.text = eventLogText.text.Substring(0, 1400);
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string textValue, Font font, int fontSize, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = textValue;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static Font ResolveFont()
        {
            // Use the project's Chinese font helper
            var chFont = FontHelper.GetChineseFont();
            if (chFont != null) return chFont;

            // Fallbacks
            try
            {
                var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtin != null) return builtin;
            }
            catch { }
            try
            {
                return Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            catch { }
            return null;
        }

        private static InputField CreateInput(Transform parent, string name, Font font, string placeholder, Vector2 pos, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            root.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.3f);

            var text = CreateText(root.transform, "Text", string.Empty, font, 20, Vector2.zero, new Vector2(size.x - 20, size.y - 10));
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.9f, 0.95f, 1f);

            var hint = CreateText(root.transform, "Placeholder", placeholder, font, 18, Vector2.zero, new Vector2(size.x - 20, size.y - 10));
            hint.alignment = TextAnchor.MiddleLeft;
            hint.color = new Color(0.7f, 0.78f, 0.84f);

            var input = root.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.text = placeholder;
            return input;
        }

        private static void CreateButton(Transform parent, Font font, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            CreateButton(parent, font, label, pos, new Vector2(150, 40), new Color(0.2f, 0.5f, 0.58f), onClick);
        }

        private static void CreateButton(Transform parent, Font font, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var image = go.GetComponent<Image>();
            image.color = color;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txt = CreateText(go.transform, "Text", label, font, 14, Vector2.zero, new Vector2(size.x - 16, size.y - 8));
            txt.alignment = TextAnchor.MiddleCenter;
        }

        private static Toggle CreateToggle(Transform parent, Font font, string label, Vector2 pos)
        {
            var root = new GameObject("Toggle_" + label, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 42);
            rect.anchoredPosition = pos;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(30, 30);
            bgRect.anchoredPosition = new Vector2(-62, 0);
            bg.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.4f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            var ckRect = check.GetComponent<RectTransform>();
            ckRect.sizeDelta = new Vector2(18, 18);
            ckRect.anchoredPosition = Vector2.zero;
            check.GetComponent<Image>().color = new Color(0.3f, 0.9f, 0.45f);

            var labelText = CreateText(root.transform, "Label", label, font, 20, new Vector2(28, 0), new Vector2(120, 30));
            labelText.alignment = TextAnchor.MiddleLeft;

            var toggle = root.AddComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.isOn = false;
            return toggle;
        }
    }
}
