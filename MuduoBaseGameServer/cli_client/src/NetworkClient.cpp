#include "NetworkClient.h"
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <cstring>
#include <iostream>
#include <sys/select.h>

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
        ssize_t sent = ::send(sockfd_, ptr, remaining, 0);
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

    // 超时控制
    if (timeoutMs > 0) {
        fd_set readfds;
        FD_ZERO(&readfds);
        FD_SET(sockfd_, &readfds);

        struct timeval tv;
        tv.tv_sec = timeoutMs / 1000;
        tv.tv_usec = (timeoutMs % 1000) * 1000;

        int ret = select(sockfd_ + 1, &readfds, nullptr, nullptr, &tv);
        if (ret <= 0) {
            return ret;  // 超时或错误
        }
    }

    ssize_t n = recv(sockfd_, buffer, maxLen, 0);
    return static_cast<int>(n);
}

// Step 2: 实现encodeFrame
std::string NetworkClient::encodeFrame(const std::string& payload) {
    uint32_t len = static_cast<uint32_t>(payload.size());
    std::string frame;
    frame.resize(4 + len);

    // 大端序写入长度
    frame[0] = static_cast<char>((len >> 24) & 0xFF);
    frame[1] = static_cast<char>((len >> 16) & 0xFF);
    frame[2] = static_cast<char>((len >> 8) & 0xFF);
    frame[3] = static_cast<char>(len & 0xFF);

    // 拷贝payload
    std::memcpy(&frame[4], payload.data(), len);
    return frame;
}

// Step 3: 实现decodeFrame
bool NetworkClient::decodeFrame(const std::vector<uint8_t>& buffer,
                                 size_t& frameLen,
                                 std::vector<uint8_t>& payload) {
    if (buffer.size() < 4) return false;

    // 大端序读取长度
    uint32_t len = (static_cast<uint32_t>(buffer[0]) << 24)
                 | (static_cast<uint32_t>(buffer[1]) << 16)
                 | (static_cast<uint32_t>(buffer[2]) << 8)
                 | static_cast<uint32_t>(buffer[3]);

    if (len > 10 * 1024 * 1024) {  // max 10MB
        return false;  // 非法帧
    }

    frameLen = 4 + len;
    if (buffer.size() < frameLen) return false;  // 半包

    // 提取payload
    payload.assign(buffer.begin() + 4, buffer.begin() + frameLen);
    return true;
}

// Step 4: 实现send方法
bool NetworkClient::send(const game::messages::ClientMessage& msg) {
    if (sockfd_ < 0) return false;

    // 设置seq（先不递增，发送成功后再递增）
    game::messages::ClientMessage msgWithSeq = msg;
    msgWithSeq.set_seq(clientSeq_);

    // 序列化
    std::string payload;
    if (!msgWithSeq.SerializeToString(&payload)) {
        std::cerr << "Failed to serialize ClientMessage" << std::endl;
        return false;
    }

    // 编码为帧
    std::string frame = encodeFrame(payload);

    // 发送
    if (!sendRaw(frame.data(), frame.size())) {
        return false;
    }

    // 发送成功后才递增序列号
    clientSeq_++;
    return true;
}

// Step 5: 实现hasMessage方法
bool NetworkClient::hasMessage(int timeoutMs) {
    if (sockfd_ < 0) return false;

    // CRITICAL FIX: Check recvBuffer_ first before checking socket
    // Multiple messages can arrive in one recv() call and sit in buffer
    if (recvBuffer_.size() >= 4) {
        // Check if we have at least a complete frame header
        uint32_t len = (static_cast<uint32_t>(recvBuffer_[0]) << 24)
                     | (static_cast<uint32_t>(recvBuffer_[1]) << 16)
                     | (static_cast<uint32_t>(recvBuffer_[2]) << 8)
                     | static_cast<uint32_t>(recvBuffer_[3]);

        // Validate frame length to avoid false positives from corrupted data
        if (len > 0 && len <= 10 * 1024 * 1024) {
            size_t frameLen = 4 + len;
            if (recvBuffer_.size() >= frameLen) {
                // We have a complete message in buffer
                return true;
            }
        }
    }

    // No complete message in buffer, check if socket has data
    fd_set readfds;
    FD_ZERO(&readfds);
    FD_SET(sockfd_, &readfds);

    struct timeval tv;
    tv.tv_sec = timeoutMs / 1000;
    tv.tv_usec = (timeoutMs % 1000) * 1000;

    int ret = select(sockfd_ + 1, &readfds, nullptr, nullptr, &tv);
    return ret > 0;
}

// Step 6: 实现extractOneMessage方法
bool NetworkClient::extractOneMessage(game::messages::ServerMessage& outMsg) {
    // BUG-3 Fix: Pre-check frame length header to distinguish "need more data"
    // from "corrupted data". Without this, a corrupted oversized length header
    // permanently blocks the buffer since decodeFrame refuses to process it.
    if (recvBuffer_.size() >= 4) {
        uint32_t len = (static_cast<uint32_t>(recvBuffer_[0]) << 24)
                     | (static_cast<uint32_t>(recvBuffer_[1]) << 16)
                     | (static_cast<uint32_t>(recvBuffer_[2]) << 8)
                     | static_cast<uint32_t>(recvBuffer_[3]);

        if (len > 10 * 1024 * 1024) {
            // Corrupted frame header — clear buffer to allow recovery
            std::cerr << "NetworkClient: Corrupted frame header (len=" << len
                      << "), clearing buffer to recover" << std::endl;
            recvBuffer_.clear();
            return false;
        }
    }

    size_t frameLen;
    std::vector<uint8_t> payload;

    if (!decodeFrame(recvBuffer_, frameLen, payload)) {
        return false;  // 没有完整帧（半包等待更多数据）
    }

    // 解析protobuf
    if (!outMsg.ParseFromArray(payload.data(), static_cast<int>(payload.size()))) {
        std::cerr << "Failed to parse ServerMessage" << std::endl;
        // 只移除当前错误的帧，保留后续数据
        recvBuffer_.erase(recvBuffer_.begin(), recvBuffer_.begin() + frameLen);
        return false;
    }

    // 移除已处理的帧
    recvBuffer_.erase(recvBuffer_.begin(), recvBuffer_.begin() + frameLen);
    return true;
}

// Step 7: 实现receive方法
bool NetworkClient::receive(game::messages::ServerMessage& outMsg, int timeoutMs) {
    // 先检查缓冲区是否有完整消息
    if (extractOneMessage(outMsg)) {
        return true;
    }

    // 从socket读取更多数据
    uint8_t temp[4096];
    int n = recvRaw(temp, sizeof(temp), timeoutMs);
    if (n <= 0) return false;

    recvBuffer_.insert(recvBuffer_.end(), temp, temp + n);

    // 再次尝试提取消息
    return extractOneMessage(outMsg);
}

} // namespace cabo
