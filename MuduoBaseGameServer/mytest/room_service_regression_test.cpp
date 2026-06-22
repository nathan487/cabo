#define private public
#include "room/RoomService.h"
#undef private

#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <memory>
#include <string>
#include <vector>

namespace {

game::TcpConnectionPtr fakeConn(std::intptr_t tag) {
    return game::TcpConnectionPtr(reinterpret_cast<TcpConnection*>(tag),
                                  [](TcpConnection*) {});
}

void require(bool condition, const std::string& message) {
    if (!condition) {
        std::cerr << "FAILED: " << message << "\n";
        std::exit(1);
    }
}

std::shared_ptr<game::PlayerSession> player(int64_t playerId,
                                             int32_t seat,
                                             const game::TcpConnectionPtr& conn,
                                             bool isHost) {
    auto p = std::make_shared<game::PlayerSession>();
    p->playerId = playerId;
    p->nickname = "P" + std::to_string(playerId);
    p->seatId = seat;
    p->conn = conn;
    p->isConnected = true;
    p->isHost = isHost;
    p->sessionToken = "token-" + std::to_string(playerId);
    return p;
}

std::shared_ptr<game::Room> makeRoom(const game::TcpConnectionPtr& hostConn,
                                     const game::TcpConnectionPtr& guestConn) {
    auto room = std::make_shared<game::Room>();
    room->roomId = 1;
    room->roomCode = "TEST01";
    room->maxPlayers = 2;
    room->state = ::game::common::ROOM_STATE_WAITING;
    room->hostPlayerId = 10000;
    room->players.push_back(player(10000, 0, hostConn, true));
    room->players.push_back(player(10001, 1, guestConn, false));
    return room;
}

void installRoom(game::RoomService& service, const std::shared_ptr<game::Room>& room) {
    service.rooms_[room->roomId] = room;
    for (auto& p : room->players) {
        service.playerRooms_[p->playerId] = room;
    }
}

bool containsPlayer(const game::Room& room, int64_t playerId) {
    for (auto& p : room.players) {
        if (p->playerId == playerId) return true;
    }
    return false;
}

int64_t firstLobbyPlayerId(const game::RoomService& service) {
    require(!service.lobbyPlayers_.empty(),
            "test setup should create a lobby player");
    return service.lobbyPlayers_.begin()->first;
}

int64_t firstAccessId(const game::RoomService& service) {
    require(!service.accessRecords_.empty(),
            "test setup should create a pending access record");
    return service.accessRecords_.begin()->first;
}

int64_t lobbyPlayerIdForConn(const game::RoomService& service,
                             const game::TcpConnectionPtr& conn) {
    auto it = service.lobbyPlayersByConn_.find(conn.get());
    require(it != service.lobbyPlayersByConn_.end(),
            "test setup should map connection to a lobby player");
    return it->second;
}

int countAccessRecordsWithStatus(
    const game::RoomService& service,
    ::game::room::RoomAccessStatus status) {
    int count = 0;
    for (const auto& kv : service.accessRecords_) {
        if (kv.second.status == status)
            ++count;
    }
    return count;
}

void enterLobby(game::RoomService& service,
                const game::TcpConnectionPtr& conn,
                int64_t requestId,
                const std::string& nickname) {
    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_enter_lobby_req();
    req->set_request_id(requestId);
    req->set_nickname(nickname);
    req->set_character_id("pomelo");
    service.handleEnterLobby(conn, msg);
}

void sendInvite(game::RoomService& service,
                const game::TcpConnectionPtr& conn,
                int64_t requestId,
                int64_t playerId,
                int64_t roomId,
                int64_t lobbyPlayerId) {
    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_invite_lobby_player_req();
    req->set_request_id(requestId);
    req->set_player_id(playerId);
    req->set_room_id(roomId);
    req->set_lobby_player_id(lobbyPlayerId);
    service.handleInviteLobbyPlayer(conn, msg);
}

::game::messages::ServerMessage decodeFrame(const std::string& frame) {
    require(frame.size() >= 2, "test frame should include a websocket header");
    size_t pos = 0;
    const auto byte0 = static_cast<unsigned char>(frame[pos++]);
    require((byte0 & 0x0F) == 0x02, "test frame should be a binary websocket frame");
    auto len = static_cast<uint64_t>(static_cast<unsigned char>(frame[pos++]) & 0x7F);
    if (len == 126) {
        require(frame.size() >= pos + 2, "test frame should include extended length");
        len = (static_cast<uint64_t>(static_cast<unsigned char>(frame[pos])) << 8)
            | static_cast<uint64_t>(static_cast<unsigned char>(frame[pos + 1]));
        pos += 2;
    } else if (len == 127) {
        require(frame.size() >= pos + 8, "test frame should include long extended length");
        len = 0;
        for (int i = 0; i < 8; ++i) {
            len = (len << 8) | static_cast<uint64_t>(static_cast<unsigned char>(frame[pos + i]));
        }
        pos += 8;
    }
    require(frame.size() >= pos + len, "test frame should include the declared payload");
    std::string payload = frame.substr(pos, static_cast<size_t>(len));
    ::game::messages::ServerMessage msg;
    require(msg.ParseFromString(payload),
            "test should parse a sent server message");
    return msg;
}

void ordinaryRoomMemberCanInviteLobbyPlayer() {
    game::RoomService service;
    std::vector<std::string> sentFrames;
    service.setSendFunc([&](const game::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back(frame);
    });

    auto hostConn = fakeConn(21);
    auto guestConn = fakeConn(22);
    auto lobbyConn = fakeConn(23);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    installRoom(service, room);

    enterLobby(service, lobbyConn, 101, "LobbyC");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);

