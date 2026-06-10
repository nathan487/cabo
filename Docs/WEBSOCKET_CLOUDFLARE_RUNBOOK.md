# WebSocket + Cloudflare Runbook

## Current Transport

- Local Unity URL: `ws://127.0.0.1:8888`
- Cloudflare Unity URL: `wss://<name>.trycloudflare.com`
- WebSocket binary message payload: serialized protobuf bytes only.
- The old TCP `[4-byte length][protobuf]` codec is still kept in code for reference/backward compatibility, but the main Unity gateway now uses WebSocket.

## Start Locally

The server is built and run from WSL:

```bash
cd "/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer/build"
cmake ..
make -j1 GameServer websocket_codec_test
./websocket_codec_test
LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer 8888
```

Do not omit `LD_PRELOAD`; protobuf is intentionally loaded that way so the server keeps using the system `libstdc++`.

## Cloudflare Quick Tunnel

In a separate WSL terminal after the local server is listening:

```bash
cloudflared tunnel --url http://localhost:8888
```

Cloudflare prints a temporary URL like:

```text
https://example.trycloudflare.com
```

Enter either of these in the Unity home screen:

```text
wss://example.trycloudflare.com
https://example.trycloudflare.com
```

The Unity client normalizes `https://` to `wss://` before connecting.

## Verification Status

Checks completed:

- WSL `cmake ..`
- WSL `make -j1 GameServer websocket_codec_test`
- WSL `./websocket_codec_test`: 10 passed, 0 failed
- WSL runtime dependency check with protobuf preload:
  - `libprotobuf.so.31` from anaconda preload
  - `libmymuduo.so` from `MuduoBaseGameServer/lib`
  - `libcrypto.so.1.1` from system library path
  - `libstdc++.so.6` from system library path
- Unity MCP script refresh/compile request executed successfully.
- Local raw WebSocket handshake to `ws://127.0.0.1:8888` returned `101 Switching Protocols`.
- Cloudflare quick tunnel generated:

```text
https://currently-warming-assigned-genes.trycloudflare.com
wss://currently-warming-assigned-genes.trycloudflare.com
```

- Public `wss://currently-warming-assigned-genes.trycloudflare.com` WebSocket handshake returned `101 Switching Protocols`.
- Temporary .NET protobuf/WebSocket test over the Cloudflare `wss://` URL succeeded:
  - first client sent `CreateRoomReq` and received `CreateRoomRsp`;
  - second client sent `JoinRoomReq` and received `JoinRoomRsp` for the same room;
  - test output ended with `CLOUDFLARE_WS_PROTOBUF_OK`.

Manual checks still recommended:

- Local Unity `ws://127.0.0.1:8888` end-to-end room/game flow.
- Cloudflare `wss://...trycloudflare.com` with two real Unity clients through waiting room, ready/start, and at least one game action.

Known Unity Console note:

- Current Console readback shows existing `The referenced script (Unknown) on this Behaviour is missing!` asset errors.
- No C# compile errors were reported by the MCP compile/console readback, but these existing asset errors prevent a literal `0 errors` Console until the missing script references are cleaned separately.
