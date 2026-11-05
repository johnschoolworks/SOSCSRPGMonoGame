using System.Linq;
using SoscMonoGame.Engine;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

var game = new GameSession(new Random(42));
Check(game.CurrentLocation.Id == 1, "Starts at Home");
Check(game.CurrentPlayer.Inventory.Count(ItemCatalog.MinorPotion.Id) == 3, "Initial potions");
Check(ItemFactory.Get<Weapon>(2).Name == "Iron Sword", "ItemFactory resolves weapons");
Check(MonsterFactory.Create(1).Name == "Venomous Spider", "MonsterFactory creates monsters");
Check(game.Move(Direction.North) && game.IsAtTrader, "Town Square movement");
Check(game.BuyItem(ItemCatalog.MinorPotion.Id), "Buy potion");
Check(game.CurrentPlayer.Gold == 2, "Gold deducted");
Check(!game.BuyItem(ItemCatalog.MinorPotion.Id), "Insufficient gold rejected");
Check(game.Move(Direction.North), "Enter forest");
Check(game.CurrentMonster is not null, "Forest encounter");
Check(!game.Move(Direction.South), "Cannot flee living monster");
while (game.CurrentMonster is not null && game.CurrentPlayer.CurrentHitPoints > 0)
    game.Attack();
Check(game.CurrentMonster is null, "Monster defeated");
Check(game.CurrentPlayer.Inventory.Count(ItemCatalog.SpiderSilk.Id) == 1, "Loot obtained");
Check(game.Move(Direction.South) && game.Move(Direction.South), "Return Home");
Check(game.CurrentPlayer.CurrentHitPoints == game.CurrentPlayer.MaximumHitPoints, "Rest heals");
var player = new Player(20, 0);
player.TakeDamage(8);
Check(Heal.Execute(player, ItemCatalog.MinorPotion) == 5, "Heal action restores HP");
Check(game.CurrentPlayer.ExperiencePoints >= 10, "Combat awards experience");
Check(game.ActiveQuest?.Quest.Id == 1, "First quest active");
Check(game.Quests.Count == 2, "Two quests defined");
Check(!game.Quests[1].IsCompleted, "Second quest initially locked");
Check(MonsterFactory.Create(MonsterFactory.ForestWolfId).Name == "Forest Wolf", "Second monster factory");
Check(game.World.Locations.Single(l => l.Id == 5).MonsterIds.Contains(MonsterFactory.ForestWolfId), "Clearing encounters wolf");
var guaranteed = LootFactory.Roll(MonsterFactory.Create(MonsterFactory.ForestWolfId), new Random(42));
Check(guaranteed.Any(e => e.Item.Id == ItemCatalog.WolfFang.Id), "Wolf fang guaranteed loot");
var progressPlayer = new Player(20, 0);
Check(progressPlayer.GainExperience(25) == 1 && progressPlayer.Level == 2, "Level up at 25 XP");
Check(progressPlayer.MaximumHitPoints == 25 && progressPlayer.CurrentHitPoints == 25, "Level up restores and grows HP");
Check(progressPlayer.GainExperience(24) == 0 && progressPlayer.Level == 2, "No premature level up");
Check(progressPlayer.GainExperience(1) == 1 && progressPlayer.Level == 3, "Cumulative level threshold");
var questGame = new GameSession(new Random(42));
questGame.Move(Direction.North);
questGame.CurrentPlayer.Inventory.Add(ItemCatalog.SpiderSilk, 3);
questGame.TurnInQuest();
Check(questGame.VillageQuest.IsCompleted, "First quest turn in");
Check(questGame.ActiveQuest?.Quest.Id == 2, "Second quest unlocked");
questGame.CurrentPlayer.Inventory.Add(ItemCatalog.WolfFang, 2);
questGame.TurnInQuest();
Check(questGame.Quests[1].IsCompleted && questGame.ActiveQuest is null, "Second quest completed");
Console.WriteLine("Engine checks completed.");

