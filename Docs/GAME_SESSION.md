# Game Session — Round 3 (Final Round)

> Captured: 2026-06-06 | 4-player game, CLI clients + muduo server
> Players: pl1(10000,Host), pl2(10001), pl3(10002), pl4(10003)

## Event Timeline

Each event shows what ALL players see (action broadcast + game state).

### Turn 1 — pl1 draws, uses Spy on pl2

| Who | Sees |
|-----|------|
| **All** | `>>> pl1 drew a card from the deck` |
| **All** | Draw Pile: 35, Discard: 0 (Top: -) |
| **pl1** | `You drew: [9] — Spy skill` → choice 1 (discard only) |
| **All** | `>>> pl1 discarded the drawn card (Spy skill available)` |
| **All** | Discard Pile: 1 (Top: 9) |
| **pl1** | `SPY: Choose opponent → pl2, slot 2` |
| **All** | `>>> pl1 spied on pl2's slot 2` |
| **pl1** | `║ SPY: Opponent's card = [4] ║` (2s display) |
| **All** | Turn 2 → pl2's turn |

### Turn 2 — pl2 draws, uses Swap with pl3

| Who | Sees |
|-----|------|
| **All** | `>>> pl2 drew a card from the deck` |
| **All** | Draw Pile: 34, Discard: 1 (Top: 9) |
| **pl2** | `You drew: [11] — Swap skill` → choice 3 (discard + use skill) |
| **All** | `>>> pl2 discarded the drawn card (Swap skill available)` |
| **All** | Discard Pile: 2 (Top: 11) |
| **pl2** | `SWAP: my slot 0 ↔ pl3 slot 0` |
| **pl3** | `[GameState] Swap: my slot 0 now unknown` — card becomes [?] |
| **All** | `>>> pl2 swapped their slot 0 with pl3's slot 0` |
| **pl2** | `Swap completed! Your slot 0 now unknown (blind swap)` (2s) |
| **All** | Turn 3 → pl3's turn |

### Turn 3 — pl3 calls CABO

| Who | Sees |
|-----|------|
| **pl3** | Your Turn! → choice 3 (Call CABO) |
| **All** | `>>> pl3 called CABO!` |
| **All** | Turn 4 → pl4's turn (final round, pl3 skipped) |

### Turn 4 — pl4 draws, replaces card

| Who | Sees |
|-----|------|
| **All** | `>>> pl4 drew a card from the deck` |
| **All** | Draw Pile: 33, Discard: 2 (Top: 11) |
| **pl4** | `You drew: [13]` → choice 2 (replace), slot 0 |
| **All** | `>>> pl4 replaced 1 card(s) with the drawn card` |
| **All** | Discard Pile: 3 (Top: 13) |
| **All** | Turn 5 → pl1's turn |

### Turn 5 — pl1 draws, replaces card

| Who | Sees |
|-----|------|
| **All** | `>>> pl1 drew a card from the deck` |
| **All** | Draw Pile: 32, Discard: 3 (Top: 13) |
| **pl1** | `You drew: [9] — Spy skill` → choice 2 (replace, no skill), slot 1 |
| **pl1** | Cards: [0] [9] [?] [?] (was [0] [10] [?] [?]) |
| **All** | `>>> pl1 replaced 1 card(s) with the drawn card` |
| **All** | Discard Pile: 4 (Top: 10) |
| **All** | Turn 6 → pl2's turn |

### Turn 6 — pl2 draws, uses PeekSelf → Round Ends

| Who | Sees |
|-----|------|
| **All** | `>>> pl2 drew a card from the deck` |
| **All** | Draw Pile: 31, Discard: 4 (Top: 10) |
| **pl2** | `You drew: [8] — Peek Self skill` → choice 3 (discard + use skill) |
| **All** | `>>> pl2 discarded the drawn card (Peek Self skill available)` |
| **pl2** | `PEEK SELF: slot 0 → [1]` (2s display) |
| **pl2** | Cards: [1] [4] [?] [?] (was [?] [4] [?] [?]) |
| **All** | `>>> pl2 peeked at their own slot 0` |

### Round 3 Reveal

All players see the scoring panel:

```
pl1:  [0] [9] [6] [2]  = 17
pl2:  [1] [4] [1] [8]  = 14
pl3 (called CABO):  [12] [11] [4] [3]  = 30  (+10 penalty) = 40
pl4:  [3] [1] [5] [4]  = 13  ← Lowest!

Scores after Round 3:
  pl1: 53  |  pl2: 51  |  pl3: 106  |  pl4: 70
```

### Inter-Round & Game Over

| Who | Sees |
|-----|------|
| **All** | Ready panel: 0/4 ready → GameOverNotify overrides |
| **All** | Final: pl2(51) WINNER, pl1(53), pl4(70), pl3(106) |

---

## Server Log (Key Events)

```
Round start → GameStartNotify + TurnStartNotify (turn=1, player=10000)

[Game] DrawCard player=10000 → value=9 (Spy)
[Game] DiscardDrawn → Skill card (9) discarded
[Game] Spy: pl1 looks at pl2's slot 2 = 4
[Game] TurnStartNotify → turn=2, player=10001

[Game] DrawCard player=10001 → value=11 (Swap)
[Game] DiscardDrawn → Skill card (11) discarded
[Game] Swap: pl2[0] <-> pl3[0]
[Game] TurnStartNotify → turn=3, player=10002

[Game] Steady called by pl3! Final round: 3 turns remaining
[Game] TurnStartNotify → turn=4, player=10003

[Game] DrawCard player=10003 → value=13
[Game] ReplaceWithDrawnRsp: success
[Game] TurnStartNotify → turn=5, player=10000

[Game] DrawCard player=10000 → value=9 (Spy)
[Game] ReplaceWithDrawnRsp: success
[Game] TurnStartNotify → turn=6, player=10001

[Game] DrawCard player=10001 → value=8 (PeekSelf)
[Game] DiscardDrawn → Skill card (8) discarded
[Game] PeekSelf: pl2 looks at own slot 0 = 1
[Game] Round 3 reveal — scoring...
[Game] Game over! Winner: pl2 (51 pts)
```

---

## Key Observations for Unity Migration

1. **Action broadcast works**: All 4 clients see `>>> pl1 drew`, `>>> pl2 swapped`, `>>> pl3 called CABO`
2. **Skill flow**: Discard→ActionResult→skill params→UseSkillRsp→ActionResult→TurnStart (all visible in 1.5s window)
3. **Swap visible to target**: pl3's slot 0 became [?] via `ActionResultNotify.swap_occurred` handler
4. **PeekSelf updates card**: pl2's slot 0 changed from [?] to [1] on their own screen only (value private)
5. **CABO + final round**: pl3 called, others each got 1 more turn (pl3 skipped in `nextPlayer`)
6. **Skill discard without use**: pl1 drew Spy card (value 9) in Turn 5 but chose replace (no skill used) — turn ended normally
7. **1.5s delay working**: Action text displayed ~1.5s before TurnStartNotify switches current player
8. **First turn discard empty**: `Discard Pile: 0 (Top: -)` — "Take from discard" hidden
9. **GameOver after reveal**: Ready panel shown briefly then replaced by GameOver screen (game ended at round 3)
