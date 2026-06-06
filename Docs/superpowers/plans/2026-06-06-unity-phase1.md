# Unity Phase 1 Implementation Plan — Connect → Room → Hand Layout

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire existing Unity infrastructure (TCP + protobuf + UI Toolkit) to complete Phase 1 flow: connect → room → ready → start → 4-player hand layout visible.

**Architecture:** Reuse existing `ProtoGateway` / `TcpNetworkClient` / `MessageDispatcher` / `GameTableUIToolkit`. Port CLI's `GameState` + drain-then-decide pattern to C#. Connect proto messages to UI Toolkit VisualElements.

**Tech Stack:** Unity 6 + C# + UI Toolkit (UXML/USS) + protobuf (existing generated C#)

---

## File Structure

| Existing (keep/modify) | Responsibility |
|------------------------|---------------|
| `Assets/Scripts/Network/TcpNetworkClient.cs` | TCP connect + async receive — keep as-is |
| `Assets/Scripts/Network/MessageDispatcher.cs` | ServerMessage oneof dispatch — keep as-is |
| `Assets/Scripts/Network/MessageCodec.cs` | Frame encode/decode — keep as-is |
| `Assets/Scripts/Network/RequestTracker.cs` | request_id timeout tracking — keep as-is |
| `Assets/Scripts/ClientCore/Network/ProtoGateway.cs` | Modify: wire to real flow, add error events |
| `Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs` | Modify: ProtoGateway as default mode |
| `Assets/Scripts/ClientCore/Game/GameTableUIToolkit.cs` | Modify: wire proto events to UI |
| `Assets/Scripts/ClientCore/Game/GameSceneBootstrap.cs` | Modify: init GameFlow on scene load |
| `Assets/Scripts/ClientCore/Room/RoomClientController.cs` | Keep: room flow controller |
| `Assets/Scripts/UI/LobbyRoomDemoUI.cs` | Keep: room UI (already working) |

| New | Responsibility |
|-----|---------------|
| `Assets/Scripts/ClientCore/Game/GameState.cs` | C# port of CLI `GameState` (fields + UpdateFromMessage) |
| `Assets/Scripts/ClientCore/Game/GameFlow.cs` | C# port of CLI `gameLoop` (drain-then-decide + state machine) |

| Remove/Disable | Reason |
|---------------|--------|
| `Assets/Scripts/ClientCore/Network/MockBackendGateway.cs` | Remove — ProtoGateway is real now |
| `Assets/Scripts/ClientCore/Network/ProtoGatewayPlaceholder.cs` | Remove — replaced by real ProtoGateway |
| All `Assets/Scripts/Game/` | Disable — old hot-seat code, not needed |
| All `Assets/Scripts/UI/` except `LobbyRoomDemoUI.cs` | Disable — old hot-seat UI |

---

### Task 1: Clean Up — Remove Mock/Placeholder/Hot-seat Code

**Files:**
- Delete: `Assets/Scripts/ClientCore/Network/MockBackendGateway.cs` (and .meta)
- Delete: `Assets/Scripts/ClientCore/Network/ProtoGatewayPlaceholder.cs` (and .meta)  
- Remove registration from RoomClientController

- [ ] **Step 1: Delete mock backend files**

Remove `MockBackendGateway.cs` and `ProtoGatewayPlaceholder.cs` from Unity project.
Also remove their `.meta` files.

- [ ] **Step 2: Update RoomClientController to remove Mock/Placeholder references**

In `RoomClientController.cs`, remove `BackendMode` enum entirely and the `Mock` / `ProtoPlaceholder` cases. Simplify to always use the real `ProtoGateway`.

```csharp
// RoomClientController.cs — simplified (remove enum, always use ProtoGateway)
public sealed class RoomClientController : MonoBehaviour
{
    private ProtoGateway gateway;
    
    public void Initialize(ProtoGateway gw)
    {
        this.gateway = gw;
        gw.ConnectionStatusChanged += OnConnectionChanged;
        gw.RoomUpdated += OnRoomUpdated;
        gw.RoomStarted += OnRoomStarted;
    }
    // ... rest unchanged
}
```

- [ ] **Step 3: Disable old hot-seat scripts**

