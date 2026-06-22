#include "room/RoomService.h"
#include "common/WebSocketCodec.h"
#include <mymuduo/logger.h>
#include <algorithm>
#include <chrono>
#include <cctype>
#include <sstream>

namespace game {

namespace {

constexpr int64_t kReconnectWindowMs = 60 * 1000;

std::string trimAsciiWhitespace(const std::string& input) {
    size_t begin = 0;
    size_t end = input.size();
    while (begin < end && std::isspace(static_cast<unsigned char>(input[begin]))) {
        ++begin;
    }
    while (end > begin && std::isspace(static_cast<unsigned char>(input[end - 1]))) {
        --end;
    }
    return input.substr(begin, end - begin);
}

std::string normalizeCharacterId(const std::string& input) {
    const std::string value = trimAsciiWhitespace(input);
    if (value == "pomelo" || value == "strawberry"
        || value == "oat" || value == "bean"
        || value == "trainee" || value == "milkdragon")
        return value;
    return "pomelo";
}

int64_t nowMs() {
    using namespace std::chrono;
    return duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
}

size_t utf8CodePointCount(const std::string& input) {
    size_t count = 0;
    for (unsigned char ch : input) {
        if ((ch & 0xC0) != 0x80) {
            ++count;
        }
    }
    return count;
}

} // namespace

RoomService::RoomService() : rng_(std::random_device{}()) {}

// ── Public access ──

const std::shared_ptr<Room> RoomService::getRoom(int64_t roomId) const {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    auto it = rooms_.find(roomId);
    return (it != rooms_.end()) ? it->second : nullptr;
}

std::shared_ptr<Room> RoomService::getRoom(int64_t roomId) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    auto it = rooms_.find(roomId);
    return (it != rooms_.end()) ? it->second : nullptr;
}

std::shared_ptr<Room> RoomService::getRoomMutable(int64_t roomId) {
    return getRoom(roomId);
}

RoomGameStartSnapshot RoomService::getGameStartSnapshot(int64_t roomId) const {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    RoomGameStartSnapshot snapshot;
    auto it = rooms_.find(roomId);
    if (it == rooms_.end()) {
        return snapshot;
    }

    const auto& room = it->second;
    snapshot.valid = true;
    snapshot.roomId = room->roomId;
    snapshot.hostPlayerId = room->hostPlayerId;
    snapshot.players.reserve(room->players.size());
    for (const auto& p : room->players) {
        PlayerSessionSnapshot player;
        player.playerId = p->playerId;
        player.nickname = p->nickname;
        player.characterId = p->characterId;
        player.seatId = p->seatId;
        player.isConnected = p->isConnected;
        player.totalScore = p->totalScore;
        player.conn = p->conn;
        snapshot.players.push_back(std::move(player));
    }
    return snapshot;
}

// ── Helpers ──

std::string RoomService::generateRoomCode() {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
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
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    return nextPlayerId_++;
}

int64_t RoomService::nextRoomId() {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    return nextRoomId_++;
}

int64_t RoomService::nextChatMessageId() {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    return nextChatMessageId_++;
}

int64_t RoomService::nextLobbyPlayerId() {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    return nextLobbyPlayerId_++;
}

int64_t RoomService::nextAccessId() {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    return nextAccessId_++;
}

std::shared_ptr<PlayerSession> RoomService::findPlayer(Room& room, int64_t playerId) {
    for (auto& player : room.players) {
        if (player->playerId == playerId) return player;
    }
    return nullptr;
}

std::shared_ptr<PlayerSession> RoomService::findPlayerByConnection(Room& room,
                                                                    const TcpConnectionPtr& conn) {
    if (!conn) return nullptr;
    for (auto& player : room.players) {
        if (player->isConnected && player->conn && player->conn.get() == conn.get())
            return player;
    }
    return nullptr;
}

bool RoomService::isPlayerConnection(const PlayerSession& player,
                                     const TcpConnectionPtr& conn) const {
    return player.isConnected && player.conn && conn && player.conn.get() == conn.get();
}

bool RoomService::isConnectionInAnyRoom(const TcpConnectionPtr& conn) const {
    if (!conn) return false;
    for (const auto& kv : rooms_) {
        for (const auto& p : kv.second->players) {
            if (p->isConnected && p->conn && p->conn.get() == conn.get())
                return true;
        }
    }
    return false;
}

std::shared_ptr<Room> RoomService::findRoomByCode(const std::string& roomCode) const {
    const std::string code = trimAsciiWhitespace(roomCode);
    for (const auto& kv : rooms_) {
        if (kv.second->roomCode == code)
            return kv.second;
    }
    return nullptr;
}

std::shared_ptr<LobbyPlayer> RoomService::findLobbyPlayerForConnection(
    int64_t lobbyPlayerId, const TcpConnectionPtr& conn) const {
    auto it = lobbyPlayers_.find(lobbyPlayerId);
    if (it == lobbyPlayers_.end() || !it->second || !it->second->conn || !conn)
        return nullptr;
    if (it->second->conn.get() != conn.get())
        return nullptr;
    return it->second;
}

