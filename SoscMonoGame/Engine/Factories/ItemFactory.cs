using System;
using System.Collections.Generic;

namespace SoscMonoGame.Engine;

/// <summary>Central registry of item definitions, keyed by stable IDs.</summary>
public static class ItemFactory
{
    private static readonly IReadOnlyDictionary<int, Item> Items = new Dictionary<int, Item>
    {
        [1] = new Weapon(1, "Rusty Sword", 2, 5),
        [2] = new Weapon(2, "Iron Sword", 4, 8),
        [3] = new HealingPotion(3, "Minor Healing Potion", 5),
        [4] = new Loot(4, "Spider Silk"),
        [5] = new Loot(5, "Wolf Fang")
    };

    public static Item Get(int id) => Items.TryGetValue(id, out var item)
        ? item : throw new ArgumentOutOfRangeException(nameof(id), $"Unknown item ID {id}");
    public static T Get<T>(int id) where T : Item => Get(id) as T
        ?? throw new InvalidOperationException($"Item {id} is not {typeof(T).Name}");
}
