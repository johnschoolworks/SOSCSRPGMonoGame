namespace Tutorial006.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public Inventory Inventory { get; } = new();

    public Player(string name, int maximumHitPoints = 20)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name is required.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }

    public void GainRewards(int gold, int experience)
    {
        if (gold < 0)
            throw new ArgumentOutOfRangeException(nameof(gold));

        if (experience < 0)
            throw new ArgumentOutOfRangeException(nameof(experience));

        // Calculate both results before changing either property.
        int newGold = checked(Gold + gold);
        int newExperience = checked(ExperiencePoints + experience);

        Gold = newGold;
        ExperiencePoints = newExperience;
    }
}
