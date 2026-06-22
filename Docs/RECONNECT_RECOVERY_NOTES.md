# 掉线重连状态恢复排查记录

日期：2026-06-22

## 背景

第一版重连实现已经能用 `session_token` 在 60 秒内重新绑定连接，并由服务端下发 `StateSyncNotify` 全量快照。实机测试发现：玩家抽牌后尚未选择弃牌、替换或使用技能时断线，重连可以成功回到牌桌，但操作按钮消失，客户端卡住。

## 根因定位

客户端分为两层状态：

- `GameState`：保存服务端快照，例如 `HasDrawnCard`、`DrawnCardId`、`CurrentPlayerId`。
- `GameFlow.SubState`：决定当前 UI 可以显示哪些操作按钮，例如 `AwaitingDrawnDecision` 才会显示弃牌、替换、技能按钮。

`StateSyncNotify` 已经把 `GameState.HasDrawnCard` 和抽到的牌恢复出来，但 `GameFlow.CompleteReconnect()` 把 `SubState` 固定重置为 `Idle`。`GameTablePanel` 的操作面板只看 `SubState`，所以按钮全部消失。

另外发现一个相邻边界：`StateSyncNotify` 当前没有同步 `turn_number`，而 UI 之前用 `TurnNumber > 1` 决定“拿弃牌”是否可用。重连后 `TurnNumber == 0` 表示未知，会误禁用“拿弃牌”。

## 修复方案

### 1. 快照后恢复可操作子状态

在 `GameFlow` 增加快照恢复规则：收到 `StateSyncNotify` 或完成重连时，不再盲目进入 `Idle`，而是根据权威 `GameState` 推导：

- 不是自己的回合：`Idle`
- 自己回合，且有待使用技能牌：恢复到对应技能入口
  - 看自己牌：`SkillPeekSlot`
  - 偷看：`SkillSpyTarget`
  - 换牌：`SkillSwapMySlot`
- 自己回合，且 `HasDrawnCard == true`：`AwaitingDrawnDecision`
- 自己回合，且没有挂起抽牌：`AwaitingMainInput`

`CheckTransitions()` 也加了兜底，避免后续只靠帧循环时再次落入 `Idle`。

### 2. 识别“已弃技能牌，等待选技能”

当前协议没有 `STEP_TYPE_WAITING_SKILL_DECISION`，但服务端快照里有两个可用事实：

- `pending_step.drawn_card_id`
- `discard_pile.top_card.card_id`

如果两者相同，并且这张牌是可用技能牌，说明该牌已经弃出，服务端正在等待 `UseSkillReq`。客户端据此恢复：

- `HasDrawnCard = false`
- `PendingSkillCardId = pending_step.drawn_card_id`
- `PendingSkillCardSkill = pending_step.drawn_card_skill`
- `GameFlow.SubState` 恢复到对应技能选择状态

这样避免把“已弃出的技能牌”错误恢复成“仍可弃牌/替换”的抽牌决策。

### 3. 拿弃牌按钮补偿

`TurnNumber == 0` 在重连快照中代表未知，不应视为第一回合。UI 现在只在明确 `TurnNumber == 1` 时禁用“拿弃牌”；`TurnNumber == 0` 且弃牌堆有牌时允许操作，最终仍以服务端校验为准。

## 边界情况矩阵

| 场景 | 当前恢复结果 | 说明 |
| --- | --- | --- |
| 未入房断线 | 不进入牌局重连 | 没有 `session_token` / `room_id`，只能回首页 |
| 等待房间断线 | 可恢复房间 | `RoomState` 会恢复玩家列表、房主、在线状态 |
| 非当前玩家断线 | 可恢复旁观等待状态 | 回到牌桌但操作面板为等待状态 |
| 当前玩家回合，未抽牌 | 可恢复主操作 | 显示抽牌、拿弃牌、CABO；拿弃牌在 `turn_number` 未知时允许 |
| 抽牌后未操作 | 可恢复抽牌决策 | 显示弃牌、替换、技能按钮 |
| 抽牌后点了“替换”，但未确认槽位 | 可恢复到抽牌决策 | 本地已选槽位不会保留，需要重新选择 |
| 抽牌后点了“弃牌并使用技能”，技能牌已弃出但未选目标 | 可恢复到技能入口 | 通过弃牌堆顶 card id 与 pending card id 匹配推断 |
| 正在选择偷看/换牌目标，断线前已选了部分目标但未发送请求 | 可恢复到技能入口 | 本地已选目标不保留，需要重新选择 |
| 请求已发送但响应丢失 | 以服务端快照为准 | 如果服务端已处理，会恢复到处理后的牌局；如果未处理，会回到处理前可操作状态 |
| 超过 60 秒或 token 无效 | 失败并回首页 | 服务端返回失败，客户端提示离开牌局 |

## 仍无法完全还原的内容

这些属于本地 UI/动画过程，不在当前协议快照内：

- 已选中的替换槽位、偷看目标、换牌双方目标。
- 断线前正在播放的动画进度。
- 断线前已经打开但尚未提交的局部 UI 面板状态。

当前补偿方案是恢复到最近的服务端权威可操作入口，让玩家重新选择。这个策略不会丢服务端牌局状态，但可能无法 1:1 还原断线前的渲染细节。

## 后续协议级增强建议

如果需要把技能/替换过程做到完全精确恢复，建议扩展 `TurnStepState`：

- 新增 `STEP_TYPE_WAITING_SKILL_DECISION`
- 增加 `skill_card_id`、`skill_type`
- 可选增加 `selected_own_slots`、`selected_target_player_id`、`selected_target_slot`

服务端也应显式维护 `pendingSkillCard` / `pendingSkillType`，而不是让客户端通过弃牌堆顶进行推断。这样可以减少隐式规则，让客户端、服务端和测试都更清楚。

## 本次新增/覆盖的回归测试

- `GameFlowReconnectStateTests.StateSyncPendingDrawDecisionRestoresDrawnDecisionSubState`
- `GameFlowReconnectStateTests.StateSyncForCurrentPlayerWithoutPendingDrawRestoresMainInputSubState`
- `GameFlowReconnectStateTests.StateSyncForOtherPlayersTurnRestoresIdleSubState`
- `GameFlowReconnectStateTests.StateSyncDiscardedSpySkillRestoresSkillTargetSubState`
- `GameStateSkillCardTests.StateSyncRestoresDiscardedSkillCardAsPendingSkillDecision`
- `GameTablePanelSfxTests.TakeFromDiscardIsAllowedWhenTurnNumberIsUnknownAfterStateSync`
- `GameTablePanelSfxTests.TakeFromDiscardIsDisabledOnlyForKnownFirstTurnOrEmptyDiscardPile`

## 手工验证建议

1. 开局后轮到自己，点击“模拟断线”，重连后应回到主操作按钮。
2. 抽牌后不做任何操作，点击“模拟断线”，重连后应看到弃牌、替换、技能按钮。
3. 抽到 9/10/11/12 等技能牌，点击“使用技能”使技能牌弃出后，在选择目标前点击“模拟断线”，重连后应回到对应技能选择入口。
4. 在替换/技能选择中选中一部分目标但不确认，点击“模拟断线”，重连后可继续，但需要重新选择目标。
