# Cabo Multiplayer Card Game

Multiplayer online card game based on the Cabo/Kabo card game.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Server | C++ (muduo network library + protobuf) |
| CLI Client | C++ (TCP socket + protobuf + ANSI terminal) |
| Unity Client | C# (planned) |
| Protocol | TCP + protobuf, `[4-byte big-endian length][protobuf data]` |

## Current Status (2026-06-06)

- **Server**: Complete — room system, turn logic, card dealing, skill system, scoring, multi-round game loop
- **CLI Client**: Complete — full game flow from connect through multi-round play, state-machine-driven non-blocking I/O
- **Protocol**: Stable — all Req/Rsp/Notify messages defined and implemented
- **Unity Client**: Pending — protocol layer ready, need to build UI on top of existing message contract
- **4-player game**: Fixed at 4 players (MVP constraint)

## Project Structure

```
.
├── MuduoBaseGameServer/       # C++ Server
│   ├── src/game/GameService.*   # Authoritative game logic
│   ├── src/room/RoomService.*   # Room management + ready/start
│   ├── src/server/GameServer.cc # Entry point + message dispatch
│   ├── src/network/             # TCP frame codec
│   ├── src/proto/               # Generated protobuf code
│   ├── cli_client/              # C++ CLI debug client
│   │   └── src/
│   │       ├── ClientApp.*        # Main flow + state machine
│   │       ├── NetworkClient.*    # TCP + protobuf
│   │       ├── GameState.*        # Client-side state tracking
│   │       └── UIRenderer.*       # Terminal rendering
│   └── build/
├── Proto/                     # Protocol definitions (.proto)
├── Docs/                      # Documentation
└── unity dev/                 # Unity project
```

## Key Design Principles

- Server is the **sole authoritative** state source
- Client does NOT decide game outcomes — only renders
- All state sync is broadcast by server
- Protocol uses protobuf exclusively (no JSON/XML)
