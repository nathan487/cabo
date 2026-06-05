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

        // Critical Fix #2: 检查玩家是否已存在，避免重复添加
        bool playerExists = false;
        for (const auto& existing : players) {
            if (existing.playerId == pinfo.player_id()) {
                playerExists = true;
                std::cerr << "[GameState] WARNING: Player " << pinfo.player_id()
                          << " already exists, skipping duplicate join" << std::endl;
                break;
            }
        }

        if (!playerExists) {
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

        // Important Fix #5: 添加玩家未找到警告
        bool found = false;
        for (auto& p : players) {
            if (p.playerId == readyPlayerId) {
                p.isReady = isReady;
                found = true;
                break;
            }
        }

        if (!found) {
            std::cerr << "[GameState] WARNING: Player " << readyPlayerId
                      << " not found for ready status update" << std::endl;
        }

        std::cout << "[GameState] PlayerReadyNotify: playerId=" << readyPlayerId
                  << ", ready=" << isReady << std::endl;
    }

    // 处理 ReadyRsp
    else if (msg.has_ready_rsp()) {
        const auto& rsp = msg.ready_rsp();
        if (rsp.error().code() == 0) {
            std::cout << "[GameState] ReadyRsp: success" << std::endl;
        } else {
            std::cerr << "[GameState] ReadyRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 StartGameRsp (房主会收到)
    else if (msg.has_start_game_rsp()) {
        const auto& rsp = msg.start_game_rsp();
        if (rsp.error().code() == 0) {
            gameStartConfirmed = true;  // 标记服务器已确认游戏启动
            std::cout << "[GameState] StartGameRsp: success, game starting" << std::endl;
        } else {
            std::cerr << "[GameState] StartGameRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 RoomStartNotify (所有玩家会收到)
    else if (msg.has_room_start_notify()) {
        const auto& notify = msg.room_start_notify();
        std::cout << "[GameState] RoomStartNotify: roomId=" << notify.room_id() << std::endl;
        // 房间开始游戏，等待 GameStartNotify
    }

    // 处理 GameStartNotify
    else if (msg.has_game_start_notify()) {
        const auto& notify = msg.game_start_notify();

        phase = PLAYING;
        roundNumber = notify.round_number();
        currentPlayerId = notify.first_player_id();
        isFinalRound = false;  // 新回合开始，重置最终轮标志
        finalRoundRemaining = 0;

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
                // Critical Fix #3: 验证弃牌堆顶牌的值范围（0-13）
                if (view.discard_pile().has_top_card()) {
                    int32_t topValue = view.discard_pile().top_card().value();
                    if (topValue >= 0 && topValue <= 13) {
                        discardTopValue = topValue;
                    } else {
                        std::cerr << "[GameState] WARNING: Invalid discard top card value: "
                                  << topValue << ", setting to -1" << std::endl;
                        discardTopValue = -1;
                    }
                }
            }

            // 更新玩家手牌数量
            for (const auto& oppHand : view.opponent_hands()) {
                // Important Fix #5: 添加玩家未找到警告
                bool found = false;
                for (auto& p : players) {
                    if (p.playerId == oppHand.player_id()) {
                        p.cardCount = oppHand.card_count();
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    std::cerr << "[GameState] WARNING: Player " << oppHand.player_id()
                              << " not found for card count update" << std::endl;
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

        // 仅在自己的回合开始时重置等待标志和抽牌状态
        if (notify.current_player_id() == myPlayerId) {
            hasDrawnCard = false;
            drawnCardValue = 0;
            drawnCardSkill = 0;
            waitingForDrawResponse = false;
            waitingForTakeResponse = false;
            waitingForCallSteadyResponse = false;
            waitingForSkillResponse = false;
        }

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
            // Critical Fix #3: 验证弃牌堆顶牌的值范围（0-13）
            if (notify.discard_pile().has_top_card()) {
                int32_t topValue = notify.discard_pile().top_card().value();
                if (topValue >= 0 && topValue <= 13) {
                    discardTopValue = topValue;
                } else {
                    std::cerr << "[GameState] WARNING: Invalid discard top card value: "
                              << topValue << ", setting to -1" << std::endl;
                    discardTopValue = -1;
                }
            }
        }

        std::cout << "[GameState] TurnStartNotify: turn=" << turnNumber
                  << ", currentPlayer=" << currentPlayerId
                  << ", isMyTurn=" << isMyTurn() << std::endl;
    }

    // 处理 DrawCardRsp
    else if (msg.has_draw_card_rsp()) {
        const auto& rsp = msg.draw_card_rsp();
        waitingForDrawResponse = false;  // 清除等待标志
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
            // Bug #7 Fix: 清除抽牌状态，操作已完成
            hasDrawnCard = false;
            drawnCardValue = 0;
            drawnCardSkill = 0;
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
                int32_t incomingValue = result.incoming_card_value();
                int32_t addedCardCount = result.selected_slot_indices_size();

                if (result.success()) {
                    if (addedCardCount > 1) {
                        // Multi-card success: discard selected, keep unselected, add drawn at end
                        // Card count decreases: old N → (N - addedCardCount + 1)
                        std::vector<Card> newMyCards;
                        for (size_t i = 0; i < myCards.size(); i++) {
                            bool isSelected = false;
                            for (int j = 0; j < result.selected_slot_indices_size(); j++) {
                                if (static_cast<int32_t>(i) == result.selected_slot_indices(j)) {
                                    isSelected = true; break;
                                }
                            }
                            if (!isSelected) {
                                Card c = myCards[i];
                                c.slotIndex = static_cast<int32_t>(newMyCards.size());
                                newMyCards.push_back(c);
                            }
                        }
                        // Add drawn card at end
                        Card newCard;
                        newCard.slotIndex = static_cast<int32_t>(newMyCards.size());
                        newCard.value = incomingValue;
                        newCard.isKnown = true;
                        newMyCards.push_back(newCard);
                        myCards = std::move(newMyCards);
                    } else {
                        // Single card: update in place
                        int32_t slotIdx = result.selected_slot_indices(0);
                        if (slotIdx >= 0 && slotIdx < static_cast<int32_t>(myCards.size())) {
                            myCards[slotIdx].value = incomingValue;
                            myCards[slotIdx].isKnown = true;
                        }
                    }
                    std::cout << "[GameState] ReplaceWithDrawnRsp: success, replaced "
                              << addedCardCount << " cards" << std::endl;
                } else {
                    // Critical Fix #1: 替换失败，正确设置slotIndex
                    for (int i = 0; i < addedCardCount; ++i) {
                        Card c;
                        c.slotIndex = static_cast<int32_t>(myCards.size());
                        c.value = incomingValue;
                        c.isKnown = true;
                        myCards.push_back(c);
                    }

                    std::cout << "[GameState] ReplaceWithDrawnRsp: failed, card added to hand" << std::endl;
                }
            }

            // Bug #7 Fix: 清除抽牌状态，操作已完成
            hasDrawnCard = false;
            drawnCardValue = 0;
            drawnCardSkill = 0;
        } else {
            std::cerr << "[GameState] ReplaceWithDrawnRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 TakeFromDiscardRsp
    else if (msg.has_take_from_discard_rsp()) {
        const auto& rsp = msg.take_from_discard_rsp();
        waitingForTakeResponse = false;  // Bug #8 Fix: 清除等待标志
        if (rsp.error().code() == 0) {
            if (rsp.has_exchange_result()) {
                const auto& result = rsp.exchange_result();
                int32_t incomingValue = result.incoming_card_value();
                int32_t addedCardCount = result.selected_slot_indices_size();

                if (result.success()) {
                    if (addedCardCount > 1) {
                        // Multi-card success: discard selected, keep unselected, add at end
                        std::vector<Card> newMyCards;
                        for (size_t i = 0; i < myCards.size(); i++) {
                            bool isSelected = false;
                            for (int j = 0; j < result.selected_slot_indices_size(); j++) {
                                if (static_cast<int32_t>(i) == result.selected_slot_indices(j)) {
                                    isSelected = true; break;
                                }
                            }
                            if (!isSelected) {
                                Card c = myCards[i];
                                c.slotIndex = static_cast<int32_t>(newMyCards.size());
                                newMyCards.push_back(c);
                            }
                        }
                        Card newCard;
                        newCard.slotIndex = static_cast<int32_t>(newMyCards.size());
                        newCard.value = incomingValue;
                        newCard.isKnown = true;
                        newMyCards.push_back(newCard);
                        myCards = std::move(newMyCards);
                    } else {
                        int32_t slotIdx = result.selected_slot_indices(0);
                        if (slotIdx >= 0 && slotIdx < static_cast<int32_t>(myCards.size())) {
                            myCards[slotIdx].value = incomingValue;
                            myCards[slotIdx].isKnown = true;
                        }
                    }
                    std::cout << "[GameState] TakeFromDiscardRsp: success" << std::endl;
                } else {
                    // Critical Fix #1: 替换失败，正确设置slotIndex
                    for (int i = 0; i < addedCardCount; ++i) {
                        Card c;
                        c.slotIndex = static_cast<int32_t>(myCards.size());
                        c.value = incomingValue;
                        c.isKnown = true;
                        myCards.push_back(c);
                    }

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
        waitingForSkillResponse = false;  // 清除等待标志
        if (rsp.error().code() == 0) {
            // 保存技能结果，供ClientApp更新手牌状态
            lastPeekedValue = rsp.peeked_value();
            lastSwapOccurred = rsp.swap_occurred();

            if (rsp.peeked_value() >= 0) {
                std::cout << "[GameState] UseSkillRsp: peeked value=" << rsp.peeked_value() << std::endl;
            }

            if (rsp.swap_occurred()) {
                std::cout << "[GameState] UseSkillRsp: swap occurred" << std::endl;
            }
        } else {
            std::cerr << "[GameState] UseSkillRsp error: " << rsp.error().message() << std::endl;
        }
    }

    // 处理 CallSteadyRsp (Call CABO)
    else if (msg.has_call_steady_rsp()) {
        const auto& rsp = msg.call_steady_rsp();
        waitingForCallSteadyResponse = false;  // Bug #8 Fix: 清除等待标志
        if (rsp.error().code() == 0) {
            std::cout << "[GameState] CallSteadyRsp: CABO call accepted" << std::endl;
            // 游戏进入最终轮，等待后续的通知消息
        } else {
            std::cerr << "[GameState] CallSteadyRsp error: " << rsp.error().message() << std::endl;
            // 显示错误给用户
            std::cerr << ">>> Call CABO failed: " << rsp.error().message() << std::endl;
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
                int32_t topValue = notify.discard_pile().top_card().value();
                if (topValue >= 0 && topValue <= 13) {
                    discardTopValue = topValue;
                } else {
                    std::cerr << "[GameState] WARNING: Invalid discard top card value: "
                              << topValue << ", setting to -1" << std::endl;
                    discardTopValue = -1;
                }
            }
        }

        // 处理Swap技能：通过ActionResultNotify广播更新受影响的双方手牌状态
        if (notify.swap_occurred()) {
            // 发起交换的玩家：其source_slot变为未知
            if (notify.source_player_id() == myPlayerId) {
                int32_t slot = notify.source_slot();
                if (slot >= 0 && slot < static_cast<int32_t>(myCards.size())) {
                    myCards[slot].isKnown = false;
                    std::cout << "[GameState] Swap: my slot " << slot
                              << " now unknown (blind swap, via broadcast)" << std::endl;
                }
            }
            // 被交换的玩家：其target_slot变为未知
            if (notify.target_player_id() == myPlayerId) {
                int32_t slot = notify.target_slot();
                if (slot >= 0 && slot < static_cast<int32_t>(myCards.size())) {
                    myCards[slot].isKnown = false;
                    std::cout << "[GameState] Swap: my slot " << slot
                              << " now unknown (blind swap, via broadcast)" << std::endl;
                }
            }
        }

        // BUG-4 Fix: Update all players' card counts from ActionResultNotify.
        // This keeps opponent card counts in sync after failed replaces
        // where a player's hand can grow beyond 4 cards.
        for (const auto& hand : notify.player_hands()) {
            for (auto& p : players) {
                if (p.playerId == hand.player_id()) {
                    p.cardCount = hand.card_count();
                    break;
                }
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
        roundJustRevealed = true;  // 标记回合已结算，确保结算界面显示

        // 填充详细的结算信息
        lastRoundResults.clear();
        for (const auto& score : notify.scores()) {
            RoundResult result;
            result.playerId = score.player_id();
            result.handTotal = score.hand_total();
            result.penalty = score.penalty();
            result.roundScore = score.round_score();
            result.cumulativeScore = score.cumulative_score();
            result.isSteadyCaller = score.is_steady_caller();
            result.isLowest = score.is_lowest();
            result.isKamikaze = score.is_kamikaze();

            // 找到昵称
            for (const auto& p : players) {
                if (p.playerId == score.player_id()) {
                    result.nickname = p.nickname;
                    break;
                }
            }

            // 找到手牌
            for (const auto& hand : notify.revealed_hands()) {
                if (hand.player_id() == score.player_id()) {
                    for (int val : hand.card_values()) {
                        result.cardValues.push_back(val);
                    }
                    break;
                }
            }

            lastRoundResults.push_back(result);
        }

        // 更新玩家分数
        for (const auto& scoreDetail : notify.scores()) {
            bool found = false;
            for (auto& p : players) {
                if (p.playerId == scoreDetail.player_id()) {
                    p.totalScore = scoreDetail.cumulative_score();
                    found = true;
                    break;
                }
            }
            if (!found) {
                std::cerr << "[GameState] WARNING: Player " << scoreDetail.player_id()
                          << " not found for score update" << std::endl;
            }
        }

        std::cout << "[GameState] RoundRevealNotify: round=" << notify.round_number() << std::endl;
    }

    // 处理 ScoreUpdateNotify
    else if (msg.has_score_update_notify()) {
        const auto& notify = msg.score_update_notify();

        // 更新玩家总分
        for (const auto& scoreInfo : notify.scores()) {
            // Important Fix #5: 添加玩家未找到警告
            bool found = false;
            for (auto& p : players) {
                if (p.playerId == scoreInfo.player_id()) {
                    p.totalScore = scoreInfo.total_score();
                    found = true;
                    break;
                }
            }
            if (!found) {
                std::cerr << "[GameState] WARNING: Player " << scoreInfo.player_id()
                          << " not found for score update" << std::endl;
            }
        }

        std::cout << "[GameState] ScoreUpdateNotify: round=" << notify.round_number() << std::endl;
    }

    // 处理 GameOverNotify
    else if (msg.has_game_over_notify()) {
        const auto& notify = msg.game_over_notify();

        phase = GAME_OVER;

        // 填充最终排名信息
        finalRankings.clear();
        for (const auto& ranking : notify.rankings()) {
            FinalRank rank;
            rank.rank = ranking.rank();
            rank.playerId = ranking.player_id();
            rank.nickname = ranking.nickname();
            rank.finalScore = ranking.final_score();
            rank.isWinner = ranking.is_winner();
            finalRankings.push_back(rank);
        }

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
                    // Critical Fix #3: 验证弃牌堆顶牌的值范围（0-13）
                    if (view.discard_pile().has_top_card()) {
                        int32_t topValue = view.discard_pile().top_card().value();
                        if (topValue >= 0 && topValue <= 13) {
                            discardTopValue = topValue;
                        } else {
                            std::cerr << "[GameState] WARNING: Invalid discard top card value: "
                                      << topValue << ", setting to -1" << std::endl;
                            discardTopValue = -1;
                        }
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
