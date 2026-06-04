# Unity Client - Claude Guide

本文档是 Claude 在 `unity dev/` 子项目中工作的最高优先级指引。目标是让 Unity 客户端成为一个表现优秀、协议可靠、规则权威交给服务端的游戏客户端。

## 1. 产品方向

Unity 主要负责玩家体验：

- 美术资源：场景、卡牌、桌面、UI 皮肤、图标、背景、粒子、主题化材质。
- 音乐音效：大厅音乐、对局环境音、抽牌/弃牌/交换/稳态/结算反馈音。
- 动画表现：发牌、抽牌、翻牌、弃牌、交换、偷看、回合切换、结算、胜负展示。
- 交互体验：清晰的可操作状态、禁用态、等待态、错误提示、断线重连提示。

Unity 不应该成为规则权威。它只负责把玩家操作转换为 protobuf 请求，把服务端响应解析为本地视图和表现反馈。

## 2. 当前实现快照

当前项目里同时存在两套东西：

- `Assets/Scripts/ClientCore/`：新的客户端骨架，包含 `IBackendGateway`、`MockBackendGateway`、`ProtoGatewayPlaceholder`、`RoomClientController`、`LobbyRoomDemoUI`、`ClientAppBootstrap`。
- `Assets/Scripts/Game/` 与 `Assets/Scripts/UI/`：旧 hot-seat 单机原型，包含本地发牌、回合、技能、计分和临时 UI。

工作原则：

- 新联机客户端以 `ClientCore` 为主线。
- 旧 hot-seat 代码只能作为视觉、交互、离线演示参考。
- 不要把旧 hot-seat 的本地规则裁决继续扩展成联机裁决。
- 若需要复用旧代码，优先抽取纯表现组件或临时调试工具，避免复用发牌、胜负、技能判定逻辑。

## 3. 权威来源顺序

发生冲突时按以下顺序执行：

1. 仓库根目录 `Proto/*.proto`
2. 根目录 `游戏规则.md`
3. 根目录 `Docs/GDD.md`
4. 本文件
5. `unity dev/skills/*.md`

注意：如果 skills 文档中出现旧协议流程，以本文件和最新 proto 为准。

## 4. 客户端职责边界

客户端必须做：

- 根据 protobuf 生成或接入 C# 消息类型。
- 通过 `messages.ClientMessage` 发送玩家意图。
- 解析 `messages.ServerMessage`，按 oneof payload 分发。
- 维护只用于显示的本地 `ClientViewState`。
- 用服务端快照和广播覆盖本地显示状态。
- 管理资源、动画、音频、UI 绑定和操作反馈。

客户端禁止做：

- 洗牌、发牌、抽牌随机结果。
- 判定多张交换成功或失败。
- 判定技能是否合法或是否命中。
- 判定稳态成功、神风特攻队、100 分重置、胜负排名。
- 为了“手感”私自改变服务端下发的牌值、分数、回合、玩家状态。

本地可以做的校验仅限 UX：

- 输入为空时不发请求。
- 非当前玩家时禁用按钮。
- 没有选中槽位时提示用户。
- 防止重复点击同一个按钮。

这些校验不能替代服务端校验，服务端返回错误时必须服从服务端。

## 5. 最新 protobuf 交互要点

总入口：

- 上行：`game.messages.ClientMessage`
- 下行：`game.messages.ServerMessage`

基础要求：

- 每个请求使用递增 `ClientMessage.seq`。
- 按协议填充 `request_id`、`player_id`、`room_id`、`client_seq`。
- 保存并检查 `ServerMessage.server_seq`，重复或倒序消息要记录并避免重复应用。
- 所有 oneof 分支都要显式处理；未知分支记录 warning。

房间链路：

- `CreateRoomReq/Rsp`
- `JoinRoomReq/Rsp`
- `ReadyReq/Rsp`
- `StartGameReq/Rsp`
- `RoomStateNotify`
- `PlayerJoinNotify`
- `PlayerLeaveNotify`
- `PlayerReadyNotify`
- `RoomStartNotify`

对局链路：

- `GameStartNotify`
- `TurnStartNotify`
- `DrawCardReq/Rsp`
- `DiscardDrawnReq/Rsp`
- `ReplaceWithDrawnReq/Rsp`
- `TakeFromDiscardReq/Rsp`
- `UseSkillReq/Rsp`
- `CallSteadyReq/Rsp`
- `ActionResultNotify`
- `RoundRevealNotify`
- `ScoreUpdateNotify`
- `GameOverNotify`

最新规则对应的协议注意事项：

- 13 没有技能。
- 7-12 只有从牌库抽到后“直接弃掉”才可触发技能。
- `UseSkillReq.skill_params` 只应使用 `peek_self`、`spy`、`swap`。
- 不要实现旧版 `SkillPromptNotify`、`BlindSwapRespondReq`、`LookSwapDecideReq`。
- 多张替换使用 `slot_indices`，不是旧版单个 `slot_index`。
- 多张替换结果以服务端返回的 `ExchangeAttemptResult` 为准。
- 失败交换导致牌区数量可能超过 4，UI 布局必须支持可变数量。

## 6. 推荐架构

继续沿用并扩展当前骨架：

