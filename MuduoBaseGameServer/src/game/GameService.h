#pragma once
#include "proto/game.pb.h"
#include "proto/common.pb.h"
#include "proto/messages.pb.h"
#include "proto/sync.pb.h"
#include "common/SendBufferPool.h"
#include <functional>
#include "common/MessageDispatcher.h" // for TcpConnectionPtr
#include <cstddef>
#include <memory>
#include <mutex>
#include <random>
#include <string>
#include <unordered_map>
#include <vector>

namespace cabogame {

using game::TcpConnectionPtr;

// Per-player game state tracked by the server
struct PlayerGameState {
    int64_t playerId;
    std::string nickname;
    std::string characterId = "pomelo";
    int32_t seatId;
    TcpConnectionPtr conn;
    bool isConnected;
    int64_t disconnectedAtMs = 0;

    // Cards in player's area (starts at 4, can increase via penalty)
    std::vector<::game::common::CardInfo> cards;
    // Which slots the player knows (from initial peek or peekself)
    std::vector<bool> knownSlots;

    // Cumulative score across rounds
    int32_t totalScore = 0;
    int32_t lastRoundScore = 0;
    // Has used 100→50 reset this game?
    bool hasUsedReset = false;

    // Per-round state
    int32_t roundScore() const;
    bool hasKamikaze() const;
};

// Game phases mirror proto GamePhase
enum class GameStep {
    WaitingToStart,
    Playing,          // Normal turn loop
    WaitingDrawDecision, // Player drew from deck, deciding
    FinalRound,       // Steady called, last turns
    Reveal,           // Showing cards, scoring
    GameOver
};

struct GameRoom {
    mutable std::mutex stateMutex;

    int64_t roomId;
    std::string roomCode;
    int32_t maxPlayers;
    int64_t hostPlayerId;

    GameStep step = GameStep::WaitingToStart;

    // Players in seat order
    std::vector<std::shared_ptr<PlayerGameState>> players;

    // Deck
    std::vector<::game::common::CardInfo> drawPile;
    std::vector<::game::common::CardInfo> discardPile;

    // Turn state
    int32_t roundNumber = 0;
    int32_t turnNumber = 0;
    int32_t currentPlayerSeat = 0;
    int32_t steadyCallerSeat = -1;
    int32_t finalRoundRemaining = 0;

    // Current drawn card (during WaitingDrawDecision step)
    ::game::common::CardInfo pendingDrawnCard;
    bool pendingDrewFromDiscard = false;

    // Pending early-end request (awaiting host decision)
    int64_t pendingEndGameRequesterPlayerId = 0;
    TcpConnectionPtr pendingEndGameRequesterConn;
};

// Authoritative game logic. One instance manages all games.
class GameService {
public:
    using SendFunc = std::function<void(const TcpConnectionPtr&, const std::string&)>;
    using GameFinishedFunc = std::function<void(int64_t)>;

#ifdef CABO_ENABLE_SEND_PATH_STATS
    struct SendPathStats {
        std::size_t encodedFrames = 0;
    };
#endif

    GameService();
    void setSendFunc(SendFunc f) { sendFunc_ = std::move(f); }
    void setGameFinishedFunc(GameFinishedFunc f) { gameFinishedFunc_ = std::move(f); }

#ifdef CABO_ENABLE_SEND_PATH_STATS
    const SendPathStats& sendPathStatsForTests() const { return sendPathStats_; }
    void resetSendPathStatsForTests() { sendPathStats_ = {}; }
#endif

    // Start a game for a room (called from RoomService when host starts)
    void startGame(int64_t roomId,
                   const std::vector<std::shared_ptr<PlayerGameState>>& players,
                   int64_t hostPlayerId);

    // Inter-round restart: resume existing game with startNewRound
    bool hasGame(int64_t roomId) const;
    bool isGameOver(int64_t roomId) const;
    bool canRestartRound(int64_t roomId) const;
    void restartRound(int64_t roomId);
    void onConnectionClosed(const TcpConnectionPtr& conn);
    void onPlayerLeft(const TcpConnectionPtr& conn);
    bool reconnectPlayer(int64_t roomId,
                         int64_t playerId,
                         const TcpConnectionPtr& conn);
    bool fillGameSyncState(int64_t roomId,
                           int64_t playerId,
                           ::game::sync::GameSyncState* state);

