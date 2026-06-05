using Cabo.Client.Network;
using Cabo.Client.Room;
using Cabo.Client.Runtime;
using UnityEngine;

namespace Cabo.Client.Game
{
    /// <summary>
    /// Placed in GameScene. Finds cross-scene ProtoGateway and GameClientController
    /// (on ClientAppBootstrap's DontDestroyOnLoad GameObject) and wires them to GameTableUIToolkit.
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        private static GameSceneController _instance;

        public ProtoGateway Gateway { get; private set; }
        public GameClientController GameCtrl { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[GameSceneController] ⚠️ Duplicate instance detected on '{gameObject.name}'. Destroying to prevent conflicts.");
                Debug.LogWarning($"[GameSceneController] ⚠️ Existing instance: '{_instance.gameObject.name}', Current instance: '{gameObject.name}'");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Debug.Log($"[GameSceneController] ✅ Singleton instance set on '{gameObject.name}'");
        }

        private void Start()
        {
            if (_instance != this)
            {
                Debug.LogWarning("[GameSceneController] ⚠️ This is not the singleton instance, skipping Start()");
                return;
            }

            Debug.Log("[GameSceneController] ===== Start() 开始 =====");
            Debug.Log($"[GameSceneController] PendingGameStart status: {(GameSceneBootstrap.PendingGameStart != null ? "AVAILABLE" : "NULL")}");

            var bootstrap = FindObjectOfType<ClientAppBootstrap>();
            Debug.Log($"[GameSceneController] FindObjectOfType<ClientAppBootstrap>: {(bootstrap != null ? "找到" : "未找到")}");

            if (bootstrap == null)
            {
                Debug.LogError("[GameSceneController] ❌ ClientAppBootstrap not found!");
                Debug.LogError("[GameSceneController] ❌ Cannot run GameScene standalone. Please start from MainMenu → Lobby → Start Game.");
                return;
            }

            Debug.Log($"[GameSceneController] ClientAppBootstrap 找到: {bootstrap.name}");

            // ProtoGateway is not a MonoBehaviour - access via RoomClientController
            var roomCtrl = bootstrap.GetComponent<RoomClientController>();
            Debug.Log($"[GameSceneController] RoomClientController: {(roomCtrl != null ? "找到" : "未找到")}");

            if (roomCtrl != null)
            {
                Gateway = roomCtrl.GetGateway<ProtoGateway>();
                Debug.Log($"[GameSceneController] ProtoGateway from RoomController: {(Gateway != null ? "找到" : "未找到")}");
            }

            GameCtrl = bootstrap.GetComponent<GameClientController>();
            Debug.Log($"[GameSceneController] GameClientController: {(GameCtrl != null ? "找到" : "未找到")}");

            if (Gateway == null)
                Debug.LogError("[GameSceneController] ❌ ProtoGateway not found (room controller or gateway missing)!");
            if (GameCtrl == null)
                Debug.LogError("[GameSceneController] ❌ GameClientController missing on bootstrap!");

            // Find and initialize UI Toolkit version (uGUI version has been removed)
            var tableUIToolkit = FindObjectOfType<GameTableUIToolkit>();
            if (tableUIToolkit != null)
            {
                Debug.Log($"[GameSceneController] Found GameTableUIToolkit, about to initialize...");
                tableUIToolkit.Initialize(Gateway, GameCtrl);
                Debug.Log($"[GameSceneController] GameTableUIToolkit initialized (Gateway: {(Gateway != null ? "✅" : "❌ null")})");
            }
            else
            {
                Debug.LogError("[GameSceneController] ❌ GameTableUIToolkit not found in scene!");
            }

            Debug.Log("[GameSceneController] ===== Start() 完成 =====");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
