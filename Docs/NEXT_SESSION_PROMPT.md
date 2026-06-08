# Next Session Prompt

Copy this into the next Codex session:

```text
请先阅读 Docs/UNITY_CLIENT_HANDOFF.md、Docs/CURRENT_TASK.md、Docs/UNITY_GAME_SCENE_TASK.md、Docs/UNITY_ANIMATION_NOTES.md，然后继续开发 unity dev/New Client_Unity_Base_Cli。需要用 Unity MCP，请按 handoff 文档快速启动/连接 MCP，不要重新摸索。服务端 build 和启动由我负责，除非我明确要求，否则不要自行 build/start 服务端。

当前项目状态：Unity 客户端已经完成首页服务器地址输入/缓存/连接状态、创建房间、加入房间、退出游戏、房间页退出房间、GameOver 回到房间/回首页/退出游戏，以及最新卡牌动作动画修复。动画修复已通过我测试，提交为 78958c9 Improve card action animation clarity。

下一个任务：1. 在牌桌界面加入一个合适位置的框，支持点击切换“游戏日志”和“房间交流”；房间交流需要支持玩家间文字对话和表情包。请预留/使用表情资源路径 unity dev/New Client_Unity_Base_Cli/Assets/Art/Stickers/<pack-name>/*.png。2. 在玩家首页增加头像选择，头像资源路径 unity dev/New Client_Unity_Base_Cli/Assets/Art/Avatars/*.png；选择后的头像要在房间页和对局中一直显示。

请先分析现有 RoomPanel、GameTablePanel、GameFlow、GameState、GameScreen.uss，再给出小步实现方案并开始开发。完成后告诉我需要我如何测试；服务端相关 build/start 我来操作。
```

Asset recommendation:

- Stickers: transparent PNG, square 256x256 or 512x512, grouped by pack folder.
- Avatars: transparent or solid-background PNG, square 256x256 or 512x512.
- Prefer ASCII filenames for predictable Unity asset paths.
