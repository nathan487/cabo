# Cabo CLI Client

C++ command-line client for debugging Cabo game server.

## Build

```bash
cd cli_client
mkdir -p build && cd build
cmake ..
make
```

## Run

```bash
./cabo_cli_client
```

## Features

- 4-player game support
- TCP + protobuf communication
- Real-time terminal UI
- Full game flow: lobby → room → game → result