    // Player action handlers
    void handleDrawCard(const TcpConnectionPtr& conn,
                        const ::game::messages::ClientMessage& msg);
    void handleDiscardDrawn(const TcpConnectionPtr& conn,
                            const ::game::messages::ClientMessage& msg);
    void handleReplaceWithDrawn(const TcpConnectionPtr& conn,
                                const ::game::messages::ClientMessage& msg);
    void handleTakeFromDiscard(const TcpConnectionPtr& conn,
                               const ::game::messages::ClientMessage& msg);
    void handleUseSkill(const TcpConnectionPtr& conn,
                        const ::game::messages::ClientMessage& msg);
    void handleCallSteady(const TcpConnectionPtr& conn,
                          const ::game::messages::ClientMessage& msg);
    void handleEndGameEarly(const TcpConnectionPtr& conn,
                            const ::game::messages::ClientMessage& msg);
    void handleEndGameEarlyDecision(const TcpConnectionPtr& conn,
                                    const ::game::messages::ClientMessage& msg);

private:
    std::shared_ptr<GameRoom> getRoom(int64_t roomId);
    std::shared_ptr<PlayerGameState> getPlayer(GameRoom& room, int64_t playerId);
    int32_t getPlayerSeat(GameRoom& room, int64_t playerId);
    bool isCurrentPlayer(GameRoom& room, int64_t playerId);
    bool isPlayerConnection(const PlayerGameState& player,
                            const TcpConnectionPtr& conn) const;

    // Deck
    void initDeck(GameRoom& room);
    ::game::common::CardInfo drawCard(GameRoom& room);
    void discardCard(GameRoom& room, const ::game::common::CardInfo& card);
    int nextCardId;

    // Notifications
    game::SendBufferPool::Lease encodeServerMessage(const ::game::messages::ServerMessage& msg);
    void sendFrameToPlayer(const TcpConnectionPtr& conn,
                           const std::string& frame);
    void sendToPlayer(const TcpConnectionPtr& conn,
                      const ::game::messages::ServerMessage& msg);
    void broadcastToRoom(GameRoom& room,
                         const ::game::messages::ServerMessage& msg,
                         int64_t excludePlayerId = 0);
    void sendGameStart(GameRoom& room);
    void sendTurnStart(GameRoom& room);
    void fillVisibleHandState(::game::common::OpponentHandState* hand,
                              const PlayerGameState& player);
    void sendActionResult(GameRoom& room, int64_t sourcePlayerId,
                          ::game::common::ActionType actionType,
                          int64_t targetPlayerId = 0,
                          ::game::common::SkillType skillUsed = ::game::common::SKILL_TYPE_NONE,
                          bool swapOccurred = false,
                          const ::game::common::ExchangeAttemptResult* exchangeResult = nullptr,
                          int32_t sourceSlot = -1,
                          int32_t targetSlot = -1,
                          bool hideIncomingValueFromOthers = false);

    // Turn management
    bool endTurn(GameRoom& room);
    bool revealAndScore(GameRoom& room);
    void startNewRound(GameRoom& room);
    void nextPlayer(GameRoom& room);
    bool canEndGameEarly(const GameRoom& room) const;
    void clearPendingEndGameRequest(GameRoom& room);
    void broadcastGameOver(GameRoom& room,
                           const std::vector<std::shared_ptr<PlayerGameState>>& rankedPlayers,
                           const std::vector<int64_t>& winnerPlayerIds);
    bool finalizeEarlyGameOver(GameRoom& room);
    void notifyGameFinished(int64_t roomId);

    // Skill execution
    void executePeekSelf(GameRoom& room, int64_t playerId, int32_t slotIndex);
    void executeSpy(GameRoom& room, int64_t playerId,
                    int64_t targetPlayerId, int32_t targetSlotIndex);
    void executeSwap(GameRoom& room, int64_t playerId,
                     int32_t ownSlot, int64_t targetPlayerId, int32_t targetSlot);

    // Multi-card replace
    bool doMultiReplace(GameRoom& room,
                        const ::game::messages::ClientMessage& msg,
                        bool fromDiscard);

    void sendError(const TcpConnectionPtr& conn, int64_t requestId,
                   int32_t code, const std::string& message);

    SendFunc sendFunc_;
    GameFinishedFunc gameFinishedFunc_;
#ifdef CABO_ENABLE_SEND_PATH_STATS
    SendPathStats sendPathStats_;
#endif
    mutable std::mutex mutex_;
    std::mt19937 rng_;
    std::unordered_map<int64_t, std::shared_ptr<GameRoom>> games_;
};

} // namespace cabogame
