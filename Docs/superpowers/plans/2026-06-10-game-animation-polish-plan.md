# Game Animation Polish Plan

Date: 2026-06-10

## Goal

Improve the in-game animation experience for both the local player and opponents.

This task is about animation clarity and feel:

- whether every player action is shown in the right order;
- whether the visual source, target, and result are easy to understand;
- whether timing is readable without feeling slow;
- whether transitions are smooth and do not fight UI state changes;
- whether local-player feedback and opponent-action feedback feel consistent.

Do not change game rules, protobuf schemas, server room logic, scoring rules, or the high-level game state machine unless a bug proves that animation data is impossible to represent with the current client state.

## Primary Code Areas

Unity project:

- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/GameTablePanel.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameFlow.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/Core/GameState.cs`
- `unity dev/New Client_Unity_Base_Cli/Assets/Scripts/UI/UIManager.cs`

Current animation notes:

- `Docs/UNITY_ANIMATION_NOTES.md`

Network/action data reference:

- `Docs/NETWORK_LAYER.md`
- `Docs/GAME_SESSION.md`

## Current Animation Baseline

The current game table uses UI Toolkit elements and a lightweight overlay animation layer in `GameTablePanel`.

Important existing concepts:

- `_animationLayer` is an absolute overlay and should keep `PickingMode.Ignore`.
- `RenderGame()` detects new `GameState.LastActionSequence` values and builds action animation snapshots.
- Action animations are queued through `_animationQueueUntil` / `_lastAnimatedActionSequence`.
- `UIManager` delays `RoundReveal` while `GameTablePanel.HasPendingActionAnimation` is true.
- `GameTablePanel.Tick()` is used for skill inspection recovery.
- The client must preserve the drain-then-decide network behavior in `GameFlow.Tick()`.

## User Experience Requirements

The desired feel is a relaxed multiplayer card game, not a debug visualizer.

Animation should:

- make it clear who acted;
- make it clear which card slot was selected;
- make it clear whether the card came from deck, discard pile, self hand, or opponent hand;
- show the result before the turn visually moves on;
- avoid sudden layout jumps during movement;
- avoid overlapping card trails, stuck temporary cards, stale highlights, or double current-turn borders;
- keep the table readable for 2, 3, and 4 players;
- keep the right-side chat/log panel stable during animations.

## Scope

Review and improve all in-game action animations:

- Local player draw.
- Local player discard drawn card.
- Local player replace with drawn card.
- Local player take from discard.
- Local player Peek/Spy/Swap skills.
- Local player CABO call.
- Opponent draw.
- Opponent discard drawn card.
- Opponent replace with drawn card.
- Opponent take from discard.
- Opponent Peek/Spy/Swap skills.
- Opponent CABO call.
- Round reveal transition after the final action animation.

## Things To Audit First

Before editing, inspect these flows in `GameTablePanel.cs`:

- `RenderGame()`
- `BuildActionAnimationSnapshot(...)`
- `EnqueueActionAnimation(...)`
- `PlayActionAnimation(...)`
- `PlaySkillAnimation(...)`
- `PlayFlyCard(...)`
- `PulsePlayer(...)`
- `PulseCard(...)`
- `RenderSeats(...)`
- `RenderActionPanel(...)`
- `ReleaseTurnDisplay(...)`
- `Tick()`

Then inspect the state fields written in `GameState.HandleActionResult(...)`:

- `LastActionSequence`
- `LastActionType`
- `LastActionSkill`
- `LastActionSourcePlayerId`
- `LastActionTargetPlayerId`
- `LastActionSourceSlot`
- `LastActionTargetSlot`
- `LastActionSwapOccurred`
- `LastActionExchangeSucceeded`
- `LastActionIncomingCardValue`
- `LastActionDiscardedCount`
- `LastActionSelectedSlots`
- opponent hand-count data from action broadcasts

## Expected Implementation Direction

Prefer small, reviewable animation improvements instead of a large rewrite.

Good first steps:

1. Build an action-animation matrix documenting current behavior for each action and viewpoint.
2. Identify duplicated timing constants and normalize them into readable named values.
3. Ensure action snapshots capture pre-render bounds before authoritative state reflows the card layout.
4. Ensure every temporary card is cleaned up on completion, scene change, or re-render.
5. Ensure seat/card pulses re-render from current state after completion instead of restoring stale colors.
6. Ensure final action animations finish before round reveal and GameOver panels take over.
7. Add or improve debug-only animation probes if needed, but keep them out of normal player UI.

Avoid:

- changing protobuf schemas as a first resort;
- blocking network message draining while animations play;
- making animations so slow that normal play feels delayed;
- relying on text logs as the primary explanation;
- using screenshots alone to prove smoothness.

## Suggested Timing Targets

Use these as starting points, then tune by Play Mode observation:

- quick draw/deck movement: about `0.45s - 0.75s`;
- replace/take outgoing card movement: about `0.45s - 0.70s`;
- incoming card landing after empty-slot hold: about `0.45s - 0.70s`;
- empty selected-slot hold: about `0.35s - 0.60s`;
- skill inspect move out: about `0.55s - 0.85s`;
- skill inspect hold: about `1.20s - 2.00s`;
- skill inspect return: about `0.55s - 0.85s`;
- swap cross-move: about `0.70s - 1.10s`;
- CABO call banner/pulse: about `0.90s - 1.40s`.

Animations should feel readable at normal play speed. If an action has multiple phases, the total time should usually stay under about `2.5s`, except private skill inspection where the actor needs time to read a card.

## Unity MCP Verification Workflow

Use the `unity-mcp-orchestrator` skill.

MCP endpoint:

- `http://127.0.0.1:8080/mcp`

