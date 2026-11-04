using Tutorial016.Factories;
using Tutorial016.Models;

namespace Tutorial016.Economy;

public sealed record TradeResult(bool Succeeded, string Message);

public static class TradingService
{
    public static TradeResult Buy(Player player, ShopDefinition shop, int itemId, int quantity)
    {
        if (quantity <= 0) return new(false, "Quantity must be positive.");
        ShopOffer? offer = shop.Offers.FirstOrDefault(o => o.ItemId == itemId);
        if (offer is null) return new(false, "Item is not sold here.");

        long total = (long)offer.BuyPrice * quantity;
        if (total > int.MaxValue || player.Gold < total)
            return new(false, "Not enough Gold or total price is too large.");
        if ((long)player.Inventory.Count(itemId) + quantity > int.MaxValue)
            return new(false, "Inventory quantity limit reached.");

        // Validate all expected failure conditions before mutation.
        if (!player.TrySpendGold((int)total))
            return new(false, "Not enough Gold.");

        player.Inventory.Add(ItemFactory.Get(itemId), quantity);
        return new(true, $"Bought {quantity} x {offer.Name} for {total} Gold.");
    }

    public static TradeResult Sell(Player player, ShopDefinition shop, int itemId, int quantity)
    {
        if (quantity <= 0) return new(false, "Quantity must be positive.");
        ShopOffer? offer = shop.Offers.FirstOrDefault(o => o.ItemId == itemId);
        if (offer is null) return new(false, "Merchant does not buy this item.");
        if (player.Inventory.Count(itemId) < quantity)
            return new(false, "Not enough items to sell.");

        long total = (long)offer.SellPrice * quantity;
        if (total > int.MaxValue || (long)player.Gold + total > int.MaxValue)
            return new(false, "Gold limit reached.");

        // Inventory and Gold updates are local synchronous operations.
        // A production engine should batch notifications and commit atomically.
        if (!player.Inventory.Remove(itemId, quantity))
            return new(false, "Not enough items to sell.");

        player.ReceiveRewards((int)total, 0);
        return new(true, $"Sold {quantity} x {offer.Name} for {total} Gold.");
    }
}
