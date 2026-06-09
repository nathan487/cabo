# Next Session Prompt

Copy this into the next Codex session:

```text
请先阅读这些文档，快速接手当前项目状态：

1. Docs/CURRENT_TASK.md
2. Docs/NETWORK_LAYER.md
3. Docs/UNITY_CLIENT_HANDOFF.md
4. Docs/superpowers/plans/2026-06-08-websocket-cloudflare-plan.md
5. Docs/superpowers/specs/2026-06-08-websocket-cloudflare-design.md

当前项目路径：
- 服务端：MuduoBaseGameServer
- Unity 客户端：unity dev/New Client_Unity_Base_Cli

重要背景：
- 当前游戏逻辑、房间逻辑、protobuf 协议、Unity UI/动画/状态机都已经能工作。
- 目前网络传输还是 raw TCP，格式是 `[4 bytes big-endian length][protobuf payload]`。
- 下一步目标是把传输层改成 WebSocket，然后通过 Cloudflare Tunnel 暴露一个临时公网域名，让公网玩家的 Unity 客户端可以连接我的本地服务器。
- 只改传输层和必要的连接地址输入/配置，不改游戏规则、不改 protobuf schema、不改房间/对局状态机、不重构 UI 布局。

本次新任务目标：

1. 服务端新增 WebSocket 支持
   - 在 muduo TCP 连接之上新增 `WebSocketCodec`。
   - 实现 RFC 6455 HTTP Upgrade 握手。
   - 支持 WebSocket binary frame 解码。
   - 客户端到服务端 frame 是 masked，需要解 mask。
   - 服务端到客户端 frame 不 mask。
   - 支持 ping/pong/close 基础控制帧。
   - 解出的 binary payload 仍然是现有 protobuf bytes，继续交给现有 dispatcher。
   - 发送给客户端时，把现有 protobuf payload 包成 WebSocket binary frame。

2. Unity 客户端改成 WebSocket transport
   - 新增或改造网络层，使用 `System.Net.WebSockets.ClientWebSocket`。
   - 连接地址改为完整 WebSocket URL：
     - 本地：`ws://127.0.0.1:8888`
     - Cloudflare：`wss://xxxx.trycloudflare.com`
   - WebSocket 已经提供消息边界，所以 Unity 收包不再读取 4 字节长度前缀。
   - `MessageCodec.Encode` / `Decode` 只处理 protobuf 序列化/反序列化，或者保留旧 TCP codec 但不要让 WebSocket 路径继续使用 4 字节 framing。
   - 保持 `GameFlow` drain-then-render 行为，不要因为改 transport 破坏状态同步。

3. Cloudflare Tunnel 临时公网访问
   - 添加文档或脚本，说明如何启动本地 GameServer 和 cloudflared。
   - 典型命令：
     - 本地服务器监听 `8888`。
     - `cloudflared tunnel --url http://localhost:8888`
   - Cloudflare 输出 `https://xxx.trycloudflare.com` 后，Unity 客户端使用 `wss://xxx.trycloudflare.com`。
   - 注意：如果 Cloudflare 对 raw WebSocket upgrade 到非 HTTP 服务有要求，请验证实际握手路径。目标是 Unity `ClientWebSocket` 能通过 `wss://...trycloudflare.com` 连到本地 server。

验证要求：

- 服务端 WebSocketCodec 单元测试：
  - handshake returns 101
  - encode small/medium/large binary payload
  - decode masked binary payload
  - partial frame reassembly
  - ping -> pong
  - close frame handling
- 服务端编译通过。
- Unity 编译通过，Console 必须为 0 errors / 0 warnings。
- 本地端到端验证：
  - Unity 连接 `ws://127.0.0.1:8888`
  - 创建房间
  - 加入房间
  - 准备/开始游戏
  - 至少完成抽牌/弃牌或一个基础操作
- 公网验证：
  - 启动 Cloudflare Tunnel。
  - Unity 连接 `wss://xxx.trycloudflare.com`。
  - 至少两个客户端能进入同一房间。

重要限制：

- 我会自己决定什么时候 build/start 服务端。不要主动启动长期运行的服务端，除非我明确要求。
- 不要提交或保留 Unity MCP 截图产物，除非我明确要求。
- 当前工作树可能有历史未提交文件，不要 revert 不属于本任务的改动。
- 优先做小步提交/小步验证。不要一次性大改所有网络文件。

当前最新 UI 状态：
- 等待房间输入框黑框/右侧白边问题已优化。
- 等待房间房主标记已改为金色徽章，左侧是真正生成的皇冠 bitmap 图案，右侧是 `房主`。
- Unity MCP 已用 4 人等待房间截图验证过该 UI。

建议实施顺序：

1. 先审查现有 `MessageCodec`、`NetworkGateway`、`TcpNetworkClient`、服务端 `GameServer` / `RoomService` / `GameService` 的网络边界。
2. 实现并测试服务端 `WebSocketCodec`。
3. 把服务端发送/接收切到 WebSocket binary payload，但保持 protobuf dispatcher 不变。
4. 实现 Unity `ClientWebSocket` transport。
5. 改 Unity 连接 UI/缓存，让用户输入完整 WebSocket URL。
6. 本地 ws:// 测通后，再处理 Cloudflare wss://。
7. 更新文档，记录实际命令、坑点和最终验证结果。
```

Asset note:

- Unity MCP screenshots under `Assets/Screenshots/` are verification artifacts.
- Do not commit screenshot artifacts unless explicitly requested.
