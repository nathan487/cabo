using Cabo.Client.Art;
using System.Reflection;
using Cabo.Client;
using Cabo.Client.UI;
using Cabo.Client.UI.CardTable;
using Game.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.Tests
{
    public sealed class GameTablePanelSfxTests
    {
        [Test]
        public void SkippedSkillActionDoesNotPlaySkillSfx()
        {
            var cues = GameTablePanel.GetActionSfxCues(ActionType.UseSkill, SkillType.Unknown, false);

            Assert.IsEmpty(cues);
        }

        [Test]
        public void RealPeekSkillActionPlaysSkillAndFlipSfx()
        {
            var cues = GameTablePanel.GetActionSfxCues(ActionType.UseSkill, SkillType.PeekSelf, false);

            CollectionAssert.AreEqual(new[] { CaboSfx.Skill, CaboSfx.Flip }, cues);
        }

        [Test]
        public void RealSwapSkillActionPlaysSkillSfx()
        {
            var cues = GameTablePanel.GetActionSfxCues(ActionType.UseSkill, SkillType.Swap, false);

            CollectionAssert.AreEqual(new[] { CaboSfx.Skill }, cues);
        }

        [Test]
        public void SkippedSkillActionDoesNotPlaySpecialEffect()
        {
            var effect = GameTablePanel.GetActionSpecialEffect(ActionType.UseSkill, SkillType.Unknown);

            Assert.AreEqual(CaboSpecialEffect.None, effect);
        }

        [Test]
        public void RealSkillActionsMapToSpecialEffects()
        {
            Assert.AreEqual(CaboSpecialEffect.PeekSelf, GameTablePanel.GetActionSpecialEffect(ActionType.UseSkill, SkillType.PeekSelf));
            Assert.AreEqual(CaboSpecialEffect.Spy, GameTablePanel.GetActionSpecialEffect(ActionType.UseSkill, SkillType.Spy));
            Assert.AreEqual(CaboSpecialEffect.Swap, GameTablePanel.GetActionSpecialEffect(ActionType.UseSkill, SkillType.Swap));
        }

        [Test]
        public void CallSteadyActionMapsToCaboSpecialEffect()
        {
            var effect = GameTablePanel.GetActionSpecialEffect(ActionType.CallSteady, SkillType.Unknown);

            Assert.AreEqual(CaboSpecialEffect.Cabo, effect);
        }

        [Test]
        public void RevealDrainTimeoutCoversLongestBlockingActionAnimation()
        {
            float longestAction = GameTablePanel.LongestRevealBlockingActionDurationSeconds
                + GameTablePanel.PlaybackLayoutSettleDelaySeconds;

            Assert.GreaterOrEqual(UIManager.RevealAnimationDrainTimeoutSeconds, longestAction);
        }

        [Test]
        public void RevealLayoutRefreshKeepsGameViewWhileActionAnimationIsPending()
        {
            Assert.IsTrue(GameTablePanel.ShouldRenderGameForRevealLayoutRefresh(
                GamePhase.RoundReveal,
                false,
                FlowState.RoundReveal,
                true));
        }

        [Test]
        public void RevealLayoutRefreshAllowsRevealWhenNoActionAnimationIsPending()
        {
            Assert.IsFalse(GameTablePanel.ShouldRenderGameForRevealLayoutRefresh(
                GamePhase.RoundReveal,
                false,
                FlowState.RoundReveal,
                false));
        }

        [Test]
        public void CardTableStaysVisibleDuringRevealActionDrain()
        {
            Assert.IsTrue(GameTablePanel.ShouldShowCardTable(
                panelVisible: true,
                phase: GamePhase.RoundReveal,
                hasPendingActionAnimation: true,
                hasEndGameModal: false));
        }

        [Test]
        public void CardTableHidesForRevealWhenActionDrainCompletes()
        {
            Assert.IsFalse(GameTablePanel.ShouldShowCardTable(
                panelVisible: true,
                phase: GamePhase.RoundReveal,
                hasPendingActionAnimation: false,
                hasEndGameModal: false));
        }

        [Test]
        public void CardTableLayerRefreshIsSuppressedForSettledReveal()
        {
            Assert.IsFalse(GameTablePanel.ShouldRefreshCardTableLayer(
                GamePhase.RoundReveal,
                true,
                FlowState.RoundReveal,
                false));
        }

        [Test]
        public void CardTableLayerRefreshIsAllowedWhileRevealActionDrainPending()
        {
            Assert.IsTrue(GameTablePanel.ShouldRefreshCardTableLayer(
                GamePhase.RoundReveal,
                true,
                FlowState.RoundReveal,
                true));
        }

        [Test]
        public void GameplayPilesAreHiddenForSettlementOverlays()
        {
            Assert.IsTrue(GameTablePanel.ShouldShowGameplayPiles(GamePhase.Playing));
            Assert.IsFalse(GameTablePanel.ShouldShowGameplayPiles(GamePhase.RoundReveal));
            Assert.IsFalse(GameTablePanel.ShouldShowGameplayPiles(GamePhase.GameOver));
        }

        [Test]
        public void RenderRevealKeepsGameplaySurfaceHiddenWithStaleActionSequence()
        {
            var root = new VisualElement();
            var flow = new GameFlow(new NetworkGateway());
            flow.State.Phase = GamePhase.RoundReveal;
            flow.State.RoundNumber = 1;
            flow.State.MyPlayerId = 1;
            flow.State.RoomId = 10;
            flow.State.LastActionSequence = 99;
            flow.State.LastActionType = ActionType.Draw;
            flow.State.Players.Add(new PlayerInfo
            {
                PlayerId = 1,
                Nickname = "Tester",
                CardCount = 4,
                IsHost = true,
                IsReady = true
            });
            flow.State.MyCards.Add(new CardState { SlotIndex = 0, Value = 1, IsKnown = true });
            flow.State.MyCards.Add(new CardState { SlotIndex = 1, Value = 2, IsKnown = true });

            var panel = new GameTablePanel(root, flow, null);
            try
            {
                panel.SetVisible(true);
                panel.RenderReveal();

                var pileRow = root.Q<VisualElement>("PileRow");
                var cardTable = GetCardTableView(panel);

                Assert.NotNull(pileRow);
                Assert.NotNull(cardTable);
                Assert.AreEqual(DisplayStyle.None, pileRow.style.display.value);
                Assert.IsFalse(cardTable.gameObject.activeSelf);
            }
            finally
            {
                panel.Dispose();
            }
        }

        [Test]
        public void RevealDrainTimeoutCoversLargeMultiCardExchangeAnimation()
        {
            float exchangeDuration = GameTablePanel.GetEstimatedRevealBlockingExchangeDuration(
                ActionType.ReplaceWithDrawn,
                selectedSlotCount: 12,
                survivorMoveCount: 1);

            Assert.GreaterOrEqual(
                UIManager.RevealAnimationDrainTimeoutSeconds,
                exchangeDuration + GameTablePanel.PlaybackLayoutSettleDelaySeconds);
        }

        [Test]
        public void SkillFallbackRevealGateCoversNonActorView()
        {
            Assert.GreaterOrEqual(
                GameTablePanel.GetSkillFallbackRevealGateDuration(SkillType.Spy),
                GameTablePanel.GetEstimatedRevealBlockingActionDuration(
                    ActionType.UseSkill,
                    SkillType.Spy,
                    false,
                    false));
        }

        [Test]
        public void SkippedSkillActionDoesNotBlockSettlementReveal()
        {
            Assert.IsFalse(GameTablePanel.ShouldBlockRevealForActionAnimation(
                ActionType.UseSkill,
                SkillType.Unknown,
                false));
        }

        [Test]
        public void VisibleCardActionsBlockSettlementRevealUntilAnimationFinishes()
        {
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.Draw, SkillType.Unknown, false));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.DiscardDrawn, SkillType.Unknown, false));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.ReplaceWithDrawn, SkillType.Unknown, false));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.TakeFromDiscard, SkillType.Unknown, false));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.UseSkill, SkillType.PeekSelf, false));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.UseSkill, SkillType.Spy, false));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.UseSkill, SkillType.Swap, true));
            Assert.IsTrue(GameTablePanel.ShouldBlockRevealForActionAnimation(ActionType.CallSteady, SkillType.Unknown, false));
        }

        [Test]
        public void SwapSkillWithoutSwapDoesNotBlockSettlementReveal()
        {
            Assert.IsFalse(GameTablePanel.ShouldBlockRevealForActionAnimation(
                ActionType.UseSkill,
                SkillType.Swap,
                false));
        }

        [Test]
        public void ExtremeDrawValuesMapToSpecialEffects()
        {
            Assert.AreEqual(CaboSpecialEffect.LowSugarSpring, GameTablePanel.GetDrawnCardSpecialEffect(0));
            Assert.AreEqual(CaboSpecialEffect.SugarBomb, GameTablePanel.GetDrawnCardSpecialEffect(13));
        }

        [Test]
        public void OrdinaryDrawValuesDoNotMapToSpecialEffects()
        {
            Assert.AreEqual(CaboSpecialEffect.None, GameTablePanel.GetDrawnCardSpecialEffect(1));
            Assert.AreEqual(CaboSpecialEffect.None, GameTablePanel.GetDrawnCardSpecialEffect(7));
            Assert.AreEqual(CaboSpecialEffect.None, GameTablePanel.GetDrawnCardSpecialEffect(12));
        }

        [Test]
        public void KnownValueReactionRewardsWhenDiscardedSumIsHigher()
        {
            Assert.IsTrue(GameTablePanel.TryGetKnownValueReactionDelta(12, 5, out int delta));
            Assert.Less(delta, 0);
        }

        [Test]
        public void KnownValueReactionPenalizesWhenIncomingSumIsHigher()
        {
            Assert.IsTrue(GameTablePanel.TryGetKnownValueReactionDelta(3, 9, out int delta));
            Assert.Greater(delta, 0);
        }

        [Test]
        public void KnownValueReactionDoesNotTriggerWhenSumsMatch()
        {
            Assert.IsFalse(GameTablePanel.TryGetKnownValueReactionDelta(7, 7, out _));
        }

        [Test]
        public void KnownValueReactionDoesNotTriggerWhenIncomingValueIsUnknown()
        {
            Assert.IsFalse(GameTablePanel.TryGetKnownValueReactionDelta(10, 1, false, out _));
        }

        [Test]
        public void TakeFromDiscardIsAllowedWhenTurnNumberIsUnknownAfterStateSync()
        {
            Assert.IsTrue(GameTablePanel.CanTakeFromDiscard(new GameState
            {
                TurnNumber = 0,
                DiscardPileCount = 1
            }));
        }

        [Test]
        public void TakeFromDiscardIsDisabledOnlyForKnownFirstTurnOrEmptyDiscardPile()
        {
            Assert.IsFalse(GameTablePanel.CanTakeFromDiscard(new GameState
            {
                TurnNumber = 1,
                DiscardPileCount = 1
            }));
            Assert.IsFalse(GameTablePanel.CanTakeFromDiscard(new GameState
            {
                TurnNumber = 0,
                DiscardPileCount = 0
            }));
            Assert.IsTrue(GameTablePanel.CanTakeFromDiscard(new GameState
            {
                TurnNumber = 2,
                DiscardPileCount = 1
            }));
        }

        [Test]
        public void SpecialEffectSpritesAreConfiguredInArtCatalog()
        {
            CaboArt.ResetCache();

            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.PeekSelf));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.Spy));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.Swap));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.Cabo));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.LowSugarSpring));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.SugarBomb));
        }

        [Test]
        public void SpecialEffectOverlayCanvasSortsAboveCardTableCanvas()
        {
            var root = new VisualElement();
            var owner = new GameObject("SpecialEffectOverlayOwner");
            var flow = new GameFlow(new NetworkGateway());
            var panel = new GameTablePanel(root, flow, owner.transform);
            try
            {
                var field = typeof(GameTablePanel).GetField("_specialEffectOverlayCanvas", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(field);
                var canvas = (Canvas)field.GetValue(panel);

                Assert.NotNull(canvas);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                var tableCanvas = GetCardTableView(panel).GetComponent<Canvas>();
                Assert.NotNull(tableCanvas);
                Assert.Greater(canvas.sortingOrder, tableCanvas.sortingOrder);
                Assert.Greater(canvas.sortingOrder, 300);
            }
            finally
            {
                panel.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        static CardTableView GetCardTableView(GameTablePanel panel)
        {
            var field = typeof(GameTablePanel).GetField("_cardTableView", BindingFlags.NonPublic | BindingFlags.Instance);
            return (CardTableView)field?.GetValue(panel);
        }
    }
}
