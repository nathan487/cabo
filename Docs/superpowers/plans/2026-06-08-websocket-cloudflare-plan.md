# WebSocket + Cloudflare Tunnel 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

## 2026-06-09 Plan Review Notes: Do Not Execute Blindly

This plan is a starting point, not a verified implementation script. Before coding, the next agent must inspect the current server/client code and correct the plan where it conflicts with the real codebase.

Known risks and required corrections:

- **HTTP/WebSocket handshake parsing is too idealized in the sample code.**
  - Header names must be parsed case-insensitively.
  - `Connection` may contain comma-separated tokens such as `keep-alive, Upgrade`.
  - `Upgrade: websocket`, `Sec-WebSocket-Version: 13`, and `Sec-WebSocket-Key` should be validated.
  - Cloudflare may add extra headers; the parser must tolerate unknown headers.
- **Client-to-server masking must be enforced.**
  - RFC 6455 requires clients to mask frames.
  - The sample decoder currently tolerates unmasked frames because the mask key defaults to zero. That should be fixed: unmasked client data frames should close with a protocol error.
- **WebSocket fragmentation is under-specified.**
  - The current test plan covers TCP partial reads, not WebSocket continuation frames.
  - Either implement continuation opcode `0x0` for fragmented binary messages or explicitly reject fragmented messages with a protocol error after verifying `ClientWebSocket` and Cloudflare never fragment the expected payloads.
  - Prefer implementing minimal binary continuation support; protobuf messages may grow and proxies are allowed to fragment.
- **Control-frame rules need stricter handling.**
  - Ping/pong/close payload length must be `<= 125`.
  - Control frames must not be fragmented.
  - Close frames should include an RFC status code when possible, especially `1002` for protocol errors.
- **OpenSSL/CMake details are environment-sensitive.**
  - Do not blindly add `ssl crypto` to `target_link_libraries`.
  - First inspect the current `MuduoBaseGameServer/CMakeLists.txt`.
  - Prefer `find_package(OpenSSL REQUIRED)` and `OpenSSL::SSL` / `OpenSSL::Crypto` if the project/toolchain supports it.
  - If OpenSSL is not available in the target environment, consider a small self-contained SHA1 + Base64 implementation or an existing project dependency.
- **Unity threading must be handled carefully.**
  - `ClientWebSocket.ReceiveAsync` runs off the Unity main thread.
  - Only enqueue decoded messages or raw payloads from the background receive loop.
  - Do not touch Unity UI, `GameFlow` rendering, or Unity objects from the receive thread.
  - Preserve the existing drain-then-render behavior.
- **Do not redefine an existing enum without checking current code.**
  - The sample `WebSocketNetworkClient` includes `NetworkClientState`; the project already has network state types. Reuse or rename carefully to avoid duplicate-type compile errors.
- **Do not globally break the legacy TCP codec until WebSocket is verified.**
  - The sample suggests simplifying `MessageCodec.Encode` to pure protobuf. That may break any remaining TCP tests/tools.
  - Prefer a clear split:
    - protobuf serializer/deserializer for message bytes.
    - TCP length-prefix codec kept for legacy/reference paths.
    - WebSocket transport sends/receives pure protobuf payloads.
- **Cloudflare tunnel command must be validated end to end.**
  - `cloudflared tunnel --url http://localhost:8888` should forward HTTP Upgrade requests to the custom WebSocket server, but this must be verified with a real `ClientWebSocket` or `websocat`.
  - The Unity client should accept both `https://...trycloudflare.com` and `wss://...trycloudflare.com`, normalizing `https` to `wss` only for connection.
- **Server lifetime remains user-owned.**
  - Do not start long-running GameServer/cloudflared processes unless explicitly requested by the user.

Additional tests to add before integration is considered complete:

- Handshake with lower/mixed-case headers.
- Handshake with `Connection: keep-alive, Upgrade`.
- Invalid version/key/missing upgrade returns a clean failure.
- Unmasked client binary frame is rejected.
- Fragmented binary message is either correctly reassembled or explicitly rejected with `1002`.
- Ping with payload receives Pong with the same payload.
- Close frame returns Close and cleans up the connection codec map.
- Unity WebSocket receive loop can handle fragmented `ClientWebSocket` receives by accumulating until `EndOfMessage`.
- Local `ws://127.0.0.1:8888` works before attempting Cloudflare.
- Public `wss://...trycloudflare.com` works with at least two clients in the same room.

**Goal:** 将 Cabo 游戏从 raw TCP 自定义帧升级为 WebSocket 协议，并通过 Cloudflare Tunnel 实现公网访问。

**Architecture:** 在 muduo 网络库之上新增 WebSocketCodec（握手+帧编解码），Unity 端用 ClientWebSocket 替换 TcpClient，protobuf 消息层完全不动。

**Tech Stack:** C++11 + muduo + protobuf, Unity C# (.NET 4.x), cloudflared

---

### Task 1: 创建 C++ WebSocketCodec 头文件

**Files:**
- Create: `MuduoBaseGameServer/src/common/WebSocketCodec.h`

- [ ] **Step 1: 编写 WebSocketCodec.h**

