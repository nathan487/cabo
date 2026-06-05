#pragma once

#include <string>
#include <vector>
#include <cstdint>

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

private:
    int sockfd_ = -1;
};

} // namespace cabo
