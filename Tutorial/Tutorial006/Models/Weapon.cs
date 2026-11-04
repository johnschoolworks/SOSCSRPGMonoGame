namespace Tutorial006.Models;

public sealed class Weapon : GameItem
{
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }

    public Weapon(
        int id, string name, int price,
        int minimumDamage, int maximumDamage)
        : base(id, name, price)
    {
        if (minimumDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDamage));

        if (maximumDamage < minimumDamage)
            throw new ArgumentOutOfRangeException(nameof(maximumDamage));

        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
    }

    public override string Describe() =>
        $"{base.Describe()} | Damage {MinimumDamage}-{MaximumDamage}";
}
