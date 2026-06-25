using System.Collections;
using System.Collections.Generic;
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

        [Test]
        public void TransientDrawnCardsUseDedicatedRootAboveStaticCards()
        {
            var host = new GameObject("CardTableViewTransientLayerHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                view.Render(CreateState(1, 2, 3, 4), CreateLayout(1, 2, 3, 4));

                var action = new CardTableActionSnapshot
                {
                    ActionType = ActionType.Draw,
                    SourcePlayerId = PlayerId + 1,
                    DrawPilePosition = new Vector2(300f, 220f),
                    DrawPileSize = new Vector2(70f, 96f),
                    TargetInspectionPosition = new Vector2(120f, 300f),
                    TargetInspectionSize = new Vector2(60f, 84f)
                };

                Assert.IsTrue(view.PlayAction(action));

                var marker = GetDrawnMarker(view, PlayerId + 1);
                var staticRoot = GetPrivateField<RectTransform>(view, "_cardRoot");
                var transientRoot = GetPrivateField<RectTransform>(view, "_transientRoot");

                Assert.AreSame(transientRoot, marker.RectTransform.parent);
                Assert.Greater(
                    transientRoot.GetSiblingIndex(),
                    staticRoot.GetSiblingIndex(),
                    "Moving cards must live in a dedicated top layer so static pile/card refreshes cannot cover them.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DrawAnimationStartsWithDrawPileSize()
        {
            var host = new GameObject("CardTableViewDrawSizeHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                view.Render(CreateState(1, 2, 3, 4), CreateLayout(1, 2, 3, 4));

                var drawPileSize = new Vector2(70f, 96f);
                var targetSize = new Vector2(112f, 148f);
                var action = new CardTableActionSnapshot
                {
                    ActionType = ActionType.Draw,
                    SourcePlayerId = PlayerId + 1,
                    DrawPilePosition = new Vector2(300f, 220f),
                    DrawPileSize = drawPileSize,
                    TargetInspectionPosition = new Vector2(120f, 300f),
                    TargetInspectionSize = targetSize
                };

                Assert.IsTrue(view.PlayAction(action));

                var marker = GetDrawnMarker(view, PlayerId + 1);
                Assert.AreEqual(drawPileSize.x, marker.RectTransform.sizeDelta.x, 0.01f);
                Assert.AreEqual(drawPileSize.y, marker.RectTransform.sizeDelta.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SwapAnimationStartsWithEachCardsSourceSize()
        {
            var host = new GameObject("CardTableViewSwapSizeHost", typeof(RectTransform));
            var view = CardTableView.Create(host.transform);

            try
            {
                var sourceSize = new Vector2(60f, 84f);
                var targetSize = new Vector2(46f, 66f);
                var sourcePosition = new Vector2(120f, 120f);
                var targetPosition = new Vector2(240f, 260f);
                view.Render(CreateTwoPlayerState(), CreateSwapLayout(sourcePosition, sourceSize, targetPosition, targetSize));

                var action = new CardTableActionSnapshot
                {
                    ActionType = ActionType.UseSkill,
                    Skill = SkillType.Swap,
                    SwapOccurred = true,
                    SourcePlayerId = PlayerId,
                    TargetPlayerId = PlayerId + 1,
                    SourceSlot = 0,
                    TargetSlot = 0
                };
                action.SourceSwapSlots.Add(new CardTableSlotSnapshot
                {
                    PlayerId = PlayerId,
                    SlotIndex = 0,
                    AnchoredPosition = sourcePosition,
                    Size = sourceSize,
                    FaceUp = true,
                    Value = 1
                });
                action.TargetSlots.Add(new CardTableSlotSnapshot
                {
                    PlayerId = PlayerId + 1,
                    SlotIndex = 0,
                    AnchoredPosition = targetPosition,
                    Size = targetSize,
                    FaceUp = false,
                    Value = 0
                });

                Assert.IsTrue(view.PlayAction(action));

                var transients = GetPrivateField<List<CardView>>(view, "_transientCards");
                var sourceCard = transients.Find(card => card != null && card.name == $"Card_{PlayerId}_0");
                var targetCard = transients.Find(card => card != null && card.name == $"Card_{PlayerId + 1}_0");

                Assert.NotNull(sourceCard);
                Assert.NotNull(targetCard);
                Assert.AreEqual(sourceSize.x, sourceCard.RectTransform.sizeDelta.x, 0.01f);
                Assert.AreEqual(sourceSize.y, sourceCard.RectTransform.sizeDelta.y, 0.01f);
                Assert.AreEqual(targetSize.x, targetCard.RectTransform.sizeDelta.x, 0.01f);
                Assert.AreEqual(targetSize.y, targetCard.RectTransform.sizeDelta.y, 0.01f);
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

        static GameState CreateTwoPlayerState()
        {
            var state = CreateState(1);
            state.Players.Add(new PlayerInfo
            {
                PlayerId = PlayerId + 1,
                Nickname = "Opponent",
                CardCount = 1
            });
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

        static CardTableLayout CreateSwapLayout(Vector2 sourcePosition, Vector2 sourceSize, Vector2 targetPosition, Vector2 targetSize)
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

            layout.Slots.Add(new CardTableSlotLayout
            {
                PlayerId = PlayerId,
                SlotIndex = 0,
                AnchoredPosition = sourcePosition,
                Size = sourceSize,
                FaceUp = true,
                Value = 1
            });
            layout.Slots.Add(new CardTableSlotLayout
            {
                PlayerId = PlayerId + 1,
                SlotIndex = 0,
                AnchoredPosition = targetPosition,
                Size = targetSize,
                FaceUp = false,
                Value = 0
            });
            return layout;
        }

        static CardView GetDrawnMarker(CardTableView view, long playerId)
        {
            var markers = GetPrivateField<Dictionary<long, CardView>>(view, "_drawnMarkers");
            Assert.IsTrue(markers.TryGetValue(playerId, out var marker));
            Assert.NotNull(marker);
            return marker;
        }

        static T GetPrivateField<T>(CardTableView view, string fieldName)
        {
            var field = typeof(CardTableView).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"Missing private field {fieldName}.");
            return (T)field.GetValue(view);
        }
    }
}
