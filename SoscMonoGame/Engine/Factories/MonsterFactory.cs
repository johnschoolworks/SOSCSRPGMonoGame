using System;

namespace SoscMonoGame.Engine;

public static class MonsterFactory
{
    public const int VenomousSpiderId = 1;
    public const int ForestWolfId = 2;

    public static Monster Create(int id) => id switch
    {
        VenomousSpiderId => new Monster("Venomous Spider", 10, 1, 3, 10, 5,
            new[] { new LootDrop(ItemCatalog.SpiderSilk.Id, 100),
                    new LootDrop(ItemCatalog.MinorPotion.Id, 15) }),
        ForestWolfId => new Monster("Forest Wolf", 14, 2, 4, 15, 8,
            new[] { new LootDrop(ItemCatalog.WolfFang.Id, 100),
                    new LootDrop(ItemCatalog.MinorPotion.Id, 20) }),
        _ => throw new ArgumentOutOfRangeException(nameof(id), $"Unknown monster ID {id}")
    };
}
