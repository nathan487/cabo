using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central game state machine for Glucose Cabo (hot-seat mode).
/// Manages shuffle, deal, turns, skills, scoring, and game end.
/// </summary>
public class CaboGameManager : MonoBehaviour
{
    public static CaboGameManager Instance { get; private set; }

    [Header("Game Config")]
    public int playerCount = 2;
    public int scoreLimit = 100;
    public string[] playerNames = new string[] { "Player 1", "Player 2" };

    // Game state
    public Deck Deck { get; private set; }
    public List<Player> Players { get; private set; } = new List<Player>();
    public int CurrentPlayerIndex { get; private set; }
    public int RoundNumber { get; private set; }
    public bool GameOver { get; private set; }
    public Player Winner { get; private set; }

    // Steady (Cabo) state
    public bool SteadyCalled { get; private set; }
    public int SteadyCallerIndex { get; private set; }
    public bool FinalRoundActive { get; private set; }
    private int finalRoundTurnsRemaining;

    // Round result tracking (for UI display)
    public bool LastRoundHadKamikaze { get; private set; }
    public Player LastRoundKamikazePlayer { get; private set; }
    public List<Player> LastRoundHundredResets { get; private set; } = new List<Player>();

    // Drawn card state (current turn)
    public Card CurrentDrawnCard { get; private set; }
    public bool HasDrawn { get; private set; }
    public bool DrewFromDiscard { get; private set; }
    public TurnPhase CurrentPhase { get; set; }