bool RoomService::isRoomJoinable(const Room& room, std::string* errorMessage) const {
    if (room.state != ::game::common::ROOM_STATE_WAITING) {
        if (errorMessage) *errorMessage = "Game already in progress";
        return false;
    }
    if (static_cast<int>(room.players.size()) >= room.maxPlayers) {
        if (errorMessage) *errorMessage = "Room is full";
        return false;
    }
    bool hostOnline = false;
    for (const auto& p : room.players) {
        if (p->playerId == room.hostPlayerId && p->isConnected) {
            hostOnline = true;
            break;
        }
    }
    if (!hostOnline) {
        if (errorMessage) *errorMessage = "Host is offline";
        return false;
    }
    return true;
}

std::string RoomService::hostNickname(const Room& room) const {
    for (const auto& p : room.players) {
        if (p->playerId == room.hostPlayerId)
            return p->nickname;
    }
    return "Player";
}

SendBufferPool::Lease RoomService::encodeServerMessage(
    const ::game::messages::ServerMessage& msg) {
#ifdef CABO_ENABLE_SEND_PATH_STATS
    ++sendPathStats_.encodedFrames;
#endif
    auto payload = SendBufferPool::threadLocal().acquire();
    auto frame = SendBufferPool::threadLocal().acquire();
    msg.SerializeToString(&payload.get());
    WebSocketCodec::encode(payload.get(), &frame.get());
    return frame;
}

void RoomService::sendFrame(const TcpConnectionPtr& conn,
                            const std::string& frame) {
    if (!sendFunc_ || !conn) return;
    sendFunc_(conn, frame);
}

void RoomService::sendTo(const TcpConnectionPtr& conn,
                          const ::game::messages::ServerMessage& msg) {
    if (!sendFunc_ || !conn) return;
    auto frame = encodeServerMessage(msg);
    sendFrame(conn, frame.get());
}

void RoomService::fillRoomState(const Room& room, ::game::room::RoomState* state) const {
    if (!state) return;

    state->Clear();
    state->set_room_id(room.roomId);
    state->set_room_code(room.roomCode);
    state->set_max_players(room.maxPlayers);
    state->set_state(room.state);
    state->set_host_player_id(room.hostPlayerId);

    for (auto& p : room.players) {
        auto* ppi = state->add_players();
        ppi->set_player_id(p->playerId);
        ppi->set_nickname(p->nickname);
        ppi->set_character_id(p->characterId);
        ppi->set_seat_id(p->seatId);
        ppi->set_is_ready(p->isReady);
        ppi->set_is_host(p->isHost);
        ppi->set_is_connected(p->isConnected);
        ppi->set_total_score(p->totalScore);
    }
}

void RoomService::fillRoomSummary(const Room& room, ::game::room::RoomSummary* summary) const {
    if (!summary) return;
    summary->set_room_id(room.roomId);
    summary->set_room_code(room.roomCode);
    summary->set_host_nickname(hostNickname(room));
    summary->set_player_count(static_cast<int32_t>(room.players.size()));
    summary->set_max_players(room.maxPlayers);
    summary->set_is_full(static_cast<int>(room.players.size()) >= room.maxPlayers);
}

void RoomService::fillAccessItem(const RoomAccessRecord& record,
                                 ::game::room::RoomAccessItem* item) const {
    if (!item) return;
    item->set_access_id(record.accessId);
    item->set_type(record.type);
    item->set_status(record.status);
    item->set_room_id(record.roomId);
    item->set_room_code(record.roomCode);
    item->set_host_nickname(record.hostNickname);
    item->set_requester_player_id(record.requesterPlayerId);
    item->set_requester_nickname(record.requesterNickname);
    item->set_lobby_player_id(record.lobbyPlayerId);
    item->set_lobby_nickname(record.lobbyNickname);
    item->set_created_time_ms(record.createdTimeMs);
}

void RoomService::fillAccessDecision(const RoomAccessRecord& record,
                                     ::game::room::RoomAccessDecisionNotify* notify,
                                     ::game::room::RoomAccessStatus status,
                                     int32_t errorCode,
                                     const std::string& message) const {
    if (!notify) return;
    notify->set_access_id(record.accessId);
    notify->set_type(record.type);
    notify->set_status(status);
    notify->mutable_error()->set_code(errorCode);
    notify->mutable_error()->set_message(message);
    notify->set_room_id(record.roomId);
    notify->set_room_code(record.roomCode);
    notify->set_message(message);
}

void RoomService::sendRoomStateToAll(const std::shared_ptr<Room>& room) {
    if (!room) return;
    for (auto& p : room->players) {
        if (p->conn && p->isConnected)
            sendRoomState(room->roomId, p->conn);
    }
}

void RoomService::sendRoomListTo(const TcpConnectionPtr& conn) {
    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_room_list_notify();
    for (const auto& kv : rooms_) {
        const auto& room = kv.second;
        if (!room || room->state != ::game::common::ROOM_STATE_WAITING)
            continue;
        fillRoomSummary(*room, notify->add_rooms());
    }
    sendTo(conn, msg);
}

void RoomService::broadcastRoomListToLobby() {
    for (const auto& kv : lobbyPlayers_) {
        if (kv.second && kv.second->conn)
            sendRoomListTo(kv.second->conn);
    }
}

