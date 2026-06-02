# Scene Resource Plan (M1/M2)

## Scope

This plan targets M1 and M2 only:
- M1: network pipeline skeleton (connect, dispatch, state update)
- M2: room loop closure (create, join, ready, start)

## Current Status

- Existing project has a local hot-seat prototype.
- New client skeleton exists in Assets/Scripts/ClientCore.
- New runtime UI can be auto-created by ClientAppBootstrap.

## Scene Plan

1. Scene: BootScene (new)
- Purpose: only startup entry.
- Objects:
  - ClientAppBootstrap (with RoomClientController and LobbyRoomDemoUI)
- Result:
  - Connect and room loop can be tested in one scene.

2. Scene: LobbyRoomScene (new, optional in M2)
- Purpose: production replacement for generated demo UI.
- Objects:
  - Canvas root
  - LobbyPanel
  - RoomPanel
  - EventLogPanel
  - RoomClientController binder object
- Result:
  - Better authored UI while reusing same controller.

3. Scene: GameScene (next phase, M3)
- Not in current implementation.
- Entered after StartGame succeeds.

## Prefab Plan

1. UI_LobbyPanel
- Nickname input
- Connect button
- Create room button
- Join room input/button

2. UI_RoomPanel
- Room code text
- Player list container
- Ready toggle
- Start button

3. UI_PlayerRowItem
- Seat text
- Nickname text
- Host badge
- Ready state tag
- Online state tag

4. UI_EventLog
- Scroll view text log

## Script Binding Plan

1. RoomClientController
- Keep as single room flow source.
- Bind all UI actions to this controller.

2. LobbyRoomDemoUI
- Keep as temporary auto-generated debug panel.
- Replace with authored presenters in next milestone.

## MCP Asset Batch Plan

Batch A (BootScene)
- Create BootScene
- Add ClientAppBootstrap object
- Save scene and set as first scene in build settings

Batch B (LobbyRoomScene base)
- Create Canvas and panel hierarchy
- Create UI_LobbyPanel and UI_RoomPanel prefabs
- Create one UI_PlayerRowItem prefab

Batch C (Bindings)
- Add binder MonoBehaviours
- Bind serialized references
- Verify no missing references

## Verification Checklist

- Enter play mode:
  - Connection status changes to Connected after Connect.
  - Create Room updates room summary.
  - Join Room updates player list.
  - Ready toggle updates local ready state.
  - Start Game requires host and all ready.
- No red errors in Console from new ClientCore scripts.

## Risks

- Existing prototype scripts may still auto-create UI and overlap visuals.
- If overlap occurs, disable old bootstrap objects in the active scene.
- Proto gateway is placeholder for now; real socket/protobuf wiring is M1.5.

## Next Engineering Task

- Replace ProtoGatewayPlaceholder with real transport:
  - TCP/WebSocket binary
  - protobuf serialization
  - messages.proto oneof dispatch
