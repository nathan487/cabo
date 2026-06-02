using System;

namespace Cabo.Client.Network
{
    public interface IBackendGateway
    {
        ConnectionStatus Status { get; }
        string LocalPlayerId { get; }
        string SessionToken { get; }

        event Action<ConnectionStatus> ConnectionStatusChanged;
        event Action<RoomSnapshot> RoomUpdated;
        event Action<string> RoomStarted;
        event Action<BackendError> OperationFailed;

        void Connect(string nickname);
        void Disconnect();
        void CreateRoom(int maxPlayers);
        void JoinRoom(string roomCode);
        void SetReady(bool isReady);
        void StartGame();
    }
}
