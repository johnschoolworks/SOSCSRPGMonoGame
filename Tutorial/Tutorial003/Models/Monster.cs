namespace Tutorial003.Models;

public sealed class Monster
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }

    public bool IsAlive => HitPoints > 0;

    public Monster(
        string name,
        int maximumHitPoints,
        int minimumDamage,
        int maximumDamage,
        int rewardGold,
        int rewardExperience)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Monster name cannot be empty.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        if (minimumDamage < 0 || maximumDamage < minimumDamage)
            throw new ArgumentOutOfRangeException(nameof(maximumDamage));

        if (rewardGold < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardGold));

        if (rewardExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardExperience));

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Max(0, HitPoints - amount);
    }
}
