namespace SoscMonoGame.Engine;

public abstract class Item
{
    public int Id { get; }
    public string Name { get; }
    protected Item(int id, string name) { Id = id; Name = name; }
}

public sealed class Weapon : Item
{
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }
    public Weapon(int id, string name, int minimumDamage, int maximumDamage) : base(id, name)
    {
        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
    }
}

public sealed class HealingPotion : Item
{
    public int HealingAmount { get; }
    public HealingPotion(int id, string name, int healingAmount) : base(id, name)
    {
        HealingAmount = healingAmount;
    }
}

public sealed class Loot : Item
{
    public Loot(int id, string name) : base(id, name) { }
}
