#include "ClientApp.h"
#include <iostream>
#include <limits>
#include <chrono>
#include <sys/select.h>

#ifdef _WIN32
    #include <windows.h>
#else
    #include <unistd.h>
#endif

namespace cabo {

// ============================================================================
// 流程方法（保持原有逻辑，移除nextSeq_引用）
// ============================================================================

void ClientApp::connectToServer() {
    while (running_) {
        std::string hostPort;
        std::cout << ">>> Enter server IP:port (e.g., 127.0.0.1:8888): ";
        std::cin >> hostPort;

        size_t colonPos = hostPort.find(':');
        if (colonPos == std::string::npos) {
            std::cout << "ERROR: Invalid format! Use IP:port" << std::endl;
            std::cin.clear();
            std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
            continue;
        }

        std::string host = hostPort.substr(0, colonPos);
        int port = std::stoi(hostPort.substr(colonPos + 1));

        std::cout << ">>> Connecting to " << host << ":" << port << "..." << std::endl;

        if (!network_.connect(host, port)) {
            std::cout << "ERROR: Failed to connect!" << std::endl;
            std::cout << ">>> Retry? (y/n): ";
            char choice;
            std::cin >> choice;
            std::cin.clear();
            std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
            if (choice != 'y' && choice != 'Y') {
                running_ = false;
                return;
            }
            continue;
        }

        std::cout << ">>> Connected!" << std::endl;
        return;
    }
}

void ClientApp::loginFlow() {
    std::cout << ">>> Ready to create or join a room..." << std::endl;
}

void ClientApp::run() {
    std::cout << "================================================================================" << std::endl;
    std::cout << "                    Welcome to Cabo Game CLI Client" << std::endl;
    std::cout << "================================================================================" << std::endl;

    connectToServer();
    if (!running_) return;

    loginFlow();
    if (!running_) return;

    roomFlow();
    if (!running_) return;

    waitingRoomLoop();
    if (!running_) return;

    gameLoop();
}

void ClientApp::roomFlow() {
    while (running_) {
        std::cout << "\n>>> Room Options:" << std::endl;
        std::cout << "    1. Create a new room" << std::endl;
        std::cout << "    2. Join an existing room" << std::endl;
        std::cout << ">>> Enter choice: ";

        int choice;
        std::cin >> choice;

        if (choice == 1) {
            std::string nickname;
            std::cout << ">>> Enter your nickname: ";
            std::cin >> nickname;

            if (nickname.empty() || nickname.length() > 20) {
                std::cout << "ERROR: Nickname must be 1-20 characters!" << std::endl;
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                continue;
            }

            game::messages::ClientMessage reqMsg;
            auto* createReq = reqMsg.mutable_create_room_req();
            createReq->set_request_id(network_.nextRequestId());
            createReq->set_max_players(4);
            createReq->set_nickname(nickname);

            std::cout << ">>> Creating room..." << std::endl;
            if (!network_.send(reqMsg)) {
                std::cout << "ERROR: Failed to send CreateRoomReq!" << std::endl;
                running_ = false;
                return;
            }

            game::messages::ServerMessage rspMsg;
            if (!network_.receive(rspMsg, 5000)) {
                std::cout << "ERROR: Timeout waiting for CreateRoomRsp!" << std::endl;
                running_ = false;
                return;
            }

            state_.updateFromMessage(rspMsg);

            if (!rspMsg.has_create_room_rsp() || rspMsg.create_room_rsp().error().code() != 0) {
                std::cout << "ERROR: Failed to create room!" << std::endl;
                if (rspMsg.has_create_room_rsp()) {
                    std::cout << "       " << rspMsg.create_room_rsp().error().message() << std::endl;
                }
                running_ = false;
                return;
            }

            // 处理CreateRoom后服务端发送的RoomStateNotify等后续消息
            drainMessages(false);

            std::cout << ">>> Room created successfully!" << std::endl;
            std::cout << ">>> Room Code: " << state_.roomCode << std::endl;
            std::cout << ">>> Room ID: " << state_.roomId << std::endl;
            std::cout << ">>> Your Player ID: " << state_.myPlayerId << std::endl;
            break;

        } else if (choice == 2) {
            std::string nickname, roomCode;
            std::cout << ">>> Enter your nickname: ";
            std::cin >> nickname;

            if (nickname.empty() || nickname.length() > 20) {
                std::cout << "ERROR: Nickname must be 1-20 characters!" << std::endl;
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                continue;
            }

            std::cout << ">>> Enter room code: ";
            std::cin >> roomCode;

            if (roomCode.empty()) {
                std::cout << "ERROR: Room code cannot be empty!" << std::endl;
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                continue;
            }

            game::messages::ClientMessage reqMsg;
            auto* joinReq = reqMsg.mutable_join_room_req();
            joinReq->set_request_id(network_.nextRequestId());
            joinReq->set_room_code(roomCode);
            joinReq->set_nickname(nickname);

            std::cout << ">>> Joining room..." << std::endl;
            if (!network_.send(reqMsg)) {
                std::cout << "ERROR: Failed to send JoinRoomReq!" << std::endl;
                running_ = false;
                return;
            }

            bool joinConfirmed = false;
            int attempts = 0;
            const int MAX_ATTEMPTS = 10;

            while (!joinConfirmed && attempts < MAX_ATTEMPTS) {
                game::messages::ServerMessage rspMsg;
                if (!network_.receive(rspMsg, 5000)) {
                    std::cout << "ERROR: Timeout waiting for JoinRoomRsp!" << std::endl;
                    running_ = false;
                    return;
                }

                state_.updateFromMessage(rspMsg);

                if (rspMsg.has_join_room_rsp()) {
                    if (rspMsg.join_room_rsp().error().code() == 0) {
                        joinConfirmed = true;

                        // 处理JoinRoom后服务端发送的后续消息
                        drainMessages(false);

                        std::cout << ">>> Joined room successfully!" << std::endl;
                        std::cout << ">>> Room ID: " << state_.roomId << std::endl;
                        std::cout << ">>> Your Player ID: " << state_.myPlayerId << std::endl;
                    } else {
                        std::cout << "ERROR: Failed to join room: "
                                  << rspMsg.join_room_rsp().error().message() << std::endl;
                        running_ = false;
                        return;
                    }
                }
                attempts++;
            }

            if (!joinConfirmed) {
                std::cout << "ERROR: Did not receive JoinRoomRsp after " << MAX_ATTEMPTS << " messages!" << std::endl;
                running_ = false;
                return;
            }

            break;

        } else {
            std::cout << "ERROR: Invalid choice!" << std::endl;
            std::cin.clear();
            std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
            continue;
        }
    }

    if (!running_) return;
}

void ClientApp::waitingRoomLoop() {
    std::cout << "\n>>> Entering waiting room..." << std::endl;
    std::cout << ">>> Type 'ready' and press Enter to mark yourself as ready" << std::endl;

    // 先处理pending消息
    drainMessages(false);

    bool isHost = false;
    bool autoStartSent = false;
    size_t lastPlayerCount = 0;
    bool lastAllReady = false;
    auto startSentTime = std::chrono::steady_clock::time_point();
    const int START_TIMEOUT_MS = 10000;

    while (running_) {
        // 非阻塞检查并处理所有消息
        bool hasNewMessage = false;
        while (network_.hasMessage(0)) {
            game::messages::ServerMessage msg;
            if (!network_.receive(msg, 1000)) {
                std::cerr << "ERROR: Failed to receive message, connection may be lost" << std::endl;
                running_ = false;
                return;
            }
            state_.updateFromMessage(msg);
            hasNewMessage = true;
        }

        // 检查phase是否已变为PLAYING
        if (state_.phase == GameState::PLAYING) {
            std::cout << "\n>>> Game starting! Transitioning to game loop..." << std::endl;
            break;
        }

        // 检查是否是房主
        isHost = false;
        for (const auto& p : state_.players) {
            if (p.playerId == state_.myPlayerId && p.isHost) {
                isHost = true;
                break;
            }
        }

        // 检查是否所有玩家都ready且人满4人
        bool allReady = true;
        int readyCount = 0;
        if (state_.players.size() == 4) {
            for (const auto& p : state_.players) {
                if (p.isReady) {
                    readyCount++;
                } else {
                    allReady = false;
                    break;
                }
            }
        } else {
            allReady = false;
        }

        // 检查用户输入（非阻塞select on stdin）
        fd_set readfds;
        FD_ZERO(&readfds);
        FD_SET(STDIN_FILENO, &readfds);

        struct timeval tv;
        tv.tv_sec = 0;
        tv.tv_usec = 10000;  // 10ms

        int ret = select(STDIN_FILENO + 1, &readfds, nullptr, nullptr, &tv);

        if (ret > 0 && FD_ISSET(STDIN_FILENO, &readfds)) {
            std::string input;
            std::getline(std::cin, input);

            if (input == "ready") {
                bool alreadyReady = false;
                for (const auto& p : state_.players) {
                    if (p.playerId == state_.myPlayerId && p.isReady) {
                        alreadyReady = true;
                        break;
                    }
                }

                if (alreadyReady) {
                    std::cout << ">>> You are already ready!" << std::endl;
                } else {
                    game::messages::ClientMessage readyMsg;
                    auto* readyReq = readyMsg.mutable_ready_req();
                    readyReq->set_request_id(network_.nextRequestId());
                    readyReq->set_player_id(state_.myPlayerId);
                    readyReq->set_room_id(state_.roomId);
                    readyReq->set_is_ready(true);

                    if (!network_.send(readyMsg)) {
                        std::cout << "ERROR: Failed to send ReadyReq!" << std::endl;
                        running_ = false;
                        return;
                    }
                    std::cout << ">>> Ready signal sent!" << std::endl;
                }
            } else if (input == "start") {
                if (!isHost) {
                    std::cout << ">>> Only host can start the game!" << std::endl;
                } else if (autoStartSent) {
                    std::cout << ">>> Game start already requested, waiting for server..." << std::endl;
                } else if (!allReady || state_.players.size() != 4) {
                    std::cout << ">>> Cannot start: not all players are ready!" << std::endl;
                } else {
                    game::messages::ClientMessage startMsg;
                    auto* startReq = startMsg.mutable_start_game_req();
                    startReq->set_request_id(network_.nextRequestId());
                    startReq->set_player_id(state_.myPlayerId);
                    startReq->set_room_id(state_.roomId);

                    if (!network_.send(startMsg)) {
                        std::cout << "ERROR: Failed to send StartGameReq!" << std::endl;
                        running_ = false;
                        return;
                    }

                    autoStartSent = true;
                    startSentTime = std::chrono::steady_clock::now();
                    std::cout << ">>> StartGameReq sent!" << std::endl;
                }
            }
        }

        // 只在状态变化时渲染
        if (state_.players.size() != lastPlayerCount || allReady != lastAllReady) {
            renderer_.render(state_);
            lastPlayerCount = state_.players.size();
            lastAllReady = allReady;

            if (allReady && state_.players.size() == 4) {
                if (isHost) {
                    std::cout << "\n>>> All players ready! Type 'start' to begin the game" << std::endl;
                } else {
                    std::cout << "\n>>> All players ready! Waiting for host to start..." << std::endl;
                }
            }
        }

        // 检查启动超时
        if (autoStartSent && !state_.gameStartConfirmed) {
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - startSentTime
            ).count();
            if (elapsed > START_TIMEOUT_MS) {
                std::cerr << "ERROR: Game start timeout, server not responding" << std::endl;
                running_ = false;
                return;
            }
        }

        if (!hasNewMessage) {
            #ifdef _WIN32
                Sleep(10);
            #else
                usleep(10000);
            #endif
        }
    }
}

