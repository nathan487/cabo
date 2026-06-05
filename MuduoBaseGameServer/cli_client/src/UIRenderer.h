#pragma once

#include "GameState.h"
#include <string>

namespace cabo {

class UIRenderer {
public:
    void render(const GameState& state);

private:
    void clearScreen();
    void renderHeader(const GameState& state);
    void renderPiles(const GameState& state);
    void renderPlayers(const GameState& state);
    void renderMyCards(const GameState& state);
    void renderActionMenu(const GameState& state);
    void renderRoundReveal(const GameState& state);
    void renderGameOver(const GameState& state);

    std::string formatCard(const Card& card);
    std::string formatPlayerArea(const Player& p, bool isCurrent, bool isMe, int cardCount);
};

} // namespace cabo
