#include <iostream>
#include <thread>
#include <chrono>
#include "GameState.h"
#include "UIRenderer.h"

void testWaitingRoom() {
    cabo::GameState state;
    state.myPlayerId = 1;
    state.phase = cabo::GameState::WAITING_ROOM;

    cabo::Player p1, p2, p3;
    p1.playerId = 1; p1.nickname = "Alice"; p1.seatId = 0; p1.isHost = true; p1.isReady = true;
    p2.playerId = 2; p2.nickname = "Bob"; p2.seatId = 1; p2.isReady = false;
    p3.playerId = 3; p3.nickname = "Charlie"; p3.seatId = 2; p3.isReady = true;

    state.players = {p1, p2, p3};

    cabo::UIRenderer renderer;
    renderer.render(state);
    std::cout << std::endl << "=== WAITING ROOM TEST ===" << std::endl;
    std::this_thread::sleep_for(std::chrono::seconds(2));
}

void testPlayingPhase() {
    cabo::GameState state;
    state.myPlayerId = 1;
    state.phase = cabo::GameState::PLAYING;
    state.roundNumber = 1;
    state.turnNumber = 3;
    state.currentPlayerId = 1;

    // Setup players
    cabo::Player p1, p2, p3, p4;
    p1.playerId = 1; p1.nickname = "Alice"; p1.seatId = 0; p1.cardCount = 4; p1.totalScore = 12;
    p2.playerId = 2; p2.nickname = "Bob"; p2.seatId = 1; p2.cardCount = 4; p2.totalScore = 15;
    p3.playerId = 3; p3.nickname = "Charlie"; p3.seatId = 2; p3.cardCount = 4; p3.totalScore = 8;
    p4.playerId = 4; p4.nickname = "David"; p4.seatId = 3; p4.cardCount = 4; p4.totalScore = 20;

    state.players = {p1, p2, p3, p4};

    // Setup my cards
    cabo::Card c1, c2, c3, c4;
    c1.slotIndex = 0; c1.isKnown = true; c1.value = 5;
    c2.slotIndex = 1; c2.isKnown = false;
    c3.slotIndex = 2; c3.isKnown = true; c3.value = 3;
    c4.slotIndex = 3; c4.isKnown = false;

    state.myCards = {c1, c2, c3, c4};

    // Setup piles
    state.drawPileCount = 32;
    state.discardPileCount = 5;
    state.discardTopValue = 7;

    cabo::UIRenderer renderer;
    renderer.render(state);
    std::cout << std::endl << "=== PLAYING PHASE TEST (My Turn) ===" << std::endl;
    std::this_thread::sleep_for(std::chrono::seconds(2));
}

void testWaitingForOthers() {
    cabo::GameState state;
    state.myPlayerId = 1;
    state.phase = cabo::GameState::PLAYING;
    state.roundNumber = 2;
    state.turnNumber = 8;
    state.currentPlayerId = 3; // Charlie's turn

    // Setup players
    cabo::Player p1, p2, p3, p4;
    p1.playerId = 1; p1.nickname = "Alice"; p1.seatId = 0; p1.cardCount = 4; p1.totalScore = 12;
    p2.playerId = 2; p2.nickname = "Bob"; p2.seatId = 1; p2.cardCount = 4; p2.totalScore = 15;
    p3.playerId = 3; p3.nickname = "Charlie"; p3.seatId = 2; p3.cardCount = 3; p3.totalScore = 8;
    p4.playerId = 4; p4.nickname = "David"; p4.seatId = 3; p4.cardCount = 4; p4.totalScore = 20;

    state.players = {p1, p2, p3, p4};

    // Setup my cards
    cabo::Card c1, c2, c3, c4;
    c1.slotIndex = 0; c1.isKnown = true; c1.value = 2;
    c2.slotIndex = 1; c2.isKnown = true; c2.value = 8;
    c3.slotIndex = 2; c3.isKnown = false;
    c4.slotIndex = 3; c4.isKnown = true; c4.value = 6;

    state.myCards = {c1, c2, c3, c4};

    // Setup piles
    state.drawPileCount = 28;
    state.discardPileCount = 8;
    state.discardTopValue = 4;

    cabo::UIRenderer renderer;
    renderer.render(state);
    std::cout << std::endl << "=== PLAYING PHASE TEST (Waiting for Charlie) ===" << std::endl;
    std::this_thread::sleep_for(std::chrono::seconds(2));
}

int main() {
    std::cout << "Starting UIRenderer comprehensive test..." << std::endl;
    std::this_thread::sleep_for(std::chrono::seconds(1));

    testWaitingRoom();
    testPlayingPhase();
    testWaitingForOthers();

    std::cout << std::endl << "All tests completed! Press Enter to exit..." << std::endl;
    std::cin.get();

    return 0;
}
