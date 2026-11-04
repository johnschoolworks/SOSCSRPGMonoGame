namespace Tutorial004.Models;

public sealed class Weapon : GameItem
{
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }

    public Weapon(
        int id,
        string name,
        int price,
        int minimumDamage,
        int maximumDamage)
        : base(id, name, price)
    {
        if (minimumDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDamage));

        if (maximumDamage < minimumDamage)
            throw new ArgumentOutOfRangeException(nameof(maximumDamage));

        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
    }

    public int RollDamage()
    {
        // Random.Shared.Next uses an exclusive upper bound.
        // The two-argument overload requires MaximumDamage < int.MaxValue.
        if (MaximumDamage == int.MaxValue)
            throw new InvalidOperationException(
                "MaximumDamage is too large for this tutorial's damage roller.");

        return Random.Shared.Next(MinimumDamage, MaximumDamage + 1);
    }

    public override string Describe()
    {
        return $"{base.Describe()} | Damage: {MinimumDamage}-{MaximumDamage}";
    }
}
