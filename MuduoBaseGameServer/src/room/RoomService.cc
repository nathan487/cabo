#include "room/RoomService.h"
#include "common/MessageCodec.h"
#include <mymuduo/logger.h>
#include <algorithm>
#include <sstream>

namespace game {

RoomService::RoomService() : rng_(std::random_device{}()) {}

// ── Public access ──

const std::shared_ptr<Room> RoomService::getRoom(int64_t roomId) const {
    auto it = rooms_.find(roomId);
    return (it != rooms_.end()) ? it->second : nullptr;
}

std::shared_ptr<Room> RoomService::getRoom(int64_t roomId) {
    auto it = rooms_.find(roomId);
    return (it != rooms_.end()) ? it->second : nullptr;
}

std::shared_ptr<Room> RoomService::getRoomMutable(int64_t roomId) {
    return getRoom(roomId);
}

// ── Helpers ──

std::string RoomService::generateRoomCode() {
    static const char chars[] = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    std::uniform_int_distribution<int> dist(0, static_cast<int>(sizeof(chars)) - 2);
    std::string code(6, '\0');
    for (int i = 0; i < 6; ++i) {
        code[i] = chars[dist(rng_)];
    }
    return code;
}

std::string RoomService::generateSessionToken() {
    // Simple: two room codes concatenated = 12-char random token
    return generateRoomCode() + generateRoomCode();
}

int64_t RoomService::nextPlayerId() {
    return nextPlayerId_++;
}

int64_t RoomService::nextRoomId() {
    return nextRoomId_++;
}

void RoomService::sendTo(const TcpConnectionPtr& conn,
                          const ::game::messages::ServerMessage& msg) {
    if (!sendFunc_ || !conn) return;
    std::string payload;
    msg.SerializeToString(&payload);
    auto frame = MessageCodec::encode(payload);
    sendFunc_(conn, frame);
}

void RoomService::broadcastToRoom(int64_t roomId,
                                   const ::game::messages::ServerMessage& msg,
                                   int64_t excludePlayerId) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = rooms_.find(roomId);
    if (it == rooms_.end()) return;

    for (auto& player : it->second->players) {
        if (player->playerId == excludePlayerId) continue;
        if (player->conn && player->isConnected) {
            sendTo(player->conn, msg);
        }
    }
}

void RoomService::sendRoomState(int64_t roomId, const TcpConnectionPtr& conn) {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = rooms_.find(roomId);
    if (it == rooms_.end()) return;
    auto& room = it->second;

    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_room_state_notify();
    notify->set_room_id(room->roomId);
    auto* rs = notify->mutable_room();
    rs->set_room_id(room->roomId);
    rs->set_room_code(room->roomCode);
    rs->set_max_players(room->maxPlayers);
    rs->set_state(room->state);
    rs->set_host_player_id(room->hostPlayerId);

    for (auto& p : room->players) {
        auto* ppi = rs->add_players();
        ppi->set_player_id(p->playerId);
        ppi->set_nickname(p->nickname);
        ppi->set_seat_id(p->seatId);
        ppi->set_is_ready(p->isReady);
        ppi->set_is_host(p->isHost);
        ppi->set_is_connected(p->isConnected);
        ppi->set_total_score(p->totalScore);
    }

    sendTo(conn, msg);
}

// ── Handle CreateRoom ──

void RoomService::handleCreateRoom(const TcpConnectionPtr& conn,
                                    const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.create_room_req();

    // Check if this connection is already in a room
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& kv : playerRooms_) {
            auto& room = kv.second;
            for (auto& p : room->players) {
                if (p->conn.get() == conn.get()) {
                    ::game::messages::ServerMessage rspMsg;
                    auto* rsp = rspMsg.mutable_create_room_rsp();
                    rsp->set_request_id(req.request_id());
                    rsp->mutable_error()->set_code(1100);
                    rsp->mutable_error()->set_message("Already in a room. Leave first.");
                    sendTo(conn, rspMsg);
                    return;
                }
            }
        }
    }

    auto player = std::make_shared<PlayerSession>();
    player->playerId = nextPlayerId();
    player->nickname = req.nickname().empty() ? "Player" : req.nickname();
    player->seatId = 0;
    player->isReady = false;
    player->isHost = true;
    player->isConnected = true;
    player->totalScore = 0;
    player->conn = conn;
    player->sessionToken = generateSessionToken();

    auto room = std::make_shared<Room>();
    room->roomId = nextRoomId();
    room->roomCode = generateRoomCode();
    room->maxPlayers = std::max(2, std::min(6, req.max_players()));
    room->state = ::game::common::ROOM_STATE_WAITING;
    room->hostPlayerId = player->playerId;
    room->players.push_back(player);

    {
        std::lock_guard<std::mutex> lock(mutex_);
        rooms_[room->roomId] = room;
        playerRooms_[player->playerId] = room;
    }

    LOG_INFO("[Room] Creating room for '%s' (max %d)...", player->nickname.c_str(), room->maxPlayers);

    // CreateRoomRsp
    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_create_room_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    rsp->set_room_id(room->roomId);
    rsp->set_room_code(room->roomCode);
    rsp->set_player_id(player->playerId);
    rsp->set_session_token(player->sessionToken);
    sendTo(conn, rspMsg);

    // Send full room state
    sendRoomState(room->roomId, conn);

    LOG_INFO("[Room] Room %s created — player %lld (host)", room->roomCode.c_str(), (long long)player->playerId);
}

