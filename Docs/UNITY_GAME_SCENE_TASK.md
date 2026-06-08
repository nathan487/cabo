# Unity Game Scene Task

> Created: 2026-06-07
> Target project: `unity dev/New Client_Unity_Base_Cli`

## One-Line Goal

Build the actual multiplayer Cabo card table scene for the Unity client: keep CLI behavior and message flow as the logic reference, but replace CLI-like text rendering with a real card game UI that a player can use visually and interactively.

## Current Baseline

The lobby / pre-game flow is already proven end-to-end:

- Unity host connects to `127.0.0.1:8888`.
- Host enters nickname and creates a room.
- Room code is visible and can be copied.
- Three bot clients can join and ready.
- Unity host can ready and start.
- Unity transitions from `SampleScene` to `CaboGameScene`.
- Final verified state:

```text
scene=CaboGameScene; connected=True; flow=Playing; phase=Playing; players=4; cards=4
```

The next task is not networking foundation. The next task is making `CaboGameScene` into a usable online card game table.

## Important Product Direction

Do not fully mimic the CLI UI.

The CLI client remains the behavior reference for:

- state transitions
- drain-then-decide ordering
- what actions are legal in each sub-state
- how server messages update state
- multi-card exchange rules
- skill flow
- round reveal / inter-round ready / game over handling

But the Unity game scene must render differently:

- Use a real table layout, not a terminal transcript.
- Render cards as card-shaped UI elements, not `[?]` / `[7]` text labels.
- Use spatial multiplayer positions: self at bottom, opponents at top/left/right.
- Use click/tap card selection instead of asking for slot numbers as text.
- Show turn ownership visually, not only a text arrow.
- Show piles as visual deck/discard stacks.
- Show action state through panels/buttons/highlights.
- Keep logs secondary; the main experience should be the table.

## Current Game Scene Gap

Current `Assets/Scripts/UI/GameTablePanel.cs` is still a prototype. It renders:

- opponent rows with text labels
- cards as `[?]`
- own cards as `[value]` / `[?]`
- action buttons in a row
- reveal/game-over as text blocks

This is enough to debug state, but not enough for the next milestone.

## Target UX

### Table Layout

The first screen after game start should be a full-screen table:

- Center:
  - draw pile stack
  - discard pile top card
  - round/turn indicator
- Bottom:
  - local player's name, score, ready/turn state
  - local player's card row, with stable card slots
  - selected cards clearly highlighted
  - primary action controls near the local hand
- Top/left/right:
  - opponent name
  - total score
  - card backs/count
  - current-turn highlight

Use restrained game-table styling: dark table background, clear card contrast, readable labels, no marketing hero layout.

### Card Rendering

Each card should be a reusable visual element with stable size:

- hidden card: card back
- known own card: face value
- drawn card: face-up temporary card near action controls
- discard top: face-up card
- opponent cards: card backs, count updated by state
- selected cards: border/glow/highlight
- illegal selection: disabled or rejected with status text

Card values should be readable at desktop Game View size. Use color or badge hints for skill values:

- 7-8: Peek self
- 9-10: Spy
- 11-12: Swap

Do not rely on emoji for core status.

### Player Actions

Actions should map to CLI flow but be UI-native.

Main turn options:

- Draw from deck
- Take discard
- Call CABO

After drawing:

- show drawn card
- Replace with selected own card(s)
- Discard drawn card
- if skill card is eligible, allow Use Skill after discard decision as currently modeled by `GameFlow`

Take from discard:

- select one or more own card slots
- confirm replacement
- support multi-card selection

Skill flows:

- Peek self: click one own hidden/known slot, show private result
- Spy: choose opponent, then one opponent slot
- Swap: choose own slot, choose opponent, choose opponent slot
- Skip skill if needed by protocol/flow

The UI should make the current `GameSubState` obvious.

## Logic Rules To Preserve From CLI

Keep these behavior rules aligned with the CLI:

- Drain all pending server messages before making UI/action decisions.
- `ActionResultNotify` is the main public animation/update driver.
- `TurnStartNotify` determines current player and action availability.
- `DrawCardRsp` creates the drawn-card decision state.
- `ReplaceWithDrawnRsp` and `TakeFromDiscardRsp` must handle success/failure and card count changes.
- Skill result values are private where the protocol says private.
- First turn or empty discard pile disables Take discard.
- CABO cannot be called during final round.
- Round reveal must not be hidden by a fast next `GameStartNotify`.
- Inter-round ready state must sync from room state notifications.

## Suggested Implementation Plan

### Step 1: Stabilize GameTablePanel Structure

Refactor only as much as needed inside the UI boundary.

Recommended small components/classes inside `Assets/Scripts/UI`:

- `CardView` or `CardVisual`: UI Toolkit `VisualElement` wrapper for card face/back/selected/disabled.
- `PlayerSeatView`: name, score, turn highlight, card row for one player.
- `PileView`: draw/discard piles and counts.
- Keep `GameTablePanel` as the coordinator.

Avoid adding a new global manager unless there is a clear need.

### Step 2: Build Static Table Render

Use current `GameState` only:

