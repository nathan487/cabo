using System.Collections;
using System.Collections.Generic;
using System;
using Cabo.Client;
using Game.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cabo.Client.UI.CardTable
{
    public struct CardTableSlotLayout
    {
        public long PlayerId;
        public int SlotIndex;
        public Vector2 AnchoredPosition;
        public Vector2 Size;
        public bool FaceUp;
        public int Value;
        public bool Selected;
        public bool Clickable;
        public Action Clicked;
        public bool IsValid => PlayerId > 0 && SlotIndex >= 0 && Size.x > 1f && Size.y > 1f;
    }

    public struct CardTableSlotSnapshot
    {
        public long PlayerId;
        public int SlotIndex;
        public Vector2 AnchoredPosition;
        public Vector2 Size;
        public bool FaceUp;
        public int Value;
        public bool IsValid => PlayerId > 0 && SlotIndex >= 0 && Size.x > 1f && Size.y > 1f;
    }

    public sealed class CardTableLayout
    {
        public readonly List<CardTableSlotLayout> Slots = new();
        public Vector2 DrawPilePosition;
        public Vector2 DrawPileSize;
        public Vector2 DiscardPilePosition;
        public Vector2 DiscardPileSize;
        public string DrawPileCaption;
        public string DiscardPileCaption;
    }

    public sealed class CardTableActionSnapshot
    {
        public long Sequence;
        public ActionType ActionType;
        public SkillType Skill;
        public long SourcePlayerId;
        public long TargetPlayerId;
        public int SourceSlot;
        public int TargetSlot;
        public bool SwapOccurred;
        public bool ExchangeSucceeded;
        public int IncomingCardValue;
        public int DiscardTopValue;
        public bool DrewExtraPenaltyCard;
        public Vector2 SourcePlayerPosition;
        public Vector2 TargetPlayerPosition;
        public Vector2 DrawPilePosition;
        public Vector2 DrawPileSize;
        public Vector2 DiscardPilePosition;
        public Vector2 DiscardPileSize;
        public Vector2 SourceInspectionPosition;
        public Vector2 TargetInspectionPosition;
        public Vector2 TargetInspectionSize;
        public int PeekedValue = -1;
        public readonly List<int> SelectedSlots = new();
        public readonly List<CardTableSlotSnapshot> SourceHand = new();
        public readonly List<CardTableSlotSnapshot> FinalSourceHand = new();
        public readonly List<CardTableSlotSnapshot> SourceSlots = new();
        public readonly List<CardTableSlotSnapshot> SourceSwapSlots = new();
        public readonly List<CardTableSlotSnapshot> TargetSlots = new();
    }

    public sealed class CardTableView : MonoBehaviour
    {
        const float MoveDuration = 0.70f;
        const float DiscardStagger = 0.20f;
        const float EmptyOriginHold = 0.20f;
        const float EmptyHold = 0.45f;
        const float IncomingLandingPause = 0.12f;
        const float SwapDuration = 0.90f;
        const float InspectHoldDuration = 1.15f;

        readonly Dictionary<long, HandView> _hands = new();
        readonly Dictionary<long, CardView> _drawnMarkers = new();
        readonly List<CardView> _transientCards = new();
        readonly HashSet<long> _animatingPlayers = new();

        RectTransform _root;
        RectTransform _slotRoot;
        RectTransform _cardRoot;
        CardSlotView _drawPileSlot;
        CardSlotView _discardPileSlot;
        CardView _drawPileCard;
        CardView _discardPileCard;
        Text _drawPileCaption;
        Text _discardPileCaption;
        GameState _lastState;
        CardTableLayout _lastLayout;
        long _myPlayerId;

        public bool HasRenderableLayout { get; private set; }

        public static CardTableView Create(Transform parent)
        {
            DestroyAllUnder(parent);

            var go = new GameObject("CardTableView", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var view = go.AddComponent<CardTableView>();
            view.Initialize();
            return view;
        }

        public static void DestroyAllUnder(Transform parent, CardTableView except = null)
        {
            if (parent == null)
                return;

            var views = parent.GetComponentsInChildren<CardTableView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || view == except)
                    continue;
                DestroyView(view);
            }
        }

        public static void DestroyView(CardTableView view)
        {
            if (view == null)
                return;

            view.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(view.gameObject);
            else
                DestroyImmediate(view.gameObject);
        }

        void Awake()
        {
            Initialize();
        }

        void Initialize()
        {
            if (_root != null)
                return;

            _root = GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            var group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = false;
            EnsureEventSystem();

            _slotRoot = CreateRoot("Slots");
            _cardRoot = CreateRoot("Cards");

            _drawPileSlot = CardSlotView.Create(_slotRoot, "DrawPileSlot");
            _discardPileSlot = CardSlotView.Create(_slotRoot, "DiscardPileSlot");
            _drawPileCard = CardView.Create(_cardRoot, "DrawPileCard");
            _discardPileCard = CardView.Create(_cardRoot, "DiscardPileCard");
            _drawPileCaption = CreateCaption("DrawPileCaption");
            _discardPileCaption = CreateCaption("DiscardPileCaption");
            _drawPileCard.ShowBack();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
        }

        RectTransform CreateRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        Text CreateCaption(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_cardRoot, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(130f, 22f);

            var text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = UITheme.TextSecondary;
            text.raycastTarget = false;
            return text;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ClearTransient()
        {
            StopAllCoroutines();
            foreach (var hand in _hands.Values)
                if (hand != null)
                    hand.CancelCardAnimations();
            var markers = new List<CardView>(_drawnMarkers.Values);
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (marker != null)
                    DestroyCard(marker);
            }
            DestroyTransientCards();
            _drawnMarkers.Clear();
            _animatingPlayers.Clear();
            if (_lastState != null && _lastLayout != null)
                Render(_lastState, _lastLayout);
        }

        public void Render(GameState state, CardTableLayout layout, long frozenPlayerId = 0, long secondFrozenPlayerId = 0)
        {
            if (state == null || layout == null)
                return;

            _lastState = state;
            _lastLayout = layout;
            _myPlayerId = state.MyPlayerId;
            HasRenderableLayout = layout.Slots.Count > 0;

            RenderPiles(state, layout);
            EnsureHands(state);

            foreach (var hand in _hands)
            {
                long playerId = hand.Key;
                bool frozen = playerId == frozenPlayerId || playerId == secondFrozenPlayerId || _animatingPlayers.Contains(playerId);
                if (frozen)
                {
                    hand.Value.SetLayout(layout.Slots);
                    hand.Value.DisableCardInteractions();
                }
                else
                    hand.Value.RenderAuthoritative(layout.Slots);
            }
        }

        public bool RefreshInteractions(GameState state, CardTableLayout layout)
        {
            if (state == null || layout == null || !HasRenderableLayout)
                return false;

            _lastState = state;
            _lastLayout = layout;
            _myPlayerId = state.MyPlayerId;

            RenderPiles(state, layout);
            EnsureHands(state);
            foreach (var hand in _hands)
            {
                if (_animatingPlayers.Contains(hand.Key))
                    hand.Value.DisableCardInteractions();
                else if (!hand.Value.RefreshLayoutAndInteractions(layout.Slots))
                    return false;
            }

            return true;
        }

        public bool PlayAction(CardTableActionSnapshot action)
        {
            if (!HasRenderableLayout || action == null || action.ActionType == ActionType.Unknown)
                return false;

            if (action.ActionType == ActionType.Draw)
            {
                if (action.SourcePlayerId == _myPlayerId)
                    return true;
                StartCoroutine(PlayDraw(action));
                return true;
            }

            if (action.ActionType == ActionType.DiscardDrawn)
            {
                StartCoroutine(PlayDiscardDrawn(action));
                return true;
            }

            if (action.ActionType == ActionType.ReplaceWithDrawn)
            {
                BeginExchange(action, false);
                return true;
            }

            if (action.ActionType == ActionType.TakeFromDiscard)
            {
                BeginExchange(action, true);
                return true;
            }

            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Swap && action.SwapOccurred)
            {
                BeginSwap(action);
                return true;
            }

            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.PeekSelf)
            {
                StartCoroutine(PlayPeekSelf(action));
                return true;
            }

            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Spy)
            {
                StartCoroutine(PlaySpy(action));
                return true;
            }

            return false;
        }

        public bool PlayLocalDraw(int value, Vector2 targetPosition, Vector2 targetSize)
        {
            if (!HasRenderableLayout || _lastLayout == null || _lastLayout.DrawPileSize.x <= 1f || _lastLayout.DrawPileSize.y <= 1f)
                return false;

            StartCoroutine(PlayLocalDrawRoutine(value, targetPosition, targetSize));
            return true;
        }

        IEnumerator PlayLocalDrawRoutine(int value, Vector2 targetPosition, Vector2 targetSize)
        {
            RemoveDrawnMarker(_myPlayerId);
            var marker = GetOrCreateDrawnMarker(_myPlayerId, targetSize);
            marker.RectTransform.anchoredPosition = _lastLayout.DrawPilePosition;
            marker.SetSize(targetSize.x > 1f && targetSize.y > 1f ? targetSize : new Vector2(70f, 96f));
            marker.ShowBack();
            marker.SetVisible(true);

            yield return marker.MoveTo(targetPosition, MoveDuration);
            yield return marker.FlipToFront(value, 0.18f);
        }

        void RenderPiles(GameState state, CardTableLayout layout)
        {
            if (layout.DrawPileSize.x > 1f && layout.DrawPileSize.y > 1f)
            {
                _drawPileSlot.Configure(0, 0, layout.DrawPilePosition, layout.DrawPileSize);
                _drawPileCard.RectTransform.anchoredPosition = layout.DrawPilePosition;
                _drawPileCard.SetSize(layout.DrawPileSize);
                _drawPileCard.ShowBack();
                _drawPileCard.SetInteraction(false, null);
                _drawPileCard.SetVisible(true);
                PositionCaption(_drawPileCaption, layout.DrawPileCaption, layout.DrawPilePosition, layout.DrawPileSize);
            }
            else
            {
                _drawPileCard.SetVisible(false);
                _drawPileCaption.gameObject.SetActive(false);
            }

            if (layout.DiscardPileSize.x > 1f && layout.DiscardPileSize.y > 1f)
            {
                _discardPileSlot.Configure(0, 1, layout.DiscardPilePosition, layout.DiscardPileSize);
                _discardPileCard.RectTransform.anchoredPosition = layout.DiscardPilePosition;
                _discardPileCard.SetSize(layout.DiscardPileSize);
                if (state.DiscardTopValue >= 0)
                    _discardPileCard.ShowFront(state.DiscardTopValue, false);
                else
                    _discardPileCard.ShowBack();
                _discardPileCard.SetInteraction(false, null);
                _discardPileCard.SetVisible(true);
                PositionCaption(_discardPileCaption, layout.DiscardPileCaption, layout.DiscardPilePosition, layout.DiscardPileSize);
            }
            else
            {
                _discardPileCard.SetVisible(false);
                _discardPileCaption.gameObject.SetActive(false);
            }
        }

        static void PositionCaption(Text caption, string text, Vector2 pilePosition, Vector2 pileSize)
        {
            if (caption == null)
                return;

            caption.text = text ?? "";
            caption.rectTransform.anchoredPosition = pilePosition + new Vector2(0f, -pileSize.y * 0.5f - 13f);
            caption.gameObject.SetActive(!string.IsNullOrEmpty(caption.text));
        }

        void EnsureHands(GameState state)
        {
            var live = new HashSet<long>();
            for (int i = 0; i < state.Players.Count; i++)
            {
                long playerId = state.Players[i].PlayerId;
                if (playerId <= 0)
                    continue;

                live.Add(playerId);
                if (!_hands.ContainsKey(playerId))
                    _hands[playerId] = HandView.Create(_slotRoot, _cardRoot, playerId);
            }

            var remove = new List<long>();
            foreach (var hand in _hands)
                if (!live.Contains(hand.Key))
                    remove.Add(hand.Key);

            for (int i = 0; i < remove.Count; i++)
            {
                var key = remove[i];
                if (_hands.TryGetValue(key, out var hand) && hand != null)
                    Destroy(hand.gameObject);
                _hands.Remove(key);
            }
        }

        IEnumerator PlayDraw(CardTableActionSnapshot action)
        {
            var marker = GetOrCreateDrawnMarker(action.SourcePlayerId, action.DrawPileSize);
            marker.RectTransform.anchoredPosition = action.DrawPilePosition;
            marker.SetSize(action.TargetInspectionSize.x > 1f && action.TargetInspectionSize.y > 1f
                ? action.TargetInspectionSize
                : action.DrawPileSize);
            marker.ShowBack();
            marker.SetVisible(true);

            Vector2 end = action.TargetInspectionPosition != Vector2.zero
                ? action.TargetInspectionPosition
                : (action.SourceInspectionPosition != Vector2.zero ? action.SourceInspectionPosition : action.SourcePlayerPosition);
            yield return marker.MoveTo(end, MoveDuration);
        }

        IEnumerator PlayDiscardDrawn(CardTableActionSnapshot action)
        {
            bool hadMarker = _drawnMarkers.ContainsKey(action.SourcePlayerId);
            var marker = GetOrCreateDrawnMarker(action.SourcePlayerId, action.DrawPileSize);
            if (!hadMarker)
                marker.RectTransform.anchoredPosition = action.TargetInspectionPosition != Vector2.zero
                    ? action.TargetInspectionPosition
                    : (action.SourceInspectionPosition != Vector2.zero ? action.SourceInspectionPosition : action.SourcePlayerPosition);
            if (action.DiscardTopValue >= 0)
                marker.ShowFront(action.DiscardTopValue);

            yield return marker.MoveTo(action.DiscardPilePosition, MoveDuration);
            RemoveDrawnMarker(action.SourcePlayerId);
            Reconcile();
        }

        void BeginExchange(CardTableActionSnapshot action, bool fromDiscard)
        {
            EnsureSelectedSlots(action);
            if (!HasExchangePlan(action))
            {
                Reconcile();
                return;
            }

            StartCoroutine(PlayExchange(action, fromDiscard));
        }

        bool HasExchangePlan(CardTableActionSnapshot action)
        {
            if (action == null || action.SourcePlayerId <= 0 || GetHand(action.SourcePlayerId) == null)
                return false;

            if (!action.ExchangeSucceeded)
                return action.SourceSlots.Count > 0 || action.SourceHand.Count > 0;

            if (action.SelectedSlots.Count == 0 || !HasUsablePile(action.DiscardPilePosition, action.DiscardPileSize))
                return false;

            if (action.SelectedSlots.Count > 1 && (action.SourceHand.Count == 0 || action.FinalSourceHand.Count == 0))
                return false;

            if (!HasAllSelectedSources(action))
                return false;

            var incomingFinal = FindIncomingFinalSlot(action, CountSurvivors(action));
            return incomingFinal.IsValid;
        }

        static void EnsureSelectedSlots(CardTableActionSnapshot action)
        {
            if (action == null || action.SelectedSlots.Count > 0 || action.SourceSlot < 0)
                return;

            action.SelectedSlots.Add(action.SourceSlot);
        }

        bool HasAllSelectedSources(CardTableActionSnapshot action)
        {
            var selected = new HashSet<int>(action.SelectedSlots);
            var source = action.SourceHand.Count > 0 ? action.SourceHand : action.SourceSlots;
            var hand = GetHand(action.SourcePlayerId);
            foreach (var slot in selected)
            {
                if (FindSnapshot(source, slot).IsValid)
                    continue;
                if (hand?.GetCard(slot) != null)
                    continue;
                return false;
            }
            return true;
        }

        int CountSurvivors(CardTableActionSnapshot action)
        {
            if (action.SelectedSlots.Count <= 1)
                return 0;

            var selected = new HashSet<int>(action.SelectedSlots);
            int count = 0;
            for (int i = 0; i < action.SourceHand.Count; i++)
                if (!selected.Contains(action.SourceHand[i].SlotIndex))
                    count++;
            return count;
        }

        IEnumerator PlayExchange(CardTableActionSnapshot action, bool fromDiscard)
        {
            if (!action.ExchangeSucceeded)
            {
                yield return ShakeSourceSlots(action);
                Reconcile();
                yield break;
            }

            _animatingPlayers.Add(action.SourcePlayerId);
            var hand = GetHand(action.SourcePlayerId);
            if (hand == null)
            {
                _animatingPlayers.Remove(action.SourcePlayerId);
                Reconcile();
                yield break;
            }

            var selected = new HashSet<int>(action.SelectedSlots);
            var outgoing = new List<CardView>();
            var sourceForOutgoing = action.SourceHand.Count > 0 ? action.SourceHand : action.SourceSlots;
            var outgoingSlots = new HashSet<int>();
            var discardPosition = GetDiscardPilePosition(action);
            for (int i = 0; i < sourceForOutgoing.Count; i++)
            {
                var slot = sourceForOutgoing[i];
                if (!selected.Contains(slot.SlotIndex))
                    continue;

                var card = hand.DetachCard(slot.SlotIndex);
                if (card == null)
                    card = CreateSnapshotCard(slot);
                TrackTransient(card);
                PlaceCard(card, slot);
                outgoing.Add(card);
                outgoingSlots.Add(slot.SlotIndex);
            }

            foreach (var slotIndex in selected)
            {
                if (outgoingSlots.Contains(slotIndex))
                    continue;

                var card = hand.GetCard(slotIndex);
                if (card == null)
                    continue;

                var slot = SnapshotFromCard(action.SourcePlayerId, slotIndex, card);
                card = hand.DetachCard(slotIndex);
                if (card == null)
                    continue;
                TrackTransient(card);
                PlaceCard(card, slot);
                outgoing.Add(card);
            }

            if (outgoing.Count == 0)
            {
                _animatingPlayers.Remove(action.SourcePlayerId);
                Reconcile();
                yield break;
            }

            yield return new WaitForSecondsRealtime(EmptyOriginHold);
            for (int i = 0; i < outgoing.Count; i++)
            {
                var card = outgoing[i];
                card.MoveTo(discardPosition, MoveDuration);
                if (i < outgoing.Count - 1)
                    yield return new WaitForSecondsRealtime(DiscardStagger);
            }

            yield return new WaitForSecondsRealtime(MoveDuration + EmptyHold);
            for (int i = 0; i < outgoing.Count; i++)
                if (outgoing[i] != null)
                    DestroyCard(outgoing[i]);

            var survivorCards = action.SelectedSlots.Count > 1
                ? DetachSurvivors(hand, action, selected)
                : new List<CardView>();
            var incoming = CreateIncomingCard(action, fromDiscard);
            if (fromDiscard)
                incoming.RectTransform.anchoredPosition = discardPosition;
            else
            {
                incoming.RectTransform.anchoredPosition = GetDrawnMarkerPosition(action.SourcePlayerId,
                    action.TargetInspectionPosition != Vector2.zero
                        ? action.TargetInspectionPosition
                        : (action.SourceInspectionPosition != Vector2.zero ? action.SourceInspectionPosition : action.SourcePlayerPosition));
                RemoveDrawnMarker(action.SourcePlayerId);
            }

            var incomingFinal = FindIncomingFinalSlot(action, survivorCards.Count);
            if (incomingFinal.IsValid)
            {
                incoming.SetSize(incomingFinal.Size);
                if (action.SelectedSlots.Count > 1)
                    StartSurvivorMoves(hand, action, survivorCards);
                yield return incoming.MoveTo(incomingFinal.AnchoredPosition, MoveDuration);
                hand.AttachCard(incomingFinal.SlotIndex, incoming);
                UntrackTransient(incoming);
            }
            else
            {
                if (action.SelectedSlots.Count > 1)
                    StartSurvivorMoves(hand, action, survivorCards);
                yield return incoming.MoveTo(action.SourcePlayerPosition, MoveDuration);
                DestroyCard(incoming);
            }

            for (int i = 0; i < survivorCards.Count; i++)
                UntrackTransient(survivorCards[i]);
            yield return new WaitForSecondsRealtime(IncomingLandingPause);
            _animatingPlayers.Remove(action.SourcePlayerId);
            Reconcile();
        }

        void BeginSwap(CardTableActionSnapshot action)
        {
            if (!HasSwapPlan(action))
            {
                Reconcile();
                return;
            }

            StartCoroutine(PlaySwap(action));
        }

        bool HasSwapPlan(CardTableActionSnapshot action)
        {
            if (action == null || action.SourcePlayerId <= 0 || action.TargetPlayerId <= 0 || action.SourceSlot < 0 || action.TargetSlot < 0)
                return false;

            var sourceHand = GetHand(action.SourcePlayerId);
            var targetHand = GetHand(action.TargetPlayerId);
            if (sourceHand == null || targetHand == null)
                return false;

            return FindSnapshot(action.SourceSwapSlots, action.SourceSlot).IsValid
                && FindSnapshot(action.TargetSlots, action.TargetSlot).IsValid
                && (sourceHand.GetCard(action.SourceSlot) != null || action.SourceSwapSlots.Count > 0)
                && (targetHand.GetCard(action.TargetSlot) != null || action.TargetSlots.Count > 0);
        }

        IEnumerator PlaySwap(CardTableActionSnapshot action)
        {
            _animatingPlayers.Add(action.SourcePlayerId);
            _animatingPlayers.Add(action.TargetPlayerId);

            var sourceHand = GetHand(action.SourcePlayerId);
            var targetHand = GetHand(action.TargetPlayerId);
            if (sourceHand == null || targetHand == null)
            {
                _animatingPlayers.Remove(action.SourcePlayerId);
                _animatingPlayers.Remove(action.TargetPlayerId);
                Reconcile();
                yield break;
            }

            var sourceSlot = FindSnapshot(action.SourceSwapSlots, action.SourceSlot);
            var targetSlot = FindSnapshot(action.TargetSlots, action.TargetSlot);
            var sourceCard = sourceHand.DetachCard(action.SourceSlot) ?? CreateSnapshotCard(sourceSlot);
            var targetCard = targetHand.DetachCard(action.TargetSlot) ?? CreateSnapshotCard(targetSlot);
            TrackTransient(sourceCard);
            TrackTransient(targetCard);
            PlaceCard(sourceCard, sourceSlot);
            PlaceCard(targetCard, targetSlot);

            sourceCard.ShowBack();
            targetCard.ShowBack();
            sourceCard.MoveTo(targetSlot.AnchoredPosition, SwapDuration);
            yield return targetCard.MoveTo(sourceSlot.AnchoredPosition, SwapDuration);

            targetHand.AttachCard(action.TargetSlot, sourceCard);
            sourceHand.AttachCard(action.SourceSlot, targetCard);
            UntrackTransient(sourceCard);
            UntrackTransient(targetCard);
            _animatingPlayers.Remove(action.SourcePlayerId);
            _animatingPlayers.Remove(action.TargetPlayerId);
            Reconcile();
        }

        IEnumerator PlayPeekSelf(CardTableActionSnapshot action)
        {
            long playerId = action.SourcePlayerId;
            int slotIndex = action.SourceSlot >= 0 ? action.SourceSlot : (action.SelectedSlots.Count > 0 ? action.SelectedSlots[0] : -1);
            if (slotIndex < 0)
            {
                Reconcile();
                yield break;
            }

            var hand = GetHand(playerId);
            if (hand == null)
            {
                Reconcile();
                yield break;
            }

            var slot = hand.GetSlot(slotIndex);
            var card = hand.DetachCard(slotIndex);
            if (card == null)
            {
                Reconcile();
                yield break;
            }

            TrackTransient(card);
            var start = slot != null ? slot.RectTransform.anchoredPosition : card.RectTransform.anchoredPosition;
            var size = slot != null ? slot.RectTransform.sizeDelta : card.RectTransform.sizeDelta;
            card.SetSize(size);
            card.RectTransform.anchoredPosition = start;
            card.ShowBack();
            card.SetVisible(true);

            Vector2 inspect = action.SourceInspectionPosition != Vector2.zero
                ? action.SourceInspectionPosition
                : start + new Vector2(0f, Mathf.Max(42f, size.y * 0.55f));

            yield return card.MoveTo(inspect, MoveDuration);
            if (playerId == _myPlayerId && action.PeekedValue >= 0)
                yield return card.FlipToFront(action.PeekedValue, 0.18f);
            else
                card.ShowBack();

            yield return new WaitForSecondsRealtime(InspectHoldDuration);
            yield return card.MoveTo(start, MoveDuration);
            hand.AttachCard(slotIndex, card);
            UntrackTransient(card);
            Reconcile();
        }

        IEnumerator PlaySpy(CardTableActionSnapshot action)
        {
            long sourcePlayerId = action.SourcePlayerId;
            long targetPlayerId = action.TargetPlayerId;
            int slotIndex = action.TargetSlot >= 0 ? action.TargetSlot : (action.SelectedSlots.Count > 0 ? action.SelectedSlots[0] : -1);
            if (slotIndex < 0)
            {
                Reconcile();
                yield break;
            }

            var targetHand = GetHand(targetPlayerId);
            if (targetHand == null)
            {
                Reconcile();
                yield break;
            }

            var slot = targetHand.GetSlot(slotIndex);
            var card = targetHand.DetachCard(slotIndex);
            if (card == null)
            {
                Reconcile();
                yield break;
            }

            TrackTransient(card);
            var start = slot != null ? slot.RectTransform.anchoredPosition : card.RectTransform.anchoredPosition;
            var size = slot != null ? slot.RectTransform.sizeDelta : card.RectTransform.sizeDelta;
            card.SetSize(size);
            card.RectTransform.anchoredPosition = start;
            card.ShowBack();
            card.SetVisible(true);

            Vector2 inspect = action.SourceInspectionPosition != Vector2.zero
                ? action.SourceInspectionPosition
                : action.SourcePlayerPosition != Vector2.zero
                    ? action.SourcePlayerPosition
                    : start + new Vector2(0f, Mathf.Max(42f, size.y * 0.55f));

            yield return card.MoveTo(inspect, MoveDuration);
            if (sourcePlayerId == _myPlayerId && action.PeekedValue >= 0)
                yield return card.FlipToFront(action.PeekedValue, 0.18f);
            else
                card.ShowBack();

            yield return new WaitForSecondsRealtime(InspectHoldDuration);
            yield return card.MoveTo(start, MoveDuration);
            targetHand.AttachCard(slotIndex, card);
            UntrackTransient(card);
            Reconcile();
        }

        IEnumerator ShakeSourceSlots(CardTableActionSnapshot action)
        {
            float elapsed = 0f;
            var cards = new List<CardView>();
            var starts = new List<Vector2>();
            for (int i = 0; i < action.SourceSlots.Count; i++)
            {
                var slot = action.SourceSlots[i];
                var hand = GetHand(slot.PlayerId);
                var card = hand?.GetCard(slot.SlotIndex);
                if (card != null)
                {
                    cards.Add(card);
                    starts.Add(card.RectTransform.anchoredPosition);
                }
            }

            while (elapsed < 0.35f)
            {
                elapsed += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(elapsed * 58f) * 6f;
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null)
                        cards[i].RectTransform.anchoredPosition = starts[i] + new Vector2(offset, 0f);
                yield return null;
            }

            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null)
                    cards[i].RectTransform.anchoredPosition = starts[i];
        }

        List<CardView> DetachSurvivors(HandView hand, CardTableActionSnapshot action, HashSet<int> selected)
        {
            var result = new List<CardView>();
            for (int i = 0; i < action.SourceHand.Count; i++)
            {
                var source = action.SourceHand[i];
                if (selected.Contains(source.SlotIndex))
                    continue;

                var card = hand.DetachCard(source.SlotIndex) ?? CreateSnapshotCard(source);
                TrackTransient(card);
                PlaceCard(card, source);
                result.Add(card);
            }
            return result;
        }

        void StartSurvivorMoves(HandView hand, CardTableActionSnapshot action, List<CardView> survivors)
        {
            int index = 0;
            for (int i = 0; i < action.FinalSourceHand.Count && index < survivors.Count; i++)
            {
                var finalSlot = action.FinalSourceHand[i];
                var card = survivors[index++];
                card.SetSize(finalSlot.Size);
                card.MoveTo(finalSlot.AnchoredPosition, MoveDuration);
                hand.AttachCard(finalSlot.SlotIndex, card, false);
            }
        }

        CardTableSlotSnapshot FindIncomingFinalSlot(CardTableActionSnapshot action, int survivorCount)
        {
            if (action.SelectedSlots.Count <= 1)
            {
                int targetSlot = action.SelectedSlots.Count > 0 ? action.SelectedSlots[0] : action.SourceSlot;
                var snapshot = FindSnapshot(action.FinalSourceHand, targetSlot);
                if (snapshot.IsValid)
                    return snapshot;
                return FindSnapshot(action.SourceSlots, targetSlot);
            }

            if (survivorCount >= 0 && survivorCount < action.FinalSourceHand.Count)
                return action.FinalSourceHand[survivorCount];
            return default;
        }

        static CardTableSlotSnapshot SnapshotFromCard(long playerId, int slotIndex, CardView card)
        {
            if (card == null)
                return default;

            return new CardTableSlotSnapshot
            {
                PlayerId = playerId,
                SlotIndex = slotIndex,
                AnchoredPosition = card.RectTransform.anchoredPosition,
                Size = card.RectTransform.sizeDelta,
                FaceUp = card.FaceUp,
                Value = card.Value
            };
        }

        CardView CreateIncomingCard(CardTableActionSnapshot action, bool fromDiscard)
        {
            var card = CardView.Create(_cardRoot, $"Incoming_{action.Sequence}");
            TrackTransient(card);
            var size = action.DiscardPileSize.x > 1f ? action.DiscardPileSize : action.DrawPileSize;
            card.SetSize(size);
            int displayValue = action.IncomingCardValue >= 0 ? action.IncomingCardValue : (fromDiscard ? action.DiscardTopValue : -1);
            bool reveal = displayValue >= 0 && (fromDiscard || action.SourcePlayerId == _myPlayerId);
            if (reveal)
                card.ShowFront(displayValue);
            else
                card.ShowBack();
            PrepareMovingCard(card);
            return card;
        }

        CardView CreateSnapshotCard(CardTableSlotSnapshot snapshot)
        {
            var card = CardView.Create(_cardRoot, $"Snapshot_{snapshot.PlayerId}_{snapshot.SlotIndex}");
            TrackTransient(card);
            PlaceCard(card, snapshot);
            if (snapshot.FaceUp)
                card.ShowFront(snapshot.Value);
            else
                card.ShowBack();
            PrepareMovingCard(card);
            return card;
        }

        CardView GetOrCreateDrawnMarker(long playerId, Vector2 size)
        {
            if (_drawnMarkers.TryGetValue(playerId, out var marker) && marker != null)
                return marker;

            marker = CardView.Create(_cardRoot, $"Drawn_{playerId}");
            TrackTransient(marker);
            marker.SetSize(size.x > 1f ? size : new Vector2(56f, 78f));
            marker.ShowBack();
            PrepareMovingCard(marker);
            _drawnMarkers[playerId] = marker;
            return marker;
        }

        Vector2 GetDrawnMarkerPosition(long playerId, Vector2 fallback)
        {
            if (_drawnMarkers.TryGetValue(playerId, out var marker) && marker != null)
                return marker.RectTransform.anchoredPosition;
            return fallback;
        }

        void RemoveDrawnMarker(long playerId)
        {
            if (!_drawnMarkers.TryGetValue(playerId, out var marker))
                return;
            if (marker != null)
                DestroyCard(marker);
            _drawnMarkers.Remove(playerId);
        }

        void PlaceCard(CardView card, CardTableSlotSnapshot snapshot)
        {
            if (card == null || !snapshot.IsValid)
                return;

            card.RectTransform.anchoredPosition = snapshot.AnchoredPosition;
            card.SetSize(snapshot.Size);
            if (snapshot.FaceUp)
                card.ShowFront(snapshot.Value);
            else
                card.ShowBack();
            PrepareMovingCard(card);
        }

        void PrepareMovingCard(CardView card)
        {
            if (card == null)
                return;

            card.CancelAnimations();
            card.SetSelected(false);
            card.SetInteraction(false, null);
            card.transform.SetAsLastSibling();
        }

        void TrackTransient(CardView card)
        {
            if (card == null || _transientCards.Contains(card))
                return;

            _transientCards.Add(card);
        }

        void UntrackTransient(CardView card)
        {
            if (card == null)
                return;

            _transientCards.Remove(card);
        }

        void DestroyTransientCards()
        {
            var cards = new List<CardView>(_transientCards);
            for (int i = cards.Count - 1; i >= 0; i--)
                DestroyCard(cards[i]);
            _transientCards.Clear();
        }

        void DestroyCard(CardView card)
        {
            if (card == null)
                return;

            UntrackTransient(card);
            card.CancelAnimations();
            if (Application.isPlaying)
                Destroy(card.gameObject);
            else
                DestroyImmediate(card.gameObject);
        }

        Vector2 GetDiscardPilePosition(CardTableActionSnapshot action)
        {
            if (action != null && HasUsablePile(action.DiscardPilePosition, action.DiscardPileSize))
                return action.DiscardPilePosition;
            if (_lastLayout != null && HasUsablePile(_lastLayout.DiscardPilePosition, _lastLayout.DiscardPileSize))
                return _lastLayout.DiscardPilePosition;
            return Vector2.zero;
        }

        static bool HasUsablePile(Vector2 position, Vector2 size)
        {
            return size.x > 1f
                && size.y > 1f
                && IsFinite(position.x)
                && IsFinite(position.y)
                && IsFinite(size.x)
                && IsFinite(size.y);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        CardTableSlotSnapshot FindSnapshot(List<CardTableSlotSnapshot> snapshots, int slot)
        {
            for (int i = 0; i < snapshots.Count; i++)
                if (snapshots[i].SlotIndex == slot)
                    return snapshots[i];
            return default;
        }

        HandView GetHand(long playerId)
        {
            _hands.TryGetValue(playerId, out var hand);
            return hand;
        }

        void Reconcile()
        {
            if (_lastState != null && _lastLayout != null)
                Render(_lastState, _lastLayout);
        }
    }
}