```cpp
#pragma once
#include <cstdint>
#include <functional>
#include <string>
#include <vector>

namespace game {

// Per-connection WebSocket codec: HTTP upgrade handshake → binary frame codec.
// Replaces MessageCodec's [4B len] framing with RFC 6455 WebSocket framing.
class WebSocketCodec {
public:
    // Called when a complete binary message payload (protobuf bytes) is decoded.
    using MessageCallback = std::function<void(const std::vector<uint8_t>& payload)>;

    // Called when the codec needs to send data back to the client
    // (handshake 101 response, Pong, Close frame).
    using SendCallback = std::function<void(const std::string& data)>;

    enum class State {
        Handshake,  // Waiting for HTTP upgrade request
        Frame       // WebSocket framed messages
    };

    WebSocketCodec() = default;

    // Feed raw TCP bytes.
    // - onMessage: called with each complete binary message payload (protobuf bytes).
    // - onSend: called when the codec needs to send data back (101 response / Pong / Close).
    void feedBytes(const char* data, size_t len,
                   const MessageCallback& onMessage,
                   const SendCallback& onSend);

    // Encode a protobuf payload into a WebSocket binary frame (server→client, no mask).
    // Returns [0x82][variable-length payload size][payload bytes].
    // This is a STATIC method — server-side encoding has no per-connection state.
    static std::string encode(const std::string& payload);

    // Reset state (on connection close).
    void reset();

    State state() const { return state_; }

private:
    State state_ = State::Handshake;
    std::vector<uint8_t> buffer_;

    // Handshake
    bool tryCompleteHandshake(const SendCallback& onSend);
    static std::string computeAcceptKey(const std::string& clientKey);

    // Frame decode
    void decodeFrames(const MessageCallback& onMessage,
                      const SendCallback& onSend);
    static std::string buildPongFrame(const std::vector<uint8_t>& pingPayload);
    static std::string buildCloseFrame();
};

} // namespace game
```

- [ ] **Step 2: 提交**

```bash
git add MuduoBaseGameServer/src/common/WebSocketCodec.h
git commit -m "feat: add WebSocketCodec header"
```

---

### Task 2: 实现 WebSocketCodec（握手、编码、帧解码、控制帧）

**Files:**
- Create: `MuduoBaseGameServer/src/common/WebSocketCodec.cc`
- Modify: `MuduoBaseGameServer/CMakeLists.txt`

- [ ] **Step 1: 编写完整 WebSocketCodec.cc**