Disable `CaboGameManager.cs`, `GameManager.cs`, `PlayerController.cs` (all in `Assets/Scripts/Game/` and `Assets/Scripts/UI/`). Add `#if false` block or remove from GameObject bindings.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: remove mock/placeholder/hot-seat, wire real ProtoGateway"
```

---

### Task 2: Create GameState — C# Port of CLI GameState

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/GameState.cs`

- [ ] **Step 1: Create GameState.cs with fields matching CLI GameState.h**

```csharp
using System.Collections.Generic;
using Game.Messages;
using Game.Room;
using Game.Game;
using Game.Common;
using Game.Sync;
using UnityEngine;

namespace Cabo.Client.Game
{
    public enum GamePhase { Lobby, WaitingRoom, Playing, RoundReveal, GameOver }

    public class CardState
    {
        public int SlotIndex;
        public bool IsKnown;
        public int Value;
    }

    public class PlayerInfo
    {
        public long PlayerId;
        public string Nickname;
        public int SeatId;
        public int TotalScore;
        public int CardCount;
        public bool IsReady;
        public bool IsHost;
    }

    public class RoundResult
    {
        public long PlayerId;
        public string Nickname;
        public List<int> CardValues = new();
        public int HandTotal;
        public int Penalty;
        public int RoundScore;
        public int CumulativeScore;
        public bool IsSteadyCaller;
        public bool IsLowest;
        public bool IsKamikaze;
    }

    public class FinalRank
    {
        public int Rank;
        public long PlayerId;
        public string Nickname;
        public int FinalScore;
        public bool IsWinner;
    }

    public class GameState
    {
        // Connection
        public long MyPlayerId;
        public long RoomId;
        public string RoomCode;

        // Phase
        public GamePhase Phase = GamePhase.Lobby;

        // Players
        public List<PlayerInfo> Players = new();

        // My hand
        public List<CardState> MyCards = new();

        // Piles
        public int DrawPileCount;
        public int DiscardPileCount;
        public int DiscardTopValue = -1;

        // Turn
        public long CurrentPlayerId;
        public int RoundNumber;
        public int TurnNumber;

        // Draw state
        public bool HasDrawnCard;
        public int DrawnCardValue;
        public int DrawnCardSkill;

        // Waiting flags
        public bool WaitingForDrawResponse;
        public bool WaitingForTakeResponse;
        public bool WaitingForCallSteadyResponse;
        public bool WaitingForSkillResponse;

        // Final round
        public bool IsFinalRound;
        public int FinalRoundRemaining;

        // Round reveal
        public bool RoundJustRevealed;
        public List<RoundResult> LastRoundResults = new();
        public List<FinalRank> FinalRankings = new();

        // Skill results
        public int LastPeekedValue = -1;
        public bool LastSwapOccurred;

        // Action broadcast
        public string LastActionMessage;

        public bool IsMyTurn => CurrentPlayerId == MyPlayerId;

        public int GetMyPlayerIndex()
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].PlayerId == MyPlayerId) return i;
            return -1;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/ClientCore/Game/GameState.cs
git commit -m "feat: add GameState — C# port of CLI GameState"
```

---

### Task 3: Create GameState.UpdateFromMessage — Message Handlers

**Files:**
- Modify: `Assets/Scripts/ClientCore/Game/GameState.cs`

- [ ] **Step 1: Add UpdateFromMessage with all handler cases**

Add this method to `GameState`:

```csharp
public void UpdateFromMessage(ServerMessage msg)
{
    switch (msg.PayloadCase)
    {
        case ServerMessage.PayloadOneofCase.CreateRoomRsp:
            var cr = msg.CreateRoomRsp;
            if (cr.Error?.Code == 0) {
                RoomId = cr.RoomId; MyPlayerId = cr.PlayerId;
                RoomCode = cr.RoomCode; Phase = GamePhase.WaitingRoom;
                Debug.Log($"[GameState] CreateRoomRsp: room={RoomCode} player={MyPlayerId}");
            }
            break;

        case ServerMessage.PayloadOneofCase.JoinRoomRsp:
            var jr = msg.JoinRoomRsp;
            if (jr.Error?.Code == 0) {
                RoomId = jr.RoomId; MyPlayerId = jr.PlayerId;
                Phase = GamePhase.WaitingRoom;
                Debug.Log($"[GameState] JoinRoomRsp: room={RoomId} player={MyPlayerId}");
            }
            break;

        case ServerMessage.PayloadOneofCase.RoomStateNotify:
            var rsn = msg.RoomStateNotify;
            var room = rsn.Room;
            if (room == null) break;
            RoomId = room.RoomId; RoomCode = room.RoomCode;
            Players.Clear();
            foreach (var pi in room.Players)
            {
                Players.Add(new PlayerInfo {
                    PlayerId = pi.PlayerId, Nickname = pi.Nickname,
                    SeatId = pi.SeatId, IsReady = pi.IsReady,
                    IsHost = pi.IsHost, TotalScore = pi.TotalScore
                });
            }
            break;

        case ServerMessage.PayloadOneofCase.PlayerJoinNotify:
            var pjn = msg.PlayerJoinNotify;
            var pj = pjn.Player;
            if (pj != null && !Players.Exists(p => p.PlayerId == pj.PlayerId))
            {
                Players.Add(new PlayerInfo {
                    PlayerId = pj.PlayerId, Nickname = pj.Nickname,
                    SeatId = pj.SeatId, IsReady = pj.IsReady, IsHost = pj.IsHost
                });
            }
            break;

        case ServerMessage.PayloadOneofCase.PlayerReadyNotify:
            var prn = msg.PlayerReadyNotify;
            var found = Players.Find(p => p.PlayerId == prn.PlayerId);
            if (found != null) found.IsReady = prn.IsReady;
            break;

        case ServerMessage.PayloadOneofCase.GameStartNotify:
            var gsn = msg.GameStartNotify;
            Phase = GamePhase.Playing;
            RoundNumber = gsn.RoundNumber;
            CurrentPlayerId = gsn.FirstPlayerId;
            IsFinalRound = false;
            FinalRoundRemaining = 0;

            if (gsn.YourView != null)
            {
                MyCards.Clear();
                foreach (var oc in gsn.YourView.OwnCards)
                    MyCards.Add(new CardState {
                        SlotIndex = oc.SlotIndex, IsKnown = oc.IsKnown,
                        Value = oc.IsKnown ? oc.Value : 0
                    });
                DrawPileCount = gsn.YourView.DrawPile?.Count ?? 0;
                DiscardPileCount = gsn.YourView.DiscardPile?.Count ?? 0;
                if (gsn.YourView.DiscardPile?.TopCard != null)
                    DiscardTopValue = gsn.YourView.DiscardPile.TopCard.Value;
                else DiscardTopValue = -1;

                foreach (var oh in gsn.YourView.OpponentHands)
                {
                    var p = Players.Find(x => x.PlayerId == oh.PlayerId);
                    if (p != null) p.CardCount = oh.CardCount;
                }
            }
            break;

        case ServerMessage.PayloadOneofCase.TurnStartNotify:
            var tsn = msg.TurnStartNotify;
            CurrentPlayerId = tsn.CurrentPlayerId;
            TurnNumber = tsn.TurnNumber;
            RoundNumber = tsn.RoundNumber;
            if (tsn.CurrentPlayerId == MyPlayerId)
            {
                HasDrawnCard = false; DrawnCardValue = 0; DrawnCardSkill = 0;
                WaitingForDrawResponse = false; WaitingForTakeResponse = false;
                WaitingForCallSteadyResponse = false; WaitingForSkillResponse = false;
            }
            if (tsn.Phase == GamePhaseType.GamePhaseFinalRound)
            { IsFinalRound = true; FinalRoundRemaining = tsn.FinalRoundRemaining; }
            DrawPileCount = tsn.DrawPile?.Count ?? 0;
            DiscardPileCount = tsn.DiscardPile?.Count ?? 0;
            if (tsn.DiscardPile?.TopCard != null)
                DiscardTopValue = tsn.DiscardPile.TopCard.Value;
            break;

        case ServerMessage.PayloadOneofCase.DrawCardRsp:
            var dr = msg.DrawCardRsp;
            WaitingForDrawResponse = false;
            if (dr.Error?.Code == 0) {
                HasDrawnCard = true; DrawnCardValue = dr.Value;
                DrawnCardSkill = (int)dr.Skill;
            }
            break;

        case ServerMessage.PayloadOneofCase.ActionResultNotify:
            var ar = msg.ActionResultNotify;
            DrawPileCount = ar.DrawPile?.Count ?? 0;
            DiscardPileCount = ar.DiscardPile?.Count ?? 0;
            if (ar.DiscardPile?.TopCard != null) DiscardTopValue = ar.DiscardPile.TopCard.Value;

            // Update opponent card counts
            foreach (var h in ar.PlayerHands)
            {
                var p = Players.Find(x => x.PlayerId == h.PlayerId);
                if (p != null) p.CardCount = h.CardCount;
            }

            // Swap: update own card state if affected
            if (ar.SwapOccurred)
            {
                if (ar.SourcePlayerId == MyPlayerId)
                {
                    int slot = ar.SourceSlot;
                    if (slot >= 0 && slot < MyCards.Count) MyCards[slot].IsKnown = false;
                }
                if (ar.TargetPlayerId == MyPlayerId)
                {
                    int slot = ar.TargetSlot;
                    if (slot >= 0 && slot < MyCards.Count) MyCards[slot].IsKnown = false;
                }
            }

            if (ar.TurnEnded) { CurrentPlayerId = ar.NextPlayerId; HasDrawnCard = false; }

            // Build action broadcast message
            LastActionMessage = BuildActionMessage(ar);
            break;

        case ServerMessage.PayloadOneofCase.RoundRevealNotify:
            var rrn = msg.RoundRevealNotify;
            Phase = GamePhase.RoundReveal;
            RoundJustRevealed = true;
            LastRoundResults.Clear();
            foreach (var sc in rrn.Scores)
            {
                var rr = new RoundResult {
                    PlayerId = sc.PlayerId, HandTotal = sc.HandTotal, Penalty = sc.Penalty,
                    RoundScore = sc.RoundScore, CumulativeScore = sc.CumulativeScore,
                    IsSteadyCaller = sc.IsSteadyCaller, IsLowest = sc.IsLowest,
                    IsKamikaze = sc.IsKamikaze
                };
                var pl = Players.Find(p => p.PlayerId == sc.PlayerId);
                if (pl != null) rr.Nickname = pl.Nickname;
                foreach (var rh in rrn.RevealedHands)
                {
                    if (rh.PlayerId == sc.PlayerId)
                    { rr.CardValues.AddRange(rh.CardValues); break; }
                }
                LastRoundResults.Add(rr);
            }
            // Update scores
            foreach (var sc in rrn.Scores)
            {
                var pl = Players.Find(p => p.PlayerId == sc.PlayerId);
                if (pl != null) pl.TotalScore = sc.CumulativeScore;
            }
            break;

        case ServerMessage.PayloadOneofCase.GameOverNotify:
            var go = msg.GameOverNotify;
            Phase = GamePhase.GameOver;
            FinalRankings.Clear();
            foreach (var r in go.Rankings)
                FinalRankings.Add(new FinalRank {
                    Rank = r.Rank, PlayerId = r.PlayerId, Nickname = r.Nickname,
                    FinalScore = r.FinalScore, IsWinner = r.IsWinner
                });
            break;
    }
}

private string BuildActionMessage(ActionResultNotify ar)
{
    string name = "Player";
    var pl = Players.Find(p => p.PlayerId == ar.SourcePlayerId);
    if (pl != null) name = pl.Nickname;
    string you = ar.SourcePlayerId == MyPlayerId ? " (You)" : "";

    switch (ar.ActionType)
    {
        case ActionType.ActionTypeDraw: return $">>> {name}{you} drew a card from the deck";
        case ActionType.ActionTypeDiscardDrawn:
            string skill = ar.SkillUsed switch {
                SkillType.SkillTypePeekSelf => " (Peek Self available)",
                SkillType.SkillTypeSpy => " (Spy available)",
                SkillType.SkillTypeSwap => " (Swap available)",
                _ => ""
            };
            return $">>> {name}{you} discarded the drawn card{skill}";
        case ActionType.ActionTypeReplaceWithDrawn:
            if (ar.ExchangeResult != null)
                return ar.ExchangeResult.Success
                    ? $">>> {name}{you} replaced {ar.ExchangeResult.DiscardedCount} card(s) with the drawn card"
                    : $">>> {name}{you} failed to replace — card added to hand";
            break;
        case ActionType.ActionTypeTakeFromDiscard:
            if (ar.ExchangeResult != null)
                return ar.ExchangeResult.Success
                    ? $">>> {name}{you} took from discard and replaced {ar.ExchangeResult.DiscardedCount} card(s)"
                    : $">>> {name}{you} took from discard but failed to replace";
            break;
        case ActionType.ActionTypeUseSkill:
            if (ar.SkillUsed == SkillType.SkillTypePeekSelf)
                return $">>> {name}{you} peeked at their own slot {ar.SourceSlot}";
            if (ar.SkillUsed == SkillType.SkillTypeSpy)
            {
                string tgt = "Player";
                var tp = Players.Find(p => p.PlayerId == ar.TargetPlayerId);
                if (tp != null) tgt = tp.Nickname;
                return $">>> {name}{you} spied on {tgt}'s slot {ar.TargetSlot}";
            }
            if (ar.SkillUsed == SkillType.SkillTypeSwap)
            {
                string tgt = "Player";
                var tp = Players.Find(p => p.PlayerId == ar.TargetPlayerId);
                if (tp != null) tgt = tp.Nickname;
                return $">>> {name}{you} swapped slot {ar.SourceSlot} with {tgt}'s slot {ar.TargetSlot}";
            }
            break;
        case ActionType.ActionTypeCallSteady:
            return $">>> {name}{you} called CABO!";
    }
    return "";
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/ClientCore/Game/GameState.cs
git commit -m "feat: add UpdateFromMessage — all proto handlers + action broadcast"
```

