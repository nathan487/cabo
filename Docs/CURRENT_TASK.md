# Current Task: Unity Client Migration

## 2026-06-10 Next Task: In-Game Animation Polish

The next requested task is to optimize in-game animation rendering and player experience.

Primary plan:

- `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`

Primary implementation area:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`

Task focus:

- local-player action animation;
- opponent action animation;
- animation order and logic;
- timing and smoothness;
- whether final action animations finish before reveal/settlement;
- whether animations communicate source, target, and result without relying on logs.

Important constraints:

- Use the `unity-mcp-orchestrator` skill and Unity MCP for compile checks, Play Mode state injection, screenshots, and Console verification.
- Do not change game rules, protobuf schema, WebSocket transport, room logic, scoring, or the table/chat layout unless an animation bug absolutely requires it.
- Do not commit MCP screenshot artifacts under `Assets/Screenshots/`.
- Server build/start remains user-owned unless explicitly requested.

Recommended first step in the next session:

1. Read `Docs/UNITY_ANIMATION_NOTES.md`.
2. Read `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`.
3. Inspect current `GameTablePanel.cs` animation methods before editing.
4. Build an action-animation matrix for local and opponent viewpoints.
5. Then implement small, verified animation improvements.

## 2026-06-10 Update: WebSocket + Cloudflare Transport Implemented

The raw TCP transport migration is now implemented for the main Unity client/server path.

Server changes:

- Added `MuduoBaseGameServer/src/common/WebSocketCodec.h/.cc`.
- `WebSocketCodec` handles RFC 6455 HTTP Upgrade, `Sec-WebSocket-Accept`, client mask enforcement, server binary frame encoding, ping/pong/close, partial TCP reads, and binary continuation reassembly.
- `GameServer` now keeps one `WebSocketCodec` per connection and passes decoded protobuf payload bytes to the existing `MessageDispatcher`.
- `RoomService` and `GameService` now send protobuf payloads wrapped as WebSocket binary frames.
- `MessageCodec` remains in the repo for legacy TCP/reference paths.
- `MuduoBaseGameServer/CMakeLists.txt` now finds OpenSSL Crypto and sets `GameServer` RPATH directly to the local muduo library path instead of relying on `patchelf`.

Unity changes:

- Added `Assets/Scripts/Network/WebSocketNetworkClient.cs` using `System.Net.WebSockets.ClientWebSocket`.
- `NetworkGateway` now sends and receives pure protobuf bytes over WebSocket while preserving the existing background-queue plus main-thread `DrainMessages` behavior.
- `MessageCodec` now exposes pure protobuf helpers `EncodePayload` / `DecodePayload`; legacy TCP length-prefix `Encode` / `FeedBytes` remain available.
- `GameFlow.ConnectToServerAddress` now accepts WebSocket URLs and normalizes:
  - `https://...` to `wss://...`
  - `http://...` to `ws://...`
  - bare `host:port` to `ws://host:port`
- Default server address is now `ws://127.0.0.1:8888`.

Verified:

- WSL `cmake .. && make -j1 GameServer websocket_codec_test` succeeded.
- `./websocket_codec_test` returned `10 passed, 0 failed`.
- Local WebSocket handshake to `ws://127.0.0.1:8888` returned `101 Switching Protocols`.
- Cloudflare quick tunnel produced:
  - `https://currently-warming-assigned-genes.trycloudflare.com`
  - Unity/client URL: `wss://currently-warming-assigned-genes.trycloudflare.com`
- Public `wss://...trycloudflare.com` WebSocket handshake returned `101 Switching Protocols`.
- A temporary .NET protobuf/WebSocket test client successfully:
  - sent `CreateRoomReq` over the Cloudflare `wss://` URL and received `CreateRoomRsp`;
  - opened a second WebSocket client, sent `JoinRoomReq`, and received `JoinRoomRsp` for the same room.

Known follow-up:

- Real Unity player/editor end-to-end gameplay over the Cloudflare URL still needs a hands-on pass.
- Unity MCP compile refresh succeeded, but the current Editor Console still reports existing `The referenced script (Unknown) on this Behaviour is missing!` asset errors unrelated to the WebSocket C# compile path.
- The account-less Cloudflare quick tunnel URL is temporary and has no uptime guarantee; restart `cloudflared tunnel --url http://localhost:8888` to get a new URL when needed.

## 2026-06-09 Update: Waiting-Room Input Cleanup + Host Crown Badge

Latest local Unity client UI polish in `unity dev/New Client_Unity_Base_Cli`:

