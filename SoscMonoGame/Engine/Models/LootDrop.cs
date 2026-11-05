using System;

namespace SoscMonoGame.Engine;

/// <summary>Independent probability for a single drop, 0..100 percent.</summary>
public sealed record LootDrop
{
    public int ItemId { get; }
    public int ChancePercent { get; }
    public int MinimumQuantity { get; }
    public int MaximumQuantity { get; }

    public LootDrop(int itemId, int chancePercent, int minimumQuantity = 1, int maximumQuantity = 1)
    {
        if (chancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(chancePercent));
        if (minimumQuantity < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumQuantity));
        if (maximumQuantity < minimumQuantity)
            throw new ArgumentOutOfRangeException(nameof(maximumQuantity));

        ItemId = itemId;
        ChancePercent = chancePercent;
        MinimumQuantity = minimumQuantity;
        MaximumQuantity = maximumQuantity;
    }
}
