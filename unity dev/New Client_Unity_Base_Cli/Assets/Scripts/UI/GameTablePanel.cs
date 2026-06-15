using System;
using System.Collections.Generic;
using System.Globalization;
using Cabo.Client.Art;
using Cabo.Client.UI.CardTable;
using Game.Common;
using Game.Room;
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
        const string RulesText =
            "\u3010\u76ee\u6807\u3011\n"
            + "\u5c3d\u91cf\u8ba9\u81ea\u5df1\u7684\u624b\u724c\u7cd6\u5206\u603b\u503c\u66f4\u4f4e\u3002\u6bcf\u8f6e\u7ed3\u675f\u540e\u7d2f\u52a0\u5f97\u5206\uff0c\u6709\u4eba\u7d2f\u8ba1\u8fbe\u5230100\u5206\u65f6\u6e38\u620f\u7ed3\u675f\uff0c\u603b\u5206\u6700\u4f4e\u8005\u80dc\u5229\u3002\n\n"
            + "\u3010\u5f00\u5c40\u3011\n"
            + "\u6bcf\u4eba\u83b7\u5f974\u5f20\u6697\u724c\uff0c\u53ea\u80fd\u5077\u770b\u81ea\u5df1\u5176\u4e2d2\u5f20\u3002\u8bf7\u8bb0\u4f4f\u724c\u7684\u4f4d\u7f6e\uff0c\u4e0d\u8981\u968f\u610f\u7ffb\u5f00\u3002\n\n"
            + "\u3010\u56de\u5408\u6d41\u7a0b\u3011\n"
            + "1. \u4ece\u724c\u5e93\u62bd\u4e00\u5f20\uff0c\u6216\u62ff\u53d6\u5f03\u724c\u5806\u9876\u724c\u3002\n"
            + "2. \u62bd\u5230\u7684\u724c\u53ef\u4e0e\u81ea\u5df1\u4e00\u5f20\u6216\u591a\u5f20\u540c\u503c\u624b\u724c\u66ff\u6362\uff0c\u4e5f\u53ef\u76f4\u63a5\u5f03\u6389\u3002\n"
            + "3. \u591a\u5f20\u66ff\u6362\u82e5\u5224\u5b9a\u5931\u8d25\uff0c\u4f1a\u53d7\u5230\u989d\u5916\u62bd\u724c\u60e9\u7f5a\u3002\n\n"
            + "\u3010\u7279\u6b8a\u6280\u80fd\u3011\n"
            + "7-8 \u81ea\u68c0\uff1a\u5077\u770b\u81ea\u5df1\u4e00\u5f20\u6697\u724c\u3002\n"
            + "9-10 \u4fa6\u67e5\uff1a\u5077\u770b\u4e00\u540d\u5bf9\u624b\u7684\u4e00\u5f20\u6697\u724c\u3002\n"
            + "11-12 \u4ea4\u6362\uff1a\u5c06\u81ea\u5df1\u4e00\u5f20\u724c\u4e0e\u5bf9\u624b\u4e00\u5f20\u724c\u4ea4\u6362\uff0c\u4e0d\u67e5\u770b\u724c\u9762\u3002\n"
            + "\u6280\u80fd\u53ea\u5728\u4ece\u724c\u5e93\u62bd\u5230\u5bf9\u5e94\u724c\u5e76\u9009\u62e9\u5f03\u6389\u65f6\u53d1\u52a8\u3002\n\n"
            + "\u3010CABO\u4e0e\u7ed3\u7b97\u3011\n"
            + "\u8ba4\u4e3a\u81ea\u5df1\u624b\u724c\u5f88\u4f4e\u65f6\u53ef\u547c\u558aCABO\u3002\u5176\u4ed6\u73a9\u5bb6\u5404\u5b8c\u6210\u6700\u540e\u4e00\u56de\u5408\u540e\u5168\u5458\u4eae\u724c\u3002\u624b\u724c\u7cd6\u5206\u8ba1\u5165\u672c\u8f6e\u5f97\u5206\uff0c\u7279\u6b8a\u7ed3\u679c\u53ef\u89e6\u53d1\u5956\u52b1\u6216\u60e9\u7f5a\u3002\n\n"
            + "\u3010\u672c\u5730\u6807\u8bb0\u3011\n"
            + "\u4f60\u5077\u770b\u8fc7\u7684\u5bf9\u624b\u724c\u4f1a\u663e\u793a\u201c\u5df2\u770b\u201d\u6807\u8bb0\uff1b\u81ea\u5df1\u5df2\u88ab\u516c\u5f00\u7684\u724c\u4f1a\u663e\u793a\u6a59\u8272\u201c\u516c\u5f00\u201d\u6807\u8bb0\u3002\u8fd9\u4e9b\u4fe1\u606f\u4ec5\u5bf9\u672c\u5730\u73a9\u5bb6\u53ef\u89c1\u3002";

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
        readonly Transform _ownerTransform;

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
        readonly Button _rulesButton;
        readonly VisualElement _rulesOverlay;
        readonly VisualElement _animationLayer;
        readonly CardTableView _cardTableView;
        SettlementStageRuntime _settlementStage;
        TableCharacterRuntime _tableCharacterStage;
        readonly List<SettlementScoreRowView> _settlementScoreRows = new();
        Button _interRoundReadyButton;
        Button _interRoundStartButton;
        Label _settlementGateLabel;
        bool _interRoundReadyCanUse;
        bool _interRoundStartCanUse;
        bool _settlementPlaybackComplete;
        int _settlementRoundNumber = -1;
        bool _gameOverFinalePlaying;
        int _gameOverFinaleRound = -1;
        bool _victorySfxPlayed;
        EventCallback<GeometryChangedEvent> _geometryChangedHandler;
        Action _animationQueueDrained;

        readonly HashSet<int> _selectedOwnSlots = new();
        readonly List<TableFeedEntry> _gameLogEntries = new();
        readonly HashSet<string> _seenChatBubbleMessages = new();
        long _chatBubbleRoomId;
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
        bool _rulesVisible;
        bool _chatBubblesInitialized;
        bool _cardTableRefreshQueued;
        bool _layoutRefreshQueued;
        bool _uiActionQueued;
        bool _isVisible;
        int _tableCharacterRound = -1;
        int _tableCharacterCardCount = -1;
        int _tableCharacterKnownTotal;
        long _tableCharacterActionSequence;
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
            _ownerTransform = ownerTransform;

            _container = new VisualElement { name = "CaboGameTable" };
            StretchToParent(_container);
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.position = Position.Relative;
            _container.style.paddingLeft = 14;
            _container.style.paddingRight = 14;
            _container.style.paddingTop = 12;
            _container.style.paddingBottom = 12;
            _container.style.backgroundColor = Color.clear;
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
            _endGameModalCard.style.backgroundColor = UITheme.PanelGlassStrong;
            _endGameModalCard.style.borderTopLeftRadius = 18;
            _endGameModalCard.style.borderTopRightRadius = 18;
            _endGameModalCard.style.borderBottomLeftRadius = 18;
            _endGameModalCard.style.borderBottomRightRadius = 18;
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
            _centerTable.style.backgroundColor = UITheme.TableGlass;
            _centerTable.style.borderTopLeftRadius = 28;
            _centerTable.style.borderTopRightRadius = 28;
            _centerTable.style.borderBottomLeftRadius = 28;
            _centerTable.style.borderBottomRightRadius = 28;
            _centerTable.style.borderTopWidth = 2;
            _centerTable.style.borderRightWidth = 2;
            _centerTable.style.borderBottomWidth = 2;
            _centerTable.style.borderLeftWidth = 2;
            _centerTable.style.borderTopColor = UITheme.TableSoftBorder;
            _centerTable.style.borderRightColor = UITheme.TableSoftBorder;
            _centerTable.style.borderBottomColor = UITheme.TableSoftBorder;
            _centerTable.style.borderLeftColor = UITheme.TableSoftBorder;
            _centerTable.style.position = Position.Relative;
            _centerTable.style.overflow = Overflow.Hidden;
            if (CaboArt.TableCenterBackground != null)
            {
                _centerTable.style.backgroundImage = new StyleBackground(CaboArt.TableCenterBackground);
                _centerTable.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _centerTable.style.backgroundColor = Color.white;
            }
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
            _inspectionZone.style.backgroundColor = UITheme.PanelGlassStrong;
            _inspectionZone.style.borderTopLeftRadius = 14;
            _inspectionZone.style.borderTopRightRadius = 14;
            _inspectionZone.style.borderBottomLeftRadius = 14;
            _inspectionZone.style.borderBottomRightRadius = 14;
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
            _actionPanel.style.backgroundColor = UITheme.PanelGlassStrong;
            _actionPanel.style.borderTopLeftRadius = 14;
            _actionPanel.style.borderTopRightRadius = 14;
            _actionPanel.style.borderBottomLeftRadius = 14;
            _actionPanel.style.borderBottomRightRadius = 14;
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
            _socialPanel.style.width = 280;
            _socialPanel.style.minWidth = 280;
            _socialPanel.style.maxWidth = 280;
            _socialPanel.style.flexGrow = 0;
            _socialPanel.style.flexShrink = 0;
            _socialPanel.style.minHeight = 0;
            _socialPanel.style.height = Length.Percent(100);
            _socialPanel.style.position = Position.Relative;
            _socialPanel.style.alignSelf = Align.Stretch;
            _socialPanel.style.marginLeft = 12;
            _socialPanel.style.paddingLeft = 10;
            _socialPanel.style.paddingRight = 10;
            _socialPanel.style.paddingTop = 10;
            _socialPanel.style.paddingBottom = 10;
            _socialPanel.style.backgroundColor = UITheme.TableSocialGlass;
            _socialPanel.style.borderTopLeftRadius = 16;
            _socialPanel.style.borderTopRightRadius = 16;
            _socialPanel.style.borderBottomLeftRadius = 16;
            _socialPanel.style.borderBottomRightRadius = 16;
            _socialPanel.style.overflow = Overflow.Hidden;
            SetBorderWidth(_socialPanel, 1);
            SetBorderColor(_socialPanel, UITheme.TableSoftBorder);
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

            _rulesOverlay = CreateRulesOverlay();
            _socialPanel.Add(_rulesOverlay);

            _playArea.Add(_selfSeat.Root);

            _rulesButton = new Button(ToggleRulesOverlay)
            {
                text = "?",
                tooltip = "\u67e5\u770b\u6e38\u620f\u89c4\u5219"
            };
            _rulesButton.style.position = Position.Absolute;
            _rulesButton.style.left = 0;
            _rulesButton.style.top = 0;
            _rulesButton.style.width = 24;
            _rulesButton.style.height = 24;
            _rulesButton.style.minWidth = 24;
            _rulesButton.style.paddingLeft = 0;
            _rulesButton.style.paddingRight = 0;
            _rulesButton.style.paddingTop = 0;
            _rulesButton.style.paddingBottom = 0;
            _rulesButton.style.fontSize = 16;
            _rulesButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _rulesButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            UITheme.SetButtonRole(_rulesButton, UITheme.SoftButtonClass);
            StyleTableButton(_rulesButton, true);
            _playArea.Add(_rulesButton);
            _rulesButton.BringToFront();

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
            UITheme.SetButtonRole(_endGameButton, UITheme.DangerButtonClass);
            StyleTableButton(_endGameButton, true);
            root.Add(_endGameButton);
            _endGameButton.BringToFront();
            _endGameModalOverlay.BringToFront();
        }

        VisualElement CreateRulesOverlay()
        {
            var overlay = new VisualElement { name = "RulesOverlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.paddingLeft = 12;
            overlay.style.paddingRight = 12;
            overlay.style.paddingTop = 12;
            overlay.style.paddingBottom = 12;
            overlay.style.backgroundColor = new Color(0.99f, 0.96f, 0.87f, 0.99f);
            overlay.style.display = DisplayStyle.None;

            var title = new Label("\u6e38\u620f\u89c4\u5219");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 8;
            overlay.Add(title);

            var close = new Button(HideRulesOverlay) { text = "X", tooltip = "\u5173\u95ed\u89c4\u5219" };
            close.style.position = Position.Absolute;
            close.style.right = 8;
            close.style.top = 8;
            close.style.width = 24;
            close.style.height = 24;
            close.style.minWidth = 24;
            close.style.paddingLeft = 0;
            close.style.paddingRight = 0;
            close.style.paddingTop = 0;
            close.style.paddingBottom = 0;
            close.style.unityFontStyleAndWeight = FontStyle.Bold;
            UITheme.SetButtonRole(close, UITheme.DangerButtonClass);
            StyleTableButton(close, true);
            overlay.Add(close);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.minHeight = 0;
            scroll.style.marginTop = 4;
            var body = new Label(RulesText);
            body.style.fontSize = 12;
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.color = UITheme.TextPrimary;
            body.style.paddingLeft = 2;
            body.style.paddingRight = 6;
            body.style.paddingBottom = 12;
            scroll.Add(body);
            overlay.Add(scroll);
            return overlay;
        }

        void ToggleRulesOverlay()
        {
            _rulesVisible = !_rulesVisible;
            _rulesOverlay.style.display = _rulesVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_rulesVisible)
                _rulesOverlay.BringToFront();
        }

        void HideRulesOverlay()
        {
            _rulesVisible = false;
            _rulesOverlay.style.display = DisplayStyle.None;
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
                HideRulesOverlay();
                HideAllChatBubbles();
                SynchronizeChatBubbleHistory();
                HideTableCharacterStage();
                _showLocalEndGameConfirm = false;
                ApplyEndGameModal(EndGameModalKind.None, "", "");
                ClearTransientAnimationState();
                HideSettlementStage();
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
            if (_settlementStage != null)
                UnityEngine.Object.Destroy(_settlementStage.gameObject);
            if (_tableCharacterStage != null)
                UnityEngine.Object.Destroy(_tableCharacterStage.gameObject);
        }

        public bool HasPendingActionAnimation =>
            (_flow != null && _flow.State.LastActionSequence > _lastAnimatedActionSequence)
            || Time.realtimeSinceStartup < _animationQueueUntil
            || _cardTableView.HasActiveTransientAnimation
            || _inspectionActive;

        public void SetAnimationQueueDrainedCallback(Action callback)
        {
            _animationQueueDrained = callback;
        }

        public void Tick()
        {
            RefreshChatBubblePositions();

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

        void RefreshChatBubblePositions()
        {
            _topSeat?.RefreshChatBubblePosition();
            _leftSeat?.RefreshChatBubblePosition();
            _rightSeat?.RefreshChatBubblePosition();
            _selfSeat?.RefreshChatBubblePosition();
        }

        public void RenderGame()
        {
            _victorySfxPlayed = false;
            _gameOverFinalePlaying = false;
            _gameOverFinaleRound = -1;
            HideSettlementStage();
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
            RenderTableCharacter(state);
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
            HideTableCharacterStage();
            _cardTableView.SetVisible(false);
            var state = _flow.State;
            ConfigureActionPanelForOverlay();
            _roundLabel.style.display = DisplayStyle.Flex;
            _turnLabel.style.display = DisplayStyle.Flex;
            _roundLabel.text = $"第 {state.RoundNumber} 轮 · 结算小剧场";
            _turnLabel.text = "角色吃下最终餐盘食物，糖能逐项累加";
            StyleOverlayHeading(_roundLabel, true);
            StyleOverlayHeading(_turnLabel, false);
            HideSeatsForOverlay();
            RenderPiles(state);
            RenderSocialPanel(state);
            RenderEndGameControls(state, true);

            _drawnCardSlot.Clear();
            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;
            _actionPanel.style.marginTop = 8;

            if (_settlementRoundNumber != state.RoundNumber)
            {
                _settlementRoundNumber = state.RoundNumber;
                _settlementPlaybackComplete = false;
                _gameOverFinalePlaying = false;
                _gameOverFinaleRound = -1;
            }

            var title = new Label("本轮餐盘糖能");
            StylePanelTitle(title);
            _actionPanel.Add(title);

            var revealContent = new VisualElement { name = "SettlementRevealContent" };
            revealContent.style.width = Length.Percent(100);
            revealContent.style.height = 230;
            revealContent.style.flexDirection = FlexDirection.Row;
            revealContent.style.alignItems = Align.Stretch;
            revealContent.style.flexShrink = 0;
            revealContent.style.marginBottom = 4;
            _actionPanel.Add(revealContent);

            var scorePanel = new VisualElement { name = "SettlementScorePanel" };
            scorePanel.style.width = 300;
            scorePanel.style.minWidth = 270;
            scorePanel.style.flexShrink = 1;
            scorePanel.style.paddingLeft = 8;
            scorePanel.style.paddingRight = 8;
            scorePanel.style.paddingTop = 6;
            scorePanel.style.paddingBottom = 6;
            scorePanel.style.backgroundColor = UITheme.PanelGlass;
            UITheme.SetRadius(scorePanel, 10);
            SetBorderWidth(scorePanel, 1);
            SetBorderColor(scorePanel, UITheme.PanelBorder);
            revealContent.Add(scorePanel);

            var scoreTitle = new Label("\u8BA1\u5206\u60C5\u51B5");
            scoreTitle.style.fontSize = 13;
            scoreTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            scoreTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            scoreTitle.style.marginBottom = 4;
            scoreTitle.style.flexShrink = 0;
            scorePanel.Add(scoreTitle);

            var resultList = new ScrollView(ScrollViewMode.Vertical);
            resultList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            resultList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            resultList.style.flexGrow = 1;
            resultList.style.minHeight = 0;
            _settlementScoreRows.Clear();
            foreach (var result in state.LastRoundResults)
            {
                var row = CreateCompactRevealRow(result);
                _settlementScoreRows.Add(row);
                resultList.Add(row.Root);
            }
            scorePanel.Add(resultList);

            if (state.GameOverPending)
                AddGameOverFinaleGate(_settlementPlaybackComplete);
            else
                AddInterRoundControls(state, _settlementPlaybackComplete);
            MountSettlementStage(state, revealContent);
            UpdateStatusLineForEarlyEnd(state, true);
        }

        public void RenderGameOver()
        {
            if (!_victorySfxPlayed)
            {
                CaboAudio.Play(CaboSfx.Victory, 0.92f);
                _victorySfxPlayed = true;
            }
            ClearTransientAnimationState();
            HideSettlementStage();
            HideTableCharacterStage();
            _cardTableView.SetVisible(false);
            ConfigureActionPanelForOverlay();
            _actionPanel.style.width = Length.Percent(70);
            _actionPanel.style.maxWidth = 760;
            _actionPanel.style.flexGrow = 0;
            _actionPanel.style.minHeight = 500;
            _actionPanel.style.paddingTop = 20;
            _actionPanel.style.paddingBottom = 18;
            _roundLabel.style.display = DisplayStyle.Flex;
            _turnLabel.style.display = DisplayStyle.Flex;
            _roundLabel.text = "糖糖餐桌岛 · 宴会落幕";
            _turnLabel.text = "累计糖能最低的玩家赢得健康餐桌冠军";
            StyleOverlayHeading(_roundLabel, true);
            StyleOverlayHeading(_turnLabel, false);
            HideSeatsForOverlay();
            _drawnCardSlot.Clear();
            _drawPile.Clear();
            _discardPile.Clear();
            RenderSocialPanel(_flow.State);
            _showLocalEndGameConfirm = false;
            RenderEndGameControls(_flow.State, false);

            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            var title = new Label("健康餐桌冠军");
            StylePanelTitle(title);
            title.style.fontSize = 24;
            title.style.color = UITheme.HostBadgeBorder;
            title.style.marginBottom = 4;
            _actionPanel.Add(title);

            FinalRank winner = null;
            foreach (var rank in _flow.State.FinalRankings)
            {
                if (rank.IsWinner || winner == null)
                    winner = rank.IsWinner ? rank : winner ?? rank;
            }

            if (winner != null)
            {
                var champion = new VisualElement();
                champion.style.flexDirection = FlexDirection.Row;
                champion.style.alignItems = Align.Center;
                champion.style.justifyContent = Justify.Center;
                champion.style.marginTop = 4;
                champion.style.marginBottom = 12;
                champion.style.paddingLeft = 18;
                champion.style.paddingRight = 18;
                champion.style.paddingTop = 12;
                champion.style.paddingBottom = 12;
                champion.style.backgroundColor = UITheme.HostBadgeSurface;
                champion.style.borderTopLeftRadius = 18;
                champion.style.borderTopRightRadius = 18;
                champion.style.borderBottomLeftRadius = 18;
                champion.style.borderBottomRightRadius = 18;
                SetBorderWidth(champion, 2);
                SetBorderColor(champion, UITheme.HostBadgeBorder);

                var winnerPlayer = _flow.State.Players.Find(player => player.PlayerId == winner.PlayerId);
                var avatarPath = PlayerProfileStore.GetCharacterVisualPath(winnerPlayer?.CharacterId);
                var avatar = PlayerProfileStore.CreateAvatarVisual(winner.Nickname, avatarPath, 58);
                avatar.style.marginRight = 16;
                champion.Add(avatar);

                var championText = new VisualElement();
                var championName = new Label(winner.Nickname);
                championName.style.fontSize = 22;
                championName.style.unityFontStyleAndWeight = FontStyle.Bold;
                championName.style.color = UITheme.TextPrimary;
                championText.Add(championName);

                var championScore = new Label($"累计糖能 {winner.FinalScore} · 全桌最低");
                championScore.style.fontSize = 15;
                championScore.style.unityFontStyleAndWeight = FontStyle.Bold;
                championScore.style.color = UITheme.HostBadgeBorder;
                championScore.style.marginTop = 2;
                championText.Add(championScore);

                var championLine = new Label("吃得聪明，才是真的赢");
                championLine.style.fontSize = 12;
                championLine.style.color = UITheme.TextSecondary;
                championLine.style.marginTop = 2;
                championText.Add(championLine);
                champion.Add(championText);
                _actionPanel.Add(champion);
            }

            var rankingTitle = new Label("糖能排行榜");
            rankingTitle.style.fontSize = 14;
            rankingTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            rankingTitle.style.color = UITheme.TextSecondary;
            rankingTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            rankingTitle.style.marginBottom = 4;
            _actionPanel.Add(rankingTitle);

            var rankingList = new VisualElement();
            rankingList.style.flexShrink = 1;
            foreach (var rank in _flow.State.FinalRankings)
                rankingList.Add(CreateFinalRankRow(rank));
            _actionPanel.Add(rankingList);

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

            _statusLine.text = "本桌宴会已结束，返回房间即可重新准备下一局。";
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
            int viewerSeatId = myInfo?.SeatId ?? 0;
            _selfSeat.SetStationBackground(CaboArt.GetSeatBackground(viewerSeatId, viewerSeatId));
            bool selfCabo = state.SteadyCallerId != 0 && state.SteadyCallerId == state.MyPlayerId;
            _selfSeat.RenderHeader(myInfo?.Nickname ?? "你", myInfo?.TotalScore ?? 0, visualCurrentPlayerId == state.MyPlayerId, selfCabo ? "CABO" : "你", selfCabo,
                PlayerProfileStore.GetCharacterVisualPath(myInfo?.CharacterId));
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
                _opponentSeats[i].SetStationBackground(CaboArt.GetSeatBackground(player.SeatId, viewerSeatId));
                bool current = player.PlayerId == visualCurrentPlayerId;
                bool selected = _selectedOpponentPlayerId == player.PlayerId;
                bool cabo = state.SteadyCallerId != 0 && state.SteadyCallerId == player.PlayerId;
                _opponentSeats[i].RenderHeader(player.Nickname, player.TotalScore, current, cabo ? "CABO" : selected ? "目标" : "对手", cabo,
                    PlayerProfileStore.GetCharacterVisualPath(player.CharacterId));
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
            UpdateChatBubbles(state);
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

        void UpdateChatBubbles(GameState state)
        {
            if (state?.RoomChatMessages == null)
                return;

            if (_chatBubbleRoomId != state.RoomId)
            {
                _chatBubbleRoomId = state.RoomId;
                _seenChatBubbleMessages.Clear();
                _chatBubblesInitialized = false;
                HideAllChatBubbles();
            }

            if (!_chatBubblesInitialized)
            {
                for (int i = 0; i < state.RoomChatMessages.Count; i++)
                    _seenChatBubbleMessages.Add(ChatBubbleKey(state.RoomChatMessages[i]));
                _chatBubblesInitialized = true;
                return;
            }

            var latestTextByPlayer = new Dictionary<long, RoomChatMessage>();
            for (int i = 0; i < state.RoomChatMessages.Count; i++)
            {
                var message = state.RoomChatMessages[i];
                if (message == null || !_seenChatBubbleMessages.Add(ChatBubbleKey(message)))
                    continue;
                if (message.Type == RoomChatType.Text && !string.IsNullOrWhiteSpace(message.Text))
                    latestTextByPlayer[message.SenderPlayerId] = message;
            }

            foreach (var pair in latestTextByPlayer)
            {
                var seat = GetSeatView(pair.Key);
                seat?.ShowChatBubble(TruncateChatBubble(pair.Value.Text));
            }
        }

        SeatView GetSeatView(long playerId)
        {
            if (playerId == _flow.State.MyPlayerId)
                return _selfSeat;

            var indices = BuildOpponentIndices(_flow.State);
            for (int i = 0; i < indices.Count && i < _opponentSeats.Length; i++)
                if (_flow.State.Players[indices[i]].PlayerId == playerId)
                    return _opponentSeats[i];
            return null;
        }

        static string ChatBubbleKey(RoomChatMessage message)
        {
            if (message == null)
                return "null";
            return $"{message.RoomId}:{message.MessageId}:{message.ServerTimeMs}:{message.SenderPlayerId}";
        }

        static string TruncateChatBubble(string text)
        {
            string trimmed = text?.Trim() ?? "";
            var elements = StringInfo.ParseCombiningCharacters(trimmed);
            if (elements.Length <= 15)
                return trimmed;

            int end = elements[15];
            return trimmed.Substring(0, end) + "...";
        }

        void HideAllChatBubbles()
        {
            _topSeat.HideChatBubble();
            _leftSeat.HideChatBubble();
            _rightSeat.HideChatBubble();
            _selfSeat.HideChatBubble();
        }

        void SynchronizeChatBubbleHistory()
        {
            var state = _flow.State;
            _chatBubbleRoomId = state.RoomId;
            _seenChatBubbleMessages.Clear();
            for (int i = 0; i < state.RoomChatMessages.Count; i++)
                _seenChatBubbleMessages.Add(ChatBubbleKey(state.RoomChatMessages[i]));
            _chatBubblesInitialized = true;
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
                AvatarPath = PlayerProfileStore.GetCharacterVisualPath(me?.CharacterId),
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
                AvatarPath = PlayerProfileStore.GetCharacterVisualPath(me?.CharacterId),
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
            AddCardTableDrawTarget(layout, state.MyPlayerId);

            var opponentIndices = BuildOpponentIndices(state);
            for (int i = 0; i < opponentIndices.Count && i < _opponentSeats.Length; i++)
            {
                var player = state.Players[opponentIndices[i]];
                AddCardTableSlots(layout, player.PlayerId, _opponentSeats[i].CardRow, false);
                AddCardTableDrawTarget(layout, player.PlayerId);
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

        void AddCardTableDrawTarget(CardTableLayout layout, long playerId)
        {
            if (layout == null || playerId <= 0)
                return;

            var target = GetDrawRevealTarget(playerId, fallbackToSeat: true);
            if (target.position == Vector2.zero || target.size.x <= 1f || target.size.y <= 1f)
                return;

            layout.DrawTargets.Add(new CardTableDrawTarget
            {
                PlayerId = playerId,
                AnchoredPosition = target.position,
                Size = target.size
            });
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
                    LocallyPeeked = !faceUp && _flow.State.IsOpponentCardPeeked(playerId, slot),
                    PubliclyKnown = isSelf && faceUp && _flow.State.IsMyRevealedSlot(slot),
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
                            return ClampDrawRevealTarget(selfPos, size);
                    }
                }

                var rowPos = WorldBoundsToOverlayPosition(rowBounds);
                float xBias = Mathf.Clamp(rowBounds.width * 0.16f, 24f, 88f);
                float yBias = Mathf.Clamp(rowBounds.height * 0.18f, 12f, 28f);
                var pos = rowPos + new Vector2(xBias, yBias);
                if (IsFinite(pos.x) && IsFinite(pos.y))
                    return ClampDrawRevealTarget(pos, size);
            }

            if (HasUsableBounds(seatBounds))
            {
                var seatPos = WorldBoundsToOverlayPosition(seatBounds);
                var size = GetOverlayCardSizeFromRow(row, playerId);
                var pos = playerId == _flow.State.MyPlayerId
                    ? seatPos + new Vector2(Mathf.Clamp(seatBounds.width * 0.30f, 84f, 176f), Mathf.Clamp(seatBounds.height * 0.22f, 34f, 76f))
                    : seatPos + new Vector2(Mathf.Clamp(seatBounds.width * 0.28f, 36f, 120f), Mathf.Clamp(seatBounds.height * 0.18f, 12f, 28f));
                if (IsFinite(pos.x) && IsFinite(pos.y))
                    return ClampDrawRevealTarget(pos, size);
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
                            return ClampDrawRevealTarget(pos, size);
                    }
                }
            }

            return (Vector2.zero, Vector2.zero);
        }

        (Vector2 position, Vector2 size) ClampDrawRevealTarget(Vector2 position, Vector2 size)
        {
            var rootBounds = _root?.worldBound ?? Rect.zero;
            float overlayWidth = Screen.width > 1 ? Screen.width : rootBounds.width;
            float overlayHeight = Screen.height > 1 ? Screen.height : rootBounds.height;
            if (overlayWidth <= 1f || overlayHeight <= 1f)
                return (position, size);

            const float margin = 12f;
            float halfWidth = Mathf.Min(size.x * 0.5f, Mathf.Max(0f, overlayWidth * 0.5f - margin));
            float halfHeight = Mathf.Min(size.y * 0.5f, Mathf.Max(0f, overlayHeight * 0.5f - margin));
            float minX = -overlayWidth * 0.5f + margin + halfWidth;
            float maxX = overlayWidth * 0.5f - margin - halfWidth;
            float minY = -overlayHeight * 0.5f + margin + halfHeight;
            float maxY = overlayHeight * 0.5f - margin - halfHeight;

            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : 0f;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : 0f;
            return (position, size);
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
            {
                CaboAudio.Play(CaboSfx.Draw, 0.72f);
                _lastLocalDrawSequence = sequence;
            }
        }

        void PlayActionAnimation(ActionAnimationSnapshot action)
        {
            PlayActionSfx(action);

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

        void PlayActionSfx(ActionAnimationSnapshot action)
        {
            switch (action.ActionType)
            {
                case ActionType.Draw:
                    if (action.Sequence != _lastLocalDrawSequence)
                        CaboAudio.Play(CaboSfx.Draw, 0.72f);
                    break;
                case ActionType.DiscardDrawn:
                    CaboAudio.Play(CaboSfx.Discard, 0.78f);
                    break;
                case ActionType.ReplaceWithDrawn:
                case ActionType.TakeFromDiscard:
                    CaboAudio.Play(CaboSfx.Swap, 0.72f);
                    break;
                case ActionType.UseSkill:
                    CaboAudio.Play(CaboSfx.Skill, 0.72f);
                    if (action.Skill == SkillType.PeekSelf || action.Skill == SkillType.Spy)
                        CaboAudio.Play(CaboSfx.Flip, 0.62f);
                    break;
                case ActionType.CallSteady:
                    CaboAudio.Play(CaboSfx.Cabo, 0.92f);
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
            card.style.borderTopLeftRadius = 9;
            card.style.borderTopRightRadius = 9;
            card.style.borderBottomLeftRadius = 9;
            card.style.borderBottomRightRadius = 9;
            card.style.borderTopWidth = selected ? 4 : 2;
            card.style.borderRightWidth = selected ? 4 : 2;
            card.style.borderBottomWidth = selected ? 4 : 2;
            card.style.borderLeftWidth = selected ? 4 : 2;
            card.style.borderTopColor = selected ? UITheme.SelectedBorder : UITheme.CardBorder;
            card.style.borderRightColor = card.style.borderTopColor.value;
            card.style.borderBottomColor = card.style.borderTopColor.value;
            card.style.borderLeftColor = card.style.borderTopColor.value;
            bool hasArtwork = ApplyCardArtwork(card, faceUp, value);
            card.style.opacity = clickable ? 1f : 0.96f;

            var valueLabel = new Label(faceUp ? value.ToString() : hasArtwork ? "" : "CABO");
            int valueFontSize = faceUp ? Mathf.Max(11, Mathf.RoundToInt(height * 0.18f)) : Mathf.RoundToInt(height * 0.16f);
            valueLabel.style.fontSize = valueFontSize;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.unityTextAlign = faceUp ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            valueLabel.style.color = faceUp ? UITheme.TextPrimary : Color.white;
            if (faceUp)
            {
                valueLabel.style.position = Position.Absolute;
                valueLabel.style.left = 3;
                valueLabel.style.top = 3;
                valueLabel.style.paddingLeft = 3;
                valueLabel.style.paddingRight = 3;
                valueLabel.style.minWidth = value >= 10 ? valueFontSize * 1.55f + 6f : valueFontSize + 6f;
                valueLabel.style.whiteSpace = WhiteSpace.NoWrap;
                valueLabel.style.overflow = Overflow.Visible;
                valueLabel.style.backgroundColor = new Color(1f, 0.98f, 0.90f, 0.86f);
                valueLabel.style.borderTopLeftRadius = 5;
                valueLabel.style.borderTopRightRadius = 5;
                valueLabel.style.borderBottomLeftRadius = 5;
                valueLabel.style.borderBottomRightRadius = 5;
            }
            card.Add(valueLabel);

            if (showSkillBadge && faceUp && value >= 7 && value <= 12)
            {
                var badge = new Label(GetSkillShortName(value));
                badge.style.position = Position.Absolute;
                badge.style.right = 3;
                badge.style.bottom = 3;
                badge.style.fontSize = 9;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.color = UITheme.TextOnAccent;
                badge.style.backgroundColor = new Color(1f, 0.86f, 0.45f, 0.90f);
                badge.style.paddingLeft = 3;
                badge.style.paddingRight = 3;
                badge.style.borderTopLeftRadius = 4;
                badge.style.borderTopRightRadius = 4;
                badge.style.borderBottomLeftRadius = 4;
                badge.style.borderBottomRightRadius = 4;
                card.Add(badge);
            }

            if (clickable && onClick != null)
                card.RegisterCallback<ClickEvent>(_ => InvokeUiActionNextFrame(onClick));

            return card;
        }

        static bool ApplyCardArtwork(VisualElement card, bool faceUp, int value)
        {
            Sprite sprite = faceUp ? CaboArt.GetFood(value).foodSprite : CaboArt.CardBack;
            if (sprite != null)
            {
                card.style.backgroundImage = new StyleBackground(sprite);
                card.style.backgroundColor = Color.white;
                card.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                return true;
            }

            card.style.backgroundImage = StyleKeyword.None;
            card.style.backgroundColor = faceUp ? GetFaceColor(value) : UITheme.CardBack;
            return false;
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
            int value = ParseCardValue(face);
            bool hasArtwork = ApplyCardArtwork(card, faceUp, value);

            var label = new Label(faceUp ? face : hasArtwork ? "" : face);
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
                int value = ParseCardValue(face);
                bool hasArtwork = ApplyCardArtwork(card, faceUp, value);

                if (card.childCount > 0 && card[0] is Label label)
                {
                    label.text = faceUp ? face : hasArtwork ? "" : face;
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

        static int ParseCardValue(string face)
        {
            return int.TryParse(face, out int value) ? Mathf.Clamp(value, 0, 13) : 0;
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
            row.style.minHeight = 60;
            row.style.marginTop = 1;
            row.style.marginBottom = 2;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.backgroundColor = result.IsLowest ? UITheme.ReadySurface : UITheme.PanelGlass;
            row.style.borderTopLeftRadius = 10;
            row.style.borderTopRightRadius = 10;
            row.style.borderBottomLeftRadius = 10;
            row.style.borderBottomRightRadius = 10;
            SetBorderWidth(row, result.IsLowest ? 2 : 1);
            SetBorderColor(row, result.IsLowest ? UITheme.ReadyBorder : UITheme.PanelBorder);

            var identity = new VisualElement();
            identity.style.width = 174;
            identity.style.flexShrink = 0;
            identity.style.flexDirection = FlexDirection.Row;
            identity.style.alignItems = Align.Center;

            var avatarPath = PlayerProfileStore.GetCharacterVisualPath(result.CharacterId);
            var avatar = PlayerProfileStore.CreateAvatarVisual(result.Nickname, avatarPath, 34);
            avatar.style.marginRight = 8;
            identity.Add(avatar);

            var nameBlock = new VisualElement();
            var name = new Label(result.Nickname);
            name.style.fontSize = 13;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = UITheme.TextPrimary;
            nameBlock.Add(name);

            string flags = result.IsLowest ? "最低糖能" : result.IsSteadyCaller ? "健康宣言" : "餐盘结算";
            if (result.IsSteadyCaller && result.IsLowest)
                flags = "健康宣言 · 最低糖能";
            var flag = new Label(flags);
            flag.style.fontSize = 10;
            flag.style.color = result.IsLowest ? UITheme.ReadyBorder : UITheme.TextSecondary;
            flag.style.marginTop = 2;
            flag.style.whiteSpace = WhiteSpace.Normal;
            nameBlock.Add(flag);
            identity.Add(nameBlock);
            row.Add(identity);

            var cards = new VisualElement();
            cards.style.flexDirection = FlexDirection.Row;
            cards.style.flexGrow = 1;
            cards.style.flexShrink = 1;
            cards.style.justifyContent = Justify.Center;
            cards.style.minWidth = 0;
            foreach (var value in result.CardValues)
                cards.Add(CreateRevealCard(value));
            row.Add(cards);

            string penalty = result.Penalty > 0 ? $"翻车小点心 +{result.Penalty}" : "无翻车点心";
            var score = new Label($"餐盘 {result.HandTotal} · {penalty}\n本轮糖能 {result.RoundScore}　累计 {result.CumulativeScore}");
            score.style.width = 218;
            score.style.flexShrink = 0;
            score.style.fontSize = 12;
            score.style.color = UITheme.TextSecondary;
            score.style.unityTextAlign = TextAnchor.MiddleRight;
            score.style.whiteSpace = WhiteSpace.Normal;
            row.Add(score);
            return row;
        }

        SettlementScoreRowView CreateCompactRevealRow(RoundResult result)
        {
            var row = new VisualElement();
            row.style.minHeight = 76;
            row.style.marginTop = 1;
            row.style.marginBottom = 3;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = result.IsLowest ? UITheme.ReadySurface : UITheme.PanelGlassStrong;
            UITheme.SetRadius(row, 8);
            SetBorderWidth(row, result.IsLowest ? 2 : 1);
            SetBorderColor(row, result.IsLowest ? UITheme.ReadyBorder : UITheme.PanelBorder);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;
            row.Add(header);

            var avatarPath = PlayerProfileStore.GetCharacterVisualPath(result.CharacterId);
            var avatar = PlayerProfileStore.CreateAvatarVisual(result.Nickname, avatarPath, 24);
            avatar.style.marginRight = 6;
            header.Add(avatar);

            var name = new Label(result.Nickname);
            name.style.fontSize = 11;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1;
            name.style.color = UITheme.TextPrimary;
            header.Add(name);

            string flagText = result.IsLowest
                ? "最低糖能"
                : result.IsSteadyCaller ? "健康宣言" : "";
            if (!string.IsNullOrEmpty(flagText))
            {
                var flag = new Label(flagText);
                flag.style.fontSize = 9;
                flag.style.color = result.IsLowest ? UITheme.ReadyBorder : UITheme.TextSecondary;
                header.Add(flag);
            }

            string cardValues = result.CardValues != null && result.CardValues.Count > 0
                ? string.Join(" + ", result.CardValues)
                : "0";
            var score = new Label($"手牌 {cardValues}\n等待角色进食…");
            score.style.fontSize = 10;
            score.style.marginTop = 3;
            score.style.whiteSpace = WhiteSpace.Normal;
            score.style.color = UITheme.TextSecondary;
            row.Add(score);

            var effect = new Label(result.IsKamikaze ? "反转套餐待触发" : "");
            effect.style.fontSize = 9;
            effect.style.marginTop = 2;
            effect.style.color = UITheme.SelectedBorder;
            effect.style.whiteSpace = WhiteSpace.Normal;
            row.Add(effect);
            return new SettlementScoreRowView(row, score, effect, result);
        }

        void HandleSettlementCue(SettlementCue cue)
        {
            if (cue.PlayerIndex < 0 || cue.PlayerIndex >= _settlementScoreRows.Count)
                return;

            var view = _settlementScoreRows[cue.PlayerIndex];
            var result = view.Result;
            switch (cue.Type)
            {
                case SettlementCueType.FoodStarted:
                    var food = CaboArt.GetFood(cue.CardValue);
                    view.Effect.text = $"正在品尝 {food?.displayName ?? cue.CardValue.ToString()}";
                    break;
                case SettlementCueType.FoodConsumed:
                    view.Score.text = $"餐盘糖能 {cue.RunningHandTotal} / {result.HandTotal}\n" +
                        $"服务端本轮 {result.RoundScore:+0;-0;0}  · 累计 {result.CumulativeScore}";
                    view.Effect.text = $"已吃完 {CaboArt.GetFood(cue.CardValue)?.displayName ?? "本道食物"}";
                    break;
                case SettlementCueType.Penalty:
                    view.Effect.text = $"翻车小点心 +{cue.Amount}";
                    view.Effect.style.color = UITheme.CaboBorder;
                    break;
                case SettlementCueType.KamikazeTriggered:
                    view.Effect.text = "糖分反转套餐！其他玩家 +50";
                    view.Effect.style.color = UITheme.ReadyBorder;
                    break;
                case SettlementCueType.KamikazePenalty:
                    view.Effect.text = $"反转套餐波及 +{cue.Amount}";
                    view.Effect.style.color = UITheme.CaboBorder;
                    break;
                case SettlementCueType.ResultFinalized:
                    ApplyServerSettlementResult(view);
                    break;
            }
        }

        static void ApplyServerSettlementResult(SettlementScoreRowView view)
        {
            var result = view.Result;
            view.Score.text = $"餐盘 {result.HandTotal}  · 本轮 {result.RoundScore:+0;-0;0}\n" +
                $"累计 {result.CumulativeScore}";
            if (string.IsNullOrEmpty(view.Effect.text))
                view.Effect.text = "结算完成";
        }

        void HandleSettlementPlaybackComplete()
        {
            _settlementPlaybackComplete = true;
            for (int i = 0; i < _settlementScoreRows.Count; i++)
                ApplyServerSettlementResult(_settlementScoreRows[i]);

            if (_flow.State.GameOverPending)
            {
                if (_settlementGateLabel != null)
                    _settlementGateLabel.text = "\u6b63\u5728\u64ad\u653e\u6700\u7ec8\u5931\u8d25\u52a8\u753b...";
                StartGameOverFinale();
                return;
            }

            _interRoundReadyButton?.SetEnabled(_interRoundReadyCanUse);
            _interRoundStartButton?.SetEnabled(_interRoundStartCanUse);
            if (_settlementGateLabel != null)
                _settlementGateLabel.style.display = DisplayStyle.None;
        }

        void StartGameOverFinale()
        {
            var state = _flow.State;
            if (!state.GameOverPending || _gameOverFinalePlaying)
                return;

            _gameOverFinalePlaying = true;
            _gameOverFinaleRound = state.RoundNumber;

            long highestPlayerId = 0;
            int highestScore = int.MinValue;
            for (int i = 0; i < state.FinalRankings.Count; i++)
            {
                var rank = state.FinalRankings[i];
                if (rank.FinalScore > highestScore)
                {
                    highestScore = rank.FinalScore;
                    highestPlayerId = rank.PlayerId;
                }
            }

            int actorIndex = -1;
            for (int i = 0; i < state.LastRoundResults.Count; i++)
            {
                if (state.LastRoundResults[i].PlayerId == highestPlayerId)
                {
                    actorIndex = i;
                    break;
                }
            }
            if (actorIndex < 0 && state.LastRoundResults.Count > 0)
                actorIndex = 0;

            _turnLabel.text = "\u603b\u7cd6\u5206\u6700\u9ad8\u7684\u89d2\u8272\u5403\u6491\u4e86\uff0c\u6b63\u5728\u64ad\u653e\u6700\u7ec8\u5931\u8d25\u52a8\u753b";
            _statusLine.text = "\u52a8\u753b\u7ed3\u675f\u540e\u5c06\u663e\u793a\u6700\u7ec8\u6392\u540d\u3002";
            _statusLine.style.display = DisplayStyle.Flex;

            if (_settlementStage == null)
            {
                CompleteGameOverFinale();
                return;
            }

            _settlementStage.PlayGameOverFinale(actorIndex, CompleteGameOverFinale);
        }

        void CompleteGameOverFinale()
        {
            if (_gameOverFinaleRound != _flow.State.RoundNumber)
                return;

            _gameOverFinalePlaying = false;
            _flow.CompletePendingGameOverPresentation();
        }


        VisualElement CreateFinalRankRow(FinalRank rank)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 48;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 12;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = rank.IsWinner ? UITheme.HostBadgeSurface : UITheme.PanelGlass;
            row.style.borderTopLeftRadius = 10;
            row.style.borderTopRightRadius = 10;
            row.style.borderBottomLeftRadius = 10;
            row.style.borderBottomRightRadius = 10;
            SetBorderWidth(row, rank.IsWinner ? 2 : 1);
            SetBorderColor(row, rank.IsWinner ? UITheme.HostBadgeBorder : UITheme.PanelBorder);

            var badge = new Label(rank.Rank.ToString());
            badge.style.width = 32;
            badge.style.height = 32;
            badge.style.marginRight = 10;
            badge.style.fontSize = 15;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.color = UITheme.TextPrimary;
            badge.style.backgroundColor = rank.IsWinner ? UITheme.TurnHighlight : UITheme.PanelSurfaceAlt;
            badge.style.borderTopLeftRadius = 16;
            badge.style.borderTopRightRadius = 16;
            badge.style.borderBottomLeftRadius = 16;
            badge.style.borderBottomRightRadius = 16;
            row.Add(badge);

            var rankedPlayer = _flow.State.Players.Find(player => player.PlayerId == rank.PlayerId);
            var avatarPath = PlayerProfileStore.GetCharacterVisualPath(rankedPlayer?.CharacterId);
            var avatar = PlayerProfileStore.CreateAvatarVisual(rank.Nickname, avatarPath, 34);
            avatar.style.marginRight = 10;
            row.Add(avatar);

            var name = new Label(rank.Nickname + (rank.IsWinner ? " · 冠军" : ""));
            name.style.flexGrow = 1;
            name.style.fontSize = 14;
            name.style.unityFontStyleAndWeight = rank.IsWinner ? FontStyle.Bold : FontStyle.Normal;
            name.style.color = UITheme.TextPrimary;
            row.Add(name);

            var score = new Label($"累计糖能 {rank.FinalScore}");
            score.style.fontSize = 14;
            score.style.unityFontStyleAndWeight = FontStyle.Bold;
            score.style.color = rank.IsWinner ? UITheme.HostBadgeBorder : UITheme.TextSecondary;
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

        void MountSettlementStage(GameState state, VisualElement parent = null)
        {
            if (_settlementStage == null)
                _settlementStage = SettlementStageRuntime.Create(_ownerTransform);

            _settlementStage.gameObject.SetActive(true);
            _settlementStage.Play(
                state.RoundNumber,
                state.LastRoundResults,
                HandleSettlementCue,
                HandleSettlementPlaybackComplete);

            var stageFrame = new VisualElement
            {
                name = "SettlementCharacterStageFrame",
                pickingMode = PickingMode.Ignore
            };
            stageFrame.style.position = Position.Relative;
            stageFrame.style.height = 300;
            stageFrame.style.flexGrow = 1;
            stageFrame.style.flexShrink = 1;
            stageFrame.style.minWidth = 0;
            stageFrame.style.marginRight = 8;

            var stageImage = new Image
            {
                name = "SettlementCharacterStageImage",
                image = _settlementStage.Output,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            stageImage.style.position = Position.Absolute;
            stageImage.style.left = 0;
            stageImage.style.right = 0;
            stageImage.style.top = 0;
            stageImage.style.bottom = 0;
            stageFrame.Add(stageImage);

            int actorCount = Mathf.Max(1, state.LastRoundResults.Count);
            float nicknameWidth = actorCount >= 4 ? 64f : actorCount == 3 ? 80f : 108f;
            float nicknameFontSize = actorCount >= 4 ? 10f : actorCount == 3 ? 11f : 12f;
            for (int i = 0; i < state.LastRoundResults.Count; i++)
            {
                var result = state.LastRoundResults[i];
                Vector2 viewport = _settlementStage.GetActorNameViewportPosition(i);
                var nickname = new Label(string.IsNullOrWhiteSpace(result.Nickname) ? $"玩家 {i + 1}" : result.Nickname)
                {
                    name = $"SettlementNickname_{i + 1}",
                    pickingMode = PickingMode.Ignore
                };
                nickname.style.position = Position.Absolute;
                nickname.style.left = Length.Percent(viewport.x * 100f);
                nickname.style.top = Length.Percent((1f - viewport.y) * 100f);
                nickname.style.width = nicknameWidth;
                nickname.style.height = 22;
                nickname.style.marginLeft = -nicknameWidth * 0.5f;
                nickname.style.marginTop = -11;
                nickname.style.paddingLeft = 4;
                nickname.style.paddingRight = 4;
                nickname.style.unityTextAlign = TextAnchor.MiddleCenter;
                nickname.style.unityFontStyleAndWeight = FontStyle.Bold;
                nickname.style.fontSize = nicknameFontSize;
                nickname.style.color = UITheme.TextPrimary;
                nickname.style.backgroundColor = new Color(1f, 0.97f, 0.84f, 0.94f);
                nickname.style.borderLeftWidth = 1;
                nickname.style.borderRightWidth = 1;
                nickname.style.borderTopWidth = 1;
                nickname.style.borderBottomWidth = 1;
                nickname.style.borderLeftColor = UITheme.PanelBorder;
                nickname.style.borderRightColor = UITheme.PanelBorder;
                nickname.style.borderTopColor = UITheme.PanelBorder;
                nickname.style.borderBottomColor = UITheme.PanelBorder;
                nickname.style.borderTopLeftRadius = 12;
                nickname.style.borderTopRightRadius = 12;
                nickname.style.borderBottomLeftRadius = 12;
                nickname.style.borderBottomRightRadius = 12;
                nickname.style.overflow = Overflow.Hidden;
                nickname.style.textOverflow = TextOverflow.Ellipsis;
                stageFrame.Add(nickname);
            }

            (parent ?? _actionPanel).Insert(0, stageFrame);
        }

        void HideSettlementStage()
        {
            if (_settlementStage == null)
                return;
            _settlementStage.StopPlayback();
            _settlementStage.gameObject.SetActive(false);
        }

        void RenderTableCharacter(GameState state)
        {
            var me = state?.Players.Find(player => player.PlayerId == state.MyPlayerId);
            if (me == null || state.Phase != GamePhase.Playing)
            {
                HideTableCharacterStage();
                return;
            }

            if (_tableCharacterStage == null)
                _tableCharacterStage = TableCharacterRuntime.Create(_ownerTransform);

            _tableCharacterStage.Show(me.CharacterId);
            _selfSeat.SetCharacterTexture(_tableCharacterStage.Output, true);

            int knownTotal = 0;
            for (int i = 0; i < state.MyCards.Count; i++)
            {
                var card = state.MyCards[i];
                if (card != null && card.IsKnown)
                    knownTotal += card.Value;
            }

            if (_tableCharacterRound != state.RoundNumber)
            {
                _tableCharacterRound = state.RoundNumber;
                _tableCharacterCardCount = state.MyCards.Count;
                _tableCharacterKnownTotal = knownTotal;
                _tableCharacterActionSequence = state.LastActionSequence;
                return;
            }

            if (state.LastActionSequence > _tableCharacterActionSequence)
            {
                if (DidLastActionChangeMyHand(state))
                {
                    int delta = state.MyCards.Count == _tableCharacterCardCount
                        ? knownTotal - _tableCharacterKnownTotal
                        : state.MyCards.Count > _tableCharacterCardCount ? 1 : -1;
                    _tableCharacterStage.ReactToHandChange(delta);
                }
                _tableCharacterActionSequence = state.LastActionSequence;
            }

            _tableCharacterCardCount = state.MyCards.Count;
            _tableCharacterKnownTotal = knownTotal;
        }

        static bool DidLastActionChangeMyHand(GameState state)
        {
            if (state == null)
                return false;

            if ((state.LastActionType == ActionType.ReplaceWithDrawn || state.LastActionType == ActionType.TakeFromDiscard)
                && state.LastActionSourcePlayerId == state.MyPlayerId)
                return true;

            return state.LastActionType == ActionType.UseSkill
                && state.LastActionSkill == SkillType.Swap
                && state.LastActionSwapOccurred
                && (state.LastActionSourcePlayerId == state.MyPlayerId || state.LastActionTargetPlayerId == state.MyPlayerId);
        }

        void HideTableCharacterStage()
        {
            _selfSeat.SetCharacterTexture(null, false);
            _tableCharacterStage?.Hide();
            _tableCharacterRound = -1;
            _tableCharacterCardCount = -1;
            _tableCharacterKnownTotal = 0;
            _tableCharacterActionSequence = 0;
        }

        void AddGameOverFinaleGate(bool playbackComplete)
        {
            _interRoundReadyButton = null;
            _interRoundStartButton = null;
            _interRoundReadyCanUse = false;
            _interRoundStartCanUse = false;

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.marginTop = 4;
            divider.style.marginBottom = 8;
            divider.style.flexShrink = 0;
            divider.style.backgroundColor = UITheme.PanelBorder;
            _actionPanel.Add(divider);

            var title = new Label("\u6700\u7ec8\u7ed3\u7b97\u6f14\u51fa");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.flexShrink = 0;
            _actionPanel.Add(title);

            _settlementGateLabel = new Label(playbackComplete
                ? "\u6b63\u5728\u64ad\u653e\u6700\u7ec8\u5931\u8d25\u52a8\u753b..."
                : "\u672c\u8f6e\u8fdb\u98df\u5b8c\u6210\u540e\uff0c\u5c06\u64ad\u653e\u603b\u7cd6\u5206\u6700\u9ad8\u89d2\u8272\u7684\u53d8\u80d6\u54ed\u6ce3\u52a8\u753b\u3002");
            _settlementGateLabel.style.fontSize = 12;
            _settlementGateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _settlementGateLabel.style.marginTop = 5;
            _settlementGateLabel.style.color = UITheme.SelectedBorder;
            _settlementGateLabel.style.whiteSpace = WhiteSpace.Normal;
            _actionPanel.Add(_settlementGateLabel);

            _statusLine.text = "\u6700\u7ec8\u6392\u540d\u548c\u8fd4\u56de\u6309\u94ae\u5c06\u5728\u5168\u90e8\u52a8\u753b\u7ed3\u675f\u540e\u663e\u793a\u3002";
            _statusLine.style.display = DisplayStyle.Flex;
        }

        void AddInterRoundControls(GameState state, bool playbackComplete)
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

            _interRoundReadyCanUse = !myReady && state.MyPlayerId > 0 && state.RoomId > 0;
            _interRoundReadyButton = CreatePanelButton(
                myReady ? "已准备" : "准备",
                () => _flow.SendReady(),
                playbackComplete && _interRoundReadyCanUse);
            controls.Add(_interRoundReadyButton);

            if (isHost)
            {
                _interRoundStartCanUse = allReady && state.MyPlayerId > 0 && state.RoomId > 0;
                _interRoundStartButton = CreatePanelButton(
                    "开始下一轮",
                    () => _flow.SendStartGame(),
                    playbackComplete && _interRoundStartCanUse);
                controls.Add(_interRoundStartButton);
            }
            else
            {
                _interRoundStartCanUse = false;
                _interRoundStartButton = null;
                var wait = new Label(allReady ? "等待房主开始" : "等待所有玩家准备");
                wait.style.fontSize = 12;
                wait.style.unityTextAlign = TextAnchor.MiddleCenter;
                wait.style.marginTop = 4;
                wait.style.marginLeft = 8;
                wait.style.color = UITheme.TextSecondary;
                wait.style.whiteSpace = WhiteSpace.Normal;
                controls.Add(wait);
            }

            _settlementGateLabel = new Label("结算演出完成后可进入下一轮");
            _settlementGateLabel.style.fontSize = 11;
            _settlementGateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _settlementGateLabel.style.marginTop = 3;
            _settlementGateLabel.style.color = UITheme.SelectedBorder;
            _settlementGateLabel.style.display = playbackComplete ? DisplayStyle.None : DisplayStyle.Flex;
            _actionPanel.Add(_settlementGateLabel);

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
            UITheme.ApplyTitle(label);
        }

        static void StyleOverlayHeading(Label label, bool prominent)
        {
            label.style.alignSelf = Align.Center;
            label.style.color = new Color(1f, 0.97f, 0.86f, 1f);
            label.style.backgroundColor = new Color(0.16f, 0.08f, 0.20f, prominent ? 0.86f : 0.72f);
            label.style.paddingLeft = prominent ? 18f : 12f;
            label.style.paddingRight = prominent ? 18f : 12f;
            label.style.paddingTop = prominent ? 5f : 3f;
            label.style.paddingBottom = prominent ? 6f : 4f;
            UITheme.SetRadius(label, prominent ? 14f : 10f);
            if (prominent)
                UITheme.ApplyTitle(label);
        }

        sealed class SettlementScoreRowView
        {
            public readonly VisualElement Root;
            public readonly Label Score;
            public readonly Label Effect;
            public readonly RoundResult Result;

            public SettlementScoreRowView(VisualElement root, Label score, Label effect, RoundResult result)
            {
                Root = root;
                Score = score;
                Effect = effect;
                Result = result;
            }
        }

        sealed class SeatView
        {
            public readonly VisualElement Root;
            public readonly VisualElement CardRow;

            readonly Label _name;
            readonly Label _score;
            readonly Label _tag;
            readonly VisualElement _avatarSlot;
            readonly VisualElement _avatarImageHost;
            readonly VisualElement _chatBubble;
            readonly Label _chatBubbleLabel;
            readonly Image _tableCharacterImage;
            readonly string _seatName;
            readonly float _chatBubbleMaxWidth;
            float _chatBubbleWidth = 66f;
            string _avatarCacheKey;
            Sprite _stationBackground;
            IVisualElementScheduledItem _bubbleDelayItem;
            IVisualElementScheduledItem _bubbleFadeItem;
            IVisualElementScheduledItem _bubblePositionItem;
            int _bubbleVersion;

            public SeatView(string name, bool isSelf)
            {
                _seatName = name;
                _chatBubbleMaxWidth = name == "left" || name == "right" ? 132f : 210f;
                Root = new VisualElement { name = $"Seat-{name}" };
                Root.style.backgroundColor = UITheme.TableSeatGlass;
                Root.style.borderTopLeftRadius = 16;
                Root.style.borderTopRightRadius = 16;
                Root.style.borderBottomLeftRadius = 16;
                Root.style.borderBottomRightRadius = 16;
                Root.style.borderTopWidth = 1;
                Root.style.borderRightWidth = 1;
                Root.style.borderBottomWidth = 1;
                Root.style.borderLeftWidth = 1;
                Root.style.borderTopColor = UITheme.PanelBorder;
                Root.style.borderRightColor = UITheme.PanelBorder;
                Root.style.borderBottomColor = UITheme.PanelBorder;
                Root.style.borderLeftColor = UITheme.PanelBorder;
                Root.style.overflow = Overflow.Visible;
                Root.style.paddingLeft = 12;
                Root.style.paddingRight = 12;
                Root.style.paddingTop = 8;
                Root.style.paddingBottom = 8;
                Root.style.alignItems = Align.Center;
                Root.style.justifyContent = Justify.Center;

                if (isSelf)
                {
                    Root.style.minHeight = 150;
                    Root.style.flexDirection = FlexDirection.Column;

                    _tableCharacterImage = new Image
                    {
                        name = "SelfTableCharacter",
                        scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore
                    };
                    _tableCharacterImage.style.position = Position.Absolute;
                    _tableCharacterImage.style.left = 16;
                    _tableCharacterImage.style.bottom = -8;
                    _tableCharacterImage.style.width = 170;
                    _tableCharacterImage.style.height = 208;
                    _tableCharacterImage.style.display = DisplayStyle.None;
                    Root.Add(_tableCharacterImage);
                }
                else if (name == "left" || name == "right")
                {
                    Root.style.width = 210;
                    Root.style.minHeight = 246;
                    Root.style.flexShrink = 0;
                    Root.style.flexDirection = FlexDirection.Column;
                }
                else
                {
                    Root.style.minHeight = 150;
                    Root.style.flexDirection = FlexDirection.Column;
                }

                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                header.style.justifyContent = Justify.Center;
                Root.Add(header);

                _avatarSlot = new VisualElement();
                _avatarSlot.style.marginRight = 7;
                _avatarSlot.style.position = Position.Relative;
                _avatarSlot.style.overflow = Overflow.Visible;
                header.Add(_avatarSlot);

                _avatarImageHost = new VisualElement { name = $"AvatarImage-{name}" };
                _avatarImageHost.style.flexShrink = 0;
                _avatarSlot.Add(_avatarImageHost);

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

                _chatBubble = new VisualElement { name = $"ChatBubble-{name}" };
                _chatBubble.style.position = Position.Absolute;
                _chatBubble.style.minWidth = 38;
                _chatBubble.style.minHeight = 42;
                _chatBubble.style.maxWidth = _chatBubbleMaxWidth;
                _chatBubble.style.flexGrow = 0;
                _chatBubble.style.flexShrink = 1;
                _chatBubble.style.paddingLeft = 12;
                _chatBubble.style.paddingRight = 12;
                _chatBubble.style.paddingTop = 7;
                _chatBubble.style.paddingBottom = 7;
                _chatBubble.style.backgroundColor = new Color(1f, 0.995f, 0.97f, 1f);
                _chatBubble.style.borderTopLeftRadius = 999;
                _chatBubble.style.borderTopRightRadius = 999;
                _chatBubble.style.borderBottomLeftRadius = 999;
                _chatBubble.style.borderBottomRightRadius = 999;
                _chatBubble.style.borderTopWidth = 2;
                _chatBubble.style.borderRightWidth = 2;
                _chatBubble.style.borderBottomWidth = 2;
                _chatBubble.style.borderLeftWidth = 2;
                var bubbleBorder = new Color(0.16f, 0.14f, 0.12f, 1f);
                _chatBubble.style.borderTopColor = bubbleBorder;
                _chatBubble.style.borderRightColor = bubbleBorder;
                _chatBubble.style.borderBottomColor = bubbleBorder;
                _chatBubble.style.borderLeftColor = bubbleBorder;
                _chatBubble.style.display = DisplayStyle.None;
                _chatBubble.pickingMode = PickingMode.Ignore;

                _chatBubbleLabel = new Label { name = $"ChatBubbleText-{name}" };
                _chatBubbleLabel.style.position = Position.Absolute;
                _chatBubbleLabel.style.fontSize = 15;
                _chatBubbleLabel.style.minHeight = 18;
                _chatBubbleLabel.style.whiteSpace = WhiteSpace.Normal;
                _chatBubbleLabel.style.color = UITheme.TextPrimary;
                _chatBubbleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _chatBubbleLabel.style.display = DisplayStyle.None;
                _chatBubbleLabel.pickingMode = PickingMode.Ignore;
                ConfigureBubbleTail(_chatBubble, name, bubbleBorder);
                Root.Add(_chatBubble);
                Root.Add(_chatBubbleLabel);
            }

            public void SetStationBackground(Sprite stationArt)
            {
                if (_stationBackground == stationArt)
                    return;

                _stationBackground = stationArt;
                Root.style.backgroundImage = stationArt != null
                    ? new StyleBackground(stationArt)
                    : new StyleBackground(StyleKeyword.None);
                Root.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                Root.style.backgroundColor = stationArt != null ? Color.white : UITheme.TableSeatGlass;
            }

            public void SetCharacterTexture(Texture texture, bool visible)
            {
                if (_tableCharacterImage == null)
                    return;

                _tableCharacterImage.image = texture;
                _tableCharacterImage.style.display = visible && texture != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void Clear()
            {
                CardRow.Clear();
                HideChatBubble();
            }

            public void ShowChatBubble(string text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    return;

                _bubbleVersion++;
                int version = _bubbleVersion;
                _bubbleDelayItem?.Pause();
                _bubbleFadeItem?.Pause();
                _chatBubbleLabel.text = text;
                var chatFont = Resources.Load<Font>("Fonts/CaboChinese");
                if (chatFont != null)
                    _chatBubbleLabel.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(chatFont));
                else
                    UITheme.ApplyBodyFont(_chatBubbleLabel);
                _chatBubbleLabel.style.color = UITheme.TextPrimary;
                int characterCount = StringInfo.ParseCombiningCharacters(text).Length;
                _chatBubbleWidth = Mathf.Clamp(42f + characterCount * 16f, 66f, _chatBubbleMaxWidth);
                _chatBubble.style.width = _chatBubbleWidth;
                _chatBubble.style.opacity = 1f;
                _chatBubble.style.display = DisplayStyle.Flex;
                _chatBubbleLabel.style.opacity = 1f;
                _chatBubbleLabel.style.display = DisplayStyle.Flex;
                ConfigureBubblePosition();
                _bubblePositionItem?.Pause();
                _bubblePositionItem = _chatBubble.schedule.Execute(ConfigureBubblePosition);
                _bubblePositionItem.ExecuteLater(1);
                _chatBubble.BringToFront();
                _chatBubbleLabel.BringToFront();
                _bubbleDelayItem = _chatBubble.schedule.Execute(() => BeginBubbleFade(version));
                _bubbleDelayItem.ExecuteLater(3150);
            }

            void BeginBubbleFade(int version)
            {
                if (version != _bubbleVersion)
                    return;

                int step = 0;
                _bubbleFadeItem = _chatBubble.schedule.Execute(() =>
                {
                    if (version != _bubbleVersion)
                    {
                        _bubbleFadeItem?.Pause();
                        return;
                    }

                    step++;
                    _chatBubble.style.opacity = Mathf.Clamp01(1f - step / 7f);
                    _chatBubbleLabel.style.opacity = _chatBubble.style.opacity.value;
                    if (step < 7)
                        return;

                    _bubbleFadeItem?.Pause();
                    _chatBubble.style.display = DisplayStyle.None;
                    _chatBubbleLabel.style.display = DisplayStyle.None;
                    _chatBubble.style.opacity = 1f;
                    _chatBubbleLabel.style.opacity = 1f;
                });
                _bubbleFadeItem.Every(50);
            }

            public void HideChatBubble()
            {
                _bubbleVersion++;
                _bubbleDelayItem?.Pause();
                _bubbleFadeItem?.Pause();
                _bubblePositionItem?.Pause();
                _chatBubble.style.display = DisplayStyle.None;
                _chatBubbleLabel.style.display = DisplayStyle.None;
                _chatBubble.style.opacity = 1f;
                _chatBubbleLabel.style.opacity = 1f;
            }

            public void RefreshChatBubblePosition()
            {
                if (_chatBubble == null || _chatBubble.resolvedStyle.display == DisplayStyle.None)
                    return;
                ConfigureBubblePosition();
            }

            void ConfigureBubblePosition()
            {
                Rect avatarBounds = _avatarSlot.worldBound;
                Rect rootBounds = Root.worldBound;
                if (avatarBounds.width <= 0f || rootBounds.width <= 0f)
                    return;

                float avatarCenterX = avatarBounds.center.x - rootBounds.xMin;
                float avatarCenterY = avatarBounds.center.y - rootBounds.yMin;
                const float bubbleHeight = 42f;
                float left;
                float top;
                if (_seatName == "self")
                {
                    left = avatarCenterX - 18f;
                    top = avatarBounds.yMin - rootBounds.yMin - bubbleHeight - 6f;
                }
                else if (_seatName == "top")
                {
                    left = avatarBounds.xMax - rootBounds.xMin + 6f;
                    top = avatarCenterY - 21f;
                }
                else if (_seatName == "left")
                {
                    left = avatarBounds.xMax - rootBounds.xMin + 6f;
                    top = avatarCenterY - 21f;
                }
                else
                {
                    left = avatarBounds.xMin - rootBounds.xMin - _chatBubbleWidth - 6f;
                    top = avatarCenterY - 21f;
                }
                SetBubbleRect(left, top, bubbleHeight);
            }

            void SetBubbleRect(float left, float top, float bubbleHeight)
            {
                _chatBubble.style.left = left;
                _chatBubble.style.top = top;
                _chatBubble.style.width = _chatBubbleWidth;
                _chatBubble.style.minHeight = bubbleHeight;
                _chatBubbleLabel.style.left = left + 12f;
                _chatBubbleLabel.style.top = top + 8f;
                _chatBubbleLabel.style.width = Mathf.Max(36f, _chatBubbleWidth - 24f);
                _chatBubbleLabel.style.height = Mathf.Max(18f, bubbleHeight - 16f);
            }

            static void ConfigureBubbleTail(VisualElement bubble, string seatName, Color borderColor)
            {
                string direction = seatName == "self" ? "down" : seatName == "top" ? "left" : seatName == "left" ? "left" : "right";
                var outer = CreateTriangle(direction, 10, borderColor);
                var inner = CreateTriangle(direction, 7, new Color(1f, 0.995f, 0.97f, 1f));
                PositionTail(outer, direction, false);
                PositionTail(inner, direction, true);
                bubble.Add(outer);
                bubble.Add(inner);
            }

            static VisualElement CreateTriangle(string direction, float size, Color color)
            {
                var triangle = new VisualElement();
                triangle.style.position = Position.Absolute;
                triangle.style.width = 0;
                triangle.style.height = 0;
                triangle.pickingMode = PickingMode.Ignore;
                if (direction == "up" || direction == "down")
                {
                    triangle.style.borderLeftWidth = size;
                    triangle.style.borderRightWidth = size;
                    triangle.style.borderLeftColor = Color.clear;
                    triangle.style.borderRightColor = Color.clear;
                    if (direction == "up")
                    {
                        triangle.style.borderBottomWidth = size + 3;
                        triangle.style.borderBottomColor = color;
                    }
                    else
                    {
                        triangle.style.borderTopWidth = size + 3;
                        triangle.style.borderTopColor = color;
                    }
                }
                else
                {
                    triangle.style.borderTopWidth = size;
                    triangle.style.borderBottomWidth = size;
                    triangle.style.borderTopColor = Color.clear;
                    triangle.style.borderBottomColor = Color.clear;
                    if (direction == "left")
                    {
                        triangle.style.borderRightWidth = size + 3;
                        triangle.style.borderRightColor = color;
                    }
                    else
                    {
                        triangle.style.borderLeftWidth = size + 3;
                        triangle.style.borderLeftColor = color;
                    }
                }
                return triangle;
            }

            static void PositionTail(VisualElement tail, string direction, bool inner)
            {
                float inset = inner ? 2f : 0f;
                if (direction == "up")
                {
                    tail.style.left = 8 + inset;
                    tail.style.top = inner ? -7 : -11;
                }
                else if (direction == "down")
                {
                    tail.style.left = 8 + inset;
                    tail.style.bottom = inner ? -7 : -11;
                }
                else if (direction == "left")
                {
                    tail.style.left = inner ? -7 : -11;
                    tail.style.top = 9 + inset;
                }
                else
                {
                    tail.style.right = inner ? -7 : -11;
                    tail.style.top = 9 + inset;
                }
            }

            public void RenderHeader(string name, int score, bool isCurrentTurn, string tag, bool isCaboCaller = false, string avatarPath = "")
            {
                string avatarKey = $"{name}|{avatarPath}";
                if (_avatarCacheKey != avatarKey)
                {
                    _avatarImageHost.Clear();
                    _avatarImageHost.Add(PlayerProfileStore.CreateAvatarVisual(name, avatarPath, 32));
                    _avatarCacheKey = avatarKey;
                }
                _name.text = name;
                _score.text = $"总分 {score}";
                _tag.text = isCurrentTurn && isCaboCaller ? "CABO 回合" : isCurrentTurn ? "回合" : tag;
                _tag.style.color = isCaboCaller ? UITheme.TextOnDanger : isCurrentTurn ? UITheme.TextOnAccent : UITheme.TextSecondary;
                _tag.style.backgroundColor = isCaboCaller ? UITheme.CaboDanger : isCurrentTurn ? UITheme.TurnHighlight : Color.clear;
                Root.style.backgroundColor = isCaboCaller ? UITheme.CaboSurface : UITheme.TableSeatGlass;
                var borderColor = isCaboCaller ? UITheme.CaboBorder : isCurrentTurn ? UITheme.TurnBorder : UITheme.TableSoftBorder;
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
