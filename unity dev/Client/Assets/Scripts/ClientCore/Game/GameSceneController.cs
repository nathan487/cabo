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
        public ProtoGateway Gateway { get; private set; }
        public GameClientController GameCtrl { get; private set; }

        private void Start()
        {
            Debug.Log("[GameSceneController] ===== Start() 开始 =====");

            var bootstrap = FindObjectOfType<ClientAppBootstrap>();
            Debug.Log($"[GameSceneController] FindObjectOfType<ClientAppBootstrap>: {(bootstrap != null ? "找到" : "未找到")}");

            if (bootstrap == null)
            {
                Debug.LogWarning("[GameSceneController] ClientAppBootstrap not found! GameScene 独立运行模式 - 仅测试 UI 显示");

                // 独立运行模式：直接初始化 UI（仅用于测试）
                var uiToolkit = FindObjectOfType<GameTableUIToolkit>();
                if (uiToolkit != null)
                {
                    Debug.Log("[GameSceneController] 独立模式：尝试初始化 GameTableUIToolkit");
                    // 传入 null 参数，UI 会显示但不会连接网络
                    uiToolkit.Initialize(null, null);
                }
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
                tableUIToolkit.Initialize(Gateway, GameCtrl);
                Debug.Log($"[GameSceneController] GameTableUIToolkit initialized (Gateway: {(Gateway != null ? "✅" : "❌ null")})");
            }
            else
            {
                Debug.LogError("[GameSceneController] ❌ GameTableUIToolkit not found in scene!");
            }
        }
    }
}