- `Assets/UI/GameScreen.uss` and `Assets/Resources/GameScreen.uss`
  - TextField container and inner UI Toolkit input elements are now styled consistently.
  - Added explicit input background, border, radius, zero inner border width, and right padding for:
    - `.unity-base-field__input`
    - `.unity-base-text-field__input`
    - `.unity-text-field__input`
  - This targets the player-build style mismatch where a server-address/input field could show a dark frame plus a small white strip on the right.
- `Assets/Scripts/UI/UIManager.cs`
  - Runtime fallback now applies `UITheme.ApplyInputElement` to all known TextField inner input class variants, reducing Play Mode vs Build differences.
- `Assets/Scripts/UI/UITheme.cs`
  - Added host badge theme colors.
  - Added a generated bitmap crown icon (`HostCrownIcon`) so the waiting-room host marker uses an actual image/icon rather than relying on a font glyph or text-only label.
- `Assets/Scripts/UI/RoomPanel.cs`
  - Waiting-room host marker is now a gold badge containing a crown image plus `房主`.
  - Non-host rows keep the same reserved badge width, preserving row alignment.

Unity MCP verification:

- Exited Play Mode, forced `AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport)`, and re-entered Play Mode.
- Console after refresh/compile returned `0` log entries in the verification readback, and subsequent Play Mode reads showed only normal startup logs.
- Injected a synthetic 4-player waiting room and captured:
  - `Assets/Screenshots/waiting_room_host_badge_icon.png`
- Screenshot confirmed:
  - the host marker displays a real crown icon inside the badge.
  - the badge is visually clear.
  - the waiting-room TextField no longer shows the previous obvious right-side white strip / black-frame artifact.

Screenshot artifacts under `Assets/Screenshots/` are verification output and should not be committed unless explicitly requested.

## Next Major Task: WebSocket + Cloudflare Temporary Public Access

The next session should focus on changing the transport protocol, not UI:

- Replace raw TCP custom `[4-byte big-endian length][protobuf]` framing with WebSocket binary messages.
- Keep protobuf message schemas, game rules, room logic, and Unity gameplay state machine unchanged.
- Server target:
  - add a per-connection WebSocket codec for RFC 6455 handshake and binary frame encode/decode.
  - route decoded binary payloads into the existing protobuf dispatcher.
  - send protobuf payloads as WebSocket binary frames.
- Unity target:
  - add a `ClientWebSocket`-based transport.
  - adapt `NetworkGateway` and connection UI to accept full URLs such as:
    - `ws://127.0.0.1:8888`
    - `wss://xxxx.trycloudflare.com`
  - after WebSocket migration, WebSocket message boundaries replace the old 4-byte length prefix in Unity.
- Cloudflare target:
  - use `cloudflared tunnel --url http://localhost:8888` or the equivalent local service path to expose a temporary `trycloudflare.com` URL.
  - Unity clients should connect with `wss://...trycloudflare.com`.

Important constraint:

- The user owns server build/start unless they explicitly ask Codex to run it.
- Do not change network protobuf schemas, game rules, or UI layout as part of this task unless required for entering a WebSocket URL.
- Read and review the existing WebSocket docs first. They are planning artifacts and may contain implementation gaps:
  - `Docs/superpowers/plans/2026-06-08-websocket-cloudflare-plan.md`
  - `Docs/superpowers/specs/2026-06-08-websocket-cloudflare-design.md`
- The plan now has `2026-06-09 Plan Review Notes` at the top. The next session must treat those notes as mandatory corrections before implementation.

## 2026-06-09 Update: Light Warm Theme Infrastructure and First Visual Pass

Latest local Unity client visual work in `unity dev/New Client_Unity_Base_Cli`:

- `Assets/Scripts/UI/UITheme.cs`
  - Added a centralized runtime theme entry for C# UI Toolkit styling.
  - Uses semantic colors instead of ad hoc raw colors:
    - app/panel/table surfaces
    - primary/secondary/muted text
    - peach buttons
    - current-turn and selected-card highlights
    - CABO danger state
    - ready/waiting states
    - chat/log bubbles
    - card face categories
    - Peek/Spy/Swap skill colors
  - Added helpers for panels, buttons, inputs, borders, alpha colors, card colors, skill colors, and contrast ratio calculation.
- `Assets/UI/GameScreen.uss` and `Assets/Resources/GameScreen.uss`
  - Updated both copies to the same cream green + peach USS variables and default Label/Button/TextField styles.
  - This keeps Editor/Play Mode and player-build stylesheet fallback aligned.
- `Assets/Scripts/UI/UIManager.cs`
  - Runtime fallback now applies `UITheme` root, button, and TextField styles.
  - Global fallback no longer returns the client to the old dark theme.
- `Assets/Scripts/UI/RoomPanel.cs`
  - Home/waiting-room title, room code, status, avatar picker, player rows, ready state, host tag, and chat container now use warm readable theme colors.
