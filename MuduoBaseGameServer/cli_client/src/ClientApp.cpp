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

            // 等待JoinRoomRsp
            game::messages::ServerMessage rspMsg;
            if (!network_.receive(rspMsg, 5000)) {
                std::cout << "ERROR: Timeout waiting for JoinRoomRsp!" << std::endl;
                running_ = false;
                return;
            }

            state_.updateFromMessage(rspMsg);

            if (!rspMsg.has_join_room_rsp() || rspMsg.join_room_rsp().error().code() != 0) {
                std::cout << "ERROR: Failed to join room!" << std::endl;
                if (rspMsg.has_join_room_rsp()) {
                    std::cout << "       " << rspMsg.join_room_rsp().error().message() << std::endl;
                }
                running_ = false;
                return;
            }

            std::cout << ">>> Joined room successfully!" << std::endl;
            std::cout << ">>> Room ID: " << state_.roomId << std::endl;
            std::cout << ">>> Your Player ID: " << state_.myPlayerId << std::endl;
            break;  // 成功，退出循环

        } else {
            std::cout << "ERROR: Invalid choice!" << std::endl;
            std::cin.clear();
            std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
            continue;  // 重试
        }
    }

    if (!running_) return;

    // 自动发送ReadyReq
    std::cout << ">>> Sending ready signal..." << std::endl;
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

    // 等待ReadyRsp
    game::messages::ServerMessage readyRspMsg;
    if (!network_.receive(readyRspMsg, 5000)) {
        std::cout << "ERROR: Timeout waiting for ReadyRsp!" << std::endl;
        running_ = false;
        return;
    }

    if (!readyRspMsg.has_ready_rsp() || readyRspMsg.ready_rsp().error().code() != 0) {
        std::cout << "ERROR: Failed to ready up!" << std::endl;
        running_ = false;
        return;
    }

    std::cout << ">>> Ready!" << std::endl;
}

void ClientApp::waitingRoomLoop() {
    std::cout << "\n>>> Entering waiting room..." << std::endl;

    bool isHost = false;
    bool autoStartSent = false;
    size_t lastPlayerCount = 0;
    bool lastAllReady = false;
    auto startSentTime = std::chrono::steady_clock::time_point();
    const int START_TIMEOUT_MS = 10000;  // 10秒超时

    while (running_) {
        // 检查是否有新消息（100ms超时）
        if (network_.hasMessage(100)) {
            game::messages::ServerMessage msg;
            if (!network_.receive(msg, 1000)) {
                std::cerr << "ERROR: Failed to receive message, connection may be lost" << std::endl;
                running_ = false;
                return;
            }
            state_.updateFromMessage(msg);
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

        // 只在状态变化时渲染
        if (state_.players.size() != lastPlayerCount || allReady != lastAllReady) {
            renderer_.render(state_);
            lastPlayerCount = state_.players.size();
            lastAllReady = allReady;
        }

        // 如果是房主且所有人都ready，自动发送StartGameReq
        if (isHost && allReady && !autoStartSent) {
            std::cout << "\n>>> All players ready! Starting game in 1 second..." << std::endl;

            // 延迟1秒
            #ifdef _WIN32
                Sleep(1000);
            #else
                usleep(1000000);
            #endif

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

        // 检查phase是否已变为PLAYING
        if (state_.phase == GameState::PLAYING) {
            std::cout << "\n>>> Game starting! Transitioning to game loop..." << std::endl;
            break;
        }

        // 短暂休眠避免CPU占用过高
        #ifdef _WIN32
            Sleep(100);
        #else
            usleep(100000);
        #endif
    }
}

void ClientApp::gameLoop() {
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
        return;
    }

    if (std::cin.fail() || (choice != 1 && choice != 2)) {
        std::cin.clear();
        std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        std::cout << ">>> Invalid choice! Please enter 1 or 2." << std::endl;
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

        if (network_.send(req)) {
            std::cout << ">>> Discarding..." << std::endl;

            // 如果有技能，处理技能输入
            if (state_.drawnCardSkill != SKILL_TYPE_NONE) {
                handleSkillInput(state_.drawnCardSkill);
            }

            // 等待服务端确认后再清除状态
            state_.hasDrawnCard = false;
        }

    } else if (choice == 2) {
        // 替换
        handleReplaceWithDrawn();
    }
}

void ClientApp::handleReplaceWithDrawn() {
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
        std::cout << ">>> No valid slots entered! Please try again." << std::endl;
        renderer_.render(state_);
        return handleReplaceWithDrawn();  // 重试
    }

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
        state_.hasDrawnCard = false;
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
        std::cout << ">>> No valid slots entered! Please try again." << std::endl;
        renderer_.render(state_);
        return handleTakeFromDiscard();  // 重试
    }

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
}

} // namespace cabo
