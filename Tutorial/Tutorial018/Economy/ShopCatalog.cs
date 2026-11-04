using Tutorial018.Factories;
using Tutorial018.Models;
using Tutorial018.World;

namespace Tutorial018.Economy;

// Prices are game-design values, not real-world currency.
// Buy and sell prices differ to prevent risk-free trading loops.
public sealed record ShopOffer(int ItemId, string Name, int BuyPrice, int SellPrice);
public sealed record ShopDefinition(int Id, string Name, int LocationId,
    IReadOnlyList<ShopOffer> Offers);

public static class ShopCatalog
{
    public const int TownMerchantId = 1;
    public static ShopDefinition TownMerchant { get; } = new(
        TownMerchantId, "Mara's Trading Post", WorldFactory.TownSquareId,
        new[]
        {
            new ShopOffer(ItemFactory.SpiderSilkId, "Spider Silk", 6, 2),
            new ShopOffer(ItemFactory.HealingHerbId, "Healing Herb", 4, 1),
            new ShopOffer(ItemFactory.RopeId, "Rope", 8, 3),
            new ShopOffer(ItemFactory.SteelSwordId, "Steel Sword", 18, 7)
        });

    public static ShopDefinition? FindAtLocation(int shopId, int locationId) =>
        shopId == TownMerchant.Id && locationId == TownMerchant.LocationId
            ? TownMerchant : null;

    public static IReadOnlyList<ShopDefinition> AtLocation(int locationId) =>
        locationId == TownMerchant.LocationId
            ? new[] { TownMerchant } : Array.Empty<ShopDefinition>();
}
