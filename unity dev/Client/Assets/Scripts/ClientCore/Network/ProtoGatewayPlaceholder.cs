using UnityEngine;

namespace Cabo.Client.Network
{
    public sealed class ProtoGatewayPlaceholder : IBackendGateway
    {
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public string LocalPlayerId { get; private set; } = string.Empty;
        public string SessionToken { get; private set; } = string.Empty;

        public event System.Action<ConnectionStatus> ConnectionStatusChanged;
#pragma warning disable CS0067 // Events are placeholder stubs for future server integration
        public event System.Action<RoomSnapshot> RoomUpdated;
        public event System.Action<string> RoomStarted;
#pragma warning restore CS0067
        public event System.Action<BackendError> OperationFailed;

        public void Connect(string nickname)
        {
            Status = ConnectionStatus.Connecting;
            ConnectionStatusChanged?.Invoke(Status);
            RaiseNotReady("Connect");
            Status = ConnectionStatus.Disconnected;
            ConnectionStatusChanged?.Invoke(Status);
        }

        public void Disconnect()
        {
            Status = ConnectionStatus.Disconnected;
            ConnectionStatusChanged?.Invoke(Status);
        }

        public void CreateRoom(int maxPlayers)
        {
            RaiseNotReady("CreateRoom");
        }

        public void JoinRoom(string roomCode)
        {
            RaiseNotReady("JoinRoom");
        }

        public void SetReady(bool isReady)
        {
            RaiseNotReady("SetReady");
        }

        public void StartGame()
        {
            RaiseNotReady("StartGame");
        }

        private void RaiseNotReady(string op)
        {
            var error = new BackendError
            {
                Code = 9001,
                Message = "Proto gateway not wired yet: " + op + ". Use Mock mode to test M1/M2 flow."
            };

            Debug.LogWarning("[ProtoGatewayPlaceholder] " + error.Message);
            OperationFailed?.Invoke(error);
        }
    }
}
