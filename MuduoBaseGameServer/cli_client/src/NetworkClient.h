#pragma once

#include <string>
#include <vector>
#include <cstdint>

namespace cabo {

class NetworkClient {
public:
    NetworkClient();
    ~NetworkClient();

    bool connect(const std::string& host, int port);
    void disconnect();
    bool isConnected() const { return sockfd_ >= 0; }

    bool sendRaw(const void* data, size_t len);
    int recvRaw(void* buffer, size_t maxLen, int timeoutMs);

private:
    int sockfd_ = -1;
};

} // namespace cabo
