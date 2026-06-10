# Unity Client Handoff / MCP Quick Start

## 2026-06-10 Fast Resume: In-Game Animation Polish Next

Next requested task:

- Optimize in-game animations for local-player actions and opponent actions.
- Review animation logic, order, timing, smoothness, and whether the player can understand the action without relying on logs.
- Use Unity MCP for compile checks, synthetic Play Mode states, screenshots, and Console verification.

Read first:

- `Docs/CURRENT_TASK.md`
- `Docs/UNITY_ANIMATION_NOTES.md`
- `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`

Primary files:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`

Do not start by rewriting the animation system. First audit the current action animation matrix:

- self draw / discard drawn / replace / take discard;
- self Peek / Spy / Swap / CABO;
- opponent draw / discard drawn / replace / take discard;
- opponent Peek / Spy / Swap / CABO;
- final action animation into `RoundReveal`.

Verification expectation:

- Use synthetic 4-player states through Unity MCP before live-server testing.
- Capture before/mid/hold/after screenshots when checking motion phases.
- Verify no stuck temporary cards, stale highlights, layout jumps, or premature reveal panel.
- Final Console should be `0 errors / 0 warnings`, or any pre-existing unrelated scene warnings must be explicitly documented.

Constraints:

- Do not change protobuf schema, server rules, WebSocket transport, room logic, scoring, or table/chat layout unless the user explicitly expands scope.
- Do not commit Unity MCP screenshot artifacts under `Assets/Screenshots/`.
- Server build/start remains user-owned unless explicitly requested.

## 2026-06-09 Fast Resume: Skill Flow + Logs + Round Reveal

Most recent local work fixed late-round action/reveal sequencing, clarified action logs, and improved the round settlement panel.

Files changed in the latest patch:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`

What changed:

- Before sending requests to the server, the player can return to the previous selection in replace/take/skill flows.
- Peek/spy/swap no longer send immediately on every card click; selection is highlighted first, then the player confirms.
- Swap keeps the selected own card highlighted while choosing the opponent card.
- Swap target prompt is now `请点击您想换的对手的牌。`
- Skill-card alternative button text supports `改为使用xxx`.
- Game logs now include exact 1-based slots for replace, take discard, peek, spy, and swap where the protocol provides slot data.
- Failed exchange logs explain incoming-card / penalty-card hand additions.
- `UIManager` now delays `RoundReveal` while `GameTablePanel` still has queued action animation.
- When the queued animation finishes, `GameTablePanel` calls back into `UIManager` so the settlement panel appears automatically.
- Round reveal layout is tighter and stable:
  - compact reveal pile cards,
  - score rows in an internal scroll area,
  - 4-player reveal data visible in the green table area,
  - next-round ready badges/buttons/waiting text no longer clip at the bottom,
  - reveal cards hide skill badges and show clean numeric values.

Latest Unity MCP verification:

1. Triggered `AssetDatabase.Refresh()` and script compilation; final clean Unity Console check returned 0 errors/warnings.
2. Entered Play Mode through MCP without starting the server.
3. Injected a synthetic swap-skill flow and verified:
   - selected own card remained highlighted;
   - prompt text was `请点击您想换的对手的牌。`;
   - confirmation/return flow kept the user in control before server send.
4. Used Unity `execute_code` reflection to verify action-log messages:
   - replace two slots -> `第 1、3 张牌`;
   - take discard -> selected slot;
   - peek/spy/swap -> exact source/target slot wording.
5. Injected a synthetic 4-player round reveal state and captured:
   - `Assets/Screenshots/round_reveal_layout_after_card_fix.png`
6. Verified the settlement panel:
   - title/subtitle visible;
   - score panel stays inside the green table area;
   - all 4 player rows fit;
   - ready button and `等待所有玩家准备` are visible;
   - reveal cards are readable and not squeezed.
7. Verified reveal sequencing:
   - when reveal arrives while an action animation is pending, `pending_after_reveal=True`;
   - after waiting for the animation queue, state is `phase=RoundReveal`, `pending=False`, and reveal labels are rendered.
8. Exited Play Mode, loaded `SampleScene`, cleared Console, requested compilation, and final `read_console` returned 0 errors/warnings.

Temporary screenshot artifacts may exist at:

- `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots/`
- `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots.meta`

