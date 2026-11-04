using Tutorial015.Models;

namespace Tutorial015.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public const int HealingHerbId = 5;
    public const int RopeId = 6;

    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
    public static GameItem HealingHerb { get; } = new(HealingHerbId, "Healing Herb");
    public static GameItem Rope { get; } = new(RopeId, "Rope");

    public static GameItem Get(int id) => id switch
    {
        SpiderSilkId => SpiderSilk,
        HealingHerbId => HealingHerb,
        RopeId => Rope,
        _ => throw new KeyNotFoundException($"Unknown item ID: {id}")
    };
}
