using System.Collections.Generic;
using Game.Messages;
using Game.Room;
using Game.Game;
using Game.Common;
using UnityEngine;

namespace Cabo.Client
{
    public enum GamePhase { Lobby, WaitingRoom, Playing, RoundReveal, GameOver }

    public class CardState
    {
        public int SlotIndex;
        public bool IsKnown;
        public int Value;
    }

    public class PlayerInfo
    {
        public long PlayerId;
        public string Nickname;
        public int SeatId;
        public int TotalScore;
        public int CardCount;
        public bool IsReady;
        public bool IsHost;
    }

    public class RoundResult
    {
        public long PlayerId;
        public string Nickname;
        public List<int> CardValues = new();
        public int HandTotal, Penalty, RoundScore, CumulativeScore;
        public bool IsSteadyCaller, IsLowest, IsKamikaze;
    }

    public class FinalRank
    {
        public int Rank;
        public long PlayerId;
        public string Nickname;
        public int FinalScore;
        public bool IsWinner;
    }

    /// <summary>
    /// Client-side game state. Mirrors server state.
    /// Direct C# translation of CLI's GameState.h/cpp.
    /// </summary>
    public class GameState
    {
        // Connection
        public long MyPlayerId;
        public long RoomId;
        public string RoomCode;

        // Phase
        public GamePhase Phase = GamePhase.Lobby;

        // Players (seat order)
        public List<PlayerInfo> Players = new();

        // My hand
        public List<CardState> MyCards = new();

        // Piles
        public int DrawPileCount;
        public int DiscardPileCount;
        public int DiscardTopValue = -1;

        // Turn
        public long CurrentPlayerId;
        public int RoundNumber, TurnNumber;

        // Draw state
        public bool HasDrawnCard;
        public int DrawnCardValue, DrawnCardSkill;

        // Request waiting flags
        public bool WaitingForDrawResponse;
        public bool WaitingForTakeResponse;
        public bool WaitingForCallSteadyResponse;
        public bool WaitingForSkillResponse;

        // Final round
        public bool IsFinalRound;
        public int FinalRoundRemaining;

        // Inter-round
        public bool GameStartConfirmed;
        public bool RoundJustRevealed;

        // Scoring
        public List<RoundResult> LastRoundResults = new();
        public List<FinalRank> FinalRankings = new();

        // Skill results
        public int LastPeekedValue = -1;
        public bool LastSwapOccurred;

        // Action broadcast message (printed after render)
        public string LastActionMessage;

        // Helpers
        public bool IsMyTurn => CurrentPlayerId == MyPlayerId;

        public int MyPlayerIndex
        {
            get
            {
                for (int i = 0; i < Players.Count; i++)
                    if (Players[i].PlayerId == MyPlayerId) return i;
                return -1;
            }
        }

        public List<int> OpponentIndices
        {
            get
            {
                int my = MyPlayerIndex;
                if (my < 0 || Players.Count < 4) return new();
                int n = Players.Count;
                return new() { (my + 2) % n, (my + 3) % n, (my + 1) % n };
            }
        }

        // ── Message Handlers ──

