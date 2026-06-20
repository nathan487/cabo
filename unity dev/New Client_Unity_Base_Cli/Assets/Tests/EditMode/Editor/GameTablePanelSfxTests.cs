using Cabo.Client.Art;
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
        public void SpecialEffectSpritesAreConfiguredInArtCatalog()
        {
            CaboArt.ResetCache();

            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.PeekSelf));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.Spy));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.Swap));
            Assert.NotNull(CaboArt.GetSpecialEffect(CaboSpecialEffect.Cabo));
        }
    }
}
