 C++ CLI客户端设计文档

  ▎ 日期：2026-06-05
  ▎ 范围：Cabo游戏调试用CLI客户端
  ▎ 目标：替代Unity进行快速调试和测试

  1. 概述

  由于Unity调试缓慢，开发一个C++编写的CLI（命令行）客户端，用于快速调试服务端逻辑和协议交互。

  设计目标

  - 快速启动：无需Unity编辑器，直接运行可执行文件
  - 清晰可见：终端界面实时显示4人游戏状态
  - 完整流程：支持连接、房间、游戏、结算全流程
  - 协议一致：使用TCP + protobuf，与服务端完全兼容

  非目标

  - 不追求美观UI和动画效果
  - 不实现断线重连（MVP阶段）
  - 不支持2/3人游戏（固定4人）
  - 不实现语音、聊天等社交功能

  ---
  2. 架构设计

  2.1 整体架构

  main.cpp
      ↓
  ClientApp (主程序流程控制)
      ↓
  NetworkClient (TCP连接 + protobuf收发)
      ↓
  GameState (客户端游戏状态)
      ↓
  UIRenderer (终端界面渲染)

  2.2 技术选型

  方案：单线程阻塞式架构

  - 单线程主循环
  - 使用select()实现非阻塞socket检查
  - 主循环：检查消息 → 更新状态 → 重绘界面 → 处理输入

  为什么选择单线程？
  - 实现简单，易于调试
  - 无需处理多线程同步
  - 4人游戏节奏不快，单线程完全够用

  2.3 依赖项

  - protobuf：复用项目中已生成的pb.cc/pb.h
  - 标准库socket：TCP连接（无需muduo）
  - C++17：std::optional, string_view等特性
  - ANSI转义码：终端清屏和光标控制

  ---
  3. 核心组件设计

  3.1 GameState（游戏状态）

  struct Card {
      int32_t slotIndex;
      bool isKnown;        // 可见性由服务端控制
      int32_t value;       // 仅isKnown=true时有效
  };

  struct Player {
      int64_t playerId;
      string nickname;
      int32_t seatId;
      int32_t totalScore;
      int32_t cardCount;   // 对手只知道数量
      bool isReady;
      bool isHost;
  };

  struct GameState {
      // 连接状态
      int64_t myPlayerId = 0;
      int64_t roomId = 0;
      string roomCode;

      // 房间阶段
      enum Phase { LOBBY, WAITING_ROOM, PLAYING, ROUND_REVEAL, GAME_OVER };
      Phase phase = LOBBY;

      // 玩家列表（按座位顺序，固定4人）
      vector<Player> players;

      // 自己的手牌（按附录A可见性规则）
      vector<Card> myCards;

      // 牌堆信息
      int32_t drawPileCount = 0;
      int32_t discardPileCount = 0;
      int32_t discardTopValue = -1;

      // 回合信息
      int64_t currentPlayerId = 0;
      int32_t roundNumber = 0;
      int32_t turnNumber = 0;

      // 抽牌暂存（DrawCardRsp后）
      bool hasDrawnCard = false;
      int32_t drawnCardValue = 0;
      int32_t drawnCardSkill = 0;  // SkillType枚举值

      // 最终轮标志
      bool isFinalRound = false;
      int32_t finalRoundRemaining = 0;

      // 辅助方法
      bool isMyTurn() const;
      int getMyPlayerIndex() const;
      vector<int> getOpponentIndices() const;  // 按(我+3)%4, (我+2)%4, (我+1)%4顺序
      void updateFromMessage(const game::messages::ServerMessage& msg);
  };

  状态更新映射：

  ┌────────────────────┬───────────────────────────────────────────────────────┐
  │     服务端消息     │                     GameState更新                     │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ CreateRoomRsp      │ 设置roomId, myPlayerId, roomCode                      │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ JoinRoomRsp        │ 设置roomId, myPlayerId, seatId                        │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ RoomStateNotify    │ 更新players列表                                       │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ PlayerJoinNotify   │ 添加玩家到players                                     │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ PlayerReadyNotify  │ 更新玩家ready状态                                     │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ GameStartNotify    │ 初始化myCards（前2张known=true），phase=PLAYING       │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ TurnStartNotify    │ 更新currentPlayerId, drawPileCount, discardPileCount  │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ DrawCardRsp        │ 保存drawnCardValue, drawnCardSkill, hasDrawnCard=true │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ ActionResultNotify │ 更新牌堆、弃牌堆、回合切换                            │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ RoundRevealNotify  │ phase=ROUND_REVEAL，显示所有玩家手牌                  │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ ScoreUpdateNotify  │ 更新所有玩家totalScore                                │
  ├────────────────────┼───────────────────────────────────────────────────────┤
  │ GameOverNotify     │ phase=GAME_OVER，显示最终排名                         │
  └────────────────────┴───────────────────────────────────────────────────────┘

  3.2 NetworkClient（网络层）

  class NetworkClient {
  public:
      NetworkClient();
      ~NetworkClient();

      bool connect(const string& host, int port);
      bool send(const game::messages::ClientMessage& msg);
      bool hasMessage(int timeoutMs = 0);
      bool receive(game::messages::ServerMessage& outMsg, int timeoutMs = 1000);
      void disconnect();
      bool isConnected() const;

  private:
      int sockfd_ = -1;
      vector<uint8_t> recvBuffer_;
      int64_t clientSeq_ = 1;

      static string encodeFrame(const string& payload);
      static bool decodeFrame(const vector<uint8_t>& buffer,
                             size_t& frameLen,
                             ve
  ⎿  Interrupted · What should Claude do instead?

❯ 继续                                                                                                                                              

● 好的，继续展示文档内容：

  ---
  vector<uint8_t>& payload);
      bool extractOneMessage(game::messages::ServerMessage& outMsg);
  };

  关键实现：

  1. 帧格式：[4字节大端长度][protobuf数据]，与服务端MessageCodec完全一致
  2. 粘包/半包处理：
  // recvBuffer_累积接收的数据
  // 只有当buffer中有完整帧时才解析
  while (buffer_.size() >= 4) {
      int32_t payloadLen = readBigEndianInt32(buffer_.data());
      if (buffer_.size() < 4 + payloadLen) break;  // 半包
      // 提取完整帧并解析
  }
  3. 非阻塞检查：
  bool NetworkClient::hasMessage(int timeoutMs) {
      fd_set readfds;
      FD_ZERO(&readfds);
      FD_SET(sockfd_, &readfds);
      struct timeval tv = {timeoutMs/1000, (timeoutMs%1000)*1000};
      return select(sockfd_+1, &readfds, nullptr, nullptr, &tv) > 0;
  }

  3.3 UIRenderer（界面渲染）

  class UIRenderer {
  public:
      void render(const GameState& state);

  private:
      void clearScreen();  // ANSI: \033[2J\033[H
      void renderHeader();
      void renderPiles(const GameState& state);
      void renderPlayers(const GameState& state);
      void renderMyCards(const GameState& state);
      void renderActionMenu(const GameState& state);

      string formatCard(const Card& card);  // "[3]" or "[?]"
      string formatPlayerArea(const Player& p, bool isCurrent, bool isMe);
  };

  界面布局示例：

  ================================================================================
                          Cabo Game - 4 Players
                            Round 1, Turn 3
  ================================================================================

                      Draw Pile: 32      Discard Pile: 5 (Top: 7)

  --------------------------------------------------------------------------------
                                      ↓
                                [Player 2: Alice]
                                Score: 15
                                Cards: [?] [?] [?] [?]
  --------------------------------------------------------------------------------

      [Player 3: Bob]                              [Player 1: Carol]
      Score: 22                                    Score: 8
      Cards: [?] [?] [?] [?]                      Cards: [?] [?] [?] [?] [?]

  --------------------------------------------------------------------------------
                                [You: David]
                                Score: 12
                                Cards: [3] [?] [5] [?]
  ================================================================================

  >>> Your Turn! Choose action:
      1. Draw from draw pile
      2. Take from discard pile (current top: 7)
      3. Call CABO
  >>> Enter choice: _

  非当前回合时：

  ================================================================================
  ... (同样布局) ...
  ================================================================================

  >>> Waiting for Alice to act...
  >>> (Press Ctrl+C to quit)

  对手显示顺序逻辑：

  vector<int> GameState::getOpponentIndices() const {
      int myIndex = getMyPlayerIndex();
      int n = players.size();  // 固定4
      return {
          (myIndex + 2) % n,  // 对面玩家（顶部）
          (myIndex + 3) % n,  // 左侧玩家
          (myIndex + 1) % n   // 右侧玩家
      };
  }

  当前回合箭头：
  - 检查state.currentPlayerId
  - 在对应玩家信息上方输出↓

  3.4 ClientApp（主程序）

  class ClientApp {
  public:
      void run();

  private:
      NetworkClient network_;
      GameState state_;
      UIRenderer renderer_;
      bool running_ = true;

      // 流程阶段
      void connectToServer();
      void loginFlow();
      void roomFlow();
      void waitingRoomLoop();
      void gameLoop();
      void resultFlow();

      // 输入处理
      void handleGameInput();
      void handleDrawnCardDecision();
      void handleReplaceWithDrawn();
      void handleTakeFromDiscard();
      void handleSkillInput(int skillType);

      // 工具方法
      vector<int> parseSlotIndices(const string& input);
      void waitForMessage(const string& msgType, int timeoutMs = 5000);
  };

  主循环：

  void ClientApp::gameLoop() {
      while (running_ && state_.phase != GameState::GAME_OVER) {
          // 1. 非阻塞检查服务端消息（100ms超时）
          if (network_.hasMessage(100)) {
              ServerMessage msg;
              if (network_.receive(msg, 1000)) {
                  state_.updateFromMessage(msg);
                  renderer_.render(state_);

                  if (msg.has_game_over_notify()) {
                      break;
                  }
              }
          }

          // 2. 如果是我的回合且没有抽牌状态，处理输入
          if (state_.isMyTurn() && !state_.hasDrawnCard) {
              handleGameInput();
          }

          // 3. 如果抽了牌，处理抽牌后决策
          if (state_.hasDrawnCard) {
              handleDrawnCardDecision();
          }

          // 4. 非当前回合，短暂休眠
          if (!state_.isMyTurn()) {
              usleep(100000);  // 100ms
          }
      }
  }

  ---
  4. 交互流程设计
  
  4.1 启动流程

  1. 运行 ./cabo_cli_client
  2. 输入服务端地址：127.0.0.1:8888
  3. 连接成功
  4. 输入昵称：Alice

  4.2 房间流程

  创建房间：
  >>> Choose action:
      1. Create room (4 players)
      2. Join room
  >>> Enter choice: 1

  >>> Creating room...
  >>> Room created! Room Code: AB12CD
  >>> Share this code with other players

  >>> Waiting for players (1/4)...
  [Player 1: Alice (You, Host)] [Ready]
  [Player 2: ---]
  [Player 3: ---]
  [Player 4: ---]

  加入房间：
  >>> Choose action:
      1. Create room (4 players)
      2. Join room
  >>> Enter choice: 2

  >>> Enter room code: AB12CD
  >>> Joining room...
  >>> Joined room AB12CD

  >>> Waiting for players (2/4)...
  [Player 1: Alice (Host)] [Ready]
  [Player 2: Bob (You)] [Ready]
  [Player 3: ---]
  [Player 4: ---]

  自动Ready机制：
  - 创建房间后，自动发送ReadyReq
  - 加入房间后，自动发送ReadyReq
  - 不需要用户手动输入ready命令

  自动Start机制：
  - 当4人都ready后
  - 如果是房主，自动发送StartGameReq
  - 非房主等待RoomStartNotify

  4.3 游戏回合流程

  选项1：从抽牌堆抽牌

  >>> Your Turn! Choose action:
      1. Draw from draw pile
      2. Take from discard pile (current top: 7)
      3. Call CABO
  >>> Enter choice: 1

  >>> You drew: [9]
  >>> This card has a skill! (Spy)

  >>> Choose what to do:
      1. Discard and use skill
      2. Replace your cards with this card
  >>> Enter choice: 1

  >>> Using skill: Spy
  >>> Choose target player (1-3 for opponents): 2
  >>> Choose target slot (0-3): 1
  >>> You saw: [5]

  >>> Discarded [9], turn ended.

  选项2：从弃牌堆拿牌替换

  >>> Your Turn! Choose action:
      1. Draw from draw pile
      2. Take from discard pile (current top: 7)
      3. Call CABO
  >>> Enter choice: 2

  >>> Taking top card [7] from discard pile
  >>> Enter slot indices to replace (space-separated, e.g., '0 1'): 0 2

  >>> Attempting to replace slots [0, 2]...
  >>> Replace successful! Cards [3, 5] discarded.
  >>> Slots [0, 2] now have [7, 7]

  多张替换失败示例：

  >>> Enter slot indices to replace: 0 1

  >>> Attempting to replace slots [0, 1]...
  >>> Replace FAILED! Cards at slots [0, 1] have different values.
  >>> Drawn card [7] added to your hand.
  >>> Your hand now has 5 cards.

  选项3：喊CABO

  >>> Your Turn! Choose action:
      1. Draw from draw pile
      2. Take from discard pile (current top: 7)
      3. Call CABO
  >>> Enter choice: 3

  >>> You called CABO!
  >>> Entering final round, each player gets one more turn.

  4.4 技能交互

  偷看自己（7-8）：
  >>> Using skill: Peek Self
  >>> Choose your slot to peek (0-3): 2
  >>> You saw: [11]

  间谍（9-10）：
  >>> Using skill: Spy
  >>> Choose target player:
      1. Alice
      2. Bob
      3. Carol
  >>> Enter choice (1-3): 2
  >>> Choose target slot (0-3): 1
  >>> You saw opponent's card: [4]

  交换（11-12）：
  >>> Using skill: Swap
  >>> Choose your slot (0-3): 1
  >>> Choose target player:
      1. Alice
      2. Bob
      3. Carol
  >>> Enter choice (1-3): 2
  >>> Choose target slot (0-3): 3
  >>> Swap completed!
  >>> (Note: You don't know what you received)

  4.5 结算流程

  单轮结算：
  ================================================================================
                          Round 1 Reveal
  ================================================================================

  Alice (called CABO):  [3] [5] [7] [9] = 24  (+10 penalty) = 34
  Bob:                  [2] [2] [4] [6] = 14  
  Carol:                [0] [1] [3] [8] = 12  ← Lowest!
  David (You):          [3] [5] [5] [7] = 20

  Scores after Round 1:
    Alice: 34
    Bob: 14
    Carol: 12  ← Lowest
    David: 20

  >>> Press Enter to continue...
  
  游戏结束：
  ================================================================================
                          Game Over!
  ================================================================================

  Final Standings:
    1st Place: Carol  (Score: 87)  🎉 WINNER
    2nd Place: David  (Score: 92)
    3rd Place: Bob    (Score: 105)
    4th Place: Alice  (Score: 112)

  >>> Press Enter to return to lobby...

  ---
  5. 错误处理
  
  5.1 网络错误

  if (!network_.connect(host, port)) {
      cout << "ERROR: Failed to connect to " << host << ":" << port << "\n";
      cout << ">>> Retry? (y/n): ";
      char choice;
      cin >> choice;
      if (choice == 'y' || choice == 'Y') {
          return connectToServer();  // 递归重试
      }
      running_ = false;
  }

  5.2 服务端错误响应

  if (msg.has_error()) {
      cout << "ERROR from server: " << msg.error().message() << "\n";
      cout << ">>> Press Enter to continue...";
      cin.ignore();
      cin.get();
      renderer_.render(state_);  // 重新渲染界面
  }

  5.3 非法输入

  void ClientApp::handleGameInput() {
      int choice;
      cin >> choice;

      if (cin.fail() || choice < 1 || choice > 3) {
          cin.clear();
          cin.ignore(10000, '\n');
          cout << ">>> Invalid input! Please enter 1, 2, or 3.\n";
          return handleGameInput();  // 递归重试
      }

      // 正常处理...
  }

  5.4 非当前回合操作

  由于非当前回合时不显示操作菜单且阻塞等待消息，用户无法输入操作，因此不存在此类错误。

  ---
  6. 项目结构
  
  MuduoBaseGameServer/
  ├── cli_client/                   # 新增CLI客户端目录
  │   ├── CMakeLists.txt
  │   ├── README.md
  │   ├── src/
  │   │   ├── main.cpp             # 程序入口
  │   │   ├── ClientApp.h
  │   │   ├── ClientApp.cpp
  │   │   ├── NetworkClient.h
  │   │   ├── NetworkClient.cpp
  │   │   ├── GameState.h
  │   │   ├── GameState.cpp
  │   │   ├── UIRenderer.h
  │   │   └── UIRenderer.cpp
  │   └── build/                    # CMake构建目录
  └── src/                          # 服务端代码（已有）
      ├── proto/                    # 复用protobuf生成代码
      │   ├── messages.pb.h/cc
      │   ├── room.pb.h/cc
      │   ├── game.pb.h/cc
      │   ├── common.pb.h/cc
      │   └── sync.pb.h/cc
      └── ...

  ---
  7. 构建配置
  
  7.1 CMakeLists.txt

  cmake_minimum_required(VERSION 3.10)
  project(CaboCliClient)

  set(CMAKE_CXX_STANDARD 17)
  set(CMAKE_CXX_STANDARD_REQUIRED ON)

  # 查找protobuf
  find_package(Protobuf REQUIRED)

  # 包含目录
  include_directories(
      ${CMAKE_CURRENT_SOURCE_DIR}/src
      ${CMAKE_SOURCE_DIR}/src/proto  # 复用服务端生成的pb.h
      ${Protobuf_INCLUDE_DIRS}
  )

  # 客户端源文件
  set(CLIENT_SOURCES
      src/main.cpp
      src/ClientApp.cpp
      src/NetworkClient.cpp
      src/GameState.cpp
      src/UIRenderer.cpp
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

  7.2 编译步骤

  # 1. 确保服务端已编译（生成protobuf代码）
  cd MuduoBaseGameServer
  mkdir -p build && cd build
  cmake ..
  make

  # 2. 编译CLI客户端
  cd ../cli_client
  mkdir -p build && cd build
  cmake ..
  make

  # 3. 运行
  ./cabo_cli_client

  ---
  8. 附录：卡牌可见性实现
  
  根据Docs/superpowers/specs/2026-06-03-game-table-ui-design.md附录A：

  ┌────────────────┬────────────────────────┬──────────────────────────────────────────┐
  │      场景      │       客户端显示       │             服务端knownSlots             │
  ├────────────────┼────────────────────────┼──────────────────────────────────────────┤
  │ 开局           │ 前2张正面，后2张背面   │ [0]=true, [1]=true, [2]=false, [3]=false │
  ├────────────────┼────────────────────────┼──────────────────────────────────────────┤
  │ 抽牌替换       │ 新牌正面               │ knownSlots[slot]=true                    │
  ├────────────────┼────────────────────────┼──────────────────────────────────────────┤
  │ 从弃牌堆替换   │ 新牌正面               │ knownSlots[slot]=true                    │
  ├────────────────┼────────────────────────┼──────────────────────────────────────────┤
  │ 偷看自己 (7-8) │ 被看牌永久正面         │ knownSlots[slot]=true                    │
  ├────────────────┼────────────────────────┼──────────────────────────────────────────┤
  │ 交换 (11-12)   │ 换进来的是背面（盲换） │ knownSlots[slot]=false                   │
  ├────────────────┼────────────────────────┼──────────────────────────────────────────┤
  │ 失败加牌       │ 新牌正面               │ knownSlots.push_back(true)               │
  └────────────────┴────────────────────────┴──────────────────────────────────────────┘

  客户端实现：

  void GameState::updateFromMessage(const ServerMessage& msg) {
      if (msg.has_game_start_notify()) {
          auto& view = msg.game_start_notify().your_view();
          myCards.clear();
          for (const auto& cardState : view.own_cards()) {
              Card card;
              card.slotIndex = cardState.slot_index();
              card.isKnown = cardState.is_known();
              card.value = cardState.value();
              myCards.push_back(card);
          }
      }

      // 其他消息类似处理，完全信任服务端的isKnown字段
  }

  关键原则：
  - 客户端不做任何可见性判断
  - 完全依赖服务端下发的OwnCardState.is_known字段
  - 客户端只负责渲染：isKnown ? "[" + value + "]" : "[?]"

  ---
  9. 实现检查清单
  
  Phase 1: 基础框架

  - [ ] NetworkClient: TCP连接、send/receive
  - [ ] GameState: 数据结构定义
  - [ ] UIRenderer: 基础清屏和渲染
  - [ ] ClientApp: main循环骨架

  Phase 2: 房间流程

  - [ ] 连接服务端、输入昵称
  - [ ] 创建房间、显示房间号
  - [ ] 加入房间
  - [ ] 自动ready、自动start
  - [ ] 等待房间界面

  Phase 3: 游戏核心

  - [ ] GameStartNotify处理
  - [ ] TurnStartNotify处理
  - [ ] 当前回合界面（操作菜单）
  - [ ] 非当前回合界面（等待提示）
  - [ ] 抽牌流程
  - [ ] 从弃牌堆拿牌
  - [ ] 喊CABO

  Phase 4: 高级功能

  - [ ] 抽牌后决策（弃掉/替换）
  - [ ] 多张替换输入解析
  - [ ] 技能交互（偷看、间谍、交换）
  - [ ] 可见性规则（附录A）
  - [ ] 失败替换显示（5张牌）

  Phase 5: 结算

  - [ ] RoundRevealNotify显示
  - [ ] ScoreUpdateNotify显示
  - [ ] GameOverNotify排名显示

  Phase 6: 错误处理

  - [ ] 网络错误重试
  - [ ] 服务端错误提示
  - [ ] 非法输入重试
  - [ ] 超时处理

  ---
  10. 测试计划
  
  10.1 单元测试场景

  ┌──────────┬─────────────────┬───────────────────────────────┐
  │  测试项  │      输入       │           预期输出            │
  ├──────────┼─────────────────┼───────────────────────────────┤
  │ 帧编码   │ payload="hello" │ [0,0,0,5,'h','e','l','l','o'] │
  ├──────────┼─────────────────┼───────────────────────────────┤
  │ 帧解码   │ 同上            │ payload="hello"               │
  ├──────────┼─────────────────┼───────────────────────────────┤
  │ 槽位解析 │ "0 1"           │ [0, 1]                        │
  ├──────────┼─────────────────┼───────────────────────────────┤
  │ 槽位解析 │ "0,1,2"         │ [0, 1, 2]                     │
  ├──────────┼─────────────────┼───────────────────────────────┤
  │ 对手顺序 │ 我是2号         │ [0, 1, 3] (对面、左、右)      │
  └──────────┴─────────────────┴───────────────────────────────┘

  10.2 集成测试场景

  1. 连接流程：启动客户端 → 连接服务端 → 输入昵称 → 成功
  2. 创建房间：创建房间 → 显示房间号 → 自动ready
  3. 加入房间：加入房间 → 显示玩家列表 → 自动ready
  4. 游戏开始：4人ready → 房主自动start → 收到GameStartNotify
  5. 抽牌：轮到我 → 抽牌 → 看到牌值 → 选择弃掉 → 回合结束
  6. 替换：抽牌 → 选择替换 → 输入槽位 → 替换成功
  7. 多张替换成功：拿弃牌堆 → 输入"0 1" → 成功（点数相同）
  8. 多张替换失败：拿弃牌堆 → 输入"0 1" → 失败（点数不同） → 显示5张牌
  9. 技能-偷看：抽到7 → 弃掉 → 选择槽位 → 看到数值 → 该槽位变known
  10. 技能-间谍：抽到9 → 弃掉 → 选择对手和槽位 → 看到数值
  11. 技能-交换：抽到11 → 弃掉 → 选择双方槽位 → 交换 → 己方槽位变unknown
  12. 喊CABO：选择call cabo → 进入最终轮 → 其他人各行动一次 → 结算
  13. 结算：亮牌 → 显示所有人手牌和分数 → 累计分更新
  14. 游戏结束：某人≥100分 → 显示排名 → 返回大厅

  ---
  11. Why：为什么需要这个CLI客户端？
  
  问题：
  - Unity编辑器启动慢（30秒+）
  - 每次修改服务端代码，Unity需要重新连接
  - Unity调试界面复杂，干扰注意力
  - 多人测试需要启动多个Unity实例（占用大量内存）

  解决：
  - CLI客户端启动快（<1秒）
  - 轻量级，可同时运行4个实例（占用<50MB内存）
  - 终端界面清晰，直观显示所有状态
  - 快速迭代：改服务端代码 → 重启服务端 → 重连客户端（5秒内完成）

  使用场景：
  - 服务端逻辑调试
  - 协议正确性验证
  - 多人游戏流程测试
  - 性能压力测试（启动10+客户端）

  ---
  文档完成日期：2026-06-05