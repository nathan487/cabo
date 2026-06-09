# Current Task: Unity Client Migration

## 2026-06-09 Update: In-Game Two-Column Social Layout Completed

Latest committed-ready client UI work in `unity dev/New Client_Unity_Base_Cli`:

- `Assets/Scripts/UI/GameTablePanel.cs`
  - `CaboGameTable` is now a stable two-column layout.
  - Left column `TablePlayArea` owns the full card table: top opponent, middle table row, and self hand.
  - Right column `TableSocialPanel` is a fixed-width `300px` sidebar for chat/log tabs.
  - `_socialPanel` was moved out of `TableMiddle`, so chat/log content no longer participates in the left/right opponent + center table layout and cannot squeeze or stretch the game table.
  - `TableMiddle` now only contains left opponent, center table, and right opponent.
- `Assets/Scripts/UI/RoomChatPanel.cs`
  - Added a game-sidebar fill mode via `RoomChatPanel(flow, compact: true, fillHeight: true)`.
  - Game chat message list flex-fills the sidebar and the input row stays pinned near the bottom.
  - In-game input bottom spacing is now visually tight, using the sidebar padding instead of a large extra bottom margin.
  - Emoji popup still opens above the input row, self-sizes to sticker count, and keeps the current 4 stickers on one row without a visible scrollbar.
  - Waiting-room chat keeps the normal fixed-height behavior and was not converted to fill mode.

Verified with Unity MCP:

- Triggered `AssetDatabase.Refresh()` and script compilation twice; Unity Console returned 0 errors/warnings.
- Entered Play Mode without starting the server.
- Injected a synthetic 4-player active game state with long room chat history.
- Captured and visually checked:
  - `Assets/Screenshots/game_two_column_chat_final.png`
  - `Assets/Screenshots/game_two_column_log_final.png`
  - `Assets/Screenshots/game_two_column_emoji_reflection.png`
  - `Assets/Screenshots/waiting_room_after_two_column_refactor.png`
- Measured active game layout:
  - `TablePlayArea` approximately `947px` wide.
  - `TableSocialPanel` approximately `300px` wide and full table height.
  - In-game chat message scroll area filled the sidebar.
  - Emoji popup geometry: popup above input row, `gapToInput=8.6`, `trayChildren=4`, `stickerScroller=Hidden`.
- Waiting-room regression check:
  - Text field, Emoji, Send, player list, and chat messages remained visible.
  - No `Enter a room to chat` status line appeared.

Known follow-up:

- The user should still do a real Windows player build verification with live server/bots when ready.
- Server build/start remains user-owned. Do not build or start the server unless explicitly asked.

## 2026-06-09 Update: Room Chat Panel Layout Stabilized

Latest uncommitted client UI work in `unity dev/New Client_Unity_Base_Cli`:

- `Assets/Scripts/UI/RoomChatPanel.cs`
  - Room chat messages now live inside a fixed-height `ScrollView` named `RoomChatMessageScroll`.
  - Waiting-room chat and in-game chat keep stable size/position even when many messages arrive.
  - Sticker tray is popup-based and no longer permanently consumes vertical space.
  - Waiting-room controls now use build-safe ASCII labels (`Emoji`, `Close`, `Send`) to avoid player-build text resource/font fallback differences hiding the input row controls.
  - Waiting-room sticker popup is enlarged (`72x72` buttons, `58px` images) and opens above the input row, so stickers remain readable and do not cover the text field.
  - New messages auto-scroll to the bottom. Implementation schedules an immediate scroll plus an 80 ms delayed fallback after UI Toolkit layout settles.
  - Incremental rendering now rebuilds correctly when the server-side 50-message cap removes older messages while appending a new one.
- `Assets/Scripts/UI/RoomPanel.cs`
  - Waiting-room player list is a fixed-height vertical `ScrollView`, so 3-4 players do not push buttons/chat out of place.
  - Waiting room content now uses a fixed-height horizontal layout: player list on the left and room chat on the right. This matches the Windows player build better than the old vertical stacking and prevents the chat input / Emoji / Send controls from being clipped.
- `Assets/Scripts/UI/GameTablePanel.cs`
  - In-game right social/chat panel has fixed width and hidden overflow, preventing player count or chat content from stretching the card table.
- `Assets/Scripts/UI/UIManager.cs`
  - Runtime fallback styling no longer forces all buttons to `minWidth=104` or all text fields to `minWidth=180`, preserving compact chat controls.

Verified with Unity MCP:

