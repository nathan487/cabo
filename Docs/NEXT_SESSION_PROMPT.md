# Next Session Prompt

Copy this into the next Codex session:

```text
Please first read Docs/UNITY_CLIENT_HANDOFF.md, Docs/CURRENT_TASK.md, Docs/UNITY_GAME_SCENE_TASK.md, and Docs/UNITY_ANIMATION_NOTES.md, then continue development in unity dev/New Client_Unity_Base_Cli.

Use Unity MCP for Unity Editor verification. Follow the handoff instructions for connecting MCP; do not rediscover from scratch unless the connection is broken.

Important server rule: I will build/start the server myself. Do not build or start MuduoBaseGameServer unless I explicitly ask.

Current latest local state:
- The room/chat/avatar feature exists.
- The latest local UI fix stabilizes the room communication panel.
- Key files touched: Assets/Scripts/UI/RoomChatPanel.cs, RoomPanel.cs, GameTablePanel.cs, UIManager.cs.
- Chat message history is now fixed-size and scrollable in both waiting room and in-game chat.
- New messages auto-scroll to bottom with an immediate scroll plus delayed fallback after UI Toolkit layout settles.
- Waiting-room content is now fixed-height and horizontal: player list on the left, room chat on the right. This avoids the Windows exe/player build clipping the chat input row differently from Play Mode.
- Waiting-room player list is fixed-height scrollable, so 3-4 players should not deform buttons/chat.
- Waiting-room chat controls use build-safe ASCII labels: Emoji, Close, Send.
- Waiting-room sticker popup is larger and easier to read: about 72x72 buttons with about 58px sticker images, shown above the input row.
- In-game social panel has fixed width, so chat/log content should not stretch the table.
- Runtime UI fallback no longer overrides compact button/TextField widths.

Unity MCP verification already performed:
- AssetDatabase.Refresh + compile completed with 0 console errors/warnings.
- Synthetic 4-player Play Mode state with 80 long chat messages kept the in-game chat panel fixed.
- Auto-scroll test: rendered 79 messages, forced the game chat scroll offset to 0, appended message 80, re-rendered, waited 450ms, measured offsetY=maxY and atBottom=True.
- Exact auto-scroll measurement: offsetY=8116.8; maxY=8116.8; delta=0.0; atBottom=True; childCount=80.
- Synthetic 4-player waiting-room state with sticker popup open showed the input field, Close, Send, and enlarged stickers visible.
- Waiting-room popup screenshot artifact: Assets/Screenshots/waiting_room_chat_popup_above_input.png.
- Verification screenshots may exist in Assets/Screenshots; treat them as temporary artifacts unless I say to keep them.

Recommended next action:
Run a real live verification with my server/bots when I tell you the server is ready. Check waiting room and game scene with 2/3/4 players, overflow chat history, and confirm every new message scrolls to the bottom without resizing the panel or deforming the game table. Rebuild the Windows exe/player when asked and specifically verify the waiting-room input row, Emoji/Send controls, and sticker popup match Play Mode.

If any issue remains, inspect RoomChatPanel.cs layout and UIManager fallback first.
```

Asset recommendation:

- Stickers: transparent PNG, square 256x256 or 512x512, grouped by pack folder.
- Avatars: transparent or solid-background PNG, square 256x256 or 512x512.
- Prefer ASCII filenames for predictable Unity asset paths.
