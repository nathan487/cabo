#define private public
#include "game/GameService.h"
#undef private

#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <string>
#include <vector>

namespace {

void require(bool condition, const std::string& message);

cabogame::TcpConnectionPtr fakeConn(std::intptr_t tag) {
    return cabogame::TcpConnectionPtr(reinterpret_cast<TcpConnection*>(tag),
                                      [](TcpConnection*) {});
}

struct SentFrame {
    TcpConnection* conn;
    std::string frame;
};

uint64_t readBigEndian(const std::string& data, size_t offset, size_t count) {
    uint64_t value = 0;
    for (size_t i = 0; i < count; ++i) {
        value = (value << 8) | static_cast<unsigned char>(data[offset + i]);
    }
    return value;
}

std::string websocketPayload(const std::string& frame) {
    require(frame.size() >= 2, "websocket frame must contain a header");

    const auto second = static_cast<unsigned char>(frame[1]);
    require((second & 0x80) == 0, "server websocket frame must be unmasked");

    size_t offset = 2;
    uint64_t length = second & 0x7F;
    if (length == 126) {
        require(frame.size() >= 4, "websocket frame must contain extended length");
        length = readBigEndian(frame, offset, 2);
        offset += 2;
    } else if (length == 127) {
        require(frame.size() >= 10, "websocket frame must contain extended length");
        length = readBigEndian(frame, offset, 8);
        offset += 8;
    }

    require(frame.size() >= offset + length, "websocket frame payload is truncated");
    return frame.substr(offset, static_cast<size_t>(length));
}

std::vector<::game::messages::ServerMessage> messagesForConn(
    const std::vector<SentFrame>& frames,
    const cabogame::TcpConnectionPtr& conn) {
    std::vector<::game::messages::ServerMessage> result;
    for (const auto& sent : frames) {
        if (sent.conn != conn.get()) continue;
        ::game::messages::ServerMessage msg;
        require(msg.ParseFromString(websocketPayload(sent.frame)),
                "server message should parse from websocket payload");
        result.push_back(std::move(msg));
    }
    return result;
}

::game::common::CardInfo card(int id, int value) {
    ::game::common::CardInfo c;
    c.set_card_id(id);
    c.set_value(value);
    c.set_publicly_known(false);
    if (value >= 7 && value <= 8) c.set_skill(::game::common::SKILL_TYPE_PEEK_SELF);
    else if (value >= 9 && value <= 10) c.set_skill(::game::common::SKILL_TYPE_SPY);
    else if (value >= 11 && value <= 12) c.set_skill(::game::common::SKILL_TYPE_SWAP);
    else c.set_skill(::game::common::SKILL_TYPE_NONE);
    return c;
}

void require(bool condition, const std::string& message) {
    if (!condition) {
        std::cerr << "FAILED: " << message << "\n";
        std::exit(1);
    }
}

std::shared_ptr<cabogame::PlayerGameState> player(int64_t playerId,
                                                   int32_t seat,
                                                   const cabogame::TcpConnectionPtr& conn) {
    auto p = std::make_shared<cabogame::PlayerGameState>();
    p->playerId = playerId;
    p->nickname = "P" + std::to_string(playerId);
    p->seatId = seat;
    p->conn = conn;
    p->isConnected = true;
    for (int i = 0; i < 4; ++i) {
        p->cards.push_back(card(100 + static_cast<int>(playerId) + i, i + 1));
        p->knownSlots.push_back(false);
    }
    return p;
}

std::shared_ptr<cabogame::GameRoom> makeRoom(const cabogame::TcpConnectionPtr& p1Conn,
                                             const cabogame::TcpConnectionPtr& p2Conn) {
    auto room = std::make_shared<cabogame::GameRoom>();
    room->roomId = 1;
    room->roomCode = "TEST01";
    room->maxPlayers = 2;
    room->hostPlayerId = 10000;
    room->step = cabogame::GameStep::Playing;
    room->roundNumber = 1;
    room->turnNumber = 0;
    room->currentPlayerSeat = 0;
    room->players.push_back(player(10000, 0, p1Conn));
    room->players.push_back(player(10001, 1, p2Conn));
    room->drawPile.push_back(card(1, 9));
    return room;
}

void setHandValues(const std::shared_ptr<cabogame::PlayerGameState>& p,
                   const std::vector<int>& values) {
    p->cards.clear();
    p->knownSlots.clear();
    for (size_t i = 0; i < values.size(); ++i) {
        p->cards.push_back(card(500 + static_cast<int>(i), values[i]));
        p->knownSlots.push_back(true);
    }
}

void rejectsForgedConnectionForDraw() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back({nullptr, frame});
    });

    auto p1Conn = fakeConn(1);
    auto p2Conn = fakeConn(2);
    auto room = makeRoom(p1Conn, p2Conn);
    service.games_[room->roomId] = room;

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_draw_card_req();
    req->set_request_id(10);
    req->set_room_id(room->roomId);
    req->set_player_id(10000);

    service.handleDrawCard(p2Conn, msg);

    require(room->step == cabogame::GameStep::Playing,
            "forged draw must not move the room out of Playing");
    require(room->drawPile.size() == 1,
            "forged draw must not remove a card from the draw pile");
    require(!sentFrames.empty(),
            "forged draw should receive an error response");
}

