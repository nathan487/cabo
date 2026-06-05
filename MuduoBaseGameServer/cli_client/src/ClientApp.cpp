#include "ClientApp.h"
#include <iostream>
#include <limits>
#include <chrono>

#ifdef _WIN32
    #include <windows.h>
#else
    #include <unistd.h>
#endif

namespace cabo {

void ClientApp::connectToServer() {
    while (running_) {
        std::string hostPort;
        std::cout << ">>> Enter server IP:port (e.g., 127.0.0.1:8888): ";
        std::cin >> hostPort;

        // 解析host和port
        size_t colonPos = hostPort.find(':');
        if (colonPos == std::string::npos) {
            std::cout << "ERROR: Invalid format! Use IP:port" << std::endl;
            std::cin.clear();
            std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
            continue;  // 重试
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
            continue;  // 重试
        }

        std::cout << ">>> Connected!" << std::endl;
        return;  // 成功连接
    }
}

void ClientApp::loginFlow() {
    // 登录流程已简化，仅用于占位
    // 昵称将在创建/加入房间时一并发送
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
            // 创建房间流程
            std::string nickname;
            std::cout << ">>> Enter your nickname: ";
            std::cin >> nickname;

            if (nickname.empty() || nickname.length() > 20) {
                std::cout << "ERROR: Nickname must be 1-20 characters!" << std::endl;
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                continue;  // 重试
            }

            // 构建CreateRoomReq
            game::messages::ClientMessage reqMsg;
            reqMsg.set_seq(nextSeq_);
            auto* createReq = reqMsg.mutable_create_room_req();
            createReq->set_request_id(nextSeq_);
            nextSeq_++;
            createReq->set_max_players(4);
            createReq->set_nickname(nickname);

            std::cout << ">>> Creating room..." << std::endl;
            if (!network_.send(reqMsg)) {
                std::cout << "ERROR: Failed to send CreateRoomReq!" << std::endl;
                running_ = false;
                return;
            }

            // 等待CreateRoomRsp
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

            std::cout << ">>> Room created successfully!" << std::endl;
            std::cout << ">>> Room Code: " << state_.roomCode << std::endl;
            std::cout << ">>> Room ID: " << state_.roomId << std::endl;
            std::cout << ">>> Your Player ID: " << state_.myPlayerId << std::endl;
            break;  // 成功，退出循环

        } else if (choice == 2) {
            // 加入房间流程
            std::string nickname, roomCode;
            std::cout << ">>> Enter your nickname: ";
            std::cin >> nickname;

            if (nickname.empty() || nickname.length() > 20) {
                std::cout << "ERROR: Nickname must be 1-20 characters!" << std::endl;
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                continue;  // 重试
            }

            std::cout << ">>> Enter room code: ";
            std::cin >> roomCode;

            if (roomCode.empty()) {
                std::cout << "ERROR: Room code cannot be empty!" << std::endl;
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                continue;  // 重试
            }

            // 构建JoinRoomReq
            game::messages::ClientMessage reqMsg;
            reqMsg.set_seq(nextSeq_);
            auto* joinReq = reqMsg.mutable_join_room_req();
            joinReq->set_request_id(nextSeq_);
            nextSeq_++;
            joinReq->set_room_code(roomCode);
            joinReq->set_nickname(nickname);

            std::cout << ">>> Joining room..." << std::endl;
            if (!network_.send(reqMsg)) {
                std::cout << "ERROR: Failed to send JoinRoomReq!" << std::endl;
                running_ = false;
                return;
            }

            // 等待JoinRoomRsp，可能会先收到其他通知消息
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

            break;  // 成功，退出循环

        } else {
            std::cout << "ERROR: Invalid choice!" << std::endl;
            std::cin.clear();
            std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
            continue;  // 重试
        }
    }

    if (!running_) return;

    // 不再自动ready，进入等待室后用户手动输入ready命令
}

