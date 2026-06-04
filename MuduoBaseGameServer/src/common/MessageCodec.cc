#include "common/MessageCodec.h"
#include <cstring>
#include <arpa/inet.h>

namespace game {

static int32_t readBigEndianInt32(const uint8_t* buf) {
    return (static_cast<int32_t>(buf[0]) << 24)
         | (static_cast<int32_t>(buf[1]) << 16)
         | (static_cast<int32_t>(buf[2]) << 8)
         |  static_cast<int32_t>(buf[3]);
}

static void writeBigEndianInt32(uint8_t* buf, int32_t value) {
    buf[0] = static_cast<uint8_t>((value >> 24) & 0xFF);
    buf[1] = static_cast<uint8_t>((value >> 16) & 0xFF);
    buf[2] = static_cast<uint8_t>((value >>  8) & 0xFF);
    buf[3] = static_cast<uint8_t>( value        & 0xFF);
}

void MessageCodec::feedBytes(const char* data, size_t len, const MessageCallback& onMessage) {
    // Append to internal buffer
    buffer_.insert(buffer_.end(),
                   reinterpret_cast<const uint8_t*>(data),
                   reinterpret_cast<const uint8_t*>(data) + len);

    // Extract complete frames
    while (buffer_.size() >= 4) {
        int32_t payloadLen = readBigEndianInt32(buffer_.data());
        if (payloadLen < 0 || payloadLen > 10 * 1024 * 1024) { // max 10MB
            // Corrupt frame — reset
            buffer_.clear();
            return;
        }
        size_t frameLen = 4 + static_cast<size_t>(payloadLen);
        if (buffer_.size() < frameLen) {
            break; // Incomplete frame — wait for more data
        }

        // Extract payload
        std::vector<uint8_t> payload(buffer_.begin() + 4, buffer_.begin() + frameLen);

        // Dispatch
        if (onMessage) {
            onMessage(payload);
        }

        // Remove processed frame
        buffer_.erase(buffer_.begin(), buffer_.begin() + frameLen);
    }
}

std::string MessageCodec::encode(const std::string& payload) {
    std::string result;
    int32_t len = static_cast<int32_t>(payload.size());
    uint8_t header[4];
    writeBigEndianInt32(header, len);
    result.append(reinterpret_cast<const char*>(header), 4);
    result.append(payload);
    return result;
}

void MessageCodec::reset() {
    buffer_.clear();
}

} // namespace game
