/// <summary>
/// Represents a single card in the Glucose Cabo game.
/// </summary>
public class Card
{
    public int Value { get; private set; }   // 0–13, the glucose reading
    public int Points => Value;               // 0=0pts, 1-13=face value

    public bool IsSkillCard => Value >= 7 && Value <= 12;
    public SkillType Skill
    {
        get
        {
            return Value switch
            {
                7 or 8  => SkillType.PeekSelf,
                9 or 10 => SkillType.Spy,
                11 or 12 => SkillType.BlindSwap,
                _       => SkillType.None
            };
        }
    }

    public Card(int value)
    {
        Value = value;
    }

    public override string ToString() => $"Card({Value})";
}

public enum SkillType
{
    None,
    PeekSelf,    // 7-8: Look at one of your own face-down cards
    Spy,         // 9-10: Look at one opponent's face-down card
    BlindSwap    // 11-12: Swap one of yours with one of opponent's (blind)
}