- Triggered `AssetDatabase.Refresh()` and waited for compilation: Unity Console returned 0 errors/warnings.
- Entered Play Mode without starting the server.
- Injected a synthetic 4-player game state with long room chat history.
- Stress case: 4 players + 80 long chat messages. The in-game chat panel remained fixed at about `245x271` inside the social panel.
- Auto-scroll verification: render 79 messages, force `RoomChatMessageScroll.scrollOffset.y = 0`, append message 80, re-render, wait 450 ms.
  - Measured result: `offsetY=8116.8; maxY=8116.8; delta=0.0; atBottom=True; childCount=80`.
- Captured screenshots under `Assets/Screenshots/` during verification. These are test artifacts and should not be committed unless intentionally needed.
- Rechecked the waiting-room exe-specific case in Play Mode by injecting a synthetic 4-player waiting-room state and opening the sticker popup:
  - Input field, `Close`, and `Send` were visible.
  - Sticker buttons measured about `72x72`, with larger readable sticker art.
  - Popup rendered above the input row instead of covering it.
  - Screenshot artifact: `Assets/Screenshots/waiting_room_chat_popup_above_input.png`.
- Final Unity Console check after the waiting-room popup verification returned 0 errors/warnings.

Known follow-up:

- The user should still verify the same behavior in the real Windows player build with live server/bots.
- Server build/start remains user-owned. Do not build or start the server unless explicitly asked.

## 2026-06-08 Update: Room Chat / Avatars / Build Text Baseline

Latest committed work:

- `39bb458 Add room chat avatars and build UI resources`
  - Added protocol/server plumbing for room-level text and sticker chat.
  - Added shared Unity room chat panel for waiting room and game-table chat tab.
  - Added home avatar selection and avatar/sticker resource mirroring into `Assets/Resources/Art`.
  - Added runtime `Resources` copies for UI Toolkit panel/theme/stylesheet assets so Windows player builds render consistently.
  - Added static prewarmed UI Toolkit Chinese `FontAsset` / `PanelTextSettings` so build labels render Chinese.

Current follow-up bug:

- Windows build can render Chinese labels now, but player-entered Chinese text is still not usable for room chat or nickname input.
- Follow-up fix implemented after `39bb458`: `CaboChineseFont.asset` is now prewarmed and dynamic, preserving its source font and atlas textures; UI Toolkit `TextField` focus now explicitly enables Unity IME. Needs user Windows player build verification.

Server note: the user will build and start the server. Do not run server build/start unless explicitly asked.

## 2026-06-08 Latest Handoff Update

Latest committed client work:

- `879aa90 Add home server connection flow`
  - Home page now has server address input, cached last server address, connect status, connect button, hidden join-room input until Join is clicked, and Exit Game.
  - Room page now has Leave Room returning to the home page.
  - Final GameOver page has Return to Room, Return Home, and Exit Game.
- `78958c9 Improve card action animation clarity`
  - PeekSelf and Spy now use slot-specific inspection animations.
  - Previous action turn display is held until the animation queue finishes, avoiding a premature new-turn render.
  - Replace / take-from-discard animations keep the drawn/incoming card visible through the staged discard and slot-empty period.
  - Multi-card replacement uses an old-hand overlay to reduce reflow artifacts and make selected slots readable.

Next development task:

1. Add an in-game framed panel that can switch between Game Log and Room Chat.
2. Room Chat should support player text messages and sticker/emote sending.
3. Add avatar selection on the home page; selected avatar should remain visible in the waiting room and during the game.

Suggested asset paths for the next task:

- Stickers: `unity dev/New Client_Unity_Base_Cli/Assets/Art/Stickers/<pack-name>/*.png`
- Avatars: `unity dev/New Client_Unity_Base_Cli/Assets/Art/Avatars/*.png`

Preferred asset format: transparent PNG, square 256x256 or 512x512. Use ASCII filenames where possible so Unity import paths remain easy to reference.

Server note: the user will build and start the server. Do not run server build/start unless explicitly asked.

> Updated: 2026-06-08

## Goal

