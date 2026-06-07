# Unity Client Handoff / MCP Quick Start

本文档用于在新的 Codex 会话中快速继续 `unity dev/New Client_Unity_Base_Cli` 的 Unity 客户端开发，避免重新摸索 Unity MCP、当前进度和端到端验证流程。

## 新会话第一句话建议

把下面这段直接发给新的 Codex 会话：

```text
请先阅读 Docs/UNITY_CLIENT_HANDOFF.md、Docs/CURRENT_TASK.md，然后继续开发 unity dev/New Client_Unity_Base_Cli。需要用 Unity MCP。请按 handoff 文档快速启动/连接 MCP，不要重新摸索。当前目标是继续 Unity 客户端开发，已验证房主创建房间、3 个 bot 加入 ready、房主 start 后能从 SampleScene 跳到 CaboGameScene。
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

## 下一步开发建议

继续开发时优先检查：

- 游戏场景 `CaboGameScene` 的实际桌面 UI 是否完整显示 4 名玩家、手牌、牌堆和操作按钮。
- Playing 阶段的主行动流程：Draw / Take discard / Replace / Discard / Skill。
- 按 CLI 的 drain-then-decide 继续补齐每个服务器响应后的 UI 状态切换。
- 每个大改动后用 MCP 截图和 console 验证。
