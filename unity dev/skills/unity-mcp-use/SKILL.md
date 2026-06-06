## mcp 服务器启动
 cd /home/niuma/.claude/tools/unity-mcp/Server
  uv run mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools >
  /tmp/unity-mcp-server.log 2>&1 &

## mcp所在目录

     ~/.claude/tools

## 完整skill所在目录
    ~/claude/skills
