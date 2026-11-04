namespace Tutorial007.Models;

public enum QuestStatus
{
    NotStarted,
    Active,
    Completed
}

public sealed class Quest
{
    public int Id { get; }
    public string Title { get; }
    public int RequiredItemId { get; }
    public int RequiredQuantity { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public QuestStatus Status { get; private set; } = QuestStatus.NotStarted;

    public Quest(int id, string title, int requiredItemId,
                 int requiredQuantity, int rewardGold, int rewardExperience)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (requiredItemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredItemId));
        if (requiredQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
        if (rewardGold < 0 || rewardExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardGold));

        Id = id;
        Title = title;
        RequiredItemId = requiredItemId;
        RequiredQuantity = requiredQuantity;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
    }

    public void Accept()
    {
        if (Status != QuestStatus.NotStarted)
            throw new InvalidOperationException("Quest already accepted or completed.");
        Status = QuestStatus.Active;
    }

    public int Progress(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Math.Min(RequiredQuantity, player.Inventory.Count(RequiredItemId));
    }

    public bool CanComplete(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Status == QuestStatus.Active &&
               player.Inventory.Count(RequiredItemId) >= RequiredQuantity;
    }

    public bool TurnIn(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!CanComplete(player)) return false;

        // Validate rewards before consuming the objective items.
        if (!player.CanReceiveRewards(RewardGold, RewardExperience))
            return false;

        if (!player.Inventory.Remove(RequiredItemId, RequiredQuantity))
            return false;

        player.ReceiveRewards(RewardGold, RewardExperience);
        Status = QuestStatus.Completed;
        return true;
    }
}
