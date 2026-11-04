using System;

namespace SoscMonoGame.Core;

public class Player
{
    public string Name { get; set; } = "Adventurer";
    public int CurrentHitPoints { get; set; }
    public int MaximumHitPoints { get; set; }
    public int Gold { get; set; }
    public int Level { get; set; } = 1;
    public int ExperiencePoints { get; set; }

    public Player(int maxHp, int gold)
    {
        MaximumHitPoints = maxHp;
        CurrentHitPoints = maxHp;
        Gold = gold;
    }

    public void TakeDamage(int damage) => CurrentHitPoints = Math.Max(0, CurrentHitPoints - damage);
    public void Heal(int amount) => CurrentHitPoints = Math.Min(MaximumHitPoints, CurrentHitPoints + amount);
}
