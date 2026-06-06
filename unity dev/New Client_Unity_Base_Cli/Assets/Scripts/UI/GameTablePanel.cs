using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Game table UI. 4-player layout with cards, piles, action buttons.
    /// Renders based on GameState, driven by GameFlow state machine.
    /// </summary>
    public class GameTablePanel
    {
        VisualElement _root, _container;
        GameFlow _flow;

        // Header
        Label _header, _pileInfo;

        // Player areas (opponents: top, left, right + self: bottom)
        Label[] _oppNames = new Label[3];
        Label[] _oppScores = new Label[3];
        VisualElement[] _oppCardRows = new VisualElement[3];
        Label[] _oppCardCounts = new Label[3]; // "3 cards" text

        Label _selfName, _selfScore;
        VisualElement _selfCardRow;
        VisualElement _turnArrow; // ↓ indicator

        // Action buttons
        VisualElement _actionRow;
        Button _btnDraw, _btnTakeDiscard, _btnCallSteady;
        VisualElement _drawnCardPanel;
        Label _drawnCardLabel;
        Button _btnDiscard, _btnReplace, _btnUseSkill;
        VisualElement _slotInputRow;

        // Message log
        Label _actionLog;

        public GameTablePanel(VisualElement root, GameFlow flow)
        {
            _flow = flow;
            _root = root;

            _container = new VisualElement();
            _container.style.flexGrow = 1;
            _container.style.paddingTop = 10;
            _container.style.paddingBottom = 10;
            _container.style.paddingLeft = 20;
            _container.style.paddingRight = 20;
            root.Add(_container);

            // Header
            _header = new Label("Cabo Game");
            _header.style.fontSize = 24;
            _header.style.unityFontStyleAndWeight = FontStyle.Bold;
            _header.style.unityTextAlign = TextAnchor.MiddleCenter;
            _container.Add(_header);

            _pileInfo = new Label();
            _pileInfo.style.fontSize = 14;
            _pileInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            _pileInfo.style.marginBottom = 10;
            _container.Add(_pileInfo);

            // Opponent areas
            for (int i = 0; i < 3; i++)
            {
                var area = new VisualElement();
                area.style.flexDirection = FlexDirection.Row;
                area.style.justifyContent = Justify.Center;
                area.style.marginTop = 5;
                area.style.marginBottom = 5;

                _oppNames[i] = new Label();
                _oppNames[i].style.fontSize = 14;
                _oppNames[i].style.unityFontStyleAndWeight = FontStyle.Bold;
                _oppNames[i].style.width = 80;
                area.Add(_oppNames[i]);

                _oppScores[i] = new Label();
                _oppScores[i].style.fontSize = 12;
                _oppScores[i].style.width = 60;
                area.Add(_oppScores[i]);

                _oppCardRows[i] = new VisualElement();
                _oppCardRows[i].style.flexDirection = FlexDirection.Row;
                area.Add(_oppCardRows[i]);

                _oppCardCounts[i] = new Label();
                _oppCardCounts[i].style.fontSize = 12;
                _oppCardCounts[i].style.width = 60;
                area.Add(_oppCardCounts[i]);

                _container.Add(area);
            }

            // Turn arrow
            _turnArrow = new Label("↓");
            _turnArrow.style.fontSize = 20;
            _turnArrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            _turnArrow.style.color = Color.yellow;
            _container.Add(_turnArrow);

            // Self area
            var selfArea = new VisualElement();
            selfArea.style.flexDirection = FlexDirection.Row;
            selfArea.style.justifyContent = Justify.Center;
            selfArea.style.marginTop = 10;
            selfArea.style.marginBottom = 10;
            _container.Add(selfArea);

            _selfName = new Label();
            _selfName.style.fontSize = 16;
            _selfName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _selfName.style.width = 100;
            selfArea.Add(_selfName);

            _selfScore = new Label();
            _selfScore.style.fontSize = 14;
            _selfScore.style.width = 80;
            selfArea.Add(_selfScore);

            _selfCardRow = new VisualElement();
            _selfCardRow.style.flexDirection = FlexDirection.Row;
            selfArea.Add(_selfCardRow);

            // Action log (shows opponent actions)
            _actionLog = new Label();
            _actionLog.style.fontSize = 12;
            _actionLog.style.unityTextAlign = TextAnchor.MiddleCenter;
            _actionLog.style.marginTop = 5;
            _actionLog.style.color = new Color(0.7f, 0.7f, 0.3f);
            _container.Add(_actionLog);

            // ── Action Buttons ──
            _actionRow = new VisualElement();
            _actionRow.style.flexDirection = FlexDirection.Row;
            _actionRow.style.justifyContent = Justify.Center;
            _actionRow.style.marginTop = 10;
            _container.Add(_actionRow);

            _btnDraw = new Button(() => _flow.DoDraw());
            _btnDraw.text = "Draw from Deck";
            _btnDraw.style.marginRight = 8;
            _actionRow.Add(_btnDraw);

            _btnTakeDiscard = new Button(() => _flow.DoTakeFromDiscard());
            _btnTakeDiscard.text = "Take from Discard";
            _btnTakeDiscard.style.marginRight = 8;
            _actionRow.Add(_btnTakeDiscard);

            _btnCallSteady = new Button(() => _flow.DoCallSteady());
            _btnCallSteady.text = "Call CABO";
            _actionRow.Add(_btnCallSteady);

            // ── Drawn Card Decision Panel ──
            _drawnCardPanel = new VisualElement();
            _drawnCardPanel.style.flexDirection = FlexDirection.Column;
            _drawnCardPanel.style.alignItems = Align.Center;
            _drawnCardPanel.style.marginTop = 10;
            _container.Add(_drawnCardPanel);

            _drawnCardLabel = new Label();
            _drawnCardLabel.style.fontSize = 16;
            _drawnCardPanel.Add(_drawnCardLabel);

            var drawnBtnRow = new VisualElement();
            drawnBtnRow.style.flexDirection = FlexDirection.Row;
            drawnBtnRow.style.justifyContent = Justify.Center;
            _drawnCardPanel.Add(drawnBtnRow);

            _btnDiscard = new Button(() => _flow.DoDiscardDrawn(false));
            _btnDiscard.text = "Discard";
            _btnDiscard.style.marginRight = 8;
            drawnBtnRow.Add(_btnDiscard);

            _btnReplace = new Button(() => { });
            _btnReplace.text = "Replace";
            _btnReplace.style.marginRight = 8;
            drawnBtnRow.Add(_btnReplace);

            _btnUseSkill = new Button(() => _flow.DoDiscardDrawn(true));
            _btnUseSkill.text = "Use Skill";
            drawnBtnRow.Add(_btnUseSkill);
        }

        public void SetVisible(bool visible)
        {
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RenderGame()
        {
            var s = _flow.State;
            _header.text = $"Round {s.RoundNumber}, Turn {s.TurnNumber}";
            _pileInfo.text = $"Draw Pile: {s.DrawPileCount}  |  Discard: {s.DiscardPileCount}" +
                (s.DiscardPileCount > 0 && s.DiscardTopValue >= 0 ? $" (Top: {s.DiscardTopValue})" : " (Top: -)");

            // Opponent layout: (my+2)%4 top, (my+3)%4 left, (my+1)%4 right
            var oppIdx = s.OpponentIndices;
            string[] positions = { "TOP", "LEFT", "RIGHT" };
            for (int i = 0; i < 3 && i < oppIdx.Count; i++)
            {
                var p = s.Players[oppIdx[i]];
                bool isCurrent = p.PlayerId == s.CurrentPlayerId;
                string prefix = isCurrent ? "↓ " : "   ";
                _oppNames[i].text = prefix + p.Nickname;
                _oppScores[i].text = $"Score: {p.TotalScore}";

                // Show opponent cards as [?]
                _oppCardRows[i].Clear();
                for (int j = 0; j < p.CardCount; j++)
                {
                    var card = new Label("[?]");
                    card.style.fontSize = 16;
                    card.style.marginRight = 4;
                    _oppCardRows[i].Add(card);
                }
                _oppCardCounts[i].text = $"{p.CardCount} cards";
            }

            // Self
            var myInfo = s.Players.Find(p => p.PlayerId == s.MyPlayerId);
            bool myTurn = s.IsMyTurn;
            _selfName.text = (myTurn ? "↓ " : "   ") + (myInfo?.Nickname ?? "You");
            _selfScore.text = $"Score: {myInfo?.TotalScore ?? 0}";
            _turnArrow.style.display = myTurn ? DisplayStyle.Flex : DisplayStyle.None;

            _selfCardRow.Clear();
            foreach (var c in s.MyCards)
            {
                string text = c.IsKnown ? $"[{c.Value}]" : "[?]";
                var card = new Label(text);
                card.style.fontSize = 18;
                card.style.marginRight = 6;
                card.style.unityFontStyleAndWeight = FontStyle.Bold;
                if (c.IsKnown && c.Value <= 3) card.style.color = Color.green;
                else if (c.IsKnown && c.Value >= 10) card.style.color = Color.red;
                _selfCardRow.Add(card);
            }

            // Action log
            _actionLog.text = s.LastActionMessage;

            // Buttons: show based on sub-state
            bool showMain = _flow.SubState == GameSubState.AwaitingMainInput;
            bool showDrawn = _flow.SubState == GameSubState.AwaitingDrawnDecision;

            _actionRow.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            _drawnCardPanel.style.display = showDrawn ? DisplayStyle.Flex : DisplayStyle.None;

            if (showMain)
            {
                bool firstTurn = s.TurnNumber <= 1;
                _btnDraw.SetEnabled(true);
                _btnTakeDiscard.SetEnabled(!firstTurn && s.DiscardPileCount > 0);
                _btnCallSteady.SetEnabled(!s.IsFinalRound);
            }

            if (showDrawn)
            {
                _drawnCardLabel.text = $"Drawn: [{s.DrawnCardValue}]" + (s.DrawnCardSkill > 0 ? " (Skill!)" : "");
                bool hasSkill = s.DrawnCardSkill > 0;
                _btnUseSkill.SetEnabled(hasSkill);
                _btnUseSkill.style.display = hasSkill ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void RenderReveal()
        {
            var s = _flow.State;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Round {s.RoundNumber} Reveal ===").AppendLine();

            foreach (var r in s.LastRoundResults)
            {
                sb.Append(r.Nickname);
                if (r.IsSteadyCaller) sb.Append(" (CABO)");
                sb.Append(": ");
                foreach (var v in r.CardValues) sb.Append($"[{v}] ");
                sb.Append($"= {r.HandTotal}");
                if (r.Penalty > 0) sb.Append($" (+{r.Penalty})");
                sb.Append($" = {r.RoundScore}");
                if (r.IsLowest) sb.Append("  ← Lowest!");
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Scores:");
            foreach (var r in s.LastRoundResults)
                sb.AppendLine($"  {r.Nickname}: {r.CumulativeScore}");

            _header.text = "Round Reveal";
            _pileInfo.text = "";
            for (int i = 0; i < 3; i++) { _oppNames[i].text = ""; _oppScores[i].text = ""; _oppCardRows[i].Clear(); _oppCardCounts[i].text = ""; }
            _selfName.text = ""; _selfScore.text = ""; _selfCardRow.Clear();
            _actionRow.style.display = DisplayStyle.None;
            _drawnCardPanel.style.display = DisplayStyle.None;
            _actionLog.text = sb.ToString();
        }

        public void RenderGameOver()
        {
            var s = _flow.State;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== GAME OVER ===").AppendLine();
            foreach (var r in s.FinalRankings)
                sb.AppendLine($"{r.Rank}. {r.Nickname}: {r.FinalScore}" + (r.IsWinner ? " 🏆 WINNER" : ""));

            _header.text = "Game Over";
            _pileInfo.text = "";
            for (int i = 0; i < 3; i++) { _oppNames[i].text = ""; _oppScores[i].text = ""; _oppCardRows[i].Clear(); _oppCardCounts[i].text = ""; }
            _selfName.text = ""; _selfScore.text = ""; _selfCardRow.Clear();
            _actionRow.style.display = DisplayStyle.None;
            _drawnCardPanel.style.display = DisplayStyle.None;
            _actionLog.text = sb.ToString();
        }
    }
}
