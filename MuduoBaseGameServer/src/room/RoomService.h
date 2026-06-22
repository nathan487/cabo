#pragma once
#include "proto/room.pb.h"
#include "proto/common.pb.h"
#include "proto/messages.pb.h"
#include "common/SendBufferPool.h"
#include <cstddef>
#include <functional>
#include <memory>
#include <mutex>
#include <random>
#include <string>
#include <unordered_map>
#include <vector>

class TcpConnection;

namespace game {

using TcpConnectionPtr = std::shared_ptr<TcpConnection>;

struct PlayerSession {
    int64_t playerId = 0;
    std::string nickname;
    std::string characterId = "pomelo";
    int32_t seatId = 0;
    bool isReady = false;
    bool isHost = false;
    bool isConnected = true;
    int32_t totalScore = 0;
    TcpConnectionPtr conn;
    std::string sessionToken;
    int64_t disconnectedAtMs = 0;
};

struct Room {
    int64_t roomId = 0;
    std::string roomCode;
    int32_t maxPlayers = 4;
    ::game::common::RoomStateType state = ::game::common::ROOM_STATE_WAITING;
    int64_t hostPlayerId = 0;
    std::vector<std::shared_ptr<PlayerSession>> players;
};

struct LobbyPlayer {
    int64_t lobbyPlayerId = 0;
    std::string nickname;
    std::string characterId = "pomelo";
    TcpConnectionPtr conn;
};

struct RoomAccessRecord {
    int64_t accessId = 0;
    ::game::room::RoomAccessType type = ::game::room::ROOM_ACCESS_TYPE_UNKNOWN;
    ::game::room::RoomAccessStatus status = ::game::room::ROOM_ACCESS_STATUS_UNKNOWN;
    int64_t roomId = 0;
    std::string roomCode;
    std::string hostNickname;
    int64_t requesterPlayerId = 0;
    std::string requesterNickname;
    int64_t lobbyPlayerId = 0;
    std::string lobbyNickname;
    int64_t createdTimeMs = 0;
};

struct PlayerSessionSnapshot {
    int64_t playerId = 0;
    std::string nickname;
    std::string characterId;
    int32_t seatId = 0;
    bool isConnected = false;
    int32_t totalScore = 0;
    TcpConnectionPtr conn;
};

struct RoomGameStartSnapshot {
    bool valid = false;
    int64_t roomId = 0;
    int64_t hostPlayerId = 0;
    std::vector<PlayerSessionSnapshot> players;
};

struct ReconnectSessionResult {
    bool ok = false;
    int32_t errorCode = 0;
    std::string errorMessage;
    int64_t playerId = 0;
    int64_t roomId = 0;
    bool isInGame = false;
    ::game::room::RoomState roomState;
};

// Manages rooms and player sessions in memory.
// All room operations are thread-safe.
class RoomService {
public:
    // Callback type for sending a ServerMessage to a specific connection.
    // The service never touches the socket directly.
    using SendFunc = std::function<void(const TcpConnectionPtr& conn,
                                         const std::string& framedData)>;

#ifdef CABO_ENABLE_SEND_PATH_STATS
    struct SendPathStats {
        std::size_t encodedFrames = 0;
    };
#endif

    RoomService();

    void setSendFunc(SendFunc func) { sendFunc_ = std::move(func); }

#ifdef CABO_ENABLE_SEND_PATH_STATS
    const SendPathStats& sendPathStatsForTests() const { return sendPathStats_; }
    void resetSendPathStatsForTests() { sendPathStats_ = {}; }
#endif

    // Access room data (used by GameService to bridge room ↔ game state)
    const std::shared_ptr<Room> getRoom(int64_t roomId) const;
    std::shared_ptr<Room> getRoom(int64_t roomId);
    std::shared_ptr<Room> getRoomMutable(int64_t roomId);
    RoomGameStartSnapshot getGameStartSnapshot(int64_t roomId) const;

    // Request handlers
    void handleCreateRoom(const TcpConnectionPtr& conn,
                          const ::game::messages::ClientMessage& msg);
    void handleJoinRoom(const TcpConnectionPtr& conn,
                        const ::game::messages::ClientMessage& msg);
    bool handleLeaveRoom(const TcpConnectionPtr& conn,
                         const ::game::messages::ClientMessage& msg);
    void handleReady(const TcpConnectionPtr& conn,
                     const ::game::messages::ClientMessage& msg);
    bool handleStartGame(const TcpConnectionPtr& conn,
                         const ::game::messages::ClientMessage& msg);
    void handleRoomChat(const TcpConnectionPtr& conn,
                        const ::game::messages::ClientMessage& msg);
    void handleEnterLobby(const TcpConnectionPtr& conn,
                          const ::game::messages::ClientMessage& msg);
    void handleLeaveLobby(const TcpConnectionPtr& conn,
                          const ::game::messages::ClientMessage& msg);
    void handleListRooms(const TcpConnectionPtr& conn,
                         const ::game::messages::ClientMessage& msg);
    void handleApplyJoinRoom(const TcpConnectionPtr& conn,
                             const ::game::messages::ClientMessage& msg);
    void handleRespondJoinApplication(const TcpConnectionPtr& conn,
                                      const ::game::messages::ClientMessage& msg);
    void handleInviteLobbyPlayer(const TcpConnectionPtr& conn,
                                 const ::game::messages::ClientMessage& msg);
    void handleRespondRoomInvitation(const TcpConnectionPtr& conn,
                                     const ::game::messages::ClientMessage& msg);
    bool reconnectSession(const std::string& sessionToken,
                          const TcpConnectionPtr& conn,
                          ReconnectSessionResult* result);

