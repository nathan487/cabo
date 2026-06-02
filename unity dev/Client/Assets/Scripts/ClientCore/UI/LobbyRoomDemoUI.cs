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

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            var root = CreatePanel(canvasObj.transform, "Root", new Vector2(980, 620), new Color(0.08f, 0.12f, 0.18f, 0.93f));
            var font = ResolveFont();

            titleText = CreateText(root.transform, "Title", "Cabo Client M1/M2 Skeleton", font, 34, new Vector2(0, 260), new Vector2(900, 48));
            connectionText = CreateText(root.transform, "Connection", "Connection: Disconnected", font, 22, new Vector2(-270, 208), new Vector2(520, 36));

            CreateText(root.transform, "NicknameLabel", "Nickname", font, 20, new Vector2(-420, 148), new Vector2(180, 32));
            nicknameInput = CreateInput(root.transform, "NicknameInput", font, "PlayerA", new Vector2(-250, 148), new Vector2(280, 40));

            CreateText(root.transform, "RoomCodeLabel", "Room Code", font, 20, new Vector2(30, 148), new Vector2(180, 32));
            roomCodeInput = CreateInput(root.transform, "RoomCodeInput", font, "ABC123", new Vector2(200, 148), new Vector2(260, 40));

            CreateButton(root.transform, font, "Connect", new Vector2(-380, 86), OnConnectClick);
            CreateButton(root.transform, font, "Create Room", new Vector2(-170, 86), () => controller.CreateRoom(4));
            CreateButton(root.transform, font, "Join Room", new Vector2(40, 86), () => controller.JoinRoom(roomCodeInput.text));
            CreateButton(root.transform, font, "Start Game", new Vector2(250, 86), controller.StartGame);

            readyToggle = CreateToggle(root.transform, font, "Ready", new Vector2(430, 86));
            readyToggle.onValueChanged.AddListener(value => controller.SetReady(value));

            roomInfoText = CreateText(root.transform, "RoomInfo", "No room joined.", font, 20, new Vector2(0, -40), new Vector2(900, 260));
            roomInfoText.alignment = TextAnchor.UpperLeft;

            eventLogText = CreateText(root.transform, "EventLog", "", font, 18, new Vector2(0, -252), new Vector2(900, 120));
            eventLogText.alignment = TextAnchor.UpperLeft;
            eventLogText.color = new Color(0.72f, 0.91f, 0.95f);

            AppendLog("UI initialized. Connect first.");
        }

        private void OnConnectClick()
        {
            var nick = string.IsNullOrWhiteSpace(nicknameInput.text) ? "PlayerA" : nicknameInput.text.Trim();
            controller.Connect(nick);
        }

        private void HandleConnectionChanged(ConnectionStatus status)
        {
            connectionText.text = "Connection: " + status;
            AppendLog("Connection -> " + status);
        }

        private void HandleRoomUpdated(RoomSnapshot _)
        {
            roomInfoText.text = controller.BuildRoomSummary();
            AppendLog("Room updated.");
        }

        private void HandleRoomStarted(string roomCode)
        {
            AppendLog("Room " + roomCode + " started. Next: bind GameScene transition.");
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
            try
            {
                var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtin != null)
                {
                    return builtin;
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                return Font.CreateDynamicFontFromOSFont("Arial", 16);
            }
            catch
            {
                return null;
            }
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
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(190, 46);
            rect.anchoredPosition = pos;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.5f, 0.58f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txt = CreateText(go.transform, "Text", label, font, 19, Vector2.zero, new Vector2(180, 42));
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
