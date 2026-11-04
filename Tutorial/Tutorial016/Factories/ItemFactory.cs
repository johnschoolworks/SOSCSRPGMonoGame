using Tutorial016.Models;

namespace Tutorial016.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public const int HealingHerbId = 5;
    public const int RopeId = 6;
    public const int SteelSwordId = WeaponFactory.SteelSwordId;

    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
    public static GameItem HealingHerb { get; } = new(HealingHerbId, "Healing Herb");
    public static GameItem Rope { get; } = new(RopeId, "Rope");
    public static GameItem SteelSword { get; } = new(SteelSwordId, "Steel Sword");

    public static GameItem Get(int id) => id switch
    {
        SpiderSilkId => SpiderSilk,
        HealingHerbId => HealingHerb,
        RopeId => Rope,
        SteelSwordId => SteelSword,
        _ => throw new KeyNotFoundException($"Unknown item ID: {id}")
    };
}
