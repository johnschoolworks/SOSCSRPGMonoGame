using Tutorial003.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 003 ===");
Console.WriteLine("Constructors, Properties, and Encapsulation");
Console.WriteLine();

var player = new Player("Adventurer");
var spider = new Monster(
    name: "Venomous Spider",
    maximumHitPoints: 10,
    minimumDamage: 1,
    maximumDamage: 3,
    rewardGold: 5,
    rewardExperience: 10);

ShowStatus(player, spider);

Console.WriteLine("\nThe spider attacks for 3 damage.");
player.TakeDamage(3);
Console.WriteLine($"Player HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine("\nThe player drinks a healing potion (+5 HP).");
player.Heal(5);
Console.WriteLine($"Player HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine("\nThe player attacks the spider for 12 damage.");
spider.TakeDamage(12);
Console.WriteLine($"Spider HP: {spider.HitPoints}/{spider.MaximumHitPoints}");

if (!spider.IsAlive)
{
    Console.WriteLine("Spider defeated!");
    player.EarnGold(spider.RewardGold);
    player.GainExperience(spider.RewardExperience);
}

Console.WriteLine("\nThe player tries to spend 100 gold.");
bool purchaseSucceeded = player.SpendGold(100);
Console.WriteLine($"Purchase succeeded: {purchaseSucceeded}");

Console.WriteLine("\nFinal status:");
ShowStatus(player, spider);

static void ShowStatus(Player player, Monster monster)
{
    Console.WriteLine(
        $"{player.Name}: HP {player.HitPoints}/{player.MaximumHitPoints}, " +
        $"Gold {player.Gold}, XP {player.ExperiencePoints}");

    Console.WriteLine(
        $"{monster.Name}: HP {monster.HitPoints}/{monster.MaximumHitPoints}, " +
        $"Alive {monster.IsAlive}");
}