// ============================================================================
// 状态机核心方法
// ============================================================================

void ClientApp::drainMessages(bool render) {
    bool stateChanged = false;
    while (network_.hasMessage(0)) {
        game::messages::ServerMessage msg;
        if (!network_.receive(msg, 100)) break;
        handleServerError(msg);
        state_.updateFromMessage(msg);
        stateChanged = true;
    }
    if (stateChanged && render) {
        renderer_.render(state_);
    }
}

bool ClientApp::tryReadLine(std::string& line) {
    fd_set readfds;
    FD_ZERO(&readfds);
    FD_SET(STDIN_FILENO, &readfds);
    struct timeval tv = {0, 0};
    int ret = select(STDIN_FILENO + 1, &readfds, nullptr, nullptr, &tv);
    if (ret <= 0) return false;

    std::getline(std::cin, line);
    if (std::cin.eof()) {
        running_ = false;
        return false;
    }
    return true;
}

void ClientApp::transitionTo(GameSubState newState) {
    subState_ = newState;
}

bool ClientApp::isExpectingInput() const {
    switch (subState_) {
        case GameSubState::AWAITING_MAIN_INPUT:
        case GameSubState::AWAITING_DRAWN_DECISION:
        case GameSubState::AWAITING_REPLACE_SLOTS:
        case GameSubState::AWAITING_TAKE_SLOTS:
        case GameSubState::SKILL_PEEK_SLOT:
        case GameSubState::SKILL_SPY_TARGET:
        case GameSubState::SKILL_SPY_SLOT:
        case GameSubState::SKILL_SWAP_MY_SLOT:
        case GameSubState::SKILL_SWAP_TARGET_PLAYER:
        case GameSubState::SKILL_SWAP_TARGET_SLOT:
            return true;
        default:
            return false;
    }
}

