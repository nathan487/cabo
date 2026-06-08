using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using Game.Messages;
using Game.Room;
using Game.Game;
using Game.Common;
using Cabo.Client.Network;
using UnityEngine;

namespace Cabo.Client
{
    /// <summary>
    /// Thin TCP + protobuf gateway. Reuses the battle-tested TcpNetworkClient
    /// and MessageCodec from the old project.
    /// </summary>
    public class NetworkGateway : IDisposable
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private CancellationTokenSource receiveCts;
        private readonly Queue<ServerMessage> pendingMessages = new();

        // MessageCodec: [4-byte big-endian length][protobuf payload]
        private readonly MessageCodec codec = new();
        private readonly MessageDispatcher dispatcher = new();

        public bool IsConnected { get; private set; }
        public long NextSeq => _nextSeq;
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

            foreach (var msg in drained)
            {
                beforeDispatch?.Invoke(msg);
                dispatcher.Dispatch(msg);
                MessageReceived?.Invoke(msg);
            }

            return drained.Count;
        }

        public async Task ConnectAsync(string host, int port)
        {
            try
            {
                tcpClient = new TcpClient { NoDelay = true };
                await tcpClient.ConnectAsync(host, port);
                stream = tcpClient.GetStream();
                IsConnected = true;
                receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoop(receiveCts.Token));
                Connected?.Invoke();
                Debug.Log($"[NetworkGateway] Connected to {host}:{port}");
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
            IsConnected = false;

            var cts = receiveCts;
            receiveCts = null;
            try { cts?.Cancel(); } catch (ObjectDisposedException) { }
            try { cts?.Dispose(); } catch (ObjectDisposedException) { }

            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            stream = null;
            tcpClient = null;

            if (wasConnected)
                Disconnected?.Invoke();
        }

        /// <summary>Send a ClientMessage. Sets seq automatically.</summary>
        public void Send(ClientMessage msg)
        {
            if (!IsConnected) return;
            msg.Seq = _nextSeq++;
            var frame = MessageCodec.Encode(msg);
            try { stream.Write(frame, 0, frame.Length); stream.Flush(); }
            catch (Exception ex) { Error?.Invoke($"Send failed: {ex.Message}"); Disconnect(); }
        }

        /// <summary>Quick send helper for room actions.</summary>
        public void SendCreateRoom(string nickname, int maxPlayers = 4)
        {
            var reqId = _nextSeq;
            Send(new ClientMessage
            {
                CreateRoomReq = new Game.Room.CreateRoomReq { RequestId = reqId, MaxPlayers = maxPlayers, Nickname = nickname }
            });
        }

        public void SendJoinRoom(string roomCode, string nickname)
        {
            var reqId = _nextSeq;
            Send(new ClientMessage
            {
                JoinRoomReq = new Game.Room.JoinRoomReq { RequestId = reqId, RoomCode = roomCode, Nickname = nickname }
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

        public void SendUseSkillPeekSelf(long playerId, long roomId, int slot)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq
                {
                    RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId,
                    PeekSelf = new Game.Common.PeekSelfParams { SlotIndex = slot }
                }
            });
        }

        public void SendUseSkillSpy(long playerId, long roomId, long targetId, int slot)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq
                {
                    RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId,
                    Spy = new Game.Common.SpyParams { TargetPlayerId = targetId, TargetSlotIndex = slot }
                }
            });
        }

        public void SendUseSkillSwap(long playerId, long roomId, long targetId, int mySlot, int targetSlot)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq
                {
                    RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId,
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

        public void SendSkipSkill(long playerId, long roomId)
        {
            Send(new ClientMessage
            {
                UseSkillReq = new Game.Game.UseSkillReq { RequestId = _nextSeq, PlayerId = playerId, RoomId = roomId }
            });
        }

        public void Dispose()
        {
            Disconnect();
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int n = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (n == 0) break;
                    var data = new byte[n];
                    Array.Copy(buffer, data, n);
                    lock (codec)
                    {
                        codec.FeedBytes(data, msg =>
                        {
                            lock (pendingMessages)
                                pendingMessages.Enqueue(msg);
                        });
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (System.IO.IOException) { }
            catch (Exception ex) { Debug.LogError($"[NetworkGateway] Receive error: {ex}"); }
            finally { if (IsConnected) { IsConnected = false; Disconnected?.Invoke(); } }
        }
    }
}
