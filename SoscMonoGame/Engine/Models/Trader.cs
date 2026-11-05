using System;
using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public sealed class Trader
{
    public string Name { get; }
    public Inventory Stock { get; } = new();

    public Trader(string name) => Name = name;

    public int BuyPrice(Item item) => item.Id switch
    {
        1 => 12, // Rusty Sword
        2 => 45, // Iron Sword
        3 => 8,  // Minor Healing Potion
        4 => 3,  // Spider Silk
        _ => throw new ArgumentOutOfRangeException(nameof(item))
    };

    public int SellPrice(Item item) => Math.Max(1, BuyPrice(item) / 2);
}

public static class TraderFactory
{
    public static Trader CreateTownMerchant()
    {
        var trader = new Trader("Mara the Merchant");
        trader.Stock.Add(ItemCatalog.RustySword, 2);
        trader.Stock.Add(ItemCatalog.IronSword, 1);
        trader.Stock.Add(ItemCatalog.MinorPotion, 8);
        return trader;
    }
}
