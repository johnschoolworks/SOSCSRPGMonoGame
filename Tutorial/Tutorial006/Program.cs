using Tutorial006.Factories;
using Tutorial006.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 006 ===");
Console.WriteLine("Factories, Item Catalogs, and Monster Loot");
Console.WriteLine();

Console.WriteLine("Available items:");
foreach (GameItem item in ItemFactory.GetAllItems())
{
    Console.WriteLine($"- [{item.Id}] {item.Describe()}");
}

var player = new Player("Adventurer");
var random = new Random(42);

Monster spider = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

Console.WriteLine();
Console.WriteLine($"A {spider.Name} appears!");
Console.WriteLine($"HP: {spider.HitPoints}/{spider.MaximumHitPoints}");

Console.WriteLine("The player attacks for 12 damage.");
spider.TakeDamage(12);

if (!spider.IsAlive)
{
    Console.WriteLine($"{spider.Name} defeated!");

    player.GainRewards(
        spider.RewardGold,
        spider.RewardExperience);

    Console.WriteLine(
        $"Rewards: {spider.RewardGold} Gold, " +
        $"{spider.RewardExperience} XP");

    foreach (var (item, quantity) in spider.Loot.Roll(random))
    {
        player.Inventory.Add(item, quantity);
        Console.WriteLine($"Loot: {item.Name} x{quantity}");
    }
}

Console.WriteLine();
Console.WriteLine("Player inventory:");
foreach (var (item, quantity) in player.Inventory.GetEntries())
{
    Console.WriteLine($"- {item.Name} x{quantity}");
}

Console.WriteLine($"Gold: {player.Gold}");
Console.WriteLine($"XP: {player.ExperiencePoints}");

Console.WriteLine();
Console.WriteLine("Creating two independent spiders:");

Monster spiderA = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

Monster spiderB = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

spiderA.TakeDamage(4);

Console.WriteLine($"Spider A HP: {spiderA.HitPoints}");
Console.WriteLine($"Spider B HP: {spiderB.HitPoints}");

RunChecks();

static void RunChecks()
{
    Console.WriteLine();
    Console.WriteLine("=== ENGINE CHECKS ===");

    GameItem silk1 = ItemFactory.GetItem(ItemFactory.SpiderSilkId);
    GameItem silk2 = ItemFactory.GetItem(ItemFactory.SpiderSilkId);

    Check(ReferenceEquals(silk1, silk2),
        "ItemFactory returns canonical item definitions");

    var first = MonsterFactory.CreateMonster(
        MonsterFactory.VenomousSpiderId);

    var second = MonsterFactory.CreateMonster(
        MonsterFactory.VenomousSpiderId);

    Check(!ReferenceEquals(first, second),
        "MonsterFactory creates independent instances");

    first.TakeDamage(3);

    Check(first.HitPoints == 7 && second.HitPoints == 10,
        "Monster HP is isolated");

    var guaranteed = new LootTable();
    guaranteed.Add(new LootEntry(
        ItemFactory.SpiderSilkId,
        dropChancePercent: 100,
        minimumQuantity: 2,
        maximumQuantity: 2));

    var drops = guaranteed.Roll(new Random(123));

    Check(drops.Count == 1 && drops[0].Quantity == 2,
        "100% loot always drops the fixed quantity");

    var impossible = new LootTable();
    impossible.Add(new LootEntry(
        ItemFactory.WolfFangId,
        dropChancePercent: 0));

    Check(impossible.Roll(new Random(123)).Count == 0,
        "0% loot never drops");

    var inventory = new Inventory();
    inventory.Add(silk1, 2);
    inventory.Add(silk2, 3);

    Check(inventory.Count(ItemFactory.SpiderSilkId) == 5,
        "Repeated loot stacks into one inventory entry");

    Check(!inventory.Remove(ItemFactory.SpiderSilkId, 6),
        "Cannot remove more than the owned quantity");

    Check(inventory.Count(ItemFactory.SpiderSilkId) == 5,
        "Failed removal preserves inventory state");

    Console.WriteLine("ALL ENGINE CHECKS PASSED");
}

static void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException($"FAIL: {description}");

    Console.WriteLine($"PASS: {description}");
}