void ClientApp::waitingRoomLoop() {
    std::cout << "\n>>> Entering waiting room..." << std::endl;
    std::cout << ">>> Type 'ready' and press Enter to mark yourself as ready" << std::endl;

    // 先主动接收pending消息
    while (network_.hasMessage(50)) {
        game::messages::ServerMessage msg;
        if (network_.receive(msg, 100)) {
            state_.updateFromMessage(msg);
        }
    }

    bool isHost = false;
    bool autoStartSent = false;
    size_t lastPlayerCount = 0;
    bool lastAllReady = false;
    auto startSentTime = std::chrono::steady_clock::time_point();
    const int START_TIMEOUT_MS = 10000;  // 10秒超时

    while (running_) {
        // 先检查是否有新消息（使用短超时以快速响应）
        if (network_.hasMessage(50)) {
            game::messages::ServerMessage msg;
            if (!network_.receive(msg, 1000)) {
                std::cerr << "ERROR: Failed to receive message, connection may be lost" << std::endl;
                running_ = false;
                return;
            }
            state_.updateFromMessage(msg);
        }

        // 检查phase是否已变为PLAYING（必须在外面，即使没有新消息也要检查）
        if (state_.phase == GameState::PLAYING) {
            std::cout << "\n>>> Game starting! Transitioning to game loop..." << std::endl;
            std::cout << "[DEBUG] Current phase: PLAYING, breaking from waitingRoomLoop" << std::endl;
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

        // 检查用户输入（非阻塞）
        fd_set readfds;
        FD_ZERO(&readfds);
        FD_SET(STDIN_FILENO, &readfds);

        struct timeval tv;
        tv.tv_sec = 0;
        tv.tv_usec = 10000;  // 10ms timeout

        int ret = select(STDIN_FILENO + 1, &readfds, nullptr, nullptr, &tv);

        if (ret > 0 && FD_ISSET(STDIN_FILENO, &readfds)) {
            std::string input;
            std::getline(std::cin, input);

            if (input == "ready") {
                // 发送ReadyReq
                game::messages::ClientMessage readyMsg;
                readyMsg.set_seq(nextSeq_);
                auto* readyReq = readyMsg.mutable_ready_req();
                readyReq->set_request_id(nextSeq_);
                nextSeq_++;
                readyReq->set_player_id(state_.myPlayerId);
                readyReq->set_room_id(state_.roomId);
                readyReq->set_is_ready(true);

                if (!network_.send(readyMsg)) {
                    std::cout << "ERROR: Failed to send ReadyReq!" << std::endl;
                    running_ = false;
                    return;
                }

                std::cout << ">>> Ready signal sent!" << std::endl;
            } else if (input == "start") {
                // 只有房主且所有人ready才能start
                if (!isHost) {
                    std::cout << ">>> Only host can start the game!" << std::endl;
                } else if (!allReady || state_.players.size() != 4) {
                    std::cout << ">>> Cannot start: not all players are ready!" << std::endl;
                } else if (!autoStartSent) {
                    game::messages::ClientMessage startMsg;
                    startMsg.set_seq(nextSeq_);
                    auto* startReq = startMsg.mutable_start_game_req();
                    startReq->set_request_id(nextSeq_);
                    nextSeq_++;
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

            // 显示提示信息
            if (allReady && state_.players.size() == 4) {
                if (isHost) {
                    std::cout << "\n>>> All players ready! Type 'start' to begin the game" << std::endl;
                } else {
                    std::cout << "\n>>> All players ready! Waiting for host to start..." << std::endl;
                }
            }
        }

        // 检查启动超时
        if (autoStartSent) {
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - startSentTime
            ).count();
            if (elapsed > START_TIMEOUT_MS) {
                std::cerr << "ERROR: Game start timeout, server not responding" << std::endl;
                running_ = false;
                return;
            }
        }

        // 不需要额外休眠，因为hasMessage已经有50ms超时，足够避免CPU占用过高
    }
}

void ClientApp::gameLoop() {
    std::cout << "[DEBUG] Entered gameLoop, phase=" << state_.phase << std::endl;
    std::cout << "[DEBUG] myPlayerId=" << state_.myPlayerId
              << ", currentPlayerId=" << state_.currentPlayerId << std::endl;

    renderer_.render(state_);

    while (running_ && state_.phase == GameState::PLAYING) {
        // 检查服务端消息
        if (network_.hasMessage(100)) {
            game::messages::ServerMessage msg;
            if (!network_.receive(msg, 1000)) {
                std::cerr << "ERROR: Failed to receive message, connection lost" << std::endl;
                running_ = false;
                break;
            }
            handleServerError(msg);  // 先检查错误
            state_.updateFromMessage(msg);
            renderer_.render(state_);
        }

        // 如果是我的回合且没有抽牌状态
        if (state_.isMyTurn() && !state_.hasDrawnCard) {
            handleGameInput();
        }

        // 如果抽了牌，处理抽牌后决策
        if (state_.hasDrawnCard) {
            handleDrawnCardDecision();
        }

        // 非当前回合，短暂休眠
        if (!state_.isMyTurn()) {
            #ifdef _WIN32
                Sleep(100);
            #else
                usleep(100000);  // 100ms
            #endif
        }
    }

    // 处理结算阶段
    if (state_.phase == GameState::ROUND_REVEAL) {
        renderer_.render(state_);
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cin.get();

        // 等待下一轮或游戏结束
        while (running_ && state_.phase == GameState::ROUND_REVEAL) {
            if (network_.hasMessage(100)) {
                game::messages::ServerMessage msg;
                if (network_.receive(msg, 1000)) {
                    state_.updateFromMessage(msg);
                    if (state_.phase == GameState::PLAYING) {
                        // 进入下一轮
                        return gameLoop();
                    } else if (state_.phase == GameState::GAME_OVER) {
                        break;
                    }
                }
            }
            #ifdef _WIN32
                Sleep(100);
            #else
                usleep(100000);
            #endif
        }
    }

    // 处理游戏结束
    if (state_.phase == GameState::GAME_OVER) {
        renderer_.render(state_);
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cin.get();
    }
}

void ClientApp::handleGameInput() {
    int choice;
    std::cin >> choice;

    // 检查EOF
    if (std::cin.eof()) {
        std::cout << "\n>>> EOF detected, exiting..." << std::endl;
        running_ = false;
        return;
    }

    if (std::cin.fail() || choice < 1 || choice > 3) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid input! Please enter 1, 2, or 3." << std::endl;
        renderer_.render(state_);
        return;
    }

    game::messages::ClientMessage req;

    if (choice == 1) {
        // 抽牌
        auto* drawReq = req.mutable_draw_card_req();
        drawReq->set_player_id(state_.myPlayerId);
        drawReq->set_room_id(state_.roomId);
        drawReq->set_request_id(nextSeq_);
        req.set_seq(nextSeq_++);

        if (network_.send(req)) {
            std::cout << ">>> Drawing card..." << std::endl;
        }

    } else if (choice == 2) {
        // 从弃牌堆拿牌
        handleTakeFromDiscard();

    } else if (choice == 3) {
        // 喊CABO
        auto* caboReq = req.mutable_call_steady_req();
        caboReq->set_player_id(state_.myPlayerId);
        caboReq->set_room_id(state_.roomId);
        caboReq->set_request_id(nextSeq_);
        req.set_seq(nextSeq_++);

        if (network_.send(req)) {
            std::cout << ">>> Called CABO!" << std::endl;
        }
    }
}

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
                } catch (...) {
                    // 忽略非法输入
                }
                token.clear();
            }
        } else if (c >= '0' && c <= '9') {
            token += c;
        }
    }

    // 处理最后一个token
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

