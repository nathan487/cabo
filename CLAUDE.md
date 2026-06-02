# CLAUDE.md

# Project Overview

这是一个基于：

- Unity(Client)
- C++ GameServer(Server)
- muduo
- protobuf

实现的多人在线卡牌游戏项目。

项目目标：

实现一个支持多人联机的卡牌游戏原型。

核心玩法基于：

Kabo / Cabo 改编。

项目当前重点：

- 完成联机主链路
- 完成客户端与服务端通信
- 完成多人房间与回合系统
- 完成卡牌同步与状态同步

------

# Directory Structure

```text
.
├── .claude/                 # Claude CLI skills / configs
├── Docs/                    # 游戏设计文档
├── MuduoBaseGameServer/     # C++ 服务端
├── Proto/                   # protobuf协议
├── unity dev/               # Unity客户端
├── CLAUDE.md
└── 游戏简介.md
```

------

# Architecture

```text
Unity(Client)
    ↓ TCP + protobuf
GameServer(C++)
    ↓
muduo
```

------

# Client Rules

客户端：

Unity + C#

负责：

- UI
- 动画
- 特效
- 输入
- 状态展示

客户端不负责：

- 游戏判定
- 发牌
- 战斗计算
- 胜负判定

客户端不是权威状态源。

------

# Server Rules

服务端：

C++ + muduo

负责：

- 房间系统
- 回合逻辑
- 发牌
- 技能判定
- 游戏状态同步
- 玩家状态
- 广播消息

服务端是唯一权威状态源。

------

# Network Rules

网络通信：

- TCP 长连接
- protobuf 序列化

协议格式：

```text
[length][protobuf_data]
```

所有网络消息：

必须使用 protobuf。

禁止：

- JSON
- XML

------

# Proto Rules

protobuf 位于：

```text
Proto/
```

协议拆分：

```text
common.proto
login.proto
room.proto
game.proto
battle.proto
error.proto
```

消息命名：

- XxxReq
- XxxRsp
- XxxNotify

字段命名：

snake_case

例如：

```proto
player_id
room_id
card_index
```

------

# Coding Rules

## C++

- 使用现代 C++
- 避免 God Object
- 模块化
- RAII
- 智能指针优先

## Unity C#

- 单一职责
- UI 与网络分离
- 不在 Update 中做复杂逻辑
- UI 仅负责表现

------

# Current Goals

当前目标：

1. Unity 成功连接 muduo
2. protobuf 双端打通
3. 实现最小联机 Demo
4. 完成房间系统
5. 完成回合同步

------

# Current Development Priority

优先级：

1. protobuf 协议
2. muduo 消息系统
3. Unity TCP Client
4. Room 系统
5. 卡牌同步

当前不优先：

- 美术优化
- 特效
- 商业化
- 云服务器
- 微服务
- ECS

------

# AI Workflow

Claude 主要负责：

- Unity UI
- prefab
- scene
- 重复代码
- protobuf生成
- 工程结构建议

用户主要负责：

- 游戏逻辑
- muduo
- 网络架构
- protobuf设计
- 服务端核心逻辑

------

# Unity Rules

Unity 项目位于：

```text
unity dev/
```

推荐结构：

```text
Assets/
├── Scripts/
├── UI/
├── Prefabs/
├── Scenes/
├── Resources/
└── Proto/
```

------

# Server Rules

服务端位于：

```text
MuduoBaseGameServer/
```

推荐结构：

```text
src/
├── network/
├── game/
├── room/
├── player/
├── proto/
└── common/
```

------

# Important Rules

- 不要随意修改 protobuf 字段编号
- 不要让客户端决定游戏结果
- 所有状态同步由服务端广播
- 所有游戏逻辑优先服务端实现
- Unity 只负责表现层

------

# MVP Goal

最小可运行目标：

- 两个玩家联机
- 创建房间
- 发牌
- 抽牌
- 状态同步
- 回合同步
- 游戏结束判定

达到该目标后再扩展：

- UI
- 特效
- 技能动画
- 匹配系统
- 公网部署