---

### Task 4: Wire ProtoGateway to GameState + GameTableUIToolkit

**Files:**
- Modify: `Assets/Scripts/ClientCore/Game/GameTableUIToolkit.cs`
- Modify: `Assets/Scripts/ClientCore/Game/GameSceneBootstrap.cs`

- [ ] **Step 1: Add GameState field and subscription to GameTableUIToolkit**

In `GameTableUIToolkit.cs`, add:

```csharp
private GameState gameState;
private ProtoGateway gateway;

public void Initialize(ProtoGateway gw)
{
    this.gateway = gw;
    this.gameState = new GameState();
    
    // Subscribe to all game notifications
    gateway.OnTurnStart += OnTurnStart;
    gateway.OnActionResult += OnActionResult;
    gateway.OnDrawResponse += OnDrawResponse;
    gateway.OnRoundReveal += OnRoundReveal;
    gateway.OnGameEnd += OnGameEnd;
}

void OnDestroy()
{
    if (gateway != null)
    {
        gateway.OnTurnStart -= OnTurnStart;
        gateway.OnActionResult -= OnActionResult;
        gateway.OnDrawResponse -= OnDrawResponse;
        gateway.OnRoundReveal -= OnRoundReveal;
        gateway.OnGameEnd -= OnGameEnd;
    }
}

// Handle GameStartNotify from PendingGameStart
public void HandleGameStart(GameStartNotify notify)
{
    var msg = new ServerMessage { GameStartNotify = notify };
    gameState.UpdateFromMessage(msg);
    RenderGameTable();
}

// Process a batch of pending turn notifications
public void ProcessPendingNotifications()
{
    // Drain any notifications received during scene load
    var pendingTurn = gateway.GetPendingTurnStart();
    if (pendingTurn != null)
    {
        var msg = new ServerMessage { TurnStartNotify = pendingTurn };
        gameState.UpdateFromMessage(msg);
    }
    RenderGameTable();
}

void OnTurnStart(TurnStartNotify notify)
{
    var msg = new ServerMessage { TurnStartNotify = notify };
    gameState.UpdateFromMessage(msg);
    RenderGameTable();
}

void OnActionResult(ActionResultNotify notify)
{
    var msg = new ServerMessage { ActionResultNotify = notify };
    gameState.UpdateFromMessage(msg);
    RenderGameTable();
}

void OnDrawResponse(DrawCardRsp rsp)
{
    var msg = new ServerMessage { DrawCardRsp = rsp };
    gameState.UpdateFromMessage(msg);
    RenderGameTable();
}

void OnRoundReveal(RoundRevealNotify notify)
{
    var msg = new ServerMessage { RoundRevealNotify = notify };
    gameState.UpdateFromMessage(msg);
    RenderGameTable();
}

void OnGameEnd(GameOverNotify notify)
{
    var msg = new ServerMessage { GameOverNotify = notify };
    gameState.UpdateFromMessage(msg);
    RenderGameTable();
}
```

