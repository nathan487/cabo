using Cabo.Client;
using Game.Common;
using Game.Game;
using Game.Messages;
using Game.Room;
using NUnit.Framework;
using Game.Sync;

namespace Cabo.Client.Tests
{
    public sealed class GameStateSkillCardTests
    {
        [Test]
        public void SkillCardDiscardKeepsPendingSkillCardIdForFollowUpSkillRequest()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                DrawCardRsp = new DrawCardRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    CardId = 123,
                    Value = 9,
                    Skill = SkillType.Spy
                }
            });

            state.UpdateFromMessage(new ServerMessage
            {
                DiscardDrawnRsp = new DiscardDrawnRsp
                {
                    Error = new ErrorInfo { Code = 0 }
                }
            });

            Assert.IsFalse(state.HasDrawnCard);
            Assert.AreEqual(123, state.PendingSkillCardId);
            Assert.AreEqual((int)SkillType.Spy, state.PendingSkillCardSkill);
        }

        [Test]
        public void SuccessfulSkillResponseClearsPendingSkillCard()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                DrawCardRsp = new DrawCardRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    CardId = 456,
                    Value = 11,
                    Skill = SkillType.Swap
                }
            });
            state.UpdateFromMessage(new ServerMessage
            {
                DiscardDrawnRsp = new DiscardDrawnRsp
                {
                    Error = new ErrorInfo { Code = 0 }
                }
            });

            state.UpdateFromMessage(new ServerMessage
            {
                UseSkillRsp = new UseSkillRsp
                {
                    Error = new ErrorInfo { Code = 0 }
                }
            });

            Assert.AreEqual(0, state.PendingSkillCardId);
            Assert.AreEqual(0, state.PendingSkillCardSkill);
        }

        [Test]
        public void FailedSkillResponseKeepsPendingSkillCardForRetry()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                DrawCardRsp = new DrawCardRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    CardId = 789,
                    Value = 7,
                    Skill = SkillType.PeekSelf
                }
            });
            state.UpdateFromMessage(new ServerMessage
            {
                DiscardDrawnRsp = new DiscardDrawnRsp
                {
                    Error = new ErrorInfo { Code = 0 }
                }
            });

            state.UpdateFromMessage(new ServerMessage
            {
                UseSkillRsp = new UseSkillRsp
                {
                    Error = new ErrorInfo { Code = 4011, Message = "Skill card mismatch" }
                }
            });

            Assert.AreEqual(789, state.PendingSkillCardId);
            Assert.AreEqual((int)SkillType.PeekSelf, state.PendingSkillCardSkill);
        }

        [Test]
        public void NonSkillCardDiscardDoesNotCreatePendingSkillCard()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                DrawCardRsp = new DrawCardRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    CardId = 321,
                    Value = 4,
                    Skill = SkillType.None
                }
            });

            state.UpdateFromMessage(new ServerMessage
            {
                DiscardDrawnRsp = new DiscardDrawnRsp
                {
                    Error = new ErrorInfo { Code = 0 }
                }
            });

            Assert.AreEqual(0, state.PendingSkillCardId);
            Assert.AreEqual(0, state.PendingSkillCardSkill);
        }

        [Test]
        public void ScoreUpdateKeepsRoundRevealCumulativeScoreInSync()
        {
            var state = new GameState();
            state.Players.Add(new PlayerInfo { PlayerId = 10000, Nickname = "P10000" });

            state.UpdateFromMessage(new ServerMessage
            {
                RoundRevealNotify = new RoundRevealNotify
                {
                    RoomId = 1,
                    RoundNumber = 1,
                    Scores =
                    {
                        new RoundScoreDetail
                        {
                            PlayerId = 10000,
                            RoundScore = 15,
                            CumulativeScore = 100
                        }
                    }
                }
            });

            state.UpdateFromMessage(new ServerMessage
            {
                ScoreUpdateNotify = new ScoreUpdateNotify
                {
                    RoomId = 1,
                    RoundNumber = 1,
                    Scores =
                    {
                        new PlayerScoreInfo
                        {
                            PlayerId = 10000,
                            CurrentRoundScore = 15,
                            TotalScore = 50
                        }
                    }
                }
            });

            Assert.AreEqual(50, state.Players[0].TotalScore);
            Assert.AreEqual(50, state.LastRoundResults[0].CumulativeScore);
            Assert.AreEqual(15, state.LastRoundResults[0].RoundScore);
        }

        [Test]
        public void StateSyncRestoresPlayingSnapshotAndPendingDrawDecision()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                StateSyncNotify = new StateSyncNotify
                {
                    RoomId = 77,
                    IsInGame = true,
                    RoomState = new RoomState
                    {
                        RoomId = 77,
                        RoomCode = "ABCD12",
                        State = RoomStateType.RoomStatePlaying,
                        HostPlayerId = 10000,
                        Players =
                        {
                            new PlayerPublicInfo
                            {
                                PlayerId = 10000,
                                Nickname = "Me",
                                CharacterId = "bean",
                                SeatId = 0,
                                IsHost = true,
                                IsConnected = true,
                                TotalScore = 12
                            },
                            new PlayerPublicInfo
                            {
                                PlayerId = 10001,
                                Nickname = "Other",
                                CharacterId = "oat",
                                SeatId = 1,
                                IsConnected = false,
                                TotalScore = 8
                            }
                        }
                    },
                    GameState = new Game.Sync.GameSyncState
                    {
                        RoundNumber = 3,
                        Phase = Game.Common.GamePhase.Playing,
                        CurrentTurnPlayerId = 10000,
                        DrawPile = new DrawPileInfo { Count = 22 },
                        DiscardPile = new DiscardPileInfo
                        {
                            Count = 2,
                            TopCard = new CardInfo { CardId = 44, Value = 10, Skill = SkillType.Spy }
                        },
                        PlayerView = new PlayerGameView
                        {
                            PlayerId = 10000,
                            OwnCards =
                            {
                                new OwnCardState { SlotIndex = 0, IsKnown = true, Value = 2 },
                                new OwnCardState { SlotIndex = 1, IsKnown = false },
                                new OwnCardState { SlotIndex = 2, IsKnown = true, Value = 5 },
                                new OwnCardState { SlotIndex = 3, IsKnown = false }
                            },
                            OpponentHands =
                            {
                                new OpponentHandState { PlayerId = 10001, CardCount = 4 }
                            },
                            Scores =
                            {
                                new PlayerScoreInfo { PlayerId = 10000, TotalScore = 12, CurrentRoundScore = -1 },
                                new PlayerScoreInfo { PlayerId = 10001, TotalScore = 8, CurrentRoundScore = -1 }
                            }
                        },
                        PendingStep = new TurnStepState
                        {
                            StepType = TurnStepState.Types.StepType.WaitingDrawDecision,
                            WaitingPlayerId = 10000,
                            DrawnCardId = 900,
                            DrawnCardValue = 9,
                            DrawnCardSkill = SkillType.Spy
                        }
                    }
                }
            });

            Assert.AreEqual(GamePhase.Playing, state.Phase);
            Assert.AreEqual(77, state.RoomId);
            Assert.AreEqual("ABCD12", state.RoomCode);
            Assert.AreEqual(10000, state.MyPlayerId);
            Assert.AreEqual(10000, state.CurrentPlayerId);
            Assert.AreEqual(3, state.RoundNumber);
            Assert.AreEqual(22, state.DrawPileCount);
            Assert.AreEqual(2, state.DiscardPileCount);
            Assert.AreEqual(10, state.DiscardTopValue);
            Assert.AreEqual(4, state.MyCards.Count);
            Assert.IsTrue(state.HasDrawnCard);
            Assert.AreEqual(900, state.DrawnCardId);
            Assert.AreEqual(9, state.DrawnCardValue);
            Assert.AreEqual((int)SkillType.Spy, state.DrawnCardSkill);
            Assert.AreEqual(2, state.Players.Count);
            Assert.IsFalse(state.Players[1].IsReady);
            Assert.IsFalse(state.Players[1].IsHost);
            Assert.AreEqual(4, state.Players[1].CardCount);
        }

        [Test]
        public void StateSyncRestoresDiscardedSkillCardAsPendingSkillDecision()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                StateSyncNotify = new StateSyncNotify
                {
                    RoomId = 77,
                    IsInGame = true,
                    RoomState = new RoomState
                    {
                        RoomId = 77,
                        RoomCode = "ABCD12",
                        State = RoomStateType.RoomStatePlaying,
                        HostPlayerId = 10000,
                        Players =
                        {
                            new PlayerPublicInfo
                            {
                                PlayerId = 10000,
                                Nickname = "Me",
                                SeatId = 0,
                                IsConnected = true
                            }
                        }
                    },
                    GameState = new Game.Sync.GameSyncState
                    {
                        RoundNumber = 3,
                        Phase = Game.Common.GamePhase.Playing,
                        CurrentTurnPlayerId = 10000,
                        DrawPile = new DrawPileInfo { Count = 22 },
                        DiscardPile = new DiscardPileInfo
                        {
                            Count = 3,
                            TopCard = new CardInfo { CardId = 900, Value = 9, Skill = SkillType.Spy }
                        },
                        PlayerView = new PlayerGameView
                        {
                            PlayerId = 10000,
                            OwnCards =
                            {
                                new OwnCardState { SlotIndex = 0, IsKnown = true, Value = 2 },
                                new OwnCardState { SlotIndex = 1, IsKnown = false },
                                new OwnCardState { SlotIndex = 2, IsKnown = true, Value = 5 },
                                new OwnCardState { SlotIndex = 3, IsKnown = false }
                            }
                        },
                        PendingStep = new TurnStepState
                        {
                            StepType = TurnStepState.Types.StepType.WaitingDrawDecision,
                            WaitingPlayerId = 10000,
                            DrawnCardId = 900,
                            DrawnCardValue = 9,
                            DrawnCardSkill = SkillType.Spy
                        }
                    }
                }
            });

            Assert.IsFalse(state.HasDrawnCard);
            Assert.AreEqual(0, state.DrawnCardId);
            Assert.AreEqual(900, state.PendingSkillCardId);
            Assert.AreEqual((int)SkillType.Spy, state.PendingSkillCardSkill);
        }
    }
}