    sendInvite(service, guestConn, 102, 10001, room->roomId, lobbyPlayerId);

    require(service.accessRecords_.size() == 1,
            "ordinary room member should create one pending invitation");
    const auto& access = service.accessRecords_.begin()->second;
    require(access.type == ::game::room::ROOM_ACCESS_TYPE_ROOM_INVITATION,
            "pending access record should be a room invitation");
    require(access.status == ::game::room::ROOM_ACCESS_STATUS_PENDING,
            "new room invitation should be pending");
    require(access.requesterPlayerId == 10001,
            "invitation requester should be the ordinary room member");
}

void forgedRoomInviteIsRejected() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(24);
    auto guestConn = fakeConn(25);
    auto lobbyConn = fakeConn(26);
    auto forgedConn = fakeConn(27);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    installRoom(service, room);

    enterLobby(service, lobbyConn, 111, "LobbyD");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);

    sendInvite(service, forgedConn, 112, 10001, room->roomId, lobbyPlayerId);

    require(service.accessRecords_.empty(),
            "forged invite from a non-member connection must not create a pending invitation");
}

void invitationApprovalJoinsAndRemovesLobbyPlayer() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(28);
    auto guestConn = fakeConn(29);
    auto lobbyConn = fakeConn(30);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    installRoom(service, room);
    service.nextPlayerId_ = 10002;

    enterLobby(service, lobbyConn, 121, "LobbyE");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);
    sendInvite(service, guestConn, 122, 10001, room->roomId, lobbyPlayerId);
    int64_t accessId = firstAccessId(service);

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_respond_room_invitation_req();
    req->set_request_id(123);
    req->set_lobby_player_id(lobbyPlayerId);
    req->set_access_id(accessId);
    req->set_approve(true);
    service.handleRespondRoomInvitation(lobbyConn, msg);

    require(room->players.size() == 3,
            "approving a room invitation should add the lobby player to the room");
    require(containsPlayer(*room, 10002),
            "approved invitee should receive a server player id and join the room");
    require(service.lobbyPlayers_.empty(),
            "approved invitee should be removed from the online lobby players");
    require(service.playerRooms_.find(10002) != service.playerRooms_.end(),
            "approved invitee should be mapped to the joined room");
}

void hostApprovesApplicationAndApplicantJoinsRoom() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(31);
    auto guestConn = fakeConn(32);
    auto lobbyConn = fakeConn(33);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    installRoom(service, room);
    service.nextPlayerId_ = 10002;

    enterLobby(service, lobbyConn, 131, "Applicant");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);

    ::game::messages::ClientMessage applyMsg;
    auto* apply = applyMsg.mutable_apply_join_room_req();
    apply->set_request_id(132);
    apply->set_lobby_player_id(lobbyPlayerId);
    apply->set_room_code(room->roomCode);
    service.handleApplyJoinRoom(lobbyConn, applyMsg);
    int64_t accessId = firstAccessId(service);

    ::game::messages::ClientMessage respondMsg;
    auto* respond = respondMsg.mutable_respond_join_application_req();
    respond->set_request_id(133);
    respond->set_player_id(10000);
    respond->set_room_id(room->roomId);
    respond->set_access_id(accessId);
    respond->set_approve(true);
    service.handleRespondJoinApplication(hostConn, respondMsg);

    require(room->players.size() == 3,
            "host approval should directly add the applicant to the room");
    require(containsPlayer(*room, 10002),
            "approved applicant should receive a server player id and join the room");
    require(service.lobbyPlayers_.empty(),
            "approved applicant should leave the online lobby players list");
}