- [ ] **Step 2: Update GameSceneBootstrap to initialize GameTableUIToolkit**

In `GameSceneBootstrap.cs`, after scene loads, initialize the table:

```csharp
public class GameSceneBootstrap : MonoBehaviour
{
    public static GameStartNotify PendingGameStart;
    
    void Start()
    {
        var table = FindObjectOfType<GameTableUIToolkit>();
        var gw = FindObjectOfType<ProtoGateway>();
        if (gw == null)
        {
            // ProtoGateway is on DontDestroyOnLoad bootstrap
            var bootstrap = FindObjectOfType<ClientAppBootstrap>();
            if (bootstrap != null)
            {
                var rc = bootstrap.GetComponent<RoomClientController>();
                // Access gateway through room controller
            }
        }
        
        if (table != null && gw != null)
        {
            table.Initialize(gw);
            
            if (PendingGameStart != null)
            {
                table.HandleGameStart(PendingGameStart);
                PendingGameStart = null;
            }
            table.ProcessPendingNotifications();
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: wire ProtoGateway → GameState → GameTableUIToolkit"
```

---

### Task 5: Update ClientAppBootstrap — Remove Mock, Use ProtoGateway Only

**Files:**
- Modify: `Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs`

- [ ] **Step 1: Simplify to always use ProtoGateway**