If MCP is stale or disconnected:

1. Read the current generated script:
   - `unity dev/New Client_Unity_Base_Cli/Library/MCPForUnity/TerminalScripts/mcp-terminal.cmd`
2. Start the MCP HTTP server from that script.
3. In Unity, open `Window > MCP For Unity` and ensure the HTTP URL is `http://127.0.0.1:8080`.
4. Start/connect the Unity MCP session.

After script edits:

1. Request `AssetDatabase.Refresh()`.
2. Request script compilation.
3. Poll/read editor state if available until compilation is complete.
4. Run `read_console` for `error` and `warning`.
5. Console target for task completion: `0 errors / 0 warnings`.

Known caveat:

- Some Unity scenes may report existing `The referenced script (Unknown) on this Behaviour is missing!` errors in certain editor states. If encountered, determine whether they are pre-existing scene references or caused by the current edit before claiming a clean final state.

## Synthetic Test Scenarios

Use Unity MCP `execute_code` to inject synthetic state when the live server is not needed.

Minimum synthetic states:

- 4-player active game with self at bottom and three opponents.
- Local player's turn with no drawn card.
- Local player's drawn-card decision state.
- Replace one slot.
- Replace multiple slots.
- Take from discard.
- Peek self.
- Spy opponent card.
- Swap own slot with opponent slot.
- Opponent draw/discard/replace/take.
- Opponent skill actions.
- CABO call.
- Action animation pending followed by `RoundReveal`.

For each scenario, check:

- no temporary card remains stuck on `_animationLayer`;
- selected slots are visibly blanked or highlighted when appropriate;
- acting seat and target seat pulses do not leave stale borders;
- turn indicator waits for the shown action when needed;
- table layout does not jump;
- right social panel does not resize;
- no action button appears during another player's animation unless it is actually the local player's valid decision.

## Screenshot / Visual Verification

Screenshots are useful but insufficient for motion. Use them at key timestamps:

- before action;
- during movement;
- during hold/inspection;
- after cleanup;
- after next turn render;
- after round reveal handoff.

Recommended screenshot names:

- `animation_draw_self_before.png`
- `animation_draw_self_mid.png`
- `animation_replace_multi_hold.png`
- `animation_swap_cross_mid.png`
- `animation_spy_inspect_hold.png`
- `animation_opponent_action_mid.png`
- `animation_round_reveal_after_queue.png`

Screenshots under `Assets/Screenshots/` are verification artifacts. Do not commit them unless the user explicitly asks.

## Live Verification

When the user has the server and bots ready, run at least one real game pass:

- connect Unity client through the current WebSocket URL;
- create or join a room;
- get 2-4 players into a match;
- perform local player actions;
- observe opponent actions from another client or bots;
- play until at least one round reveal;
- confirm the final action animation completes before settlement.

Server build/start remains user-owned unless the user explicitly asks the agent to run it.

## Acceptance Criteria

Implementation is acceptable when:

- local-player actions and opponent actions are both readable without relying on logs;
- animation order matches server action order;
- no action animation is skipped by immediate turn/reveal rendering;
- no temporary card, pulse, marker, or highlight remains stuck;
- no layout deformation occurs during action animations;
- 4-player table remains readable;
- Unity Console is clean or any remaining messages are documented as pre-existing unrelated issues;
- the user can validate in Play Mode or a Windows build without needing internal debug steps.

## Explicit Non-Goals

- Do not redesign the whole table layout.
- Do not change room chat/log sidebar layout.
- Do not change server game rules.
- Do not change protobuf schema unless a separate protocol plan is approved.
- Do not rework the WebSocket transport as part of animation polish.
- Do not commit screenshot artifacts.
