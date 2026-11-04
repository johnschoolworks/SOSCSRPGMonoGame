using Tutorial011.Combat;
using Tutorial011.Factories;
using Tutorial011.Models;
using Tutorial011.Persistence;
using Tutorial011.World;

Console.WriteLine("=== SOSCSRPG Tutorial 011 ===");
Console.WriteLine("Turn-Based Combat and Encounter State");

var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();
quest.Accept();
var world = new GameWorld();
var combat = new CombatService(new Random(42));

Check(world.TravelTo(WorldFactory.ForestTrailId), "Travel to forest");
Check(world.TravelTo(WorldFactory.SpiderClearingId), "Enter spider clearing");
Check(!world.TravelTo(WorldFactory.ForestTrailId), "Cannot leave living encounter");

while (world.CurrentEncounter?.IsAlive == true && player.IsAlive)
    combat.Attack(player, world);

Check(player.IsAlive, "Player survived encounter");
Check(world.CurrentEncounter is { RewardsClaimed: true }, "Rewards claimed once");
Check(player.Gold == 5 && player.ExperiencePoints == 10, "Combat rewards awarded");
Check(player.Inventory.Count(ItemFactory.SpiderSilkId) == 1, "Spider Silk dropped");
Check(combat.Attack(player, world) == CombatOutcome.NoEncounter, "Cannot repeat rewards");

var save = new SaveGameService();
string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
save.Save(path, player, new[] { quest }, world);
var (loadedPlayer, loadedQuests, loadedWorld) = save.Load(path);
Check(loadedPlayer.HitPoints == player.HitPoints, "HP restored");
Check(loadedWorld.CurrentEncounter is { RewardsClaimed: true, HitPoints: 0 },
    "Defeated encounter state restored");
Check(combat.Attack(loadedPlayer, loadedWorld) == CombatOutcome.NoEncounter,
    "No duplicate rewards after load");
Check(loadedWorld.TravelTo(WorldFactory.ForestTrailId), "Can leave defeated encounter");
Check(loadedQuests.Single().Status == QuestStatus.Active, "Quest state restored");

// Save during an active encounter and verify that monster HP persists.
var midPlayer = new Player("MidBattle");
var midWorld = new GameWorld(WorldFactory.ForestTrailId);
Check(midWorld.TravelTo(WorldFactory.WolfDenId), "Enter wolf den");
midWorld.CurrentEncounter!.TakeDamage(4);
var data = save.Capture(midPlayer, Array.Empty<Quest>(), midWorld);
var (_, _, restoredWorld) = save.Restore(data);
Check(restoredWorld.CurrentEncounter is { HitPoints: 14, RewardsClaimed: false },
    "Active encounter HP restored");
Check(!restoredWorld.TravelTo(WorldFactory.ForestTrailId), "Combat lock survives load");
Console.WriteLine("ALL COMBAT CHECKS PASSED");

static void Check(bool result, string name)
{
    if (!result) throw new InvalidOperationException($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
}
