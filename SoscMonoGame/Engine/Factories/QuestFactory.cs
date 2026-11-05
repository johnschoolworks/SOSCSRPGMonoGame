using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public static class QuestFactory
{
    public static Quest SpiderSilkCollection() => new(
        1, "Silk for the Village", "Bring 3 Spider Silk to the Town Square quest board.",
        ItemCatalog.SpiderSilk.Id, 3, 20, 25, ItemCatalog.IronSword);

    public static Quest WolfFangCollection() => new(
        2, "The Wolf Threat", "After the silk quest, bring 2 Wolf Fang from the Forest Clearing.",
        ItemCatalog.WolfFang.Id, 2, 30, 35, ItemCatalog.MinorPotion, PrerequisiteQuestId: 1);

    public static IReadOnlyList<Quest> CreateAll() => new[] { SpiderSilkCollection(), WolfFangCollection() };
}
