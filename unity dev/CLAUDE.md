# Unity Client - CLAUDE.md

本文档用于指导 Claude CLI 在 unity dev 子项目中稳定、高质量地实现客户端。

## 1. 目标与边界

项目目标：实现可联机的 Kabo/Cabo 改编客户端（MVP 优先）。

客户端职责（必须做）：
- UI 展示与交互（大厅、房间、对局、结算）
- 动画与表现层反馈
- 网络请求发送与服务端广播消费
- 本地可恢复状态缓存（重连恢复）

客户端非职责（禁止做）：
- 判定胜负
- 洗牌发牌
- 技能合法性校验
- 任何影响结果的权威裁决

关键原则：服务端权威，客户端只提交操作意图并渲染服务端结果。

## 2. 需求来源优先级

发生冲突时按以下优先级执行：
1. Proto 协议（Proto/*.proto）
2. 仓库根 CLAUDE 约束（CLAUDE.md）
3. 设计文档（Docs/GDD.md）
4. 本文件与 skills 说明

协议字段、枚举、oneof 分发行为一律以 proto 为准。

## 3. 当前技术事实

- Unity 项目路径：unity dev/Client
- 协议总入口：Proto/messages.proto
- 网络模型：TCP 或 WebSocket + protobuf
- 序号机制：ClientMessage.seq、ServerMessage.server_seq
- 已配置 Unity MCP：可由 AI 直接生成场景、Prefab、UI 资源

## 4. MVP 功能范围

第一阶段必须完成：
- 连接服务端并收发 ClientMessage/ServerMessage
- 创建房间、加入房间、准备、开始游戏
- 玩家回合主链路：
	- 抽牌 -> 弃牌/替换/技能
	- 从弃牌堆拿牌并替换
	- 喊稳态
- 广播消费与本地状态刷新：
	- TurnStartNotify
	- ActionResultNotify
	- SkillPromptNotify
	- RoundRevealNotify
	- ScoreUpdateNotify
	- GameOverNotify
- 心跳与重连（ReconnectReq + StateSyncNotify）

## 5. 推荐目录规范（Unity）

建议遵循以下结构，不要求一次到位：

Assets/
- Scripts/
	- Core/
	- Network/
	- Proto/
	- Domain/
	- UI/
	- Gameplay/
	- Room/
	- Sync/
- Scenes/
- Prefabs/
- Resources/

建议层次：
- Network 仅做通信与消息分发
- Domain 仅做状态模型与 reducer
- UI 仅绑定 ViewModel，不直接操作 socket

## 6. 编码与架构约束

- 严禁在 Update 中做复杂业务流程推进
- 使用事件总线或消息中心解耦网络与 UI
- oneof 分发必须完整覆盖，未知分支需日志告警
- 任何本地预测状态必须可被服务端快照覆写
- 所有请求必须附带 request_id 与 client_seq（按协议需要）

推荐模式：
- AppState + Reducer + Dispatcher
- RequestTracker（request_id -> 回调/超时）
- ReconnectRecovery（pending step 恢复）

## 7. Claude CLI 工作方式

每次任务执行流程：
1. 先读 Proto，再落代码，不猜字段。
2. 先实现最小闭环（可跑通），再增强体验。
3. 改动后给出验证步骤（PlayMode 或手动）。
4. 对于 Unity 资源与场景，优先走 MCP 自动生成。

当任务涉及场景/预制体/UI：
- 优先调用 Unity MCP 创建或更新资源
- 明确资源命名、层级、锚点、绑定脚本
- 将生成记录写入提交说明，便于复现

当任务涉及协议变更：
- 默认不修改 proto
- 若必须修改，先标注风险点（字段编号、兼容性、双端联调影响）

## 8. 开发顺序建议

按以下里程碑推进：

M1 网络打通：
- 连接、收包、解包、消息分发、心跳

M2 房间闭环：
- 创建/加入/准备/开始 + 房间状态广播

M3 对局主链路：
- TurnStart + 三类行动 + ActionResult

M4 技能与最终轮：
- UseSkill、SkillPrompt、CallSteady、Reveal/Score

M5 重连恢复：
- Reconnect + StateSync + TurnStepState 恢复

M6 体验层：
- UI 动效、提示、错误反馈、弱网提示

## 9. Definition of Done

一次功能任务完成标准：
- 编译通过（Unity Console 无新增 error）
- 至少一条正向链路可演示
- 异常分支有可见提示（error.message）
- 不破坏现有场景/预制体引用
- 变更说明包含：改动文件、验证步骤、剩余风险

## 10. 常见陷阱

- 将 ActionResultNotify 误当作全量状态
- 遗漏 server_seq 去重导致重复应用
- 盲交换/看换的挂起步骤恢复不完整
- UI 直接依赖 protobuf 对象导致耦合失控
- 客户端擅自决定技能是否成功

## 11. 与 skills 的配合

当需求明确时，优先调用以下技能文档执行：
- skills/unity-proto-pipeline.md
- skills/unity-network-client.md
- skills/unity-state-sync-reconnect.md
- skills/unity-card-game-ui.md
- skills/unity-scene-generator.md
- skills/unity-mcp-asset-workflow.md

如出现交叉任务：先网络与状态，再 UI 与资源。
