using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public class Monster
{
    public string Name { get; }
    public int CurrentHitPoints { get; set; }
    public int MaximumHitPoints { get; }
    public int MinDamage { get; }
    public int MaxDamage { get; }
    public int RewardExp { get; }
    public int RewardGold { get; }
    public IReadOnlyList<LootDrop> LootTable { get; }
    public bool IsDead => CurrentHitPoints <= 0;

    public Monster(string name, int maxHp, int minDamage, int maxDamage, int exp, int gold,
        IReadOnlyList<LootDrop>? lootTable = null)
    {
        Name = name;
        MaximumHitPoints = maxHp;
        CurrentHitPoints = maxHp;
        MinDamage = minDamage;
        MaxDamage = maxDamage;
        RewardExp = exp;
        RewardGold = gold;
        LootTable = lootTable ?? System.Array.Empty<LootDrop>();
    }
}
