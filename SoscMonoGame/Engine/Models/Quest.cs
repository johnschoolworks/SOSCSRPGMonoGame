namespace SoscMonoGame.Engine;

public sealed record Quest(int Id, string Name, string Description, int RequiredItemId,
    int RequiredQuantity, int RewardGold, int RewardExperience, Item RewardItem,
    int? PrerequisiteQuestId = null);

public sealed class QuestProgress
{
    public Quest Quest { get; }
    public bool IsCompleted { get; private set; }
    public QuestProgress(Quest quest) => Quest = quest;
    internal void RestoreCompleted(bool completed) => IsCompleted = completed;
    public bool CanComplete(Inventory inventory) =>
        !IsCompleted && inventory.Count(Quest.RequiredItemId) >= Quest.RequiredQuantity;
    public bool Complete(Inventory inventory)
    {
        if (!CanComplete(inventory)) return false;
        if (!inventory.Remove(Quest.RequiredItemId, Quest.RequiredQuantity)) return false;
        IsCompleted = true;
        return true;
    }
}
