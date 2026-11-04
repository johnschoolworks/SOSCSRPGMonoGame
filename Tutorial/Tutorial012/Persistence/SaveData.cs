namespace Tutorial012.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 3;
    public int HitPoints { get; set; } = 20;
    public int? EncounterMonsterId { get; set; }
    public int? EncounterHitPoints { get; set; }
    public bool EncounterRewardsClaimed { get; set; }
    public int CurrentLocationId { get; set; } = 1;
    public string PlayerName { get; set; } = "";
    public int Gold { get; set; }
    public int ExperiencePoints { get; set; }
    public List<InventoryEntryData> Inventory { get; set; } = new();
    public List<QuestEntryData> Quests { get; set; } = new();
}

public sealed class InventoryEntryData
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
}

public sealed class QuestEntryData
{
    public int QuestId { get; set; }
    public string Status { get; set; } = "";
}
