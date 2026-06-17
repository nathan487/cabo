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

void gameStartSnapshotSurvivesLaterRoomMutation() {
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
    require(room->players.size() == 1,
            "test setup should remove the disconnected player from live room state");
    require(snapshot.players[1].playerId == 10001,
            "game start snapshot should preserve the disconnected player's identity");
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
    rejectsForgedReady();
    rejectsForgedStartGame();
    rejectsForgedLeaveRoom();
    gameStartSnapshotSurvivesLaterRoomMutation();
    publicRoomBroadcastEncodesFrameOnceForAllRecipients();
    std::cout << "room_service_regression_test passed\n";
    return 0;
}