- `Assets/Scripts/UI/RoomChatPanel.cs`
  - Chat bubbles, sticker popup, empty state, error text, and active emoji button now use the theme palette.
  - Existing chat layout, popup positioning, and auto-scroll behavior were not changed.
- `Assets/Scripts/UI/GameTablePanel.cs`
  - Main game background, table surface, center/action panels, side social panel, tab state, logs, cards, skill animation colors, ready badges, and seat/CABO/current-turn states now use the warm theme.
  - Layout, VisualElement hierarchy, network logic, state machine, and game rules were not changed.

Validation status:

- Attempted Unity `2022.3.62f3c1` batchmode import/compile with:
  - `Unity.exe -batchmode -quit -projectPath unity dev/New Client_Unity_Base_Cli`
- The process returned exit code 0, but the log contained a Unity Licensing message: `Access token is unavailable; failed to update`.
- Because the current session does not expose active Unity MCP tools and batchmode did not provide a clean Editor Console readback, final visual screenshot validation still needs to be done in a logged-in Unity Editor/MCP session.
- No server build/start was performed.

Known follow-up:

- In Unity MCP, run AssetDatabase refresh / script compilation and confirm Console `0 errors / 0 warnings`.
- Capture Play Mode screenshots for:
  - home page
  - 4-player waiting room with emoji popup
  - 4-player game table chat tab
  - game log tab
  - selected/target card skill states
  - round reveal
  - GameOver
- Do a real Windows player build check when requested, especially waiting-room controls and text contrast.

### 2026-06-09 Follow-up: Contrast Fixes Verified With Unity MCP

The first warm theme pass was visually too low-contrast in several small state labels. Follow-up fixes:

- `Assets/Scripts/UI/UITheme.cs`
  - Deepened the cream/green/peach theme values while keeping the same visual direction.
  - Added separate `TurnBorder` so current-turn borders are darker than the warm turn fill.
  - Deepened selected-card and swap-skill border colors.
- `Assets/Scripts/UI/RoomPanel.cs`
  - Waiting-room `房主` and `已准备` labels now use primary text color instead of low-contrast status colors.
- `Assets/Scripts/UI/GameTablePanel.cs`
  - Round-reveal ready badges now use primary text for `已准备`, preventing green-on-green low contrast.
- `Assets/Scripts/UI/PlayerProfileStore.cs`
  - Fallback avatar initials now choose dark or white text based on avatar background luminance.

Unity MCP verification:

- Refreshed Unity and requested compilation through MCP; final Console after verification was `0 errors / 0 warnings`.
- Play Mode synthetic checks were run for:
  - 4-player waiting room
  - 4-player game table with CABO caller, current turn, skill selection, chat/log sidebar
  - 4-player round reveal
  - GameOver
- Runtime contrast detector checked visible `Label` and `Button` resolved styles:
  - Waiting room: `labelContrastChecked=33; lowLabelCount=0; buttonVisibleCount=8; lowButtonCount=0`
  - Game table: `labelContrastChecked=55; lowLabelCount=0; buttonVisibleCount=8; lowButtonCount=0`
  - Round reveal: `labelContrastChecked=69; lowLabelCount=0; buttonVisibleCount=4; lowButtonCount=0`
  - GameOver: `labelContrastChecked=9; lowLabelCount=0; buttonVisibleCount=5; lowButtonCount=0`
- Screenshot artifacts generated under `Assets/Screenshots/`:
  - `theme_waiting_room_final.png`
  - `theme_game_table_final.png`
  - `theme_round_reveal_final.png`
  - `theme_gameover_final.png`

These screenshot artifacts are verification output and should not be committed unless explicitly requested.

## 2026-06-09 Update: Skill Confirm Flow, Clear Logs, and Round Reveal Polish

Latest verified Unity client work in `unity dev/New Client_Unity_Base_Cli`:

- `Assets/Scripts/Core/GameFlow.cs`
  - Added reversible local flow helpers:
    - `ReturnToMainInput()`
    - `ReturnToDrawnDecision()`
    - `ReturnToSkillStart()`
    - `ReturnToSkillTargetSelection()`
  - These keep the player able to return to the previous decision before a request is sent to the server.
- `Assets/Scripts/Core/GameState.cs`
  - Game-log messages now include player-facing 1-based slot numbers when the protocol provides them.
  - Examples:
    - `p14（你）用抽到的牌替换了第 1、3 张牌`
    - `p14（你）拿弃牌替换了第 2 张牌`
    - `p14（你）将自己的第 2 张牌与 p12 的第 4 张牌交换`
  - Failed multi-card exchanges now explain that the incoming card joined the hand and when an extra penalty card was drawn.
