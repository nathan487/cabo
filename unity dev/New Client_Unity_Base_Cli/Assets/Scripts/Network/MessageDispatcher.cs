using System;
using System.Collections.Generic;
using Game.Messages;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Routes ServerMessage oneof payload to registered typed handlers.
    /// Checks server_seq for ordering and deduplication.
    /// </summary>
    public sealed class MessageDispatcher
    {
        private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();
        private long lastServerSeq;
        private readonly HashSet<long> seenServerSeqs = new HashSet<long>();

        public event Action<long> ServerSeqSkipped;

        /// <summary>
        /// Register a typed handler for a specific ServerMessage payload type.
        /// Example: dispatcher.Register&lt;CreateRoomRsp&gt;(OnCreateRoomRsp);
        /// </summary>
        public void Register<T>(Action<T> handler) where T : class
        {
            handlers[typeof(T)] = handler;
        }

        /// <summary>
        /// Dispatch a ServerMessage to the appropriate handler.
        /// </summary>
        public void Dispatch(ServerMessage message)
        {
            // Seq check
            if (message.ServerSeq > 0)
            {
                if (seenServerSeqs.Contains(message.ServerSeq))
                {
                    Debug.LogWarning($"[MessageDispatcher] Duplicate server_seq={message.ServerSeq}, skipping");
                    ServerSeqSkipped?.Invoke(message.ServerSeq);
                    return;
                }
                if (message.ServerSeq < lastServerSeq)
                {
                    Debug.LogWarning($"[MessageDispatcher] Out-of-order server_seq={message.ServerSeq} (last={lastServerSeq})");
                }
                lastServerSeq = message.ServerSeq;
                seenServerSeqs.Add(message.ServerSeq);
            }

            // Dispatch by oneof case
            switch (message.PayloadCase)
            {
                // Room
                case ServerMessage.PayloadOneofCase.CreateRoomRsp:
                    InvokeHandler(message.CreateRoomRsp);
                    break;
                case ServerMessage.PayloadOneofCase.JoinRoomRsp:
                    InvokeHandler(message.JoinRoomRsp);
                    break;
                case ServerMessage.PayloadOneofCase.LeaveRoomRsp:
                    InvokeHandler(message.LeaveRoomRsp);
                    break;
                case ServerMessage.PayloadOneofCase.ReadyRsp:
                    InvokeHandler(message.ReadyRsp);
                    break;
                case ServerMessage.PayloadOneofCase.StartGameRsp:
                    InvokeHandler(message.StartGameRsp);
                    break;
                case ServerMessage.PayloadOneofCase.KickPlayerRsp:
                    InvokeHandler(message.KickPlayerRsp);
                    break;
                case ServerMessage.PayloadOneofCase.RoomStateNotify:
                    InvokeHandler(message.RoomStateNotify);
                    break;
                case ServerMessage.PayloadOneofCase.PlayerJoinNotify:
                    InvokeHandler(message.PlayerJoinNotify);
                    break;
                case ServerMessage.PayloadOneofCase.PlayerLeaveNotify:
                    InvokeHandler(message.PlayerLeaveNotify);
                    break;
                case ServerMessage.PayloadOneofCase.PlayerReadyNotify:
                    InvokeHandler(message.PlayerReadyNotify);
                    break;
                case ServerMessage.PayloadOneofCase.RoomStartNotify:
                    InvokeHandler(message.RoomStartNotify);
                    break;
                case ServerMessage.PayloadOneofCase.RoomChatRsp:
                    InvokeHandler(message.RoomChatRsp);
                    break;
                case ServerMessage.PayloadOneofCase.RoomChatNotify:
                    InvokeHandler(message.RoomChatNotify);
                    break;

                // Game
                case ServerMessage.PayloadOneofCase.GameStartNotify:
                    InvokeHandler(message.GameStartNotify);
                    break;
                case ServerMessage.PayloadOneofCase.TurnStartNotify:
                    InvokeHandler(message.TurnStartNotify);
                    break;
                case ServerMessage.PayloadOneofCase.DrawCardRsp:
                    InvokeHandler(message.DrawCardRsp);
                    break;
                case ServerMessage.PayloadOneofCase.DiscardDrawnRsp:
                    InvokeHandler(message.DiscardDrawnRsp);
                    break;
                case ServerMessage.PayloadOneofCase.ReplaceWithDrawnRsp:
                    InvokeHandler(message.ReplaceWithDrawnRsp);
                    break;
                case ServerMessage.PayloadOneofCase.TakeFromDiscardRsp:
                    InvokeHandler(message.TakeFromDiscardRsp);
                    break;
                case ServerMessage.PayloadOneofCase.UseSkillRsp:
                    InvokeHandler(message.UseSkillRsp);
                    break;
                case ServerMessage.PayloadOneofCase.CallSteadyRsp:
                    InvokeHandler(message.CallSteadyRsp);
                    break;
                case ServerMessage.PayloadOneofCase.ActionResultNotify:
                    InvokeHandler(message.ActionResultNotify);
                    break;
                case ServerMessage.PayloadOneofCase.RoundRevealNotify:
                    InvokeHandler(message.RoundRevealNotify);
                    break;
                case ServerMessage.PayloadOneofCase.ScoreUpdateNotify:
                    InvokeHandler(message.ScoreUpdateNotify);
                    break;
                case ServerMessage.PayloadOneofCase.GameOverNotify:
                    InvokeHandler(message.GameOverNotify);
                    break;

                // Sync
                case ServerMessage.PayloadOneofCase.ReconnectRsp:
                    InvokeHandler(message.ReconnectRsp);
                    break;
                case ServerMessage.PayloadOneofCase.StateSyncNotify:
                    InvokeHandler(message.StateSyncNotify);
                    break;
                case ServerMessage.PayloadOneofCase.HeartbeatRsp:
                    InvokeHandler(message.HeartbeatRsp);
                    break;

                case ServerMessage.PayloadOneofCase.None:
                    Debug.LogWarning("[MessageDispatcher] Received ServerMessage with None payload");
                    break;

                default:
                    Debug.LogWarning($"[MessageDispatcher] Unhandled payload type: {message.PayloadCase}");
                    break;
            }
        }

        private void InvokeHandler<T>(T payload) where T : class
        {
            if (payload == null) return;
            if (handlers.TryGetValue(typeof(T), out var handler))
            {
                ((Action<T>)handler)?.Invoke(payload);
            }
            else
            {
                // The current client flow drains raw ServerMessage batches through MessageReceived.
                // Typed handlers are optional extension points, so missing registrations are not warnings.
            }
        }

        public void Reset()
        {
            lastServerSeq = 0;
            seenServerSeqs.Clear();
        }
    }
}
