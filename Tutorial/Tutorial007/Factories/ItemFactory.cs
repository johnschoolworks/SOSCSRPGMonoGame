using Tutorial007.Models;

namespace Tutorial007.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public const int WolfFangId = 5;

    private static readonly Dictionary<int, GameItem> Items = new()
    {
        [SpiderSilkId] = new GameItem(SpiderSilkId, "Spider Silk"),
        [WolfFangId] = new GameItem(WolfFangId, "Wolf Fang")
    };

    public static GameItem Get(int id) =>
        Items.TryGetValue(id, out var item)
            ? item
            : throw new KeyNotFoundException($"Unknown item ID: {id}");
}