void fullRoomRejectsApplicationsAndInvitations() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(34);
    auto guestConn = fakeConn(35);
    auto lobbyConn = fakeConn(36);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 2;
    installRoom(service, room);

    enterLobby(service, lobbyConn, 141, "LateGuest");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);

    ::game::messages::ClientMessage applyMsg;
    auto* apply = applyMsg.mutable_apply_join_room_req();
    apply->set_request_id(142);
    apply->set_lobby_player_id(lobbyPlayerId);
    apply->set_room_id(room->roomId);
    service.handleApplyJoinRoom(lobbyConn, applyMsg);
    sendInvite(service, hostConn, 143, 10000, room->roomId, lobbyPlayerId);

    require(service.accessRecords_.empty(),
            "full rooms should not create applications or invitations");
    require(room->players.size() == 2,
            "full-room access attempts must not mutate room membership");
}

void inviterInboxIncludesPendingInvitation() {
    game::RoomService service;
    std::vector<std::pair<const TcpConnection*, std::string>> sentFrames;
    service.setSendFunc([&](const game::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.emplace_back(conn.get(), frame);
    });

    auto hostConn = fakeConn(44);
    auto guestConn = fakeConn(45);
    auto lobbyConn = fakeConn(46);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    installRoom(service, room);

    enterLobby(service, lobbyConn, 171, "Invitee");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);
    sentFrames.clear();

    sendInvite(service, guestConn, 172, 10001, room->roomId, lobbyPlayerId);

    bool guestSawInvitation = false;
    for (const auto& entry : sentFrames) {
        if (entry.first != guestConn.get())
            continue;
        auto msg = decodeFrame(entry.second);
        if (msg.payload_case() != ::game::messages::ServerMessage::kRoomAccessInboxNotify)
            continue;
        for (const auto& item : msg.room_access_inbox_notify().items()) {
            if (item.type() == ::game::room::ROOM_ACCESS_TYPE_ROOM_INVITATION
                && item.lobby_player_id() == lobbyPlayerId
                && item.status() == ::game::room::ROOM_ACCESS_STATUS_PENDING) {
                guestSawInvitation = true;
            }
        }
    }

    require(guestSawInvitation,
            "room members should receive pending invitation inbox entries for button disabling");
}

void pendingApplicationExpiresIfRoomFillsBeforeApproval() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(37);
    auto guestConn = fakeConn(38);
    auto lobbyConn = fakeConn(39);
    auto fillerConn = fakeConn(40);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 3;
    installRoom(service, room);
    service.nextPlayerId_ = 10003;

    enterLobby(service, lobbyConn, 151, "Applicant");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);

    ::game::messages::ClientMessage applyMsg;
    auto* apply = applyMsg.mutable_apply_join_room_req();
    apply->set_request_id(152);
    apply->set_lobby_player_id(lobbyPlayerId);
    apply->set_room_id(room->roomId);
    service.handleApplyJoinRoom(lobbyConn, applyMsg);
    int64_t accessId = firstAccessId(service);

    auto filler = player(10002, 2, fillerConn, false);
    room->players.push_back(filler);
    service.playerRooms_[filler->playerId] = room;

    ::game::messages::ClientMessage respondMsg;
    auto* respond = respondMsg.mutable_respond_join_application_req();
    respond->set_request_id(153);
    respond->set_player_id(10000);
    respond->set_room_id(room->roomId);
    respond->set_access_id(accessId);
    respond->set_approve(true);
    service.handleRespondJoinApplication(hostConn, respondMsg);

    require(room->players.size() == 3,
            "approving a stale application for a full room must not add the applicant");
    require(service.accessRecords_[accessId].status == ::game::room::ROOM_ACCESS_STATUS_EXPIRED,
            "stale application should be expired when room becomes full before approval");
    require(service.lobbyPlayers_.find(lobbyPlayerId) != service.lobbyPlayers_.end(),
            "failed approval should leave applicant in the lobby");
}

