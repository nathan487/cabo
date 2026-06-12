# Unity Card View Migration Plan

## Decision

Move the in-game card table rendering away from rebuild-heavy UI Toolkit card `VisualElement`s and toward persistent uGUI/GameObject card views.

Recommended target:

- Keep UI Toolkit for home, room, chat, log, action buttons, reveal panels, and non-card panels.
- Add a dedicated card-table layer using Unity `GameObject`s under a Canvas.
- Represent every visible card with a persistent `CardView` component backed by `RectTransform` and `Image`.
- Represent logical card positions with persistent `CardSlotView` anchors.
- Drive animations through explicit methods instead of rebuilding UI and hiding cloned `VisualElement`s.

Use uGUI rather than world-space SpriteRenderer for the first migration pass. Cabo is a 2D card-table UI, and uGUI gives easier `RectTransform` movement, image replacement, click handling, Canvas scaling, sorting, and resolution adaptation.

## Why This Migration Is Needed

The current card table is generated in `GameTablePanel.cs` using UI Toolkit `VisualElement`s. This works for static card display and simple actions, but replacement animations are brittle because server state and animation state want different layouts at the same time.

Current problem:

1. Server sends `ActionResultNotify`.
2. `GameState` immediately updates to the authoritative final hand.
3. `RenderSeats()` rebuilds the real card row from the final hand.
4. The animation still needs to show the old hand layout first.
5. `GameTablePanel` therefore uses captured `worldBound`, hidden real cards, temporary clone cards, and delayed callbacks.

This has become fragile for `ReplaceWithDrawn` and `TakeFromDiscard`, especially multi-card replacement. Slot indices change meaning after selected cards are removed, so hiding "slot 0/1/2" after reflow can hide the wrong visual card. Swap is stable because hand counts do not change and slot meaning stays stable.

With persistent card objects, replacement can be expressed directly:

```text
selected old cards -> discard pile
selected slots become empty
survivors -> final slots
incoming card -> final slot
```

## Current State To Read First

Read these docs before editing code:

- `Docs/CURRENT_TASK.md`
- `Docs/UNITY_CLIENT_HANDOFF.md`
- `Docs/UNITY_ANIMATION_NOTES.md`
- `Docs/UNITY_CARD_VIEW_MIGRATION.md`
- `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`

Primary code:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UITheme.cs`

Current card visuals:

- True hand cards are UI Toolkit `VisualElement`s in `SeatView.CardRow`.
- Temporary animation cards are also `VisualElement`s under `_animationLayer`.
- Drawn card markers are temporary `VisualElement`s under `_animationLayer`.
- Positions are captured from `VisualElement.worldBound`.

Recent local discussion:

- Card art replacement can be done in UI Toolkit.
- Animation reliability is the real reason to migrate.
- Do not migrate the whole UI. Migrate only the in-game card table/card visuals first.

## Target Architecture

Add a new card-table view layer that can coexist with `GameTablePanel`.

Suggested scripts:

- `Assets/Scripts/UI/CardTable/CardView.cs`
- `Assets/Scripts/UI/CardTable/CardSlotView.cs`
- `Assets/Scripts/UI/CardTable/HandView.cs`
- `Assets/Scripts/UI/CardTable/CardTableView.cs`
- Optional: `Assets/Scripts/UI/CardTable/CardArtProvider.cs`
- Optional: `Assets/Scripts/UI/CardTable/CardAnimationRunner.cs`

Suggested responsibilities:

### CardView

Persistent view object for one visible card.

Minimum API:

```csharp
public sealed class CardView : MonoBehaviour
{
    public RectTransform RectTransform { get; }
    public int Value { get; private set; }
    public bool FaceUp { get; private set; }

