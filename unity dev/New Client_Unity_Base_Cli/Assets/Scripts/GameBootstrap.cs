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
            panelGO.SetActive(false);
            panelGO.transform.SetParent(transform);
            _uiDoc = panelGO.AddComponent<UIDocument>();
            _uiDoc.panelSettings = panelSettings != null ? panelSettings : LoadPanelSettings();
            _uiDoc.visualTreeAsset = uiAsset != null ? uiAsset : LoadVisualTree();
            var styleSheet = uiStyleSheet != null ? uiStyleSheet : LoadStyleSheet();
            panelGO.SetActive(true);
            if (styleSheet != null)
            {
                _uiDoc.rootVisualElement.styleSheets.Add(styleSheet);
                Debug.Log($"[GameBootstrap] Applied UI stylesheet '{styleSheet.name}', count={_uiDoc.rootVisualElement.styleSheets.count}.");
            }

            _gateway = new NetworkGateway();
            _flow = new GameFlow(_gateway);

            _ui = gameObject.AddComponent<UIManager>();
            _ui.uiDocument = _uiDoc;
            _ui.Initialize(_flow);

            Debug.Log($"[GameBootstrap] Started - connect from the home screen ({serverHost}:{serverPort} default)");
        }

        PanelSettings LoadPanelSettings()
        {
#if UNITY_EDITOR
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/GamePanelSettings.asset");
            if (settings != null)
            {
                EnsureThemeStyleSheet(settings);
                EnsureTextSettings(settings);
                return settings;
            }
#endif
            var resourceSettings = Resources.Load<PanelSettings>("GamePanelSettings");
            if (resourceSettings != null)
            {
                EnsureThemeStyleSheet(resourceSettings);
                EnsureTextSettings(resourceSettings);
                Debug.Log("[GameBootstrap] Loaded GamePanelSettings from Resources.");
                return resourceSettings;
            }

            var fallback = ScriptableObject.CreateInstance<PanelSettings>();
            fallback.name = "RuntimePanelSettings";
            EnsureThemeStyleSheet(fallback);
            EnsureTextSettings(fallback);
            return fallback;
        }

        void EnsureThemeStyleSheet(PanelSettings settings)
        {
            if (settings == null || settings.themeStyleSheet != null) return;
#if UNITY_EDITOR
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/RuntimeTheme.tss");
            if (theme != null)
            {
                settings.themeStyleSheet = theme;
                return;
            }
#endif
            var runtimeTheme = Resources.Load<ThemeStyleSheet>("RuntimeTheme");
            if (runtimeTheme != null)
            {
                settings.themeStyleSheet = runtimeTheme;
                Debug.Log("[GameBootstrap] Loaded RuntimeTheme from Resources.");
            }
            else
                Debug.LogWarning("[GameBootstrap] RuntimeTheme.tss not found in Resources; UI Toolkit may not render in builds.");
        }

        void EnsureTextSettings(PanelSettings settings)
        {
            if (settings == null || settings.textSettings != null)
                return;

            var textSettings = Resources.Load<PanelTextSettings>("CaboPanelTextSettings");
            if (textSettings != null)
            {
                settings.textSettings = textSettings;
                Debug.Log("[GameBootstrap] Loaded CaboPanelTextSettings from Resources.");
            }
            else
                Debug.LogWarning("[GameBootstrap] CaboPanelTextSettings not found in Resources; non-ASCII UI text may not render in builds.");
        }

        VisualTreeAsset LoadVisualTree()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/GameScreen.uxml");
#else
            return Resources.Load<VisualTreeAsset>("GameScreen");
#endif
        }

        StyleSheet LoadStyleSheet()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/GameScreen.uss");
#else
            var style = Resources.Load<StyleSheet>("GameScreen");
            if (style == null)
                Debug.LogWarning("[GameBootstrap] GameScreen.uss not found in Resources; runtime UI will use fallback styling.");
            else
                Debug.Log("[GameBootstrap] Loaded GameScreen.uss from Resources.");
            return style;
#endif
        }

        void Update()
        {
            _flow?.Tick();
            _ui?.GameTablePanel?.Tick();
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
