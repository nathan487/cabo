# Skill: Unity Proto Pipeline (Codegen + Adapter)

## 适用场景

用于把 Proto 协议稳定接入 Unity 客户端，包括：
- 生成 C# protobuf 类
- 建立消息适配层（ProtoModel -> DomainModel）
- 维护协议兼容性

## 目标

构建一个可维护的协议流水线，避免 UI/业务层直接依赖 proto 细节。

## 输入

- Proto/common.proto
- Proto/room.proto
- Proto/game.proto
- Proto/sync.proto
- Proto/messages.proto

## 输出

- Unity 侧 proto 生成脚本或固定流程文档
- 生成代码放置策略（建议 Assets/Scripts/Proto/Generated）
- Adapter 层（建议 Assets/Scripts/Proto/Adapters）

## 执行步骤

1. 明确版本源
- 以仓库 Proto 文件为单一事实来源
- 避免手改 generated 文件

2. 生成 C# 代码
- 使用 protoc + csharp_out
- 生成后做一次编译验证

3. 构建 Adapter
- ServerMessage -> 领域事件
- 领域事件 -> ViewModel
- Client 意图 -> ClientMessage

4. 设计防腐层
- UI 不直接引用 generated 类型
- DomainState 不持有 protobuf 对象

## 兼容规则

- 不改字段编号
- 允许新增字段，不破坏旧字段语义
- oneof 扩展需有 default 分支防御

## 验收清单

- 协议生成后 Unity 可编译
- 收到每种关键通知都能映射为领域事件
- Adapter 有基础单测或日志验证