void ClientApp::handleDrawnCardDecision() {
    std::cout << "\n>>> You drew: [" << state_.drawnCardValue << "]" << std::endl;

    if (state_.drawnCardSkill != SKILL_TYPE_NONE) {
        std::cout << ">>> This card has a skill!" << std::endl;
    }

    std::cout << ">>> Choose what to do:" << std::endl;
    std::cout << "    1. Discard";
    if (state_.drawnCardSkill != SKILL_TYPE_NONE) {
        std::cout << " and use skill";
    }
    std::cout << std::endl;
    std::cout << "    2. Replace your cards with this card" << std::endl;
    std::cout << ">>> Enter choice: ";

    int choice;
    std::cin >> choice;

    if (std::cin.eof()) {
        running_ = false;
        state_.hasDrawnCard = false;
        return;
    }

    if (std::cin.fail() || (choice != 1 && choice != 2)) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid choice! Please enter 1 or 2." << std::endl;
        renderer_.render(state_);
        // 添加短暂延迟避免busy loop
        #ifdef _WIN32
            Sleep(100);
        #else
            usleep(100000);
        #endif
        return;  // 重新提示
    }

    if (choice == 1) {
        // 弃掉
        game::messages::ClientMessage req;
        req.set_seq(nextSeq_);
        auto* discardReq = req.mutable_discard_drawn_req();
        discardReq->set_player_id(state_.myPlayerId);
        discardReq->set_room_id(state_.roomId);
        discardReq->set_request_id(nextSeq_++);

        if (!network_.send(req)) {
            std::cout << ">>> Failed to send DiscardDrawnReq!" << std::endl;
            state_.hasDrawnCard = false;
            return;
        }

        std::cout << ">>> Discarding..." << std::endl;

        // 等待服务端确认
        game::messages::ServerMessage rsp;
        if (!network_.receive(rsp, 5000)) {
            std::cout << ">>> Timeout waiting for DiscardDrawnRsp!" << std::endl;
            state_.hasDrawnCard = false;
            return;
        }

        if (rsp.has_discard_drawn_rsp() && rsp.discard_drawn_rsp().error().code() == 0) {
            // 成功后清除状态
            state_.hasDrawnCard = false;

            // 如果有技能，处理技能输入
            if (state_.drawnCardSkill != SKILL_TYPE_NONE) {
                handleSkillInput(state_.drawnCardSkill);
            }
        } else {
            std::cout << ">>> Discard failed!" << std::endl;
            state_.hasDrawnCard = false;
        }

    } else if (choice == 2) {
        // 替换
        handleReplaceWithDrawn();
    }
}

