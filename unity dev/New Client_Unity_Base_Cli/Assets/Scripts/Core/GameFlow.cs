using System;
using System.Collections.Generic;
using Game.Messages;
using Game.Room;
using UnityEngine;

namespace Cabo.Client
{
    /// <summary>
    /// Main game flow state machine. Direct C# translation of CLI's ClientApp::gameLoop.
    /// drain-then-decide: process ALL pending messages before any UI decision.
    /// </summary>
    public enum FlowState
    {
        Home, Connecting, Reconnecting, RoomFlow, RoomBrowser, WaitingRoom, Playing, RoundReveal, GameOver
    }

    public enum GameSubState
    {
        Idle,
        AwaitingMainInput, WaitingDrawRsp, AwaitingDrawnDecision,
        WaitingDiscardRsp, AwaitingReplaceSlots, WaitingReplaceRsp,
        AwaitingTakeSlots, WaitingTakeRsp,
        SkillPeekSlot, SkillSpyTarget, SkillSpySlot,
        SkillSwapMySlot, SkillSwapTargetPlayer, SkillSwapTargetSlot,
        WaitingSkillRsp, WaitingCallSteadyRsp
    }

    public class GameFlow
    {
        public GameState State { get; } = new();
        public NetworkGateway Gateway { get; }
        public FlowState Flow { get; private set; } = FlowState.Home;
        public bool IsConnected => Gateway?.IsConnected ?? false;
        public bool CanSendRoomChat => IsConnected && State.MyPlayerId > 0 && State.RoomId > 0;
        public bool IsReconnecting { get; private set; }
        public string ConnectedAddress { get; private set; } = "";
        public string LastConnectError { get; private set; } = "";

        public GameSubState SubState { get; private set; } = GameSubState.Idle;
        public int SkillTypePending;     // 2=PeekSelf, 3=Spy, 4=Swap
        public int SkillTypeJustCompleted;
        public int SkillMySlot, SkillTargetSlot;
        public long SkillTargetPlayerId;

        public event Action StateChanged;  // Fire when UI should refresh

        public const string ServerAddressPrefsKey = "Cabo.LastServerAddress";
        public const string DefaultServerAddress = "ws://127.0.0.1:8888";
        const float ReconnectWindowSeconds = 60f;
        const float ReconnectAttemptIntervalSeconds = 2f;
        const float HeartbeatIntervalSeconds = 5f;

        string _nickname = "玩家";
        bool _running = true;
        bool _wasConnected;
        bool _reconnectAttemptInFlight;
        bool _reconnectRequestSent;
        float _reconnectStartedAt;
        float _nextReconnectAttemptAt;
        float _nextHeartbeatAt;
        string _reconnectSessionToken = "";
        long _reconnectLastServerSeq;

        public GameFlow(NetworkGateway gw) { Gateway = gw; }

        public string GetCachedServerAddress()
        {
            return PlayerPrefs.GetString(ServerAddressPrefsKey, DefaultServerAddress);
        }

        public void ConnectToServerAddress(string address, string nickname = null)
        {
            var trimmed = string.IsNullOrWhiteSpace(address) ? DefaultServerAddress : address.Trim();
            PlayerPrefs.SetString(ServerAddressPrefsKey, trimmed);
            PlayerPrefs.Save();

            if (!TryNormalizeServerUrl(trimmed, out var url))
            {
                LastConnectError = "服务器地址格式应为 ws://host:port 或 wss://域名。";
                Flow = FlowState.Home;
                StateChanged?.Invoke();
                return;
            }

            PlayerPrefs.SetString(ServerAddressPrefsKey, url);
            PlayerPrefs.Save();
            Connect(url, nickname);
        }

        public async void Connect(string url, string nickname = null)
        {
            Debug.Log($"[GameFlow] Connect requested url={url}");
            if (Gateway.IsConnected)
            {
                _wasConnected = false;
                Gateway.Disconnect();
            }

            IsReconnecting = false;
            _reconnectAttemptInFlight = false;
            _reconnectRequestSent = false;
            LastConnectError = "";
            ConnectedAddress = url;
            if (!string.IsNullOrWhiteSpace(nickname))
                _nickname = NormalizeNickname(nickname);
            Flow = FlowState.Connecting; StateChanged?.Invoke();
            await Gateway.ConnectAsync(url);
            _wasConnected = Gateway.IsConnected;
            if (!Gateway.IsConnected)
            {
                ConnectedAddress = "";
                LastConnectError = "连接失败，请检查服务器地址和服务端状态。";
                Flow = FlowState.Home;
                StateChanged?.Invoke();
                return;
            }
            Flow = FlowState.RoomFlow; StateChanged?.Invoke();
        }

