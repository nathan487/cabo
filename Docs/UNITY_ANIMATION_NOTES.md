# Unity Animation Notes

> Updated: 2026-06-07
> Target project: `unity dev/New Client_Unity_Base_Cli`

## Purpose

This document records the current Unity card-table animation implementation so it can be replaced or upgraded later without rediscovering the current behavior from code.

The current animation layer is intentionally lightweight. It is a 2D UI Toolkit implementation inside `GameTablePanel.cs`, not a full animation framework.

## Current Design

Primary files:

- `Assets/Scripts/UI/GameTablePanel.cs`
- `Assets/Scripts/Core/GameState.cs`
- `Assets/Scripts/Core/GameFlow.cs`

The table UI is generated in C# using UI Toolkit. `GameTablePanel` owns the visual elements and local animation state. Game rules and network state remain in `GameFlow` and `GameState`.

Animations are driven by two sources:

- Local state transition: `WaitingDrawRsp -> AwaitingDrawnDecision`
- Broadcast state: `ActionResultNotify`, saved as structured fields on `GameState`

The intended product direction is that player action notifications should be represented by animation first, not by CLI-style text logs.

## Animation Layer

`GameTablePanel` creates a top-level overlay:

- `_animationLayer`
- `PickingMode.Ignore`
- absolute positioned over the root UI

Temporary animated cards are added to `_animationLayer`, moved using `VisualElement.schedule`, then removed when the animation completes.

Current helper methods:

- `ScheduleDrawAnimation(int value)`
- `PlayDrawAnimation(int value)`
- `ScheduleActionAnimation(GameState state)`
- `PlayActionAnimation(ActionAnimationSnapshot action)`
- `PlaySkillAnimation(ActionAnimationSnapshot action)`
- `PlayFlyCard(...)`
- `PulsePlayer(...)`
- `PulseCard(...)`
- `PulseElement(...)`

Coordinate source:

- Uses `VisualElement.worldBound`
- Converts world positions to root-local positions by subtracting `_root.worldBound`

## Broadcast Action Data

`GameState.HandleActionResult(ActionResultNotify ar)` stores the latest action in structured fields:

- `LastActionSequence`
- `LastActionType`
- `LastActionSkill`
- `LastActionSourcePlayerId`
- `LastActionTargetPlayerId`
- `LastActionSourceSlot`
- `LastActionTargetSlot`
- `LastActionSwapOccurred`
- `LastActionTurnEnded`
- `LastActionExchangeSucceeded`
- `LastActionIncomingCardValue`
- `LastActionDiscardedCount`

`GameTablePanel.RenderGame()` compares `LastActionSequence` with `_lastAnimatedActionSequence`. If it is new, it schedules a public action animation.

Important: `ActionResultNotify` is broadcast by the server, so these animations are visible on every Unity client that receives the message, not only the acting player.

## Current Animation Coverage

Timing note:

- Public action movement now uses slower timings than the first pass so players can read the table action.
- Basic draw/discard/replace/take movement is about 0.72-0.78s.
- Skill inspection movement is about 0.96s out, about 1.18s held, then about 0.96s back.
- Peek flip hold is about 1.95s total.

### Draw

Trigger:

- Local draw decision animation: `WaitingDrawRsp -> AwaitingDrawnDecision`
- Public broadcast animation: `ActionType.Draw`

Current visual:

- A temporary card flies from the deck pile to the acting player seat or drawn-card decision area.

Limitations:

- Broadcast draw uses a card back because other players should not see the drawn value.
- Local drawn-card decision shows the actual value because `DrawCardRsp` is private to the acting player.

### Discard Drawn

Trigger:

- `ActionType.DiscardDrawn`

Current visual:

- A temporary card flies from acting player seat to discard pile.
- Acting player seat pulses.
- Skill cards use skill-tinted pulse color.

Limitations:

- The animation uses current discard top value from local state. If timing changes, this should be replaced by explicit discarded-card value from protocol or a client-side pending value.

### Replace With Drawn

Trigger:

- `ActionType.ReplaceWithDrawn`

Current visual:

- Temporary card motion from acting player toward discard pile.
- Acting player seat pulses with a color based on `IncomingCardValue` when available.

Limitations:

- Does not yet animate from exact source slots.
- Does not yet distinguish multi-card success from failed exchange visually.
- Does not yet animate card count re-layout.

