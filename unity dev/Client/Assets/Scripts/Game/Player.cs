using System.Collections.Generic;

/// <summary>
/// Player state for a single round of Glucose Cabo.
/// </summary>
public class Player
{
    public string Name;
    public int PlayerIndex;

    // The face-down cards (variable count — starts at 4, can increase via penalty)
    public List<Card> Cards = new List<Card>();
    // Which cards this player has peeked at (seen)
    public List<bool> CardKnown = new List<bool>();

    // Cumulative score across rounds
    public int TotalScore;

    // Card memory: value is meaningful only if CardKnown[i] is true
    public List<int> KnownValues = new List<int>();

    // Once-per-game: has this player used the 100→50 reset?
    public bool HasUsedReset;

    // What this player knows about other players' cards (updated by Spy / BlindSwap)
    // opponentKnown[opponentIndex][slotIndex]
    public Dictionary<int, List<bool>> OpponentKnown = new Dictionary<int, List<bool>>();
    public Dictionary<int, List<int>> OpponentValues = new Dictionary<int, List<int>>();

    public Player(string name, int index)
    {
        Name = name;
        PlayerIndex = index;
    }

    /// <summary>
    /// Ensure the player has exactly `count` card slots (used on init).
    /// </summary>
    public void InitCards(int count)
    {
        Cards.Clear();
        CardKnown.Clear();
        KnownValues.Clear();
        for (int i = 0; i < count; i++)
        {
            Cards.Add(null);
            CardKnown.Add(false);
            KnownValues.Add(-1);
        }
    }

    /// <summary>
    /// Get the known value of a card, or -1 if unknown.
    /// </summary>
    public int GetKnownValue(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= CardKnown.Count) return -1;
        return CardKnown[slotIndex] ? KnownValues[slotIndex] : -1;
    }

    /// <summary>
    /// Mark a card as known (after peeking).
    /// </summary>
    public void MarkKnown(int slotIndex, int value)
    {
        if (slotIndex < 0 || slotIndex >= Cards.Count) return;
        CardKnown[slotIndex] = true;
        KnownValues[slotIndex] = value;
    }

    /// <summary>
    /// Replace a card in this player's hand, discarding the old one.
    /// Returns the replaced (old) card.
    /// </summary>
    public Card ReplaceCard(int slotIndex, Card newCard)
    {
        if (slotIndex < 0 || slotIndex >= Cards.Count) return null;
        var oldCard = Cards[slotIndex];
        Cards[slotIndex] = newCard;
        CardKnown[slotIndex] = true;
        KnownValues[slotIndex] = newCard.Value;
        return oldCard;
    }

    /// <summary>
    /// Add a card to the player's area (penalty — increases card count).
    /// </summary>
    public void AddCard(Card card)
    {
        Cards.Add(card);
        CardKnown.Add(true);
        KnownValues.Add(card.Value);
    }

    /// <summary>
    /// Calculate the current round score (sum of all cards).
    /// </summary>
    public int GetRoundScore()
    {
        int sum = 0;
        foreach (var c in Cards)
            if (c != null)
                sum += c.Points;
        return sum;
    }

    /// <summary>
    /// Check for Kamikaze: exactly two 12s and two 13s.
    /// </summary>
    public bool HasKamikaze()
    {
        if (Cards.Count != 4) return false;
        int count12 = 0, count13 = 0;
        foreach (var c in Cards)
        {
            if (c == null) return false;
            if (c.Value == 12) count12++;
            else if (c.Value == 13) count13++;
        }
        return count12 == 2 && count13 == 2;
    }

    /// <summary>
    /// Initialize knowledge tracking for all opponent players.
    /// </summary>
    public void InitOpponentKnowledge(int totalPlayers)
    {
        OpponentKnown.Clear();
        OpponentValues.Clear();
        for (int i = 0; i < totalPlayers; i++)
        {
            if (i == PlayerIndex) continue;
            OpponentKnown[i] = new List<bool> { false, false, false, false };
            OpponentValues[i] = new List<int> { -1, -1, -1, -1 };
        }
    }

    /// <summary>
    /// Record that this player now knows an opponent's card value.
    /// </summary>
    public void LearnOpponentCard(int opponentIndex, int slotIndex, int value)
    {
        if (!OpponentKnown.ContainsKey(opponentIndex)) return;
        if (slotIndex < 0 || slotIndex >= OpponentKnown[opponentIndex].Count) return;
        OpponentKnown[opponentIndex][slotIndex] = true;
        OpponentValues[opponentIndex][slotIndex] = value;
    }

    /// <summary>
    /// Swap knowledge about two opponent cards (used during BlindSwap when
    /// this player knew about the cards being swapped).
    /// </summary>
    public void SwapOpponentKnowledge(int opponentIndex, int slotA, int slotB)
    {
        if (!OpponentKnown.ContainsKey(opponentIndex)) return;
        var known = OpponentKnown[opponentIndex];
        var values = OpponentValues[opponentIndex];
        if (slotA < 0 || slotA >= known.Count || slotB < 0 || slotB >= known.Count) return;

        bool tempKnown = known[slotA];
        known[slotA] = known[slotB];
        known[slotB] = tempKnown;

        int tempVal = values[slotA];
        values[slotA] = values[slotB];
        values[slotB] = tempVal;
    }

    public void ResetForNewRound()
    {
        // Keep HasUsedReset across rounds (once per game)
        InitCards(4);
        OpponentKnown.Clear();
        OpponentValues.Clear();
    }
}