void ClientApp::showPrompt() {
    switch (subState_) {
        case GameSubState::AWAITING_MAIN_INPUT: {
            bool firstTurn = (state_.turnNumber <= 1);
            std::cout << ">>> Your Turn! Choose action:" << std::endl;
            std::cout << "    1. Draw from draw pile" << std::endl;
            if (!firstTurn && state_.discardPileCount > 0) {
                std::cout << "    2. Take from discard pile";
                if (state_.discardTopValue >= 0) {
                    std::cout << " (current top: " << state_.discardTopValue << ")";
                }
                std::cout << std::endl;
            }
            if (!state_.isFinalRound) {
                std::cout << "    " << (firstTurn ? "2" : "3") << ". Call CABO" << std::endl;
            }
            std::cout << ">>> Enter choice: " << std::flush;
            break;
        }

        case GameSubState::AWAITING_DRAWN_DECISION: {
            bool hasSkill = (state_.drawnCardSkill != SKILL_TYPE_NONE);
            std::cout << "\n>>> You drew: [" << state_.drawnCardValue << "]" << std::endl;
            if (hasSkill) {
                std::cout << ">>> This card has a skill!" << std::endl;
            }
            std::cout << ">>> Choose what to do:" << std::endl;
            std::cout << "    1. Discard" << std::endl;
            std::cout << "    2. Replace your cards with this card" << std::endl;
            if (hasSkill) {
                std::cout << "    3. Use skill: ";
                if (state_.drawnCardSkill == 2) {
                    std::cout << "Peek one of your cards" << std::endl;
                } else if (state_.drawnCardSkill == 3) {
                    std::cout << "Spy on opponent's card" << std::endl;
                } else if (state_.drawnCardSkill == 4) {
                    std::cout << "Swap your card with opponent's card" << std::endl;
                } else {
                    std::cout << "Unknown skill (type=" << state_.drawnCardSkill << ")" << std::endl;
                }
            }
            std::cout << ">>> Enter choice: " << std::flush;
            break;
        }

        case GameSubState::AWAITING_REPLACE_SLOTS:
            std::cout << ">>> Enter slot indices to replace (space-separated, e.g., '0 1'): " << std::flush;
            break;

        case GameSubState::AWAITING_TAKE_SLOTS:
            std::cout << ">>> Taking top card [" << state_.discardTopValue
                      << "] from discard pile" << std::endl;
            std::cout << ">>> Enter slot indices to replace (space-separated, e.g., '0 1'): " << std::flush;
            break;

        case GameSubState::SKILL_PEEK_SLOT:
            std::cout << "\n>>> Peek Self Skill: Choose your slot to peek (0-3): " << std::flush;
            break;

        case GameSubState::SKILL_SPY_TARGET:
            std::cout << "\n>>> Spy Skill: Enter opponent player ID: " << std::flush;
            break;

        case GameSubState::SKILL_SPY_SLOT:
            std::cout << ">>> Choose slot to spy (0-3): " << std::flush;
            break;

        case GameSubState::SKILL_SWAP_MY_SLOT:
            std::cout << "\n>>> Swap Skill: Choose your slot (0-3): " << std::flush;
            break;

        case GameSubState::SKILL_SWAP_TARGET_PLAYER:
            std::cout << ">>> Enter opponent player ID: " << std::flush;
            break;

        case GameSubState::SKILL_SWAP_TARGET_SLOT:
            std::cout << ">>> Choose opponent slot (0-3): " << std::flush;
            break;

        default:
            break;
    }
}

bool ClientApp::sendRequestAndWait(game::messages::ClientMessage& req, GameSubState waitState) {
    if (!network_.send(req)) {
        std::cout << ">>> Failed to send request!" << std::endl;
        return false;
    }
    transitionTo(waitState);
    return true;
}

// ============================================================================
// 非阻塞游戏主循环
// ============================================================================

