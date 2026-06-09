# Next Session Prompt

Copy this into the next Codex session:

```text
Please first read Docs/UNITY_CLIENT_HANDOFF.md, Docs/CURRENT_TASK.md, Docs/UNITY_GAME_SCENE_TASK.md, and Docs/UNITY_ANIMATION_NOTES.md, then continue development in unity dev/New Client_Unity_Base_Cli.

Use Unity MCP for Unity Editor verification. Follow the handoff instructions for connecting MCP; do not rediscover from scratch unless the connection is broken.

Important server rule: I will build/start the server myself. Do not build or start MuduoBaseGameServer unless I explicitly ask.

Current latest local state:
- The room/chat/avatar feature exists.
- The latest local gameplay/UI fix adds pre-send confirmation/return flow for replace/take/peek/spy/swap decisions, clearer game logs with exact slot numbers, and a polished round reveal panel.
- Round reveal now waits for queued player action animations to finish before showing settlement. The UI no longer jumps from settlement back to the game panel because an action animation fires late.
- Round reveal layout now fits 4 players, score rows, next-round ready badges, the ready button, and waiting text inside the green table area. Reveal cards use clean numeric-only display so skill labels do not squeeze card faces.
- The latest local UI fix completes the in-game comfortable two-column layout.
- The latest follow-up also simplifies the in-game center table and fixes unreadable button states.
- Key files touched across recent UI/gameplay work: Assets/Scripts/Core/GameFlow.cs, GameState.cs, Assets/Scripts/UI/RoomChatPanel.cs, RoomPanel.cs, GameTablePanel.cs, UIManager.cs.
- `GameFlow` has return helpers for selection rollback before server send: `ReturnToMainInput`, `ReturnToDrawnDecision`, `ReturnToSkillStart`, `ReturnToSkillTargetSelection`.
- `GameState.BuildActionMessage` now formats slots as player-facing 1-based positions such as `第 1、3 张牌`.
- In-game `GameTablePanel` now uses two columns:
  - Left `TablePlayArea`: full card table, all seats, center table, and self hand.
  - Right `TableSocialPanel`: fixed 300px chat/log sidebar.
- `TableSocialPanel` was moved out of `TableMiddle`; chat/log content no longer squeezes the center table or changes opponent/self card layout.
- Normal in-game center table is intentionally quiet now: ordinary gameplay shows only draw pile and discard pile until the player has an actual decision to make.
- `_actionPanel` appears only for decision substates such as AwaitingMainInput, draw decision, replace/take selection, and skill selection.
- Action panel title/body text is hidden in ordinary action states, leaving compact action buttons.
- Reveal and GameOver screens still re-enable the round/turn labels for context.
- `UIManager.ApplyReadableButtonStyle` is the shared button readability helper.
- Runtime fallback now distinguishes enabled and disabled buttons; disabled buttons are dark with muted readable text, preventing white-on-white labels.
- Game chat uses `RoomChatPanel(flow, compact: true, fillHeight: true)`, so message history flex-fills the sidebar and the input row stays pinned near the bottom.
- Chat message history is now fixed-size and scrollable in both waiting room and in-game chat.
- New messages auto-scroll to bottom with an immediate scroll plus delayed fallback after UI Toolkit layout settles.
- Waiting-room content is now fixed-height and horizontal: player list on the left, room chat on the right. This avoids the Windows exe/player build clipping the chat input row differently from Play Mode.
- Waiting-room player list is fixed-height scrollable, so 3-4 players should not deform buttons/chat.
- Waiting-room chat controls use build-safe ASCII labels: Emoji, Close, Send.
- Waiting-room sticker popup is larger and easier to read: about 72x72 buttons with about 58px sticker images, shown above the input row.
- In-game emoji popup opens above the input row. Current 4 stickers fit on one row, and small sticker sets hide the sticker scrollbar.
- Runtime UI fallback no longer overrides compact button/TextField widths.

Unity MCP verification already performed:
- AssetDatabase.Refresh + compile completed with 0 console errors/warnings.
- Synthetic swap-skill flow kept the selected own card highlighted while choosing an opponent card and showed the prompt `请点击您想换的对手的牌。`.
- Reflection/action-log checks verified replace/take/peek/spy/swap logs include exact slot wording.
- Synthetic 4-player round reveal screenshot verified the settlement panel stays within the table area and shows the ready button / waiting text without clipping.
- Reveal sequencing check verified `pending_after_reveal=True` while an action animation was queued, then `phase=RoundReveal` and `pending=False` after the queue drained.
- Synthetic 4-player Play Mode idle game state showed only draw pile/discard pile in the center table.
- Synthetic 4-player Play Mode action state showed compact action buttons only.
- Button readability was verified in Play Mode with enabled and disabled buttons visible at the same time.
- Disabled `拿弃牌` measured as dark background with muted readable text, not white-on-white.
- Button readability screenshot artifact: Assets/Screenshots/game_center_buttons_readable-2.png.
- Synthetic 4-player Play Mode state with 80 long chat messages kept the in-game chat panel fixed.
- Auto-scroll test: rendered 79 messages, forced the game chat scroll offset to 0, appended message 80, re-rendered, waited 450ms, measured offsetY=maxY and atBottom=True.
- Exact auto-scroll measurement: offsetY=8116.8; maxY=8116.8; delta=0.0; atBottom=True; childCount=80.
- Synthetic 4-player waiting-room state with sticker popup open showed the input field, Close, Send, and enlarged stickers visible.
- Waiting-room popup screenshot artifact: Assets/Screenshots/waiting_room_chat_popup_above_input.png.
- Two-column game layout verification screenshots:
  - Assets/Screenshots/game_two_column_chat_final.png
  - Assets/Screenshots/game_two_column_log_final.png
  - Assets/Screenshots/game_two_column_emoji_reflection.png
  - Assets/Screenshots/waiting_room_after_two_column_refactor.png
- Measured game layout after the two-column refactor:
  - TablePlayArea approximately 947px wide.
  - TableSocialPanel approximately 300px wide and full table height.
  - Emoji popup gap to input row: 8.6px.
  - Sticker tray children: 4.
  - Sticker scrollbar: Hidden.
- Verification screenshots may exist in Assets/Screenshots; treat them as temporary artifacts unless I say to keep them.

Recommended next action:
Run a real live verification with my server/bots when I tell you the server is ready. Check waiting room and game scene with 2/3/4 players, overflow chat history, confirm every new message scrolls to the bottom without resizing the panel or deforming the game table, and play through a final action into round reveal to confirm the last action animation completes before settlement appears. Rebuild the Windows exe/player when asked and specifically verify the waiting-room input row, Emoji/Send controls, sticker popup, and round reveal panel match Play Mode.

If any issue remains, inspect RoomChatPanel.cs layout and UIManager fallback first.
```

Asset recommendation:

- Stickers: transparent PNG, square 256x256 or 512x512, grouped by pack folder.
- Avatars: transparent or solid-background PNG, square 256x256 or 512x512.
- Prefer ASCII filenames for predictable Unity asset paths.
