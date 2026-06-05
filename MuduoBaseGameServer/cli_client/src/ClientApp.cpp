#include "ClientApp.h"
#include <iostream>

namespace cabo {

void ClientApp::connectToServer() {
    std::string hostPort;
    std::cout << ">>> Enter server IP:port (e.g., 127.0.0.1:8888): ";
    std::cin >> hostPort;

    // 解析host和port
    size_t colonPos = hostPort.find(':');
    if (colonPos == std::string::npos) {
        std::cout << "ERROR: Invalid format! Use IP:port" << std::endl;
        return connectToServer();
    }

    std::string host = hostPort.substr(0, colonPos);
    int port = std::stoi(hostPort.substr(colonPos + 1));

    std::cout << ">>> Connecting to " << host << ":" << port << "..." << std::endl;

    if (!network_.connect(host, port)) {
        std::cout << "ERROR: Failed to connect!" << std::endl;
        std::cout << ">>> Retry? (y/n): ";
        char choice;
        std::cin >> choice;
        if (choice == 'y' || choice == 'Y') {
            return connectToServer();
        }
        running_ = false;
        return;
    }

    std::cout << ">>> Connected!" << std::endl;
}

void ClientApp::loginFlow() {
    std::string nickname;
    std::cout << ">>> Enter your nickname: ";
    std::cin >> nickname;

    if (nickname.empty() || nickname.length() > 20) {
        std::cout << "ERROR: Nickname must be 1-20 characters!" << std::endl;
        return loginFlow();
    }

    // 暂时只保存到state，稍后发送到服务端
    std::cout << ">>> Nickname set: " << nickname << std::endl;
}

void ClientApp::run() {
    std::cout << "================================================================================" << std::endl;
    std::cout << "                    Welcome to Cabo Game CLI Client" << std::endl;
    std::cout << "================================================================================" << std::endl;

    connectToServer();
    if (!running_) return;

    loginFlow();
    if (!running_) return;

    std::cout << "\n>>> Connection and login successful!" << std::endl;
    std::cout << ">>> Press Enter to continue..." << std::endl;
    std::cin.ignore();
    std::cin.get();
}

void ClientApp::roomFlow() {
    // TODO: 下一个task实现
}

void ClientApp::waitingRoomLoop() {
    // TODO: 稍后实现
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
