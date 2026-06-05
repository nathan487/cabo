#include "ClientApp.h"
#include <iostream>
#include <limits>

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
            std::cout << ">>> StartGameReq sent!" << std::endl;
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
    // TODO: 稍后实现
}

void ClientApp::handleGameInput() {
    // TODO: 稍后实现
}

std::vector<int> ClientApp::parseSlotIndices(const std::string& input) {
    // TODO: 稍后实现
    return {};
}

} // namespace cabo
