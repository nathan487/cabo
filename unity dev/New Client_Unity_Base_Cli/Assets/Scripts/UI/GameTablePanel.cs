using System;
using System.Collections.Generic;
using Cabo.Client.UI.CardTable;
using Game.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Visual multiplayer card table for the active Cabo round.
    /// The panel only owns UI selection state; network/game state stays in GameFlow.
    /// </summary>
    public class GameTablePanel
    {
        const int TableCardWidth = 64;
        const int TableCardHeight = 88;
        const int SelfCardWidth = TableCardWidth;
        const int SelfCardHeight = TableCardHeight;
        const int OppCardWidth = TableCardWidth;
        const int OppCardHeight = TableCardHeight;
        const int GameplayActionPanelTopMargin = 150;
        const int GameplayPileTopOffset = 14;
        const float QuickMoveDuration = 0.70f;
        const float SkillMoveDuration = 0.80f;
        const float SwapMoveDuration = 0.90f;
        const float EmptySlotHoldDuration = 0.45f;
        const float SkillHoldDuration = 1.60f;
        const float FlipRevealDuration = 1.80f;
        const float CaboCallDuration = 1.20f;
        const float AnimationSettleDuration = 0.25f;
        const float EmptyOriginHoldDuration = 0.20f;
        const float SurvivorMoveDuration = 0.48f;
        const float IncomingLandingPause = 0.12f;
        const float SwapEmptyHoldDuration = 0.22f;
        const float SwapSettleDuration = 0.16f;
        const float PlaybackLayoutSettleDelay = 0.1f;
        const float SurvivorMoveStagger = 0f;
        const float TakeDiscardOutgoingDelay = 0.08f;

        enum EndGameModalKind
        {
            None,
            LocalConfirm,
            HostDecision,
            RejectedInfo
        }

        readonly VisualElement _root;
        readonly VisualElement _container;
        readonly GameFlow _flow;

        readonly SeatView _topSeat;
        readonly SeatView _leftSeat;
        readonly SeatView _rightSeat;
        readonly SeatView _selfSeat;
        readonly SeatView[] _opponentSeats;

        readonly VisualElement _centerTable;
        readonly Label _roundLabel;
        readonly Label _turnLabel;
        readonly VisualElement _pileRow;
        readonly VisualElement _drawPile;
        readonly VisualElement _discardPile;
        readonly VisualElement _inspectionZone;
        readonly VisualElement _actionPanel;
        readonly Label _actionTitle;
        readonly Label _actionBody;
        readonly VisualElement _drawnCardSlot;
        readonly VisualElement _buttonRow;
        readonly List<Button> _actionButtons = new();
        readonly Label _statusLine;
        readonly Button _endGameButton;
        readonly VisualElement _endGameModalOverlay;
        readonly VisualElement _endGameModalCard;
        readonly Label _endGameModalTitle;
        readonly Label _endGameModalBody;
        readonly VisualElement _endGameModalButtons;
        readonly VisualElement _playArea;
        readonly VisualElement _socialPanel;
        readonly VisualElement _socialContent;
        readonly RoomChatPanel _roomChatPanel;
        readonly Button _logTabButton;
        readonly Button _chatTabButton;
        readonly VisualElement _animationLayer;
        readonly CardTableView _cardTableView;
        EventCallback<GeometryChangedEvent> _geometryChangedHandler;
        Action _animationQueueDrained;

        readonly HashSet<int> _selectedOwnSlots = new();
        readonly List<TableFeedEntry> _gameLogEntries = new();
        long _selectedOpponentPlayerId;
        int _selectedOpponentSlot = -1;
        long _lastLoggedActionSequence;
        GameSubState _lastSubState = GameSubState.Idle;
        GameSubState _lastRenderedSubState = GameSubState.Idle;
        GameSubState _lastActionPanelSubState = GameSubState.Idle;
        bool _lastActionPanelDeferredNewTurnActions;
        long _lastRenderedSocialActionSequence = -1;
        bool _lastRenderedSocialShowChat;
        long _lastAnimatedActionSequence;
        long _lastLocalDrawSequence;
        int _lastRenderedRoundNumber = -1;
        int _stableDiscardTopValue = int.MinValue;
        int _stableDiscardPileCount = -1;
        bool _discardVisualHoldActive;
        int _heldDiscardTopValue = int.MinValue;
        int _heldDiscardPileCount = -1;
        long _heldDiscardSequence;
        VisualElement _temporaryHiddenCard;
        readonly Dictionary<long, VisualElement> _drawnCardMarkers = new();
        readonly Dictionary<VisualElement, int> _pulseVersions = new();
        readonly Dictionary<VisualElement, float> _hiddenCardUntil = new();
        PendingSelfExchangeSnapshot _pendingSelfExchangeSnapshot;
        PendingSelfSwapSnapshot _pendingSelfSwapSnapshot;
        int _animationGeneration;
        float _animationQueueUntil;
        long _heldActionSequence;
        long _heldActionSourcePlayerId;
        float _heldActionUntil;
        bool _inspectionActive;
        bool _hideActionPanelDuringTransient;
        bool _showChat;
        bool _cardTableRefreshQueued;
        bool _layoutRefreshQueued;
        bool _uiActionQueued;
        bool _isVisible;
        Rect _lastRootBounds;
        int _lastScreenWidth;
        int _lastScreenHeight;
        float _inspectionEndsAt;
        Vector2 _inspectionReturnStart;
        Vector2 _inspectionReturnEnd;
        Color _inspectionReturnColor;
        bool _showLocalEndGameConfirm;

        public GameTablePanel(VisualElement root, GameFlow flow, Transform ownerTransform = null)
        {
            _root = root;
            _flow = flow;

            _container = new VisualElement { name = "CaboGameTable" };
            StretchToParent(_container);
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.position = Position.Relative;
            _container.style.paddingLeft = 22;
            _container.style.paddingRight = 22;
            _container.style.paddingTop = 16;
            _container.style.paddingBottom = 16;
            _container.style.backgroundColor = UITheme.AppBackground;
            root.Add(_container);

            _animationLayer = new VisualElement { name = "CardAnimationLayer" };
            _animationLayer.pickingMode = PickingMode.Ignore;
            _animationLayer.style.position = Position.Absolute;
            _animationLayer.style.left = 0;
            _animationLayer.style.right = 0;
            _animationLayer.style.top = 0;
            _animationLayer.style.bottom = 0;
            root.Add(_animationLayer);

            _endGameModalOverlay = new VisualElement { name = "EndGameModalOverlay" };
            StretchToParent(_endGameModalOverlay);
            _endGameModalOverlay.style.position = Position.Absolute;
            _endGameModalOverlay.style.left = 0;
            _endGameModalOverlay.style.right = 0;
            _endGameModalOverlay.style.top = 0;
            _endGameModalOverlay.style.bottom = 0;
            _endGameModalOverlay.style.display = DisplayStyle.None;
            _endGameModalOverlay.style.alignItems = Align.Center;
            _endGameModalOverlay.style.justifyContent = Justify.Center;
            _endGameModalOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            root.Add(_endGameModalOverlay);

            _endGameModalCard = new VisualElement { name = "EndGameModalCard" };
            _endGameModalCard.style.width = 360;
            _endGameModalCard.style.maxWidth = Length.Percent(88);
            _endGameModalCard.style.paddingLeft = 20;
            _endGameModalCard.style.paddingRight = 20;
            _endGameModalCard.style.paddingTop = 18;
            _endGameModalCard.style.paddingBottom = 18;
            _endGameModalCard.style.backgroundColor = UITheme.PanelSurface;
            _endGameModalCard.style.borderTopLeftRadius = 8;
            _endGameModalCard.style.borderTopRightRadius = 8;
            _endGameModalCard.style.borderBottomLeftRadius = 8;
            _endGameModalCard.style.borderBottomRightRadius = 8;
            SetBorderWidth(_endGameModalCard, 1);
            SetBorderColor(_endGameModalCard, UITheme.PanelBorder);
            _endGameModalOverlay.Add(_endGameModalCard);

            _endGameModalTitle = new Label();
            _endGameModalTitle.style.fontSize = 18;
            _endGameModalTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _endGameModalTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _endGameModalCard.Add(_endGameModalTitle);

            _endGameModalBody = new Label();
            _endGameModalBody.style.fontSize = 13;
            _endGameModalBody.style.whiteSpace = WhiteSpace.Normal;
            _endGameModalBody.style.unityTextAlign = TextAnchor.MiddleCenter;
            _endGameModalBody.style.marginTop = 8;
            _endGameModalBody.style.marginBottom = 14;
            _endGameModalCard.Add(_endGameModalBody);

            _endGameModalButtons = new VisualElement { name = "EndGameModalButtons" };
            _endGameModalButtons.style.flexDirection = FlexDirection.Row;
            _endGameModalButtons.style.justifyContent = Justify.Center;
            _endGameModalButtons.style.flexWrap = Wrap.Wrap;
            _endGameModalCard.Add(_endGameModalButtons);

            _cardTableView = CardTableView.Create(ownerTransform);
            _cardTableView.SetVisible(false);

            _geometryChangedHandler = _ => ScheduleLayoutRefresh();
            _root.RegisterCallback(_geometryChangedHandler);

            _topSeat = new SeatView("top", false);
            _leftSeat = new SeatView("left", false);
            _rightSeat = new SeatView("right", false);
            _selfSeat = new SeatView("self", true);
            _opponentSeats = new[] { _topSeat, _leftSeat, _rightSeat };

            _playArea = new VisualElement { name = "TablePlayArea" };
            _playArea.style.flexDirection = FlexDirection.Column;
            _playArea.style.flexGrow = 1;
            _playArea.style.flexShrink = 1;
            _playArea.style.minWidth = 0;
            _playArea.style.minHeight = 0;
            _playArea.style.height = Length.Percent(100);
            _playArea.style.position = Position.Relative;
            _container.Add(_playArea);

            _playArea.Add(_topSeat.Root);

            var middle = new VisualElement { name = "TableMiddle" };
            middle.style.flexGrow = 1;
            middle.style.flexShrink = 1;
            middle.style.minHeight = 0;
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.alignItems = Align.Stretch;
            middle.style.marginTop = 10;
            middle.style.marginBottom = 10;
            _playArea.Add(middle);

            middle.Add(_leftSeat.Root);

            _centerTable = new VisualElement { name = "CenterTable" };
            _centerTable.style.flexGrow = 1;
            _centerTable.style.flexShrink = 1;
            _centerTable.style.minWidth = 0;
            _centerTable.style.marginLeft = 16;
            _centerTable.style.marginRight = 16;
            _centerTable.style.alignItems = Align.Center;
            _centerTable.style.justifyContent = Justify.Center;
            _centerTable.style.backgroundColor = UITheme.TableSurface;
            _centerTable.style.borderTopLeftRadius = 22;
            _centerTable.style.borderTopRightRadius = 22;
            _centerTable.style.borderBottomLeftRadius = 22;
            _centerTable.style.borderBottomRightRadius = 22;
            _centerTable.style.borderTopWidth = 2;
            _centerTable.style.borderRightWidth = 2;
            _centerTable.style.borderBottomWidth = 2;
            _centerTable.style.borderLeftWidth = 2;
            _centerTable.style.borderTopColor = UITheme.TableBorder;
            _centerTable.style.borderRightColor = UITheme.TableBorder;
            _centerTable.style.borderBottomColor = UITheme.TableBorder;
            _centerTable.style.borderLeftColor = UITheme.TableBorder;
            _centerTable.style.position = Position.Relative;
            middle.Add(_centerTable);

            _roundLabel = new Label();
            _roundLabel.style.fontSize = 22;
            _roundLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _roundLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _roundLabel.style.marginBottom = 4;
            _centerTable.Add(_roundLabel);

            _turnLabel = new Label();
            _turnLabel.style.fontSize = 14;
            _turnLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _turnLabel.style.marginBottom = 10;
            _centerTable.Add(_turnLabel);

            _pileRow = new VisualElement();
            _pileRow.style.flexDirection = FlexDirection.Row;
            _pileRow.style.justifyContent = Justify.Center;
            _pileRow.style.alignItems = Align.Center;
            _pileRow.style.flexShrink = 0;
            _pileRow.style.marginTop = 2;
            _pileRow.style.marginBottom = 0;
            _centerTable.Add(_pileRow);

            _drawPile = new VisualElement();
            _discardPile = new VisualElement();
            _pileRow.Add(_drawPile);
            _pileRow.Add(_discardPile);

            _inspectionZone = new VisualElement { name = "InspectionZone" };
            _inspectionZone.style.width = Length.Percent(92);
            _inspectionZone.style.maxWidth = 640;
            _inspectionZone.style.minHeight = 118;
            _inspectionZone.style.marginBottom = 8;
            _inspectionZone.style.alignItems = Align.Center;
            _inspectionZone.style.justifyContent = Justify.Center;
            _inspectionZone.style.backgroundColor = UITheme.PanelSurface;
            _inspectionZone.style.borderTopLeftRadius = 8;
            _inspectionZone.style.borderTopRightRadius = 8;
            _inspectionZone.style.borderBottomLeftRadius = 8;
            _inspectionZone.style.borderBottomRightRadius = 8;
            _inspectionZone.style.borderTopWidth = 1;
            _inspectionZone.style.borderRightWidth = 1;
            _inspectionZone.style.borderBottomWidth = 1;
            _inspectionZone.style.borderLeftWidth = 1;
            SetBorderColor(_inspectionZone, UITheme.PanelBorder);
            _inspectionZone.style.display = DisplayStyle.None;
            _centerTable.Add(_inspectionZone);

            _drawnCardSlot = new VisualElement();
            _drawnCardSlot.style.alignItems = Align.Center;
            _drawnCardSlot.style.flexShrink = 0;
            _drawnCardSlot.style.marginTop = 2;
            _drawnCardSlot.style.marginBottom = 6;

            _actionPanel = new VisualElement();
            _actionPanel.style.width = Length.Percent(92);
            _actionPanel.style.maxWidth = 640;
            _actionPanel.style.paddingLeft = 16;
            _actionPanel.style.paddingRight = 16;
            _actionPanel.style.paddingTop = 8;
            _actionPanel.style.paddingBottom = 8;
            _actionPanel.style.flexShrink = 0;
            _actionPanel.style.marginTop = GameplayActionPanelTopMargin;
            _actionPanel.style.backgroundColor = UITheme.PanelSurface;
            _actionPanel.style.borderTopLeftRadius = 8;
            _actionPanel.style.borderTopRightRadius = 8;
            _actionPanel.style.borderBottomLeftRadius = 8;
            _actionPanel.style.borderBottomRightRadius = 8;
            _actionPanel.style.display = DisplayStyle.None;
            _centerTable.Add(_actionPanel);

            _actionTitle = new Label();
            _actionTitle.style.fontSize = 16;
            _actionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _actionTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionTitle.style.display = DisplayStyle.None;
            _actionPanel.Add(_actionTitle);

            _actionBody = new Label();
            _actionBody.style.fontSize = 12;
            _actionBody.style.whiteSpace = WhiteSpace.Normal;
            _actionBody.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionBody.style.marginTop = 4;
            _actionBody.style.marginBottom = 6;
            _actionBody.style.display = DisplayStyle.None;
            _actionPanel.Add(_actionBody);
            _actionPanel.Add(_drawnCardSlot);

            _buttonRow = new VisualElement();
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.justifyContent = Justify.Center;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.flexShrink = 0;
            _actionPanel.Add(_buttonRow);

            _statusLine = new Label();
            _statusLine.style.fontSize = 12;
            _statusLine.style.unityTextAlign = TextAnchor.MiddleCenter;
            _statusLine.style.marginTop = 6;
            _statusLine.style.color = UITheme.TextSecondary;
            _centerTable.Add(_statusLine);

            middle.Add(_rightSeat.Root);

            _socialPanel = new VisualElement { name = "TableSocialPanel" };
            _socialPanel.style.flexDirection = FlexDirection.Column;
            _socialPanel.style.width = 300;
            _socialPanel.style.minWidth = 300;
            _socialPanel.style.maxWidth = 300;
            _socialPanel.style.flexGrow = 0;
            _socialPanel.style.flexShrink = 0;
            _socialPanel.style.minHeight = 0;
            _socialPanel.style.height = Length.Percent(100);
            _socialPanel.style.alignSelf = Align.Stretch;
            _socialPanel.style.marginLeft = 12;
            _socialPanel.style.paddingLeft = 10;
            _socialPanel.style.paddingRight = 10;
            _socialPanel.style.paddingTop = 10;
            _socialPanel.style.paddingBottom = 10;
            _socialPanel.style.backgroundColor = UITheme.PanelSurface;
            _socialPanel.style.borderTopLeftRadius = 8;
            _socialPanel.style.borderTopRightRadius = 8;
            _socialPanel.style.borderBottomLeftRadius = 8;
            _socialPanel.style.borderBottomRightRadius = 8;
            _socialPanel.style.overflow = Overflow.Hidden;
            SetBorderWidth(_socialPanel, 1);
            SetBorderColor(_socialPanel, UITheme.PanelBorder);
            _container.Add(_socialPanel);

            var socialTabs = new VisualElement();
            socialTabs.style.flexDirection = FlexDirection.Row;
            socialTabs.style.flexShrink = 0;
            socialTabs.style.marginRight = 8;
            socialTabs.style.marginBottom = 8;
            _socialPanel.Add(socialTabs);

            _logTabButton = new Button(() =>
            {
                _showChat = false;
                RenderSocialPanel(_flow.State);
            }) { text = "游戏日志" };
            StyleSocialTab(_logTabButton);
            socialTabs.Add(_logTabButton);

            _chatTabButton = new Button(() =>
            {
                _showChat = true;
                RenderSocialPanel(_flow.State);
            }) { text = "房间交流" };
            StyleSocialTab(_chatTabButton);
            socialTabs.Add(_chatTabButton);

            _socialContent = new VisualElement();
            _socialContent.style.flexGrow = 1;
            _socialContent.style.flexShrink = 1;
            _socialContent.style.minHeight = 0;
            _socialContent.style.paddingLeft = 2;
            _socialContent.style.paddingRight = 2;
            _socialContent.style.overflow = Overflow.Hidden;
            _socialPanel.Add(_socialContent);

            _roomChatPanel = new RoomChatPanel(_flow, true, true);
            _roomChatPanel.Root.style.flexGrow = 1;
            _roomChatPanel.Root.style.flexShrink = 1;
            _roomChatPanel.Root.style.minHeight = 0;
            _socialPanel.Add(_roomChatPanel.Root);

            _playArea.Add(_selfSeat.Root);

            _endGameButton = new Button(() => ShowLocalEndGameConfirm())
            {
                text = "X",
                tooltip = "结束游戏"
            };
            _endGameButton.style.position = Position.Absolute;
            _endGameButton.style.top = 0;
            _endGameButton.style.right = 0;
            _endGameButton.style.width = 20;
            _endGameButton.style.height = 20;
            _endGameButton.style.minWidth = 20;
            _endGameButton.style.paddingLeft = 0;
            _endGameButton.style.paddingRight = 0;
            _endGameButton.style.paddingTop = 0;
            _endGameButton.style.paddingBottom = 0;
            _endGameButton.style.fontSize = 15;
            _endGameButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _endGameButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            _endGameButton.style.display = DisplayStyle.None;
            StyleTableButton(_endGameButton, true);
            root.Add(_endGameButton);
            _endGameButton.BringToFront();
            _endGameModalOverlay.BringToFront();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _endGameButton.style.display = DisplayStyle.None;
            _cardTableView.SetVisible(visible
                && _flow.State.Phase == GamePhase.Playing
                && GetEndGameModalKind(_flow.State) == EndGameModalKind.None);
            if (!visible)
            {
                _showLocalEndGameConfirm = false;
                ApplyEndGameModal(EndGameModalKind.None, "", "");
                ClearTransientAnimationState();
            }
        }

        public void Dispose()
        {
            ClearTransientAnimationState();
            if (_geometryChangedHandler != null && _root != null)
                _root.UnregisterCallback(_geometryChangedHandler);
            _container?.RemoveFromHierarchy();
            _animationLayer?.RemoveFromHierarchy();
            _endGameButton?.RemoveFromHierarchy();
            _endGameModalOverlay?.RemoveFromHierarchy();
            if (_cardTableView != null)
                CardTableView.DestroyView(_cardTableView);
        }

        public bool HasPendingActionAnimation => Time.realtimeSinceStartup < _animationQueueUntil;

        public void SetAnimationQueueDrainedCallback(Action callback)
        {
            _animationQueueDrained = callback;
        }

        public void Tick()
        {
            if (HasLayoutChanged())
                ScheduleLayoutRefresh();

            if (!_inspectionActive || Time.realtimeSinceStartup < _inspectionEndsAt)
                return;

            _inspectionActive = false;
            HideInspectionZone();
            if (_temporaryHiddenCard != null && _temporaryHiddenCard.panel != null)
                _temporaryHiddenCard.style.visibility = Visibility.Visible;
            PlayFlyCard(_inspectionReturnStart, _inspectionReturnEnd, false, 0, _inspectionReturnColor, SkillMoveDuration);

            _temporaryHiddenCard = null;
        }

        public void RenderGame()
        {
            var state = _flow.State;
            if (state.RoundNumber != _lastRenderedRoundNumber)
            {
                _lastLocalDrawSequence = 0;
                _lastRenderedRoundNumber = state.RoundNumber;
            }

            ConfigureActionPanelForGame();
            bool shouldPlayLocalDraw = state.HasDrawnCard
                && _flow.SubState == GameSubState.AwaitingDrawnDecision
                && state.DrawResponseSequence > _lastLocalDrawSequence;
            ActionAnimationSnapshot pendingAction = null;
            if (state.LastActionSequence > _lastAnimatedActionSequence)
            {
                _lastAnimatedActionSequence = state.LastActionSequence;
                pendingAction = BuildActionAnimationSnapshot(state);
            }

            ResetSelectionWhenSubStateChanges(_flow.SubState);
            if (!_inspectionActive)
                HideInspectionZone();

            bool holdingPreviousAction = IsActionAnimationHoldActive();
            long visualCurrentPlayerId = holdingPreviousAction ? _heldActionSourcePlayerId : state.CurrentPlayerId;
            bool deferNewTurnActions = holdingPreviousAction && visualCurrentPlayerId != state.CurrentPlayerId;
            bool freezePileVisuals = pendingAction != null || _cardTableView.HasActiveTransientAnimation || _discardVisualHoldActive;

            _roundLabel.text = "";
            _roundLabel.style.display = DisplayStyle.None;
            _turnLabel.text = "";
            _turnLabel.style.display = DisplayStyle.None;

            bool holdPendingSelfExchangeView = pendingAction == null && ShouldHoldPendingSelfExchangeView(state);
            RenderSeats(state, visualCurrentPlayerId, holdPendingSelfExchangeView);
            RenderPiles(state, freezePileVisuals);
            if (pendingAction != null)
                FinalizeActionAnimationSnapshot(pendingAction);
            RenderCardTableLayer(state, pendingAction);
            if (shouldPlayLocalDraw)
                PlayDrawAnimation(state.DrawnCardValue);
            bool hideActionPanelForTransient = _hideActionPanelDuringTransient && _cardTableView.HasActiveTransientAnimation;
            if (pendingAction != null && ShouldHideActionPanelDuringAction(pendingAction))
            {
                _hideActionPanelDuringTransient = true;
                hideActionPanelForTransient = true;
            }
            else if (!_cardTableView.HasActiveTransientAnimation)
            {
                _hideActionPanelDuringTransient = false;
            }

            RenderActionPanel(state, deferNewTurnActions, hideActionPanelForTransient);
            UpdateGameLogFromState(state);
            RenderSocialPanel(state);
            RenderEndGameControls(state, true);
            UpdateStatusLineForEarlyEnd(state);

            if (pendingAction != null)
            {
                EnqueueActionAnimation(pendingAction);
                ClearPendingSelfExchangeSnapshot(pendingAction);
                ClearPendingSelfSwapSnapshot(pendingAction);
            }
            _lastRenderedSubState = _flow.SubState;
        }

        bool HasLayoutChanged()
        {
            if (_root == null || _root.panel == null)
                return false;

            var bounds = _root.worldBound;
            if (!HasUsableBounds(bounds))
                return false;

            return !_lastRootBounds.Equals(bounds)
                || _lastScreenWidth != Screen.width
                || _lastScreenHeight != Screen.height;
        }

        void ScheduleLayoutRefresh()
        {
            if (_layoutRefreshQueued || _root == null || _root.panel == null)
                return;

            _layoutRefreshQueued = true;
            int generation = _animationGeneration;
            _root.schedule.Execute(() =>
            {
                _layoutRefreshQueued = false;
                if (generation != _animationGeneration || _flow == null || _root == null || _root.panel == null)
                    return;

                _lastRootBounds = _root.worldBound;
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;

                if (_flow.State.Phase == GamePhase.Playing)
                {
                    RenderGame();
                }
                else if (_flow.State.Phase == GamePhase.RoundReveal)
                {
                    RenderReveal();
                }
                else if (_flow.State.Phase == GamePhase.GameOver)
                {
                    RenderGameOver();
                }
            }).ExecuteLater(1);
        }

        public void RenderReveal()
        {
            ClearTransientAnimationState();
            _cardTableView.SetVisible(false);
            var state = _flow.State;
            ConfigureActionPanelForOverlay();
            _roundLabel.style.display = DisplayStyle.Flex;
            _turnLabel.style.display = DisplayStyle.Flex;
            _roundLabel.text = $"第 {state.RoundNumber} 轮结算";
            _turnLabel.text = "所有手牌已翻开计分";
            HideSeatsForOverlay();
            RenderPiles(state);
            RenderSocialPanel(state);
            RenderEndGameControls(state, true);

            _drawnCardSlot.Clear();
            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;
            _actionPanel.style.marginTop = 8;

            var title = new Label("本轮得分");
            StylePanelTitle(title);
            _actionPanel.Add(title);

            var resultList = new ScrollView(ScrollViewMode.Vertical);
            resultList.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            resultList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            resultList.style.flexGrow = 1;
            resultList.style.flexShrink = 1;
            resultList.style.minHeight = 0;
            resultList.style.marginTop = 2;
            resultList.style.marginBottom = 6;
            foreach (var result in state.LastRoundResults)
                resultList.Add(CreateRevealRow(result));
            _actionPanel.Add(resultList);

            AddInterRoundControls(state);
            UpdateStatusLineForEarlyEnd(state, true);
        }

        public void RenderGameOver()
        {
            ClearTransientAnimationState();
            _cardTableView.SetVisible(false);
            ConfigureActionPanelForOverlay();
            _roundLabel.style.display = DisplayStyle.Flex;
            _turnLabel.style.display = DisplayStyle.Flex;
            _roundLabel.text = "游戏结束";
            _turnLabel.text = "最终排名";
            HideSeatsForOverlay();
            _drawnCardSlot.Clear();
            _drawPile.Clear();
            _discardPile.Clear();
            RenderSocialPanel(_flow.State);
            _showLocalEndGameConfirm = false;
            RenderEndGameControls(_flow.State, false);

            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            var title = new Label("最终得分");
            StylePanelTitle(title);
            _actionPanel.Add(title);

            foreach (var rank in _flow.State.FinalRankings)
            {
                var row = new Label($"{rank.Rank}. {rank.Nickname}  {rank.FinalScore}" + (rank.IsWinner ? "  胜者" : ""));
                row.style.fontSize = rank.IsWinner ? 18 : 15;
                row.style.unityFontStyleAndWeight = rank.IsWinner ? FontStyle.Bold : FontStyle.Normal;
                row.style.unityTextAlign = TextAnchor.MiddleCenter;
                row.style.marginTop = 6;
                _actionPanel.Add(row);
            }

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.justifyContent = Justify.Center;
            controls.style.marginTop = 14;
            _actionPanel.Add(controls);
            controls.Add(CreatePanelButton("返回房间", () => _flow.ReturnToRoomAfterGameOver(),
                _flow.State.MyPlayerId > 0 && _flow.State.RoomId > 0));
            controls.Add(CreatePanelButton("返回首页", () => _flow.ReturnHomeAfterGameOver(),
                _flow.State.MyPlayerId > 0 && _flow.State.RoomId > 0));
            controls.Add(CreatePanelButton("退出游戏", () => _flow.ExitGame(), true));

            _statusLine.text = "返回房间后可重新准备并开始新对局。";
            _statusLine.style.display = DisplayStyle.Flex;
        }

        void ShowLocalEndGameConfirm()
        {
            if (!IsEndGameButtonEnabled(_flow.State))
                return;

            _showLocalEndGameConfirm = true;
            Debug.Log($"[GameTablePanel] Show early-end confirm. host={_flow.State.IsMyselfHost}");
            RenderEndGameControls(_flow.State, true);
        }

        void CancelLocalEndGameConfirm()
        {
            if (!_showLocalEndGameConfirm)
                return;

            _showLocalEndGameConfirm = false;
            RenderEndGameControls(_flow.State, CanShowEndGameButton(_flow.State));
            UpdateStatusLineForEarlyEnd(_flow.State);
        }

        void ConfirmLocalEndGame()
        {
            _showLocalEndGameConfirm = false;
            Debug.Log("[GameTablePanel] Confirm early-end request.");
            _flow.RequestEarlyEndGame();
        }

        void RenderEndGameControls(GameState state, bool allowButton)
        {
            bool showButton = allowButton && ShouldShowEndGameButton(state);
            _endGameButton.BringToFront();
            _endGameButton.style.display = showButton ? DisplayStyle.Flex : DisplayStyle.None;

            bool enabled = showButton && IsEndGameButtonEnabled(state);
            _endGameButton.SetEnabled(enabled);
            StyleTableButton(_endGameButton, enabled);

            switch (GetEndGameModalKind(state))
            {
                case EndGameModalKind.LocalConfirm:
                    ApplyEndGameModal(
                        EndGameModalKind.LocalConfirm,
                        state.IsMyselfHost ? "结束本局" : "发起结束申请",
                        state.IsMyselfHost ? "是否直接结束本局游戏？" : "是否发起结束游戏申请？",
                        CreateModalButton("确认", () => ConfirmLocalEndGame(), true),
                        CreateModalButton("取消", () => CancelLocalEndGameConfirm(), true));
                    break;

                case EndGameModalKind.HostDecision:
                    string requesterName = string.IsNullOrEmpty(state.PendingEndGameRequesterNickname)
                        ? "该玩家"
                        : state.PendingEndGameRequesterNickname;
                    ApplyEndGameModal(
                        EndGameModalKind.HostDecision,
                        "结束游戏申请",
                        $"{requesterName} 请求结束本局游戏，是否确认结束？",
                        CreateModalButton("确认结束", () => _flow.RespondEarlyEndGameRequest(true), true),
                        CreateModalButton("取消", () => _flow.RespondEarlyEndGameRequest(false), true));
                    break;

                case EndGameModalKind.RejectedInfo:
                    ApplyEndGameModal(
                        EndGameModalKind.RejectedInfo,
                        "结束申请未通过",
                        "房主已拒绝结束申请",
                        CreateModalButton("知道了", () => _flow.DismissEarlyEndGameRejectedPrompt(), true));
                    break;

                default:
                    ApplyEndGameModal(EndGameModalKind.None, "", "");
                    break;
            }
        }

        EndGameModalKind GetEndGameModalKind(GameState state)
        {
            if (state.ShowEndGameRejectedPrompt)
                return EndGameModalKind.RejectedInfo;

            if (state.ShowEndGameRequestPrompt
                && state.IsMyselfHost
                && state.PendingEndGameRequesterPlayerId != 0
                && state.PendingEndGameRequesterPlayerId != state.MyPlayerId)
                return EndGameModalKind.HostDecision;

            if (_showLocalEndGameConfirm)
                return EndGameModalKind.LocalConfirm;

            return EndGameModalKind.None;
        }

        bool CanShowEndGameButton(GameState state)
        {
            return state.Phase == GamePhase.Playing || state.Phase == GamePhase.RoundReveal;
        }

        bool IsEndGameButtonEnabled(GameState state)
        {
            if (!CanShowEndGameButton(state))
                return false;
            if (state.MyPlayerId <= 0 || state.RoomId <= 0)
                return false;
            if (_showLocalEndGameConfirm)
                return false;
            if (state.IsWaitingForEndGameRequestRsp || state.IsWaitingForEndGameDecisionRsp)
                return false;
            if (state.ShowEndGameRequestPrompt || state.ShowEndGameRejectedPrompt)
                return false;
            if (!state.IsMyselfHost && state.PendingEndGameRequesterPlayerId == state.MyPlayerId)
                return false;
            return true;
        }

        bool ShouldShowEndGameButton(GameState state)
        {
            if (!CanShowEndGameButton(state))
                return false;
            if (state.ShowEndGameRequestPrompt || state.ShowEndGameRejectedPrompt)
                return false;
            return true;
        }

        void UpdateStatusLineForEarlyEnd(GameState state, bool preserveExistingTextWhenEmpty = false)
        {
            string statusText = "";
            if (state.IsMyselfHost && state.IsWaitingForEndGameRequestRsp)
                statusText = "正在结束本局游戏...";
            else if (state.IsWaitingForEndGameDecisionRsp)
                statusText = "正在处理结束申请...";
            else if (state.PendingEndGameRequesterPlayerId == state.MyPlayerId)
                statusText = "已向房主发起结束申请";

            if (string.IsNullOrEmpty(statusText))
            {
                if (preserveExistingTextWhenEmpty)
                    return;
                _statusLine.text = "";
                _statusLine.style.display = DisplayStyle.None;
                return;
            }

            _statusLine.text = statusText;
            _statusLine.style.display = DisplayStyle.Flex;
        }

        void ApplyEndGameModal(EndGameModalKind kind, string title, string body, params Button[] buttons)
        {
            _endGameModalButtons.Clear();

            if (kind == EndGameModalKind.None)
            {
                _endGameModalOverlay.style.display = DisplayStyle.None;
                _cardTableView.SetVisible(_isVisible && _flow.State.Phase == GamePhase.Playing);
                _endGameModalTitle.text = "";
                _endGameModalBody.text = "";
                return;
            }

            _cardTableView.SetVisible(false);
            _endGameButton.BringToFront();
            _endGameModalOverlay.BringToFront();
            _endGameModalOverlay.style.display = DisplayStyle.Flex;
            _endGameModalTitle.text = title;
            _endGameModalBody.text = body;

            if (buttons == null)
                return;

            foreach (var button in buttons)
            {
                if (button != null)
                    _endGameModalButtons.Add(button);
            }
        }

        Button CreateModalButton(string text, Action action, bool enabled)
        {
            var button = CreatePanelButton(text, action, enabled);
            button.style.minWidth = 98;
            button.style.height = 34;
            return button;
        }

        void RenderSeats(GameState state, long visualCurrentPlayerId, bool holdPendingSelfExchangeView = false)
        {
            _selfSeat.Root.style.display = DisplayStyle.Flex;
            foreach (var seat in _opponentSeats)
            {
                seat.Root.style.display = DisplayStyle.Flex;
            }

            var myInfo = state.Players.Find(p => p.PlayerId == state.MyPlayerId);
            bool selfCabo = state.SteadyCallerId != 0 && state.SteadyCallerId == state.MyPlayerId;
            _selfSeat.RenderHeader(myInfo?.Nickname ?? "你", myInfo?.TotalScore ?? 0, visualCurrentPlayerId == state.MyPlayerId, selfCabo ? "CABO" : "你", selfCabo,
                PlayerProfileStore.GetAvatarPathForPlayer(state.MyPlayerId, true));
            int selfCardCount = holdPendingSelfExchangeView && _pendingSelfExchangeSnapshot != null
                ? _pendingSelfExchangeSnapshot.SourceHandCards.Count
                : state.MyCards.Count;
            EnsureCardAnchors(_selfSeat.CardRow, selfCardCount, SelfCardWidth, SelfCardHeight);

            var opponentIndices = BuildOpponentIndices(state);
            for (int i = 0; i < _opponentSeats.Length; i++)
            {
                if (i >= opponentIndices.Count)
                {
                    _opponentSeats[i].Root.style.visibility = Visibility.Hidden;
                    continue;
                }

                _opponentSeats[i].Root.style.visibility = Visibility.Visible;
                var player = state.Players[opponentIndices[i]];
                bool current = player.PlayerId == visualCurrentPlayerId;
                bool selected = _selectedOpponentPlayerId == player.PlayerId;
                bool cabo = state.SteadyCallerId != 0 && state.SteadyCallerId == player.PlayerId;
                _opponentSeats[i].RenderHeader(player.Nickname, player.TotalScore, current, cabo ? "CABO" : selected ? "目标" : "对手", cabo,
                    PlayerProfileStore.GetAvatarPathForPlayer(player.PlayerId, false));
                EnsureCardAnchors(_opponentSeats[i].CardRow, Mathf.Max(0, player.CardCount), OppCardWidth, OppCardHeight);
            }
        }

        void EnsureCardAnchors(VisualElement row, int count, int width, int height)
        {
            if (row == null)
                return;

            count = Mathf.Max(0, count);
            while (row.childCount > count)
                row[row.childCount - 1].RemoveFromHierarchy();

            while (row.childCount < count)
            {
                var anchor = CreateCard(false, 0, width, height, false, false, null);
                StyleCardTablePlaceholder(anchor);
                row.Add(anchor);
            }

            for (int i = 0; i < row.childCount; i++)
                StyleCardTablePlaceholder(row[i]);
        }

        void RenderPiles(GameState state, bool freezeDiscardPile = false)
        {
            EnsurePileContainersAttached();
            bool compact = state.Phase == GamePhase.RoundReveal || state.Phase == GamePhase.GameOver;
            bool usePlaceholders = state.Phase == GamePhase.Playing;
            ConfigurePileRowLayout(usePlaceholders);
            EnsurePileCard(_drawPile, "牌库", "CABO", state.DrawPileCount.ToString(), false, compact, usePlaceholders);

            int discardTopValue = state.DiscardTopValue;
            int discardPileCount = state.DiscardPileCount;
            if (_discardVisualHoldActive && _heldDiscardPileCount >= 0)
            {
                discardTopValue = _heldDiscardTopValue;
                discardPileCount = _heldDiscardPileCount;
            }
            else if (freezeDiscardPile && _stableDiscardPileCount >= 0)
            {
                discardTopValue = _stableDiscardTopValue;
                discardPileCount = _stableDiscardPileCount;
            }
            EnsurePileCard(_discardPile, "弃牌堆", discardTopValue >= 0 ? discardTopValue.ToString() : "-", discardPileCount.ToString(), true, compact, usePlaceholders);

            if (!freezeDiscardPile && !_discardVisualHoldActive)
            {
                _stableDiscardTopValue = state.DiscardTopValue;
                _stableDiscardPileCount = state.DiscardPileCount;
            }
        }

        void BeginDiscardVisualHold(ActionAnimationSnapshot action)
        {
            if (!ShouldHoldDiscardVisual(action))
                return;
            if (_discardVisualHoldActive && _heldDiscardSequence == action.Sequence)
                return;

            int topValue;
            int pileCount;
            if (_discardVisualHoldActive && _heldDiscardPileCount >= 0)
            {
                topValue = _heldDiscardTopValue;
                pileCount = _heldDiscardPileCount;
            }
            else if (_stableDiscardPileCount >= 0)
            {
                topValue = _stableDiscardTopValue;
                pileCount = _stableDiscardPileCount;
            }
            else
            {
                topValue = _flow.State.DiscardTopValue;
                pileCount = _flow.State.DiscardPileCount;
            }

            _discardVisualHoldActive = true;
            _heldDiscardSequence = action.Sequence;
            _heldDiscardTopValue = topValue;
            _heldDiscardPileCount = Mathf.Max(0, pileCount);
            RenderPiles(_flow.State, true);
        }

        void SetHeldDiscardVisual(int topValue, int pileCount)
        {
            if (!_discardVisualHoldActive)
                return;

            _heldDiscardTopValue = topValue;
            _heldDiscardPileCount = Mathf.Max(0, pileCount);
            RenderPiles(_flow.State, true);
        }

        void PopHeldDiscardVisual(ActionAnimationSnapshot action)
        {
            if (!_discardVisualHoldActive)
                return;
            if (action != null && _heldDiscardSequence != 0 && _heldDiscardSequence != action.Sequence)
                return;

            int newCount = Mathf.Max(0, _heldDiscardPileCount - 1);
            SetHeldDiscardVisual(-1, newCount);
        }

        void PushHeldDiscardVisual(ActionAnimationSnapshot action, int topValue, int pileCount)
        {
            if (!_discardVisualHoldActive)
                return;
            if (action != null && _heldDiscardSequence != 0 && _heldDiscardSequence != action.Sequence)
                return;

            SetHeldDiscardVisual(topValue, pileCount);
        }

        void ReleaseDiscardVisualHold(ActionAnimationSnapshot action)
        {
            if (!_discardVisualHoldActive)
                return;
            if (action != null && _heldDiscardSequence != 0 && _heldDiscardSequence != action.Sequence)
                return;

            float remainingQueueTime = _animationQueueUntil - Time.realtimeSinceStartup;
            if (remainingQueueTime > 0.02f)
            {
                ScheduleAfter(remainingQueueTime, () => ReleaseDiscardVisualHold(action));
                return;
            }

            _discardVisualHoldActive = false;
            _heldDiscardSequence = 0;
            _heldDiscardTopValue = int.MinValue;
            _heldDiscardPileCount = -1;
            _stableDiscardTopValue = action != null ? action.DiscardTopValue : _flow.State.DiscardTopValue;
            _stableDiscardPileCount = action != null ? action.DiscardPileCount : _flow.State.DiscardPileCount;
            RenderPiles(_flow.State);
        }

        static bool ShouldHoldDiscardVisual(ActionAnimationSnapshot action)
        {
            if (action == null)
                return false;
            if (action.ActionType == ActionType.DiscardDrawn)
                return true;
            return action.ExchangeSucceeded
                && (action.ActionType == ActionType.ReplaceWithDrawn || action.ActionType == ActionType.TakeFromDiscard);
        }

        static bool ShouldPopDiscardVisualAtStart(ActionAnimationSnapshot action)
        {
            return action != null && action.ExchangeSucceeded && action.ActionType == ActionType.TakeFromDiscard;
        }

        float GetDiscardVisualRevealDelay(ActionAnimationSnapshot action)
        {
            if (!ShouldHoldDiscardVisual(action))
                return -1f;
            if (action.ActionType == ActionType.DiscardDrawn)
                return QuickMoveDuration;

            int discardedCount = GetAnimatedDiscardedCount(action);
            float stagger = GetDiscardStagger(discardedCount - 1, discardedCount);
            if (action.ActionType == ActionType.TakeFromDiscard)
                return TakeDiscardOutgoingDelay + stagger + QuickMoveDuration;

            return EmptyOriginHoldDuration + stagger + QuickMoveDuration;
        }

        static int GetAnimatedDiscardedCount(ActionAnimationSnapshot action)
        {
            if (action == null)
                return 1;
            if (action.SelectedSlotBounds.Count > 0)
                return Mathf.Max(1, action.SelectedSlotBounds.Count);
            if (action.DiscardedCount > 0)
                return Mathf.Max(1, action.DiscardedCount);
            return Mathf.Max(1, action.SelectedSlots.Count);
        }

        void ConfigurePileRowLayout(bool fixedGameplayAnchor)
        {
            if (_pileRow == null)
                return;

            if (fixedGameplayAnchor)
            {
                _pileRow.style.position = Position.Absolute;
                _pileRow.style.left = 0;
                _pileRow.style.right = 0;
                _pileRow.style.top = GameplayPileTopOffset;
                _pileRow.style.bottom = StyleKeyword.Auto;
                _pileRow.style.height = 122;
                _pileRow.style.marginTop = 0;
                _pileRow.style.marginBottom = 0;
                return;
            }

            _pileRow.style.position = Position.Relative;
            _pileRow.style.left = StyleKeyword.Auto;
            _pileRow.style.right = StyleKeyword.Auto;
            _pileRow.style.top = StyleKeyword.Auto;
            _pileRow.style.bottom = StyleKeyword.Auto;
            _pileRow.style.height = StyleKeyword.Auto;
            _pileRow.style.marginTop = 2;
            _pileRow.style.marginBottom = 0;
        }

        void EnsurePileContainersAttached()
        {
            if (_drawPile.parent != _pileRow)
                _pileRow.Add(_drawPile);
            if (_discardPile.parent != _pileRow)
                _pileRow.Add(_discardPile);
        }

        void ConfigureActionPanelForGame()
        {
            _actionPanel.style.width = Length.Percent(92);
            _actionPanel.style.maxWidth = 640;
            _actionPanel.style.flexGrow = 0;
            _actionPanel.style.flexShrink = 0;
            _actionPanel.style.minHeight = StyleKeyword.Null;
            _actionPanel.style.maxHeight = StyleKeyword.Null;
            _actionPanel.style.paddingLeft = 16;
            _actionPanel.style.paddingRight = 16;
            _actionPanel.style.paddingTop = 8;
            _actionPanel.style.paddingBottom = 8;
            _actionPanel.style.marginTop = GameplayActionPanelTopMargin;
            _actionPanel.style.overflow = Overflow.Visible;
        }

        void ConfigureActionPanelForOverlay()
        {
            _actionPanel.style.width = Length.Percent(78);
            _actionPanel.style.maxWidth = 920;
            _actionPanel.style.flexGrow = 1;
            _actionPanel.style.flexShrink = 1;
            _actionPanel.style.minHeight = 0;
            _actionPanel.style.maxHeight = Length.Percent(82);
            _actionPanel.style.paddingLeft = 22;
            _actionPanel.style.paddingRight = 22;
            _actionPanel.style.paddingTop = 12;
            _actionPanel.style.paddingBottom = 12;
            _actionPanel.style.marginTop = 8;
            _actionPanel.style.overflow = Overflow.Hidden;
        }

        void RenderActionPanel(GameState state, bool deferNewTurnActions, bool forceHidden = false)
        {
            ResetActionPanelForGame();
            if (forceHidden)
            {
                _actionPanel.style.display = DisplayStyle.None;
                return;
            }

            bool needsRebuild = _lastActionPanelSubState != _flow.SubState
                || _lastActionPanelDeferredNewTurnActions != deferNewTurnActions;
            if (needsRebuild)
            {
                _drawnCardSlot.Clear();
                _buttonRow.Clear();
                _actionButtons.Clear();
                _drawnCardSlot.style.display = DisplayStyle.None;
                _actionBody.style.display = DisplayStyle.None;
            }
            _actionPanel.style.display = ShouldShowActionPanel(_flow.SubState) && !deferNewTurnActions
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (deferNewTurnActions)
            {
                _actionTitle.text = "动作展示中";
                _actionBody.text = "等待上一名玩家的操作动画完成。";
                _lastActionPanelSubState = _flow.SubState;
                _lastActionPanelDeferredNewTurnActions = deferNewTurnActions;
                return;
            }

            if (needsRebuild)
            {
                BuildActionPanelContent(state);
            }

            UpdateActionPanelButtons(state);
            _lastActionPanelSubState = _flow.SubState;
            _lastActionPanelDeferredNewTurnActions = deferNewTurnActions;
        }

        void BuildActionPanelContent(GameState state)
        {
            switch (_flow.SubState)
            {
                case GameSubState.AwaitingMainInput:
                    _actionTitle.text = "你的回合";
                    _actionBody.text = "选择牌堆操作，或喊 CABO。";
                    AddActionButton("抽牌", () => _flow.DoDraw(), true);
                    AddActionButton("拿弃牌", () => _flow.DoTakeFromDiscard(), state.TurnNumber > 1 && state.DiscardPileCount > 0);
                    AddActionButton("喊 CABO", () => _flow.DoCallSteady(), !state.IsFinalRound);
                    break;

                case GameSubState.AwaitingDrawnDecision:
                    _actionTitle.text = "抽到的牌";
                    _actionBody.text = "可以弃掉、替换手牌，或弃掉后发动技能。";
                    AddActionButton("弃牌", () => _flow.DoDiscardDrawn(false), true);
                    AddActionButton("替换", () => _flow.BeginReplaceWithDrawn(), true);
                    AddActionButton(BuildSkillButtonText(state.DrawnCardSkill), () => _flow.DoDiscardDrawn(true), state.DrawnCardSkill > 0);
                    break;

                case GameSubState.AwaitingReplaceSlots:
                    _actionTitle.text = "用抽牌替换";
                    _actionBody.text = "选择一张或多张自己的手牌，然后确认。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("确认替换", () => ConfirmReplace(), _selectedOwnSlots.Count > 0);
                    AddActionButton("返回选择", () => ReturnToDrawnDecision(), true);
                    AddActionButton("改为弃牌", () => _flow.DoDiscardDrawn(false), true);
                    AddActionButton(BuildChangeToSkillButtonText(state.DrawnCardSkill), () => _flow.DoDiscardDrawn(true), state.DrawnCardSkill > 0);
                    break;

                case GameSubState.AwaitingTakeSlots:
                    _actionTitle.text = "拿弃牌";
                    _actionBody.text = "选择一张或多张自己的手牌，与弃牌堆顶交换。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("确认拿牌", () => ConfirmTakeDiscard(), _selectedOwnSlots.Count > 0);
                    AddActionButton("返回选择", () => ReturnToMainInput(), true);
                    break;

                case GameSubState.SkillPeekSlot:
                    _actionTitle.text = "看牌技能";
                    _actionBody.text = "选择一张自己的牌私下查看。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("确认看牌", () => ConfirmSkillPeekSlot(), _selectedOwnSlots.Count == 1);
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSpyTarget:
                    _actionTitle.text = "偷看技能";
                    _actionBody.text = "选择一名对手。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSpySlot:
                    _actionTitle.text = "偷看技能";
                    _actionBody.text = "选择该对手的一张牌。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("确认偷看", () => ConfirmSkillTargetSlot(), _selectedOpponentSlot >= 0);
                    AddActionButton("返回选择对手", () => ReturnToSkillTargetSelection(), true);
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapMySlot:
                    _actionTitle.text = "换牌技能";
                    _actionBody.text = "选择自己要交换的一张牌。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapTargetPlayer:
                    _actionTitle.text = "换牌技能";
                    _actionBody.text = "请点击您想换的对手的牌。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("返回选择己方牌", () => ReturnToSkillStart(), true);
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapTargetSlot:
                    _actionTitle.text = "换牌技能";
                    _actionBody.text = "请点击您想换的对手的牌。";
                    _actionBody.style.display = DisplayStyle.Flex;
                    AddActionButton("确认换牌", () => ConfirmSkillTargetSlot(), _selectedOpponentSlot >= 0);
                    AddActionButton("返回选择对手", () => ReturnToSkillTargetSelection(), true);
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.Idle:
                    _actionTitle.text = "等待中";
                    _actionBody.text = state.IsMyTurn ? "正在准备你的操作。" : "其他玩家正在行动。";
                    break;

                default:
                    _actionTitle.text = "等待服务器";
                    _actionBody.text = "请求已发送。";
                    break;
            }
        }

        static bool ShouldShowActionPanel(GameSubState subState)
        {
            return subState == GameSubState.AwaitingMainInput
                || subState == GameSubState.AwaitingDrawnDecision
                || subState == GameSubState.AwaitingReplaceSlots
                || subState == GameSubState.AwaitingTakeSlots
                || subState == GameSubState.SkillPeekSlot
                || subState == GameSubState.SkillSpyTarget
                || subState == GameSubState.SkillSpySlot
                || subState == GameSubState.SkillSwapMySlot
                || subState == GameSubState.SkillSwapTargetPlayer
                || subState == GameSubState.SkillSwapTargetSlot;
        }

        void UpdateGameLogFromState(GameState state)
        {
            if (state.LastActionSequence <= 0 || state.LastActionSequence == _lastLoggedActionSequence)
                return;

            _lastLoggedActionSequence = state.LastActionSequence;
            if (string.IsNullOrWhiteSpace(state.LastActionMessage))
                return;

            _gameLogEntries.Add(new TableFeedEntry
            {
                PlayerName = "",
                Message = state.LastActionMessage.Replace(">>> ", ""),
                AvatarPath = "",
                IsSticker = false
            });
            TrimFeed(_gameLogEntries, 40);
        }

        void RenderSocialPanel(GameState state)
        {
            StyleSocialTabState(_logTabButton, !_showChat);
            StyleSocialTabState(_chatTabButton, _showChat);

            bool showChatChanged = _lastRenderedSocialShowChat != _showChat;
            bool logChanged = !_showChat && state.LastActionSequence != _lastRenderedSocialActionSequence;

            _socialContent.style.display = _showChat ? DisplayStyle.None : DisplayStyle.Flex;
            _roomChatPanel.Root.style.display = _showChat ? DisplayStyle.Flex : DisplayStyle.None;

            if (_showChat)
            {
                _roomChatPanel.Render();
            }
            else if (showChatChanged || logChanged)
            {
                _socialContent.Clear();
                if (_gameLogEntries.Count == 0 && state.LastActionSequence == 0)
                    AddFeedPlaceholder("本局动作会显示在这里");
                else
                    RenderFeed(_gameLogEntries, "等待第一条游戏日志");
            }

            _lastRenderedSocialShowChat = _showChat;
            _lastRenderedSocialActionSequence = state.LastActionSequence;
        }

        void RenderFeed(List<TableFeedEntry> entries, string emptyText)
        {
            if (entries.Count == 0)
            {
                AddFeedPlaceholder(emptyText);
                return;
            }

            int start = Mathf.Max(0, entries.Count - 8);
            for (int i = start; i < entries.Count; i++)
                _socialContent.Add(CreateFeedRow(entries[i]));
        }

        void AddFeedPlaceholder(string text)
        {
            var empty = new Label(text);
            empty.style.fontSize = 12;
            empty.style.color = UITheme.TextMuted;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.marginTop = 18;
            _socialContent.Add(empty);
        }

        VisualElement CreateFeedRow(TableFeedEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 7;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = UITheme.FeedBubble;
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;

            if (!string.IsNullOrEmpty(entry.PlayerName))
            {
                var avatar = PlayerProfileStore.CreateAvatarVisual(entry.PlayerName, entry.AvatarPath, 28);
                avatar.style.marginRight = 7;
                row.Add(avatar);
            }

            var body = new VisualElement();
            body.style.flexGrow = 1;
            row.Add(body);

            if (!string.IsNullOrEmpty(entry.PlayerName))
            {
                var name = new Label(entry.PlayerName);
                name.style.fontSize = 11;
                name.style.color = UITheme.TextSecondary;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                body.Add(name);
            }

            if (entry.IsSticker)
            {
                var sticker = PlayerProfileStore.CreateStickerVisual(entry.StickerPath, 48);
                sticker.style.marginTop = 2;
                body.Add(sticker);
            }
            else
            {
                var message = new Label(entry.Message);
                message.style.fontSize = 12;
                message.style.whiteSpace = WhiteSpace.Normal;
                message.style.color = UITheme.TextPrimary;
                body.Add(message);
            }

            return row;
        }

#if false
        void RenderStickerTray()
        {
            var stickers = PlayerProfileStore.GetStickerAssets();
            if (stickers.Count == 0)
            {
                var none = new Label("暂无表情资源");
                none.style.fontSize = 11;
                none.style.color = new Color(0.62f, 0.70f, 0.67f);
                none.style.unityTextAlign = TextAnchor.MiddleCenter;
                _stickerTray.Add(none);
                return;
            }

            int count = Mathf.Min(stickers.Count, 12);
            for (int i = 0; i < count; i++)
            {
                var sticker = stickers[i];
                var button = new Button(() => SendLocalSticker(sticker));
                button.text = "";
                button.style.width = 42;
                button.style.height = 42;
                button.style.minWidth = 42;
                button.style.marginLeft = 2;
                button.style.marginRight = 2;
                button.style.marginTop = 2;
                button.style.marginBottom = 2;
                button.style.paddingLeft = 3;
                button.style.paddingRight = 3;
                button.style.paddingTop = 3;
                button.style.paddingBottom = 3;
                button.Add(PlayerProfileStore.CreateStickerVisual(sticker.AssetPath, 30));
                _stickerTray.Add(button);
            }
        }

        void SendLocalChat()
        {
            var text = _chatInput.value?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            var me = _flow.State.Players.Find(p => p.PlayerId == _flow.State.MyPlayerId);
            var name = me?.Nickname ?? "你";
            _chatEntries.Add(new TableFeedEntry
            {
                PlayerName = name,
                AvatarPath = PlayerProfileStore.GetAvatarPathForPlayer(_flow.State.MyPlayerId, true),
                Message = text
            });
            TrimFeed(_chatEntries, 40);
            _chatInput.value = "";
            _showChat = true;
            RenderSocialPanel(_flow.State);
        }

        void SendLocalSticker(ArtAsset sticker)
        {
            var me = _flow.State.Players.Find(p => p.PlayerId == _flow.State.MyPlayerId);
            var name = me?.Nickname ?? "你";
            _chatEntries.Add(new TableFeedEntry
            {
                PlayerName = name,
                AvatarPath = PlayerProfileStore.GetAvatarPathForPlayer(_flow.State.MyPlayerId, true),
                Message = sticker.DisplayName,
                StickerPath = sticker.AssetPath,
                IsSticker = true
            });
            TrimFeed(_chatEntries, 40);
            _showChat = true;
            RenderSocialPanel(_flow.State);
        }

#endif
        static void TrimFeed(List<TableFeedEntry> entries, int maxCount)
        {
            while (entries.Count > maxCount)
                entries.RemoveAt(0);
        }

        VisualElement CreateDrawnCard(GameState state, int width, int height)
        {
            return new VisualElement();
        }

        void ScheduleDrawAnimation(int value)
        {
            int generation = _animationGeneration;
            _root.schedule.Execute(() =>
            {
                if (generation == _animationGeneration)
                    PlayDrawAnimation(value);
            }).ExecuteLater(1);
        }

        ActionAnimationSnapshot BuildActionAnimationSnapshot(GameState state)
        {
            var snapshot = new ActionAnimationSnapshot
            {
                ActionType = state.LastActionType,
                Skill = state.LastActionSkill,
                Sequence = state.LastActionSequence,
                SourcePlayerId = state.LastActionSourcePlayerId,
                TargetPlayerId = state.LastActionTargetPlayerId,
                SourceSlot = state.LastActionSourceSlot,
                TargetSlot = state.LastActionTargetSlot,
                SwapOccurred = state.LastActionSwapOccurred,
                ExchangeSucceeded = state.LastActionExchangeSucceeded,
                AttemptedMultiCard = state.LastActionAttemptedMultiCard,
                IncomingCardValue = state.LastActionIncomingCardValue,
                AddedCardCount = state.LastActionAddedCardCount,
                DiscardedCount = state.LastActionDiscardedCount,
                DrewExtraPenaltyCard = state.LastActionDrewExtraPenaltyCard,
                DiscardTopValue = state.DiscardTopValue,
                DiscardPileCount = state.DiscardPileCount,
                PeekedValue = state.LastPeekedValue,
                SourcePlayerBounds = GetPlayerBounds(state.LastActionSourcePlayerId),
                TargetPlayerBounds = GetPlayerBounds(state.LastActionTargetPlayerId),
                DrawPileBounds = GetPileCardBounds(_drawPile),
                DiscardPileBounds = GetPileCardBounds(_discardPile)
            };

            snapshot.SelectedSlots.AddRange(state.LastActionSelectedSlots);
            if (snapshot.SelectedSlots.Count == 0
                && (snapshot.ActionType == ActionType.ReplaceWithDrawn || snapshot.ActionType == ActionType.TakeFromDiscard)
                && snapshot.SourceSlot >= 0)
            {
                snapshot.SelectedSlots.Add(snapshot.SourceSlot);
            }

            snapshot.SourceSlotBounds.AddRange(CaptureSlotBounds(snapshot.SourcePlayerId, snapshot.SelectedSlots, true));
            snapshot.SourceSwapSlotBounds.AddRange(CaptureSlotBounds(snapshot.SourcePlayerId, new[] { snapshot.SourceSlot }, true));
            snapshot.TargetSlotBounds.AddRange(CaptureSlotBounds(snapshot.TargetPlayerId, new[] { snapshot.TargetSlot }, true));
            snapshot.SourceHandBounds.AddRange(CaptureAllSlotBounds(snapshot.SourcePlayerId, true));
            TryApplyPendingSelfExchangeSnapshot(snapshot);
            TryApplyPendingSelfSwapSnapshot(snapshot);
            return snapshot;
        }

        void FinalizeActionAnimationSnapshot(ActionAnimationSnapshot action)
        {
            if (action == null)
                return;

            if (action.ActionType == ActionType.ReplaceWithDrawn || action.ActionType == ActionType.TakeFromDiscard)
            {
                // FinalSourceHandBounds will be captured later in PlayActionAnimation
                // after the layout settle delay, to ensure UI Toolkit has completed layout
                BuildExchangeMotionPlan(action);
            }
        }

        void RenderCardTableLayer(GameState state, ActionAnimationSnapshot pendingAction)
        {
            var layout = BuildCardTableLayout(state);
            long frozenPlayerId = pendingAction != null ? pendingAction.SourcePlayerId : 0;
            long secondFrozenPlayerId = pendingAction != null && pendingAction.ActionType == ActionType.UseSkill && pendingAction.Skill == SkillType.Swap
                ? pendingAction.TargetPlayerId
                : 0;
            bool freezePiles = pendingAction != null || _cardTableView.HasActiveTransientAnimation || _discardVisualHoldActive;
            _cardTableView.SetVisible(true);
            _cardTableView.Render(state, layout, frozenPlayerId, secondFrozenPlayerId, freezePiles);
            if (!HasValidCardTableLayout(layout) && pendingAction == null)
                ScheduleCardTableLayerRefresh();
        }

        void RefreshCardInteractionLayer()
        {
            if (_flow.State.Phase != GamePhase.Playing)
            {
                RenderGame();
                return;
            }

            if (_cardTableView.HasActiveTransientAnimation)
                return;

            var layout = BuildCardTableLayout(_flow.State);
            if (!_cardTableView.HasRenderableLayout
                || !HasValidCardTableLayout(layout)
                || !_cardTableView.RefreshInteractions(_flow.State, layout, _discardVisualHoldActive))
            {
                RenderCardTableLayer(_flow.State, null);
            }
            UpdateActionPanelButtons(_flow.State);
        }

        static bool HasValidCardTableLayout(CardTableLayout layout)
        {
            for (int i = 0; i < layout.Slots.Count; i++)
                if (layout.Slots[i].IsValid)
                    return true;
            return false;
        }

        void ScheduleCardTableLayerRefresh()
        {
            if (_cardTableRefreshQueued)
                return;

            _cardTableRefreshQueued = true;
            int generation = _animationGeneration;
            _root.schedule.Execute(() =>
            {
                _cardTableRefreshQueued = false;
                if (generation != _animationGeneration || _flow.State.Phase != GamePhase.Playing)
                    return;
                if (_cardTableView.HasActiveTransientAnimation)
                {
                    ScheduleCardTableLayerRefresh();
                    return;
                }
                RenderCardTableLayer(_flow.State, null);
            }).ExecuteLater(50);
        }

        CardTableLayout BuildCardTableLayout(GameState state)
        {
            var layout = new CardTableLayout();
            AddCardTableSlots(layout, state.MyPlayerId, _selfSeat.CardRow, true);

            var opponentIndices = BuildOpponentIndices(state);
            for (int i = 0; i < opponentIndices.Count && i < _opponentSeats.Length; i++)
            {
                var player = state.Players[opponentIndices[i]];
                AddCardTableSlots(layout, player.PlayerId, _opponentSeats[i].CardRow, false);
            }

            var drawBounds = GetPileCardBounds(_drawPile);
            var discardBounds = GetPileCardBounds(_discardPile);
            layout.DrawPilePosition = WorldBoundsToOverlayPosition(drawBounds);
            layout.DrawPileSize = BoundsSize(drawBounds);
            layout.DiscardPilePosition = WorldBoundsToOverlayPosition(discardBounds);
            layout.DiscardPileSize = BoundsSize(discardBounds);
            layout.DrawPileCaption = $"\u724c\u5e93  {state.DrawPileCount}";
            layout.DiscardPileCaption = $"\u5f03\u724c\u5806  {state.DiscardPileCount}";
            return layout;
        }

        void AddCardTableSlots(CardTableLayout layout, long playerId, VisualElement row, bool isSelf)
        {
            if (row == null)
                return;

            for (int slot = 0; slot < row.childCount; slot++)
            {
                var bounds = row[slot]?.worldBound ?? Rect.zero;
                if (!HasUsableBounds(bounds))
                    continue;

                bool faceUp = false;
                int value = 0;
                bool selected = false;
                bool clickable = false;
                Action clicked = null;
                if (_flow.State.TryGetVisibleCardValue(playerId, slot, out int visibleValue))
                {
                    faceUp = true;
                    value = visibleValue;
                }

                if (isSelf && slot >= 0 && slot < _flow.State.MyCards.Count)
                {
                    selected = _selectedOwnSlots.Contains(slot);
                    clickable = IsOwnSlotClickable(_flow.SubState);
                    int ownSlot = slot;
                    clicked = () => OnOwnSlotClicked(ownSlot);
                }
                else if (!isSelf)
                {
                    bool playerSelected = _selectedOpponentPlayerId == playerId;
                    selected = playerSelected && (_selectedOpponentSlot < 0 || _selectedOpponentSlot == slot);
                    clickable = IsOpponentSlotClickable(_flow.SubState, playerId);
                    int targetSlot = slot;
                    long targetPlayerId = playerId;
                    clicked = () => OnOpponentSlotClicked(targetPlayerId, targetSlot);
                }

                layout.Slots.Add(new CardTableSlotLayout
                {
                    PlayerId = playerId,
                    SlotIndex = slot,
                    AnchoredPosition = WorldBoundsToOverlayPosition(bounds),
                    Size = BoundsSize(bounds),
                    FaceUp = faceUp,
                    Value = value,
                    Selected = selected,
                    Clickable = clickable,
                    Clicked = clicked
                });
            }
        }

        CardTableActionSnapshot ToCardTableAction(ActionAnimationSnapshot action)
        {
            if (action == null)
                return null;

            var drawTarget = GetDrawRevealTarget(action.SourcePlayerId, fallbackToSeat: true);
            var inspectionTarget = action.Skill == SkillType.PeekSelf
                ? drawTarget.position
                : action.Skill == SkillType.Spy
                    ? GetCenterInspectionTarget()
                    : GetPlayerInspectionCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            var snapshot = new CardTableActionSnapshot
            {
                Sequence = action.Sequence,
                ActionType = action.ActionType,
                Skill = action.Skill,
                SourcePlayerId = action.SourcePlayerId,
                TargetPlayerId = action.TargetPlayerId,
                SourceSlot = action.SourceSlot,
                TargetSlot = action.TargetSlot,
                SwapOccurred = action.SwapOccurred,
                ExchangeSucceeded = action.ExchangeSucceeded,
                IncomingCardValue = action.IncomingCardValue,
                DiscardTopValue = action.DiscardTopValue,
                DrewExtraPenaltyCard = action.DrewExtraPenaltyCard,
                SourcePlayerPosition = WorldBoundsToOverlayPosition(action.SourcePlayerBounds),
                TargetPlayerPosition = WorldBoundsToOverlayPosition(action.TargetPlayerBounds),
                DrawPilePosition = WorldBoundsToOverlayPosition(action.DrawPileBounds),
                DrawPileSize = BoundsSize(action.DrawPileBounds),
                DiscardPilePosition = WorldBoundsToOverlayPosition(action.DiscardPileBounds),
                DiscardPileSize = BoundsSize(action.DiscardPileBounds),
                SourceInspectionPosition = inspectionTarget,
                TargetInspectionPosition = drawTarget.position,
                TargetInspectionSize = drawTarget.size,
                PeekedValue = action.PeekedValue
            };

            snapshot.SelectedSlots.AddRange(action.SelectedSlots);
            AddCardTableSnapshots(snapshot.SourceHand, action.SourceHandBounds);
            AddCardTableSnapshots(snapshot.FinalSourceHand, action.FinalSourceHandBounds);
            AddCardTableSnapshots(snapshot.SourceSlots, action.SourceSlotBounds);
            AddCardTableSnapshots(snapshot.SourceSwapSlots, action.SourceSwapSlotBounds);
            AddCardTableSnapshots(snapshot.TargetSlots, action.TargetSlotBounds);
            return snapshot;
        }

        void AddCardTableSnapshots(List<CardTableSlotSnapshot> target, List<SlotSnapshot> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var slot = source[i];
                target.Add(new CardTableSlotSnapshot
                {
                    PlayerId = slot.PlayerId,
                    SlotIndex = slot.Slot,
                    AnchoredPosition = WorldBoundsToOverlayPosition(slot.Bounds),
                    Size = BoundsSize(slot.Bounds),
                    FaceUp = slot.FaceUp,
                    Value = slot.Value
                });
            }
        }

        Vector2 WorldBoundsToOverlayPosition(Rect bounds)
        {
            if (!HasUsableBounds(bounds))
                return Vector2.zero;

            var rootBounds = _root?.worldBound ?? Rect.zero;
            if (!HasUsableBounds(rootBounds))
                return Vector2.zero;

            float overlayWidth = Screen.width > 1 ? Screen.width : rootBounds.width;
            float overlayHeight = Screen.height > 1 ? Screen.height : rootBounds.height;
            float scaleX = overlayWidth / rootBounds.width;
            float scaleY = overlayHeight / rootBounds.height;
            float centerX = bounds.x + bounds.width * 0.5f - rootBounds.x;
            float centerY = bounds.y + bounds.height * 0.5f - rootBounds.y;
            float x = (centerX - rootBounds.width * 0.5f) * scaleX;
            float y = (rootBounds.height * 0.5f - centerY) * scaleY;
            return new Vector2(x, y);
        }

        (Vector2 position, Vector2 size) GetDrawRevealTarget(long playerId, bool fallbackToSeat = false)
        {
            var row = GetCardRow(playerId);
            var rowBounds = row?.worldBound ?? Rect.zero;
            var seat = GetSeatRoot(playerId);
            var seatBounds = seat?.worldBound ?? Rect.zero;
            if (HasUsableBounds(rowBounds))
            {
                var size = GetOverlayCardSizeFromRow(row, playerId);
                if (playerId == _flow.State.MyPlayerId)
                {
                    if (HasUsableBounds(seatBounds))
                    {
                        var seatCenter = WorldBoundsToOverlayPosition(seatBounds);
                        var seatSize = BoundsSize(seatBounds);
                        float rightInset = Mathf.Max(size.x * 0.72f, seatSize.x * 0.11f);
                        float bottomInset = Mathf.Max(size.y * 0.98f, seatSize.y * 0.23f);
                        var selfPos = seatCenter + new Vector2(seatSize.x * 0.5f - rightInset, -(seatSize.y * 0.5f - bottomInset));
                        if (IsFinite(selfPos.x) && IsFinite(selfPos.y))
                            return (selfPos, size);
                    }
                }

                var rowPos = WorldBoundsToOverlayPosition(rowBounds);
                float xBias = Mathf.Clamp(rowBounds.width * 0.16f, 24f, 88f);
                float yBias = Mathf.Clamp(rowBounds.height * 0.18f, 12f, 28f);
                var pos = rowPos + new Vector2(xBias, yBias);
                if (IsFinite(pos.x) && IsFinite(pos.y))
                    return (pos, size);
            }

            if (HasUsableBounds(seatBounds))
            {
                var seatPos = WorldBoundsToOverlayPosition(seatBounds);
                var size = GetOverlayCardSizeFromRow(row, playerId);
                var pos = playerId == _flow.State.MyPlayerId
                    ? seatPos + new Vector2(Mathf.Clamp(seatBounds.width * 0.30f, 84f, 176f), Mathf.Clamp(seatBounds.height * 0.22f, 34f, 76f))
                    : seatPos + new Vector2(Mathf.Clamp(seatBounds.width * 0.28f, 36f, 120f), Mathf.Clamp(seatBounds.height * 0.18f, 12f, 28f));
                if (IsFinite(pos.x) && IsFinite(pos.y))
                    return (pos, size);
            }

            if (fallbackToSeat)
            {
                if (row != null && row.childCount > 0)
                {
                    int fallbackIndex = Mathf.Clamp(row.childCount - 1, 0, row.childCount - 1);
                    var fallback = row[fallbackIndex]?.worldBound ?? Rect.zero;
                    if (HasUsableBounds(fallback))
                    {
                        var size = BoundsSize(fallback);
                        var pos = WorldBoundsToOverlayPosition(fallback) + new Vector2(Mathf.Clamp(fallback.width * 0.24f, 22f, 86f), Mathf.Clamp(fallback.height * 0.16f, 10f, 24f));
                        if (IsFinite(pos.x) && IsFinite(pos.y))
                            return (pos, size);
                    }
                }
            }

            return (Vector2.zero, Vector2.zero);
        }

        Vector2 GetCenterInspectionTarget()
        {
            var actionBounds = _actionPanel?.worldBound ?? Rect.zero;
            if (HasUsableBounds(actionBounds))
                return WorldBoundsToOverlayPosition(actionBounds);

            var centerBounds = _centerTable?.worldBound ?? Rect.zero;
            if (!HasUsableBounds(centerBounds))
                return Vector2.zero;

            var center = WorldBoundsToOverlayPosition(centerBounds);
            var size = BoundsSize(centerBounds);
            return center + new Vector2(0f, -Mathf.Min(48f, size.y * 0.20f));
        }

        static bool ShouldHideActionPanelDuringAction(ActionAnimationSnapshot action)
        {
            return action != null
                && action.ActionType == ActionType.UseSkill
                && (action.Skill == SkillType.PeekSelf || action.Skill == SkillType.Spy);
        }

        Vector2 GetOverlayCardSizeFromRow(VisualElement row, long playerId)
        {
            if (row != null && row.childCount > 0)
            {
                var bounds = row[0]?.worldBound ?? Rect.zero;
                if (HasUsableBounds(bounds))
                    return BoundsSize(bounds);
            }

            return CssSizeToOverlaySize(playerId == _flow.State.MyPlayerId
                ? new Vector2(SelfCardWidth, SelfCardHeight)
                : new Vector2(OppCardWidth, OppCardHeight));
        }

        Vector2 CssSizeToOverlaySize(Vector2 cssSize)
        {
            var rootBounds = _root?.worldBound ?? Rect.zero;
            if (!HasUsableBounds(rootBounds))
                return cssSize;

            float overlayWidth = Screen.width > 1 ? Screen.width : rootBounds.width;
            float overlayHeight = Screen.height > 1 ? Screen.height : rootBounds.height;
            return new Vector2(cssSize.x * overlayWidth / rootBounds.width, cssSize.y * overlayHeight / rootBounds.height);
        }

        Vector2 BoundsSize(Rect bounds)
        {
            if (!HasUsableBounds(bounds))
                return Vector2.zero;

            var rootBounds = _root?.worldBound ?? Rect.zero;
            if (!HasUsableBounds(rootBounds))
                return new Vector2(bounds.width, bounds.height);

            float overlayWidth = Screen.width > 1 ? Screen.width : rootBounds.width;
            float overlayHeight = Screen.height > 1 ? Screen.height : rootBounds.height;
            float scaleX = overlayWidth / rootBounds.width;
            float scaleY = overlayHeight / rootBounds.height;
            return new Vector2(bounds.width * scaleX, bounds.height * scaleY);
        }

        static bool HasUsableBounds(Rect bounds)
        {
            return bounds.width > 1
                && bounds.height > 1
                && IsFinite(bounds.x)
                && IsFinite(bounds.y)
                && IsFinite(bounds.width)
                && IsFinite(bounds.height);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        void EnqueueActionAnimation(ActionAnimationSnapshot action)
        {
            float now = Time.realtimeSinceStartup;
            float delay = Mathf.Max(0f, _animationQueueUntil - now);
            float duration = EstimateActionAnimationDuration(action);
            float settleDelay = delay <= 0.001f ? PlaybackLayoutSettleDelay : 0f;
            bool holdDiscard = ShouldHoldDiscardVisual(action);
            if (holdDiscard && !_discardVisualHoldActive && delay <= 0.001f)
                BeginDiscardVisualHold(action);

            _animationQueueUntil = now + delay + settleDelay + duration;
            HideAuthoritativeCardsForAnimation(action, delay + settleDelay + duration);
            HoldTurnDisplay(action, _animationQueueUntil);
            ScheduleAnimationAfter(delay + settleDelay, () =>
            {
                if (holdDiscard)
                {
                    BeginDiscardVisualHold(action);
                    if (ShouldPopDiscardVisualAtStart(action))
                        PopHeldDiscardVisual(action);
                }
                PlayActionAnimation(action);
            });
            if (holdDiscard)
            {
                float revealDelay = GetDiscardVisualRevealDelay(action);
                if (revealDelay >= 0f)
                    ScheduleAnimationAfter(delay + settleDelay + revealDelay,
                        () => PushHeldDiscardVisual(action, action.DiscardTopValue, action.DiscardPileCount));
                ScheduleAfter(delay + settleDelay + duration, () => ReleaseDiscardVisualHold(action));
            }
            ScheduleAfter(delay + settleDelay + duration, () => ReleaseTurnDisplay(action));
        }

        bool IsActionAnimationHoldActive()
        {
            return _heldActionSourcePlayerId > 0 && Time.realtimeSinceStartup < _heldActionUntil;
        }

        void HoldTurnDisplay(ActionAnimationSnapshot action, float until)
        {
            if (action == null || action.SourcePlayerId <= 0 || action.ActionType == ActionType.Unknown)
                return;

            _heldActionSequence = action.Sequence;
            _heldActionSourcePlayerId = action.SourcePlayerId;
            _heldActionUntil = until;
        }

        void ReleaseTurnDisplay(ActionAnimationSnapshot action)
        {
            if (action == null || _heldActionSequence != action.Sequence)
                return;

            _heldActionSourcePlayerId = 0;
            _heldActionUntil = 0f;
            _animationQueueDrained?.Invoke();
        }

        void ClearTransientAnimationState()
        {
            _animationGeneration++;
            _drawnCardMarkers.Clear();
            _pulseVersions.Clear();
            _hiddenCardUntil.Clear();
            _pendingSelfExchangeSnapshot = null;
            _pendingSelfSwapSnapshot = null;
            _cardTableRefreshQueued = false;
            _discardVisualHoldActive = false;
            _heldDiscardTopValue = int.MinValue;
            _heldDiscardPileCount = -1;
            _heldDiscardSequence = 0;
            _uiActionQueued = false;
            _animationLayer.Clear();
            _cardTableView.ClearTransient();
            _inspectionActive = false;
            _inspectionEndsAt = 0f;
            HideInspectionZone();
            if (_temporaryHiddenCard != null && _temporaryHiddenCard.panel != null)
                _temporaryHiddenCard.style.visibility = Visibility.Visible;
            _temporaryHiddenCard = null;
            _animationQueueUntil = 0f;
            _heldActionSequence = 0;
            _stableDiscardTopValue = int.MinValue;
            _stableDiscardPileCount = -1;
            _heldActionSourcePlayerId = 0;
            _heldActionUntil = 0f;
            _hideActionPanelDuringTransient = false;
            _lastLocalDrawSequence = 0;
        }

        void PlayDrawAnimation(int value)
        {
            long sequence = _flow.State.DrawResponseSequence;
            if (sequence != 0 && sequence <= _lastLocalDrawSequence)
                return;

            var drawStart = GetPileCardBounds(_drawPile);
            if (drawStart.width <= 1 || drawStart.height <= 1)
                return;

            var target = GetDrawRevealTarget(_flow.State.MyPlayerId);
            if (target.position == Vector2.zero || target.size == Vector2.zero)
                target = GetDrawRevealTarget(_flow.State.MyPlayerId, fallbackToSeat: true);
            if (target.position == Vector2.zero || target.size == Vector2.zero)
                return;

            if (_cardTableView.PlayLocalDraw(value, target.position, target.size))
                _lastLocalDrawSequence = sequence;
        }

        void PlayActionAnimation(ActionAnimationSnapshot action)
        {
            // For exchange actions, capture FinalSourceHandBounds now after layout settle delay
            // This ensures UI Toolkit has completed layout calculations for the new hand size
            if ((action.ActionType == ActionType.ReplaceWithDrawn || action.ActionType == ActionType.TakeFromDiscard)
                && action.FinalSourceHandBounds.Count == 0)
            {
                Debug.Log($"[PlayActionAnimation] Capturing FinalSourceHandBounds for {action.ActionType}, before capture count={action.FinalSourceHandBounds.Count}");
                action.FinalSourceHandBounds.Clear();
                action.FinalSourceHandBounds.AddRange(CaptureAllSlotBounds(action.SourcePlayerId));
                Debug.Log($"[PlayActionAnimation] After capture: FinalSourceHandBounds.Count={action.FinalSourceHandBounds.Count}");
                BuildExchangeMotionPlan(action);
            }

            if (_cardTableView.PlayAction(ToCardTableAction(action)))
                return;

            switch (action.ActionType)
            {
                case ActionType.Draw:
                    PlayDrawAction(action);
                    break;
                case ActionType.DiscardDrawn:
                    PlayDiscardDrawnAction(action);
                    break;
                case ActionType.ReplaceWithDrawn:
                    PlayReplaceWithDrawnAction(action);
                    break;
                case ActionType.TakeFromDiscard:
                    PlayTakeFromDiscardAction(action);
                    break;
                case ActionType.UseSkill:
                    PlaySkillAnimation(action);
                    break;
                case ActionType.CallSteady:
                    PlayCaboCallAnimation(action.SourcePlayerId);
                    break;
            }
        }

        void PlayDrawAction(ActionAnimationSnapshot action)
        {
            if (_cardTableView.PlayAction(ToCardTableAction(action)))
                return;

            var color = UITheme.SkillPeek;
            var start = CenterOf(action.DrawPileBounds);
            if (start == Vector2.zero)
                start = CenterOf(GetPileCardBounds(_drawPile));
            var end = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            if (end == Vector2.zero)
                end = CenterOf(action.SourcePlayerBounds);

            RemoveDrawnMarker(action.SourcePlayerId);
            PlayMovingCard(start, end, false, 0, color, QuickMoveDuration, false,
                () => ShowDrawnMarker(action.SourcePlayerId, end, color));
            PulsePlayer(action.SourcePlayerId, color, QuickMoveDuration + AnimationSettleDuration);
        }

        void PlayDiscardDrawnAction(ActionAnimationSnapshot action)
        {
            var color = GetSkillColor(action.Skill);
            var start = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            var discardBounds = GetValidBounds(action.DiscardPileBounds, GetPileCardBounds(_discardPile));

            RemoveDrawnMarker(action.SourcePlayerId);
            PlayMovingCardToBounds(start, discardBounds, true, action.DiscardTopValue, color, QuickMoveDuration, null, true);
            PulsePlayer(action.SourcePlayerId, color, QuickMoveDuration + AnimationSettleDuration);
        }

        void PlayReplaceWithDrawnAction(ActionAnimationSnapshot action)
        {
            var color = action.IncomingCardValue >= 0 ? GetFaceColor(action.IncomingCardValue) : UITheme.SkillSwap;
            if (!action.ExchangeSucceeded)
            {
                ClearPendingSelfExchangeSnapshot(action);
                PlayFailedExchangeAction(action, color);
                return;
            }

            var discardBounds = GetValidBounds(action.DiscardPileBounds, GetPileCardBounds(_discardPile));

            float total = GetExchangeSuccessDuration(action);
            HideExchangeAnimatedCards(action, total);
            ShowFrozenExchangeSourceHand(action, color, total);

            for (int i = 0; i < action.SelectedSlotBounds.Count; i++)
            {
                var slot = action.SelectedSlotBounds[i];
                float offset = EmptyOriginHoldDuration + GetDiscardStagger(i, action.SelectedSlotBounds.Count);
                ScheduleAnimationAfter(offset, () =>
                    PlayMovingCardBetweenBounds(slot.Bounds, discardBounds, false, 0, color, QuickMoveDuration, true, null));
            }

            bool revealIncoming = action.SourcePlayerId == _flow.State.MyPlayerId && action.IncomingCardValue >= 0;
            float incomingDelay = GetIncomingDelay(action);
            ScheduleSurvivorMoves(action, color, incomingDelay);
            ScheduleAnimationAfter(incomingDelay, () =>
            {
                var incomingStart = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
                RemoveDrawnMarker(action.SourcePlayerId);
                var incomingTarget = GetIncomingTargetBounds(action);
                PlayMovingCardToBounds(incomingStart, incomingTarget, revealIncoming, action.IncomingCardValue, color, QuickMoveDuration,
                    () => ShowCurrentSlot(action.SourcePlayerId, action.IncomingFinalSlot));
            });
            PulsePlayer(action.SourcePlayerId, color, total);
            ClearPendingSelfExchangeSnapshot(action);
        }

        void PlayTakeFromDiscardAction(ActionAnimationSnapshot action)
        {
            var color = UITheme.TurnHighlight;
            if (!action.ExchangeSucceeded)
            {
                ClearPendingSelfExchangeSnapshot(action);
                PlayFailedExchangeAction(action, color);
                return;
            }

            var discardBounds = GetValidBounds(action.DiscardPileBounds, GetPileCardBounds(_discardPile));
            var discardCenter = CenterOf(discardBounds);

            float outgoingTail = GetDiscardStagger(action.SelectedSlotBounds.Count - 1, action.SelectedSlotBounds.Count);
            float total = TakeDiscardOutgoingDelay + outgoingTail + QuickMoveDuration + IncomingLandingPause;
            HideExchangeAnimatedCards(action, total);
            ShowFrozenExchangeSourceHand(action, color, total);

            for (int i = 0; i < action.SelectedSlotBounds.Count; i++)
            {
                var slot = action.SelectedSlotBounds[i];
                float offset = TakeDiscardOutgoingDelay + GetDiscardStagger(i, action.SelectedSlotBounds.Count);
                ScheduleAnimationAfter(offset, () =>
                    PlayMovingCardBetweenBounds(slot.Bounds, discardBounds, false, 0, color, QuickMoveDuration, true, null));
            }

            int incomingValue = action.IncomingCardValue >= 0 ? action.IncomingCardValue : action.DiscardTopValue;
            ScheduleSurvivorMoves(action, color, 0f);
            ScheduleAnimationAfter(0f, () =>
            {
                var incomingTarget = GetIncomingTargetBounds(action);
                PlayMovingCardToBounds(discardCenter, incomingTarget, true, incomingValue, color, QuickMoveDuration,
                    () => ShowCurrentSlot(action.SourcePlayerId, action.IncomingFinalSlot));
            });
            PulsePlayer(action.SourcePlayerId, color, total);
            ClearPendingSelfExchangeSnapshot(action);
        }

        void PlayFailedExchangeAction(ActionAnimationSnapshot action, Color color)
        {
            float total = QuickMoveDuration + EmptySlotHoldDuration + AnimationSettleDuration;
            foreach (var slot in action.SelectedSlotBounds.Count > 0 ? action.SelectedSlotBounds : action.SourceSlotBounds)
                ShakeCardAt(slot.Bounds, color, QuickMoveDuration + AnimationSettleDuration);
            if (action.SelectedSlotBounds.Count == 0 && action.SourceSlotBounds.Count == 0)
                PulsePlayer(action.SourcePlayerId, color, QuickMoveDuration + AnimationSettleDuration);

            var start = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            var end = CenterOf(action.SourcePlayerBounds);
            ScheduleAnimationAfter(AnimationSettleDuration, () =>
            {
                var markerStart = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
                RemoveDrawnMarker(action.SourcePlayerId);
                PlayMovingCard(markerStart, end, action.SourcePlayerId == _flow.State.MyPlayerId, action.IncomingCardValue, color, QuickMoveDuration, true, null);
            });

            if (action.DrewExtraPenaltyCard)
            {
                var deck = CenterOf(action.DrawPileBounds);
                ScheduleAnimationAfter(0.70f, () =>
                    PlayMovingCard(deck, end + new Vector2(18f, 0f), false, 0, UITheme.SkillPeek, QuickMoveDuration, true, null));
            }
            PulsePlayer(action.SourcePlayerId, color, total);
        }

        void BuildExchangeMotionPlan(ActionAnimationSnapshot action)
        {
            action.SelectedSlotBounds.Clear();
            action.SurvivorMoves.Clear();
            action.IncomingFinalBounds = Rect.zero;
            action.IncomingFinalSlot = -1;

            var selected = new HashSet<int>(action.SelectedSlots);
            foreach (var slot in action.SourceHandBounds)
            {
                if (selected.Contains(slot.Slot))
                    action.SelectedSlotBounds.Add(slot);
            }

            if (!action.ExchangeSucceeded)
                return;

            if (action.SelectedSlots.Count <= 1)
            {
                int targetSlot = action.SelectedSlots.Count > 0
                    ? action.SelectedSlots[0]
                    : action.SourceSlot;
                action.IncomingFinalBounds = GetFinalSlotBounds(action, targetSlot);
                action.IncomingFinalSlot = targetSlot;
                if (action.IncomingFinalBounds.width <= 1 && action.SelectedSlotBounds.Count > 0)
                    action.IncomingFinalBounds = action.SelectedSlotBounds[0].Bounds;
                return;
            }

            if (action.FinalSourceHandBounds.Count == 0)
                return;

            int survivorIndex = 0;
            foreach (var oldSlot in action.SourceHandBounds)
            {
                if (selected.Contains(oldSlot.Slot))
                    continue;
                if (survivorIndex >= action.FinalSourceHandBounds.Count)
                    break;

                var finalSlot = action.FinalSourceHandBounds[survivorIndex];
                action.SurvivorMoves.Add(new SlotMove
                {
                    PlayerId = action.SourcePlayerId,
                    OldSlot = oldSlot.Slot,
                    NewSlot = finalSlot.Slot,
                    From = oldSlot.Bounds,
                    To = finalSlot.Bounds,
                    FaceUp = oldSlot.FaceUp,
                    Value = oldSlot.Value
                });
                survivorIndex++;
            }

            if (survivorIndex < action.FinalSourceHandBounds.Count)
            {
                action.IncomingFinalBounds = action.FinalSourceHandBounds[survivorIndex].Bounds;
                action.IncomingFinalSlot = action.FinalSourceHandBounds[survivorIndex].Slot;
            }
        }

        Rect GetFinalSlotBounds(ActionAnimationSnapshot action, int slot)
        {
            if (slot < 0)
                return Rect.zero;

            for (int i = 0; i < action.FinalSourceHandBounds.Count; i++)
                if (action.FinalSourceHandBounds[i].Slot == slot)
                    return action.FinalSourceHandBounds[i].Bounds;
            return GetCardBounds(action.SourcePlayerId, slot);
        }

        void ScheduleSurvivorMoves(ActionAnimationSnapshot action, Color color, float baseDelay)
        {
            if (action.SurvivorMoves.Count == 0)
                return;

            for (int i = 0; i < action.SurvivorMoves.Count; i++)
            {
                var move = action.SurvivorMoves[i];
                if (SameCenter(move.From, move.To))
                    continue;

                float delay = baseDelay + i * SurvivorMoveStagger;
                Action playMove = () => PlayMovingCardBetweenBounds(move.From, move.To, move.FaceUp, move.Value, color, SurvivorMoveDuration, false,
                    () => ShowCurrentSlot(move.PlayerId, move.NewSlot));
                ScheduleAnimationAfter(delay, playMove);
            }
        }

        void ShowFrozenExchangeSourceHand(ActionAnimationSnapshot action, Color color, float duration)
        {
            if (action.SourceHandBounds.Count == 0)
                return;

            var selected = new HashSet<int>(action.SelectedSlots);
            foreach (var slot in action.SourceHandBounds)
            {
                if (selected.Contains(slot.Slot))
                    continue;
                ShowStaticCardOverlay(slot.Bounds, slot.FaceUp, slot.Value, color, duration);
            }
        }

        void ShowStaticCardOverlay(Rect bounds, bool faceUp, int value, Color color, float duration)
        {
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
                return;

            int width = Mathf.RoundToInt(Mathf.Clamp(bounds.width, 44f, 70f));
            int height = Mathf.RoundToInt(Mathf.Clamp(bounds.height, 60f, 96f));
            var card = CreateCard(faceUp, value, width, height, false, false, null);
            card.style.position = Position.Absolute;
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            SetBorderColor(card, UITheme.WithAlpha(color, 0.82f));
            PositionAbsolute(card, center, bounds.width, bounds.height);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);
            ScheduleAnimationAfter(duration, () =>
            {
                if (card.panel != null)
                    card.RemoveFromHierarchy();
            });
        }

        void HideAuthoritativeCardsForAnimation(ActionAnimationSnapshot action, float duration)
        {
            if (action == null)
                return;

            if (action.ActionType == ActionType.ReplaceWithDrawn || action.ActionType == ActionType.TakeFromDiscard)
            {
                HideAuthoritativeExchangeLayout(action, duration);
                return;
            }

            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Swap && action.SwapOccurred)
            {
                float swapHideDuration = SwapEmptyHoldDuration + SwapMoveDuration;
                HideCurrentSlot(action.SourcePlayerId, action.SourceSlot, swapHideDuration);
                HideCurrentSlot(action.TargetPlayerId, action.TargetSlot, swapHideDuration);
            }
        }

        void HideExchangeAnimatedCards(ActionAnimationSnapshot action, float duration)
        {
            if (!action.ExchangeSucceeded)
                return;

            foreach (var slot in action.SelectedSlots)
                HideCurrentSlot(action.SourcePlayerId, slot, Mathf.Min(duration, EmptyOriginHoldDuration + QuickMoveDuration));

            foreach (var move in action.SurvivorMoves)
            {
                if (!SameCenter(move.From, move.To))
                    HideCurrentSlot(action.SourcePlayerId, move.NewSlot, duration);
            }

            if (action.IncomingFinalSlot >= 0)
                HideCurrentSlot(action.SourcePlayerId, action.IncomingFinalSlot, duration);
        }

        void HideAuthoritativeExchangeLayout(ActionAnimationSnapshot action, float duration)
        {
            if (!action.ExchangeSucceeded)
                return;

            foreach (var slot in action.SelectedSlots)
                HideCurrentSlot(action.SourcePlayerId, slot, Mathf.Min(duration, EmptyOriginHoldDuration + QuickMoveDuration));

            foreach (var move in action.SurvivorMoves)
            {
                if (!SameCenter(move.From, move.To))
                    HideCurrentSlot(action.SourcePlayerId, move.NewSlot, duration);
            }

            if (action.IncomingFinalSlot >= 0)
                HideCurrentSlot(action.SourcePlayerId, action.IncomingFinalSlot, duration);
        }

        Rect GetIncomingTargetBounds(ActionAnimationSnapshot action)
        {
            if (action.IncomingFinalBounds.width > 1 && action.IncomingFinalBounds.height > 1)
                return action.IncomingFinalBounds;

            if (action.SourceSlotBounds.Count > 0)
                return action.SourceSlotBounds[0].Bounds;

            if (action.SelectedSlots.Count > 0)
            {
                var bounds = GetCardBounds(action.SourcePlayerId, action.SelectedSlots[0]);
                if (bounds.width > 1 && bounds.height > 1)
                    return bounds;
            }

            return action.SourcePlayerBounds;
        }

        float GetIncomingDelay(ActionAnimationSnapshot action)
        {
            float discardPhase = GetDiscardPhaseDuration(action.SelectedSlotBounds.Count);
            return discardPhase + EmptyOriginHoldDuration + EmptySlotHoldDuration;
        }

        float GetExchangeSuccessDuration(ActionAnimationSnapshot action)
        {
            float survivorPhase = action.SurvivorMoves.Count > 0
                ? (action.SurvivorMoves.Count - 1) * SurvivorMoveStagger + SurvivorMoveDuration
                : 0f;
            return GetIncomingDelay(action) + Mathf.Max(QuickMoveDuration, survivorPhase) + IncomingLandingPause;
        }

        void PlaySkillAnimation(ActionAnimationSnapshot action)
        {
            var color = GetSkillColor(action.Skill);
            if (action.Skill == SkillType.Swap && action.SwapOccurred)
            {
                PlaySwapAction(action, color);
                return;
            }

            if (action.Skill == SkillType.Spy)
            {
                int privateValue = action.SourcePlayerId == _flow.State.MyPlayerId ? action.PeekedValue : -1;
                action.PeekedValue = privateValue;
                if (!_cardTableView.PlayAction(ToCardTableAction(action)))
                    PlaySpyCardInspection(action, privateValue, color);
                return;
            }

            if (action.Skill == SkillType.PeekSelf)
            {
                int privateValue = action.SourcePlayerId == _flow.State.MyPlayerId ? GetKnownOwnCardValue(action.SourceSlot, action.PeekedValue) : -1;
                action.PeekedValue = privateValue;
                if (!_cardTableView.PlayAction(ToCardTableAction(action)))
                    PlayPeekSelfInspection(action, privateValue, color);
                return;
            }

            PulsePlayer(action.SourcePlayerId, color);
        }

        void PlaySwapAction(ActionAnimationSnapshot action, Color color)
        {
            var sourceBounds = GetCapturedSlotBounds(action.SourceSwapSlotBounds, action.SourcePlayerId, action.SourceSlot);
            var targetBounds = GetCapturedSlotBounds(action.TargetSlotBounds, action.TargetPlayerId, action.TargetSlot);
            var source = CenterOf(sourceBounds);
            var target = CenterOf(targetBounds);
            if (source == Vector2.zero)
                source = CenterOf(action.SourcePlayerBounds);
            if (target == Vector2.zero)
                target = CenterOf(action.TargetPlayerBounds);
            if (source == Vector2.zero || target == Vector2.zero)
            {
                PulsePlayer(action.SourcePlayerId, color, SwapMoveDuration + EmptySlotHoldDuration);
                PulsePlayer(action.TargetPlayerId, color, SwapMoveDuration + EmptySlotHoldDuration);
                return;
            }

            float total = SwapEmptyHoldDuration + SwapMoveDuration + SwapSettleDuration;
            float hideDuration = SwapEmptyHoldDuration + SwapMoveDuration;
            HideCurrentSlot(action.SourcePlayerId, action.SourceSlot, hideDuration);
            HideCurrentSlot(action.TargetPlayerId, action.TargetSlot, hideDuration);

            ScheduleAnimationAfter(SwapEmptyHoldDuration, () =>
            {
                PlayMovingCardBetweenBounds(sourceBounds, targetBounds, false, 0, color, SwapMoveDuration, false,
                    () => ShowCurrentSlot(action.TargetPlayerId, action.TargetSlot), -24f);
                PlayMovingCardBetweenBounds(targetBounds, sourceBounds, false, 0, color, SwapMoveDuration, false,
                    () => ShowCurrentSlot(action.SourcePlayerId, action.SourceSlot), 24f);
            });

            PulsePlayer(action.SourcePlayerId, color, total);
            PulsePlayer(action.TargetPlayerId, color, total);
            ClearPendingSelfSwapSnapshot(action);
        }

        void PlayFlyCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration)
        {
            PlayMovingCard(start, end, faceUp, value, color, duration, true, null);
        }

        void PlayMovingCardFromBounds(Rect startBounds, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete)
        {
            var start = CenterOf(startBounds);
            if (start == Vector2.zero)
                return;

            float width = Mathf.Clamp(startBounds.width, 44f, 70f);
            float height = Mathf.Clamp(startBounds.height, 60f, 96f);
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, width, height);
        }

        void PlayMovingCardBetweenBounds(Rect startBounds, Rect endBounds, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete, float arcHeight = 0f)
        {
            var start = CenterOf(startBounds);
            var end = CenterOf(endBounds);
            if (start == Vector2.zero || end == Vector2.zero)
                return;

            float startWidth = Mathf.Clamp(startBounds.width, 44f, 70f);
            float startHeight = Mathf.Clamp(startBounds.height, 60f, 96f);
            float endWidth = Mathf.Clamp(endBounds.width, 44f, 70f);
            float endHeight = Mathf.Clamp(endBounds.height, 60f, 96f);
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, startWidth, startHeight, endWidth, endHeight, arcHeight);
        }

        void PlayMovingCardToBounds(Vector2 start, Rect endBounds, bool faceUp, int value, Color color, float duration, Action onComplete, bool fadeOut = false)
        {
            var end = CenterOf(endBounds);
            if (end == Vector2.zero)
                return;

            float startWidth = 46f;
            float startHeight = 62f;
            float endWidth = Mathf.Clamp(endBounds.width, 44f, 70f);
            float endHeight = Mathf.Clamp(endBounds.height, 60f, 96f);
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, startWidth, startHeight, endWidth, endHeight, 0f);
        }

        void PlayMovingCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete)
        {
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, 44f, 60f);
        }

        void PlayMovingCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete, float width, float height)
        {
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, width, height, 0f);
        }

        void PlayMovingCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete, float width, float height, float arcHeight)
        {
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, width, height, width, height, arcHeight);
        }

        void PlayMovingCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete, float startWidth, float startHeight, float endWidth, float endHeight, float arcHeight)
        {
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            if (start == Vector2.zero || end == Vector2.zero || !IsFinite(start) || !IsFinite(end) || !IsFinite(rootOrigin))
                return;

            int generation = _animationGeneration;
            var card = CreateCard(faceUp, value >= 0 ? value : 0, Mathf.RoundToInt(startWidth), Mathf.RoundToInt(startHeight), false, false, null);
            card.style.position = Position.Absolute;
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            SetBorderColor(card, color);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);

            float startedAt = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                if (generation != _animationGeneration)
                {
                    item?.Pause();
                    if (card.panel != null)
                        card.RemoveFromHierarchy();
                    return;
                }

                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                var p = Vector2.Lerp(start, end, eased) - rootOrigin;
                if (Mathf.Abs(arcHeight) > 0.01f)
                    p.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                float width = Mathf.Lerp(startWidth, endWidth, eased);
                float height = Mathf.Lerp(startHeight, endHeight, eased);
                card.style.width = width;
                card.style.height = height;
                card.style.left = p.x - width * 0.5f;
                card.style.top = p.y - height * 0.5f;
                card.style.opacity = fadeOut ? Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.78f) / 0.22f)) : 1f;

                if (t >= 1f)
                {
                    item?.Pause();
                    card.RemoveFromHierarchy();
                    onComplete?.Invoke();
                }
            }).Every(16);
        }

        void PlayPeekSelfInspection(ActionAnimationSnapshot action, int privateValue, Color color)
        {
            if (_cardTableView.PlayAction(ToCardTableAction(action)))
                return;
            PulsePlayer(action.SourcePlayerId, color, FlipRevealDuration);
        }

        void PlaySpyCardInspection(ActionAnimationSnapshot action, int privateValue, Color color)
        {
            if (_cardTableView.PlayAction(ToCardTableAction(action)))
                return;
            PulsePlayer(action.SourcePlayerId, color, FlipRevealDuration);
            PulsePlayer(action.TargetPlayerId, color, FlipRevealDuration);
        }

        void PlayPeekSelfFlip(long playerId, int slot, int privateValue, Color color)
        {
            var bounds = GetCardBounds(playerId, slot);
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
            {
                PulsePlayer(playerId, color, FlipRevealDuration);
                return;
            }

            PulseCard(playerId, slot, color, FlipRevealDuration);
            bool revealValue = privateValue >= 0;
            PlayFlipCardAt(center, revealValue, privateValue, color, "看牌", FlipRevealDuration);
            ShowHeldInspectionCard(center, revealValue, privateValue, color, "看牌", FlipRevealDuration);
        }

        void PlaySpyCardInspection(long sourcePlayerId, long targetPlayerId, int targetSlot, int privateValue, Color color)
        {
            var start = CenterOf(GetCardBounds(targetPlayerId, targetSlot));
            if (start == Vector2.zero)
                start = CenterOf(GetPlayerBounds(targetPlayerId));
            var end = CenterOf(_centerTable.worldBound);
            if (end == Vector2.zero)
                end = CenterOf(_actionPanel.worldBound);
            if (start == Vector2.zero || end == Vector2.zero)
            {
                PulseCard(targetPlayerId, targetSlot, color, FlipRevealDuration);
                PulsePlayer(sourcePlayerId, color, FlipRevealDuration);
                return;
            }

            end = ShowInspectionZone(privateValue >= 0, privateValue, color, "偷看");
            if (end == Vector2.zero)
                end = CenterOf(_centerTable.worldBound);

            var targetCard = GetCardElement(targetPlayerId, targetSlot);
            SetTemporaryCardVisibility(targetCard, false);
            _temporaryHiddenCard = targetCard;
            _inspectionActive = true;
            _inspectionEndsAt = Time.realtimeSinceStartup + SkillMoveDuration + SkillHoldDuration;
            _inspectionReturnStart = end;
            _inspectionReturnEnd = start;
            _inspectionReturnColor = color;
            PulsePlayer(sourcePlayerId, color, SkillMoveDuration + SkillHoldDuration);
            PlayFlyCard(start, end, privateValue >= 0, privateValue, color, SkillMoveDuration);
        }

        void PlayCaboCallAnimation(long sourcePlayerId)
        {
            var color = UITheme.TurnHighlight;
            PulsePlayer(sourcePlayerId, color, CaboCallDuration);

            var bounds = GetPlayerBounds(sourcePlayerId);
            var center = CenterOf(bounds);
            if (center == Vector2.zero) return;

            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            var banner = new Label("CABO");
            banner.style.position = Position.Absolute;
            banner.style.width = 104;
            banner.style.height = 34;
            banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            banner.style.unityFontStyleAndWeight = FontStyle.Bold;
            banner.style.fontSize = 22;
            banner.style.color = UITheme.TextOnAccent;
            banner.style.backgroundColor = color;
            banner.style.borderTopLeftRadius = 6;
            banner.style.borderTopRightRadius = 6;
            banner.style.borderBottomLeftRadius = 6;
            banner.style.borderBottomRightRadius = 6;
            _animationLayer.Add(banner);

            float startedAt = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / CaboCallDuration);
                float lift = Mathf.Sin(t * Mathf.PI) * 18f;
                var p = center - rootOrigin + new Vector2(0f, -bounds.height * 0.42f - lift);
                banner.style.left = p.x - 52f;
                banner.style.top = p.y - 17f;
                banner.style.opacity = Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.82f) / 0.18f));

                if (t >= 1f)
                {
                    item?.Pause();
                    banner.RemoveFromHierarchy();
                }
            }).Every(16);
        }

        void PlayFlipCardAt(Vector2 center, bool revealValue, int value, Color color, string caption, float duration)
        {
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            const int width = 52;
            const int height = 72;
            var card = CreateFloatingCard(false, 0, width, height, color, caption);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);

            int phase = 0;
            float startedAt = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
                if (phase == 0 && t >= 0.18f)
                {
                    phase = 1;
                    ReplaceFloatingCardContent(card, revealValue, value, width, height, color, caption);
                }
                else if (phase == 1 && t >= 0.78f)
                {
                    phase = 2;
                    ReplaceFloatingCardContent(card, false, 0, width, height, color, caption);
                }

                float fold;
                if (t < 0.18f) fold = Mathf.Lerp(1f, 0.12f, t / 0.18f);
                else if (t < 0.32f) fold = Mathf.Lerp(0.12f, 1f, (t - 0.18f) / 0.14f);
                else if (t < 0.78f) fold = 1f;
                else if (t < 0.88f) fold = Mathf.Lerp(1f, 0.12f, (t - 0.78f) / 0.10f);
                else fold = Mathf.Lerp(0.12f, 1f, (t - 0.88f) / 0.12f);

                PositionFloating(card, center, width * fold, height, rootOrigin);
                card.style.opacity = Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.92f) / 0.08f));

                if (t >= 1f)
                {
                    item?.Pause();
                    card.RemoveFromHierarchy();
                }
            }).Every(16);
        }

        void PlayInspectCardRoundTrip(Vector2 start, Vector2 inspectAt, bool revealValue, int value, Color color, string caption)
        {
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            const int width = 56;
            const int height = 78;
            var card = CreateFloatingCard(revealValue, value, width, height, color, caption);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);

            float startedAt = Time.realtimeSinceStartup;
            float total = SkillMoveDuration + SkillHoldDuration + SkillMoveDuration;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                float elapsed = Time.realtimeSinceStartup - startedAt;
                Vector2 p;
                if (elapsed < SkillMoveDuration)
                {
                    float t = Mathf.Clamp01(elapsed / SkillMoveDuration);
                    p = Vector2.Lerp(start, inspectAt, 1f - Mathf.Pow(1f - t, 3f));
                }
                else if (elapsed < SkillMoveDuration + SkillHoldDuration)
                {
                    p = inspectAt;
                }
                else
                {
                    float t = Mathf.Clamp01((elapsed - SkillMoveDuration - SkillHoldDuration) / SkillMoveDuration);
                    p = Vector2.Lerp(inspectAt, start, t * t * (3f - 2f * t));
                }

                float scale = elapsed < SkillMoveDuration + SkillHoldDuration ? 1.14f : Mathf.Lerp(1.14f, 1f, Mathf.Clamp01((elapsed - SkillMoveDuration - SkillHoldDuration) / SkillMoveDuration));
                PositionFloating(card, p, width * scale, height * scale, rootOrigin);
                card.style.opacity = Mathf.Lerp(1f, 0f, Mathf.Max(0f, (elapsed - total + 0.22f) / 0.22f));

                if (elapsed >= total)
                {
                    item?.Pause();
                    card.RemoveFromHierarchy();
                }
            }).Every(16);
        }

        void ShowHeldInspectionCard(Vector2 center, bool faceUp, int value, Color color, string caption, float duration)
        {
            if (center == Vector2.zero) return;
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            const int width = 58;
            const int height = 80;
            var card = CreateFloatingCard(faceUp, value, width, height, color, caption);
            PositionFloating(card, center, width, height, rootOrigin);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);

            ScheduleAfter(duration, () => card.RemoveFromHierarchy());
        }

        Vector2 ShowInspectionZone(bool faceUp, int value, Color color, string caption)
        {
            _inspectionZone.Clear();
            _inspectionZone.style.display = DisplayStyle.Flex;
            SetBorderColor(_inspectionZone, color);

            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.alignItems = Align.Center;
            wrap.style.justifyContent = Justify.Center;

            var card = CreateCard(faceUp, value >= 0 ? value : 0, TableCardWidth, TableCardHeight, false, false, null);
            SetBorderColor(card, color);
            wrap.Add(card);

            var label = new Label(caption == "偷看" ? "查看卡牌" : caption);
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = color;
            label.style.marginTop = 4;
            wrap.Add(label);

            _inspectionZone.Add(wrap);

            var center = CenterOf(_inspectionZone.worldBound);
            if (center == Vector2.zero)
                center = CenterOf(_centerTable.worldBound);
            return center;
        }

        void HideInspectionZone()
        {
            _inspectionZone.Clear();
            _inspectionZone.style.display = DisplayStyle.None;
            SetBorderColor(_inspectionZone, UITheme.PanelBorder);
        }

        void SetTemporaryCardVisibility(VisualElement card, bool visible)
        {
            if (card == null) return;
            card.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        List<SlotSnapshot> CaptureSlotBounds(long playerId, IEnumerable<int> slots, bool preferRenderedFace = false)
        {
            var result = new List<SlotSnapshot>();
            if (slots == null)
                return result;

            foreach (var slot in slots)
            {
                if (slot < 0)
                    continue;
                var element = GetCardElement(playerId, slot);
                var bounds = element?.worldBound ?? Rect.zero;
                if (bounds.width > 1 && bounds.height > 1)
                {
                    ResolveCardFace(playerId, slot, preferRenderedFace, out bool faceUp, out int value);
                    result.Add(new SlotSnapshot { PlayerId = playerId, Slot = slot, Bounds = bounds, FaceUp = faceUp, Value = value });
                }
            }
            return result;
        }

        List<SlotSnapshot> CaptureAllSlotBounds(long playerId, bool preferRenderedFace = false)
        {
            var result = new List<SlotSnapshot>();
            var row = GetCardRow(playerId);
            if (row == null)
                return result;

            Debug.Log($"[CaptureAllSlotBounds] playerId={playerId}, row.childCount={row.childCount}");
            for (int slot = 0; slot < row.childCount; slot++)
            {
                var bounds = row[slot]?.worldBound ?? Rect.zero;
                Debug.Log($"  slot[{slot}]: bounds={bounds}");
                if (bounds.width > 1 && bounds.height > 1)
                {
                    ResolveCardFace(playerId, slot, preferRenderedFace, out bool faceUp, out int value);
                    result.Add(new SlotSnapshot { PlayerId = playerId, Slot = slot, Bounds = bounds, FaceUp = faceUp, Value = value });
                }
            }
            Debug.Log($"[CaptureAllSlotBounds] result.Count={result.Count}");
            return result;
        }

        void ResolveCardFace(long playerId, int slot, bool preferRenderedFace, out bool faceUp, out int value)
        {
            if (preferRenderedFace
                && _cardTableView != null
                && _cardTableView.TryGetCardFace(playerId, slot, out faceUp, out value))
            {
                return;
            }

            faceUp = _flow.State.TryGetVisibleCardValue(playerId, slot, out value);
        }

        float EstimateActionAnimationDuration(ActionAnimationSnapshot action)
        {
            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Spy)
                return SkillMoveDuration + SkillHoldDuration + SkillMoveDuration + EmptySlotHoldDuration;
            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.PeekSelf)
            {
                if (action.SourcePlayerId == _flow.State.MyPlayerId)
                    return FlipRevealDuration;
                return SkillMoveDuration + SkillHoldDuration + SkillMoveDuration + EmptySlotHoldDuration;
            }
            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Swap && action.SwapOccurred)
                return SwapEmptyHoldDuration + SwapMoveDuration + SwapSettleDuration;
            if (action.ActionType == ActionType.CallSteady)
                return CaboCallDuration;
            if (action.ActionType == ActionType.ReplaceWithDrawn || action.ActionType == ActionType.TakeFromDiscard)
            {
                if (!action.ExchangeSucceeded)
                    return QuickMoveDuration + EmptySlotHoldDuration + AnimationSettleDuration;
                if (action.ActionType == ActionType.TakeFromDiscard)
                    return QuickMoveDuration
                        + GetDiscardStagger(action.SelectedSlotBounds.Count - 1, action.SelectedSlotBounds.Count)
                        + TakeDiscardOutgoingDelay
                        + IncomingLandingPause;
                return GetExchangeSuccessDuration(action);
            }
            return QuickMoveDuration + AnimationSettleDuration;
        }

        float GetDiscardPhaseDuration(int cardCount)
        {
            if (cardCount <= 1)
                return QuickMoveDuration;
            return QuickMoveDuration + GetDiscardStagger(cardCount - 1, cardCount);
        }

        float GetDiscardStagger(int index, int cardCount)
        {
            if (cardCount <= 1)
                return 0f;
            return index * 0.20f;
        }

        Vector2 GetDrawnMarkerCenter(long playerId, Rect fallbackBounds)
        {
            if (_drawnCardMarkers.TryGetValue(playerId, out var marker) && marker != null && marker.panel != null)
                return CenterOf(marker.worldBound);

            var bounds = fallbackBounds.width > 1 ? fallbackBounds : GetPlayerBounds(playerId);
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
                return Vector2.zero;
            return center + new Vector2(bounds.width * 0.30f, -Mathf.Min(36f, bounds.height * 0.22f));
        }

        Vector2 GetPlayerInspectionCenter(long playerId, Rect fallbackBounds)
        {
            var bounds = fallbackBounds.width > 1 ? fallbackBounds : GetPlayerBounds(playerId);
            if (!HasUsableBounds(bounds))
                return Vector2.zero;

            var center = WorldBoundsToOverlayPosition(bounds);
            if (center == Vector2.zero)
                return Vector2.zero;

            var rootBounds = _root?.worldBound ?? Rect.zero;
            float scaleY = HasUsableBounds(rootBounds) && rootBounds.height > 0.01f
                ? (Screen.height > 1 ? Screen.height : rootBounds.height) / rootBounds.height
                : 1f;

            float yOffset = Mathf.Min(34f, bounds.height * 0.30f) * scaleY;
            if (playerId == _flow.State.MyPlayerId)
                yOffset = Mathf.Min(48f, bounds.height * 0.34f) * scaleY;
            return center + new Vector2(0f, yOffset);
        }

        void ShowDrawnMarker(long playerId, Vector2 center, Color color)
        {
            if (center == Vector2.zero)
                return;

            RemoveDrawnMarker(playerId);
            const int width = 46;
            const int height = 62;
            var marker = CreateCard(false, 0, width, height, false, false, null);
            marker.name = $"DrawnMarker-{playerId}";
            marker.style.position = Position.Absolute;
            marker.style.marginLeft = 0;
            marker.style.marginRight = 0;
            marker.style.marginTop = 0;
            marker.style.marginBottom = 0;
            SetBorderColor(marker, color);
            PositionAbsolute(marker, center, width, height);
            _animationLayer.BringToFront();
            _animationLayer.Add(marker);
            _drawnCardMarkers[playerId] = marker;
        }

        void RemoveDrawnMarker(long playerId)
        {
            if (!_drawnCardMarkers.TryGetValue(playerId, out var marker))
                return;

            if (marker != null && marker.panel != null)
                marker.RemoveFromHierarchy();
            _drawnCardMarkers.Remove(playerId);
        }

        void HideCurrentSlot(long playerId, int slot, float duration)
        {
            var element = GetCardElement(playerId, slot);
            if (element == null)
                return;
            float showAt = Time.realtimeSinceStartup + Mathf.Max(0f, duration);
            if (_hiddenCardUntil.TryGetValue(element, out var existingShowAt) && existingShowAt > showAt)
                showAt = existingShowAt;
            _hiddenCardUntil[element] = showAt;
            element.style.visibility = Visibility.Hidden;
            ScheduleAnimationAfter(duration, () =>
            {
                if (element.panel == null)
                    return;
                if (_hiddenCardUntil.TryGetValue(element, out var hiddenUntil) && Time.realtimeSinceStartup + 0.02f < hiddenUntil)
                    return;
                _hiddenCardUntil.Remove(element);
                element.style.visibility = Visibility.Visible;
            });
        }

        void ShowCurrentSlot(long playerId, int slot)
        {
            var element = GetCardElement(playerId, slot);
            if (element == null)
                return;
            _hiddenCardUntil.Remove(element);
            element.style.visibility = Visibility.Visible;
        }

        Vector2 GetPrimarySelectedSlotCenter(ActionAnimationSnapshot action)
        {
            if (action.SourceSlotBounds.Count > 0)
                return CenterOf(action.SourceSlotBounds[0].Bounds);
            if (action.SelectedSlots.Count > 0)
                return CenterOf(GetCardBounds(action.SourcePlayerId, action.SelectedSlots[0]));
            return CenterOf(action.SourcePlayerBounds);
        }

        Rect GetCapturedSlotBounds(List<SlotSnapshot> slots, long playerId, int slot)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].PlayerId == playerId && slots[i].Slot == slot)
                    return slots[i].Bounds;
            return GetCardBounds(playerId, slot);
        }

        void ShakeCardAt(Rect bounds, Color color, float duration)
        {
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
                return;

            const int width = 48;
            const int height = 66;
            var card = CreateCard(false, 0, width, height, false, false, null);
            card.style.position = Position.Absolute;
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            SetBorderColor(card, color);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);

            float startedAt = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
                float offset = Mathf.Sin(t * Mathf.PI * 8f) * Mathf.Lerp(9f, 1f, t);
                PositionAbsolute(card, center + new Vector2(offset, 0f), width, height);
                card.style.opacity = Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.78f) / 0.22f));
                if (t >= 1f)
                {
                    item?.Pause();
                    card.RemoveFromHierarchy();
                }
            }).Every(16);
        }

        void ScheduleAfter(float delaySeconds, Action action)
        {
            int remainingTicks = Mathf.Max(1, Mathf.CeilToInt(delaySeconds / 0.05f));
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                remainingTicks--;
                if (remainingTicks > 0)
                    return;

                item?.Pause();
                action?.Invoke();
            }).Every(50);
        }

        void ScheduleAnimationAfter(float delaySeconds, Action action)
        {
            int generation = _animationGeneration;
            ScheduleAfter(delaySeconds, () =>
            {
                if (generation == _animationGeneration)
                    action?.Invoke();
            });
        }

        void InvokeUiActionNextFrame(Action action)
        {
            if (action == null || _uiActionQueued)
                return;

            if (_root == null || _root.panel == null)
            {
                action();
                return;
            }

            _uiActionQueued = true;
            int generation = _animationGeneration;
            _root.schedule.Execute(() =>
            {
                _uiActionQueued = false;
                if (generation != _animationGeneration || _root == null || _root.panel == null)
                    return;

                action();
            }).ExecuteLater(1);
        }

        VisualElement CreateFloatingCard(bool faceUp, int value, int width, int height, Color color, string caption)
        {
            var wrap = new VisualElement();
            wrap.style.position = Position.Absolute;
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.alignItems = Align.Center;
            wrap.style.justifyContent = Justify.FlexStart;
            wrap.style.overflow = Overflow.Hidden;
            wrap.style.marginLeft = 0;
            wrap.style.marginRight = 0;
            wrap.style.marginTop = 0;
            wrap.style.marginBottom = 0;
            ReplaceFloatingCardContent(wrap, faceUp, value, width, height, color, caption);
            return wrap;
        }

        void ReplaceFloatingCardContent(VisualElement wrap, bool faceUp, int value, int width, int height, Color color, string caption)
        {
            wrap.Clear();
            var card = CreateCard(faceUp, value >= 0 ? value : 0, width, height, false, false, null);
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            SetBorderColor(card, color);
            wrap.Add(card);

            var label = new Label(caption);
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = color;
            label.style.marginTop = 3;
            wrap.Add(label);
        }

        static void PositionFloating(VisualElement element, Vector2 center, float width, float height, Vector2 rootOrigin)
        {
            if (!IsFinite(center) || !IsFinite(rootOrigin) || float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f)
                return;

            var p = center - rootOrigin;
            element.style.width = width;
            element.style.height = height + 18f;
            element.style.left = p.x - width * 0.5f;
            element.style.top = p.y - height * 0.5f;
        }

        void PositionAbsolute(VisualElement element, Vector2 center, float width, float height)
        {
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            if (!IsFinite(center) || !IsFinite(rootOrigin) || width <= 0f || height <= 0f)
                return;

            var p = center - rootOrigin;
            element.style.width = width;
            element.style.height = height;
            element.style.left = p.x - width * 0.5f;
            element.style.top = p.y - height * 0.5f;
        }

        void PulsePlayer(long playerId, Color color, float duration = 0.48f)
        {
            PulseElement(GetSeatRoot(playerId), color, duration);
        }

        void PulseCard(long playerId, int slot, Color color, float duration = 0.9f)
        {
            var element = GetCardElement(playerId, slot);
            if (element != null)
                PulseElement(element, color, duration);
        }

        void PulseElement(VisualElement element, Color color, float duration)
        {
            if (element == null) return;
            var originalTop = element.style.borderTopColor.value;
            var originalRight = element.style.borderRightColor.value;
            var originalBottom = element.style.borderBottomColor.value;
            var originalLeft = element.style.borderLeftColor.value;
            int version = _pulseVersions.TryGetValue(element, out var currentVersion) ? currentVersion + 1 : 1;
            _pulseVersions[element] = version;
            float startedAt = Time.realtimeSinceStartup;

            _root.schedule.Execute(() =>
            {
                if (element.panel == null
                    || !_pulseVersions.TryGetValue(element, out var activeVersion)
                    || activeVersion != version)
                {
                    return;
                }

                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                var current = Color.Lerp(originalTop, color, pulse);
                element.style.borderTopColor = current;
                element.style.borderRightColor = Color.Lerp(originalRight, color, pulse);
                element.style.borderBottomColor = Color.Lerp(originalBottom, color, pulse);
                element.style.borderLeftColor = Color.Lerp(originalLeft, color, pulse);

                if (t >= 1f)
                {
                    _pulseVersions.Remove(element);
                    if (IsSeatRoot(element))
                    {
                        RestoreSeatBorderAndRefreshCards(element);
                    }
                    else
                    {
                        element.style.borderTopColor = originalTop;
                        element.style.borderRightColor = originalRight;
                        element.style.borderBottomColor = originalBottom;
                        element.style.borderLeftColor = originalLeft;
                    }
                }
            }).Every(16).Until(() => Time.realtimeSinceStartup - startedAt >= duration);
        }

        void RestoreSeatBorderAndRefreshCards(VisualElement element)
        {
            if (element == null || _flow.State.Phase != GamePhase.Playing)
                return;

            RenderSeatHeaderBorder(element);
            RefreshCardInteractionLayer();
        }

        void RenderSeatHeaderBorder(VisualElement element)
        {
            var state = _flow.State;
            long visualCurrentPlayerId = IsActionAnimationHoldActive() ? _heldActionSourcePlayerId : state.CurrentPlayerId;
            if (element == _selfSeat.Root)
            {
                ApplySeatRootChrome(element, visualCurrentPlayerId == state.MyPlayerId, state.SteadyCallerId == state.MyPlayerId);
                return;
            }

            var opponentIndices = BuildOpponentIndices(state);
            for (int i = 0; i < _opponentSeats.Length && i < opponentIndices.Count; i++)
            {
                if (element != _opponentSeats[i].Root)
                    continue;

                var player = state.Players[opponentIndices[i]];
                ApplySeatRootChrome(element, visualCurrentPlayerId == player.PlayerId, state.SteadyCallerId == player.PlayerId);
                return;
            }

            ApplySeatRootChrome(element, false, false);
        }

        static void ApplySeatRootChrome(VisualElement element, bool isCurrentTurn, bool isCaboCaller)
        {
            if (element == null)
                return;

            element.style.backgroundColor = isCaboCaller ? UITheme.CaboSurface : UITheme.PanelSurface;
            var borderColor = isCaboCaller ? UITheme.CaboBorder : isCurrentTurn ? UITheme.TurnBorder : UITheme.PanelBorder;
            element.style.borderTopColor = borderColor;
            element.style.borderRightColor = borderColor;
            element.style.borderBottomColor = borderColor;
            element.style.borderLeftColor = borderColor;
            float borderWidth = isCaboCaller || isCurrentTurn ? 3 : 1;
            element.style.borderTopWidth = borderWidth;
            element.style.borderRightWidth = borderWidth;
            element.style.borderBottomWidth = borderWidth;
            element.style.borderLeftWidth = borderWidth;
        }

        bool IsSeatRoot(VisualElement element)
        {
            return element == _selfSeat.Root
                || element == _topSeat.Root
                || element == _leftSeat.Root
                || element == _rightSeat.Root;
        }

        Rect GetPlayerBounds(long playerId)
        {
            var root = GetSeatRoot(playerId);
            return root?.worldBound ?? Rect.zero;
        }

        VisualElement GetSeatRoot(long playerId)
        {
            if (playerId == _flow.State.MyPlayerId)
                return _selfSeat.Root;

            var indices = BuildOpponentIndices(_flow.State);
            for (int i = 0; i < indices.Count && i < _opponentSeats.Length; i++)
            {
                if (_flow.State.Players[indices[i]].PlayerId == playerId)
                    return _opponentSeats[i].Root;
            }
            return null;
        }

        Rect GetCardBounds(long playerId, int slot)
        {
            return GetCardElement(playerId, slot)?.worldBound ?? GetPlayerBounds(playerId);
        }

        Rect GetPileCardBounds(VisualElement pile)
        {
            if (pile == null)
                return Rect.zero;

            var stack = pile.childCount > 0 ? pile[0] : null;
            var card = stack != null && stack.childCount > 0 ? stack[0] : null;
            var bounds = card?.worldBound ?? Rect.zero;
            if (bounds.width > 1 && bounds.height > 1)
                return bounds;
            return pile.worldBound;
        }

        static Rect GetValidBounds(Rect primary, Rect fallback)
        {
            return primary.width > 1 && primary.height > 1 ? primary : fallback;
        }

        VisualElement GetCardElement(long playerId, int slot)
        {
            var row = GetCardRow(playerId);

            if (row == null || slot < 0 || slot >= row.childCount)
                return null;
            return row[slot];
        }

        VisualElement GetCardRow(long playerId)
        {
            if (playerId == _flow.State.MyPlayerId)
                return _selfSeat.CardRow;

            var indices = BuildOpponentIndices(_flow.State);
            for (int i = 0; i < indices.Count && i < _opponentSeats.Length; i++)
            {
                if (_flow.State.Players[indices[i]].PlayerId == playerId)
                    return _opponentSeats[i].CardRow;
            }
            return null;
        }

        int GetKnownOwnCardValue(int slot, int fallbackValue)
        {
            if (slot >= 0 && slot < _flow.State.MyCards.Count && _flow.State.MyCards[slot].IsKnown)
                return _flow.State.MyCards[slot].Value;
            return fallbackValue;
        }

        static Color GetSkillColor(SkillType skill)
        {
            return skill switch
            {
                SkillType.PeekSelf => UITheme.SkillPeek,
                SkillType.Spy => UITheme.SkillSpy,
                SkillType.Swap => UITheme.SkillSwap,
                _ => UITheme.TurnHighlight
            };
        }

        static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }

        static void SetBorderWidth(VisualElement element, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
        }

        static void StyleSocialTab(Button button)
        {
            button.style.flexGrow = 1;
            button.style.minWidth = 0;
            button.style.height = 32;
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.fontSize = 12;
        }

        static void StyleSocialTabState(Button button, bool selected)
        {
            button.style.backgroundColor = selected ? UITheme.SelectedSurface : UITheme.PanelSurfaceAlt;
            button.style.color = UITheme.TextPrimary;
            var border = selected ? UITheme.SelectedBorder : UITheme.PanelBorder;
            button.style.borderTopColor = border;
            button.style.borderRightColor = border;
            button.style.borderBottomColor = border;
            button.style.borderLeftColor = border;
        }

        static Vector2 CenterOf(Rect rect)
        {
            if (rect.width <= 1 || rect.height <= 1)
                return Vector2.zero;
            var center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
            return IsFinite(center) ? center : Vector2.zero;
        }

        static bool IsFinite(Vector2 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.x) || float.IsInfinity(value.y));
        }

        static bool SameCenter(Rect left, Rect right)
        {
            var a = CenterOf(left);
            var b = CenterOf(right);
            return a != Vector2.zero && b != Vector2.zero && Vector2.Distance(a, b) < 0.5f;
        }

        VisualElement CreateCard(bool faceUp, int value, int width, int height, bool selected, bool clickable, Action onClick, bool showSkillBadge = true)
        {
            var card = new VisualElement();
            card.style.width = width;
            card.style.height = height;
            card.style.flexShrink = 0;
            card.style.marginLeft = 4;
            card.style.marginRight = 4;
            card.style.marginTop = 4;
            card.style.marginBottom = 4;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            card.style.borderTopLeftRadius = 7;
            card.style.borderTopRightRadius = 7;
            card.style.borderBottomLeftRadius = 7;
            card.style.borderBottomRightRadius = 7;
            card.style.borderTopWidth = selected ? 4 : 2;
            card.style.borderRightWidth = selected ? 4 : 2;
            card.style.borderBottomWidth = selected ? 4 : 2;
            card.style.borderLeftWidth = selected ? 4 : 2;
            card.style.borderTopColor = selected ? UITheme.SelectedBorder : UITheme.CardBorder;
            card.style.borderRightColor = card.style.borderTopColor.value;
            card.style.borderBottomColor = card.style.borderTopColor.value;
            card.style.borderLeftColor = card.style.borderTopColor.value;
            card.style.backgroundColor = faceUp ? GetFaceColor(value) : UITheme.CardBack;
            card.style.opacity = clickable ? 1f : 0.96f;

            var valueLabel = new Label(faceUp ? value.ToString() : "CABO");
            valueLabel.style.fontSize = faceUp ? Mathf.RoundToInt(height * 0.34f) : Mathf.RoundToInt(height * 0.16f);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            valueLabel.style.color = faceUp ? UITheme.TextPrimary : Color.white;
            card.Add(valueLabel);

            if (showSkillBadge && faceUp && value >= 7 && value <= 12)
            {
                var badge = new Label(GetSkillShortName(value));
                badge.style.fontSize = 10;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.color = UITheme.TextOnAccent;
                card.Add(badge);
            }

            if (clickable && onClick != null)
                card.RegisterCallback<ClickEvent>(_ => InvokeUiActionNextFrame(onClick));

            return card;
        }

        static void StyleCardTablePlaceholder(VisualElement card)
        {
            if (card == null)
                return;

            card.style.opacity = 0f;
            card.style.backgroundColor = Color.clear;
            card.style.borderTopColor = Color.clear;
            card.style.borderRightColor = Color.clear;
            card.style.borderBottomColor = Color.clear;
            card.style.borderLeftColor = Color.clear;
            card.pickingMode = PickingMode.Ignore;
        }

        static void StylePileCardPlaceholder(VisualElement pileVisual)
        {
            if (pileVisual == null || pileVisual.childCount == 0)
                return;

            for (int i = 0; i < pileVisual.childCount; i++)
            {
                var child = pileVisual[i];
                StyleCardTablePlaceholder(child);
                child.style.visibility = Visibility.Hidden;
                child.pickingMode = PickingMode.Ignore;
            }
            pileVisual.pickingMode = PickingMode.Ignore;
        }

        void EnsurePileCard(VisualElement pile, string title, string face, string count, bool faceUp, bool compact, bool placeholder)
        {
            if (pile == null)
                return;

            VisualElement stack;
            if (pile.childCount == 0)
            {
                stack = CreatePileCard(title, face, count, faceUp, compact);
                pile.Add(stack);
            }
            else
            {
                stack = pile[0];
                UpdatePileCard(stack, title, face, count, faceUp, compact);
            }

            pile.pickingMode = placeholder ? PickingMode.Ignore : PickingMode.Position;
            if (placeholder)
                StylePileCardPlaceholder(stack);
            else
                RestorePileCardVisibility(stack);
        }

        VisualElement CreatePileCard(string title, string face, string count, bool faceUp, bool compact = false)
        {
            var stack = new VisualElement();
            stack.style.alignItems = Align.Center;
            stack.style.marginLeft = compact ? 10 : 14;
            stack.style.marginRight = compact ? 10 : 14;

            var card = new VisualElement();
            card.style.width = compact ? 54 : TableCardWidth;
            card.style.height = compact ? 70 : TableCardHeight;
            card.style.flexShrink = 0;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.borderTopWidth = 2;
            card.style.borderRightWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            card.style.borderTopColor = UITheme.CardBorder;
            card.style.borderRightColor = UITheme.CardBorder;
            card.style.borderBottomColor = UITheme.CardBorder;
            card.style.borderLeftColor = UITheme.CardBorder;
            card.style.backgroundColor = faceUp ? UITheme.CardMid : UITheme.CardBack;

            var label = new Label(face);
            label.style.fontSize = compact ? faceUp ? 22 : 11 : faceUp ? 26 : 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = faceUp ? UITheme.TextPrimary : Color.white;
            card.Add(label);
            stack.Add(card);

            var caption = new Label($"{title}  {count}");
            caption.style.fontSize = compact ? 10 : 12;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.style.marginTop = compact ? 3 : 6;
            stack.Add(caption);
            return stack;
        }

        void UpdatePileCard(VisualElement stack, string title, string face, string count, bool faceUp, bool compact)
        {
            if (stack == null)
                return;

            stack.style.alignItems = Align.Center;
            stack.style.marginLeft = compact ? 10 : 14;
            stack.style.marginRight = compact ? 10 : 14;

            var card = stack.childCount > 0 ? stack[0] : null;
            if (card != null)
            {
                card.style.width = compact ? 54 : TableCardWidth;
                card.style.height = compact ? 70 : TableCardHeight;
                card.style.flexShrink = 0;
                card.style.alignItems = Align.Center;
                card.style.justifyContent = Justify.Center;
                card.style.borderTopLeftRadius = 8;
                card.style.borderTopRightRadius = 8;
                card.style.borderBottomLeftRadius = 8;
                card.style.borderBottomRightRadius = 8;
                card.style.borderTopWidth = 2;
                card.style.borderRightWidth = 2;
                card.style.borderBottomWidth = 2;
                card.style.borderLeftWidth = 2;
                card.style.borderTopColor = UITheme.CardBorder;
                card.style.borderRightColor = UITheme.CardBorder;
                card.style.borderBottomColor = UITheme.CardBorder;
                card.style.borderLeftColor = UITheme.CardBorder;
                card.style.backgroundColor = faceUp ? UITheme.CardMid : UITheme.CardBack;

                if (card.childCount > 0 && card[0] is Label label)
                {
                    label.text = face;
                    label.style.fontSize = compact ? faceUp ? 22 : 11 : faceUp ? 26 : 14;
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    label.style.color = faceUp ? UITheme.TextPrimary : Color.white;
                }
            }

            if (stack.childCount > 1 && stack[1] is Label caption)
            {
                caption.text = $"{title}  {count}";
                caption.style.fontSize = compact ? 10 : 12;
                caption.style.unityTextAlign = TextAnchor.MiddleCenter;
                caption.style.marginTop = compact ? 3 : 6;
            }
        }

        static void RestorePileCardVisibility(VisualElement stack)
        {
            if (stack == null)
                return;

            stack.style.opacity = 1f;
            stack.style.visibility = Visibility.Visible;
            stack.pickingMode = PickingMode.Position;
            for (int i = 0; i < stack.childCount; i++)
            {
                var child = stack[i];
                child.style.opacity = 1f;
                child.style.visibility = Visibility.Visible;
                child.pickingMode = PickingMode.Position;
            }
        }

        VisualElement CreateRevealRow(RoundResult result)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.minHeight = 58;
            row.style.marginTop = 2;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = UITheme.PanelBorder;

            var name = new Label(result.Nickname + (result.IsSteadyCaller ? "  CABO" : "") + (result.IsLowest ? "  最低" : ""));
            name.style.width = 150;
            name.style.flexShrink = 0;
            name.style.fontSize = 13;
            name.style.whiteSpace = WhiteSpace.Normal;
            row.Add(name);

            var cards = new VisualElement();
            cards.style.flexDirection = FlexDirection.Row;
            cards.style.flexGrow = 1;
            cards.style.flexShrink = 1;
            cards.style.justifyContent = Justify.Center;
            cards.style.minWidth = 0;
            foreach (var value in result.CardValues)
                cards.Add(CreateRevealCard(value));
            row.Add(cards);

            var score = new Label($"{result.HandTotal} + {result.Penalty} = {result.RoundScore}    总分 {result.CumulativeScore}");
            score.style.width = 170;
            score.style.flexShrink = 0;
            score.style.fontSize = 13;
            score.style.unityTextAlign = TextAnchor.MiddleRight;
            score.style.whiteSpace = WhiteSpace.Normal;
            row.Add(score);
            return row;
        }

        VisualElement CreateRevealCard(int value)
        {
            var card = CreateCard(true, value, 34, 46, false, false, null, false);
            card.style.marginLeft = 3;
            card.style.marginRight = 3;
            card.style.marginTop = 2;
            card.style.marginBottom = 2;
            return card;
        }

        void AddInterRoundControls(GameState state)
        {
            var me = state.Players.Find(p => p.PlayerId == state.MyPlayerId);
            bool isHost = me != null && me.IsHost;
            bool myReady = me != null && me.IsReady;
            int playerCount = state.Players.Count;
            int readyCount = 0;
            for (int i = 0; i < state.Players.Count; i++)
                if (state.Players[i].IsReady) readyCount++;
            bool allReady = playerCount > 0 && readyCount == playerCount;

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.marginTop = 4;
            divider.style.marginBottom = 6;
            divider.style.flexShrink = 0;
            divider.style.backgroundColor = UITheme.PanelBorder;
            _actionPanel.Add(divider);

            var readyTitle = new Label("下一轮准备");
            readyTitle.style.fontSize = 14;
            readyTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            readyTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            readyTitle.style.marginBottom = 3;
            readyTitle.style.flexShrink = 0;
            _actionPanel.Add(readyTitle);

            var readyGrid = new VisualElement();
            readyGrid.style.flexDirection = FlexDirection.Row;
            readyGrid.style.flexWrap = Wrap.Wrap;
            readyGrid.style.justifyContent = Justify.Center;
            readyGrid.style.marginBottom = 5;
            readyGrid.style.flexShrink = 0;
            foreach (var player in state.Players)
                readyGrid.Add(CreateReadyBadge(player, player.PlayerId == state.MyPlayerId));
            _actionPanel.Add(readyGrid);

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.justifyContent = Justify.Center;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.alignItems = Align.Center;
            controls.style.flexShrink = 0;
            _actionPanel.Add(controls);

            controls.Add(CreatePanelButton(myReady ? "已准备" : "准备", () => _flow.SendReady(), !myReady && state.MyPlayerId > 0 && state.RoomId > 0));

            if (isHost)
                controls.Add(CreatePanelButton("开始下一轮", () => _flow.SendStartGame(), allReady && state.MyPlayerId > 0 && state.RoomId > 0));
            else
            {
                var wait = new Label(allReady ? "等待房主开始" : "等待所有玩家准备");
                wait.style.fontSize = 12;
                wait.style.unityTextAlign = TextAnchor.MiddleCenter;
                wait.style.marginTop = 4;
                wait.style.marginLeft = 8;
                wait.style.color = UITheme.TextSecondary;
                wait.style.whiteSpace = WhiteSpace.Normal;
                controls.Add(wait);
            }

            _statusLine.text = isHost
                ? $"{readyCount}/{playerCount} 已准备。全员准备后可开始。"
                : $"{readyCount}/{playerCount} 已准备。";
            _statusLine.style.display = DisplayStyle.Flex;
        }

        VisualElement CreateReadyBadge(PlayerInfo player, bool isSelf)
        {
            var badge = new VisualElement();
            badge.style.flexDirection = FlexDirection.Row;
            badge.style.alignItems = Align.Center;
            badge.style.width = 130;
            badge.style.marginLeft = 3;
            badge.style.marginRight = 3;
            badge.style.marginTop = 3;
            badge.style.marginBottom = 3;
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 4;
            badge.style.paddingBottom = 4;
            badge.style.borderTopLeftRadius = 6;
            badge.style.borderTopRightRadius = 6;
            badge.style.borderBottomLeftRadius = 6;
            badge.style.borderBottomRightRadius = 6;
            badge.style.backgroundColor = player.IsReady ? UITheme.ReadySurface : UITheme.WaitingSurface;
            SetBorderWidth(badge, 1);
            SetBorderColor(badge, player.IsReady ? UITheme.ReadyBorder : UITheme.WaitingBorder);

            var dot = new VisualElement();
            dot.style.width = 9;
            dot.style.height = 9;
            dot.style.flexShrink = 0;
            dot.style.marginRight = 6;
            dot.style.borderTopLeftRadius = 5;
            dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5;
            dot.style.borderBottomRightRadius = 5;
            dot.style.backgroundColor = player.IsReady ? UITheme.ReadyBorder : UITheme.TextMuted;
            badge.Add(dot);

            var name = new Label((isSelf ? "你 " : "") + player.Nickname);
            name.style.flexGrow = 1;
            name.style.minWidth = 0;
            name.style.fontSize = 11;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.color = UITheme.TextPrimary;
            badge.Add(name);

            var readyState = new Label(player.IsReady ? "已准备" : "...");
            readyState.style.fontSize = 11;
            readyState.style.unityTextAlign = TextAnchor.MiddleRight;
            readyState.style.color = UITheme.TextPrimary;
            badge.Add(readyState);
            return badge;
        }

        Button CreatePanelButton(string text, Action action, bool enabled)
        {
            var button = new Button(() => InvokeUiActionNextFrame(action)) { text = text };
            button.style.minWidth = 120;
            button.style.height = 32;
            button.style.marginLeft = 5;
            button.style.marginRight = 5;
            button.style.marginTop = 3;
            button.style.marginBottom = 3;
            StyleTableButton(button, enabled);
            button.SetEnabled(enabled);
            return button;
        }

        void ResetActionPanelForGame()
        {
            if (_actionTitle.parent == _actionPanel
                && _actionBody.parent == _actionPanel
                && _drawnCardSlot.parent == _actionPanel
                && _buttonRow.parent == _actionPanel)
                return;

            _actionPanel.Clear();
            _actionPanel.Add(_actionTitle);
            _actionPanel.Add(_actionBody);
            _actionPanel.Add(_drawnCardSlot);
            _actionPanel.Add(_buttonRow);
            _lastActionPanelSubState = GameSubState.Idle;
            _lastActionPanelDeferredNewTurnActions = false;
            _actionButtons.Clear();
        }

        void AddActionButton(string text, Action action, bool enabled)
        {
            var button = new Button(() => InvokeUiActionNextFrame(action)) { text = text };
            button.style.minWidth = 116;
            button.style.height = 32;
            button.style.marginLeft = 5;
            button.style.marginRight = 5;
            button.style.marginTop = 4;
            button.style.marginBottom = 4;
            StyleTableButton(button, enabled);
            button.SetEnabled(enabled);
            _buttonRow.Add(button);
            _actionButtons.Add(button);
        }

        void UpdateActionPanelButtons(GameState state)
        {
            switch (_flow.SubState)
            {
                case GameSubState.AwaitingMainInput:
                    SetActionButtonEnabled(0, true);
                    SetActionButtonEnabled(1, state.TurnNumber > 1 && state.DiscardPileCount > 0);
                    SetActionButtonEnabled(2, !state.IsFinalRound);
                    break;
                case GameSubState.AwaitingDrawnDecision:
                    SetActionButtonEnabled(0, true);
                    SetActionButtonEnabled(1, true);
                    SetActionButtonEnabled(2, state.DrawnCardSkill > 0);
                    break;
                case GameSubState.AwaitingReplaceSlots:
                    SetActionButtonEnabled(0, _selectedOwnSlots.Count > 0);
                    SetActionButtonEnabled(1, true);
                    SetActionButtonEnabled(2, true);
                    SetActionButtonEnabled(3, state.DrawnCardSkill > 0);
                    break;
                case GameSubState.AwaitingTakeSlots:
                    SetActionButtonEnabled(0, _selectedOwnSlots.Count > 0);
                    SetActionButtonEnabled(1, true);
                    break;
                case GameSubState.SkillPeekSlot:
                    SetActionButtonEnabled(0, _selectedOwnSlots.Count == 1);
                    SetActionButtonEnabled(1, true);
                    break;
                case GameSubState.SkillSpyTarget:
                    SetActionButtonEnabled(0, true);
                    break;
                case GameSubState.SkillSpySlot:
                    SetActionButtonEnabled(0, _selectedOpponentSlot >= 0);
                    SetActionButtonEnabled(1, true);
                    SetActionButtonEnabled(2, true);
                    break;
                case GameSubState.SkillSwapMySlot:
                    SetActionButtonEnabled(0, true);
                    break;
                case GameSubState.SkillSwapTargetPlayer:
                    SetActionButtonEnabled(0, true);
                    SetActionButtonEnabled(1, true);
                    break;
                case GameSubState.SkillSwapTargetSlot:
                    SetActionButtonEnabled(0, _selectedOpponentSlot >= 0);
                    SetActionButtonEnabled(1, true);
                    SetActionButtonEnabled(2, true);
                    break;
            }
        }

        void SetActionButtonEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= _actionButtons.Count)
                return;

            var button = _actionButtons[index];
            if (button == null)
                return;

            button.SetEnabled(enabled);
            StyleTableButton(button, enabled);
        }

        static void StyleTableButton(Button button, bool enabled)
        {
            UIManager.ApplyReadableButtonStyle(button, enabled);
        }

        void OnOwnSlotClicked(int slot)
        {
            switch (_flow.SubState)
            {
                case GameSubState.AwaitingReplaceSlots:
                case GameSubState.AwaitingTakeSlots:
                    if (!_selectedOwnSlots.Add(slot))
                        _selectedOwnSlots.Remove(slot);
                    RefreshCardInteractionLayer();
                    break;
                case GameSubState.SkillPeekSlot:
                    _selectedOwnSlots.Clear();
                    _selectedOwnSlots.Add(slot);
                    RefreshCardInteractionLayer();
                    break;
                case GameSubState.SkillSwapMySlot:
                    _selectedOwnSlots.Clear();
                    _selectedOwnSlots.Add(slot);
                    _flow.DoSkillSwapMySlot(slot);
                    break;
            }
        }

        void OnOpponentClicked(long playerId)
        {
            if (_flow.SubState == GameSubState.SkillSpyTarget)
                _flow.DoSkillSpyTarget(playerId);
            else if (_flow.SubState == GameSubState.SkillSwapTargetPlayer)
                _flow.DoSkillSwapTargetPlayer(playerId);
        }

        void OnOpponentSlotClicked(long playerId, int slot)
        {
            if (_flow.SubState == GameSubState.SkillSpyTarget)
            {
                _flow.DoSkillSpyTarget(playerId);
                _selectedOpponentSlot = slot;
                RefreshCardInteractionLayer();
                return;
            }
            if (_flow.SubState == GameSubState.SkillSwapTargetPlayer)
            {
                _flow.DoSkillSwapTargetPlayer(playerId);
                _selectedOpponentSlot = slot;
                RefreshCardInteractionLayer();
                return;
            }
            if (_flow.SkillTargetPlayerId != playerId) return;

            if (_flow.SubState == GameSubState.SkillSpySlot)
            {
                _selectedOpponentSlot = slot;
                RefreshCardInteractionLayer();
            }
            else if (_flow.SubState == GameSubState.SkillSwapTargetSlot)
            {
                _selectedOpponentSlot = slot;
                RefreshCardInteractionLayer();
            }
        }

        void ConfirmReplace()
        {
            CachePendingSelfExchangeSnapshot(ActionType.ReplaceWithDrawn);
            _flow.DoReplaceWithDrawn(ToSortedArray(_selectedOwnSlots));
            _selectedOwnSlots.Clear();
        }

        void ConfirmTakeDiscard()
        {
            CachePendingSelfExchangeSnapshot(ActionType.TakeFromDiscard);
            _flow.DoTakeFromDiscardSlots(ToSortedArray(_selectedOwnSlots));
            _selectedOwnSlots.Clear();
        }

        void CachePendingSelfExchangeSnapshot(ActionType actionType)
        {
            if (_selectedOwnSlots.Count == 0)
            {
                _pendingSelfExchangeSnapshot = null;
                return;
            }

            var selected = ToSortedArray(_selectedOwnSlots);
            var snapshot = new PendingSelfExchangeSnapshot
            {
                ActionType = actionType,
                SourcePlayerBounds = GetPlayerBounds(_flow.State.MyPlayerId)
            };
            snapshot.SelectedSlots.AddRange(selected);
            snapshot.SourceSlotBounds.AddRange(CaptureSlotBounds(_flow.State.MyPlayerId, selected));
            snapshot.SourceHandBounds.AddRange(CaptureAllSlotBounds(_flow.State.MyPlayerId));
            foreach (var card in _flow.State.MyCards)
                snapshot.SourceHandCards.Add(new CardSnapshot { FaceUp = card.IsKnown, Value = card.Value });
            _pendingSelfExchangeSnapshot = snapshot;
        }

        void TryApplyPendingSelfExchangeSnapshot(ActionAnimationSnapshot snapshot)
        {
            var pending = _pendingSelfExchangeSnapshot;
            if (pending == null || pending.SelectedSlots.Count == 0)
                return;
            if (snapshot.SourcePlayerId != _flow.State.MyPlayerId || snapshot.ActionType != pending.ActionType)
                return;

            if (!SameSlots(snapshot.SelectedSlots, pending.SelectedSlots))
            {
                _pendingSelfExchangeSnapshot = null;
                return;
            }

            if (pending.SourcePlayerBounds.width > 1 && pending.SourcePlayerBounds.height > 1)
                snapshot.SourcePlayerBounds = pending.SourcePlayerBounds;
            snapshot.SourceSlotBounds.Clear();
            snapshot.SourceSlotBounds.AddRange(pending.SourceSlotBounds);
            snapshot.SourceHandBounds.Clear();
            snapshot.SourceHandBounds.AddRange(pending.SourceHandBounds);
            ApplyPendingSelfCards(snapshot.SourceHandBounds, pending.SourceHandCards);
            ApplyPendingSelfCards(snapshot.SourceSlotBounds, pending.SourceHandCards);
            snapshot.UsesPendingSelfExchangeSnapshot = true;
        }

        static void ApplyPendingSelfCards(List<SlotSnapshot> slots, List<CardSnapshot> cards)
        {
            if (slots == null || cards == null)
                return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Slot < 0 || slot.Slot >= cards.Count)
                    continue;

                var card = cards[slot.Slot];
                slot.FaceUp = card.FaceUp;
                slot.Value = card.Value;
                slots[i] = slot;
            }
        }

        static bool SameSlots(List<int> left, List<int> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
                if (!right.Contains(left[i]))
                    return false;
            return true;
        }

        void ClearPendingSelfExchangeSnapshot(ActionAnimationSnapshot action)
        {
            if (action == null || !action.UsesPendingSelfExchangeSnapshot)
                return;
            _pendingSelfExchangeSnapshot = null;
        }

        bool ShouldHoldPendingSelfExchangeView(GameState state)
        {
            var pending = _pendingSelfExchangeSnapshot;
            if (pending == null || pending.SelectedSlots.Count == 0)
                return false;
            if (state.LastActionSequence > _lastAnimatedActionSequence)
                return false;
            return _flow.SubState == GameSubState.WaitingReplaceRsp
                || _flow.SubState == GameSubState.WaitingTakeRsp
                || _flow.SubState == GameSubState.Idle;
        }

        void CachePendingSelfSwapSnapshot()
        {
            int sourceSlot = _selectedOwnSlots.Count == 1 ? ToSortedArray(_selectedOwnSlots)[0] : _flow.SkillMySlot;
            int targetSlot = _selectedOpponentSlot;
            long targetPlayerId = _flow.SkillTargetPlayerId;
            if (sourceSlot < 0 || targetSlot < 0 || targetPlayerId <= 0)
            {
                _pendingSelfSwapSnapshot = null;
                return;
            }

            var snapshot = new PendingSelfSwapSnapshot
            {
                SourcePlayerId = _flow.State.MyPlayerId,
                TargetPlayerId = targetPlayerId,
                SourceSlot = sourceSlot,
                TargetSlot = targetSlot,
                SourcePlayerBounds = GetPlayerBounds(_flow.State.MyPlayerId),
                TargetPlayerBounds = GetPlayerBounds(targetPlayerId)
            };
            snapshot.SourceSwapSlotBounds.AddRange(CaptureSlotBounds(_flow.State.MyPlayerId, new[] { sourceSlot }));
            snapshot.TargetSlotBounds.AddRange(CaptureSlotBounds(targetPlayerId, new[] { targetSlot }));
            _pendingSelfSwapSnapshot = snapshot;
        }

        void TryApplyPendingSelfSwapSnapshot(ActionAnimationSnapshot snapshot)
        {
            var pending = _pendingSelfSwapSnapshot;
            if (pending == null)
                return;
            if (snapshot.ActionType != ActionType.UseSkill || snapshot.Skill != SkillType.Swap || !snapshot.SwapOccurred)
                return;
            if (snapshot.SourcePlayerId != pending.SourcePlayerId
                || snapshot.TargetPlayerId != pending.TargetPlayerId
                || snapshot.SourceSlot != pending.SourceSlot
                || snapshot.TargetSlot != pending.TargetSlot)
                return;

            if (pending.SourcePlayerBounds.width > 1 && pending.SourcePlayerBounds.height > 1)
                snapshot.SourcePlayerBounds = pending.SourcePlayerBounds;
            if (pending.TargetPlayerBounds.width > 1 && pending.TargetPlayerBounds.height > 1)
                snapshot.TargetPlayerBounds = pending.TargetPlayerBounds;
            snapshot.SourceSwapSlotBounds.Clear();
            snapshot.SourceSwapSlotBounds.AddRange(pending.SourceSwapSlotBounds);
            snapshot.TargetSlotBounds.Clear();
            snapshot.TargetSlotBounds.AddRange(pending.TargetSlotBounds);
            snapshot.UsesPendingSelfSwapSnapshot = true;
        }

        void ClearPendingSelfSwapSnapshot(ActionAnimationSnapshot action)
        {
            if (action == null || !action.UsesPendingSelfSwapSnapshot)
                return;
            _pendingSelfSwapSnapshot = null;
        }

        void ConfirmSkillTargetSlot()
        {
            if (_selectedOpponentSlot < 0)
                return;

            if (_flow.SubState == GameSubState.SkillSpySlot)
                _flow.DoSkillSpySlot(_selectedOpponentSlot);
            else if (_flow.SubState == GameSubState.SkillSwapTargetSlot)
            {
                CachePendingSelfSwapSnapshot();
                _flow.DoSkillSwapTargetSlot(_selectedOpponentSlot);
            }
            _selectedOpponentSlot = -1;
        }

        void ConfirmSkillPeekSlot()
        {
            if (_selectedOwnSlots.Count != 1)
                return;

            _flow.DoSkillPeek(ToSortedArray(_selectedOwnSlots)[0]);
            _selectedOwnSlots.Clear();
        }

        void ReturnToDrawnDecision()
        {
            _selectedOwnSlots.Clear();
            _flow.ReturnToDrawnDecision();
        }

        void ReturnToMainInput()
        {
            _selectedOwnSlots.Clear();
            _flow.ReturnToMainInput();
        }

        void ReturnToSkillStart()
        {
            _selectedOwnSlots.Clear();
            _selectedOpponentPlayerId = 0;
            _selectedOpponentSlot = -1;
            _flow.ReturnToSkillStart();
        }

        void ReturnToSkillTargetSelection()
        {
            _selectedOpponentPlayerId = 0;
            _selectedOpponentSlot = -1;
            _flow.ReturnToSkillTargetSelection();
        }

        void ResetSelectionWhenSubStateChanges(GameSubState subState)
        {
            if (_lastSubState == subState) return;
            if (!ShouldPreserveOwnSelection(_lastSubState, subState))
                _selectedOwnSlots.Clear();
            _selectedOpponentPlayerId = subState == GameSubState.SkillSpySlot || subState == GameSubState.SkillSwapTargetSlot
                ? _flow.SkillTargetPlayerId
                : 0;
            _selectedOpponentSlot = -1;
            _lastSubState = subState;
        }

        static bool ShouldPreserveOwnSelection(GameSubState previous, GameSubState current)
        {
            return previous == GameSubState.SkillSwapMySlot
                && (current == GameSubState.SkillSwapTargetPlayer || current == GameSubState.SkillSwapTargetSlot)
                || previous == GameSubState.SkillSwapTargetPlayer && current == GameSubState.SkillSwapTargetSlot
                || previous == GameSubState.SkillSwapTargetSlot && current == GameSubState.SkillSwapTargetPlayer;
        }

        bool IsOwnSlotClickable(GameSubState subState)
        {
            return subState == GameSubState.AwaitingReplaceSlots
                || subState == GameSubState.AwaitingTakeSlots
                || subState == GameSubState.SkillPeekSlot
                || subState == GameSubState.SkillSwapMySlot;
        }

        bool IsOpponentSlotClickable(GameSubState subState, long playerId)
        {
            return subState == GameSubState.SkillSpyTarget
                || subState == GameSubState.SkillSwapTargetPlayer
                || ((subState == GameSubState.SkillSpySlot || subState == GameSubState.SkillSwapTargetSlot)
                    && _flow.SkillTargetPlayerId == playerId);
        }

        static int[] ToSortedArray(HashSet<int> slots)
        {
            var result = new int[slots.Count];
            slots.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        List<int> BuildOpponentIndices(GameState state)
        {
            if (state.Players.Count == 0) return new List<int>();
            if (state.Players.Count >= 4 && state.MyPlayerIndex >= 0) return state.OpponentIndices;

            var result = new List<int>();
            for (int i = 0; i < state.Players.Count; i++)
                if (state.Players[i].PlayerId != state.MyPlayerId)
                    result.Add(i);
            return result;
        }

        string BuildTurnText(GameState state, long visualCurrentPlayerId, bool holdingPreviousAction)
        {
            var current = state.Players.Find(p => p.PlayerId == visualCurrentPlayerId);
            string name = current?.Nickname ?? "等待中";
            if (holdingPreviousAction && visualCurrentPlayerId != state.CurrentPlayerId)
                return $"正在展示 {name} 的操作";

            if (state.SteadyCallerId != 0)
            {
                var caller = state.Players.Find(p => p.PlayerId == state.SteadyCallerId);
                string callerName = caller?.Nickname ?? "有玩家";
                if (state.IsFinalRound)
                    return $"{callerName} 已喊 CABO，最终轮剩余 {state.FinalRoundRemaining} 回合。当前：{name}";
            }
            if (state.IsFinalRound)
                return $"最终轮剩余 {state.FinalRoundRemaining} 回合。当前：{name}";
            return visualCurrentPlayerId == state.MyPlayerId ? "你的回合" : $"当前回合：{name}";
        }

        string BuildTurnText(GameState state)
        {
            var current = state.Players.Find(p => p.PlayerId == state.CurrentPlayerId);
            string name = current?.Nickname ?? "等待中";
            if (state.SteadyCallerId != 0)
            {
                var caller = state.Players.Find(p => p.PlayerId == state.SteadyCallerId);
                string callerName = caller?.Nickname ?? "有玩家";
                if (state.IsFinalRound)
                    return $"{callerName} 已喊 CABO，最终轮剩余 {state.FinalRoundRemaining} 回合。当前：{name}";
            }
            if (state.IsFinalRound)
                return $"最终轮剩余 {state.FinalRoundRemaining} 回合。当前：{name}";
            return state.IsMyTurn ? "你的回合" : $"当前回合：{name}";
        }

        string BuildStatusText(GameState state)
        {
            if (state.Players.Count < 4) return "等待玩家加入。";
            if (state.DiscardPileCount == 0) return "弃牌堆为空。";
            return "牌桌已与服务器同步。";
        }

        static bool ShouldShowActionStatus(GameSubState subState)
        {
            return subState == GameSubState.AwaitingMainInput
                || subState == GameSubState.Idle
                || subState == GameSubState.WaitingDrawRsp
                || subState == GameSubState.WaitingDiscardRsp
                || subState == GameSubState.WaitingReplaceRsp
                || subState == GameSubState.WaitingTakeRsp
                || subState == GameSubState.WaitingSkillRsp
                || subState == GameSubState.WaitingCallSteadyRsp;
        }

        static string BuildSkillButtonText(int skill)
        {
            return skill > 0 ? $"使用{GetSkillName(skill)}" : "使用技能";
        }

        static string BuildChangeToSkillButtonText(int skill)
        {
            return skill > 0 ? $"改为使用{GetSkillName(skill)}" : "改为使用技能";
        }

        static string GetSkillName(int skill)
        {
            return skill switch
            {
                2 => "看牌",
                3 => "偷看",
                4 => "换牌",
                _ => "无技能"
            };
        }

        static string GetSkillShortName(int value)
        {
            if (value == 7 || value == 8) return "看牌";
            if (value == 9 || value == 10) return "偷看";
            if (value == 11 || value == 12) return "换牌";
            return "";
        }

        static Color GetFaceColor(int value)
        {
            return UITheme.CardFace(value);
        }

        void HideSeatsForOverlay()
        {
            _selfSeat.Root.style.display = DisplayStyle.None;
            foreach (var seat in _opponentSeats)
                seat.Root.style.display = DisplayStyle.None;
        }

        static void StretchToParent(VisualElement element)
        {
            element.style.flexGrow = 1;
            element.style.width = Length.Percent(100);
            element.style.height = Length.Percent(100);
        }

        static void StylePanelTitle(Label label)
        {
            label.style.fontSize = 18;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = 8;
        }

        sealed class SeatView
        {
            public readonly VisualElement Root;
            public readonly VisualElement CardRow;

            readonly Label _name;
            readonly Label _score;
            readonly Label _tag;
            readonly VisualElement _avatarSlot;
            string _avatarCacheKey;

            public SeatView(string name, bool isSelf)
            {
                Root = new VisualElement { name = $"Seat-{name}" };
                Root.style.backgroundColor = UITheme.PanelSurface;
                Root.style.borderTopLeftRadius = 8;
                Root.style.borderTopRightRadius = 8;
                Root.style.borderBottomLeftRadius = 8;
                Root.style.borderBottomRightRadius = 8;
                Root.style.borderTopWidth = 1;
                Root.style.borderRightWidth = 1;
                Root.style.borderBottomWidth = 1;
                Root.style.borderLeftWidth = 1;
                Root.style.borderTopColor = UITheme.PanelBorder;
                Root.style.borderRightColor = UITheme.PanelBorder;
                Root.style.borderBottomColor = UITheme.PanelBorder;
                Root.style.borderLeftColor = UITheme.PanelBorder;
                Root.style.paddingLeft = 12;
                Root.style.paddingRight = 12;
                Root.style.paddingTop = 8;
                Root.style.paddingBottom = 8;
                Root.style.alignItems = Align.Center;
                Root.style.justifyContent = Justify.Center;

                if (isSelf)
                {
                    Root.style.minHeight = 166;
                    Root.style.flexDirection = FlexDirection.Column;
                }
                else if (name == "left" || name == "right")
                {
                    Root.style.width = 230;
                    Root.style.minHeight = 262;
                    Root.style.flexShrink = 0;
                    Root.style.flexDirection = FlexDirection.Column;
                }
                else
                {
                    Root.style.minHeight = 166;
                    Root.style.flexDirection = FlexDirection.Column;
                }

                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.justifyContent = Justify.Center;
                Root.Add(header);

                _avatarSlot = new VisualElement();
                _avatarSlot.style.marginRight = 7;
                header.Add(_avatarSlot);

                _tag = new Label();
                _tag.style.fontSize = 10;
                _tag.style.color = UITheme.TextSecondary;
                _tag.style.marginRight = 8;
                _tag.style.paddingLeft = 5;
                _tag.style.paddingRight = 5;
                _tag.style.paddingTop = 2;
                _tag.style.paddingBottom = 2;
                _tag.style.borderTopLeftRadius = 4;
                _tag.style.borderTopRightRadius = 4;
                _tag.style.borderBottomLeftRadius = 4;
                _tag.style.borderBottomRightRadius = 4;
                header.Add(_tag);

                _name = new Label();
                _name.style.fontSize = isSelf ? 17 : 14;
                _name.style.unityFontStyleAndWeight = FontStyle.Bold;
                _name.style.unityTextAlign = TextAnchor.MiddleCenter;
                header.Add(_name);

                _score = new Label();
                _score.style.fontSize = 12;
                _score.style.color = UITheme.TextSecondary;
                _score.style.unityTextAlign = TextAnchor.MiddleCenter;
                _score.style.marginTop = 2;
                Root.Add(_score);

                CardRow = new VisualElement();
                CardRow.style.flexDirection = FlexDirection.Row;
                CardRow.style.flexWrap = Wrap.Wrap;
                CardRow.style.justifyContent = Justify.Center;
                CardRow.style.alignItems = Align.Center;
                CardRow.style.marginTop = 6;
                Root.Add(CardRow);
            }

            public void Clear()
            {
                CardRow.Clear();
            }

            public void RenderHeader(string name, int score, bool isCurrentTurn, string tag, bool isCaboCaller = false, string avatarPath = "")
            {
                string avatarKey = $"{name}|{avatarPath}";
                if (_avatarCacheKey != avatarKey)
                {
                    _avatarSlot.Clear();
                    _avatarSlot.Add(PlayerProfileStore.CreateAvatarVisual(name, avatarPath, 32));
                    _avatarCacheKey = avatarKey;
                }
                _name.text = name;
                _score.text = $"总分 {score}";
                _tag.text = isCurrentTurn && isCaboCaller ? "CABO 回合" : isCurrentTurn ? "回合" : tag;
                _tag.style.color = isCaboCaller ? UITheme.TextOnDanger : isCurrentTurn ? UITheme.TextOnAccent : UITheme.TextSecondary;
                _tag.style.backgroundColor = isCaboCaller ? UITheme.CaboDanger : isCurrentTurn ? UITheme.TurnHighlight : Color.clear;
                Root.style.backgroundColor = isCaboCaller ? UITheme.CaboSurface : UITheme.PanelSurface;
                var borderColor = isCaboCaller ? UITheme.CaboBorder : isCurrentTurn ? UITheme.TurnBorder : UITheme.PanelBorder;
                Root.style.borderTopColor = borderColor;
                Root.style.borderRightColor = Root.style.borderTopColor.value;
                Root.style.borderBottomColor = Root.style.borderTopColor.value;
                Root.style.borderLeftColor = Root.style.borderTopColor.value;
                float borderWidth = isCaboCaller || isCurrentTurn ? 3 : 1;
                Root.style.borderTopWidth = borderWidth;
                Root.style.borderRightWidth = borderWidth;
                Root.style.borderBottomWidth = borderWidth;
                Root.style.borderLeftWidth = borderWidth;
            }
        }

        sealed class TableFeedEntry
        {
            public string PlayerName;
            public string AvatarPath;
            public string Message;
            public string StickerPath;
            public bool IsSticker;
        }

        sealed class ActionAnimationSnapshot
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
            public bool AttemptedMultiCard;
            public int IncomingCardValue;
            public int DiscardedCount;
            public int AddedCardCount;
            public bool DrewExtraPenaltyCard;
            public int DiscardTopValue;
            public int DiscardPileCount;
            public int PeekedValue;
            public bool UsesPendingSelfExchangeSnapshot;
            public bool UsesPendingSelfSwapSnapshot;
            public Rect IncomingFinalBounds;
            public int IncomingFinalSlot = -1;
            public Rect SourcePlayerBounds;
            public Rect TargetPlayerBounds;
            public Rect DrawPileBounds;
            public Rect DiscardPileBounds;
            public readonly List<int> SelectedSlots = new();
            public readonly List<SlotMove> SurvivorMoves = new();
            public readonly List<SlotSnapshot> SourceHandBounds = new();
            public readonly List<SlotSnapshot> FinalSourceHandBounds = new();
            public readonly List<SlotSnapshot> SelectedSlotBounds = new();
            public readonly List<SlotSnapshot> SourceSlotBounds = new();
            public readonly List<SlotSnapshot> SourceSwapSlotBounds = new();
            public readonly List<SlotSnapshot> TargetSlotBounds = new();
        }

        sealed class PendingSelfExchangeSnapshot
        {
            public ActionType ActionType;
            public Rect SourcePlayerBounds;
            public readonly List<int> SelectedSlots = new();
            public readonly List<CardSnapshot> SourceHandCards = new();
            public readonly List<SlotSnapshot> SourceHandBounds = new();
            public readonly List<SlotSnapshot> SourceSlotBounds = new();
        }

        sealed class PendingSelfSwapSnapshot
        {
            public long SourcePlayerId;
            public long TargetPlayerId;
            public int SourceSlot;
            public int TargetSlot;
            public Rect SourcePlayerBounds;
            public Rect TargetPlayerBounds;
            public readonly List<SlotSnapshot> SourceSwapSlotBounds = new();
            public readonly List<SlotSnapshot> TargetSlotBounds = new();
        }

        struct SlotMove
        {
            public long PlayerId;
            public int OldSlot;
            public int NewSlot;
            public Rect From;
            public Rect To;
            public bool FaceUp;
            public int Value;
        }

        struct CardSnapshot
        {
            public bool FaceUp;
            public int Value;
        }

        struct SlotSnapshot
        {
            public long PlayerId;
            public int Slot;
            public Rect Bounds;
            public bool FaceUp;
            public int Value;
        }
    }
}
