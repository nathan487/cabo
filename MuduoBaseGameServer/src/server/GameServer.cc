#include <mymuduo/TcpServer.h>
#include <mymuduo/EventLoop.h>
#include <mymuduo/InetAddress.h>
#include <mymuduo/logger.h>
#include <mymuduo/TcpConnection.h>
#include <mymuduo/Buffer.h>

#include "common/WebSocketCodec.h"
#include "common/MessageDispatcher.h"
#include "room/RoomService.h"
#include "game/GameService.h"

#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <chrono>

namespace {

int64_t nowMs() {
    using namespace std::chrono;
    return duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
}

} // namespace

class GameServer {
public:
    GameServer(EventLoop* loop, const InetAddress& addr,
               const std::string& name)
        : server_(loop, addr, name), loop_(loop)
    {
        server_.setConnectionCallback(
            [this](const TcpConnectionPtr& conn) { onConnection(conn); });
        server_.setMessageCallback(
            [this](const TcpConnectionPtr& conn,
                   Buffer* buf, Timestamp time) {
                onMessage(conn, buf, time);
            });
        server_.setThreadNum(4);

        // ── Room handlers ──
        dispatcher_.registerHandler(10, // create_room_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                roomService_.handleCreateRoom(conn, msg);
            });
        dispatcher_.registerHandler(11, // join_room_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                roomService_.handleJoinRoom(conn, msg);
            });
        dispatcher_.registerHandler(12, // leave_room_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                if (roomService_.handleLeaveRoom(conn, msg)) {
                    gameService_.onPlayerLeft(conn);
                }
            });
        dispatcher_.registerHandler(13, // ready_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                roomService_.handleReady(conn, msg);
            });
        dispatcher_.registerHandler(14, // start_game_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                onStartGame(conn, msg);
            });

        // ── Game action handlers ──
        dispatcher_.registerHandler(16, // room_chat_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                roomService_.handleRoomChat(conn, msg);
            });

        dispatcher_.registerHandler(20, // draw_card_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleDrawCard(conn, msg);
            });
        dispatcher_.registerHandler(21, // discard_drawn_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleDiscardDrawn(conn, msg);
            });
        dispatcher_.registerHandler(22, // replace_with_drawn_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleReplaceWithDrawn(conn, msg);
            });
        dispatcher_.registerHandler(23, // take_from_discard_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleTakeFromDiscard(conn, msg);
            });
        dispatcher_.registerHandler(24, // use_skill_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleUseSkill(conn, msg);
            });
        dispatcher_.registerHandler(25, // call_steady_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleCallSteady(conn, msg);
            });
        dispatcher_.registerHandler(26, // end_game_early_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleEndGameEarly(conn, msg);
            });
        dispatcher_.registerHandler(27, // end_game_early_decision_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                gameService_.handleEndGameEarlyDecision(conn, msg);
            });

        dispatcher_.registerHandler(30, // reconnect_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                onReconnect(conn, msg);
            });
        dispatcher_.registerHandler(31, // heartbeat_req
            [this](const cabogame::TcpConnectionPtr& conn,
                   const ::game::messages::ClientMessage& msg) {
                onHeartbeat(conn, msg);
            });

        // Wire room send function
        roomService_.setSendFunc(
            [](const cabogame::TcpConnectionPtr& conn, const std::string& framedData) {
                conn->send(framedData);
            });

        // Wire game send function
        gameService_.setSendFunc(
            [](const cabogame::TcpConnectionPtr& conn, const std::string& framedData) {
                conn->send(framedData);
            });

        gameService_.setGameFinishedFunc(
            [this](int64_t roomId) {
                roomService_.markGameFinished(roomId);
            });
    }

    void start() { server_.start(); }