        public void UpdateFromMessage(ServerMessage msg)
        {
            switch (msg.PayloadCase)
            {
                case ServerMessage.PayloadOneofCase.CreateRoomRsp:
                    HandleCreateRoom(msg.CreateRoomRsp); break;
                case ServerMessage.PayloadOneofCase.JoinRoomRsp:
                    HandleJoinRoom(msg.JoinRoomRsp); break;
                case ServerMessage.PayloadOneofCase.RoomStateNotify:
                    HandleRoomState(msg.RoomStateNotify); break;
                case ServerMessage.PayloadOneofCase.PlayerJoinNotify:
                    HandlePlayerJoin(msg.PlayerJoinNotify); break;
                case ServerMessage.PayloadOneofCase.PlayerReadyNotify:
                    HandlePlayerReady(msg.PlayerReadyNotify); break;
                case ServerMessage.PayloadOneofCase.ReadyRsp:
                    HandleReady(msg.ReadyRsp); break;
                case ServerMessage.PayloadOneofCase.StartGameRsp:
                    HandleStartGame(msg.StartGameRsp); break;
                case ServerMessage.PayloadOneofCase.RoomStartNotify:
                    HandleRoomStart(msg.RoomStartNotify); break;
                case ServerMessage.PayloadOneofCase.GameStartNotify:
                    HandleGameStart(msg.GameStartNotify); break;
                case ServerMessage.PayloadOneofCase.TurnStartNotify:
                    HandleTurnStart(msg.TurnStartNotify); break;
                case ServerMessage.PayloadOneofCase.DrawCardRsp:
                    HandleDrawCard(msg.DrawCardRsp); break;
                case ServerMessage.PayloadOneofCase.DiscardDrawnRsp:
                    HandleDiscardDrawn(msg.DiscardDrawnRsp); break;
                case ServerMessage.PayloadOneofCase.ReplaceWithDrawnRsp:
                    HandleReplaceWithDrawn(msg.ReplaceWithDrawnRsp); break;
                case ServerMessage.PayloadOneofCase.TakeFromDiscardRsp:
                    HandleTakeFromDiscard(msg.TakeFromDiscardRsp); break;
                case ServerMessage.PayloadOneofCase.UseSkillRsp:
                    HandleUseSkill(msg.UseSkillRsp); break;
                case ServerMessage.PayloadOneofCase.CallSteadyRsp:
                    HandleCallSteady(msg.CallSteadyRsp); break;
                case ServerMessage.PayloadOneofCase.ActionResultNotify:
                    HandleActionResult(msg.ActionResultNotify); break;
                case ServerMessage.PayloadOneofCase.RoundRevealNotify:
                    HandleRoundReveal(msg.RoundRevealNotify); break;
                case ServerMessage.PayloadOneofCase.ScoreUpdateNotify:
                    HandleScoreUpdate(msg.ScoreUpdateNotify); break;
                case ServerMessage.PayloadOneofCase.GameOverNotify:
                    HandleGameOver(msg.GameOverNotify); break;
            }
        }

        void HandleCreateRoom(CreateRoomRsp rsp)
        {
            if (rsp.Error?.Code != 0) return;
            RoomId = rsp.RoomId; MyPlayerId = rsp.PlayerId;
            RoomCode = rsp.RoomCode; Phase = GamePhase.WaitingRoom;
            Debug.Log($"[GameState] CreateRoom: {RoomCode} player={MyPlayerId}");
        }

        void HandleJoinRoom(JoinRoomRsp rsp)
        {
            if (rsp.Error?.Code != 0) return;
            RoomId = rsp.RoomId; MyPlayerId = rsp.PlayerId;
            Phase = GamePhase.WaitingRoom;
            Debug.Log($"[GameState] JoinRoom: {RoomId} player={MyPlayerId}");
        }

        void HandleRoomState(RoomStateNotify notify)
        {
            var room = notify.Room;
            if (room == null) return;
            RoomId = room.RoomId; RoomCode = room.RoomCode;
            Players.Clear();
            foreach (var p in room.Players)
                Players.Add(new PlayerInfo
                {
                    PlayerId = p.PlayerId, Nickname = p.Nickname,
                    SeatId = p.SeatId, IsReady = p.IsReady,
                    IsHost = p.IsHost, TotalScore = p.TotalScore
                });
        }

        void HandlePlayerJoin(PlayerJoinNotify notify)
        {
            var pj = notify.Player;
            if (pj != null && !Players.Exists(p => p.PlayerId == pj.PlayerId))
                Players.Add(new PlayerInfo
                {
                    PlayerId = pj.PlayerId, Nickname = pj.Nickname,
                    SeatId = pj.SeatId, IsReady = pj.IsReady, IsHost = pj.IsHost
                });
        }

