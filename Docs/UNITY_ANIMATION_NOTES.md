# Unity Animation Notes

## 2026-06-11 Decision: Migrate Card Visuals To Persistent Card Views

The next animation task should not continue deepening the UI Toolkit clone/hide workaround for replacement animations. Start the card-table migration described in:

- `Docs/UNITY_CARD_VIEW_MIGRATION.md`

Decision:

- Keep UI Toolkit for home, room, chat, log, action buttons, reveal panels, and non-card UI.
- Move in-game card visuals to persistent uGUI/GameObject card views.
- Prefer uGUI `RectTransform` + `Image` card objects under a Canvas for the first pass.

Reason:

- Current cards are `VisualElement`s rebuilt by `GameTablePanel.RenderSeats()`.
- Action broadcasts immediately update `GameState` to the authoritative final hand.
- Replacement/take animations need to show old layout first, then final layout.
- The current approach uses captured `worldBound`, hidden real cards, temporary clone cards, and scheduled callbacks.
- This is fragile when selected cards are removed and survivors reflow; slot indices change meaning after the server state applies.
- Swap is stable because hand counts do not change.

Target replacement behavior for the new CardView layer:

- Single replace/take:
  - selected old card moves to discard;
  - selected slot is empty;
  - incoming card lands in that final slot;
  - other hand cards stay still.
- Multi replace/take:
  - old hand is visually frozen;
  - selected old cards move to discard;
  - selected old slots remain empty;
  - survivors and incoming move together to final compacted slots;
  - final authoritative hand is shown only after movement completes.

Validation remains the same:

- Use Unity MCP.
- Validate/compile after C# edits.
- Check Console.
- Use synthetic 4-player states and screenshots for before/mid/hold/after animation phases.
- Ensure `RoundReveal` still waits for pending action animations.

## 2026-06-10 Next Work: Animation Experience Polish

The next requested Unity task is to review and improve all in-game action animations for both the local player and opponents.

Start from:

- `Docs/superpowers/plans/2026-06-10-game-animation-polish-plan.md`
- `Assets/Scripts/UI/GameTablePanel.cs`
- `Assets/Scripts/Core/GameState.cs`
- `Assets/Scripts/Core/GameFlow.cs`
- `Assets/Scripts/UI/UIManager.cs`

Focus areas:

- action order: visual sequence must match server action order;
- source/target clarity: players should understand who acted and which slot changed;
- timing: readable but not sluggish;
- smoothness: no abrupt layout jumps, stuck temporary cards, or stale highlights;
- viewpoint quality: local-player feedback and opponent-action feedback must both be understandable;
- round reveal handoff: the final action animation should finish before settlement/reveal UI takes over.

Use Unity MCP for:

- compile refresh and Console checks;
- synthetic 4-player game states;
- screenshots at before/mid/hold/after timestamps;
- round-reveal handoff tests while action animation is pending.

Do not change:

- game rules;
- protobuf schema;
- server logic;
- WebSocket transport;
- table/chat layout.

Screenshots under `Assets/Screenshots/` are verification artifacts and should not be committed unless explicitly requested.

## 2026-06-09 Update: Round Reveal Waits for Action Queue

Latest behavior:

- `GameTablePanel` exposes `HasPendingActionAnimation`, based on the queued action animation end time.
- `UIManager` delays `RoundReveal` rendering while a public action animation is still pending.
- When the action queue drains, `GameTablePanel` invokes the callback registered by `UIManager`, causing UI routing to re-evaluate and render the settlement panel automatically.
- `ReleaseTurnDisplay(...)` no longer calls `RenderGame()` directly. This prevents a late animation callback from pulling the user back from settlement into the game panel.
- Round reveal uses a compact settlement layout:
  - compact pile cards,
  - internal score-list scrolling,
  - fixed bottom ready controls,
  - reveal-only numeric card faces without skill badges.

Verified with Unity MCP:

