# Current Task: Unity Client Migration

> 2026-06-06

## Goal

Build a functional Unity (C#) client based on the fully working C++ CLI client and server implementation.

## What We Have

- **Server**: Complete — all game logic, room management, scoring, skills
- **Protocol**: Stable — all message types defined and tested
- **CLI Client**: Complete — full reference implementation for state management, message handling, game flow

## Unity Development Priorities (Ordered)

### Phase 1: Network + State Sync (Foundation)
The most critical challenge from CLI development — messages arrive in batches and must be drained before rendering decisions are made.

1. **TCP + protobuf layer** — Port `NetworkClient.cpp` encodeFrame/decodeFrame to C#
2. **Message dispatch** — Route `ServerMessage` oneof fields to handlers
3. **State management** — Port `GameState.cpp updateFromMessage()` to C#
4. **Drain-then-decide** — Process ALL pending messages before evaluating UI state (critical for sync)

### Phase 2: Core UI Rendering
5. **Card layout** — Show own cards (known `[value]` / unknown `[?]`), opponent card counts
6. **Turn indicator** — Highlight current player based on `TurnStartNotify.current_player_id`
7. **Action buttons** — Draw / Take from discard / Call CABO menu
8. **Pile display** — Draw pile count, discard pile top card

### Phase 3: Action Animation
9. **Draw animation** — Card from deck to hand area
10. **Replace animation** — Single/multi card swap, card count change on multi-success
11. **Take from discard animation** — Card from discard pile to hand
12. **Skill animations** — PeekSelf (flip own card), Spy (point to opponent), Swap (exchange cards)

### Phase 4: Game Flow
13. **Round reveal panel** — All cards visible + scores. Must handle `roundJustRevealed` to prevent GameStartNotify from hiding it
14. **Inter-round ready** — Show ready status, ready button, host start button
15. **Game over screen** — Rankings display

## Key Sync/Display Challenges (From CLI Experience)

| Challenge | CLI Solution | Unity Needs |
|-----------|-------------|-------------|
| Messages arrive in TCP bursts | `drainMessages()` drains ALL before deciding | Same pattern: read all available, then update UI once |
| ActionResultNotify + TurnStartNotify in same frame | State machine checks phase transitions | 1.5s server delay + process Action first, render, then TurnStart |
| Card count changes (multi-replace) | Rebuild myCards vector | Animate card removal + re-layout |
| Skill result not visible to other players | Broadcast `ActionResultNotify.skill_used + source/target_slot` | Play "peek" or "spy" animation on source/target player cards |
| Round reveal hidden by GameStartNotify | `roundJustRevealed` flag | Show reveal panel, wait for user tap, then transition |
| isFinalRound not reset between rounds | Reset in `GameStartNotify` handler | Reset flag on each new round |
| Inter-round ready state sync | `RoomStateNotify` broadcast after `isReady` reset | Show ready checklist, update on each RoomStateNotify |
| First turn no discard pile | Check `turnNumber` or `discardPileCount == 0` | Hide/disable "Take from discard" button |

## Key Reference Files

- `Docs/NETWORK_LAYER.md` — Protocol reference + animation mapping
- `Docs/ARCHITECTURE.md` — Server + CLI architecture
- `Docs/GAME_SESSION.md` — Captured 4-player game session (reference data)
- `MuduoBaseGameServer/cli_client/src/` — Reference C++ implementation
