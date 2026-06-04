# Build 后中文不显示 - 临时解决方案

## 问题
UI Toolkit 在 Build 后使用 `resource('Fonts/xxx')` 加载 TTF 字体可能失败。

## 快速解决方案

### 方案 1：使用代码动态设置字体（推荐）

在 `GameTableUIToolkit.cs` 的 `Initialize()` 方法中添加：

```csharp
private void QueryElements()
{
    // ... 现有代码 ...
    
    // 动态加载字体（Build 后可靠）
    var font = Resources.Load<Font>("Fonts/ZCOOLKuaiLe");
    if (font != null)
    {
        root.style.unityFont = font;
        Debug.Log("[GameTableUIToolkit] 中文字体加载成功");
    }
    else
    {
        Debug.LogWarning("[GameTableUIToolkit] 中文字体加载失败！");
    }
}
```

### 方案 2：回退到 uGUI Text

如果 UI Toolkit 字体问题难以解决，暂时使用旧版 `GameTableUI.cs`（uGUI 版本），它使用 `UnityEngine.UI.Text`，中文显示没有问题。

---

## 立即测试方案 1

我现在就为你实现方案 1。
