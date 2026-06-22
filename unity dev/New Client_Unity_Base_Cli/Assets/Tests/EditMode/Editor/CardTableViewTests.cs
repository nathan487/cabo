using System.Collections;
using System.Reflection;
using Cabo.Client;
using Cabo.Client.UI.CardTable;
using Game.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cabo.Client.Tests
{
    public sealed class CardTableViewTests
    {
        const long PlayerId = 1001;

        [UnityTest]
        public IEnumerator LayoutRefreshKeepsFrozenPlayerOnPreviouslyRenderedHand()
        {
            var host = new GameObject("CardTableViewTestsHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                view.Render(CreateState(1, 2, 3, 4), CreateLayout(1, 2, 3, 4));
                Assert.IsTrue(view.TryGetCardFace(PlayerId, 0, out _, out int initialValue));
                Assert.AreEqual(1, initialValue);

                view.Render(CreateState(9, 8, 7, 6), CreateLayout(9, 8, 7, 6), PlayerId, 0, true);

                yield return RunLayoutRefreshRoutine(view);

                Assert.IsTrue(view.TryGetCardFace(PlayerId, 0, out _, out int valueAfterRefresh));
                Assert.AreEqual(1, valueAfterRefresh);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator RoundRevealWithTemporarilyEmptyLayoutKeepsPreviouslyRenderedCards()
        {
            var host = new GameObject("CardTableViewRoundRevealHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                var state = CreateState(1, 2, 3, 4);
                view.Render(state, CreateLayout(1, 2, 3, 4));
                Assert.IsTrue(view.TryGetCardFace(PlayerId, 0, out _, out int initialValue));
                Assert.AreEqual(1, initialValue);

                state.Phase = GamePhase.RoundReveal;
                view.Render(state, new CardTableLayout());

                yield return null;

                Assert.IsTrue(view.TryGetCardFace(PlayerId, 0, out _, out int valueAfterEmptyLayout));
                Assert.AreEqual(1, valueAfterEmptyLayout);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PlayActionReturnsFalseWhenSwapPlanIsMissing()
        {
            var host = new GameObject("CardTableViewMissingSwapHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                view.Render(CreateState(1, 2, 3, 4), CreateLayout(1, 2, 3, 4));

                var action = new CardTableActionSnapshot
                {
                    ActionType = ActionType.UseSkill,
                    Skill = SkillType.Swap,
                    SwapOccurred = true,
                    SourcePlayerId = PlayerId,
                    TargetPlayerId = 2002,
                    SourceSlot = 0,
                    TargetSlot = 0
                };

                Assert.IsFalse(view.PlayAction(action));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PlayActionReturnsFalseWhenPeekSlotIsMissing()
        {
            var host = new GameObject("CardTableViewMissingPeekHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                view.Render(CreateState(1, 2, 3, 4), CreateLayout(1, 2, 3, 4));

                var action = new CardTableActionSnapshot
                {
                    ActionType = ActionType.UseSkill,
                    Skill = SkillType.PeekSelf,
                    SourcePlayerId = PlayerId,
                    SourceSlot = 99
                };

                Assert.IsFalse(view.PlayAction(action));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NonSelfDrawReportsActiveTransientAnimationWhenMarkerMoves()
        {
            var host = new GameObject("CardTableViewDrawHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                view.Render(CreateState(1, 2, 3, 4), CreateLayout(1, 2, 3, 4));

                var action = new CardTableActionSnapshot
                {
                    ActionType = ActionType.Draw,
                    SourcePlayerId = PlayerId + 1
                };

                Assert.IsTrue(view.PlayAction(action));
                Assert.IsTrue(view.HasActiveTransientAnimation);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static IEnumerator RunLayoutRefreshRoutine(CardTableView view)
        {
            var method = typeof(CardTableView).GetMethod("LayoutRefreshRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var routine = (IEnumerator)method.Invoke(view, null);
            while (routine.MoveNext())
                yield return routine.Current;
        }

        static GameState CreateState(params int[] values)
        {
            var state = new GameState
            {
                Phase = GamePhase.Playing,
                MyPlayerId = PlayerId,
                DrawPileCount = 10,
                DiscardPileCount = 1,
                DiscardTopValue = 5
            };
            state.Players.Add(new PlayerInfo
            {
                PlayerId = PlayerId,
                Nickname = "Tester",
                CardCount = values.Length
            });
            for (int i = 0; i < values.Length; i++)
            {
                state.MyCards.Add(new CardState
                {
                    SlotIndex = i,
                    IsKnown = true,
                    Value = values[i]
                });
            }
            return state;
        }

        static CardTableLayout CreateLayout(params int[] values)
        {
            var layout = new CardTableLayout
            {
                DrawPilePosition = new Vector2(300f, 220f),
                DrawPileSize = new Vector2(70f, 96f),
                DiscardPilePosition = new Vector2(400f, 220f),
                DiscardPileSize = new Vector2(70f, 96f),
                DrawPileCaption = "Deck",
                DiscardPileCaption = "Discard"
            };

            for (int i = 0; i < values.Length; i++)
            {
                layout.Slots.Add(new CardTableSlotLayout
                {
                    PlayerId = PlayerId,
                    SlotIndex = i,
                    AnchoredPosition = new Vector2(120f + i * 80f, 120f),
                    Size = new Vector2(60f, 84f),
                    FaceUp = true,
                    Value = values[i]
                });
            }

            layout.Slots.Add(new CardTableSlotLayout
            {
                PlayerId = PlayerId + 1,
                SlotIndex = 0,
                AnchoredPosition = new Vector2(120f, 300f),
                Size = new Vector2(60f, 84f),
                FaceUp = false,
                Value = 0
            });
            return layout;
        }
    }
}
