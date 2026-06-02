# Skill: Unity Scene Generator (MCP First)

## 适用场景

当需要快速生成或重构场景、UI 层级、Prefab 引用关系时使用。
优先通过 Unity MCP 完成可视资源创建，再补脚本绑定。

## 目标

将文档需求转换为可运行的 Unity 场景资产：
- LobbyScene
- RoomScene
- GameScene

## 前置检查

- Unity 项目已打开：unity dev/Client
- Unity MCP 服务可调用
- 目标脚本命名已确定（避免挂载错位）

## MCP 执行策略

1. 先建场景骨架
- 创建 Canvas、EventSystem、主容器节点
- 命名规范统一（如 UI_RoomPanel, UI_PlayerSlot_0）

2. 再建可复用 Prefab
- 玩家槽位
- 手牌槽位
- 弹窗（Confirm/Toast/SkillPrompt）

3. 最后做引用绑定
- 控制器脚本字段绑定
- 按钮事件绑定到 Presenter/Controller

## 本项目推荐层级

GameScene 示例：
- Root
- UI
- TopBar
- CenterBoard
- BottomHand
- RightPlayers
- ActionPanel
- OverlayPopups

## 约束

- 不把业务逻辑写进 MonoBehaviour 的 Update
- Prefab 粒度适中，避免每个 Text 都做单独 Prefab
- 所有动态列表使用模板+容器实例化

## 交付物

- 新增/更新的场景列表
- 新增/更新的 Prefab 列表
- 每个脚本的序列化引用绑定说明
- 可复现的 MCP 操作摘要

## 验收清单

- 场景可直接运行，无 Missing Reference
- 关键按钮点击有正确回调
- 分辨率变化下布局不崩
- 场景切换后对象生命周期正常

