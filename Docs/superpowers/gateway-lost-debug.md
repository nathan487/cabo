# Gateway 丢失问题排查

## 问题现象
从 MainMenuScene 进入 GameScene 后，Console 显示：
- `[GameSceneController] ProtoGateway not found`
- `[GameTableUIToolkit] ProtoGateway is null - 独立 UI 测试模式`

## 可能原因

### 1. ClientAppBootstrap 的 BackendMode 设置错误
在 MainMenuScene 的 ClientAppBootstrap GameObject 上：
- **检查 Inspector 中的 Backend Mode 字段**
- 如果是 `Mock`，改为 `ProtoPlaceholder`

### 2. ClientAppBootstrap 未正确持久化
- 检查 ClientAppBootstrap 的 Awake() 中是否调用了 `DontDestroyOnLoad(gameObject)`
- 检查是否有多个 ClientAppBootstrap 实例（导致冲突）

### 3. 从错误的场景启动
- 必须从 **MainMenuScene** 启动
- 不能直接从 GameScene 启动（会找不到 Bootstrap）

## 立即检查步骤

### 在 Unity Editor 中：
1. 打开 **MainMenuScene**
2. 在 Hierarchy 中查找 **ClientAppBootstrap** GameObject
3. 查看 Inspector：
   - **Backend Mode**: 应该是 `ProtoPlaceholder` 或 `Proto`，**不是 Mock**
   - **Server Host**: `127.0.0.1`
   - **Server Port**: `8888`
4. 如果没有 ClientAppBootstrap GameObject，需要手动添加或检查 `LobbyRoomDemoUI` 是否会自动创建

## 解决方案

如果确认配置正确，添加更多调试日志：

```csharp
// 在 ClientAppBootstrap.Awake() 中添加：
Debug.Log($"[ClientAppBootstrap] Awake - BackendMode: {backendMode}, DontDestroyOnLoad set");

// 在 GameSceneController.Start() 中添加：
Debug.Log($"[GameSceneController] Finding bootstrap...");
var bootstrap = FindObjectOfType<ClientAppBootstrap>();
Debug.Log($"[GameSceneController] Bootstrap found: {bootstrap != null}");
```

## 当前已修复
- ✅ GameSceneController 现在即使 Gateway 为 null 也会初始化 UI
- ✅ 这样至少字体加载代码会执行
- ✅ Build 后应该能显示中文（通过 FontHelper）

## 下一步
请检查 MainMenuScene 中 ClientAppBootstrap 的配置，并告诉我 Backend Mode 是什么。
