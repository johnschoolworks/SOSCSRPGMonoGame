namespace Tutorial006.Models;

public sealed class LootEntry
{
    public int ItemId { get; }
    public int DropChancePercent { get; }
    public int MinimumQuantity { get; }
    public int MaximumQuantity { get; }

    public LootEntry(
        int itemId,
        int dropChancePercent,
        int minimumQuantity = 1,
        int maximumQuantity = 1)
    {
        if (itemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemId));

        if (dropChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(dropChancePercent));

        if (minimumQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumQuantity));

        if (maximumQuantity < minimumQuantity)
            throw new ArgumentOutOfRangeException(nameof(maximumQuantity));

        ItemId = itemId;
        DropChancePercent = dropChancePercent;
        MinimumQuantity = minimumQuantity;
        MaximumQuantity = maximumQuantity;
    }
}
