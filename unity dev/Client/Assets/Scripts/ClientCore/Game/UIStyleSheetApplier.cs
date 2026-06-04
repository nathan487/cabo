using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.Game
{
    /// <summary>辅助组件：在运行时应用样式表到 UIDocument</summary>
    public class UIStyleSheetApplier : MonoBehaviour
    {
        public StyleSheet styleSheet;

        void Start()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null && styleSheet != null)
            {
                uiDoc.rootVisualElement.styleSheets.Add(styleSheet);
            }
        }
    }
}
