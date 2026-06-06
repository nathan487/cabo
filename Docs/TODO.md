# TODO

## Immediate Next Task

- [ ] **Plan Unity client migration** — Based on working CLI client + stable protocol

## Server Enhancements (Future)

- [ ] Per-player initial peek card selection (currently hardcoded to first 2 cards)
- [ ] Discard pile reshuffle when deck empties mid-round
- [ ] 2-3 player game support (currently fixed 4)
- [ ] Reconnect / state recovery (StateSyncNotify exists but not fully tested)
- [ ] Per-action timers (replace server sleep with muduo timer for non-blocking delays)

## Unity Client (Planned)

- [ ] TCP + protobuf network layer (C# port of NetworkClient)
- [ ] GameState management (C# port of GameState)
- [ ] 4-player table layout UI
- [ ] Card display (known/unknown states)
- [ ] Action UI (draw, take from discard, call CABO)
- [ ] Skill UI (peek, spy, swap)
- [ ] Multi-card replace UI
- [ ] Round reveal panel with ready/start
- [ ] Score display + game over screen