void ClientApp::gameLoop() {
    std::cout << "[DEBUG] Entered gameLoop, phase=" << state_.phase << std::endl;
    std::cout << "[DEBUG] myPlayerId=" << state_.myPlayerId
              << ", currentPlayerId=" << state_.currentPlayerId << std::endl;

    renderer_.render(state_);
    subState_ = GameSubState::IDLE;

    while (running_ && state_.phase != GameState::GAME_OVER) {
        if (state_.phase != GameState::PLAYING) break;

        bool activity = false;

        // ---- Step 1: 处理所有pending消息 ----
        drainMessages(true);

        // 回合结算：即使GameStartNotify已将phase重置为PLAYING，
        // 也要先显示结算界面再继续
        if (state_.roundJustRevealed) {
            handleRoundRevealPhase();
            state_.roundJustRevealed = false;
            // 结算后可能game over或继续下一轮
            if (state_.phase == GameState::GAME_OVER) break;
            // 下一轮已开始，重置子状态
            subState_ = GameSubState::IDLE;
            renderer_.render(state_);
        }

        if (state_.phase != GameState::PLAYING) break;

        // ---- Step 2: 评估状态转换 ----

        // 非自己回合 → IDLE
        if (!state_.isMyTurn() && subState_ != GameSubState::IDLE &&
            subState_ != GameSubState::WAITING_DRAW_RSP &&
            subState_ != GameSubState::WAITING_DISCARD_RSP &&
            subState_ != GameSubState::WAITING_REPLACE_RSP &&
            subState_ != GameSubState::WAITING_TAKE_RSP &&
            subState_ != GameSubState::WAITING_SKILL_RSP &&
            subState_ != GameSubState::WAITING_CALL_STEADY_RSP) {
            transitionTo(GameSubState::IDLE);
        }

        // DrawCardRsp已到达：waitingForDrawResponse被清除
        if (subState_ == GameSubState::WAITING_DRAW_RSP && !state_.waitingForDrawResponse) {
            if (state_.hasDrawnCard) {
                transitionTo(GameSubState::AWAITING_DRAWN_DECISION);
                showPrompt();
            } else {
                transitionTo(GameSubState::IDLE);
            }
        }

        // TakeFromDiscardRsp已到达
        if (subState_ == GameSubState::WAITING_TAKE_RSP && !state_.waitingForTakeResponse) {
            transitionTo(GameSubState::IDLE);
        }

        // CallSteadyRsp已到达
        if (subState_ == GameSubState::WAITING_CALL_STEADY_RSP && !state_.waitingForCallSteadyResponse) {
            transitionTo(GameSubState::IDLE);
        }

        // DiscardDrawnRsp已到达（hasDrawnCard被清除）
        if (subState_ == GameSubState::WAITING_DISCARD_RSP && !state_.hasDrawnCard) {
            if (skillTypePending_ > 0) {
                // Discard + Use Skill: 进入技能流程
                switch (skillTypePending_) {
                    case 2: transitionTo(GameSubState::SKILL_PEEK_SLOT); break;
                    case 3: transitionTo(GameSubState::SKILL_SPY_TARGET); break;
                    case 4: transitionTo(GameSubState::SKILL_SWAP_MY_SLOT); break;
                    default: transitionTo(GameSubState::IDLE); break;
                }
                showPrompt();
            } else if (skillTypePending_ == -1) {
                // 技能牌丢弃但不使用：发空UseSkillReq通知服务端结束回合
                skillTypePending_ = 0;
                sendSkipSkillRequest();
            } else {
                transitionTo(GameSubState::IDLE);
            }
        }

        // ReplaceWithDrawnRsp已到达（hasDrawnCard被清除）
        if (subState_ == GameSubState::WAITING_REPLACE_RSP && !state_.hasDrawnCard) {
            transitionTo(GameSubState::IDLE);
        }

        // UseSkillRsp已到达
        if (subState_ == GameSubState::WAITING_SKILL_RSP && !state_.waitingForSkillResponse) {
            // 根据技能类型更新手牌状态
            // 保存当前回合状态，技能结果展示期间暂不切换回合
            int64_t savedCurrentPlayerId = state_.currentPlayerId;
            int32_t savedTurnNumber = state_.turnNumber;
            state_.currentPlayerId = state_.myPlayerId;  // 保持箭头在自己身上

            if (skillTypeJustCompleted_ == 2) {
                // PEEK_SELF: 偷看自己的牌 → 标记为已知
                int slot = skillPending_.mySlot;
                if (slot >= 0 && slot < static_cast<int>(state_.myCards.size())) {
                    state_.myCards[slot].isKnown = true;
                    if (state_.lastPeekedValue >= 0) {
                        state_.myCards[slot].value = state_.lastPeekedValue;
                    }
                }
                renderer_.render(state_);
                std::cout << "\n  ╔══════════════════════════════════╗" << std::endl;
                std::cout <<   "  ║  PEEK SELF: Your slot " << skillPending_.mySlot
                          << " = [" << state_.lastPeekedValue << "]" << std::endl;
                std::cout <<   "  ╚══════════════════════════════════╝" << std::endl;
                std::cout << "  (Continuing in 2 seconds...)" << std::flush;
                usleep(2000000);
            } else if (skillTypeJustCompleted_ == 3) {
                // SPY: 偷看对手的牌 → 仅显示数值，不影响自己手牌
                renderer_.render(state_);
                std::cout << "\n  ╔══════════════════════════════════╗" << std::endl;
                std::cout <<   "  ║  SPY: Opponent's card = ["
                          << state_.lastPeekedValue << "]" << std::endl;
                std::cout <<   "  ╚══════════════════════════════════╝" << std::endl;
                std::cout << "  (Continuing in 2 seconds...)" << std::flush;
                usleep(2000000);
            } else if (skillTypeJustCompleted_ == 4) {
                // SWAP: 交换 → 自己的槽位变为未知（盲换）
                int slot = skillPending_.mySlot;
                if (slot >= 0 && slot < static_cast<int>(state_.myCards.size())) {
                    state_.myCards[slot].isKnown = false;
                }
                renderer_.render(state_);
                std::cout << "\n  >>> Swap completed! Your slot " << skillPending_.mySlot
                          << " is now unknown (blind swap)." << std::endl;
                std::cout << "  (Continuing in 2 seconds...)" << std::flush;
                usleep(2000000);
            }

            // 恢复真实的回合状态（切换到下一玩家）
            state_.currentPlayerId = savedCurrentPlayerId;
            state_.turnNumber = savedTurnNumber;
            skillTypeJustCompleted_ = 0;
            transitionTo(GameSubState::IDLE);
            renderer_.render(state_);  // 刷新显示，切换到下一玩家回合
        }

        // ---- Step 3: 自己回合且空闲 → 进入主输入状态 ----
        if (state_.isMyTurn() && subState_ == GameSubState::IDLE &&
            !state_.hasDrawnCard &&
            !state_.waitingForDrawResponse &&
            !state_.waitingForTakeResponse &&
            !state_.waitingForCallSteadyResponse) {
            transitionTo(GameSubState::AWAITING_MAIN_INPUT);
            showPrompt();
        }

        // ---- Step 4: 等待中显示状态 ----
        if (subState_ == GameSubState::IDLE && !state_.isMyTurn()) {
            // 找到当前玩家的昵称
            std::string currentPlayerName = "Player";
            for (const auto& p : state_.players) {
                if (p.playerId == state_.currentPlayerId) {
                    currentPlayerName = p.nickname;
                    break;
                }
            }
        }

        // ---- Step 5: 如果需要输入，select()检查stdin ----
        if (isExpectingInput()) {
            fd_set readfds;
            FD_ZERO(&readfds);
            FD_SET(STDIN_FILENO, &readfds);
            struct timeval tv = {0, 50000};  // 50ms
            int ret = select(STDIN_FILENO + 1, &readfds, nullptr, nullptr, &tv);
            if (ret > 0) {
                std::string line;
                if (tryReadLine(line)) {
                    activity = true;
                    handleInputLine(line);
                }
            }
        }

        // ---- Step 6: 空闲时短暂休眠（避免CPU空转） ----
        if (!activity) {
            #ifdef _WIN32
                Sleep(50);
            #else
                usleep(50000);
            #endif
        }
    }

    // ---- 阶段转换处理 ----
    // handleRoundRevealPhase is now called from inside the main loop (via roundJustRevealed)
    // or when phase transitions to ROUND_REVEAL between rounds.

    if (state_.phase == GameState::GAME_OVER) {
        renderer_.render(state_);
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cin.get();
    }
}

