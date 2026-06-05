using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.Game
{
    /// <summary>
    /// UI Toolkit 测试脚本 - 用于在没有服务端时预览 UI
    /// NOTE: DISABLED - This component is disabled to prevent "Test Mode" from appearing during real games
    /// </summary>
    public class GameTableUIToolkitTester : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool enableTestMode = false;

        void Start()
        {
            // DISABLED: This tester interferes with real game sessions
            Debug.Log("[UIToolkitTester] Tester is DISABLED - use only for standalone UI testing");
            enabled = false;
            return;

            // ===========================================
            // CODE BELOW IS DISABLED (unreachable)
            // To re-enable: remove the "return;" above
            // ===========================================
#pragma warning disable CS0162 // Unreachable code detected

            if (!enableTestMode)

            // 自动获取 UIDocument
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                Debug.Log("[UIToolkitTester] 自动获取 UIDocument: " + (uiDocument != null ? "Success" : "Failed"));
            }

            if (uiDocument == null)
            {
                Debug.LogError("[UIToolkitTester] UIDocument 未找到！请确保该组件与 UIDocument 在同一 GameObject 上");
                return;
            }

            if (uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[UIToolkitTester] rootVisualElement 为 null！请检查 UIDocument 的 Source Asset 是否已设置");
                return;
            }

            var root = uiDocument.rootVisualElement;
            Debug.Log($"[UIToolkitTester] Root element: {root.name}, 子元素数量: {root.childCount}");

            // 填充测试数据
            FillTestData(root);
#pragma warning restore CS0162
        }

        private void FillTestData(VisualElement root)
        {
            int successCount = 0;
            int failCount = 0;

            // HUD
            var roundInfo = root.Q<Label>("RoundInfo");
            var phaseText = root.Q<Label>("PhaseText");

            if (roundInfo != null)
            {
                roundInfo.text = "Round 1 ";
                successCount++;
                Debug.Log("[UIToolkitTester] 设置 RoundInfo Success");
            }
            else
            {
                failCount++;
                Debug.LogWarning("[UIToolkitTester] 未找到 RoundInfo");
            }

            if (phaseText != null)
            {
                phaseText.text = "★ Test Mode - UI Preview ★";
                successCount++;
            }
            else
            {
                failCount++;
                Debug.LogWarning("[UIToolkitTester] 未找到 PhaseText");
            }

            // Opponent区域
            var oppName = root.Q<Label>("OpponentName");
            var oppScore = root.Q<Label>("OpponentScore");

            if (oppName != null) { oppName.text = "Opponent"; successCount++; }
            else { failCount++; Debug.LogWarning("[UIToolkitTester] 未找到 OpponentName"); }

            if (oppScore != null) { oppScore.text = "15 pts"; successCount++; }
            else { failCount++; }

            // Opponent卡牌
            for (int i = 0; i < 4; i++)
            {
                var card = root.Q<VisualElement>($"OpponentCard{i}");
                if (card != null)
                {
                    // 检查是否已有 Label
                    var existingLabel = card.Q<Label>();
                    if (existingLabel == null)
                    {
                        var label = new Label("?");
                        label.style.fontSize = 24;
                        label.style.color = Color.white;
                        card.Add(label);
                        successCount++;
                    }
                }
                else
                {
                    failCount++;
                }
            }

            // 自己区域
            var selfName = root.Q<Label>("SelfName");
            var selfScore = root.Q<Label>("SelfScore");

            if (selfName != null) { selfName.text = "You"; successCount++; }
            else { failCount++; Debug.LogWarning("[UIToolkitTester] 未找到 SelfName"); }

            if (selfScore != null) { selfScore.text = "8 pts"; successCount++; }
            else { failCount++; }

            // 自己卡牌 - 显示一些数字
            int[] cardValues = { 3, 7, -1, 11 }; // -1 表示未知
            for (int i = 0; i < 4; i++)
            {
                var card = root.Q<VisualElement>($"SelfCard{i}");
                if (card != null)
                {
                    string text = cardValues[i] >= 0 ? cardValues[i].ToString() : "?";

                    // 检查是否已有 Label
                    var existingLabel = card.Q<Label>();
                    if (existingLabel == null)
                    {
                        var label = new Label(text);
                        label.style.fontSize = 24;
                        label.style.color = Color.white;
                        card.Add(label);
                    }
                    else
                    {
                        existingLabel.text = text;
                    }

                    // 已知卡牌设置颜色
                    if (cardValues[i] >= 0)
                    {
                        card.RemoveFromClassList("card-back");
                        card.style.backgroundColor = GetCardColor(cardValues[i]);
                    }
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            // 牌堆
            var drawCount = root.Q<Label>("DrawCount");
            var discardTop = root.Q<Label>("DiscardTop");

            if (drawCount != null) { drawCount.text = "42"; successCount++; }
            else { failCount++; Debug.LogWarning("[UIToolkitTester] 未找到 DrawCount"); }

            if (discardTop != null) { discardTop.text = "5"; successCount++; }
            else { failCount++; Debug.LogWarning("[UIToolkitTester] 未找到 DiscardTop"); }

            Debug.Log($"[UIToolkitTester] 测试数据填充完成: {successCount} Success, {failCount} Failed");
        }

        private Color GetCardColor(int value)
        {
            float t = value / 13f;
            if (t < 0.33f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f), new Color(0.8f, 0.8f, 0.1f), t / 0.33f);
            if (t < 0.66f) return Color.Lerp(new Color(0.8f, 0.8f, 0.1f), new Color(1f, 0.4f, 0.1f), (t - 0.33f) / 0.33f);
            return Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.1f, 0.1f), (t - 0.66f) / 0.34f);
        }
    }
}