void pendingInvitationExpiresIfRoomStartsBeforeAcceptance() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(41);
    auto guestConn = fakeConn(42);
    auto lobbyConn = fakeConn(43);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    installRoom(service, room);

    enterLobby(service, lobbyConn, 161, "Invitee");
    int64_t lobbyPlayerId = firstLobbyPlayerId(service);
    sendInvite(service, guestConn, 162, 10001, room->roomId, lobbyPlayerId);
    int64_t accessId = firstAccessId(service);

    room->state = ::game::common::ROOM_STATE_PLAYING;

    ::game::messages::ClientMessage respondMsg;
    auto* respond = respondMsg.mutable_respond_room_invitation_req();
    respond->set_request_id(163);
    respond->set_lobby_player_id(lobbyPlayerId);
    respond->set_access_id(accessId);
    respond->set_approve(true);
    service.handleRespondRoomInvitation(lobbyConn, respondMsg);

    require(room->players.size() == 2,
            "accepting a stale invitation after game start must not add the invitee");
    require(service.accessRecords_[accessId].status == ::game::room::ROOM_ACCESS_STATUS_EXPIRED,
            "stale invitation should be expired when room starts before acceptance");
    require(service.lobbyPlayers_.find(lobbyPlayerId) != service.lobbyPlayers_.end(),
            "failed invitation acceptance should leave invitee in the lobby");
}

void pendingAccessRecordsExpireWhenRoomStarts() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(47);
    auto guestConn = fakeConn(48);
    auto applicantConn = fakeConn(49);
    auto inviteeConn = fakeConn(50);
    auto room = makeRoom(hostConn, guestConn);
    room->maxPlayers = 4;
    room->players[0]->isReady = true;
    room->players[1]->isReady = true;
    installRoom(service, room);

    enterLobby(service, applicantConn, 181, "Applicant");
    int64_t applicantLobbyId = lobbyPlayerIdForConn(service, applicantConn);

    ::game::messages::ClientMessage applyMsg;
    auto* apply = applyMsg.mutable_apply_join_room_req();
    apply->set_request_id(182);
    apply->set_lobby_player_id(applicantLobbyId);
    apply->set_room_id(room->roomId);
    service.handleApplyJoinRoom(applicantConn, applyMsg);

    enterLobby(service, inviteeConn, 183, "Invitee");
    int64_t inviteeLobbyId = lobbyPlayerIdForConn(service, inviteeConn);
    sendInvite(service, guestConn, 184, 10001, room->roomId, inviteeLobbyId);

    require(countAccessRecordsWithStatus(
                service, ::game::room::ROOM_ACCESS_STATUS_PENDING) == 2,
            "test setup should create one pending application and one pending invitation");

    ::game::messages::ClientMessage startMsg;
    auto* start = startMsg.mutable_start_game_req();
    start->set_request_id(185);
    start->set_player_id(10000);
    start->set_room_id(room->roomId);

    bool started = service.handleStartGame(hostConn, startMsg);

    require(started, "host should be able to start when all room players are ready");
    require(room->state == ::game::common::ROOM_STATE_PLAYING,
            "room should enter playing state");
    require(countAccessRecordsWithStatus(
                service, ::game::room::ROOM_ACCESS_STATUS_PENDING) == 0,
            "starting a room should clear pending access records for that room");
    require(countAccessRecordsWithStatus(
                service, ::game::room::ROOM_ACCESS_STATUS_EXPIRED) == 2,
            "starting a room should expire both applications and invitations");
}

void rejectsForgedReady() {
    game::RoomService service;
    std::vector<std::string> sentFrames;
    service.setSendFunc([&](const game::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back(frame);
    });

    auto hostConn = fakeConn(1);
    auto guestConn = fakeConn(2);
    auto room = makeRoom(hostConn, guestConn);
    installRoom(service, room);

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_ready_req();
    req->set_request_id(10);
    req->set_player_id(10000);
    req->set_room_id(room->roomId);
    req->set_is_ready(true);

    service.handleReady(guestConn, msg);

    require(!room->players[0]->isReady,
            "forged ready must not update another player's ready state");
    require(!sentFrames.empty(),
            "forged ready should receive an error response");
}

void rejectsForgedStartGame() {
    game::RoomService service;
    std::vector<std::string> sentFrames;
    service.setSendFunc([&](const game::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back(frame);
    });

    auto hostConn = fakeConn(3);
    auto guestConn = fakeConn(4);
    auto room = makeRoom(hostConn, guestConn);
    room->players[0]->isReady = true;
    room->players[1]->isReady = true;
    installRoom(service, room);

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_start_game_req();
    req->set_request_id(20);
    req->set_player_id(10000);
    req->set_room_id(room->roomId);

    bool started = service.handleStartGame(guestConn, msg);

    require(!started, "forged start game must be rejected");
    require(room->state == ::game::common::ROOM_STATE_WAITING,
            "forged start game must not move room to playing");
    require(!sentFrames.empty(),
            "forged start game should receive an error response");
}