void RoomService::broadcastOnlineLobbyPlayers() {
    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_online_lobby_players_notify();
    for (const auto& kv : lobbyPlayers_) {
        const auto& lobby = kv.second;
        if (!lobby || !lobby->conn)
            continue;
        auto* p = notify->add_players();
        p->set_lobby_player_id(lobby->lobbyPlayerId);
        p->set_nickname(lobby->nickname);
        p->set_character_id(lobby->characterId);
    }

    for (const auto& kv : rooms_) {
        for (const auto& player : kv.second->players) {
            if (player->conn && player->isConnected)
                sendTo(player->conn, msg);
        }
    }
    for (const auto& kv : lobbyPlayers_) {
        if (kv.second && kv.second->conn)
            sendTo(kv.second->conn, msg);
    }
}

void RoomService::sendAccessInboxTo(const TcpConnectionPtr& conn) {
    if (!conn) return;
    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_room_access_inbox_notify();

    int64_t lobbyPlayerId = 0;
    auto lit = lobbyPlayersByConn_.find(conn.get());
    if (lit != lobbyPlayersByConn_.end())
        lobbyPlayerId = lit->second;

    int64_t playerId = 0;
    int64_t roomId = 0;
    for (const auto& kv : rooms_) {
        for (const auto& p : kv.second->players) {
            if (p->isConnected && p->conn && p->conn.get() == conn.get()) {
                playerId = p->playerId;
                roomId = kv.second->roomId;
                break;
            }
        }
        if (playerId > 0) break;
    }

    for (const auto& kv : accessRecords_) {
        const auto& record = kv.second;
        if (record.status != ::game::room::ROOM_ACCESS_STATUS_PENDING)
            continue;
        if (record.type == ::game::room::ROOM_ACCESS_TYPE_JOIN_APPLICATION
            && ((roomId > 0 && record.roomId == roomId)
                || (lobbyPlayerId > 0 && record.lobbyPlayerId == lobbyPlayerId))) {
            fillAccessItem(record, notify->add_items());
        } else if (record.type == ::game::room::ROOM_ACCESS_TYPE_ROOM_INVITATION
                   && ((lobbyPlayerId > 0 && record.lobbyPlayerId == lobbyPlayerId)
                       || (roomId > 0 && record.roomId == roomId))) {
            fillAccessItem(record, notify->add_items());
        }
    }
    sendTo(conn, msg);
}

void RoomService::broadcastAccessInboxes() {
    for (const auto& kv : rooms_) {
        for (const auto& p : kv.second->players) {
            if (p->conn && p->isConnected)
                sendAccessInboxTo(p->conn);
        }
    }
    for (const auto& kv : lobbyPlayers_) {
        if (kv.second && kv.second->conn)
            sendAccessInboxTo(kv.second->conn);
    }
}

void RoomService::expireLobbyAccessRecords(int64_t lobbyPlayerId) {
    for (auto& kv : accessRecords_) {
        auto& record = kv.second;
        if (record.lobbyPlayerId == lobbyPlayerId
            && record.status == ::game::room::ROOM_ACCESS_STATUS_PENDING) {
            record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        }
    }
}

void RoomService::expireRoomAccessRecords(int64_t roomId) {
    for (auto& kv : accessRecords_) {
        auto& record = kv.second;
        if (record.roomId == roomId
            && record.status == ::game::room::ROOM_ACCESS_STATUS_PENDING) {
            record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        }
    }
}

void RoomService::removeLobbyPlayer(int64_t lobbyPlayerId) {
    auto it = lobbyPlayers_.find(lobbyPlayerId);
    if (it == lobbyPlayers_.end())
        return;
    if (it->second && it->second->conn)
        lobbyPlayersByConn_.erase(it->second->conn.get());
    lobbyPlayers_.erase(it);
    expireLobbyAccessRecords(lobbyPlayerId);
}

std::shared_ptr<PlayerSession> RoomService::addLobbyPlayerToRoom(
    const std::shared_ptr<LobbyPlayer>& lobbyPlayer,
    const std::shared_ptr<Room>& room) {
    if (!lobbyPlayer || !room)
        return nullptr;

    auto player = std::make_shared<PlayerSession>();
    player->playerId = nextPlayerId();
    player->nickname = lobbyPlayer->nickname.empty() ? "Player" : lobbyPlayer->nickname;
    player->characterId = normalizeCharacterId(lobbyPlayer->characterId);
    player->seatId = static_cast<int32_t>(room->players.size());
    player->isReady = false;
    player->isHost = false;
    player->isConnected = true;
    player->totalScore = 0;
    player->conn = lobbyPlayer->conn;
    player->sessionToken = generateSessionToken();

    room->players.push_back(player);
    playerRooms_[player->playerId] = room;
    removeLobbyPlayer(lobbyPlayer->lobbyPlayerId);
    return player;
}

void RoomService::sendAccessDecisionToLobby(
    const RoomAccessRecord& record,
    ::game::room::RoomAccessStatus status,
    int32_t errorCode,
    const std::string& message,
    const std::shared_ptr<PlayerSession>& joinedPlayer) {
    TcpConnectionPtr targetConn;
    auto lobbyIt = lobbyPlayers_.find(record.lobbyPlayerId);
    if (lobbyIt != lobbyPlayers_.end() && lobbyIt->second)
        targetConn = lobbyIt->second->conn;
    if (!targetConn && joinedPlayer)
        targetConn = joinedPlayer->conn;
    if (!targetConn)
        return;

    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_room_access_decision_notify();
    fillAccessDecision(record, notify, status, errorCode, message);
    if (joinedPlayer) {
        notify->set_player_id(joinedPlayer->playerId);
        notify->set_session_token(joinedPlayer->sessionToken);
        notify->set_seat_id(joinedPlayer->seatId);
    }
    sendTo(targetConn, msg);
}