        // ── Room Actions ──

        public void CreateRoom(string nickname, string characterId)
        {
            _nickname = NormalizeNickname(nickname);
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            State.SetRequestedCharacterId(normalizedCharacterId);
            Gateway.SendCreateRoom(_nickname, normalizedCharacterId);
            Flow = FlowState.WaitingRoom; StateChanged?.Invoke();
        }

        public void JoinRoom(string roomCode, string nickname, string characterId)
        {
            _nickname = NormalizeNickname(nickname);
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            State.SetRequestedCharacterId(normalizedCharacterId);
            Gateway.SendJoinRoom(roomCode, _nickname, normalizedCharacterId);
            Flow = FlowState.WaitingRoom; StateChanged?.Invoke();
        }

        public void JoinRoomFromBrowser(string roomCode)
        {
            var normalizedCode = roomCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(normalizedCode))
                return;

            if (Gateway.IsConnected && State.LobbyPlayerId > 0)
                Gateway.SendLeaveLobby(State.LobbyPlayerId);
            State.ClearRoomBrowserState();
            JoinRoom(normalizedCode, _nickname, State.RequestedCharacterId);
        }

        public void EnterRoomBrowser(string nickname, string characterId)
        {
            _nickname = NormalizeNickname(nickname);
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            State.SetRequestedCharacterId(normalizedCharacterId);
            State.ClearRoomBrowserState();
            if (Gateway.IsConnected)
                Gateway.SendEnterLobby(_nickname, normalizedCharacterId);
            Flow = FlowState.RoomBrowser;
            StateChanged?.Invoke();
        }

        public void ReturnHomeFromRoomBrowser()
        {
            if (Gateway.IsConnected && State.LobbyPlayerId > 0)
                Gateway.SendLeaveLobby(State.LobbyPlayerId);
            State.ClearRoomBrowserState();
            Flow = Gateway.IsConnected ? FlowState.RoomFlow : FlowState.Home;
            StateChanged?.Invoke();
        }

        public void RefreshRooms()
        {
            if (Gateway.IsConnected)
                Gateway.SendListRooms();
        }

        public void ApplyJoinRoom(long roomId, string roomCode)
        {
            if (!Gateway.IsConnected || State.LobbyPlayerId <= 0)
                return;
            Gateway.SendApplyJoinRoom(State.LobbyPlayerId, roomId, roomCode);
            StateChanged?.Invoke();
        }

        public void InviteLobbyPlayer(long lobbyPlayerId)
        {
            if (!Gateway.IsConnected || State.MyPlayerId <= 0 || State.RoomId <= 0 || lobbyPlayerId <= 0)
                return;
            Gateway.SendInviteLobbyPlayer(State.MyPlayerId, State.RoomId, lobbyPlayerId);
            StateChanged?.Invoke();
        }

        public void RespondJoinApplication(long accessId, bool approve)
        {
            if (!Gateway.IsConnected || State.MyPlayerId <= 0 || State.RoomId <= 0 || accessId <= 0)
                return;
            Gateway.SendRespondJoinApplication(State.MyPlayerId, State.RoomId, accessId, approve);
            StateChanged?.Invoke();
        }

        public void RespondRoomInvitation(long accessId, bool approve)
        {
            if (!Gateway.IsConnected || State.LobbyPlayerId <= 0 || accessId <= 0)
                return;
            Gateway.SendRespondRoomInvitation(State.LobbyPlayerId, accessId, approve);
            StateChanged?.Invoke();
        }

        static string NormalizeNickname(string nickname)
        {
            var trimmed = nickname?.Trim();
            return string.IsNullOrEmpty(trimmed) ? "玩家" : trimmed;
        }

        static string NormalizeCharacterId(string characterId)
        {
            var trimmed = characterId?.Trim().ToLowerInvariant();
            switch (trimmed)
            {
                case "strawberry":
                case "oat":
                case "bean":
                case "trainee":
                case "milkdragon":
                    return trimmed;
                default:
                    return "pomelo";
            }
        }

