namespace CvAspNet.Poker.Model;

public readonly record struct PlayerId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct TableId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>A player's state; immutable — the engine replaces it with an updated copy.</summary>
public sealed record Player(
    PlayerId Id,
    string Name,
    int Coins,
    Hand HoleCards,
    bool IsBot = true,
    bool Folded = false);
