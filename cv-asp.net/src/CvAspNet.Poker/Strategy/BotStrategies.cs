using CvAspNet.Poker.Engine;

namespace CvAspNet.Poker.Strategy;

/// <summary>Decides a bot's move (Strategy pattern: the engine plays every bot the same way, whatever its logic).</summary>
public interface IBotStrategy
{
    PlayerAction Decide(TableView view, PlayerView player);
}

/// <summary>Folds one time in ten; otherwise checks when nothing is owed and calls when something is.</summary>
public sealed class RandomBotStrategy(Random random) : IBotStrategy
{
    private const int FoldOneIn = 10;

    public RandomBotStrategy()
        : this(Random.Shared)
    {
    }

    public PlayerAction Decide(TableView view, PlayerView player)
    {
        if (random.Next(FoldOneIn) == 0) return new PlayerAction.Fold(player.Id);
        return view.ToCall == 0 ? new PlayerAction.Check(player.Id) : new PlayerAction.Call(player.Id);
    }
}