void hidesDrawnIncomingValueFromOtherPlayers() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.push_back({conn.get(), frame});
    });

    auto p1Conn = fakeConn(3);
    auto p2Conn = fakeConn(4);
    auto room = makeRoom(p1Conn, p2Conn);
    room->step = cabogame::GameStep::WaitingDrawDecision;
    room->pendingDrewFromDiscard = false;
    room->pendingDrawnCard = card(200, 12);
    service.games_[room->roomId] = room;

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_replace_with_drawn_req();
    req->set_request_id(20);
    req->set_room_id(room->roomId);
    req->set_player_id(10000);
    req->add_slot_indices(0);

    service.handleReplaceWithDrawn(p1Conn, msg);

    bool sourceSawValue = false;
    for (const auto& serverMsg : messagesForConn(sentFrames, p1Conn)) {
        if (!serverMsg.has_action_result_notify()) continue;
        const auto& exchange = serverMsg.action_result_notify().exchange_result();
        if (exchange.incoming_card_value() == 12) {
            sourceSawValue = true;
        }
    }
    require(sourceSawValue, "drawing player should still see their incoming card value");

    bool otherSawHiddenValue = false;
    for (const auto& serverMsg : messagesForConn(sentFrames, p2Conn)) {
        if (!serverMsg.has_action_result_notify()) continue;
        const auto& exchange = serverMsg.action_result_notify().exchange_result();
        if (exchange.incoming_card_value() == -1) {
            otherSawHiddenValue = true;
        }
        require(exchange.incoming_card_value() != 12,
                "other players must not see drawn incoming card value");
    }
    require(otherSawHiddenValue,
            "other players should receive a hidden incoming card value marker");
}

void rejectsSkillTypeMismatch() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.push_back({conn.get(), frame});
    });

    auto p1Conn = fakeConn(5);
    auto p2Conn = fakeConn(6);
    auto room = makeRoom(p1Conn, p2Conn);
    room->step = cabogame::GameStep::WaitingDrawDecision;
    room->pendingDrewFromDiscard = false;
    room->pendingDrawnCard = card(300, 8);
    service.games_[room->roomId] = room;

    const int p1Slot0 = room->players[0]->cards[0].value();
    const int p2Slot0 = room->players[1]->cards[0].value();

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_use_skill_req();
    req->set_request_id(30);
    req->set_room_id(room->roomId);
    req->set_player_id(10000);
    req->set_card_id(room->pendingDrawnCard.card_id());
    auto* swap = req->mutable_swap();
    swap->set_own_slot_index(0);
    swap->set_target_player_id(10001);
    swap->set_target_slot_index(0);

    service.handleUseSkill(p1Conn, msg);

    require(room->players[0]->cards[0].value() == p1Slot0,
            "mismatched skill must not swap the source card");
    require(room->players[1]->cards[0].value() == p2Slot0,
            "mismatched skill must not swap the target card");
    require(room->step == cabogame::GameStep::WaitingDrawDecision,
            "mismatched skill must not end the turn");
    require(!sentFrames.empty(),
            "mismatched skill should receive an error response");
}

void removesDisconnectedPlayerFromActiveGame() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.push_back({conn.get(), frame});
    });

    auto p1Conn = fakeConn(7);
    auto p2Conn = fakeConn(8);
    auto room = makeRoom(p1Conn, p2Conn);
    room->step = cabogame::GameStep::Playing;
    room->currentPlayerSeat = 0;
    service.games_[room->roomId] = room;

    service.onConnectionClosed(p1Conn);

    require(room->players.size() == 1,
            "disconnected player should be removed from game state");
    require(room->players[0]->playerId == 10001,
            "remaining player should stay in game state");
    require(room->step == cabogame::GameStep::GameOver,
            "single remaining player should end the active game");

    bool remainingSawGameOver = false;
    for (const auto& serverMsg : messagesForConn(sentFrames, p2Conn)) {
        if (serverMsg.has_game_over_notify()) {
            remainingSawGameOver = true;
        }
    }
    require(remainingSawGameOver,
            "remaining player should receive GameOver after opponent disconnects");
}

