# TODO

## Immediate Next Task

- [ ] **Optimize in-game animation experience** - Review and improve local-player and opponent action animations, including order, timing, smoothness, readability, and round-reveal handoff. Start from `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`.

## Server Enhancements (Future)

- [ ] Per-player initial peek card selection (currently hardcoded to first 2 cards)
- [ ] Discard pile reshuffle when deck empties mid-round
- [ ] 2-3 player game support (currently fixed 4)
- [ ] Reconnect / state recovery (StateSyncNotify exists but not fully tested)
- [ ] Per-action timers (replace server sleep with muduo timer for non-blocking delays)

## Unity Client

- [x] WebSocket + protobuf network layer for the main client path
- [x] GameState management
- [x] 4-player table layout UI
- [x] Card display (known/unknown states)
- [x] Action UI (draw, take from discard, call CABO)
- [x] Skill UI (peek, spy, swap)
- [x] Multi-card replace UI
- [x] Round reveal panel with ready/start
- [x] Score display + game over screen
- [ ] In-game animation polish for local and opponent actions
