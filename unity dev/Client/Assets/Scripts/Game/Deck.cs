using System.Collections.Generic;

/// <summary>
/// Manages the draw pile and discard pile for Glucose Cabo.
/// Uses standard 52-card Cabo deck: 0(x2), 1-12(x4), 13(x2).
/// </summary>
public class Deck
{
    private List<Card> drawPile = new List<Card>();
    public List<Card> DiscardPile = new List<Card>();

    public int DrawCount => drawPile.Count;
    public Card TopDiscard => DiscardPile.Count > 0 ? DiscardPile[DiscardPile.Count - 1] : null;

    /// <summary>
    /// Create and shuffle a full 52-card Cabo deck.
    /// </summary>
    public void Initialize()
    {
        drawPile.Clear();
        DiscardPile.Clear();

        // 0 (×2)
        for (int i = 0; i < 2; i++) drawPile.Add(new Card(0));
        // 1–12 (×4 each)
        for (int v = 1; v <= 12; v++)
            for (int i = 0; i < 4; i++)
                drawPile.Add(new Card(v));
        // 13 (×2)
        for (int i = 0; i < 2; i++) drawPile.Add(new Card(13));

        Shuffle();
    }

    public void Shuffle()
    {
        var rng = new System.Random();
        int n = drawPile.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var temp = drawPile[k];
            drawPile[k] = drawPile[n];
            drawPile[n] = temp;
        }
    }

    /// <summary>
    /// Draw the top card from the draw pile. Returns null if empty.
    /// </summary>
    public Card Draw()
    {
        if (drawPile.Count == 0) return null;
        int last = drawPile.Count - 1;
        var card = drawPile[last];
        drawPile.RemoveAt(last);
        return card;
    }

    /// <summary>
    /// Take the top card from the discard pile. Returns null if empty.
    /// </summary>
    public Card TakeFromDiscard()
    {
        if (DiscardPile.Count == 0) return null;
        int last = DiscardPile.Count - 1;
        var card = DiscardPile[last];
        DiscardPile.RemoveAt(last);
        return card;
    }

    /// <summary>
    /// Add a card to the top of the discard pile.
    /// </summary>
    public void Discard(Card card)
    {
        DiscardPile.Add(card);
    }
}
