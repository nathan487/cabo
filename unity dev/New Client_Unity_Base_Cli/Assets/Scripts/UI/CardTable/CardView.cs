using System;
using System.Collections;
using Cabo.Client.Art;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cabo.Client.UI.CardTable
{
    public sealed class CardView : MonoBehaviour, IPointerClickHandler
    {
        Image _background;
        Image _foodArt;
        Text _label;
        Text _foodName;
        Text _skillBadge;
        Image _knowledgeBadgeBackground;
        Text _knowledgeBadge;
        Outline _outline;
        CanvasGroup _canvasGroup;
        Coroutine _moveRoutine;
        Coroutine _flipRoutine;
        Coroutine _clickRoutine;
        Action _onClicked;
        bool _selected;
        bool _locallyPeeked;
        bool _publiclyKnown;
        Color _defaultOutlineColor;

        public RectTransform RectTransform { get; private set; }
        public int Value { get; private set; }
        public bool FaceUp { get; private set; }

        public static CardView Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<CardView>();
            view.Initialize();
            return view;
        }

        void Awake()
        {
            Initialize();
        }

        void Initialize()
        {
            if (RectTransform != null)
                return;

            RectTransform = GetComponent<RectTransform>();
            RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);

            _background = GetComponent<Image>();
            _background.raycastTarget = false;
            _background.type = Image.Type.Simple;

            var artGo = new GameObject("FoodArt", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.06f, 0.22f);
            artRect.anchorMax = new Vector2(0.94f, 0.93f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            _foodArt = artGo.GetComponent<Image>();
            _foodArt.preserveAspect = true;
            _foodArt.raycastTarget = false;

            _outline = gameObject.GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(2f, -2f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.04f, 0.70f);
            labelRect.anchorMax = new Vector2(0.56f, 0.97f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _label = labelGo.GetComponent<Text>();
            _label.alignment = TextAnchor.UpperLeft;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontStyle = FontStyle.Bold;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            var valueOutline = labelGo.AddComponent<Outline>();
            valueOutline.effectColor = new Color(1f, 1f, 1f, 0.95f);
            valueOutline.effectDistance = new Vector2(1f, -1f);

            var nameGo = new GameObject("FoodName", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(transform, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.03f, 0.015f);
            nameRect.anchorMax = new Vector2(0.97f, 0.25f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            _foodName = nameGo.GetComponent<Text>();
            _foodName.alignment = TextAnchor.MiddleCenter;
            _foodName.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _foodName.fontStyle = FontStyle.Bold;
            _foodName.raycastTarget = false;

            var badgeGo = new GameObject("SkillBadge", typeof(RectTransform), typeof(Text));
            badgeGo.transform.SetParent(transform, false);
            var badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.56f, 0.72f);
            badgeRect.anchorMax = new Vector2(0.96f, 0.96f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;

            _skillBadge = badgeGo.GetComponent<Text>();
            _skillBadge.alignment = TextAnchor.UpperRight;
            _skillBadge.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skillBadge.fontStyle = FontStyle.Bold;
            _skillBadge.raycastTarget = false;
            _skillBadge.color = UITheme.SkillSpy;
            _skillBadge.gameObject.SetActive(false);
            var badgeOutline = badgeGo.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(1f, 1f, 1f, 0.95f);
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            var knowledgeGo = new GameObject("KnowledgeBadge", typeof(RectTransform), typeof(Image));
            knowledgeGo.transform.SetParent(transform, false);
            var knowledgeRect = knowledgeGo.GetComponent<RectTransform>();
            knowledgeRect.anchorMin = new Vector2(0.48f, 0.51f);
            knowledgeRect.anchorMax = new Vector2(0.96f, 0.70f);
            knowledgeRect.offsetMin = Vector2.zero;
            knowledgeRect.offsetMax = Vector2.zero;
            _knowledgeBadgeBackground = knowledgeGo.GetComponent<Image>();
            _knowledgeBadgeBackground.raycastTarget = false;

            var knowledgeTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            knowledgeTextGo.transform.SetParent(knowledgeGo.transform, false);
            var knowledgeTextRect = knowledgeTextGo.GetComponent<RectTransform>();
            knowledgeTextRect.anchorMin = Vector2.zero;
            knowledgeTextRect.anchorMax = Vector2.one;
            knowledgeTextRect.offsetMin = Vector2.zero;
            knowledgeTextRect.offsetMax = Vector2.zero;
            _knowledgeBadge = knowledgeTextGo.GetComponent<Text>();
            _knowledgeBadge.alignment = TextAnchor.MiddleCenter;
            _knowledgeBadge.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _knowledgeBadge.fontStyle = FontStyle.Bold;
            _knowledgeBadge.color = Color.white;
            _knowledgeBadge.raycastTarget = false;
            knowledgeGo.SetActive(false);

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = false;
        }

        public void SetSize(Vector2 size)
        {
            RectTransform.sizeDelta = new Vector2(Mathf.Max(24f, size.x), Mathf.Max(32f, size.y));
            _label.fontSize = Mathf.Max(10, Mathf.RoundToInt(RectTransform.sizeDelta.y * (FaceUp ? 0.22f : 0.16f)));
            _foodName.fontSize = Mathf.Max(7, Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.105f));
            _skillBadge.fontSize = Mathf.Max(7, Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.095f));
            _knowledgeBadge.fontSize = Mathf.Max(7, Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.09f));
        }

        public void ShowFront(int value, bool showSkillBadge = true)
        {
            Value = value;
            FaceUp = true;
            var food = CaboArt.GetFood(value);
            _background.sprite = null;
            _background.color = Color.Lerp(UITheme.PanelSurface, food.accentColor, 0.13f);
            _foodArt.sprite = food.foodSprite;
            _foodArt.color = Color.white;
            _foodArt.gameObject.SetActive(food.foodSprite != null);
            _label.text = value.ToString();
            _label.color = UITheme.TextPrimary;
            _label.alignment = TextAnchor.UpperLeft;
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0.04f, 0.70f);
            labelRect.anchorMax = new Vector2(0.56f, 0.97f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label.fontSize = Mathf.Max(10, Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.22f));
            _label.gameObject.SetActive(true);
            _foodName.text = food.displayName;
            _foodName.color = UITheme.TextPrimary;
            _foodName.gameObject.SetActive(true);
            _defaultOutlineColor = Color.Lerp(UITheme.CardBorder, food.accentColor, 0.45f);

            string skillName = showSkillBadge ? food.skillLabel : "";
            _skillBadge.text = skillName;
            _skillBadge.color = SkillBadgeColor(value);
            _skillBadge.gameObject.SetActive(!string.IsNullOrEmpty(skillName));
            RefreshKnowledgeStyle();
        }

        public void ShowBack()
        {
            Value = 0;
            FaceUp = false;
            _background.sprite = CaboArt.CardBack;
            _background.color = _background.sprite != null ? Color.white : UITheme.CardBack;
            _foodArt.gameObject.SetActive(false);
            _foodName.gameObject.SetActive(false);
            _label.text = _background.sprite == null ? "CABO" : "";
            _label.color = Color.white;
            _label.alignment = TextAnchor.MiddleCenter;
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            _label.fontSize = Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.16f);
            _label.gameObject.SetActive(_background.sprite == null);
            _defaultOutlineColor = UITheme.CardBorder;
            _skillBadge.gameObject.SetActive(false);
            RefreshKnowledgeStyle();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            RefreshKnowledgeStyle();
        }

        public void SetKnowledgeMarkers(bool locallyPeeked, bool publiclyKnown)
        {
            _locallyPeeked = locallyPeeked;
            _publiclyKnown = publiclyKnown;
            RefreshKnowledgeStyle();
        }

        void RefreshKnowledgeStyle()
        {
            if (_knowledgeBadge == null || _knowledgeBadgeBackground == null || _outline == null)
                return;

            bool showPeeked = _locallyPeeked && !FaceUp;
            bool showPublic = _publiclyKnown && FaceUp;
            _knowledgeBadgeBackground.gameObject.SetActive(showPeeked || showPublic);
            if (showPublic)
            {
                _knowledgeBadge.text = "公开";
                _knowledgeBadgeBackground.color = new Color(0.93f, 0.38f, 0.05f, 0.94f);
            }
            else if (showPeeked)
            {
                _knowledgeBadge.text = "已看";
                _knowledgeBadgeBackground.color = new Color(0.46f, 0.20f, 0.68f, 0.94f);
            }

            if (!FaceUp)
            {
                if (_background.sprite != null)
                    _background.color = showPeeked ? new Color(0.82f, 0.72f, 1f, 1f) : Color.white;
                else
                    _background.color = showPeeked ? new Color(0.44f, 0.29f, 0.67f, 1f) : UITheme.CardBack;
            }

            _outline.effectDistance = _selected || showPublic ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            _outline.effectColor = _selected
                ? UITheme.SelectedBorder
                : showPublic
                    ? new Color(1f, 0.42f, 0.05f, 1f)
                    : showPeeked
                        ? new Color(0.58f, 0.34f, 0.82f, 1f)
                        : _defaultOutlineColor;
        }

        public void SetInteraction(bool clickable, Action onClicked)
        {
            _onClicked = clickable ? onClicked : null;
            _canvasGroup.blocksRaycasts = true;
            _background.raycastTarget = clickable && _onClicked != null;
        }

        public void CancelAnimations()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (_flipRoutine != null)
            {
                StopCoroutine(_flipRoutine);
                _flipRoutine = null;
            }

            RectTransform.localScale = Vector3.one;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_onClicked == null || _clickRoutine != null)
                return;

            var click = _onClicked;
            _clickRoutine = StartCoroutine(InvokeClickNextFrame(click));
        }

        IEnumerator InvokeClickNextFrame(Action click)
        {
            yield return null;
            _clickRoutine = null;

            if (this == null || !isActiveAndEnabled || click == null || click != _onClicked)
                yield break;

            click.Invoke();
        }

        public Coroutine MoveTo(RectTransform target, float duration)
        {
            return target == null ? null : MoveTo(target.anchoredPosition, duration);
        }

        public Coroutine MoveTo(Vector2 anchoredPosition, float duration)
        {
            if (_moveRoutine != null)
                StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(MoveRoutine(anchoredPosition, Mathf.Max(0.01f, duration)));
            return _moveRoutine;
        }

        public Coroutine FlipToFront(int value, float duration)
        {
            if (_flipRoutine != null)
                StopCoroutine(_flipRoutine);
            _flipRoutine = StartCoroutine(FlipRoutine(value, Mathf.Max(0.05f, duration)));
            return _flipRoutine;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        IEnumerator MoveRoutine(Vector2 target, float duration)
        {
            var start = RectTransform.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                RectTransform.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
                yield return null;
            }

            RectTransform.anchoredPosition = target;
            _moveRoutine = null;
        }

        IEnumerator FlipRoutine(int value, float duration)
        {
            float half = duration * 0.5f;
            yield return ScaleX(1f, 0.05f, half);
            ShowFront(value);
            yield return ScaleX(0.05f, 1f, half);
            _flipRoutine = null;
        }

        IEnumerator ScaleX(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                RectTransform.localScale = new Vector3(Mathf.Lerp(from, to, t), 1f, 1f);
                yield return null;
            }
            RectTransform.localScale = new Vector3(to, 1f, 1f);
        }

        static Color SkillBadgeColor(int value)
        {
            if (value == 7 || value == 8) return UITheme.SkillPeek;
            if (value == 9 || value == 10) return UITheme.SkillSpy;
            if (value == 11 || value == 12) return UITheme.SkillSwap;
            return UITheme.TextSecondary;
        }
    }
}
