# Skill: Unity MCP Asset Workflow

## 适用场景

当 Claude CLI 需要通过 Unity MCP 快速生成资源时使用：
- 场景搭建
- UI 结构创建
- Prefab 批量生成
- 基础材质与样式统一

## 目标

让资源生成可重复、可审计、可维护，避免一次性手工堆对象。

## 工作流

1. 定义资产清单
- 场景名
- Prefab 名
- 节点层级
- 绑定脚本

2. 批量创建
- 按模块分批生成（Lobby/Room/Game）
- 每批后立即检查命名与层级

3. 绑定与校验
- 绑定 Controller/Presenter 字段
- 校验丢失引用与重复组件

4. 产出记录
- 记录新增资源路径
- 记录自动绑定结果与手工补充项

## 资源命名约定

- 场景：LobbyScene, RoomScene, GameScene
- 面板：UI_XXXPanel
- 按钮：Btn_XXX
- 文本：Txt_XXX
- 列表项：Item_XXX

## 风险控制

- 不在一次操作中生成过多对象（便于回滚）
- 每次批量生成后立刻保存场景
- 同名资产更新时先确认覆盖策略

## 验收清单

- 资源路径规范且可搜索
- 场景打开无 Missing Script
- Prefab 实例化后引用完整
- 新资源可被现有脚本直接使用
