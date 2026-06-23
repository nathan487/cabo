using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Messages;
using Game.Room;
using Game.Game;
using Game.Common;
using Game.Sync;
using Cabo.Client.Network;
using UnityEngine;

namespace Cabo.Client
{
    /// <summary>
    /// Thin WebSocket + protobuf gateway. WebSocket message boundaries carry
    /// pure protobuf payloads; dispatch still drains on Unity's main thread.
    /// </summary>
    public class NetworkGateway : IDisposable
    {
        private WebSocketNetworkClient wsClient;
        private readonly Queue<ServerMessage> pendingMessages = new();

        private readonly MessageDispatcher dispatcher = new();

        private int isConnected;
        public bool IsConnected => Volatile.Read(ref isConnected) != 0;
        public long NextSeq => _nextSeq;
        public long LastServerSeq { get; private set; }
        private long _nextSeq = 1;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<ServerMessage> MessageReceived;
        public event Action<string> Error;

        /// <summary>Register a typed handler. Example: Register&lt;DrawCardRsp&gt;(OnDrawCard);</summary>
        public void Register<T>(Action<T> handler) where T : class => dispatcher.Register(handler);

        /// <summary>
        /// Drain every message received by the background socket task.
        /// Call from Unity's main thread before rendering or making UI decisions.
        /// </summary>
        public int DrainMessages(Action<ServerMessage> beforeDispatch = null)
        {
            List<ServerMessage> drained = null;
            lock (pendingMessages)
            {
                if (pendingMessages.Count > 0)
                {
                    drained = new List<ServerMessage>(pendingMessages.Count);
                    while (pendingMessages.Count > 0)
                        drained.Add(pendingMessages.Dequeue());
                }
            }

            if (drained == null) return 0;

            Debug.Log($"[NetworkGateway] Draining messages count={drained.Count}");

            foreach (var msg in drained)
            {
                if (msg.ServerSeq > LastServerSeq)
                    LastServerSeq = msg.ServerSeq;
                beforeDispatch?.Invoke(msg);
                dispatcher.Dispatch(msg);
                MessageReceived?.Invoke(msg);
            }

            return drained.Count;
        }

        public async Task ConnectAsync(string url)
        {
            try
            {
                if (wsClient != null)
                {
                    try { wsClient.Dispose(); } catch { }
                    wsClient = null;
                    SetConnected(false);
                }

                wsClient = new WebSocketNetworkClient(url);
                wsClient.Connected += () =>
                {
                    SetConnected(true);
                    Connected?.Invoke();
                };
                wsClient.Disconnected += () =>
                {
                    bool wasConnected = IsConnected;
                    SetConnected(false);
                    if (wasConnected)
                        Disconnected?.Invoke();
                };
                wsClient.DataReceived += OnDataReceived;
                wsClient.ErrorOccurred += error => Error?.Invoke(error);

                await wsClient.ConnectAsync();
                Debug.Log($"[NetworkGateway] Connect attempt finished for {url}");
            }
            catch (Exception ex)
            {
                Error?.Invoke($"Connect failed: {ex.Message}");
                Debug.LogError($"[NetworkGateway] {ex}");
            }
        }

        public void Disconnect()
        {
            bool wasConnected = IsConnected;

            var client = wsClient;
            wsClient = null;
            try { client?.Disconnect(); } catch { }

            if (wasConnected && IsConnected)
            {
                SetConnected(false);
                Disconnected?.Invoke();
            }
            else if (!wasConnected)
            {
                SetConnected(false);
            }
        }

        /// <summary>Send a ClientMessage. Sets seq automatically.</summary>
        public void Send(ClientMessage msg)
        {
            if (!IsConnected) return;
            msg.Seq = _nextSeq++;
            var payload = MessageCodec.EncodePayload(msg);
            try { wsClient?.Send(payload); }
            catch (Exception ex) { Error?.Invoke($"Send failed: {ex.Message}"); Disconnect(); }
        }

