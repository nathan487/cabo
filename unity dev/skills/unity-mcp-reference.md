# Unity MCP 参数格式参考

> 通过 2026-06-02 会话探索总结，避免重复踩坑。

## MCP 连接

### 初始化会话

```bash
# Step 1: POST initialize 获取 session ID
curl -s --noproxy '*' -D - "http://127.0.0.1:8080/mcp" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -X POST \
  -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude-code","version":"1.0"}},"id":1}'

# 从响应头提取 Mcp-Session-Id

# Step 2: 发送 initialized 通知
curl ... -H "Mcp-Session-Id: $SESS" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}'

# Step 3: 后续调用都带上 Mcp-Session-Id header
curl ... -H "Mcp-Session-Id: $SESS" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{...},"id":N}'
```

### 注意事项
- 必须使用 `--noproxy '*'` 避免代理干扰本地连接
- Session ID 通过 header `Mcp-Session-Id` 传递（不是 query string）
- Unity domain reload 后 session 可能失效，需重新初始化
- 响应是 SSE 格式，用 `grep 'data: ' | sed 's/^data: //'` 提取 JSON

## 工具参数格式

### manage_gameobject

```json
// 创建（注意：components 参数创建时不生效，需单独用 manage_components add）
{"action": "create", "name": "MyObject", "parent": "ParentName"}
{"action": "create", "name": "MyObject", "parent": "ParentName", "components": ["Image", "Button"]}

// 删除
{"action": "delete", "target": "MyObject", "search_method": "by_name"}
{"action": "delete", "target": "MyObject", "search_method": "by_path"}

// target 必须是 string（name 或 path），不能用 instanceID
```

**⚠️ 陷阱**: `create` 不会自动添加 `RectTransform`（即使 parent 是 Canvas），创建的 GameObject 只带 `Transform`。需要用 Editor 脚本的 `new GameObject(name, typeof(RectTransform))` 来正确创建 UI 对象。

### manage_components

```json
// 添加组件（短名称即可，系统自动补全 Unity 命名空间）
{"action": "add", "target": "Canvas", "component_type": "Canvas"}
{"action": "add", "target": "Canvas", "component_type": "CanvasScaler"}
{"action": "add", "target": "Canvas", "component_type": "GraphicRaycaster"}

// 删除组件
{"action": "remove", "target": 26230, "component_type": "CaboSceneSetup"}

// 设置属性（target 必须能找到，建议用 instanceID）
{"action": "set_property", "target": "Canvas", "component_type": "Canvas", "property": "renderMode", "value": 0}

// Vector2/Color 等复杂类型用 JSON 字符串
{"action": "set_property", "target": "Canvas", "component_type": "CanvasScaler", 
 "property": "referenceResolution", "value": "{\"x\":800,\"y\":600}"}

// target 支持 int (instanceID) 或 string (name/path)
// component_type 支持短名如 "Text", "Image", "Button", "CanvasScaler"
```

**⚠️ 陷阱**: `set_property` 的 `target` 用 name 搜索时可能失败（找不到组件），推荐使用 instanceID。component_type 用短名即可。

### manage_script

```json
// 创建脚本（name + path 都必填）
{"action": "create", "name": "MyScript", "path": "Editor/MyScript", "contents": "using UnityEngine;..."}

// ⚠️ path 不带 .cs 后缀，系统自动添加
// ⚠️ path 为 "Editor/Foo" 会创建目录 Editor/Foo/Foo.cs（目录+文件）
// ⚠️ path 为 "Editor/Foo" 非 "Editor/Foo.cs" —— 要创建单文件，直接用 Write 工具
```

**推荐**: 用 Write 工具直接写入 .cs 文件更可靠，避免 manage_script 的路径歧义。

### execute_code

```json
// action 必填，code 用完全限定类型名
{"action": "execute", "code": "var go = UnityEngine.GameObject.Find(\"Foo\");",
 "compiler": "auto", "safety_checks": true}

// 注意：code 中不能用 using 语句（CodeDom 编译器限制）
// 注意：方法需要返回值，否则编译报错 "not all code paths return a value"
```

**推荐**: 避免使用 execute_code 做复杂操作，改用 Editor 脚本 + execute_menu_item。

### batch_execute

```json
// 批量执行多个命令，推荐用于批量操作
{"name": "batch_execute", "arguments": {
  "commands": [
    {"tool": "manage_components", "params": {...}},
    {"tool": "manage_gameobject", "params": {...}}
  ],
  "parallel": false
}}

// 每个 command 的 params 格式与单独调用完全相同
```

### 其他重要工具

```json
// 刷新 Unity
{"name": "refresh_unity", "arguments": {"compile": "request"}}
// compile 可选: "none" (默认), "request"

// 执行菜单项
{"name": "execute_menu_item", "arguments": {"menu_path": "Tools/Build Game Scene"}}

// 读取控制台
{"name": "read_console", "arguments": {}}
// 无参数，返回最近10条

// 获取场景层级
{"name": "manage_scene", "arguments": {"action": "get_hierarchy", "page_size": 50}}

// 管理编辑器
{"name": "manage_editor", "arguments": {"action": "play"}}
{"name": "manage_editor", "arguments": {"action": "stop"}}

// 管理 UI (UI Toolkit)
{"name": "manage_ui", "arguments": {"action": "create", ...}}
// 注意：manage_ui 主要针对 UI Toolkit (UI Document)，不是 uGUI
```

## 关键经验教训

1. **不要用 manage_gameobject 创建 UI** — 它不会生成 RectTransform，UI 对象无法正确工作
2. **推荐模式**: Write 工具写 Editor 脚本 → MCP compile → MCP execute_menu_item
3. **manage_script 路径有歧义** — 用 Write 工具写入文件更可靠
4. **GameObject.Find 找不到 inactive 对象** — 需要传引用，不要 search
5. **Unity console 可能返回缓存结果** — domain reload 后重新查询
6. **session 跨 Unity domain reload 可能失效** — 操作后重新建立 session