```cpp
#include "common/WebSocketCodec.h"
#include <cstring>
#include <sstream>
#include <algorithm>
#include <openssl/sha.h>

namespace game {

// ── Base64 ──────────────────────────────────────────────────────

static std::string base64Encode(const unsigned char* data, size_t len) {
    static const char* chars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string result;
    result.reserve(((len + 2) / 3) * 4);
    for (size_t i = 0; i < len; i += 3) {
        unsigned int n = static_cast<unsigned int>(data[i]) << 16;
        if (i + 1 < len) n |= static_cast<unsigned int>(data[i + 1]) << 8;
        if (i + 2 < len) n |= static_cast<unsigned int>(data[i + 2]);
        result += chars[(n >> 18) & 0x3F];
        result += chars[(n >> 12) & 0x3F];
        result += (i + 1 < len) ? chars[(n >> 6) & 0x3F] : '=';
        result += (i + 2 < len) ? chars[n & 0x3F] : '=';
    }
    return result;
}

// ── SHA1 + Accept Key ───────────────────────────────────────────

std::string WebSocketCodec::computeAcceptKey(const std::string& clientKey) {
    static const char* MAGIC = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    std::string combined = clientKey + MAGIC;
    unsigned char hash[SHA_DIGEST_LENGTH];
    SHA1(reinterpret_cast<const unsigned char*>(combined.data()),
         combined.size(), hash);
    return base64Encode(hash, SHA_DIGEST_LENGTH);
}

// ── feedBytes ───────────────────────────────────────────────────

void WebSocketCodec::feedBytes(const char* data, size_t len,
                               const MessageCallback& onMessage,
                               const SendCallback& onSend) {
    buffer_.insert(buffer_.end(),
                   reinterpret_cast<const uint8_t*>(data),
                   reinterpret_cast<const uint8_t*>(data) + len);

    if (state_ == State::Handshake) {
        if (!tryCompleteHandshake(onSend))
            return; // Incomplete headers — wait for more data
        // Handshake done; remaining bytes in buffer_ are frame data
    }

    decodeFrames(onMessage, onSend);
}

// ── Handshake ───────────────────────────────────────────────────

bool WebSocketCodec::tryCompleteHandshake(const SendCallback& onSend) {
    // Find \r\n\r\n
    const uint8_t delim[] = "\r\n\r\n";
    auto it = std::search(buffer_.begin(), buffer_.end(), delim, delim + 4);
    if (it == buffer_.end())
        return false;

    std::string headers(buffer_.begin(), it);
    size_t consumed = static_cast<size_t>(it - buffer_.begin()) + 4;

    // Extract Sec-WebSocket-Key
    const char* keyTag = "Sec-WebSocket-Key: ";
    auto keyPos = headers.find(keyTag);
    if (keyPos == std::string::npos) {
        // Not WebSocket — send 400
        onSend("HTTP/1.1 400 Bad Request\r\n\r\n");
        buffer_.clear();
        return false;
    }
    keyPos += strlen(keyTag);
    auto keyEnd = headers.find('\r', keyPos);
    std::string clientKey = headers.substr(keyPos, keyEnd - keyPos);

    // Build 101 Switching Protocols
    std::ostringstream rsp;
    rsp << "HTTP/1.1 101 Switching Protocols\r\n"
        << "Upgrade: websocket\r\n"
        << "Connection: Upgrade\r\n"
        << "Sec-WebSocket-Accept: " << computeAcceptKey(clientKey) << "\r\n"
        << "\r\n";

    onSend(rsp.str());

    buffer_.erase(buffer_.begin(), buffer_.begin() + static_cast<long>(consumed));
    state_ = State::Frame;
    return true;
}

// ── Static encode (server→client, no mask) ──────────────────────

std::string WebSocketCodec::encode(const std::string& payload) {
    std::string frame;
    frame.reserve(10 + payload.size());

    // Byte 0: FIN(0x80) | Binary opcode(0x2) = 0x82
    frame.push_back(static_cast<char>(0x82));

    // Byte 1+: payload length (no mask bit → server→client)
    uint64_t len = payload.size();
    if (len < 126) {
        frame.push_back(static_cast<char>(len));
    } else if (len <= 65535) {
        frame.push_back(static_cast<char>(126));
        frame.push_back(static_cast<char>((len >> 8) & 0xFF));
        frame.push_back(static_cast<char>(len & 0xFF));
    } else {
        frame.push_back(static_cast<char>(127));
        for (int i = 7; i >= 0; --i)
            frame.push_back(static_cast<char>((len >> (i * 8)) & 0xFF));
    }

    frame.append(payload);
    return frame;
}

// ── Frame decode ────────────────────────────────────────────────

void WebSocketCodec::decodeFrames(const MessageCallback& onMessage,
                                  const SendCallback& onSend) {
    while (buffer_.size() >= 2) {
        size_t pos = 0;

        uint8_t byte0 = buffer_[pos++];
        uint8_t opcode = byte0 & 0x0F;

        if (pos >= buffer_.size()) return;
        uint8_t byte1 = buffer_[pos++];
        bool masked = (byte1 >> 7) & 1;
        uint64_t payloadLen = byte1 & 0x7F;

        // Extended payload length
        if (payloadLen == 126) {
            if (buffer_.size() < pos + 2) return;
            payloadLen = (static_cast<uint64_t>(buffer_[pos]) << 8)
                       | buffer_[pos + 1];
            pos += 2;
        } else if (payloadLen == 127) {
            if (buffer_.size() < pos + 8) return;
            payloadLen = 0;
            for (int i = 0; i < 8; ++i)
                payloadLen = (payloadLen << 8) | buffer_[pos + i];
            pos += 8;
        }

        // Mask key (client→server MUST be masked per RFC 6455 §5.1)
        uint8_t maskKey[4] = {0, 0, 0, 0};
        if (masked) {
            if (buffer_.size() < pos + 4) return;
            for (int i = 0; i < 4; ++i) maskKey[i] = buffer_[pos + i];
            pos += 4;
        }

        // Safety limit
        if (payloadLen > 10 * 1024 * 1024) {
            onSend(buildCloseFrame());
            buffer_.clear();
            return;
        }

        // Wait for complete payload
        if (buffer_.size() < pos + payloadLen) return;

        // Dispatch by opcode
        switch (opcode) {
        case 0x2: { // Binary frame
            std::vector<uint8_t> payload(payloadLen);
            for (uint64_t i = 0; i < payloadLen; ++i)
                payload[i] = buffer_[pos + i] ^ maskKey[i % 4];
            if (onMessage) onMessage(payload);
            break;
        }
        case 0x8: // Close
            onSend(buildCloseFrame());
            buffer_.clear();
            return;
        case 0x9: { // Ping → Pong
            std::vector<uint8_t> pingPayload(payloadLen);
            for (uint64_t i = 0; i < payloadLen; ++i)
                pingPayload[i] = buffer_[pos + i] ^ maskKey[i % 4];
            onSend(buildPongFrame(pingPayload));
            break;
        }
        case 0xA: // Pong — ignore
            break;
        default:
            // Unknown opcode — ignore frame
            break;
        }

        buffer_.erase(buffer_.begin(),
                      buffer_.begin() + static_cast<long>(pos + payloadLen));
    }
}

std::string WebSocketCodec::buildPongFrame(const std::vector<uint8_t>& pingPayload) {
    std::string pong;
    pong.reserve(2 + pingPayload.size());
    pong.push_back(static_cast<char>(0x8A)); // FIN | Pong
    pong.push_back(static_cast<char>(pingPayload.size()));
    pong.append(reinterpret_cast<const char*>(pingPayload.data()),
                pingPayload.size());
    return pong;
}

std::string WebSocketCodec::buildCloseFrame() {
    std::string close;
    close.push_back(static_cast<char>(0x88)); // FIN | Close
    close.push_back(static_cast<char>(0x00)); // 0-length payload
    return close;
}

void WebSocketCodec::reset() {
    buffer_.clear();
    state_ = State::Handshake;
}

} // namespace game
```

- [ ] **Step 2: 更新 CMakeLists.txt 添加新文件和 OpenSSL 链接**

在 `CMakeLists.txt` 的 `GAME_SERVER_SRCS` 中添加 `src/common/WebSocketCodec.cc`，并在 `target_link_libraries` 中添加 `ssl` 和 `crypto`：

```cmake
set(GAME_SERVER_SRCS
    src/common/WebSocketCodec.cc
    src/common/MessageCodec.cc
    src/common/MessageDispatcher.cc
    src/room/RoomService.cc
    src/game/GameService.cc
    src/server/GameServer.cc
    ${PROTO_SRCS}
)

# ... later in the file ...

target_link_libraries(GameServer
    mymuduo
    ${PROTOBUF_LIBRARY}
    pthread
    ssl
    crypto
)
```

