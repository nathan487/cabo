#include "common/MessageDispatcher.h"
#include <google/protobuf/descriptor.h>

namespace game {

void MessageDispatcher::registerHandler(int fieldNumber, HandlerFunc handler) {
    handlers_[fieldNumber] = std::move(handler);
}

void MessageDispatcher::dispatch(const TcpConnectionPtr& conn,
                                  const std::vector<uint8_t>& payload) {
    ::game::messages::ClientMessage msg;
    if (!msg.ParseFromArray(payload.data(), static_cast<int>(payload.size()))) {
        // Failed to parse — skip
        return;
    }

    // Determine which oneof field is set
    const auto* desc = msg.GetDescriptor();
    const auto* ref = msg.GetReflection();
    const auto* oneofDesc = desc->FindOneofByName("payload");
    if (!oneofDesc) return;

    const auto* field = ref->GetOneofFieldDescriptor(msg, oneofDesc);
    if (!field) return;

    auto it = handlers_.find(field->number());
    if (it != handlers_.end()) {
        it->second(conn, msg);
    }
}

} // namespace game