void ClientApp::handleRoundRevealPhase() {
    drainMessages(true);

    // 先展示结算面板，等待用户按Enter确认
    std::cout << "\n>>> Press Enter to continue to waiting room..." << std::flush;
    bool enterPressed = false;
    while (running_ && !enterPressed) {
        drainMessages(true);
        if (state_.phase == GameState::GAME_OVER) return;

        fd_set readfds;
        FD_ZERO(&readfds);
        FD_SET(STDIN_FILENO, &readfds);
        struct timeval tv = {0, 100000};
        int ret = select(STDIN_FILENO + 1, &readfds, nullptr, nullptr, &tv);
        if (ret > 0) {
            std::string line;
            std::getline(std::cin, line);
            enterPressed = true;
        }
    }

    drainMessages(true);
    if (!running_) return;
    if (state_.phase == GameState::GAME_OVER) return;

    // 进入回合间等待：结算面板保持显示，等待所有人ready + 房主start
    std::cout << "\n>>> Type 'ready' to confirm, host type 'start' to begin next round" << std::endl;
    state_.gameStartConfirmed = false;
    bool autoStartSent = false;
    auto startSentTime = std::chrono::steady_clock::time_point();
    const int START_TIMEOUT_MS = 30000;  // 30秒超时

    while (running_ && state_.phase != GameState::PLAYING) {
        drainMessages(true);
        if (state_.phase == GameState::PLAYING) break;
        if (state_.phase == GameState::GAME_OVER) return;

        // 检查是否是房主
        bool isHost = false;
        for (const auto& p : state_.players) {
            if (p.playerId == state_.myPlayerId && p.isHost) {
                isHost = true; break;
            }
        }

        // 检查所有玩家ready状态
        int readyCount = 0;
        bool allReady = true;
        for (const auto& p : state_.players) {
            if (p.isReady) readyCount++;
            else allReady = false;
        }

        // 非阻塞检查stdin
        fd_set readfds;
        FD_ZERO(&readfds);
        FD_SET(STDIN_FILENO, &readfds);
        struct timeval tv = {0, 100000};
        int ret = select(STDIN_FILENO + 1, &readfds, nullptr, nullptr, &tv);

        if (ret > 0 && FD_ISSET(STDIN_FILENO, &readfds)) {
            std::string input;
            std::getline(std::cin, input);

            if (input == "ready") {
                bool alreadyReady = false;
                for (const auto& p : state_.players) {
                    if (p.playerId == state_.myPlayerId && p.isReady) {
                        alreadyReady = true; break;
                    }
                }
                if (alreadyReady) {
                    std::cout << ">>> You are already ready! (" << readyCount << "/4 ready)" << std::endl;
                } else {
                    game::messages::ClientMessage readyMsg;
                    auto* readyReq = readyMsg.mutable_ready_req();
                    readyReq->set_request_id(network_.nextRequestId());
                    readyReq->set_player_id(state_.myPlayerId);
                    readyReq->set_room_id(state_.roomId);
                    readyReq->set_is_ready(true);
                    if (network_.send(readyMsg)) {
                        std::cout << ">>> Ready! (" << (readyCount + 1) << "/4 ready)" << std::endl;
                    }
                }
            } else if (input == "start") {
                if (!isHost) {
                    std::cout << ">>> Only host can start the next round!" << std::endl;
                } else if (autoStartSent) {
                    std::cout << ">>> Start already requested..." << std::endl;
                } else if (!allReady) {
                    std::cout << ">>> Cannot start: " << readyCount << "/4 ready" << std::endl;
                } else {
                    game::messages::ClientMessage startMsg;
                    auto* startReq = startMsg.mutable_start_game_req();
                    startReq->set_request_id(network_.nextRequestId());
                    startReq->set_player_id(state_.myPlayerId);
                    startReq->set_room_id(state_.roomId);
                    if (network_.send(startMsg)) {
                        autoStartSent = true;
                        startSentTime = std::chrono::steady_clock::now();
                        std::cout << ">>> Starting next round..." << std::endl;
                    }
                }
            }
        }

        // 超时检查
        if (autoStartSent) {
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - startSentTime).count();
            if (elapsed > START_TIMEOUT_MS) {
                std::cerr << "ERROR: Start timeout, server not responding" << std::endl;
                running_ = false;
                return;
            }
        }

        usleep(50000);
    }
}

