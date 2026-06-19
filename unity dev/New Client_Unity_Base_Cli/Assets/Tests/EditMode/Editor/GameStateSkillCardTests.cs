using Cabo.Client;
using Game.Common;
using Game.Game;
using Game.Messages;
using NUnit.Framework;

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
    }
}