void ClientApp::handleReplaceWithDrawn() {
    const int MAX_RETRIES = 5;
    int retryCount = 0;

    while (retryCount < MAX_RETRIES) {
        std::cout << ">>> Enter slot indices to replace (space-separated, e.g., '0 1'): ";
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');

        std::string line;
        std::getline(std::cin, line);

        if (std::cin.eof()) {
            running_ = false;
            return;
        }

        std::vector<int> slots = parseSlotIndices(line);

        if (slots.empty()) {
            retryCount++;
            std::cout << ">>> No valid slots entered! "
                      << (MAX_RETRIES - retryCount) << " attempts remaining." << std::endl;
            if (retryCount < MAX_RETRIES) {
                renderer_.render(state_);
                continue;
            } else {
                std::cout << ">>> Max retries reached. Canceling replace action." << std::endl;
                state_.hasDrawnCard = false;
                return;
            }
        }

        // 找到有效输入，继续处理
        game::messages::ClientMessage req;
        req.set_seq(nextSeq_);
        auto* replaceReq = req.mutable_replace_with_drawn_req();
        replaceReq->set_player_id(state_.myPlayerId);
        replaceReq->set_room_id(state_.roomId);
        replaceReq->set_request_id(nextSeq_++);

        for (int slot : slots) {
            replaceReq->add_slot_indices(slot);
        }

        if (network_.send(req)) {
            std::cout << ">>> Attempting to replace slots [";
            for (size_t i = 0; i < slots.size(); ++i) {
                std::cout << slots[i];
                if (i < slots.size() - 1) std::cout << ", ";
            }
            std::cout << "]..." << std::endl;

            // 等待服务端确认后再清除状态
            game::messages::ServerMessage rsp;
            if (network_.receive(rsp, 5000) && rsp.has_replace_with_drawn_rsp()) {
                if (rsp.replace_with_drawn_rsp().error().code() == 0) {
                    state_.hasDrawnCard = false;
                } else {
                    std::cout << ">>> Replace failed: "
                              << rsp.replace_with_drawn_rsp().error().message() << std::endl;
                    state_.hasDrawnCard = false;
                }
            }
        }
        return;
    }
}

void ClientApp::handleSkillInput(int skillType) {
    // 7-8: Peek Self
    // 9-10: Spy
    // 11-12: Swap
    if (skillType == 7 || skillType == 8) {
        handlePeekSelfSkill();
    } else if (skillType == 9 || skillType == 10) {
        handleSpySkill();
    } else if (skillType == 11 || skillType == 12) {
        handleSwapSkill();
    }
}

void ClientApp::handlePeekSelfSkill() {
    std::cout << "\n>>> Peek Self Skill: Choose your slot to peek (0-3): ";
    int slot;
    std::cin >> slot;

    if (std::cin.eof()) {
        running_ = false;
        return;
    }

    if (std::cin.fail() || slot < 0 || slot >= 4) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid slot! Must be 0-3." << std::endl;
        return;
    }

    // 构建UseSkillReq
    game::messages::ClientMessage req;
    req.set_seq(nextSeq_);
    auto* skillReq = req.mutable_use_skill_req();
    skillReq->set_player_id(state_.myPlayerId);
    skillReq->set_room_id(state_.roomId);
    skillReq->set_request_id(nextSeq_++);
    skillReq->set_card_id(0);  // 服务端会忽略此字段

    auto* params = skillReq->mutable_peek_self();
    params->set_slot_index(slot);

    if (!network_.send(req)) {
        std::cout << ">>> Failed to send UseSkillReq!" << std::endl;
        return;
    }

    std::cout << ">>> Using Peek Self skill on slot " << slot << "..." << std::endl;

    // 等待UseSkillRsp
    game::messages::ServerMessage rsp;
    if (!network_.receive(rsp, 5000)) {
        std::cout << ">>> Timeout waiting for UseSkillRsp!" << std::endl;
        return;
    }

    if (rsp.has_use_skill_rsp()) {
        if (rsp.use_skill_rsp().error().code() == 0) {
            std::cout << ">>> You peeked slot " << slot << ": ["
                      << rsp.use_skill_rsp().peeked_value() << "]" << std::endl;
        } else {
            std::cout << ">>> Skill failed: " << rsp.use_skill_rsp().error().message() << std::endl;
        }
    }
}

