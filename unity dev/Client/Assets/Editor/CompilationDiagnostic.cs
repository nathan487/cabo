using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// 诊断和修复编译错误的工具
/// </summary>
public class CompilationDiagnostic
{
    [MenuItem("Tools/诊断编译错误")]
    public static void DiagnoseCompilation()
    {
        Debug.Log("========== 开始诊断编译错误 ==========");
        
        // 1. 检查是否有编译错误
        var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies();
        Debug.Log($"总程序集数量: {assemblies.Length}");
        
        // 2. 强制重新导入所有脚本
        Debug.Log("正在刷新资源数据库...");
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        
        // 3. 重新编译
        Debug.Log("触发重新编译...");
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        
        Debug.Log("========== 诊断完成 ==========");
        Debug.Log("如果仍有错误，请查看 Console 窗口的错误信息");
    }
    
    [MenuItem("Tools/强制重新导入所有资源")]
    public static void ReimportAll()
    {
        Debug.Log("开始重新导入所有资源...");
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        Debug.Log("重新导入完成！");
    }
}
