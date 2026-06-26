# Next Session Prompt

Copy this into the next Codex session if you want to continue from the current state:

```text
请接手当前 Cabo 多人卡牌项目。

Workspace:
- C:\Users\Admin\Desktop\Cabo GameObject

当前重点：
- Docs 已从早期 CLI/Unity migration 口径更新到当前实现。
- 请继续围绕“腾讯后台开发实习生简历素材”整理项目经历。
- 优先关注网络、计算机系统、进程/线程、连接生命周期、并发同步、断线重连、广播性能优化。

请先阅读：
1. Docs/CURRENT_TASK.md
2. Docs/README.md
3. Docs/TENCENT_BACKEND_RESUME_DRAFT.md
4. Docs/BACKEND_RESUME_PROBLEM_LOG.md
5. Docs/BUG_LOG.md
6. Docs/ARCHITECTURE.md
7. Docs/NETWORK_LAYER.md
8. Docs/PROJECT_OVERVIEW.md

关键背景：
- 当前分支是 codex/special-effects。
- main 分叉后主要只有 .gitignore 维护提交。
- 当前分支额外包含 reconnect、room browser/access flow、Unity UI/animation/cross-platform fixes。
- 后台亮点主要来自 WebSocket transport、session binding、state synchronization、reconnect recovery、broadcast send optimization。

注意：
- 工作树里已有一个 Unity art catalog 的未提交改动：
  unity dev/New Client_Unity_Base_Cli/Assets/Resources/Art/CaboArtCatalog.asset
  这不是本轮 Docs 修改产生的，除非用户明确要求，不要碰它。

如果继续写简历，建议输出：
- 3 条简历项目 bullet；
- 1 段项目介绍；
- 3-5 个面试可展开的问题故事；
- 每个故事包含 Problem / Root cause / Solution / Verification。
```
