#pragma once
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

namespace game {

// [4-byte big-endian length][protobuf payload]
// Per-connection codec instance — each TcpConnection gets its own.
class MessageCodec {
public:
    using MessageCallback = std::function<void(const std::vector<uint8_t>& payload)>;

    MessageCodec() = default;

    // Feed raw bytes from TCP. Calls onMessage for each complete frame extracted.
    void feedBytes(const char* data, size_t len, const MessageCallback& onMessage);

    // Encode a protobuf payload into a framed message: [4-byte len][payload].
    static std::string encode(const std::string& payload);

    // Reset internal accumulation buffer (e.g., on connection close).
    void reset();

private:
    std::vector<uint8_t> buffer_;
};

} // namespace game
