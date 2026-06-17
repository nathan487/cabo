#include "common/WebSocketCodec.h"

#include <algorithm>
#include <cctype>
#include <cstring>
#include <map>
#include <openssl/sha.h>
#include <sstream>

namespace game {
namespace {

const size_t kMaxPayloadSize = 10 * 1024 * 1024;
const char* kWebSocketMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

std::string toLower(std::string value) {
    std::transform(value.begin(), value.end(), value.begin(),
                   [](unsigned char ch) { return static_cast<char>(std::tolower(ch)); });
    return value;
}

std::string trim(const std::string& value) {
    size_t first = 0;
    while (first < value.size() && std::isspace(static_cast<unsigned char>(value[first])))
        ++first;
    size_t last = value.size();
    while (last > first && std::isspace(static_cast<unsigned char>(value[last - 1])))
        --last;
    return value.substr(first, last - first);
}

bool tokenListContains(const std::string& value, const std::string& expected) {
    std::string lowerExpected = toLower(expected);
    size_t start = 0;
    while (start <= value.size()) {
        size_t comma = value.find(',', start);
        std::string token = trim(value.substr(start, comma == std::string::npos
                                                     ? std::string::npos
                                                     : comma - start));
        if (toLower(token) == lowerExpected)
            return true;
        if (comma == std::string::npos)
            break;
        start = comma + 1;
    }
    return false;
}

bool isValidBase64Key(const std::string& key) {
    if (key.empty())
        return false;
    for (char ch : key) {
        bool ok = std::isalnum(static_cast<unsigned char>(ch))
               || ch == '+'
               || ch == '/'
               || ch == '=';
        if (!ok)
            return false;
    }
    return true;
}

std::string base64Encode(const unsigned char* data, size_t len) {
    static const char* chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string out;
    out.reserve(((len + 2) / 3) * 4);
    for (size_t i = 0; i < len; i += 3) {
        unsigned int n = static_cast<unsigned int>(data[i]) << 16;
        if (i + 1 < len)
            n |= static_cast<unsigned int>(data[i + 1]) << 8;
        if (i + 2 < len)
            n |= static_cast<unsigned int>(data[i + 2]);

        out.push_back(chars[(n >> 18) & 0x3F]);
        out.push_back(chars[(n >> 12) & 0x3F]);
        out.push_back(i + 1 < len ? chars[(n >> 6) & 0x3F] : '=');
        out.push_back(i + 2 < len ? chars[n & 0x3F] : '=');
    }
    return out;
}

void appendUint16(std::string& out, uint16_t value) {
    out.push_back(static_cast<char>((value >> 8) & 0xFF));
    out.push_back(static_cast<char>(value & 0xFF));
}

void appendPayloadLength(std::string& frame, uint64_t len) {
    if (len < 126) {
        frame.push_back(static_cast<char>(len));
    } else if (len <= 65535) {
        frame.push_back(static_cast<char>(126));
        appendUint16(frame, static_cast<uint16_t>(len));
    } else {
        frame.push_back(static_cast<char>(127));
        for (int i = 7; i >= 0; --i)
            frame.push_back(static_cast<char>((len >> (i * 8)) & 0xFF));
    }
}

std::map<std::string, std::string> parseHeaders(const std::string& request) {
    std::map<std::string, std::string> headers;
    size_t lineStart = 0;
    bool firstLine = true;
    while (lineStart <= request.size()) {
        size_t lineEnd = request.find("\r\n", lineStart);
        std::string line = request.substr(lineStart, lineEnd == std::string::npos
                                                     ? std::string::npos
                                                     : lineEnd - lineStart);
        if (!firstLine) {
            size_t colon = line.find(':');
            if (colon != std::string::npos) {
                headers[toLower(trim(line.substr(0, colon)))] = trim(line.substr(colon + 1));
            }
        }
        firstLine = false;
        if (lineEnd == std::string::npos)
            break;
        lineStart = lineEnd + 2;
    }
    return headers;
}

} // namespace

void WebSocketCodec::feedBytes(const char* data, size_t len,
                               const MessageCallback& onMessage,
                               const SendCallback& onSend,
                               const CloseCallback& onClose) {
    if (state_ == State::Closed)
        return;

    buffer_.insert(buffer_.end(),
                   reinterpret_cast<const uint8_t*>(data),
                   reinterpret_cast<const uint8_t*>(data) + len);

    if (state_ == State::Handshake) {
        if (!tryCompleteHandshake(onSend, onClose))
            return;
    }

    if (state_ == State::Frame)
        decodeFrames(onMessage, onSend, onClose);
}

bool WebSocketCodec::tryCompleteHandshake(const SendCallback& onSend,
                                          const CloseCallback& onClose) {
    const uint8_t delim[] = {'\r', '\n', '\r', '\n'};
    auto it = std::search(buffer_.begin(), buffer_.end(), delim, delim + 4);
    if (it == buffer_.end())
        return false;

    std::string request(buffer_.begin(), it);
    size_t consumed = static_cast<size_t>(it - buffer_.begin()) + 4;
    bool valid = false;

    size_t firstLineEnd = request.find("\r\n");
    std::string firstLine = firstLineEnd == std::string::npos
                          ? request
                          : request.substr(0, firstLineEnd);
    auto headers = parseHeaders(request);
    auto upgradeIt = headers.find("upgrade");
    auto connectionIt = headers.find("connection");
    auto versionIt = headers.find("sec-websocket-version");
    auto keyIt = headers.find("sec-websocket-key");

    if (firstLine.find("GET ") == 0
        && upgradeIt != headers.end()
        && toLower(upgradeIt->second) == "websocket"
        && connectionIt != headers.end()
        && tokenListContains(connectionIt->second, "upgrade")
        && versionIt != headers.end()
        && trim(versionIt->second) == "13"
        && keyIt != headers.end()
        && isValidBase64Key(keyIt->second)) {
        valid = true;
    }

    if (!valid) {
        if (onSend)
            onSend("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
        buffer_.clear();
        state_ = State::Closed;
        if (onClose)
            onClose();
        return false;
    }

    std::ostringstream response;
    response << "HTTP/1.1 101 Switching Protocols\r\n"
             << "Upgrade: websocket\r\n"
             << "Connection: Upgrade\r\n"
             << "Sec-WebSocket-Accept: " << computeAcceptKey(keyIt->second) << "\r\n"
             << "\r\n";

    if (onSend)
        onSend(response.str());

    buffer_.erase(buffer_.begin(), buffer_.begin() + static_cast<long>(consumed));
    state_ = State::Frame;
    return true;
}

void WebSocketCodec::decodeFrames(const MessageCallback& onMessage,
                                  const SendCallback& onSend,
                                  const CloseCallback& onClose) {
    while (state_ == State::Frame && buffer_.size() >= 2) {
        size_t pos = 0;
        uint8_t byte0 = buffer_[pos++];
        bool fin = (byte0 & 0x80) != 0;
        bool rsvSet = (byte0 & 0x70) != 0;
        uint8_t opcode = byte0 & 0x0F;

        uint8_t byte1 = buffer_[pos++];
        bool masked = (byte1 & 0x80) != 0;
        uint64_t payloadLen = byte1 & 0x7F;

        if (payloadLen == 126) {
            if (buffer_.size() < pos + 2)
                return;
            payloadLen = (static_cast<uint64_t>(buffer_[pos]) << 8)
                       | static_cast<uint64_t>(buffer_[pos + 1]);
            pos += 2;
        } else if (payloadLen == 127) {
            if (buffer_.size() < pos + 8)
                return;
            payloadLen = 0;
            for (int i = 0; i < 8; ++i)
                payloadLen = (payloadLen << 8) | static_cast<uint64_t>(buffer_[pos + i]);
            pos += 8;
            if ((payloadLen & (1ULL << 63)) != 0) {
                protocolError(onSend, onClose);
                return;
            }
        }

        if (!masked || rsvSet || payloadLen > kMaxPayloadSize) {
            protocolError(onSend, onClose);
            return;
        }

        bool isControl = (opcode & 0x08) != 0;
        if (isControl && (!fin || payloadLen > 125)) {
            protocolError(onSend, onClose);
            return;
        }

        if (buffer_.size() < pos + 4)
            return;
        uint8_t maskKey[4] = {
            buffer_[pos], buffer_[pos + 1], buffer_[pos + 2], buffer_[pos + 3]
        };
        pos += 4;

        if (buffer_.size() < pos + payloadLen)
            return;

        std::vector<uint8_t> payload(static_cast<size_t>(payloadLen));
        for (uint64_t i = 0; i < payloadLen; ++i)
            payload[static_cast<size_t>(i)] = buffer_[pos + static_cast<size_t>(i)] ^ maskKey[i % 4];

        buffer_.erase(buffer_.begin(),
                      buffer_.begin() + static_cast<long>(pos + payloadLen));

        switch (opcode) {
        case 0x0: // continuation
            if (fragmentState_ == FragmentState::None) {
                protocolError(onSend, onClose);
                return;
            }
            if (fragmentedMessage_.size() + payload.size() > kMaxPayloadSize) {
                protocolError(onSend, onClose);
                return;
            }
            fragmentedMessage_.insert(fragmentedMessage_.end(), payload.begin(), payload.end());
            if (fin) {
                if (onMessage)
                    onMessage(fragmentedMessage_);
                fragmentedMessage_.clear();
                fragmentState_ = FragmentState::None;
            }
            break;
        case 0x2: // binary
            if (fragmentState_ != FragmentState::None) {
                protocolError(onSend, onClose);
                return;
            }
            if (fin) {
                if (onMessage)
                    onMessage(payload);
            } else {
                fragmentState_ = FragmentState::Binary;
                fragmentedMessage_ = payload;
            }
            break;
        case 0x8: // close
            if (onSend)
                onSend(buildCloseFrame());
            state_ = State::Closed;
            if (onClose)
                onClose();
            return;
        case 0x9: // ping
            if (onSend)
                onSend(buildPongFrame(payload));
            break;
        case 0xA: // pong
            break;
        default:
            protocolError(onSend, onClose);
            return;
        }
    }
}

std::string WebSocketCodec::encode(const std::string& payload) {
    std::string frame;
    encode(payload, &frame);
    return frame;
}

void WebSocketCodec::encode(const std::string& payload, std::string* frame) {
    if (!frame) return;
    frame->clear();
    frame->reserve(10 + payload.size());
    frame->push_back(static_cast<char>(0x82));
    appendPayloadLength(*frame, payload.size());
    frame->append(payload);
}

std::string WebSocketCodec::buildCloseFrame(uint16_t code, const std::string& reason) {
    std::string payload;
    appendUint16(payload, code);
    payload.append(reason);

    std::string frame;
    frame.reserve(2 + payload.size());
    frame.push_back(static_cast<char>(0x88));
    appendPayloadLength(frame, payload.size());
    frame.append(payload);
    return frame;
}

std::string WebSocketCodec::buildPongFrame(const std::vector<uint8_t>& payload) {
    std::string frame;
    frame.reserve(2 + payload.size());
    frame.push_back(static_cast<char>(0x8A));
    frame.push_back(static_cast<char>(payload.size()));
    frame.append(reinterpret_cast<const char*>(payload.data()), payload.size());
    return frame;
}

void WebSocketCodec::protocolError(const SendCallback& onSend,
                                   const CloseCallback& onClose,
                                   uint16_t code) {
    if (onSend)
        onSend(buildCloseFrame(code));
    buffer_.clear();
    fragmentedMessage_.clear();
    fragmentState_ = FragmentState::None;
    state_ = State::Closed;
    if (onClose)
        onClose();
}

std::string WebSocketCodec::computeAcceptKey(const std::string& clientKey) {
    std::string combined = clientKey + kWebSocketMagic;
    unsigned char hash[SHA_DIGEST_LENGTH];
    SHA1(reinterpret_cast<const unsigned char*>(combined.data()),
         combined.size(),
         hash);
    return base64Encode(hash, SHA_DIGEST_LENGTH);
}

void WebSocketCodec::reset() {
    buffer_.clear();
    fragmentedMessage_.clear();
    fragmentState_ = FragmentState::None;
    state_ = State::Handshake;
}

} // namespace game