    // Events for UI
    public UnityEvent<int> OnTurnStarted = new UnityEvent<int>();       // playerIndex
    public UnityEvent<int, Card> OnCardDrawn = new UnityEvent<int, Card>();    // playerIndex, card
    public UnityEvent<int, int, Card> OnCardReplaced = new UnityEvent<int, int, Card>(); // playerIndex, slot, newCard
    public UnityEvent<int> OnTurnEnded = new UnityEvent<int>();
    public UnityEvent OnSteadyCalled = new UnityEvent();
    public UnityEvent<int, int> OnRoundEnded = new UnityEvent<int, int>();    // roundNum, steadyCallerIndex
    public UnityEvent<Player> OnGameOver = new UnityEvent<Player>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // In case serialized fields were cleared, make sure runtime events are always usable.
        OnTurnStarted ??= new UnityEvent<int>();
        OnCardDrawn ??= new UnityEvent<int, Card>();
        OnCardReplaced ??= new UnityEvent<int, int, Card>();
        OnTurnEnded ??= new UnityEvent<int>();
        OnSteadyCalled ??= new UnityEvent();
        OnRoundEnded ??= new UnityEvent<int, int>();
        OnGameOver ??= new UnityEvent<Player>();
    }

    public void StartNewGame(int numPlayers)
    {
        playerCount = numPlayers;
        Players.Clear();
        for (int i = 0; i < playerCount; i++)
        {
            var name = i < playerNames.Length ? playerNames[i] : $"Player {i + 1}";
            Players.Add(new Player(name, i));
        }

        RoundNumber = 0;
        GameOver = false;
        Winner = null;
        StartNewRound();
    }

    public void StartNewRound()
    {
        RoundNumber++;
        Deck = new Deck();
        Deck.Initialize();

        // Reset all players for the new round (4 cards each)
        foreach (var p in Players)
        {
            p.ResetForNewRound(); // InitCards(4)
            for (int i = 0; i < 4; i++)
                p.Cards[i] = Deck.Draw();
        }

        // Flip one card to start the discard pile
        var firstDiscard = Deck.Draw();
        if (firstDiscard != null)
            Deck.Discard(firstDiscard);

        // Setup: each player peeks at their 2 leftmost cards (index 0, 1)
        // and initialize opponent knowledge tracking
        foreach (var p in Players)
        {
            p.MarkKnown(0, p.Cards[0].Value);
            p.MarkKnown(1, p.Cards[1].Value);
            p.InitOpponentKnowledge(Players.Count);
        }

        SteadyCalled = false;
        SteadyCallerIndex = -1;
        FinalRoundActive = false;
        CurrentDrawnCard = null;
        HasDrawn = false;
        CurrentPlayerIndex = 0;
        CurrentPhase = TurnPhase.WaitingForAction;
        LastRoundHadKamikaze = false;
        LastRoundKamikazePlayer = null;
        LastRoundHundredResets.Clear();

        OnTurnStarted?.Invoke(CurrentPlayerIndex);
    }

    // ── Player Actions ──

    /// <summary>
    /// Option A1: Draw from the draw pile (blind draw).
    /// </summary>
    public void ActionDrawFromDeck()
    {
        if (HasDrawn) return;
        var card = Deck.Draw();
        if (card == null) { Debug.Log("Deck empty!"); return; }

        CurrentDrawnCard = card;
        HasDrawn = true;
        DrewFromDiscard = false;
        CurrentPhase = TurnPhase.DecidingDrawnCard;
        OnCardDrawn?.Invoke(CurrentPlayerIndex, card);
    }

    /// <summary>
    /// Option B: Take from the discard pile.
    /// </summary>
    public void ActionTakeFromDiscard()
    {
        if (HasDrawn) return;
        var card = Deck.TakeFromDiscard();
        if (card == null) { Debug.Log("Discard empty!"); return; }

        CurrentDrawnCard = card;
        HasDrawn = true;
        DrewFromDiscard = true;
        CurrentPhase = TurnPhase.ChoosingReplaceSlot;
        OnCardDrawn?.Invoke(CurrentPlayerIndex, card);
    }

    /// <summary>
    /// Replace one of your face-down cards with the drawn card.
    /// </summary>
    public void ActionReplaceCard(int slotIndex)
    {
        var player = Players[CurrentPlayerIndex];
        if (!HasDrawn || slotIndex < 0 || slotIndex >= player.Cards.Count) return;

        var oldCard = player.ReplaceCard(slotIndex, CurrentDrawnCard);
        Deck.Discard(oldCard);

        // Other players who knew this slot now have stale knowledge
        ClearOthersKnowledgeOfSlot(CurrentPlayerIndex, slotIndex);

        OnCardReplaced?.Invoke(CurrentPlayerIndex, slotIndex, CurrentDrawnCard);
        EndTurn();
    }

    /// <summary>
    /// Replace multiple cards (must all have the same value) with the drawn card.
    /// Multi-card swap rule:
    /// - If all selected cards have the same value: swap succeeds, old cards go to discard.
    /// - If values differ: swap fails — drawn card is added to player area (increases count),
    ///   selected cards stay. If attempting 3+ cards, also draw 1 penalty card from deck.
    /// </summary>
    public bool ActionReplaceMultipleCards(List<int> slotIndices)
    {
        var player = Players[CurrentPlayerIndex];
        if (!HasDrawn || slotIndices == null || slotIndices.Count == 0) return false;
        foreach (var idx in slotIndices)
            if (idx < 0 || idx >= player.Cards.Count) return false;

        // Single card: always succeeds
        if (slotIndices.Count == 1)
        {
            ActionReplaceCard(slotIndices[0]);
            return true;
        }

        // Multi-card: check that all selected cards have the same value
        int firstValue = player.Cards[slotIndices[0]].Value;
        bool allSame = true;
        foreach (var idx in slotIndices)
        {
            if (player.Cards[idx].Value != firstValue)
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
        {
            // Success: swap all selected cards with the drawn card
            foreach (var idx in slotIndices)
            {
                var oldCard = player.ReplaceCard(idx, CurrentDrawnCard);
                Deck.Discard(oldCard);
                ClearOthersKnowledgeOfSlot(CurrentPlayerIndex, idx);
                OnCardReplaced?.Invoke(CurrentPlayerIndex, idx, CurrentDrawnCard);
            }
            EndTurn();
            return true;
        }
        else
        {
            // Failure: drawn card added to player area (increases card count)
            player.AddCard(CurrentDrawnCard);
            Debug.Log($"[Game] Multi-swap failed! {player.Name} keeps selected cards + drawn card ({CurrentDrawnCard.Value})");

            // 3+ card attempt: additional penalty — draw 1 from deck
            if (slotIndices.Count >= 3)
            {
                var penaltyCard = Deck.Draw();
                if (penaltyCard != null)
                {
                    player.AddCard(penaltyCard);
                    Debug.Log($"[Game] Extra penalty: {player.Name} draws +1 card ({penaltyCard.Value}) from deck");
                }
            }

            EndTurn();
            return false;
        }
    }

    /// <summary>
    /// Discard the drawn card without replacing.
    /// Per rules: if discarded from deck draw AND the card is 7-12,
    /// the skill is triggered as part of the discard action.
    /// Cards 0-6 and 13 are discarded directly with no skill.
    /// </summary>
    public void ActionDiscardDrawn()
    {
        if (!HasDrawn || DrewFromDiscard) return; // Can only discard if from draw pile

        // Per game rules: 7-12 skill cards trigger their ability when discarded from a deck draw.
        // 13 has no skill, 0-6 have no skill.
        if (CurrentDrawnCard != null && CurrentDrawnCard.IsSkillCard)
        {
            // Transition to skill phase — the skill card is still held, not yet in discard.
            // CompleteSkill() or DeclineSkillAndDiscard() will place it in the discard pile.
            CurrentPhase = TurnPhase.SkillActive;
        }
        else
        {
            // Non-skill card: discard immediately and end turn.
            Deck.Discard(CurrentDrawnCard);
            EndTurn();
        }
    }

    /// <summary>
    /// Called after a skill has been resolved. Discards the skill card and ends the turn.
    /// </summary>
    public void CompleteSkill()
    {
        Deck.Discard(CurrentDrawnCard);
        EndTurn();
    }

    /// <summary>
    /// Called when the player declines to use the skill on a 7-12 card
    /// they just discarded from a deck draw. The card still goes to discard,
    /// but no skill effect is applied.
    /// </summary>
    public void DeclineSkillAndDiscard()
    {
        Deck.Discard(CurrentDrawnCard);
        EndTurn();
    }

    /// <summary>
    /// Option C: Call "Steady!" (Cabo).
    /// After calling, all OTHER players get exactly one more turn each.
    /// The caller does NOT get another turn.
    /// </summary>
    public void ActionCallSteady()
    {
        if (FinalRoundActive) return; // Can't call steady during final round

        SteadyCalled = true;
        SteadyCallerIndex = CurrentPlayerIndex;
        FinalRoundActive = true;
        finalRoundTurnsRemaining = Players.Count - 1; // Everyone else gets one more turn

        OnSteadyCalled?.Invoke();

        // End the caller's turn WITHOUT decrementing finalRoundTurnsRemaining.
        // The decrement should only happen when OTHER players finish their turns.
        CurrentDrawnCard = null;
        HasDrawn = false;
        DrewFromDiscard = false;
        CurrentPhase = TurnPhase.WaitingForAction;

        // If no other players (solo), reveal immediately
        if (finalRoundTurnsRemaining <= 0)
        {
            RevealAndScore();
            return;
        }

        // Move to next player, skip the steady caller
        do
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        } while (CurrentPlayerIndex == SteadyCallerIndex);

        OnTurnStarted?.Invoke(CurrentPlayerIndex);
    }

    // ── Knowledge Tracking ──

    /// <summary>
    /// When a player replaces a card, other players who knew that slot
    /// must have their knowledge invalidated (the card is new to them).
    /// </summary>
    private void ClearOthersKnowledgeOfSlot(int playerIndex, int slotIndex)
    {
        foreach (var p in Players)
        {
            if (p.PlayerIndex == playerIndex) continue;
            if (p.OpponentKnown.ContainsKey(playerIndex)
                && slotIndex < p.OpponentKnown[playerIndex].Count)
            {
                p.OpponentKnown[playerIndex][slotIndex] = false;
                p.OpponentValues[playerIndex][slotIndex] = -1;
            }
        }
    }

    // ── Skills ──

    public void ExecutePeekSelf(int ownSlotIndex)
    {
        var player = Players[CurrentPlayerIndex];
        player.MarkKnown(ownSlotIndex, player.Cards[ownSlotIndex].Value);
        CompleteSkill();
    }

    public void ExecuteSpy(int targetPlayerIndex, int targetSlotIndex)
    {
        var target = Players[targetPlayerIndex];
        var value = target.Cards[targetSlotIndex].Value;
        // Record: the current player now knows this opponent card
        Players[CurrentPlayerIndex].LearnOpponentCard(targetPlayerIndex, targetSlotIndex, value);
        Debug.Log($"[Spy] {Players[CurrentPlayerIndex].Name} sees {target.Name}'s card[{targetSlotIndex}] = {value}");
        CompleteSkill();
    }

    public void ExecuteBlindSwap(int ownSlotIndex, int targetPlayerIndex, int targetSlotIndex)
    {
        var self = Players[CurrentPlayerIndex];
        var other = Players[targetPlayerIndex];
        int selfIdx = CurrentPlayerIndex;
        int otherIdx = targetPlayerIndex;

        // ── Capture ALL knowledge before the swap ──
        bool selfKnewOwn = self.CardKnown[ownSlotIndex];
        int selfOwnVal = self.KnownValues[ownSlotIndex];
        bool otherKnewOwn = other.CardKnown[targetSlotIndex];
        int otherOwnVal = other.KnownValues[targetSlotIndex];

        bool selfSpiedOther = self.OpponentKnown.ContainsKey(otherIdx)
            && targetSlotIndex < self.OpponentKnown[otherIdx].Count
            && self.OpponentKnown[otherIdx][targetSlotIndex];
        int selfSpiedOtherVal = selfSpiedOther
            ? self.OpponentValues[otherIdx][targetSlotIndex] : -1;

        bool otherSpiedSelf = other.OpponentKnown.ContainsKey(selfIdx)
            && ownSlotIndex < other.OpponentKnown[selfIdx].Count
            && other.OpponentKnown[selfIdx][ownSlotIndex];
        int otherSpiedSelfVal = otherSpiedSelf
            ? other.OpponentValues[selfIdx][ownSlotIndex] : -1;

        // ── Swap the actual cards ──
        var tempCard = self.Cards[ownSlotIndex];
        self.Cards[ownSlotIndex] = other.Cards[targetSlotIndex];
        other.Cards[targetSlotIndex] = tempCard;

        // ── Compute new own-card knowledge ──
        // A's slot now holds B's old card. A knows it if B knew it OR A spied it.
        self.CardKnown[ownSlotIndex] = otherKnewOwn || selfSpiedOther;
        self.KnownValues[ownSlotIndex] = otherKnewOwn ? otherOwnVal
            : (selfSpiedOther ? selfSpiedOtherVal : -1);

        // B's slot now holds A's old card. B knows it if A knew it OR B spied it.
        other.CardKnown[targetSlotIndex] = selfKnewOwn || otherSpiedSelf;
        other.KnownValues[targetSlotIndex] = selfKnewOwn ? selfOwnVal
            : (otherSpiedSelf ? otherSpiedSelfVal : -1);

        // ── Compute new opponent-card knowledge (self → other) ──
        // A now knows B's slot (which holds A's old card) if A peeked own card.
        if (self.OpponentKnown.ContainsKey(otherIdx)
            && targetSlotIndex < self.OpponentKnown[otherIdx].Count)
        {
            self.OpponentKnown[otherIdx][targetSlotIndex] = selfKnewOwn;
            self.OpponentValues[otherIdx][targetSlotIndex] = selfKnewOwn ? selfOwnVal : -1;
        }
        // B now knows A's slot (which holds B's old card) if B peeked own card.
        if (other.OpponentKnown.ContainsKey(selfIdx)
            && ownSlotIndex < other.OpponentKnown[selfIdx].Count)
        {
            other.OpponentKnown[selfIdx][ownSlotIndex] = otherKnewOwn;
            other.OpponentValues[selfIdx][ownSlotIndex] = otherKnewOwn ? otherOwnVal : -1;
        }

        // ── Third-party knowledge: swap positions for every other player ──
        foreach (var p in Players)
        {
            if (p.PlayerIndex == selfIdx || p.PlayerIndex == otherIdx) continue;

            bool knewSelfSlot = p.OpponentKnown.ContainsKey(selfIdx)
                && ownSlotIndex < p.OpponentKnown[selfIdx].Count
                && p.OpponentKnown[selfIdx][ownSlotIndex];
            int valSelfSlot = knewSelfSlot && p.OpponentValues.ContainsKey(selfIdx)
                && ownSlotIndex < p.OpponentValues[selfIdx].Count
                ? p.OpponentValues[selfIdx][ownSlotIndex] : -1;

            bool knewOtherSlot = p.OpponentKnown.ContainsKey(otherIdx)
                && targetSlotIndex < p.OpponentKnown[otherIdx].Count
                && p.OpponentKnown[otherIdx][targetSlotIndex];
            int valOtherSlot = knewOtherSlot && p.OpponentValues.ContainsKey(otherIdx)
                && targetSlotIndex < p.OpponentValues[otherIdx].Count
                ? p.OpponentValues[otherIdx][targetSlotIndex] : -1;

            if (p.OpponentKnown.ContainsKey(selfIdx)
                && ownSlotIndex < p.OpponentKnown[selfIdx].Count)
            {
                p.OpponentKnown[selfIdx][ownSlotIndex] = knewOtherSlot;
                p.OpponentValues[selfIdx][ownSlotIndex] = valOtherSlot;
            }
            if (p.OpponentKnown.ContainsKey(otherIdx)
                && targetSlotIndex < p.OpponentKnown[otherIdx].Count)
            {
                p.OpponentKnown[otherIdx][targetSlotIndex] = knewSelfSlot;
                p.OpponentValues[otherIdx][targetSlotIndex] = valSelfSlot;
            }
        }

        CompleteSkill();
    }

    // ── Turn Management ──

    private void EndTurn()
    {
        CurrentDrawnCard = null;
        HasDrawn = false;
        DrewFromDiscard = false;
        CurrentPhase = TurnPhase.WaitingForAction;

        if (FinalRoundActive)
        {
            finalRoundTurnsRemaining--;
            if (finalRoundTurnsRemaining <= 0)
            {
                RevealAndScore();
                return;
            }
        }

        // Move to next player, skip steady caller during final round
        do
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        } while (FinalRoundActive && CurrentPlayerIndex == SteadyCallerIndex);

        OnTurnStarted?.Invoke(CurrentPlayerIndex);
    }

    private void RevealAndScore()
    {
        // Reveal: all cards become known
        foreach (var p in Players)
            for (int i = 0; i < p.Cards.Count; i++)
                p.MarkKnown(i, p.Cards[i].Value);

        // Reset round tracking
        LastRoundHadKamikaze = false;
        LastRoundKamikazePlayer = null;
        LastRoundHundredResets.Clear();

        // ═══ Check Kamikaze (KAMIKAZE特攻队) first ═══
        // If any player has exactly two 12s and two 13s, they get 0, others get 50.
        Player kamikazePlayer = null;
        foreach (var p in Players)
        {
            if (p.HasKamikaze())
            {
                kamikazePlayer = p;
                break;
            }
        }

        if (kamikazePlayer != null)
        {
            LastRoundHadKamikaze = true;
            LastRoundKamikazePlayer = kamikazePlayer;
            Debug.Log($"[Game] KAMIKAZE特攻队！{kamikazePlayer.Name} has 12,12,13,13!");
            foreach (var p in Players)
            {
                if (p == kamikazePlayer)
                    p.TotalScore += 0; // Kamikaze player: 0 points
                else
                    p.TotalScore += 50; // Everyone else: 50 points
            }
        }
        else
        {
            // ═══ Normal scoring ═══
            // Determine if steady caller had the lowest (or tied lowest) score
            int minScore = int.MaxValue;
            foreach (var p in Players)
            {
                int s = p.GetRoundScore();
                if (s < minScore) minScore = s;
            }

            // Safety guard: SteadyCallerIndex must be valid
            if (SteadyCallerIndex >= 0 && SteadyCallerIndex < Players.Count)
            {
                int steadyScore = Players[SteadyCallerIndex].GetRoundScore();
                bool steadyWon = steadyScore <= minScore;

                // Apply scores
                foreach (var p in Players)
                {
                    if (p.PlayerIndex == SteadyCallerIndex)
                    {
                        if (steadyWon)
                            p.TotalScore += 0; // 0 points this round!
                        else
                            p.TotalScore += p.GetRoundScore() + 10; // penalty
                    }
                    else
                    {
                        p.TotalScore += p.GetRoundScore();
                    }
                }
            }
            else
            {
                // Fallback: no steady caller — everyone gets their round score
                Debug.LogWarning("[Game] RevealAndScore called with invalid SteadyCallerIndex. Using fallback scoring.");
                foreach (var p in Players)
                    p.TotalScore += p.GetRoundScore();
            }
        }

        OnRoundEnded?.Invoke(RoundNumber, SteadyCallerIndex);

        // ═══ 翻大浪 (Once per game, if exactly 100, reset to 50) ═══
        foreach (var p in Players)
        {
            if (p.TotalScore == 100 && !p.HasUsedReset)
            {
                p.TotalScore = 50;
                p.HasUsedReset = true;
                LastRoundHundredResets.Add(p);
                Debug.Log($"[Game] 翻大浪！{p.Name} hit exactly 100 → reset to 50 (used).");
            }
            else if (p.TotalScore == 100 && p.HasUsedReset)
            {
                Debug.Log($"[Game] {p.Name} hit exactly 100 but already used reset.");
            }
        }

        // ═══ Game over: score >= 100 ═══
        foreach (var p in Players)
        {
            if (p.TotalScore >= scoreLimit)
            {
                GameOver = true;
            }
        }

        if (GameOver)
        {
            // Find winner (lowest total score)
            // Tie-breaker: lowest score in the last round wins
            int bestScore = int.MaxValue;
            foreach (var p in Players)
            {
                if (p.TotalScore < bestScore)
                    bestScore = p.TotalScore;
            }

            // Collect all players tied for best score
            var tiedPlayers = new List<Player>();
            foreach (var p in Players)
            {
                if (p.TotalScore == bestScore)
                    tiedPlayers.Add(p);
            }

            if (tiedPlayers.Count == 1)
            {
                Winner = tiedPlayers[0];
            }
            else
            {
                // Tie-breaker: lowest last-round score wins
                int bestLastRound = int.MaxValue;
                foreach (var p in tiedPlayers)
                {
                    int lastScore = p.GetRoundScore();
                    if (lastScore < bestLastRound)
                    {
                        bestLastRound = lastScore;
                        Winner = p;
                    }
                }
                Debug.Log($"[Game] Tie-breaker: {Winner.Name} wins with last round score {bestLastRound}");
            }

            OnGameOver?.Invoke(Winner);
        }
        else
        {
            StartNewRound();
        }
    }
}

public enum TurnPhase
{
    WaitingForAction,    // Player chooses: draw, take discard, or call steady
    DecidingDrawnCard,   // Player has drawn from deck; choose: discard, replace, or skill
    ChoosingReplaceSlot, // Player took from discard; must choose a slot to replace
    SkillActive          // Skill is being resolved
}
