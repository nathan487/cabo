using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal test: creates one text label to verify Canvas rendering works.
/// </summary>
public class SimpleTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[SimpleTest] Start running...");

        // Create Canvas
        var canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(800, 600);

        // Create EventSystem if missing
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // Create a text label
        var textGo = new GameObject("HelloText", typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        var text = textGo.GetComponent<Text>();
        text.text = "HELLO CABO!\nIf you see this, rendering works.";
        text.fontSize = 36;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(400, 200);
        rt.anchoredPosition = Vector2.zero;

        Debug.Log("[SimpleTest] UI created successfully!");
    }
}
