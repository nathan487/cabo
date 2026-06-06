## MCP 服务启动

```bash
cd /home/niuma/.claude/tools/unity-mcp/Server
uv run mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools > /tmp/unity-mcp-server.log 2>&1 &
```

## 目录

| 项目 | 路径 |
|------|------|
| MCP工具 | `~/.claude/tools/unity-mcp` |
| MCP CLI | `/home/niuma/anaconda3/bin/unity-mcp` |
| Skill | `~/.claude/skills/unity-mcp-skill` |
| 项目Skill | `unity dev/skills/unity-mcp-use/SKILL.md` |

## CLI 常用命令

```bash
unity-mcp editor console            # 查看编译错误
unity-mcp editor play               # 进入Play模式
unity-mcp editor refresh            # 强制刷新资源+编译
unity-mcp scene hierarchy           # 查看场景层级
unity-mcp component --help          # 组件操作
unity-mcp gameobject --help         # GameObject操作
```

## 编译修复标准流程

```bash
# 1. 修改C#代码
# 2. 触发编译
unity-mcp editor refresh && sleep 8

# 3. 检查错误
unity-mcp editor console 2>&1 | grep "error CS"

# 4. 重复2-3直到零错误
```

## 排错记录 (2026-06-06)

### 1. httpcore[asyncio] 缺失

```
❌ Running with asyncio requires installation of 'httpcore[asyncio]'.
```

修复: `pip install "httpcore[asyncio]" "httpx[http2]"`

### 2. unity-mcp status 失败但服务在运行

`unity-mcp status` 报连接失败，但 `curl http://127.0.0.1:8080/` 返回404（服务正常）。实际子命令（editor/scene/component）可用。

### 3. 代码修改后编译未自动触发

修改文件后 `editor console` 仍显示旧错误。用 `unity-mcp editor refresh` 手动刷新，等5-10秒再查。

### 4. Unity断连 (domain reload)

大改动后出现 `Unity plugin session disconnected`，等几秒自动重连。

### 5. protobuf字段名差异

生成的C#字段名与代码预期不一致的常见修复：

| 代码中 | proto实际 |
|--------|----------|
| `SkillType.SkillTypeNone` | `SkillType.None` |
| `ActionType.ActionTypeDraw` | `ActionType.Draw` |
| `GamePhaseType.GamePhaseFinalRound` | `Game.Common.GamePhase.FinalRound` |
| `rsp.HasExchangeResult` | `rsp.ExchangeResult != null` |
| `Game.Game.PeekSelfParams` | `Game.Common.PeekSelfParams` |

排查: `grep "class XXX\|enum XXX" Assets/Scripts/Proto/Generated/*.cs`

### 6. 手动创建的Scene YAML GUID无效

手工写的.unity文件缺少真实GUID引用。改用 `[RuntimeInitializeOnLoadMethod]` 代码自启动，无需场景配置。
