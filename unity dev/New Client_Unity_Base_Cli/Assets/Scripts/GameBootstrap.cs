using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cabo.Client.UI;

namespace Cabo.Client
{
    /// <summary>
    /// Self-initializing entry point. No scene setup required.
    /// Drop this script on any GameObject or let it auto-create via RuntimeInitializeOnLoadMethod.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 8888;
        [SerializeField] private string gameSceneName = "CaboGameScene";
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private VisualTreeAsset uiAsset;
        [SerializeField] private StyleSheet uiStyleSheet;

        NetworkGateway _gateway;
        GameFlow _flow;
        UIManager _ui;
        UIDocument _uiDoc;
        Scene _lobbyScene;
        bool _gameSceneActivated;
        bool _loadingGameScene;

        static GameBootstrap _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (_instance != null) return;
            var go = new GameObject("GameBootstrap");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameBootstrap>();
        }

        void Start()
        {
            if (_instance == null) _instance = this;
            _lobbyScene = SceneManager.GetActiveScene();

            // Create UIDocument
            var panelGO = new GameObject("UIDocument");
            panelGO.transform.SetParent(transform);
            _uiDoc = panelGO.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings != null ? panelSettings : LoadPanelSettings();
            _uiDoc.visualTreeAsset = uiAsset != null ? uiAsset : LoadVisualTree();
            var styleSheet = uiStyleSheet != null ? uiStyleSheet : LoadStyleSheet();
            if (styleSheet != null)
                _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            _gateway = new NetworkGateway();
            _flow = new GameFlow(_gateway);

            _ui = gameObject.AddComponent<UIManager>();
            _ui.uiDocument = _uiDoc;
            _ui.Initialize(_flow);

            _flow.Connect(serverHost, serverPort);

            Debug.Log($"[GameBootstrap] Started - {serverHost}:{serverPort}");
        }

        PanelSettings LoadPanelSettings()
        {
#if UNITY_EDITOR
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/GamePanelSettings.asset");
            if (settings != null)
            {
                EnsureThemeStyleSheet(settings);
                return settings;
            }
#endif
            var fallback = ScriptableObject.CreateInstance<PanelSettings>();
            fallback.name = "RuntimePanelSettings";
#if UNITY_EDITOR
            EnsureThemeStyleSheet(fallback);
#endif
            return fallback;
        }

#if UNITY_EDITOR
        void EnsureThemeStyleSheet(PanelSettings settings)
        {
            if (settings == null || settings.themeStyleSheet != null) return;
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/RuntimeTheme.tss");
            if (theme != null)
                settings.themeStyleSheet = theme;
        }
#endif

        VisualTreeAsset LoadVisualTree()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/GameScreen.uxml");
#else
            return null;
#endif
        }

        StyleSheet LoadStyleSheet()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/GameScreen.uss");
#else
            return null;
#endif
        }

        void Update()
        {
            _flow?.Tick();
            HandleSceneTransition();
        }

        void OnDestroy()
        {
            _flow?.Dispose();
        }

        void HandleSceneTransition()
        {
            if (_flow == null || _gameSceneActivated || _loadingGameScene) return;
            if (_flow.State.Phase != GamePhase.Playing) return;

            _gameSceneActivated = true;

            if (SceneManager.GetActiveScene().name == gameSceneName)
                return;

            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                StartCoroutine(LoadGameScene());
            }
            else
            {
                var gameScene = SceneManager.CreateScene(gameSceneName);
                SceneManager.SetActiveScene(gameScene);
                if (_lobbyScene.IsValid() && _lobbyScene.isLoaded)
                    SceneManager.UnloadSceneAsync(_lobbyScene);
                Debug.LogWarning($"[GameBootstrap] Created runtime game scene '{gameSceneName}'. Add it to Build Settings for asset-based loading.");
            }
        }

        IEnumerator LoadGameScene()
        {
            _loadingGameScene = true;
            var op = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
                yield return null;
            _loadingGameScene = false;
            Debug.Log($"[GameBootstrap] Loaded game scene '{gameSceneName}'");
        }
    }
}
