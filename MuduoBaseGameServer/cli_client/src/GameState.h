#pragma once

#include <string>
#include <vector>
#include <cstdint>

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

    // 最终轮标志
    bool isFinalRound = false;
    int32_t finalRoundRemaining = 0;

    // 辅助方法
    bool isMyTurn() const;
    int getMyPlayerIndex() const;
    std::vector<int> getOpponentIndices() const;
};

} // namespace cabo
