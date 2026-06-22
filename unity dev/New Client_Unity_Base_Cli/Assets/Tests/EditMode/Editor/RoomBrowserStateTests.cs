using Cabo.Client;
using Game.Common;
using Game.Messages;
using Game.Room;
using NUnit.Framework;

namespace Cabo.Client.Tests
{
    public sealed class RoomBrowserStateTests
    {
        [Test]
        public void EnterRoomBrowserThenReturnHomeClearsLobbyState()
        {
            var flow = new GameFlow(new NetworkGateway());

            flow.EnterRoomBrowser("Browser", "bean");
            flow.ProcessServerMessage(new ServerMessage
            {
                EnterLobbyRsp = new EnterLobbyRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    LobbyPlayerId = 50001
                }
            });
            flow.State.RoomSummaries.Add(new RoomSummaryInfo { RoomId = 1 });
            flow.State.AccessInboxItems.Add(new RoomAccessInboxItem { AccessId = 2 });

            Assert.AreEqual(FlowState.RoomBrowser, flow.Flow);
            Assert.AreEqual(50001, flow.State.LobbyPlayerId);

            flow.ReturnHomeFromRoomBrowser();

            Assert.AreEqual(FlowState.Home, flow.Flow);
            Assert.AreEqual(0, flow.State.LobbyPlayerId);
            Assert.AreEqual(0, flow.State.RoomSummaries.Count);
            Assert.AreEqual(0, flow.State.AccessInboxItems.Count);
        }

        [Test]
        public void ApprovedRoomAccessDecisionFromBrowserMovesFlowToWaitingRoom()
        {
            var flow = new GameFlow(new NetworkGateway());
            flow.EnterRoomBrowser("Browser", "bean");
            flow.ProcessServerMessage(new ServerMessage
            {
                EnterLobbyRsp = new EnterLobbyRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    LobbyPlayerId = 50001
                }
            });

            flow.ProcessServerMessage(new ServerMessage
            {
                RoomAccessDecisionNotify = new RoomAccessDecisionNotify
                {
                    AccessId = 10,
                    Type = RoomAccessType.RoomInvitation,
                    Status = RoomAccessStatus.Approved,
                    Error = new ErrorInfo { Code = 0 },
                    RoomId = 8,
                    RoomCode = "ROOM88",
                    PlayerId = 10005,
                    SessionToken = "session-token",
                    SeatId = 2
                }
            });

