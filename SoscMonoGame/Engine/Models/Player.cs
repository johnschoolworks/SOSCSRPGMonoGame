using System;

namespace SoscMonoGame.Engine;

public class Player
{
    public string Name { get; set; } = "Adventurer";
    public int CurrentHitPoints { get; set; }
    public int MaximumHitPoints { get; set; }
    public int Gold { get; set; }
    public int Level { get; private set; } = 1;
    public int ExperiencePoints { get; private set; }
    public int ExperienceToNextLevel => Level * 25;
    public Inventory Inventory { get; } = new();
    public Weapon EquippedWeapon { get; private set; } = ItemCatalog.RustySword;

    public bool Equip(Weapon weapon)
    {
        if (Inventory.Count(weapon.Id) == 0) return false;
        EquippedWeapon = weapon;
        return true;
    }

    public Player(int maxHp, int gold)
    {
        MaximumHitPoints = maxHp;
        CurrentHitPoints = maxHp;
        Gold = gold;
    }

    /// <summary>Experience is cumulative; each level threshold is 25 * level.</summary>
    public int GainExperience(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ExperiencePoints += amount;
        int levels = 0;
        while (ExperiencePoints >= ExperienceToNextLevel)
        {
            Level++;
            levels++;
            MaximumHitPoints += 5;
            CurrentHitPoints = MaximumHitPoints;
        }
        return levels;
    }

    // Used only after save-file validation.
    internal void Restore(int level, int experience, int maxHp, int hp, int gold, string name)
    {
        Level = level;
        ExperiencePoints = experience;
        MaximumHitPoints = maxHp;
        CurrentHitPoints = hp;
        Gold = gold;
        Name = name;
    }

    public void TakeDamage(int damage) => CurrentHitPoints = Math.Max(0, CurrentHitPoints - Math.Max(0, damage));
    public void Heal(int amount) => CurrentHitPoints = Math.Min(MaximumHitPoints, CurrentHitPoints + Math.Max(0, amount));
}
