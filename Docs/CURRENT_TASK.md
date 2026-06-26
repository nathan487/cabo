# Current Task

## 2026-06-25 — Docs refresh and backend resume extraction

当前任务是把项目文档从早期 Unity/CLI 原型口径更新到当前实现，并从 git 提交历史中整理适合写进后台开发实习简历的“发现问题、定位问题、解决问题、验证问题”素材。

目标岗位：腾讯后台开发实习生。重点方向：

- C++ 网络编程；
- TCP length-prefix framing、半包/粘包、异常帧恢复；
- WebSocket / Protobuf 协议设计；
- Reactor / epoll / 非阻塞 socket；
- 多连接并发、线程同步、连接生命周期；
- 服务端权威状态、会话绑定、断线重连；
- 多人广播性能优化；
- 回归测试与问题复盘。

## Branch Context

截至 2026-06-25：

- 当前分支：`codex/special-effects`；
- `main` 分支在分叉后主要包含 `.gitignore` 维护提交；
- 当前分支额外包含断线重连、房间浏览/申请/邀请、Unity 表现层和跨平台 UI 修复；
- 后台/系统问题主要来自两条分支共有历史以及当前分支的 reconnect / room browser 提交。

## Updated Docs

本轮已更新或新增：

- `Docs/PROJECT_OVERVIEW.md` — 当前项目总览，替换早期“Unity planned / TCP length-prefix / 固定 4 人”口径；
- `Docs/README.md` — 新增 Docs 入口索引，区分当前实现、简历素材和历史记录；
- `Docs/HIGH_IMPACT_BACKEND_PROBLEM_CASES.md` — 新增强问题案例排序，优先用于腾讯后台简历/面试；
- `Docs/ARCHITECTURE.md` — 当前 Unity WebSocket + C++ authoritative server 架构；
- `Docs/NETWORK_LAYER.md` — 当前 WebSocket binary protobuf 网络层、并发修复、重连和广播发送路径；
- `Docs/BUG_LOG.md` — 合并早期 bug 与 git-derived 后台/系统问题；
- `Docs/BACKEND_RESUME_PROBLEM_LOG.md` — 专门用于简历和面试的后台问题日志；
- `Docs/TENCENT_BACKEND_RESUME_DRAFT.md` — 新增可直接改写到简历中的腾讯后台开发实习项目描述；
- `Docs/TODO.md` — 修正 2-6 人房间、重连恢复等过期 TODO；
- `Docs/SESSION_SUMMARY.md` / `Docs/NEXT_SESSION_PROMPT.md` — 更新为当前交接语境；
- `Docs/GAME_SESSION.md` — 标记为历史 CLI 参考，不再作为当前架构说明。

## Best Resume Material

优先使用这些项目亮点：

1. TCP 长连接健壮性：修复 partial send、半包/粘包、异常长度头阻塞缓冲区、响应与通知消息穿插导致状态卡住等问题。
2. WebSocket + Protobuf 二进制通信层：从裸 TCP length-prefixed 协议升级，支持 Unity 客户端和 Cloudflare `wss` 公网访问。
3. 服务端权威连接-玩家会话绑定：修复客户端伪造 `player_id` 操作 ready/start/draw/discard/skill、回合劫持和隐藏牌值泄漏。
4. 并发与生命周期修复：为 WebSocket codec map、RoomService、GameService、Unity WebSocket 状态机建立同步边界，修复竞态、死锁、SIGPIPE 和断线写入问题。
5. 断线重连恢复：通过 session token、offline marker、StateSync snapshot 恢复当前回合、待抽牌、技能目标等上下文。
6. 广播性能优化：WebSocket frame 一次编码、多连接复用；发送 buffer 使用线程本地池复用。

详细展开见 `Docs/HIGH_IMPACT_BACKEND_PROBLEM_CASES.md` 和 `Docs/BACKEND_RESUME_PROBLEM_LOG.md`。

## Next Steps

- 人工审阅 Docs diff，确认描述符合你希望在简历中呈现的参与程度；
- 从 `BACKEND_RESUME_PROBLEM_LOG.md` 里挑 3-5 条写入简历项目经历；
- 如果要投腾讯后台开发，建议简历项目描述优先压缩到“网络协议 + 并发同步 + 会话重连 + 性能优化”四个关键词；
- 若后续需要更硬的量化指标，可以补一次本地压测或广播 benchmark，再把性能优化写得更有说服力。

## Historical Unity Work

Unity UI、动画、CardView 迁移等历史交接仍保留在：

- `Docs/UNITY_CLIENT_HANDOFF.md`
- `Docs/UNITY_ANIMATION_NOTES.md`
- `Docs/UNITY_CARD_VIEW_MIGRATION.md`
- `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`

这些文档是历史开发记录，不代表当前项目总览或后台简历口径。
