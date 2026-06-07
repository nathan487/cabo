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
        readonly VisualElement _actionPanel;
        readonly Label _actionTitle;
        readonly Label _actionBody;
        readonly VisualElement _drawnCardSlot;
        readonly VisualElement _buttonRow;
        readonly Label _statusLine;
        readonly VisualElement _animationLayer;

        readonly HashSet<int> _selectedOwnSlots = new();
        long _selectedOpponentPlayerId;
        GameSubState _lastSubState = GameSubState.Idle;
        GameSubState _lastRenderedSubState = GameSubState.Idle;
        long _lastAnimatedActionSequence;
        IVisualElementScheduledItem _drawAnimation;

        public GameTablePanel(VisualElement root, GameFlow flow)
        {
            _root = root;
            _flow = flow;

            _container = new VisualElement { name = "CaboGameTable" };
            StretchToParent(_container);
            _container.style.flexDirection = FlexDirection.Column;
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

            _container.Add(_topSeat.Root);

            var middle = new VisualElement { name = "TableMiddle" };
            middle.style.flexGrow = 1;
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.alignItems = Align.Stretch;
            middle.style.marginTop = 10;
            middle.style.marginBottom = 10;
            _container.Add(middle);

            middle.Add(_leftSeat.Root);

            _centerTable = new VisualElement { name = "CenterTable" };
            _centerTable.style.flexGrow = 1;
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
            _container.Add(_selfSeat.Root);
        }

        public void SetVisible(bool visible)
        {
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RenderGame()
        {
            var state = _flow.State;
            bool shouldAnimateDraw = _lastRenderedSubState == GameSubState.WaitingDrawRsp
                && _flow.SubState == GameSubState.AwaitingDrawnDecision
                && state.HasDrawnCard;

            ResetSelectionWhenSubStateChanges(_flow.SubState);

            _roundLabel.text = $"Round {state.RoundNumber}  Turn {state.TurnNumber}";
            _turnLabel.text = BuildTurnText(state);

            RenderSeats(state);
            RenderPiles(state);
            RenderActionPanel(state);

            _statusLine.text = ShouldShowActionStatus(_flow.SubState) ? BuildStatusText(state) : "";
            _statusLine.style.display = string.IsNullOrWhiteSpace(_statusLine.text) ? DisplayStyle.None : DisplayStyle.Flex;

            if (shouldAnimateDraw)
                ScheduleDrawAnimation(state.DrawnCardValue);
            if (state.LastActionSequence > _lastAnimatedActionSequence)
            {
                _lastAnimatedActionSequence = state.LastActionSequence;
                ScheduleActionAnimation(state);
            }

            _lastRenderedSubState = _flow.SubState;
        }

        public void RenderReveal()
        {
            var state = _flow.State;
            _roundLabel.text = $"Round {state.RoundNumber} reveal";
            _turnLabel.text = "Cards are face up for scoring";
            HideSeatsForOverlay();
            RenderPiles(state);

            _drawnCardSlot.Clear();
            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            var title = new Label("Round scores");
            StylePanelTitle(title);
            _actionPanel.Add(title);

            foreach (var result in state.LastRoundResults)
                _actionPanel.Add(CreateRevealRow(result));

            AddInterRoundControls(state);
        }

        public void RenderGameOver()
        {
            _roundLabel.text = "Game over";
            _turnLabel.text = "Final ranking";
            HideSeatsForOverlay();
            _drawnCardSlot.Clear();
            _pileRow.Clear();

            _actionPanel.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            var title = new Label("Final scores");
            StylePanelTitle(title);
            _actionPanel.Add(title);

            foreach (var rank in _flow.State.FinalRankings)
            {
                var row = new Label($"{rank.Rank}. {rank.Nickname}  {rank.FinalScore}" + (rank.IsWinner ? "  Winner" : ""));
                row.style.fontSize = rank.IsWinner ? 18 : 15;
                row.style.unityFontStyleAndWeight = rank.IsWinner ? FontStyle.Bold : FontStyle.Normal;
                row.style.unityTextAlign = TextAnchor.MiddleCenter;
                row.style.marginTop = 6;
                _actionPanel.Add(row);
            }

            _statusLine.text = "";
        }

        void RenderSeats(GameState state)
        {
            _selfSeat.Root.style.display = DisplayStyle.Flex;
            foreach (var seat in _opponentSeats)
            {
                seat.Root.style.display = DisplayStyle.Flex;
                seat.Clear();
            }

            var myInfo = state.Players.Find(p => p.PlayerId == state.MyPlayerId);
            _selfSeat.RenderHeader(myInfo?.Nickname ?? "You", myInfo?.TotalScore ?? 0, state.IsMyTurn, "You");
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
                bool current = player.PlayerId == state.CurrentPlayerId;
                bool selected = _selectedOpponentPlayerId == player.PlayerId;
                _opponentSeats[i].RenderHeader(player.Nickname, player.TotalScore, current, selected ? "Target" : "Opponent");
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

            _drawPile.Add(CreatePileCard("Deck", "CABO", state.DrawPileCount.ToString(), false));
            _discardPile.Add(CreatePileCard("Discard", state.DiscardTopValue >= 0 ? state.DiscardTopValue.ToString() : "-", state.DiscardPileCount.ToString(), true));

            _pileRow.Add(_drawPile);
            _pileRow.Add(_discardPile);
        }

        void RenderActionPanel(GameState state)
        {
            ResetActionPanelForGame();
            _drawnCardSlot.Clear();
            _drawnCardSlot.style.display = DisplayStyle.None;
            _buttonRow.Clear();
            _actionPanel.style.display = DisplayStyle.Flex;

            switch (_flow.SubState)
            {
                case GameSubState.AwaitingMainInput:
                    _actionTitle.text = "Your turn";
                    _actionBody.text = "Choose a pile action or call CABO.";
                    AddActionButton("Draw", () => _flow.DoDraw(), true);
                    AddActionButton("Take discard", () => _flow.DoTakeFromDiscard(), state.TurnNumber > 1 && state.DiscardPileCount > 0);
                    AddActionButton("Call CABO", () => _flow.DoCallSteady(), !state.IsFinalRound);
                    break;

                case GameSubState.AwaitingDrawnDecision:
                    _actionTitle.text = "Drawn card";
                    _actionBody.text = "Discard it, replace selected cards, or use its skill after discarding.";
                    _drawnCardSlot.style.display = DisplayStyle.Flex;
                    _drawnCardSlot.Add(CreateDrawnCard(state, 48, 64));
                    AddActionButton("Discard", () => _flow.DoDiscardDrawn(false), true);
                    AddActionButton("Replace", () => _flow.BeginReplaceWithDrawn(), true);
                    AddActionButton(BuildSkillButtonText(state.DrawnCardSkill), () => _flow.DoDiscardDrawn(true), state.DrawnCardSkill > 0);
                    break;

                case GameSubState.AwaitingReplaceSlots:
                    _actionTitle.text = "Replace with drawn card";
                    _actionBody.text = "Select one or more of your cards, then confirm.";
                    _drawnCardSlot.style.display = DisplayStyle.Flex;
                    _drawnCardSlot.Add(CreateDrawnCard(state, 48, 64));
                    AddActionButton("Confirm replace", () => ConfirmReplace(), _selectedOwnSlots.Count > 0);
                    AddActionButton("Discard instead", () => _flow.DoDiscardDrawn(false), true);
                    break;

                case GameSubState.AwaitingTakeSlots:
                    _actionTitle.text = "Take discard";
                    _actionBody.text = "Select one or more of your cards to exchange with the discard top.";
                    AddActionButton("Confirm take", () => ConfirmTakeDiscard(), _selectedOwnSlots.Count > 0);
                    break;

                case GameSubState.SkillPeekSlot:
                    _actionTitle.text = "Peek skill";
                    _actionBody.text = "Select one of your cards to reveal privately.";
                    AddActionButton("Skip skill", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSpyTarget:
                    _actionTitle.text = "Spy skill";
                    _actionBody.text = "Choose an opponent seat.";
                    AddActionButton("Skip skill", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSpySlot:
                    _actionTitle.text = "Spy skill";
                    _actionBody.text = "Choose a target card from the selected opponent.";
                    AddActionButton("Skip skill", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapMySlot:
                    _actionTitle.text = "Swap skill";
                    _actionBody.text = "Choose one of your cards to swap.";
                    AddActionButton("Skip skill", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapTargetPlayer:
                    _actionTitle.text = "Swap skill";
                    _actionBody.text = "Choose an opponent seat.";
                    AddActionButton("Skip skill", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.SkillSwapTargetSlot:
                    _actionTitle.text = "Swap skill";
                    _actionBody.text = "Choose a target card from the selected opponent.";
                    AddActionButton("Skip skill", () => _flow.DoSkipSkill(), true);
                    break;

                case GameSubState.Idle:
                    _actionTitle.text = "Waiting";
                    _actionBody.text = state.IsMyTurn ? "Preparing your actions." : "Another player is taking a turn.";
                    break;

                default:
                    _actionTitle.text = "Waiting for server";
                    _actionBody.text = "The request has been sent.";
                    break;
            }
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

        void ScheduleActionAnimation(GameState state)
        {
            var snapshot = new ActionAnimationSnapshot
            {
                ActionType = state.LastActionType,
                Skill = state.LastActionSkill,
                SourcePlayerId = state.LastActionSourcePlayerId,
                TargetPlayerId = state.LastActionTargetPlayerId,
                SourceSlot = state.LastActionSourceSlot,
                TargetSlot = state.LastActionTargetSlot,
                SwapOccurred = state.LastActionSwapOccurred,
                IncomingCardValue = state.LastActionIncomingCardValue,
                DiscardTopValue = state.DiscardTopValue
            };
            _root.schedule.Execute(() => PlayActionAnimation(snapshot)).ExecuteLater(1);
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
            const float duration = 0.42f;
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
                    PlayFlyCard(CenterOf(_drawPile.worldBound), CenterOf(GetPlayerBounds(action.SourcePlayerId)), false, 0, new Color(0.30f, 0.55f, 1f), 0.34f);
                    break;
                case ActionType.DiscardDrawn:
                    PlayFlyCard(CenterOf(GetPlayerBounds(action.SourcePlayerId)), CenterOf(_discardPile.worldBound), true, action.DiscardTopValue, GetSkillColor(action.Skill), 0.38f);
                    PulsePlayer(action.SourcePlayerId, GetSkillColor(action.Skill));
                    break;
                case ActionType.ReplaceWithDrawn:
                    PlayFlyCard(CenterOf(GetPlayerBounds(action.SourcePlayerId)), CenterOf(_discardPile.worldBound), false, 0, new Color(1f, 0.72f, 0.30f), 0.38f);
                    PulsePlayer(action.SourcePlayerId, action.IncomingCardValue >= 0 ? GetFaceColor(action.IncomingCardValue) : new Color(1f, 0.72f, 0.30f));
                    break;
                case ActionType.TakeFromDiscard:
                    PlayFlyCard(CenterOf(_discardPile.worldBound), CenterOf(GetPlayerBounds(action.SourcePlayerId)), true, action.DiscardTopValue, new Color(1f, 0.85f, 0.42f), 0.38f);
                    PulsePlayer(action.SourcePlayerId, new Color(1f, 0.85f, 0.42f));
                    break;
                case ActionType.UseSkill:
                    PlaySkillAnimation(action);
                    break;
                case ActionType.CallSteady:
                    PulsePlayer(action.SourcePlayerId, new Color(1f, 0.82f, 0.22f), 0.9f);
                    break;
            }
        }

        void PlaySkillAnimation(ActionAnimationSnapshot action)
        {
            var color = GetSkillColor(action.Skill);
            if (action.Skill == SkillType.Swap && action.SwapOccurred)
            {
                var source = CenterOf(GetCardBounds(action.SourcePlayerId, action.SourceSlot));
                var target = CenterOf(GetCardBounds(action.TargetPlayerId, action.TargetSlot));
                PlayFlyCard(source, target, false, 0, color, 0.44f);
                PlayFlyCard(target, source, false, 0, color, 0.44f);
                PulseCard(action.SourcePlayerId, action.SourceSlot, color);
                PulseCard(action.TargetPlayerId, action.TargetSlot, color);
                return;
            }

            if (action.Skill == SkillType.Spy)
            {
                PulseCard(action.TargetPlayerId, action.TargetSlot, color);
                PlayFlyCard(CenterOf(GetPlayerBounds(action.SourcePlayerId)), CenterOf(GetCardBounds(action.TargetPlayerId, action.TargetSlot)), false, 0, color, 0.34f);
                return;
            }

            if (action.Skill == SkillType.PeekSelf)
            {
                PulseCard(action.SourcePlayerId, action.SourceSlot, color);
                return;
            }

            PulsePlayer(action.SourcePlayerId, color);
        }

        void PlayFlyCard(Vector2 start, Vector2 end, bool faceUp, int value, Color color, float duration)
        {
            var rootOrigin = new Vector2(_root.worldBound.x, _root.worldBound.y);
            if (start == Vector2.zero || end == Vector2.zero)
                return;

            const int width = 44;
            const int height = 60;
            var card = CreateCard(faceUp, value >= 0 ? value : 0, width, height, false, false, null);
            card.style.position = Position.Absolute;
            card.style.marginLeft = 0;
            card.style.marginRight = 0;
            card.style.marginTop = 0;
            card.style.marginBottom = 0;
            SetBorderColor(card, color);
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
                card.style.opacity = Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.78f) / 0.22f));

                if (t >= 1f)
                {
                    item?.Pause();
                    card.RemoveFromHierarchy();
                }
            }).Every(16);
        }

        void PulsePlayer(long playerId, Color color, float duration = 0.48f)
        {
            PulseElement(GetSeatRoot(playerId), color, duration);
        }

        void PulseCard(long playerId, int slot, Color color)
        {
            var element = GetCardElement(playerId, slot);
            if (element != null)
                PulseElement(element, color, 0.56f);
        }

        void PulseElement(VisualElement element, Color color, float duration)
        {
            if (element == null) return;
            var originalTop = element.style.borderTopColor.value;
            var originalRight = element.style.borderRightColor.value;
            var originalBottom = element.style.borderBottomColor.value;
            var originalLeft = element.style.borderLeftColor.value;
            float startedAt = Time.realtimeSinceStartup;

            _root.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                var current = Color.Lerp(originalTop, color, pulse);
                element.style.borderTopColor = current;
                element.style.borderRightColor = Color.Lerp(originalRight, color, pulse);
                element.style.borderBottomColor = Color.Lerp(originalBottom, color, pulse);
                element.style.borderLeftColor = Color.Lerp(originalLeft, color, pulse);

                if (t >= 1f)
                {
                    element.style.borderTopColor = originalTop;
                    element.style.borderRightColor = originalRight;
                    element.style.borderBottomColor = originalBottom;
                    element.style.borderLeftColor = originalLeft;
                }
            }).Every(16).Until(() => Time.realtimeSinceStartup - startedAt >= duration);
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
            VisualElement row = null;
            if (playerId == _flow.State.MyPlayerId)
            {
                row = _selfSeat.CardRow;
            }
            else
            {
                var indices = BuildOpponentIndices(_flow.State);
                for (int i = 0; i < indices.Count && i < _opponentSeats.Length; i++)
                {
                    if (_flow.State.Players[indices[i]].PlayerId == playerId)
                    {
                        row = _opponentSeats[i].CardRow;
                        break;
                    }
                }
            }

            if (row == null || slot < 0 || slot >= row.childCount)
                return null;
            return row[slot];
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

        static Vector2 CenterOf(Rect rect)
        {
            if (rect.width <= 1 || rect.height <= 1)
                return Vector2.zero;
            return new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
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

            var name = new Label(result.Nickname + (result.IsSteadyCaller ? "  CABO" : "") + (result.IsLowest ? "  Lowest" : ""));
            name.style.width = 180;
            name.style.fontSize = 13;
            row.Add(name);

            var cards = new VisualElement();
            cards.style.flexDirection = FlexDirection.Row;
            cards.style.flexGrow = 1;
            foreach (var value in result.CardValues)
                cards.Add(CreateCard(true, value, 36, 50, false, false, null));
            row.Add(cards);

            var score = new Label($"{result.HandTotal} + {result.Penalty} = {result.RoundScore}    total {result.CumulativeScore}");
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

            var readyTitle = new Label("Ready for next round");
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

            controls.Add(CreatePanelButton(myReady ? "Ready" : "Ready up", () => _flow.SendReady(), !myReady && state.MyPlayerId > 0 && state.RoomId > 0));

            if (isHost)
                controls.Add(CreatePanelButton("Start next round", () => _flow.SendStartGame(), allReady && state.MyPlayerId > 0 && state.RoomId > 0));
            else
            {
                var wait = new Label(allReady ? "Waiting for host to start" : "Waiting for all players");
                wait.style.fontSize = 12;
                wait.style.unityTextAlign = TextAnchor.MiddleCenter;
                wait.style.marginTop = 7;
                wait.style.marginLeft = 8;
                wait.style.color = new Color(0.78f, 0.88f, 0.82f);
                controls.Add(wait);
            }

            _statusLine.text = isHost
                ? $"{readyCount}/{playerCount} ready. Start unlocks when everyone is ready."
                : $"{readyCount}/{playerCount} ready.";
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

            var name = new Label((isSelf ? "You " : "") + player.Nickname);
            name.style.flexGrow = 1;
            name.style.fontSize = 12;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.color = Color.white;
            badge.Add(name);

            var readyState = new Label(player.IsReady ? "Ready" : "...");
            readyState.style.fontSize = 11;
            readyState.style.unityTextAlign = TextAnchor.MiddleRight;
            readyState.style.color = player.IsReady ? new Color(0.72f, 1f, 0.75f) : new Color(0.76f, 0.80f, 0.78f);
            badge.Add(readyState);
            return badge;
        }

        Button CreatePanelButton(string text, Action action, bool enabled)
        {
            var button = new Button(action) { text = text };
            button.style.minWidth = 148;
            button.style.height = 32;
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
            button.style.minWidth = 132;
            button.style.height = 30;
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

        string BuildTurnText(GameState state)
        {
            var current = state.Players.Find(p => p.PlayerId == state.CurrentPlayerId);
            string name = current?.Nickname ?? "Waiting";
            if (state.IsFinalRound)
                return $"Final round: {state.FinalRoundRemaining} turns left. Current turn: {name}";
            return state.IsMyTurn ? "Your turn" : $"Current turn: {name}";
        }

        string BuildStatusText(GameState state)
        {
            if (state.Players.Count < 4) return "Waiting for players.";
            if (state.DiscardPileCount == 0) return "Discard pile is empty.";
            return "Table is synced with the server.";
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
            return skill > 0 ? $"Use {GetSkillName(skill)}" : "Use skill";
        }

        static string GetSkillName(int skill)
        {
            return skill switch
            {
                2 => "Peek self",
                3 => "Spy",
                4 => "Swap",
                _ => "No skill"
            };
        }

        static string GetSkillShortName(int value)
        {
            if (value == 7 || value == 8) return "PEEK";
            if (value == 9 || value == 10) return "SPY";
            if (value == 11 || value == 12) return "SWAP";
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

                _tag = new Label();
                _tag.style.fontSize = 10;
                _tag.style.color = new Color(0.82f, 0.76f, 0.52f);
                _tag.style.marginRight = 8;
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

            public void RenderHeader(string name, int score, bool isCurrentTurn, string tag)
            {
                _name.text = name;
                _score.text = $"Score {score}";
                _tag.text = isCurrentTurn ? "TURN" : tag;
                Root.style.borderTopColor = isCurrentTurn ? new Color(1f, 0.78f, 0.22f) : new Color(0.10f, 0.28f, 0.23f);
                Root.style.borderRightColor = Root.style.borderTopColor.value;
                Root.style.borderBottomColor = Root.style.borderTopColor.value;
                Root.style.borderLeftColor = Root.style.borderTopColor.value;
                Root.style.borderTopWidth = isCurrentTurn ? 3 : 1;
                Root.style.borderRightWidth = isCurrentTurn ? 3 : 1;
                Root.style.borderBottomWidth = isCurrentTurn ? 3 : 1;
                Root.style.borderLeftWidth = isCurrentTurn ? 3 : 1;
            }
        }

        struct ActionAnimationSnapshot
        {
            public ActionType ActionType;
            public SkillType Skill;
            public long SourcePlayerId;
            public long TargetPlayerId;
            public int SourceSlot;
            public int TargetSlot;
            public bool SwapOccurred;
            public int IncomingCardValue;
            public int DiscardTopValue;
        }
    }
}