### Take From Discard

Trigger:

- `ActionType.TakeFromDiscard`

Current visual:

- Temporary card flies from discard pile to acting player seat.
- Acting player seat pulses.

Limitations:

- Does not yet animate to exact selected slot(s).
- Does not yet show failure animation separately.

### Peek Self

Trigger:

- `ActionType.UseSkill` with `SkillType.PeekSelf`

Current visual:

- Pulses the source player's selected slot.
- Plays an overlay flip animation at that exact slot and holds it long enough to read.
- On the acting player's own Unity client, the overlay can show the private value when `UseSkillRsp.peeked_value` has arrived or the card has already become locally known.
- On other clients, the same slot visibly flips/highlights but remains a card back because the protocol does not broadcast the hidden value.

Limitations:

- Non-acting clients cannot show the actual value without a server/protocol change.
- If `ActionResultNotify` arrives before the private `UseSkillRsp`, the actor may see the slot flip without the value for that one animation; the state still updates when the private response is processed.

### Spy

Trigger:

- `ActionType.UseSkill` with `SkillType.Spy`

Current visual:

- The target slot pulses.
- A temporary card back moves from the target slot to the skill source player's seat, pauses in front of that player, then returns to the target slot.
- This movement is public and visible on all Unity clients, making the selected target slot clear without text.
- On the acting player's own Unity client, the temporary inspected card can show the private peek value when available.

Limitations:

- Does not reveal target value publicly; that is intentionally private in the current protocol.
- If product design later wants every client to see the inspected value, `ActionResultNotify` would need a new public field and the server would need to send it.

### Swap

Trigger:

- `ActionType.UseSkill` with `SkillType.Swap` and `SwapOccurred=true`

Current visual:

- Two temporary card backs fly between source slot and target slot.
- Both affected slots pulse.
- Movement is slower than the first pass so observers can read both endpoints.

Limitations:

- Uses card backs only.
- Does not physically reorder visual card elements before/after server state is applied.
- If one endpoint is missing, it falls back to player-seat bounds.

### Call CABO

Trigger:

- `ActionType.CallSteady`

Current visual:

- Acting player seat pulses gold.
- A short CABO banner appears over the caller.
- The caller remains marked on their seat until the round reveal / next round transition clears the final-round state.

Limitations:

- The persistent marker is a UI state marker, not a separate animation track.

## Text Notifications

The main table no longer uses `LastActionMessage` as the primary action notification.

Current use:

- `LastActionMessage` still exists for debug/reference.
- Main table status shows only compact state text such as "Table is synced with the server."

Future direction:

- Remove or hide remaining action-log text from normal gameplay.
- Keep debug logging behind an explicit dev/debug mode if needed.

## Upgrade Targets

Recommended next iterations:

1. Exact slot movement for replace/take actions.
2. Failed exchange animation: shake selected cards, then show incoming card added to hand.
3. Private reveal animation for PeekSelf and Spy on the acting client only.
4. Swap animation that waits for both source and target card elements, then visually exchanges them before final re-render.
5. Round reveal animation: flip all cards face-up, then score rows.
6. CABO call animation: visible table-wide signal and final-round counter transition.
7. Animation queue: process multiple `ActionResultNotify` messages in order instead of playing only latest immediately.
8. Dedicated animation data model instead of many `LastAction...` fields on `GameState`.

## Known Technical Constraints

- UI Toolkit scheduled animations are frame-driven but lightweight; they are suitable for this 2D table pass.
- Screenshots may miss short animations because current durations are around 0.34 to 0.56 seconds.
- The server does not expose every visual detail needed for perfect animation. For example, discarded card value and exact multi-card exchange visuals may require either protocol additions or carefully maintained client pending-action state.
- `ActionResultNotify + TurnStartNotify` can arrive in the same TCP burst. The client must keep the existing drain-then-decide behavior and avoid blocking state updates on animations.

## Verification Notes

Verified with Unity MCP:

- Script validation passed for `GameState.cs` and `GameTablePanel.cs`.
- Unity compilation completed after refresh.
- Play Mode simulation triggered public `Draw` and `Swap` animations.
- Console reported 0 errors and 0 warnings after verification.
- Temporary screenshots under `Assets/Screenshots` were cleaned after verification.
