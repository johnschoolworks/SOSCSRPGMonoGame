using Tutorial006.Models;

namespace Tutorial006.Factories;

public static class MonsterFactory
{
    public const int VenomousSpiderId = 1;
    public const int ForestWolfId = 2;

    public static Monster CreateMonster(int monsterId) =>
        monsterId switch
        {
            VenomousSpiderId => CreateSpider(),
            ForestWolfId => CreateWolf(),
            _ => throw new KeyNotFoundException(
                $"Unknown monster ID: {monsterId}")
        };

    private static Monster CreateSpider()
    {
        var loot = new LootTable();

        loot.Add(new LootEntry(
            ItemFactory.SpiderSilkId,
            dropChancePercent: 75,
            minimumQuantity: 1,
            maximumQuantity: 2));

        loot.Add(new LootEntry(
            ItemFactory.MinorHealingPotionId,
            dropChancePercent: 20));

        return new Monster(
            id: VenomousSpiderId,
            name: "Venomous Spider",
            maximumHitPoints: 10,
            rewardGold: 5,
            rewardExperience: 10,
            loot: loot);
    }

    private static Monster CreateWolf()
    {
        var loot = new LootTable();

        loot.Add(new LootEntry(
            ItemFactory.WolfFangId,
            dropChancePercent: 80));

        loot.Add(new LootEntry(
            ItemFactory.MinorHealingPotionId,
            dropChancePercent: 10));

        return new Monster(
            id: ForestWolfId,
            name: "Forest Wolf",
            maximumHitPoints: 18,
            rewardGold: 9,
            rewardExperience: 15,
            loot: loot);
    }
}