void RoomService::broadcastToRoom(int64_t roomId,
                                   const ::game::messages::ServerMessage& msg,
                                   int64_t excludePlayerId) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    auto it = rooms_.find(roomId);
    if (it == rooms_.end()) return;

    SendBufferPool::Lease frame;
    bool hasFrame = false;
    for (auto& player : it->second->players) {
        if (player->playerId == excludePlayerId) continue;
        if (!player->conn || !player->isConnected) continue;
        if (!hasFrame) {
            frame = encodeServerMessage(msg);
            hasFrame = true;
        }
        sendFrame(player->conn, frame.get());
    }
}

void RoomService::sendRoomState(int64_t roomId, const TcpConnectionPtr& conn) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    auto it = rooms_.find(roomId);
    if (it == rooms_.end()) return;
    auto& room = it->second;

    ::game::messages::ServerMessage msg;
    auto* notify = msg.mutable_room_state_notify();
    notify->set_room_id(room->roomId);
    fillRoomState(*room, notify->mutable_room());

    sendTo(conn, msg);
}

// ── Handle CreateRoom ──

void RoomService::handleCreateRoom(const TcpConnectionPtr& conn,
                                    const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.create_room_req();

    // Check if this connection is already in a room
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
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
    player->characterId = normalizeCharacterId(req.character_id());
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
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        rooms_[room->roomId] = room;
        playerRooms_[player->playerId] = room;
        auto lobbyIt = lobbyPlayersByConn_.find(conn.get());
        if (lobbyIt != lobbyPlayersByConn_.end())
            removeLobbyPlayer(lobbyIt->second);
    }

    LOG_INFO("[Room] Creating room for '%s' character='%s' (max %d)...",
             player->nickname.c_str(), player->characterId.c_str(), room->maxPlayers);

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
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();

    LOG_INFO("[Room] Room %s created — player %lld (host)", room->roomCode.c_str(), (long long)player->playerId);
}

// ── Handle JoinRoom ──

void RoomService::handleJoinRoom(const TcpConnectionPtr& conn,
                                  const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.join_room_req();

    // Check if this connection is already in a room
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
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
        std::lock_guard<std::recursive_mutex> lock(mutex_);
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
    player->characterId = normalizeCharacterId(req.character_id());
    player->seatId = static_cast<int32_t>(room->players.size());
    player->isReady = false;
    player->isHost = false;
    player->isConnected = true;
    player->totalScore = 0;
    player->conn = conn;
    player->sessionToken = generateSessionToken();

    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        room->players.push_back(player);
        playerRooms_[player->playerId] = room;
        auto lobbyIt = lobbyPlayersByConn_.find(conn.get());
        if (lobbyIt != lobbyPlayersByConn_.end())
            removeLobbyPlayer(lobbyIt->second);
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
    jpi->set_character_id(player->characterId);
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

    LOG_INFO("[Room] Player %s character='%s' joined room %s (seat %d, %zu/%d players)",
             player->nickname.c_str(), player->characterId.c_str(), room->roomCode.c_str(),
             player->seatId, room->players.size(), room->maxPlayers);
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();
}

void RoomService::handleEnterLobby(const TcpConnectionPtr& conn,
                                   const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.enter_lobby_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_enter_lobby_rsp();
    rsp->set_request_id(req.request_id());

    if (!conn) {
        rsp->mutable_error()->set_code(4001);
        rsp->mutable_error()->set_message("Invalid connection");
        sendTo(conn, rspMsg);
        return;
    }
    if (isConnectionInAnyRoom(conn)) {
        rsp->mutable_error()->set_code(4002);
        rsp->mutable_error()->set_message("Already in a room");
        sendTo(conn, rspMsg);
        return;
    }

    std::shared_ptr<LobbyPlayer> lobby;
    auto byConn = lobbyPlayersByConn_.find(conn.get());
    if (byConn != lobbyPlayersByConn_.end()) {
        auto it = lobbyPlayers_.find(byConn->second);
        if (it != lobbyPlayers_.end())
            lobby = it->second;
    }
    if (!lobby) {
        lobby = std::make_shared<LobbyPlayer>();
        lobby->lobbyPlayerId = nextLobbyPlayerId();
    }

    lobby->nickname = req.nickname().empty() ? "Player" : req.nickname();
    lobby->characterId = normalizeCharacterId(req.character_id());
    lobby->conn = conn;
    lobbyPlayers_[lobby->lobbyPlayerId] = lobby;
    lobbyPlayersByConn_[conn.get()] = lobby->lobbyPlayerId;

    rsp->mutable_error()->set_code(0);
    rsp->set_lobby_player_id(lobby->lobbyPlayerId);
    sendTo(conn, rspMsg);
    sendRoomListTo(conn);
    sendAccessInboxTo(conn);
    broadcastOnlineLobbyPlayers();
}

