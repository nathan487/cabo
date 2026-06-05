# CLI客户端测试指南

## 🔧 修复内容

已修复 `NetworkClient::hasMessage()` 的缓冲区检查bug，该bug导致客户端无法处理快速到达的服务器消息。

**修复文件**: `cli_client/src/NetworkClient.cpp`  
**Bug报告**: 参见 `BUG_FIX_REPORT.md`

---

## 📋 测试前准备

### 1. 确认编译状态

```bash
# 检查服务器可执行文件
ls -l MuduoBaseGameServer/build/GameServer

# 检查CLI客户端可执行文件  
ls -l MuduoBaseGameServer/cli_client/build/cabo_cli_client

# 如果需要重新编译客户端
cd MuduoBaseGameServer/cli_client/build
cmake ..
make
```

### 2. 准备4个终端窗口

- **终端1**: 运行服务器
- **终端2**: 客户端1 (房主)
- **终端3**: 客户端2
- **终端4**: 客户端3
- **终端5**: 客户端4

---

## 🚀 测试步骤

### 步骤1: 启动服务器

**终端1**:
```bash
cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/build"
./GameServer 8888
```

**期望输出**:
```
[INFO] Server listening on 0.0.0.0:8888
```

---

### 步骤2: 启动客户端1 (房主)

**终端2**:
```bash
cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/cli_client/build"
./cabo_cli_client
```

**交互步骤**:
```
>>> Enter server IP:port: 127.0.0.1:8888
>>> Connected!

>>> Room Options:
    1. Create a new room
    2. Join an existing room
>>> Enter choice: 1

>>> Enter your nickname: Alice

>>> Creating room...
>>> Room created successfully!
>>> Room Code: XXXXXX    ← 记下这个房间码!
>>> Your Player ID: 10000

>>> Type 'ready' and press Enter to mark yourself as ready
```

输入: `ready`

**期望**: 显示 "Ready signal sent!"

---

### 步骤3: 启动客户端2-4 (其他玩家)

**终端3 (客户端2)**:
```bash
cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/cli_client/build"
./cabo_cli_client
```

**交互步骤**:
```
>>> Enter server IP:port: 127.0.0.1:8888
>>> Connected!

>>> Room Options:
>>> Enter choice: 2

>>> Enter your nickname: Bob
>>> Enter room code: XXXXXX    ← 输入客户端1显示的房间码

>>> Joining room...
>>> Joined room successfully!

>>> Type 'ready' and press Enter
```

输入: `ready`

**重复此步骤**为客户端3和客户端4 (使用不同昵称: Carol, David)

---

### 步骤4: 验证所有玩家Ready

在所有4个客户端终端，你应该看到:

```
================================================================================
                          Cabo Game - 4 Players
================================================================================

>>> Waiting for players (4/4)...
[Player 1: Alice (Host)] [Ready]
[Player 2: Bob] [Ready]
[Player 3: Carol] [Ready]
[Player 4: David] [Ready]

>>> All players ready! Type 'start' to begin the game    ← 仅房主看到
```

---

### 步骤5: 启动游戏 (关键测试点!)

**在终端2 (客户端1 - 房主)** 输入:
```
start
```

---

## ✅ 成功标志

### Bug修复前的错误行为:
- ❌ 客户端卡在等待室
- ❌ 显示 "Waiting for host to start..."
- ❌ 没有任何反应，永久等待

### Bug修复后的正确行为:

**所有4个客户端应该立即显示**:

```
>>> Game starting! Transitioning to game loop...
[DEBUG] Current phase: PLAYING, breaking from waitingRoomLoop
[DEBUG] Entered gameLoop, phase=2

================================================================================
                        Cabo Game - 4 Players
                          Round 1, Turn 1
================================================================================

                    Draw Pile: 32      Discard Pile: 1 (Top: X)

--------------------------------------------------------------------------------
                                    ↓
                              [Player 1: Alice]
                              Score: 0
                              Cards: [?] [?] [?] [?]
--------------------------------------------------------------------------------

      [Player 3: Carol]                            [Player 2: Bob]
      Score: 0                                     Score: 0
      Cards: [?] [?] [?] [?]                      Cards: [?] [?] [?] [?]

--------------------------------------------------------------------------------
                              [You: David]
                              Score: 0
                              Cards: [3] [5] [?] [?]    ← 前2张可见
================================================================================

>>> Your Turn! Choose action:     ← 如果轮到你
    1. Draw from draw pile
    2. Take from discard pile (current top: X)
    3. Call CABO
>>> Enter choice: 

或

>>> Waiting for Alice to act...   ← 如果不是你的回合
>>> (Press Ctrl+C to quit)
```

---

## 🔍 关键验证点

### ✅ 检查清单

