# Current Task: Unity Client Migration

> 2026-06-06

## Goal

Build a functional Unity (C#) client based on the fully working C++ CLI client and server implementation.

## What We Have

- **Server**: Complete — all game logic, room management, scoring, skills
- **Protocol**: Stable — all message types defined and tested
- **CLI Client**: Complete — full reference implementation for state management, message handling, game flow

## What We Need

A Unity client that renders the game visually:
- Card layout with known/unknown cards
- Player positions (4-player table layout)
- Turn indicator
- Action buttons (draw, take from discard, call CABO)
- Skill UI (peek self, spy, swap)
- Multi-card replace UI
- Score display
- Round reveal panel
- Game over screen

## Key Reference Files

- `Docs/NETWORK_LAYER.md` — Protocol reference
- `Docs/ARCHITECTURE.md` — Server + CLI architecture
- `MuduoBaseGameServer/cli_client/src/` — Reference C++ implementation