- [ ] **Step 3: 提交**

```bash
git add MuduoBaseGameServer/src/common/WebSocketCodec.cc MuduoBaseGameServer/CMakeLists.txt
git commit -m "feat: implement WebSocket codec (handshake, encode, decode, control frames)"
```

---

### Task 3: 编写 WebSocketCodec 单元测试

**Files:**
- Create: `MuduoBaseGameServer/mytest/websocket_codec_test.cpp`
- Modify: `MuduoBaseGameServer/CMakeLists.txt` (添加测试 target)

- [ ] **Step 1: 编写测试**

```cpp
#include <cassert>
#include <iostream>
#include <string>
#include <vector>
#include "src/common/WebSocketCodec.h"

static int testsPassed = 0;
static int testsFailed = 0;

#define TEST(name) do { std::cout << "  " << name << "... "; } while(0)
#define PASS() do { std::cout << "PASS" << std::endl; testsPassed++; } while(0)
#define FAIL(msg) do { std::cout << "FAIL: " << msg << std::endl; testsFailed++; } while(0)
#define CHECK(cond) do { if (!(cond)) { FAIL(#cond); return; } } while(0)

// ── Test 1: encode small payload (<126 bytes) ──
void testEncodeSmall() {
    TEST("encode small payload");
    std::string payload = "hello";
    std::string frame = game::WebSocketCodec::encode(payload);

    // [0x82][0x05]["hello"]
    CHECK(frame.size() == 2 + payload.size());
    CHECK((uint8_t)frame[0] == 0x82);  // FIN | Binary
    CHECK((uint8_t)frame[1] == 0x05);  // len=5
    CHECK(frame.substr(2) == payload);
    PASS();
}

// ── Test 2: encode 126-65535 payload ──
void testEncodeMedium() {
    TEST("encode medium payload (126-65535)");
    std::string payload(200, 'x');
    std::string frame = game::WebSocketCodec::encode(payload);

    CHECK((uint8_t)frame[0] == 0x82);
    CHECK((uint8_t)frame[1] == 126);   // marker
    uint16_t extLen = ((uint8_t)frame[2] << 8) | (uint8_t)frame[3];
    CHECK(extLen == 200);
    CHECK(frame.substr(4) == payload);
    PASS();
}

// ── Test 3: encode payload >= 65536 ──
void testEncodeLarge() {
    TEST("encode large payload (>=65536)");
    std::string payload(70000, 'y');
    std::string frame = game::WebSocketCodec::encode(payload);

    CHECK((uint8_t)frame[0] == 0x82);
    CHECK((uint8_t)frame[1] == 127);   // 64-bit length marker
    uint64_t extLen = 0;
    for (int i = 0; i < 8; ++i)
        extLen = (extLen << 8) | (uint8_t)frame[2 + i];
    CHECK(extLen == 70000);
    CHECK(frame.substr(10) == payload);
    PASS();
}

// ── Test 4: handshake → 101 response ──
void testHandshake() {
    TEST("handshake produces 101 response");
    game::WebSocketCodec codec;
    CHECK(codec.state() == game::WebSocketCodec::State::Handshake);

    std::string sendData;
    std::vector<uint8_t> receivedMessage;

    auto onSend = [&](const std::string& data) { sendData = data; };
    auto onMsg = [&](const std::vector<uint8_t>& p) { receivedMessage = p; };

    // Build a realistic HTTP upgrade request
    std::string httpReq =
        "GET / HTTP/1.1\r\n"
        "Host: localhost:8888\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        "Sec-WebSocket-Version: 13\r\n"
        "\r\n";

    codec.feedBytes(httpReq.data(), httpReq.size(), onMsg, onSend);

    CHECK(codec.state() == game::WebSocketCodec::State::Frame);
    CHECK(sendData.find("101 Switching Protocols") != std::string::npos);
    CHECK(sendData.find("Upgrade: websocket") != std::string::npos);
    CHECK(sendData.find("Sec-WebSocket-Accept:") != std::string::npos);
    PASS();
}

// ── Test 5: decode masked binary frame ──
void testDecodeBinary() {
    TEST("decode masked binary frame");
    game::WebSocketCodec codec;
    std::string sendData;
    std::vector<uint8_t> receivedMessage;

    auto onSend = [&](const std::string& d) { sendData += d; };
    auto onMsg = [&](const std::vector<uint8_t>& p) { receivedMessage = p; };

    // First, complete handshake
    std::string httpReq =
        "GET / HTTP/1.1\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        "Sec-WebSocket-Version: 13\r\n"
        "\r\n";
    codec.feedBytes(httpReq.data(), httpReq.size(), onMsg, onSend);
    sendData.clear();

    // Now send a masked binary frame: payload = "protobuf_data"
    std::string payload = "protobuf_data";
    uint8_t maskKey[4] = {0x12, 0x34, 0x56, 0x78};

    std::vector<uint8_t> wsFrame;
    wsFrame.push_back(0x82);  // FIN | Binary
    wsFrame.push_back(0x80 | static_cast<uint8_t>(payload.size())); // MASK=1, len
    wsFrame.insert(wsFrame.end(), maskKey, maskKey + 4);
    for (size_t i = 0; i < payload.size(); ++i)
        wsFrame.push_back(static_cast<uint8_t>(payload[i]) ^ maskKey[i % 4]);

    codec.feedBytes(reinterpret_cast<const char*>(wsFrame.data()),
                    wsFrame.size(), onMsg, onSend);

    CHECK(receivedMessage.size() == payload.size());
    std::string decoded(receivedMessage.begin(), receivedMessage.end());
    CHECK(decoded == payload);
    PASS();
}

// ── Test 6: ping → pong ──
void testPingPong() {
    TEST("ping triggers pong");
    game::WebSocketCodec codec;
    std::string sendData;
    std::vector<uint8_t> receivedMessage;

    auto onSend = [&](const std::string& d) { sendData += d; };
    auto onMsg = [&](const std::vector<uint8_t>& p) { receivedMessage = p; };

    // Handshake first
    std::string httpReq =
        "GET / HTTP/1.1\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        "Sec-WebSocket-Version: 13\r\n"
        "\r\n";
    codec.feedBytes(httpReq.data(), httpReq.size(), onMsg, onSend);
    sendData.clear();

    // Send ping frame: opcode=0x9, mask, payload="ping"
    std::vector<uint8_t> pingFrame;
    pingFrame.push_back(0x89);  // FIN | Ping
    pingFrame.push_back(0x80 | 4); // MASK=1, len=4
    uint8_t mk[4] = {0xAA, 0xBB, 0xCC, 0xDD};
    pingFrame.insert(pingFrame.end(), mk, mk + 4);
    std::string pingPayload = "ping";
    for (size_t i = 0; i < 4; ++i)
        pingFrame.push_back(pingPayload[i] ^ mk[i % 4]);

    codec.feedBytes(reinterpret_cast<const char*>(pingFrame.data()),
                    pingFrame.size(), onMsg, onSend);

    // sendData should contain a Pong frame
    CHECK(!sendData.empty());
    CHECK((uint8_t)sendData[0] == 0x8A);  // FIN | Pong
    PASS();
}

// ── Test 7: partial data (frame split across TCP reads) ──
void testPartialFrame() {
    TEST("partial frame reassembly");
    game::WebSocketCodec codec;
    std::string sendData;
    std::vector<uint8_t> receivedMessage;

    auto onSend = [&](const std::string& d) { sendData += d; };
    auto onMsg = [&](const std::vector<uint8_t>& p) { receivedMessage = p; };

    // Handshake
    std::string httpReq =
        "GET / HTTP/1.1\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        "Sec-WebSocket-Version: 13\r\n"
        "\r\n";
    codec.feedBytes(httpReq.data(), httpReq.size(), onMsg, onSend);
    sendData.clear();

    // Build a masked binary frame
    std::string payload = "split_test_data";
    uint8_t maskKey[4] = {0x01, 0x02, 0x03, 0x04};

    std::vector<uint8_t> fullFrame;
    fullFrame.push_back(0x82);
    fullFrame.push_back(0x80 | static_cast<uint8_t>(payload.size()));
    fullFrame.insert(fullFrame.end(), maskKey, maskKey + 4);
    for (size_t i = 0; i < payload.size(); ++i)
        fullFrame.push_back(payload[i] ^ maskKey[i % 4]);

    // Feed first 5 bytes only
    codec.feedBytes(reinterpret_cast<const char*>(fullFrame.data()), 5,
                    onMsg, onSend);
    CHECK(receivedMessage.empty()); // Should not trigger yet

    // Feed the rest
    codec.feedBytes(reinterpret_cast<const char*>(fullFrame.data() + 5),
                    fullFrame.size() - 5, onMsg, onSend);

    CHECK(receivedMessage.size() == payload.size());
    std::string decoded(receivedMessage.begin(), receivedMessage.end());
    CHECK(decoded == payload);
    PASS();
}

// ── Main ──
int main() {
    std::cout << "WebSocketCodec Tests" << std::endl;
    std::cout << "====================" << std::endl;

    testEncodeSmall();
    testEncodeMedium();
    testEncodeLarge();
    testHandshake();
    testDecodeBinary();
    testPingPong();
    testPartialFrame();

    std::cout << std::endl;
    std::cout << testsPassed << " passed, " << testsFailed << " failed" << std::endl;
    return testsFailed > 0 ? 1 : 0;
}
```

