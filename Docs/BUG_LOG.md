# Bug Log — CLI Client & Server

> 2026-06-05 to 2026-06-06

## Server Bugs

| # | Description | Fix |
|---|-------------|-----|
| 1 | `handleUseSkill` missing `room->step = Playing` before `endTurn` — skill use blocked next player's draw | Added step set |
| 2 | `handleUseSkill` no step guard — could use skill at any time | Added `!= WaitingDrawDecision` check |
| 3 | `handleCallSteady` no step guard — could call CABO after drawing | Added `!= Playing` check |
| 4 | `handleReplaceWithDrawn` broken `doMultiReplace` call with null shared_ptr | Removed dead code |
| 5 | Empty draw pile → silent return, game deadlocks | Call `revealAndScore` when deck empty |
| 6 | CABO caller lowest score not 0 — scored card sum instead | Fixed scoring: `if (isSteady && isLowest) roundSc = 0` |
| 7 | `swap_occurred` set to true even when swap bounds check failed | Reset `stype = NONE` on bounds failure |
| 8 | Discard pile starts with flipped card (not standard for first turn) | Removed initial flip; discard starts empty |
| 9 | Multi-replace duplicated card into all selected slots | Rebuild: discard all selected + add drawn at end |
| 10 | Multi-replace no slot bounds/duplicate validation | Added validation before processing |
| 11 | `handleDiscardDrawn` no current-player check for skill card discard | Added `isCurrentPlayer` guard |
| 12 | TurnStartNotify reaches client instantly after ActionResultNotify | Added 1.5s delay in `endTurn` for animation window |

## Client Bugs

| # | Description | Fix |
|---|-------------|-----|
| 1 | gameLoop used blocking `cin >>` — froze network during input | Refactored to non-blocking select() on stdin |
| 2 | Only 1 message processed per loop iteration | `drainMessages()` drains ALL pending before deciding |
| 3 | All action handlers used blocking I/O | Replaced with GameSubState state machine |
| 4 | `clientSeq_` incremented before send success | Moved increment after `sendRaw` success |
| 5 | `skillTypePending_` cleared too early in skill flow | Cleared after `sendSkillRequest` reads it |
| 6 | `WAITING_SKILL_RSP` transitioned to IDLE unconditionally | Added `waitingForSkillResponse` flag check |
| 7 | `renderActionMenu` showed main menu during skill input | Only shows "Waiting for..." when not my turn |
| 8 | CPU spin-loop when waiting for DrawCardRsp | Always `usleep(50000)` when idle |
| 9 | `usleep(50000)` race before draining pending messages | Replaced with `drainMessages()` |
| 10 | `isFinalRound` never reset between rounds — CABO option permanently hidden | Reset in GameStartNotify handler |
| 11 | Skill card discarded without use → turn never ends | Client sends empty UseSkillReq to end turn |
| 12 | Round reveal screen skipped by GameStartNotify | `roundJustRevealed` flag preserves reveal display |
| 13 | Card state not updated after useSkill (PeekSelf/Swap) | Updated myCards on UseSkillRsp + ActionResultNotify |
| 14 | Skill result not displayed prominently | 2-second display overlay with save/restore currentPlayerId |
| 15 | Inter-round ready/start without force-ready enforcement | RoomService resets isReady + broadcasts RoomStateNotify |
| 16 | Waiting room ready status not updating on individual changes | Track `lastReadyStates_` for per-player refresh |
| 17 | TakeFromDiscard popped from pile before validating slots | Moved validation before `pop_back` |
| 18 | Network buffer could fill without processing during blocking calls | `hasMessage` checks buffer first; `extractOneMessage` validates header |