Build a functional Unity (C#) client based on the fully working C++ CLI client and server implementation.

Current immediate goal: implement the actual multiplayer card game scene in Unity. The CLI client remains the logic and state-machine reference, but the Unity game scene must be a real visual card table, not a terminal-style clone.

Latest completed task: final `GameOver` now has a `Return to Room` button. It returns the Unity client to the existing waiting-room panel without leaving the room. The server marks the room as waiting after final `GameOver`, clears ready flags, preserves online players, preserves or migrates host ownership, and treats the next Start as a fresh full game with reset cumulative scores.

See `Docs/UNITY_GAME_SCENE_TASK.md` for the next-session task brief.
See `Docs/UNITY_ANIMATION_NOTES.md` for the current Unity card-table animation implementation, including slot-level exchange animations, slower skill-inspection animations, and the CABO caller marker.

## What We Have

- **Server**: Complete — all game logic, room management, scoring, skills
- **Protocol**: Stable — all message types defined and tested
- **CLI Client**: Complete — full reference implementation for state management, message handling, game flow
- **Unity Lobby / Start Flow**: Verified end-to-end — Unity host can create room, bots can join/ready, host can start, and Unity transitions to `CaboGameScene`

## Unity Development Priorities (Ordered)

### Phase 1: Network + State Sync (Foundation)
The most critical challenge from CLI development — messages arrive in batches and must be drained before rendering decisions are made.

1. **TCP + protobuf layer** — Port `NetworkClient.cpp` encodeFrame/decodeFrame to C#
2. **Message dispatch** — Route `ServerMessage` oneof fields to handlers
3. **State management** — Port `GameState.cpp updateFromMessage()` to C#
4. **Drain-then-decide** — Process ALL pending messages before evaluating UI state (critical for sync)

### Phase 2: Core UI Rendering (Current Focus)
5. **Card layout** — Show own cards (known `[value]` / unknown `[?]`), opponent card counts
6. **Turn indicator** — Highlight current player based on `TurnStartNotify.current_player_id`
7. **Action buttons** — Draw / Take from discard / Call CABO menu
8. **Pile display** — Draw pile count, discard pile top card

Important update: the original Phase 2 text describes functional data, not final rendering style. The next implementation should render these as a real card game table:

- card-shaped UI elements instead of `[?]` text
- self at bottom, opponents top/left/right
- visible deck/discard piles
- clickable card slots for selections
- state-specific action panels
- reveal/game-over panels as visual UI, not text logs

### Phase 3: Action Animation
9. **Draw animation** — Card from deck to hand area
10. **Replace animation** — Single/multi card swap, card count change on multi-success
11. **Take from discard animation** — Card from discard pile to hand
12. **Skill animations** — PeekSelf (flip own card), Spy (point to opponent), Swap (exchange cards)

Current Phase 3 status: first pass implemented for deck-to-player draw markers, selected-slot blanking for replace/take, failed exchange shake, opponent hand-count updates from `ActionResultNotify.player_hands`, and Swap slot cross-movement. User screenshot review found and fixed stale dual TURN borders plus 3-or-more failed multi-exchange penalty-card rendering.

### Phase 4: Game Flow
13. **Round reveal panel** — All cards visible + scores. Must handle `roundJustRevealed` to prevent GameStartNotify from hiding it
14. **Inter-round ready** — Show ready status, ready button, host start button
15. **Game over screen** — Rankings display plus Return to Room for a fresh next game

## Key Sync/Display Challenges (From CLI Experience)

| Challenge | CLI Solution | Unity Needs |
|-----------|-------------|-------------|
| Messages arrive in TCP bursts | `drainMessages()` drains ALL before deciding | Same pattern: read all available, then update UI once |
| ActionResultNotify + TurnStartNotify in same frame | State machine checks phase transitions | 1.5s server delay + process Action first, render, then TurnStart |
| Card count changes (multi-replace) | Rebuild myCards vector | Unity now reads `ActionResultNotify.player_hands`; verify live count changes with screenshots |
| Skill result not visible to other players | Broadcast `ActionResultNotify.skill_used + source/target_slot` | Play "peek" or "spy" animation on source/target player cards |
| Round reveal hidden by GameStartNotify | `roundJustRevealed` flag | Show reveal panel, wait for user tap, then transition |
| isFinalRound not reset between rounds | Reset in `GameStartNotify` handler | Reset flag on each new round |
| Inter-round ready state sync | `RoomStateNotify` broadcast after `isReady` reset | Show ready checklist, update on each RoomStateNotify |
| First turn no discard pile | Check `turnNumber` or `discardPileCount == 0` | Hide/disable "Take from discard" button |

## Key Reference Files

- `Docs/UNITY_GAME_SCENE_TASK.md` — Next task brief for the real Unity game scene
- `Docs/UNITY_ANIMATION_NOTES.md` — Current UI Toolkit animation coverage, trigger sources, limitations, and upgrade path
- `Docs/UNITY_CLIENT_HANDOFF.md` — MCP quick start and current verified baseline
- `Docs/NETWORK_LAYER.md` — Protocol reference + animation mapping
- `Docs/ARCHITECTURE.md` — Server + CLI architecture
- `Docs/GAME_SESSION.md` — Captured 4-player game session (reference data)
- `MuduoBaseGameServer/cli_client/src/` — Reference C++ implementation