            Assert.AreEqual(FlowState.WaitingRoom, flow.Flow);
            Assert.AreEqual(GamePhase.WaitingRoom, flow.State.Phase);
            Assert.AreEqual(0, flow.State.LobbyPlayerId);
        }

        [Test]
        public void DirectJoinRoomCodeFromBrowserLeavesLobbyAndWaitsForRoom()
        {
            var flow = new GameFlow(new NetworkGateway());
            flow.EnterRoomBrowser("Browser", "bean");
            flow.ProcessServerMessage(new ServerMessage
            {
                EnterLobbyRsp = new EnterLobbyRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    LobbyPlayerId = 50001
                }
            });
            flow.State.RoomSummaries.Add(new RoomSummaryInfo { RoomId = 1 });
            flow.State.AccessInboxItems.Add(new RoomAccessInboxItem { AccessId = 2 });

            flow.JoinRoomFromBrowser("ABCD12");

            Assert.AreEqual(FlowState.WaitingRoom, flow.Flow);
            Assert.AreEqual(0, flow.State.LobbyPlayerId);
            Assert.AreEqual(0, flow.State.RoomSummaries.Count);
            Assert.AreEqual(0, flow.State.AccessInboxItems.Count);
        }

        [Test]
        public void RoomBrowserNotificationsUpdateListsAndInbox()
        {
            var state = new GameState();

            state.UpdateFromMessage(new ServerMessage
            {
                EnterLobbyRsp = new EnterLobbyRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    LobbyPlayerId = 50001
                }
            });
            state.UpdateFromMessage(new ServerMessage
            {
                RoomListNotify = new RoomListNotify
                {
                    Rooms =
                    {
                        new RoomSummary
                        {
                            RoomId = 7,
                            RoomCode = "ABCD12",
                            HostNickname = "Host",
                            PlayerCount = 2,
                            MaxPlayers = 4,
                            IsFull = false
                        }
                    }
                }
            });
            state.UpdateFromMessage(new ServerMessage
            {
                OnlineLobbyPlayersNotify = new OnlineLobbyPlayersNotify
                {
                    Players =
                    {
                        new OnlineLobbyPlayer
                        {
                            LobbyPlayerId = 50002,
                            Nickname = "Fresh",
                            CharacterId = "bean"
                        }
                    }
                }
            });
            state.UpdateFromMessage(new ServerMessage
            {
                RoomAccessInboxNotify = new RoomAccessInboxNotify
                {
                    Items =
                    {
                        new RoomAccessItem
                        {
                            AccessId = 9,
                            Type = RoomAccessType.RoomInvitation,
                            Status = RoomAccessStatus.Pending,
                            RoomId = 7,
                            RoomCode = "ABCD12",
                            HostNickname = "Host",
                            RequesterPlayerId = 10001,
                            RequesterNickname = "Guest",
                            LobbyPlayerId = 50001,
                            LobbyNickname = "Me"
                        }
                    }
                }
            });

            Assert.AreEqual(50001, state.LobbyPlayerId);
            Assert.AreEqual(1, state.RoomSummaries.Count);
            Assert.AreEqual("Host", state.RoomSummaries[0].HostNickname);
            Assert.AreEqual(1, state.OnlineLobbyPlayers.Count);
            Assert.AreEqual("Fresh", state.OnlineLobbyPlayers[0].Nickname);
            Assert.AreEqual(1, state.AccessInboxItems.Count);
            Assert.AreEqual(RoomAccessType.RoomInvitation, state.AccessInboxItems[0].Type);
        }

        [Test]
        public void ApprovedAccessDecisionMovesLobbyPlayerIntoWaitingRoom()
        {
            var state = new GameState();
            state.UpdateFromMessage(new ServerMessage
            {
                EnterLobbyRsp = new EnterLobbyRsp
                {
                    Error = new ErrorInfo { Code = 0 },
                    LobbyPlayerId = 50001
                }
            });

            state.UpdateFromMessage(new ServerMessage
            {
                RoomAccessDecisionNotify = new RoomAccessDecisionNotify
                {
                    AccessId = 10,
                    Type = RoomAccessType.RoomInvitation,
                    Status = RoomAccessStatus.Approved,
                    Error = new ErrorInfo { Code = 0 },
                    RoomId = 8,
                    RoomCode = "ROOM88",
                    PlayerId = 10005,
                    SessionToken = "session-token",
                    SeatId = 2,
                    Message = "Invitation approved"
                }
            });

            Assert.AreEqual(0, state.LobbyPlayerId);
            Assert.AreEqual(8, state.RoomId);
            Assert.AreEqual("ROOM88", state.RoomCode);
            Assert.AreEqual(10005, state.MyPlayerId);
            Assert.AreEqual("session-token", state.SessionToken);
            Assert.AreEqual(GamePhase.WaitingRoom, state.Phase);
            Assert.AreEqual("Invitation approved", state.LastRoomAccessMessage);
        }

        [Test]
        public void ReturnHomeClearsRoomBrowserState()
        {
            var state = new GameState
            {
                LobbyPlayerId = 50001,
                LastRoomAccessMessage = "sent",
                LastRoomAccessError = "error"
            };
            state.RoomSummaries.Add(new RoomSummaryInfo { RoomId = 1 });
            state.OnlineLobbyPlayers.Add(new OnlineLobbyPlayerInfo { LobbyPlayerId = 2 });
            state.AccessInboxItems.Add(new RoomAccessInboxItem { AccessId = 3 });

            state.ReturnHome();

            Assert.AreEqual(0, state.LobbyPlayerId);
            Assert.AreEqual(0, state.RoomSummaries.Count);
            Assert.AreEqual(0, state.OnlineLobbyPlayers.Count);
            Assert.AreEqual(0, state.AccessInboxItems.Count);
            Assert.AreEqual("", state.LastRoomAccessMessage);
            Assert.AreEqual("", state.LastRoomAccessError);
        }
    }
}
