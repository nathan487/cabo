# Cabo CLI Client

C++ command-line client for debugging Cabo game server.

## Features

- 4-player game support
- TCP + protobuf communication with game server
- Real-time terminal UI with ANSI colors
- Full game flow: connect → room → game → result
- All game actions: draw, discard, replace, skills, call CABO
- Round reveal and final rankings display

## Requirements

- C++17 compiler (g++ 7+ or clang++ 5+)
- CMake 3.10+
- protobuf 3.0+
- Linux/macOS/WSL (ANSI terminal support)

## Build

```bash
# 1. Ensure server is built first (generates protobuf files)
cd ../
mkdir -p build && cd build
cmake ..
make

# 2. Build CLI client
cd ../cli_client
mkdir -p build && cd build
cmake ..
make
```

## Run

```bash
# Start server first
cd ../../build
./game_server 8888

# In another terminal, start client
cd ../cli_client/build
./cabo_cli_client
```

## Usage Flow

1. **Connect**: Enter server address (e.g., `127.0.0.1:8888`)
2. **Create/Join Room**:
   - Create: Automatically creates 4-player room, displays room code
   - Join: Enter room code to join existing room
3. **Auto-Ready**: Client automatically sends ready signal
4. **Game Start**: When 4 players ready, host auto-starts game
5. **Play**: Follow on-screen prompts for actions
6. **Result**: View round reveals and final rankings

## Game Actions

### Main Turn Options

- **Draw from draw pile**: Draw a card, then choose to discard or replace
- **Take from discard pile**: Take top card and replace your cards
- **Call CABO**: Trigger final round

### After Drawing

- **Discard**: Discard drawn card (use skill if available)
- **Replace**: Replace one or more of your cards with drawn card

### Skills

- **Peek Self (7-8)**: Look at one of your own cards
- **Spy (9-10)**: Look at opponent's card
- **Swap (11-12)**: Swap your card with opponent's card

### Multi-Card Replace

When taking from discard pile, you can replace multiple cards:
- Enter slot indices separated by spaces: `0 1 2`
- Success: All cards must have the same value
- Failure: Card(s) added to your hand (5+ cards possible)

## Troubleshooting

### Connection Issues

- Verify server is running on specified port
- Check firewall settings
- Try `127.0.0.1` instead of `localhost`

### Build Errors

- Ensure protobuf is installed: `sudo apt-get install libprotobuf-dev protobuf-compiler`
- Verify server built successfully (generates .pb.cc/.pb.h files)
- Check CMake version: `cmake --version` (need 3.10+)

### Runtime Issues

- **EOF/Connection Lost**: Server crashed or closed connection
- **Timeout**: Server not responding, check server logs
- **Invalid Input**: Enter numbers only for choices

## Development

### Project Structure

```
cli_client/
├── src/
│   ├── main.cpp              # Entry point
│   ├── ClientApp.cpp/.h      # Main application logic
│   ├── NetworkClient.cpp/.h  # TCP + protobuf communication
│   ├── GameState.cpp/.h      # Game state management
│   └── UIRenderer.cpp/.h     # Terminal UI rendering
├── build/                    # CMake build directory
├── CMakeLists.txt
└── README.md
```

### Adding Features

1. Modify source files in `src/`
2. Rebuild: `cd build && make`
3. Test with server

### Debugging

Enable verbose logging in `GameState.cpp` - check console output prefixed with `[GameState]`

## Known Limitations

- MVP tool: No persistent config or saved games
- Single-threaded: Blocking I/O model
- No reconnection support
- Fixed 4-player games only
- Terminal-only UI (no graphics)

## License

Part of Cabo GameObject project.
