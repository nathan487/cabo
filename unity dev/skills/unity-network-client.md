# Skill: Unity Network Client (Proto + Message Pump)

## 适用场景

当你需要在 Unity 客户端实现以下能力时使用本技能：
- 与服务端建立 TCP/WS 长连接
- 发送 ClientMessage，接收 ServerMessage
- 按 oneof payload 做消息分发
- 管理 seq / server_seq / request_id

## 目标

实现一条可复用的网络主链路：
1. Connect
2. Send(ClientMessage)
3. Receive(ServerMessage)
4. Dispatch(handler)
5. Error/Timeout/Retry

## 输入依赖

- Proto/messages.proto
- Proto/common.proto
- Proto/room.proto
- Proto/game.proto
- Proto/sync.proto

## 输出成果（最小集）

- NetworkClient（连接、收发、断线事件）
- MessageCodec（protobuf 编解码）
- MessageDispatcher（oneof 分发）
- RequestTracker（request_id 与超时）
- ConnectionState（Disconnected/Connecting/Connected/Reconnecting）

## 实施步骤

1. 建立传输层
- 支持 TCP length-prefix 或 WebSocket binary frame
- 建立统一字节流到 ServerMessage 的入口

2. 封装发送路径
- 构造 ClientMessage
- 填充 seq
- 填充具体 payload
- 序列化并发送

3. 封装接收路径
- 反序列化 ServerMessage
- 校验 server_seq（去重/乱序保护）
- 将 payload 路由到 handler

4. 落地请求跟踪
- request_id -> pending callback
- 超时回调与 UI 友好错误提示

5. 健壮性处理
- 断线回调
- 自动重连入口
- 心跳保活（HeartbeatReq/HeartbeatRsp）

## 协议映射建议

- 房间域 handler：CreateRoomRsp/JoinRoomRsp/RoomStateNotify 等
- 对局域 handler：TurnStartNotify/ActionResultNotify 等
- 同步域 handler：StateSyncNotify/ReconnectRsp

不要在网络层做业务判定，只做消息搬运与分发。

## 验收清单

- 能稳定收发 room 请求与响应
- server_seq 重复包不会重复应用
- 超时请求能回调失败并提示
- 断线后可进入重连流程

## 常见错误

- 忘记处理 oneof 未知分支
- 将 request_id 与 seq 混用
- 在 handler 中直接操作 UI 控件导致耦合