- [ ] **Step 2: 编译并运行测试**

```bash
cd MuduoBaseGameServer/build
cmake .. && make -j$(nproc)

# Compile and run test
g++ -std=c++11 -I.. -I../src \
    -o websocket_test ../mytest/websocket_codec_test.cpp \
    ../src/common/WebSocketCodec.cc \
    -lssl -lcrypto

./websocket_test
```

Expected output: `7 passed, 0 failed`

- [ ] **Step 3: 提交**

```bash
git add MuduoBaseGameServer/mytest/websocket_codec_test.cpp
git commit -m "test: add WebSocketCodec unit tests (7 cases)"
```

---

### Task 4: 集成 WebSocketCodec 到 GameServer

**Files:**
- Modify: `MuduoBaseGameServer/src/server/GameServer.cc`
- Modify: `MuduoBaseGameServer/src/room/RoomService.cc`
- Modify: `MuduoBaseGameServer/src/game/GameService.cc`

- [ ] **Step 1: 修改 GameServer.cc — 替换 MessageCodec 为 WebSocketCodec**

在 `GameServer.cc` 中做以下修改：

```cpp
// 修改 include（第8行）：
// 之前：#include "common/MessageCodec.h"
// 之后：
#include "common/WebSocketCodec.h"

// 修改 codecs_ 声明（第180行）：
// 之前：std::unordered_map<const TcpConnection*, game::MessageCodec> codecs_;
// 之后：
std::unordered_map<const TcpConnection*, game::WebSocketCodec> codecs_;

// 修改 onConnection — new connection（第151-153行）：
// 之前：codecs_[conn.get()] = game::MessageCodec();
// 之后：
codecs_[conn.get()] = game::WebSocketCodec();

// 修改 onMessage（第163-173行）：
void onMessage(const TcpConnectionPtr& conn,
               Buffer* buf, Timestamp /*time*/) {
    std::string raw = buf->retrieveAllAsString();
    auto it = codecs_.find(conn.get());
    if (it == codecs_.end()) return;

    it->second.feedBytes(raw.data(), raw.size(),
        // onMessage — dispatch protobuf payload
        [this, conn](const std::vector<uint8_t>& payload) {
            dispatcher_.dispatch(conn, payload);
        },
        // onSend — send WebSocket handshake/control frames back
        [conn](const std::string& data) {
            conn->send(data);
        });
}
```