// ============================================================================
// 输入分发
// ============================================================================

void ClientApp::handleInputLine(const std::string& line) {
    switch (subState_) {
        case GameSubState::AWAITING_MAIN_INPUT:
            onMainMenuInput(line);
            break;
        case GameSubState::AWAITING_DRAWN_DECISION:
            onDrawnCardDecision(line);
            break;
        case GameSubState::AWAITING_REPLACE_SLOTS:
            onSlotListInput(line, false);
            break;
        case GameSubState::AWAITING_TAKE_SLOTS:
            onSlotListInput(line, true);
            break;
        case GameSubState::SKILL_PEEK_SLOT:
        case GameSubState::SKILL_SPY_TARGET:
        case GameSubState::SKILL_SPY_SLOT:
        case GameSubState::SKILL_SWAP_MY_SLOT:
        case GameSubState::SKILL_SWAP_TARGET_PLAYER:
        case GameSubState::SKILL_SWAP_TARGET_SLOT:
            onSkillInputLine(line);
            break;
        default:
            break;
    }
}

// ============================================================================
// 主菜单输入处理
// ============================================================================

void ClientApp::onMainMenuInput(const std::string& line) {
    bool firstTurn = (state_.turnNumber <= 1);
    int maxChoice;
    if (firstTurn) {
        maxChoice = state_.isFinalRound ? 1 : 2;  // only draw (+ cabo if not final)
    } else if (state_.discardPileCount <= 0) {
        maxChoice = state_.isFinalRound ? 1 : 2;  // only draw (+ cabo)
    } else {
        maxChoice = state_.isFinalRound ? 2 : 3;  // full options (less cabo if final)
    }

    int choice;
    if (!parseInt(line, choice, 1, maxChoice)) {
        showPrompt();
        return;
    }

    // Remap choice: on first turn or empty discard, option 2 is CABO (normally option 3)
    int action = choice;
    if (firstTurn || state_.discardPileCount <= 0) {
        if (choice == 2 && !state_.isFinalRound) action = 3;  // remap to CABO
    }

    game::messages::ClientMessage req;
    int64_t reqId = network_.nextRequestId();

    if (action == 1) {
        // 抽牌
        auto* drawReq = req.mutable_draw_card_req();
        drawReq->set_player_id(state_.myPlayerId);
        drawReq->set_room_id(state_.roomId);
        drawReq->set_request_id(reqId);

        if (sendRequestAndWait(req, GameSubState::WAITING_DRAW_RSP)) {
            std::cout << ">>> Drawing card..." << std::endl;
            state_.waitingForDrawResponse = true;
        }
    } else if (action == 2) {
        // 从弃牌堆拿牌 → 先输入槽位
        transitionTo(GameSubState::AWAITING_TAKE_SLOTS);
        showPrompt();
    } else if (action == 3) {
        // 喊CABO
        if (state_.waitingForCallSteadyResponse) {
            std::cout << ">>> Already waiting for CABO response from server!" << std::endl;
            showPrompt();
            return;
        }
        auto* caboReq = req.mutable_call_steady_req();
        caboReq->set_player_id(state_.myPlayerId);
        caboReq->set_room_id(state_.roomId);
        caboReq->set_request_id(reqId);

        if (sendRequestAndWait(req, GameSubState::WAITING_CALL_STEADY_RSP)) {
            std::cout << ">>> Called CABO!" << std::endl;
            state_.waitingForCallSteadyResponse = true;
        }
    }
}

// ============================================================================
// 抽牌后决策输入处理
// ============================================================================

void ClientApp::onDrawnCardDecision(const std::string& line) {
    bool hasSkill = (state_.drawnCardSkill != SKILL_TYPE_NONE);
    int maxChoice = hasSkill ? 3 : 2;

    int choice;
    if (!parseInt(line, choice, 1, maxChoice)) {
        showPrompt();
        return;
    }

    int64_t reqId = network_.nextRequestId();
    game::messages::ClientMessage req;

    if (choice == 1) {
        // 弃掉（不使用技能）
        auto* discardReq = req.mutable_discard_drawn_req();
        discardReq->set_player_id(state_.myPlayerId);
        discardReq->set_room_id(state_.roomId);
        discardReq->set_request_id(reqId);

        if (sendRequestAndWait(req, GameSubState::WAITING_DISCARD_RSP)) {
            std::cout << ">>> Discarding..." << std::endl;
            // 技能牌丢弃但不使用：标记为 -1，稍后发空UseSkillReq通知服务端结束回合
            // 非技能牌：标记为 0，DiscardDrawnRsp后服务端已自动结束回合
            skillTypePending_ = hasSkill ? -1 : 0;
        }
    } else if (choice == 2) {
        // 替换
        transitionTo(GameSubState::AWAITING_REPLACE_SLOTS);
        showPrompt();
    } else if (choice == 3) {
        // 弃掉并使用技能
        auto* discardReq = req.mutable_discard_drawn_req();
        discardReq->set_player_id(state_.myPlayerId);
        discardReq->set_room_id(state_.roomId);
        discardReq->set_request_id(reqId);

        if (sendRequestAndWait(req, GameSubState::WAITING_DISCARD_RSP)) {
            std::cout << ">>> Discarding and using skill..." << std::endl;
            skillTypePending_ = state_.drawnCardSkill;
        }
    }
}

