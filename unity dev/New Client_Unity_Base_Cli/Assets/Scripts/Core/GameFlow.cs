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
        Connecting, RoomFlow, WaitingRoom, Playing, RoundReveal, GameOver
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
        public FlowState Flow { get; private set; } = FlowState.Connecting;

        public GameSubState SubState { get; private set; } = GameSubState.Idle;
        public int SkillTypePending;     // 2=PeekSelf, 3=Spy, 4=Swap
        public int SkillTypeJustCompleted;
        public int SkillMySlot, SkillTargetSlot;
        public long SkillTargetPlayerId;

        public event Action StateChanged;  // Fire when UI should refresh

        string _host, _nickname = "Player";
        int _port;
        bool _running = true;

        public GameFlow(NetworkGateway gw) { Gateway = gw; }

        public async void Connect(string host, int port, string nickname = null)
        {
            _host = host; _port = port;
            if (!string.IsNullOrWhiteSpace(nickname))
                _nickname = NormalizeNickname(nickname);
            Flow = FlowState.Connecting; StateChanged?.Invoke();
            await Gateway.ConnectAsync(host, port);
            if (!Gateway.IsConnected) return;
            Flow = FlowState.RoomFlow; StateChanged?.Invoke();
        }

        // ── Room Actions ──

        public void CreateRoom(string nickname)
        {
            _nickname = NormalizeNickname(nickname);
            Gateway.SendCreateRoom(_nickname);
            Flow = FlowState.WaitingRoom; StateChanged?.Invoke();
        }

        public void JoinRoom(string roomCode, string nickname)
        {
            _nickname = NormalizeNickname(nickname);
            Gateway.SendJoinRoom(roomCode, _nickname);
            Flow = FlowState.WaitingRoom; StateChanged?.Invoke();
        }

        static string NormalizeNickname(string nickname)
        {
            var trimmed = nickname?.Trim();
            return string.IsNullOrEmpty(trimmed) ? "Player" : trimmed;
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

        public void ProcessServerMessage(ServerMessage msg)
        {
            var previousFlow = Flow;
            var previousPhase = State.Phase;

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
                    if (SkillMySlot >= 0 && SkillMySlot < State.MyCards.Count)
                        State.MyCards[SkillMySlot].IsKnown = false;
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
