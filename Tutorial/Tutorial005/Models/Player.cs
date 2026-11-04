namespace Tutorial005.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }

    public Inventory Inventory { get; } = new();
    public Weapon? EquippedWeapon { get; private set; }

    public bool IsAlive => HitPoints > 0;

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

        HitPoints = (int)Math.Max(0L, (long)HitPoints - amount);
    }

    public bool Equip(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        if (!Inventory.Contains(weapon.Id))
            return false;

        EquippedWeapon = weapon;
        return true;
    }

    public bool DrinkPotion(HealingPotion potion)
    {
        ArgumentNullException.ThrowIfNull(potion);

        if (!IsAlive || HitPoints == MaximumHitPoints)
            return false;

        if (!Inventory.Contains(potion.Id))
            return false;

        // A successful use consumes exactly one potion.
        // Since the preconditions are checked, removal must succeed.
        int healedHp = (int)Math.Min(
            (long)MaximumHitPoints,
            (long)HitPoints + potion.AmountToHeal);

        if (!Inventory.Remove(potion.Id))
            return false;

        HitPoints = healedHp;
        return true;
    }

    public bool DiscardItem(int itemId, int quantity = 1)
    {
        // Preserve at least one equipped weapon.
        if (EquippedWeapon is not null &&
            EquippedWeapon.Id == itemId &&
            Inventory.Count(itemId) - quantity < 1)
        {
            return false;
        }

        return Inventory.Remove(itemId, quantity);
    }
}