- map players into self/top/left/right
- render own cards and opponent backs
- render draw/discard pile counts
- render current turn highlight
- render score/name/card count

Acceptance:

- after the existing 4-player start test, `CaboGameScene` shows a real table with all 4 players and 4 local cards.
- no text-only `[?]` card rows remain in the main game scene.

### Step 3: Card Selection UI

Add selection state in UI layer:

- selected own card slots
- selected opponent player
- selected opponent slot

Expose selection to `GameFlow` actions:

- `DoReplaceWithDrawn(int[] slots)`
- `DoTakeFromDiscardSlots(int[] slots)`
- `DoSkillPeek(int slot)`
- `DoSkillSpyTarget(long targetId)`
- `DoSkillSpySlot(int slot)`
- `DoSkillSwapMySlot(int slot)`
- `DoSkillSwapTargetPlayer(long targetId)`
- `DoSkillSwapTargetSlot(int slot)`

Acceptance:

- UI can select cards by clicking/tapping card visuals.
- selected slots are highlighted.
- confirm buttons call the existing `GameFlow` methods.

### Step 4: Action Panels Per Sub-State

Render controls based on `GameFlow.SubState`:

- `AwaitingMainInput`: Draw / Take discard / Call CABO
- `AwaitingDrawnDecision`: drawn card + Discard / Replace / Use Skill if available
- `AwaitingReplaceSlots`: select own slots + confirm replace
- `AwaitingTakeSlots`: select own slots + confirm take
- `SkillPeekSlot`: select own slot
- `SkillSpyTarget`: select opponent
- `SkillSpySlot`: select opponent slot
- `SkillSwapMySlot`: select own slot
- `SkillSwapTargetPlayer`: select opponent
- `SkillSwapTargetSlot`: select opponent slot
- waiting states: show "Waiting for server..." and disable action input

Acceptance:

- no invalid action button is active for the current sub-state.
- waiting states cannot send duplicate requests.

### Step 5: Round Reveal / Game Over Panels

Replace text block reveal/game-over with UI panels:

- reveal each player's cards as face-up cards
- show round score, penalty, cumulative score
- highlight CABO caller, lowest hand, winner
- game over ranking panel with final scores

Acceptance:

- reveal/game-over are readable and not just terminal text.

Current implementation note:

- Final `GameOver` rankings now include `Return to Room`.
- Returning to room switches the Unity flow back to the existing waiting-room UI without sending LeaveRoom.
- Server-side final GameOver now returns the room to waiting, clears ready flags, keeps online players, migrates host if needed through existing disconnect handling, and lets the next host Start create a fresh full game instead of restarting the ended game.

### Step 6: Visual Feedback / Animation Hooks

Do lightweight visual feedback first:

- turn highlight
- selected card highlight
- card moved/discarded flash
- action status toast

Do not block core playability on complex animation.

Current implementation note:

- A first-pass UI Toolkit animation layer now exists in `GameTablePanel.cs`.
- It plays local draw-decision animation and public `ActionResultNotify` animations for Draw, DiscardDrawn, ReplaceWithDrawn, TakeFromDiscard, PeekSelf, Spy, Swap, and Call CABO.
- PeekSelf and Spy were upgraded to slower, slot-specific inspection animations: observers see which slot was inspected, while only the acting client can show the private peek value under the current protocol.
- Call CABO now creates a visible banner and leaves a persistent caller marker until the round reveal / next round transition.
- Details, limitations, and the replacement/upgrade path are recorded in `Docs/UNITY_ANIMATION_NOTES.md`.

## End-to-End Verification

Use the workflow in `Docs/UNITY_CLIENT_HANDOFF.md`.

Minimum verification for this milestone:

1. Start server on `127.0.0.1:8888`.
2. Start Unity MCP session.
3. Enter Play Mode.
4. Unity creates room as host.
5. Three bot clients join and ready.
6. Host ready/start.
7. Unity enters `CaboGameScene`.
8. Verify screenshot:
   - four player seats visible
   - local cards visible as card UI
   - draw/discard piles visible
   - current turn indicator visible
   - legal action controls visible only on local player's turn
9. Console has no game error/warning.

Useful status query should still return:

```text
scene=CaboGameScene; connected=True; flow=Playing; phase=Playing; players=4; cards=4
```

## Non-Goals For The First Game Scene Pass

Do not spend the first pass on:

- complex 3D table/camera
- elaborate card movement animation
- custom art generation
- replacing networking/state architecture
- rewriting protobuf or server logic
- redesigning lobby again

The goal is a polished-enough 2D UI Toolkit multiplayer card table that can play through actions.

## Reference Files

Primary Unity files:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/NetworkGateway.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/UI/GameScreen.uss`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scenes/CaboGameScene.unity`

Reference logic:

- `MuduoBaseGameServer/cli_client/src/ClientApp.cpp`
- `MuduoBaseGameServer/cli_client/src/GameState.cpp`
- `MuduoBaseGameServer/cli_client/src/UIRenderer.cpp`
- `Docs/NETWORK_LAYER.md`
- `Docs/GAME_SESSION.md`

Handoff / tooling:

- `Docs/UNITY_CLIENT_HANDOFF.md`
