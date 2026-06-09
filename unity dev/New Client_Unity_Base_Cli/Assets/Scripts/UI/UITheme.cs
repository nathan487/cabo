using UnityEngine;
using UnityEngine.UIElements;
using Game.Common;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Central runtime theme for UI Toolkit elements that need C# state styling.
    /// USS owns default controls; this class owns dynamic and fallback styles.
    /// </summary>
    public static class UITheme
    {
        public static readonly Color AppBackground = new(0.98f, 0.94f, 0.86f, 1f);
        public static readonly Color AppBackgroundSoft = new(1.00f, 0.97f, 0.90f, 1f);
        public static readonly Color PanelSurface = new(1.00f, 0.98f, 0.91f, 1f);
        public static readonly Color PanelSurfaceAlt = new(0.96f, 0.91f, 0.80f, 1f);
        public static readonly Color PanelBorder = new(0.75f, 0.62f, 0.43f, 1f);

        public static readonly Color TableSurface = new(0.75f, 0.88f, 0.74f, 1f);
        public static readonly Color TableSurfaceAlt = new(0.66f, 0.82f, 0.68f, 1f);
        public static readonly Color TableBorder = new(0.34f, 0.55f, 0.39f, 1f);

        public static readonly Color TextPrimary = new(0.14f, 0.20f, 0.16f, 1f);
        public static readonly Color TextSecondary = new(0.35f, 0.43f, 0.34f, 1f);
        public static readonly Color TextMuted = new(0.47f, 0.53f, 0.44f, 1f);
        public static readonly Color TextOnAccent = new(0.24f, 0.15f, 0.09f, 1f);
        public static readonly Color TextOnDanger = Color.white;

        public static readonly Color AccentPeach = new(0.93f, 0.55f, 0.36f, 1f);
        public static readonly Color AccentPeachHover = new(1.00f, 0.66f, 0.44f, 1f);
        public static readonly Color AccentPeachBorder = new(0.75f, 0.34f, 0.20f, 1f);

        public static readonly Color ButtonDisabled = new(0.86f, 0.82f, 0.74f, 1f);
        public static readonly Color ButtonDisabledBorder = new(0.68f, 0.62f, 0.54f, 1f);
        public static readonly Color TextDisabled = new(0.50f, 0.47f, 0.41f, 1f);

        public static readonly Color InputBackground = new(1.00f, 0.99f, 0.95f, 1f);
        public static readonly Color InputBorder = new(0.72f, 0.61f, 0.45f, 1f);

        public static readonly Color TurnHighlight = new(0.95f, 0.73f, 0.25f, 1f);
        public static readonly Color SelectedBorder = new(1.00f, 0.70f, 0.16f, 1f);
        public static readonly Color SelectedSurface = new(1.00f, 0.93f, 0.67f, 1f);

        public static readonly Color CaboDanger = new(0.80f, 0.22f, 0.25f, 1f);
        public static readonly Color CaboSurface = new(1.00f, 0.86f, 0.82f, 1f);
        public static readonly Color CaboBorder = new(0.87f, 0.25f, 0.29f, 1f);

        public static readonly Color ReadySurface = new(0.78f, 0.91f, 0.72f, 1f);
        public static readonly Color ReadyBorder = new(0.36f, 0.68f, 0.36f, 1f);
        public static readonly Color WaitingSurface = new(0.95f, 0.90f, 0.80f, 1f);
        public static readonly Color WaitingBorder = new(0.72f, 0.61f, 0.45f, 1f);

        public static readonly Color ChatSelfBubble = new(0.86f, 0.94f, 0.80f, 1f);
        public static readonly Color ChatOtherBubble = new(1.00f, 0.94f, 0.84f, 1f);
        public static readonly Color FeedBubble = new(0.94f, 0.90f, 0.80f, 1f);
        public static readonly Color StickerPopupSurface = new(1.00f, 0.96f, 0.86f, 1f);

        public static readonly Color CardLow = new(0.84f, 0.95f, 0.78f, 1f);
        public static readonly Color CardMid = new(0.98f, 0.95f, 0.85f, 1f);
        public static readonly Color CardSkill = new(1.00f, 0.88f, 0.62f, 1f);
        public static readonly Color CardHigh = new(1.00f, 0.80f, 0.74f, 1f);
        public static readonly Color CardBack = new(0.42f, 0.55f, 0.78f, 1f);
        public static readonly Color CardBorder = new(0.67f, 0.58f, 0.44f, 1f);

        public static readonly Color SkillPeek = new(0.20f, 0.64f, 0.72f, 1f);
        public static readonly Color SkillSpy = new(0.70f, 0.45f, 0.78f, 1f);
        public static readonly Color SkillSwap = new(0.95f, 0.55f, 0.16f, 1f);

        public static void ApplyRoot(VisualElement root)
        {
            if (root == null) return;
            root.style.backgroundColor = AppBackground;
            root.style.color = TextPrimary;
        }

        public static void ApplyPanel(VisualElement element, Color? surface = null, Color? border = null, float radius = 8f, float borderWidth = 1f)
        {
            if (element == null) return;
            element.style.backgroundColor = surface ?? PanelSurface;
            SetRadius(element, radius);
            SetBorderWidth(element, borderWidth);
            SetBorderColor(element, border ?? PanelBorder);
        }

        public static void ApplyButton(Button button, bool enabled)
        {
            if (button == null) return;
            button.style.color = enabled ? TextOnAccent : TextDisabled;
            button.style.backgroundColor = enabled ? AccentPeach : ButtonDisabled;
            SetRadius(button, 6f);
            SetBorderWidth(button, 1f);
            SetBorderColor(button, enabled ? AccentPeachBorder : ButtonDisabledBorder);
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        public static void ApplyInput(TextField field)
        {
            if (field == null) return;
            field.style.color = TextPrimary;
            field.style.backgroundColor = InputBackground;
        }

        public static void ApplyInputElement(VisualElement input)
        {
            if (input == null) return;
            input.style.color = TextPrimary;
            input.style.backgroundColor = InputBackground;
        }

        public static void SetBorderColor(VisualElement element, Color color)
        {
            if (element == null) return;
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }

        public static void SetBorderWidth(VisualElement element, float width)
        {
            if (element == null) return;
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
        }

        public static void SetRadius(VisualElement element, float radius)
        {
            if (element == null) return;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        public static Color SkillColor(SkillType skill)
        {
            return skill switch
            {
                SkillType.PeekSelf => SkillPeek,
                SkillType.Spy => SkillSpy,
                SkillType.Swap => SkillSwap,
                _ => TurnHighlight
            };
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static Color CardFace(int value)
        {
            if (value <= 3) return CardLow;
            if (value >= 10) return CardHigh;
            if (value >= 7) return CardSkill;
            return CardMid;
        }

        public static float ContrastRatio(Color foreground, Color background)
        {
            float a = RelativeLuminance(foreground) + 0.05f;
            float b = RelativeLuminance(background) + 0.05f;
            return a > b ? a / b : b / a;
        }

        static float RelativeLuminance(Color color)
        {
            return 0.2126f * LinearChannel(color.r)
                + 0.7152f * LinearChannel(color.g)
                + 0.0722f * LinearChannel(color.b);
        }

        static float LinearChannel(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }
    }
}