- Synthetic state: action animation queued, then round reveal state injected before the queue finished.
- Immediate check: `pending_after_reveal=True`, so reveal was deferred and game table remained visible for the final animation.
- Delayed check after the queue drained: `phase=RoundReveal`, `pending=False`, and reveal labels including `本轮得分` / `第 3 轮结算` were present.
- Final clean compile/Console check returned 0 errors/warnings.

## 2026-06-08 Latest Animation Fix

Committed in `78958c9 Improve card action animation clarity`.

Latest behavior:

- PeekSelf:
  - When an opponent uses the self-peek skill, the selected slot is blanked.
  - A card moves from that slot to the acting player's name/seat area, pauses, then returns.
  - Other clients see card backs only; private values are not leaked.
- Spy:
  - The viewed target slot is blanked.
  - The inspected card moves from the target player to the acting player's name/seat area, pauses, then returns.
  - The animation no longer moves generically to the center.
- Turn display:
  - After an action broadcast, the visual current-turn label is held on the acting player until the queued action animation finishes.
  - New-turn buttons are deferred while the previous action is still being shown.
- Replace / take-from-discard:
  - The temporary drawn/incoming card stays visible until it actually flies into the target slot.
  - Selected old cards fly to the discard pile in a staggered sequence.
  - Selected slots remain blank during the hold period so observers can see which slots were operated on.
- Multi-card replace:
  - The previous hand layout is captured before server-driven reflow.
  - A temporary old-hand overlay shows non-selected old cards while selected slots are blank.
  - This reduces residual-card artifacts and makes the selected slots easier to infer.

Known design target: action animations should communicate what happened without requiring text notifications. Future changes should preserve the source slot, target slot, empty-slot hold, and final landing position as the primary visual information.

> Updated: 2026-06-08
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
- Basic draw/discard/replace/take movement is about 1.15s per move segment.
- Replace/take/swap actions reserve an empty slot for about 0.65s before the incoming card lands.
- Spy inspection is about 1.20s into the center inspection zone, about 2.25s held, then about 1.20s returned.
- Peek flip hold is about 3.00s total.
- Public action animations are queued so back-to-back server broadcasts do not clear each other immediately.

### Draw

Trigger:

- Local draw decision animation: `WaitingDrawRsp -> AwaitingDrawnDecision`
- Public broadcast animation: `ActionType.Draw`

Current visual:

- A temporary card back flies from the deck pile to the acting player seat.
- The card remains near that seat as a drawn-card marker until the follow-up action consumes it.

Limitations:

- Broadcast draw uses a card back because other players should not see the drawn value.
- Local drawn-card decision shows the actual value because `DrawCardRsp` is private to the acting player.

### Discard Drawn

Trigger:

- `ActionType.DiscardDrawn`

Current visual:

- The acting player's drawn-card marker moves from their seat to the discard pile.
- Acting player seat pulses.
- Skill cards use skill-tinted pulse color.

Limitations:

- The animation uses current discard top value from local state. If timing changes, this should be replaced by explicit discarded-card value from protocol or a client-side pending value.

### Replace With Drawn

Trigger:

- `ActionType.ReplaceWithDrawn`

Current visual:

- The selected slot or slots are captured before the new server state is rendered.
- Selected slots are temporarily blanked so observers can identify exactly which hand positions were exchanged.
- Outgoing hand cards move from those slot positions to the discard pile.
- After a short empty-slot hold, the drawn-card marker moves into the primary selected slot.
- Failed exchanges shake the selected slots and animate the incoming/penalty card into the acting player's hand area.
- Failed multi-card exchanges follow the server rule visually and in local hand state: 2-card mismatch adds the incoming card (+1), while 3-or-more mismatch also adds the extra penalty draw (+2 total).

Limitations:

- Hidden outgoing card values are still shown as backs unless the protocol later exposes public values.
- Multi-card final layout is still rendered by server state; the animation shows the selected old slots and then lets the UI settle into the authoritative count.

