#define private public
#include "game/GameService.h"
#undef private

#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <string>
#include <vector>

namespace {

cabogame::TcpConnectionPtr fakeConn(std::intptr_t tag) {
    return cabogame::TcpConnectionPtr(reinterpret_cast<TcpConnection*>(tag),
                                      [](TcpConnection*) {});
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

void rejectsForgedConnectionForDraw() {
    cabogame::GameService service;
    std::vector<std::string> sentFrames;
    service.setSendFunc([&](const cabogame::TcpConnectionPtr&, const std::string& frame) {
        sentFrames.push_back(frame);
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

} // namespace

int main() {
    rejectsForgedConnectionForDraw();
    std::cout << "game_service_regression_test passed\n";
    return 0;
}
