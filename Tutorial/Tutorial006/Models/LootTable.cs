using Tutorial006.Factories;

namespace Tutorial006.Models;

public sealed class LootTable
{
    private readonly List<LootEntry> _entries = new();

    public void Add(LootEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Fail early if the item ID is not in the catalog.
        _ = ItemFactory.GetItem(entry.ItemId);

        _entries.Add(entry);
    }

    public IReadOnlyList<(GameItem Item, int Quantity)> Roll(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var drops = new List<(GameItem Item, int Quantity)>();

        foreach (LootEntry entry in _entries)
        {
            // Next(100) yields an integer from 0 through 99.
            int roll = random.Next(100);

            if (roll >= entry.DropChancePercent)
                continue;

            int quantity = entry.MinimumQuantity == entry.MaximumQuantity
                ? entry.MinimumQuantity
                : (int)random.NextInt64(
                    entry.MinimumQuantity,
                    (long)entry.MaximumQuantity + 1);

            GameItem item = ItemFactory.GetItem(entry.ItemId);
            drops.Add((item, quantity));
        }

        return drops.AsReadOnly();
    }
}
