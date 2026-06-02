using Cabo.Client.Room;
using UnityEngine;

namespace Cabo.Client.Runtime
{
    public sealed class ClientAppBootstrap : MonoBehaviour
    {
        [SerializeField] private RoomClientController.BackendMode backendMode = RoomClientController.BackendMode.Mock;
        [SerializeField] private bool autoCreateRoomUi = true;

        // Auto-bootstrap is disabled for MVP offline testing (hot-seat mode).
        // Re-enable by uncommenting the attribute below when server networking (M1) is ready.
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            // Stub — no-op.
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            var roomController = GetComponent<RoomClientController>();
            if (roomController == null)
            {
                roomController = gameObject.AddComponent<RoomClientController>();
            }

            roomController.Initialize(backendMode);

            // CaboSceneSetup removed — UI is now built by SceneBuilder (Editor/Tools/Build Game Scene)
            if (autoCreateRoomUi && GetComponent<Cabo.Client.UI.LobbyRoomDemoUI>() == null)
            {
                gameObject.AddComponent<Cabo.Client.UI.LobbyRoomDemoUI>();
            }
        }
    }
}