        void HandlePlayerReady(PlayerReadyNotify notify)
        {
            var p = Players.Find(x => x.PlayerId == notify.PlayerId);
            if (p != null) p.IsReady = notify.IsReady;
        }

        void HandleReady(ReadyRsp rsp) { }

        void HandleStartGame(StartGameRsp rsp)
        {
            if (rsp.Error?.Code == 0) GameStartConfirmed = true;
        }

        void HandleRoomStart(RoomStartNotify notify) { }

        void HandleGameStart(GameStartNotify notify)
        {
            Phase = GamePhase.Playing;
            RoundNumber = notify.RoundNumber;
            CurrentPlayerId = notify.FirstPlayerId;
            IsFinalRound = false; FinalRoundRemaining = 0;

            if (notify.YourView != null)
            {
                MyCards.Clear();
                foreach (var oc in notify.YourView.OwnCards)
                    MyCards.Add(new CardState
                    {
                        SlotIndex = oc.SlotIndex, IsKnown = oc.IsKnown,
                        Value = oc.IsKnown ? oc.Value : 0
                    });
                DrawPileCount = notify.YourView.DrawPile?.Count ?? 0;
                DiscardPileCount = notify.YourView.DiscardPile?.Count ?? 0;
                if (notify.YourView.DiscardPile?.TopCard != null)
                    DiscardTopValue = notify.YourView.DiscardPile.TopCard.Value;
                else DiscardTopValue = -1;

                foreach (var oh in notify.YourView.OpponentHands)
                {
                    var p = Players.Find(x => x.PlayerId == oh.PlayerId);
                    if (p != null) p.CardCount = oh.CardCount;
                }
            }
            Debug.Log($"[GameState] GameStart: round={RoundNumber} firstPlayer={CurrentPlayerId} cards={MyCards.Count}");
        }

        void HandleTurnStart(TurnStartNotify notify)
        {
            CurrentPlayerId = notify.CurrentPlayerId;
            TurnNumber = notify.TurnNumber;
            RoundNumber = notify.RoundNumber;

            if (notify.CurrentPlayerId == MyPlayerId)
            {
                HasDrawnCard = false; DrawnCardValue = 0; DrawnCardSkill = 0;
                WaitingForDrawResponse = false; WaitingForTakeResponse = false;
                WaitingForCallSteadyResponse = false; WaitingForSkillResponse = false;
            }
            if (notify.Phase == Game.Common.GamePhase.FinalRound)
            { IsFinalRound = true; FinalRoundRemaining = notify.FinalRoundRemaining; }

            DrawPileCount = notify.DrawPile?.Count ?? 0;
            DiscardPileCount = notify.DiscardPile?.Count ?? 0;
            DiscardTopValue = notify.DiscardPile?.TopCard?.Value ?? -1;
        }

        void HandleDrawCard(DrawCardRsp rsp)
        {
            WaitingForDrawResponse = false;
            if (rsp.Error?.Code == 0)
            {
                HasDrawnCard = true; DrawnCardValue = rsp.Value;
                DrawnCardSkill = (int)(rsp.Skill);
            }
        }

        void HandleDiscardDrawn(DiscardDrawnRsp rsp)
        {
            if (rsp.Error?.Code == 0) { HasDrawnCard = false; }
        }

        void HandleReplaceWithDrawn(ReplaceWithDrawnRsp rsp)
        {
            if (rsp.Error?.Code != 0 || rsp.ExchangeResult == null) return;
            var ex = rsp.ExchangeResult;
            if (ex.Success)
            {
                if (ex.SelectedSlotIndices?.Count > 0)
                {
                    if (ex.SelectedSlotIndices.Count > 1)
                    {
                        // Multi-success: rebuild cards
                        var newCards = new List<CardState>();
                        for (int i = 0; i < MyCards.Count; i++)
                            if (!ex.SelectedSlotIndices.Contains(i))
                                { var c = MyCards[i]; c.SlotIndex = newCards.Count; newCards.Add(c); }
                        newCards.Add(new CardState { SlotIndex = newCards.Count, Value = ex.IncomingCardValue, IsKnown = true });
                        MyCards = newCards;
                    }
                    else
                    {
                        int slot = ex.SelectedSlotIndices[0];
                        if (slot >= 0 && slot < MyCards.Count)
                        { MyCards[slot].Value = ex.IncomingCardValue; MyCards[slot].IsKnown = true; }
                    }
                }
            }
            else
            {
                // Failure: add card to hand
                MyCards.Add(new CardState { SlotIndex = MyCards.Count, Value = ex.IncomingCardValue, IsKnown = true });
            }
            HasDrawnCard = false;
        }

