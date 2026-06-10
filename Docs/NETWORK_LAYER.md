# Network Layer Reference (for Unity Client)

## 2026-06-10 Current Transport

Cabo now uses WebSocket binary messages for the main Unity client and C++ server path. The old raw TCP `[4-byte length][protobuf]` codec is still kept in the repository as a reference/backward-compatible helper, but Unity's `NetworkGateway` now sends and receives pure protobuf payloads over WebSocket.

Current transport:

- Local development URL: `ws://127.0.0.1:8888`
- Cloudflare temporary public URL: `wss://<random>.trycloudflare.com`
- Payload format inside each WebSocket binary message: pure serialized protobuf bytes.
- WebSocket message boundaries replace the old `[4 bytes big-endian length]` frame in the Unity client.
- Protobuf schemas and all `ClientMessage` / `ServerMessage` payloads remain unchanged.

Server transport implementation:

- `MuduoBaseGameServer/src/common/WebSocketCodec.*` owns:
  - HTTP upgrade handshake.
  - `Sec-WebSocket-Accept` generation.
  - masked client-to-server binary frame decoding.
  - unmasked server-to-client binary frame encoding.
  - ping/pong/close control frames.
  - binary continuation frame reassembly.
- `GameServer` keeps one `WebSocketCodec` per connection.
- Existing dispatcher and services continue to receive/send protobuf payload bytes.
- `RoomService::sendTo` and `GameService::sendToPlayer` encode outbound payloads with `WebSocketCodec::encode`.

Unity transport implementation:

- `WebSocketNetworkClient` uses `System.Net.WebSockets.ClientWebSocket`.
- `NetworkGateway` receives complete binary WebSocket messages and calls protobuf decode directly.
- `MessageCodec.EncodePayload` / `DecodePayload` are pure protobuf helpers; the legacy TCP `Encode` / `FeedBytes` remain length-prefixed.
- The home/server-address UI accepts full URLs and normalizes:
  - `https://...trycloudflare.com` -> `wss://...trycloudflare.com`
  - `http://host:port` -> `ws://host:port`
  - bare `host:port` -> `ws://host:port`
- The existing drain-then-render rule remains intact: background receive only queues messages, and Unity state/UI updates happen from `DrainMessages`.

Cloudflare Tunnel boundary:

- Start the local server on `localhost:8888`.
- Start a tunnel such as `cloudflared tunnel --url http://localhost:8888`.
- Convert the generated `https://...trycloudflare.com` URL to `wss://...trycloudflare.com` in the Unity client.
- 2026-06-09 validation used `wss://currently-warming-assigned-genes.trycloudflare.com` and confirmed:
  - public WebSocket handshake returned `101 Switching Protocols`.
  - a protobuf `CreateRoomReq` returned `CreateRoomRsp`.
  - a second WebSocket client joined the same room and received `JoinRoomRsp`.

Detailed implementation docs:

- `Docs/WEBSOCKET_CLOUDFLARE_RUNBOOK.md`
- `Docs/superpowers/plans/2026-06-08-websocket-cloudflare-plan.md`
- `Docs/superpowers/specs/2026-06-08-websocket-cloudflare-design.md`

Historical review warning:

- The existing WebSocket plan/spec were written before implementation and contain sample-code inaccuracies.
- The implemented path follows the `2026-06-09 Plan Review Notes`: case-insensitive handshake parsing, client mask enforcement, continuation frame handling, OpenSSL CMake integration, Unity background receive queueing, and careful split between TCP framing and pure protobuf helpers.

## Transport

- WebSocket long connection
- Frame: WebSocket binary message containing pure protobuf payload bytes.
- Legacy TCP reference: `MessageCodec.*` and `cli_client/src/NetworkClient.cpp` still show `[4 bytes big-endian length][protobuf payload]`.

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

## Sync & Display Challenges (Critical for Unity)

These are the issues discovered during CLI client development. Unity must handle them correctly:

### 1. Message Batching
TCP delivers multiple messages in one `recv()`. The CLI pattern: `drainMessages()` reads ALL available messages before any render/decision. **Without this, messages are processed one-at-a-time and UI decisions are made on stale state.**

### 2. Action + Turn Transition Timing
`ActionResultNotify` and `TurnStartNotify` arrive in the same TCP frame. Server now has 1.5s delay between them, but Unity must still: process Action → update display → wait for TurnStart → switch current player.

### 3. Multi-Card Replace Layout Change
When multi-replace succeeds, card count decreases (N out, 1 in). Unity must rebuild card layout: remove selected slots, shift remaining cards, add new card at end.

### 4. Card State After Skills
- `PeekSelf`: `myCards[slot].isKnown = true` from UseSkillRsp
- `Swap`: `myCards[slot].isKnown = false` + `ActionResultNotify` handles target player too
- Skill results display for 2 seconds before turn switches

### 5. Round Reveal Panel
`RoundRevealNotify` and `GameStartNotify` (next round) arrive in rapid succession. The `roundJustRevealed` flag ensures the reveal panel is shown before the new round's UI appears. Unity should: show panel → wait for user tap → transition.

### 6. Inter-Round Ready Sync
After `isReady` is reset by `RoomService::handleStartGame`, a `RoomStateNotify` is broadcast. Unity must update the ready checklist on each `RoomStateNotify` and show who is ready / not ready.

### 7. Opponent Card Counts
Updated via `ActionResultNotify.player_hands[]` (not `TurnStartNotify`). Unity must track card counts for each opponent and update on every action broadcast.

### 8. First Turn Special Case
Discard pile starts empty. Unity must hide/disable "Take from discard" button when `turnNumber == 1` or `discardPileCount == 0`.

### 9. isFinalRound Reset
This flag is set by `TurnStartNotify` but only reset by `GameStartNotify` (new round). Unity must reset the CABO button visibility on each new round, not just on turn changes.

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
