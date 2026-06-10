#pragma once

#include <cstdint>
#include <functional>
#include <string>
#include <vector>

namespace game {

class WebSocketCodec {
public:
    using MessageCallback = std::function<void(const std::vector<uint8_t>& payload)>;
    using SendCallback = std::function<void(const std::string& data)>;
    using CloseCallback = std::function<void()>;

    enum class State {
        Handshake,
        Frame,
        Closed
    };

    WebSocketCodec() = default;

    void feedBytes(const char* data, size_t len,
                   const MessageCallback& onMessage,
                   const SendCallback& onSend,
                   const CloseCallback& onClose);

    static std::string encode(const std::string& payload);
    static std::string buildCloseFrame(uint16_t code = 1000,
                                       const std::string& reason = "");

    void reset();

    State state() const { return state_; }

private:
    enum class FragmentState {
        None,
        Binary
    };

    State state_ = State::Handshake;
    FragmentState fragmentState_ = FragmentState::None;
    std::vector<uint8_t> buffer_;
    std::vector<uint8_t> fragmentedMessage_;

    bool tryCompleteHandshake(const SendCallback& onSend,
                              const CloseCallback& onClose);
    void decodeFrames(const MessageCallback& onMessage,
                      const SendCallback& onSend,
                      const CloseCallback& onClose);
    void protocolError(const SendCallback& onSend,
                       const CloseCallback& onClose,
                       uint16_t code = 1002);

    static std::string computeAcceptKey(const std::string& clientKey);
    static std::string buildPongFrame(const std::vector<uint8_t>& payload);
};

} // namespace game