- `IBackendGateway`：UI 层唯一依赖的后端接口。
- `MockBackendGateway`：只用于离线 UI 演示，不代表规则真实结果。
- `ProtoGateway`：替换 `ProtoGatewayPlaceholder`，负责真实 TCP/WebSocket + protobuf。
- `RoomClientController`：房间流程的应用层控制器。
- `GameClientController`：对局流程的应用层控制器，后续新增。
- `ClientViewState`：客户端渲染状态，不保存权威规则。
- `MessageDispatcher`：ServerMessage oneof 分发。
- `RequestTracker`：request_id 到等待状态、超时、错误提示的映射。

不要让 UI 脚本直接操作 socket，也不要让网络层直接操作场景物体。网络层发事件，控制器更新 ViewState，UI 根据 ViewState 渲染。

## 7. Unity 资源与表现要求

Claude 优先帮助构建这些内容：

- 主菜单、大厅、房间、对局、结算、断线重连页面。
- 卡牌 prefab：正面、背面、已知、未知、选中、不可选、警示等状态。
- 玩家区域 prefab：昵称、座位、在线、准备、累计分、当前回合高亮。
- 牌堆和弃牌堆视觉：数量、顶部牌、可点击状态。
- 动画控制器：抽牌、弃牌、替换、多张替换失败加牌、交换、偷看、结算翻牌。
- 音频管理：BGM、按钮、抽牌、弃牌、交换、错误、胜利、失败。
- 状态提示：等待对手、轮到你、选择目标、服务端拒绝、重连中。

UI 实现要求：

- 新 UI 使用 TextMeshPro。
- 保留旧 `UnityEngine.UI.Text` 只限已有原型或临时 debug 面板。
- 新控件使用 `SerializeField` 绑定，避免 `FindObjectOfType`。
- 不要在 `Update` 中推进复杂业务逻辑。
- 视觉资源优先做 prefab 和 ScriptableObject 配置，避免散落在脚本常量里。
- 不删除现有 Scene；新增或替换前先说明用途。

## 8. 场景路线

建议场景分层：

- `BootScene`：初始化 `ClientAppBootstrap`、音频、资源配置、网络入口。
- `MainMenuScene`：主菜单、昵称、连接状态。
- `LobbyRoomScene`：创建/加入房间、玩家列表、准备、开始。
- `GameScene`：对局桌面、卡牌区、操作栏、状态提示。
- `ResultOverlay`：作为 prefab 或 additive UI，用于每轮和整局结算。

当前 `LobbyRoomDemoUI` 是临时自动生成 UI，后续应逐步替换为作者化 prefab/UI，而不是长期堆功能。

## 9. 开发顺序

优先级从高到低：

1. 协议生成和 `ProtoGateway`：能收发 `ClientMessage/ServerMessage`。
2. 房间闭环：连接、创建、加入、准备、开始、房间状态同步。
3. 对局状态渲染：服务端下发什么，客户端正确显示什么。
4. 玩家操作请求：抽牌、弃牌、替换、技能、喊稳态。
5. 重连恢复：`ReconnectReq/Rsp` + `StateSyncNotify`。
6. 体验增强：prefab、美术、音乐、动画、过渡和提示。

每一步都要保留 Mock 或本地演示入口，让 UI 资源可以在没有服务器时预览。

## 10. Claude 每次任务的工作流程

开始前：

- 先读相关 proto，不猜字段。
- 先看现有脚本和 prefab，不新建重复系统。
- 判断任务属于“协议接入”“状态渲染”“资源表现”还是“场景搭建”。

实现时：

- 小步修改，优先复用现有 `ClientCore`。
- UI 与网络分离，资源与逻辑分离。
- 对协议变更保持保守；默认不改 proto。
- 如确实需要改 proto，先说明兼容性影响和双端影响。

结束时：

- 说明改了什么、为什么改、影响哪些对象。
- 给出 Unity 中的手动验证步骤。
- 标出是否需要重新生成 protobuf C#。
- 标出仍然依赖服务端实现的部分。

## 11. Definition of Done

一次 Claude 任务完成标准：

- Unity Console 无新增编译 error。
- 没有新增 Missing Script。
- 没有新增明显 NullReferenceException。
- UI 按钮状态和服务端状态一致。
- 服务器错误能显示给玩家或开发者。
- Mock 模式仍能用于表现预览。
- 真实 Proto 模式不做规则裁决，只发送意图并消费响应。

## 12. 常见陷阱

- 把旧 hot-seat 的本地 `CaboGameManager` 当作联机规则核心继续扩展。
- 把 `ActionResultNotify` 当作完整状态快照。
- 忽略 `server_seq`，导致重复播放动画或重复应用分数。
- 在客户端判断多张交换是否成功。
- 误把 13 当成技能牌。
- 继续实现旧的看+换或 SkillPrompt 流程。
- UI 直接依赖 protobuf 对象，导致场景和协议强耦合。
- 为了快速搭 UI 大量使用 `FindObjectOfType` 或运行时乱建重复 Canvas。

## 13. Skills 使用建议

按任务选择：

- 协议生成：`skills/unity-proto-pipeline.md`
- 网络连接：`skills/unity-network-client.md`
- 重连同步：`skills/unity-state-sync-reconnect.md`
- UI 和卡牌表现：`skills/unity-card-game-ui.md`
- 场景搭建：`skills/unity-scene-generator.md`
- 资源批处理：`skills/unity-mcp-asset-workflow.md`

如果 skills 与本文件冲突，以本文件和最新 proto 为准。