```csharp
public sealed class ClientAppBootstrap : MonoBehaviour
{
    [SerializeField] private string serverHost = "127.0.0.1";
    [SerializeField] private int serverPort = 8888;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // Create real gateway
        var gateway = new ProtoGateway(serverHost, serverPort, this);
        
        var roomController = GetComponent<RoomClientController>();
        if (roomController == null)
            roomController = gameObject.AddComponent<RoomClientController>();
        
        roomController.Initialize(gateway);

        // Add game controller for cross-scene forwarding
        if (GetComponent<GameClientController>() == null)
            gameObject.AddComponent<GameClientController>();

        // Add room UI
        if (GetComponent<LobbyRoomDemoUI>() == null)
            gameObject.AddComponent<LobbyRoomDemoUI>();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs
git commit -m "refactor: simplify ClientAppBootstrap to ProtoGateway-only mode"
```

---

### Task 6: Verify Phase 1 — 4-CLI + 1-Unity Test

- [ ] **Step 1: Build and run**

```bash
# Terminal 1: Server
cd MuduoBaseGameServer/build && ./GameServer 8888

# Terminals 2-4: 3 CLI clients
cd MuduoBaseGameServer/cli_client/build
./cabo_cli_client  # ×3

# Unity: Open project, hit Play in Editor
```

- [ ] **Step 2: Test flow**

1. CLI-1 creates room, gets room code
2. CLI-2 and CLI-3 join
3. Unity client joins room
4. All 4 players type `ready` (Unity via button, CLI via stdin)
5. Host clicks `start`
6. **Verify**: Unity shows 4-player layout with own cards (2 known, 2 unknown), opponent card counts, draw/discard pile, turn indicator

- [ ] **Step 3: Commit any fixes**

```bash
git add -A
git commit -m "fix: Phase 1 verification fixes"
```

---

## Verification Checklist

- [ ] Unity connects to server without errors
- [ ] Room creation and join work end-to-end
- [ ] Ready/Start flow works
- [ ] GameStartNotify populates GameState correctly
- [ ] UI Toolkit shows 4 player areas with correct layout
- [ ] Own cards display known/unknown correctly (first 2 visible, last 2 hidden)
- [ ] Opponent card counts display correctly
- [ ] Draw/Discard pile counts display
- [ ] Turn indicator highlights current player
- [ ] Console shows DEBUG log for each proto message received
