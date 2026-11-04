using Tutorial011.Models;

namespace Tutorial011.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
