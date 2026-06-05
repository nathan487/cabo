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
    std::cout << "                        Cabo Game - " << GameState::PLAYER_COUNT << " Players" << std::endl;
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

    if (!state.isMyTurn()) {
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
    // 当自己回合时，操作菜单由ClientApp状态机统一管理（showPrompt）
}

void UIRenderer::render(const GameState& state) {
    clearScreen();
    renderHeader(state);

    // 回合结算优先：即使GameStartNotify已将phase覆盖为PLAYING，也先显示结算
    if (state.roundJustRevealed) {
        renderRoundReveal(state);
        return;
    }

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
    } else if (state.phase == GameState::ROUND_REVEAL) {
        renderRoundReveal(state);
    } else if (state.phase == GameState::GAME_OVER) {
        renderGameOver(state);
    }
}

void UIRenderer::renderRoundReveal(const GameState& state) {
    std::cout << "================================================================================" << std::endl;
    std::cout << "                        Round " << state.roundNumber << " Reveal" << std::endl;
    std::cout << "================================================================================" << std::endl;
    std::cout << std::endl;

    for (const auto& result : state.lastRoundResults) {
        std::cout << result.nickname;
        if (result.playerId == state.myPlayerId) {
            std::cout << " (You)";
        }
        if (result.isSteadyCaller) {
            std::cout << " (called CABO)";
        }
        std::cout << ":  ";

        // 显示手牌
        for (int val : result.cardValues) {
            std::cout << "[" << val << "] ";
        }

        std::cout << "= " << result.handTotal;

        if (result.penalty > 0) {
            std::cout << "  (+" << result.penalty << " penalty)";
        }

        std::cout << " = " << result.roundScore;

        if (result.isLowest) {
            std::cout << "  <- Lowest!";
        }

        if (result.isKamikaze) {
            std::cout << "  KAMIKAZE!";
        }

        std::cout << std::endl;
    }

    std::cout << std::endl;
    std::cout << "Scores after Round " << state.roundNumber << ":" << std::endl;
    for (const auto& result : state.lastRoundResults) {
        std::cout << "  " << result.nickname << ": " << result.cumulativeScore;
        if (result.isLowest) {
            std::cout << "  <- Lowest";
        }
        std::cout << std::endl;
    }

    std::cout << std::endl;
    std::cout << ">>> Press Enter to continue...";
}

void UIRenderer::renderGameOver(const GameState& state) {
    std::cout << "================================================================================" << std::endl;
    std::cout << "                        Game Over!" << std::endl;
    std::cout << "================================================================================" << std::endl;
    std::cout << std::endl;

    std::cout << "Final Standings:" << std::endl;
    for (const auto& rank : state.finalRankings) {
        std::cout << "  " << rank.rank;
        if (rank.rank == 1) std::cout << "st";
        else if (rank.rank == 2) std::cout << "nd";
        else if (rank.rank == 3) std::cout << "rd";
        else std::cout << "th";

        std::cout << " Place: " << rank.nickname << "  (Score: " << rank.finalScore << ")";

        if (rank.isWinner) {
            std::cout << "  WINNER";
        }

        std::cout << std::endl;
    }

    std::cout << std::endl;
    std::cout << ">>> Press Enter to exit...";
}


} // namespace cabo
