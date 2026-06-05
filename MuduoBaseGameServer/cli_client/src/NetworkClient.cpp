#include "NetworkClient.h"
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <cstring>
#include <iostream>

namespace cabo {

NetworkClient::NetworkClient() : sockfd_(-1) {}

NetworkClient::~NetworkClient() {
    disconnect();
}

bool NetworkClient::connect(const std::string& host, int port) {
    // 创建socket
    sockfd_ = socket(AF_INET, SOCK_STREAM, 0);
    if (sockfd_ < 0) {
        std::cerr << "Failed to create socket" << std::endl;
        return false;
    }

    // 设置服务端地址
    struct sockaddr_in serverAddr;
    std::memset(&serverAddr, 0, sizeof(serverAddr));
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(static_cast<uint16_t>(port));

    if (inet_pton(AF_INET, host.c_str(), &serverAddr.sin_addr) <= 0) {
        std::cerr << "Invalid address: " << host << std::endl;
        close(sockfd_);
        sockfd_ = -1;
        return false;
    }

    // 连接
    if (::connect(sockfd_, (struct sockaddr*)&serverAddr, sizeof(serverAddr)) < 0) {
        std::cerr << "Connection failed" << std::endl;
        close(sockfd_);
        sockfd_ = -1;
        return false;
    }

    std::cout << "Connected to " << host << ":" << port << std::endl;
    return true;
}

void NetworkClient::disconnect() {
    if (sockfd_ >= 0) {
        close(sockfd_);
        sockfd_ = -1;
    }
}

bool NetworkClient::sendRaw(const void* data, size_t len) {
    if (sockfd_ < 0) return false;

    const char* ptr = static_cast<const char*>(data);
    size_t remaining = len;

    while (remaining > 0) {
        ssize_t sent = send(sockfd_, ptr, remaining, 0);
        if (sent <= 0) {
            if (sent < 0) {
                std::cerr << "Send failed" << std::endl;
            }
            return false;
        }
        ptr += sent;
        remaining -= sent;
    }

    return true;
}

int NetworkClient::recvRaw(void* buffer, size_t maxLen, int timeoutMs) {
    if (sockfd_ < 0) return -1;

    // TODO: 添加select超时控制
    ssize_t n = recv(sockfd_, buffer, maxLen, 0);
    return static_cast<int>(n);
}

} // namespace cabo
