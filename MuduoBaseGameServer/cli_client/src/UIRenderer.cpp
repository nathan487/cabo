#include "UIRenderer.h"
#include <iostream>

namespace cabo {

void UIRenderer::clearScreen() {
    // ANSI清屏+光标归位
    std::cout << "\033[2J\033[H" << std::flush;
}

std::string UIRenderer::formatCard(const Card& card) {
    if (card.isKnown) {
        return "[" + std::to_string(card.value) + "]";
    }
    return "[?]";
}

void UIRenderer::renderHeader(const GameState& state) {
    std::cout << "================================================================================" << std::endl;
    std::cout << "                        Cabo Game - 4 Players" << std::endl;
    if (state.phase == GameState::PLAYING) {
        std::cout << "                          Round " << state.roundNumber
                  << ", Turn " << state.turnNumber << std::endl;
    }
    std::cout << "================================================================================" << std::endl;
    std::cout << std::endl;
}

void UIRenderer::renderPiles(const GameState& state) {
    std::cout << "                    Draw Pile: " << state.drawPileCount;

    std::cout << "      Discard Pile: " << state.discardPileCount;
    if (state.discardTopValue >= 0) {
        std::cout << " (Top: " << state.discardTopValue << ")";
    }
    std::cout << std::endl << std::endl;
}

std::string UIRenderer::formatPlayerArea(const Player& p, bool isCurrent, bool isMe, int cardCount) {
    std::string result;
    result += "[";
    if (isMe) {
        result += "You: " + p.nickname;
    } else {
        result += p.nickname;
    }
    result += "]";
    return result;
}

void UIRenderer::renderPlayers(const GameState& state) {
    std::cout << "--------------------------------------------------------------------------------" << std::endl;

    // 简化：只显示玩家列表
    for (const auto& p : state.players) {
        bool isCurrent = (p.playerId == state.currentPlayerId);
        bool isMe = (p.playerId == state.myPlayerId);

        if (isCurrent) {
            std::cout << "                                    ↓" << std::endl;
        }

        std::cout << "                              "
                  << formatPlayerArea(p, isCurrent, isMe, p.cardCount) << std::endl;
        std::cout << "                              Score: " << p.totalScore << std::endl;

        if (!isMe) {
            std::cout << "                              Cards: ";
            for (int i = 0; i < p.cardCount; ++i) {
                std::cout << "[?] ";
            }
            std::cout << std::endl;
        }
        std::cout << std::endl;
    }

    std::cout << "--------------------------------------------------------------------------------" << std::endl;
}

void UIRenderer::renderMyCards(const GameState& state) {
    if (state.myCards.empty()) return;

    std::cout << "                              [You: " << state.myPlayerId << "]" << std::endl;
    std::cout << "                              Cards: ";
    for (const auto& card : state.myCards) {
        std::cout << formatCard(card) << " ";
    }
    std::cout << std::endl;
    std::cout << "================================================================================" << std::endl;
    std::cout << std::endl;
}

void UIRenderer::renderActionMenu(const GameState& state) {
    if (state.phase != GameState::PLAYING) return;

    if (state.isMyTurn()) {
        std::cout << ">>> Your Turn! Choose action:" << std::endl;
        std::cout << "    1. Draw from draw pile" << std::endl;
        std::cout << "    2. Take from discard pile";
        if (state.discardTopValue >= 0) {
            std::cout << " (current top: " << state.discardTopValue << ")";
        }
        std::cout << std::endl;
        std::cout << "    3. Call CABO" << std::endl;
        std::cout << ">>> Enter choice: ";
    } else {
        // 找到当前玩家昵称
        std::string currentPlayerName = "Player";
        for (const auto& p : state.players) {
            if (p.playerId == state.currentPlayerId) {
                currentPlayerName = p.nickname;
                break;
            }
        }
        std::cout << ">>> Waiting for " << currentPlayerName << " to act..." << std::endl;
        std::cout << ">>> (Press Ctrl+C to quit)" << std::endl;
    }
}

void UIRenderer::render(const GameState& state) {
    clearScreen();
    renderHeader(state);

    if (state.phase == GameState::PLAYING) {
        renderPiles(state);
        renderPlayers(state);
        renderMyCards(state);
        renderActionMenu(state);
    } else if (state.phase == GameState::WAITING_ROOM) {
        std::cout << ">>> Waiting for players (" << state.players.size() << "/4)..." << std::endl;
        for (const auto& p : state.players) {
            std::cout << "[Player " << p.seatId + 1 << ": " << p.nickname;
            if (p.playerId == state.myPlayerId) std::cout << " (You)";
            if (p.isHost) std::cout << " (Host)";
            std::cout << "] ";
            if (p.isReady) std::cout << "[Ready]";
            std::cout << std::endl;
        }
    }
}

} // namespace cabo
