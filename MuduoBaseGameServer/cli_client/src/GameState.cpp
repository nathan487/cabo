#include "GameState.h"
#include <cassert>

namespace cabo {

bool GameState::isMyTurn() const {
    return currentPlayerId == myPlayerId;
}

int GameState::getMyPlayerIndex() const {
    for (size_t i = 0; i < players.size(); ++i) {
        if (players[i].playerId == myPlayerId) {
            return static_cast<int>(i);
        }
    }
    return -1;
}

std::vector<int> GameState::getOpponentIndices() const {
    int myIndex = getMyPlayerIndex();
    if (myIndex < 0) return {};

    // CLI客户端固定4人游戏
    int n = static_cast<int>(players.size());
    assert(n == PLAYER_COUNT && "CLI client only supports 4-player games");

    return {
        (myIndex + 2) % n,  // 对面玩家（顶部）
        (myIndex + 3) % n,  // 左侧玩家
        (myIndex + 1) % n   // 右侧玩家
    };
}

} // namespace cabo