void RoomService::handleLeaveLobby(const TcpConnectionPtr& conn,
                                   const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.leave_lobby_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_leave_lobby_rsp();
    rsp->set_request_id(req.request_id());

    auto lobby = findLobbyPlayerForConnection(req.lobby_player_id(), conn);
    if (!lobby) {
        rsp->mutable_error()->set_code(4003);
        rsp->mutable_error()->set_message("Lobby player not found");
        sendTo(conn, rspMsg);
        return;
    }

    removeLobbyPlayer(req.lobby_player_id());
    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();
}

void RoomService::handleListRooms(const TcpConnectionPtr& conn,
                                  const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.list_rooms_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_list_rooms_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    for (const auto& kv : rooms_) {
        const auto& room = kv.second;
        if (!room || room->state != ::game::common::ROOM_STATE_WAITING)
            continue;
        fillRoomSummary(*room, rsp->add_rooms());
    }
    sendTo(conn, rspMsg);
}

void RoomService::handleApplyJoinRoom(const TcpConnectionPtr& conn,
                                      const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.apply_join_room_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_apply_join_room_rsp();
    rsp->set_request_id(req.request_id());

    auto lobby = findLobbyPlayerForConnection(req.lobby_player_id(), conn);
    auto fail = [&](int code, const std::string& message) {
        rsp->mutable_error()->set_code(code);
        rsp->mutable_error()->set_message(message);
        sendTo(conn, rspMsg);
    };

    if (!lobby) {
        fail(4101, "Lobby player not found");
        return;
    }
    if (isConnectionInAnyRoom(conn)) {
        fail(4102, "Already in a room");
        return;
    }

    std::shared_ptr<Room> room;
    if (req.room_id() > 0) {
        auto it = rooms_.find(req.room_id());
        if (it != rooms_.end())
            room = it->second;
    }
    if (!room && !req.room_code().empty())
        room = findRoomByCode(req.room_code());
    if (!room) {
        fail(4103, "Room not found");
        return;
    }

    std::string roomError;
    if (!isRoomJoinable(*room, &roomError)) {
        fail(4104, roomError);
        return;
    }

    for (const auto& kv : accessRecords_) {
        const auto& record = kv.second;
        if (record.type == ::game::room::ROOM_ACCESS_TYPE_JOIN_APPLICATION
            && record.status == ::game::room::ROOM_ACCESS_STATUS_PENDING
            && record.roomId == room->roomId
            && record.lobbyPlayerId == lobby->lobbyPlayerId) {
            rsp->mutable_error()->set_code(0);
            rsp->set_access_id(record.accessId);
            sendTo(conn, rspMsg);
            return;
        }
    }

    RoomAccessRecord record;
    record.accessId = nextAccessId();
    record.type = ::game::room::ROOM_ACCESS_TYPE_JOIN_APPLICATION;
    record.status = ::game::room::ROOM_ACCESS_STATUS_PENDING;
    record.roomId = room->roomId;
    record.roomCode = room->roomCode;
    record.hostNickname = hostNickname(*room);
    record.lobbyPlayerId = lobby->lobbyPlayerId;
    record.lobbyNickname = lobby->nickname;
    record.createdTimeMs = nowMs();
    accessRecords_[record.accessId] = record;

    rsp->mutable_error()->set_code(0);
    rsp->set_access_id(record.accessId);
    sendTo(conn, rspMsg);
    broadcastAccessInboxes();
}

void RoomService::handleRespondJoinApplication(
    const TcpConnectionPtr& conn,
    const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.respond_join_application_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_respond_join_application_rsp();
    rsp->set_request_id(req.request_id());

    auto fail = [&](int code, const std::string& message) {
        rsp->mutable_error()->set_code(code);
        rsp->mutable_error()->set_message(message);
        sendTo(conn, rspMsg);
    };

    auto accessIt = accessRecords_.find(req.access_id());
    if (accessIt == accessRecords_.end()
        || accessIt->second.type != ::game::room::ROOM_ACCESS_TYPE_JOIN_APPLICATION
        || accessIt->second.status != ::game::room::ROOM_ACCESS_STATUS_PENDING) {
        fail(4201, "Application not found");
        return;
    }
    auto& record = accessIt->second;
    auto roomIt = rooms_.find(record.roomId);
    if (roomIt == rooms_.end()) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        fail(4202, "Room not found");
        return;
    }
    auto room = roomIt->second;
    auto responder = findPlayer(*room, req.player_id());
    if (!responder || !isPlayerConnection(*responder, conn)
        || responder->playerId != room->hostPlayerId) {
        fail(4203, "Only host can respond to applications");
        return;
    }

    if (!req.approve()) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_REJECTED;
        rsp->mutable_error()->set_code(0);
        sendTo(conn, rspMsg);
        sendAccessDecisionToLobby(record, record.status, 0, "Application rejected");
        broadcastAccessInboxes();
        return;
    }

    auto lobbyIt = lobbyPlayers_.find(record.lobbyPlayerId);
    if (lobbyIt == lobbyPlayers_.end() || !lobbyIt->second) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        fail(4204, "Applicant is offline");
        broadcastAccessInboxes();
        return;
    }

    std::string roomError;
    if (!isRoomJoinable(*room, &roomError)) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        sendAccessDecisionToLobby(record, record.status, 4205, roomError);
        fail(4205, roomError);
        broadcastAccessInboxes();
        return;
    }

    auto joinedPlayer = addLobbyPlayerToRoom(lobbyIt->second, room);
    record.status = ::game::room::ROOM_ACCESS_STATUS_APPROVED;
    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);
    sendAccessDecisionToLobby(record, record.status, 0, "Application approved", joinedPlayer);
    sendRoomStateToAll(room);
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();
}