void ClientApp::handleSpySkill() {
    std::cout << "\n>>> Spy Skill: Choose opponent player ID: ";
    int targetPlayerId;
    std::cin >> targetPlayerId;

    if (std::cin.eof()) {
        running_ = false;
        return;
    }

    if (std::cin.fail()) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid player ID!" << std::endl;
        return;
    }

    std::cout << ">>> Choose slot to spy (0-3): ";
    int slot;
    std::cin >> slot;

    if (std::cin.eof()) {
        running_ = false;
        return;
    }

    if (std::cin.fail() || slot < 0 || slot >= 4) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid slot! Must be 0-3." << std::endl;
        return;
    }

    // 构建UseSkillReq
    game::messages::ClientMessage req;
    req.set_seq(nextSeq_);
    auto* skillReq = req.mutable_use_skill_req();
    skillReq->set_player_id(state_.myPlayerId);
    skillReq->set_room_id(state_.roomId);
    skillReq->set_request_id(nextSeq_++);
    skillReq->set_card_id(0);

    auto* params = skillReq->mutable_spy();
    params->set_target_player_id(targetPlayerId);
    params->set_target_slot_index(slot);

    if (!network_.send(req)) {
        std::cout << ">>> Failed to send UseSkillReq!" << std::endl;
        return;
    }

    std::cout << ">>> Using Spy skill on player " << targetPlayerId
              << " slot " << slot << "..." << std::endl;

    // 等待UseSkillRsp
    game::messages::ServerMessage rsp;
    if (!network_.receive(rsp, 5000)) {
        std::cout << ">>> Timeout waiting for UseSkillRsp!" << std::endl;
        return;
    }

    if (rsp.has_use_skill_rsp()) {
        if (rsp.use_skill_rsp().error().code() == 0) {
            std::cout << ">>> You spied player " << targetPlayerId
                      << " slot " << slot << ": ["
                      << rsp.use_skill_rsp().peeked_value() << "]" << std::endl;
        } else {
            std::cout << ">>> Skill failed: " << rsp.use_skill_rsp().error().message() << std::endl;
        }
    }
}

void ClientApp::handleSwapSkill() {
    std::cout << "\n>>> Swap Skill: Choose your slot (0-3): ";
    int mySlot;
    std::cin >> mySlot;

    if (std::cin.eof()) {
        running_ = false;
        return;
    }

    if (std::cin.fail() || mySlot < 0 || mySlot >= 4) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid slot! Must be 0-3." << std::endl;
        return;
    }

    std::cout << ">>> Choose opponent player ID: ";
    int targetPlayerId;
    std::cin >> targetPlayerId;

    if (std::cin.eof()) {
        running_ = false;
        return;
    }

    if (std::cin.fail()) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid player ID!" << std::endl;
        return;
    }

    std::cout << ">>> Choose opponent slot (0-3): ";
    int targetSlot;
    std::cin >> targetSlot;

    if (std::cin.eof()) {
        running_ = false;
        return;
    }

    if (std::cin.fail() || targetSlot < 0 || targetSlot >= 4) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid slot! Must be 0-3." << std::endl;
        return;
    }

    // 构建UseSkillReq
    game::messages::ClientMessage req;
    req.set_seq(nextSeq_);
    auto* skillReq = req.mutable_use_skill_req();
    skillReq->set_player_id(state_.myPlayerId);
    skillReq->set_room_id(state_.roomId);
    skillReq->set_request_id(nextSeq_++);
    skillReq->set_card_id(0);

    auto* params = skillReq->mutable_swap();
    params->set_own_slot_index(mySlot);
    params->set_target_player_id(targetPlayerId);
    params->set_target_slot_index(targetSlot);

    if (!network_.send(req)) {
        std::cout << ">>> Failed to send UseSkillReq!" << std::endl;
        return;
    }

    std::cout << ">>> Using Swap skill: your slot " << mySlot
              << " <-> player " << targetPlayerId << " slot " << targetSlot << "..." << std::endl;

    // 等待UseSkillRsp
    game::messages::ServerMessage rsp;
    if (!network_.receive(rsp, 5000)) {
        std::cout << ">>> Timeout waiting for UseSkillRsp!" << std::endl;
        return;
    }

    if (rsp.has_use_skill_rsp()) {
        if (rsp.use_skill_rsp().error().code() == 0) {
            std::cout << ">>> Swap successful!" << std::endl;
        } else {
            std::cout << ">>> Skill failed: " << rsp.use_skill_rsp().error().message() << std::endl;
        }
    }
}

