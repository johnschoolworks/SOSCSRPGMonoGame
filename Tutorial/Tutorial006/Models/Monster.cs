namespace Tutorial006.Models;

public sealed class Monster
{
    public int Id { get; }
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public LootTable Loot { get; }

    public bool IsAlive => HitPoints > 0;

    public Monster(
        int id,
        string name,
        int maximumHitPoints,
        int rewardGold,
        int rewardExperience,
        LootTable loot)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Monster name is required.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        if (rewardGold < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardGold));

        if (rewardExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardExperience));

        ArgumentNullException.ThrowIfNull(loot);

        Id = id;
        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
        Loot = loot;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }
}
