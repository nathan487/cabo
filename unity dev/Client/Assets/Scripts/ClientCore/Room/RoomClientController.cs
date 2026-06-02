using System;
using System.Text;
using Cabo.Client.Network;
using UnityEngine;

namespace Cabo.Client.Room
{
    public sealed class RoomClientController : MonoBehaviour
    {
        public enum BackendMode
        {
            Mock,
            ProtoPlaceholder
        }

        [SerializeField] private BackendMode backendMode = BackendMode.Mock;

        public ConnectionStatus ConnectionStatus { get; private set; } = ConnectionStatus.Disconnected;
        public RoomSnapshot CurrentRoom { get; private set; }
        public string LastErrorMessage { get; private set; } = string.Empty;

        public event Action<ConnectionStatus> ConnectionStatusChanged;
        public event Action<RoomSnapshot> RoomUpdated;
        public event Action<string> RoomStarted;
        public event Action<string> ErrorRaised;

        private IBackendGateway gateway;

        public void Initialize(BackendMode mode)
        {
            backendMode = mode;
            BuildGateway();
        }

        private void Awake()
        {
            BuildGateway();
        }

        private void BuildGateway()
        {
            if (gateway != null)
            {
                Unsubscribe(gateway);
            }

            gateway = backendMode == BackendMode.Mock
                ? (IBackendGateway)new MockBackendGateway()
                : new ProtoGatewayPlaceholder();

            Subscribe(gateway);
        }

        private void Subscribe(IBackendGateway target)
        {
            target.ConnectionStatusChanged += OnConnectionStatusChanged;
            target.RoomUpdated += OnRoomUpdated;
            target.RoomStarted += OnRoomStarted;
            target.OperationFailed += OnOperationFailed;
        }

        private void Unsubscribe(IBackendGateway target)
        {
            target.ConnectionStatusChanged -= OnConnectionStatusChanged;
            target.RoomUpdated -= OnRoomUpdated;
            target.RoomStarted -= OnRoomStarted;
            target.OperationFailed -= OnOperationFailed;
        }

        public void Connect(string nickname)
        {
            gateway.Connect(nickname);
        }

        public void Disconnect()
        {
            gateway.Disconnect();
        }

        public void CreateRoom(int maxPlayers)
        {
            gateway.CreateRoom(maxPlayers);
        }

        public void JoinRoom(string roomCode)
        {
            gateway.JoinRoom(roomCode);
        }

        public void SetReady(bool isReady)
        {
            gateway.SetReady(isReady);
        }

        public void StartGame()
        {
            gateway.StartGame();
        }

        public string BuildRoomSummary()
        {
            if (CurrentRoom == null)
            {
                return "No room joined.";
            }

            var sb = new StringBuilder();
            sb.Append("Room ").Append(CurrentRoom.RoomCode)
              .Append(" (").Append(CurrentRoom.Players.Count).Append("/").Append(CurrentRoom.MaxPlayers).Append(")");

            if (CurrentRoom.InGame)
            {
                sb.Append(" | IN_GAME");
            }

            for (var i = 0; i < CurrentRoom.Players.Count; i++)
            {
                var p = CurrentRoom.Players[i];
                sb.Append("\n[").Append(p.SeatId).Append("] ").Append(p.Nickname);
                if (p.IsHost)
                {
                    sb.Append(" (Host)");
                }

                sb.Append(" - ").Append(p.IsReady ? "Ready" : "NotReady");
                sb.Append(p.IsConnected ? " - Online" : " - Offline");
            }

            return sb.ToString();
        }

        private void OnDestroy()
        {
            if (gateway != null)
            {
                Unsubscribe(gateway);
            }
        }

        private void OnConnectionStatusChanged(ConnectionStatus status)
        {
            ConnectionStatus = status;
            ConnectionStatusChanged?.Invoke(status);
        }

        private void OnRoomUpdated(RoomSnapshot snapshot)
        {
            CurrentRoom = snapshot;
            RoomUpdated?.Invoke(snapshot);
        }

        private void OnRoomStarted(string roomCode)
        {
            RoomStarted?.Invoke(roomCode);
        }

        private void OnOperationFailed(BackendError error)
        {
            LastErrorMessage = error.Code + ": " + error.Message;
            ErrorRaised?.Invoke(LastErrorMessage);
        }
    }
}
