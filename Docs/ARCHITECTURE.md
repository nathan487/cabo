# Architecture

## Server Architecture

```
GameServer (TCP entry point)
├── RoomService (room CRUD, Ready/Start state machine)
│   └── PlayerInfo: playerId, nickname, seatId, isReady, isHost, conn
└── GameService (authoritative game logic)
    ├── GameRoom: step, players[], drawPile[], discardPile[], turn state
    ├── GameStep: WaitingToStart → Playing → WaitingDrawDecision → Playing → ...
    ├── PlayerGameState: cards[], knownSlots[], totalScore
    ├── Handlers: drawCard, discardDrawn, replaceWithDrawn, takeFromDiscard,
    │             useSkill (peekSelf/spy/swap), callSteady
    └── Turn: endTurn → nextPlayer → sendTurnStart
```

### Game State Machine

```
WaitingToStart  →  (host starts)  →  Playing
Playing  →  (draw)  →  WaitingDrawDecision
WaitingDrawDecision  →  (discard/replace)  →  Playing  →  endTurn
Playing  →  (take from discard)  →  endTurn
Playing  →  (call CABO)  →  Playing (final round)  →  Reveal
Reveal  →  WaitingToStart (next round) or GameOver
```

### Message Flow Per Action

```
Player Action → Rsp (private to actor) → ActionResultNotify (broadcast) → TurnStartNotify (broadcast)
                                                                              ↑ 1.5s delay for animation
```

## CLI Client Architecture

```
ClientApp::run()
├── connectToServer (TCP to muduo)
├── loginFlow / roomFlow (create/join room)
├── waitingRoomLoop (non-blocking select on stdin + socket)
├── gameLoop (GameSubState state machine)
│   ├── drainMessages() — process ALL pending before deciding
│   ├── GameSubState transitions:
│   │   IDLE → AWAITING_MAIN_INPUT
│   │   → WAITING_DRAW_RSP → AWAITING_DRAWN_DECISION
│   │   → WAITING_DISCARD_RSP → SKILL_* → WAITING_SKILL_RSP
│   │   → WAITING_REPLACE_RSP / WAITING_TAKE_RSP
│   │   → WAITING_CALL_STEADY_RSP
│   └── Non-blocking stdin via select() — never blocks network
└── handleRoundRevealPhase (inter-round ready/start loop)
```

### Key Components

| Component | File | Purpose |
|-----------|------|---------|
| NetworkClient | NetworkClient.* | TCP socket + protobuf frame codec + non-blocking select |
| GameState | GameState.* | Client-side state mirroring server |
| UIRenderer | UIRenderer.* | ANSI terminal rendering |
| ClientApp | ClientApp.* | Main flow + GameSubState state machine |
