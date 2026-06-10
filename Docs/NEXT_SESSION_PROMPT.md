# Next Session Prompt

Copy this into the next Codex session:

```text
请先阅读这些文档，快速接手当前 Unity 客户端状态：

1. Docs/CURRENT_TASK.md
2. Docs/UNITY_ANIMATION_NOTES.md
3. Docs/UNITY_CLIENT_HANDOFF.md
4. Docs/NETWORK_LAYER.md
5. Docs/GAME_SESSION.md
6. Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md

当前工作区：

- Workspace: C:\Users\Admin\Desktop\Cabo GameObject
- Unity client: unity dev/New Client_Unity_Base_Cli
- Server: MuduoBaseGameServer

本次新任务：

优化游戏对局中的动画体验，包括玩家本人这边的所有动作，以及对手动作的展示。重点审查并优化动画渲染逻辑、顺序、时间、节奏、顺滑程度和可读性，让玩家不看日志也能理解刚刚发生了什么。

必须先做审查和小计划，不要一上来大规模重写动画系统。先建立 action-animation matrix，覆盖：

- 本人抽牌；
- 本人丢弃刚抽的牌；
- 本人用刚抽的牌替换；
- 本人拿弃牌堆并替换；
- 本人 Peek / Spy / Swap 技能；
- 本人 CABO；
- 对手抽牌；
- 对手丢弃刚抽的牌；
- 对手替换；
- 对手拿弃牌堆；
- 对手 Peek / Spy / Swap 技能；
- 对手 CABO；
- 最后一手动作动画结束后再进入 RoundReveal / 结算面板。

核心目标：

- 动画顺序必须和服务器 ActionResultNotify / TurnStartNotify 的顺序一致。
- 动画要清楚表达谁行动、牌从哪里来、移动到哪里、哪个槽位被影响、结果是什么。
- 本人视角和对手视角都要舒服、清晰。
- 动画不能让牌桌布局跳动，不能影响右侧聊天/日志侧栏。
- 不能有残留的临时牌、残留高亮、重复当前回合边框、卡住的 inspect/skill 状态。
- 最后一手动作还没播放完时，不允许结算界面抢先出现；动画播放完后才进入结算。

重点代码：

- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs
- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs
- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs
- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs

优先审查 GameTablePanel.cs 中这些方法：

- RenderGame()
- BuildActionAnimationSnapshot(...)
- EnqueueActionAnimation(...)
- PlayActionAnimation(...)
- PlaySkillAnimation(...)
- PlayFlyCard(...)
- PulsePlayer(...)
- PulseCard(...)
- RenderSeats(...)
- RenderActionPanel(...)
- ReleaseTurnDisplay(...)
- Tick()

重点检查 GameState.HandleActionResult(...) 写入的字段是否足够支持动画：

- LastActionSequence
- LastActionType
- LastActionSkill
- LastActionSourcePlayerId
- LastActionTargetPlayerId
- LastActionSourceSlot
- LastActionTargetSlot
- LastActionSwapOccurred
- LastActionExchangeSucceeded
- LastActionIncomingCardValue
- LastActionDiscardedCount
- LastActionSelectedSlots
- ActionResultNotify.player_hands

Unity MCP 要求：

- 必须使用 unity-mcp-orchestrator skill。
- MCP endpoint 通常是 http://127.0.0.1:8080/mcp。
- 如果 MCP 断开，先读当前项目生成的：
  unity dev/New Client_Unity_Base_Cli/Library/MCPForUnity/TerminalScripts/mcp-terminal.cmd
  不要复用旧 token。
- 在 Unity 中确认 Window > MCP For Unity 连接到 http://127.0.0.1:8080。

每次 C# 修改后必须验证：

1. AssetDatabase.Refresh()
2. 请求脚本编译
3. 等待 Unity 编译结束
4. read_console 检查 error / warning
5. 最终目标是 Console 0 errors / 0 warnings；如果有已存在且无关的 warning/error，必须说明证据。

建议用 Unity MCP 注入 synthetic 4-player game state，先不依赖真实服务器。至少验证：

- 4 人 active game；
- 本人回合未抽牌；
- 本人抽牌后等待决策；
- 单槽替换；
- 多槽替换；
- 拿弃牌堆；
- Peek 自己；
- Spy 对手；
- Swap 自己槽位和对手槽位；
- 对手 draw/discard/replace/take；
- 对手 skill action；
- CABO；
- action animation pending 时收到 RoundReveal，必须等待动画完成再渲染结算。

截图验收要求：

- 截图不能只截最终态，关键动画需要 before / mid / hold / after。
- 推荐命名：
  - animation_draw_self_before.png
  - animation_draw_self_mid.png
  - animation_replace_multi_hold.png
  - animation_swap_cross_mid.png
  - animation_spy_inspect_hold.png
  - animation_opponent_action_mid.png
  - animation_round_reveal_after_queue.png
- Unity MCP 截图属于验证产物，不要提交 Assets/Screenshots/ 或 Assets/Screenshots.meta，除非用户明确要求。

限制：

- 不修改游戏规则。
- 不修改 protobuf schema，除非先提出单独协议计划并获得确认。
- 不修改 WebSocket transport。
- 不修改服务器逻辑。
- 不修改房间逻辑、结算规则、聊天/日志侧栏布局、牌桌整体布局。
- 不要启动或构建服务器，除非用户明确要求；服务器 build/start 由用户掌控。
- 不要一次性大规模重写。优先小步修改、小步编译、小步截图验证。

完成标准：

- 本人动作和对手动作都能被玩家看懂。
- 动画顺序与真实动作顺序一致。
- 转入下一回合或结算前，上一手动作已经完整展示。
- 4 人牌桌不变形，右侧聊天/日志不抖动。
- 没有卡住的临时牌、残留边框、残留高亮或重复 turn 状态。
- Unity Console 最终为 0 errors / 0 warnings，或明确记录无关的既有问题。
```

Asset note:

- Unity MCP screenshots under `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots/` are verification artifacts.
- Do not commit screenshot artifacts unless explicitly requested.