// ============================================================================
// 槽位列表输入处理（替换/从弃牌堆拿牌共用）
// ============================================================================

void ClientApp::onSlotListInput(const std::string& line, bool fromDiscard) {
    std::vector<int> slots = parseSlotIndices(line);

    if (slots.empty()) {
        std::cout << ">>> No valid slots entered. Please enter space-separated slot numbers." << std::endl;
        showPrompt();
        return;
    }

    int64_t reqId = network_.nextRequestId();
    game::messages::ClientMessage req;

    if (fromDiscard) {
        auto* takeReq = req.mutable_take_from_discard_req();
        takeReq->set_player_id(state_.myPlayerId);
        takeReq->set_room_id(state_.roomId);
        takeReq->set_request_id(reqId);
        for (int s : slots) takeReq->add_slot_indices(s);
    } else {
        auto* replaceReq = req.mutable_replace_with_drawn_req();
        replaceReq->set_player_id(state_.myPlayerId);
        replaceReq->set_room_id(state_.roomId);
        replaceReq->set_request_id(reqId);
        for (int s : slots) replaceReq->add_slot_indices(s);
    }

    GameSubState waitState = fromDiscard ? GameSubState::WAITING_TAKE_RSP : GameSubState::WAITING_REPLACE_RSP;

    if (fromDiscard) {
        state_.waitingForTakeResponse = true;
    }

    std::cout << ">>> Attempting to replace slots [";
    for (size_t i = 0; i < slots.size(); ++i) {
        std::cout << slots[i];
        if (i < slots.size() - 1) std::cout << ", ";
    }
    std::cout << "]..." << std::endl;

    if (!sendRequestAndWait(req, waitState)) {
        if (fromDiscard) state_.waitingForTakeResponse = false;
        transitionTo(GameSubState::IDLE);
    }
}

// ============================================================================
// 技能输入处理（链式状态机）
// ============================================================================

void ClientApp::onSkillInputLine(const std::string& line) {
    switch (subState_) {
        case GameSubState::SKILL_PEEK_SLOT: {
            int slot;
            if (!parseInt(line, slot, 0, 3)) { showPrompt(); return; }
            skillPending_.mySlot = slot;
            sendSkillRequest();
            break;
        }
        case GameSubState::SKILL_SPY_TARGET: {
            int64_t pid;
            if (!parseInt64(line, pid)) { showPrompt(); return; }
            bool found = false;
            for (const auto& p : state_.players) {
                if (p.playerId == pid && p.playerId != state_.myPlayerId) {
                    found = true; break;
                }
            }
            if (!found) {
                std::cout << ">>> Player ID not found or is yourself. Try again." << std::endl;
                showPrompt(); return;
            }
            skillPending_.targetPlayerId = pid;
            transitionTo(GameSubState::SKILL_SPY_SLOT);
            showPrompt();
            break;
        }
        case GameSubState::SKILL_SPY_SLOT: {
            int slot;
            if (!parseInt(line, slot, 0, 3)) { showPrompt(); return; }
            skillPending_.targetSlot = slot;
            sendSkillRequest();
            break;
        }
        case GameSubState::SKILL_SWAP_MY_SLOT: {
            int slot;
            if (!parseInt(line, slot, 0, 3)) { showPrompt(); return; }
            skillPending_.mySlot = slot;
            transitionTo(GameSubState::SKILL_SWAP_TARGET_PLAYER);
            showPrompt();
            break;
        }
        case GameSubState::SKILL_SWAP_TARGET_PLAYER: {
            int64_t pid;
            if (!parseInt64(line, pid)) { showPrompt(); return; }
            bool found = false;
            for (const auto& p : state_.players) {
                if (p.playerId == pid && p.playerId != state_.myPlayerId) {
                    found = true; break;
                }
            }
            if (!found) {
                std::cout << ">>> Player ID not found or is yourself. Try again." << std::endl;
                showPrompt(); return;
            }
            skillPending_.targetPlayerId = pid;
            transitionTo(GameSubState::SKILL_SWAP_TARGET_SLOT);
            showPrompt();
            break;
        }
        case GameSubState::SKILL_SWAP_TARGET_SLOT: {
            int slot;
            if (!parseInt(line, slot, 0, 3)) { showPrompt(); return; }
            skillPending_.targetSlot = slot;
            sendSkillRequest();
            break;
        }
        default:
            break;
    }
}

void ClientApp::sendSkillRequest() {
    int64_t reqId = network_.nextRequestId();
    game::messages::ClientMessage req;
    auto* skillReq = req.mutable_use_skill_req();
    skillReq->set_player_id(state_.myPlayerId);
    skillReq->set_room_id(state_.roomId);
    skillReq->set_request_id(reqId);
    skillReq->set_card_id(0);

    switch (skillTypePending_) {
        case 2: {  // PEEK_SELF
            auto* params = skillReq->mutable_peek_self();
            params->set_slot_index(skillPending_.mySlot);
            std::cout << ">>> Using Peek Self skill on slot " << skillPending_.mySlot << "..." << std::endl;
            break;
        }
        case 3: {  // SPY
            auto* params = skillReq->mutable_spy();
            params->set_target_player_id(skillPending_.targetPlayerId);
            params->set_target_slot_index(skillPending_.targetSlot);
            std::cout << ">>> Using Spy skill on player " << skillPending_.targetPlayerId
                      << " slot " << skillPending_.targetSlot << "..." << std::endl;
            break;
        }
        case 4: {  // SWAP
            auto* params = skillReq->mutable_swap();
            params->set_own_slot_index(skillPending_.mySlot);
            params->set_target_player_id(skillPending_.targetPlayerId);
            params->set_target_slot_index(skillPending_.targetSlot);
            std::cout << ">>> Using Swap skill: your slot " << skillPending_.mySlot
                      << " <-> player " << skillPending_.targetPlayerId
                      << " slot " << skillPending_.targetSlot << "..." << std::endl;
            break;
        }
    }

    skillTypeJustCompleted_ = skillTypePending_;  // 保存以在UseSkillRsp后更新手牌
    skillTypePending_ = 0;  // 清除待处理技能

    if (sendRequestAndWait(req, GameSubState::WAITING_SKILL_RSP)) {
        state_.waitingForSkillResponse = true;
    } else {
        skillTypeJustCompleted_ = 0;
        transitionTo(GameSubState::IDLE);
    }
}