void RoomService::handleInviteLobbyPlayer(const TcpConnectionPtr& conn,
                                          const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.invite_lobby_player_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_invite_lobby_player_rsp();
    rsp->set_request_id(req.request_id());

    auto fail = [&](int code, const std::string& message) {
        rsp->mutable_error()->set_code(code);
        rsp->mutable_error()->set_message(message);
        sendTo(conn, rspMsg);
    };

    auto roomIt = rooms_.find(req.room_id());
    if (roomIt == rooms_.end()) {
        fail(4301, "Room not found");
        return;
    }
    auto room = roomIt->second;
    auto requester = findPlayer(*room, req.player_id());
    if (!requester || !isPlayerConnection(*requester, conn)) {
        fail(4302, "Requester is not in this room");
        return;
    }
    std::string roomError;
    if (!isRoomJoinable(*room, &roomError)) {
        fail(4303, roomError);
        return;
    }
    auto lobbyIt = lobbyPlayers_.find(req.lobby_player_id());
    if (lobbyIt == lobbyPlayers_.end() || !lobbyIt->second) {
        fail(4304, "Lobby player not found");
        return;
    }

    for (const auto& kv : accessRecords_) {
        const auto& record = kv.second;
        if (record.type == ::game::room::ROOM_ACCESS_TYPE_ROOM_INVITATION
            && record.status == ::game::room::ROOM_ACCESS_STATUS_PENDING
            && record.roomId == room->roomId
            && record.lobbyPlayerId == lobbyIt->second->lobbyPlayerId) {
            rsp->mutable_error()->set_code(0);
            rsp->set_access_id(record.accessId);
            sendTo(conn, rspMsg);
            return;
        }
    }

    RoomAccessRecord record;
    record.accessId = nextAccessId();
    record.type = ::game::room::ROOM_ACCESS_TYPE_ROOM_INVITATION;
    record.status = ::game::room::ROOM_ACCESS_STATUS_PENDING;
    record.roomId = room->roomId;
    record.roomCode = room->roomCode;
    record.hostNickname = hostNickname(*room);
    record.requesterPlayerId = requester->playerId;
    record.requesterNickname = requester->nickname;
    record.lobbyPlayerId = lobbyIt->second->lobbyPlayerId;
    record.lobbyNickname = lobbyIt->second->nickname;
    record.createdTimeMs = nowMs();
    accessRecords_[record.accessId] = record;

    rsp->mutable_error()->set_code(0);
    rsp->set_access_id(record.accessId);
    sendTo(conn, rspMsg);
    broadcastAccessInboxes();
}

void RoomService::handleRespondRoomInvitation(
    const TcpConnectionPtr& conn,
    const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.respond_room_invitation_req();

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_respond_room_invitation_rsp();
    rsp->set_request_id(req.request_id());

    auto fail = [&](int code, const std::string& message) {
        rsp->mutable_error()->set_code(code);
        rsp->mutable_error()->set_message(message);
        sendTo(conn, rspMsg);
    };

    auto lobby = findLobbyPlayerForConnection(req.lobby_player_id(), conn);
    if (!lobby) {
        fail(4401, "Lobby player not found");
        return;
    }

    auto accessIt = accessRecords_.find(req.access_id());
    if (accessIt == accessRecords_.end()
        || accessIt->second.type != ::game::room::ROOM_ACCESS_TYPE_ROOM_INVITATION
        || accessIt->second.status != ::game::room::ROOM_ACCESS_STATUS_PENDING
        || accessIt->second.lobbyPlayerId != lobby->lobbyPlayerId) {
        fail(4402, "Invitation not found");
        return;
    }
    auto& record = accessIt->second;

    if (!req.approve()) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_REJECTED;
        rsp->mutable_error()->set_code(0);
        sendTo(conn, rspMsg);
        sendAccessDecisionToLobby(record, record.status, 0, "Invitation rejected");
        broadcastAccessInboxes();
        return;
    }

    auto roomIt = rooms_.find(record.roomId);
    if (roomIt == rooms_.end()) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        sendAccessDecisionToLobby(record, record.status, 4403, "Room not found");
        fail(4403, "Room not found");
        broadcastAccessInboxes();
        return;
    }
    auto room = roomIt->second;
    std::string roomError;
    if (!isRoomJoinable(*room, &roomError)) {
        record.status = ::game::room::ROOM_ACCESS_STATUS_EXPIRED;
        sendAccessDecisionToLobby(record, record.status, 4404, roomError);
        fail(4404, roomError);
        broadcastAccessInboxes();
        return;
    }

    auto joinedPlayer = addLobbyPlayerToRoom(lobby, room);
    record.status = ::game::room::ROOM_ACCESS_STATUS_APPROVED;
    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);
    sendAccessDecisionToLobby(record, record.status, 0, "Invitation approved", joinedPlayer);
    sendRoomStateToAll(room);
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();
}

// ── Handle LeaveRoom ──

