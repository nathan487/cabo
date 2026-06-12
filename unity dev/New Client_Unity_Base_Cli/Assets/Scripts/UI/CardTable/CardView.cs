using System.Collections;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cabo.Client.UI.CardTable
{
    public sealed class CardView : MonoBehaviour, IPointerClickHandler
    {
        Image _background;
        Text _label;
        Text _skillBadge;
        Outline _outline;
        CanvasGroup _canvasGroup;
        Coroutine _moveRoutine;
        Coroutine _flipRoutine;
        Action _onClicked;

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

            _outline = gameObject.GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(2f, -2f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4f, 12f);
            labelRect.offsetMax = new Vector2(-4f, -8f);

            _label = labelGo.GetComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontStyle = FontStyle.Bold;
            _label.raycastTarget = false;

            var badgeGo = new GameObject("SkillBadge", typeof(RectTransform), typeof(Text));
            badgeGo.transform.SetParent(transform, false);
            var badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0.34f);
            badgeRect.offsetMin = new Vector2(4f, 4f);
            badgeRect.offsetMax = new Vector2(-4f, -2f);

            _skillBadge = badgeGo.GetComponent<Text>();
            _skillBadge.alignment = TextAnchor.MiddleCenter;
            _skillBadge.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _skillBadge.fontStyle = FontStyle.Bold;
            _skillBadge.raycastTarget = false;
            _skillBadge.color = UITheme.TextOnAccent;
            _skillBadge.gameObject.SetActive(false);

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = false;
        }

        public void SetSize(Vector2 size)
        {
            RectTransform.sizeDelta = new Vector2(Mathf.Max(24f, size.x), Mathf.Max(32f, size.y));
            _label.fontSize = Mathf.RoundToInt(FaceUp ? RectTransform.sizeDelta.y * 0.34f : RectTransform.sizeDelta.y * 0.16f);
            _skillBadge.fontSize = Mathf.Max(8, Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.11f));
        }

        public void ShowFront(int value, bool showSkillBadge = true)
        {
            Value = value;
            FaceUp = true;
            _background.color = UITheme.CardFace(value);
            _label.text = value.ToString();
            _label.color = UITheme.TextPrimary;
            _label.fontSize = Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.34f);
            _outline.effectColor = UITheme.CardBorder;

            string skillName = showSkillBadge ? GetSkillShortName(value) : "";
            _skillBadge.text = skillName;
            _skillBadge.gameObject.SetActive(!string.IsNullOrEmpty(skillName));
        }

        public void ShowBack()
        {
            Value = 0;
            FaceUp = false;
            _background.color = UITheme.CardBack;
            _label.text = "CABO";
            _label.color = Color.white;
            _label.fontSize = Mathf.RoundToInt(RectTransform.sizeDelta.y * 0.16f);
            _outline.effectColor = UITheme.CardBorder;
            _skillBadge.gameObject.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            _outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
            _outline.effectColor = selected ? UITheme.SelectedBorder : UITheme.CardBorder;
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
            _onClicked?.Invoke();
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

        static string GetSkillShortName(int value)
        {
            if (value == 7 || value == 8) return "\u770b\u724c";
            if (value == 9 || value == 10) return "\u5077\u770b";
            if (value == 11 || value == 12) return "\u6362\u724c";
            return "";
        }
    }
}
