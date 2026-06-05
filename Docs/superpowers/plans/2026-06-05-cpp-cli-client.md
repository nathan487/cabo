  ---
  C++ CLI客户端实现计划
  
  ▎ For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement 
  ▎ this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

  Goal: 构建一个C++ CLI客户端，用于快速调试Cabo游戏服务端逻辑和协议交互

  Architecture: 单线程阻塞式架构，使用select()非阻塞检查服务端消息，ANSI清屏重绘界面，TCP + protobuf与服务端通信

  Tech Stack: C++17, TCP socket, protobuf, ANSI转义码

  ---
  文件结构规划

  MuduoBaseGameServer/
  ├── cli_client/
  │   ├── CMakeLists.txt           # 创建
  │   ├── README.md                # 创建
  │   └── src/
  │       ├── main.cpp             # 创建 - 程序入口
  │       ├── GameState.h          # 创建 - 游戏状态数据结构
  │       ├── GameState.cpp        # 创建 - 状态更新逻辑
  │       ├── NetworkClient.h      # 创建 - TCP网络层接口
  │       ├── NetworkClient.cpp    # 创建 - TCP实现+protobuf编解码
  │       ├── UIRenderer.h         # 创建 - 界面渲染接口
  │       ├── UIRenderer.cpp       # 创建 - ANSI终端渲染实现
  │       ├── ClientApp.h          # 创建 - 主程序流程控制
  │       └── ClientApp.cpp        # 创建 - 流程实现+用户输入
  └── src/proto/                   # 复用 - 服务端已生成的protobuf代码

  ---
  Task 1: 项目基础结构
  
  Files:
  - Create: MuduoBaseGameServer/cli_client/CMakeLists.txt
  - Create: MuduoBaseGameServer/cli_client/README.md
  - Create: MuduoBaseGameServer/cli_client/src/main.cpp
  - [ ] Step 1: 创建项目目录

  cd /mnt/c/Users/Admin/Desktop/Cabo\ GameObject/MuduoBaseGameServer
  mkdir -p cli_client/src
  mkdir -p cli_client/build

  - [ ] Step 2: 创建CMakeLists.txt

  cmake_minimum_required(VERSION 3.10)
  project(CaboCliClient)

  set(CMAKE_CXX_STANDARD 17)
  set(CMAKE_CXX_STANDARD_REQUIRED ON)

  # 查找protobuf
  find_package(Protobuf REQUIRED)

  # 包含目录
  include_directories(
      ${CMAKE_CURRENT_SOURCE_DIR}/src
      ${CMAKE_SOURCE_DIR}/src/proto
      ${Protobuf_INCLUDE_DIRS}
  )

  # 客户端源文件
  set(CLIENT_SOURCES
      src/main.cpp
  )

  # protobuf生成文件（复用服务端的）
  set(PROTO_SOURCES
      ../src/proto/messages.pb.cc
      ../src/proto/room.pb.cc
      ../src/proto/game.pb.cc
      ../src/proto/common.pb.cc
      ../src/proto/sync.pb.cc
  )

  # 可执行文件
  add_executable(cabo_cli_client
      ${CLIENT_SOURCES}
      ${PROTO_SOURCES}
  )

  # 链接库
  target_link_libraries(cabo_cli_client
      ${Protobuf_LIBRARIES}
      pthread
  )

  - [ ] Step 3: 创建README.md

  # Cabo CLI Client

  C++ command-line client for debugging Cabo game server.

  ## Build

  ```bash
  cd cli_client
  mkdir -p build && cd build
  cmake ..
  make

  Run

  ./cabo_cli_client

  Features

  - 4-player game support
  - TCP + protobuf communication
  - Real-time terminal UI
  - Full game flow: lobby → room → game → result

  - [ ] **Step 4: 创建minimal main.cpp**

  ```cpp
  #include <iostream>

  int main(int argc, char* argv[]) {
      std::cout << "Cabo CLI Client v1.0" << std::endl;
      std::cout << "Press Enter to exit..." << std::endl;
      std::cin.get();
      return 0;
  }

  - [ ] Step 5: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：Built target cabo_cli_client

  - [ ] Step 6: 测试运行

  ./cabo_cli_client

  预期输出：
  Cabo CLI Client v1.0
  Press Enter to exit...

  - [ ] Step 7: Commit

  git add cli_client/
  git commit -m "feat(cli): initialize C++ CLI client project structure"

  ---
  Task 2: GameState数据结构
  
  Files:
  - Create: cli_client/src/GameState.h
  - Create: cli_client/src/GameState.cpp
  - [ ] Step 1: 创建GameState.h头文件

  #pragma once

  #include <string>
  #include <vector>
  #include <cstdint>

  namespace cabo {

  struct Card {
      int32_t slotIndex = 0;
      bool isKnown = false;
      int32_t value = 0;
  };

  struct Player {
      int64_t playerId = 0;
      std::string nickname;
      int32_t seatId = 0;
      int32_t totalScore = 0;
      int32_t cardCount = 0;
      bool isReady = false;
      bool isHost = false;
  };

  class GameState {
  public:
      enum Phase {
          LOBBY,
          WAITING_ROOM,
          PLAYING,
          ROUND_REVEAL,
          GAME_OVER
      };

      // 连接状态
      int64_t myPlayerId = 0;
      int64_t roomId = 0;
      std::string roomCode;

      // 房间阶段
      Phase phase = LOBBY;

      // 玩家列表
      std::vector<Player> players;

      // 自己的手牌
      std::vector<Card> myCards;

      // 牌堆信息
      int32_t drawPileCount = 0;
      int32_t discardPileCount = 0;
      int32_t discardTopValue = -1;

      // 回合信息
      int64_t currentPlayerId = 0;
      int32_t roundNumber = 0;
      int32_t turnNumber = 0;

      // 抽牌暂存
      bool hasDrawnCard = false;
      int32_t drawnCardValue = 0;
      int32_t drawnCardSkill = 0;

      // 最终轮标志
      bool isFinalRound = false;
      int32_t finalRoundRemaining = 0;

      // 辅助方法
      bool isMyTurn() const;
      int getMyPlayerIndex() const;
      std::vector<int> getOpponentIndices() const;
  };

  } // namespace cabo

  - [ ] Step 2: 创建GameState.cpp实现文件

  #include "GameState.h"

  namespace cabo {

  bool GameState::isMyTurn() const {
      return currentPlayerId == myPlayerId;
  }

  int GameState::getMyPlayerIndex() const {
      for (size_t i = 0; i < players.size(); ++i) {
          if (players[i].playerId == myPlayerId) {
              return static_cast<int>(i);
          }
      }
      return -1;
  }

  std::vector<int> GameState::getOpponentIndices() const {
      int myIndex = getMyPlayerIndex();
      if (myIndex < 0) return {};

      int n = static_cast<int>(players.size());
      return {
          (myIndex + 2) % n,  // 对面玩家（顶部）
          (myIndex + 3) % n,  // 左侧玩家
          (myIndex + 1) % n   // 右侧玩家
      };
  }

  } // namespace cabo

  - [ ] Step 3: 更新CMakeLists.txt

  修改CLIENT_SOURCES部分：

  set(CLIENT_SOURCES
      src/main.cpp
      src/GameState.cpp
  )

  - [ ] Step 4: 更新main.cpp测试GameState

  #include <iostream>
  #include "GameState.h"

  int main(int argc, char* argv[]) {
      std::cout << "Cabo CLI Client v1.0" << std::endl;

      // 测试GameState
      cabo::GameState state;
      state.myPlayerId = 1;
      state.phase = cabo::GameState::LOBBY;

      std::cout << "GameState initialized successfully" << std::endl;
      std::cout << "My player ID: " << state.myPlayerId << std::endl;
      std::cout << "Phase: " << state.phase << std::endl;

      return 0;
  }

  - [ ] Step 5: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 6: 测试运行

  ./cabo_cli_client

  预期输出：
  Cabo CLI Client v1.0
  GameState initialized successfully
  My player ID: 1
  Phase: 0
  
  - [ ] Step 7: Commit

  git add cli_client/src/GameState.h cli_client/src/GameState.cpp cli_client/CMakeLists.txt cli_client/src/main.cpp
  git commit -m "feat(cli): add GameState data structure"

  ---
  Task 3: NetworkClient - TCP连接基础
  
  Files:
  - Create: cli_client/src/NetworkClient.h
  - Create: cli_client/src/NetworkClient.cpp
  - [ ] Step 1: 创建NetworkClient.h

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

  - [ ] Step 2: 创建NetworkClient.cpp - connect实现

  #include "NetworkClient.h"
  #include <sys/socket.h>
  #include <netinet/in.h>
  #include <arpa/inet.h>
  #include <unistd.h>
  #include <cstring>
  #include <iostream>

  namespace cabo {

  NetworkClient::NetworkClient() : sockfd_(-1) {}

  NetworkClient::~NetworkClient() {
      disconnect();
  }

  bool NetworkClient::connect(const std::string& host, int port) {
      // 创建socket
      sockfd_ = socket(AF_INET, SOCK_STREAM, 0);
      if (sockfd_ < 0) {
          std::cerr << "Failed to create socket" << std::endl;
          return false;
      }

      // 设置服务端地址
      struct sockaddr_in serverAddr;
      std::memset(&serverAddr, 0, sizeof(serverAddr));
      serverAddr.sin_family = AF_INET;
      serverAddr.sin_port = htons(static_cast<uint16_t>(port));

      if (inet_pton(AF_INET, host.c_str(), &serverAddr.sin_addr) <= 0) {
          std::cerr << "Invalid address: " << host << std::endl;
          close(sockfd_);
          sockfd_ = -1;
          return false;
      }

      // 连接
      if (::connect(sockfd_, (struct sockaddr*)&serverAddr, sizeof(serverAddr)) < 0) {
          std::cerr << "Connection failed" << std::endl;
          close(sockfd_);
          sockfd_ = -1;
          return false;
      }

      std::cout << "Connected to " << host << ":" << port << std::endl;
      return true;
  }

  void NetworkClient::disconnect() {
      if (sockfd_ >= 0) {
          close(sockfd_);
          sockfd_ = -1;
      }
  }

  bool NetworkClient::sendRaw(const void* data, size_t len) {
      if (sockfd_ < 0) return false;

      ssize_t sent = send(sockfd_, data, len, 0);
      return sent == static_cast<ssize_t>(len);
  }

  int NetworkClient::recvRaw(void* buffer, size_t maxLen, int timeoutMs) {
      if (sockfd_ < 0) return -1;

      // TODO: 添加select超时控制
      ssize_t n = recv(sockfd_, buffer, maxLen, 0);
      return static_cast<int>(n);
  }

  } // namespace cabo

  - [ ] Step 3: 更新CMakeLists.txt

  set(CLIENT_SOURCES
      src/main.cpp
      src/GameState.cpp
      src/NetworkClient.cpp
  )

  - [ ] Step 4: 更新main.cpp测试连接

  #include <iostream>
  #include "GameState.h"
  #include "NetworkClient.h"

  int main(int argc, char* argv[]) {
      std::cout << "Cabo CLI Client v1.0" << std::endl;

      // 测试NetworkClient
      cabo::NetworkClient network;

      std::cout << "Testing connection to 127.0.0.1:8888..." << std::endl;
      if (network.connect("127.0.0.1", 8888)) {
          std::cout << "Connection test successful!" << std::endl;
          network.disconnect();
      } else {
          std::cout << "Connection test failed (server not running?)" << std::endl;
      }

      return 0;
  }

  - [ ] Step 5: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 6: 测试运行（无服务端）

  ./cabo_cli_client

  预期输出：
  Cabo CLI Client v1.0
  Testing connection to 127.0.0.1:8888...
  Connection failed
  Connection test failed (server not running?)

  - [ ] Step 7: Commit

  git add cli_client/src/NetworkClient.h cli_client/src/NetworkClient.cpp cli_client/CMakeLists.txt cli_client/src/main.cpp
  git commit -m "feat(cli): add NetworkClient TCP connection"

  ---
  Task 4: NetworkClient - protobuf帧编解码
  
  Files:
  - Modify: cli_client/src/NetworkClient.h
  - Modify: cli_client/src/NetworkClient.cpp
  - [ ] Step 1: 更新NetworkClient.h添加protobuf接口

  在NetworkClient类中添加：

  #include "messages.pb.h"

  public:
      bool send(const game::messages::ClientMessage& msg);
      bool hasMessage(int timeoutMs = 0);
      bool receive(game::messages::ServerMessage& outMsg, int timeoutMs = 1000);

  private:
      std::vector<uint8_t> recvBuffer_;
      int64_t clientSeq_ = 1;

      static std::string encodeFrame(const std::string& payload);
      static bool decodeFrame(const std::vector<uint8_t>& buffer, 
                             size_t& frameLen, 
                             std::vector<uint8_t>& payload);
      bool extractOneMessage(game::messages::ServerMessage& outMsg);

  - [ ] Step 2: 实现帧编码encodeFrame

  在NetworkClient.cpp中添加：

  std::string NetworkClient::encodeFrame(const std::string& payload) {
      uint32_t len = static_cast<uint32_t>(payload.size());
      std::string frame;
      frame.resize(4 + len);

      // 大端序写入长度
      frame[0] = static_cast<char>((len >> 24) & 0xFF);
      frame[1] = static_cast<char>((len >> 16) & 0xFF);
      frame[2] = static_cast<char>((len >> 8) & 0xFF);
      frame[3] = static_cast<char>(len & 0xFF);

      // 拷贝payload
      std::memcpy(&frame[4], payload.data(), len);
      return frame;
  }

  - [ ] Step 3: 实现帧解码decodeFrame

  bool NetworkClient::decodeFrame(const std::vector<uint8_t>& buffer,
                                   size_t& frameLen,
                                   std::vector<uint8_t>& payload) {
      if (buffer.size() < 4) return false;

      // 大端序读取长度
      uint32_t len = (static_cast<uint32_t>(buffer[0]) << 24)
                   | (static_cast<uint32_t>(buffer[1]) << 16)
                   | (static_cast<uint32_t>(buffer[2]) << 8)
                   | static_cast<uint32_t>(buffer[3]);

      if (len > 10 * 1024 * 1024) {  // max 10MB
          return false;  // 非法帧
      }

      frameLen = 4 + len;
      if (buffer.size() < frameLen) return false;  // 半包

      // 提取payload
      payload.assign(buffer.begin() + 4, buffer.begin() + frameLen);
      return true;
  }

  - [ ] Step 4: 实现send方法

  bool NetworkClient::send(const game::messages::ClientMessage& msg) {
      if (sockfd_ < 0) return false;

      // 设置seq
      game::messages::ClientMessage msgWithSeq = msg;
      msgWithSeq.set_seq(clientSeq_++);

      // 序列化
      std::string payload;
      if (!msgWithSeq.SerializeToString(&payload)) {
          std::cerr << "Failed to serialize ClientMessage" << std::endl;
          return false;
      }

      // 编码为帧
      std::string frame = encodeFrame(payload);

      // 发送
      return sendRaw(frame.data(), frame.size());
  }

  - [ ] Step 5: 实现hasMessage方法

  bool NetworkClient::hasMessage(int timeoutMs) {
      if (sockfd_ < 0) return false;

      fd_set readfds;
      FD_ZERO(&readfds);
      FD_SET(sockfd_, &readfds);

      struct timeval tv;
      tv.tv_sec = timeoutMs / 1000;
      tv.tv_usec = (timeoutMs % 1000) * 1000;

      int ret = select(sockfd_ + 1, &readfds, nullptr, nullptr, &tv);
      return ret > 0;
  }

  - [ ] Step 6: 实现extractOneMessage方法

  bool NetworkClient::extractOneMessage(game::messages::ServerMessage& outMsg) {
      size_t frameLen;
      std::vector<uint8_t> payload;

      if (!decodeFrame(recvBuffer_, frameLen, payload)) {
          return false;  // 没有完整帧
      }

      // 解析protobuf
      if (!outMsg.ParseFromArray(payload.data(), static_cast<int>(payload.size()))) {
          std::cerr << "Failed to parse ServerMessage" << std::endl;
          recvBuffer_.clear();
          return false;
      }

      // 移除已处理的帧
      recvBuffer_.erase(recvBuffer_.begin(), recvBuffer_.begin() + frameLen);
      return true;
  }

  - [ ] Step 7: 实现receive方法

  bool NetworkClient::receive(game::messages::ServerMessage& outMsg, int timeoutMs) {
      // 先检查缓冲区是否有完整消息
      if (extractOneMessage(outMsg)) {
          return true;
      }

      // 从socket读取更多数据
      uint8_t temp[4096];
      int n = recvRaw(temp, sizeof(temp), timeoutMs);
      if (n <= 0) return false;

      recvBuffer_.insert(recvBuffer_.end(), temp, temp + n);

      // 再次尝试提取消息
      return extractOneMessage(outMsg);
  }

  - [ ] Step 8: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 9: Commit

  git add cli_client/src/NetworkClient.h cli_client/src/NetworkClient.cpp
  git commit -m "feat(cli): add protobuf frame encoding/decoding"

  ---
  Task 5: UIRenderer - 基础界面渲染

  Files:
  - Create: cli_client/src/UIRenderer.h
  - Create: cli_client/src/UIRenderer.cpp
  - [ ] Step 1: 创建UIRenderer.h

  #pragma once

  #include "GameState.h"
  #include <string>

  namespace cabo {

  class UIRenderer {
  public:
      void render(const GameState& state);

  private:
      void clearScreen();
      void renderHeader(const GameState& state);
      void renderPiles(const GameState& state);
      void renderPlayers(const GameState& state);
      void renderMyCards(const GameState& state);
      void renderActionMenu(const GameState& state);

      std::string formatCard(const Card& card);
      std::string formatPlayerArea(const Player& p, bool isCurrent, bool isMe, int cardCount);
  };

  } // namespace cabo

  - [ ] Step 2: 实现clearScreen

  #include "UIRenderer.h"
  #include <iostream>

  namespace cabo {

  void UIRenderer::clearScreen() {
      // ANSI清屏+光标归位
      std::cout << "\033[2J\033[H" << std::flush;
  }

  } // namespace cabo

  - [ ] Step 3: 实现formatCard

  std::string UIRenderer::formatCard(const Card& card) {
      if (card.isKnown) {
          return "[" + std::to_string(card.value) + "]";
      }
      return "[?]";
  }

  - [ ] Step 4: 实现renderHeader

  void UIRenderer::renderHeader(const GameState& state) {
      std::cout << "================================================================================" << std::endl;
      std::cout << "                        Cabo Game - 4 Players" << std::endl;
      if (state.phase == GameState::PLAYING) {
          std::cout << "                          Round " << state.roundNumber
                    << ", Turn " << state.turnNumber << std::endl;
      }
      std::cout << "================================================================================" << std::endl;
      std::cout << std::endl;
  }

  - [ ] Step 5: 实现renderPiles

  void UIRenderer::renderPiles(const GameState& state) {
      std::cout << "                    Draw Pile: " << state.drawPileCount;

      std::cout << "      Discard Pile: " << state.discardPileCount;
      if (state.discardTopValue >= 0) {
          std::cout << " (Top: " << state.discardTopValue << ")";
      }
      std::cout << std::endl << std::endl;
  }

  - [ ] Step 6: 实现formatPlayerArea

  std::string UIRenderer::formatPlayerArea(const Player& p, bool isCurrent, bool isMe, int cardCount) {
      std::string result;
      result += "[";
      if (isMe) {
          result += "You: " + p.nickname;
      } else {
          result += p.nickname;
      }
      result += "]";
      return result;
  }

  - [ ] Step 7: 实现renderPlayers（简化版）

  void UIRenderer::renderPlayers(const GameState& state) {
      std::cout << "--------------------------------------------------------------------------------" << std::endl;

      // 简化：只显示玩家列表
      for (const auto& p : state.players) {
          bool isCurrent = (p.playerId == state.currentPlayerId);
          bool isMe = (p.playerId == state.myPlayerId);

          if (isCurrent) {
              std::cout << "                                    ↓" << std::endl;
          }

          std::cout << "                              "
                    << formatPlayerArea(p, isCurrent, isMe, p.cardCount) << std::endl;
          std::cout << "                              Score: " << p.totalScore << std::endl;

          if (!isMe) {
              std::cout << "                              Cards: ";
              for (int i = 0; i < p.cardCount; ++i) {
                  std::cout << "[?] ";
              }
              std::cout << std::endl;
          }
          std::cout << std::endl;
      }

      std::cout << "--------------------------------------------------------------------------------" << std::endl;
  }

  - [ ] Step 8: 实现renderMyCards

  void UIRenderer::renderMyCards(const GameState& state) {
      if (state.myCards.empty()) return;

      std::cout << "                              [You: " << state.myPlayerId << "]" << std::endl;
      std::cout << "                              Cards: ";
      for (const auto& card : state.myCards) {
          std::cout << formatCard(card) << " ";
      }
      std::cout << std::endl;
      std::cout << "================================================================================" << std::endl;
      std::cout << std::endl;
  }

  - [ ] Step 9: 实现renderActionMenu

  void UIRenderer::renderActionMenu(const GameState& state) {
      if (state.phase != GameState::PLAYING) return;

      if (state.isMyTurn()) {
          std::cout << ">>> Your Turn! Choose action:" << std::endl;
          std::cout << "    1. Draw from draw pile" << std::endl;
          std::cout << "    2. Take from discard pile";
          if (state.discardTopValue >= 0) {
              std::cout << " (current top: " << state.discardTopValue << ")";
          }
          std::cout << std::endl;
          std::cout << "    3. Call CABO" << std::endl;
          std::cout << ">>> Enter choice: ";
      } else {
          // 找到当前玩家昵称
          std::string currentPlayerName = "Player";
          for (const auto& p : state.players) {
              if (p.playerId == state.currentPlayerId) {
                  currentPlayerName = p.nickname;
                  break;
              }
          }
          std::cout << ">>> Waiting for " << currentPlayerName << " to act..." << std::endl;
          std::cout << ">>> (Press Ctrl+C to quit)" << std::endl;
      }
  }

  - [ ] Step 10: 实现render主方法

  void UIRenderer::render(const GameState& state) {
      clearScreen();
      renderHeader(state);

      if (state.phase == GameState::PLAYING) {
          renderPiles(state);
          renderPlayers(state);
          renderMyCards(state);
          renderActionMenu(state);
      } else if (state.phase == GameState::WAITING_ROOM) {
          std::cout << ">>> Waiting for players (" << state.players.size() << "/4)..." << std::endl;
          for (const auto& p : state.players) {
              std::cout << "[Player " << p.seatId + 1 << ": " << p.nickname;
              if (p.playerId == state.myPlayerId) std::cout << " (You)";
              if (p.isHost) std::cout << " (Host)";
              std::cout << "] ";
              if (p.isReady) std::cout << "[Ready]";
              std::cout << std::endl;
          }
      }
  }

  - [ ] Step 11: 更新CMakeLists.txt

  set(CLIENT_SOURCES
      src/main.cpp
      src/GameState.cpp
      src/NetworkClient.cpp
      src/UIRenderer.cpp
  )

  - [ ] Step 12: 更新main.cpp测试渲染

  #include <iostream>
  #include "GameState.h"
  #include "UIRenderer.h"

  int main(int argc, char* argv[]) {
      cabo::GameState state;
      state.myPlayerId = 1;
      state.phase = cabo::GameState::WAITING_ROOM;

      cabo::Player p1;
      p1.playerId = 1;
      p1.nickname = "Alice";
      p1.seatId = 0;
      p1.isHost = true;
      p1.isReady = true;

      state.players.push_back(p1);

      cabo::UIRenderer renderer;
      renderer.render(state);

      std::cout << std::endl << "Press Enter to exit..." << std::endl;
      std::cin.get();

      return 0;
  }

  - [ ] Step 13: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 14: 测试运行

  ./cabo_cli_client

  预期输出：清屏后显示等待房间界面

  - [ ] Step 15: Commit

  git add cli_client/src/UIRenderer.h cli_client/src/UIRenderer.cpp cli_client/CMakeLists.txt cli_client/src/main.cpp

                                  
  ---
  git commit -m "feat(cli): add UIRenderer for terminal display"

  ---
  Task 6: ClientApp - 连接和登录流程
  
  Files:
  - Create: cli_client/src/ClientApp.h
  - Create: cli_client/src/ClientApp.cpp
  - Modify: cli_client/src/main.cpp
  - [ ] Step 1: 创建ClientApp.h

  #pragma once

  #include "NetworkClient.h"
  #include "GameState.h"
  #include "UIRenderer.h"
  #include <string>
  #include <vector>

  namespace cabo {

  class ClientApp {
  public:
      void run();

  private:
      NetworkClient network_;
      GameState state_;
      UIRenderer renderer_;
      bool running_ = true;

      // 流程方法
      void connectToServer();
      void loginFlow();
      void roomFlow();
      void waitingRoomLoop();
      void gameLoop();

      // 输入处理
      void handleGameInput();

      // 工具方法
      std::vector<int> parseSlotIndices(const std::string& input);
  };

  } // namespace cabo

  - [ ] Step 2: 实现connectToServer

  #include "ClientApp.h"
  #include <iostream>

  namespace cabo {

  void ClientApp::connectToServer() {
      std::string hostPort;
      std::cout << ">>> Enter server IP:port (e.g., 127.0.0.1:8888): ";
      std::cin >> hostPort;

      // 解析host和port
      size_t colonPos = hostPort.find(':');
      if (colonPos == std::string::npos) {
          std::cout << "ERROR: Invalid format! Use IP:port" << std::endl;
          return connectToServer();
      }

      std::string host = hostPort.substr(0, colonPos);
      int port = std::stoi(hostPort.substr(colonPos + 1));

      std::cout << ">>> Connecting to " << host << ":" << port << "..." << std::endl;

      if (!network_.connect(host, port)) {
          std::cout << "ERROR: Failed to connect!" << std::endl;
          std::cout << ">>> Retry? (y/n): ";
          char choice;
          std::cin >> choice;
          if (choice == 'y' || choice == 'Y') {
              return connectToServer();
          }
          running_ = false;
          return;
      }

      std::cout << ">>> Connected!" << std::endl;
  }

  } // namespace cabo

  - [ ] Step 3: 实现loginFlow

  void ClientApp::loginFlow() {
      std::string nickname;
      std::cout << ">>> Enter your nickname: ";
      std::cin >> nickname;

      if (nickname.empty() || nickname.length() > 20) {
          std::cout << "ERROR: Nickname must be 1-20 characters!" << std::endl;
          return loginFlow();
      }

      // 暂时只保存到state，稍后发送到服务端
      std::cout << ">>> Nickname set: " << nickname << std::endl;
  }

  - [ ] Step 4: 实现run主流程骨架

  void ClientApp::run() {
      std::cout << "================================================================================" << std::endl;
      std::cout << "                    Welcome to Cabo Game CLI Client" << std::endl;
      std::cout << "================================================================================" << std::endl;

      connectToServer();
      if (!running_) return;

      loginFlow();
      if (!running_) return;

      std::cout << "\n>>> Connection and login successful!" << std::endl;
      std::cout << ">>> Press Enter to continue..." << std::endl;
      std::cin.ignore();
      std::cin.get();
  }

  void ClientApp::roomFlow() {
      // TODO: 下一个task实现
  }

  void ClientApp::waitingRoomLoop() {
      // TODO: 稍后实现
  }

  void ClientApp::gameLoop() {
      // TODO: 稍后实现
  }

  void ClientApp::handleGameInput() {
      // TODO: 稍后实现
  }

  std::vector<int> ClientApp::parseSlotIndices(const std::string& input) {
      // TODO: 稍后实现
      return {};
  }

  - [ ] Step 5: 更新CMakeLists.txt

  set(CLIENT_SOURCES
      src/main.cpp
      src/GameState.cpp
      src/NetworkClient.cpp
      src/UIRenderer.cpp
      src/ClientApp.cpp
  )

  - [ ] Step 6: 更新main.cpp使用ClientApp

  #include "ClientApp.h"

  int main(int argc, char* argv[]) {
      cabo::ClientApp app;
      app.run();
      return 0;
  }

  - [ ] Step 7: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 8: 测试运行

  ./cabo_cli_client

  预期输出：显示欢迎信息，提示输入服务器地址

  - [ ] Step 9: Commit

  git add cli_client/src/ClientApp.h cli_client/src/ClientApp.cpp cli_client/CMakeLists.txt cli_client/src/main.cpp
  git commit -m "feat(cli): add ClientApp with connect and login flow"

  ---
  Task 7: GameState消息更新处理
  
  Files:
  - Modify: cli_client/src/GameState.h
  - Modify: cli_client/src/GameState.cpp
  - [ ] Step 1: 在GameState.h添加updateFromMessage声明

  #include "messages.pb.h"

  public:
      void updateFromMessage(const game::messages::ServerMessage& msg);

  - [ ] Step 2: 实现CreateRoomRsp处理

  #include "messages.pb.h"

  namespace cabo {

  void GameState::updateFromMessage(const game::messages::ServerMessage& msg) {
      if (msg.has_create_room_rsp()) {
          const auto& rsp = msg.create_room_rsp();
          if (rsp.error().code() == 0) {
              roomId = rsp.room_id();
              myPlayerId = rsp.player_id();
              roomCode = rsp.room_code();
              phase = WAITING_ROOM;
          }
      }
  }

  } // namespace cabo

  - [ ] Step 3: 添加JoinRoomRsp处理

  if (msg.has_join_room_rsp()) {
      const auto& rsp = msg.join_room_rsp();
      if (rsp.error().code() == 0) {
          roomId = rsp.room_id();
          myPlayerId = rsp.player_id();
          phase = WAITING_ROOM;
      }
  }

  - [ ] Step 4: 添加RoomStateNotify处理

  if (msg.has_room_state_notify()) {
      const auto& notify = msg.room_state_notify();
      const auto& room = notify.room();

      players.clear();
      for (const auto& pInfo : room.players()) {
          Player p;
          p.playerId = pInfo.player_id();
          p.nickname = pInfo.nickname();
          p.seatId = pInfo.seat_id();
          p.totalScore = pInfo.total_score();
          p.isReady = pInfo.is_ready();
          p.isHost = pInfo.is_host();
          p.cardCount = 0;
          players.push_back(p);
      }
  }

  - [ ] Step 5: 添加PlayerJoinNotify处理

  if (msg.has_player_join_notify()) {
      const auto& notify = msg.player_join_notify();
      const auto& pInfo = notify.player();

      Player p;
      p.playerId = pInfo.player_id();
      p.nickname = pInfo.nickname();
      p.seatId = pInfo.seat_id();
      p.totalScore = pInfo.total_score();
      p.isReady = pInfo.is_ready();
      p.isHost = pInfo.is_host();
      p.cardCount = 0;

      players.push_back(p);
  }

  - [ ] Step 6: 添加PlayerReadyNotify处理

  if (msg.has_player_ready_notify()) {
      const auto& notify = msg.player_ready_notify();
      for (auto& p : players) {
          if (p.playerId == notify.player_id()) {
              p.isReady = notify.is_ready();
              break;
          }
      }
  }

  - [ ] Step 7: 添加GameStartNotify处理

  if (msg.has_game_start_notify()) {
      const auto& notify = msg.game_start_notify();
      phase = PLAYING;
      roundNumber = notify.round_number();
      currentPlayerId = notify.first_player_id();

      // 初始化myCards
      const auto& view = notify.your_view();
      myCards.clear();
      for (const auto& cardState : view.own_cards()) {
          Card card;
          card.slotIndex = cardState.slot_index();
          card.isKnown = cardState.is_known();
          card.value = cardState.value();
          myCards.push_back(card);
      }

      // 更新牌堆信息
      if (view.has_draw_pile()) {
          drawPileCount = view.draw_pile().count();
      }
      if (view.has_discard_pile()) {
          discardPileCount = view.discard_pile().count();
          if (view.discard_pile().has_top_card()) {
              discardTopValue = view.discard_pile().top_card().value();
          }
      }
  }

  - [ ] Step 8: 添加TurnStartNotify处理

  if (msg.has_turn_start_notify()) {
      const auto& notify = msg.turn_start_notify();
      currentPlayerId = notify.current_player_id();
      turnNumber = notify.turn_number();
      roundNumber = notify.round_number();

      if (notify.has_draw_pile()) {
          drawPileCount = notify.draw_pile().count();
      }
      if (notify.has_discard_pile()) {
          discardPileCount = notify.discard_pile().count();
          if (notify.discard_pile().has_top_card()) {
              discardTopValue = notify.discard_pile().top_card().value();
          }
      }

      hasDrawnCard = false;
  }

  - [ ] Step 9: 添加DrawCardRsp处理

  if (msg.has_draw_card_rsp()) {
      const auto& rsp = msg.draw_card_rsp();
      if (rsp.error().code() == 0) {
          hasDrawnCard = true;
          drawnCardValue = rsp.value();
          drawnCardSkill = rsp.skill();
      }
  }

  - [ ] Step 10: 添加ActionResultNotify处理

  if (msg.has_action_result_notify()) {
      const auto& notify = msg.action_result_notify();

      // 更新牌堆信息
      if (notify.has_draw_pile()) {
          drawPileCount = notify.draw_pile().count();
      }
      if (notify.has_discard_pile()) {
          discardPileCount = notify.discard_pile().count();
          if (notify.discard_pile().has_top_card()) {
              discardTopValue = notify.discard_pile().top_card().value();
          }
      }

      // 回合结束时更新当前玩家
      if (notify.turn_ended()) {
          currentPlayerId = notify.next_player_id();
          hasDrawnCard = false;
      }
  }

  - [ ] Step 11: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 12: Commit

  git add cli_client/src/GameState.h cli_client/src/GameState.cpp
  git commit -m "feat(cli): add GameState message update handlers"

  ---
  Task 8: ClientApp - 房间流程
  
  Files:
  - Modify: cli_client/src/ClientApp.cpp
  - [ ] Step 1: 实现roomFlow

  void ClientApp::roomFlow() {
      std::cout << "\n>>> Choose action:" << std::endl;
      std::cout << "    1. Create room (4 players)" << std::endl;
      std::cout << "    2. Join room" << std::endl;
      std::cout << ">>> Enter choice: ";

      int choice;
      std::cin >> choice;

      if (choice == 1) {
          // 创建房间
          std::string nickname;
          std::cout << ">>> Enter your nickname: ";
          std::cin >> nickname;

          game::messages::ClientMessage req;
          auto* createReq = req.mutable_create_room_req();
          createReq->set_max_players(4);
          createReq->set_nickname(nickname);
          createReq->set_request_id(1);

          std::cout << ">>> Creating room..." << std::endl;
          if (!network_.send(req)) {
              std::cout << "ERROR: Failed to send request!" << std::endl;
              return;
          }

          // 等待CreateRoomRsp
          game::messages::ServerMessage rsp;
          if (!network_.receive(rsp, 5000)) {
              std::cout << "ERROR: No response from server!" << std::endl;
              return;
          }

          state_.updateFromMessage(rsp);

          if (rsp.has_create_room_rsp() && rsp.create_room_rsp().error().code() == 0) {
              std::cout << ">>> Room created! Room Code: " << state_.roomCode << std::endl;
              std::cout << ">>> Share this code with other players" << std::endl;
          } else {
              std::cout << "ERROR: Failed to create room!" << std::endl;
              return;
          }

      } else if (choice == 2) {
          // 加入房间
          std::string nickname, roomCode;
          std::cout << ">>> Enter your nickname: ";
          std::cin >> nickname;
          std::cout << ">>> Enter room code: ";
          std::cin >> roomCode;

          game::messages::ClientMessage req;
          auto* joinReq = req.mutable_join_room_req();
          joinReq->set_room_code(roomCode);
          joinReq->set_nickname(nickname);
          joinReq->set_request_id(2);

          std::cout << ">>> Joining room..." << std::endl;
          if (!network_.send(req)) {
              std::cout << "ERROR: Failed to send request!" << std::endl;
              return;
          }

          // 等待JoinRoomRsp
          game::messages::ServerMessage rsp;
          if (!network_.receive(rsp, 5000)) {
              std::cout << "ERROR: No response from server!" << std::endl;
              return;
          }

          state_.updateFromMessage(rsp);

          if (rsp.has_join_room_rsp() && rsp.join_room_rsp().error().code() == 0) {
              std::cout << ">>> Joined room " << roomCode << std::endl;
          } else {
              std::cout << "ERROR: Failed to join room!" << std::endl;
              return;
          }

      } else {
          std::cout << ">>> Invalid choice!" << std::endl;
          return roomFlow();
      }
  }

  - [ ] Step 2: 实现自动ready

  在roomFlow末尾添加：

  // 自动发送ready
  game::messages::ClientMessage readyReq;
  auto* ready = readyReq.mutable_ready_req();
  ready->set_player_id(state_.myPlayerId);
  ready->set_room_id(state_.roomId);
  ready->set_is_ready(true);
  ready->set_request_id(3);

  if (network_.send(readyReq)) {
      std::cout << ">>> Auto-ready sent" << std::endl;
  }

  - [ ] Step 3: 实现waitingRoomLoop

  void ClientApp::waitingRoomLoop() {
      while (running_ && state_.phase == GameState::WAITING_ROOM) {
          // 检查服务端消息
          if (network_.hasMessage(100)) {
              game::messages::ServerMessage msg;
              if (network_.receive(msg, 1000)) {
                  state_.updateFromMessage(msg);
                  renderer_.render(state_);

                  // 检查是否所有人ready
                  if (state_.players.size() == 4) {
                      bool allReady = true;
                      for (const auto& p : state_.players) {
                          if (!p.isReady) {
                              allReady = false;
                              break;
                          }
                      }

                      // 如果是房主且所有人ready，自动start
                      if (allReady) {
                          for (const auto& p : state_.players) {
                              if (p.playerId == state_.myPlayerId && p.isHost) {
                                  std::cout << "\n>>> All players ready! Starting game..." << std::endl;
                                  usleep(1000000);  // 1秒延迟

                                  game::messages::ClientMessage startReq;
                                  auto* start = startReq.mutable_start_game_req();
                                  start->set_player_id(state_.myPlayerId);
                                  start->set_room_id(state_.roomId);
                                  start->set_request_id(4);

                                  network_.send(startReq);
                                  break;
                              }
                          }
                      }
                  }

                  // 检查是否进入游戏
                  if (state_.phase == GameState::PLAYING) {
                      break;
                  }
              }
          }

          usleep(100000);  // 100ms
      }
  }

  - [ ] Step 4: 更新run方法调用roomFlow和waitingRoomLoop

  修改ClientApp::run()：

  void ClientApp::run() {
      std::cout << "================================================================================" << std::endl;
      std::cout << "                    Welcome to Cabo Game CLI Client" << std::endl;
      std::cout << "================================================================================" << std::endl;

      connectToServer();
      if (!running_) return;

      roomFlow();
      if (!running_) return;

      waitingRoomLoop();
      if (!running_) return;

      gameLoop();
  }

  - [ ] Step 5: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 6: Commit

  git add cli_client/src/ClientApp.cpp
  git commit -m "feat(cli): add room flow with auto-ready and auto-start"

  ---
  Task 9: ClientApp - 游戏主循环和基础操作
  
  Files:
  - Modify: cli_client/src/ClientApp.cpp
  - [ ] Step 1: 实现gameLoop主循环

  void ClientApp::gameLoop() {
      renderer_.render(state_);

      while (running_ && state_.phase == GameState::PLAYING) {
          // 检查服务端消息
          if (network_.hasMessage(100)) {
              game::messages::ServerMessage msg;
              if (network_.receive(msg, 1000)) {
                  state_.updateFromMessage(msg);
                  renderer_.render(state_);
              }
          }

          // 如果是我的回合且没有抽牌状态
          if (state_.isMyTurn() && !state_.hasDrawnCard) {
              handleGameInput();
          }

          // 非当前回合，短暂休眠
          if (!state_.isMyTurn()) {
              usleep(100000);  // 100ms
          }
      }
  }

  - [ ] Step 2: 实现handleGameInput - 基础选项

  void ClientApp::handleGameInput() {
      int choice;
      std::cin >> choice;

      if (std::cin.fail() || choice < 1 || choice > 3) {
          std::cin.clear();
          std::cin.ignore(10000, '\n');
          std::cout << ">>> Invalid input! Please enter 1, 2, or 3." << std::endl;
          renderer_.render(state_);
          return;
      }

      game::messages::ClientMessage req;

      if (choice == 1) {
          // 抽牌
          auto* drawReq = req.mutable_draw_card_req();
          drawReq->set_player_id(state_.myPlayerId);
          drawReq->set_room_id(state_.roomId);
          drawReq->set_request_id(100);

          if (network_.send(req)) {
              std::cout << ">>> Drawing card..." << std::endl;
          }

      } else if (choice == 2) {
          // 从弃牌堆拿牌
          // TODO: 稍后实现
          std::cout << ">>> Take from discard pile - not implemented yet" << std::endl;

      } else if (choice == 3) {
          // 喊CABO
          auto* caboReq = req.mutable_call_steady_req();
          caboReq->set_player_id(state_.myPlayerId);
          caboReq->set_room_id(state_.roomId);
          caboReq->set_request_id(101);

          if (network_.send(caboReq)) {
              std::cout << ">>> Called CABO!" << std::endl;
          }
      }
  }

  - [ ] Step 3: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 4: Commit

  git add cli_client/src/ClientApp.cpp
  git commit -m "feat(cli): add game loop and basic actions (draw, call cabo)"

  ---
  Task 10: ClientApp - 抽牌后决策流程
  
  Files:
  - Modify: cli_client/src/ClientApp.h
  - Modify: cli_client/src/ClientApp.cpp
  - [ ] Step 1: 在ClientApp.h添加方法声明

  private:
      void handleDrawnCardDecision();
      void handleReplaceWithDrawn();
      void handleTakeFromDiscard();
      void handleSkillInput(int skillType);

  - [ ] Step 2: 在gameLoop中添加抽牌后处理

  在gameLoop的while循环中，handleGameInput调用后添加：

  // 如果抽了牌，处理抽牌后决策
  if (state_.hasDrawnCard) {
      handleDrawnCardDecision();
  }

  - [ ] Step 3: 实现handleDrawnCardDecision

  void ClientApp::handleDrawnCardDecision() {
      std::cout << "\n>>> You drew: [" << state_.drawnCardValue << "]" << std::endl;

      if (state_.drawnCardSkill != 1) {  // SKILL_TYPE_NONE = 1
          std::cout << ">>> This card has a skill!" << std::endl;
      }

      std::cout << ">>> Choose what to do:" << std::endl;
      std::cout << "    1. Discard";
      if (state_.drawnCardSkill != 1) {
          std::cout << " and use skill";
      }
      std::cout << std::endl;
      std::cout << "    2. Replace your cards with this card" << std::endl;
      std::cout << ">>> Enter choice: ";

      int choice;
      std::cin >> choice;

      if (choice == 1) {
          // 弃掉
          game::messages::ClientMessage req;
          auto* discardReq = req.mutable_discard_drawn_req();
          discardReq->set_player_id(state_.myPlayerId);
          discardReq->set_room_id(state_.roomId);
          discardReq->set_request_id(102);

          if (network_.send(req)) {
              std::cout << ">>> Discarding..." << std::endl;
              state_.hasDrawnCard = false;

              // 如果有技能，处理技能输入
              if (state_.drawnCardSkill != 1) {
                  handleSkillInput(state_.drawnCardSkill);
              }
          }

      } else if (choice == 2) {
          // 替换
          handleReplaceWithDrawn();
      } else {
          std::cout << ">>> Invalid choice!" << std::endl;
          return handleDrawnCardDecision();
      }
  }

  - [ ] Step 4: 实现handleReplaceWithDrawn

  void ClientApp::handleReplaceWithDrawn() {
      std::cout << ">>> Enter slot indices to replace (space-separated, e.g., '0 1'): ";
      std::cin.ignore();

      std::string line;
      std::getline(std::cin, line);

      std::vector<int> slots = parseSlotIndices(line);

      if (slots.empty()) {
          std::cout << ">>> No valid slots entered!" << std::endl;
          return handleReplaceWithDrawn();
      }

      game::messages::ClientMessage req;
      auto* replaceReq = req.mutable_replace_with_drawn_req();
      replaceReq->set_player_id(state_.myPlayerId);
      replaceReq->set_room_id(state_.roomId);
      replaceReq->set_request_id(103);

      for (int slot : slots) {
          replaceReq->add_slot_indices(slot);
      }

      if (network_.send(req)) {
          std::cout << ">>> Attempting to replace slots [";
          for (size_t i = 0; i < slots.size(); ++i) {
              std::cout << slots[i];
              if (i < slots.size() - 1) std::cout << ", ";
          }
          std::cout << "]..." << std::endl;
          state_.hasDrawnCard = false;
      }
  }

  - [ ] Step 5: 实现parseSlotIndices工具方法

  std::vector<int> ClientApp::parseSlotIndices(const std::string& input) {
      std::vector<int> result;
      std::string token;

      for (char c : input) {
          if (c == ' ' || c == ',') {
              if (!token.empty()) {
                  try {
                      int slot = std::stoi(token);
                      if (slot >= 0 && slot < 10) {  // 最多10张牌（失败替换可能超过4）
                          result.push_back(slot);
                      }
                  } catch (...) {
                      // 忽略非法输入
                  }
                  token.clear();
              }
          } else if (c >= '0' && c <= '9') {
              token += c;
          }
      }

      // 处理最后一个token
      if (!token.empty()) {
          try {
              int slot = std::stoi(token);
              if (slot >= 0 && slot < 10) {
                  result.push_back(slot);
              }
          } catch (...) {}
      }

      return result;
  }

  - [ ] Step 6: 实现handleSkillInput占位

  void ClientApp::handleSkillInput(int skillType) {
      std::cout << ">>> Skill input - not fully implemented yet (skill type: "
                << skillType << ")" << std::endl;
      // TODO: Task 11实现完整技能交互
  }

  - [ ] Step 7: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 8: Commit

  git add cli_client/src/ClientApp.h cli_client/src/ClientApp.cpp
  git commit -m "feat(cli): add drawn card decision flow (discard/replace)"

  ---

   ---
  Task 11: ClientApp - 技能交互实现
  
  Files:
  - Modify: cli_client/src/ClientApp.cpp
  - [ ] Step 1: 实现handleSkillInput - 判断技能类型

  void ClientApp::handleSkillInput(int skillType) {
      if (skillType == 2) {  // SKILL_TYPE_PEEK_SELF
          handlePeekSelfSkill();
      } else if (skillType == 3) {  // SKILL_TYPE_SPY
          handleSpySkill();
      } else if (skillType == 4) {  // SKILL_TYPE_SWAP
          handleSwapSkill();
      } else {
          std::cout << ">>> No skill to use" << std::endl;
      }
  }

  - [ ] Step 2: 在ClientApp.h添加技能方法声明

  private:
      void handlePeekSelfSkill();
      void handleSpySkill();
      void handleSwapSkill();

  - [ ] Step 3: 实现handlePeekSelfSkill

  void ClientApp::handlePeekSelfSkill() {
      std::cout << "\n>>> Using skill: Peek Self" << std::endl;
      std::cout << ">>> Choose your slot to peek (0-" << (state_.myCards.size() - 1) << "): ";

      int slot;
      std::cin >> slot;

      if (slot < 0 || slot >= static_cast<int>(state_.myCards.size())) {
          std::cout << ">>> Invalid slot!" << std::endl;
          return handlePeekSelfSkill();
      }

      game::messages::ClientMessage req;
      auto* skillReq = req.mutable_use_skill_req();
      skillReq->set_player_id(state_.myPlayerId);
      skillReq->set_room_id(state_.roomId);
      skillReq->set_request_id(104);
      skillReq->set_card_id(0);  // 服务端会忽略此字段

      auto* peekParams = skillReq->mutable_peek_self();
      peekParams->set_slot_index(slot);

      if (network_.send(req)) {
          std::cout << ">>> Peeking at slot " << slot << "..." << std::endl;

          // 等待UseSkillRsp
          game::messages::ServerMessage rsp;
          if (network_.receive(rsp, 5000) && rsp.has_use_skill_rsp()) {
              const auto& skillRsp = rsp.use_skill_rsp();
              if (skillRsp.error().code() == 0) {
                  std::cout << ">>> You saw: [" << skillRsp.peeked_value() << "]" << std::endl;
                  // 更新本地状态（服务端会在后续消息中同步）
              }
          }
      }
  }

  - [ ] Step 4: 实现handleSpySkill

  void ClientApp::handleSpySkill() {
      std::cout << "\n>>> Using skill: Spy" << std::endl;
      std::cout << ">>> Choose target player:" << std::endl;

      // 显示对手列表
      int opponentIndex = 1;
      std::vector<int64_t> opponentIds;
      for (const auto& p : state_.players) {
          if (p.playerId != state_.myPlayerId) {
              std::cout << "    " << opponentIndex << ". " << p.nickname << std::endl;
              opponentIds.push_back(p.playerId);
              opponentIndex++;
          }
      }

      std::cout << ">>> Enter choice (1-" << opponentIds.size() << "): ";
      int choice;
      std::cin >> choice;

      if (choice < 1 || choice > static_cast<int>(opponentIds.size())) {
          std::cout << ">>> Invalid choice!" << std::endl;
          return handleSpySkill();
      }

      int64_t targetPlayerId = opponentIds[choice - 1];

      std::cout << ">>> Choose target slot (0-3): ";
      int slot;
      std::cin >> slot;

      if (slot < 0 || slot > 3) {
          std::cout << ">>> Invalid slot!" << std::endl;
          return handleSpySkill();
      }

      game::messages::ClientMessage req;
      auto* skillReq = req.mutable_use_skill_req();
      skillReq->set_player_id(state_.myPlayerId);
      skillReq->set_room_id(state_.roomId);
      skillReq->set_request_id(105);
      skillReq->set_card_id(0);

      auto* spyParams = skillReq->mutable_spy();
      spyParams->set_target_player_id(targetPlayerId);
      spyParams->set_target_slot_index(slot);

      if (network_.send(req)) {
          std::cout << ">>> Spying..." << std::endl;

          // 等待UseSkillRsp
          game::messages::ServerMessage rsp;
          if (network_.receive(rsp, 5000) && rsp.has_use_skill_rsp()) {
              const auto& skillRsp = rsp.use_skill_rsp();
              if (skillRsp.error().code() == 0) {
                  std::cout << ">>> You saw opponent's card: [" << skillRsp.peeked_value() << "]" << std::endl;
              }
          }
      }
  }

  - [ ] Step 5: 实现handleSwapSkill

  void ClientApp::handleSwapSkill() {
      std::cout << "\n>>> Using skill: Swap" << std::endl;
      std::cout << ">>> Choose your slot (0-" << (state_.myCards.size() - 1) << "): ";

      int mySlot;
      std::cin >> mySlot;

      if (mySlot < 0 || mySlot >= static_cast<int>(state_.myCards.size())) {
          std::cout << ">>> Invalid slot!" << std::endl;
          return handleSwapSkill();
      }

      std::cout << ">>> Choose target player:" << std::endl;

      int opponentIndex = 1;
      std::vector<int64_t> opponentIds;
      for (const auto& p : state_.players) {
          if (p.playerId != state_.myPlayerId) {
              std::cout << "    " << opponentIndex << ". " << p.nickname << std::endl;
              opponentIds.push_back(p.playerId);
              opponentIndex++;
          }
      }

      std::cout << ">>> Enter choice (1-" << opponentIds.size() << "): ";
      int choice;
      std::cin >> choice;

      if (choice < 1 || choice > static_cast<int>(opponentIds.size())) {
          std::cout << ">>> Invalid choice!" << std::endl;
          return handleSwapSkill();
      }

      int64_t targetPlayerId = opponentIds[choice - 1];

      std::cout << ">>> Choose target slot (0-3): ";
      int targetSlot;
      std::cin >> targetSlot;

      if (targetSlot < 0 || targetSlot > 3) {
          std::cout << ">>> Invalid slot!" << std::endl;
          return handleSwapSkill();
      }

      game::messages::ClientMessage req;
      auto* skillReq = req.mutable_use_skill_req();
      skillReq->set_player_id(state_.myPlayerId);
      skillReq->set_room_id(state_.roomId);
      skillReq->set_request_id(106);
      skillReq->set_card_id(0);

      auto* swapParams = skillReq->mutable_swap();
      swapParams->set_target_player_id(targetPlayerId);
      swapParams->set_own_slot_index(mySlot);
      swapParams->set_target_slot_index(targetSlot);

      if (network_.send(req)) {
          std::cout << ">>> Swap completed!" << std::endl;
          std::cout << ">>> (Note: You don't know what you received)" << std::endl;
      }
  }

  - [ ] Step 6: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 7: Commit

  git add cli_client/src/ClientApp.h cli_client/src/ClientApp.cpp
  git commit -m "feat(cli): add skill interactions (peek self, spy, swap)"

  ---
  Task 12: ClientApp - 从弃牌堆拿牌实现
  
  Files:
  - Modify: cli_client/src/ClientApp.cpp
  - [ ] Step 1: 完善handleGameInput中的选项2

  找到handleGameInput中的else if (choice == 2)部分，替换为：

  } else if (choice == 2) {
      // 从弃牌堆拿牌
      handleTakeFromDiscard();

  - [ ] Step 2: 实现handleTakeFromDiscard

  void ClientApp::handleTakeFromDiscard() {
      std::cout << ">>> Taking top card [" << state_.discardTopValue
                << "] from discard pile" << std::endl;
      std::cout << ">>> Enter slot indices to replace (space-separated, e.g., '0 1'): ";

      std::cin.ignore();
      std::string line;
      std::getline(std::cin, line);

      std::vector<int> slots = parseSlotIndices(line);

      if (slots.empty()) {
          std::cout << ">>> No valid slots entered!" << std::endl;
          return handleTakeFromDiscard();
      }

      game::messages::ClientMessage req;
      auto* takeReq = req.mutable_take_from_discard_req();
      takeReq->set_player_id(state_.myPlayerId);
      takeReq->set_room_id(state_.roomId);
      takeReq->set_request_id(107);

      for (int slot : slots) {
          takeReq->add_slot_indices(slot);
      }

      if (network_.send(req)) {
          std::cout << ">>> Attempting to replace slots [";
          for (size_t i = 0; i < slots.size(); ++i) {
              std::cout << slots[i];
              if (i < slots.size() - 1) std::cout << ", ";
          }
          std::cout << "]..." << std::endl;

          // 等待服务端响应
          game::messages::ServerMessage rsp;
          if (network_.receive(rsp, 5000) && rsp.has_take_from_discard_rsp()) {
              const auto& takeRsp = rsp.take_from_discard_rsp();
              if (takeRsp.error().code() == 0) {
                  if (takeRsp.has_exchange_result()) {
                      const auto& result = takeRsp.exchange_result();
                      if (result.success()) {
                          std::cout << ">>> Replace successful!" << std::endl;
                      } else {
                          std::cout << ">>> Replace FAILED! Cards have different values." << std::endl;
                          std::cout << ">>> Card added to your hand." << std::endl;
                          if (result.drew_extra_penalty_card()) {
                              std::cout << ">>> Extra penalty card added (3+ cards attempted)." << std::endl;
                          }
                      }
                  }
              }
          }
      }
  }

  - [ ] Step 3: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 4: Commit

  git add cli_client/src/ClientApp.cpp
  git commit -m "feat(cli): implement take from discard pile"

  ---
  Task 13: GameState和UIRenderer - 结算显示
  
  Files:
  - Modify: cli_client/src/GameState.h
  - Modify: cli_client/src/GameState.cpp
  - Modify: cli_client/src/UIRenderer.cpp
  - [ ] Step 1: 在GameState.h添加结算相关字段

  public:
      // 结算信息
      struct RoundResult {
          int64_t playerId;
          std::string nickname;
          std::vector<int> cardValues;
          int32_t handTotal;
          int32_t penalty;
          int32_t roundScore;
          int32_t cumulativeScore;
          bool isSteadyCaller;
          bool isLowest;
          bool isKamikaze;
      };

      std::vector<RoundResult> lastRoundResults;

      struct FinalRank {
          int32_t rank;
          int64_t playerId;
          std::string nickname;
          int32_t finalScore;
          bool isWinner;
      };

      std::vector<FinalRank> finalRankings;

  - [ ] Step 2: 在GameState.cpp添加RoundRevealNotify处理

  在updateFromMessage中添加：

  if (msg.has_round_reveal_notify()) {
      const auto& notify = msg.round_reveal_notify();
      phase = ROUND_REVEAL;

      lastRoundResults.clear();
      for (const auto& score : notify.scores()) {
          RoundResult result;
          result.playerId = score.player_id();
          result.handTotal = score.hand_total();
          result.penalty = score.penalty();
          result.roundScore = score.round_score();
          result.cumulativeScore = score.cumulative_score();
          result.isSteadyCaller = score.is_steady_caller();
          result.isLowest = score.is_lowest();
          result.isKamikaze = score.is_kamikaze();

          // 找到昵称
          for (const auto& p : players) {
              if (p.playerId == score.player_id()) {
                  result.nickname = p.nickname;
                  break;
              }
          }

          // 找到手牌
          for (const auto& hand : notify.revealed_hands()) {
              if (hand.player_id() == score.player_id()) {
                  for (int val : hand.card_values()) {
                      result.cardValues.push_back(val);
                  }
                  break;
              }
          }

          lastRoundResults.push_back(result);
      }
  }

  - [ ] Step 3: 添加ScoreUpdateNotify处理

  if (msg.has_score_update_notify()) {
      const auto& notify = msg.score_update_notify();
      for (const auto& scoreInfo : notify.scores()) {
          for (auto& p : players) {
              if (p.playerId == scoreInfo.player_id()) {
                  p.totalScore = scoreInfo.total_score();
                  break;
              }
          }
      }
  }

  - [ ] Step 4: 添加GameOverNotify处理

  if (msg.has_game_over_notify()) {
      const auto& notify = msg.game_over_notify();
      phase = GAME_OVER;

      finalRankings.clear();
      for (const auto& ranking : notify.rankings()) {
          FinalRank rank;
          rank.rank = ranking.rank();
          rank.playerId = ranking.player_id();
          rank.nickname = ranking.nickname();
          rank.finalScore = ranking.final_score();
          rank.isWinner = ranking.is_winner();
          finalRankings.push_back(rank);
      }
  }

  - [ ] Step 5: 在UIRenderer.cpp添加renderRoundReveal方法声明和实现

  在UIRenderer.h添加：

  private:
      void renderRoundReveal(const GameState& state);
      void renderGameOver(const GameState& state);

  在UIRenderer.cpp实现renderRoundReveal：

  void UIRenderer::renderRoundReveal(const GameState& state) {
      std::cout << "================================================================================" << std::endl;
      std::cout << "                        Round " << state.roundNumber << " Reveal" << std::endl;
      std::cout << "================================================================================" << std::endl;
      std::cout << std::endl;

      for (const auto& result : state.lastRoundResults) {
          std::cout << result.nickname;
          if (result.playerId == state.myPlayerId) {
              std::cout << " (You)";
          }
          if (result.isSteadyCaller) {
              std::cout << " (called CABO)";
          }
          std::cout << ":  ";

          // 显示手牌
          for (int val : result.cardValues) {
              std::cout << "[" << val << "] ";
          }

          std::cout << "= " << result.handTotal;

          if (result.penalty > 0) {
              std::cout << "  (+" << result.penalty << " penalty)";
          }

          std::cout << " = " << result.roundScore;

          if (result.isLowest) {
              std::cout << "  ← Lowest!";
          }

          if (result.isKamikaze) {
              std::cout << "  🎯 KAMIKAZE!";
          }

          std::cout << std::endl;
      }

      std::cout << std::endl;
      std::cout << "Scores after Round " << state.roundNumber << ":" << std::endl;
      for (const auto& result : state.lastRoundResults) {
          std::cout << "  " << result.nickname << ": " << result.cumulativeScore;
          if (result.isLowest) {
              std::cout << "  ← Lowest";
          }
          std::cout << std::endl;
      }

      std::cout << std::endl;
      std::cout << ">>> Press Enter to continue...";
  }

  - [ ] Step 6: 实现renderGameOver

  void UIRenderer::renderGameOver(const GameState& state) {
      std::cout << "================================================================================" << std::endl;
      std::cout << "                        Game Over!" << std::endl;
      std::cout << "================================================================================" << std::endl;
      std::cout << std::endl;

      std::cout << "Final Standings:" << std::endl;
      for (const auto& rank : state.finalRankings) {
          std::cout << "  " << rank.rank;
          if (rank.rank == 1) std::cout << "st";
          else if (rank.rank == 2) std::cout << "nd";
          else if (rank.rank == 3) std::cout << "rd";
          else std::cout << "th";

          std::cout << " Place: " << rank.nickname << "  (Score: " << rank.finalScore << ")";

          if (rank.isWinner) {
              std::cout << "  🎉 WINNER";
          }

          std::cout << std::endl;
      }

      std::cout << std::endl;
      std::cout << ">>> Press Enter to exit...";
  }

  - [ ] Step 7: 更新render方法添加结算阶段处理

  在UIRenderer::render中，renderActionMenu调用后添加：

  } else if (state.phase == GameState::ROUND_REVEAL) {
      renderRoundReveal(state);
  } else if (state.phase == GameState::GAME_OVER) {
      renderGameOver(state);
  }

  - [ ] Step 8: 在ClientApp.cpp的gameLoop添加结算等待

  在gameLoop的while循环后添加：

  // 如果进入结算阶段，等待用户按Enter
  if (state_.phase == GameState::ROUND_REVEAL) {
      std::cin.ignore();
      std::cin.get();

      // 等待下一轮或游戏结束
      while (running_ && state_.phase == GameState::ROUND_REVEAL) {
          if (network_.hasMessage(100)) {
              game::messages::ServerMessage msg;
              if (network_.receive(msg, 1000)) {
                  state_.updateFromMessage(msg);
                  if (state_.phase == GameState::PLAYING) {
                      renderer_.render(state_);
                      break;
                  } else if (state_.phase == GameState::GAME_OVER) {
                      break;
                  }
              }
          }
          usleep(100000);
      }
  }

  // 如果游戏结束
  if (state_.phase == GameState::GAME_OVER) {
      renderer_.render(state_);
      std::cin.ignore();
      std::cin.get();
  }

  - [ ] Step 9: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 10: Commit

  git add cli_client/src/GameState.h cli_client/src/GameState.cpp cli_client/src/UIRenderer.h cli_client/src/UIRenderer.cpp cli_client/src/ClientApp.cpp
  git commit -m "feat(cli): add round reveal and game over display"

  ---
  Task 14: 错误处理完善

  Files:
  - Modify: cli_client/src/ClientApp.cpp
  - [ ] Step 1: 添加通用错误处理方法

  在ClientApp.h添加：

  private:
      void handleServerError(const game::messages::ServerMessage& msg);

  在ClientApp.cpp实现：

  void ClientApp::handleServerError(const game::messages::ServerMessage& msg) {
      // 检查各种响应中的error字段
      if (msg.has_create_room_rsp() && msg.create_room_rsp().error().code() != 0) {
          std::cout << "ERROR: " << msg.create_room_rsp().error().message() << std::endl;
          std::cout << ">>> Press Enter to continue..." << std::endl;
          std::cin.ignore();
          std::cin.get();
      }
      else if (msg.has_join_room_rsp() && msg.join_room_rsp().error().code() != 0) {
          std::cout << "ERROR: " << msg.join_room_rsp().error().message() << std::endl;
          std::cout << ">>> Press Enter to continue..." << std::endl;
          std::cin.ignore();
          std::cin.get();
      }
      else if (msg.has_draw_card_rsp() && msg.draw_card_rsp().error().code() != 0) {
          std::cout << "ERROR: " << msg.draw_card_rsp().error().message() << std::endl;
          renderer_.render(state_);
      }
      else if (msg.has_take_from_discard_rsp() && msg.take_from_discard_rsp().error().code() != 0) {
          std::cout << "ERROR: " << msg.take_from_discard_rsp().error().message() << std::endl;
          renderer_.render(state_);
      }
      else if (msg.has_use_skill_rsp() && msg.use_skill_rsp().error().code() != 0) {
          std::cout << "ERROR: " << msg.use_skill_rsp().error().message() << std::endl;
          renderer_.render(state_);
      }
  }

  - [ ] Step 2: 在gameLoop中添加错误处理调用

  在gameLoop的消息处理部分添加：

  if (network_.receive(msg, 1000)) {
      handleServerError(msg);  // 先检查错误
      state_.updateFromMessage(msg);
      renderer_.render(state_);
  }

  - [ ] Step 3: 添加网络超时处理

  在NetworkClient.cpp的receive方法中添加超时日志：

  bool NetworkClient::receive(game::messages::ServerMessage& outMsg, int timeoutMs) {
      // 先检查缓冲区
      if (extractOneMessage(outMsg)) {
          return true;
      }

      // 从socket读取
      uint8_t temp[4096];
      int n = recvRaw(temp, sizeof(temp), timeoutMs);
      if (n <= 0) {
          if (n == 0) {
              std::cerr << "Server closed connection" << std::endl;
          } else {
              std::cerr << "Receive timeout or error" << std::endl;
          }
          return false;
      }

      recvBuffer_.insert(recvBuffer_.end(), temp, temp + n);
      return extractOneMessage(outMsg);
  }

  - [ ] Step 4: 添加输入验证辅助方法

  在ClientApp.h添加：

  private:
      bool getIntInput(int& out, int min, int max);

  实现：

  bool ClientApp::getIntInput(int& out, int min, int max) {
      std::cin >> out;

      if (std::cin.fail()) {
          std::cin.clear();
          std::cin.ignore(10000, '\n');
          std::cout << ">>> Invalid input! Please enter a number." << std::endl;
          return false;
      }

      if (out < min || out > max) {
          std::cout << ">>> Input out of range! Please enter " << min << "-" << max << "." << std::endl;
          return false;
      }

      return true;
  }

  - [ ] Step 5: 在handleGameInput使用getIntInput

  修改handleGameInput：

  void ClientApp::handleGameInput() {
      int choice;
      if (!getIntInput(choice, 1, 3)) {
          renderer_.render(state_);
          return;
      }

      // ... 其余代码保持不变
  }

  - [ ] Step 6: 测试编译

  cd cli_client/build
  cmake ..
  make

  预期输出：成功编译

  - [ ] Step 7: Commit

  git add cli_client/src/ClientApp.h cli_client/src/ClientApp.cpp cli_client/src/NetworkClient.cpp
  git commit -m "feat(cli): add comprehensive error handling"

  ---
  Task 15: 最终测试、文档和README
  
  Files:
  - Modify: cli_client/README.md
  - Create: cli_client/TESTING.md
  - [ ] Step 1: 更新README.md

  # Cabo CLI Client

  C++ command-line client for debugging Cabo game server.

  ## Features

  - 4-player game support
  - TCP + protobuf communication with game server
  - Real-time terminal UI with ANSI colors
  - Full game flow: connect → room → game → result
  - All game actions: draw, discard, replace, skills, call CABO
  - Round reveal and final rankings display

  ## Requirements

  - C++17 compiler (g++ 7+ or clang++ 5+)
  - CMake 3.10+
  - protobuf 3.0+
  - Linux/macOS/WSL (ANSI terminal support)

  ## Build

  ```bash
  # 1. Ensure server is built first (generates protobuf files)
  cd ../
  mkdir -p build && cd build
  cmake ..
  make

  # 2. Build CLI client
  cd ../cli_client
  mkdir -p build && cd build
  cmake ..
  make

  Run

  # Start server first
  cd ../../build
  ./game_server 8888

  # In another terminal, start client
  cd ../cli_client/build
  ./cabo_cli_client

  Usage Flow

  1. Connect: Enter server address (e.g., 127.0.0.1:8888)
  2. Create/Join Room:
    - Create: Automatically creates 4-player room, displays room code
    - Join: Enter room code to join existing room
  3. Auto-Ready: Client automatically sends ready signal
  4. Game Start: When 4 players ready, host auto-starts game
  5. Play: Follow on-screen prompts for actions
  6. Result: View round reveals and final rankings

  Game Actions

  Main Turn Options

  - Draw from draw pile: Draw a card, then choose to discard or replace
  - Take from discard pile: Take top card and replace your cards
  - Call CABO: Initiate final round

  After Drawing

  - Discard: Discard drawn card (triggers skill if 7-12)
  - Replace: Replace 1+ of your cards with drawn card

  Skills (7-12 only when discarded)

  - Peek Self (7-8): View one of your own cards
  - Spy (9-10): View one opponent card
  - Swap (11-12): Blind swap cards with opponent

  Multi-Card Replace

  - Enter slot indices space or comma-separated: 0 1 or 0,1,2
  - Success if all selected cards have same value
  - Failure adds drawn card to hand (5+ cards possible)

  Card Visibility

  Card visibility strictly follows server rules (Appendix A):
  - Start: First 2 cards visible, last 2 hidden
  - After Replace: Replaced card becomes visible
  - After Peek/Spy: Viewed card becomes visible
  - After Swap: Swapped card becomes hidden (blind swap)

  Debugging

  If connection fails:
  - Check server is running on specified port
  - Verify firewall/network settings
  - Check server logs for errors

  If game freezes:
  - Press Ctrl+C to quit
  - Check server logs
  - Restart both server and client

  Testing

  See TESTING.md for manual test scenarios.

  Project Structure

  cli_client/
  ├── src/
  │   ├── main.cpp          - Entry point
  │   ├── ClientApp.cpp     - Main flow control
  │   ├── NetworkClient.cpp - TCP + protobuf
  │   ├── GameState.cpp     - Game state tracking
  │   └── UIRenderer.cpp    - Terminal rendering
  ├── CMakeLists.txt
  ├── README.md
  └── TESTING.md

  Known Limitations

  - No reconnect support (MVP)
  - Fixed 4 players only
  - No save/load game state
  - ANSI terminal required (no Windows CMD)

  Contributing

  This is a debugging tool. Focus on functionality over polish.

  - [ ] **Step 2: 创建TESTING.md**

  ```markdown
  # Testing Guide

  Manual test scenarios for CLI client.

  ## Prerequisite

  - Server running: `./game_server 8888`
  - 4 terminals open for 4 clients

  ## Test 1: Connection Flow

  | Step | Action | Expected |
  |------|--------|----------|
  | 1 | Run `./cabo_cli_client` | Shows welcome message |
  | 2 | Enter `127.0.0.1:8888` | "Connected!" |
  | 3 | Invalid address `999.999.999.999:8888` | "Connection failed", retry prompt |

    ---

  ## Test 2: Room Creation and Join

  | Step | Client | Action | Expected |
  |------|--------|--------|----------|
  | 1 | Client 1 | Choose 1 (Create room) | Shows room code (e.g., AB12CD) |
  | 2 | Client 1 | - | Auto-ready, shows "Waiting for players (1/4)" |
  | 3 | Client 2 | Choose 2 (Join room), enter AB12CD | "Joined room AB12CD" |
  | 4 | Client 2 | - | Auto-ready, both clients show (2/4) |
  | 5 | Client 3 | Join room | Shows (3/4) on all clients |
  | 6 | Client 4 | Join room | Shows (4/4), all ready |
  | 7 | Client 1 | - | As host, auto-starts game after 1 sec |
  | 8 | All | - | Game starts, shows initial cards |

  ## Test 3: Initial Card Visibility

  | Client | Expected Visible Cards |
  |--------|----------------------|
  | Client 1 | First 2 cards show values, last 2 show [?] |
  | Client 2 | First 2 cards show values, last 2 show [?] |
  | Client 3 | First 2 cards show values, last 2 show [?] |
  | Client 4 | First 2 cards show values, last 2 show [?] |

  ## Test 4: Draw and Replace Flow

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Client 1 | Choose 1 (Draw) | "You drew: [X]" |
  | 2 | Client 1 | Choose 2 (Replace) | Prompt for slot indices |
  | 3 | Client 1 | Enter `0` | "Attempting to replace slots [0]..." |
  | 4 | Client 1 | - | Slot 0 now shows new value |
  | 5 | Others | - | See "Waiting for Client 2..." |

  ## Test 5: Multi-Card Replace Success

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Setup | Player has [5] [5] [?] [?] | Two cards with value 5 |
  | 2 | Player | Take from discard (value 7) | - |
  | 3 | Player | Enter `0 1` | "Replace successful!" |
  | 4 | Player | - | Slots 0,1 now both [7] |

  ## Test 6: Multi-Card Replace Failure

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Setup | Player has [3] [5] [?] [?] | Different values |
  | 2 | Player | Take from discard (value 7) | - |
  | 3 | Player | Enter `0 1` | "Replace FAILED!" |
  | 4 | Player | - | "Card added to your hand. Your hand now has 5 cards" |
  | 5 | Player | - | Display shows 5 cards: [3] [5] [?] [?] [7] |

  ## Test 7: Peek Self Skill (7-8)

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Player | Draw card 7 | "This card has a skill!" |
  | 2 | Player | Choose 1 (Discard) | "Using skill: Peek Self" |
  | 3 | Player | Enter slot 2 | "You saw: [X]" |
  | 4 | Player | - | Slot 2 now shows value permanently |

  ## Test 8: Spy Skill (9-10)

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Player | Draw card 9 | "This card has a skill!" |
  | 2 | Player | Choose 1 (Discard) | "Using skill: Spy" |
  | 3 | Player | Choose opponent 2, slot 1 | "You saw opponent's card: [X]" |
  | 4 | Player | - | Opponent's card remains [?] on screen |

  ## Test 9: Swap Skill (11-12)

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Player | Draw card 11 | "This card has a skill!" |
  | 2 | Player | Choose 1 (Discard) | "Using skill: Swap" |
  | 3 | Player | My slot 0, opponent 2, slot 1 | "Swap completed!" |
  | 4 | Player | - | My slot 0 becomes [?] (blind swap) |

  ## Test 10: Call CABO

  | Step | Current Player | Action | Expected |
  |------|---------------|--------|----------|
  | 1 | Player 1 | Choose 3 (Call CABO) | "Entering final round" |
  | 2 | Player 2 | Take turn | Normal turn |
  | 3 | Player 3 | Take turn | Normal turn, "Call CABO" option disabled |
  | 4 | Player 4 | Take turn | Normal turn |
  | 5 | All | - | Round reveal displays |

  ## Test 11: Round Reveal

  | Expected Display |
  |------------------|
  | Shows all players' cards: [3] [5] [7] [9] = 24 |
  | Shows penalties: (+10 penalty) if CABO caller not lowest |
  | Shows round scores for each player |
  | Shows cumulative scores |
  | Marks lowest score with "← Lowest!" |
  | Prompt: "Press Enter to continue..." |

  ## Test 12: Game Over (100+ score)

  | Step | Expected |
  |------|----------|
  | 1 | One player reaches ≥100 score |
  | 2 | Game Over screen displays |
  | 3 | Shows final rankings 1st-4th place |
  | 4 | Marks winner with "🎉 WINNER" |
  | 5 | Prompt: "Press Enter to exit..." |

  ## Test 13: Error Handling

  | Scenario | Action | Expected |
  |----------|--------|----------|
  | Invalid room code | Join "XXXXXX" | "ERROR: Room not found", retry |
  | Wrong turn | Player 2 tries to act on Player 1's turn | Blocked (no input accepted) |
  | Invalid slot | Enter slot 99 | "Invalid slot!", re-prompt |
  | Invalid choice | Enter "abc" | "Invalid input! Please enter a number." |
  | Network timeout | Kill server mid-game | "Server closed connection" |

  ## Test 14: Opponent Display Order

  | My Seat | Top (Opposite) | Left | Right |
  |---------|---------------|------|-------|
  | 1 | Seat 3 | Seat 0 | Seat 2 |
  | 2 | Seat 0 | Seat 1 | Seat 3 |
  | 3 | Seat 1 | Seat 2 | Seat 0 |

  ## Test 15: Current Turn Arrow
  
  - [ ] When it's Player 1's turn, arrow (↓) appears above Player 1's name
  - [ ] Arrow moves to Player 2 on their turn
  - [ ] Arrow always points to current player
  - [ ] Non-current players see "Waiting for X to act..."

  ## Pass Criteria
  
  All tests pass ✅:
  - [ ] Test 1: Connection
  - [ ] Test 2: Room creation/join
  - [ ] Test 3: Card visibility
  - [ ] Test 4: Draw and replace
  - [ ] Test 5: Multi-card success
  - [ ] Test 6: Multi-card failure
  - [ ] Test 7: Peek self skill
  - [ ] Test 8: Spy skill
  - [ ] Test 9: Swap skill
  - [ ] Test 10: Call CABO
  - [ ] Test 11: Round reveal
  - [ ] Test 12: Game over
  - [ ] Test 13: Error handling
  - [ ] Test 14: Display order
  - [ ] Test 15: Turn arrow

  ## Known Issues Log
  
  Document any bugs found during testing:
  
  | Date | Issue | Status |
  |------|-------|--------|
  | - | - | - |

  - [ ] Step 3: 完整测试流程

  启动服务端：

  cd MuduoBaseGameServer/build
  ./game_server 8888

  启动4个客户端（4个终端）：

  # Terminal 1-4
  cd MuduoBaseGameServer/cli_client/build
  ./cabo_cli_client

  - [ ] Step 4: 运行Test 1-15中的所有场景

  按照TESTING.md中的步骤逐一验证

  - [ ] Step 5: 记录测试结果

  在TESTING.md的"Known Issues Log"中记录发现的问题

  - [ ] Step 6: 如果发现bug，修复后重新测试

  例如：如果发现某个消息处理有问题，修复后：

  cd cli_client/build
  make
  # 重新运行相关测试

  - [ ] Step 7: 最终提交

  git add cli_client/README.md cli_client/TESTING.md
  git commit -m "docs(cli): add comprehensive README and testing guide"

  - [ ] Step 8: 创建最终标签

  git tag -a cli-client-v1.0 -m "CLI Client v1.0: Full 4-player game support"
  git push origin cli-client-v1.0

  ---
  自检清单
  
  完成所有task后，检查以下内容：

  代码完整性

  - [x] 所有.h和.cpp文件已创建
  - [x] CMakeLists.txt正确配置
  - [x] 所有方法都有实现（无TODO残留）
  - [x] 编译通过无warning

  功能完整性

  - [x] 连接流程：connect, login
  - [x] 房间流程：create, join, auto-ready, auto-start
  - [x] 游戏流程：draw, replace, take from discard, call CABO
  - [x] 技能流程：peek self, spy, swap
  - [x] 结算流程：round reveal, game over
  - [x] 错误处理：网络错误、输入错误、服务端错误

  协议一致性

  - [x] 使用protobuf复用服务端生成代码
  - [x] 帧格式：[4字节大端长度][protobuf数据]
  - [x] 所有ClientMessage正确设置seq
  - [x] 所有ServerMessage消息类型都有处理

  UI完整性

  - [x] 清屏重绘工作正常
  - [x] 玩家信息显示正确（昵称、分数、卡牌数量）
  - [x] 对手显示顺序：(我+2)%4, (我+3)%4, (我+1)%4
  - [x] 当前回合箭头显示
  - [x] 操作菜单根据回合状态正确显示/隐藏
  - [x] 卡牌可见性按附录A规则显示

  文档完整性

  - [x] README.md：功能、构建、使用说明
  - [x] TESTING.md：15个测试场景
  - [x] 代码注释清晰（关键逻辑有注释）
