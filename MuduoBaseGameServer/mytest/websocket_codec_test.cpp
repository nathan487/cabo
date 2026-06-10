#include "common/WebSocketCodec.h"

#include <cassert>
#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

namespace {

int passed = 0;
int failed = 0;

#define CHECK(expr) do { if (!(expr)) { \
    std::cerr << "FAIL " << __FUNCTION__ << ": " << #expr << std::endl; \
    ++failed; return; \
} } while (0)

void pass() {
    ++passed;
}

std::string handshake(const std::string& extraHeaders = "") {
    return "GET / HTTP/1.1\r\n"
           "Host: localhost:8888\r\n"
           "Upgrade: websocket\r\n"
           "Connection: Upgrade\r\n"
           "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
           "Sec-WebSocket-Version: 13\r\n"
           + extraHeaders +
           "\r\n";
}

std::string maskedFrame(uint8_t opcode, const std::string& payload, bool fin = true) {
    std::string frame;
    frame.push_back(static_cast<char>((fin ? 0x80 : 0x00) | opcode));
    uint8_t maskKey[4] = {0x12, 0x34, 0x56, 0x78};
    uint64_t len = payload.size();
    if (len < 126) {
        frame.push_back(static_cast<char>(0x80 | len));
    } else if (len <= 65535) {
        frame.push_back(static_cast<char>(0x80 | 126));
        frame.push_back(static_cast<char>((len >> 8) & 0xFF));
        frame.push_back(static_cast<char>(len & 0xFF));
    } else {
        frame.push_back(static_cast<char>(0x80 | 127));
        for (int i = 7; i >= 0; --i)
            frame.push_back(static_cast<char>((len >> (i * 8)) & 0xFF));
    }
    frame.append(reinterpret_cast<const char*>(maskKey), 4);
    for (size_t i = 0; i < payload.size(); ++i)
        frame.push_back(static_cast<char>(payload[i] ^ maskKey[i % 4]));
    return frame;
}

void feedHandshake(game::WebSocketCodec& codec, std::string* sent = nullptr) {
    std::string localSent;
    bool closed = false;
    codec.feedBytes(handshake().data(), handshake().size(),
        [](const std::vector<uint8_t>&) {},
        [&](const std::string& data) { localSent += data; },
        [&]() { closed = true; });
    CHECK(!closed);
    CHECK(codec.state() == game::WebSocketCodec::State::Frame);
    CHECK(localSent.find("101 Switching Protocols") != std::string::npos);
    if (sent)
        *sent = localSent;
}

void testHandshake101() {
    game::WebSocketCodec codec;
    std::string sent;
    feedHandshake(codec, &sent);
    CHECK(sent.find("Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=") != std::string::npos);
    pass();
}

void testMixedCaseHeadersAndConnectionTokens() {
    game::WebSocketCodec codec;
    std::string req =
        "GET /play HTTP/1.1\r\n"
        "hOSt: localhost\r\n"
        "uPgRaDe: websocket\r\n"
        "cOnNeCtIoN: keep-alive, Upgrade\r\n"
        "sEc-WeBsOcKeT-kEy: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        "SEC-WEBSOCKET-VERSION: 13\r\n"
        "CF-Connecting-IP: 203.0.113.1\r\n"
        "\r\n";
    std::string sent;
    bool closed = false;
    codec.feedBytes(req.data(), req.size(),
        [](const std::vector<uint8_t>&) {},
        [&](const std::string& data) { sent += data; },
        [&]() { closed = true; });
    CHECK(!closed);
    CHECK(codec.state() == game::WebSocketCodec::State::Frame);
    CHECK(sent.find("101 Switching Protocols") != std::string::npos);
    pass();
}

void testInvalidHandshake() {
    game::WebSocketCodec codec;
    std::string req =
        "GET / HTTP/1.1\r\n"
        "Host: localhost\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        "Sec-WebSocket-Version: 12\r\n"
        "\r\n";
    std::string sent;
    bool closed = false;
    codec.feedBytes(req.data(), req.size(),
        [](const std::vector<uint8_t>&) {},
        [&](const std::string& data) { sent += data; },
        [&]() { closed = true; });
    CHECK(closed);
    CHECK(codec.state() == game::WebSocketCodec::State::Closed);
    CHECK(sent.find("400 Bad Request") != std::string::npos);
    pass();
}

void testEncodePayloadSizes() {
    std::string small = game::WebSocketCodec::encode("abc");
    CHECK(static_cast<uint8_t>(small[0]) == 0x82);
    CHECK(static_cast<uint8_t>(small[1]) == 3);
    CHECK(small.substr(2) == "abc");

    std::string mediumPayload(200, 'm');
    std::string medium = game::WebSocketCodec::encode(mediumPayload);
    CHECK(static_cast<uint8_t>(medium[1]) == 126);
    CHECK((((uint8_t)medium[2] << 8) | (uint8_t)medium[3]) == 200);
    CHECK(medium.substr(4) == mediumPayload);

    std::string largePayload(70000, 'l');
    std::string large = game::WebSocketCodec::encode(largePayload);
    CHECK(static_cast<uint8_t>(large[1]) == 127);
    uint64_t len = 0;
    for (int i = 0; i < 8; ++i)
        len = (len << 8) | static_cast<uint8_t>(large[2 + i]);
    CHECK(len == largePayload.size());
    CHECK(large.substr(10) == largePayload);
    pass();
}

