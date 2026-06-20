using System.Collections;
using System.Reflection;
using Cabo.Client;
using Cabo.Client.UI.CardTable;
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
            return layout;
        }
    }
}