void rejectsForgedLeaveRoom() {
    game::RoomService service;
    std::vector<std::string> sentFrames;
    service.setSendFunc([&](const game::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back(frame);
    });

    auto hostConn = fakeConn(5);
    auto guestConn = fakeConn(6);
    auto room = makeRoom(hostConn, guestConn);
    installRoom(service, room);

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_leave_room_req();
    req->set_request_id(30);
    req->set_player_id(10000);
    req->set_room_id(room->roomId);

    service.handleLeaveRoom(guestConn, msg);

    require(containsPlayer(*room, 10000),
            "forged leave must not remove another player from the room");
    require(service.playerRooms_.find(10000) != service.playerRooms_.end(),
            "forged leave must not remove another player's room mapping");
    require(!sentFrames.empty(),
            "forged leave should receive an error response");
}

void connectionCloseKeepsPlayerSessionForReconnect() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(7);
    auto guestConn = fakeConn(8);
    auto room = makeRoom(hostConn, guestConn);
    installRoom(service, room);

    auto snapshot = service.getGameStartSnapshot(room->roomId);

    service.onConnectionClosed(guestConn);

    require(snapshot.valid, "game start snapshot should exist for a valid room");
    require(snapshot.players.size() == 2,
            "game start snapshot should keep the validated player list stable");
    require(room->players.size() == 2,
            "transient connection close should keep the disconnected player in live room state");
    require(snapshot.players[1].playerId == 10001,
            "game start snapshot should preserve the disconnected player's identity");
    require(!room->players[1]->isConnected,
            "closed connection should mark the session offline");
    require(room->players[1]->conn == nullptr,
            "closed connection should release the stale connection");
    require(room->players[1]->disconnectedAtMs > 0,
            "closed connection should record when the reconnect window started");
    require(service.playerRooms_.find(10001) != service.playerRooms_.end(),
            "closed connection should keep player to room lookup for reconnect");
}

void reconnectSessionBindsNewConnectionWithinWindow() {
    game::RoomService service;
    service.setSendFunc([](const game::TcpConnectionPtr&, const std::string&) {});

    auto hostConn = fakeConn(11);
    auto guestConn = fakeConn(12);
    auto newGuestConn = fakeConn(13);
    auto room = makeRoom(hostConn, guestConn);
    installRoom(service, room);

    service.onConnectionClosed(guestConn);

    game::ReconnectSessionResult result;
    require(service.reconnectSession("token-10001", newGuestConn, &result),
            "valid session token inside the reconnect window should reconnect");
    require(result.playerId == 10001,
            "reconnect result should identify the restored player");
    require(result.roomId == room->roomId,
            "reconnect result should identify the restored room");
    require(result.roomState.players_size() == 2,
            "reconnect result should include the full room state");
    require(room->players[1]->isConnected,
            "reconnected session should be marked online");
    require(room->players[1]->conn.get() == newGuestConn.get(),
            "reconnected session should bind the new connection");
    require(room->players[1]->disconnectedAtMs == 0,
            "reconnected session should clear the disconnect timestamp");
}

void publicRoomBroadcastEncodesFrameOnceForAllRecipients() {
    game::RoomService service;
    std::vector<std::string> sentFrames;
    service.setSendFunc([&](const game::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back(frame);
    });

    auto hostConn = fakeConn(9);
    auto guestConn = fakeConn(10);
    auto room = makeRoom(hostConn, guestConn);
    installRoom(service, room);

    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_room_start_notify();
    notify->set_room_id(room->roomId);

    service.resetSendPathStatsForTests();
    service.broadcastToRoom(room->roomId, msg);

    require(sentFrames.size() == 2,
            "public room broadcast should still send one frame to each connected player");
    require(service.sendPathStatsForTests().encodedFrames == 1,
            "public room broadcast should encode and frame the shared payload once");
}

} // namespace

int main() {
    ordinaryRoomMemberCanInviteLobbyPlayer();
    forgedRoomInviteIsRejected();
    invitationApprovalJoinsAndRemovesLobbyPlayer();
    hostApprovesApplicationAndApplicantJoinsRoom();
    fullRoomRejectsApplicationsAndInvitations();
    inviterInboxIncludesPendingInvitation();
    pendingApplicationExpiresIfRoomFillsBeforeApproval();
    pendingInvitationExpiresIfRoomStartsBeforeAcceptance();
    pendingAccessRecordsExpireWhenRoomStarts();
    rejectsForgedReady();
    rejectsForgedStartGame();
    rejectsForgedLeaveRoom();
    connectionCloseKeepsPlayerSessionForReconnect();
    reconnectSessionBindsNewConnectionWithinWindow();
    publicRoomBroadcastEncodesFrameOnceForAllRecipients();
    std::cout << "room_service_regression_test passed\n";
    return 0;
}
