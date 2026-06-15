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
        public const string PrimaryButtonClass = "cabo-button-primary";
        public const string SecondaryButtonClass = "cabo-button-secondary";
        public const string SoftButtonClass = "cabo-button-soft";
        public const string DangerButtonClass = "cabo-button-danger";
        public const string TitleTextClass = "cabo-title-text";

        static Texture2D _hostCrownIcon;
        static Font _bodyFont;
        static Font _displayFont;
        static bool _fontsLoaded;

        public static readonly Color AppBackground = new(0.96f, 0.90f, 0.78f, 1f);
        public static readonly Color AppBackgroundSoft = new(0.99f, 0.94f, 0.83f, 1f);
        public static readonly Color PanelSurface = new(1.00f, 0.97f, 0.87f, 1f);
        public static readonly Color PanelSurfaceAlt = new(0.91f, 0.83f, 0.67f, 1f);
        public static readonly Color PanelGlass = new(1.00f, 0.97f, 0.87f, 0.92f);
        public static readonly Color PanelGlassStrong = new(1.00f, 0.98f, 0.91f, 0.97f);
        public static readonly Color PanelBorder = new(0.47f, 0.35f, 0.20f, 1f);

        public static readonly Color TableSurface = new(0.62f, 0.78f, 0.57f, 1f);
        public static readonly Color TableSurfaceAlt = new(0.48f, 0.68f, 0.50f, 1f);
        public static readonly Color TableSeatGlass = new(1.00f, 0.98f, 0.90f, 0.84f);
        public static readonly Color TableSocialGlass = new(1.00f, 0.98f, 0.90f, 0.92f);
        public static readonly Color TableGlass = new(0.90f, 0.96f, 0.82f, 0.68f);
        public static readonly Color TableSoftBorder = new(0.31f, 0.24f, 0.14f, 0.72f);
        public static readonly Color TableBorder = new(0.16f, 0.34f, 0.20f, 1f);

        public static readonly Color TextPrimary = new(0.22f, 0.15f, 0.20f, 1f);
        public static readonly Color TextSecondary = new(0.31f, 0.25f, 0.29f, 1f);
        public static readonly Color TextMuted = new(0.43f, 0.37f, 0.40f, 1f);
        public static readonly Color TextOnAccent = new(1.00f, 0.98f, 0.93f, 1f);
        public static readonly Color TextOnDanger = Color.white;

        public static readonly Color AccentPeach = new(0.85f, 0.37f, 0.48f, 1f);
        public static readonly Color AccentPeachHover = new(0.92f, 0.48f, 0.57f, 1f);
        public static readonly Color AccentPeachPressed = new(0.72f, 0.27f, 0.40f, 1f);
        public static readonly Color AccentPeachBorder = new(0.48f, 0.18f, 0.29f, 1f);

        public static readonly Color AccentMint = new(0.55f, 0.78f, 0.69f, 1f);
        public static readonly Color AccentMintHover = new(0.64f, 0.85f, 0.76f, 1f);
        public static readonly Color AccentMintPressed = new(0.44f, 0.69f, 0.59f, 1f);
        public static readonly Color AccentMintBorder = new(0.22f, 0.42f, 0.35f, 1f);

        public static readonly Color AccentButter = new(0.95f, 0.83f, 0.56f, 1f);
        public static readonly Color AccentButterHover = new(1.00f, 0.90f, 0.67f, 1f);
        public static readonly Color AccentButterPressed = new(0.86f, 0.73f, 0.43f, 1f);
        public static readonly Color AccentButterBorder = new(0.54f, 0.40f, 0.20f, 1f);

        public static readonly Color AccentDanger = new(0.78f, 0.32f, 0.32f, 1f);
        public static readonly Color AccentDangerHover = new(0.87f, 0.41f, 0.38f, 1f);
        public static readonly Color AccentDangerPressed = new(0.65f, 0.25f, 0.28f, 1f);
        public static readonly Color AccentDangerBorder = new(0.43f, 0.16f, 0.20f, 1f);

        public static readonly Color ButtonDisabled = new(0.73f, 0.69f, 0.61f, 1f);
        public static readonly Color ButtonDisabledBorder = new(0.43f, 0.38f, 0.31f, 1f);
        public static readonly Color TextDisabled = new(0.24f, 0.22f, 0.18f, 1f);

        public static readonly Color InputBackground = new(1.00f, 0.99f, 0.94f, 1f);
        public static readonly Color InputBorder = new(0.38f, 0.28f, 0.16f, 1f);
        public static readonly Color HostBadgeSurface = new(1.00f, 0.86f, 0.45f, 1f);
        public static readonly Color HostBadgeBorder = new(0.52f, 0.25f, 0.00f, 1f);

        public static readonly Color TurnHighlight = new(0.93f, 0.61f, 0.09f, 1f);
        public static readonly Color TurnBorder = new(0.52f, 0.25f, 0.00f, 1f);
        public static readonly Color SelectedBorder = new(0.58f, 0.20f, 0.00f, 1f);
        public static readonly Color SelectedSurface = new(1.00f, 0.86f, 0.45f, 1f);

        public static readonly Color CaboDanger = new(0.68f, 0.08f, 0.13f, 1f);
        public static readonly Color CaboSurface = new(0.98f, 0.76f, 0.70f, 1f);
        public static readonly Color CaboBorder = new(0.55f, 0.03f, 0.08f, 1f);

        public static readonly Color ReadySurface = new(0.68f, 0.86f, 0.59f, 1f);
        public static readonly Color ReadyBorder = new(0.17f, 0.48f, 0.19f, 1f);
        public static readonly Color WaitingSurface = new(0.90f, 0.82f, 0.66f, 1f);
        public static readonly Color WaitingBorder = new(0.46f, 0.35f, 0.20f, 1f);

        public static readonly Color ChatSelfBubble = new(0.76f, 0.91f, 0.67f, 1f);
        public static readonly Color ChatOtherBubble = new(0.98f, 0.88f, 0.70f, 1f);
        public static readonly Color FeedBubble = new(0.88f, 0.80f, 0.63f, 1f);
        public static readonly Color StickerPopupSurface = new(0.99f, 0.91f, 0.73f, 1f);

        public static readonly Color CardLow = new(0.74f, 0.91f, 0.63f, 1f);
        public static readonly Color CardMid = new(0.99f, 0.93f, 0.76f, 1f);
        public static readonly Color CardSkill = new(1.00f, 0.75f, 0.39f, 1f);
        public static readonly Color CardHigh = new(0.98f, 0.63f, 0.56f, 1f);
        public static readonly Color CardBack = new(0.13f, 0.30f, 0.50f, 1f);
        public static readonly Color CardBorder = new(0.31f, 0.23f, 0.14f, 1f);

        public static readonly Color SkillPeek = new(0.00f, 0.47f, 0.57f, 1f);
        public static readonly Color SkillSpy = new(0.54f, 0.20f, 0.66f, 1f);
        public static readonly Color SkillSwap = new(0.68f, 0.22f, 0.00f, 1f);

        public static void ApplyRoot(VisualElement root)
        {
            if (root == null) return;
            root.style.backgroundColor = Color.clear;
            root.style.color = TextPrimary;
            ApplyBodyFont(root);
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
            if (ContainsLatinLetter(button.text))
                ApplyBodyFont(button);
            else
                ApplyDisplayFont(button);
            ApplyButtonColors(button, enabled, false, false);
            bool compact = button.resolvedStyle.height > 0f && button.resolvedStyle.height <= 36f;
            SetRadius(button, compact ? 9f : 14f);
            button.style.borderTopWidth = 2f;
            button.style.borderRightWidth = 2f;
            button.style.borderLeftWidth = 2f;
            button.style.borderBottomWidth = compact ? 2f : 4f;
            if (!compact)
            {
                button.style.paddingTop = 8f;
                button.style.paddingBottom = 9f;
                button.style.paddingLeft = 16f;
                button.style.paddingRight = 16f;
            }
            button.style.unityTextAlign = TextAnchor.MiddleCenter;

            const string boundClass = "cabo-themed-button-bound";
            if (button.ClassListContains(boundClass))
                return;

            button.AddToClassList(boundClass);
            button.RegisterCallback<PointerEnterEvent>(_ => ApplyButtonColors(button, button.enabledSelf, true, false));
            button.RegisterCallback<PointerLeaveEvent>(_ => ApplyButtonColors(button, button.enabledSelf, false, false));
            button.RegisterCallback<PointerDownEvent>(_ => ApplyButtonColors(button, button.enabledSelf, false, true));
            button.RegisterCallback<PointerUpEvent>(_ => ApplyButtonColors(button, button.enabledSelf, true, false));
        }

        public static void SetButtonRole(Button button, string roleClass)
        {
            if (button == null) return;
            button.RemoveFromClassList(PrimaryButtonClass);
            button.RemoveFromClassList(SecondaryButtonClass);
            button.RemoveFromClassList(SoftButtonClass);
            button.RemoveFromClassList(DangerButtonClass);
            if (!string.IsNullOrEmpty(roleClass))
                button.AddToClassList(roleClass);
        }

        public static void ApplyBodyFont(VisualElement element)
        {
            if (element == null) return;
            EnsureFontsLoaded();
            if (_bodyFont != null)
                element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_bodyFont));
        }

        public static void ApplyDisplayFont(VisualElement element)
        {
            if (element == null) return;
            EnsureFontsLoaded();
            if (_displayFont != null)
                element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_displayFont));
        }

        public static void ApplyTitle(Label label)
        {
            if (label == null) return;
            label.AddToClassList(TitleTextClass);
            if (ContainsLatinLetter(label.text))
            {
                ApplyBodyFont(label);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                ApplyDisplayFont(label);
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        static void ApplyButtonColors(Button button, bool enabled, bool hovered, bool pressed)
        {
            if (!enabled)
            {
                button.style.color = TextDisabled;
                button.style.backgroundColor = ButtonDisabled;
                SetBorderColor(button, ButtonDisabledBorder);
                return;
            }

            Color normal;
            Color hover;
            Color down;
            Color border;
            Color text;
            if (button.ClassListContains(DangerButtonClass))
            {
                normal = AccentDanger;
                hover = AccentDangerHover;
                down = AccentDangerPressed;
                border = AccentDangerBorder;
                text = TextOnDanger;
            }
            else if (button.ClassListContains(SecondaryButtonClass))
            {
                normal = AccentMint;
                hover = AccentMintHover;
                down = AccentMintPressed;
                border = AccentMintBorder;
                text = TextPrimary;
            }
            else if (button.ClassListContains(SoftButtonClass))
            {
                normal = AccentButter;
                hover = AccentButterHover;
                down = AccentButterPressed;
                border = AccentButterBorder;
                text = TextPrimary;
            }
            else
            {
                normal = AccentPeach;
                hover = AccentPeachHover;
                down = AccentPeachPressed;
                border = AccentPeachBorder;
                text = TextOnAccent;
            }

            button.style.color = text;
            button.style.backgroundColor = pressed ? down : hovered ? hover : normal;
            SetBorderColor(button, border);
        }

        static void EnsureFontsLoaded()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;
            _bodyFont = Resources.Load<Font>("Fonts/Theme/NotoSansSC-Variable")
                ?? Resources.Load<Font>("Fonts/CaboChinese");
            _displayFont = Resources.Load<Font>("Fonts/Theme/ZCOOLKuaiLe-Regular")
                ?? _bodyFont;
        }

        static bool ContainsLatinLetter(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
                    return true;
            }
            return false;
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

        public static Texture2D HostCrownIcon
        {
            get
            {
                if (_hostCrownIcon == null)
                    _hostCrownIcon = CreateHostCrownIcon();
                return _hostCrownIcon;
            }
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

        static Texture2D CreateHostCrownIcon()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            texture.SetPixels(pixels);

            var fill = TextPrimary;
            FillTriangle(texture, new Vector2Int(3, 10), new Vector2Int(8, 24), new Vector2Int(13, 10), fill);
            FillTriangle(texture, new Vector2Int(10, 10), new Vector2Int(16, 28), new Vector2Int(22, 10), fill);
            FillTriangle(texture, new Vector2Int(19, 10), new Vector2Int(24, 24), new Vector2Int(29, 10), fill);
            FillRect(texture, 4, 6, 28, 13, fill);
            FillRect(texture, 3, 4, 29, 8, fill);
            texture.Apply(false, true);
            return texture;
        }

        static void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                    texture.SetPixel(x, y, color);
            }
        }

        static void FillTriangle(Texture2D texture, Vector2Int a, Vector2Int b, Vector2Int c, Color color)
        {
            int minX = Mathf.Max(0, Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            int maxX = Mathf.Min(texture.width - 1, Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int minY = Mathf.Max(0, Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            int maxY = Mathf.Min(texture.height - 1, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (PointInTriangle(new Vector2(x, y), a, b, c))
                        texture.SetPixel(x, y, color);
                }
            }
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
