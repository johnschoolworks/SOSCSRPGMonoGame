using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public sealed class GameSaveData
{
    public int Version { get; set; } = 1;
    public PlayerSaveData Player { get; set; } = new();
    public int LocationId { get; set; }
    public MonsterSaveData? Monster { get; set; }
    public List<int> CompletedQuestIds { get; set; } = new();
    public List<ItemStackSaveData> MerchantStock { get; set; } = new();
    public List<string> CombatLog { get; set; } = new();
}
public sealed class PlayerSaveData
{
    public string Name { get; set; } = "Adventurer";
    public int Level { get; set; }
    public int ExperiencePoints { get; set; }
    public int MaximumHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int Gold { get; set; }
    public int EquippedWeaponId { get; set; }
    public List<ItemStackSaveData> Inventory { get; set; } = new();
}
public sealed class ItemStackSaveData
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
}
public sealed class MonsterSaveData
{
    public int MonsterId { get; set; }
    public int CurrentHitPoints { get; set; }
}
