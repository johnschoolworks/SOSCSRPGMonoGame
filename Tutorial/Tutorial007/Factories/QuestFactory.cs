using Tutorial007.Models;

namespace Tutorial007.Factories;

public static class QuestFactory
{
    public const int CollectSpiderSilkId = 1;

    public static Quest Create(int id) => id switch
    {
        CollectSpiderSilkId => new Quest(
            id: CollectSpiderSilkId,
            title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3,
            rewardGold: 10,
            rewardExperience: 20),
        _ => throw new KeyNotFoundException($"Unknown quest ID: {id}")
    };
}
