#pragma once

#include <string>
#include <vector>
#include <cstdint>

namespace game {
namespace messages {
    class ServerMessage;
}
}

namespace cabo {

struct Card {
    int32_t slotIndex = 0;
    bool isKnown = false;
    int32_t value = 0;
};

struct Player {
    int64_t playerId = 0;
    std::string nickname;
    int32_t seatId = 0;
    int32_t totalScore = 0;
    int32_t cardCount = 0;
    bool isReady = false;
    bool isHost = false;
};

class GameState {
public:
    static constexpr int PLAYER_COUNT = 4;  // CLI客户端固定4人游戏

    enum Phase {
        LOBBY,
        WAITING_ROOM,
        PLAYING,
        ROUND_REVEAL,
        GAME_OVER
    };

    // 连接状态
    int64_t myPlayerId = 0;
    int64_t roomId = 0;
    std::string roomCode;

    // 房间阶段
    Phase phase = LOBBY;

    // 玩家列表
    std::vector<Player> players;

    // 自己的手牌
    std::vector<Card> myCards;

    // 牌堆信息
    int32_t drawPileCount = 0;
    int32_t discardPileCount = 0;
    int32_t discardTopValue = -1;

    // 回合信息
    int64_t currentPlayerId = 0;
    int32_t roundNumber = 0;
    int32_t turnNumber = 0;

    // 抽牌暂存
    bool hasDrawnCard = false;
    int32_t drawnCardValue = 0;
    int32_t drawnCardSkill = 0;

    // 请求等待标志（防止重复发送）
    bool waitingForDrawResponse = false;
    bool waitingForTakeResponse = false;
    bool waitingForCallSteadyResponse = false;
    bool waitingForSkillResponse = false;

    // 技能结果暂存（UseSkillRsp后更新myCards用）
    int32_t lastPeekedValue = -1;
    bool lastSwapOccurred = false;
    bool isFinalRound = false;
    int32_t finalRoundRemaining = 0;

    // 游戏启动确认（用于房主超时检查）
    bool gameStartConfirmed = false;

    // 回合结算标记（确保GameStartNotify不会跳过结算界面）
    bool roundJustRevealed = false;

    // 结算信息
    struct RoundResult {
        int64_t playerId;
        std::string nickname;
        std::vector<int> cardValues;
        int32_t handTotal;
        int32_t penalty;
        int32_t roundScore;
        int32_t cumulativeScore;
        bool isSteadyCaller;
        bool isLowest;
        bool isKamikaze;
    };

    std::vector<RoundResult> lastRoundResults;

    struct FinalRank {
        int32_t rank;
        int64_t playerId;
        std::string nickname;
        int32_t finalScore;
        bool isWinner;
    };

    std::vector<FinalRank> finalRankings;

    // 辅助方法
    bool isMyTurn() const;
    int getMyPlayerIndex() const;
    std::vector<int> getOpponentIndices() const;

    // 消息更新处理
    void updateFromMessage(const game::messages::ServerMessage& msg);
};

} // namespace cabo
