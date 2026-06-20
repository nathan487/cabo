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
    }
}