void testDecodeMaskedBinary() {
    game::WebSocketCodec codec;
    feedHandshake(codec);
    std::vector<uint8_t> got;
    std::string sent;
    bool closed = false;
    std::string frame = maskedFrame(0x2, "protobuf");
    codec.feedBytes(frame.data(), frame.size(),
        [&](const std::vector<uint8_t>& data) { got = data; },
        [&](const std::string& data) { sent += data; },
        [&]() { closed = true; });
    CHECK(!closed);
    CHECK(std::string(got.begin(), got.end()) == "protobuf");
    CHECK(sent.empty());
    pass();
}

void testRejectUnmaskedBinary() {
    game::WebSocketCodec codec;
    feedHandshake(codec);
    std::string frame;
    frame.push_back(static_cast<char>(0x82));
    frame.push_back(static_cast<char>(0x03));
    frame += "bad";
    std::string sent;
    bool closed = false;
    codec.feedBytes(frame.data(), frame.size(),
        [](const std::vector<uint8_t>&) {},
        [&](const std::string& data) { sent += data; },
        [&]() { closed = true; });
    CHECK(closed);
    CHECK(codec.state() == game::WebSocketCodec::State::Closed);
    CHECK(sent.size() >= 4);
    CHECK(static_cast<uint8_t>(sent[0]) == 0x88);
    CHECK(static_cast<uint8_t>(sent[3]) == 0xEA); // 1002
    pass();
}

void testPartialTcpReads() {
    game::WebSocketCodec codec;
    feedHandshake(codec);
    std::vector<uint8_t> got;
    std::string frame = maskedFrame(0x2, "split");
    codec.feedBytes(frame.data(), 3,
        [&](const std::vector<uint8_t>& data) { got = data; },
        [](const std::string&) {},
        []() {});
    CHECK(got.empty());
    codec.feedBytes(frame.data() + 3, frame.size() - 3,
        [&](const std::vector<uint8_t>& data) { got = data; },
        [](const std::string&) {},
        []() {});
    CHECK(std::string(got.begin(), got.end()) == "split");
    pass();
}

void testFragmentedBinaryReassembly() {
    game::WebSocketCodec codec;
    feedHandshake(codec);
    std::vector<uint8_t> got;
    std::string first = maskedFrame(0x2, "frag-", false);
    std::string second = maskedFrame(0x0, "ment", true);
    codec.feedBytes(first.data(), first.size(),
        [&](const std::vector<uint8_t>& data) { got = data; },
        [](const std::string&) {},
        []() {});
    CHECK(got.empty());
    codec.feedBytes(second.data(), second.size(),
        [&](const std::vector<uint8_t>& data) { got = data; },
        [](const std::string&) {},
        []() {});
    CHECK(std::string(got.begin(), got.end()) == "frag-ment");
    pass();
}

void testPingPong() {
    game::WebSocketCodec codec;
    feedHandshake(codec);
    std::string sent;
    bool closed = false;
    std::string frame = maskedFrame(0x9, "hi");
    codec.feedBytes(frame.data(), frame.size(),
        [](const std::vector<uint8_t>&) {},
        [&](const std::string& data) { sent += data; },
        [&]() { closed = true; });
    CHECK(!closed);
    CHECK(sent.size() == 4);
    CHECK(static_cast<uint8_t>(sent[0]) == 0x8A);
    CHECK(static_cast<uint8_t>(sent[1]) == 2);
    CHECK(sent.substr(2) == "hi");
    pass();
}

void testCloseHandling() {
    game::WebSocketCodec codec;
    feedHandshake(codec);
    std::string sent;
    bool closed = false;
    std::string frame = maskedFrame(0x8, "");
    codec.feedBytes(frame.data(), frame.size(),
        [](const std::vector<uint8_t>&) {},
        [&](const std::string& data) { sent += data; },
        [&]() { closed = true; });
    CHECK(closed);
    CHECK(codec.state() == game::WebSocketCodec::State::Closed);
    CHECK(static_cast<uint8_t>(sent[0]) == 0x88);
    pass();
}

} // namespace

int main() {
    testHandshake101();
    testMixedCaseHeadersAndConnectionTokens();
    testInvalidHandshake();
    testEncodePayloadSizes();
    testDecodeMaskedBinary();
    testRejectUnmaskedBinary();
    testPartialTcpReads();
    testFragmentedBinaryReassembly();
    testPingPong();
    testCloseHandling();

    std::cout << passed << " passed, " << failed << " failed" << std::endl;
    return failed == 0 ? 0 : 1;
}