private:
    void sendServerMessage(const cabogame::TcpConnectionPtr& conn,
                           const ::game::messages::ServerMessage& msg) {
        if (!conn) return;
        std::string payload;
        msg.SerializeToString(&payload);
        conn->send(game::WebSocketCodec::encode(payload));
    }

    void sendStartGameError(const cabogame::TcpConnectionPtr& conn,
                            int64_t requestId,
                            int32_t code,
                            const std::string& message) {
        ::game::messages::ServerMessage rspMsg;
        auto* rsp = rspMsg.mutable_start_game_rsp();
        rsp->set_request_id(requestId);
        rsp->mutable_error()->set_code(code);
        rsp->mutable_error()->set_message(message);

        std::string payload;
        rspMsg.SerializeToString(&payload);
        conn->send(game::WebSocketCodec::encode(payload));
    }

    void onReconnect(const cabogame::TcpConnectionPtr& conn,
                     const ::game::messages::ClientMessage& msg) {
        const auto& req = msg.reconnect_req();

        game::ReconnectSessionResult result;
        const bool roomOk = roomService_.reconnectSession(req.session_token(), conn, &result);

        ::game::messages::ServerMessage rspMsg;
        auto* rsp = rspMsg.mutable_reconnect_rsp();
        rsp->set_request_id(req.request_id());
        if (!roomOk) {
            rsp->mutable_error()->set_code(result.errorCode == 0 ? 1016 : result.errorCode);
            rsp->mutable_error()->set_message(result.errorMessage.empty()
                ? "Reconnect failed"
                : result.errorMessage);
            sendServerMessage(conn, rspMsg);
            return;
        }

        const bool isInGame = gameService_.reconnectPlayer(result.roomId, result.playerId, conn);
        rsp->mutable_error()->set_code(0);
        rsp->set_player_id(result.playerId);
        rsp->set_room_id(result.roomId);
        rsp->set_is_in_game(isInGame);
        sendServerMessage(conn, rspMsg);

        ::game::messages::ServerMessage syncMsg;
        auto* sync = syncMsg.mutable_state_sync_notify();
        sync->set_room_id(result.roomId);
        sync->set_server_time_ms(nowMs());
        *sync->mutable_room_state() = result.roomState;
        sync->set_is_in_game(isInGame);
        if (isInGame) {
            gameService_.fillGameSyncState(result.roomId, result.playerId,
                                           sync->mutable_game_state());
        }
        sendServerMessage(conn, syncMsg);
    }

    void onHeartbeat(const cabogame::TcpConnectionPtr& conn,
                     const ::game::messages::ClientMessage& msg) {
        const auto& req = msg.heartbeat_req();
        ::game::messages::ServerMessage rspMsg;
        auto* rsp = rspMsg.mutable_heartbeat_rsp();
        rsp->set_request_id(req.request_id());
        rsp->set_server_time_ms(nowMs());
        rsp->set_client_time_ms(req.client_time_ms());
        sendServerMessage(conn, rspMsg);
    }

    // Triggered when host clicks Start Game.
    // Validates room state, then delegates to GameService to begin.
    void onStartGame(const cabogame::TcpConnectionPtr& conn,
                     const ::game::messages::ClientMessage& msg) {
        const auto& req = msg.start_game_req();
        int64_t roomId = req.room_id();
        if (!gameService_.canRestartRound(roomId)) {
            sendStartGameError(conn, req.request_id(), 2005, "Game is still in progress");
            return;
        }

        // RoomService validates host/ready/etc and sends RoomStartNotify
        if (!roomService_.handleStartGame(conn, msg))
            return;

        // Kick off game logic
        bool startNewGame = !gameService_.hasGame(roomId) || gameService_.isGameOver(roomId);

        // Inter-round restart: existing active game, just start new round
        if (!startNewGame) {
            gameService_.restartRound(roomId);
            return;
        }

        // First game start, or a new full game after GameOver.
        auto snapshot = roomService_.getGameStartSnapshot(roomId);
        if (!snapshot.valid) return;

        std::vector<std::shared_ptr<cabogame::PlayerGameState>> gamePlayers;
        for (const auto& rp : snapshot.players) {
            auto gp = std::make_shared<cabogame::PlayerGameState>();
            gp->playerId = rp.playerId;
            gp->nickname = rp.nickname;
            gp->characterId = rp.characterId;
            gp->seatId = rp.seatId;
            gp->conn = rp.conn;
            gp->isConnected = rp.isConnected;
            gp->totalScore = rp.totalScore;
            gamePlayers.push_back(gp);
        }

        gameService_.startGame(roomId, gamePlayers, snapshot.hostPlayerId);
    }

    void onConnection(const TcpConnectionPtr& conn) {
        if (conn->connected()) {
            LOG_INFO("GameServer - connection UP : %s",
                     conn->peerAddress().toIpPort().c_str());
            std::lock_guard<std::mutex> lock(codecsMutex_);
            codecs_[conn.get()] = std::make_shared<game::WebSocketCodec>();
        } else {
            LOG_INFO("GameServer - connection DOWN : %s",
                     conn->peerAddress().toIpPort().c_str());
            {
                std::lock_guard<std::mutex> lock(codecsMutex_);
                codecs_.erase(conn.get());
            }
            gameService_.onConnectionClosed(conn);
            roomService_.onConnectionClosed(conn);
        }
    }

    void onMessage(const TcpConnectionPtr& conn,
                   Buffer* buf, Timestamp /*time*/) {
        std::string raw = buf->retrieveAllAsString();
        std::shared_ptr<game::WebSocketCodec> codec;
        {
            std::lock_guard<std::mutex> lock(codecsMutex_);
            auto it = codecs_.find(conn.get());
            if (it == codecs_.end()) return;
            codec = it->second;
        }

        codec->feedBytes(raw.data(), raw.size(),
            [this, conn](const std::vector<uint8_t>& payload) {
                dispatcher_.dispatch(conn, payload);
            },
            [conn](const std::string& data) {
                conn->send(data);
            },
            [conn]() {
                conn->shutdown();
            });
    }

    TcpServer server_;
    EventLoop* loop_;
    game::MessageDispatcher dispatcher_;
    game::RoomService roomService_;
    cabogame::GameService gameService_;
    std::mutex codecsMutex_;
    std::unordered_map<const TcpConnection*, std::shared_ptr<game::WebSocketCodec>> codecs_;
};

int main(int argc, char* argv[]) {
    int port = 8888;
    if (argc > 1) {
        port = std::stoi(argv[1]);
    }

    LOG_INFO("GameServer starting on port %d ...", port);

    EventLoop loop;
    InetAddress addr(static_cast<uint16_t>(port));
    GameServer server(&loop, addr, "GameServer");
    server.start();

    LOG_INFO("GameServer listening on port %d", port);
    loop.loop();

    return 0;
}
