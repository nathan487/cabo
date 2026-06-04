using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Builds GameScene using UI Toolkit instead of uGUI</summary>
public static class GameSceneBuilderUIToolkit
{
    [MenuItem("Tools/Build Game Scene (UI Toolkit)")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        cam.GetComponent<Camera>().orthographic = true;
        cam.GetComponent<Camera>().orthographicSize = 5;
        cam.transform.position = new Vector3(0, 5, -6);

        // EventSystem
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // UI Document
        var uiGo = new GameObject("GameUI", typeof(UIDocument));
        var uiDoc = uiGo.GetComponent<UIDocument>();

        // 加载 UXML 和 USS
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/GameScene.uxml");
        var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Styles/GameScene.uss");

        if (uxml == null)
        {
            Debug.LogError("[GameSceneBuilderUIToolkit] GameScene.uxml not found! Path: Assets/UI/GameScene.uxml");
            return;
        }

        if (uss == null)
        {
            Debug.LogWarning("[GameSceneBuilderUIToolkit] GameScene.uss not found, styles will not be applied");
        }

        uiDoc.visualTreeAsset = uxml;

        // 添加样式表应用器
        if (uss != null)
        {
            var applier = uiGo.AddComponent<Cabo.Client.Game.UIStyleSheetApplier>();
            applier.styleSheet = uss;
        }

        // 添加 GameTableUIToolkit 控制器
        uiGo.AddComponent<Cabo.Client.Game.GameTableUIToolkit>();

        // GameSceneController
        var ctrlGo = new GameObject("GameSceneController");
        ctrlGo.AddComponent<Cabo.Client.Game.GameSceneController>();

        // 保存场景
        string path = "Assets/Scenes/GameSceneUIToolkit.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[GameSceneBuilderUIToolkit] Scene saved: {path}");
    }
}
