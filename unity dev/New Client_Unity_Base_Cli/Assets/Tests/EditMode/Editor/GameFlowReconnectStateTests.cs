using Cabo.Client;
using Game.Common;
using Game.Game;
using Game.Messages;
using Game.Room;
using Game.Sync;
using NUnit.Framework;

namespace Cabo.Client.Tests
{
    public sealed class GameFlowReconnectStateTests
    {
        [Test]
        public void StateSyncPendingDrawDecisionRestoresDrawnDecisionSubState()
        {
            var flow = new GameFlow(new NetworkGateway());

            flow.ProcessServerMessage(BuildStateSync(
                myPlayerId: 10000,
                currentPlayerId: 10000,
                pendingDrawnCardId: 900,
                pendingDrawnCardSkill: SkillType.Spy));

            Assert.AreEqual(GamePhase.Playing, flow.State.Phase);
            Assert.IsTrue(flow.State.HasDrawnCard);
            Assert.AreEqual(GameSubState.AwaitingDrawnDecision, flow.SubState);
        }

        [Test]
        public void StateSyncForCurrentPlayerWithoutPendingDrawRestoresMainInputSubState()
        {
            var flow = new GameFlow(new NetworkGateway());

            flow.ProcessServerMessage(BuildStateSync(
                myPlayerId: 10000,
                currentPlayerId: 10000));

            Assert.AreEqual(GamePhase.Playing, flow.State.Phase);
            Assert.IsFalse(flow.State.HasDrawnCard);
            Assert.AreEqual(GameSubState.AwaitingMainInput, flow.SubState);
        }

        [Test]
        public void StateSyncForOtherPlayersTurnRestoresIdleSubState()
        {
            var flow = new GameFlow(new NetworkGateway());

            flow.ProcessServerMessage(BuildStateSync(
                myPlayerId: 10000,
                currentPlayerId: 10001));

            Assert.AreEqual(GamePhase.Playing, flow.State.Phase);
            Assert.AreEqual(GameSubState.Idle, flow.SubState);
        }

        [Test]
        public void StateSyncDiscardedSpySkillRestoresSkillTargetSubState()
        {
            var flow = new GameFlow(new NetworkGateway());

            flow.ProcessServerMessage(BuildStateSync(
                myPlayerId: 10000,
                currentPlayerId: 10000,
                pendingDrawnCardId: 900,
                pendingDrawnCardSkill: SkillType.Spy,
                discardTopCardId: 900));

            Assert.IsFalse(flow.State.HasDrawnCard);
            Assert.AreEqual(900, flow.State.PendingSkillCardId);
            Assert.AreEqual(GameSubState.SkillSpyTarget, flow.SubState);
        }

        static ServerMessage BuildStateSync(
            long myPlayerId,
            long currentPlayerId,
            int pendingDrawnCardId = 0,
            SkillType pendingDrawnCardSkill = SkillType.Unknown,
            int discardTopCardId = 44)
        {
            var gameState = new GameSyncState
            {
                RoundNumber = 2,
                Phase = Game.Common.GamePhase.Playing,
                CurrentTurnPlayerId = currentPlayerId,
                DrawPile = new DrawPileInfo { Count = 20 },
                DiscardPile = new DiscardPileInfo
                {
                    Count = 1,
                    TopCard = new CardInfo
                    {
                        CardId = discardTopCardId,
                        Value = discardTopCardId == pendingDrawnCardId ? 9 : 6,
                        Skill = discardTopCardId == pendingDrawnCardId ? pendingDrawnCardSkill : SkillType.Unknown
                    }
                },
                PlayerView = new PlayerGameView
                {
                    PlayerId = myPlayerId,
                    OwnCards =
                    {
                        new OwnCardState { SlotIndex = 0, IsKnown = true, Value = 2 },
                        new OwnCardState { SlotIndex = 1, IsKnown = false },
                        new OwnCardState { SlotIndex = 2, IsKnown = false },
                        new OwnCardState { SlotIndex = 3, IsKnown = true, Value = 5 }
                    },
                    OpponentHands =
                    {
                        new OpponentHandState { PlayerId = 10001, CardCount = 4 }
                    }
                }
            };

            gameState.PendingStep = pendingDrawnCardId > 0
                ? new TurnStepState
                {
                    StepType = TurnStepState.Types.StepType.WaitingDrawDecision,
                    WaitingPlayerId = myPlayerId,
                    DrawnCardId = pendingDrawnCardId,
                    DrawnCardValue = 9,
                    DrawnCardSkill = pendingDrawnCardSkill
                }
                : new TurnStepState
                {
                    StepType = TurnStepState.Types.StepType.None,
                    WaitingPlayerId = currentPlayerId
                };

            return new ServerMessage
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
                                PlayerId = myPlayerId,
                                Nickname = "Me",
                                CharacterId = "bean",
                                SeatId = 0,
                                IsHost = myPlayerId == 10000,
                                IsConnected = true
                            },
                            new PlayerPublicInfo
                            {
                                PlayerId = 10001,
                                Nickname = "Other",
                                CharacterId = "oat",
                                SeatId = 1,
                                IsConnected = true
                            }
                        }
                    },
                    GameState = gameState
                }
            };
        }
    }
}
