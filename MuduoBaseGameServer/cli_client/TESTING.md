# Testing Guide for Cabo CLI Client

## Overview

This document describes how to test the CLI client implementation.

## Prerequisites

1. **Build both server and client**:
```bash
# Build server
cd MuduoBaseGameServer
mkdir -p build && cd build
cmake ..
make

# Build client
cd ../cli_client
mkdir -p build && cd build
cmake ..
make
```

2. **Start the server**:
```bash
cd ../../build
./game_server 8888
```

## Test Scenarios

### 1. Connection Flow

**Test**: Connect to server and enter nickname

```
Steps:
1. Run ./cabo_cli_client
2. Enter: 127.0.0.1:8888
3. Enter nickname: TestPlayer

Expected:
- Connection successful message
- Prompt for room action
```

### 2. Room Creation

**Test**: Create a 4-player room

```
Steps:
1. Choose: 1 (Create room)

Expected:
- Room created with room code (e.g., AB12CD)
- Auto-ready message
- Waiting for players (1/4)
```

### 3. Room Join

**Test**: Join existing room (requires 2 clients)

```
Terminal 1:
1. Create room, note room code

Terminal 2:
1. Run second client
2. Connect and login
3. Choose: 2 (Join room)
4. Enter room code from Terminal 1

Expected:
- Join successful
- Both terminals show updated player list (2/4)
```

### 4. Game Start

**Test**: Start game with 4 players

```
Steps:
1. Connect 4 clients
2. All join same room

Expected:
- When 4th player joins, game starts automatically
- All clients display game board
- First player sees action menu
```

### 5. Draw Card

**Test**: Draw from draw pile

```
Steps:
1. Wait for your turn
2. Choose: 1 (Draw from draw pile)
3. Choose: 1 (Discard) or 2 (Replace)

Expected:
- Drew card message with value
- Skill indication if card has skill
- Options to discard or replace
```

### 6. Take from Discard

**Test**: Single card replacement

```
Steps:
1. Choose: 2 (Take from discard pile)
2. Enter: 0

Expected:
- Success: slot 0 replaced with discard card
- Or failure: card added to hand
```

**Test**: Multi-card replacement (success)

```
Steps:
1. Ensure you have matching cards (e.g., two 5s at slots 0,1)
2. Take card with value 5 from discard
3. Enter: 0 1

Expected:
- Replace successful message
- Both slots now have the taken card value
```

**Test**: Multi-card replacement (failure)

```
Steps:
1. Take card from discard
2. Enter slots with different values: 0 2

Expected:
- Replace FAILED message
- Card added to hand
- Hand size increases to 5 cards
```

### 7. Skills

**Test**: Peek Self (card 7 or 8)

```
Steps:
1. Draw a 7 or 8
2. Choose: 1 (Discard and use skill)
3. Enter slot to peek: 2

Expected:
- "You saw: [value]" message
- That slot remains visible for rest of game
```

**Test**: Spy (card 9 or 10)

```
Steps:
1. Draw a 9 or 10
2. Choose: 1 (Discard and use skill)
3. Choose opponent (1-3)
4. Enter slot to spy (0-3)

Expected:
- "You saw opponent's card: [value]" message
```

**Test**: Swap (card 11 or 12)

```
Steps:
1. Draw an 11 or 12
2. Choose: 1 (Discard and use skill)
3. Enter your slot
4. Choose opponent
5. Enter opponent's slot

Expected:
- "Swap completed!" message
- Your slot becomes unknown (?)
```

### 8. Call CABO

**Test**: Trigger final round

```
Steps:
1. On your turn, choose: 3 (Call CABO)

Expected:
- "Called CABO!" message
- Final round indication
- Each player gets one more turn
- Round reveal after final turn
```

### 9. Round Reveal

**Test**: View round results

```
Expected Display:
- All players' hands revealed with card values
- Hand totals calculated
- Penalties shown (if CABO caller didn't have lowest)
- Round scores and cumulative scores
- "Press Enter to continue"
```

### 10. Game Over

**Test**: Complete full game

```
Expected:
- Game continues until a player reaches 100+ points
- Final rankings displayed:
  - 1st Place: [name] (Score: X) WINNER
  - 2nd Place: [name] (Score: Y)
  - etc.
- "Press Enter to exit"
```

## Error Handling Tests

### Network Errors

**Test**: Connection lost

```
Steps:
1. Start game
2. Kill server (Ctrl+C on server terminal)

Expected:
- "Connection lost" error message
- Client exits gracefully
```

**Test**: Connection timeout

```
Steps:
1. Enter wrong IP address or port
2. Wait for timeout

Expected:
- Connection failed message
- Option to retry
```

### Input Validation

**Test**: Invalid choice input

```
Steps:
1. When prompted for choice, enter: abc

Expected:
- "Invalid input! Please enter a number." message
- Prompt again
```

**Test**: Out of range input

```
Steps:
1. When prompted for choice (1-3), enter: 5

Expected:
- "Input out of range!" message
- Prompt again
```

**Test**: Empty slot input

```
Steps:
1. When replacing cards, press Enter without typing

Expected:
- "No valid slots entered!" message
- Max 5 retries before canceling
```

## Known Issues / Limitations

1. **No reconnection**: If connection drops, must restart client
2. **Single-threaded**: UI updates only when messages received
3. **Terminal size**: Best viewed in 80x24 or larger terminal
4. **ANSI colors**: May not work in some Windows terminals (use WSL)

## Reporting Bugs

When reporting issues, include:
1. Steps to reproduce
2. Expected vs actual behavior
3. Client console output
4. Server console output
5. Operating system and terminal type
