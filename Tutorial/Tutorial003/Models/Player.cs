namespace Tutorial003.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public bool IsAlive => HitPoints > 0;

    public Player(string name, int maximumHitPoints, int startingGold)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Player name cannot be empty.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumHitPoints),
                "Maximum HP must be positive.");

        if (startingGold < 0)
            throw new ArgumentOutOfRangeException(
                nameof(startingGold),
                "Starting gold cannot be negative.");

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        Gold = startingGold;
        ExperiencePoints = 0;
    }

    // Constructor overload: convenient default values.
    public Player(string name) : this(name, 20, 10)
    {
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Max(0, HitPoints - amount);
    }

    public void Heal(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Min(MaximumHitPoints, HitPoints + amount);
    }

    public void EarnGold(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        Gold = checked(Gold + amount);
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (amount > Gold)
            return false;

        Gold -= amount;
        return true;
    }

    public void GainExperience(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        ExperiencePoints = checked(ExperiencePoints + amount);
    }
}
