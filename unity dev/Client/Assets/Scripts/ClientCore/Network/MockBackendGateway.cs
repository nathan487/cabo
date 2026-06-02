using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cabo.Client.Network
{
    public sealed class MockBackendGateway : IBackendGateway
    {
        private sealed class MockRoom
        {
            public long RoomId;
            public string RoomCode = string.Empty;
            public int MaxPlayers;
            public string HostPlayerId = string.Empty;
            public readonly List<PlayerPublicInfoModel> Players = new List<PlayerPublicInfoModel>();
            public bool InGame;
        }

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public string LocalPlayerId { get; private set; } = string.Empty;
        public string SessionToken { get; private set; } = string.Empty;

        public event Action<ConnectionStatus> ConnectionStatusChanged;
        public event Action<RoomSnapshot> RoomUpdated;
        public event Action<string> RoomStarted;
        public event Action<BackendError> OperationFailed;

        private MockRoom currentRoom;
        private string nickname = "Player";

        public void Connect(string nextNickname)
        {
            if (Status == ConnectionStatus.Connected)
            {
                return;
            }

            Status = ConnectionStatus.Connecting;
            ConnectionStatusChanged?.Invoke(Status);

            nickname = string.IsNullOrWhiteSpace(nextNickname) ? "Player" : nextNickname.Trim();
            LocalPlayerId = "P-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            SessionToken = Guid.NewGuid().ToString("N");

            Status = ConnectionStatus.Connected;
            ConnectionStatusChanged?.Invoke(Status);
            Debug.Log("[MockBackendGateway] Connected as " + nickname + " " + LocalPlayerId);
        }

        public void Disconnect()
        {
            currentRoom = null;
            Status = ConnectionStatus.Disconnected;
            ConnectionStatusChanged?.Invoke(Status);
        }

        public void CreateRoom(int maxPlayers)
        {
            if (!EnsureConnected())
            {
                return;
            }

            currentRoom = new MockRoom
            {
                RoomId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                RoomCode = GenerateRoomCode(),
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 6),
                HostPlayerId = LocalPlayerId
            };

            currentRoom.Players.Add(new PlayerPublicInfoModel
            {
                PlayerId = LocalPlayerId,
                Nickname = nickname,
                SeatId = 0,
                IsReady = false,
                IsHost = true,
                IsConnected = true,
                TotalScore = 0
            });

            EmitRoomSnapshot();
        }

        public void JoinRoom(string roomCode)
        {
            if (!EnsureConnected())
            {
                return;
            }

            if (currentRoom == null)
            {
                currentRoom = new MockRoom
                {
                    RoomId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    RoomCode = string.IsNullOrWhiteSpace(roomCode) ? GenerateRoomCode() : roomCode.Trim().ToUpperInvariant(),
                    MaxPlayers = 4,
                    HostPlayerId = "BOT-HOST"
                };

                currentRoom.Players.Add(new PlayerPublicInfoModel
                {
                    PlayerId = currentRoom.HostPlayerId,
                    Nickname = "HostBot",
                    SeatId = 0,
                    IsReady = true,
                    IsHost = true,
                    IsConnected = true,
                    TotalScore = 0
                });
            }

            var seat = currentRoom.Players.Count;
            if (seat >= currentRoom.MaxPlayers)
            {
                RaiseError(1201, "Room is full.");
                return;
            }

            var alreadyInRoom = currentRoom.Players.Exists(p => p.PlayerId == LocalPlayerId);
            if (!alreadyInRoom)
            {
                currentRoom.Players.Add(new PlayerPublicInfoModel
                {
                    PlayerId = LocalPlayerId,
                    Nickname = nickname,
                    SeatId = seat,
                    IsReady = false,
                    IsHost = false,
                    IsConnected = true,
                    TotalScore = 0
                });
            }

            EmitRoomSnapshot();
        }

        public void SetReady(bool isReady)
        {
            if (!EnsureInRoom())
            {
                return;
            }

            var player = currentRoom.Players.Find(p => p.PlayerId == LocalPlayerId);
            if (player == null)
            {
                RaiseError(1202, "Player not found in room.");
                return;
            }

            player.IsReady = isReady;
            EmitRoomSnapshot();
        }

        public void StartGame()
        {
            if (!EnsureInRoom())
            {
                return;
            }

            var local = currentRoom.Players.Find(p => p.PlayerId == LocalPlayerId);
            if (local == null)
            {
                RaiseError(1203, "Player missing.");
                return;
            }

            if (!local.IsHost)
            {
                RaiseError(1204, "Only host can start game.");
                return;
            }

            var enoughPlayers = currentRoom.Players.Count >= 2;
            if (!enoughPlayers)
            {
                RaiseError(1205, "Need at least 2 players.");
                return;
            }

            for (var i = 0; i < currentRoom.Players.Count; i++)
            {
                if (!currentRoom.Players[i].IsReady)
                {
                    RaiseError(1206, "All players must be ready.");
                    return;
                }
            }

            currentRoom.InGame = true;
            EmitRoomSnapshot();
            RoomStarted?.Invoke(currentRoom.RoomCode);
        }

        private bool EnsureConnected()
        {
            if (Status == ConnectionStatus.Connected)
            {
                return true;
            }

            RaiseError(1101, "Not connected.");
            return false;
        }

        private bool EnsureInRoom()
        {
            if (!EnsureConnected())
            {
                return false;
            }

            if (currentRoom != null)
            {
                return true;
            }

            RaiseError(1102, "Not in room.");
            return false;
        }

        private void EmitRoomSnapshot()
        {
            if (currentRoom == null)
            {
                return;
            }

            var snapshot = new RoomSnapshot
            {
                RoomId = currentRoom.RoomId,
                RoomCode = currentRoom.RoomCode,
                MaxPlayers = currentRoom.MaxPlayers,
                HostPlayerId = currentRoom.HostPlayerId,
                InGame = currentRoom.InGame,
                Players = new List<PlayerPublicInfoModel>()
            };

            for (var i = 0; i < currentRoom.Players.Count; i++)
            {
                var p = currentRoom.Players[i];
                snapshot.Players.Add(new PlayerPublicInfoModel
                {
                    PlayerId = p.PlayerId,
                    Nickname = p.Nickname,
                    SeatId = p.SeatId,
                    IsReady = p.IsReady,
                    IsHost = p.IsHost,
                    IsConnected = p.IsConnected,
                    TotalScore = p.TotalScore
                });
            }

            RoomUpdated?.Invoke(snapshot);
        }

        private void RaiseError(int code, string message)
        {
            OperationFailed?.Invoke(new BackendError { Code = code, Message = message });
        }

        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new System.Random();
            var buff = new char[6];
            for (var i = 0; i < buff.Length; i++)
            {
                buff[i] = chars[random.Next(chars.Length)];
            }

            return new string(buff);
        }
    }
}