void rejectsRestartRoundWhileRoundIsActive() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.push_back({conn.get(), frame});
    });

    auto p1Conn = fakeConn(9);
    auto p2Conn = fakeConn(10);
    auto room = makeRoom(p1Conn, p2Conn);
    room->step = cabogame::GameStep::Playing;
    room->roundNumber = 3;
    room->turnNumber = 7;
    const auto originalDrawPileSize = room->drawPile.size();
    service.games_[room->roomId] = room;

    service.restartRound(room->roomId);

    require(room->step == cabogame::GameStep::Playing,
            "active round restart should keep the current step");
    require(room->roundNumber == 3,
            "active round restart should not advance the round number");
    require(room->turnNumber == 7,
            "active round restart should not reset turn number");
    require(room->drawPile.size() == originalDrawPileSize,
            "active round restart should not reinitialize the deck");
    require(sentFrames.empty(),
            "active round restart should not broadcast a new game start");
}

void deckEmptyDrawReturnsAnError() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.push_back({conn.get(), frame});
    });

    auto p1Conn = fakeConn(11);
    auto p2Conn = fakeConn(12);
    auto room = makeRoom(p1Conn, p2Conn);
    room->step = cabogame::GameStep::Playing;
    room->currentPlayerSeat = 0;
    room->drawPile.clear();
    service.games_[room->roomId] = room;

    ::game::messages::ClientMessage msg;
    auto* req = msg.mutable_draw_card_req();
    req->set_request_id(40);
    req->set_room_id(room->roomId);
    req->set_player_id(10000);

    service.handleDrawCard(p1Conn, msg);

    bool sawDrawError = false;
    for (const auto& serverMsg : messagesForConn(sentFrames, p1Conn)) {
        if (!serverMsg.has_draw_card_rsp()) continue;
        sawDrawError = serverMsg.draw_card_rsp().error().code() != 0;
    }
    require(sawDrawError,
            "empty deck draw should return a non-zero DrawCardRsp error");
}

void gameOverTieBreakUsesActualRoundScore() {
    cabogame::GameService service;
    std::vector<SentFrame> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr& conn, const std::string& frame) {
        sentFrames.push_back({conn.get(), frame});
    });

    auto p1Conn = fakeConn(13);
    auto p2Conn = fakeConn(14);
    auto room = std::make_shared<cabogame::GameRoom>();
    room->roomId = 2;
    room->roomCode = "TEST02";
    room->maxPlayers = 2;
    room->hostPlayerId = 10001;
    room->step = cabogame::GameStep::Playing;
    room->roundNumber = 1;
    room->currentPlayerSeat = 0;

    auto firstInOrder = player(10001, 0, p2Conn);
    auto steadyCaller = player(10000, 1, p1Conn);
    firstInOrder->totalScore = 95;
    steadyCaller->totalScore = 100;
    firstInOrder->hasUsedReset = true;
    steadyCaller->hasUsedReset = true;
    setHandValues(firstInOrder, {1, 1, 1, 2});
    setHandValues(steadyCaller, {1, 1, 1, 2});
    room->players.push_back(firstInOrder);
    room->players.push_back(steadyCaller);
    room->steadyCallerSeat = 1;
    service.games_[room->roomId] = room;

    service.revealAndScore(*room);

    int64_t winnerId = 0;
    for (const auto& serverMsg : messagesForConn(sentFrames, p1Conn)) {
        if (!serverMsg.has_game_over_notify()) continue;
        for (const auto& ranking : serverMsg.game_over_notify().rankings()) {
            if (ranking.is_winner()) {
                winnerId = ranking.player_id();
                break;
            }
        }
    }

    require(winnerId == 10000,
            "game over tie-break should use actual last round score");
}

} // namespace

int main() {
    rejectsForgedConnectionForDraw();
    hidesDrawnIncomingValueFromOtherPlayers();
    rejectsSkillTypeMismatch();
    removesDisconnectedPlayerFromActiveGame();
    rejectsRestartRoundWhileRoundIsActive();
    deckEmptyDrawReturnsAnError();
    gameOverTieBreakUsesActualRoundScore();
    std::cout << "game_service_regression_test passed\n";
    return 0;
}
