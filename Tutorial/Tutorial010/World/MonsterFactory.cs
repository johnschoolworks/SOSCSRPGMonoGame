namespace Tutorial010.World;

public sealed class Monster
{
    public int Id { get; }
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public bool IsAlive => HitPoints > 0;

    public Monster(int id, string name, int hitPoints)
    {
        Id = id;
        Name = name;
        if (hitPoints <= 0) throw new ArgumentOutOfRangeException(nameof(hitPoints));
        MaximumHitPoints = hitPoints;
        HitPoints = hitPoints;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }
}

public static class MonsterFactory
{
    public const int SpiderId = 1;
    public const int WolfId = 2;

    public static Monster Create(int id) => id switch
    {
        SpiderId => new Monster(id, "Venomous Spider", 10),
        WolfId => new Monster(id, "Forest Wolf", 18),
        _ => throw new KeyNotFoundException($"Unknown monster: {id}")
    };
}
