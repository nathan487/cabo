#pragma once

#include <string>
#include <vector>
#include <cstdint>
#include "messages.pb.h"

namespace cabo {

class NetworkClient {
public:
    NetworkClient();
    ~NetworkClient();

    // 禁止拷贝（socket资源不可共享）
    NetworkClient(const NetworkClient&) = delete;
    NetworkClient& operator=(const NetworkClient&) = delete;

    bool connect(const std::string& host, int port);
    void disconnect();
    bool isConnected() const { return sockfd_ >= 0; }

    bool sendRaw(const void* data, size_t len);
    int recvRaw(void* buffer, size_t maxLen, int timeoutMs);

    // Protobuf接口
    bool send(const game::messages::ClientMessage& msg);
    bool hasMessage(int timeoutMs = 0);
    bool receive(game::messages::ServerMessage& outMsg, int timeoutMs = 1000);

    // 获取下一个请求ID（用于request_id字段）
    int64_t nextRequestId() const { return clientSeq_; }

private:
    int sockfd_ = -1;
    std::vector<uint8_t> recvBuffer_;
    int64_t clientSeq_ = 1;

    static std::string encodeFrame(const std::string& payload);
    static bool decodeFrame(const std::vector<uint8_t>& buffer,
                           size_t& frameLen,
                           std::vector<uint8_t>& payload);
    bool extractOneMessage(game::messages::ServerMessage& outMsg);
};

} // namespace cabo
