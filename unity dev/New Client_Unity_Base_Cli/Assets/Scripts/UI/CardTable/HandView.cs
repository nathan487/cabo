using System.Collections.Generic;
using UnityEngine;

namespace Cabo.Client.UI.CardTable
{
    public sealed class HandView : MonoBehaviour
    {
        readonly Dictionary<int, CardSlotView> _slots = new();
        readonly Dictionary<int, CardView> _cards = new();

        RectTransform _slotRoot;
        RectTransform _cardRoot;

        public long PlayerId { get; private set; }
        public IReadOnlyDictionary<int, CardView> Cards => _cards;

        public static HandView Create(Transform parent, Transform cardRoot, long playerId)
        {
            var go = new GameObject($"Hand_{playerId}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<HandView>();
            view.Initialize(cardRoot, playerId);
            return view;
        }

        void Initialize(Transform cardRoot, long playerId)
        {
            PlayerId = playerId;
            if (_slotRoot == null)
            {
                _slotRoot = GetComponent<RectTransform>();
                _slotRoot.anchorMin = Vector2.zero;
                _slotRoot.anchorMax = Vector2.one;
                _slotRoot.offsetMin = Vector2.zero;
                _slotRoot.offsetMax = Vector2.zero;
            }
            _cardRoot = cardRoot as RectTransform;
        }

        public void SetLayout(IReadOnlyList<CardTableSlotLayout> layouts)
        {
            var live = new HashSet<int>();
            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout.PlayerId != PlayerId || !layout.IsValid)
                    continue;

                live.Add(layout.SlotIndex);
                var slot = GetOrCreateSlot(layout.SlotIndex);
                slot.Configure(PlayerId, layout.SlotIndex, layout.AnchoredPosition, layout.Size);
            }

            RemoveMissingSlots(live);
        }

        public void RenderAuthoritative(IReadOnlyList<CardTableSlotLayout> layouts)
        {
            SetLayout(layouts);
            var live = new HashSet<int>();
            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout.PlayerId != PlayerId || !layout.IsValid)
                    continue;

                live.Add(layout.SlotIndex);
                var card = GetOrCreateCard(layout.SlotIndex);
                var slot = GetOrCreateSlot(layout.SlotIndex);
                card.CancelAnimations();
                card.RectTransform.anchoredPosition = slot.RectTransform.anchoredPosition;
                card.SetSize(layout.Size);
                if (layout.FaceUp)
                    card.ShowFront(layout.Value);
                else
                    card.ShowBack();
                card.SetKnowledgeMarkers(layout.LocallyPeeked, layout.PubliclyKnown);
                card.SetSelected(layout.Selected);
                card.SetInteraction(layout.Clickable, layout.Clicked);
                card.SetVisible(true);
                slot.CurrentCard = card;
            }

            RemoveMissingCards(live);
        }

        public bool RefreshLayoutAndInteractions(IReadOnlyList<CardTableSlotLayout> layouts)
        {
            var live = new HashSet<int>();
            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout.PlayerId == PlayerId && layout.IsValid)
                    live.Add(layout.SlotIndex);
            }

            if (live.Count != _cards.Count)
                return false;
            foreach (var slotIndex in live)
                if (GetCard(slotIndex) == null)
                    return false;

            SetLayout(layouts);
            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout.PlayerId != PlayerId || !layout.IsValid)
                    continue;

                var card = GetCard(layout.SlotIndex);
                if (card == null)
                    continue;

                card.SetKnowledgeMarkers(layout.LocallyPeeked, layout.PubliclyKnown);
                card.SetSelected(layout.Selected);
                card.SetInteraction(layout.Clickable, layout.Clicked);
            }

            return true;
        }

        public void DisableCardInteractions()
        {
            foreach (var card in _cards.Values)
            {
                if (card != null)
                    card.SetInteraction(false, null);
            }
        }

        public void CancelCardAnimations()
        {
            foreach (var card in _cards.Values)
            {
                if (card != null)
                    card.CancelAnimations();
            }
        }

        public CardSlotView GetSlot(int slotIndex)
        {
            _slots.TryGetValue(slotIndex, out var slot);
            return slot;
        }

        public CardView GetCard(int slotIndex)
        {
            if (_cards.TryGetValue(slotIndex, out var card) && card != null)
                return card;

            _cards.Remove(slotIndex);
            if (_slots.TryGetValue(slotIndex, out var slot))
                slot.CurrentCard = null;
            return null;
        }

        public CardView DetachCard(int slotIndex)
        {
            if (!_cards.TryGetValue(slotIndex, out var card))
                return null;

            _cards.Remove(slotIndex);
            if (_slots.TryGetValue(slotIndex, out var slot))
                slot.CurrentCard = null;
            if (card == null)
                return null;

            card.transform.SetParent(_cardRoot, false);
            card.SetSelected(false);
            card.SetInteraction(false, null);
            return card;
        }

        public void AttachCard(int slotIndex, CardView card, bool snapToSlot = true)
        {
            if (card == null)
                return;

            var slot = GetOrCreateSlot(slotIndex);
            if (_cards.TryGetValue(slotIndex, out var existing) && existing != null && existing != card)
                Destroy(existing.gameObject);
            _cards[slotIndex] = card;
            slot.CurrentCard = card;
            card.transform.SetParent(_cardRoot, false);
            if (snapToSlot)
                card.CancelAnimations();
            card.SetSize(slot.RectTransform.sizeDelta);
            card.SetSelected(false);
            card.SetInteraction(false, null);
            if (snapToSlot)
                card.RectTransform.anchoredPosition = slot.RectTransform.anchoredPosition;
        }

        CardSlotView GetOrCreateSlot(int slotIndex)
        {
            if (_slots.TryGetValue(slotIndex, out var slot))
                return slot;

            slot = CardSlotView.Create(_slotRoot, $"Slot_{PlayerId}_{slotIndex}");
            slot.PlayerId = PlayerId;
            slot.SlotIndex = slotIndex;
            _slots[slotIndex] = slot;
            return slot;
        }

        CardView GetOrCreateCard(int slotIndex)
        {
            if (_cards.TryGetValue(slotIndex, out var card) && card != null)
                return card;

            _cards.Remove(slotIndex);
            card = CardView.Create(_cardRoot, $"Card_{PlayerId}_{slotIndex}");
            _cards[slotIndex] = card;
            return card;
        }

        void RemoveMissingSlots(HashSet<int> live)
        {
            var remove = new List<int>();
            foreach (var slot in _slots)
                if (!live.Contains(slot.Key))
                    remove.Add(slot.Key);

            for (int i = 0; i < remove.Count; i++)
            {
                var key = remove[i];
                if (_slots.TryGetValue(key, out var slot) && slot != null)
                    Destroy(slot.gameObject);
                _slots.Remove(key);
            }
        }

        void RemoveMissingCards(HashSet<int> live)
        {
            var remove = new List<int>();
            foreach (var card in _cards)
                if (!live.Contains(card.Key))
                    remove.Add(card.Key);

            for (int i = 0; i < remove.Count; i++)
            {
                var key = remove[i];
                if (_cards.TryGetValue(key, out var card) && card != null)
                    Destroy(card.gameObject);
                _cards.Remove(key);
            }
        }
    }
}