### Take From Discard

Trigger:

- `ActionType.TakeFromDiscard`

Current visual:

- The selected slot or slots are temporarily blanked.
- Outgoing hand cards move from selected slots to the discard pile.
- The previous discard top value moves from the discard pile into the primary selected slot.
- Failed exchanges shake the selected slots and leave the discard card in place.

Limitations:

- Hidden outgoing card values are still shown as backs.

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

- The target card slot is temporarily hidden, leaving an empty slot at the target player's hand.
- The inspected card moves into the center table inspection zone and remains there long enough to read.
- After the hold, the center inspection zone clears, a return card animation plays, and the original target slot is restored.
- This slot-emptying behavior is public and visible on all Unity clients, making the selected target slot clear without text.
- On the acting player's own Unity client, the center inspected card can show the private peek value when available.
- Inspection recovery is driven from `GameBootstrap.Update()` via `GameTablePanel.Tick()` instead of delayed UI Toolkit callbacks, so the center zone cannot remain stuck after the hold period.

Limitations:

- Does not reveal target value publicly; that is intentionally private in the current protocol.
- If product design later wants every client to see the inspected value, `ActionResultNotify` would need a new public field and the server would need to send it.

### Swap

Trigger:

- `ActionType.UseSkill` with `SkillType.Swap` and `SwapOccurred=true`

Current visual:

- Source and target slots are temporarily blanked before movement starts.
- Two temporary card backs cross between the source and target slots.
- Both affected player seats pulse for the whole sequence.
- Movement is slower than the first pass so observers can read both endpoints.
- Seat pulse recovery re-renders from current game state instead of restoring stale border colors, so old turn highlights do not remain after `TurnStartNotify`.

Limitations:

- Uses card backs only.
- Does not reveal either swapped value.
- Final ownership is rendered from authoritative server state after the animation completes.
- If one endpoint is missing, it falls back to player-seat bounds.

### Call CABO

Trigger:

- `ActionType.CallSteady`

Current visual:

- Acting player seat pulses gold.
- A short CABO banner appears over the caller.
- The caller remains marked on their seat until the round reveal / next round transition clears the final-round state.
- The persistent caller marker now uses a red seat tint and red tag so it is visually distinct from the current-turn gold highlight.

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

1. Round reveal animation: flip all cards face-up, then score rows.
2. CABO call animation: visible table-wide signal and final-round counter transition.
3. Dedicated animation data model instead of many `LastAction...` fields on `GameState`.
4. Optional public per-card discard detail if the server later exposes every outgoing card value.

## Known Technical Constraints

- UI Toolkit scheduled animations are frame-driven but lightweight; they are suitable for this 2D table pass.
- Screenshots can still miss movement segments, but the current skill inspection hold is long enough to capture in normal Game View verification.
- The Unity C# generated proto now includes `ActionResultNotify.player_hands`, so opponent hand counts can update after every broadcast.
- The server does not expose every visual detail needed for perfect animation. For example, hidden outgoing hand-card values are intentionally not revealed.
- `ActionResultNotify + TurnStartNotify` can arrive in the same TCP burst. The client must keep the existing drain-then-decide behavior and avoid blocking state updates on animations.
- Seat-border pulse animations must not restore captured border colors after the turn changes; they should re-render seat headers from `GameState`.

## Verification Notes

Verified with Unity MCP:

- Script validation passed for `GameState.cs` and `GameTablePanel.cs`.
- Unity compilation completed after refresh.
- Script validation also passed for `Assets/Scripts/Proto/Generated/Game.cs` after syncing `player_hands`.
- Console reported 0 errors and 0 warnings after verification.
- MCP Game View screenshot captured only the camera background in the current editor state, so final animation visual approval should use user-provided real-game screenshots.
- After user screenshot review, fixed stale dual TURN borders and corrected 3-or-more failed multi-exchange rendering to show the extra penalty card.
