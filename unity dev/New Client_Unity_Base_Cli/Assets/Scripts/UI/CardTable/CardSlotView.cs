using UnityEngine;

namespace Cabo.Client.UI.CardTable
{
    public sealed class CardSlotView : MonoBehaviour
    {
        public long PlayerId;
        public int SlotIndex;
        public CardView CurrentCard;
        public RectTransform RectTransform { get; private set; }

        public static CardSlotView Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CardSlotView>();
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
        }

        public void Configure(long playerId, int slotIndex, Vector2 anchoredPosition, Vector2 size)
        {
            PlayerId = playerId;
            SlotIndex = slotIndex;
            RectTransform.anchoredPosition = anchoredPosition;
            RectTransform.sizeDelta = size;
        }
    }
}