These screenshots are MCP verification artifacts and should not be committed unless explicitly requested.

Recommended next live verification:

- User starts server and bots.
- Play through a real final action into round reveal.
- Confirm the last action animation completes before settlement appears.
- Confirm 2/3/4-player settlement layouts remain readable in the Windows player build.

Server note: the user builds and starts the server. Do not run server build/start unless explicitly requested.

## 2026-06-09 Fast Resume: Center Table Cleanup + Readable Buttons

Most recent local work reduced the in-game center panel noise and fixed unreadable button states.

Files changed in the latest patch:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`

What changed:

- Normal active gameplay hides `_roundLabel`, `_turnLabel`, and `_statusLine`.
- The center table now shows only draw pile and discard pile unless the local player has a meaningful decision.
- `_actionPanel` defaults hidden and is only shown for decision substates:
  - `AwaitingMainInput`
  - `AwaitingDrawnDecision`
  - `AwaitingReplaceSlots`
  - `AwaitingTakeSlots`
  - skill selection substates
- `_actionTitle` and `_actionBody` remain hidden in ordinary action states so the action UI is a compact button row.
- `RenderReveal()` and `RenderGameOver()` explicitly re-show the round/turn labels for settlement screens.
- Added `UIManager.ApplyReadableButtonStyle(Button button, bool enabled)`.
- Runtime fallback button styling now honors enabled/disabled state.
- Enabled buttons use a dark readable background and light text.
- Disabled buttons use a darker background and muted readable text, avoiding white text on light/default buttons in Play Mode or player builds.
- `GameTablePanel` action/panel buttons now call the shared readable style helper.

Latest Unity MCP verification:

1. Triggered `AssetDatabase.Refresh()` and script compilation; Unity Console returned 0 errors/warnings.
2. Entered Play Mode through MCP without starting the server.
3. Injected a synthetic 4-player active game state.
4. Verified idle/normal center table showed only draw pile and discard pile:
   - `Assets/Screenshots/game_center_minimal_idle_clean.png`
5. Verified action state showed only a compact action button row:
   - `Assets/Screenshots/game_center_minimal_action_buttons_only.png`
6. Verified enabled and disabled buttons were readable at the same time:
   - `Assets/Screenshots/game_center_buttons_readable-2.png`
   - Example measured style: disabled `拿弃牌` background `0.12,0.13,0.18`, text `0.68,0.70,0.76`.
7. Exited Play Mode cleanly.
8. Final Unity Console check returned 0 errors/warnings.

Recommended next verification:

- User starts server and bots.
- Run live 2/3/4 player gameplay.
- Confirm the center table remains visually quiet during ordinary turns.
- Confirm decision buttons remain readable in all enabled/disabled states.
- Rebuild Windows player when asked and compare against Play Mode.

Server note: the user builds and starts the server. Do not run server build/start unless explicitly requested.

## 2026-06-09 Fast Resume: In-Game Two-Column Social Layout

Most recent local work completed the comfortable two-column game-table layout requested by the user.

Files changed in the latest patch:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs`

What changed in the latest patch:

- `CaboGameTable` now lays out as two columns.
- Left `TablePlayArea` contains the complete card table: top opponent, middle table row, and self hand.
- Right `TableSocialPanel` is a fixed `300px` full-height sidebar for the game log / room chat tabs.
- `_socialPanel` was moved out of `TableMiddle`, so chat/log content no longer squeezes or stretches the main table.
- `TableMiddle` now only contains left opponent, center table, and right opponent.
- In-game chat now uses `RoomChatPanel(flow, compact: true, fillHeight: true)`.
- In-game chat messages flex-fill the sidebar, while the input row stays pinned near the bottom.
- The in-game emoji popup opens above the input row, self-sizes to the sticker count, and keeps the current 4 stickers in one row with no visible sticker scrollbar.
- Waiting-room chat keeps the default fixed-height layout and was regression-checked after the refactor.

Latest Unity MCP verification:

1. Triggered `AssetDatabase.Refresh()` and script compilation; Unity Console returned 0 errors/warnings.
2. Entered Play Mode through MCP without starting the server.
3. Injected a synthetic 4-player active game state with chat history.
4. Captured screenshots:
   - `Assets/Screenshots/game_two_column_chat_final.png`
   - `Assets/Screenshots/game_two_column_log_final.png`
   - `Assets/Screenshots/game_two_column_emoji_reflection.png`
   - `Assets/Screenshots/waiting_room_after_two_column_refactor.png`