bool RoomService::handleLeaveRoom(const TcpConnectionPtr& conn,
                                   const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.leave_room_req();
    int64_t playerId = req.player_id();
    int64_t roomId = req.room_id();

    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
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

    if (!room) return false;

    int64_t newHostId = 0;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        auto& players = room->players;
        auto leavingPlayer = findPlayer(*room, playerId);
        if (!leavingPlayer || !isPlayerConnection(*leavingPlayer, conn)) {
            ::game::messages::ServerMessage rspMsg;
            auto* rsp = rspMsg.mutable_leave_room_rsp();
            rsp->set_request_id(req.request_id());
            rsp->mutable_error()->set_code(2004);
            rsp->mutable_error()->set_message("Player connection mismatch");
            sendTo(conn, rspMsg);
            return false;
        }
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
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();

    return true;
}

// ── Handle Ready ──

void RoomService::handleReady(const TcpConnectionPtr& conn,
                               const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.ready_req();
    int64_t playerId = req.player_id();
    bool ready = req.is_ready();

    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
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
        auto player = findPlayer(*room, playerId);
        if (!player || !isPlayerConnection(*player, conn)) {
            ::game::messages::ServerMessage rspMsg;
            auto* rsp = rspMsg.mutable_ready_rsp();
            rsp->set_request_id(req.request_id());
            rsp->mutable_error()->set_code(2004);
            rsp->mutable_error()->set_message("Player connection mismatch");
            sendTo(conn, rspMsg);
            return;
        }
        player->isReady = ready;
        LOG_INFO("[Room] Player %s set ready=%d in room %s",
                 player->nickname.c_str(), ready, room->roomCode.c_str());
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

bool RoomService::handleStartGame(const TcpConnectionPtr& conn,
                                   const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.start_game_req();
    int64_t playerId = req.player_id();

    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        auto it = playerRooms_.find(playerId);
        if (it == playerRooms_.end()) {
            ::game::messages::ServerMessage rspMsg;
            auto* rsp = rspMsg.mutable_start_game_rsp();
            rsp->set_request_id(req.request_id());
            rsp->mutable_error()->set_code(2001);
            rsp->mutable_error()->set_message("Not in room");
            sendTo(conn, rspMsg);
            return false;
        }
        room = it->second;
        auto player = findPlayer(*room, playerId);
        if (!player || !isPlayerConnection(*player, conn)) {
            ::game::messages::ServerMessage rspMsg;
            auto* rsp = rspMsg.mutable_start_game_rsp();
            rsp->set_request_id(req.request_id());
            rsp->mutable_error()->set_code(2004);
            rsp->mutable_error()->set_message("Player connection mismatch");
            sendTo(conn, rspMsg);
            return false;
        }
    }

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_start_game_rsp();
    rsp->set_request_id(req.request_id());

    // Validate: must be host
    if (playerId != room->hostPlayerId) {
        rsp->mutable_error()->set_code(2001);
        rsp->mutable_error()->set_message("Only host can start game");
        sendTo(conn, rspMsg);
        return false;
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
        return false;
    }

    // Start!
    room->state = ::game::common::ROOM_STATE_PLAYING;
    expireRoomAccessRecords(room->roomId);

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
    broadcastRoomListToLobby();
    broadcastAccessInboxes();

    return true;
}

void RoomService::handleRoomChat(const TcpConnectionPtr& conn,
                                  const ::game::messages::ClientMessage& msg) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const auto& req = msg.room_chat_req();
    const int64_t playerId = req.player_id();
    const int64_t roomId = req.room_id();
    const auto chatType = req.type();
    const std::string text = trimAsciiWhitespace(req.text());
    const std::string stickerPack = trimAsciiWhitespace(req.sticker_pack());
    const std::string stickerName = trimAsciiWhitespace(req.sticker_name());

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_room_chat_rsp();
    rsp->set_request_id(req.request_id());

    auto fail = [&](int code, const std::string& message) {
        rsp->mutable_error()->set_code(code);
        rsp->mutable_error()->set_message(message);
        sendTo(conn, rspMsg);
    };

    if (playerId <= 0 || roomId <= 0) {
        fail(3001, "Invalid room chat identity");
        return;
    }

    if (chatType == ::game::room::ROOM_CHAT_TYPE_TEXT) {
        if (text.empty()) {
            fail(3002, "Text message is empty");
            return;
        }
        if (utf8CodePointCount(text) > 120) {
            fail(3003, "Text message is too long");
            return;
        }
    } else if (chatType == ::game::room::ROOM_CHAT_TYPE_STICKER) {
        if (stickerPack.empty() || stickerName.empty()) {
            fail(3004, "Sticker pack and name are required");
            return;
        }
    } else {
        fail(3005, "Unsupported room chat type");
        return;
    }

    std::string nickname;
    int64_t messageId = 0;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        auto roomIt = rooms_.find(roomId);
        auto playerRoomIt = playerRooms_.find(playerId);
        if (roomIt == rooms_.end() || playerRoomIt == playerRooms_.end()
            || playerRoomIt->second.get() != roomIt->second.get()) {
            rsp->mutable_error()->set_code(3006);
            rsp->mutable_error()->set_message("Player is not in this room");
            sendTo(conn, rspMsg);
            return;
        }

        bool foundOnlineSender = false;
        for (auto& p : roomIt->second->players) {
            if (p->playerId != playerId) {
                continue;
            }
            foundOnlineSender = p->isConnected && p->conn && p->conn.get() == conn.get();
            nickname = p->nickname;
            break;
        }

        if (!foundOnlineSender) {
            rsp->mutable_error()->set_code(3007);
            rsp->mutable_error()->set_message("Player connection is not active in this room");
            sendTo(conn, rspMsg);
            return;
        }

        messageId = nextChatMessageId();
    }

    rsp->mutable_error()->set_code(0);
    sendTo(conn, rspMsg);

    ::game::messages::ServerMessage notifyMsg;
    auto* notify = notifyMsg.mutable_room_chat_notify();
    notify->set_room_id(roomId);
    notify->set_message_id(messageId);
    notify->set_sender_player_id(playerId);
    notify->set_sender_nickname(nickname);
    notify->set_type(chatType);
    notify->set_text(chatType == ::game::room::ROOM_CHAT_TYPE_TEXT ? text : "");
    notify->set_sticker_pack(chatType == ::game::room::ROOM_CHAT_TYPE_STICKER ? stickerPack : "");
    notify->set_sticker_name(chatType == ::game::room::ROOM_CHAT_TYPE_STICKER ? stickerName : "");
    notify->set_server_time_ms(nowMs());
    broadcastToRoom(roomId, notifyMsg);
}