        void HandleTakeFromDiscard(TakeFromDiscardRsp rsp)
        {
            WaitingForTakeResponse = false;
            if (rsp.Error?.Code != 0 || rsp.ExchangeResult == null) return;
            var ex = rsp.ExchangeResult;
            if (ex.Success)
            {
                if (ex.SelectedSlotIndices?.Count > 0)
                {
                    if (ex.SelectedSlotIndices.Count > 1)
                    {
                        var newCards = new List<CardState>();
                        for (int i = 0; i < MyCards.Count; i++)
                            if (!ex.SelectedSlotIndices.Contains(i))
                                { var c = MyCards[i]; c.SlotIndex = newCards.Count; newCards.Add(c); }
                        newCards.Add(new CardState { SlotIndex = newCards.Count, Value = ex.IncomingCardValue, IsKnown = true });
                        MyCards = newCards;
                    }
                    else
                    {
                        int slot = ex.SelectedSlotIndices[0];
                        if (slot >= 0 && slot < MyCards.Count)
                        { MyCards[slot].Value = ex.IncomingCardValue; MyCards[slot].IsKnown = true; }
                    }
                }
            }
            else
            {
                MyCards.Add(new CardState { SlotIndex = MyCards.Count, Value = ex.IncomingCardValue, IsKnown = true });
            }
        }

        void HandleUseSkill(UseSkillRsp rsp)
        {
            WaitingForSkillResponse = false;
            if (rsp.Error?.Code == 0)
            {
                LastPeekedValue = rsp.PeekedValue;
                LastSwapOccurred = rsp.SwapOccurred;
            }
        }

        void HandleCallSteady(CallSteadyRsp rsp)
        {
            WaitingForCallSteadyResponse = false;
        }

        void HandleActionResult(ActionResultNotify ar)
        {
            DrawPileCount = ar.DrawPile?.Count ?? 0;
            DiscardPileCount = ar.DiscardPile?.Count ?? 0;
            DiscardTopValue = ar.DiscardPile?.TopCard?.Value ?? -1;

            // Note: PlayerHands not yet in generated C# proto — opponent counts
            // updated via GameStartNotify per round

            // Swap: update own card state
            if (ar.SwapOccurred)
            {
                if (ar.SourcePlayerId == MyPlayerId)
                {
                    int slot = ar.SourceSlot;
                    if (slot >= 0 && slot < MyCards.Count) MyCards[slot].IsKnown = false;
                }
                if (ar.TargetPlayerId == MyPlayerId)
                {
                    int slot = ar.TargetSlot;
                    if (slot >= 0 && slot < MyCards.Count) MyCards[slot].IsKnown = false;
                }
            }

            if (ar.TurnEnded) { HasDrawnCard = false; }

            LastActionMessage = BuildActionMessage(ar);
        }

