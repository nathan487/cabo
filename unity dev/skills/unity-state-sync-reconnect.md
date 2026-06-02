# Skill: Unity State Sync and Reconnect

## 适用场景

用于实现弱网可恢复体验：
- 心跳检测
- 断线重连
- StateSyncNotify 全量回放
- TurnStepState 挂起步骤恢复

## 关键协议

- ReconnectReq / ReconnectRsp
- StateSyncNotify
- GameSyncState
- TurnStepState
- HeartbeatReq / HeartbeatRsp

## 目标

客户端在断线后可恢复到服务端权威状态，并继续当前流程，不出现双提交或错回合。

## 实施步骤

1. 心跳与掉线判定
- 周期发送 HeartbeatReq
- 超时阈值触发重连状态

2. 重连握手
- 携带 session_token 与 last_server_seq
- ReconnectRsp 成功后等待 StateSyncNotify

3. 全量状态覆盖
- 用 StateSyncNotify 覆盖本地 AppState
- 清理与旧状态相关的临时 UI

4. 挂起步骤恢复
- STEP_TYPE_WAITING_DRAW_DECISION：恢复二段决策弹窗
- STEP_TYPE_WAITING_BLIND_SWAP：仅目标玩家可操作
- STEP_TYPE_WAITING_LOOK_SWAP：仅发起者可做换牌决策

5. 幂等保护
- 按 server_seq 去重
- 对已处理 request_id 不重复提交

## UI 配合

- 显示连接状态（已连接/重连中）
- 重连成功后提示已恢复
- 重连失败给出返回大厅入口

## 验收清单

- 拔网线后可自动重连
- 重连后界面与服务端一致
- 挂起操作能继续进行而不丢失
- 不会重复执行旧广播
