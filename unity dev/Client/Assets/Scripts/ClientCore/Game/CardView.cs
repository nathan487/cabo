using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    /// <summary>
    /// Single card view. Data-driven: shows value when known, "?" when unknown.
    /// Supports simple scale-based flip animation.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Image cardBack;
        [SerializeField] private Button clickButton;

        [Header("Config")]
        [SerializeField] private Gradient valueColorGradient;

        private static readonly Color UnknownColor = new Color(0.12f, 0.18f, 0.28f);

        public int SlotIndex { get; private set; }
        public bool IsKnown { get; private set; }
        public int CardValue { get; private set; }

        public System.Action<int> OnClicked; // slotIndex

        public void Initialize(int slotIndex)
        {
            SlotIndex = slotIndex;
            if (clickButton != null)
                clickButton.onClick.AddListener(() => OnClicked?.Invoke(SlotIndex));
            SetUnknown();
        }

        public void SetKnown(int value)
        {
            IsKnown = true;
            CardValue = value;
            if (valueText != null)
            {
                valueText.text = value.ToString();
                valueText.gameObject.SetActive(true);
            }
            if (cardBack != null) cardBack.gameObject.SetActive(false);
            if (background != null)
                background.color = EvaluateGradient(value);
        }

        public void SetUnknown()
        {
            IsKnown = false;
            CardValue = -1;
            if (valueText != null)
            {
                valueText.text = "?";
                valueText.gameObject.SetActive(true);
                valueText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            if (cardBack != null) cardBack.gameObject.SetActive(true);
            if (background != null)
                background.color = UnknownColor;
        }

        /// <summary>Flip to known value (scale X→0, change face, scale→1). ~0.3s.</summary>
        public System.Collections.IEnumerator FlipToKnown(int value)
        {
            yield return StartCoroutine(FlipHalf());
            SetKnown(value);
            yield return StartCoroutine(FlipHalfBack());
        }

        /// <summary>Flip to unknown (scale X→0, change face, scale→1). ~0.3s.</summary>
        public System.Collections.IEnumerator FlipToUnknown()
        {
            yield return StartCoroutine(FlipHalf());
            SetUnknown();
            yield return StartCoroutine(FlipHalfBack());
        }

        private System.Collections.IEnumerator FlipHalf()
        {
            float t = 0f;
            Vector3 s = transform.localScale;
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                transform.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.12f), s.y, s.z);
                yield return null;
            }
            transform.localScale = new Vector3(0f, s.y, s.z);
        }

        private System.Collections.IEnumerator FlipHalfBack()
        {
            float t = 0f;
            Vector3 s = transform.localScale;
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                transform.localScale = new Vector3(Mathf.Lerp(0f, 1f, t / 0.12f), s.y, s.z);
                yield return null;
            }
            transform.localScale = s;
        }

        private Color EvaluateGradient(int value)
        {
            if (valueColorGradient != null)
                return valueColorGradient.Evaluate(value / 13f);

            float t = value / 13f;
            if (t < 0.33f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f), new Color(0.8f, 0.8f, 0.1f), t / 0.33f);
            if (t < 0.66f) return Color.Lerp(new Color(0.8f, 0.8f, 0.1f), new Color(1f, 0.4f, 0.1f), (t - 0.33f) / 0.33f);
            return Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.1f, 0.1f), (t - 0.66f) / 0.34f);
        }
    }
}