5. Measured layout:
   - `TablePlayArea` approximately `947px` wide.
   - `TableSocialPanel` approximately `300px` wide and full table height.
   - Emoji popup gap to input row: `8.6px`.
   - Sticker tray children: `4`.
   - Sticker scrollbar: `Hidden`.
6. Waiting-room regression screenshot confirmed the player list, chat messages, text field, Emoji button, and Send button remained visible.
7. Final Unity Console check returned 0 errors/warnings.

Temporary screenshot artifacts may exist at:

- `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots/`
- `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots.meta`

These screenshots are MCP verification artifacts and should not be committed unless the user explicitly asks to keep them.

Recommended next live verification:

- User starts server and bots.
- Test waiting room and game scene with 2, 3, and 4 players.
- Send enough chat messages to overflow the panel.
- Confirm new messages auto-scroll to bottom.
- Confirm the game table does not resize or deform when chat/log/emoji state changes.
- Rebuild Windows player when asked and compare waiting-room/game-scene chat behavior against Play Mode.

Server note: the user builds and starts the server. Do not run server build/start unless explicitly requested.

## 2026-06-09 Fast Resume: Chat Panel Layout Fix

Most recent local work focuses on the room communication panel in both the waiting room and in-game table.

Files changed:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomChatPanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/RoomPanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`

What changed:

- Room chat message display is now a fixed-height scroll area (`RoomChatMessageScroll`), so chat history never stretches the panel or the game table.
- New room-chat messages auto-scroll to the bottom. The code does an immediate `ScrollTo(last)` and a delayed 80 ms retry after UI Toolkit layout settles.
- Waiting-room player list is fixed-height and scrollable; 3-4 players should not deform the buttons/chat area.
- Waiting-room content now uses a fixed-height horizontal layout: player list on the left, room chat on the right. This was added because the Windows exe/player build clipped the old vertically stacked chat input row differently from Play Mode.
- Waiting-room chat controls use build-safe ASCII labels (`Emoji`, `Close`, `Send`) instead of Chinese control text, avoiding player-build font/text-resource fallback issues that made the text field, emoji, and send controls appear missing.
- Waiting-room sticker popup is larger and easier to read: sticker buttons are about `72x72`, sticker images are about `58px`, and the popup opens above the input row instead of covering it.
- In-game social panel width is fixed; chat/log content cannot stretch the main card table.
- Global runtime fallback styling no longer overwrites compact button/text-field widths.

Unity MCP verification already performed:

1. `AssetDatabase.Refresh()` via `execute_code`, waited for compilation.
2. `read_console` returned 0 errors/warnings.
3. Entered Play Mode through `manage_editor`.
4. Injected a synthetic 4-player game state with room chat messages directly into `GameFlow.State`.
5. Rendered the in-game chat tab with 80 long messages.
6. Verified fixed layout: in-game chat panel measured about `245.0x271.0`.
7. Verified auto-scroll:
   - Render 79 messages.
   - Force `RoomChatMessageScroll.scrollOffset.y = 0`.
   - Append message 80.
   - Re-render and wait 450 ms.
   - Measurement: `offsetY=8116.8; maxY=8116.8; delta=0.0; atBottom=True; childCount=80`.
8. Injected a synthetic 4-player waiting-room state and toggled the sticker popup.
9. Verified the waiting-room input field, `Close`, `Send`, and enlarged sticker popup are visible and stable.
10. Screenshot artifact: `Assets/Screenshots/waiting_room_chat_popup_above_input.png`.
11. `read_console` again returned 0 errors/warnings.

Temporary screenshot artifacts may exist at:

- `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots/`
- `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots.meta`

These screenshots were created by MCP verification and are not part of the gameplay feature unless the user explicitly wants to keep them.

Recommended next live verification:

- User starts server and bots.
- Test waiting room with 2, 3, and 4 players.
- Send enough room chat messages to overflow the panel.
- Rebuild the Windows exe/player and specifically confirm the waiting-room input row, Emoji/Send controls, and sticker popup match Play Mode.
- Confirm the message panel stays fixed and each new message scrolls to the bottom in both waiting room and game scene.

Server note: the user builds and starts the server. Do not run server build/start unless explicitly requested.

## 2026-06-08 Latest Stable Commit

Latest committed baseline:

- `39bb458 Add room chat avatars and build UI resources`
  - Room chat protocol/server broadcast and Unity shared chat UI are implemented.
  - Waiting room and game-table chat use the same room chat model.
  - Home avatar selection is implemented; avatar/sticker assets are mirrored to `Assets/Resources/Art` for player builds.
  - Windows player build now loads `GamePanelSettings`, `GameScreen.uss`, `RuntimeTheme.tss`, and UI text resources from `Resources`.
  - UI Toolkit Chinese labels in player builds are fixed by `Assets/Resources/CaboPanelTextSettings.asset` and `Assets/Resources/Fonts/CaboChineseFont.asset`.

Current known issue:

- Chinese labels render in the Windows player. A follow-up patch now keeps the UI Chinese `FontAsset` dynamic with source font + atlas references and enables IME on focused UI Toolkit `TextField`s.
- Next verification: rebuild the Windows player and test Chinese nickname creation/join plus Chinese room chat in waiting room and game scene. Keep server build/start as user-owned.

## 2026-06-08 Fast Resume Update

Latest accepted client state:

- Home/start UI includes server address input, cached last address, connection status, connect button, hidden join-room input, and Exit Game.
- Waiting room includes Leave Room.
- Final GameOver includes Return to Room, Return Home, and Exit Game.
- Latest animation fix is committed as `78958c9 Improve card action animation clarity`.
- Current card action animations hold the previous acting player until animations finish, preserve drawn/incoming cards through replacement, and use slot-specific PeekSelf/Spy motion.

Next task for a new session:

1. Add a framed in-game panel for `游戏日志` and `房间交流`, switchable by buttons/tabs.
2. `房间交流` should support player text chat and sticker/emote sending.
3. Add avatar selection on the home page; show the selected avatar in the waiting room and throughout the game.

Suggested asset paths:

- Stickers: `unity dev/New Client_Unity_Base_Cli/Assets/Art/Stickers/<pack-name>/*.png`
- Avatars: `unity dev/New Client_Unity_Base_Cli/Assets/Art/Avatars/*.png`

Use transparent PNG assets, preferably square 256x256 or 512x512. The user will place the actual files.

Important: the user builds and starts the server. Do not build/start the server unless explicitly requested.

Recommended next-session prompt is also stored in `Docs/NEXT_SESSION_PROMPT.md`.

本文档用于在新的 Codex 会话中快速继续 `unity dev/New Client_Unity_Base_Cli` 的 Unity 客户端开发，避免重新摸索 Unity MCP、当前进度和端到端验证流程。

## 新会话第一句话建议

把下面这段直接发给新的 Codex 会话：

```text
请先阅读 Docs/UNITY_CLIENT_HANDOFF.md、Docs/CURRENT_TASK.md、Docs/UNITY_GAME_SCENE_TASK.md，然后继续开发 unity dev/New Client_Unity_Base_Cli。需要用 Unity MCP。请按 handoff 文档快速启动/连接 MCP，不要重新摸索。当前已验证房主创建房间、3 个 bot 加入 ready、房主 start 后能从 SampleScene 跳到 CaboGameScene。下一个目标是实现真正的多人在线 Cabo 卡牌游戏场景：逻辑/状态流参考 CLI，但 UI 不能照搬 CLI 文本，要做成可交互的卡牌桌界面。
```

## 当前项目路径

- Workspace: `C:\Users\Admin\Desktop\Cabo GameObject`
- Unity client: `C:\Users\Admin\Desktop\Cabo GameObject\unity dev\New Client_Unity_Base_Cli`
- Server: `MuduoBaseGameServer`
- Docs: `Docs`

## 当前已完成状态

Unity 客户端当前已经验证通过最初目标：

- Unity 作为房主连接服务端。
- 输入 nickname 后创建房间。
- 创建房间后 UI 显示 `Room Code: ...`。
- `Copy Code` 按钮可复制房间码到系统剪贴板。
- 3 个 bot/CLI 类客户端加入并 ready。
- Unity 房主 ready 后发送 start。
- 成功从 `SampleScene` 跳转到 `CaboGameScene`。
- 最终运行态示例：

```text
scene=CaboGameScene; connected=True; flow=Playing; phase=Playing; players=4; cards=4
```

Unity Console 在验证中没有游戏逻辑 error/warning；只见过 Unity-MCP 自身的 WebSocket warning，可忽略。

## 关键修复摘要

已修改的重点：

- `Assets/Scripts/Core/NetworkGateway.cs`
  - socket 接收线程只入队消息。
  - `DrainMessages(...)` 在 Unity 主线程 drain，再更新状态和派发事件。
- `Assets/Scripts/Core/GameFlow.cs`
  - `Tick()` 先 drain 全部服务端消息，再做状态机决策。
  - `CreateRoom(nickname)` / `JoinRoom(roomCode, nickname)` 使用 UI 输入昵称。
- `Assets/Scripts/GameBootstrap.cs`
  - 自动创建 `UIDocument`，绑定 PanelSettings/UXML/USS。
  - start 后根据 `GamePhase.Playing` 加载 `CaboGameScene`。
- `Assets/Scripts/UI/RoomPanel.cs`
  - 增加 Nickname 输入框。
  - Room Code 输入框。
  - 创建房间后显示房间码。
  - `Copy Code` 按钮复制房间码。
- `Assets/Scripts/UI/UIManager.cs`
  - rootVisualElement 填满屏幕，避免 Play Mode 空画面。
- `Assets/UI/GamePanelSettings.asset`
  - 已用 Unity API 重建，修复坏 PanelSettings 资产。
- `Assets/UI/RuntimeTheme.tss`
  - Runtime theme: `@import url("unity-theme://default");`
- `ProjectSettings/EditorBuildSettings.asset`
  - 包含 `SampleScene` 和 `CaboGameScene`。

## Unity MCP 快速启动

### 1. 先看 Unity 是否已连接 MCP

Unity 中打开：

```text
Window > MCP For Unity
```

确保 HTTP URL 是：

```text
http://127.0.0.1:8080
```

如果 MCP server 没启动，按下一节启动。

### 2. 启动 MCP server

优先读取当前 Unity 项目生成的脚本，不要复用旧 token：

```powershell
Get-Content "unity dev\New Client_Unity_Base_Cli\Library\MCPForUnity\TerminalScripts\mcp-terminal.cmd"
```

当前脚本形态类似：

```cmd
C:\Users\Admin\.local\bin\uvx.exe --offline --from "mcpforunityserver==9.7.1" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools --pidfile "...RunState\mcp_http_8080.pid" --unity-instance-token <CURRENT_TOKEN>
```

推荐在 PowerShell 后台启动：

```powershell
$env:FASTMCP_CHECK_FOR_UPDATES='off'
Start-Process -FilePath "cmd.exe" `
  -ArgumentList "/c", "`"unity dev\New Client_Unity_Base_Cli\Library\MCPForUnity\TerminalScripts\mcp-terminal.cmd`" > `"unity dev\New Client_Unity_Base_Cli\Library\MCPForUnity\RunState\mcp_http_8080.log`" 2>&1" `
  -WorkingDirectory "C:\Users\Admin\Desktop\Cabo GameObject" `
  -WindowStyle Hidden
```

然后在 Unity 的 MCP For Unity 窗口点击/start session。用户通常会告诉 Codex “已 start session”。

### 3. 验证 HTTP MCP 是否可用

PowerShell 初始化 MCP session：

```powershell
$initBody = @{
  jsonrpc='2.0'
  id=1
  method='initialize'
  params=@{
    protocolVersion='2024-11-05'
    capabilities=@{}
    clientInfo=@{name='codex';version='1'}
  }
} | ConvertTo-Json -Depth 20 -Compress

$init = Invoke-WebRequest `
  -Uri 'http://127.0.0.1:8080/mcp' `
  -Method Post `
  -ContentType 'application/json' `
  -Headers @{Accept='application/json, text/event-stream'} `
  -Body $initBody `
  -UseBasicParsing `
  -TimeoutSec 5

$sid = $init.Headers['mcp-session-id']

$notif = @{jsonrpc='2.0';method='notifications/initialized';params=@{}} |
  ConvertTo-Json -Depth 10 -Compress

Invoke-WebRequest `
  -Uri 'http://127.0.0.1:8080/mcp' `
  -Method Post `
  -ContentType 'application/json' `
  -Headers @{Accept='application/json, text/event-stream'; 'mcp-session-id'=$sid} `
  -Body $notif `
  -UseBasicParsing `
  -TimeoutSec 5 | Out-Null
```

通用 MCP tool 调用函数：

```powershell
$global:McpId = 100

function Invoke-McpToolRaw($sid, $name, $arguments, $timeout=60) {
  $global:McpId++
  $body = @{
    jsonrpc='2.0'
    id=$global:McpId
    method='tools/call'
    params=@{name=$name;arguments=$arguments}
  } | ConvertTo-Json -Depth 80 -Compress

  (Invoke-WebRequest `
    -Uri 'http://127.0.0.1:8080/mcp' `
    -Method Post `
    -ContentType 'application/json' `
    -Headers @{Accept='application/json, text/event-stream'; 'mcp-session-id'=$sid} `
    -Body $body `
    -UseBasicParsing `
    -TimeoutSec $timeout).Content
}

function Invoke-McpTool($sid, $name, $arguments, $timeout=60) {
  $raw = Invoke-McpToolRaw $sid $name $arguments $timeout
  $line = ($raw -split "`n" | Where-Object { $_ -like 'data: *' } | Select-Object -Last 1)
  if (-not $line) { return @{ raw=$raw } }
  $outer = ($line.Substring(6) | ConvertFrom-Json)
  return $outer.result.structuredContent
}
```

常用调用：

```powershell
Invoke-McpTool $sid 'manage_editor' @{action='stop'}
Invoke-McpTool $sid 'read_console' @{action='clear'}
Invoke-McpTool $sid 'manage_scene' @{action='load';path='Assets/Scenes/SampleScene.unity'}
Invoke-McpTool $sid 'manage_editor' @{action='play'}
Invoke-McpTool $sid 'read_console' @{action='get';types=@('error','warning');count='20';format='detailed';include_stacktrace=$true}
```

运行 C# 查询：

```powershell
$code = @'
return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
'@

Invoke-McpTool $sid 'execute_code' @{
  action='execute'
  code=$code
  safety_checks=$true
  compiler='auto'
}
```

截图：

```powershell
Invoke-McpTool $sid 'manage_camera' @{
  action='screenshot'
  capture_source='game_view'
  include_image=$false
  screenshot_file_name='check.png'
  output_folder='Assets/Screenshots'
} 120
```

验证后删除临时截图目录，避免污染 git：

```powershell
$target = Resolve-Path -LiteralPath "unity dev\New Client_Unity_Base_Cli\Assets\Screenshots" -ErrorAction SilentlyContinue
if ($target) { Remove-Item -LiteralPath $target.Path -Recurse -Force }
Remove-Item "unity dev\New Client_Unity_Base_Cli\Assets\Screenshots.meta" -Force -ErrorAction SilentlyContinue
```

## 获取 Unity 当前客户端状态

可用此 C# 片段查询当前状态：

```powershell
$code = @'
var ui = UnityEngine.Object.FindObjectOfType<Cabo.Client.UI.UIManager>();
if (ui == null) return "ui=null";
var field = typeof(Cabo.Client.UI.UIManager).GetField("_flow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var flow = (Cabo.Client.GameFlow)field.GetValue(ui);
var s = flow.State;
int ready = 0;
foreach (var p in s.Players) if (p.IsReady) ready++;
return $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name};connected={flow.Gateway.IsConnected};flow={flow.Flow};phase={s.Phase};roomCode={s.RoomCode};roomId={s.RoomId};my={s.MyPlayerId};players={s.Players.Count};ready={ready};cards={s.MyCards.Count};names={string.Join(",", s.Players.Select(p => p.Nickname + (p.IsReady ? ":R" : ":N") + (p.IsHost ? ":H" : "")).ToArray())}";
'@

Invoke-McpTool $sid 'execute_code' @{
  action='execute'
  code=$code
  safety_checks=$true
  compiler='auto'
}
```

## 端到端验证流程

前提：

- 服务端已启动，监听 `127.0.0.1:8888`。
- 可先验证：

```powershell
Test-NetConnection 127.0.0.1 -Port 8888 | Format-List ComputerName,RemotePort,TcpTestSucceeded
```

### 临时 bot 项目

之前使用过的临时 bot 项目位置：

```text
%TEMP%\CaboBotTest
```

它引用 Unity 生成的 protobuf C# 文件和 `Google.Protobuf.dll`，用于模拟 3 个客户端加入并 ready。

构建：

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "$env:TEMP\CaboBotTest\CaboBotTest.csproj"
```

注意清理旧 bot 进程时不要误杀当前 PowerShell。只杀 dotnet 且命令行包含 CaboBotTest：

```powershell
$old = Get-CimInstance Win32_Process |
  Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*CaboBotTest*' }
foreach ($p in $old) {
  Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}
```

启动 bot：

```powershell
$roomCode = '<ROOM_CODE>'
$botDir = Join-Path $env:TEMP 'CaboBotTest'
$log = Join-Path $botDir 'e2e-bots.log'
$err = Join-Path $botDir 'e2e-bots.err'

$bot = Start-Process `
  -FilePath 'C:\Program Files\dotnet\dotnet.exe' `
  -ArgumentList @('run','--project',(Join-Path $botDir 'CaboBotTest.csproj'),'--',$roomCode) `
  -WorkingDirectory $botDir `
  -WindowStyle Hidden `
  -RedirectStandardOutput $log `
  -RedirectStandardError $err `
  -PassThru
```

成功日志包含：

```text
BOTS_READY
```

### Unity 房主自动化动作

创建房间：

```powershell
$code = @'
var ui = UnityEngine.Object.FindObjectOfType<Cabo.Client.UI.UIManager>();
var field = typeof(Cabo.Client.UI.UIManager).GetField("_flow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var flow = (Cabo.Client.GameFlow)field.GetValue(ui);
flow.CreateRoom("UnityHost");
return "create_sent";
'@

Invoke-McpTool $sid 'execute_code' @{action='execute';code=$code;safety_checks=$true;compiler='auto'}
```

房主 ready：

```powershell
$code = @'
var ui = UnityEngine.Object.FindObjectOfType<Cabo.Client.UI.UIManager>();
var field = typeof(Cabo.Client.UI.UIManager).GetField("_flow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var flow = (Cabo.Client.GameFlow)field.GetValue(ui);
flow.SendReady();
return "host_ready_sent";
'@

Invoke-McpTool $sid 'execute_code' @{action='execute';code=$code;safety_checks=$true;compiler='auto'}
```

房主 start：

```powershell
$code = @'
var ui = UnityEngine.Object.FindObjectOfType<Cabo.Client.UI.UIManager>();
var field = typeof(Cabo.Client.UI.UIManager).GetField("_flow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var flow = (Cabo.Client.GameFlow)field.GetValue(ui);
flow.SendStartGame();
return "start_sent";
'@

Invoke-McpTool $sid 'execute_code' @{action='execute';code=$code;safety_checks=$true;compiler='auto'}
```

期望最终状态：

```text
scene=CaboGameScene; connected=True; flow=Playing; phase=Playing; players=4; cards=4
```

## 已知注意事项

- Unity MCP 的 `execute_code` 有时使用 CodeDom，部分 LINQ/UI Toolkit 扩展方法可能编译不过；可改用显式递归或简单反射。
- MCP server token 会随 Unity 项目/会话变化；总是先读 `mcp-terminal.cmd`。
- 外部用 `apply_patch` 改 C# 后，Unity 可能还没刷新程序集；可通过 MCP 执行：

```csharp
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
return "refresh_requested";
```

- Play Mode 截图会生成 `Assets/Screenshots`，验证后清理。
- `MuduoBaseGameServer/.claude/` 是未跟踪目录，不属于本轮 Unity 客户端改动，通常忽略。

## 下一步开发目标

下一阶段主文档：`Docs/UNITY_GAME_SCENE_TASK.md`。

核心方向：

- CLI 继续作为逻辑参考。
- Unity 游戏场景不能继续做终端式文本 UI。
- `CaboGameScene` 要成为真正的多人在线卡牌桌界面。

继续开发时优先检查：

- 游戏场景 `CaboGameScene` 的实际桌面 UI 是否完整显示 4 名玩家、手牌、牌堆和操作按钮。
- Playing 阶段的主行动流程：Draw / Take discard / Replace / Discard / Skill。
- 按 CLI 的 drain-then-decide 继续补齐每个服务器响应后的 UI 状态切换。
- 每个大改动后用 MCP 截图和 console 验证。
