using Tutorial006.Models;

namespace Tutorial006.Factories;

public static class ItemFactory
{
    public const int RustySwordId = 1;
    public const int IronSwordId = 2;
    public const int MinorHealingPotionId = 3;
    public const int SpiderSilkId = 4;
    public const int WolfFangId = 5;

    private static readonly Dictionary<int, GameItem> Items = new()
    {
        [RustySwordId] = new Weapon(
            RustySwordId, "Rusty Sword", 12, 2, 5),

        [IronSwordId] = new Weapon(
            IronSwordId, "Iron Sword", 45, 5, 9),

        [MinorHealingPotionId] = new HealingPotion(
            MinorHealingPotionId, "Minor Healing Potion", 8, 5),

        [SpiderSilkId] = new QuestItem(
            SpiderSilkId, "Spider Silk", 2),

        [WolfFangId] = new QuestItem(
            WolfFangId, "Wolf Fang", 4)
    };

    public static GameItem GetItem(int itemId)
    {
        if (!Items.TryGetValue(itemId, out GameItem? item))
            throw new KeyNotFoundException($"Unknown item ID: {itemId}");

        return item;
    }

    public static T GetItem<T>(int itemId) where T : GameItem
    {
        GameItem item = GetItem(itemId);

        if (item is not T typedItem)
            throw new InvalidOperationException(
                $"Item {itemId} is not a {typeof(T).Name}.");

        return typedItem;
    }

    public static IReadOnlyList<GameItem> GetAllItems() =>
        Items.Values.OrderBy(item => item.Id).ToList().AsReadOnly();
}