// Phase 7: deterministic snapshot roundtrip (including in-progress combat and merchant stock).
var savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sosc-phase7-" + Guid.NewGuid() + ".json");
try
{
    var snapshotGame = new GameSession(new Random(99));
    snapshotGame.Move(Direction.North);
    snapshotGame.BuyItem(ItemCatalog.MinorPotion.Id);
    snapshotGame.Move(Direction.North);
    snapshotGame.CurrentPlayer.TakeDamage(3);
    GameSaveService.Save(snapshotGame, savePath);
    var restored = GameSaveService.Load(savePath, new Random(99));
    Check(restored.CurrentLocation.Id == snapshotGame.CurrentLocation.Id, "Save/load location");
    Check(restored.CurrentMonster?.Name == snapshotGame.CurrentMonster?.Name, "Save/load active monster");
    Check(restored.CurrentPlayer.CurrentHitPoints == snapshotGame.CurrentPlayer.CurrentHitPoints, "Save/load player HP");
    Check(restored.CurrentPlayer.Inventory.Count(ItemCatalog.MinorPotion.Id) == 4, "Save/load inventory");
    Check(restored.TownMerchant.Stock.Count(ItemCatalog.MinorPotion.Id) == 7, "Save/load merchant stock");
    Check(!restored.Move(Direction.South), "Loaded game blocks movement during active combat");
    Check(restored.CurrentLocation.Id == 3, "Blocked movement preserves current location");
    System.IO.File.WriteAllText(savePath, "{not-json");
    bool rejected = false;
    try { GameSaveService.Load(savePath); }
    catch (System.Text.Json.JsonException) { rejected = true; }
    Check(rejected, "Corrupted save rejected");
}
finally { if (System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath); }

// Phase 8: edge cases, event delivery and save-file validation.
var edge = new GameSession(new Random(7));
int notifications = 0;
edge.StateChanged += (_, _) => notifications++;
Check(!edge.Move(Direction.West), "Invalid direction is rejected");
Check(notifications > 0, "StateChanged fires on rejected movement log");
Check(edge.CurrentLocation.Id == 1, "Rejected movement does not change location");
int initialPotions = edge.CurrentPlayer.Inventory.Count(ItemCatalog.MinorPotion.Id);
edge.UsePotion();
Check(edge.CurrentPlayer.Inventory.Count(ItemCatalog.MinorPotion.Id) == initialPotions,
    "Full-HP potion is not consumed");
edge.CurrentPlayer.TakeDamage(9);
edge.UsePotion();
Check(edge.CurrentPlayer.Inventory.Count(ItemCatalog.MinorPotion.Id) == initialPotions - 1,
    "Potion is consumed after healing");
Check(edge.CurrentPlayer.CurrentHitPoints == 16, "Potion restores exactly five HP");
Check(!edge.SellItem(ItemCatalog.RustySword.Id), "Cannot trade outside town");
edge.Move(Direction.North);
Check(!edge.SellItem(ItemCatalog.RustySword.Id), "Cannot sell only equipped weapon");
var auditSavePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sosc-phase8-" + Guid.NewGuid() + ".json");
try
{
    GameSaveService.Save(edge, auditSavePath);
    var originalJson = System.IO.File.ReadAllText(auditSavePath);
    var saved = GameSaveService.Load(auditSavePath);
    Check(saved.CurrentPlayer.CurrentHitPoints == edge.CurrentPlayer.CurrentHitPoints,
        "Save/load preserves damaged HP");
    Check(saved.CurrentPlayer.Inventory.Count(ItemCatalog.MinorPotion.Id) == initialPotions - 1,
        "Save/load preserves consumed potion count");
    void ExpectInvalid(string json, string title)
    {
        System.IO.File.WriteAllText(auditSavePath, json);
        bool invalid = false;
        try { GameSaveService.Load(auditSavePath); }
        catch (System.IO.InvalidDataException) { invalid = true; }
        Check(invalid, title);
    }
    ExpectInvalid(originalJson.Replace("\"Version\": 1", "\"Version\": 999"),
        "Unsupported save version rejected");
    ExpectInvalid(originalJson.Replace("\"Gold\": 10", "\"Gold\": -1"),
        "Negative gold rejected");
    ExpectInvalid(originalJson.Replace("\"LocationId\": 2", "\"LocationId\": 999"),
        "Unknown location rejected");
}
finally { if (System.IO.File.Exists(auditSavePath)) System.IO.File.Delete(auditSavePath); }
Console.WriteLine("Phase 8 audit checks completed.");
