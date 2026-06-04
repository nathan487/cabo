using System;
using System.Collections;
using System.Collections.Generic;
using Game.Common;
using Game.Game;
using Game.Messages;
using Game.Room;
using Game.Sync;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Real IBackendGateway implementation using TCP + protobuf.
    /// Replaces ProtoGatewayPlaceholder for actual server communication.
    /// </summary>
    public sealed class ProtoGateway : IBackendGateway, IDisposable
    {
        private readonly TcpNetworkClient client;
        private readonly MessageCodec codec = new MessageCodec();
        private readonly MessageDispatcher dispatcher = new MessageDispatcher();
        private readonly RequestTracker requestTracker = new RequestTracker();
        private readonly MonoBehaviour coroutineOwner;

        // Thread-safe queue: background thread enqueues, main thread dequeues
        private readonly Queue<byte[]> incomingQueue = new Queue<byte[]>();
        private readonly object queueLock = new object();
        private volatile bool coroutineStarted;

        private long nextSeq;
        private long nextRequestId;
        private string nickname = "Player";
        private long localPlayerIdLong;
        private long currentRoomId;

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public string LocalPlayerId => localPlayerIdLong > 0 ? localPlayerIdLong.ToString() : string.Empty;
        public string SessionToken { get; private set; } = string.Empty;

        public event Action<ConnectionStatus> ConnectionStatusChanged;
        public event Action<RoomSnapshot> RoomUpdated;
        public event Action<string> RoomStarted;
        public event Action<BackendError> OperationFailed;

        private RoomSnapshot currentRoomSnapshot;

        // Game state (updated by notifications)
        public long CurrentTurnPlayerId { get; private set; }
        public int CurrentRoundNumber { get; private set; }
        public event Action GameStateChanged;

        // Game events for UI layer (fired from game notification handlers)
        public event Action<GameStartNotify> OnStartGame;
        public event Action<TurnStartNotify> OnTurnStart;
        public event Action<ActionResultNotify> OnActionResult;
        public event Action<RoundRevealNotify> OnRoundReveal;
        public event Action<ScoreUpdateNotify> OnScoreUpdate;
        public event Action<GameOverNotify> OnGameEnd;
        public event Action<DrawCardRsp> OnDrawResponse;
        public event Action<ReplaceWithDrawnRsp> OnReplaceResponse;
        public event Action<DiscardDrawnRsp> OnDiscardResponse;
        public event Action<TakeFromDiscardRsp> OnTakeDiscardResponse;

        public ProtoGateway(string host, int port, MonoBehaviour coroutineOwner)
        {
            this.coroutineOwner = coroutineOwner;
            client = new TcpNetworkClient(host, port);

            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;
            client.DataReceived += OnDataReceived;
            client.ErrorOccurred += OnClientError;

            RegisterHandlers();

            // Start main-thread tick coroutine immediately (safe to call from main thread)
            StartTickIfNeeded();
        }

        private void StartTickIfNeeded()
        {
            if (coroutineStarted) return;
            if (coroutineOwner == null) return;

            if (coroutineOwner.isActiveAndEnabled)
            {
                coroutineStarted = true;
                coroutineOwner.StartCoroutine(TickCoroutine());
                Debug.Log("[ProtoGateway] Tick coroutine started");
            }
            // If not active yet, will retry on next Connect() call
        }

        private void RegisterHandlers()
        {
            dispatcher.Register<CreateRoomRsp>(OnCreateRoomRsp);
            dispatcher.Register<JoinRoomRsp>(OnJoinRoomRsp);
            dispatcher.Register<LeaveRoomRsp>(OnLeaveRoomRsp);
            dispatcher.Register<ReadyRsp>(OnReadyRsp);
            dispatcher.Register<StartGameRsp>(OnStartGameRsp);
            dispatcher.Register<RoomStateNotify>(OnRoomStateNotify);
            dispatcher.Register<PlayerJoinNotify>(OnPlayerJoinNotify);
            dispatcher.Register<PlayerLeaveNotify>(OnPlayerLeaveNotify);
            dispatcher.Register<PlayerReadyNotify>(OnPlayerReadyNotify);
            dispatcher.Register<RoomStartNotify>(OnRoomStartNotify);

            // Game responses — resolve request tracker
            dispatcher.Register<DrawCardRsp>(rsp => { requestTracker.Resolve(rsp.RequestId); Debug.Log($"[ProtoGateway] DrawCardRsp: card={rsp.CardId} value={rsp.Value}"); OnDrawResponse?.Invoke(rsp); });
            dispatcher.Register<DiscardDrawnRsp>(rsp => { requestTracker.Resolve(rsp.RequestId); Debug.Log("[ProtoGateway] DiscardDrawnRsp OK"); OnDiscardResponse?.Invoke(rsp); });
            dispatcher.Register<ReplaceWithDrawnRsp>(rsp => { requestTracker.Resolve(rsp.RequestId); Debug.Log($"[ProtoGateway] ReplaceWithDrawnRsp: ok"); OnReplaceResponse?.Invoke(rsp); });
            dispatcher.Register<TakeFromDiscardRsp>(rsp => { requestTracker.Resolve(rsp.RequestId); Debug.Log($"[ProtoGateway] TakeFromDiscardRsp: ok"); OnTakeDiscardResponse?.Invoke(rsp); });
            dispatcher.Register<UseSkillRsp>(rsp => { requestTracker.Resolve(rsp.RequestId); Debug.Log($"[ProtoGateway] UseSkillRsp: ok"); });
            dispatcher.Register<CallSteadyRsp>(rsp => { requestTracker.Resolve(rsp.RequestId); Debug.Log("[ProtoGateway] CallSteadyRsp OK"); });

            // Game notifications — log
            dispatcher.Register<GameStartNotify>(OnGameStartNotify);
            dispatcher.Register<TurnStartNotify>(OnTurnStartNotify);
            dispatcher.Register<ActionResultNotify>(OnActionResultNotify);
            dispatcher.Register<RoundRevealNotify>(OnRoundRevealNotify);
            dispatcher.Register<ScoreUpdateNotify>(OnScoreUpdateNotify);
            dispatcher.Register<GameOverNotify>(OnGameOverNotify);
        }

        // ── IBackendGateway ──

        public void Connect(string nextNickname)
        {
            nickname = string.IsNullOrWhiteSpace(nextNickname) ? "Player" : nextNickname.Trim();
            Status = ConnectionStatus.Connecting;
            ConnectionStatusChanged?.Invoke(Status);

            // Ensure tick coroutine is running (may have failed during construction)
            StartTickIfNeeded();

            _ = client.ConnectAsync();
        }

        public void Disconnect()
        {
            client.Disconnect();
            requestTracker.Clear();
            codec.Reset();
            dispatcher.Reset();
            SetStatus(ConnectionStatus.Disconnected);
        }

        public void CreateRoom(int maxPlayers)
        {
            Debug.Log($"[ProtoGateway] CreateRoom called (nickname={nickname}, maxPlayers={maxPlayers})");
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                CreateRoomReq = new CreateRoomReq
                {
                    RequestId = requestId,
                    MaxPlayers = maxPlayers,
                    Nickname = nickname
                }
            };
            SendWithTracking(requestId, msg, "CreateRoom");
        }

        public void JoinRoom(string roomCode)
        {
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                JoinRoomReq = new JoinRoomReq
                {
                    RequestId = requestId,
                    RoomCode = roomCode.Trim().ToUpperInvariant(),
                    Nickname = nickname
                }
            };
            SendWithTracking(requestId, msg, "JoinRoom");
        }

        public void SetReady(bool isReady)
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                ReadyReq = new ReadyReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId,
                    IsReady = isReady
                }
            };
            SendWithTracking(requestId, msg, "SetReady");
        }

        public void StartGame()
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                StartGameReq = new StartGameReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId
                }
            };
            SendWithTracking(requestId, msg, "StartGame");
        }

        // ── Game Actions ──

        public void DrawCard()
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                DrawCardReq = new DrawCardReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId
                }
            };
            SendWithTracking(requestId, msg, "DrawCard");
        }

        public void DiscardDrawn()
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                DiscardDrawnReq = new DiscardDrawnReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId
                }
            };
            SendWithTracking(requestId, msg, "DiscardDrawn");
        }

        public void ReplaceWithDrawn(int slotIndex)
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                ReplaceWithDrawnReq = new ReplaceWithDrawnReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId,
                    SlotIndices = { slotIndex }
                }
            };
            SendWithTracking(requestId, msg, "ReplaceWithDrawn");
        }

        public void TakeFromDiscard(int slotIndex)
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                TakeFromDiscardReq = new TakeFromDiscardReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId,
                    SlotIndices = { slotIndex }
                }
            };
            SendWithTracking(requestId, msg, "TakeFromDiscard");
        }

        public void CallSteady()
        {
            if (localPlayerIdLong <= 0 || currentRoomId <= 0) return;
            var requestId = ++nextRequestId;
            var msg = new ClientMessage
            {
                Seq = ++nextSeq,
                CallSteadyReq = new CallSteadyReq
                {
                    RequestId = requestId,
                    PlayerId = localPlayerIdLong,
                    RoomId = currentRoomId
                }
            };
            SendWithTracking(requestId, msg, "CallSteady");
        }

        private void SendWithTracking(long requestId, ClientMessage msg, string operationName)
        {
            requestTracker.Register(requestId,
                onResponse: () =>
                {
                    Debug.Log($"[ProtoGateway] {operationName} request {requestId} resolved");
                },
                onTimeout: error =>
                {
                    Debug.LogError($"[ProtoGateway] {operationName} request {requestId} timed out");
                    OperationFailed?.Invoke(new BackendError { Code = 9002, Message = error });
                });

            var frame = MessageCodec.Encode(msg);
            client.Send(frame);
        }

        // ── Client events ──

        private void OnClientConnected()
        {
            SetStatus(ConnectionStatus.Connected);
        }

        private void OnClientDisconnected()
        {
            requestTracker.Clear();
            SetStatus(ConnectionStatus.Disconnected);
        }

        private void OnDataReceived(byte[] data)
        {
            // Called from background thread — just enqueue, don't process directly
            lock (queueLock)
            {
                incomingQueue.Enqueue(data);
            }
            Debug.Log($"[ProtoGateway] Received {data.Length} bytes from server (queued)");
        }

        private void OnClientError(string error)
        {
            OperationFailed?.Invoke(new BackendError { Code = 9003, Message = error });
        }

        private void SetStatus(ConnectionStatus newStatus)
        {
            if (Status == newStatus) return;
            Status = newStatus;
            ConnectionStatusChanged?.Invoke(Status);
        }

        private IEnumerator TickCoroutine()
        {
            Debug.Log("[ProtoGateway] TickCoroutine loop started");
            // Run forever until explicitly stopped by Dispose
            while (true)
            {
                if (Status == ConnectionStatus.Connected)
                {
                    ProcessIncomingQueue();
                    requestTracker.Tick(Time.time);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void ProcessIncomingQueue()
        {
            byte[][] items;
            lock (queueLock)
            {
                if (incomingQueue.Count == 0) return;
                items = incomingQueue.ToArray();
                incomingQueue.Clear();
            }

            Debug.Log($"[ProtoGateway] Processing {items.Length} queued messages on main thread");
            foreach (var data in items)
            {
                codec.FeedBytes(data, message =>
                {
                    dispatcher.Dispatch(message);
                });
            }
        }

        // ── Room response handlers ──

        private void OnCreateRoomRsp(CreateRoomRsp rsp)
        {
            Debug.Log($"[ProtoGateway] OnCreateRoomRsp called — code={rsp.Error?.Code}");
            if (!CheckError(rsp.Error)) return;
            localPlayerIdLong = rsp.PlayerId;
            SessionToken = rsp.SessionToken;
            currentRoomId = rsp.RoomId;
            requestTracker.Resolve(rsp.RequestId);
            Debug.Log($"[ProtoGateway] Room created: code={rsp.RoomCode}, playerId={rsp.PlayerId}");
        }

        private void OnRoomStateNotify(RoomStateNotify notify)
        {
            Debug.Log($"[ProtoGateway] OnRoomStateNotify called — room={notify.Room?.RoomCode}");
            var room = notify.Room;
            if (room == null) { Debug.LogWarning("[ProtoGateway] RoomStateNotify has null Room!"); return; }
            currentRoomId = room.RoomId;
            currentRoomSnapshot = ConvertRoomState(room);
            Debug.Log($"[ProtoGateway] Firing RoomUpdated: code={currentRoomSnapshot.RoomCode}, players={currentRoomSnapshot.Players.Count}");
            RoomUpdated?.Invoke(currentRoomSnapshot);
        }

        private void OnJoinRoomRsp(JoinRoomRsp rsp)
        {
            if (!CheckError(rsp.Error)) return;
            localPlayerIdLong = rsp.PlayerId;
            SessionToken = rsp.SessionToken;
            currentRoomId = rsp.RoomId;
            requestTracker.Resolve(rsp.RequestId);
            Debug.Log($"[ProtoGateway] Joined room: id={rsp.RoomId}, seat={rsp.SeatId}");
        }

        private void OnLeaveRoomRsp(LeaveRoomRsp rsp)
        {
            requestTracker.Resolve(rsp.RequestId);
            currentRoomId = 0;
            currentRoomSnapshot = null;
            Debug.Log("[ProtoGateway] Left room");
        }

        private void OnReadyRsp(ReadyRsp rsp)
        {
            if (!CheckError(rsp.Error)) return;
            requestTracker.Resolve(rsp.RequestId);
        }

        private void OnStartGameRsp(StartGameRsp rsp)
        {
            if (!CheckError(rsp.Error)) return;
            requestTracker.Resolve(rsp.RequestId);
        }

        private void OnPlayerJoinNotify(PlayerJoinNotify notify)
        {
            Debug.Log($"[ProtoGateway] Player joined: {notify.Player?.PlayerId}");
        }

        private void OnPlayerLeaveNotify(PlayerLeaveNotify notify)
        {
            Debug.Log($"[ProtoGateway] Player left: {notify.PlayerId}");
        }

        private void OnPlayerReadyNotify(PlayerReadyNotify notify)
        {
            Debug.Log($"[ProtoGateway] Player ready: {notify.PlayerId} = {notify.IsReady}");
        }

        private void OnRoomStartNotify(RoomStartNotify notify)
        {
            Debug.Log($"[ProtoGateway] Room {notify.RoomId} started!");
            RoomStarted?.Invoke(currentRoomSnapshot?.RoomCode ?? "");
        }

        // ── Game notification stubs (log + forward to GameClientController later) ──

        private void OnGameStartNotify(GameStartNotify notify)
        {
            Debug.Log($"[ProtoGateway] Game started: round={notify.RoundNumber}, firstPlayer={notify.FirstPlayerId}");
            GameSceneBootstrap.PendingGameStart = notify;
            // Defer scene load to avoid interrupting message processing loop
            if (coroutineOwner != null)
                coroutineOwner.StartCoroutine(LoadGameSceneNextFrame());
            OnStartGame?.Invoke(notify);
        }

        private System.Collections.IEnumerator LoadGameSceneNextFrame()
        {
            yield return null; // wait one frame
            GameSceneBootstrap.LoadGameScene();
        }

        private void OnTurnStartNotify(TurnStartNotify notify)
        {
            Debug.Log($"[ProtoGateway] ========== TurnStartNotify Received ==========");
            Debug.Log($"[ProtoGateway] Room: {notify.RoomId}");
            Debug.Log($"[ProtoGateway] Current Player: {notify.CurrentPlayerId}");
            Debug.Log($"[ProtoGateway] Turn: {notify.TurnNumber}");
            Debug.Log($"[ProtoGateway] Round: {notify.RoundNumber}");
            Debug.Log($"[ProtoGateway] Local Player ID: '{LocalPlayerId}'");
            Debug.Log($"[ProtoGateway] OnTurnStart subscribers: {(OnTurnStart?.GetInvocationList().Length ?? 0)}");
            Debug.Log($"[ProtoGateway] ================================================");

            CurrentTurnPlayerId = notify.CurrentPlayerId;
            CurrentRoundNumber = notify.RoundNumber;
            GameStateChanged?.Invoke();
            OnTurnStart?.Invoke(notify);
        }

        private void OnActionResultNotify(ActionResultNotify notify)
        {
            Debug.Log($"[ProtoGateway] Action: type={notify.ActionType}, player={notify.SourcePlayerId}");
            OnActionResult?.Invoke(notify);
        }

        private void OnRoundRevealNotify(RoundRevealNotify notify)
        {
            Debug.Log($"[ProtoGateway] Round reveal: round={notify.RoundNumber}, caller={notify.SteadyCallerId}");
            OnRoundReveal?.Invoke(notify);
        }

        private void OnScoreUpdateNotify(ScoreUpdateNotify notify)
        {
            Debug.Log($"[ProtoGateway] Score update: round={notify.RoundNumber}");
            OnScoreUpdate?.Invoke(notify);
        }

        private void OnGameOverNotify(GameOverNotify notify)
        {
            Debug.Log($"[ProtoGateway] Game over! rounds={notify.TotalRounds}");
            OnGameEnd?.Invoke(notify);
        }

        // ── Helpers ──

        private bool CheckError(ErrorInfo error)
        {
            if (error != null && error.Code != 0)
            {
                Debug.LogError($"[ProtoGateway] Server error {error.Code}: {error.Message}");
                OperationFailed?.Invoke(new BackendError { Code = error.Code, Message = error.Message });
                return false;
            }
            return true;
        }

        private static RoomSnapshot ConvertRoomState(RoomState room)
        {
            var snapshot = new RoomSnapshot
            {
                RoomId = room.RoomId,
                RoomCode = room.RoomCode,
                MaxPlayers = room.MaxPlayers,
                HostPlayerId = room.HostPlayerId.ToString(),
                InGame = room.State == RoomStateType.RoomStatePlaying,
                Players = new List<PlayerPublicInfoModel>()
            };

            foreach (var p in room.Players)
            {
                snapshot.Players.Add(new PlayerPublicInfoModel
                {
                    PlayerId = p.PlayerId.ToString(),
                    Nickname = p.Nickname,
                    SeatId = p.SeatId,
                    IsReady = p.IsReady,
                    IsHost = p.IsHost,
                    IsConnected = p.IsConnected,
                    TotalScore = p.TotalScore
                });
            }

            return snapshot;
        }

        public void Dispose()
        {
            client?.Dispose();
        }
    }
}
