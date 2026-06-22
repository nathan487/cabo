using Cabo.Client.Art;
using Cabo.Client;
using Cabo.Client.UI;
using Game.Common;
using NUnit.Framework;

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
    }
}