void ClientApp::sendSkipSkillRequest() {
    // 发空参数UseSkillReq通知服务端：丢弃了技能牌但不使用技能，请结束回合
    int64_t reqId = network_.nextRequestId();
    game::messages::ClientMessage req;
    auto* skillReq = req.mutable_use_skill_req();
    skillReq->set_player_id(state_.myPlayerId);
    skillReq->set_room_id(state_.roomId);
    skillReq->set_request_id(reqId);
    skillReq->set_card_id(0);
    // 不设置任何技能参数 → 服务端识别为"skip skill"

    std::cout << ">>> Skipping skill, ending turn..." << std::endl;

    if (sendRequestAndWait(req, GameSubState::WAITING_SKILL_RSP)) {
        state_.waitingForSkillResponse = true;
    } else {
        transitionTo(GameSubState::IDLE);
    }
}

// ============================================================================
// 解析辅助方法
// ============================================================================

bool ClientApp::parseInt(const std::string& str, int& out, int min, int max) {
    try {
        size_t pos;
        int val = std::stoi(str, &pos);
        if (pos != str.length()) {
            std::cout << ">>> Please enter just a number." << std::endl;
            return false;
        }
        if (val < min || val > max) {
            std::cout << ">>> Please enter " << min << "-" << max << "." << std::endl;
            return false;
        }
        out = val;
        return true;
    } catch (...) {
        std::cout << ">>> Invalid number." << std::endl;
        return false;
    }
}

bool ClientApp::parseInt64(const std::string& str, int64_t& out) {
    try {
        size_t pos;
        out = std::stoll(str, &pos);
        if (pos != str.length()) {
            std::cout << ">>> Please enter just a number." << std::endl;
            return false;
        }
        return true;
    } catch (...) {
        std::cout << ">>> Invalid number." << std::endl;
        return false;
    }
}

// ============================================================================
// 工具方法
// ============================================================================

std::vector<int> ClientApp::parseSlotIndices(const std::string& input) {
    std::vector<int> result;
    std::string token;

    for (char c : input) {
        if (c == ' ' || c == ',') {
            if (!token.empty()) {
                try {
                    int slot = std::stoi(token);
                    if (slot >= 0 && slot < MAX_SLOT_INDEX) {
                        result.push_back(slot);
                    }
                } catch (...) {}
                token.clear();
            }
        } else if (c >= '0' && c <= '9') {
            token += c;
        }
    }

    if (!token.empty()) {
        try {
            int slot = std::stoi(token);
            if (slot >= 0 && slot < MAX_SLOT_INDEX) {
                result.push_back(slot);
            }
        } catch (...) {}
    }

    return result;
}

// ============================================================================
// 错误处理
// ============================================================================

void ClientApp::handleServerError(const game::messages::ServerMessage& msg) {
    if (msg.has_create_room_rsp() && msg.create_room_rsp().error().code() != 0) {
        std::cout << "ERROR: " << msg.create_room_rsp().error().message() << std::endl;
        std::cout << ">>> Press Enter to continue..." << std::endl;
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cin.get();
    }
    else if (msg.has_join_room_rsp() && msg.join_room_rsp().error().code() != 0) {
        std::cout << "ERROR: " << msg.join_room_rsp().error().message() << std::endl;
        std::cout << ">>> Press Enter to continue..." << std::endl;
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cin.get();
    }
    else if (msg.has_ready_rsp() && msg.ready_rsp().error().code() != 0) {
        std::cout << "ERROR: Ready failed - " << msg.ready_rsp().error().message() << std::endl;
    }
    else if (msg.has_start_game_rsp() && msg.start_game_rsp().error().code() != 0) {
        std::cout << "ERROR: Start game failed - " << msg.start_game_rsp().error().message() << std::endl;
    }
    else if (msg.has_draw_card_rsp() && msg.draw_card_rsp().error().code() != 0) {
        std::cout << "ERROR: " << msg.draw_card_rsp().error().message() << std::endl;
        renderer_.render(state_);
    }
    else if (msg.has_discard_drawn_rsp() && msg.discard_drawn_rsp().error().code() != 0) {
        std::cout << "ERROR: Discard failed - " << msg.discard_drawn_rsp().error().message() << std::endl;
        renderer_.render(state_);
    }
    else if (msg.has_replace_with_drawn_rsp() && msg.replace_with_drawn_rsp().error().code() != 0) {
        std::cout << "ERROR: Replace failed - " << msg.replace_with_drawn_rsp().error().message() << std::endl;
        renderer_.render(state_);
    }
    else if (msg.has_take_from_discard_rsp() && msg.take_from_discard_rsp().error().code() != 0) {
        std::cout << "ERROR: " << msg.take_from_discard_rsp().error().message() << std::endl;
        renderer_.render(state_);
    }
    else if (msg.has_use_skill_rsp() && msg.use_skill_rsp().error().code() != 0) {
        std::cout << "ERROR: " << msg.use_skill_rsp().error().message() << std::endl;
        renderer_.render(state_);
    }
    else if (msg.has_call_steady_rsp() && msg.call_steady_rsp().error().code() != 0) {
        std::cout << "ERROR: Call CABO failed - " << msg.call_steady_rsp().error().message() << std::endl;
        renderer_.render(state_);
    }
}

} // namespace cabo
