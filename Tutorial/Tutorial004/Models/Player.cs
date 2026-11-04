namespace Tutorial004.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }

    public Player(string name, int maximumHitPoints = 20)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name cannot be empty.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Max(0, HitPoints - amount);
    }

    public bool Heal(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (HitPoints == 0 || HitPoints == MaximumHitPoints)
            return false;

        int previous = HitPoints;
        HitPoints = (int)Math.Min(
            (long)MaximumHitPoints,
            (long)HitPoints + amount);

        return HitPoints > previous;
    }
}