    public void ShowFront(int value);
    public void ShowBack();
    public void SetSelected(bool selected);
    public Coroutine MoveTo(RectTransform target, float duration);
    public Coroutine MoveTo(Vector2 anchoredPosition, float duration);
    public Coroutine FlipToFront(int value, float duration);
    public void SetVisible(bool visible);
}
```

Implementation can initially use simple generated/solid card art matching current UITheme colors. The important part is persistence and animation control. Real card images can be plugged into `CardArtProvider` later.

### CardSlotView

Persistent anchor for a logical slot. It does not have to hold a card permanently, but it must provide a stable `RectTransform` target.

Minimum data:

```csharp
public sealed class CardSlotView : MonoBehaviour
{
    public long PlayerId;
    public int SlotIndex;
    public RectTransform RectTransform { get; }
    public CardView CurrentCard;
}
```

### HandView

Owns card slots and card objects for one player.

Minimum API:

```csharp
public sealed class HandView : MonoBehaviour
{
    public long PlayerId { get; }
    public void SetCardCount(int count);
    public CardSlotView GetSlot(int slot);
    public CardView GetCard(int slot);
    public IReadOnlyList<CardView> Cards { get; }
    public void RenderAuthoritativeHand(...);
}
```

For the local player, `RenderAuthoritativeHand` can show known card fronts. For opponents, show card backs unless the protocol explicitly exposes a value.

### CardTableView

Bridge from `GameState`/`GameFlow` to persistent card views.

Responsibilities:

- Create and position the card Canvas/layer.
- Create player hand views for self and opponents.
- Create draw-pile and discard-pile anchors.
- Update static card counts from `GameState`.
- Play public action animations from `ActionResultNotify` snapshots.
- Expose `HasPendingActionAnimation` or integrate with the existing `GameTablePanel.HasPendingActionAnimation` so `RoundReveal` still waits.

## Integration Strategy

Do not rewrite everything at once.

Phase 1 should preserve the existing UI Toolkit table layout and add a card-object overlay only for card visuals.

Recommended sequence:

1. Add the card-table layer under the existing runtime UI root or a sibling Canvas created by `GameBootstrap`/`GameTablePanel`.
2. Hide only the UI Toolkit card rectangles while keeping seat headers, action buttons, chat/log panel, and center panels intact.
3. Use the existing UI Toolkit `worldBound` positions only to initialize stable uGUI slot anchors for the first pass.
4. Once anchors are working, move card rendering and animation to `CardTableView`.
5. Keep existing `GameState` and `GameFlow` logic unchanged.

Do not change:

- Game rules.
- Protobuf schema.
- Server logic.
- WebSocket transport.
- Room flow.
- Scoring.
- Chat/log/sidebar layout.

## Animation Rules To Implement First

Use a queue so public action broadcasts play in server order.

Every animation must work for both local-player and opponent viewpoints.

### Draw

- A card back moves from draw pile to acting player's drawn-card marker/hand area.
- Local private drawn decision can reveal the actual value only on the acting client.

### Discard Drawn

- Drawn-card marker moves to discard pile.
- Discard pile updates after movement.

### Replace With Drawn

Success, single-card:

1. Selected old card moves from its current slot to discard pile.
2. The selected slot is empty during this first phase.
3. Incoming drawn card moves into that same final slot.
4. Other hand cards do not move.

Success, multi-card:

1. Freeze the old hand visually.
2. Selected old cards move to discard pile.
3. Their old slots are empty.
4. Survivors move from old slots to final compacted slots.
5. Incoming card moves to its final slot during the same second phase.
6. Only after movement completes should the hand be reconciled to authoritative final state.

Failure:

- Selected slots/cards visibly shake or highlight.
- Incoming card remains or moves according to current failed-exchange behavior.
- Extra penalty card should be shown when `DrewExtraPenaltyCard` is true.

### Take From Discard

Same as replace, except the incoming card starts at the discard pile rather than drawn-card marker.

### Swap

- Keep the current stable behavior as the target: source and target cards cross between slots.
- Swap is easier because hand counts do not change.

### Peek / Spy

- Preserve privacy rules.
- Acting player may see private value when protocol/state provides it.
- Other clients should see card backs unless value is public.

### Round Reveal

- `RoundReveal` must still wait until the public action animation queue is drained.

## Verification Plan

Use Unity MCP and the `unity-mcp-orchestrator` skill.

After every C# edit:

1. `validate_script` for edited scripts.
2. `refresh_unity(scope="scripts", compile="request", wait_for_ready=true)`.
3. `read_console(types=["error","warning"])`.
4. Final target: 0 errors / 0 warnings, except clearly identified unrelated MCP warnings.

Synthetic Play Mode tests:

- 4-player active game state.
- Local single-card `ReplaceWithDrawn`.
- Local multi-card `ReplaceWithDrawn`.
- Local single-card `TakeFromDiscard`.
- Local multi-card `TakeFromDiscard`.
- Opponent replace/take with hidden card backs.
- Swap after migration, to confirm no regression.
- Draw and discard drawn.
- PeekSelf and Spy.
- Action animation pending followed by `RoundReveal`; reveal must wait.

Screenshots:

- Capture before/mid/hold/after for replace and take.
- Recommended names:
  - `cardview_replace_single_mid.png`
  - `cardview_replace_multi_phase1_empty_slots.png`
  - `cardview_replace_multi_phase2_survivors_incoming.png`
  - `cardview_take_discard_multi_phase2.png`
  - `cardview_swap_cross_mid.png`
  - `cardview_round_reveal_after_queue.png`
- Screenshots under `Assets/Screenshots/` are verification artifacts. Do not commit them unless the user explicitly asks.

## Acceptance Criteria

- Local and opponent card visuals are driven by persistent card views, not rebuilt UI Toolkit card rectangles.
- Single-card replace shows the selected old card going to discard, selected slot empty, incoming card landing, and other cards staying still.
- Multi-card replace shows old selected cards leaving first, then survivors and incoming moving together to final slots.
- Take-from-discard follows the same two-phase logic, with incoming starting from discard pile.
- Swap behavior remains stable.
- Card art can be replaced through `CardArtProvider` or prefab images without changing animation code.
- No overlapping duplicate cards during replacement.
- No disappearing unrelated cards during replacement.
- No stuck temporary cards, stale highlights, or repeated turn borders.
- Round reveal still waits for the final queued action animation.
- Unity Console final check is clean.

## Recommended First Development Step

Start by creating the minimal `CardView`, `CardSlotView`, and `CardTableView` scripts with generated placeholder visuals. Then connect only the local player's hand in a synthetic state. Once that displays correctly, add opponent hands, pile anchors, and finally replace/take animations.

Do not start by deleting the current `GameTablePanel` card code. Let both systems coexist until the new card layer proves it can render and animate every required action.
