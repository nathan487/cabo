using System;
using System.Collections.Generic;

namespace Cabo.Client.Network
{
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    public sealed class BackendError
    {
        public int Code;
        public string Message = string.Empty;
    }

    public sealed class PlayerPublicInfoModel
    {
        public string PlayerId = string.Empty;
        public string Nickname = string.Empty;
        public int SeatId;
        public bool IsReady;
        public bool IsHost;
        public bool IsConnected;
        public int TotalScore;
    }

    public sealed class RoomSnapshot
    {
        public long RoomId;
        public string RoomCode = string.Empty;
        public int MaxPlayers;
        public string HostPlayerId = string.Empty;
        public List<PlayerPublicInfoModel> Players = new List<PlayerPublicInfoModel>();
        public bool InGame;
    }

    public sealed class ConnectResult
    {
        public bool Success;
        public string PlayerId = string.Empty;
        public string SessionToken = string.Empty;
        public BackendError Error = new BackendError();
    }

    public sealed class RoomOpResult
    {
        public bool Success;
        public RoomSnapshot Snapshot = new RoomSnapshot();
        public BackendError Error = new BackendError();
    }
}
