using Tutorial020.Factories;
using Tutorial020.World;

namespace Tutorial020.Combat;

// Independent optional rolls: each entry has a chance in [0, 1].
// Guaranteed quest loot remains defined on Monster.LootItemId.
public sealed record LootEntry(int ItemId, double Chance, int Quantity = 1);

public static class LootTable
{
    public static IReadOnlyList<LootEntry> ForMonster(int monsterId) => monsterId switch
    {
        MonsterFactory.SpiderId => new[] { new LootEntry(ItemFactory.HealingHerbId, 0.25) },
        MonsterFactory.WolfId => new[] { new LootEntry(ItemFactory.HealingHerbId, 0.35) },
        _ => Array.Empty<LootEntry>()
    };

    public static IReadOnlyList<LootEntry> Roll(int monsterId, Random random) =>
        ForMonster(monsterId).Where(entry => random.NextDouble() < entry.Chance).ToArray();
}
