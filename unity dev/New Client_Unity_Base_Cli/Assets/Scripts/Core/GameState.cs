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

    public class RoomChatMessage
    {
        public long RoomId;
        public long MessageId;
        public long SenderPlayerId;
        public string SenderNickname;
        public RoomChatType Type = RoomChatType.Unknown;
        public string Text;
        public string StickerPack;
        public string StickerName;
        public long ServerTimeMs;
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
        public long SteadyCallerId;

        // Inter-round
        public bool GameStartConfirmed;
        public bool RoundJustRevealed;

        // Scoring
        public List<RoundResult> LastRoundResults = new();
        public List<FinalRank> FinalRankings = new();

        // Room chat
        public List<RoomChatMessage> RoomChatMessages = new();
        public string LastRoomChatError = "";

        // Skill results
        public int LastPeekedValue = -1;
        public bool LastSwapOccurred;

        // Action broadcast message (printed after render)
        public string LastActionMessage;
        public long LastActionSequence;
        public ActionType LastActionType = ActionType.Unknown;
        public SkillType LastActionSkill = SkillType.Unknown;
        public long LastActionSourcePlayerId;
        public long LastActionTargetPlayerId;
        public int LastActionSourceSlot;
        public int LastActionTargetSlot;
        public bool LastActionSwapOccurred;
        public bool LastActionTurnEnded;
        public bool LastActionExchangeSucceeded;
        public int LastActionIncomingCardValue;
        public int LastActionDiscardedCount;
        public bool LastActionAttemptedMultiCard;
        public int LastActionAddedCardCount;
        public bool LastActionDrewExtraPenaltyCard;
        public List<int> LastActionSelectedSlots = new();

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
                case ServerMessage.PayloadOneofCase.LeaveRoomRsp:
                    HandleLeaveRoom(msg.LeaveRoomRsp); break;
                case ServerMessage.PayloadOneofCase.RoomStateNotify:
                    HandleRoomState(msg.RoomStateNotify); break;
                case ServerMessage.PayloadOneofCase.PlayerJoinNotify:
                    HandlePlayerJoin(msg.PlayerJoinNotify); break;
                case ServerMessage.PayloadOneofCase.PlayerLeaveNotify:
                    HandlePlayerLeave(msg.PlayerLeaveNotify); break;
                case ServerMessage.PayloadOneofCase.PlayerReadyNotify:
                    HandlePlayerReady(msg.PlayerReadyNotify); break;
                case ServerMessage.PayloadOneofCase.ReadyRsp:
                    HandleReady(msg.ReadyRsp); break;
                case ServerMessage.PayloadOneofCase.StartGameRsp:
                    HandleStartGame(msg.StartGameRsp); break;
                case ServerMessage.PayloadOneofCase.RoomStartNotify:
                    HandleRoomStart(msg.RoomStartNotify); break;
                case ServerMessage.PayloadOneofCase.RoomChatRsp:
                    HandleRoomChatRsp(msg.RoomChatRsp); break;
                case ServerMessage.PayloadOneofCase.RoomChatNotify:
                    HandleRoomChatNotify(msg.RoomChatNotify); break;
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

        void HandleLeaveRoom(LeaveRoomRsp rsp)
        {
            if (rsp.Error?.Code != 0) return;
            ReturnHome();
        }

        void HandleRoomState(RoomStateNotify notify)
        {
            var room = notify.Room;
            if (room == null) return;
            RoomId = room.RoomId; RoomCode = room.RoomCode;

            if (Phase == GamePhase.Playing)
            {
                foreach (var p in room.Players)
                    UpsertPlayer(p.PlayerId, p.Nickname, p.SeatId, p.IsReady, p.IsHost, p.TotalScore);
                return;
            }

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

        void HandlePlayerLeave(PlayerLeaveNotify notify)
        {
            if (notify.PlayerId == MyPlayerId)
            {
                ReturnHome();
                return;
            }

            Players.RemoveAll(p => p.PlayerId == notify.PlayerId);
            if (notify.NewHostPlayerId > 0)
            {
                foreach (var player in Players)
                    player.IsHost = player.PlayerId == notify.NewHostPlayerId;
            }
        }

        void HandlePlayerReady(PlayerReadyNotify notify)
        {
            var p = Players.Find(x => x.PlayerId == notify.PlayerId);
            if (p != null) p.IsReady = notify.IsReady;
        }

        void HandleReady(ReadyRsp rsp)
        {
            if (rsp.Error?.Code != 0) return;
            var me = Players.Find(x => x.PlayerId == MyPlayerId);
            if (me != null) me.IsReady = rsp.IsReady;
        }

        void HandleStartGame(StartGameRsp rsp)
        {
            if (rsp.Error?.Code == 0) GameStartConfirmed = true;
        }

        void HandleRoomStart(RoomStartNotify notify) { }

        void HandleRoomChatRsp(RoomChatRsp rsp)
        {
            LastRoomChatError = rsp.Error?.Code == 0
                ? ""
                : (string.IsNullOrEmpty(rsp.Error?.Message) ? "聊天发送失败" : rsp.Error.Message);
        }

        void HandleRoomChatNotify(RoomChatNotify notify)
        {
            if (notify == null)
                return;
            if (RoomId > 0 && notify.RoomId != RoomId)
                return;
            if (RoomChatMessages.Exists(m => m.MessageId == notify.MessageId && m.RoomId == notify.RoomId))
                return;

            RoomChatMessages.Add(new RoomChatMessage
            {
                RoomId = notify.RoomId,
                MessageId = notify.MessageId,
                SenderPlayerId = notify.SenderPlayerId,
                SenderNickname = notify.SenderNickname,
                Type = notify.Type,
                Text = notify.Text,
                StickerPack = notify.StickerPack,
                StickerName = notify.StickerName,
                ServerTimeMs = notify.ServerTimeMs
            });

            while (RoomChatMessages.Count > 50)
                RoomChatMessages.RemoveAt(0);
        }

        void HandleGameStart(GameStartNotify notify)
        {
            Phase = GamePhase.Playing;
            RoundJustRevealed = false;
            GameStartConfirmed = false;
            RoundNumber = notify.RoundNumber;
            CurrentPlayerId = notify.FirstPlayerId;
            IsFinalRound = false; FinalRoundRemaining = 0; SteadyCallerId = 0;
            HasDrawnCard = false; DrawnCardValue = 0; DrawnCardSkill = 0;
            WaitingForDrawResponse = false; WaitingForTakeResponse = false;
            WaitingForCallSteadyResponse = false; WaitingForSkillResponse = false;
            foreach (var player in Players)
                player.IsReady = false;

            if (notify.YourView != null)
            {
                MyCards.Clear();
                foreach (var oc in notify.YourView.OwnCards)
                    MyCards.Add(new CardState
                    {
                        SlotIndex = oc.SlotIndex, IsKnown = oc.IsKnown,
                        Value = oc.IsKnown ? oc.Value : 0
                    });
                var me = Players.Find(x => x.PlayerId == MyPlayerId);
                if (me != null)
                    me.CardCount = MyCards.Count;
                else
                    Players.Add(new PlayerInfo
                    {
                        PlayerId = MyPlayerId,
                        Nickname = "你",
                        SeatId = 0,
                        CardCount = MyCards.Count,
                        IsHost = true
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
                    else
                    {
                        Players.Add(new PlayerInfo
                        {
                            PlayerId = oh.PlayerId,
                            Nickname = $"玩家 {Players.Count + 1}",
                            SeatId = Players.Count,
                            CardCount = oh.CardCount
                        });
                    }
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
                AddFailedExchangeCards(ex);
            }
            HasDrawnCard = false;
            SyncMyCardCount();
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
                AddFailedExchangeCards(ex);
            }
            SyncMyCardCount();
        }

        void AddFailedExchangeCards(ExchangeAttemptResult ex)
        {
            MyCards.Add(new CardState
            {
                SlotIndex = MyCards.Count,
                Value = ex.IncomingCardValue,
                IsKnown = true
            });

            if (!ex.DrewExtraPenaltyCard)
                return;

            MyCards.Add(new CardState
            {
                SlotIndex = MyCards.Count,
                Value = 0,
                IsKnown = false
            });
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
            LastActionSequence = ar.ServerSeq != 0 ? ar.ServerSeq : LastActionSequence + 1;
            LastActionType = ar.ActionType;
            LastActionSkill = ar.SkillUsed;
            LastActionSourcePlayerId = ar.SourcePlayerId;
            LastActionTargetPlayerId = ar.TargetPlayerId;
            LastActionSourceSlot = ar.SourceSlot;
            LastActionTargetSlot = ar.TargetSlot;
            LastActionSwapOccurred = ar.SwapOccurred;
            LastActionTurnEnded = ar.TurnEnded;
            LastActionExchangeSucceeded = ar.ExchangeResult?.Success ?? false;
            LastActionIncomingCardValue = ar.ExchangeResult?.IncomingCardValue ?? -1;
            LastActionDiscardedCount = ar.ExchangeResult?.DiscardedCount ?? 0;
            LastActionAttemptedMultiCard = ar.ExchangeResult?.AttemptedMultiCard ?? false;
            LastActionAddedCardCount = ar.ExchangeResult?.AddedCardCount ?? 0;
            LastActionDrewExtraPenaltyCard = ar.ExchangeResult?.DrewExtraPenaltyCard ?? false;
            LastActionSelectedSlots.Clear();
            if (ar.ExchangeResult?.SelectedSlotIndices != null)
                LastActionSelectedSlots.AddRange(ar.ExchangeResult.SelectedSlotIndices);
            if (ar.ActionType == ActionType.CallSteady)
                SteadyCallerId = ar.SourcePlayerId;

            DrawPileCount = ar.DrawPile?.Count ?? 0;
            DiscardPileCount = ar.DiscardPile?.Count ?? 0;
            DiscardTopValue = ar.DiscardPile?.TopCard?.Value ?? -1;
            if (ar.PlayerHands != null && ar.PlayerHands.Count > 0)
                ApplyPlayerHandCounts(ar.PlayerHands);
            else
                ApplyFallbackActionHandCount(ar);

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
            SyncMyCardCount();

            LastActionMessage = BuildActionMessage(ar);
        }

        void ApplyPlayerHandCounts(IEnumerable<OpponentHandState> hands)
        {
            foreach (var hand in hands)
            {
                var player = Players.Find(p => p.PlayerId == hand.PlayerId);
                if (player != null)
                    player.CardCount = hand.CardCount;
            }
        }

        void ApplyFallbackActionHandCount(ActionResultNotify ar)
        {
            if (ar.ExchangeResult == null)
                return;

            var player = Players.Find(p => p.PlayerId == ar.SourcePlayerId);
            if (player == null)
                return;

            var ex = ar.ExchangeResult;
            if (ex.Success)
            {
                int removed = Mathf.Max(0, ex.DiscardedCount);
                int added = Mathf.Max(0, ex.AddedCardCount);
                if (removed > 0 || added > 0)
                    player.CardCount = Mathf.Max(0, player.CardCount - removed + added);
            }
            else
            {
                int added = Mathf.Max(1, ex.AddedCardCount);
                if (ex.DrewExtraPenaltyCard)
                    added = Mathf.Max(added, 2);
                player.CardCount = Mathf.Max(0, player.CardCount + added);
            }
        }

        void SyncMyCardCount()
        {
            var me = Players.Find(x => x.PlayerId == MyPlayerId);
            if (me != null)
                me.CardCount = MyCards.Count;
        }

        void HandleRoundReveal(RoundRevealNotify rrn)
        {
            Phase = GamePhase.RoundReveal; RoundJustRevealed = true;
            GameStartConfirmed = false;
            SteadyCallerId = rrn.SteadyCallerId;
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

        public void ReturnToRoomAfterGameOver()
        {
            if (Phase != GamePhase.GameOver)
                return;

            Phase = GamePhase.WaitingRoom;
            MyCards.Clear();
            DrawPileCount = 0;
            DiscardPileCount = 0;
            DiscardTopValue = -1;
            CurrentPlayerId = 0;
            RoundNumber = 0;
            TurnNumber = 0;
            HasDrawnCard = false;
            DrawnCardValue = 0;
            DrawnCardSkill = 0;
            WaitingForDrawResponse = false;
            WaitingForTakeResponse = false;
            WaitingForCallSteadyResponse = false;
            WaitingForSkillResponse = false;
            IsFinalRound = false;
            FinalRoundRemaining = 0;
            SteadyCallerId = 0;
            GameStartConfirmed = false;
            RoundJustRevealed = false;
            LastRoundResults.Clear();
            FinalRankings.Clear();
            LastPeekedValue = -1;
            LastSwapOccurred = false;
            LastActionMessage = "";
            LastActionSequence = 0;
            LastActionType = ActionType.Unknown;
            LastActionSkill = SkillType.Unknown;
            LastActionSourcePlayerId = 0;
            LastActionTargetPlayerId = 0;
            LastActionSourceSlot = -1;
            LastActionTargetSlot = -1;
            LastActionSwapOccurred = false;
            LastActionTurnEnded = false;
            LastActionExchangeSucceeded = false;
            LastActionIncomingCardValue = -1;
            LastActionDiscardedCount = 0;
            LastActionAttemptedMultiCard = false;
            LastActionAddedCardCount = 0;
            LastActionDrewExtraPenaltyCard = false;
            LastActionSelectedSlots.Clear();

            foreach (var player in Players)
            {
                player.IsReady = false;
                player.CardCount = 0;
            }
        }

        public void ReturnHome()
        {
            Phase = GamePhase.Lobby;
            MyPlayerId = 0;
            RoomId = 0;
            RoomCode = "";
            Players.Clear();
            MyCards.Clear();
            DrawPileCount = 0;
            DiscardPileCount = 0;
            DiscardTopValue = -1;
            CurrentPlayerId = 0;
            RoundNumber = 0;
            TurnNumber = 0;
            HasDrawnCard = false;
            DrawnCardValue = 0;
            DrawnCardSkill = 0;
            WaitingForDrawResponse = false;
            WaitingForTakeResponse = false;
            WaitingForCallSteadyResponse = false;
            WaitingForSkillResponse = false;
            IsFinalRound = false;
            FinalRoundRemaining = 0;
            SteadyCallerId = 0;
            GameStartConfirmed = false;
            RoundJustRevealed = false;
            LastRoundResults.Clear();
            FinalRankings.Clear();
            RoomChatMessages.Clear();
            LastRoomChatError = "";
            LastPeekedValue = -1;
            LastSwapOccurred = false;
            LastActionMessage = "";
            LastActionSequence = 0;
            LastActionType = ActionType.Unknown;
            LastActionSkill = SkillType.Unknown;
            LastActionSourcePlayerId = 0;
            LastActionTargetPlayerId = 0;
            LastActionSourceSlot = -1;
            LastActionTargetSlot = -1;
            LastActionSwapOccurred = false;
            LastActionTurnEnded = false;
            LastActionExchangeSucceeded = false;
            LastActionIncomingCardValue = -1;
            LastActionDiscardedCount = 0;
            LastActionAttemptedMultiCard = false;
            LastActionAddedCardCount = 0;
            LastActionDrewExtraPenaltyCard = false;
            LastActionSelectedSlots.Clear();
        }

        void UpsertPlayer(long playerId, string nickname, int seatId, bool isReady, bool isHost, int totalScore)
        {
            var existing = Players.Find(x => x.PlayerId == playerId);
            if (existing != null)
            {
                existing.Nickname = nickname;
                existing.SeatId = seatId;
                existing.IsReady = isReady;
                existing.IsHost = isHost;
                existing.TotalScore = totalScore;
                return;
            }

            Players.Add(new PlayerInfo
            {
                PlayerId = playerId,
                Nickname = nickname,
                SeatId = seatId,
                IsReady = isReady,
                IsHost = isHost,
                TotalScore = totalScore
            });
        }

        string BuildActionMessage(ActionResultNotify ar)
        {
            string name = "玩家";
            var pl = Players.Find(p => p.PlayerId == ar.SourcePlayerId);
            if (pl != null) name = pl.Nickname;
            string you = ar.SourcePlayerId == MyPlayerId ? "（你）" : "";

            switch (ar.ActionType)
            {
                case ActionType.Draw: return $">>> {name}{you} 抽了一张牌";
                case ActionType.DiscardDrawn:
                    string skill = ar.SkillUsed switch
                    {
                        SkillType.PeekSelf => "（看牌）",
                        SkillType.Spy => "（偷看）",
                        SkillType.Swap => "（换牌）",
                        _ => ""
                    };
                    return $">>> {name}{you} 弃掉抽到的牌{skill}";
                case ActionType.ReplaceWithDrawn:
                    if (ar.ExchangeResult != null)
                        return ar.ExchangeResult.Success
                            ? $">>> {name}{you} 替换了 {ar.ExchangeResult.DiscardedCount} 张牌"
                            : $">>> {name}{you} 替换失败，牌加入手牌";
                    break;
                case ActionType.TakeFromDiscard:
                    if (ar.ExchangeResult != null)
                        return ar.ExchangeResult.Success
                            ? $">>> {name}{you} 拿弃牌并替换了 {ar.ExchangeResult.DiscardedCount} 张牌"
                            : $">>> {name}{you} 拿弃牌失败";
                    break;
                case ActionType.UseSkill:
                    if (ar.SkillUsed == SkillType.PeekSelf)
                        return $">>> {name}{you} 查看了自己的第 {ar.SourceSlot + 1} 张牌";
                    if (ar.SkillUsed == SkillType.Spy)
                    {
                        string tgt = "玩家";
                        var tp = Players.Find(p => p.PlayerId == ar.TargetPlayerId);
                        if (tp != null) tgt = tp.Nickname;
                        return $">>> {name}{you} 偷看了 {tgt} 的第 {ar.TargetSlot + 1} 张牌";
                    }
                    if (ar.SkillUsed == SkillType.Swap)
                    {
                        string tgt = "玩家";
                        var tp = Players.Find(p => p.PlayerId == ar.TargetPlayerId);
                        if (tp != null) tgt = tp.Nickname;
                        return $">>> {name}{you} 将第 {ar.SourceSlot + 1} 张牌与 {tgt} 的第 {ar.TargetSlot + 1} 张牌交换";
                    }
                    break;
                case ActionType.CallSteady:
                    return $">>> {name}{you} 喊了 CABO！";
            }
            return "";
        }
    }
}
