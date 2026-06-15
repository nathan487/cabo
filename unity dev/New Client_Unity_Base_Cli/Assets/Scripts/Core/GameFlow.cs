using System;
using System.Collections.Generic;
using Game.Messages;
using UnityEngine;

namespace Cabo.Client
{
    /// <summary>
    /// Main game flow state machine. Direct C# translation of CLI's ClientApp::gameLoop.
    /// drain-then-decide: process ALL pending messages before any UI decision.
    /// </summary>
    public enum FlowState
    {
        Home, Connecting, RoomFlow, WaitingRoom, Playing, RoundReveal, GameOver
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

        string _nickname = "玩家";
        bool _running = true;
        bool _wasConnected;

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
            if (Gateway.IsConnected)
            {
                _wasConnected = false;
                Gateway.Disconnect();
            }

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

            Debug.Log($"[GameFlow] ProcessServerMessage {msg.PayloadCase}");
            State.UpdateFromMessage(msg);

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
            SkillTypePending = useSkill ? State.DrawnCardSkill : (State.DrawnCardSkill > 0 ? -1 : 0);
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
            switch (SkillTypePending)
            {
                case 2: Gateway.SendUseSkillPeekSelf(State.MyPlayerId, State.RoomId, SkillMySlot); break;
                case 3: Gateway.SendUseSkillSpy(State.MyPlayerId, State.RoomId, SkillTargetPlayerId, SkillTargetSlot); break;
                case 4: Gateway.SendUseSkillSwap(State.MyPlayerId, State.RoomId, SkillTargetPlayerId, SkillMySlot, SkillTargetSlot); break;
            }
            SkillTypeJustCompleted = SkillTypePending;
            SkillTypePending = 0;
            State.WaitingForSkillResponse = true;
            SubState = GameSubState.WaitingSkillRsp;
            StateChanged?.Invoke();
        }

        public void DoSkipSkill()
        {
            Gateway.SendSkipSkill(State.MyPlayerId, State.RoomId);
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
                ConnectedAddress = "";
                LastConnectError = "已断开服务器连接。";
                State.ReturnHome();
                Flow = FlowState.Home;
                SubState = GameSubState.Idle;
                StateChanged?.Invoke();
            }
            else if (!_wasConnected && Gateway.IsConnected)
            {
                _wasConnected = true;
            }

            // Drain all pending TCP messages before the state machine decides what UI/actions are valid.
            Gateway.DrainMessages(ProcessServerMessage);

            if (Flow != FlowState.Playing) return;

            // Step 1: Check transitions
            CheckTransitions();
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
                if (SkillTypeJustCompleted == 2) // PeekSelf
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
                SkillTypeJustCompleted = 0;
                SubState = GameSubState.Idle;
            }

            // My turn + Idle → show main menu
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

        public void Dispose()
        {
            _running = false;
            Gateway?.Dispose();
        }
    }
}
