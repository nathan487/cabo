using System;
using System.Collections.Generic;
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
        const int SelfCardWidth = 70;
        const int SelfCardHeight = 96;
        const int OppCardWidth = 46;
        const int OppCardHeight = 64;
        const float QuickMoveDuration = 1.15f;
        const float SkillMoveDuration = 1.20f;
        const float EmptySlotHoldDuration = 0.65f;
        const float SkillHoldDuration = 2.25f;
        const float FlipRevealDuration = 3.0f;

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
        readonly Label _statusLine;
        readonly VisualElement _playArea;
        readonly VisualElement _socialPanel;
        readonly VisualElement _socialContent;
        readonly RoomChatPanel _roomChatPanel;
        readonly Button _logTabButton;
        readonly Button _chatTabButton;
        readonly VisualElement _animationLayer;

        readonly HashSet<int> _selectedOwnSlots = new();
        readonly List<TableFeedEntry> _gameLogEntries = new();
        long _selectedOpponentPlayerId;
        long _lastLoggedActionSequence;
        GameSubState _lastSubState = GameSubState.Idle;
        GameSubState _lastRenderedSubState = GameSubState.Idle;
        long _lastAnimatedActionSequence;
        IVisualElementScheduledItem _drawAnimation;
        VisualElement _temporaryHiddenCard;
        readonly Dictionary<long, VisualElement> _drawnCardMarkers = new();
        readonly Dictionary<VisualElement, int> _pulseVersions = new();
        float _animationQueueUntil;
        long _heldActionSequence;
        long _heldActionSourcePlayerId;
        float _heldActionUntil;
        bool _inspectionActive;
        bool _showChat;
        float _inspectionEndsAt;
        Vector2 _inspectionReturnStart;
        Vector2 _inspectionReturnEnd;
        Color _inspectionReturnColor;

        public GameTablePanel(VisualElement root, GameFlow flow)
        {
            _root = root;
            _flow = flow;

            _container = new VisualElement { name = "CaboGameTable" };
            StretchToParent(_container);
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.paddingLeft = 22;
            _container.style.paddingRight = 22;
            _container.style.paddingTop = 16;
            _container.style.paddingBottom = 16;
            _container.style.backgroundColor = new Color(0.035f, 0.10f, 0.085f);
            root.Add(_container);

            _animationLayer = new VisualElement { name = "CardAnimationLayer" };
            _animationLayer.pickingMode = PickingMode.Ignore;
            _animationLayer.style.position = Position.Absolute;
            _animationLayer.style.left = 0;
            _animationLayer.style.right = 0;
            _animationLayer.style.top = 0;
            _animationLayer.style.bottom = 0;
            root.Add(_animationLayer);

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
            _centerTable.style.backgroundColor = new Color(0.02f, 0.22f, 0.16f);
            _centerTable.style.borderTopLeftRadius = 22;
            _centerTable.style.borderTopRightRadius = 22;
            _centerTable.style.borderBottomLeftRadius = 22;
            _centerTable.style.borderBottomRightRadius = 22;
            _centerTable.style.borderTopWidth = 2;
            _centerTable.style.borderRightWidth = 2;
            _centerTable.style.borderBottomWidth = 2;
            _centerTable.style.borderLeftWidth = 2;
            _centerTable.style.borderTopColor = new Color(0.12f, 0.45f, 0.33f);
            _centerTable.style.borderRightColor = new Color(0.12f, 0.45f, 0.33f);
            _centerTable.style.borderBottomColor = new Color(0.12f, 0.45f, 0.33f);
            _centerTable.style.borderLeftColor = new Color(0.12f, 0.45f, 0.33f);
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
            _pileRow.style.marginBottom = 8;
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
            _inspectionZone.style.backgroundColor = new Color(0.035f, 0.18f, 0.15f);
            _inspectionZone.style.borderTopLeftRadius = 8;
            _inspectionZone.style.borderTopRightRadius = 8;
            _inspectionZone.style.borderBottomLeftRadius = 8;
            _inspectionZone.style.borderBottomRightRadius = 8;
            _inspectionZone.style.borderTopWidth = 1;
            _inspectionZone.style.borderRightWidth = 1;
            _inspectionZone.style.borderBottomWidth = 1;
            _inspectionZone.style.borderLeftWidth = 1;
            SetBorderColor(_inspectionZone, new Color(0.14f, 0.40f, 0.34f));
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
            _actionPanel.style.backgroundColor = new Color(0.025f, 0.13f, 0.11f);
            _actionPanel.style.borderTopLeftRadius = 8;
            _actionPanel.style.borderTopRightRadius = 8;
            _actionPanel.style.borderBottomLeftRadius = 8;
            _actionPanel.style.borderBottomRightRadius = 8;
            _centerTable.Add(_actionPanel);

            _actionTitle = new Label();
            _actionTitle.style.fontSize = 16;
            _actionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _actionTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionPanel.Add(_actionTitle);

            _actionBody = new Label();
            _actionBody.style.fontSize = 12;
            _actionBody.style.whiteSpace = WhiteSpace.Normal;
            _actionBody.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionBody.style.marginTop = 4;
            _actionBody.style.marginBottom = 6;
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
            _statusLine.style.color = new Color(0.86f, 0.78f, 0.48f);
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
            _socialPanel.style.backgroundColor = new Color(0.025f, 0.13f, 0.11f);
            _socialPanel.style.borderTopLeftRadius = 8;
            _socialPanel.style.borderTopRightRadius = 8;
            _socialPanel.style.borderBottomLeftRadius = 8;
            _socialPanel.style.borderBottomRightRadius = 8;
            _socialPanel.style.overflow = Overflow.Hidden;
            SetBorderWidth(_socialPanel, 1);
            SetBorderColor(_socialPanel, new Color(0.12f, 0.32f, 0.27f));
            _container.Add(_socialPanel);

            var socialTabs = new VisualElement();
            socialTabs.style.flexDirection = FlexDirection.Row;
            socialTabs.style.flexShrink = 0;
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
        }

        public void SetVisible(bool visible)
        {
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Tick()
        {
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

            _roundLabel.text = $"第 {state.RoundNumber} 轮  第 {state.TurnNumber} 回合";
            _turnLabel.text = BuildTurnText(state, visualCurrentPlayerId, holdingPreviousAction);

            RenderSeats(state, visualCurrentPlayerId);
            RenderPiles(state);
            RenderActionPanel(state, deferNewTurnActions);
            UpdateGameLogFromState(state);
            RenderSocialPanel(state);

            _statusLine.text = ShouldShowActionStatus(_flow.SubState) ? BuildStatusText(state) : "";
            _statusLine.style.display = string.IsNullOrWhiteSpace(_statusLine.text) ? DisplayStyle.None : DisplayStyle.Flex;

            if (pendingAction != null)
                EnqueueActionAnimation(pendingAction);

            _lastRenderedSubState = _flow.SubState;
        }

        public void RenderReveal()
        {
            var state = _flow.State;
            _roundLabel.text = $"第 {state.RoundNumber} 轮结算";
            _turnLabel.text = "所有手牌已翻开计分";
            HideSeatsForOverlay();
            RenderPiles(state);
            RenderSocialPanel(state);

            _drawnCardSlot.Clear();
            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            var title = new Label("本轮得分");
            StylePanelTitle(title);
            _actionPanel.Add(title);

            foreach (var result in state.LastRoundResults)
                _actionPanel.Add(CreateRevealRow(result));

            AddInterRoundControls(state);
        }

        public void RenderGameOver()
        {
            _roundLabel.text = "游戏结束";
            _turnLabel.text = "最终排名";
            HideSeatsForOverlay();
            _drawnCardSlot.Clear();
            _pileRow.Clear();
            RenderSocialPanel(_flow.State);

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

        void RenderSeats(GameState state, long visualCurrentPlayerId)
        {
            _selfSeat.Root.style.display = DisplayStyle.Flex;
            foreach (var seat in _opponentSeats)
            {
                seat.Root.style.display = DisplayStyle.Flex;
                seat.Clear();
            }

            var myInfo = state.Players.Find(p => p.PlayerId == state.MyPlayerId);
            bool selfCabo = state.SteadyCallerId != 0 && state.SteadyCallerId == state.MyPlayerId;
            _selfSeat.RenderHeader(myInfo?.Nickname ?? "你", myInfo?.TotalScore ?? 0, visualCurrentPlayerId == state.MyPlayerId, selfCabo ? "CABO" : "你", selfCabo,
                PlayerProfileStore.GetAvatarPathForPlayer(state.MyPlayerId, true));
            _selfSeat.CardRow.Clear();
            for (int i = 0; i < state.MyCards.Count; i++)
            {
                int slot = i;
                var card = state.MyCards[i];
                _selfSeat.CardRow.Add(CreateCard(
                    card.IsKnown,
                    card.Value,
                    SelfCardWidth,
                    SelfCardHeight,
                    _selectedOwnSlots.Contains(slot),
                    IsOwnSlotClickable(_flow.SubState),
                    () => OnOwnSlotClicked(slot)));
            }

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
                _opponentSeats[i].CardRow.Clear();

                int cardCount = Mathf.Max(0, player.CardCount);
                for (int slot = 0; slot < cardCount; slot++)
                {
                    int targetSlot = slot;
                    _opponentSeats[i].CardRow.Add(CreateCard(
                        false,
                        0,
                        OppCardWidth,
                        OppCardHeight,
                        selected,
                        IsOpponentSlotClickable(_flow.SubState, player.PlayerId),
                        () => OnOpponentSlotClicked(player.PlayerId, targetSlot)));
                }
            }
        }

        void RenderPiles(GameState state)
        {
            _pileRow.Clear();
            _drawPile.Clear();
            _discardPile.Clear();

            _drawPile.Add(CreatePileCard("牌库", "CABO", state.DrawPileCount.ToString(), false));
            _discardPile.Add(CreatePileCard("弃牌堆", state.DiscardTopValue >= 0 ? state.DiscardTopValue.ToString() : "-", state.DiscardPileCount.ToString(), true));

            _pileRow.Add(_drawPile);
            _pileRow.Add(_discardPile);
        }

        void RenderActionPanel(GameState state, bool deferNewTurnActions)
        {
            ResetActionPanelForGame();
            _drawnCardSlot.Clear();
            _drawnCardSlot.style.display = DisplayStyle.None;
            _buttonRow.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            if (deferNewTurnActions)
            {
                _actionTitle.text = "动作展示中";
                _actionBody.text = "等待上一名玩家的操作动画完成。";
                return;
            }

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
                    _drawnCardSlot.style.display = DisplayStyle.Flex;
                    _drawnCardSlot.Add(CreateDrawnCard(state, 48, 64));
                    AddActionButton("弃牌", () => _flow.DoDiscardDrawn(false), true);
                    AddActionButton("替换", () => _flow.BeginReplaceWithDrawn(), true);
                    AddActionButton(BuildSkillButtonText(state.DrawnCardSkill), () => _flow.DoDiscardDrawn(true), state.DrawnCardSkill > 0);
                    break;

                case GameSubState.AwaitingReplaceSlots:
                    _actionTitle.text = "用抽牌替换";
                    _actionBody.text = "选择一张或多张自己的手牌，然后确认。";
                    _drawnCardSlot.style.display = DisplayStyle.Flex;
                    _drawnCardSlot.Add(CreateDrawnCard(state, 48, 64));
                    AddActionButton("确认替换", () => ConfirmReplace(), _selectedOwnSlots.Count > 0);
                    AddActionButton("改为弃牌", () => _flow.DoDiscardDrawn(false), true);
                    break;

                case GameSubState.AwaitingTakeSlots:
                    _actionTitle.text = "拿弃牌";
                    _actionBody.text = "选择一张或多张自己的手牌，与弃牌堆顶交换。";
                    AddActionButton("确认拿牌", () => ConfirmTakeDiscard(), _selectedOwnSlots.Count > 0);
                    break;

                case GameSubState.SkillPeekSlot:
                    _actionTitle.text = "看牌技能";
                    _actionBody.text = "选择一张自己的牌私下查看。";
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSpyTarget:
                    _actionTitle.text = "偷看技能";
                    _actionBody.text = "选择一名对手。";
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSpySlot:
                    _actionTitle.text = "偷看技能";
                    _actionBody.text = "选择该对手的一张牌。";
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapMySlot:
                    _actionTitle.text = "换牌技能";
                    _actionBody.text = "选择自己要交换的一张牌。";
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapTargetPlayer:
                    _actionTitle.text = "换牌技能";
                    _actionBody.text = "选择一名对手。";
                    AddActionButton("跳过技能", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapTargetSlot:
                    _actionTitle.text = "换牌技能";
                    _actionBody.text = "选择该对手要交换的一张牌。";
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

            _socialContent.Clear();
            _socialContent.style.display = _showChat ? DisplayStyle.None : DisplayStyle.Flex;
            _roomChatPanel.Root.style.display = _showChat ? DisplayStyle.Flex : DisplayStyle.None;

            if (_showChat)
            {
                _roomChatPanel.Render();
            }
            else
            {
                if (_gameLogEntries.Count == 0 && state.LastActionSequence == 0)
                    AddFeedPlaceholder("本局动作会显示在这里");
                else
                    RenderFeed(_gameLogEntries, "等待第一条游戏日志");
            }
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
            empty.style.color = new Color(0.62f, 0.70f, 0.67f);
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
            row.style.backgroundColor = new Color(0.035f, 0.16f, 0.135f);
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
                name.style.color = new Color(0.84f, 0.91f, 0.86f);
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
                message.style.color = new Color(0.88f, 0.90f, 0.86f);
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
            var wrap = new VisualElement();
            wrap.style.alignItems = Align.Center;
            wrap.Add(CreateCard(true, state.DrawnCardValue, width, height, false, false, null));

            var label = new Label(GetSkillName(state.DrawnCardSkill));
            label.style.fontSize = 12;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = new Color(0.95f, 0.82f, 0.42f);
            label.style.marginTop = 4;
            wrap.Add(label);
            return wrap;
        }

        void ScheduleDrawAnimation(int value)
        {
            _root.schedule.Execute(() => PlayDrawAnimation(value)).ExecuteLater(1);
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
                PeekedValue = state.LastPeekedValue,
                SourcePlayerBounds = GetPlayerBounds(state.LastActionSourcePlayerId),
                TargetPlayerBounds = GetPlayerBounds(state.LastActionTargetPlayerId),
                DrawPileBounds = _drawPile.worldBound,
                DiscardPileBounds = _discardPile.worldBound
            };

            snapshot.SelectedSlots.AddRange(state.LastActionSelectedSlots);
            if (snapshot.SelectedSlots.Count == 0
                && (snapshot.ActionType == ActionType.ReplaceWithDrawn || snapshot.ActionType == ActionType.TakeFromDiscard)
                && snapshot.SourceSlot >= 0)
            {
                snapshot.SelectedSlots.Add(snapshot.SourceSlot);
            }

            snapshot.SourceSlotBounds.AddRange(CaptureSlotBounds(snapshot.SourcePlayerId, snapshot.SelectedSlots));
            snapshot.SourceSwapSlotBounds.AddRange(CaptureSlotBounds(snapshot.SourcePlayerId, new[] { snapshot.SourceSlot }));
            snapshot.TargetSlotBounds.AddRange(CaptureSlotBounds(snapshot.TargetPlayerId, new[] { snapshot.TargetSlot }));
            snapshot.SourceHandBounds.AddRange(CaptureAllSlotBounds(snapshot.SourcePlayerId));
            return snapshot;
        }

        void EnqueueActionAnimation(ActionAnimationSnapshot action)
        {
            float now = Time.realtimeSinceStartup;
            float delay = Mathf.Max(0f, _animationQueueUntil - now);
            float duration = EstimateActionAnimationDuration(action);
            _animationQueueUntil = now + delay + duration;
            HoldTurnDisplay(action, _animationQueueUntil);
            ScheduleAfter(delay, () => PlayActionAnimation(action));
            ScheduleAfter(delay + duration, () => ReleaseTurnDisplay(action));
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
            RenderGame();
        }

        void PlayDrawAnimation(int value)
        {
            var start = CenterOf(_drawPile.worldBound);
            var targetBounds = _drawnCardSlot.worldBound;
            if (targetBounds.width <= 1 || targetBounds.height <= 1)
                targetBounds = _actionPanel.worldBound;
            var end = CenterOf(targetBounds);
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);

            if (start == Vector2.zero || end == Vector2.zero)
                return;

            _drawAnimation?.Pause();
            _animationLayer.Clear();

            const int width = 48;
            const int height = 64;
            var card = CreateCard(true, value, width, height, false, false, null);
            card.style.position = Position.Absolute;
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            _animationLayer.Add(card);

            float startedAt = Time.realtimeSinceStartup;
            const float duration = 1.10f;
            _drawAnimation = _root.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                var p = Vector2.Lerp(start, end, eased) - rootOrigin;
                card.style.left = p.x - width * 0.5f;
                card.style.top = p.y - height * 0.5f;
                card.style.opacity = Mathf.Lerp(1f, 0.15f, Mathf.Max(0f, (t - 0.72f) / 0.28f));

                if (t >= 1f)
                {
                    _drawAnimation?.Pause();
                    _animationLayer.Clear();
                }
            }).Every(16);
        }

        void PlayActionAnimation(ActionAnimationSnapshot action)
        {
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
            var color = new Color(0.30f, 0.55f, 1f);
            var start = CenterOf(action.DrawPileBounds);
            if (start == Vector2.zero)
                start = CenterOf(_drawPile.worldBound);
            var end = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            if (end == Vector2.zero)
                end = CenterOf(action.SourcePlayerBounds);

            RemoveDrawnMarker(action.SourcePlayerId);
            PlayMovingCard(start, end, false, 0, color, QuickMoveDuration, false,
                () => ShowDrawnMarker(action.SourcePlayerId, end, color));
            PulsePlayer(action.SourcePlayerId, color, QuickMoveDuration + 0.35f);
        }

        void PlayDiscardDrawnAction(ActionAnimationSnapshot action)
        {
            var color = GetSkillColor(action.Skill);
            var start = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            var end = CenterOf(action.DiscardPileBounds);
            if (end == Vector2.zero)
                end = CenterOf(_discardPile.worldBound);

            RemoveDrawnMarker(action.SourcePlayerId);
            PlayMovingCard(start, end, true, action.DiscardTopValue, color, QuickMoveDuration, true, null);
            PulsePlayer(action.SourcePlayerId, color, QuickMoveDuration + 0.45f);
        }

        void PlayReplaceWithDrawnAction(ActionAnimationSnapshot action)
        {
            var color = action.IncomingCardValue >= 0 ? GetFaceColor(action.IncomingCardValue) : new Color(1f, 0.72f, 0.30f);
            if (!action.ExchangeSucceeded)
            {
                PlayFailedExchangeAction(action, color);
                return;
            }

            var discardCenter = CenterOf(action.DiscardPileBounds);
            if (discardCenter == Vector2.zero)
                discardCenter = CenterOf(_discardPile.worldBound);
            var incomingTarget = GetPrimarySelectedSlotCenter(action);
            if (incomingTarget == Vector2.zero)
                incomingTarget = CenterOf(action.SourcePlayerBounds);

            float discardPhase = GetDiscardPhaseDuration(action.SourceSlotBounds.Count);
            float incomingDelay = discardPhase + EmptySlotHoldDuration;
            float total = incomingDelay + QuickMoveDuration + 0.35f;
            ShowSelectedSlotBlanks(action, color, total);
            HideCurrentSlots(action.SourcePlayerId, action.SelectedSlots, total);
            if (action.SourceSlotBounds.Count > 1)
                ShowSourceHandOverlayDuringReflow(action, color, total);

            for (int i = 0; i < action.SourceSlotBounds.Count; i++)
            {
                var slot = action.SourceSlotBounds[i];
                float offset = GetDiscardStagger(i, action.SourceSlotBounds.Count);
                ScheduleAfter(offset, () =>
                    PlayMovingCardFromBounds(slot.Bounds, discardCenter, false, 0, color, QuickMoveDuration, true, null));
            }

            bool revealIncoming = action.SourcePlayerId == _flow.State.MyPlayerId && action.IncomingCardValue >= 0;
            ScheduleAfter(incomingDelay, () =>
            {
                var incomingStart = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
                RemoveDrawnMarker(action.SourcePlayerId);
                PlayMovingCard(incomingStart, incomingTarget, revealIncoming, action.IncomingCardValue, color, QuickMoveDuration, true, null);
            });
            PulsePlayer(action.SourcePlayerId, color, total);
        }

        void PlayTakeFromDiscardAction(ActionAnimationSnapshot action)
        {
            var color = new Color(1f, 0.85f, 0.42f);
            if (!action.ExchangeSucceeded)
            {
                PlayFailedExchangeAction(action, color);
                return;
            }

            var discardCenter = CenterOf(action.DiscardPileBounds);
            if (discardCenter == Vector2.zero)
                discardCenter = CenterOf(_discardPile.worldBound);
            var incomingTarget = GetPrimarySelectedSlotCenter(action);
            if (incomingTarget == Vector2.zero)
                incomingTarget = CenterOf(action.SourcePlayerBounds);

            float discardPhase = GetDiscardPhaseDuration(action.SourceSlotBounds.Count);
            float incomingDelay = discardPhase + EmptySlotHoldDuration;
            float total = incomingDelay + QuickMoveDuration + 0.35f;
            ShowSelectedSlotBlanks(action, color, total);
            HideCurrentSlots(action.SourcePlayerId, action.SelectedSlots, total);
            if (action.SourceSlotBounds.Count > 1)
                ShowSourceHandOverlayDuringReflow(action, color, total);

            for (int i = 0; i < action.SourceSlotBounds.Count; i++)
            {
                var slot = action.SourceSlotBounds[i];
                float offset = GetDiscardStagger(i, action.SourceSlotBounds.Count);
                ScheduleAfter(offset, () =>
                    PlayMovingCardFromBounds(slot.Bounds, discardCenter, false, 0, color, QuickMoveDuration, true, null));
            }

            int incomingValue = action.IncomingCardValue >= 0 ? action.IncomingCardValue : action.DiscardTopValue;
            ScheduleAfter(incomingDelay, () =>
                PlayMovingCard(discardCenter, incomingTarget, true, incomingValue, color, QuickMoveDuration, true, null));
            PulsePlayer(action.SourcePlayerId, color, total);
        }

        void PlayFailedExchangeAction(ActionAnimationSnapshot action, Color color)
        {
            float total = QuickMoveDuration + EmptySlotHoldDuration + 0.65f;
            foreach (var slot in action.SourceSlotBounds)
                ShakeCardAt(slot.Bounds, color, 1.15f);
            if (action.SourceSlotBounds.Count == 0)
                PulsePlayer(action.SourcePlayerId, color, 1.15f);

            var start = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
            var end = CenterOf(action.SourcePlayerBounds);
            ScheduleAfter(0.35f, () =>
            {
                var markerStart = GetDrawnMarkerCenter(action.SourcePlayerId, action.SourcePlayerBounds);
                RemoveDrawnMarker(action.SourcePlayerId);
                PlayMovingCard(markerStart, end, action.SourcePlayerId == _flow.State.MyPlayerId, action.IncomingCardValue, color, QuickMoveDuration, true, null);
            });

            if (action.DrewExtraPenaltyCard)
            {
                var deck = CenterOf(action.DrawPileBounds);
                ScheduleAfter(0.70f, () =>
                    PlayMovingCard(deck, end + new Vector2(18f, 0f), false, 0, new Color(0.30f, 0.55f, 1f), QuickMoveDuration, true, null));
            }
            PulsePlayer(action.SourcePlayerId, color, total);
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
                PlaySpyCardInspection(action, privateValue, color);
                return;
            }

            if (action.Skill == SkillType.PeekSelf)
            {
                int privateValue = action.SourcePlayerId == _flow.State.MyPlayerId ? GetKnownOwnCardValue(action.SourceSlot, action.PeekedValue) : -1;
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
                PulsePlayer(action.SourcePlayerId, color, SkillMoveDuration + SkillHoldDuration);
                PulsePlayer(action.TargetPlayerId, color, SkillMoveDuration + SkillHoldDuration);
                return;
            }

            float total = EmptySlotHoldDuration + SkillMoveDuration + EmptySlotHoldDuration;
            ShowSlotBlank(sourceBounds, color, total);
            ShowSlotBlank(targetBounds, color, total);
            HideCurrentSlot(action.SourcePlayerId, action.SourceSlot, total);
            HideCurrentSlot(action.TargetPlayerId, action.TargetSlot, total);

            ScheduleAfter(EmptySlotHoldDuration, () =>
            {
                PlayMovingCard(source, target, false, 0, color, SkillMoveDuration, true, null);
                PlayMovingCard(target, source, false, 0, color, SkillMoveDuration, true, null);
            });

            PulsePlayer(action.SourcePlayerId, color, total);
            PulsePlayer(action.TargetPlayerId, color, total);
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

        void PlayMovingCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete)
        {
            PlayMovingCard(start, end, faceUp, value, color, duration, fadeOut, onComplete, 44f, 60f);
        }

        void PlayMovingCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration, bool fadeOut, Action onComplete, float width, float height)
        {
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            if (start == Vector2.zero || end == Vector2.zero || !IsFinite(start) || !IsFinite(end) || !IsFinite(rootOrigin))
                return;

            var card = CreateCard(faceUp, value >= 0 ? value : 0, Mathf.RoundToInt(width), Mathf.RoundToInt(height), false, false, null);
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
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                var p = Vector2.Lerp(start, end, eased) - rootOrigin;
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
            long playerId = action.SourcePlayerId;
            int slot = action.SourceSlot;
            var bounds = GetCapturedSlotBounds(action.SourceSwapSlotBounds, playerId, slot);
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
            {
                PulsePlayer(playerId, color, FlipRevealDuration);
                return;
            }

            bool revealValue = privateValue >= 0;
            if (playerId == _flow.State.MyPlayerId)
            {
                PulseCard(playerId, slot, color, FlipRevealDuration);
                PlayFlipCardAt(center, revealValue, privateValue, color, "看牌", FlipRevealDuration);
                ShowHeldInspectionCard(center, revealValue, privateValue, color, "看牌", FlipRevealDuration);
                return;
            }

            float total = SkillMoveDuration + SkillHoldDuration + SkillMoveDuration + EmptySlotHoldDuration;
            ShowSlotBlank(bounds, color, total);
            HideCurrentSlot(playerId, slot, total);

            var inspectAt = GetPlayerInspectionCenter(playerId, action.SourcePlayerBounds);
            if (inspectAt == Vector2.zero)
                inspectAt = CenterOf(action.SourcePlayerBounds);
            if (inspectAt == Vector2.zero)
                inspectAt = center;

            PlayInspectCardRoundTrip(center, inspectAt, false, 0, color, "看牌");
            PulsePlayer(playerId, color, total);
        }

        void PlaySpyCardInspection(ActionAnimationSnapshot action, int privateValue, Color color)
        {
            long sourcePlayerId = action.SourcePlayerId;
            long targetPlayerId = action.TargetPlayerId;
            int targetSlot = action.TargetSlot;
            var targetBounds = GetCapturedSlotBounds(action.TargetSlotBounds, targetPlayerId, targetSlot);
            var start = CenterOf(targetBounds);
            if (start == Vector2.zero)
                start = CenterOf(GetPlayerBounds(targetPlayerId));
            var end = GetPlayerInspectionCenter(sourcePlayerId, action.SourcePlayerBounds);
            if (end == Vector2.zero)
                end = CenterOf(action.SourcePlayerBounds);
            if (start == Vector2.zero || end == Vector2.zero)
            {
                PulseCard(targetPlayerId, targetSlot, color, FlipRevealDuration);
                PulsePlayer(sourcePlayerId, color, FlipRevealDuration);
                return;
            }

            float total = SkillMoveDuration + SkillHoldDuration + SkillMoveDuration + EmptySlotHoldDuration;
            ShowSlotBlank(targetBounds, color, total);
            HideCurrentSlot(targetPlayerId, targetSlot, total);
            PlayInspectCardRoundTrip(start, end, privateValue >= 0, privateValue, color, "偷看");
            PulsePlayer(sourcePlayerId, color, total);
            PulsePlayer(targetPlayerId, color, total);
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
            var color = new Color(1f, 0.82f, 0.22f);
            PulsePlayer(sourcePlayerId, color, 1.65f);

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
            banner.style.color = new Color(0.12f, 0.08f, 0.02f);
            banner.style.backgroundColor = color;
            banner.style.borderTopLeftRadius = 6;
            banner.style.borderTopRightRadius = 6;
            banner.style.borderBottomLeftRadius = 6;
            banner.style.borderBottomRightRadius = 6;
            _animationLayer.Add(banner);

            float startedAt = Time.realtimeSinceStartup;
            const float duration = 1.55f;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
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

            var card = CreateCard(faceUp, value >= 0 ? value : 0, 74, 104, false, false, null);
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
            SetBorderColor(_inspectionZone, new Color(0.14f, 0.40f, 0.34f));
        }

        void SetTemporaryCardVisibility(VisualElement card, bool visible)
        {
            if (card == null) return;
            card.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        List<SlotSnapshot> CaptureSlotBounds(long playerId, IEnumerable<int> slots)
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
                    result.Add(new SlotSnapshot { PlayerId = playerId, Slot = slot, Bounds = bounds });
            }
            return result;
        }

        List<SlotSnapshot> CaptureAllSlotBounds(long playerId)
        {
            var result = new List<SlotSnapshot>();
            var row = GetCardRow(playerId);
            if (row == null)
                return result;

            for (int slot = 0; slot < row.childCount; slot++)
            {
                var bounds = row[slot]?.worldBound ?? Rect.zero;
                if (bounds.width > 1 && bounds.height > 1)
                    result.Add(new SlotSnapshot { PlayerId = playerId, Slot = slot, Bounds = bounds });
            }
            return result;
        }

        float EstimateActionAnimationDuration(ActionAnimationSnapshot action)
        {
            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Spy)
                return SkillMoveDuration + SkillHoldDuration + SkillMoveDuration + 0.35f;
            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.PeekSelf)
                return SkillMoveDuration + SkillHoldDuration + SkillMoveDuration + 0.35f;
            if (action.ActionType == ActionType.UseSkill && action.Skill == SkillType.Swap && action.SwapOccurred)
                return EmptySlotHoldDuration + SkillMoveDuration + EmptySlotHoldDuration + 0.35f;
            if (action.ActionType == ActionType.ReplaceWithDrawn || action.ActionType == ActionType.TakeFromDiscard)
                return GetDiscardPhaseDuration(action.SourceSlotBounds.Count) + EmptySlotHoldDuration + QuickMoveDuration + 0.55f;
            return QuickMoveDuration + 0.55f;
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
            return index * 0.32f;
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
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
                return Vector2.zero;

            float yOffset = -Mathf.Min(34f, bounds.height * 0.30f);
            if (playerId == _flow.State.MyPlayerId)
                yOffset = -Mathf.Min(48f, bounds.height * 0.34f);
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

        void ShowSelectedSlotBlanks(ActionAnimationSnapshot action, Color color, float duration)
        {
            foreach (var slot in action.SourceSlotBounds)
                ShowSlotBlank(slot.Bounds, color, duration);
        }

        void ShowSlotBlank(Rect bounds, Color color, float duration)
        {
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
                return;

            var blank = new VisualElement();
            blank.style.position = Position.Absolute;
            blank.style.marginLeft = 0;
            blank.style.marginRight = 0;
            blank.style.marginTop = 0;
            blank.style.marginBottom = 0;
            blank.style.backgroundColor = new Color(0.015f, 0.08f, 0.07f, 0.88f);
            blank.style.borderTopLeftRadius = 7;
            blank.style.borderTopRightRadius = 7;
            blank.style.borderBottomLeftRadius = 7;
            blank.style.borderBottomRightRadius = 7;
            SetBorderWidth(blank, 2);
            SetBorderColor(blank, color);
            PositionAbsolute(blank, center, bounds.width, bounds.height);
            _animationLayer.BringToFront();
            _animationLayer.Add(blank);
            ScheduleAfter(duration, () => blank.RemoveFromHierarchy());
        }

        void ShowSourceHandOverlayDuringReflow(ActionAnimationSnapshot action, Color color, float duration)
        {
            HideCurrentHandCards(action.SourcePlayerId, duration);

            var selected = new HashSet<int>(action.SelectedSlots);
            foreach (var slot in action.SourceHandBounds)
            {
                if (selected.Contains(slot.Slot))
                    continue;
                ShowStaticCardOverlay(slot.Bounds, color, duration);
            }
        }

        void ShowStaticCardOverlay(Rect bounds, Color color, float duration)
        {
            var center = CenterOf(bounds);
            if (center == Vector2.zero)
                return;

            var card = CreateCard(false, 0, Mathf.RoundToInt(bounds.width), Mathf.RoundToInt(bounds.height), false, false, null);
            card.style.position = Position.Absolute;
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            SetBorderColor(card, new Color(color.r, color.g, color.b, 0.82f));
            PositionAbsolute(card, center, bounds.width, bounds.height);
            _animationLayer.BringToFront();
            _animationLayer.Add(card);
            ScheduleAfter(duration, () => card.RemoveFromHierarchy());
        }

        void HideCurrentSlots(long playerId, IEnumerable<int> slots, float duration)
        {
            if (slots == null)
                return;
            foreach (var slot in slots)
                HideCurrentSlot(playerId, slot, duration);
        }

        void HideCurrentHandCards(long playerId, float duration)
        {
            var row = GetCardRow(playerId);
            if (row == null)
                return;

            for (int i = 0; i < row.childCount; i++)
            {
                var element = row[i];
                element.style.visibility = Visibility.Hidden;
                ScheduleAfter(duration, () =>
                {
                    if (element.panel != null)
                        element.style.visibility = Visibility.Visible;
                });
            }
        }

        void HideCurrentSlot(long playerId, int slot, float duration)
        {
            var element = GetCardElement(playerId, slot);
            if (element == null)
                return;
            element.style.visibility = Visibility.Hidden;
            ScheduleAfter(duration, () =>
            {
                if (element.panel != null)
                    element.style.visibility = Visibility.Visible;
            });
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
                        RenderGame();
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
                SkillType.PeekSelf => new Color(0.45f, 0.90f, 1f),
                SkillType.Spy => new Color(0.95f, 0.72f, 1f),
                SkillType.Swap => new Color(1f, 0.74f, 0.28f),
                _ => new Color(0.86f, 0.78f, 0.48f)
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
            button.style.backgroundColor = selected ? new Color(0.16f, 0.35f, 0.27f) : new Color(0.06f, 0.16f, 0.14f);
            var border = selected ? new Color(0.88f, 0.74f, 0.30f) : new Color(0.16f, 0.34f, 0.28f);
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

        VisualElement CreateCard(bool faceUp, int value, int width, int height, bool selected, bool clickable, Action onClick)
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
            card.style.borderTopColor = selected ? new Color(1f, 0.82f, 0.22f) : new Color(0.78f, 0.78f, 0.72f);
            card.style.borderRightColor = card.style.borderTopColor.value;
            card.style.borderBottomColor = card.style.borderTopColor.value;
            card.style.borderLeftColor = card.style.borderTopColor.value;
            card.style.backgroundColor = faceUp ? GetFaceColor(value) : new Color(0.10f, 0.20f, 0.42f);
            card.style.opacity = clickable ? 1f : 0.96f;

            var valueLabel = new Label(faceUp ? value.ToString() : "CABO");
            valueLabel.style.fontSize = faceUp ? Mathf.RoundToInt(height * 0.34f) : Mathf.RoundToInt(height * 0.16f);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            valueLabel.style.color = faceUp ? new Color(0.10f, 0.10f, 0.12f) : new Color(0.88f, 0.92f, 1f);
            card.Add(valueLabel);

            if (faceUp && value >= 7 && value <= 12)
            {
                var badge = new Label(GetSkillShortName(value));
                badge.style.fontSize = 10;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.color = new Color(0.22f, 0.15f, 0.08f);
                card.Add(badge);
            }

            if (clickable && onClick != null)
                card.RegisterCallback<ClickEvent>(_ => onClick());

            return card;
        }

        VisualElement CreatePileCard(string title, string face, string count, bool faceUp)
        {
            var stack = new VisualElement();
            stack.style.alignItems = Align.Center;
            stack.style.marginLeft = 14;
            stack.style.marginRight = 14;

            var card = new VisualElement();
            card.style.width = 70;
            card.style.height = 92;
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
            card.style.borderTopColor = new Color(0.82f, 0.82f, 0.78f);
            card.style.borderRightColor = new Color(0.82f, 0.82f, 0.78f);
            card.style.borderBottomColor = new Color(0.82f, 0.82f, 0.78f);
            card.style.borderLeftColor = new Color(0.82f, 0.82f, 0.78f);
            card.style.backgroundColor = faceUp ? new Color(0.92f, 0.90f, 0.80f) : new Color(0.08f, 0.14f, 0.34f);

            var label = new Label(face);
            label.style.fontSize = faceUp ? 26 : 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = faceUp ? new Color(0.10f, 0.10f, 0.12f) : Color.white;
            card.Add(label);
            stack.Add(card);

            var caption = new Label($"{title}  {count}");
            caption.style.fontSize = 12;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.style.marginTop = 6;
            stack.Add(caption);
            return stack;
        }

        VisualElement CreateRevealRow(RoundResult result)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 8;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.20f, 0.36f, 0.31f);

            var name = new Label(result.Nickname + (result.IsSteadyCaller ? "  CABO" : "") + (result.IsLowest ? "  最低" : ""));
            name.style.width = 180;
            name.style.fontSize = 13;
            row.Add(name);

            var cards = new VisualElement();
            cards.style.flexDirection = FlexDirection.Row;
            cards.style.flexGrow = 1;
            foreach (var value in result.CardValues)
                cards.Add(CreateCard(true, value, 36, 50, false, false, null));
            row.Add(cards);

            var score = new Label($"{result.HandTotal} + {result.Penalty} = {result.RoundScore}    总分 {result.CumulativeScore}");
            score.style.width = 190;
            score.style.fontSize = 13;
            score.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(score);
            return row;
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
            divider.style.marginTop = 10;
            divider.style.marginBottom = 8;
            divider.style.backgroundColor = new Color(0.22f, 0.42f, 0.36f);
            _actionPanel.Add(divider);

            var readyTitle = new Label("下一轮准备");
            readyTitle.style.fontSize = 15;
            readyTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            readyTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            readyTitle.style.marginBottom = 4;
            _actionPanel.Add(readyTitle);

            var readyGrid = new VisualElement();
            readyGrid.style.flexDirection = FlexDirection.Row;
            readyGrid.style.flexWrap = Wrap.Wrap;
            readyGrid.style.justifyContent = Justify.Center;
            readyGrid.style.marginBottom = 8;
            foreach (var player in state.Players)
                readyGrid.Add(CreateReadyBadge(player, player.PlayerId == state.MyPlayerId));
            _actionPanel.Add(readyGrid);

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.justifyContent = Justify.Center;
            controls.style.flexWrap = Wrap.Wrap;
            _actionPanel.Add(controls);

            controls.Add(CreatePanelButton(myReady ? "已准备" : "准备", () => _flow.SendReady(), !myReady && state.MyPlayerId > 0 && state.RoomId > 0));

            if (isHost)
                controls.Add(CreatePanelButton("开始下一轮", () => _flow.SendStartGame(), allReady && state.MyPlayerId > 0 && state.RoomId > 0));
            else
            {
                var wait = new Label(allReady ? "等待房主开始" : "等待所有玩家准备");
                wait.style.fontSize = 12;
                wait.style.unityTextAlign = TextAnchor.MiddleCenter;
                wait.style.marginTop = 7;
                wait.style.marginLeft = 8;
                wait.style.color = new Color(0.78f, 0.88f, 0.82f);
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
            badge.style.width = 150;
            badge.style.marginLeft = 4;
            badge.style.marginRight = 4;
            badge.style.marginTop = 4;
            badge.style.marginBottom = 4;
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 5;
            badge.style.paddingBottom = 5;
            badge.style.borderTopLeftRadius = 6;
            badge.style.borderTopRightRadius = 6;
            badge.style.borderBottomLeftRadius = 6;
            badge.style.borderBottomRightRadius = 6;
            badge.style.backgroundColor = player.IsReady ? new Color(0.10f, 0.32f, 0.22f) : new Color(0.12f, 0.18f, 0.17f);
            SetBorderWidth(badge, 1);
            SetBorderColor(badge, player.IsReady ? new Color(0.42f, 0.86f, 0.56f) : new Color(0.30f, 0.42f, 0.38f));

            var dot = new VisualElement();
            dot.style.width = 9;
            dot.style.height = 9;
            dot.style.flexShrink = 0;
            dot.style.marginRight = 7;
            dot.style.borderTopLeftRadius = 5;
            dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5;
            dot.style.borderBottomRightRadius = 5;
            dot.style.backgroundColor = player.IsReady ? new Color(0.50f, 1f, 0.58f) : new Color(0.50f, 0.56f, 0.54f);
            badge.Add(dot);

            var name = new Label((isSelf ? "你 " : "") + player.Nickname);
            name.style.flexGrow = 1;
            name.style.fontSize = 12;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.color = Color.white;
            badge.Add(name);

            var readyState = new Label(player.IsReady ? "已准备" : "...");
            readyState.style.fontSize = 11;
            readyState.style.unityTextAlign = TextAnchor.MiddleRight;
            readyState.style.color = player.IsReady ? new Color(0.72f, 1f, 0.75f) : new Color(0.76f, 0.80f, 0.78f);
            badge.Add(readyState);
            return badge;
        }

        Button CreatePanelButton(string text, Action action, bool enabled)
        {
            var button = new Button(action) { text = text };
            button.style.minWidth = 120;
            button.style.height = 34;
            button.style.marginLeft = 5;
            button.style.marginRight = 5;
            button.style.marginTop = 4;
            button.style.marginBottom = 4;
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
        }

        void AddActionButton(string text, Action action, bool enabled)
        {
            var button = new Button(action) { text = text };
            button.style.minWidth = 116;
            button.style.height = 32;
            button.style.marginLeft = 5;
            button.style.marginRight = 5;
            button.style.marginTop = 4;
            button.style.marginBottom = 4;
            button.SetEnabled(enabled);
            _buttonRow.Add(button);
        }

        void OnOwnSlotClicked(int slot)
        {
            switch (_flow.SubState)
            {
                case GameSubState.AwaitingReplaceSlots:
                case GameSubState.AwaitingTakeSlots:
                    if (!_selectedOwnSlots.Add(slot))
                        _selectedOwnSlots.Remove(slot);
                    RenderGame();
                    break;
                case GameSubState.SkillPeekSlot:
                    _flow.DoSkillPeek(slot);
                    break;
                case GameSubState.SkillSwapMySlot:
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
                _flow.DoSkillSpySlot(slot);
                return;
            }
            if (_flow.SubState == GameSubState.SkillSwapTargetPlayer)
            {
                _flow.DoSkillSwapTargetPlayer(playerId);
                _flow.DoSkillSwapTargetSlot(slot);
                return;
            }
            if (_flow.SkillTargetPlayerId != playerId) return;

            if (_flow.SubState == GameSubState.SkillSpySlot)
                _flow.DoSkillSpySlot(slot);
            else if (_flow.SubState == GameSubState.SkillSwapTargetSlot)
                _flow.DoSkillSwapTargetSlot(slot);
        }

        void ConfirmReplace()
        {
            _flow.DoReplaceWithDrawn(ToSortedArray(_selectedOwnSlots));
            _selectedOwnSlots.Clear();
        }

        void ConfirmTakeDiscard()
        {
            _flow.DoTakeFromDiscardSlots(ToSortedArray(_selectedOwnSlots));
            _selectedOwnSlots.Clear();
        }

        void ResetSelectionWhenSubStateChanges(GameSubState subState)
        {
            if (_lastSubState == subState) return;
            _selectedOwnSlots.Clear();
            _selectedOpponentPlayerId = subState == GameSubState.SkillSpySlot || subState == GameSubState.SkillSwapTargetSlot
                ? _flow.SkillTargetPlayerId
                : 0;
            _lastSubState = subState;
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
            if (value <= 3) return new Color(0.86f, 0.96f, 0.82f);
            if (value >= 10) return new Color(0.98f, 0.82f, 0.78f);
            if (value >= 7) return new Color(0.98f, 0.92f, 0.72f);
            return new Color(0.94f, 0.92f, 0.84f);
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

            public SeatView(string name, bool isSelf)
            {
                Root = new VisualElement { name = $"Seat-{name}" };
                Root.style.backgroundColor = new Color(0.025f, 0.13f, 0.11f);
                Root.style.borderTopLeftRadius = 8;
                Root.style.borderTopRightRadius = 8;
                Root.style.borderBottomLeftRadius = 8;
                Root.style.borderBottomRightRadius = 8;
                Root.style.borderTopWidth = 1;
                Root.style.borderRightWidth = 1;
                Root.style.borderBottomWidth = 1;
                Root.style.borderLeftWidth = 1;
                Root.style.borderTopColor = new Color(0.10f, 0.28f, 0.23f);
                Root.style.borderRightColor = new Color(0.10f, 0.28f, 0.23f);
                Root.style.borderBottomColor = new Color(0.10f, 0.28f, 0.23f);
                Root.style.borderLeftColor = new Color(0.10f, 0.28f, 0.23f);
                Root.style.paddingLeft = 12;
                Root.style.paddingRight = 12;
                Root.style.paddingTop = 8;
                Root.style.paddingBottom = 8;
                Root.style.alignItems = Align.Center;
                Root.style.justifyContent = Justify.Center;

                if (isSelf)
                {
                    Root.style.minHeight = 132;
                    Root.style.flexDirection = FlexDirection.Column;
                }
                else if (name == "left" || name == "right")
                {
                    Root.style.width = 230;
                    Root.style.flexShrink = 0;
                    Root.style.flexDirection = FlexDirection.Column;
                }
                else
                {
                    Root.style.minHeight = 108;
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
                _tag.style.color = new Color(0.82f, 0.76f, 0.52f);
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
                _score.style.color = new Color(0.76f, 0.82f, 0.76f);
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
                _avatarSlot.Clear();
                _avatarSlot.Add(PlayerProfileStore.CreateAvatarVisual(name, avatarPath, 32));
                _name.text = name;
                _score.text = $"总分 {score}";
                _tag.text = isCurrentTurn && isCaboCaller ? "CABO 回合" : isCurrentTurn ? "回合" : tag;
                _tag.style.color = isCaboCaller ? Color.white : isCurrentTurn ? new Color(0.12f, 0.09f, 0.02f) : new Color(0.82f, 0.76f, 0.52f);
                _tag.style.backgroundColor = isCaboCaller ? new Color(0.74f, 0.10f, 0.18f) : isCurrentTurn ? new Color(1f, 0.78f, 0.22f) : Color.clear;
                Root.style.backgroundColor = isCaboCaller ? new Color(0.20f, 0.055f, 0.075f) : new Color(0.025f, 0.13f, 0.11f);
                var borderColor = isCaboCaller ? new Color(1f, 0.18f, 0.28f) : isCurrentTurn ? new Color(1f, 0.78f, 0.22f) : new Color(0.10f, 0.28f, 0.23f);
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
            public int PeekedValue;
            public Rect SourcePlayerBounds;
            public Rect TargetPlayerBounds;
            public Rect DrawPileBounds;
            public Rect DiscardPileBounds;
            public readonly List<int> SelectedSlots = new();
            public readonly List<SlotSnapshot> SourceHandBounds = new();
            public readonly List<SlotSnapshot> SourceSlotBounds = new();
            public readonly List<SlotSnapshot> SourceSwapSlotBounds = new();
            public readonly List<SlotSnapshot> TargetSlotBounds = new();
        }

        struct SlotSnapshot
        {
            public long PlayerId;
            public int Slot;
            public Rect Bounds;
        }
    }
}
