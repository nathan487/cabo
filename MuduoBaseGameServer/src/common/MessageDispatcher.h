#pragma once
#include "proto/messages.pb.h"
#include <functional>
#include <memory>
#include <unordered_map>
#include <vector>

class TcpConnection;

namespace game {

using TcpConnectionPtr = std::shared_ptr<TcpConnection>;

// Routes ClientMessage oneof payload to registered handlers by field number.
class MessageDispatcher {
public:
    using HandlerFunc = std::function<void(
        const TcpConnectionPtr& conn,
        const ::game::messages::ClientMessage& msg)>;

    MessageDispatcher() = default;

    // Register a handler for a specific oneof field number
    // (field numbers from messages.proto ClientMessage payload).
    void registerHandler(int fieldNumber, HandlerFunc handler);

    // Deserialize payload (without 4-byte length prefix) and dispatch.
    void dispatch(const TcpConnectionPtr& conn,
                  const std::vector<uint8_t>& payload);

private:
    std::unordered_map<int, HandlerFunc> handlers_;
};

} // namespace game
