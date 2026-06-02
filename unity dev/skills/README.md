# Unity Skills Index

本目录用于给 Claude CLI 提供可执行的 Unity 客户端开发技能。

## 快速选型

- 协议接入与代码生成：unity-proto-pipeline.md
- 网络连接与消息分发：unity-network-client.md
- 重连与状态同步：unity-state-sync-reconnect.md
- UI 页面与交互：unity-card-game-ui.md
- 场景生成：unity-scene-generator.md
- MCP 资源批处理：unity-mcp-asset-workflow.md

## 推荐调用顺序（从零开始）

1. unity-proto-pipeline.md
2. unity-network-client.md
3. unity-state-sync-reconnect.md
4. unity-card-game-ui.md
5. unity-scene-generator.md
6. unity-mcp-asset-workflow.md

## 组合示例

- 做房间系统：proto-pipeline + network-client + card-game-ui
- 做完整对局：network-client + state-sync-reconnect + card-game-ui + scene-generator
- 做资源补齐：scene-generator + mcp-asset-workflow