        /// <summary>Quick send helper for room actions.</summary>
        public void SendCreateRoom(string nickname, string characterId, int maxPlayers = 4)
        {
            var reqId = _nextSeq;
            Send(new ClientMessage
            {
                CreateRoomReq = new Game.Room.CreateRoomReq { RequestId = reqId, MaxPlayers = maxPlayers, Nickname = nickname, CharacterId = characterId }
            });
        }

        public void SendJoinRoom(string roomCode, string nickname, string characterId)
        {
            var reqId = _nextSeq;
            Send(new ClientMessage
            {
                JoinRoomReq = new Game.Room.JoinRoomReq { RequestId = reqId, RoomCode = roomCode, Nickname = nickname, CharacterId = characterId }
            });
        }

        public void SendEnterLobby(string nickname, string characterId)
        {
            var reqId = _nextSeq;
            Send(new ClientMessage
            {
                EnterLobbyReq = new EnterLobbyReq { RequestId = reqId, Nickname = nickname, CharacterId = characterId }
            });
        }

        public void SendLeaveLobby(long lobbyPlayerId)
        {
            Send(new ClientMessage
            {
                LeaveLobbyReq = new LeaveLobbyReq { RequestId = _nextSeq, LobbyPlayerId = lobbyPlayerId }
            });
        }

        public void SendListRooms()
        {
            Send(new ClientMessage
            {
                ListRoomsReq = new ListRoomsReq { RequestId = _nextSeq }
            });
        }

        public void SendApplyJoinRoom(long lobbyPlayerId, long roomId, string roomCode)
        {
            Send(new ClientMessage
            {
                ApplyJoinRoomReq = new ApplyJoinRoomReq
                {
                    RequestId = _nextSeq,
                    LobbyPlayerId = lobbyPlayerId,
                    RoomId = roomId,
                    RoomCode = roomCode ?? ""
                }
            });
        }

        public void SendRespondJoinApplication(long playerId, long roomId, long accessId, bool approve)
        {
            Send(new ClientMessage
            {
                RespondJoinApplicationReq = new RespondJoinApplicationReq
                {
                    RequestId = _nextSeq,
                    PlayerId = playerId,
                    RoomId = roomId,
                    AccessId = accessId,
                    Approve = approve
                }
            });
        }

        public void SendInviteLobbyPlayer(long playerId, long roomId, long lobbyPlayerId)
        {
            Send(new ClientMessage
            {
                InviteLobbyPlayerReq = new InviteLobbyPlayerReq
                {
                    RequestId = _nextSeq,
                    PlayerId = playerId,
                    RoomId = roomId,
                    LobbyPlayerId = lobbyPlayerId
                }
            });
        }

        public void SendRespondRoomInvitation(long lobbyPlayerId, long accessId, bool approve)
        {
            Send(new ClientMessage
            {
                RespondRoomInvitationReq = new RespondRoomInvitationReq
                {
                    RequestId = _nextSeq,
                    LobbyPlayerId = lobbyPlayerId,
                    AccessId = accessId,
                    Approve = approve
                }
            });
        }