    // Called by GameService after final GameOver has been broadcast.
    void markGameFinished(int64_t roomId);

    // Called when a connection drops
    void onConnectionClosed(const TcpConnectionPtr& conn);

private:
    SendBufferPool::Lease encodeServerMessage(const ::game::messages::ServerMessage& msg);
    void sendFrame(const TcpConnectionPtr& conn,
                   const std::string& frame);
    void sendTo(const TcpConnectionPtr& conn,
                const ::game::messages::ServerMessage& msg);
    void broadcastToRoom(int64_t roomId,
                         const ::game::messages::ServerMessage& msg,
                         int64_t excludePlayerId = 0);
    void sendRoomState(int64_t roomId, const TcpConnectionPtr& conn);
    void fillRoomState(const Room& room, ::game::room::RoomState* state) const;
    void fillRoomSummary(const Room& room, ::game::room::RoomSummary* summary) const;
    void fillAccessItem(const RoomAccessRecord& record,
                        ::game::room::RoomAccessItem* item) const;
    void fillAccessDecision(const RoomAccessRecord& record,
                            ::game::room::RoomAccessDecisionNotify* notify,
                            ::game::room::RoomAccessStatus status,
                            int32_t errorCode,
                            const std::string& message) const;
    std::string generateRoomCode();
    std::string generateSessionToken();
    int64_t nextPlayerId();
    int64_t nextRoomId();
    int64_t nextChatMessageId();
    int64_t nextLobbyPlayerId();
    int64_t nextAccessId();
    std::shared_ptr<PlayerSession> findPlayer(Room& room, int64_t playerId);
    std::shared_ptr<PlayerSession> findPlayerByConnection(Room& room,
                                                          const TcpConnectionPtr& conn);
    bool isPlayerConnection(const PlayerSession& player,
                            const TcpConnectionPtr& conn) const;
    bool isConnectionInAnyRoom(const TcpConnectionPtr& conn) const;
    std::shared_ptr<Room> findRoomByCode(const std::string& roomCode) const;
    std::shared_ptr<LobbyPlayer> findLobbyPlayerForConnection(int64_t lobbyPlayerId,
                                                              const TcpConnectionPtr& conn) const;
    bool isRoomJoinable(const Room& room, std::string* errorMessage = nullptr) const;
    std::string hostNickname(const Room& room) const;
    void removeLobbyPlayer(int64_t lobbyPlayerId);
    void expireLobbyAccessRecords(int64_t lobbyPlayerId);
    void expireRoomAccessRecords(int64_t roomId);
    void sendRoomListTo(const TcpConnectionPtr& conn);
    void broadcastRoomListToLobby();
    void broadcastOnlineLobbyPlayers();
    void sendAccessInboxTo(const TcpConnectionPtr& conn);
    void broadcastAccessInboxes();
    std::shared_ptr<PlayerSession> addLobbyPlayerToRoom(const std::shared_ptr<LobbyPlayer>& lobbyPlayer,
                                                        const std::shared_ptr<Room>& room);
    void sendAccessDecisionToLobby(const RoomAccessRecord& record,
                                   ::game::room::RoomAccessStatus status,
                                   int32_t errorCode,
                                   const std::string& message,
                                   const std::shared_ptr<PlayerSession>& joinedPlayer = nullptr);
    void sendRoomStateToAll(const std::shared_ptr<Room>& room);

    std::unordered_map<int64_t, std::shared_ptr<Room>> rooms_;
    // Maps playerId -> room (for quick room lookup)
    std::unordered_map<int64_t, std::shared_ptr<Room>> playerRooms_;
    std::unordered_map<int64_t, std::shared_ptr<LobbyPlayer>> lobbyPlayers_;
    std::unordered_map<const TcpConnection*, int64_t> lobbyPlayersByConn_;
    std::unordered_map<int64_t, RoomAccessRecord> accessRecords_;

    SendFunc sendFunc_;
#ifdef CABO_ENABLE_SEND_PATH_STATS
    SendPathStats sendPathStats_;
#endif
    mutable std::recursive_mutex mutex_;
    std::mt19937 rng_;
    int64_t nextPlayerId_ = 10000;
    int64_t nextRoomId_ = 1;
    int64_t nextChatMessageId_ = 1;
    int64_t nextLobbyPlayerId_ = 50000;
    int64_t nextAccessId_ = 1;
};

} // namespace game
