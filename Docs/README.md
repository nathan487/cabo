# Documentation Index

这是当前 `Docs/` 的入口索引，用来区分“当前实现文档”“简历素材文档”和“历史开发记录”。如果只想快速了解项目或准备面试，优先看前两个区域。

## Start Here

| Need | Read |
| --- | --- |
| 了解当前项目做到了什么 | [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) |
| 了解当前服务端/客户端架构 | [ARCHITECTURE.md](ARCHITECTURE.md) |
| 了解网络层、WebSocket、重连、广播发送 | [NETWORK_LAYER.md](NETWORK_LAYER.md) |
| 查看修过哪些后台/系统问题 | [BUG_LOG.md](BUG_LOG.md) |
| 准备腾讯后台开发实习简历，优先看强案例排序 | [HIGH_IMPACT_BACKEND_PROBLEM_CASES.md](HIGH_IMPACT_BACKEND_PROBLEM_CASES.md) |
| 查看可直接改写到简历里的项目描述 | [TENCENT_BACKEND_RESUME_DRAFT.md](TENCENT_BACKEND_RESUME_DRAFT.md) |
| 查看完整“问题-原因-解决-验证”素材 | [BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md) |
| 接手当前上下文 | [CURRENT_TASK.md](CURRENT_TASK.md) |

## Current Architecture Docs

- [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) — 当前项目总览、技术栈、分支背景。
- [ARCHITECTURE.md](ARCHITECTURE.md) — Unity WebSocket + C++ authoritative server 架构、线程同步、状态生命周期。
- [NETWORK_LAYER.md](NETWORK_LAYER.md) — WebSocket binary protobuf、Unity 网络线程模型、断线重连、广播优化。
- [WEBSOCKET_CLOUDFLARE_RUNBOOK.md](WEBSOCKET_CLOUDFLARE_RUNBOOK.md) — WebSocket / Cloudflare Tunnel 运行手册。
- [RECONNECT_RECOVERY_NOTES.md](RECONNECT_RECOVERY_NOTES.md) — 断线重连恢复细节和改进建议。

## Resume / Interview Docs

- [HIGH_IMPACT_BACKEND_PROBLEM_CASES.md](HIGH_IMPACT_BACKEND_PROBLEM_CASES.md) — 按含金量重排的强问题案例，优先用于腾讯后台简历和面试展开。
- [TENCENT_BACKEND_RESUME_DRAFT.md](TENCENT_BACKEND_RESUME_DRAFT.md) — 可以直接改写到简历里的项目描述和 bullet。
- [BACKEND_RESUME_PROBLEM_LOG.md](BACKEND_RESUME_PROBLEM_LOG.md) — 更完整的问题故事，包含 Problem / Root cause / Solution / Verification。
- [BUG_LOG.md](BUG_LOG.md) — 统一 bug / regression log。

## Current Handoff Docs

- [CURRENT_TASK.md](CURRENT_TASK.md) — 当前任务上下文。
- [SESSION_SUMMARY.md](SESSION_SUMMARY.md) — 最近交接摘要。
- [NEXT_SESSION_PROMPT.md](NEXT_SESSION_PROMPT.md) — 下次接手提示词。
- [TODO.md](TODO.md) — 当前 TODO，已去掉明显过期的 fixed-4-player / reconnect-not-tested 口径。

## Unity Feature / Historical Implementation Notes

这些文档主要记录 Unity UI、动画、卡牌视图迁移过程。它们仍有参考价值，但不应作为当前后台架构或简历口径的主来源。

- [UNITY_CLIENT_HANDOFF.md](UNITY_CLIENT_HANDOFF.md)
- [UNITY_GAME_SCENE_TASK.md](UNITY_GAME_SCENE_TASK.md)
- [UNITY_CARD_VIEW_MIGRATION.md](UNITY_CARD_VIEW_MIGRATION.md)
- [UNITY_ANIMATION_NOTES.md](UNITY_ANIMATION_NOTES.md)
- [ART_REPLACEMENT_PLAN.md](ART_REPLACEMENT_PLAN.md)
- [GAME_SESSION.md](GAME_SESSION.md) — 早期 CLI 4 人对局 trace，已标记为历史参考。

## Planning / Spec Archive

`Docs/superpowers/specs/` 和 `Docs/superpowers/plans/` 是历史设计与执行计划归档。它们记录当时的开发意图和阶段性决策，可能包含已经被后续实现替换的旧假设，例如 raw TCP、固定 4 人、早期 Unity 迁移步骤等。

除非你要追溯开发过程，否则当前实现请优先看上面的 current architecture docs。
