namespace SoscMonoGame.Engine;

/// <summary>Named IDs for existing gameplay code. Definitions live in ItemFactory.</summary>
public static class ItemCatalog
{
    public static Weapon RustySword => ItemFactory.Get<Weapon>(1);
    public static Weapon IronSword => ItemFactory.Get<Weapon>(2);
    public static HealingPotion MinorPotion => ItemFactory.Get<HealingPotion>(3);
    public static Loot SpiderSilk => ItemFactory.Get<Loot>(4);
    public static Loot WolfFang => ItemFactory.Get<Loot>(5);
}
