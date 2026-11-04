using System;

namespace SoscMonoGame.Core;

public class Monster
{
    public string Name { get; }
    public int CurrentHitPoints { get; set; }
    public int MaximumHitPoints { get; }
    public int MinDamage { get; }
    public int MaxDamage { get; }
    public int RewardExp { get; }
    public int RewardGold { get; }
    public bool IsDead => CurrentHitPoints <= 0;

    public Monster(string name, int maxHp, int minDamage, int maxDamage, int exp, int gold)
    {
        Name = name;
        MaximumHitPoints = maxHp;
        CurrentHitPoints = maxHp;
        MinDamage = minDamage;
        MaxDamage = maxDamage;
        RewardExp = exp;
        RewardGold = gold;
    }
}
