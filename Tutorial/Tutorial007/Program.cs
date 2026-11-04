using Tutorial007.Factories;
using Tutorial007.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 007 ===");
Console.WriteLine("Quests, Objectives, and Rewards");

var player = new Player("Adventurer");
var quest = QuestFactory.Create(QuestFactory.CollectSpiderSilkId);
var silk = ItemFactory.Get(ItemFactory.SpiderSilkId);

quest.Accept();
Console.WriteLine($"Quest: {quest.Title} [{quest.Status}]");

player.Inventory.Add(silk, 2);
Console.WriteLine($"Progress: {quest.Progress(player)}/{quest.RequiredQuantity}");
Console.WriteLine($"Turn in early: {quest.TurnIn(player)}");

player.Inventory.Add(silk);
Console.WriteLine($"Progress: {quest.Progress(player)}/{quest.RequiredQuantity}");
Console.WriteLine($"Turn in: {quest.TurnIn(player)}");
Console.WriteLine($"Quest status: {quest.Status}");
Console.WriteLine($"Gold: {player.Gold}; XP: {player.ExperiencePoints}");
Console.WriteLine($"Spider Silk remaining: {player.Inventory.Count(silk.Id)}");
Console.WriteLine($"Turn in again: {quest.TurnIn(player)}");

RunChecks();
Console.WriteLine("ALL QUEST CHECKS PASSED");

static void RunChecks()
{
    var silk = ItemFactory.Get(ItemFactory.SpiderSilkId);
    var hero = new Player("Tester");
    var quest = QuestFactory.Create(QuestFactory.CollectSpiderSilkId);

    Check(!quest.TurnIn(hero), "Cannot turn in before accepting");
    quest.Accept();
    Check(!quest.CanComplete(hero), "Quest needs items");
    hero.Inventory.Add(silk, 2);
    Check(quest.Progress(hero) == 2, "Partial progress");
    Check(!quest.TurnIn(hero), "Early turn-in fails");
    Check(hero.Inventory.Count(silk.Id) == 2, "Failed turn-in keeps items");

    hero.Inventory.Add(silk, 2);
    Check(quest.Progress(hero) == 3, "Progress capped at target");
    Check(quest.TurnIn(hero), "Turn-in succeeds");
    Check(quest.Status == QuestStatus.Completed, "Quest marked completed");
    Check(hero.Inventory.Count(silk.Id) == 1, "Only required items consumed");
    Check(hero.Gold == 10 && hero.ExperiencePoints == 20, "Rewards granted");
    Check(!quest.TurnIn(hero), "Second turn-in blocked");
    Check(hero.Gold == 10 && hero.ExperiencePoints == 20, "No duplicate rewards");

    var wealthy = new Player("Wealthy");
    wealthy.ReceiveRewards(int.MaxValue, 0);
    wealthy.Inventory.Add(silk, 3);
    var overflowQuest = QuestFactory.Create(QuestFactory.CollectSpiderSilkId);
    overflowQuest.Accept();
    Check(!overflowQuest.TurnIn(wealthy), "Overflow reward rejected");
    Check(wealthy.Inventory.Count(silk.Id) == 3, "Overflow keeps items");
    Check(overflowQuest.Status == QuestStatus.Active, "Overflow keeps quest active");
}

static void Check(bool result, string description)
{
    if (!result) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}
