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
        const float TakeDiscardOutgoingDelay = 0.08f;
        const int DrawPileVisualDepth = 3;
        const int MaxDiscardStackOffsetDepth = 3;
        const float PileStackOffsetX = 1.0f;
        const float PileStackOffsetY = 0.85f;

        readonly Dictionary<long, HandView> _hands = new();
        readonly Dictionary<long, CardView> _drawnMarkers = new();
        readonly List<CardView> _transientCards = new();
        readonly List<CardView> _drawPileBackCards = new();
        readonly List<DiscardStackEntry> _discardStack = new();
        readonly HashSet<long> _animatingPlayers = new();

        RectTransform _root;
        RectTransform _slotRoot;
        RectTransform _cardRoot;
        CardSlotView _drawPileSlot;
        CardSlotView _discardPileSlot;
        CardView _drawPileCard;
        Text _drawPileCaption;
        Text _discardPileCaption;
        GameState _lastState;
        CardTableLayout _lastLayout;
        long _myPlayerId;
        Rect _lastRootBounds;
        int _lastScreenWidth;
        int _lastScreenHeight;
        bool _layoutRefreshQueued;
        bool _lastFreezePiles;

        public bool HasRenderableLayout { get; private set; }
        public bool HasActiveTransientAnimation => _animatingPlayers.Count > 0;

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
            for (int i = 0; i < DrawPileVisualDepth - 1; i++)
                _drawPileBackCards.Add(CardView.Create(_cardRoot, $"DrawPileBack_{i}"));
            _drawPileCaption = CreateCaption("DrawPileCaption");
            _discardPileCaption = CreateCaption("DiscardPileCaption");
            _drawPileCard.ShowBack();
            for (int i = 0; i < _drawPileBackCards.Count; i++)
            {
                _drawPileBackCards[i].ShowBack();
                _drawPileBackCards[i].SetVisible(false);
            }
            _lastRootBounds = _root.rect;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
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
            if (visible)
                ScheduleLayoutRefresh();
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
            _lastFreezePiles = false;
            SetDrawPileStackVisible(false);
            if (_lastState != null && _lastLayout != null)
                Render(_lastState, _lastLayout);
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (HasLayoutChanged())
                ScheduleLayoutRefresh();
        }

        public void Render(GameState state, CardTableLayout layout, long frozenPlayerId = 0, long secondFrozenPlayerId = 0, bool freezePiles = false)
        {
            if (state == null || layout == null)
                return;

            _lastState = state;
            _lastLayout = layout;
            _myPlayerId = state.MyPlayerId;
            _lastRootBounds = _root.rect;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            HasRenderableLayout = layout.Slots.Count > 0;
            _lastFreezePiles = freezePiles;

            if (!freezePiles)
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

        public bool RefreshInteractions(GameState state, CardTableLayout layout, bool freezePiles = false)
        {
            if (state == null || layout == null || !HasRenderableLayout)
                return false;

            _lastState = state;
            _lastLayout = layout;
            _myPlayerId = state.MyPlayerId;
            _lastRootBounds = _root.rect;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastFreezePiles = freezePiles;

            if (!freezePiles)
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

        bool HasLayoutChanged()
        {
            if (_root == null)
                return false;

            return _lastRootBounds != _root.rect
                || _lastScreenWidth != Screen.width
                || _lastScreenHeight != Screen.height;
        }

        void ScheduleLayoutRefresh()
        {
            if (_layoutRefreshQueued || _lastState == null || _lastLayout == null)
                return;

            _layoutRefreshQueued = true;
            StartCoroutine(LayoutRefreshRoutine());
        }

        IEnumerator LayoutRefreshRoutine()
        {
            yield return null;
            _layoutRefreshQueued = false;
            if (_lastState == null || _lastLayout == null || !gameObject.activeInHierarchy)
                yield break;

            _lastRootBounds = _root.rect;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            Render(_lastState, _lastLayout, 0, 0, HasActiveTransientAnimation || _lastFreezePiles);
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
                RenderDrawPileStack(state, layout);
                PositionCaption(_drawPileCaption, layout.DrawPileCaption, layout.DrawPilePosition, layout.DrawPileSize);
            }
            else
            {
                SetDrawPileStackVisible(false);
                _drawPileCaption.gameObject.SetActive(false);
            }

            if (layout.DiscardPileSize.x > 1f && layout.DiscardPileSize.y > 1f)
            {
                _discardPileSlot.Configure(0, 1, layout.DiscardPilePosition, layout.DiscardPileSize);
                RenderDiscardStack(state, layout);
                PositionCaption(_discardPileCaption, layout.DiscardPileCaption, layout.DiscardPilePosition, layout.DiscardPileSize);
            }
            else
            {
                SetDiscardStackVisible(false);
                _discardPileCaption.gameObject.SetActive(false);
            }
        }

        void RenderDrawPileStack(GameState state, CardTableLayout layout)
        {
            int visibleCount = Mathf.Clamp(state.DrawPileCount, 0, DrawPileVisualDepth);
            if (visibleCount <= 0)
            {
                SetDrawPileStackVisible(false);
                return;
            }

            int backCount = Mathf.Min(_drawPileBackCards.Count, visibleCount - 1);
            for (int depth = backCount; depth >= 1; depth--)
            {
                var card = _drawPileBackCards[depth - 1];
                if (card == null)
                    continue;

                card.RectTransform.anchoredPosition = layout.DrawPilePosition + GetPileStackOffset(depth);
                card.SetSize(layout.DrawPileSize);
                card.ShowBack();
                card.SetInteraction(false, null);
                card.SetVisible(true);
                card.transform.SetAsLastSibling();
            }

            for (int i = backCount; i < _drawPileBackCards.Count; i++)
                if (_drawPileBackCards[i] != null)
                    _drawPileBackCards[i].SetVisible(false);

            _drawPileCard.RectTransform.anchoredPosition = layout.DrawPilePosition;
            _drawPileCard.SetSize(layout.DrawPileSize);
            _drawPileCard.ShowBack();
            _drawPileCard.SetInteraction(false, null);
            _drawPileCard.SetVisible(true);
            _drawPileCard.transform.SetAsLastSibling();
        }

        void SetDrawPileStackVisible(bool visible)
        {
            if (_drawPileCard != null)
                _drawPileCard.SetVisible(visible);
            for (int i = 0; i < _drawPileBackCards.Count; i++)
            {
                var card = _drawPileBackCards[i];
                if (card != null)
                    card.SetVisible(visible);
            }
        }

        void RenderDiscardStack(GameState state, CardTableLayout layout)
        {
            int count = Mathf.Max(0, state.DiscardPileCount);
            if (count == 0)
            {
                ClearDiscardStack();
                return;
            }

            while (_discardStack.Count > count)
                RemoveDiscardStackEntryAt(0);
            if (_discardStack.Count == 0)
                _discardStack.Add(CreateDiscardStackEntry(state.DiscardTopValue, state.DiscardTopValue >= 0));

            if (_discardStack.Count > 0)
            {
                var top = _discardStack[_discardStack.Count - 1];
                top.Value = state.DiscardTopValue;
                top.Known = state.DiscardTopValue >= 0;
            }
            LayoutDiscardStack(layout.DiscardPilePosition, layout.DiscardPileSize);
        }

        void LayoutDiscardStack(Vector2 pilePosition, Vector2 pileSize)
        {
            for (int i = 0; i < _discardStack.Count; i++)
            {
                var entry = _discardStack[i];
                if (entry?.Card == null)
                    continue;

                int depthFromTop = _discardStack.Count - 1 - i;
                int visibleDepth = Mathf.Min(depthFromTop, MaxDiscardStackOffsetDepth);
                entry.Card.RectTransform.anchoredPosition = pilePosition + GetPileStackOffset(visibleDepth);
                entry.Card.SetSize(pileSize);
                ApplyDiscardStackFace(entry);
                entry.Card.SetInteraction(false, null);
                entry.Card.SetVisible(true);
                entry.Card.transform.SetAsLastSibling();
            }
        }

        static Vector2 GetPileStackOffset(int depthFromTop)
        {
            if (depthFromTop <= 0)
                return Vector2.zero;

            return new Vector2(-depthFromTop * PileStackOffsetX, depthFromTop * PileStackOffsetY);
        }

        void ApplyDiscardStackFace(DiscardStackEntry entry)
        {
            if (entry == null || entry.Card == null)
                return;

            if (entry.Known && entry.Value >= 0)
                entry.Card.ShowFront(entry.Value, false);
            else
                entry.Card.ShowBack();
        }

        void SetDiscardStackVisible(bool visible)
        {
            for (int i = 0; i < _discardStack.Count; i++)
            {
                var card = _discardStack[i]?.Card;
                if (card != null)
                    card.SetVisible(visible);
            }
        }

        DiscardStackEntry CreateDiscardStackEntry(int value, bool known)
        {
            var card = CardView.Create(_cardRoot, $"DiscardStack_{_discardStack.Count}");
            card.SetInteraction(false, null);
            return new DiscardStackEntry
            {
                Card = card,
                Value = value,
                Known = known && value >= 0
            };
        }

        void ClearDiscardStack()
        {
            for (int i = _discardStack.Count - 1; i >= 0; i--)
            {
                var card = _discardStack[i]?.Card;
                if (card != null)
                    DestroyCard(card);
            }
            _discardStack.Clear();
        }

        void RemoveDiscardStackEntryAt(int index)
        {
            if (index < 0 || index >= _discardStack.Count)
                return;

            var card = _discardStack[index]?.Card;
            if (card != null)
                DestroyCard(card);
            _discardStack.RemoveAt(index);
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
            _animatingPlayers.Add(action.SourcePlayerId);
            bool hadMarker = _drawnMarkers.ContainsKey(action.SourcePlayerId);
            var marker = GetOrCreateDrawnMarker(action.SourcePlayerId, action.DrawPileSize);
            if (!hadMarker)
                marker.RectTransform.anchoredPosition = action.TargetInspectionPosition != Vector2.zero
                    ? action.TargetInspectionPosition
                    : (action.SourceInspectionPosition != Vector2.zero ? action.SourceInspectionPosition : action.SourcePlayerPosition);
            bool revealDuringFlight = action.SourcePlayerId == _myPlayerId && action.DiscardTopValue >= 0;
            if (revealDuringFlight)
                marker.ShowFront(action.DiscardTopValue);
            else
                marker.ShowBack();

            marker.transform.SetAsLastSibling();
            yield return MoveCardOntoDiscardStack(action, marker, 0, 1,
                action.DiscardPilePosition, GetDiscardPileSize(action), false);
            _drawnMarkers.Remove(action.SourcePlayerId);
            _animatingPlayers.Remove(action.SourcePlayerId);
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

                var card = CreateSnapshotCard(slot);
                var liveCard = hand.DetachCard(slot.SlotIndex);
                if (liveCard != null)
                    DestroyCard(liveCard);
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

            if (fromDiscard)
                yield return PlayTakeFromDiscardExchange(action, hand, selected, outgoing, discardPosition);
            else
                yield return PlayDrawnCardExchange(action, hand, selected, outgoing, discardPosition);

            yield return new WaitForSecondsRealtime(IncomingLandingPause);
            _animatingPlayers.Remove(action.SourcePlayerId);
            Reconcile();
        }

        IEnumerator PlayDrawnCardExchange(CardTableActionSnapshot action, HandView hand, HashSet<int> selected, List<CardView> outgoing, Vector2 discardPosition)
        {
            yield return MoveOutgoingCardsToDiscardStack(action, outgoing, discardPosition, GetDiscardPileSize(action), EmptyOriginHold, true);

            var survivorCards = action.SelectedSlots.Count > 1
                ? DetachSurvivors(hand, action, selected)
                : new List<CardView>();
            var incoming = CreateIncomingCard(action, false);
            incoming.RectTransform.anchoredPosition = GetDrawnMarkerPosition(action.SourcePlayerId,
                action.TargetInspectionPosition != Vector2.zero
                    ? action.TargetInspectionPosition
                    : (action.SourceInspectionPosition != Vector2.zero ? action.SourceInspectionPosition : action.SourcePlayerPosition));
            RemoveDrawnMarker(action.SourcePlayerId);

            yield return MoveIncomingCardToHand(action, hand, incoming, survivorCards);
            for (int i = 0; i < survivorCards.Count; i++)
                UntrackTransient(survivorCards[i]);
        }

        IEnumerator PlayTakeFromDiscardExchange(CardTableActionSnapshot action, HandView hand, HashSet<int> selected, List<CardView> outgoing, Vector2 discardPosition)
        {
            var incoming = PopDiscardTopCard(action);
            var survivorCards = action.SelectedSlots.Count > 1
                ? DetachSurvivors(hand, action, selected)
                : new List<CardView>();

            var outgoingRoutine = StartCoroutine(MoveOutgoingCardsToDiscardStack(action, outgoing, discardPosition, GetDiscardPileSize(action),
                TakeDiscardOutgoingDelay, false));
            yield return MoveIncomingCardToHand(action, hand, incoming, survivorCards);
            yield return outgoingRoutine;

            for (int i = 0; i < survivorCards.Count; i++)
                UntrackTransient(survivorCards[i]);
        }

        IEnumerator MoveIncomingCardToHand(CardTableActionSnapshot action, HandView hand, CardView incoming, List<CardView> survivorCards)
        {
            if (incoming == null)
                yield break;

            int incomingInsertIndex = GetIncomingInsertIndex(action);
            var incomingFinal = FindIncomingFinalSlot(action, incomingInsertIndex);
            if (incomingFinal.IsValid)
            {
                incoming.SetSize(incomingFinal.Size);
                if (action.SelectedSlots.Count > 1)
                    StartSurvivorMoves(hand, action, survivorCards, incomingInsertIndex);
                yield return incoming.MoveTo(incomingFinal.AnchoredPosition, MoveDuration);
                hand.AttachCard(incomingFinal.SlotIndex, incoming);
                UntrackTransient(incoming);
            }
            else
            {
                if (action.SelectedSlots.Count > 1)
                    StartSurvivorMoves(hand, action, survivorCards, incomingInsertIndex);
                yield return incoming.MoveTo(action.SourcePlayerPosition, MoveDuration);
                DestroyCard(incoming);
            }
        }

        IEnumerator MoveOutgoingCardsToDiscardStack(CardTableActionSnapshot action, List<CardView> outgoing, Vector2 discardPosition,
            Vector2 discardSize, float initialDelay, bool holdAfterLanding)
        {
            if (initialDelay > 0f)
                yield return new WaitForSecondsRealtime(initialDelay);

            var landingRoutines = new List<Coroutine>();
            for (int i = 0; i < outgoing.Count; i++)
            {
                var card = outgoing[i];
                if (card != null)
                    landingRoutines.Add(StartCoroutine(MoveCardOntoDiscardStack(action, card, i, outgoing.Count, discardPosition, discardSize, holdAfterLanding)));
                if (i < outgoing.Count - 1)
                    yield return new WaitForSecondsRealtime(DiscardStagger);
            }

            for (int i = 0; i < landingRoutines.Count; i++)
                if (landingRoutines[i] != null)
                    yield return landingRoutines[i];
        }

        IEnumerator MoveCardOntoDiscardStack(CardTableActionSnapshot action, CardView card, int index, int count,
            Vector2 discardPosition, Vector2 discardSize, bool holdAfterLanding)
        {
            if (card == null)
                yield break;

            bool known = TryGetOutgoingDiscardValue(action, card, index, count, out int value);
            card.transform.SetAsLastSibling();
            bool revealAtLanding = known && (!card.FaceUp || card.Value != value);
            if (!known)
                card.ShowBack();

            yield return MoveCardToPositionOnTop(card, discardPosition, MoveDuration);
            card.transform.SetAsLastSibling();
            if (revealAtLanding)
                card.ShowFront(value, false);
            PushDiscardCard(card, value, known, discardPosition, discardSize);

            if (holdAfterLanding)
                yield return new WaitForSecondsRealtime(EmptyHold);
        }

        static IEnumerator MoveCardToPositionOnTop(CardView card, Vector2 target, float duration)
        {
            if (card == null)
                yield break;

            var rect = card.RectTransform;
            var start = rect.anchoredPosition;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                t = t * t * (3f - 2f * t);
                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
                card.transform.SetAsLastSibling();
                yield return null;
            }

            rect.anchoredPosition = target;
            card.transform.SetAsLastSibling();
        }

        bool TryGetOutgoingDiscardValue(CardTableActionSnapshot action, CardView card, int index, int count, out int value)
        {
            if (index == count - 1 && action.DiscardTopValue >= 0)
            {
                value = action.DiscardTopValue;
                return true;
            }

            if (card != null && card.FaceUp)
            {
                value = card.Value;
                return true;
            }

            value = -1;
            return false;
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
            var sharedSize = GetActionDisplaySize(sourceSlot.Size, targetSlot.Size);
            if (sharedSize.x > 1f && sharedSize.y > 1f)
            {
                sourceCard.SetSize(sharedSize);
                targetCard.SetSize(sharedSize);
            }
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

            if (playerId == _myPlayerId)
            {
                _animatingPlayers.Add(playerId);
                var previewSlot = hand.GetSlot(slotIndex);
                var fallbackStart = previewSlot != null ? previewSlot.RectTransform.anchoredPosition : Vector2.zero;
                var fallbackSize = previewSlot != null ? previewSlot.RectTransform.sizeDelta : new Vector2(70f, 96f);
                var preview = CardView.Create(_cardRoot, $"PeekPreview_{action.Sequence}");
                TrackTransient(preview);
                preview.SetSize(fallbackSize);
                preview.RectTransform.anchoredPosition = fallbackStart;
                preview.ShowBack();
                preview.SetVisible(true);
                PrepareMovingCard(preview);

                if (action.PeekedValue >= 0)
                    yield return preview.FlipToFront(action.PeekedValue, 0.18f);
                else
                    yield return new WaitForSecondsRealtime(0.18f);

                yield return new WaitForSecondsRealtime(InspectHoldDuration);
                DestroyCard(preview);
                _animatingPlayers.Remove(playerId);
                Reconcile();
                yield break;
            }

            _animatingPlayers.Add(playerId);

            var slot = hand.GetSlot(slotIndex);
            var card = hand.DetachCard(slotIndex);
            if (card == null)
            {
                _animatingPlayers.Remove(playerId);
                Reconcile();
                yield break;
            }

            TrackTransient(card);
            var start = slot != null ? slot.RectTransform.anchoredPosition : card.RectTransform.anchoredPosition;
            var size = slot != null ? slot.RectTransform.sizeDelta : card.RectTransform.sizeDelta;
            size = GetInspectionDisplaySize(size);
            card.SetSize(size);
            card.RectTransform.anchoredPosition = start;
            card.ShowBack();
            card.SetVisible(true);

            Vector2 inspect = GetInspectionTarget(action, start, size);

            yield return card.MoveTo(inspect, MoveDuration);
            if (playerId == _myPlayerId && action.PeekedValue >= 0)
                yield return card.FlipToFront(action.PeekedValue, 0.18f);
            else
                card.ShowBack();

            yield return new WaitForSecondsRealtime(InspectHoldDuration);
            yield return card.MoveTo(start, MoveDuration);
            hand.AttachCard(slotIndex, card);
            UntrackTransient(card);
            _animatingPlayers.Remove(playerId);
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

            _animatingPlayers.Add(targetPlayerId);

            var slot = targetHand.GetSlot(slotIndex);
            var card = targetHand.DetachCard(slotIndex);
            if (card == null)
            {
                _animatingPlayers.Remove(targetPlayerId);
                Reconcile();
                yield break;
            }

            TrackTransient(card);
            var start = slot != null ? slot.RectTransform.anchoredPosition : card.RectTransform.anchoredPosition;
            var size = slot != null ? slot.RectTransform.sizeDelta : card.RectTransform.sizeDelta;
            size = GetInspectionDisplaySize(size);
            card.SetSize(size);
            card.RectTransform.anchoredPosition = start;
            card.ShowBack();
            card.SetVisible(true);

            Vector2 inspect = GetInspectionTarget(action, start, size);

            yield return card.MoveTo(inspect, MoveDuration);
            if (sourcePlayerId == _myPlayerId && action.PeekedValue >= 0)
                yield return card.FlipToFront(action.PeekedValue, 0.18f);
            else
                card.ShowBack();

            yield return new WaitForSecondsRealtime(InspectHoldDuration);
            yield return card.MoveTo(start, MoveDuration);
            targetHand.AttachCard(slotIndex, card);
            UntrackTransient(card);
            _animatingPlayers.Remove(targetPlayerId);
            Reconcile();
        }

        static Vector2 GetInspectionTarget(CardTableActionSnapshot action, Vector2 start, Vector2 size)
        {
            if (action != null)
            {
                if (action.SourceInspectionPosition != Vector2.zero)
                    return action.SourceInspectionPosition;
                if (action.SourcePlayerPosition != Vector2.zero)
                    return action.SourcePlayerPosition;
                if (action.TargetInspectionPosition != Vector2.zero)
                    return action.TargetInspectionPosition;
            }

            return start + new Vector2(0f, Mathf.Max(42f, size.y * 0.55f));
        }

        Vector2 GetInspectionDisplaySize(Vector2 fallback)
        {
            var reference = GetPreferredActionCardSize();
            if (reference.x > 1f && reference.y > 1f)
                return reference;

            return new Vector2(Mathf.Max(fallback.x, 56f), Mathf.Max(fallback.y, 78f));
        }

        Vector2 GetActionDisplaySize(Vector2 first, Vector2 second)
        {
            var reference = GetPreferredActionCardSize();
            float width = Mathf.Max(first.x, second.x);
            float height = Mathf.Max(first.y, second.y);
            if (reference.x > 1f && reference.y > 1f)
            {
                width = Mathf.Max(width, reference.x);
                height = Mathf.Max(height, reference.y);
            }

            return new Vector2(Mathf.Max(width, 56f), Mathf.Max(height, 78f));
        }

        Vector2 GetPreferredActionCardSize()
        {
            var hand = GetHand(_myPlayerId);
            if (hand != null)
            {
                foreach (var pair in hand.Cards)
                {
                    var card = pair.Value;
                    if (card != null && card.RectTransform != null)
                    {
                        var size = card.RectTransform.sizeDelta;
                        if (size.x > 1f && size.y > 1f)
                            return size;
                    }
                }
            }

            if (_lastLayout != null)
            {
                for (int i = 0; i < _lastLayout.Slots.Count; i++)
                {
                    var slot = _lastLayout.Slots[i];
                    if (slot.PlayerId == _myPlayerId && slot.Size.x > 1f && slot.Size.y > 1f)
                        return slot.Size;
                }
            }

            return Vector2.zero;
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

        void StartSurvivorMoves(HandView hand, CardTableActionSnapshot action, List<CardView> survivors, int incomingInsertIndex)
        {
            int index = 0;
            for (int i = 0; i < action.FinalSourceHand.Count && index < survivors.Count; i++)
            {
                if (i == incomingInsertIndex)
                    continue;

                var finalSlot = action.FinalSourceHand[i];
                var card = survivors[index++];
                card.SetSize(finalSlot.Size);
                card.MoveTo(finalSlot.AnchoredPosition, MoveDuration);
                hand.AttachCard(finalSlot.SlotIndex, card, false);
            }
        }

        CardTableSlotSnapshot FindIncomingFinalSlot(CardTableActionSnapshot action, int incomingInsertIndex)
        {
            if (action.SelectedSlots.Count <= 1)
            {
                int targetSlot = action.SelectedSlots.Count > 0 ? action.SelectedSlots[0] : action.SourceSlot;
                var snapshot = FindSnapshot(action.FinalSourceHand, targetSlot);
                if (snapshot.IsValid)
                    return snapshot;
                return FindSnapshot(action.SourceSlots, targetSlot);
            }

            if (incomingInsertIndex >= 0 && incomingInsertIndex < action.FinalSourceHand.Count)
                return action.FinalSourceHand[incomingInsertIndex];
            return default;
        }

        int GetIncomingInsertIndex(CardTableActionSnapshot action)
        {
            if (action == null || action.FinalSourceHand.Count == 0)
                return -1;

            if (action.SelectedSlots.Count <= 1)
            {
                int targetSlot = action.SelectedSlots.Count > 0 ? action.SelectedSlots[0] : action.SourceSlot;
                for (int i = 0; i < action.FinalSourceHand.Count; i++)
                    if (action.FinalSourceHand[i].SlotIndex == targetSlot)
                        return i;
                return Mathf.Clamp(targetSlot, 0, action.FinalSourceHand.Count - 1);
            }

            var selected = new HashSet<int>(action.SelectedSlots);
            int insertionIndex = 0;
            for (int i = 0; i < action.SourceHand.Count; i++)
            {
                if (selected.Contains(action.SourceHand[i].SlotIndex))
                    break;
                insertionIndex++;
            }

            return Mathf.Clamp(insertionIndex, 0, action.FinalSourceHand.Count - 1);
        }

        void PushDiscardCard(CardView card, int value, bool known, Vector2 discardPosition, Vector2 discardSize)
        {
            if (card == null)
                return;

            UntrackTransient(card);
            card.transform.SetParent(_cardRoot, false);
            card.CancelAnimations();
            card.RectTransform.localScale = Vector3.one;
            card.SetSelected(false);
            card.SetInteraction(false, null);

            var entry = new DiscardStackEntry
            {
                Card = card,
                Value = value,
                Known = known && value >= 0
            };
            _discardStack.Add(entry);
            LayoutDiscardStack(discardPosition, discardSize);
            BringTransientCardsToFront();
        }

        CardView PopDiscardTopCard(CardTableActionSnapshot action)
        {
            var discardPosition = GetDiscardPilePosition(action);
            var discardSize = GetDiscardPileSize(action);

            if (_discardStack.Count == 0)
            {
                var fallback = CreateIncomingCard(action, true);
                fallback.RectTransform.anchoredPosition = discardPosition;
                return fallback;
            }

            var topIndex = _discardStack.Count - 1;
            var entry = _discardStack[topIndex];
            _discardStack.RemoveAt(topIndex);
            var card = entry?.Card;
            LayoutDiscardStack(discardPosition, discardSize);

            if (card == null)
            {
                var fallback = CreateIncomingCard(action, true);
                fallback.RectTransform.anchoredPosition = discardPosition;
                return fallback;
            }

            int value = action.IncomingCardValue >= 0
                ? action.IncomingCardValue
                : (entry.Known ? entry.Value : -1);
            TrackTransient(card);
            card.transform.SetParent(_cardRoot, false);
            card.RectTransform.anchoredPosition = discardPosition;
            card.SetSize(discardSize);
            if (value >= 0)
                card.ShowFront(value);
            else
                card.ShowBack();
            PrepareMovingCard(card);
            return card;
        }

        void BringTransientCardsToFront()
        {
            for (int i = 0; i < _transientCards.Count; i++)
            {
                var card = _transientCards[i];
                if (card != null)
                    card.transform.SetAsLastSibling();
            }
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

        Vector2 GetDiscardPileSize(CardTableActionSnapshot action)
        {
            if (action != null && action.DiscardPileSize.x > 1f && action.DiscardPileSize.y > 1f)
                return action.DiscardPileSize;
            if (_lastLayout != null && _lastLayout.DiscardPileSize.x > 1f && _lastLayout.DiscardPileSize.y > 1f)
                return _lastLayout.DiscardPileSize;
            return new Vector2(70f, 96f);
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

        sealed class DiscardStackEntry
        {
            public CardView Card;
            public int Value = -1;
            public bool Known;
        }
    }
}
