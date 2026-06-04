using Cabo.Client.Network;
using Cabo.Client.Room;
using UnityEngine;

namespace Cabo.Client.Runtime
{
    public sealed class ClientAppBootstrap : MonoBehaviour
    {
        [SerializeField] private RoomClientController.BackendMode backendMode = RoomClientController.BackendMode.Mock;
        [SerializeField] private bool autoCreateRoomUi = true;

        // Proto mode settings — server connection
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 8888;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            var roomController = GetComponent<RoomClientController>();
            if (roomController == null)
            {
                roomController = gameObject.AddComponent<RoomClientController>();
            }

            roomController.Initialize(backendMode);

            // If Proto mode, inject the real ProtoGateway (replaces the placeholder
            // created by Initialize() for ProtoPlaceholder mode)
            if (backendMode == RoomClientController.BackendMode.ProtoPlaceholder)
            {
                var realGateway = new ProtoGateway(serverHost, serverPort, this);
                roomController.SetProtoGateway(realGateway);
                Debug.Log($"[ClientAppBootstrap] ProtoGateway injected -> {serverHost}:{serverPort}");
            }

            // Add game controller for cross-scene game event forwarding
            if (GetComponent<Cabo.Client.Game.GameClientController>() == null)
            {
                gameObject.AddComponent<Cabo.Client.Game.GameClientController>();
            }

            if (autoCreateRoomUi && GetComponent<Cabo.Client.UI.LobbyRoomDemoUI>() == null)
            {
                gameObject.AddComponent<Cabo.Client.UI.LobbyRoomDemoUI>();
            }
        }
    }
}