- [ ] **Step 2: 修改 RoomService.cc — 替换静态编码调用**

```cpp
// 修改 include：
// 之前：#include "common/MessageCodec.h"
// 之后：
#include "common/WebSocketCodec.h"

// 在 sendTo 方法中（第57行）：
// 之前：auto frame = MessageCodec::encode(payload);
// 之后：
auto frame = WebSocketCodec::encode(payload);
```

- [ ] **Step 3: 修改 GameService.cc — 替换静态编码调用**

```cpp
// 修改 include：
// 之前：#include "common/MessageCodec.h"
// 之后：
#include "common/WebSocketCodec.h"

// 在 sendToPlayer 方法中（第133行）：
// 之前：auto frame = game::MessageCodec::encode(payload);
// 之后：
auto frame = game::WebSocketCodec::encode(payload);
```

- [ ] **Step 4: 编译验证**

```bash
cd MuduoBaseGameServer/build && cmake .. && make -j$(nproc)
```

Expected: 编译成功，无错误。

- [ ] **Step 5: 提交**

```bash
git add MuduoBaseGameServer/src/server/GameServer.cc \
        MuduoBaseGameServer/src/room/RoomService.cc \
        MuduoBaseGameServer/src/game/GameService.cc
git commit -m "feat: integrate WebSocketCodec into GameServer, RoomService, GameService"
```

---

### Task 5: 服务端集成测试（手动 WebSocket 客户端验证）

**Files:**
- No new files

- [ ] **Step 1: 启动 GameServer**

```bash
cd MuduoBaseGameServer/build
LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer 8888
```

Expected: `GameServer listening on port 8888`

- [ ] **Step 2: 用 wscat 或 websocat 验证握手和发送**

```bash
# 安装 websocat（如果没有）
# sudo apt install websocat  # 或 cargo install websocat

# 连接
websocat ws://127.0.0.1:8888

# 应该能连接成功（websocat 会显示连接已建立）
# 发送一个 protobuf 消息测试（需要预先序列化好的 bytes）
```

Expected: 连接建立，无协议错误。按 Ctrl+C 断开。

- [ ] **Step 3: 验证 Close 帧处理**

断开 websocat 后，GameServer 日志应显示：
```
GameServer - connection DOWN : 127.0.0.1:xxxxx
```

---

### Task 6: 创建 Unity WebSocketNetworkClient

