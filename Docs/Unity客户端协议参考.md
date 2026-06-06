# Unity 客户端协议参考

> 基于 CLI 客户端的完整实现，本文档列出 Unity 客户端需要处理的所有消息和状态。

## 一、网络层

- **协议**: TCP + protobuf
- **帧格式**: `[4 字节大端长度][protobuf 数据]`
- **复用**: `NetworkClient.cpp` 的 TCP/编解码逻辑可直接翻译为 C#

## 二、消息处理清单

### 房间阶段

| 消息 | 处理 |
|------|------|
| `CreateRoomRsp` | 保存 roomId, myPlayerId, roomCode → 进入等待室 |
| `JoinRoomRsp` | 同上 |
| `RoomStateNotify` | 更新 players 列表（id/nickname/seat/isReady/isHost/score） |
| `PlayerJoinNotify` | 添加新玩家（去重） |
| `PlayerReadyNotify` | 更新玩家 isReady |
| `ReadyRsp` | 确认 ready 成功 |
| `StartGameRsp` | 房主收到，标记 gameStartConfirmed |
| `RoomStartNotify` | 所有人收到，等待 GameStartNotify |

### 游戏阶段

| 消息 | 处理 |
|------|------|
| `GameStartNotify` | **核心初始化**：读取 your_view → 构建 myCards + opponent cardCounts + 牌堆信息 + 分数。重置 isFinalRound。 |
| `TurnStartNotify` | 更新 currentPlayerId/turnNumber。**自己的回合**时重置 waiting/hasDrawnCard 标志。更新 isFinalRound。更新牌堆。 |
| `DrawCardRsp` | 设置 hasDrawnCard=true, drawnCardValue, drawnCardSkill。**仅操作者收到。** |
| `DiscardDrawnRsp` | 清除 hasDrawnCard。**仅操作者收到。** |
| `ReplaceWithDrawnRsp` | 多张成功：重排 myCards（弃选中+加新牌）。单张成功：更新槽位。失败：添加牌到手牌。 |
| `TakeFromDiscardRsp` | 同上逻辑。 |
| `UseSkillRsp` | 保存 lastPeekedValue/lastSwapOccurred。**仅操作者收到（值私密）。** |
| `CallSteadyRsp` | 确认 CABO 被接受。 |
| `ActionResultNotify` | **广播给所有人**。处理：更新牌堆计数、更新对手手牌数、处理 swap 卡牌状态更新、检测 turn_ended。**所有技能操作信息（skill_used/source_slot/target_slot）在此消息中。** |
| `RoundRevealNotify` | 设置 phase=ROUND_REVEAL + roundJustRevealed。填充 lastRoundResults（所有玩家手牌值+计分）。更新分数。 |
| `ScoreUpdateNotify` | 更新玩家 totalScore。 |
| `GameOverNotify` | 设置 phase=GAME_OVER，填充 finalRankings。 |
| `StateSyncNotify` | 重连恢复（已实现，Unity 可能需要）。 |

## 三、Unity 渲染所需的关键状态

```csharp
class GameState {
    // 连接
    int64 myPlayerId;
    
    // 玩家列表
    List<Player> players; // id, nickname, seatId, isReady, isHost, totalScore, cardCount
    
    // 自己的手牌
    List<Card> myCards;   // slotIndex, isKnown, value
    
    // 牌堆
    int drawPileCount;
    int discardPileCount;
    int discardTopValue;  // -1 = 未知
    
    // 回合
    int64 currentPlayerId;
    int roundNumber, turnNumber;
    bool isFinalRound;
    
    // 操作中状态
    bool hasDrawnCard;
    int drawnCardValue, drawnCardSkill;
    
    // 结算
    List<RoundResult> lastRoundResults;
    bool roundJustRevealed;
    
    bool isMyTurn() => currentPlayerId == myPlayerId;
}
```

## 四、Unity 动画驱动

每个 `ActionResultNotify` 触发的动画：

| action_type | 动画 |
|-------------|------|
| DRAW | 操作者从牌库抽牌 |
| DISCARD_DRAWN | 操作者弃牌（如果 skill_used!=NONE，播放技能特效） |
| REPLACE_WITH_DRAWN | 操作者替换手牌（exchange_result 含哪些槽位） |
| TAKE_FROM_DISCARD | 操作者从弃牌堆拿牌替换 |
| USE_SKILL + PEEK_SELF | 操作者偷看自己槽位 source_slot |
| USE_SKILL + SPY | 操作者偷看 target_player 的 target_slot |
| USE_SKILL + SWAP | 两玩家交换对应槽位 |
| CALL_STEADY | 操作者喊 CABO |

## 五、回合间结算流程

1. `RoundRevealNotify` → 展示结算面板（所有玩家手牌值+分数）
2. 底部显示各玩家 ready 状态
3. 玩家输入 ready → 房主 start
4. `GameStartNotify` → 新回合开始

## 六、关键注意事项

- `GameStartNotify` 是**私信**（每人不同），需要在每回合开始时重建手牌布局
- `ActionResultNotify` 包含 `player_hands[]` 字段，用于更新对手手牌数
- 技能结果（peeked_value）只私信发给操作者，其他玩家只看到操作动作
- 多张替换成功时牌数减少（N 出 1 进），需要重新对手牌布局
- `endTurn` 后有 1.5 秒延迟，给动画播放时间
