#include "game/GameService.h"
#include "common/MessageCodec.h"
#include <mymuduo/logger.h>
#include <algorithm>
#include <sstream>

namespace cabogame {

// ── Player helpers ──

int32_t PlayerGameState::roundScore() const {
    int32_t sum = 0;
    for (auto& c : cards) sum += c.value();
    return sum;
}

bool PlayerGameState::hasKamikaze() const {
    if (cards.size() != 4) return false;
    int cnt12 = 0, cnt13 = 0;
    for (auto& c : cards) {
        if (c.value() == 12) cnt12++;
        else if (c.value() == 13) cnt13++;
    }
    return cnt12 == 2 && cnt13 == 2;
}

// ── GameService ──

GameService::GameService() : rng_(std::random_device{}()) {
    nextCardId = 1;
}

std::shared_ptr<GameRoom> GameService::getRoom(int64_t roomId) {
    auto it = games_.find(roomId);
    return (it != games_.end()) ? it->second : nullptr;
}

std::shared_ptr<PlayerGameState> GameService::getPlayer(GameRoom& room, int64_t playerId) {
    for (auto& p : room.players)
        if (p->playerId == playerId) return p;
    return nullptr;
}

int32_t GameService::getPlayerSeat(GameRoom& room, int64_t playerId) {
    for (size_t i = 0; i < room.players.size(); i++)
        if (room.players[i]->playerId == playerId) return static_cast<int32_t>(i);
    return -1;
}

bool GameService::isCurrentPlayer(GameRoom& room, int64_t playerId) {
    // Use array index instead of seatId to match sendTurnStart() logic
    // This ensures consistency and robustness for future features (reconnect, etc.)
    if (room.currentPlayerSeat < 0 ||
        room.currentPlayerSeat >= static_cast<int32_t>(room.players.size())) {
        return false;
    }
    return room.players[room.currentPlayerSeat]->playerId == playerId;
}

// ── Deck ──

void GameService::initDeck(GameRoom& room) {
    room.drawPile.clear();
    room.discardPile.clear();

    auto add = [&](int value, int count) {
        for (int i = 0; i < count; i++) {
            ::game::common::CardInfo c;
            c.set_card_id(nextCardId++);
            c.set_value(value);
            if (value >= 7 && value <= 8) c.set_skill(::game::common::SKILL_TYPE_PEEK_SELF);
            else if (value >= 9 && value <= 10) c.set_skill(::game::common::SKILL_TYPE_SPY);
            else if (value >= 11 && value <= 12) c.set_skill(::game::common::SKILL_TYPE_SWAP);
            else c.set_skill(::game::common::SKILL_TYPE_NONE);
            room.drawPile.push_back(c);
        }
    };

    add(0, 2);
    for (int v = 1; v <= 12; v++) add(v, 4);
    add(13, 2);

    // Shuffle
    std::shuffle(room.drawPile.begin(), room.drawPile.end(), rng_);
}

::game::common::CardInfo GameService::drawCard(GameRoom& room) {
    auto c = room.drawPile.back();
    room.drawPile.pop_back();
    return c;
}

void GameService::discardCard(GameRoom& room, const ::game::common::CardInfo& card) {
    room.discardPile.push_back(card);
}

// ── Messaging ──

void GameService::sendToPlayer(const TcpConnectionPtr& conn,
                                const ::game::messages::ServerMessage& msg) {
    if (!sendFunc_) { LOG_INFO("[Game] sendToPlayer: no sendFunc!"); return; }
    if (!conn) { LOG_INFO("[Game] sendToPlayer: null conn!"); return; }

    // 获取消息类型用于日志
    std::string msgType = "Unknown";
    if (msg.has_create_room_rsp()) msgType = "CreateRoomRsp";
    else if (msg.has_join_room_rsp()) msgType = "JoinRoomRsp";
    else if (msg.has_room_state_notify()) msgType = "RoomStateNotify";
    else if (msg.has_player_join_notify()) msgType = "PlayerJoinNotify";
    else if (msg.has_player_leave_notify()) msgType = "PlayerLeaveNotify";
    else if (msg.has_player_ready_notify()) msgType = "PlayerReadyNotify";
    else if (msg.has_ready_rsp()) msgType = "ReadyRsp";
    else if (msg.has_start_game_rsp()) msgType = "StartGameRsp";
    else if (msg.has_room_start_notify()) msgType = "RoomStartNotify";
    else if (msg.has_game_start_notify()) msgType = "GameStartNotify";
    else if (msg.has_turn_start_notify()) msgType = "TurnStartNotify";
    else if (msg.has_draw_card_rsp()) msgType = "DrawCardRsp";
    else if (msg.has_discard_drawn_rsp()) msgType = "DiscardDrawnRsp";
    else if (msg.has_replace_with_drawn_rsp()) msgType = "ReplaceWithDrawnRsp";
    else if (msg.has_take_from_discard_rsp()) msgType = "TakeFromDiscardRsp";
    else if (msg.has_use_skill_rsp()) msgType = "UseSkillRsp";
    else if (msg.has_call_steady_rsp()) msgType = "CallSteadyRsp";
    else if (msg.has_action_result_notify()) msgType = "ActionResultNotify";
    else if (msg.has_round_reveal_notify()) msgType = "RoundRevealNotify";
    else if (msg.has_score_update_notify()) msgType = "ScoreUpdateNotify";
    else if (msg.has_game_over_notify()) msgType = "GameOverNotify";
    else if (msg.has_state_sync_notify()) msgType = "StateSyncNotify";

    std::string payload;
    msg.SerializeToString(&payload);
    auto frame = game::MessageCodec::encode(payload);
    LOG_INFO("[Game] sendToPlayer: %s (%zu bytes)", msgType.c_str(), frame.size());
    sendFunc_(conn, frame);
}

void GameService::broadcastToRoom(GameRoom& room,
                                   const ::game::messages::ServerMessage& msg,
                                   int64_t excludePlayerId) {
    for (auto& p : room.players) {
        if (p->playerId == excludePlayerId) continue;
        if (p->conn && p->isConnected)
            sendToPlayer(p->conn, msg);
    }
}

void GameService::sendError(const TcpConnectionPtr& conn, int64_t requestId,
                             int32_t code, const std::string& message) {
    // Generic error — we send a server message with error info in the appropriate response.
    // For simplicity, we use a generic mechanism.
    LOG_INFO("[Game] Error %d: %s (req %lld)", code, message.c_str(), (long long)requestId);
}

// ── Game Start ──

void GameService::startGame(int64_t roomId,
                             const std::vector<std::shared_ptr<PlayerGameState>>& players,
                             int64_t hostPlayerId) {
    auto room = std::make_shared<GameRoom>();
    room->roomId = roomId;
    room->hostPlayerId = hostPlayerId;
    room->maxPlayers = static_cast<int32_t>(players.size());
    room->players = players;
    room->step = GameStep::Playing;

    games_[roomId] = room;

    startNewRound(*room);
}

void GameService::startNewRound(GameRoom& room) {
    room.roundNumber++;
    room.steadyCallerSeat = -1;
    room.finalRoundRemaining = 0;
    room.currentPlayerSeat = 0;
    room.turnNumber = 0;

    initDeck(room);

    // Deal 4 cards to each player
    for (auto& p : room.players) {
        p->cards.clear();
        p->knownSlots.clear();
        for (int i = 0; i < 4; i++) {
            p->cards.push_back(drawCard(room));
            p->knownSlots.push_back(false);
        }
        // Each player peeks at their first 2 cards
        p->knownSlots[0] = true;
        p->knownSlots[1] = true;
    }

    // Flip first card to discard
    auto firstDiscard = drawCard(room);
    discardCard(room, firstDiscard);

    sendGameStart(room);
    sendTurnStart(room);
}

void GameService::sendGameStart(GameRoom& room) {
    for (auto& p : room.players) {
        ::game::messages::ServerMessage msg;
        auto* gs = msg.mutable_game_start_notify();
        gs->set_room_id(room.roomId);
        gs->set_round_number(room.roundNumber);
        gs->set_first_player_id(room.players[0]->playerId);

        // Build player view
        auto* view = gs->mutable_your_view();
        view->set_player_id(p->playerId);
        for (size_t i = 0; i < p->cards.size(); i++) {
            auto* own = view->add_own_cards();
            own->set_slot_index(static_cast<int32_t>(i));
            own->set_is_known(p->knownSlots[i]);
            if (p->knownSlots[i]) own->set_value(p->cards[i].value());
        }
        for (auto& other : room.players) {
            if (other->playerId == p->playerId) continue;
            auto* opp = view->add_opponent_hands();
            opp->set_player_id(other->playerId);
            opp->set_card_count(static_cast<int32_t>(other->cards.size()));
        }
        auto* dp = view->mutable_draw_pile();
        dp->set_count(static_cast<int32_t>(room.drawPile.size()));
        auto* dcp = view->mutable_discard_pile();
        dcp->set_count(static_cast<int32_t>(room.discardPile.size()));
        if (!room.discardPile.empty()) {
            *dcp->mutable_top_card() = room.discardPile.back();
        }
        for (auto& pl : room.players) {
            auto* si = view->add_scores();
            si->set_player_id(pl->playerId);
            si->set_total_score(pl->totalScore);
            si->set_current_round_score(-1);
        }

        sendToPlayer(p->conn, msg);
    }
}

void GameService::sendTurnStart(GameRoom& room) {
    room.turnNumber++;
    auto current = room.players[room.currentPlayerSeat];

    ::game::messages::ServerMessage msg;
    auto* ts = msg.mutable_turn_start_notify();
    ts->set_room_id(room.roomId);
    ts->set_current_player_id(current->playerId);
    ts->set_turn_number(room.turnNumber);
    ts->set_round_number(room.roundNumber);
    ts->set_phase(room.steadyCallerSeat >= 0
        ? ::game::common::GAME_PHASE_FINAL_ROUND : ::game::common::GAME_PHASE_PLAYING);

    auto* dp = ts->mutable_draw_pile();
    dp->set_count(static_cast<int32_t>(room.drawPile.size()));
    auto* dcp = ts->mutable_discard_pile();
    dcp->set_count(static_cast<int32_t>(room.discardPile.size()));
    if (!room.discardPile.empty())
        *dcp->mutable_top_card() = room.discardPile.back();

    if (room.steadyCallerSeat >= 0)
        ts->set_final_round_remaining(room.finalRoundRemaining);

    broadcastToRoom(room, msg);
}

void GameService::sendActionResult(GameRoom& room, int64_t sourcePlayerId,
                                    ::game::common::ActionType actionType,
                                    int64_t targetPlayerId,
                                    ::game::common::SkillType skillUsed,
                                    bool swapOccurred,
                                    const ::game::common::ExchangeAttemptResult* exchangeResult,
                                    int32_t sourceSlot,
                                    int32_t targetSlot) {
    ::game::messages::ServerMessage msg;
    auto* ar = msg.mutable_action_result_notify();
    ar->set_room_id(room.roomId);
    ar->set_action_type(actionType);
    ar->set_source_player_id(sourcePlayerId);
    ar->set_target_player_id(targetPlayerId);
    ar->set_skill_used(skillUsed);
    ar->set_swap_occurred(swapOccurred);
    if (sourceSlot >= 0) ar->set_source_slot(sourceSlot);
    if (targetSlot >= 0) ar->set_target_slot(targetSlot);

    auto* dp = ar->mutable_draw_pile();
    dp->set_count(static_cast<int32_t>(room.drawPile.size()));
    auto* dcp = ar->mutable_discard_pile();
    dcp->set_count(static_cast<int32_t>(room.discardPile.size()));
    if (!room.discardPile.empty())
        *dcp->mutable_top_card() = room.discardPile.back();

    if (exchangeResult)
        *ar->mutable_exchange_result() = *exchangeResult;

    broadcastToRoom(room, msg);
}

// ── Turn Management ──

void GameService::endTurn(GameRoom& room) {
    if (room.steadyCallerSeat >= 0) {
        room.finalRoundRemaining--;
        if (room.finalRoundRemaining <= 0) {
            revealAndScore(room);
            return;
        }
    }

    nextPlayer(room);
    sendTurnStart(room);
}

void GameService::nextPlayer(GameRoom& room) {
    do {
        room.currentPlayerSeat = (room.currentPlayerSeat + 1) % static_cast<int32_t>(room.players.size());
    } while (room.steadyCallerSeat >= 0 && room.currentPlayerSeat == room.steadyCallerSeat);
}

// ── Reveal & Score ──

void GameService::revealAndScore(GameRoom& room) {
    LOG_INFO("[Game] Round %d reveal — scoring...", room.roundNumber);

    bool hadKamikaze = false;
    int64_t kamikazePlayerId = 0;

    for (auto& p : room.players) {
        if (p->hasKamikaze()) {
            hadKamikaze = true;
            kamikazePlayerId = p->playerId;
            LOG_INFO("[Game] Kamikaze! %s has 12,12,13,13", p->nickname.c_str());
            break;
        }
    }

    ::game::messages::ServerMessage revealMsg;
    auto* rn = revealMsg.mutable_round_reveal_notify();
    rn->set_room_id(room.roomId);
    rn->set_round_number(room.roundNumber);
    rn->set_steady_caller_id(room.steadyCallerSeat >= 0
        ? room.players[room.steadyCallerSeat]->playerId : 0);

    if (hadKamikaze) {
        for (auto& p : room.players) {
            if (p->playerId == kamikazePlayerId)
                p->totalScore += 0;
            else
                p->totalScore += 50;

            auto* rh = rn->add_revealed_hands();
            rh->set_player_id(p->playerId);
            for (auto& c : p->cards) rh->add_card_values(c.value());
            rh->set_total(p->roundScore());

            auto* sc = rn->add_scores();
            sc->set_player_id(p->playerId);
            sc->set_hand_total(p->roundScore());
            sc->set_penalty(p->playerId != kamikazePlayerId ? 50 : 0);
            sc->set_round_score(p->playerId == kamikazePlayerId ? 0 : 50);
            sc->set_cumulative_score(p->totalScore);
            sc->set_is_steady_caller(p->seatId == room.steadyCallerSeat);
            sc->set_is_lowest(p->playerId == kamikazePlayerId);
            sc->set_is_kamikaze(p->playerId == kamikazePlayerId);
        }
    } else {
        // Normal scoring
        int32_t minScore = INT32_MAX;
        for (auto& p : room.players) {
            int32_t s = p->roundScore();
            if (s < minScore) minScore = s;
        }

        for (auto& p : room.players) {
            bool isSteady = p->seatId == room.steadyCallerSeat;
            bool isLowest = p->roundScore() <= minScore;

            int32_t penalty = 0;
            int32_t roundSc;
            if (isSteady && isLowest) {
                // CABO caller who is lowest (or tied) scores 0
                penalty = 0;
                roundSc = 0;
            } else if (isSteady && !isLowest) {
                // CABO caller who is NOT lowest: card sum + 10 penalty
                penalty = 10;
                roundSc = p->roundScore() + penalty;
            } else {
                // Non-caller: card sum
                penalty = 0;
                roundSc = p->roundScore();
            }
            p->totalScore += roundSc;

            auto* rh = rn->add_revealed_hands();
            rh->set_player_id(p->playerId);
            for (auto& c : p->cards) rh->add_card_values(c.value());
            rh->set_total(p->roundScore());

            auto* sc = rn->add_scores();
            sc->set_player_id(p->playerId);
            sc->set_hand_total(p->roundScore());
            sc->set_penalty(penalty);
            sc->set_round_score(roundSc);
            sc->set_cumulative_score(p->totalScore);
            sc->set_is_steady_caller(isSteady);
            sc->set_is_lowest(isLowest);
            sc->set_is_kamikaze(false);
        }
    }

    broadcastToRoom(room, revealMsg);

    // 100-point resets
    ::game::messages::ServerMessage scoreMsg;
    auto* su = scoreMsg.mutable_score_update_notify();
    su->set_room_id(room.roomId);
    su->set_round_number(room.roundNumber);

    for (auto& p : room.players) {
        if (p->totalScore == 100 && !p->hasUsedReset) {
            p->totalScore = 50;
            p->hasUsedReset = true;
            LOG_INFO("[Game] Reset! %s hit exactly 100 → 50", p->nickname.c_str());
            auto* ht = su->add_hundred_triggers();
            ht->set_player_id(p->playerId);
            ht->set_new_score(50);
        }
        auto* si = su->add_scores();
        si->set_player_id(p->playerId);
        si->set_total_score(p->totalScore);
        si->set_current_round_score(p->roundScore());
    }
    broadcastToRoom(room, scoreMsg);

    // Game over?
    bool gameOver = false;
    for (auto& p : room.players) {
        if (p->totalScore >= 100) { gameOver = true; break; }
    }

    if (gameOver) {
        // Find winner
        int32_t bestScore = INT32_MAX;
        for (auto& p : room.players)
            if (p->totalScore < bestScore) bestScore = p->totalScore;

        // Collect tied
        std::vector<std::shared_ptr<PlayerGameState>> tied;
        for (auto& p : room.players)
            if (p->totalScore == bestScore) tied.push_back(p);

        std::shared_ptr<PlayerGameState> winner;
        if (tied.size() == 1) {
            winner = tied[0];
        } else {
            int32_t bestLastRound = INT32_MAX;
            for (auto& p : tied) {
                if (p->roundScore() < bestLastRound) {
                    bestLastRound = p->roundScore();
                    winner = p;
                }
            }
        }

        LOG_INFO("[Game] Game over! Winner: %s (%d pts)", winner->nickname.c_str(), winner->totalScore);

        ::game::messages::ServerMessage goMsg;
        auto* go = goMsg.mutable_game_over_notify();
        go->set_room_id(room.roomId);
        go->set_total_rounds(room.roundNumber);

        // Rankings sorted by score
        auto ranked = room.players;
        std::sort(ranked.begin(), ranked.end(), [](const std::shared_ptr<PlayerGameState>& a, const std::shared_ptr<PlayerGameState>& b) {
            return a->totalScore < b->totalScore;
        });
        for (size_t i = 0; i < ranked.size(); i++) {
            auto* fr = go->add_rankings();
            fr->set_rank(static_cast<int32_t>(i + 1));
            fr->set_player_id(ranked[i]->playerId);
            fr->set_nickname(ranked[i]->nickname);
            fr->set_final_score(ranked[i]->totalScore);
            fr->set_is_winner(ranked[i]->playerId == winner->playerId);
        }
        broadcastToRoom(room, goMsg);
        room.step = GameStep::GameOver;
    } else {
        room.step = GameStep::Playing;
        startNewRound(room);
    }
}

// ── Player Actions ──

void GameService::handleDrawCard(const TcpConnectionPtr& conn,
                                  const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.draw_card_req();
    LOG_INFO("[Game] DrawCard req: player=%lld, room=%lld", (long long)req.player_id(), (long long)req.room_id());
    auto room = getRoom(req.room_id());
    if (!room) { LOG_INFO("[Game] DrawCard: room not found"); return; }

    auto player = getPlayer(*room, req.player_id());
    if (!player) { LOG_INFO("[Game] DrawCard: player not found"); return; }
    if (!isCurrentPlayer(*room, req.player_id())) {
        LOG_INFO("[Game] DrawCard: NOT player %lld's turn (current seat=%d)",
                 (long long)req.player_id(), room->currentPlayerSeat);
        // Send error: not your turn
        ::game::messages::ServerMessage errMsg;
        auto* rsp = errMsg.mutable_draw_card_rsp();
        rsp->set_request_id(req.request_id());
        rsp->mutable_error()->set_code(3001);
        rsp->mutable_error()->set_message("Not your turn");
        sendToPlayer(conn, errMsg);
        return;
    }
    if (room->step != GameStep::Playing) { LOG_INFO("[Game] DrawCard: not in Playing step"); return; }
    if (room->drawPile.empty()) {
        LOG_INFO("[Game] DrawCard: deck empty, ending round");
        // 牌库抽空时结束当前轮，触发结算
        ::game::messages::ServerMessage errMsg;
        auto* rsp2 = errMsg.mutable_draw_card_rsp();
        rsp2->set_request_id(req.request_id());
        rsp2->mutable_error()->set_code(0);
        rsp2->mutable_error()->set_message("Deck empty, round ending");
        sendToPlayer(conn, errMsg);
        revealAndScore(*room);
        return;
    }

    auto card = drawCard(*room);
    room->pendingDrawnCard = card;
    room->pendingDrewFromDiscard = false;
    room->step = GameStep::WaitingDrawDecision;

    LOG_INFO("[Game] Sending DrawCardRsp to player %lld (card value=%d)",
             (long long)player->playerId, card.value());

    // Send DrawCardRsp to the drawing player only
    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_draw_card_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    rsp->set_card_id(card.card_id());
    rsp->set_value(card.value());
    rsp->set_skill(card.skill());
    sendToPlayer(conn, rspMsg);

    // Broadcast draw action
    sendActionResult(*room, player->playerId, ::game::common::ACTION_TYPE_DRAW, 0, ::game::common::SKILL_TYPE_NONE, false, nullptr, -1, -1);
}

void GameService::handleDiscardDrawn(const TcpConnectionPtr& conn,
                                      const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.discard_drawn_req();
    auto room = getRoom(req.room_id());
    if (!room) return;
    if (room->step != GameStep::WaitingDrawDecision) return;
    if (room->pendingDrewFromDiscard) return;

    auto player = getPlayer(*room, req.player_id());
    if (!player) return;

    int cardValue = room->pendingDrawnCard.value();
    bool hasSkill = cardValue >= 7 && cardValue <= 12;

    // Discard the card. If 7-12, skill triggers as part of discard.
    // Player can send UseSkillReq (or skip by not sending anything).
    // Turn ends after skill or after non-skill discard.

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_discard_drawn_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    sendToPlayer(conn, rspMsg);

    discardCard(*room, room->pendingDrawnCard);
    sendActionResult(*room, player->playerId, ::game::common::ACTION_TYPE_DISCARD_DRAWN,
        0, cardValue >= 7 && cardValue <= 8 ? ::game::common::SKILL_TYPE_PEEK_SELF
          : (cardValue >= 9 && cardValue <= 10 ? ::game::common::SKILL_TYPE_SPY
          : (cardValue >= 11 && cardValue <= 12 ? ::game::common::SKILL_TYPE_SWAP
          : ::game::common::SKILL_TYPE_NONE)), false, nullptr, -1, -1);

    if (hasSkill) {
        // Don't end turn — wait for skill resolution (UseSkillReq will end turn)
        // But if player doesn't use skill within timeout, they effectively skip
        LOG_INFO("[Game] Skill card (%d) discarded — waiting for skill action or skip", cardValue);
        // For MVP, stay in current step so player can send UseSkillReq
        // The client should send UseSkillReq or a "skip skill" message
    } else {
        room->step = GameStep::Playing;
        endTurn(*room);
    }
}

void GameService::handleReplaceWithDrawn(const TcpConnectionPtr& conn,
                                          const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.replace_with_drawn_req();
    auto room = getRoom(req.room_id());
    if (!room) return;
    if (room->step != GameStep::WaitingDrawDecision) return;

    auto player = getPlayer(*room, req.player_id());
    if (!player) return;

    auto card = room->pendingDrawnCard;
    const auto& indices = req.slot_indices();

    if (indices.size() == 0) return;

    ::game::common::ExchangeAttemptResult exResult;
    exResult.set_attempted_multi_card(indices.size() > 1);
    for (int i = 0; i < indices.size(); i++)
        exResult.add_selected_slot_indices(indices.Get(i));
    exResult.set_incoming_card_value(card.value());

    if (indices.size() == 1) {
        // Single card: always succeeds
        int32_t slot = indices.Get(0);
        if (slot < 0 || slot >= static_cast<int32_t>(player->cards.size())) return;
        auto oldCard = player->cards[slot];
        player->cards[slot] = card;
        player->knownSlots[slot] = true;
        discardCard(*room, oldCard);

        exResult.set_success(true);
        exResult.set_discarded_count(1);
        exResult.set_added_card_count(0);
    } else {
        // Multi-card: check all same value
        int32_t firstVal = player->cards[indices.Get(0)].value();
        bool allSame = true;
        for (int i = 1; i < indices.size(); i++) {
            if (player->cards[indices.Get(i)].value() != firstVal) {
                allSame = false;
                break;
            }
        }

        if (allSame) {
            for (int i = 0; i < indices.size(); i++) {
                int32_t slot = indices.Get(i);
                auto oldCard = player->cards[slot];
                player->cards[slot] = card;
                player->knownSlots[slot] = true;
                discardCard(*room, oldCard);
            }
            exResult.set_success(true);
            exResult.set_discarded_count(indices.size());
            exResult.set_added_card_count(0);
        } else {
            // Failure: drawn card added to player area
            player->cards.push_back(card);
            player->knownSlots.push_back(true);

            exResult.set_success(false);
            exResult.set_discarded_count(0);
            exResult.set_added_card_count(1);

            if (indices.size() >= 3) {
                // Extra penalty
                if (!room->drawPile.empty()) {
                    auto penalty = drawCard(*room);
                    player->cards.push_back(penalty);
                    player->knownSlots.push_back(true);
                    exResult.set_added_card_count(2);
                    exResult.set_drew_extra_penalty_card(true);
                }
            }
        }
    }

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_replace_with_drawn_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    *rsp->mutable_exchange_result() = exResult;
    sendToPlayer(conn, rspMsg);

    sendActionResult(*room, player->playerId,
        ::game::common::ACTION_TYPE_REPLACE_WITH_DRAWN, 0,
        ::game::common::SKILL_TYPE_NONE, false, &exResult, -1, -1);

    room->step = GameStep::Playing;
    endTurn(*room);
}

void GameService::handleTakeFromDiscard(const TcpConnectionPtr& conn,
                                         const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.take_from_discard_req();
    auto room = getRoom(req.room_id());
    if (!room) return;
    if (room->step != GameStep::Playing) return;
    if (room->discardPile.empty()) return;

    auto player = getPlayer(*room, req.player_id());
    if (!player || !isCurrentPlayer(*room, req.player_id())) return;

    auto card = room->discardPile.back();
    room->discardPile.pop_back();

    room->pendingDrawnCard = card;
    room->pendingDrewFromDiscard = true;
    room->step = GameStep::WaitingDrawDecision;

    // Same multi-replace logic as replace_with_drawn, but source is discard
    // For MVP: auto-replace first slot (simplified — full multi-replace via replace flow)
    const auto& indices = req.slot_indices();
    if (indices.size() == 0) return;

    ::game::common::ExchangeAttemptResult exResult;
    exResult.set_attempted_multi_card(indices.size() > 1);
    for (int i = 0; i < indices.size(); i++)
        exResult.add_selected_slot_indices(indices.Get(i));
    exResult.set_incoming_card_value(card.value());

    if (indices.size() == 1) {
        int32_t slot = indices.Get(0);
        if (slot < 0 || slot >= static_cast<int32_t>(player->cards.size())) return;
        auto oldCard = player->cards[slot];
        player->cards[slot] = card;
        player->knownSlots[slot] = true;
        discardCard(*room, oldCard);
        exResult.set_success(true);
        exResult.set_discarded_count(1);
    } else {
        int32_t firstVal = player->cards[indices.Get(0)].value();
        bool allSame = true;
        for (int i = 1; i < indices.size(); i++) {
            if (player->cards[indices.Get(i)].value() != firstVal) {
                allSame = false; break;
            }
        }
        if (allSame) {
            for (int i = 0; i < indices.size(); i++) {
                int32_t slot = indices.Get(i);
                auto oldCard = player->cards[slot];
                player->cards[slot] = card;
                player->knownSlots[slot] = true;
                discardCard(*room, oldCard);
            }
            exResult.set_success(true);
            exResult.set_discarded_count(indices.size());
        } else {
            player->cards.push_back(card);
            player->knownSlots.push_back(true);
            exResult.set_success(false);
            exResult.set_added_card_count(1);
            if (indices.size() >= 3 && !room->drawPile.empty()) {
                auto penalty = drawCard(*room);
                player->cards.push_back(penalty);
                player->knownSlots.push_back(true);
                exResult.set_added_card_count(2);
                exResult.set_drew_extra_penalty_card(true);
            }
        }
    }

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_take_from_discard_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    *rsp->mutable_exchange_result() = exResult;
    sendToPlayer(conn, rspMsg);

    sendActionResult(*room, player->playerId,
        ::game::common::ACTION_TYPE_TAKE_FROM_DISCARD, 0,
        ::game::common::SKILL_TYPE_NONE, false, &exResult, -1, -1);

    room->step = GameStep::Playing;
    endTurn(*room);
}

void GameService::handleUseSkill(const TcpConnectionPtr& conn,
                                  const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.use_skill_req();
    auto room = getRoom(req.room_id());
    if (!room) return;

    auto player = getPlayer(*room, req.player_id());
    if (!player) return;

    // Skill can only be used after drawing and discarding a skill card
    if (!isCurrentPlayer(*room, req.player_id())) return;
    if (room->step != GameStep::WaitingDrawDecision) {
        LOG_INFO("[Game] UseSkill: not in WaitingDrawDecision step (current=%d)", (int)room->step);
        return;
    }

    int64_t targetPlayer = 0;
    int32_t srcSlot = -1, dstSlot = -1;
    int32_t peekedValue = -1;
    ::game::common::SkillType stype = ::game::common::SKILL_TYPE_NONE;

    if (req.has_peek_self()) {
        stype = ::game::common::SKILL_TYPE_PEEK_SELF;
        srcSlot = req.peek_self().slot_index();
        if (srcSlot >= 0 && srcSlot < static_cast<int32_t>(player->cards.size())) {
            player->knownSlots[srcSlot] = true;
            peekedValue = player->cards[srcSlot].value();
            LOG_INFO("[Game] PeekSelf: %s looks at own slot %d = %d",
                     player->nickname.c_str(), srcSlot, peekedValue);
        }
    } else if (req.has_spy()) {
        stype = ::game::common::SKILL_TYPE_SPY;
        targetPlayer = req.spy().target_player_id();
        dstSlot = req.spy().target_slot_index();
        auto target = getPlayer(*room, targetPlayer);
        if (target && dstSlot >= 0 && dstSlot < static_cast<int32_t>(target->cards.size())) {
            peekedValue = target->cards[dstSlot].value();
            LOG_INFO("[Game] Spy: %s looks at %s's slot %d = %d",
                     player->nickname.c_str(), target->nickname.c_str(),
                     dstSlot, peekedValue);
        }
    } else if (req.has_swap()) {
        stype = ::game::common::SKILL_TYPE_SWAP;
        targetPlayer = req.swap().target_player_id();
        srcSlot = req.swap().own_slot_index();
        dstSlot = req.swap().target_slot_index();
        auto target = getPlayer(*room, targetPlayer);
        if (target && srcSlot >= 0 && srcSlot < static_cast<int32_t>(player->cards.size())
            && dstSlot >= 0 && dstSlot < static_cast<int32_t>(target->cards.size())) {
            std::swap(player->cards[srcSlot], target->cards[dstSlot]);
            // Blind swap: neither player knows the swapped-in card value
            player->knownSlots[srcSlot] = false;
            target->knownSlots[dstSlot] = false;
            LOG_INFO("[Game] Swap: %s[%d] <-> %s[%d]",
                     player->nickname.c_str(), srcSlot,
                     target->nickname.c_str(), dstSlot);
        } else {
            // Swap failed — invalid target or slot
            LOG_INFO("[Game] Swap FAILED: %s slot=%d -> player=%lld slot=%d (bounds check failed)",
                     player->nickname.c_str(), srcSlot,
                     (long long)targetPlayer, dstSlot);
            stype = ::game::common::SKILL_TYPE_NONE;  // 防止swap_occurred被设为true
        }
    }

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_use_skill_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    rsp->set_peeked_value(peekedValue);
    rsp->set_swap_occurred(stype == ::game::common::SKILL_TYPE_SWAP);
    sendToPlayer(conn, rspMsg);

    sendActionResult(*room, player->playerId,
        ::game::common::ACTION_TYPE_USE_SKILL, targetPlayer, stype,
        stype == ::game::common::SKILL_TYPE_SWAP, nullptr, srcSlot, dstSlot);

    // Skill ends the turn (skill card goes to discard, turn passes)
    room->step = GameStep::Playing;
    endTurn(*room);
}

void GameService::handleCallSteady(const TcpConnectionPtr& conn,
                                    const ::game::messages::ClientMessage& msg) {
    const auto& req = msg.call_steady_req();
    auto room = getRoom(req.room_id());
    if (!room) return;
    if (room->steadyCallerSeat >= 0) return; // Already called
    if (!isCurrentPlayer(*room, req.player_id())) return;
    if (room->step != GameStep::Playing) {
        LOG_INFO("[Game] CallSteady: not in Playing step (current=%d)", (int)room->step);
        return;
    }

    auto player = getPlayer(*room, req.player_id());
    if (!player) return;

    room->steadyCallerSeat = player->seatId;
    room->finalRoundRemaining = static_cast<int32_t>(room->players.size()) - 1;

    LOG_INFO("[Game] Steady called by %s! Final round: %d turns remaining",
             player->nickname.c_str(), room->finalRoundRemaining);

    ::game::messages::ServerMessage rspMsg;
    auto* rsp = rspMsg.mutable_call_steady_rsp();
    rsp->set_request_id(req.request_id());
    rsp->mutable_error()->set_code(0);
    sendToPlayer(conn, rspMsg);

    sendActionResult(*room, player->playerId, ::game::common::ACTION_TYPE_CALL_STEADY, 0, ::game::common::SKILL_TYPE_NONE, false, nullptr, -1, -1);

    if (room->finalRoundRemaining <= 0) {
        revealAndScore(*room);
        return;
    }

    nextPlayer(*room);
    sendTurnStart(*room);
}

// Multi-replace placeholder (used by handleReplaceWithDrawn)
bool GameService::doMultiReplace(GameRoom& room,
                                  const ::game::messages::ClientMessage& msg,
                                  bool fromDiscard) {
    (void)room; (void)msg; (void)fromDiscard;
    // Actual logic is inline in handleReplaceWithDrawn/handleTakeFromDiscard
    return true;
}

} // namespace cabogame
