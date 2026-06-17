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
};

struct Room {
    int64_t roomId = 0;
    std::string roomCode;
    int32_t maxPlayers = 4;
    ::game::common::RoomStateType state = ::game::common::ROOM_STATE_WAITING;
    int64_t hostPlayerId = 0;
    std::vector<std::shared_ptr<PlayerSession>> players;
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
    std::string generateRoomCode();
    std::string generateSessionToken();
    int64_t nextPlayerId();
    int64_t nextRoomId();
    int64_t nextChatMessageId();
    std::shared_ptr<PlayerSession> findPlayer(Room& room, int64_t playerId);
    bool isPlayerConnection(const PlayerSession& player,
                            const TcpConnectionPtr& conn) const;

    std::unordered_map<int64_t, std::shared_ptr<Room>> rooms_;
    // Maps playerId -> room (for quick room lookup)
    std::unordered_map<int64_t, std::shared_ptr<Room>> playerRooms_;

    SendFunc sendFunc_;
#ifdef CABO_ENABLE_SEND_PATH_STATS
    SendPathStats sendPathStats_;
#endif
    mutable std::recursive_mutex mutex_;
    std::mt19937 rng_;
    int64_t nextPlayerId_ = 10000;
    int64_t nextRoomId_ = 1;
    int64_t nextChatMessageId_ = 1;
};

} // namespace game
