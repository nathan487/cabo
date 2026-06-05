#include <iostream>
#include "GameState.h"

int main(int argc, char* argv[]) {
    std::cout << "Cabo CLI Client v1.0" << std::endl;

    // 测试GameState
    cabo::GameState state;
    state.myPlayerId = 1;
    state.phase = cabo::GameState::LOBBY;

    std::cout << "GameState initialized successfully" << std::endl;
    std::cout << "My player ID: " << state.myPlayerId << std::endl;
    std::cout << "Phase: " << state.phase << std::endl;

    return 0;
}