// ── Handle JoinRoom ──

void RoomService::handleJoinRoom(const TcpConnectionPtr& conn,
                                  const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.join_room_req();

    // Check if this connection is already in a room
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& kv : playerRooms_) {
            auto& room = kv.second;
            for (auto& p : room->players) {
                if (p->conn.get() == conn.get()) {
                    ::game::messages::ServerMessage rspMsg;
                    auto* rsp = rspMsg.mutable_join_room_rsp();
                    rsp->set_request_id(req.request_id());
                    rsp->mutable_error()->set_code(1100);
                    rsp->mutable_error()->set_message("Already in a room. Leave first.");
                    sendTo(conn, rspMsg);
                    return;
                }
            }
        }
    }

    // Find room by code
    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& kv : rooms_) {
            if (kv.second->roomCode == req.room_code()) {
                room = kv.second;
                break;
            }
        }
    }

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_join_room_rsp();
    rsp->set_request_id(req.request_id());

    if (!room) {
        rsp->mutable_error()->set_code(1001);
        rsp->mutable_error()->set_message("Room not found");
        sendTo(conn, rspMsg);
        return;
    }

    if (static_cast<int>(room->players.size()) >= room->maxPlayers) {
        rsp->mutable_error()->set_code(1002);
        rsp->mutable_error()->set_message("Room is full");
        sendTo(conn, rspMsg);
        return;
    }

    if (room->state != ::game::common::ROOM_STATE_WAITING) {
        rsp->mutable_error()->set_code(1003);
        rsp->mutable_error()->set_message("Game already in progress");
        sendTo(conn, rspMsg);
        return;
    }

    auto player = std::make_shared<PlayerSession>();
    player->playerId = nextPlayerId();
    player->nickname = req.nickname().empty() ? "Player" : req.nickname();
    player->seatId = static_cast<int32_t>(room->players.size());
    player->isReady = false;
    player->isHost = false;
    player->isConnected = true;
    player->totalScore = 0;
    player->conn = conn;
    player->sessionToken = generateSessionToken();

    {
        std::lock_guard<std::mutex> lock(mutex_);
        room->players.push_back(player);
        playerRooms_[player->playerId] = room;
    }

    // JoinRoomRsp
    rsp->mutable_error()->set_code(0);
    rsp->set_room_id(room->roomId);
    rsp->set_player_id(player->playerId);
    rsp->set_session_token(player->sessionToken);
    rsp->set_seat_id(player->seatId);
    sendTo(conn, rspMsg);

    // PlayerJoinNotify to others
    ::game::messages::ServerMessage joinNotify;
    auto* jn = joinNotify.mutable_player_join_notify();
    jn->set_room_id(room->roomId);
    auto* jpi = jn->mutable_player();
    jpi->set_player_id(player->playerId);
    jpi->set_nickname(player->nickname);
    jpi->set_seat_id(player->seatId);
    jpi->set_is_ready(false);
    jpi->set_is_host(false);
    jpi->set_is_connected(true);
    jpi->set_total_score(0);
    broadcastToRoom(room->roomId, joinNotify, player->playerId);

    // Full room state to the new player AND all existing players
    for (auto& p : room->players) {
        if (p->conn && p->isConnected)
            sendRoomState(room->roomId, p->conn);
    }

    LOG_INFO("[Room] Player %s joined room %s (seat %d, %zu/%d players)",
             player->nickname.c_str(), room->roomCode.c_str(),
             player->seatId, room->players.size(), room->maxPlayers);
}

// ── Handle LeaveRoom ──

void RoomService::handleLeaveRoom(const TcpConnectionPtr& conn,
                                   const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.leave_room_req();
    int64_t playerId = req.player_id();
    int64_t roomId = req.room_id();

    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = rooms_.find(roomId);
        if (it == rooms_.end()) {
            // Try playerRooms_ lookup
            auto pit = playerRooms_.find(playerId);
            if (pit != playerRooms_.end()) {
                room = pit->second;
            }
        } else {
            room = it->second;
        }
    }

    if (!room) return;

    int64_t newHostId = 0;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto& players = room->players;
        players.erase(
            std::remove_if(players.begin(), players.end(),
                           [playerId](const std::shared_ptr<PlayerSession>& p) { return p->playerId == playerId; }),
            players.end());
        playerRooms_.erase(playerId);

        // If host left, assign new host
        if (playerId == room->hostPlayerId && !players.empty()) {
            players[0]->isHost = true;
            room->hostPlayerId = players[0]->playerId;
            newHostId = players[0]->playerId;
        }
    }

    // LeaveRoomRsp
    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_leave_room_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);

    // Broadcast updated room state to remaining players
    for (auto& p : room->players) {
        if (p->conn && p->isConnected)
            sendRoomState(room->roomId, p->conn);
    }
}

