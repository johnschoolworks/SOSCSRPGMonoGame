using Tutorial004.Interfaces;

namespace Tutorial004.Models;

public sealed class HealingPotion : GameItem, IUsableItem
{
    public int AmountToHeal { get; }

    public HealingPotion(
        int id,
        string name,
        int price,
        int amountToHeal)
        : base(id, name, price)
    {
        if (amountToHeal <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountToHeal));

        AmountToHeal = amountToHeal;
    }

    public bool Use(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Heal(AmountToHeal);
    }

    public override string Describe()
    {
        return $"{base.Describe()} | Restores {AmountToHeal} HP";
    }
}
