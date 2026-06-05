#include "GameState.h"
#include "messages.pb.h"
#include "room.pb.h"
#include "game.pb.h"
#include "sync.pb.h"
#include "common.pb.h"
#include <cassert>
#include <iostream>

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

void GameState::updateFromMessage(const game::messages::ServerMessage& msg) {
    using namespace game::messages;
    using namespace game::room;
    using namespace game::game;
    using namespace game::sync;
    using namespace game::common;

    // 处理 CreateRoomRsp
    if (msg.has_create_room_rsp()) {
        const auto& rsp = msg.create_room_rsp();
        if (rsp.error().code() == 0) {
            roomId = rsp.room_id();
            myPlayerId = rsp.player_id();
            roomCode = rsp.room_code();
            phase = WAITING_ROOM;
            std::cout << "[GameState] CreateRoomRsp: roomId=" << roomId
                      << ", myPlayerId=" << myPlayerId
                      << ", roomCode=" << roomCode << std::endl;
        } else {
            std::cerr << "[GameState] CreateRoomRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 JoinRoomRsp
    else if (msg.has_join_room_rsp()) {
        const auto& rsp = msg.join_room_rsp();
        if (rsp.error().code() == 0) {
            roomId = rsp.room_id();
            myPlayerId = rsp.player_id();
            phase = WAITING_ROOM;
            std::cout << "[GameState] JoinRoomRsp: roomId=" << roomId
                      << ", myPlayerId=" << myPlayerId << std::endl;
        } else {
            std::cerr << "[GameState] JoinRoomRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 RoomStateNotify
    else if (msg.has_room_state_notify()) {
        const auto& notify = msg.room_state_notify();
        const auto& room = notify.room();

        roomId = room.room_id();
        roomCode = room.room_code();

        // 更新玩家列表
        players.clear();
        for (const auto& pinfo : room.players()) {
            Player p;
            p.playerId = pinfo.player_id();
            p.nickname = pinfo.nickname();
            p.seatId = pinfo.seat_id();
            p.isReady = pinfo.is_ready();
            p.isHost = pinfo.is_host();
            p.totalScore = pinfo.total_score();
            players.push_back(p);
        }

        std::cout << "[GameState] RoomStateNotify: " << players.size() << " players" << std::endl;
    }

    // 处理 PlayerJoinNotify
    else if (msg.has_player_join_notify()) {
        const auto& notify = msg.player_join_notify();
        const auto& pinfo = notify.player();

        Player p;
        p.playerId = pinfo.player_id();
        p.nickname = pinfo.nickname();
        p.seatId = pinfo.seat_id();
        p.isReady = pinfo.is_ready();
        p.isHost = pinfo.is_host();
        p.totalScore = pinfo.total_score();
        players.push_back(p);

        std::cout << "[GameState] PlayerJoinNotify: " << p.nickname << " joined" << std::endl;
    }

    // 处理 PlayerLeaveNotify
    else if (msg.has_player_leave_notify()) {
        const auto& notify = msg.player_leave_notify();
        int64_t leftPlayerId = notify.player_id();

        auto it = std::remove_if(players.begin(), players.end(),
            [leftPlayerId](const Player& p) { return p.playerId == leftPlayerId; });
        players.erase(it, players.end());

        std::cout << "[GameState] PlayerLeaveNotify: playerId=" << leftPlayerId << std::endl;
    }

    // 处理 PlayerReadyNotify
    else if (msg.has_player_ready_notify()) {
        const auto& notify = msg.player_ready_notify();
        int64_t readyPlayerId = notify.player_id();
        bool isReady = notify.is_ready();

        for (auto& p : players) {
            if (p.playerId == readyPlayerId) {
                p.isReady = isReady;
                break;
            }
        }

        std::cout << "[GameState] PlayerReadyNotify: playerId=" << readyPlayerId
                  << ", ready=" << isReady << std::endl;
    }

    // 处理 GameStartNotify
    else if (msg.has_game_start_notify()) {
        const auto& notify = msg.game_start_notify();

        phase = PLAYING;
        roundNumber = notify.round_number();
        currentPlayerId = notify.first_player_id();

        // 初始化自己的手牌（从 PlayerGameView）
        if (notify.has_your_view()) {
            const auto& view = notify.your_view();
            myCards.clear();

            for (const auto& ownCard : view.own_cards()) {
                Card c;
                c.slotIndex = ownCard.slot_index();
                c.isKnown = ownCard.is_known();
                c.value = ownCard.is_known() ? ownCard.value() : 0;
                myCards.push_back(c);
            }

            // 更新牌堆信息
            if (view.has_draw_pile()) {
                drawPileCount = view.draw_pile().count();
            }
            if (view.has_discard_pile()) {
                discardPileCount = view.discard_pile().count();
                if (view.discard_pile().has_top_card()) {
                    discardTopValue = view.discard_pile().top_card().value();
                }
            }

            // 更新玩家手牌数量
            for (const auto& oppHand : view.opponent_hands()) {
                for (auto& p : players) {
                    if (p.playerId == oppHand.player_id()) {
                        p.cardCount = oppHand.card_count();
                        break;
                    }
                }
            }
        }

        std::cout << "[GameState] GameStartNotify: round=" << roundNumber
                  << ", currentPlayer=" << currentPlayerId
                  << ", myCards=" << myCards.size() << std::endl;
    }

    // 处理 TurnStartNotify
    else if (msg.has_turn_start_notify()) {
        const auto& notify = msg.turn_start_notify();

        currentPlayerId = notify.current_player_id();
        turnNumber = notify.turn_number();
        roundNumber = notify.round_number();
        hasDrawnCard = false;  // 重置抽牌状态
        drawnCardValue = 0;
        drawnCardSkill = 0;

        // 更新游戏阶段
        if (notify.phase() == GAME_PHASE_FINAL_ROUND) {
            isFinalRound = true;
            finalRoundRemaining = notify.final_round_remaining();
        }

        // 更新牌堆信息
        if (notify.has_draw_pile()) {
            drawPileCount = notify.draw_pile().count();
        }
        if (notify.has_discard_pile()) {
            discardPileCount = notify.discard_pile().count();
            if (notify.discard_pile().has_top_card()) {
                discardTopValue = notify.discard_pile().top_card().value();
            }
        }

        std::cout << "[GameState] TurnStartNotify: turn=" << turnNumber
                  << ", currentPlayer=" << currentPlayerId
                  << ", isMyTurn=" << isMyTurn() << std::endl;
    }

    // 处理 DrawCardRsp
    else if (msg.has_draw_card_rsp()) {
        const auto& rsp = msg.draw_card_rsp();
        if (rsp.error().code() == 0) {
            hasDrawnCard = true;
            drawnCardValue = rsp.value();
            drawnCardSkill = static_cast<int32_t>(rsp.skill());

            std::cout << "[GameState] DrawCardRsp: value=" << drawnCardValue
                      << ", skill=" << drawnCardSkill << std::endl;
        } else {
            std::cerr << "[GameState] DrawCardRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 DiscardDrawnRsp
    else if (msg.has_discard_drawn_rsp()) {
        const auto& rsp = msg.discard_drawn_rsp();
        if (rsp.error().code() == 0) {
            // 弃牌成功，等待 ActionResultNotify 更新弃牌堆
            std::cout << "[GameState] DiscardDrawnRsp: success" << std::endl;
        } else {
            std::cerr << "[GameState] DiscardDrawnRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 ReplaceWithDrawnRsp
    else if (msg.has_replace_with_drawn_rsp()) {
        const auto& rsp = msg.replace_with_drawn_rsp();
        if (rsp.error().code() == 0) {
            if (rsp.has_exchange_result()) {
                const auto& result = rsp.exchange_result();

                if (result.success()) {
                    // 替换成功，更新对应槽位
                    for (int32_t slotIdx : result.selected_slot_indices()) {
                        if (slotIdx >= 0 && slotIdx < static_cast<int32_t>(myCards.size())) {
                            myCards[slotIdx].value = result.incoming_card_value();
                            myCards[slotIdx].isKnown = true;
                        }
                    }
                    std::cout << "[GameState] ReplaceWithDrawnRsp: success, replaced "
                              << result.selected_slot_indices_size() << " cards" << std::endl;
                } else {
                    // 替换失败，牌加入手牌区
                    Card c;
                    c.slotIndex = static_cast<int32_t>(myCards.size());
                    c.value = result.incoming_card_value();
                    c.isKnown = true;
                    myCards.push_back(c);

                    std::cout << "[GameState] ReplaceWithDrawnRsp: failed, card added to hand" << std::endl;
                }
            }
        } else {
            std::cerr << "[GameState] ReplaceWithDrawnRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 TakeFromDiscardRsp
    else if (msg.has_take_from_discard_rsp()) {
        const auto& rsp = msg.take_from_discard_rsp();
        if (rsp.error().code() == 0) {
            if (rsp.has_exchange_result()) {
                const auto& result = rsp.exchange_result();

                if (result.success()) {
                    // 替换成功
                    for (int32_t slotIdx : result.selected_slot_indices()) {
                        if (slotIdx >= 0 && slotIdx < static_cast<int32_t>(myCards.size())) {
                            myCards[slotIdx].value = result.incoming_card_value();
                            myCards[slotIdx].isKnown = true;
                        }
                    }
                    std::cout << "[GameState] TakeFromDiscardRsp: success" << std::endl;
                } else {
                    // 替换失败
                    Card c;
                    c.slotIndex = static_cast<int32_t>(myCards.size());
                    c.value = result.incoming_card_value();
                    c.isKnown = true;
                    myCards.push_back(c);

                    std::cout << "[GameState] TakeFromDiscardRsp: failed, card added to hand" << std::endl;
                }
            }
        } else {
            std::cerr << "[GameState] TakeFromDiscardRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 UseSkillRsp
    else if (msg.has_use_skill_rsp()) {
        const auto& rsp = msg.use_skill_rsp();
        if (rsp.error().code() == 0) {
            // 技能使用成功
            if (rsp.peeked_value() >= 0) {
                // 偷看技能，更新对应槽位
                std::cout << "[GameState] UseSkillRsp: peeked value=" << rsp.peeked_value() << std::endl;
            }
            if (rsp.swap_occurred()) {
                std::cout << "[GameState] UseSkillRsp: swap occurred" << std::endl;
            }
        } else {
            std::cerr << "[GameState] UseSkillRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 ActionResultNotify
    else if (msg.has_action_result_notify()) {
        const auto& notify = msg.action_result_notify();

        // 更新牌堆信息
        if (notify.has_draw_pile()) {
            drawPileCount = notify.draw_pile().count();
        }
        if (notify.has_discard_pile()) {
            discardPileCount = notify.discard_pile().count();
            if (notify.discard_pile().has_top_card()) {
                discardTopValue = notify.discard_pile().top_card().value();
            }
        }

        // 更新回合状态
        if (notify.turn_ended()) {
            currentPlayerId = notify.next_player_id();
            hasDrawnCard = false;
        }

        std::cout << "[GameState] ActionResultNotify: action=" << notify.action_type()
                  << ", turnEnded=" << notify.turn_ended() << std::endl;
    }

    // 处理 RoundRevealNotify
    else if (msg.has_round_reveal_notify()) {
        const auto& notify = msg.round_reveal_notify();

        phase = ROUND_REVEAL;

        // 更新玩家分数
        for (const auto& scoreDetail : notify.scores()) {
            for (auto& p : players) {
                if (p.playerId == scoreDetail.player_id()) {
                    p.totalScore = scoreDetail.cumulative_score();
                    break;
                }
            }
        }

        std::cout << "[GameState] RoundRevealNotify: round=" << notify.round_number() << std::endl;
    }

    // 处理 ScoreUpdateNotify
    else if (msg.has_score_update_notify()) {
        const auto& notify = msg.score_update_notify();

        // 更新玩家总分
        for (const auto& scoreInfo : notify.scores()) {
            for (auto& p : players) {
                if (p.playerId == scoreInfo.player_id()) {
                    p.totalScore = scoreInfo.total_score();
                    break;
                }
            }
        }

        std::cout << "[GameState] ScoreUpdateNotify: round=" << notify.round_number() << std::endl;
    }

    // 处理 GameOverNotify
    else if (msg.has_game_over_notify()) {
        const auto& notify = msg.game_over_notify();

        phase = GAME_OVER;

        std::cout << "[GameState] GameOverNotify: totalRounds=" << notify.total_rounds() << std::endl;
        for (const auto& ranking : notify.rankings()) {
            std::cout << "  Rank " << ranking.rank() << ": " << ranking.nickname()
                      << " (score=" << ranking.final_score() << ")" << std::endl;
        }
    }

    // 处理 StateSyncNotify (重连恢复)
    else if (msg.has_state_sync_notify()) {
        const auto& notify = msg.state_sync_notify();

        roomId = notify.room_id();

        // 更新房间状态
        if (notify.has_room_state()) {
            const auto& room = notify.room_state();
            roomCode = room.room_code();

            players.clear();
            for (const auto& pinfo : room.players()) {
                Player p;
                p.playerId = pinfo.player_id();
                p.nickname = pinfo.nickname();
                p.seatId = pinfo.seat_id();
                p.isReady = pinfo.is_ready();
                p.isHost = pinfo.is_host();
                p.totalScore = pinfo.total_score();
                players.push_back(p);
            }
        }

        // 如果在游戏中，更新游戏状态
        if (notify.is_in_game() && notify.has_game_state()) {
            const auto& gameState = notify.game_state();

            phase = PLAYING;
            roundNumber = gameState.round_number();
            currentPlayerId = gameState.current_turn_player_id();

            if (gameState.phase() == GAME_PHASE_FINAL_ROUND) {
                isFinalRound = true;
                finalRoundRemaining = gameState.final_round_remaining();
            }

            // 更新手牌和牌堆信息
            if (gameState.has_player_view()) {
                const auto& view = gameState.player_view();

                myCards.clear();
                for (const auto& ownCard : view.own_cards()) {
                    Card c;
                    c.slotIndex = ownCard.slot_index();
                    c.isKnown = ownCard.is_known();
                    c.value = ownCard.is_known() ? ownCard.value() : 0;
                    myCards.push_back(c);
                }

                if (view.has_draw_pile()) {
                    drawPileCount = view.draw_pile().count();
                }
                if (view.has_discard_pile()) {
                    discardPileCount = view.discard_pile().count();
                    if (view.discard_pile().has_top_card()) {
                        discardTopValue = view.discard_pile().top_card().value();
                    }
                }
            }

            // 处理挂起的抽牌状态
            if (gameState.has_pending_step()) {
                const auto& pendingStep = gameState.pending_step();
                if (pendingStep.step_type() == TurnStepState::STEP_TYPE_WAITING_DRAW_DECISION &&
                    pendingStep.waiting_player_id() == myPlayerId) {
                    hasDrawnCard = true;
                    drawnCardValue = pendingStep.drawn_card_value();
                    drawnCardSkill = static_cast<int32_t>(pendingStep.drawn_card_skill());
                }
            }
        }

        std::cout << "[GameState] StateSyncNotify: roomId=" << roomId
                  << ", isInGame=" << notify.is_in_game() << std::endl;
    }
}

} // namespace cabo