bool RoomService::reconnectSession(const std::string& sessionToken,
                                   const TcpConnectionPtr& conn,
                                   ReconnectSessionResult* result) {
    if (result) {
        *result = ReconnectSessionResult{};
    }

    const std::string token = trimAsciiWhitespace(sessionToken);
    if (token.empty()) {
        if (result) {
            result->errorCode = 1016;
            result->errorMessage = "session_token is empty";
        }
        return false;
    }

    std::lock_guard<std::recursive_mutex> lock(mutex_);
    const int64_t now = nowMs();

    for (auto& kv : rooms_) {
        auto& room = kv.second;
        for (auto& player : room->players) {
            if (player->sessionToken != token)
                continue;

            if (!player->isConnected
                && player->disconnectedAtMs > 0
                && now - player->disconnectedAtMs > kReconnectWindowMs) {
                if (result) {
                    result->errorCode = 1017;
                    result->errorMessage = "Reconnect window expired";
                    result->playerId = player->playerId;
                    result->roomId = room->roomId;
                }
                return false;
            }

            player->conn = conn;
            player->isConnected = true;
            player->disconnectedAtMs = 0;

            if (result) {
                result->ok = true;
                result->errorCode = 0;
                result->playerId = player->playerId;
                result->roomId = room->roomId;
                result->isInGame = room->state == ::game::common::ROOM_STATE_PLAYING;
                fillRoomState(*room, &result->roomState);
            }
            return true;
        }
    }

    if (result) {
        result->errorCode = 1016;
        result->errorMessage = "session_token invalid";
    }
    return false;
}

void RoomService::markGameFinished(int64_t roomId) {
    std::lock_guard<std::recursive_mutex> lock(mutex_);
    std::shared_ptr<Room> room;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        auto it = rooms_.find(roomId);
        if (it == rooms_.end()) return;

        room = it->second;
        room->state = ::game::common::ROOM_STATE_WAITING;

        bool hostStillPresent = false;
        for (auto& p : room->players) {
            p->isReady = false;
            p->isHost = false;
            p->totalScore = 0;
            if (p->playerId == room->hostPlayerId)
                hostStillPresent = true;
        }

        if (!room->players.empty()) {
            if (!hostStillPresent)
                room->hostPlayerId = room->players[0]->playerId;

            bool hostAssigned = false;
            for (auto& p : room->players) {
                if (p->playerId == room->hostPlayerId) {
                    p->isHost = true;
                    hostAssigned = true;
                    break;
                }
            }

            if (!hostAssigned) {
                room->hostPlayerId = room->players[0]->playerId;
                room->players[0]->isHost = true;
            }
        } else {
            room->hostPlayerId = 0;
        }
    }

    LOG_INFO("[Room] Game finished in room %lld; returned to waiting", (long long)roomId);

    for (auto& p : room->players) {
        if (p->conn && p->isConnected)
            sendRoomState(roomId, p->conn);
    }
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();
}

// ── Connection closed ──

void RoomService::onConnectionClosed(const TcpConnectionPtr& conn) {
    if (!conn) return;
    std::lock_guard<std::recursive_mutex> lock(mutex_);

    std::vector<int64_t> roomsToUpdate;
    {
        std::lock_guard<std::recursive_mutex> lock(mutex_);
        for (auto& kv : rooms_) {
            auto& room = kv.second;
            for (auto& player : room->players) {
                if (player->conn && player->conn.get() == conn.get()) {
                    LOG_INFO("[Room] Player %lld disconnected from room %s",
                             (long long)player->playerId, room->roomCode.c_str());
                    player->isConnected = false;
                    player->conn.reset();
                    player->disconnectedAtMs = nowMs();
                    roomsToUpdate.push_back(room->roomId);
                    break;
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
    auto lobbyIt = lobbyPlayersByConn_.find(conn.get());
    if (lobbyIt != lobbyPlayersByConn_.end())
        removeLobbyPlayer(lobbyIt->second);
    broadcastRoomListToLobby();
    broadcastOnlineLobbyPlayers();
    broadcastAccessInboxes();
}

} // namespace game