- `Assets/Scripts/UI/GameTablePanel.cs`
  - Skill and exchange selections no longer immediately send every choice to the server.
  - Replace/take/peek/spy/swap flows now expose confirmation and return buttons where appropriate.
  - Skill card action button text now supports `改为使用xxx`.
  - Swap skill keeps the selected own card highlighted while selecting the opponent card.
  - Swap prompt now reads: `请点击您想换的对手的牌。`
  - Round reveal waits for the current queued action animation to finish before the UI switches to the settlement panel.
  - Round reveal layout was tightened so 4 players, score rows, next-round ready badges, ready button, and waiting text fit inside the green table area.
  - Settlement cards use a reveal-specific numeric-only display so skill labels no longer crowd or deform small cards.
- `Assets/Scripts/UI/UIManager.cs`
  - Checks `GameTablePanel.HasPendingActionAnimation` before showing `RoundReveal`.
  - Registers a callback so the UI re-evaluates state as soon as the animation queue drains.

Verified with Unity MCP:

- Triggered `AssetDatabase.Refresh()` and script compilation; final clean Unity Console check returned 0 errors/warnings.
- Play Mode synthetic swap flow verified:
  - selected own card remained highlighted across the swap target steps.
  - prompt showed `请点击您想换的对手的牌。`
- Reflection/sample log verification checked:
  - multi-slot replace logs include `第 1、3 张牌`.
  - take-from-discard logs include the selected slot.
  - peek/spy/swap logs include exact slots.
- Play Mode synthetic round reveal verification checked:
  - 4-player settlement layout stays inside the table area.
  - bottom ready button and `等待所有玩家准备` are visible.
  - reveal cards show clean numeric values without compressed skill labels.
  - action animation pending state delays the reveal panel; after the queue drains, `RoundReveal` renders automatically.
- Screenshot artifacts were generated under `Assets/Screenshots/`, including:
  - `round_reveal_layout_after_card_fix.png`
  - `round_reveal_after_animation_wait_probe.png`
  - These are MCP verification artifacts and should not be committed unless explicitly requested.

Known follow-up:

- Do a real Windows player build verification with the live server/bots when ready.
- Server build/start remains user-owned. Do not build or start the server unless explicitly asked.

## 2026-06-09 Update: Center Table Cleanup + Readable Buttons

Latest local client UI work in `unity dev/New Client_Unity_Base_Cli`:

- `Assets/Scripts/UI/GameTablePanel.cs`
  - Normal in-game center table now stays minimal: only draw pile and discard pile are shown when no player decision is needed.
  - `_roundLabel`, `_turnLabel`, and `_statusLine` are hidden during ordinary gameplay so nonessential text no longer competes with the card table.
  - `_actionPanel` is hidden by default and only appears for real decision substates:
    - `AwaitingMainInput`
    - `AwaitingDrawnDecision`
    - `AwaitingReplaceSlots`
    - `AwaitingTakeSlots`
    - skill selection substates
  - Action panel title/body copy is hidden in normal action states, leaving a compact button row.
  - Reveal and GameOver screens explicitly re-enable round/turn labels so settlement screens still have context.
  - Table action buttons and panel buttons now use a shared readable dark-button style.
- `Assets/Scripts/UI/UIManager.cs`
  - Added `ApplyReadableButtonStyle(Button button, bool enabled)`.
  - Runtime UI fallback now styles enabled and disabled buttons differently instead of applying one generic style to all buttons.
  - Enabled buttons use dark background plus light text.
  - Disabled buttons use darker background plus muted readable text, preventing white-on-white or invisible button labels in Play Mode/player builds.

Verified with Unity MCP:

- Triggered `AssetDatabase.Refresh()` and script compilation; Unity Console returned 0 errors/warnings.
- Entered Play Mode without starting the server.
- Injected a synthetic 4-player active game state.
- Verified normal/idle center table only showed draw pile and discard pile:
  - `Assets/Screenshots/game_center_minimal_idle_clean.png`
- Verified action state showed only a compact action button row:
  - `Assets/Screenshots/game_center_minimal_action_buttons_only.png`
- Verified button readability with enabled and disabled buttons visible at the same time:
  - `Assets/Screenshots/game_center_buttons_readable-2.png`
  - Example measured style: disabled `拿弃牌` resolved to dark background `0.12,0.13,0.18` and muted readable text `0.68,0.70,0.76`.
- Exited Play Mode cleanly.
- Final Unity Console check returned 0 errors/warnings.

Known follow-up:

- The user should still do a real Windows player build verification with live server/bots when ready.
- Server build/start remains user-owned. Do not build or start the server unless explicitly asked.
- MCP screenshots under `Assets/Screenshots/` are verification artifacts and should not be committed unless the user explicitly asks to keep them.

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