- [ ] **服务器启动**: 端口8888监听成功
- [ ] **客户端连接**: 4个客户端都成功连接
- [ ] **房间创建**: 房主成功创建房间并获得房间码
- [ ] **加入房间**: 其他3个客户端成功加入
- [ ] **Ready状态**: 所有4个客户端显示 [Ready]
- [ ] **游戏启动**: 房主输入 `start` 后
  - [ ] 看到 "Game starting!" 消息
  - [ ] 看到 "[DEBUG] Current phase: PLAYING"
  - [ ] 进入游戏界面
  - [ ] 显示 "Round 1, Turn 1"
  - [ ] 显示牌堆信息 (Draw Pile, Discard Pile)
  - [ ] 显示所有4个玩家
  - [ ] 自己的前2张牌显示数字 (如 [3] [5])
  - [ ] 自己的后2张牌显示 [?]
  - [ ] 当前回合玩家上方有箭头 ↓
  - [ ] 如果是自己回合，显示操作菜单
  - [ ] 如果不是自己回合，显示 "Waiting for X to act..."

---

## 🐛 如果仍然卡住

### 调试步骤

1. **检查服务器日志** (终端1):
   ```
   应该看到:
   [INFO] Player 10000 connected
   [Room] Room XXXXXX created
   [Room] Player 10001 joined
   ... (等等)
   [Game] Starting game in room XXXXXX
   [Game] Dealing cards...
   ```

2. **检查客户端调试输出**:
   ```
   应该看到:
   [GameState] RoomStateNotify: 4 players
   [GameState] PlayerReadyNotify: playerId=10000, ready=true
   ...
   [GameState] GameStartNotify: round=1, currentPlayer=10000, myCards=4
   ```

3. **如果没有看到 GameStartNotify**:
   - 这意味着修复没有生效
   - 检查是否重新编译了客户端
   - 检查 `NetworkClient.cpp` 的修改是否正确

4. **验证修复是否应用**:
   ```bash
   cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/cli_client/src"
   grep -A 20 "bool NetworkClient::hasMessage" NetworkClient.cpp | head -25
   ```
   
   应该看到:
   ```cpp
   // CRITICAL FIX: Check recvBuffer_ first before checking socket
   if (recvBuffer_.size() >= 4) {
   ```

---

## 📊 预期消息流

### 正常流程的消息序列:

```
1. 房主输入 "start"
   ↓
2. 客户端发送: StartGameReq
   ↓
3. 服务器发送 (给所有玩家):
   - StartGameRsp (给房主)
   - RoomStartNotify (广播)
   - GameStartNotify (每个玩家单独发送，包含各自手牌)
   - TurnStartNotify (广播)
   ↓
4. 客户端接收并处理:
   - 处理 RoomStartNotify (可能批量接收多条)
   - hasMessage() 检测到 recvBuffer_ 中有 GameStartNotify ✅
   - 处理 GameStartNotify → phase = PLAYING
   - 处理 TurnStartNotify
   ↓
5. waitingRoomLoop 检测到 phase == PLAYING
   ↓
6. 跳出等待室，进入 gameLoop()
   ↓
7. 渲染游戏界面
```

---

## 🎮 基本游戏测试

如果成功进入游戏，可以测试基本操作:

### 测试抽牌:
```
>>> Enter choice: 1
>>> Drawing card...
>>> You drew: [7]
>>> Choose what to do:
    1. Discard and use skill
    2. Replace your cards with this card
```

### 测试替换:
```
>>> Enter choice: 2
>>> Enter slot indices to replace: 0
>>> Attempting to replace slots [0]...
```

### 测试Call CABO:
```
>>> Enter choice: 3
>>> You called CABO!
```

---

## 📝 测试报告

测试完成后，请记录:

- ✅ 成功点: 哪些功能正常工作
- ❌ 失败点: 哪些地方还有问题
- 🐛 新bug: 发现的新问题
- 📋 日志: 服务器和客户端的关键输出

---

## 🆘 常见问题

### Q: 客户端显示 "Connection failed"
**A**: 检查服务器是否在运行，端口8888是否被占用

### Q: 加入房间失败 "Room not found"
**A**: 检查房间码是否输入正确（区分大小写）

### Q: 所有人ready后，房主看不到 "Type 'start'"
**A**: 检查是否所有4个玩家都ready了

### Q: 输入start后没反应
**A**: 这就是我们修复的bug！确认客户端是否重新编译

### Q: 游戏界面显示乱码
**A**: 确保终端支持UTF-8和ANSI转义码

---

## 🔄 清理和重启

如果需要重新测试:

1. **停止所有客户端**: Ctrl+C
2. **停止服务器**: Ctrl+C
3. **重新启动**: 从步骤1开始

---

**祝测试顺利！如有问题，请查看 BUG_FIX_REPORT.md 了解详细技术分析。**
