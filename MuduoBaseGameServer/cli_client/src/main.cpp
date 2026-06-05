#include <iostream>
#include "GameState.h"
#include "UIRenderer.h"

int main(int argc, char* argv[]) {
    cabo::GameState state;
    state.myPlayerId = 1;
    state.phase = cabo::GameState::WAITING_ROOM;

    cabo::Player p1;
    p1.playerId = 1;
    p1.nickname = "Alice";
    p1.seatId = 0;
    p1.isHost = true;
    p1.isReady = true;

    state.players.push_back(p1);

    cabo::UIRenderer renderer;
    renderer.render(state);

    std::cout << std::endl << "Press Enter to exit..." << std::endl;
    std::cin.get();

    return 0;
}
