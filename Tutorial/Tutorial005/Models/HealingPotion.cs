namespace Tutorial005.Models;

public sealed class HealingPotion : GameItem
{
    public int AmountToHeal { get; }

    public HealingPotion(int id, string name, int price, int amountToHeal)
        : base(id, name, price)
    {
        if (amountToHeal <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountToHeal));

        AmountToHeal = amountToHeal;
    }

    public override string Describe() =>
        $"{base.Describe()} | Restores {AmountToHeal} HP";
}