**Files:**
- Create: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Network/WebSocketNetworkClient.cs`

- [ ] **Step 1: 编写 WebSocketNetworkClient.cs**

```csharp
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cabo.Client.Network
{
    public enum NetworkClientState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// WebSocket transport layer. Replaces TcpNetworkClient.
    /// Uses System.Net.WebSockets.ClientWebSocket (Unity .NET 4.x compatible).
    /// </summary>
    public sealed class WebSocketNetworkClient : IDisposable
    {
        private ClientWebSocket ws;
        private CancellationTokenSource receiveCts;
        private readonly string url;
        private const int ReceiveBufferSize = 8192;

        public NetworkClientState State { get; private set; } = NetworkClientState.Disconnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> DataReceived;
        public event Action<string> ErrorOccurred;

        /// <param name="url">Full WebSocket URL, e.g. "wss://xxx.trycloudflare.com" or "ws://127.0.0.1:8888"</param>
        public WebSocketNetworkClient(string url)
        {
            this.url = url;
        }

        public async Task ConnectAsync()
        {
            if (State == NetworkClientState.Connected || State == NetworkClientState.Connecting)
                return;

            State = NetworkClientState.Connecting;
            try
            {
                ws = new ClientWebSocket();
                // Enable built-in keep-alive (ping/pong every 30s)
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                State = NetworkClientState.Connected;
                receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoop(receiveCts.Token));
                Connected?.Invoke();
                Debug.Log($"[WebSocketClient] Connected to {url}");
            }
            catch (Exception ex)
            {
                State = NetworkClientState.Disconnected;
                ErrorOccurred?.Invoke($"Connect failed: {ex.Message}");
                Debug.LogError($"[WebSocketClient] Connect error: {ex}");
            }
        }

        public async void Disconnect()
        {
            receiveCts?.Cancel();
            if (ws != null && ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                        "Client disconnect",
                                        CancellationToken.None);
                }
                catch { }
            }
            ws?.Dispose();
            ws = null;
            if (State != NetworkClientState.Disconnected)
            {
                State = NetworkClientState.Disconnected;
                Disconnected?.Invoke();
            }
            Debug.Log("[WebSocketClient] Disconnected");
        }

        public async Task SendAsync(byte[] data)
        {
            if (State != NetworkClientState.Connected || ws == null)
            {
                Debug.LogWarning("[WebSocketClient] Cannot send — not connected");
                return;
            }

            try
            {
                await ws.SendAsync(new ArraySegment<byte>(data),
                                   WebSocketMessageType.Binary,
                                   endOfMessage: true,
                                   CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketClient] Send error: {ex}");
                Disconnect();
            }
        }

        // Synchronous wrapper for existing code that expects void Send(byte[])
        public void Send(byte[] data)
        {
            _ = SendAsync(data);
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuffer = new System.Collections.Generic.List<byte>();
            try
            {
                while (!ct.IsCancellationRequested && ws != null &&
                       ws.State == WebSocketState.Open)
                {
                    messageBuffer.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.Log("[WebSocketClient] Server sent Close frame");
                            break;
                        }
                        messageBuffer.AddRange(
                            new ArraySegment<byte>(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (messageBuffer.Count > 0)
                        DataReceived?.Invoke(messageBuffer.ToArray());
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (System.IO.IOException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketClient] Receive error: {ex}");
            }
            finally
            {
                if (State == NetworkClientState.Connected)
                {
                    State = NetworkClientState.Disconnected;
                    Disconnected?.Invoke();
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            receiveCts?.Dispose();
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add "unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Network/WebSocketNetworkClient.cs"
git commit -m "feat: add WebSocketNetworkClient for Unity"
```

---

### Task 7: 适配 NetworkGateway 使用 WebSocket

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/NetworkGateway.cs`

- [ ] **Step 1: 修改 NetworkGateway.cs**

主要改动：`TcpClient` → `WebSocketNetworkClient`，`ConnectAsync(host, port)` → `ConnectAsync(url)`，`MessageCodec` 去掉 `[4B len]` 帧只保留 protobuf 序列化。

```csharp
// 修改前（头几行）：
// using System.Net.Sockets;
// using Cabo.Client.Network;

// 修改后：移除 using System.Net.Sockets;

// 修改字段声明：
// 之前：private TcpClient tcpClient;
//       private NetworkStream stream;
// 之后：
private WebSocketNetworkClient wsClient;

// 修改 ConnectAsync 签名：
// 之前：public async Task ConnectAsync(string host, int port)
// 之后：
/// <param name="url">WebSocket URL, e.g. "ws://127.0.0.1:8888" or "wss://xxx.trycloudflare.com"</param>
public async Task ConnectAsync(string url)
{
    try
    {
        wsClient = new WebSocketNetworkClient(url);
        wsClient.Connected += () =>
        {
            IsConnected = true;
            Connected?.Invoke();
        };
        wsClient.Disconnected += () =>
        {
            IsConnected = false;
            Disconnected?.Invoke();
        };
        wsClient.DataReceived += OnDataReceived;
        wsClient.ErrorOccurred += e => Error?.Invoke(e);

        await wsClient.ConnectAsync();
    }
    catch (Exception ex)
    {
        Error?.Invoke($"Connect failed: {ex.Message}");
        Debug.LogError($"[NetworkGateway] {ex}");
    }
}

// 新增：OnDataReceived 方法（替换原来的 ReceiveLoop）：
private void OnDataReceived(byte[] data)
{
    // WebSocket already provides message boundaries — no length prefix
    // data is pure protobuf payload
    var msg = MessageCodec.Decode(data);
    lock (pendingMessages)
        pendingMessages.Enqueue(msg);
}

// 修改 Disconnect：
public void Disconnect()
{
    wsClient?.Disconnect();
    IsConnected = false;
    Disconnected?.Invoke();
}

// 修改 Send 方法：
/// <summary>Send a ClientMessage. Sets seq automatically.</summary>
public void Send(ClientMessage msg)
{
    if (!IsConnected || wsClient == null) return;
    msg.Seq = _nextSeq++;
    // MessageCodec.Encode now does pure protobuf serialization (no length prefix)
    var payload = MessageCodec.Encode(msg);
    wsClient.Send(payload);
}

// 修改 Dispose：
public void Dispose()
{
    Disconnect();
    wsClient?.Dispose();
}

// 移除: CancellationTokenSource receiveCts (line 23)
// 移除: private async Task ReceiveLoop(CancellationToken ct) 整个方法
```

同时简化 `MessageCodec`（`Assets/Scripts/Network/MessageCodec.cs`），将 `Encode` 方法改为纯 protobuf 序列化：

```csharp
/// <summary>
/// Serialize a ClientMessage to bytes (pure protobuf, no length prefix).
/// WebSocket provides message boundaries.
/// </summary>
public static byte[] Encode(ClientMessage message)
{
    return message.ToByteArray();
}
```

`FeedBytes` 方法保留（用于向后兼容），但 `NetworkGateway` 不再使用它 — 接收端直接用 `Decode` 处理完整的 WebSocket 消息。

- [ ] **Step 2: 提交**

```bash
git add "unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/NetworkGateway.cs" \
        "unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Network/MessageCodec.cs"
git commit -m "feat: adapt NetworkGateway to use WebSocketNetworkClient"
```

---

### Task 8: 适配 GameFlow 和 GameBootstrap 使用 WebSocket URL

**Files:**
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- Modify: `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/GameBootstrap.cs`

- [ ] **Step 1: 修改 GameFlow.cs — Connect 方法签名**

```csharp
// 之前：
// string _host, _nickname = "玩家";
// int _port;
// public async void Connect(string host, int port, string nickname = null)

// 之后：
string _serverUrl = "ws://127.0.0.1:8888";
string _nickname = "玩家";

public async void Connect(string url, string nickname = null)
{
    _serverUrl = url;
    if (!string.IsNullOrWhiteSpace(nickname))
        _nickname = NormalizeNickname(nickname);
    Flow = FlowState.Connecting; StateChanged?.Invoke();
    await Gateway.ConnectAsync(url);
    if (!Gateway.IsConnected) return;
    Flow = FlowState.RoomFlow; StateChanged?.Invoke();
}
```

- [ ] **Step 2: 修改 GameBootstrap.cs — 连接参数**

```csharp
// 之前：
// [SerializeField] private string serverHost = "127.0.0.1";
// [SerializeField] private int serverPort = 8888;

// 之后：
[SerializeField] private string serverUrl = "ws://127.0.0.1:8888";

// 在 Start() 方法中：
// 之前：_flow.Connect(serverHost, serverPort);
// 之后：
_flow.Connect(serverUrl);

// 日志：
// 之前：Debug.Log($"[GameBootstrap] Started - {serverHost}:{serverPort}");
// 之后：
Debug.Log($"[GameBootstrap] Started - {serverUrl}");
```

- [ ] **Step 3: 提交**

```bash
git add "unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs" \
        "unity dev/New Client_Unity_Base_Cli/Assets/Scripts/GameBootstrap.cs"
git commit -m "feat: adapt GameFlow and GameBootstrap for WebSocket URL connections"
```

---

### Task 9: 本地 WebSocket 端到端测试

**Files:**
- No new files — manual test

- [ ] **Step 1: 启动 C++ GameServer**

```bash
cd MuduoBaseGameServer/build
LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer 8888
```

- [ ] **Step 2: 在 Unity Editor 中运行客户端**

设置 `GameBootstrap.serverUrl = "ws://127.0.0.1:8888"`，启动 Play Mode。

- [ ] **Step 3: 验证完整流程**

1. 客户端连接成功 → GameServer 日志显示 connection UP
2. 创建房间 → RoomService 返回 RoomStateNotify
3. 加入房间 → 广播 PlayerJoinNotify
4. 开始游戏 → GameService 发牌、发送 TurnStartNotify
5. 抽牌 → DrawCardRsp 返回
6. 弃牌 → DiscardDrawnRsp 返回
7. 确认游戏正常进行一轮

- [ ] **Step 4: 验证断开重连**

停止 Unity Play Mode → GameServer 日志显示 connection DOWN。
重新 Play → 连接成功。

---

### Task 10: 配置 Cloudflare Tunnel

**Files:**
- Create: `shotcuts/start_cloudflared.sh`（可选，方便启动）

- [ ] **Step 1: 安装 cloudflared**

```bash
# 下载并安装
wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb
sudo dpkg -i cloudflared-linux-amd64.deb

# 验证安装
cloudflared --version
```

- [ ] **Step 2: 创建启动脚本**

```bash
#!/bin/bash
# shotcuts/start_server_public.sh

echo "=== Starting Cabo GameServer + Cloudflare Tunnel ==="

# Start GameServer in background
cd "$(dirname "$0")/../MuduoBaseGameServer/build"
LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer 8888 &
GAME_PID=$!
echo "GameServer PID: $GAME_PID"

sleep 1

# Start Cloudflare Tunnel
echo ""
echo "=== Starting Cloudflare Tunnel ==="
echo "Share the HTTPS URL below with players (use wss:// for the client):"
echo ""
cloudflared tunnel --url http://localhost:8888

# Cleanup on exit
kill $GAME_PID 2>/dev/null
```

```bash
chmod +x shotcuts/start_server_public.sh
```

- [ ] **Step 3: 启动并获取临时域名**

```bash
./shotcuts/start_server_public.sh
```

Output 中会显示 `https://xxx.trycloudflare.com`。记录这个 URL。

- [ ] **Step 4: 更新 Unity 客户端连接**

在 Unity Editor 中将 `GameBootstrap.serverUrl` 设置为 `wss://xxx.trycloudflare.com`（注意 `https://` → `wss://`）。运行 Play Mode 验证连接。

- [ ] **Step 5: 提交**

```bash
git add shotcuts/start_server_public.sh
git commit -m "feat: add Cloudflare Tunnel startup script"
```

---

### Task 11: 最终验证与清理

- [ ] **Step 1: 多玩家测试**

同时运行两个 Unity Editor 实例（或一个 Editor + 一个 Build）:
1. 两个客户端同时连接（通过 cloudflared 临时域名）
2. 创建房间 → 加入 → 开始游戏
3. 完成一局完整游戏

- [ ] **Step 2: 清理旧文件（可选）**

`MessageCodec.h/cc` 不再被使用，可以保留作为参考，或标记为 deprecated：

```cpp
// MessageCodec.h 顶部添加：
// DEPRECATED: Replaced by WebSocketCodec. Kept for reference only.
```

`TcpNetworkClient.cs` 同样标记为 deprecated。

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "chore: mark legacy TCP codec as deprecated, final verification"
```
