using Tutorial009.Factories;
using Tutorial009.Models;
using Tutorial009.Persistence;

Console.WriteLine("=== SOSCSRPG Tutorial 009 ===");
Console.WriteLine("JSON Save/Load and Game State Persistence");

var service = new SaveGameService();
var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();
quest.Accept();
player.Inventory.Add(ItemFactory.SpiderSilk, 5);
player.ReceiveRewards(7, 11);

string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
service.Save(path, player, new[] { quest });
Console.WriteLine($"Saved: {path}");
Console.WriteLine(File.ReadAllText(path));

var (loadedPlayer, loadedQuests) = service.Load(path);
var loadedQuest = loadedQuests.Single();
Console.WriteLine($"Loaded: {loadedPlayer.Name}, {loadedPlayer.Gold} Gold, {loadedPlayer.ExperiencePoints} XP");
Console.WriteLine($"Quest: {loadedQuest.Title} [{loadedQuest.Status}], progress {loadedQuest.Progress(loadedPlayer)}/3");

Check(loadedPlayer.Name == "Adventurer", "Player name restored");
Check(loadedPlayer.Gold == 7 && loadedPlayer.ExperiencePoints == 11, "Rewards restored");
Check(loadedPlayer.Inventory.Count(ItemFactory.SpiderSilkId) == 5, "Inventory restored");
Check(loadedQuest.Status == QuestStatus.Active, "Quest status restored");
Check(loadedQuest.TurnIn(loadedPlayer), "Restored quest can be completed");
Check(loadedPlayer.Inventory.Count(ItemFactory.SpiderSilkId) == 2, "Only required items consumed");
Check(loadedPlayer.Gold == 17 && loadedPlayer.ExperiencePoints == 31, "Quest rewards added");
Check(!loadedQuest.TurnIn(loadedPlayer), "Duplicate reward prevented");

var completed = service.Deserialize(service.Serialize(service.Capture(loadedPlayer, loadedQuests)));
var (again, againQuests) = service.Restore(completed);
Check(againQuests.Single().Status == QuestStatus.Completed, "Completed quest survives round-trip");
Check(again.Gold == 17, "Round-trip gold correct");

ExpectInvalid("Unknown item", () => service.Restore(new SaveData
{
    PlayerName = "Bad", Inventory = new() { new() { ItemId = 999, Quantity = 1 } }
}));
ExpectInvalid("Duplicate quest", () => service.Restore(new SaveData
{
    PlayerName = "Bad", Quests = new()
    {
        new() { QuestId = 1, Status = "Active" },
        new() { QuestId = 1, Status = "Completed" }
    }
}));
ExpectInvalid("Unsupported version", () => service.Restore(new SaveData
{
    Version = 99, PlayerName = "Bad"
}));
Console.WriteLine("ALL SAVE/LOAD CHECKS PASSED");

static void Check(bool ok, string description)
{
    if (!ok) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}

static void ExpectInvalid(string description, Action action)
{
    try { action(); }
    catch (InvalidDataException)
    {
        Console.WriteLine($"PASS: {description} rejected");
        return;
    }
    throw new InvalidOperationException($"FAIL: {description} accepted");
}