void ClientApp::handleTakeFromDiscard() {
    const int MAX_RETRIES = 5;
    int retryCount = 0;

    while (retryCount < MAX_RETRIES) {
        std::cout << ">>> Taking top card [" << state_.discardTopValue
                  << "] from discard pile" << std::endl;
        std::cout << ">>> Enter slot indices to replace (space-separated, e.g., '0 1'): ";

        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::string line;
        std::getline(std::cin, line);

        if (std::cin.eof()) {
            running_ = false;
            return;
        }

        std::vector<int> slots = parseSlotIndices(line);

        if (slots.empty()) {
            retryCount++;
            std::cout << ">>> No valid slots entered! "
                      << (MAX_RETRIES - retryCount) << " attempts remaining." << std::endl;
            if (retryCount < MAX_RETRIES) {
                renderer_.render(state_);
                continue;
            } else {
                std::cout << ">>> Max retries reached. Canceling take action." << std::endl;
                return;
            }
        }

        // 找到有效输入，继续处理
        game::messages::ClientMessage req;
        req.set_seq(nextSeq_);
        auto* takeReq = req.mutable_take_from_discard_req();
        takeReq->set_player_id(state_.myPlayerId);
        takeReq->set_room_id(state_.roomId);
        takeReq->set_request_id(nextSeq_++);

        for (int slot : slots) {
            takeReq->add_slot_indices(slot);
        }

        if (!network_.send(req)) {
            std::cout << ">>> Failed to send TakeFromDiscardReq!" << std::endl;
            return;
        }

        std::cout << ">>> Attempting to replace slots [";
        for (size_t i = 0; i < slots.size(); ++i) {
            std::cout << slots[i];
            if (i < slots.size() - 1) std::cout << ", ";
        }
        std::cout << "]..." << std::endl;

        // 等待服务端响应
        game::messages::ServerMessage rsp;
        if (!network_.receive(rsp, 5000)) {
            std::cout << ">>> Timeout waiting for TakeFromDiscardRsp!" << std::endl;
            return;
        }

        if (rsp.has_take_from_discard_rsp()) {
            const auto& takeRsp = rsp.take_from_discard_rsp();
            if (takeRsp.error().code() == 0) {
                if (takeRsp.has_exchange_result()) {
                    const auto& result = takeRsp.exchange_result();
                    if (result.success()) {
                        std::cout << ">>> Replace successful!" << std::endl;
                    } else {
                        std::cout << ">>> Replace FAILED! Cards have different values." << std::endl;
                        std::cout << ">>> Card added to your hand." << std::endl;
                        if (result.drew_extra_penalty_card()) {
                            std::cout << ">>> Extra penalty card added (3+ cards attempted)." << std::endl;
                        }
                    }
                }
            } else {
                std::cout << ">>> Take from discard failed: " << takeRsp.error().message() << std::endl;
            }
        }
        return;
    }
}

void ClientApp::handleServerError(const game::messages::ServerMessage& msg) {
    // 检查各种响应中的error字段
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
    else if (msg.has_draw_card_rsp() && msg.draw_card_rsp().error().code() != 0) {
        std::cout << "ERROR: " << msg.draw_card_rsp().error().message() << std::endl;
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
}

bool ClientApp::getIntInput(int& out, int min, int max) {
    std::cin >> out;

    if (std::cin.fail()) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid input! Please enter a number." << std::endl;
        return false;
    }

    if (out < min || out > max) {
        std::cout << ">>> Input out of range! Please enter " << min << "-" << max << "." << std::endl;
        return false;
    }

    return true;
}

} // namespace cabo
