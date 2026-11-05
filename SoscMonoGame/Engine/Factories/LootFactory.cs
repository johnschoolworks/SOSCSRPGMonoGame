using System;
using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public static class LootFactory
{
    public static IReadOnlyList<(Item Item, int Quantity)> Roll(Monster monster, Random random)
    {
        var result = new List<(Item, int)>();
        foreach (var drop in monster.LootTable)
        {
            if (random.Next(100) >= drop.ChancePercent) continue;
            int quantity = random.Next(drop.MinimumQuantity, drop.MaximumQuantity + 1);
            result.Add((ItemFactory.Get(drop.ItemId), quantity));
        }
        return result;
    }
}