        static bool TryNormalizeServerUrl(string address, out string url)
        {
            url = "";

            if (string.IsNullOrWhiteSpace(address))
                return false;

            var trimmed = address.Trim();
            if (!trimmed.Contains("://"))
                trimmed = "ws://" + trimmed;

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return false;

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme == "https")
                scheme = "wss";
            else if (scheme == "http")
                scheme = "ws";

            if (scheme != "ws" && scheme != "wss")
                return false;

            if (string.IsNullOrWhiteSpace(uri.Host))
                return false;

            var builder = new UriBuilder(uri) { Scheme = scheme };
            if ((scheme == "ws" && builder.Port == 80) || (scheme == "wss" && builder.Port == 443))
                builder.Port = -1;
            url = builder.Uri.ToString();
            if (url.EndsWith("/", StringComparison.Ordinal) && string.IsNullOrEmpty(uri.PathAndQuery.Trim('/')))
                url = url.Substring(0, url.Length - 1);
            return true;
        }

        public void Connect(string host, int port, string nickname = null)
        {
            if (!TryNormalizeServerUrl($"{host}:{port}", out var url))
            {
                LastConnectError = "服务器地址格式应为 ws://host:port 或 wss://域名。";
                Flow = FlowState.Home;
                StateChanged?.Invoke();
                return;
            }
            Connect(url, nickname);
        }

        public void SendReady()
        {
            if (State.MyPlayerId > 0 && State.RoomId > 0)
                Gateway.SendReady(State.MyPlayerId, State.RoomId);
        }

        public void SendStartGame()
        {
            if (State.MyPlayerId > 0 && State.RoomId > 0)
                Gateway.SendStartGame(State.MyPlayerId, State.RoomId);
        }

        public void SendRoomChatText(string text)
        {
            var trimmed = text?.Trim();
            if (!CanSendRoomChat || string.IsNullOrEmpty(trimmed))
                return;

            if (trimmed.Length > 120)
                trimmed = trimmed.Substring(0, 120);
            Gateway.SendRoomChatText(State.MyPlayerId, State.RoomId, trimmed);
        }

        public void SendRoomChatSticker(string stickerPack, string stickerName)
        {
            if (!CanSendRoomChat || string.IsNullOrWhiteSpace(stickerPack) || string.IsNullOrWhiteSpace(stickerName))
                return;

            Gateway.SendRoomChatSticker(State.MyPlayerId, State.RoomId, stickerPack.Trim(), stickerName.Trim());
        }

        public void LeaveRoomToHome()
        {
            if (State.MyPlayerId > 0 && State.RoomId > 0 && Gateway.IsConnected)
                Gateway.SendLeaveRoom(State.MyPlayerId, State.RoomId);

            State.ReturnHome();
            Flow = Gateway.IsConnected ? FlowState.RoomFlow : FlowState.Home;
            SubState = GameSubState.Idle;
            SkillTypePending = 0;
            SkillTypeJustCompleted = 0;
            SkillMySlot = -1;
            SkillTargetSlot = -1;
            SkillTargetPlayerId = 0;
            StateChanged?.Invoke();
        }

        public void ReturnToRoomAfterGameOver()
        {
            if (State.Phase != GamePhase.GameOver)
                return;

            State.ReturnToRoomAfterGameOver();
            Flow = FlowState.WaitingRoom;
            SubState = GameSubState.Idle;
            SkillTypePending = 0;
            SkillTypeJustCompleted = 0;
            SkillMySlot = -1;
            SkillTargetSlot = -1;
            SkillTargetPlayerId = 0;
            StateChanged?.Invoke();
        }

        public void ReturnHomeAfterGameOver()
        {
            if (State.Phase != GamePhase.GameOver)
                return;

            LeaveRoomToHome();
        }

        public void CompletePendingGameOverPresentation()
        {
            if (!State.CompletePendingGameOverPresentation())
                return;

            Flow = FlowState.GameOver;
            SubState = GameSubState.Idle;
            StateChanged?.Invoke();
        }

        public void RequestEarlyEndGame()
        {
            if (!Gateway.IsConnected || State.MyPlayerId <= 0 || State.RoomId <= 0)
            {
                Debug.LogWarning($"[GameFlow] RequestEarlyEndGame ignored. connected={Gateway.IsConnected} player={State.MyPlayerId} room={State.RoomId}");
                return;
            }
            if (State.Phase != GamePhase.Playing && State.Phase != GamePhase.RoundReveal)
            {
                Debug.LogWarning($"[GameFlow] RequestEarlyEndGame ignored in phase {State.Phase}");
                return;
            }

            Debug.Log($"[GameFlow] SendEndGameEarly player={State.MyPlayerId} room={State.RoomId} host={State.IsMyselfHost}");
            Gateway.SendEndGameEarly(State.MyPlayerId, State.RoomId);
            State.IsWaitingForEndGameRequestRsp = true;
            State.ShowEndGameRequestPrompt = false;
            if (!State.IsMyselfHost)
            {
                State.PendingEndGameRequesterPlayerId = State.MyPlayerId;
                var me = State.Players.Find(player => player.PlayerId == State.MyPlayerId);
                State.PendingEndGameRequesterNickname = me?.Nickname ?? "";
            }
            StateChanged?.Invoke();
        }

        public void RespondEarlyEndGameRequest(bool approve)
        {
            if (!Gateway.IsConnected || State.MyPlayerId <= 0 || State.RoomId <= 0)
            {
                Debug.LogWarning($"[GameFlow] RespondEarlyEndGameRequest ignored. connected={Gateway.IsConnected} player={State.MyPlayerId} room={State.RoomId}");
                return;
            }
            if (!State.IsMyselfHost || State.PendingEndGameRequesterPlayerId == 0)
            {
                Debug.LogWarning($"[GameFlow] RespondEarlyEndGameRequest ignored. host={State.IsMyselfHost} requester={State.PendingEndGameRequesterPlayerId}");
                return;
            }

            Debug.Log($"[GameFlow] SendEndGameEarlyDecision approve={approve} requester={State.PendingEndGameRequesterPlayerId}");
            Gateway.SendEndGameEarlyDecision(State.MyPlayerId, State.RoomId, approve);
            State.IsWaitingForEndGameDecisionRsp = true;
            State.ShowEndGameRequestPrompt = false;
            StateChanged?.Invoke();
        }

        public void DismissEarlyEndGamePrompt()
        {
            if (!State.ShowEndGameRequestPrompt)
                return;

            State.ShowEndGameRequestPrompt = false;
            StateChanged?.Invoke();
        }

        public void DismissEarlyEndGameRejectedPrompt()
        {
            if (!State.ShowEndGameRejectedPrompt)
                return;

            State.ShowEndGameRejectedPrompt = false;
            StateChanged?.Invoke();
        }

        public void ExitGame()
        {
            _running = false;
            Gateway?.Dispose();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ProcessServerMessage(ServerMessage msg)
        {
            var previousFlow = Flow;
            var previousPhase = State.Phase;

            Debug.Log($"[GameFlow] <= {msg.PayloadCase} seq={msg.ServerSeq} before flow={Flow} phase={State.Phase} room={State.RoomId} my={State.MyPlayerId} players={State.Players.Count} address={ConnectedAddress}");
            State.UpdateFromMessage(msg);
            Debug.Log($"[GameFlow] <= {msg.PayloadCase} after flow={Flow} phase={State.Phase} room={State.RoomId} my={State.MyPlayerId} players={State.Players.Count} rooms={State.RoomSummaries.Count} inbox={State.AccessInboxItems.Count}");

            if (IsReconnecting)
            {
                if (msg.PayloadCase == ServerMessage.PayloadOneofCase.ReconnectRsp
                    && msg.ReconnectRsp?.Error?.Code != 0)
                {
                    FailReconnect(string.IsNullOrEmpty(msg.ReconnectRsp.Error.Message)
                        ? "重连失败，已离开牌局。"
                        : msg.ReconnectRsp.Error.Message);
                    return;
                }

                if (msg.PayloadCase == ServerMessage.PayloadOneofCase.StateSyncNotify)
                    CompleteReconnect();
            }

            switch (State.Phase)
            {
                case GamePhase.WaitingRoom:
                    if (Flow != FlowState.Playing && Flow != FlowState.RoundReveal && Flow != FlowState.GameOver)
                        Flow = FlowState.WaitingRoom;
                    break;
                case GamePhase.Playing:
                    if (Flow != FlowState.Playing)
                    {
                        Flow = FlowState.Playing;
                        SubState = GameSubState.Idle;
                    }
                    break;
                case GamePhase.RoundReveal:
                    Flow = FlowState.RoundReveal;
                    SubState = GameSubState.Idle;
                    break;
                case GamePhase.GameOver:
                    Flow = FlowState.GameOver;
                    SubState = GameSubState.Idle;
                    break;
            }

            if (msg.PayloadCase == ServerMessage.PayloadOneofCase.StateSyncNotify)
                RestoreSubStateFromSynchronizedState();

            if (State.Phase == GamePhase.Lobby
                && State.LobbyPlayerId > 0
                && Flow != FlowState.Playing
                && Flow != FlowState.RoundReveal
                && Flow != FlowState.GameOver)
            {
                Flow = FlowState.RoomBrowser;
            }

            if (Flow != previousFlow || State.Phase != previousPhase || msg.PayloadCase != ServerMessage.PayloadOneofCase.None)
                StateChanged?.Invoke();
        }

        // ── Game Actions ──

        public void DoDraw()
        {
            Gateway.SendDrawCard(State.MyPlayerId, State.RoomId);
            State.WaitingForDrawResponse = true;
            SubState = GameSubState.WaitingDrawRsp;
            StateChanged?.Invoke();
        }

        public void DoTakeFromDiscard()
        {
            SubState = GameSubState.AwaitingTakeSlots;
            StateChanged?.Invoke();
        }

        public void ReturnToMainInput()
        {
            if (!State.IsMyTurn || State.HasDrawnCard)
                return;

            SubState = GameSubState.AwaitingMainInput;
            StateChanged?.Invoke();
        }

        public void DoCallSteady()
        {
            Gateway.SendCallSteady(State.MyPlayerId, State.RoomId);
            State.WaitingForCallSteadyResponse = true;
            SubState = GameSubState.WaitingCallSteadyRsp;
            StateChanged?.Invoke();
        }

        public void DoDiscardDrawn(bool useSkill)
        {
            Gateway.SendDiscardDrawn(State.MyPlayerId, State.RoomId);
            bool hasPlayableSkill = GameState.IsPlayableSkill(State.DrawnCardSkill);
            SkillTypePending = useSkill && hasPlayableSkill ? State.DrawnCardSkill : hasPlayableSkill ? -1 : 0;
            SubState = GameSubState.WaitingDiscardRsp;
            StateChanged?.Invoke();
        }

        public void BeginReplaceWithDrawn()
        {
            if (!State.HasDrawnCard) return;
            SubState = GameSubState.AwaitingReplaceSlots;
            StateChanged?.Invoke();
        }

        public void ReturnToDrawnDecision()
        {
            if (!State.IsMyTurn || !State.HasDrawnCard)
                return;

            SubState = GameSubState.AwaitingDrawnDecision;
            StateChanged?.Invoke();
        }

        public void DoReplaceWithDrawn(int[] slots)
        {
            Gateway.SendReplaceWithDrawn(State.MyPlayerId, State.RoomId, slots);
            SubState = GameSubState.WaitingReplaceRsp;
            StateChanged?.Invoke();
        }

        public void DoTakeFromDiscardSlots(int[] slots)
        {
            Gateway.SendTakeFromDiscard(State.MyPlayerId, State.RoomId, slots);
            State.WaitingForTakeResponse = true;
            SubState = GameSubState.WaitingTakeRsp;
            StateChanged?.Invoke();
        }

        public void DoSkillPeek(int slot)
        {
            SkillMySlot = slot;
            SendSkillRequest();
        }

        public void DoSkillSpyTarget(long targetId)
        {
            SkillTargetPlayerId = targetId;
            SubState = GameSubState.SkillSpySlot; StateChanged?.Invoke();
        }

        public void DoSkillSpySlot(int slot)
        {
            SkillTargetSlot = slot;
            SendSkillRequest();
        }

        public void DoSkillSwapMySlot(int slot)
        {
            SkillMySlot = slot;
            SubState = GameSubState.SkillSwapTargetPlayer; StateChanged?.Invoke();
        }

        public void DoSkillSwapTargetPlayer(long targetId)
        {
            SkillTargetPlayerId = targetId;
            SubState = GameSubState.SkillSwapTargetSlot; StateChanged?.Invoke();
        }

        public void DoSkillSwapTargetSlot(int slot)
        {
            SkillTargetSlot = slot;
            SendSkillRequest();
        }

        public void ReturnToSkillStart()
        {
            SkillMySlot = -1;
            SkillTargetSlot = -1;
            SkillTargetPlayerId = 0;
            SubState = SkillTypePending switch
            {
                2 => GameSubState.SkillPeekSlot,
                3 => GameSubState.SkillSpyTarget,
                4 => GameSubState.SkillSwapMySlot,
                _ => SubState
            };
            StateChanged?.Invoke();
        }

        public void ReturnToSkillTargetSelection()
        {
            SkillTargetSlot = -1;
            SkillTargetPlayerId = 0;
            SubState = SkillTypePending switch
            {
                3 => GameSubState.SkillSpyTarget,
                4 => GameSubState.SkillSwapTargetPlayer,
                _ => SubState
            };
            StateChanged?.Invoke();
        }

        void SendSkillRequest()
        {
            int skillCardId = State.PendingSkillCardId != 0 ? State.PendingSkillCardId : State.DrawnCardId;
            switch (SkillTypePending)
            {
                case 2: Gateway.SendUseSkillPeekSelf(State.MyPlayerId, State.RoomId, skillCardId, SkillMySlot); break;
                case 3: Gateway.SendUseSkillSpy(State.MyPlayerId, State.RoomId, skillCardId, SkillTargetPlayerId, SkillTargetSlot); break;
                case 4: Gateway.SendUseSkillSwap(State.MyPlayerId, State.RoomId, skillCardId, SkillTargetPlayerId, SkillMySlot, SkillTargetSlot); break;
            }
            SkillTypeJustCompleted = SkillTypePending;
            SkillTypePending = 0;
            State.WaitingForSkillResponse = true;
            SubState = GameSubState.WaitingSkillRsp;
            StateChanged?.Invoke();
        }

        public void DoSkipSkill()
        {
            int skillCardId = State.PendingSkillCardId != 0 ? State.PendingSkillCardId : State.DrawnCardId;
            Gateway.SendSkipSkill(State.MyPlayerId, State.RoomId, skillCardId);
            SkillTypeJustCompleted = -1;
            SkillTypePending = 0;
            State.WaitingForSkillResponse = true;
            SubState = GameSubState.WaitingSkillRsp;
            StateChanged?.Invoke();
        }

        // ── Tick ── Called once per frame. Processes state transitions. ──

        public void Tick()
        {
            if (!_running) return;

            if (_wasConnected && !Gateway.IsConnected)
            {
                _wasConnected = false;
                if (CanBeginReconnect())
                {
                    BeginReconnect();
                }
                else
                {
                    ConnectedAddress = "";
                    LastConnectError = "已断开服务器连接。";
                    State.ReturnHome();
                    Flow = FlowState.Home;
                    SubState = GameSubState.Idle;
                    StateChanged?.Invoke();
                }
            }
            else if (!_wasConnected && Gateway.IsConnected)
            {
                _wasConnected = true;
            }

            // Drain all pending TCP messages before the state machine decides what UI/actions are valid.
            Gateway.DrainMessages(ProcessServerMessage);
            TickHeartbeat();

            if (IsReconnecting)
            {
                TickReconnect();
                return;
            }

            if (Flow != FlowState.Playing) return;

            // Step 1: Check transitions
            CheckTransitions();
        }

        void TickHeartbeat()
        {
            if (!Gateway.IsConnected || Time.realtimeSinceStartup < _nextHeartbeatAt)
                return;

            _nextHeartbeatAt = Time.realtimeSinceStartup + HeartbeatIntervalSeconds;
            long playerId = State.MyPlayerId > 0 ? State.MyPlayerId : State.LobbyPlayerId;
            Gateway.SendHeartbeat(playerId);
            Debug.Log($"[GameFlow] Heartbeat sent player={playerId} lastSeq={Gateway.LastServerSeq}");
        }

        bool CanBeginReconnect()
        {
            return !string.IsNullOrEmpty(ConnectedAddress)
                && !string.IsNullOrEmpty(State.SessionToken)
                && State.MyPlayerId > 0
                && State.RoomId > 0;
        }

        void BeginReconnect()
        {
            IsReconnecting = true;
            _reconnectAttemptInFlight = false;
            _reconnectRequestSent = false;
            _reconnectStartedAt = Time.realtimeSinceStartup;
            _nextReconnectAttemptAt = 0f;
            _reconnectSessionToken = State.SessionToken;
            _reconnectLastServerSeq = Gateway.LastServerSeq;
            LastConnectError = "正在重连...";
            Flow = FlowState.Reconnecting;
            RestoreSubStateFromSynchronizedState();
            StateChanged?.Invoke();
        }

        void TickReconnect()
        {
            if (Time.realtimeSinceStartup - _reconnectStartedAt > ReconnectWindowSeconds)
            {
                FailReconnect("重连超时，已离开牌局。");
                return;
            }

            if (Gateway.IsConnected)
            {
                if (!_reconnectRequestSent)
                {
                    Gateway.SendReconnect(_reconnectSessionToken, _reconnectLastServerSeq);
                    _reconnectRequestSent = true;
                    StateChanged?.Invoke();
                }
                return;
            }

            if (_reconnectAttemptInFlight || Time.realtimeSinceStartup < _nextReconnectAttemptAt)
                return;

            AttemptReconnect();
        }

        async void AttemptReconnect()
        {
            _reconnectAttemptInFlight = true;
            _nextReconnectAttemptAt = Time.realtimeSinceStartup + ReconnectAttemptIntervalSeconds;
            await Gateway.ConnectAsync(ConnectedAddress);
            _reconnectAttemptInFlight = false;
            if (!IsReconnecting)
                return;

            if (Gateway.IsConnected)
            {
                _wasConnected = true;
                _reconnectRequestSent = false;
            }
        }

        void CompleteReconnect()
        {
            IsReconnecting = false;
            _reconnectAttemptInFlight = false;
            _reconnectRequestSent = false;
            LastConnectError = "";
            _wasConnected = Gateway.IsConnected;
            Flow = State.Phase switch
            {
                GamePhase.WaitingRoom => FlowState.WaitingRoom,
                GamePhase.Playing => FlowState.Playing,
                GamePhase.RoundReveal => FlowState.RoundReveal,
                GamePhase.GameOver => FlowState.GameOver,
                _ => FlowState.RoomFlow
            };
            SubState = GameSubState.Idle;
            StateChanged?.Invoke();
        }

        void FailReconnect(string message)
        {
            IsReconnecting = false;
            _reconnectAttemptInFlight = false;
            _reconnectRequestSent = false;
            LastConnectError = string.IsNullOrEmpty(message) ? "重连失败，已离开牌局。" : message;
            ConnectedAddress = "";
            State.ReturnHome();
            Flow = FlowState.Home;
            SubState = GameSubState.Idle;
            _wasConnected = false;
            StateChanged?.Invoke();
        }

        void CheckTransitions()
        {
            var previousSubState = SubState;

            // Not my turn → Idle
            if (!State.IsMyTurn && SubState != GameSubState.Idle
                && SubState != GameSubState.WaitingDrawRsp
                && SubState != GameSubState.WaitingDiscardRsp
                && SubState != GameSubState.WaitingReplaceRsp
                && SubState != GameSubState.WaitingTakeRsp
                && SubState != GameSubState.WaitingSkillRsp
                && SubState != GameSubState.WaitingCallSteadyRsp)
            {
                SubState = GameSubState.Idle;
            }

            // DrawCardRsp arrived
            if (SubState == GameSubState.WaitingDrawRsp && !State.WaitingForDrawResponse)
            {
                SubState = State.HasDrawnCard ? GameSubState.AwaitingDrawnDecision : GameSubState.Idle;
            }

            // TakeFromDiscardRsp arrived
            if (SubState == GameSubState.WaitingTakeRsp && !State.WaitingForTakeResponse)
                SubState = GameSubState.Idle;

            // CallSteadyRsp arrived
            if (SubState == GameSubState.WaitingCallSteadyRsp && !State.WaitingForCallSteadyResponse)
                SubState = GameSubState.Idle;

            // DiscardDrawnRsp arrived
            if (SubState == GameSubState.WaitingDiscardRsp && !State.HasDrawnCard)
            {
                if (SkillTypePending > 0)
                {
                    SubState = SkillTypePending switch
                    {
                        2 => GameSubState.SkillPeekSlot,
                        3 => GameSubState.SkillSpyTarget,
                        4 => GameSubState.SkillSwapMySlot,
                        _ => GameSubState.Idle
                    };
                }
                else if (SkillTypePending == -1)
                {
                    SkillTypePending = 0;
                    DoSkipSkill();
                    return;
                }
                else SubState = GameSubState.Idle;
            }

            // ReplaceWithDrawnRsp arrived
            if (SubState == GameSubState.WaitingReplaceRsp && !State.HasDrawnCard)
                SubState = GameSubState.Idle;

            // UseSkillRsp arrived
            if (SubState == GameSubState.WaitingSkillRsp && !State.WaitingForSkillResponse)
            {
                bool skillStillPending = State.PendingSkillCardId != 0
                    && GameState.IsPlayableSkill(State.PendingSkillCardSkill);
                if (skillStillPending)
                {
                    SkillTypePending = State.PendingSkillCardSkill;
                    SkillTypeJustCompleted = 0;
                    SkillMySlot = -1;
                    SkillTargetSlot = -1;
                    SkillTargetPlayerId = 0;
                    SubState = SkillTypePending switch
                    {
                        2 => GameSubState.SkillPeekSlot,
                        3 => GameSubState.SkillSpyTarget,
                        4 => GameSubState.SkillSwapMySlot,
                        _ => GameSubState.Idle
                    };
                }
                else if (SkillTypeJustCompleted == 2) // PeekSelf
                {
                    if (SkillMySlot >= 0 && SkillMySlot < State.MyCards.Count)
                    {
                        State.MyCards[SkillMySlot].IsKnown = true;
                        State.MyCards[SkillMySlot].Value = State.LastPeekedValue;
                    }
                }
                else if (SkillTypeJustCompleted == 4) // Swap
                {
                    State.ApplyOwnSwapVisibility(SkillMySlot);
                }
                if (!skillStillPending)
                {
                    SkillTypeJustCompleted = 0;
                    SubState = GameSubState.Idle;
                }
            }

            // My turn + Idle → show main menu
            if (State.IsMyTurn && SubState == GameSubState.Idle
                && State.PendingSkillCardId != 0
                && GameState.IsPlayableSkill(State.PendingSkillCardSkill))
            {
                SkillTypePending = State.PendingSkillCardSkill;
                SkillTypeJustCompleted = 0;
                SkillMySlot = -1;
                SkillTargetSlot = -1;
                SkillTargetPlayerId = 0;
                SubState = SkillTypePending switch
                {
                    2 => GameSubState.SkillPeekSlot,
                    3 => GameSubState.SkillSpyTarget,
                    4 => GameSubState.SkillSwapMySlot,
                    _ => GameSubState.Idle
                };
                StateChanged?.Invoke();
            }

            if (State.IsMyTurn && SubState == GameSubState.Idle
                && State.HasDrawnCard
                && !State.WaitingForDrawResponse)
            {
                SubState = GameSubState.AwaitingDrawnDecision;
                StateChanged?.Invoke();
            }

            if (State.IsMyTurn && SubState == GameSubState.Idle
                && !State.HasDrawnCard
                && !State.WaitingForDrawResponse
                && !State.WaitingForTakeResponse
                && !State.WaitingForCallSteadyResponse)
            {
                SubState = GameSubState.AwaitingMainInput;
                StateChanged?.Invoke();
            }

            if (SubState != previousSubState)
                StateChanged?.Invoke();
        }

        void RestoreSubStateFromSynchronizedState()
        {
            SkillTypePending = 0;
            SkillTypeJustCompleted = 0;
            SkillMySlot = -1;
            SkillTargetSlot = -1;
            SkillTargetPlayerId = 0;

            if (State.Phase != GamePhase.Playing || !State.IsMyTurn)
            {
                SubState = GameSubState.Idle;
                return;
            }

            if (State.PendingSkillCardId != 0
                && GameState.IsPlayableSkill(State.PendingSkillCardSkill))
            {
                SkillTypePending = State.PendingSkillCardSkill;
                SubState = SkillTypePending switch
                {
                    2 => GameSubState.SkillPeekSlot,
                    3 => GameSubState.SkillSpyTarget,
                    4 => GameSubState.SkillSwapMySlot,
                    _ => GameSubState.Idle
                };
                return;
            }

            if (State.HasDrawnCard)
            {
                SubState = GameSubState.AwaitingDrawnDecision;
                return;
            }

            if (!State.WaitingForDrawResponse
                && !State.WaitingForTakeResponse
                && !State.WaitingForCallSteadyResponse
                && !State.WaitingForSkillResponse)
            {
                SubState = GameSubState.AwaitingMainInput;
                return;
            }

            SubState = GameSubState.Idle;
        }

        public void Dispose()
        {
            _running = false;
            Gateway?.Dispose();
        }
    }
}