        void HandleRoundReveal(RoundRevealNotify rrn)
        {
            Phase = GamePhase.RoundReveal; RoundJustRevealed = true;
            LastRoundResults.Clear();
            foreach (var sc in rrn.Scores)
            {
                var rr = new RoundResult
                {
                    PlayerId = sc.PlayerId, HandTotal = sc.HandTotal, Penalty = sc.Penalty,
                    RoundScore = sc.RoundScore, CumulativeScore = sc.CumulativeScore,
                    IsSteadyCaller = sc.IsSteadyCaller, IsLowest = sc.IsLowest, IsKamikaze = sc.IsKamikaze
                };
                var pl = Players.Find(p => p.PlayerId == sc.PlayerId);
                if (pl != null) rr.Nickname = pl.Nickname;
                foreach (var rh in rrn.RevealedHands)
                    if (rh.PlayerId == sc.PlayerId) { rr.CardValues.AddRange(rh.CardValues); break; }
                LastRoundResults.Add(rr);
            }
            foreach (var sc in rrn.Scores)
            {
                var pl = Players.Find(p => p.PlayerId == sc.PlayerId);
                if (pl != null) pl.TotalScore = sc.CumulativeScore;
            }
        }

        void HandleScoreUpdate(ScoreUpdateNotify notify)
        {
            foreach (var si in notify.Scores)
            {
                var pl = Players.Find(p => p.PlayerId == si.PlayerId);
                if (pl != null) pl.TotalScore = si.TotalScore;
            }
        }

        void HandleGameOver(GameOverNotify go)
        {
            Phase = GamePhase.GameOver;
            FinalRankings.Clear();
            foreach (var r in go.Rankings)
                FinalRankings.Add(new FinalRank
                {
                    Rank = r.Rank, PlayerId = r.PlayerId, Nickname = r.Nickname,
                    FinalScore = r.FinalScore, IsWinner = r.IsWinner
                });
        }

        string BuildActionMessage(ActionResultNotify ar)
        {
            string name = "Player";
            var pl = Players.Find(p => p.PlayerId == ar.SourcePlayerId);
            if (pl != null) name = pl.Nickname;
            string you = ar.SourcePlayerId == MyPlayerId ? " (You)" : "";

            switch (ar.ActionType)
            {
                case ActionType.Draw: return $">>> {name}{you} drew a card";
                case ActionType.DiscardDrawn:
                    string skill = ar.SkillUsed switch
                    {
                        SkillType.PeekSelf => " (Peek Self)",
                        SkillType.Spy => " (Spy)",
                        SkillType.Swap => " (Swap)",
                        _ => ""
                    };
                    return $">>> {name}{you} discarded the card{skill}";
                case ActionType.ReplaceWithDrawn:
                    if (ar.ExchangeResult != null)
                        return ar.ExchangeResult.Success
                            ? $">>> {name}{you} replaced {ar.ExchangeResult.DiscardedCount} card(s)"
                            : $">>> {name}{you} replace FAILED — card added to hand";
                    break;
                case ActionType.TakeFromDiscard:
                    if (ar.ExchangeResult != null)
                        return ar.ExchangeResult.Success
                            ? $">>> {name}{you} took from discard, replaced {ar.ExchangeResult.DiscardedCount} card(s)"
                            : $">>> {name}{you} take from discard FAILED";
                    break;
                case ActionType.UseSkill:
                    if (ar.SkillUsed == SkillType.PeekSelf)
                        return $">>> {name}{you} peeked at own slot {ar.SourceSlot}";
                    if (ar.SkillUsed == SkillType.Spy)
                    {
                        string tgt = "Player";
                        var tp = Players.Find(p => p.PlayerId == ar.TargetPlayerId);
                        if (tp != null) tgt = tp.Nickname;
                        return $">>> {name}{you} spied on {tgt}'s slot {ar.TargetSlot}";
                    }
                    if (ar.SkillUsed == SkillType.Swap)
                    {
                        string tgt = "Player";
                        var tp = Players.Find(p => p.PlayerId == ar.TargetPlayerId);
                        if (tp != null) tgt = tp.Nickname;
                        return $">>> {name}{you} swapped slot {ar.SourceSlot} with {tgt}'s slot {ar.TargetSlot}";
                    }
                    break;
                case ActionType.CallSteady:
                    return $">>> {name}{you} called CABO!";
            }
            return "";
        }
    }
}
