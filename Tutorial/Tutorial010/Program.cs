using Tutorial010.Factories;
using Tutorial010.Models;
using Tutorial010.Persistence;
using Tutorial010.World;

Console.WriteLine("=== SOSCSRPG Tutorial 010 ===");
Console.WriteLine("Locations, Navigation, and Encounters");

var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();
quest.Accept();
var world = new GameWorld();
world.LocationChanged += (_, e) =>
    Console.WriteLine($"[World] {e.Previous.Name} -> {e.Current.Name}");

PrintLocation(world);
Check(!world.TravelTo(WorldFactory.SpiderClearingId),
    "Cannot skip an unconnected location");
Check(world.TravelTo(WorldFactory.ForestTrailId), "Town -> Forest Trail");
PrintLocation(world);
Check(world.TravelTo(WorldFactory.SpiderClearingId), "Trail -> Spider Clearing");
PrintLocation(world);
Check(world.CurrentEncounter is not null, "Spider encounter generated");
Check(world.CurrentEncounter!.Name == "Venomous Spider", "Correct encounter");

var firstSpider = world.CurrentEncounter;
firstSpider.TakeDamage(4);
Check(firstSpider.HitPoints == 6, "Encounter takes damage");
Check(world.TravelTo(WorldFactory.ForestTrailId), "Return to trail");
Check(world.TravelTo(WorldFactory.SpiderClearingId), "Re-enter clearing");
Check(world.CurrentEncounter is not null && world.CurrentEncounter.HitPoints == 10,
    "New visit creates a fresh encounter");

player.Inventory.Add(ItemFactory.SpiderSilk, 3);
var saveService = new SaveGameService();
string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
saveService.Save(path, player, new[] { quest }, world);
Console.WriteLine($"Saved game to {path}");

var (loadedPlayer, loadedQuests, loadedWorld) = saveService.Load(path);
Check(loadedWorld.CurrentLocationId == WorldFactory.SpiderClearingId,
    "Location restored from JSON");
Check(loadedWorld.CurrentEncounter is null,
    "Transient encounter is not restored");
Check(loadedPlayer.Inventory.Count(ItemFactory.SpiderSilkId) == 3,
    "Inventory restored");
Check(loadedQuests.Single().Status == QuestStatus.Active,
    "Quest status restored");

Check(loadedWorld.TravelTo(WorldFactory.ForestTrailId),
    "Restored world can navigate");
Check(!loadedWorld.TravelTo(999), "Invalid destination rejected");

try
{
    saveService.Restore(new SaveData
    {
        PlayerName = "Invalid",
        CurrentLocationId = 999
    });
    throw new Exception("FAIL: invalid location accepted");
}
catch (InvalidDataException)
{
    Console.WriteLine("PASS: Invalid saved location rejected");
}

Console.WriteLine("ALL WORLD CHECKS PASSED");

static void PrintLocation(GameWorld world)
{
    Console.WriteLine($"\nLocation: {world.CurrentLocation.Name}");
    Console.WriteLine(world.CurrentLocation.Description);
    Console.WriteLine("Travel options: " +
        string.Join(", ", world.AvailableDestinations().Select(x => x.Name)));
    if (world.CurrentEncounter is Monster monster)
        Console.WriteLine($"Encounter: {monster.Name} ({monster.HitPoints} HP)");
}

static void Check(bool result, string description)
{
    if (!result) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}
