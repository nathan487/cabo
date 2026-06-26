# Historical Game Session — Round 3 Final Round

> Captured on 2026-06-06 with 4 CLI clients and the early muduo server prototype.

This document is retained only as a historical gameplay trace. It is not the current architecture reference.

Current implementation has since changed in several important ways:

- Unity is now the main client path;
- WebSocket binary protobuf is the main transport;
- rooms support variable player counts instead of the original fixed 4-player prototype assumption;
- reconnect and StateSync recovery have been added;
- room browser / access application / invitation flows have been added.

Use these current references instead:

- `Docs/PROJECT_OVERVIEW.md`
- `Docs/ARCHITECTURE.md`
- `Docs/NETWORK_LAYER.md`
- `Docs/BACKEND_RESUME_PROBLEM_LOG.md`

## Why keep this file?

The old trace is still useful for understanding the original Cabo gameplay loop:

1. player draws from deck or takes from discard;
2. player may discard drawn card, replace hand cards, or use skill;
3. action result is broadcast to all players;
4. hidden card values are only visible to the owning/authorized player;
5. final round and scoring are driven by server state.

For current debugging, prefer live server logs and the latest Unity/GameService tests rather than this historical trace.