// ── Handle Ready ──

void RoomService::handleReady(const TcpConnectionPtr& conn,
                               const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.ready_req();
    int64_t playerId = req.player_id();
    bool ready = req.is_ready();

    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = playerRooms_.find(playerId);
        if (it == playerRooms_.end()) {
            ::game::messages::ServerMessage rspMsg;
            auto* rsp = rspMsg.mutable_ready_rsp();
            rsp->set_request_id(req.request_id());
            rsp->mutable_error()->set_code(2003);
            rsp->mutable_error()->set_message("Player not in room");
            sendTo(conn, rspMsg);
            return;
        }
        room = it->second;

        // Update player ready state while holding lock
        for (auto& p : room->players) {
            if (p->playerId == playerId) {
                p->isReady = ready;
                LOG_INFO("[Room] Player %s set ready=%d in room %s",
                         p->nickname.c_str(), ready, room->roomCode.c_str());
                break;
            }
        }
    }

    // Send ReadyRsp to the requesting player
    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_ready_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    rsp->set_is_ready(ready);
    sendTo(conn, rspMsg);

    // Broadcast updated room state to all players
    for (auto& p : room->players) {
        if (p->conn && p->isConnected) {
            sendRoomState(room->roomId, p->conn);
        }
    }
}

// ── Handle StartGame ──

void RoomService::handleStartGame(const TcpConnectionPtr& conn,
                                   const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.start_game_req();
    int64_t playerId = req.player_id();

    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        auto it = playerRooms_.find(playerId);
        if (it == playerRooms_.end()) {
            ::game::messages::ServerMessage rspMsg;
            auto* rsp = rspMsg.mutable_start_game_rsp();
            rsp->set_request_id(req.request_id());
            rsp->mutable_error()->set_code(2001);
            rsp->mutable_error()->set_message("Not in room");
            sendTo(conn, rspMsg);
            return;
        }
        room = it->second;
    }

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_start_game_rsp();
    rsp->set_request_id(req.request_id());

    // Validate: must be host
    if (playerId != room->hostPlayerId) {
        rsp->mutable_error()->set_code(2001);
        rsp->mutable_error()->set_message("Only host can start game");
        sendTo(conn, rspMsg);
        return;
    }

    // Validate: all ready (min 2)
    int readyCount = 0;
    for (auto& p : room->players) {
        if (p->isReady) readyCount++;
    }
    if (readyCount < 2 || readyCount != static_cast<int>(room->players.size())) {
        rsp->mutable_error()->set_code(2002);
        rsp->mutable_error()->set_message("All players must be ready (min 2)");
        sendTo(conn, rspMsg);
        return;
    }

    // Start!
    room->state = ::game::common::ROOM_STATE_PLAYING;

    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);

    // Reset ready flags so players must re-ready for inter-round restart
    for (auto& p : room->players) {
        p->isReady = false;
    }

    // RoomStartNotify to all
    ::game::messages::ServerMessage startNotify;
    auto* sn = startNotify.mutable_room_start_notify();
    sn->set_room_id(room->roomId);
    broadcastToRoom(room->roomId, startNotify);

    // Broadcast updated room state so all clients see isReady=false
    for (auto& p : room->players) {
        if (p->conn && p->isConnected) {
            sendRoomState(room->roomId, p->conn);
        }
    }
}

// ── Connection closed ──

void RoomService::onConnectionClosed(const TcpConnectionPtr& conn) {
    if (!conn) return;

    std::vector<int64_t> roomsToUpdate;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (auto& kv : rooms_) {
            auto& room = kv.second;
            for (auto it = room->players.begin(); it != room->players.end(); ) {
                if ((*it)->conn && (*it)->conn.get() == conn.get()) {
                    int64_t pid = (*it)->playerId;
                    LOG_INFO("[Room] Player %lld disconnected, removing from room %s",
                             (long long)pid, room->roomCode.c_str());
                    bool wasHost = (*it)->isHost;
                    it = room->players.erase(it);
                    playerRooms_.erase(pid);

                    // Reassign host if needed
                    if (wasHost && !room->players.empty()) {
                        room->players[0]->isHost = true;
                        room->hostPlayerId = room->players[0]->playerId;
                        LOG_INFO("[Room] New host: %lld", (long long)room->hostPlayerId);
                    }

                    if (!room->players.empty())
                        roomsToUpdate.push_back(room->roomId);
                } else {
                    ++it;
                }
            }
        }
    }

    // Broadcast updated state to remaining players (outside lock)
    for (auto roomId : roomsToUpdate) {
        auto room = getRoomMutable(roomId);
        if (room) {
            for (auto& p : room->players) {
                if (p->conn && p->isConnected)
                    sendRoomState(roomId, p->conn);
            }
        }
    }
}

} // namespace game
