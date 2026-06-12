# Next Session Prompt

Copy this into the next Codex session:

```text
请接手当前 Unity 客户端任务。工作区：

- Workspace: C:\Users\Admin\Desktop\Cabo GameObject
- Unity client: unity dev/New Client_Unity_Base_Cli
- Server: MuduoBaseGameServer

必须先阅读这些文档：

1. Docs/CURRENT_TASK.md
2. Docs/UNITY_CARD_VIEW_MIGRATION.md
3. Docs/UNITY_CLIENT_HANDOFF.md
4. Docs/UNITY_ANIMATION_NOTES.md
5. Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md

当前任务：

开始把游戏牌桌里的“卡牌显示和卡牌动画”从 UI Toolkit 的 VisualElement 卡牌迁移到持久化 uGUI/GameObject CardView。

重要背景：

- 现在 GameTablePanel.cs 里的卡牌不是 Unity GameObject，而是 UI Toolkit VisualElement。
- 真实手牌每次 RenderSeats() 都会根据 GameState 重建。
- 动画牌是 _animationLayer 上的临时 VisualElement clone。
- ReplaceWithDrawn / TakeFromDiscard 很难修，是因为服务器状态一回来，真实手牌已经是最终布局；但动画需要先显示旧布局，只空 selected，再飞弃牌堆，最后 survivors + incoming 到最终布局。
- Swap 已经稳定，原因是 swap 不改变手牌数量，slot 语义不会重排。
- 单牌替换和多牌替换出现过重叠/错误消失，本质是旧布局 clone、最终真实卡、隐藏真实卡三层生命周期纠缠。

架构决策：

- 不要继续在 VisualElement + clone + hide + worldBound 上无限修。
- 不要迁移整个 UI。
- 保留 UI Toolkit 做首页、房间、聊天、日志、按钮、结算面板。
- 只把游戏牌桌里的卡牌视觉和动画迁移成持久化 uGUI/GameObject CardView。
- 第一阶段优先用 uGUI：Canvas + RectTransform + Image，而不是世界空间 SpriteRenderer。

目标新增脚本建议放在：

- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/CardTable/CardView.cs
- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/CardTable/CardSlotView.cs
- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/CardTable/HandView.cs
- unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/CardTable/CardTableView.cs
- 可选：CardArtProvider.cs
- 可选：CardAnimationRunner.cs

第一阶段不要删除旧 GameTablePanel 卡牌代码。让新 CardTableView 和旧 UI 共存，先证明新卡牌层可以渲染和动画。

推荐开发顺序：

1. 使用 unity-mcp-orchestrator skill。
2. 检查 git status，注意已有未提交文件，不要回滚用户/旧会话改动。
3. 阅读 GameTablePanel.cs 当前 RenderSeats、BuildActionAnimationSnapshot、EnqueueActionAnimation、PlayReplaceWithDrawnAction、PlayTakeFromDiscardAction、PlaySwapAction。
4. 创建最小 CardView / CardSlotView / CardTableView：
   - CardView.ShowFront(value)
   - CardView.ShowBack()
   - CardView.SetSelected(bool)
   - CardView.MoveTo(RectTransform target, float duration)
   - CardView.FlipToFront(value, duration)
   - CardView.SetVisible(bool)
5. 先用占位卡面，不需要等待正式美术资源；但结构要支持以后通过 Image/Sprite 替换卡面和牌背。
6. 用合成 4 人 GameState 先渲染本机手牌。
7. 再渲染对手手牌、抽牌堆、弃牌堆 anchors。
8. 实现新 CardTableView 里的替换动画，不要依赖 UI Toolkit clone：
   - 单牌替换：selected old card -> discard，selected slot empty，incoming -> final slot，其它手牌不动。
   - 多牌替换：旧手牌冻结，selected old cards -> discard，selected slots empty，然后 survivors + incoming 一起移动到最终 compacted slots。
   - TakeFromDiscard 与替换相同，但 incoming 从弃牌堆开始。
9. 保持 swap 稳定，不要破坏现有稳定行为；迁移后也要验证 swap。
10. 保持 RoundReveal 等待 action animation queue 的行为。

不要改：

- 游戏规则
- protobuf schema
- 服务器逻辑
- WebSocket transport
- 房间逻辑
- 结算规则
- 聊天/日志侧栏布局
- 整体牌桌布局，除非只是为了挂载新卡牌 Canvas/layer

每次 C# 修改后必须验证：

1. validate_script
2. refresh_unity(scope="scripts", compile="request", wait_for_ready=true)
3. read_console(types=["error","warning"])
4. 最终目标是 Console 0 errors / 0 warnings；如果只有无关 MCP warning，要明确说明。

合成验证至少覆盖：

- 4 人 active game
- 本人单牌 ReplaceWithDrawn
- 本人多牌 ReplaceWithDrawn
- 本人单牌 TakeFromDiscard
- 本人多牌 TakeFromDiscard
- 对手 replace/take，隐藏值只显示背面
- Swap cross movement
- Draw / DiscardDrawn
- PeekSelf / Spy 隐私规则不破坏
- action animation pending 时收到 RoundReveal，必须等动画结束再进结算

截图验证要求：

- 不要只截最终态。
- Replace/Take 至少截 before / phase1 selected empty / phase2 survivors+incoming / after。
- 推荐命名：
  - cardview_replace_single_mid.png
  - cardview_replace_multi_phase1_empty_slots.png
  - cardview_replace_multi_phase2_survivors_incoming.png
  - cardview_take_discard_multi_phase2.png
  - cardview_swap_cross_mid.png
  - cardview_round_reveal_after_queue.png
- Unity MCP 截图是验证产物，不要提交 Assets/Screenshots/ 或 Assets/Screenshots.meta，除非用户明确要求。

完成标准：

- 新卡牌层使用持久化 CardView 对象驱动，不再靠替换动画中的 VisualElement clone/hide/worldBound 纠缠。
- 单牌替换和多牌替换都能清楚渲染“旧牌进弃牌堆 -> selected 槽为空 -> survivors + incoming 到最终位置”。
- 本人视角和对手视角都能看懂。
- 没有卡牌重叠、无关卡消失、临时卡残留、残留高亮或重复 turn 边框。
- 以后替换正式卡牌图片时，只需要换 Sprite/Image/provider，不需要重写动画逻辑。
```

Asset note:

- Unity MCP screenshots under `unity dev/New Client_Unity_Base_Cli/Assets/Screenshots/` are verification artifacts.
- Do not commit screenshot artifacts unless explicitly requested.
