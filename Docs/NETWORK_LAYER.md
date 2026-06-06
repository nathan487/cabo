# Network Layer Reference (for Unity Client)

## Transport

- TCP long connection
- Frame: `[4 bytes big-endian length][protobuf payload]`
- C++ reference: `NetworkClient.cpp` (encodeFrame / decodeFrame)

## Message Protocol

All messages use protobuf. Categories:

| Category | File | Messages |
|----------|------|----------|
| Common | common.proto | CardInfo, SkillType, ActionType, ExchangeAttemptResult |
| Login | login.proto | (placeholder) |
| Room | room.proto | Create/JoinRoom, Ready, RoomState, PlayerJoin/Leave/Ready |
| Game | game.proto | Draw, Discard, Replace, TakeFromDiscard, UseSkill, CallSteady, TurnStart, Action, Reveal, Score, GameOver |
| Sync | sync.proto | StateSync (reconnect) |

## All Messages (Unity Client Must Handle)

### Room Phase

| Message | Direction | Key Fields |
|---------|-----------|------------|
| `CreateRoomRsp` | private | roomId, playerId, roomCode |
| `JoinRoomRsp` | private | roomId, playerId |
| `RoomStateNotify` | broadcast | room.players[](playerId, nickname, seatId, isReady, isHost, totalScore) |
| `PlayerJoinNotify` | broadcast | player info |
| `PlayerReadyNotify` | broadcast | playerId, isReady |
| `ReadyRsp` | private | error |
| `StartGameRsp` | private | error |
| `RoomStartNotify` | broadcast | roomId (game about to start) |
| `PlayerLeaveNotify` | broadcast | playerId (player disconnected/left) |

### Game Phase

| Message | Direction | Key Fields | Render Action |
|---------|-----------|------------|---------------|
| `GameStartNotify` | **private** (per-player) | `your_view.own_cards[]` (slot, isKnown, value), `opponent_hands[]` (cardCount), draw/discard pile, scores[] | Rebuild hand layout, show opponent counts |
| `TurnStartNotify` | broadcast | `current_player_id`, turn/round#, phase (FINAL_ROUND), draw/discard pile | Highlight current player, show menu/wait |
| `DrawCardRsp` | **private** | value, skill | Show drawn card popup |
| `DiscardDrawnRsp` | **private** | error | Card goes to discard |
| `ReplaceWithDrawnRsp` | **private** | `exchange_result` (success, selected_slots, incoming_value, discarded_count, added_count, drew_extra_penalty) | Replace animation or add-to-hand animation |
| `TakeFromDiscardRsp` | **private** | same as above | Same, but source is discard pile |
| `UseSkillRsp` | **private** | peeked_value, swap_occurred | Show peeked value (private), mark swap unknown |
| `CallSteadyRsp` | **private** | error | CABO announcement |
| `ActionResultNotify` | **broadcast** | `action_type`, `source_player_id`, `target_player_id`, `skill_used`, `swap_occurred`, `source_slot`, `target_slot`, `exchange_result`, `player_hands[]`, draw/discard pile, `turn_ended`, `next_player_id` | **Main animation driver** — see below |
| `RoundRevealNotify` | broadcast | `revealed_hands[]` (all card values), `scores[]` (detail) | Show scoring panel |
| `ScoreUpdateNotify` | broadcast | `scores[]` (cumulative) | Update score display |
| `GameOverNotify` | broadcast | `rankings[]` (rank, playerId, nickname, finalScore, isWinner) | Show final standings |

## ActionResultNotify: The Animation Driver

Every action broadcasts this. Unity should use it to:
1. Identify what action happened (`action_type`)
2. Who did it (`source_player_id`)  
3. Who was affected (`target_player_id`, `source_slot`, `target_slot`)
4. Update pile counts and opponent hand sizes (`player_hands[]`)
5. Detect turn end (`turn_ended` → next player highlight)

### Action Types

| action_type | Unity Animation |
|-------------|----------------|
| DRAW | Actor draws from deck |
| DISCARD_DRAWN | Actor discards a card |
| REPLACE_WITH_DRAWN | Actor replaces cards (slots in exchange_result) |
| TAKE_FROM_DISCARD | Actor takes and replaces from discard pile |
| USE_SKILL | Check skill_used: PEEK_SELF / SPY / SWAP |
| CALL_STEADY | Actor calls CABO |

### Skill Animations

When `action_type == USE_SKILL`:
- `skill_used == PEEK_SELF`: Actor peeks own `source_slot` (others see action, not value)
- `skill_used == SPY`: Actor peeks target's `target_slot`
- `skill_used == SWAP`: Two players swap `source_slot` ↔ `target_slot`

## State Sync

`GameState.h` / `GameState.cpp` is the reference implementation. Unity should mirror these fields:

```csharp
// Turn
int64 currentPlayerId;   bool isMyTurn();
int32 roundNumber, turnNumber;
bool isFinalRound;

// Cards
List<Card> myCards;       // slotIndex, isKnown, value
int32 drawPileCount, discardPileCount, discardTopValue;

// Action state
bool hasDrawnCard;        int32 drawnCardValue, drawnCardSkill;

// Scoring
List<RoundResult> lastRoundResults;
bool roundJustRevealed;   // prevents GameStartNotify from hiding reveal panel
```