        public void SendLeaveRoom(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                LeaveRoomReq = new Game.Room.LeaveRoomReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void SendReady(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                ReadyReq = new Game.Room.ReadyReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId, IsReady = true }
            });
        }

        public void SendStartGame(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                StartGameReq = new Game.Room.StartGameReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void SendReconnect(string sessionToken, long lastServerSeq)
        {
            Send(new ClientMessage
            {
                ReconnectReq = new ReconnectReq
                {
                    RequestId = _nextSeq,
                    SessionToken = sessionToken ?? "",
                    LastServerSeq = lastServerSeq
                }
            });
        }

        public void SendHeartbeat(long playerId)
        {
            Send(new ClientMessage
            {
                HeartbeatReq = new HeartbeatReq
                {
                    RequestId = _nextSeq,
                    PlayerId = playerId,
                    ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            });
        }

        public void SendRoomChatText(long playerId, long roomId, string text)
        {
            Send(new ClientMessage
            {
                RoomChatReq = new Game.Room.RoomChatReq
                {
                    RequestId = _nextSeq,
                    PlayerId = playerId,
                    RoomId = roomId,
                    Type = RoomChatType.Text,
                    Text = text ?? ""
                }
            });
        }

        public void SendRoomChatSticker(long playerId, long roomId, string stickerPack, string stickerName)
        {
            Send(new ClientMessage
            {
                RoomChatReq = new Game.Room.RoomChatReq
                {
                    RequestId = _nextSeq,
                    PlayerId = playerId,
                    RoomId = roomId,
                    Type = RoomChatType.Sticker,
                    StickerPack = stickerPack ?? "",
                    StickerName = stickerName ?? ""
                }
            });
        }

        public void SendDrawCard(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                DrawCardReq = new Game.Game.DrawCardReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void SendDiscardDrawn(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                DiscardDrawnReq = new Game.Game.DiscardDrawnReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void SendReplaceWithDrawn(long playerId, long roomId, int[] slotIndices)
        {
            var req = new ClientMessage
            {
                ReplaceWithDrawnReq = new Game.Game.ReplaceWithDrawnReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            };
            req.ReplaceWithDrawnReq.SlotIndices.AddRange(slotIndices);
            Send(req);
        }

        public void SendTakeFromDiscard(long playerId, long roomId, int[] slotIndices)
        {
            var req = new ClientMessage
            {
                TakeFromDiscardReq = new Game.Game.TakeFromDiscardReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            };
            req.TakeFromDiscardReq.SlotIndices.AddRange(slotIndices);
            Send(req);
        }

        public void SendUseSkillPeekSelf(long playerId, long roomId, int cardId, int slot)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq
                {
                    RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId, CardId = cardId,
                    PeekSelf = new Game.Common.PeekSelfParams { SlotIndex = slot }
                }
            });
        }

        public void SendUseSkillSpy(long playerId, long roomId, int cardId, long targetId, int slot)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq
                {
                    RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId, CardId = cardId,
                    Spy = new Game.Common.SpyParams { TargetPlayerId = targetId, TargetSlotIndex = slot }
                }
            });
        }

        public void SendUseSkillSwap(long playerId, long roomId, int cardId, long targetId, int mySlot, int targetSlot)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq
                {
                    RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId, CardId = cardId,
                    Swap = new Game.Common.SwapParams { OwnSlotIndex = mySlot, TargetPlayerId = targetId, TargetSlotIndex = targetSlot }
                }
            });
        }

        public void SendCallSteady(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                CallSteadyReq = new Game.Game.CallSteadyReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void SendEndGameEarly(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                EndGameEarlyReq = new Game.Game.EndGameEarlyReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void SendEndGameEarlyDecision(long playerId, long roomId, bool approve)
        {
            Send(new ClientMessage
            {
                EndGameEarlyDecisionReq = new Game.Game.EndGameEarlyDecisionReq
                {
                    RequestId = _nextSeq,
                    PlayerId = playerId,
                    RoomId = roomId,
                    Approve = approve
                }
            });
        }

        public void SendSkipSkill(long playerId, long roomId, int cardId)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId, CardId = cardId }
            });
        }

        public void Dispose()
        {
            Disconnect();
            wsClient?.Dispose();
        }

        private void OnDataReceived(byte[] data)
        {
            try
            {
                var msg = MessageCodec.DecodePayload(data);
                int queuedCount;
                lock (pendingMessages)
                {
                    pendingMessages.Enqueue(msg);
                    queuedCount = pendingMessages.Count;
                }
                Debug.Log($"[NetworkGateway] Queued {msg.PayloadCase} seq={msg.ServerSeq} pending={queuedCount}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkGateway] Decode error: {ex}");
            }
        }

        private void SetConnected(bool connected)
        {
            Volatile.Write(ref isConnected, connected ? 1 : 0);
        }
    }
}
